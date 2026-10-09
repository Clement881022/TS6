using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core
{
    /// <summary>單一狀態（嘲諷、燃燒）。燃燒的 Power 是層數且無持續回合；嘲諷的 SourceId 是施放者。</summary>
    public sealed class StatusState
    {
        public int Power;
        public int Turns;
        /// <summary>嘲諷的施放者單位 id。</summary>
        public int SourceId = -1;
    }

    /// <summary>一筆增益（防禦 / 攻擊 / 謀略 / 閃避）：各自獨立計時、可疊加。</summary>
    public sealed class Buff
    {
        public StatusType Type;
        /// <summary>DefUp / DodgeUp：固定加成值；AtkUp / IntUp：加成百分比（25 = +25%）。</summary>
        public int Power;
        public int Turns;
    }

    /// <summary>一筆破甲：降低防禦的比例與剩餘回合；同名破甲不疊加，取最高值生效，各筆仍各自計時。</summary>
    public sealed class DefBreak
    {
        /// <summary>降低防禦的比例（0.25 = -25%）。</summary>
        public double Percent;
        public int Turns;
    }

    public sealed class Unit
    {
        public int Id;
        public string Name = "";
        /// <summary>資料表 id（HeroDef.Id / EnemyDef.Id），表現層用來挑選模型。</summary>
        public string DefId = "";
        /// <summary>表現層用的模型 id（敵人借用模型時與 <see cref="DefId"/> 不同）。</summary>
        public string ArtId = "";
        public Side Side;
        public AttackType AttackType;
        public Stats Stats = new Stats();
        public int Hp;
        /// <summary>護盾：視為額外生命值，先於生命值吸收物理與法術傷害，無持續時間，吸收完畢才消失。</summary>
        public int Shield;
        public Position Pos;
        public bool Alive = true;
        /// <summary>保護目標（陣亡即失敗）。</summary>
        public bool Protected;

        // ---- 僅敵人使用 ----
        public EnemyTier Tier;
        public int Level = 1;
        public bool Magical;
        public double AttackMultiplier = 1.0;
        public int ChargeTurns;
        public int ChargeInterval = 1;
        public double ChargePower = 2.0;
        /// <summary>蓄力中。</summary>
        public bool Charging;
        /// <summary>蓄力剩餘行動數；每次行動 -1，歸零的那次行動釋放大招。</summary>
        public int ChargeLeft;
        /// <summary>距離上次蓄力 / 開場以來的普通行動次數。</summary>
        public int IdleActions;
        public bool IsObjective;
        /// <summary>燃燒抗性（0–1，見 <see cref="EnemyDef.BurnResist"/>）。</summary>
        public double BurnResist;
        /// <summary>目前階段（1 起算）；Boss 跌破 <see cref="EnemyDef.PhaseHpPercent"/> 後變為 2。</summary>
        public int Phase = 1;
        /// <summary>敵人的資料定義（階段切換用）；武將為 null。</summary>
        public EnemyDef? Enemy;

        public HeroDef? Hero;
        /// <summary>單槽狀態：Taunt、Burn。</summary>
        public Dictionary<StatusType, StatusState> Statuses = new Dictionary<StatusType, StatusState>();
        public List<Buff> Buffs = new List<Buff>();
        public List<DefBreak> DefBreaks = new List<DefBreak>();

        public int MaxHp => Stats.Hp;

        public bool Has(StatusType type)
        {
            switch (type)
            {
                case StatusType.ArmorBreak: return DefBreaks.Count > 0;
                case StatusType.DefUp:
                case StatusType.AtkUp:
                case StatusType.IntUp:
                case StatusType.DodgeUp:
                case StatusType.CritUp:
                    return Buffs.Any(b => b.Type == type);
                default: return Statuses.ContainsKey(type);
            }
        }

        /// <summary>燃燒層數。</summary>
        public int BurnStacks => Statuses.TryGetValue(StatusType.Burn, out var s) ? s.Power : 0;

        /// <summary>某種增益的總加成（所有同種增益相加）。</summary>
        public int BuffTotal(StatusType type)
        {
            int total = 0;
            foreach (var b in Buffs) if (b.Type == type) total += b.Power;
            return total;
        }

        /// <summary>目前生效的破甲比例：多筆破甲取最高值。</summary>
        public double ArmorBreakPercent => DefBreaks.Count == 0 ? 0.0 : DefBreaks.Max(b => b.Percent);

        /// <summary>有效防禦：(基礎防禦 + 防禦增益加總) × (1 - 生效的破甲比例)。</summary>
        public double EffectiveDef => System.Math.Max(0.0, Stats.Def + BuffTotal(StatusType.DefUp)) * (1.0 - ArmorBreakPercent);

        /// <summary>有效攻擊力：基礎攻擊 × (1 + 攻擊增益加總)。攻擊增益僅提升物攻。</summary>
        public int EffectiveAtk => (int)System.Math.Round(Stats.Atk * (1.0 + BuffTotal(StatusType.AtkUp) / 100.0), System.MidpointRounding.AwayFromZero);

        /// <summary>有效謀略：基礎謀略 × (1 + 謀略增益加總)。</summary>
        public int EffectiveInt => (int)System.Math.Round(Stats.Int * (1.0 + BuffTotal(StatusType.IntUp) / 100.0), System.MidpointRounding.AwayFromZero);

        /// <summary>有效爆擊率（含爆擊率增益與「美髯公」+15，上限 100）。</summary>
        public int EffectiveCrit => System.Math.Min(DamageCalc.CritCap,
            Stats.Crit + BuffTotal(StatusType.CritUp) + (Passive == PassiveKind.MeiRan ? Passives.MeiRanCrit : 0));

        /// <summary>入戰時已解鎖的被動（敵人與未滿突的武將為 None）。</summary>
        public PassiveKind Passive => Hero?.ActivePassive ?? PassiveKind.None;
        /// <summary>只觸發一次的被動是否已觸發（剛烈不屈）。</summary>
        public bool PassiveFired;
        /// <summary>「白馬將軍」上次觸發的回合。</summary>
        public int PassiveTurn;

        /// <summary>有效閃避（含閃避增益，上限 <see cref="DamageCalc.DodgeCap"/>）。</summary>
        public int EffectiveDodge => System.Math.Min(DamageCalc.DodgeCap, Stats.Dodge + BuffTotal(StatusType.DodgeUp));

        /// <summary>攻擊範圍（曼哈頓格距）。</summary>
        public int AttackRange => Stats.Range;

        public override string ToString() => $"{Name}#{Id}@{Pos}";
    }

    public sealed class CardInstance
    {
        public int Id;
        public CardDef Def;
        /// <summary>持有者；null = 全隊通用卡（移動牌），打出時由玩家指定要移動的武將。</summary>
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
        /// <summary>事件發生當下來源 / 目標的陣營與位置（單位死亡或移動後仍可定位，供表現層播放特效）。</summary>
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
        /// <summary>true = 這次行動會釋放蓄力大招（僅引擎使用；預告 UI 在蓄力期間只顯示「蓄力中」）。</summary>
        public bool Big;
        public Unit? Target;
        public Position? MoveTo;
    }
}
