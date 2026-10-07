using System.Collections.Generic;

namespace SanGuo.Core
{
    public sealed class StatusState
    {
        /// <summary>Burn / Poison：每回合持續傷害；DefUp / AtkUp / CritUp：加成百分比（30 = +30%）；其餘狀態未使用。</summary>
        public int Power;
        public int Turns;
    }

    /// <summary>一筆破甲：降低防禦的百分比與剩餘回合；多筆各自獨立存在、各自到期。</summary>
    public sealed class DefBreak
    {
        /// <summary>降低防禦的比例（0.4 = -40%）。</summary>
        public double Percent;
        public int Turns;
    }

    public sealed class Unit
    {
        public int Id;
        public string Name = "";
        /// <summary>資料表 id（HeroDef.Id / EnemyDef.Id），表現層用來挑選模型。</summary>
        public string DefId = "";
        public Side Side;
        public AttackType AttackType;
        public Stats Stats = new Stats();
        public int Hp;
        public int Armor;
        public Position Pos;
        public bool Alive = true;
        /// <summary>保護目標（陣亡即失敗）。</summary>
        public bool Protected;
        /// <summary>僅敵人使用：普通攻擊倍率。</summary>
        public double AttackMultiplier = 1.0;
        public EnemyAbility Ability;
        public EnemyDef? SummonDef;
        public int SummonCap = 6;
        /// <summary>每 N 次行動召喚一次（1 = 每次行動都召喚）。</summary>
        public int SummonEvery = 1;
        /// <summary>已執行的行動次數（被昏亂跳過的不算），用來排行動週期。</summary>
        public int Actions;
        public double AbilityPower;
        public int StunGauge;
        public int StunGaugeMax = 100;
        public double StunGrowth = 0.5;
        /// <summary>敵人蓄力中：下一次行動放大招。</summary>
        public bool Charging;
        public HeroDef? Hero;
        public Dictionary<StatusType, StatusState> Statuses = new Dictionary<StatusType, StatusState>();

        public int MaxHp => Stats.Hp;

        public bool Has(StatusType type) => Statuses.ContainsKey(type);

        /// <summary>破甲清單（不是層數）：每一筆有自己的百分比與持續回合。</summary>
        public List<DefBreak> DefBreaks = new List<DefBreak>();

        /// <summary>有效防禦：基礎防禦 × 防禦增益 × 每一筆破甲剩餘比例（1 - 百分比）連乘。</summary>
        public double EffectiveDef
        {
            get
            {
                double def = Stats.Def * (1.0 + BuffPercent(StatusType.DefUp) / 100.0);
                foreach (var b in DefBreaks) def *= 1.0 - b.Percent;
                return def;
            }
        }

        /// <summary>有效攻擊力：基礎攻擊 × 攻擊增益。傷害 / 治療 / 護甲 / 狀態威力都以它計算。</summary>
        public int EffectiveAtk => (int)System.Math.Round(Stats.Atk * (1.0 + BuffPercent(StatusType.AtkUp) / 100.0), System.MidpointRounding.AwayFromZero);

        /// <summary>法系（法師 / 醫療 / 軍師，以及會治療的敵人）：傷害、治療、護甲、增減益強度吃謀略而非攻擊力。</summary>
        public bool IsCaster => Hero != null
            ? (Hero.Role == Role.Mage || Hero.Role == Role.Healer || Hero.Role == Role.Strategist)
            : Ability.HasFlag(EnemyAbility.Healer);

        /// <summary>有效謀略：基礎謀略 × 攻擊增益（鼓舞同時提升法系的威力）。</summary>
        public int EffectiveInt => (int)System.Math.Round(Stats.Int * (1.0 + BuffPercent(StatusType.AtkUp) / 100.0), System.MidpointRounding.AwayFromZero);

        /// <summary>施放威力：法系吃有效謀略，其餘吃有效攻擊。治療 / 護甲 / 狀態威力都以它計算。</summary>
        public int EffectivePower => IsCaster ? EffectiveInt : EffectiveAtk;

        /// <summary>增減益強度倍率：法系 = 謀略 / <see cref="DamageCalc.CasterReference"/>，其餘 1。</summary>
        public double StatusPotency => IsCaster ? Stats.Int / (double)DamageCalc.CasterReference : 1.0;

        /// <summary>普通攻擊 / 敵人的攻擊距離：近戰 1 格、遠程 3 格。</summary>
        public int AttackRange => AttackType == AttackType.Melee ? MeleeRange : RangedRange;
        public const int MeleeRange = 1;
        public const int RangedRange = 3;

        /// <summary>有效爆擊率（百分比）：基礎爆擊 + 爆擊增益。</summary>
        public int EffectiveCrit => Stats.Crit + BuffPercent(StatusType.CritUp);

        private int BuffPercent(StatusType type) => Statuses.TryGetValue(type, out var s) ? s.Power : 0;

        public override string ToString() => $"{Name}#{Id}@{Pos}";
    }

    public sealed class CardInstance
    {
        public int Id;
        public CardDef Def;
        public Unit Owner;

        public CardInstance(int id, CardDef def, Unit owner)
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
        /// <summary>事件發生當下來源 / 目標的陣營與位置（單位死亡或移動後仍可定位，供表現層播放特效）。</summary>
        public Side SourceSide;
        public Side TargetSide;
        public Position SourcePos;
        public Position TargetPos;

        public override string ToString() => $"{Type} {Source}->{Target} {Value} {Text}";
    }

    public sealed class Intent
    {
        public enum Kind { Attack, Move, Stunned, Heal, Charge, Summon, None }

        public Kind Type;
        /// <summary>true = 這次攻擊是蓄力後的大招。</summary>
        public bool Big;
        public Unit? Target;
        public Position? MoveTo;
    }
}
