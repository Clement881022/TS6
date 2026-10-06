using System;
using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core
{
    /// <summary>
    /// 戰鬥模擬核心（不依賴 Unity）。規則見 docs/combat.md。
    /// 使用方式：new Battle(setup) 後呼叫 PlayCard / Move / EndTurn，並讀取 Events 做表現。
    /// </summary>
    public sealed class Battle
    {
        public const int MaxHandSize = 10;
        public const double LevelGrowthPerLevel = 0.1;

        private readonly Unit?[,] _playerBoard;
        private readonly Unit?[,] _enemyBoard;
        private int _nextCardId;

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
        /// <summary>本回合已使用的移動次數。</summary>
        public int MovesUsed { get; private set; }
        public BattleResult Result { get; private set; } = BattleResult.Ongoing;

        public Battle(BattleSetup setup)
        {
            Setup = setup;
            Rng = new Rng(setup.Seed);
            _playerBoard = new Unit?[setup.Lanes, setup.Rows];
            _enemyBoard = new Unit?[setup.Lanes, setup.Rows];

            var allCards = new List<CardInstance>();
            foreach (var slot in setup.Heroes)
            {
                var unit = CreateUnit(slot.Def.Name, Side.Player, slot.Def.AttackType, ScaleForLevel(slot.Def.Base, slot.Level), slot.Pos);
                unit.Hero = slot.Def;
                unit.DefId = slot.Def.Id;
                foreach (var cardDef in slot.Def.Deck)
                    allCards.Add(new CardInstance(_nextCardId++, cardDef, unit));
            }
            foreach (var slot in setup.Enemies)
            {
                var unit = CreateUnit(slot.Def.Name, Side.Enemy, slot.Def.AttackType, slot.Def.Base.Clone(), slot.Pos);
                unit.AttackMultiplier = slot.Def.AttackMultiplier;
                unit.Ability = slot.Def.Ability;
                unit.AbilityPower = slot.Def.AbilityPower;
                unit.DefId = slot.Def.Id;
            }

            Shuffle(allCards);
            // 先登：開局必在起手牌，穩定地排到抽牌堆最前面。
            DrawPile.AddRange(allCards.Where(c => (c.Def.Keywords & CardKeywords.Innate) != 0));
            DrawPile.AddRange(allCards.Where(c => (c.Def.Keywords & CardKeywords.Innate) == 0));

            StartPlayerTurn();
        }

        // ---------------------------------------------------------------- 查詢

        public Unit? UnitAt(Side side, Position pos)
        {
            if (!InBounds(pos)) return null;
            return BoardOf(side)[pos.Lane, pos.Row];
        }

        /// <summary>依「路 → 排」順序（左到右、前到後）列出該方存活單位。</summary>
        public List<Unit> AliveUnits(Side side)
        {
            return Units.Where(u => u.Side == side && u.Alive)
                        .OrderBy(u => u.Pos.Lane).ThenBy(u => u.Pos.Row).ToList();
        }

        public bool InBounds(Position pos)
        {
            return pos.Lane >= 0 && pos.Lane < Setup.Lanes && pos.Row >= 0 && pos.Row < Setup.Rows;
        }

        /// <summary>檢查卡牌目前能否打出（不產生副作用）。</summary>
        public PlayResult CanPlay(CardInstance card)
        {
            if (Result != BattleResult.Ongoing) return PlayResult.BattleOver;
            if (!Hand.Contains(card)) return PlayResult.NotInHand;
            if (!card.Owner.Alive) return PlayResult.OwnerDead;
            if (card.Owner.Has(StatusType.Stun)) return PlayResult.Stunned;
            if (Cost < card.Def.Cost) return PlayResult.NotEnoughCost;
            if (ResolveTargets(card.Owner, card.Def) == null)
                return PlayResult.NoTarget;
            return PlayResult.Ok;
        }

        /// <summary>目標自動判定；回傳 null 表示沒有可選目標（卡牌不可打出）。</summary>
        public List<Unit>? ResolveTargets(Unit owner, CardDef def)
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
                        if (best == null || (long)u.Hp * best.MaxHp < (long)best.Hp * u.MaxHp) best = u;
                    }
                    return best == null ? null : new List<Unit> { best };
                }
                case TargetRule.EnemyFront:
                case TargetRule.EnemyBack:
                {
                    var center = PickTarget(foe, owner.Pos.Lane, def.Target == TargetRule.EnemyBack);
                    if (center == null) return null;
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
            if (!enemy.Alive) return new Intent { Type = Intent.Kind.None };
            if (enemy.Has(StatusType.Stun)) return new Intent { Type = Intent.Kind.Stunned };

            if (enemy.Ability == EnemyAbility.Healer)
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

            Unit? forced = AliveUnits(Side.Player).FirstOrDefault(u => u.Has(StatusType.Taunt));
            if (forced != null)
                return new Intent { Type = Intent.Kind.Attack, Target = forced };

            bool ranged = enemy.AttackType == AttackType.Ranged;
            var target = PickTarget(Side.Player, enemy.Pos.Lane, ranged);
            if (target != null) return new Intent { Type = Intent.Kind.Attack, Target = target };

            var heroes = AliveUnits(Side.Player);
            if (heroes.Count == 0) return new Intent { Type = Intent.Kind.None };
            int bestLane = heroes
                .OrderBy(h => Math.Abs(h.Pos.Lane - enemy.Pos.Lane)).ThenBy(h => h.Pos.Lane)
                .First().Pos.Lane;
            return MoveToward(enemy, bestLane);
        }

        // ---------------------------------------------------------------- 玩家行動

        public PlayResult PlayCard(CardInstance card)
        {
            var check = CanPlay(card);
            if (check != PlayResult.Ok) return check;

            var owner = card.Owner;
            var targets = ResolveTargets(owner, card.Def)!;
            Cost -= card.Def.Cost;
            Hand.Remove(card);
            Emit(EventType.CardPlayed, owner.Id, -1, card.Def.Cost, card.Def.Name);

            foreach (var effect in card.Def.Effects)
            {
                var affected = effect.OnSelf ? new List<Unit> { owner } : targets;
                ResolveEffect(owner, effect, affected);
            }

            Discard(card);
            CheckEnd();
            return PlayResult.Ok;
        }

        /// <summary>檢查該武將現在能否使用移動按鈕（不檢查目的地）。</summary>
        public PlayResult CanMove(Unit hero)
        {
            if (Result != BattleResult.Ongoing) return PlayResult.BattleOver;
            if (hero.Side != Side.Player || !hero.Alive) return PlayResult.OwnerDead;
            if (hero.Has(StatusType.Stun)) return PlayResult.Stunned;
            if (MovesUsed >= Setup.MovesPerTurn) return PlayResult.MoveUsed;
            if (Cost < Setup.MoveCost) return PlayResult.NotEnoughCost;
            return PlayResult.Ok;
        }

        /// <summary>
        /// 移動按鈕：全隊每回合限用 MovesPerTurn 次、花 MoveCost 費。
        /// 距離（格數）不得超過速度；落在隊友格會與之換位。
        /// </summary>
        public PlayResult Move(Unit hero, Position dest)
        {
            var check = CanMove(hero);
            if (check != PlayResult.Ok) return check;
            if (!InBounds(dest)) return PlayResult.InvalidMove;
            int dist = Math.Abs(dest.Lane - hero.Pos.Lane) + Math.Abs(dest.Row - hero.Pos.Row);
            if (dist < 1 || dist > hero.Stats.Speed) return PlayResult.InvalidMove;

            Cost -= Setup.MoveCost;
            MovesUsed++;

            var board = BoardOf(hero.Side);
            var other = board[dest.Lane, dest.Row];
            var from = hero.Pos;
            board[from.Lane, from.Row] = other;
            if (other != null) other.Pos = from;
            board[dest.Lane, dest.Row] = hero;
            hero.Pos = dest;
            Emit(EventType.Move, hero.Id, other?.Id ?? -1, dist, $"{from}->{dest}");
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
            MovesUsed = 0;
            Cost = Math.Min(Cost + Setup.CostPerTurn, Setup.CostCap);
            Emit(EventType.TurnStart, -1, -1, Turn, "");

            TickDamageOverTime(Side.Player);
            if (CheckEnd()) return;

            // 回合開始時棄掉未保留的手牌，再抽到手牌上限（蓄勢的牌佔手牌數）。
            foreach (var card in Hand.ToList())
            {
                if ((card.Def.Keywords & CardKeywords.Retain) != 0) continue;
                Hand.Remove(card);
                Discard(card, forceDiscardPile: true);
            }
            while (Hand.Count < Setup.HandSize && DrawOne()) { }
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
                        Emit(EventType.EnemyAttack, enemy.Id, intent.Target!.Id, 0, "");
                        DealAttackDamage(enemy, intent.Target!, enemy.AttackMultiplier);
                        break;
                    case Intent.Kind.Heal:
                    {
                        var ally = intent.Target!;
                        int amount = DamageCalc.Scale(enemy.Stats.Atk, enemy.AbilityPower);
                        int healed = Math.Min(amount, ally.MaxHp - ally.Hp);
                        ally.Hp += healed;
                        Emit(EventType.Heal, enemy.Id, ally.Id, healed, "");
                        break;
                    }
                    case Intent.Kind.Move:
                        RelocateEnemy(enemy, intent.MoveTo!.Value);
                        break;
                    case Intent.Kind.Stunned:
                        Emit(EventType.EnemySkip, enemy.Id, -1, 0, "stun");
                        break;
                }
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
                        int amount = DamageCalc.Scale(owner.Stats.Atk, effect.Multiplier);
                        int healed = Math.Min(amount, t.MaxHp - t.Hp);
                        t.Hp += healed;
                        Emit(EventType.Heal, owner.Id, t.Id, healed, "");
                    }
                    break;
                case EffectType.Armor:
                    foreach (var t in affected)
                    {
                        if (!t.Alive) continue;
                        int amount = DamageCalc.Scale(owner.Stats.Atk, effect.Multiplier);
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
                                Percent = Math.Min(0.95, Math.Max(0.0, effect.Multiplier)),
                                Turns = effect.Amount,
                            });
                        }
                        else
                        {
                            t.Statuses[effect.Status] = new StatusState
                            {
                                Power = DamageCalc.Scale(owner.Stats.Atk, effect.Multiplier),
                                Turns = effect.Amount,
                            };
                        }
                        Emit(EventType.StatusApplied, owner.Id, t.Id, effect.Amount, effect.Status.ToString());
                    }
                    break;
                case EffectType.Draw:
                    int drawn = 0;
                    for (int i = 0; i < effect.Amount && DrawOne(); i++) drawn++;
                    Emit(EventType.Draw, owner.Id, -1, drawn, "");
                    break;
                case EffectType.GainCost:
                    Cost = Math.Min(Setup.CostCap, Cost + effect.Amount);
                    Emit(EventType.GainCost, owner.Id, -1, effect.Amount, "");
                    break;
            }
        }

        private void DealAttackDamage(Unit attacker, Unit target, double multiplier)
        {
            if (Rng.Roll(target.Stats.Dodge))
            {
                Emit(EventType.Dodge, attacker.Id, target.Id, 0, "");
                return;
            }
            bool crit = Rng.Roll(attacker.Stats.Crit);
            int dmg = DamageCalc.Compute(attacker.Stats.Atk, multiplier, target.EffectiveDef,
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
            BoardOf(unit.Side)[unit.Pos.Lane, unit.Pos.Row] = null;
            Emit(EventType.Death, -1, unit.Id, 0, "");
        }

        // ---------------------------------------------------------------- 敵方移動

        private Intent MoveToward(Unit enemy, int targetLane)
        {
            int currentDist = Math.Abs(enemy.Pos.Lane - targetLane);
            // 在速度範圍內的空格中，選離目標路最近者（同距離取步數少、再取較前排）；必須比現在更靠近才移動。
            Position? best = null;
            int bestLaneDist = int.MaxValue;
            int bestStep = int.MaxValue;
            for (int lane = 0; lane < Setup.Lanes; lane++)
            {
                for (int row = 0; row < Setup.Rows; row++)
                {
                    if (_enemyBoard[lane, row] != null) continue;
                    int step = Math.Abs(lane - enemy.Pos.Lane) + Math.Abs(row - enemy.Pos.Row);
                    if (step < 1 || step > enemy.Stats.Speed) continue;
                    int laneDist = Math.Abs(lane - targetLane);
                    if (laneDist < bestLaneDist || (laneDist == bestLaneDist && step < bestStep))
                    {
                        best = new Position(lane, row);
                        bestLaneDist = laneDist;
                        bestStep = step;
                    }
                }
            }
            if (best == null || bestLaneDist >= currentDist)
                return new Intent { Type = Intent.Kind.None };
            return new Intent { Type = Intent.Kind.Move, MoveTo = best };
        }

        private void RelocateEnemy(Unit enemy, Position dest)
        {
            var from = enemy.Pos;
            _enemyBoard[from.Lane, from.Row] = null;
            _enemyBoard[dest.Lane, dest.Row] = enemy;
            enemy.Pos = dest;
            Emit(EventType.EnemyMove, enemy.Id, -1, 0, $"{from}->{dest}");
        }

        // ---------------------------------------------------------------- 牌庫

        private bool DrawOne()
        {
            if (Hand.Count >= MaxHandSize) return false;
            if (DrawPile.Count == 0)
            {
                if (DiscardPile.Count == 0) return false;
                DrawPile.AddRange(DiscardPile);
                DiscardPile.Clear();
                Shuffle(DrawPile);
            }
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

        private Unit CreateUnit(string name, Side side, AttackType attackType, Stats stats, Position pos)
        {
            if (!InBounds(pos)) throw new ArgumentException($"{name} 的位置 {pos} 超出棋盤");
            var board = BoardOf(side);
            if (board[pos.Lane, pos.Row] != null) throw new ArgumentException($"{name} 的位置 {pos} 已有單位");
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
            board[pos.Lane, pos.Row] = unit;
            return unit;
        }

        private static Stats ScaleForLevel(Stats baseStats, int level)
        {
            var s = baseStats.Clone();
            double factor = 1.0 + LevelGrowthPerLevel * Math.Max(0, level - 1);
            s.Hp = (int)Math.Round(s.Hp * factor);
            s.Atk = (int)Math.Round(s.Atk * factor);
            s.Def = (int)Math.Round(s.Def * factor);
            return s;
        }

        private Unit?[,] BoardOf(Side side) => side == Side.Player ? _playerBoard : _enemyBoard;

        /// <summary>
        /// 目標判定：同路優先；同路沒人就由上往下（第 0 路起）找第一條有人的路。
        /// 因此 0 號位（最上方）是預設的「坦克位」。
        /// </summary>
        private Unit? PickTarget(Side side, int lane, bool backFirst)
        {
            if (backFirst) return PickBackPriority(side);
            var inLane = PickInLane(side, lane, false);
            if (inLane != null) return inLane;
            for (int l = 0; l < Setup.Lanes; l++)
            {
                if (l == lane) continue;
                var found = PickInLane(side, l, false);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// 後排優先（弓手 / 遠程）：不分路，先找最後一排，依路由上往下取第一個；
        /// 最後一排沒人才往前一排，同樣由上往下。
        /// </summary>
        private Unit? PickBackPriority(Side side)
        {
            var board = BoardOf(side);
            for (int r = Setup.Rows - 1; r >= 0; r--)
                for (int l = 0; l < Setup.Lanes; l++)
                    if (board[l, r] != null) return board[l, r];
            return null;
        }

        /// <summary>某一路上最前排（或最後排優先）的存活單位。</summary>
        private Unit? PickInLane(Side side, int lane, bool backFirst)
        {
            var board = BoardOf(side);
            if (lane < 0 || lane >= Setup.Lanes) return null;
            if (backFirst)
            {
                for (int r = Setup.Rows - 1; r >= 0; r--)
                    if (board[lane, r] != null) return board[lane, r];
            }
            else
            {
                for (int r = 0; r < Setup.Rows; r++)
                    if (board[lane, r] != null) return board[lane, r];
            }
            return null;
        }

        private static List<Unit>? NonEmpty(List<Unit> list) => list.Count == 0 ? null : list;

        private bool CheckEnd()
        {
            if (Result != BattleResult.Ongoing) return true;
            if (!Units.Any(u => u.Side == Side.Enemy && u.Alive))
                Result = BattleResult.Won;
            else if (!Units.Any(u => u.Side == Side.Player && u.Alive))
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
