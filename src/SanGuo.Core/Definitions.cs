using System.Collections.Generic;

namespace SanGuo.Core
{
    public struct Position : System.IEquatable<Position>
    {
        public int Lane;
        public int Row;

        public Position(int lane, int row)
        {
            Lane = lane;
            Row = row;
        }

        public static int Distance(Position a, Position b) => System.Math.Abs(a.Lane - b.Lane) + System.Math.Abs(a.Row - b.Row);

        public bool Equals(Position other) => Lane == other.Lane && Row == other.Row;
        public override bool Equals(object? obj) => obj is Position p && Equals(p);
        public override int GetHashCode() => Lane * 31 + Row;
        public static bool operator ==(Position a, Position b) => a.Equals(b);
        public static bool operator !=(Position a, Position b) => !a.Equals(b);

        public override string ToString() => $"({Lane},{Row})";
    }

    public sealed class Stats
    {
        public int Hp;
        public int Atk;
        public int Def;
        public int Dodge;
        public int Move = 1;
        public int Int;
        public int Crit;
        public int CritDmg = 150;
        public int Range = 1;

        public Stats Clone() => (Stats)MemberwiseClone();
    }

    public sealed class EffectDef
    {
        public EffectType Type;
        public double Multiplier;
        public DamageKind Kind = DamageKind.Physical;
        public StatusType Status;
        public int Amount;
        public bool OnSelf;
        public bool OnAllies;
        public double BonusPerDebuff;
        public double EliteBossMultiplier;

        public EffectDef Clone() => (EffectDef)MemberwiseClone();
    }

    public sealed class CardDef
    {
        public string Id = "";
        public string Name = "";
        public bool Basic;
        public int Cost;
        public TargetRule Target = TargetRule.Enemy;
        public Shape Shape = Shape.Single;
        public List<EffectDef> Effects = new List<EffectDef>();
        public bool Unlimited;
        public int KillRefund;

        public static CardDef CreateMove() => new CardDef
        {
            Id = "move", Name = "移動", Basic = true, Cost = 0, Target = TargetRule.MoveDest,
            Effects = new List<EffectDef> { new EffectDef { Type = EffectType.Move, OnSelf = true } },
        };
    }

    public sealed class HeroDef
    {
        public string Id = "";
        public string Name = "";
        public Role Role;
        public Rarity Rarity = Rarity.R;
        public AttackType AttackType = AttackType.Melee;
        public Stats Base = new Stats();
        public List<CardDef> Deck = new List<CardDef>();
        public PassiveKind Passive;
        public PassiveKind ActivePassive;
        public HeroFocus Focus;
    }

    public enum HeroFocus { General, Farming, Boss }

    public sealed class EnemyDef
    {
        public string Id = "";
        public string Name = "";
        public string Art = "";
        public Role Role = Role.Warrior;
        public EnemyTier Tier = EnemyTier.Normal;
        public AttackType AttackType = AttackType.Melee;
        public bool Magical;
        public Stats Base = new Stats();
        public double AttackMultiplier = 1.0;
        public int ChargeTurns;
        public int ChargeInterval = 1;
        public double ChargePower = 2.0;
        public double BurnResist;

        public int PhaseHpPercent;
        public int Phase2ChargeTurns;
        public int Phase2ChargeInterval = -1;
        public double Phase2ChargePower;
    }

    public sealed class HeroSlot
    {
        public HeroDef Def;
        public Position Pos;
        public int Level = 1;
        public bool IsProtected;
        public int StartHpPercent = 100;

        public HeroSlot(HeroDef def, Position pos, int level = 1)
        {
            Def = def;
            Pos = pos;
            Level = level;
        }
    }

    public sealed class EnemySlot
    {
        public EnemyDef Def;
        public Position Pos;
        public int Level = 1;
        public bool IsObjective;

        public EnemySlot(EnemyDef def, Position pos, int level = 1)
        {
            Def = def;
            Pos = pos;
            Level = level;
        }
    }

    public sealed class BattleSetup
    {
        public int Lanes = 5;
        public int Rows = 5;
        public const int FormationMinLane = 1, FormationMaxLane = 3, FormationMinRow = 3, FormationMaxRow = 4;
        public ulong Seed = 1;
        public int FirstTurnRandom = 5;
        public int FirstTurnMoves = 2;
        public int CostPerTurn = 3;
        public int CostCap = 10;
        public int DrawPerTurn = 3;
        public int TurnLimit;
        public Objective Objective = Objective.Annihilate;
        public int SurviveTurns;
        public bool AutoAllowed = true;
        public bool FormationLocked;
        public List<string> ScriptedDraw = new List<string>();
        public bool NoRandomness;
        public List<HeroSlot> Heroes = new List<HeroSlot>();
        public List<EnemySlot> Enemies = new List<EnemySlot>();
    }
}
