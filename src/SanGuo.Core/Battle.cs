using System;
using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core
{
    public sealed partial class Battle
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
