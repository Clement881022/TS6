using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core
{
    public sealed class StatusState
    {
        public int Power;
        public int Turns;
        public int SourceId = -1;
    }

    public sealed class Buff
    {
        public StatusType Type;
        public int Power;
        public int Turns;
    }

    public sealed class DefBreak
    {
        public double Percent;
        public int Turns;
    }

    public sealed class Unit
    {
        public int Id;
        public string Name = "";
        public string DefId = "";
        public string ArtId = "";
        public Side Side;
        public AttackType AttackType;
        public Stats Stats = new Stats();
        public int Hp;
        public int Shield;
        public Position Pos;
        public bool Alive = true;
        public bool Protected;

        public EnemyTier Tier;
        public int Level = 1;
        public bool Magical;
        public double AttackMultiplier = 1.0;
        public int ChargeTurns;
        public int ChargeInterval = 1;
        public double ChargePower = 2.0;
        public bool Charging;
        public int ChargeLeft;
        public int IdleActions;
        public bool IsObjective;
        public double BurnResist;
        public int Phase = 1;
        public EnemyDef? Enemy;

        public HeroDef? Hero;
        public Dictionary<StatusType, StatusState> Statuses = new Dictionary<StatusType, StatusState>();
        public List<Buff> Buffs = new List<Buff>();
        public List<DefBreak> DefBreaks = new List<DefBreak>();

        public int MaxHp => Stats.Hp;

        public int BurnStacks => Statuses.TryGetValue(StatusType.Burn, out var s) ? s.Power : 0;

        public int BuffTotal(StatusType type)
        {
            int total = 0;
            foreach (var b in Buffs) if (b.Type == type) total += b.Power;
            return total;
        }

        public double ArmorBreakPercent => DefBreaks.Count == 0 ? 0.0 : DefBreaks.Max(b => b.Percent);

        public double EffectiveDef => System.Math.Max(0.0, Stats.Def + BuffTotal(StatusType.DefUp)) * (1.0 - ArmorBreakPercent);

        public int EffectiveAtk => (int)System.Math.Round(Stats.Atk * (1.0 + BuffTotal(StatusType.AtkUp) / 100.0), System.MidpointRounding.AwayFromZero);

        public int EffectiveInt => (int)System.Math.Round(Stats.Int * (1.0 + BuffTotal(StatusType.IntUp) / 100.0), System.MidpointRounding.AwayFromZero);

        public int EffectiveCrit => System.Math.Min(DamageCalc.CritCap,
            Stats.Crit + BuffTotal(StatusType.CritUp) + (Passive == PassiveKind.MeiRan ? Passives.MeiRanCrit : 0));

        public PassiveKind Passive => Hero?.ActivePassive ?? PassiveKind.None;
        public bool PassiveFired;
        public int PassiveTurn;

        public int EffectiveDodge => System.Math.Min(DamageCalc.DodgeCap, Stats.Dodge + BuffTotal(StatusType.DodgeUp));

        public int AttackRange => Stats.Range;

        public override string ToString() => $"{Name}#{Id}@{Pos}";
    }

    public sealed class CardInstance
    {
        public int Id;
        public CardDef Def;
        public Unit? Owner;

        public CardInstance(int id, CardDef def, Unit? owner)
        {
            Id = id;
            Def = def;
            Owner = owner;
        }

        public override string ToString() => $"{Def.Name}#{Id}";
    }

    public sealed class BattleEvent
    {
        public EventType Type;
        public int Source = -1;
        public int Target = -1;
        public int Value;
        public string Text = "";
        public Side SourceSide;
        public Side TargetSide;
        public Position SourcePos;
        public Position TargetPos;

        public override string ToString() => $"{Type} {Source}->{Target} {Value} {Text}";
    }

    public sealed class Intent
    {
        public enum Kind { Attack, Move, Charge, Charging, None }

        public Kind Type;
        public Unit? Target;
        public Position? MoveTo;
    }
}
