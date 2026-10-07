using System;
using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core
{
    /// <summary>
    /// 戰鬥模擬核心（不依賴 Unity）。規則見 docs/combat.md。
    /// 使用方式：new Battle(setup) 後呼叫 PlayCard / EndTurn，並讀取 Events 做表現。
    /// </summary>
    public sealed class Battle
    {
        public const int MaxHandSize = 10;
        public const double LevelGrowthPerLevel = 0.1;

        private readonly Unit?[,] _board;
        private int _nextCardId;
        /// <summary>移動卡結算時的目的地（<see cref="PlayCard"/> 暫存給 ResolveEffect）。</summary>
        private Position? _moveDest;

        public BattleSetup Setup { get; }
        public Rng Rng { get; }
        public List<Unit> Units { get; } = new List<Unit>();
        public List<CardInstance> DrawPile { get; } = new List<CardInstance>();
        public List<CardInstance> Hand { get; } = new List<CardInstance>();
        public List<CardInstance> DiscardPile { get; } = new List<CardInstance>();
        public List<CardInstance> ExhaustPile { get; } = new List<CardInstance>();
        public List<BattleEvent> Events { get; } = new List<BattleEvent>();
        public int Cost { get; private set; }
        public int Turn { get; private set; }
        public BattleResult Result { get; private set; } = BattleResult.Ongoing;

        public Battle(BattleSetup setup)
        {
            Setup = setup;
            Rng = new Rng(setup.Seed);
            _board = new Unit?[setup.Lanes, setup.Rows];

            var allCards = new List<CardInstance>();
            var cardsByHero = new List<List<CardInstance>>();
            foreach (var slot in setup.Heroes)
            {
                var unit = CreateUnit(slot.Def.Name, Side.Player, slot.Def.AttackType, ScaleForLevel(slot.Def.Base, slot.Level), slot.Pos);
                unit.Hero = slot.Def;
                unit.Protected = slot.IsProtected;
                if (slot.StartHpPercent < 100) unit.Hp = Math.Max(1, unit.MaxHp * slot.StartHpPercent / 100);
                unit.DefId = slot.Def.Id;
                var heroCards = new List<CardInstance>();
                foreach (var cardDef in slot.Def.Deck)
                    heroCards.Add(new CardInstance(_nextCardId++, cardDef, unit));
                // 隊伍每有一名帶牌的武將，就在牌堆洗入一張 0 費移動卡。
                if (heroCards.Count > 0) heroCards.Add(new CardInstance(_nextCardId++, CardDef.CreateMove(), unit));
                allCards.AddRange(heroCards);
                cardsByHero.Add(heroCards);
            }
            foreach (var slot in setup.Enemies)
            {
                SpawnEnemy(slot.Def, slot.Pos);
            }

            if (setup.ScriptedDraw.Count > 0)
            {
                // 寫死牌序時各武將的牌輪流穿插（不洗牌，但也不會整手都是同一個人的牌）；移動卡排在起手牌之後（第 2 回合起抽到）。
                for (int i = 0; cardsByHero.Any(c => i < c.Count); i++)
                    foreach (var heroCards in cardsByHero)
                        if (i < heroCards.Count) DrawPile.Add(heroCards[i]);
                OrderByScript(DrawPile);
                var moves = DrawPile.Where(c => c.Def.Target == TargetRule.MoveDest).ToList();
                DrawPile.RemoveAll(c => c.Def.Target == TargetRule.MoveDest);
                DrawPile.InsertRange(Math.Min(setup.HandSize, DrawPile.Count), moves);
            }
            else
            {
                Shuffle(allCards);
                // 先登：開局必在起手牌，穩定地排到抽牌堆最前面。
                DrawPile.AddRange(allCards.Where(c => (c.Def.Keywords & CardKeywords.Innate) != 0));
                DrawPile.AddRange(allCards.Where(c => (c.Def.Keywords & CardKeywords.Innate) == 0));
            }

            StartPlayerTurn();
        }

        // ---------------------------------------------------------------- 查詢

        public Unit? UnitAt(Position pos)
        {
            if (!InBounds(pos)) return null;
            return _board[pos.Lane, pos.Row];
        }

        public Unit? UnitAt(Side side, Position pos)
        {
            var u = UnitAt(pos);
            return u != null && u.Side == side ? u : null;
        }

        /// <summary>依「欄 → 列」順序（左到右、上到下）列出該方存活單位。</summary>
        public List<Unit> AliveUnits(Side side)
        {
            return Units.Where(u => u.Side == side && u.Alive)
                        .OrderBy(u => u.Pos.Lane).ThenBy(u => u.Pos.Row).ToList();
        }

        public bool InBounds(Position pos)
        {
            return pos.Lane >= 0 && pos.Lane < Setup.Lanes && pos.Row >= 0 && pos.Row < Setup.Rows;
        }

        private static readonly int[] DLane = { 1, -1, 0, 0 };
        private static readonly int[] DRow = { 0, 0, 1, -1 };

        /// <summary>
        /// 單位在移動力內可到達的格子（含原地 = 0 步）及所需步數：正交移動、不可穿越任何單位，只能停在空格。
        /// </summary>
        public Dictionary<Position, int> ReachableTiles(Unit unit, int? steps = null)
        {
            int max = steps ?? unit.Stats.Move;
            var dist = new Dictionary<Position, int> { [unit.Pos] = 0 };
            var queue = new Queue<Position>();
            queue.Enqueue(unit.Pos);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                int d = dist[p];
                if (d >= max) continue;
                for (int i = 0; i < 4; i++)
                {
                    var n = new Position(p.Lane + DLane[i], p.Row + DRow[i]);
                    if (!InBounds(n) || dist.ContainsKey(n) || _board[n.Lane, n.Row] != null) continue;
                    dist[n] = d + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }

        /// <summary>檢查卡牌目前能否打出（不產生副作用）。</summary>
        public PlayResult CanPlay(CardInstance card)
        {
            if (Result != BattleResult.Ongoing) return PlayResult.BattleOver;
            if (!Hand.Contains(card)) return PlayResult.NotInHand;
            if (!card.Owner.Alive) return PlayResult.OwnerDead;
            if (card.Owner.Has(StatusType.Stun)) return PlayResult.Stunned;
            if (Cost < card.Def.Cost) return PlayResult.NotEnoughCost;
            if (card.Def.Target == TargetRule.MoveDest)
            {
                if (ReachableTiles(card.Owner).Count <= 1) return PlayResult.NoTarget;
            }
            else if (ResolveTargets(card.Owner, card.Def) == null)
                return PlayResult.NoTarget;
            return PlayResult.Ok;
        }

        /// <summary>
        /// 目標判定；回傳 null 表示沒有可選目標（卡牌不可打出）或指定的目標不在範圍內。
        /// 單體目標限卡牌射程內（曼哈頓格距）；<paramref name="chosen"/> 只有 <see cref="TargetRule.Enemy"/> 的牌會採用。
        /// </summary>
        public List<Unit>? ResolveTargets(Unit owner, CardDef def, Unit? chosen = null)
        {
            Side own = owner.Side;
            Side foe = own == Side.Player ? Side.Enemy : Side.Player;
            switch (def.Target)
            {
                case TargetRule.Self:
                    return new List<Unit> { owner };
                case TargetRule.AllAllies:
                    return NonEmpty(AliveUnits(own));
                case TargetRule.AllEnemies:
                    return NonEmpty(AliveUnits(foe));
                case TargetRule.AllyLowestHp:
                {
                    Unit? best = null;
                    foreach (var u in AliveUnits(own))
                    {
                        if (Position.Distance(owner.Pos, u.Pos) > def.Range) continue;
                        if (best == null || (long)u.Hp * best.MaxHp < (long)best.Hp * u.MaxHp) best = u;
                    }
                    return best == null ? null : new List<Unit> { best };
                }
                case TargetRule.Enemy:
                case TargetRule.EnemyLowestHp:
                {
                    var inRange = AliveUnits(foe).Where(u => Position.Distance(owner.Pos, u.Pos) <= def.Range).ToList();
                    if (inRange.Count == 0) return null;
                    Unit center;
                    if (def.Target == TargetRule.EnemyLowestHp)
                        center = inRange.OrderBy(u => u.Hp).ThenByDescending(u => u.Pos.Row).ThenBy(u => u.Pos.Lane).First();
                    else if (chosen != null)
                    {
                        if (!inRange.Contains(chosen)) return null;
                        center = chosen;
                    }
                    else
                        center = inRange.OrderBy(u => Position.Distance(owner.Pos, u.Pos)).ThenBy(u => u.Hp).ThenBy(u => u.Pos.Lane).First();
                    var result = new List<Unit>();
                    foreach (var cell in Targeting.ExpandShape(center.Pos, def.Shape, Setup.Lanes, Setup.Rows))
                    {
                        var u = UnitAt(foe, cell);
                        if (u != null && u.Alive) result.Add(u);
                    }
                    return result;
                }
                default:
                    return null;
            }
        }

        /// <summary>預覽敵方本回合會做什麼（意圖顯示用）。</summary>
        public Intent GetIntent(Unit enemy)
        {
            var intent = GetIntentCore(enemy);
            if (enemy.Ability.HasFlag(EnemyAbility.Charger) && enemy.Charging && intent.Type == Intent.Kind.Attack)
                intent.Big = true;
            return intent;
        }

        private Intent GetIntentCore(Unit enemy)
        {
            if (!enemy.Alive) return new Intent { Type = Intent.Kind.None };
            if (enemy.Has(StatusType.Stun)) return new Intent { Type = Intent.Kind.Stunned };

            if (enemy.Ability.HasFlag(EnemyAbility.Summoner) && enemy.SummonDef != null
                && (enemy.Actions + 1) % Math.Max(1, enemy.SummonEvery) == 0
                && !enemy.Charging
                && AliveUnits(Side.Enemy).Count < enemy.SummonCap)
            {
                // 召喚：填離召喚者最近的空格（同距離優先靠近我方的那一列）。
                var spawn = FindSpawnTile(enemy);
                if (spawn != null) return new Intent { Type = Intent.Kind.Summon, MoveTo = spawn };
            }

            if (enemy.Ability.HasFlag(EnemyAbility.Charger) && !enemy.Charging)
                return new Intent { Type = Intent.Kind.Charge };

            if (enemy.Ability.HasFlag(EnemyAbility.Healer))
            {
                // 治療者：有受傷的友軍就治療血量比例最低的那位，否則照常攻擊。
                Unit? hurt = null;
                foreach (var ally in AliveUnits(Side.Enemy))
                {
                    if (ally.Hp >= ally.MaxHp) continue;
                    if (hurt == null || (long)ally.Hp * hurt.MaxHp < (long)hurt.Hp * ally.MaxHp) hurt = ally;
                }
                if (hurt != null) return new Intent { Type = Intent.Kind.Heal, Target = hurt };
            }

            var heroes = AliveUnits(Side.Player);
            if (heroes.Count == 0) return new Intent { Type = Intent.Kind.None };
            Unit? forced = heroes.FirstOrDefault(u => u.Has(StatusType.Taunt));
            return PlanAttack(enemy, forced != null ? new List<Unit> { forced } : heroes);
        }

        private Position? FindSpawnTile(Unit summoner)
        {
            Position? best = null;
            for (int l = 0; l < Setup.Lanes; l++)
            {
                for (int r = 0; r < Setup.Rows; r++)
                {
                    if (_board[l, r] != null) continue;
                    var p = new Position(l, r);
                    if (best == null || IsBetterSpawn(summoner, p, best.Value)) best = p;
                }
            }
            return best;
        }

        private static bool IsBetterSpawn(Unit s, Position a, Position b)
        {
            int da = Position.Distance(s.Pos, a), db = Position.Distance(s.Pos, b);
            if (da != db) return da < db;
            if (a.Row != b.Row) return a.Row > b.Row;
            return a.Lane < b.Lane;
        }

        /// <summary>
        /// 敵方行動規劃：先看移動後（含原地）打得到的玩家單位——近戰挑最近者、遠程挑最後排者（同條件取血量最低），
        /// 以最少步數走到可攻擊的位置後出手；誰都打不到就朝最近的玩家單位靠近。
        /// </summary>
        private Intent PlanAttack(Unit enemy, List<Unit> candidates)
        {
            int range = enemy.AttackRange;
            bool ranged = enemy.AttackType == AttackType.Ranged;
            var reach = ReachableTiles(enemy);

            Unit? best = null;
            Position bestTile = enemy.Pos;
            foreach (var h in OrderTargets(enemy, candidates, ranged))
            {
                Position? tile = null;
                int steps = int.MaxValue;
                foreach (var kv in reach)
                {
                    if (Position.Distance(kv.Key, h.Pos) > range) continue;
                    if (kv.Value < steps || (kv.Value == steps && TileBefore(kv.Key, tile!.Value)))
                    {
                        tile = kv.Key;
                        steps = kv.Value;
                    }
                }
                if (tile == null) continue;
                best = h;
                bestTile = tile.Value;
                break;
            }
            if (best != null)
                return new Intent { Type = Intent.Kind.Attack, Target = best, MoveTo = bestTile == enemy.Pos ? (Position?)null : bestTile };

            // 誰都打不到：朝最近的玩家單位靠近。
            var nearest = candidates.OrderBy(h => Position.Distance(enemy.Pos, h.Pos)).ThenBy(h => h.Hp).First();
            Position dest = enemy.Pos;
            int destDist = Position.Distance(enemy.Pos, nearest.Pos);
            int destSteps = 0;
            foreach (var kv in reach)
            {
                int d = Position.Distance(kv.Key, nearest.Pos);
                if (d < destDist || (d == destDist && kv.Value < destSteps))
                {
                    dest = kv.Key;
                    destDist = d;
                    destSteps = kv.Value;
                }
            }
            if (dest == enemy.Pos) return new Intent { Type = Intent.Kind.None };
            return new Intent { Type = Intent.Kind.Move, MoveTo = dest };
        }

        private static IEnumerable<Unit> OrderTargets(Unit enemy, List<Unit> candidates, bool ranged)
        {
            if (ranged)
                return candidates.OrderByDescending(h => h.Pos.Row).ThenBy(h => h.Hp).ThenBy(h => h.Pos.Lane);
            return candidates.OrderBy(h => Position.Distance(enemy.Pos, h.Pos)).ThenBy(h => h.Hp).ThenBy(h => h.Pos.Lane);
        }

        /// <summary>同步數時的固定偏好：較靠下（近我方）的列優先，再取較左的欄。</summary>
        private static bool TileBefore(Position a, Position b) => a.Row != b.Row ? a.Row > b.Row : a.Lane < b.Lane;

        // ---------------------------------------------------------------- 玩家行動

        /// <param name="target">指定的敵方目標（只有 <see cref="TargetRule.Enemy"/> 的牌會採用）；沒給就自動挑範圍內最近者。</param>
        /// <param name="dest">移動卡的目的地格子。</param>
        public PlayResult PlayCard(CardInstance card, Unit? target = null, Position? dest = null)
        {
            var check = CanPlay(card);
            if (check != PlayResult.Ok) return check;

            var owner = card.Owner;
            List<Unit> targets;
            if (card.Def.Target == TargetRule.MoveDest)
            {
                if (dest == null) return PlayResult.NoTarget;
                var d = dest.Value;
                if (d == owner.Pos || !ReachableTiles(owner).ContainsKey(d)) return PlayResult.OutOfRange;
                _moveDest = d;
                targets = new List<Unit> { owner };
            }
            else
            {
                var resolved = ResolveTargets(owner, card.Def, target);
                if (resolved == null) return target != null ? PlayResult.OutOfRange : PlayResult.NoTarget;
                targets = resolved;
            }

            Cost -= card.Def.Cost;
            Hand.Remove(card);
            Emit(EventType.CardPlayed, owner.Id, -1, card.Def.Cost, card.Def.Name);

            foreach (var effect in card.Def.Effects)
            {
                var affected = effect.OnSelf ? new List<Unit> { owner } : targets;
                ResolveEffect(owner, effect, affected);
            }
            _moveDest = null;

            Discard(card);
            CheckEnd();
            return PlayResult.Ok;
        }

        /// <summary>結束我方回合：敵方依序行動，然後開始下一個我方回合。</summary>
        public void EndTurn()
        {
            if (Result != BattleResult.Ongoing) return;

            RunEnemyPhase();
            if (Result != BattleResult.Ongoing) return;

            EndRound();
            if (Setup.TurnLimit > 0 && Turn >= Setup.TurnLimit)
            {
                Result = BattleResult.Lost;
                Emit(EventType.BattleEnd, -1, -1, 0, "turn limit");
                return;
            }
            StartPlayerTurn();
        }

        // ---------------------------------------------------------------- 回合流程

        private void StartPlayerTurn()
        {
            Turn++;
            Cost = Math.Min(Cost + Setup.CostPerTurn, Setup.CostCap);
            Emit(EventType.TurnStart, -1, -1, Turn, "");

            TickDamageOverTime(Side.Player);
            if (CheckEnd()) return;

            // 手牌不會在回合結束時棄掉（取後不放回）：首回合抽起手牌，之後每回合抽 DrawPerTurn 張，手牌上限 MaxHandSize。
            int draw = Turn == 1 ? Setup.HandSize : Setup.DrawPerTurn;
            for (int i = 0; i < draw && DrawOne(); i++) { }
        }

        private void RunEnemyPhase()
        {
            TickDamageOverTime(Side.Enemy);
            if (CheckEnd()) return;

            foreach (var enemy in AliveUnits(Side.Enemy))
            {
                if (!enemy.Alive) continue;
                var intent = GetIntent(enemy);
                switch (intent.Type)
                {
                    case Intent.Kind.Attack:
                        if (intent.MoveTo != null) RelocateEnemy(enemy, intent.MoveTo.Value);
                        Emit(EventType.EnemyAttack, enemy.Id, intent.Target!.Id, intent.Big ? 1 : 0, intent.Big ? "big" : "");
                        DealAttackDamage(enemy, intent.Target!, intent.Big ? enemy.AbilityPower : enemy.AttackMultiplier);
                        if (intent.Big) enemy.Charging = false;
                        break;
                    case Intent.Kind.Summon:
                    {
                        var spawned = SpawnEnemy(enemy.SummonDef!, intent.MoveTo!.Value);
                        Emit(EventType.EnemySummon, enemy.Id, spawned.Id, 0, intent.MoveTo!.Value.ToString());
                        break;
                    }
                    case Intent.Kind.Charge:
                        enemy.Charging = true;
                        Emit(EventType.EnemyCharge, enemy.Id, -1, 0, "");
                        break;
                    case Intent.Kind.Heal:
                    {
                        var ally = intent.Target!;
                        int amount = DamageCalc.Scale(enemy.EffectivePower, enemy.AbilityPower);
                        int healed = Math.Min(amount, ally.MaxHp - ally.Hp);
                        ally.Hp += healed;
                        Emit(EventType.Heal, enemy.Id, ally.Id, healed, "");
                        break;
                    }
                    case Intent.Kind.Move:
                        RelocateEnemy(enemy, intent.MoveTo!.Value);
                        break;
                    case Intent.Kind.Stunned:
                        enemy.Charging = false; // 蓄力被昏亂打斷
                        Emit(EventType.EnemySkip, enemy.Id, -1, 0, "stun");
                        break;
                }
                if (intent.Type != Intent.Kind.Stunned && intent.Type != Intent.Kind.None) enemy.Actions++;
                if (CheckEnd()) return;
            }
        }

        /// <summary>一整輪結束：所有狀態回合數 -1，到期移除。</summary>
        private void EndRound()
        {
            foreach (var unit in Units.Where(u => u.Alive))
            {
                foreach (var type in unit.Statuses.Keys.ToList())
                {
                    var state = unit.Statuses[type];
                    state.Turns--;
                    if (state.Turns <= 0) unit.Statuses.Remove(type);
                }
                foreach (var b in unit.DefBreaks) b.Turns--;
                unit.DefBreaks.RemoveAll(b => b.Turns <= 0);
            }
        }

        private void TickDamageOverTime(Side side)
        {
            foreach (var unit in AliveUnits(side))
            {
                foreach (var type in new[] { StatusType.Burn, StatusType.Poison })
                {
                    if (!unit.Alive) break;
                    if (unit.Statuses.TryGetValue(type, out var state))
                        ApplyDamage(null, unit, state.Power, ignoreArmor: true, text: type.ToString());
                }
            }
        }

        // ---------------------------------------------------------------- 效果結算

        private void ResolveEffect(Unit owner, EffectDef effect, List<Unit> affected)
        {
            switch (effect.Type)
            {
                case EffectType.Damage:
                    foreach (var t in affected.ToList())
                        if (t.Alive) DealAttackDamage(owner, t, effect.Multiplier);
                    break;
                case EffectType.Heal:
                    foreach (var t in affected)
                    {
                        if (!t.Alive) continue;
                        int amount = DamageCalc.Scale(owner.EffectivePower, effect.Multiplier);
                        int healed = Math.Min(amount, t.MaxHp - t.Hp);
                        t.Hp += healed;
                        Emit(EventType.Heal, owner.Id, t.Id, healed, "");
                    }
                    break;
                case EffectType.Armor:
                    foreach (var t in affected)
                    {
                        if (!t.Alive) continue;
                        int amount = DamageCalc.Scale(owner.EffectivePower, effect.Multiplier);
                        t.Armor += amount;
                        Emit(EventType.Armor, owner.Id, t.Id, amount, "");
                    }
                    break;
                case EffectType.ApplyStatus:
                    foreach (var t in affected)
                    {
                        if (!t.Alive) continue;
                        if (effect.Status == StatusType.ArmorBreak)
                        {
                            // 破甲：Multiplier 為降低防禦的百分比；每次施加都是獨立一筆（不是層數），
                            // 各筆各自計時、彼此乘算（例：40% 與 15% 並存 → 防禦 ×0.6×0.85）。
                            t.DefBreaks.Add(new DefBreak
                            {
                                Percent = Math.Min(0.95, Math.Max(0.0, effect.Multiplier * owner.StatusPotency)),
                                Turns = effect.Amount,
                            });
                        }
                        else if (IsBuff(effect.Status))
                        {
                            // 增益：Multiplier 是加成比例；已有同種增益時取較大的加成與較長的回合數。
                            int percent = (int)Math.Round(effect.Multiplier * 100 * owner.StatusPotency, MidpointRounding.AwayFromZero);
                            if (t.Statuses.TryGetValue(effect.Status, out var cur))
                            {
                                cur.Power = Math.Max(cur.Power, percent);
                                cur.Turns = Math.Max(cur.Turns, effect.Amount);
                            }
                            else
                            {
                                t.Statuses[effect.Status] = new StatusState { Power = percent, Turns = effect.Amount };
                            }
                        }
                        else
                        {
                            t.Statuses[effect.Status] = new StatusState
                            {
                                Power = DamageCalc.Scale(owner.EffectivePower, effect.Multiplier),
                                Turns = effect.Amount,
                            };
                        }
                        Emit(EventType.StatusApplied, owner.Id, t.Id, effect.Amount, effect.Status.ToString());
                    }
                    break;
                case EffectType.Detonate:
                    // 引爆（火攻 / 瘟毒）：立刻結算剩餘持續傷害並移除狀態，再把同樣的狀態擴散給相鄰、尚未中招的敵人。
                    // 只處理施放當下就在燃燒的敵人（剛擴散到的不會在同一次引爆裡連鎖）。
                    foreach (var t in affected.Where(u => u.Alive && u.Has(effect.Status)).ToList())
                    {
                        if (!t.Alive || !t.Statuses.TryGetValue(effect.Status, out var dot)) continue;
                        int total = dot.Power * dot.Turns;
                        t.Statuses.Remove(effect.Status);
                        ApplyDamage(owner, t, total, ignoreArmor: true, text: "detonate");
                        foreach (var n in AliveUnits(t.Side))
                        {
                            if (n == t || n.Has(effect.Status)) continue;
                            bool adjacent = (n.Pos.Row == t.Pos.Row && Math.Abs(n.Pos.Lane - t.Pos.Lane) == 1)
                                         || (n.Pos.Lane == t.Pos.Lane && Math.Abs(n.Pos.Row - t.Pos.Row) == 1);
                            if (!adjacent) continue;
                            n.Statuses[effect.Status] = new StatusState { Power = dot.Power, Turns = Math.Max(1, effect.Amount) };
                            Emit(EventType.StatusApplied, owner.Id, n.Id, effect.Amount, effect.Status.ToString());
                        }
                    }
                    break;
                case EffectType.StunGauge:
                    foreach (var t in affected)
                    {
                        if (!t.Alive || t.Side != Side.Enemy) continue;
                        t.StunGauge += effect.Amount;
                        if (t.StunGauge >= t.StunGaugeMax)
                        {
                            // 昏亂條滿：眩暈 1 回合，條歸零，上限提高（越控越難控）。
                            t.StunGauge = 0;
                            t.StunGaugeMax = (int)Math.Round(t.StunGaugeMax * (1.0 + t.StunGrowth), MidpointRounding.AwayFromZero);
                            t.Statuses[StatusType.Stun] = new StatusState { Turns = 1 };
                            Emit(EventType.StatusApplied, owner.Id, t.Id, 1, StatusType.Stun.ToString());
                        }
                        Emit(EventType.StunGauge, owner.Id, t.Id, t.StunGauge, t.StunGaugeMax.ToString());
                    }
                    break;
                case EffectType.Draw:
                    int drawn = 0;
                    for (int i = 0; i < effect.Amount && DrawOne(); i++) drawn++;
                    Emit(EventType.Draw, owner.Id, -1, drawn, "");
                    break;
                case EffectType.Move:
                    if (_moveDest != null && owner.Alive)
                    {
                        var from = owner.Pos;
                        var to = _moveDest.Value;
                        _board[from.Lane, from.Row] = null;
                        _board[to.Lane, to.Row] = owner;
                        owner.Pos = to;
                        Emit(EventType.Move, owner.Id, -1, Position.Distance(from, to), $"{from}->{to}");
                    }
                    break;
                case EffectType.GainCost:
                    Cost = Math.Min(Setup.CostCap, Cost + effect.Amount);
                    Emit(EventType.GainCost, owner.Id, -1, effect.Amount, "");
                    break;
            }
        }

        private static bool IsBuff(StatusType type) =>
            type == StatusType.DefUp || type == StatusType.AtkUp || type == StatusType.CritUp;

        private void DealAttackDamage(Unit attacker, Unit target, double multiplier)
        {
            if (!Setup.NoRandomness && Rng.Roll(target.Stats.Dodge))
            {
                Emit(EventType.Dodge, attacker.Id, target.Id, 0, "");
                return;
            }
            bool crit = !Setup.NoRandomness && Rng.Roll(attacker.EffectiveCrit);
            int dmg = DamageCalc.Compute(attacker.EffectivePower, multiplier, target.EffectiveDef,
                crit, attacker.Stats.CritDmg);
            if (target.Has(StatusType.Taunt))
                dmg = Math.Max(1, (int)Math.Round(dmg * (1.0 - DamageCalc.TauntDamageReduction), MidpointRounding.AwayFromZero));
            ApplyDamage(attacker, target, dmg, ignoreArmor: false, text: crit ? "crit" : "");
        }

        private void ApplyDamage(Unit? source, Unit target, int dmg, bool ignoreArmor, string text)
        {
            int absorbed = 0;
            if (!ignoreArmor)
            {
                absorbed = Math.Min(target.Armor, dmg);
                target.Armor -= absorbed;
            }
            target.Hp -= dmg - absorbed;
            Emit(EventType.Damage, source?.Id ?? -1, target.Id, dmg, text);
            if (target.Hp <= 0) Kill(target);
        }

        private void Kill(Unit unit)
        {
            unit.Hp = 0;
            unit.Alive = false;
            _board[unit.Pos.Lane, unit.Pos.Row] = null;
            Emit(EventType.Death, -1, unit.Id, 0, "");
        }

        // ---------------------------------------------------------------- 敵方移動

        private void RelocateEnemy(Unit enemy, Position dest)
        {
            var from = enemy.Pos;
            _board[from.Lane, from.Row] = null;
            _board[dest.Lane, dest.Row] = enemy;
            enemy.Pos = dest;
            Emit(EventType.EnemyMove, enemy.Id, -1, Position.Distance(from, dest), $"{from}->{dest}");
        }

        // ---------------------------------------------------------------- 牌庫

        /// <summary>抽一張：取後不放回，牌堆抽完就沒有了（不重洗棄牌堆）；手牌上限 <see cref="MaxHandSize"/>。</summary>
        private bool DrawOne()
        {
            if (Hand.Count >= MaxHandSize) return false;
            if (DrawPile.Count == 0) return false;
            var card = DrawPile[0];
            DrawPile.RemoveAt(0);
            Hand.Add(card);
            return true;
        }

        private void Discard(CardInstance card, bool forceDiscardPile = false)
        {
            if (!forceDiscardPile && (card.Def.Keywords & CardKeywords.Exhaust) != 0)
                ExhaustPile.Add(card);
            else
                DiscardPile.Add(card);
        }

        /// <summary>依 ScriptedDraw 排序：腳本裡的卡牌 id 依序排最前面，其餘維持原順序。</summary>
        private void OrderByScript(List<CardInstance> list)
        {
            var rest = new List<CardInstance>(list);
            var ordered = new List<CardInstance>();
            foreach (var id in Setup.ScriptedDraw)
            {
                var card = rest.FirstOrDefault(c => c.Def.Id == id);
                if (card == null) continue;
                ordered.Add(card);
                rest.Remove(card);
            }
            ordered.AddRange(rest);
            list.Clear();
            list.AddRange(ordered);
        }

        private void Shuffle(List<CardInstance> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        // ---------------------------------------------------------------- 內部工具

        private Unit SpawnEnemy(EnemyDef def, Position pos)
        {
            var unit = CreateUnit(def.Name, Side.Enemy, def.AttackType, def.Base.Clone(), pos);
            unit.AttackMultiplier = def.AttackMultiplier;
            unit.Ability = def.Ability;
            unit.AbilityPower = def.AbilityPower;
            unit.SummonDef = def.Summons;
            unit.SummonCap = def.SummonCap;
            unit.SummonEvery = def.SummonEvery;
            unit.StunGaugeMax = def.StunGauge;
            unit.StunGrowth = def.StunGrowth;
            unit.DefId = def.Id;
            return unit;
        }

        private Unit CreateUnit(string name, Side side, AttackType attackType, Stats stats, Position pos)
        {
            if (!InBounds(pos)) throw new ArgumentException($"{name} 的位置 {pos} 超出棋盤");
            if (_board[pos.Lane, pos.Row] != null) throw new ArgumentException($"{name} 的位置 {pos} 已有單位");
            var unit = new Unit
            {
                Id = Units.Count,
                Name = name,
                Side = side,
                AttackType = attackType,
                Stats = stats,
                Hp = stats.Hp,
                Pos = pos,
            };
            Units.Add(unit);
            _board[pos.Lane, pos.Row] = unit;
            return unit;
        }

        private static Stats ScaleForLevel(Stats baseStats, int level)
        {
            var s = baseStats.Clone();
            double factor = 1.0 + LevelGrowthPerLevel * Math.Max(0, level - 1);
            s.Hp = (int)Math.Round(s.Hp * factor);
            s.Atk = (int)Math.Round(s.Atk * factor);
            s.Def = (int)Math.Round(s.Def * factor);
            s.Int = (int)Math.Round(s.Int * factor);
            return s;
        }

        private static List<Unit>? NonEmpty(List<Unit> list) => list.Count == 0 ? null : list;

        private bool CheckEnd()
        {
            if (Result != BattleResult.Ongoing) return true;
            if (!Units.Any(u => u.Side == Side.Enemy && u.Alive))
                Result = BattleResult.Won;
            else if (!Units.Any(u => u.Side == Side.Player && u.Alive) || Units.Any(u => u.Protected && !u.Alive))
                Result = BattleResult.Lost;
            else
                return false;
            Emit(EventType.BattleEnd, -1, -1, 0, Result.ToString());
            return true;
        }

        private void Emit(EventType type, int source, int target, int value, string text)
        {
            var e = new BattleEvent { Type = type, Source = source, Target = target, Value = value, Text = text };
            if (source >= 0 && source < Units.Count)
            {
                e.SourceSide = Units[source].Side;
                e.SourcePos = Units[source].Pos;
            }
            if (target >= 0 && target < Units.Count)
            {
                e.TargetSide = Units[target].Side;
                e.TargetPos = Units[target].Pos;
            }
            Events.Add(e);
        }
    }
}
