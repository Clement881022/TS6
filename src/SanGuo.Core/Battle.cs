using System;
using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core
{
    public sealed class Battle
    {
        public const int MaxHandSize = 10;
        public const double HeroGrowthPerLevel = 0.015;
        public const double EnemyBaseFactor = 0.6, EnemyGrowthPerLevel = 0.04;
        public const double BurnDecay = 0.5;

        public static double HeroLevelFactor(int level) => 1.0 + HeroGrowthPerLevel * Math.Max(0, level - 1);
        public static double EnemyLevelFactor(int level) => EnemyBaseFactor + EnemyGrowthPerLevel * Math.Max(0, level - 1);

        private readonly Unit?[,] _board;
        private int _nextCardId;
        private Position? _moveDest;

        public BattleSetup Setup { get; }
        public Rng Rng { get; }
        public List<Unit> Units { get; } = new List<Unit>();
        public List<CardInstance> DrawPile { get; } = new List<CardInstance>();
        public List<CardInstance> Hand { get; } = new List<CardInstance>();
        public List<CardInstance> DiscardPile { get; } = new List<CardInstance>();
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
                var unit = CreateUnit(slot.Def.Name, Side.Player, slot.Def.AttackType, ScaleHero(slot.Def.Base, slot.Level), slot.Pos);
                unit.Hero = slot.Def;
                unit.Protected = slot.IsProtected;
                unit.Level = slot.Level;
                if (slot.StartHpPercent < 100) unit.Hp = Math.Max(1, unit.MaxHp * slot.StartHpPercent / 100);
                unit.DefId = slot.Def.Id;
                unit.ArtId = slot.Def.Id;
                var heroCards = new List<CardInstance>();
                foreach (var cardDef in slot.Def.Deck)
                    heroCards.Add(new CardInstance(_nextCardId++, cardDef, unit));
                allCards.AddRange(heroCards);
                cardsByHero.Add(heroCards);
            }
            foreach (var slot in setup.Enemies) SpawnEnemy(slot);

            int moveCards = cardsByHero.Count(c => c.Count > 0);
            for (int i = 0; i < moveCards; i++) allCards.Add(new CardInstance(_nextCardId++, CardDef.CreateMove(), null));

            if (setup.ScriptedDraw.Count > 0)
            {
                for (int i = 0; cardsByHero.Any(c => i < c.Count); i++)
                    foreach (var heroCards in cardsByHero)
                        if (i < heroCards.Count) DrawPile.Add(heroCards[i]);
                OrderByScript(DrawPile);
                var moves = allCards.Where(c => c.Owner == null).Take(setup.FirstTurnMoves).ToList();
                DrawPile.InsertRange(Math.Min(setup.FirstTurnRandom, DrawPile.Count), moves);
                DrawPile.AddRange(allCards.Where(c => c.Owner == null).Skip(setup.FirstTurnMoves));
            }
            else
            {
                Shuffle(allCards);
                var moves = allCards.Where(c => c.Owner == null).Take(setup.FirstTurnMoves).ToList();
                DrawPile.AddRange(moves);
                DrawPile.AddRange(allCards.Where(c => !moves.Contains(c)));
            }
            _firstTurnDraw = setup.FirstTurnRandom + Math.Min(setup.FirstTurnMoves, moveCards);

            StartPlayerTurn();
        }

        private readonly int _firstTurnDraw;

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

        public Dictionary<Position, int> ReachableTiles(Unit unit)
        {
            int max = unit.Stats.Move;
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

        public bool CanMoveUnit(Unit u) =>
            u.Side == Side.Player && u.Alive && ReachableTiles(u).Count > 1;

        public PlayResult CanPlay(CardInstance card)
        {
            if (Result != BattleResult.Ongoing) return PlayResult.BattleOver;
            if (!Hand.Contains(card)) return PlayResult.NotInHand;
            if (Cost < card.Def.Cost) return PlayResult.NotEnoughCost;
            if (card.Owner == null)
                return AliveUnits(Side.Player).Any(CanMoveUnit) ? PlayResult.Ok : PlayResult.NoTarget;
            if (!card.Owner.Alive) return PlayResult.OwnerDead;
            if (ResolveTargets(card.Owner, card.Def) == null)
                return PlayResult.NoTarget;
            return PlayResult.Ok;
        }

        public List<Unit>? ResolveTargets(Unit owner, CardDef def, Position? chosen = null)
        {
            Side own = owner.Side;
            Side foe = own == Side.Player ? Side.Enemy : Side.Player;
            int range = CardRange(owner, def);
            switch (def.Target)
            {
                case TargetRule.Self:
                    return new List<Unit> { owner };
                case TargetRule.AllAllies:
                    return NonEmpty(AliveUnits(own));
                case TargetRule.AllEnemies:
                    return NonEmpty(AliveUnits(foe));
                case TargetRule.Ally:
                {
                    if (chosen != null)
                    {
                        var picked = UnitAt(own, chosen.Value);
                        if (picked == null || !picked.Alive || Position.Distance(owner.Pos, picked.Pos) > range) return null;
                        return new List<Unit> { picked };
                    }
                    Unit? best = null;
                    foreach (var u in AliveUnits(own))
                    {
                        if (Position.Distance(owner.Pos, u.Pos) > range) continue;
                        if (best == null || (long)u.Hp * best.MaxHp < (long)best.Hp * u.MaxHp) best = u;
                    }
                    return best == null ? null : new List<Unit> { best };
                }
                case TargetRule.Enemy:
                {
                    Position center;
                    if (chosen != null)
                    {
                        if (!InBounds(chosen.Value) || Position.Distance(owner.Pos, chosen.Value) > range) return null;
                        center = chosen.Value;
                    }
                    else
                    {
                        var inRange = AliveUnits(foe).Where(u => Position.Distance(owner.Pos, u.Pos) <= range).ToList();
                        if (inRange.Count == 0) return null;
                        center = inRange.OrderBy(u => Position.Distance(owner.Pos, u.Pos)).ThenBy(u => u.Hp).ThenBy(u => u.Pos.Lane).First().Pos;
                    }
                    var result = new List<Unit>();
                    foreach (var cell in Targeting.ExpandShape(center, def.Shape, Setup.Lanes, Setup.Rows))
                    {
                        var u = UnitAt(foe, cell);
                        if (u != null && u.Alive) result.Add(u);
                    }
                    return result.Count == 0 ? null : result;
                }
                default:
                    return null;
            }
        }

        public static int CardRange(Unit owner, CardDef def) => def.Unlimited ? 99 : owner.AttackRange;

        public Intent GetIntent(Unit enemy)
        {
            if (!enemy.Alive) return new Intent { Type = Intent.Kind.None };
            if (enemy.Charging) return new Intent { Type = Intent.Kind.Charging };
            if (WillStartCharge(enemy)) return new Intent { Type = Intent.Kind.Charge };

            var heroes = AliveUnits(Side.Player);
            if (heroes.Count == 0) return new Intent { Type = Intent.Kind.None };
            return PlanAttack(enemy, TauntCandidates(enemy, heroes));
        }

        private static bool WillStartCharge(Unit enemy) =>
            enemy.ChargeTurns > 0 && !enemy.Charging && enemy.IdleActions >= enemy.ChargeInterval;

        private List<Unit> TauntCandidates(Unit enemy, List<Unit> heroes)
        {
            if (enemy.Statuses.TryGetValue(StatusType.Taunt, out var taunt))
            {
                var source = heroes.FirstOrDefault(h => h.Id == taunt.SourceId);
                if (source != null) return new List<Unit> { source };
            }
            return heroes;
        }

        private Intent PlanAttack(Unit enemy, List<Unit> candidates)
        {
            int range = enemy.AttackRange;
            var reach = ReachableTiles(enemy);

            Unit? best = null;
            Position bestTile = enemy.Pos;
            foreach (var h in OrderTargets(enemy, candidates))
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

        private static IEnumerable<Unit> OrderTargets(Unit enemy, List<Unit> candidates) =>
            enemy.AttackRange > 1
                ? candidates.OrderByDescending(h => h.Pos.Row).ThenBy(h => h.Hp).ThenBy(h => h.Pos.Lane)
                : candidates.OrderBy(h => Position.Distance(enemy.Pos, h.Pos)).ThenBy(h => h.Hp).ThenBy(h => h.Pos.Lane);

        private static bool TileBefore(Position a, Position b) => a.Row != b.Row ? a.Row > b.Row : a.Lane < b.Lane;

        public PlayResult PlayCard(CardInstance card, Position? target = null, Unit? mover = null)
        {
            var check = CanPlay(card);
            if (check != PlayResult.Ok) return check;

            Unit owner;
            List<Unit> targets;
            if (card.Def.Target == TargetRule.MoveDest)
            {
                if (mover == null || !Units.Contains(mover) || !CanMoveUnit(mover)) return PlayResult.InvalidMover;
                if (target == null) return PlayResult.NoTarget;
                var d = target.Value;
                if (d == mover.Pos || !ReachableTiles(mover).ContainsKey(d)) return PlayResult.OutOfRange;
                _moveDest = d;
                owner = mover;
                targets = new List<Unit> { mover };
            }
            else
            {
                owner = card.Owner!;
                var resolved = ResolveTargets(owner, card.Def, target);
                if (resolved == null) return target != null ? PlayResult.OutOfRange : PlayResult.NoTarget;
                targets = resolved;
            }

            Cost -= card.Def.Cost;
            Hand.Remove(card);
            Emit(EventType.CardPlayed, owner.Id, -1, card.Def.Cost, card.Def.Name);

            var aliveBefore = targets.Where(t => t.Side != owner.Side && t.Alive).ToList();
            foreach (var effect in card.Def.Effects.Where(e => e.Type != EffectType.Damage).Concat(card.Def.Effects.Where(e => e.Type == EffectType.Damage)))
            {
                var affected = effect.OnSelf ? new List<Unit> { owner } : effect.OnAllies ? AliveUnits(owner.Side) : targets;
                ResolveEffect(owner, effect, affected);
            }
            _moveDest = null;
            if (card.Def.KillRefund > 0 && aliveBefore.Any(t => !t.Alive))
            {
                GainCost(owner, card.Def.KillRefund, "kill");
            }

            DiscardPile.Add(card);
            CheckEnd();
            return PlayResult.Ok;
        }

        public void EndTurn()
        {
            if (Result != BattleResult.Ongoing) return;

            foreach (var healer in AliveUnits(Side.Player).Where(u => u.Passive == PassiveKind.WuQin))
            {
                var low = AliveUnits(Side.Player).Where(u => u.Hp < u.MaxHp).OrderBy(u => (double)u.Hp / u.MaxHp).FirstOrDefault();
                if (low == null) continue;
                HealUnit(healer, low, Passives.WuQinHeal);
                Emit(EventType.PassiveTriggered, healer.Id, low.Id, 0, Passives.Name(PassiveKind.WuQin));
            }
            TickBurn(Side.Player);
            if (CheckEnd()) return;

            RunEnemyPhase();
            if (Result != BattleResult.Ongoing) return;

            TickBurn(Side.Enemy);
            if (CheckEnd()) return;

            EndRound();
            if ((Setup.Objective == Objective.Escort || Setup.Objective == Objective.Defend)
                && Setup.SurviveTurns > 0 && Turn >= Setup.SurviveTurns)
            {
                Result = BattleResult.Won;
                Emit(EventType.BattleEnd, -1, -1, 0, Result.ToString());
                return;
            }
            if (Setup.TurnLimit > 0 && Turn >= Setup.TurnLimit)
            {
                Result = BattleResult.Lost;
                Emit(EventType.BattleEnd, -1, -1, 0, "turn limit");
                return;
            }
            StartPlayerTurn();
        }

        private void StartPlayerTurn()
        {
            Turn++;
            Cost = Math.Min(Cost + Setup.CostPerTurn, Setup.CostCap);
            Emit(EventType.TurnStart, -1, -1, Turn, "");

            int draw = Turn == 1 ? _firstTurnDraw : Setup.DrawPerTurn;
            foreach (var u in AliveUnits(Side.Player).Where(u => u.Passive == PassiveKind.JuZhong))
                if (Turn >= 2) { draw++; Emit(EventType.PassiveTriggered, u.Id, -1, 1, Passives.Name(u.Passive)); }
            for (int i = 0; i < draw && DrawOne(); i++) { }

            if (AliveUnits(Side.Enemy).Any(e => e.Statuses.ContainsKey(StatusType.Taunt)))
                foreach (var u in AliveUnits(Side.Player).Where(u => u.Passive == PassiveKind.WanRenDi))
                {
                    u.Buffs.Add(new Buff { Type = StatusType.DefUp, Power = Passives.WanRenDiDef, Turns = 1 });
                    Emit(EventType.PassiveTriggered, u.Id, u.Id, Passives.WanRenDiDef, Passives.Name(u.Passive));
                }
        }

        private void RunEnemyPhase()
        {
            foreach (var enemy in AliveUnits(Side.Enemy))
            {
                if (!enemy.Alive) continue;

                if (enemy.Charging)
                {
                    enemy.ChargeLeft--;
                    if (enemy.ChargeLeft <= 0) ReleaseCharge(enemy);
                }
                else
                {
                    var intent = GetIntent(enemy);
                    switch (intent.Type)
                    {
                        case Intent.Kind.Charge:
                            enemy.Charging = true;
                            enemy.ChargeLeft = Math.Max(1, enemy.ChargeTurns);
                            Emit(EventType.EnemyCharge, enemy.Id, -1, enemy.ChargeLeft, "");
                            break;
                        case Intent.Kind.Attack:
                            if (intent.MoveTo != null) RelocateEnemy(enemy, intent.MoveTo.Value);
                            Emit(EventType.EnemyAttack, enemy.Id, intent.Target!.Id, 0, "");
                            DealAttackDamage(enemy, intent.Target!, enemy.Magical ? DamageKind.Magical : DamageKind.Physical, enemy.AttackMultiplier);
                            enemy.IdleActions++;
                            break;
                        case Intent.Kind.Move:
                            RelocateEnemy(enemy, intent.MoveTo!.Value);
                            enemy.IdleActions++;
                            break;
                    }
                }
                if (CheckEnd()) return;
            }
        }

        private void ReleaseCharge(Unit enemy)
        {
            enemy.Charging = false;
            enemy.IdleActions = 0;
            var kind = enemy.Magical ? DamageKind.Magical : DamageKind.Physical;
            foreach (var hero in AliveUnits(Side.Player))
            {
                if (!hero.Alive) continue;
                Emit(EventType.EnemyAttack, enemy.Id, hero.Id, 1, "big");
                DealAttackDamage(enemy, hero, kind, enemy.ChargePower);
            }
        }

        private void EndRound()
        {
            foreach (var unit in Units.Where(u => u.Alive))
            {
                foreach (var b in unit.Buffs) b.Turns--;
                unit.Buffs.RemoveAll(b => b.Turns <= 0);
                foreach (var b in unit.DefBreaks) b.Turns--;
                unit.DefBreaks.RemoveAll(b => b.Turns <= 0);
                if (unit.Statuses.TryGetValue(StatusType.Taunt, out var taunt) && --taunt.Turns <= 0)
                    unit.Statuses.Remove(StatusType.Taunt);
            }
        }

        private void TickBurn(Side side)
        {
            foreach (var unit in AliveUnits(side))
            {
                if (!unit.Alive || !unit.Statuses.TryGetValue(StatusType.Burn, out var burn)) continue;
                int dmg = burn.Power;
                burn.Power = (int)Math.Floor(burn.Power * (1.0 - BurnDecay));
                if (burn.Power <= 0) unit.Statuses.Remove(StatusType.Burn);
                if (dmg > 0) ApplyDamage(null, unit, dmg, "Burn");
            }
        }

        private void ResolveEffect(Unit owner, EffectDef effect, List<Unit> affected)
        {
            switch (effect.Type)
            {
                case EffectType.Damage:
                    foreach (var t in affected.ToList())
                        if (t.Alive) DealAttackDamage(owner, t, effect.Kind, effect.Multiplier * ConditionalMultiplier(effect, t));
                    break;
                case EffectType.Heal:
                    foreach (var t in affected)
                        if (t.Alive) HealUnit(owner, t, effect.Multiplier);
                    break;
                case EffectType.Shield:
                    foreach (var t in affected)
                    {
                        if (!t.Alive) continue;
                        int amount = DamageCalc.Scale(owner.EffectiveInt, effect.Multiplier);
                        t.Shield += amount;
                        Emit(EventType.Shield, owner.Id, t.Id, amount, "");
                    }
                    break;
                case EffectType.ApplyStatus:
                    foreach (var t in affected)
                    {
                        if (!t.Alive) continue;
                        ApplyStatus(owner, t, effect);
                    }
                    break;
                case EffectType.Draw:
                    int drawn = 0;
                    for (int i = 0; i < effect.Amount && DrawOne(); i++) drawn++;
                    Emit(EventType.Draw, owner.Id, -1, drawn, "");
                    break;
                case EffectType.Move:
                    if (_moveDest != null && owner.Alive) MoveUnit(owner, _moveDest.Value, EventType.Move);
                    break;
                case EffectType.GainCost:
                    GainCost(owner, effect.Amount, "");
                    break;
            }
        }

        private void ApplyStatus(Unit owner, Unit target, EffectDef effect)
        {
            int value = 0;
            switch (effect.Status)
            {
                case StatusType.Burn:
                {
                    value = DamageCalc.Scale(owner.EffectiveInt, effect.Multiplier * (1.0 - Math.Min(1.0, Math.Max(0.0, target.BurnResist))));
                    if (value <= 0) return;
                    AddBurn(target, value);
                    break;
                }
                case StatusType.ArmorBreak:
                    target.DefBreaks.Add(new DefBreak
                    {
                        Percent = Math.Min(0.95, Math.Max(0.0, effect.Multiplier)),
                        Turns = effect.Amount,
                    });
                    value = (int)Math.Round(effect.Multiplier * 100);
                    break;
                case StatusType.Taunt:
                    target.Statuses[StatusType.Taunt] = new StatusState { Turns = effect.Amount, SourceId = owner.Id };
                    if (target.Charging)
                    {
                        target.Charging = false;
                        target.ChargeLeft = 0;
                        target.IdleActions = target.ChargeInterval;
                        Emit(EventType.EnemyChargeBreak, owner.Id, target.Id, 0, "");
                    }
                    break;
                case StatusType.AtkUp:
                case StatusType.IntUp:
                    value = (int)Math.Round(effect.Multiplier * 100, MidpointRounding.AwayFromZero);
                    target.Buffs.Add(new Buff { Type = effect.Status, Power = value, Turns = effect.Amount });
                    break;
                case StatusType.DefUp:
                case StatusType.DodgeUp:
                case StatusType.CritUp:
                    value = (int)Math.Round(effect.Multiplier, MidpointRounding.AwayFromZero);
                    target.Buffs.Add(new Buff { Type = effect.Status, Power = value, Turns = effect.Amount });
                    break;
            }
            Emit(EventType.StatusApplied, owner.Id, target.Id, effect.Status == StatusType.Burn ? value : effect.Amount, effect.Status.ToString());
        }

        private static double ConditionalMultiplier(EffectDef effect, Unit target)
        {
            double m = 1.0;
            if (effect.BonusPerDebuff > 0)
            {
                int debuffs = (target.BurnStacks > 0 ? 1 : 0) + (target.DefBreaks.Count > 0 ? 1 : 0) + (target.Statuses.ContainsKey(StatusType.Taunt) ? 1 : 0);
                m *= 1.0 + effect.BonusPerDebuff * debuffs;
            }
            if (effect.EliteBossMultiplier > 0 && target.Tier != EnemyTier.Normal) m *= effect.EliteBossMultiplier;
            return m;
        }

        private void HealUnit(Unit healer, Unit target, double multiplier)
        {
            double bonus = AliveUnits(target.Side).Any(u => u.Passive == PassiveKind.RenJun) ? 1.0 + Passives.RenJunHeal : 1.0;
            int amount = DamageCalc.Scale(healer.EffectiveInt, multiplier * bonus);
            int healed = Math.Min(amount, target.MaxHp - target.Hp);
            target.Hp += healed;
            Emit(EventType.Heal, healer.Id, target.Id, healed, "");
        }

        private void DealAttackDamage(Unit attacker, Unit target, DamageKind kind, double multiplier)
        {
            if (!Setup.NoRandomness && Rng.Roll(target.EffectiveDodge))
            {
                Emit(EventType.Dodge, attacker.Id, target.Id, 0, "");
                return;
            }
            int dmg;
            bool crit = false;
            if (kind == DamageKind.Magical)
            {
                dmg = DamageCalc.Magical(attacker.EffectiveInt, multiplier);
            }
            else
            {
                crit = !Setup.NoRandomness && Rng.Roll(attacker.EffectiveCrit);
                double def = target.EffectiveDef;
                if (crit && attacker.Passive == PassiveKind.MeiRan) def *= 1.0 - Passives.MeiRanIgnoreDef;
                dmg = DamageCalc.Physical(attacker.EffectiveAtk, multiplier, def, crit, attacker.Stats.CritDmg);
            }
            ApplyDamage(attacker, target, dmg, crit ? "crit" : "");
        }

        private void ApplyDamage(Unit? source, Unit target, int dmg, string text)
        {
            if (target.Passive == PassiveKind.ChangBan && TauntingSomeone(target))
                dmg = Math.Max(1, (int)Math.Round(dmg * (1.0 - Passives.ChangBanReduce), MidpointRounding.AwayFromZero));
            int absorbed = Math.Min(target.Shield, dmg);
            target.Shield -= absorbed;
            target.Hp -= dmg - absorbed;
            Emit(EventType.Damage, source?.Id ?? -1, target.Id, dmg, text);
            if (target.Hp <= 0)
            {
                Kill(target);
                OnKilled(source, target, dmg, text);
                return;
            }
            CheckPhase(target);
            if (target.Passive == PassiveKind.GangLie && !target.PassiveFired && target.Hp * 2 < target.MaxHp)
            {
                target.PassiveFired = true;
                target.Buffs.Add(new Buff { Type = StatusType.DefUp, Power = Passives.GangLieDef, Turns = Passives.GangLieTurns });
                Emit(EventType.PassiveTriggered, target.Id, target.Id, Passives.GangLieDef, Passives.Name(target.Passive));
            }
        }

        private bool TauntingSomeone(Unit unit)
        {
            foreach (var u in Units)
                if (u.Alive && u.Side != unit.Side && u.Statuses.TryGetValue(StatusType.Taunt, out var t) && t.SourceId == unit.Id) return true;
            return false;
        }

        private void OnKilled(Unit? source, Unit victim, int dmg, string text)
        {
            if (victim.Side == Side.Enemy)
            {
                int burn = victim.BurnStacks;
                var taiping = AliveUnits(Side.Player).FirstOrDefault(u => u.Passive == PassiveKind.TaiPing);
                if (burn > 0 && taiping != null)
                {
                    int share = (int)Math.Floor(burn * Passives.TaiPingShare);
                    var adjacent = AliveUnits(Side.Enemy).Where(e => Position.Distance(e.Pos, victim.Pos) == 1).ToList();
                    if (share > 0 && adjacent.Count > 0)
                    {
                        foreach (var adj in adjacent)
                        {
                            AddBurn(adj, share);
                            Emit(EventType.StatusApplied, taiping.Id, adj.Id, share, StatusType.Burn.ToString());
                        }
                        Emit(EventType.PassiveTriggered, taiping.Id, victim.Id, share, Passives.Name(PassiveKind.TaiPing));
                    }
                }
            }
            if (source == null || source.Side != Side.Player || source == victim) return;
            switch (source.Passive)
            {
                case PassiveKind.RenZhongLvBu:
                    source.Buffs.Add(new Buff { Type = StatusType.CritUp, Power = Passives.LvBuCrit, Turns = Passives.LvBuTurns });
                    Emit(EventType.PassiveTriggered, source.Id, source.Id, Passives.LvBuCrit, Passives.Name(source.Passive));
                    break;
                case PassiveKind.BaiMa:
                    if (source.PassiveTurn == Turn) break;
                    source.PassiveTurn = Turn;
                    GainCost(source, 1, "kill");
                    Emit(EventType.PassiveTriggered, source.Id, -1, 1, Passives.Name(source.Passive));
                    break;
                case PassiveKind.WeiZhen:
                    if (text == "splash" || text == "Burn") break;
                    int splash = (int)Math.Round(dmg * Passives.WeiZhenSplash, MidpointRounding.AwayFromZero);
                    var near = AliveUnits(victim.Side).Where(e => Position.Distance(e.Pos, victim.Pos) == 1).ToList();
                    if (splash <= 0 || near.Count == 0) break;
                    Emit(EventType.PassiveTriggered, source.Id, victim.Id, splash, Passives.Name(source.Passive));
                    foreach (var adj in near) if (adj.Alive) ApplyDamage(source, adj, splash, "splash");
                    break;
            }
        }

        private void CheckPhase(Unit unit)
        {
            var def = unit.Enemy;
            if (def == null || def.PhaseHpPercent <= 0 || unit.Phase > 1) return;
            if (unit.Hp * 100 > unit.MaxHp * def.PhaseHpPercent) return;
            unit.Phase = 2;
            if (def.Phase2ChargeTurns > 0) unit.ChargeTurns = def.Phase2ChargeTurns;
            if (def.Phase2ChargeInterval >= 0) unit.ChargeInterval = def.Phase2ChargeInterval;
            if (def.Phase2ChargePower > 0) unit.ChargePower = def.Phase2ChargePower;
            Emit(EventType.EnemyPhase, unit.Id, unit.Id, unit.Phase, "");
        }

        private void Kill(Unit unit)
        {
            unit.Hp = 0;
            unit.Alive = false;
            unit.Charging = false;
            _board[unit.Pos.Lane, unit.Pos.Row] = null;
            Emit(EventType.Death, -1, unit.Id, 0, "");
            if (unit.Side == Side.Player && unit.Hero != null && unit.Hero.Deck.Count > 0) RemoveHeroCards(unit);
        }

        private void RemoveHeroCards(Unit unit)
        {
            DrawPile.RemoveAll(c => c.Owner == unit);
            Hand.RemoveAll(c => c.Owner == unit);
            DiscardPile.RemoveAll(c => c.Owner == unit);
            foreach (var pile in new[] { DiscardPile, DrawPile, Hand })
            {
                int i = pile.FindIndex(c => c.Owner == null);
                if (i < 0) continue;
                pile.RemoveAt(i);
                break;
            }
        }

        private void RelocateEnemy(Unit enemy, Position dest)
        {
            MoveUnit(enemy, dest, EventType.EnemyMove);
        }

        private void MoveUnit(Unit unit, Position dest, EventType type)
        {
            var from = unit.Pos;
            _board[from.Lane, from.Row] = null;
            _board[dest.Lane, dest.Row] = unit;
            unit.Pos = dest;
            Emit(type, unit.Id, -1, Position.Distance(from, dest), $"{from}->{dest}");
        }

        private void GainCost(Unit source, int amount, string text)
        {
            Cost = Math.Min(Setup.CostCap, Cost + amount);
            Emit(EventType.GainCost, source.Id, -1, amount, text);
        }

        private static void AddBurn(Unit unit, int stacks)
        {
            if (unit.Statuses.TryGetValue(StatusType.Burn, out var burn)) burn.Power += stacks;
            else unit.Statuses[StatusType.Burn] = new StatusState { Power = stacks };
        }

        private bool DrawOne()
        {
            if (DrawPile.Count == 0)
            {
                if (DiscardPile.Count == 0) return false;
                DrawPile.AddRange(DiscardPile);
                DiscardPile.Clear();
                Shuffle(DrawPile);
            }
            var card = DrawPile[0];
            DrawPile.RemoveAt(0);
            if (Hand.Count >= MaxHandSize) DiscardPile.Add(card);
            else Hand.Add(card);
            return true;
        }

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

        private Unit SpawnEnemy(EnemySlot slot)
        {
            var def = slot.Def;
            var unit = CreateUnit(def.Name, Side.Enemy, def.AttackType, ScaleEnemy(def, slot.Level), slot.Pos);
            unit.Tier = def.Tier;
            unit.Level = slot.Level;
            unit.Magical = def.Magical;
            unit.AttackMultiplier = def.AttackMultiplier;
            unit.ChargeTurns = def.ChargeTurns;
            unit.ChargeInterval = def.ChargeInterval;
            unit.ChargePower = def.ChargePower;
            unit.IsObjective = slot.IsObjective;
            unit.BurnResist = def.BurnResist;
            unit.DefId = def.Id;
            unit.Enemy = def;
            unit.ArtId = def.Art != "" ? def.Art : def.Id;
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

        public static Stats ScaleHero(Stats baseStats, int level) => ScaleCore(baseStats, HeroLevelFactor(level), 1.0, 1.0);

        public static Stats ScaleEnemy(EnemyDef def, int level)
        {
            double hpTier = def.Tier == EnemyTier.Elite ? 1.4 : def.Tier == EnemyTier.Boss ? 3.0 : 1.0;
            double powerTier = def.Tier == EnemyTier.Normal ? 1.0 : 1.4;
            return ScaleCore(def.Base, EnemyLevelFactor(level), hpTier, powerTier);
        }

        private static Stats ScaleCore(Stats baseStats, double factor, double hpTier, double powerTier)
        {
            var s = baseStats.Clone();
            s.Hp = (int)Math.Round(s.Hp * factor * hpTier, MidpointRounding.AwayFromZero);
            s.Atk = (int)Math.Round(s.Atk * factor * powerTier, MidpointRounding.AwayFromZero);
            s.Int = (int)Math.Round(s.Int * factor * powerTier, MidpointRounding.AwayFromZero);
            s.Def = (int)Math.Round(s.Def * factor, MidpointRounding.AwayFromZero);
            return s;
        }

        private static List<Unit>? NonEmpty(List<Unit> list) => list.Count == 0 ? null : list;

        private bool CheckEnd()
        {
            if (Result != BattleResult.Ongoing) return true;
            var enemies = Units.Where(u => u.Side == Side.Enemy).ToList();
            bool objectiveDone = Setup.Objective == Objective.KillTarget && enemies.Any(u => u.IsObjective)
                && enemies.Where(u => u.IsObjective).All(u => !u.Alive);
            if (!enemies.Any(u => u.Alive) || objectiveDone)
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
