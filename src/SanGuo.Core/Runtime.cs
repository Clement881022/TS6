using System.Collections.Generic;

namespace SanGuo.Core
{
    public sealed class StatusState
    {
        /// <summary>每回合持續傷害（Burn / Poison）的數值，其餘狀態未使用。</summary>
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
        /// <summary>僅敵人使用：普通攻擊倍率。</summary>
        public double AttackMultiplier = 1.0;
        public HeroDef? Hero;
        public Dictionary<StatusType, StatusState> Statuses = new Dictionary<StatusType, StatusState>();

        public int MaxHp => Stats.Hp;

        public bool Has(StatusType type) => Statuses.ContainsKey(type);

        /// <summary>破甲清單（不是層數）：每一筆有自己的百分比與持續回合。</summary>
        public List<DefBreak> DefBreaks = new List<DefBreak>();

        /// <summary>有效防禦：基礎防禦 × 每一筆破甲剩餘比例（1 - 百分比）連乘。</summary>
        public double EffectiveDef
        {
            get
            {
                double def = Stats.Def;
                foreach (var b in DefBreaks) def *= 1.0 - b.Percent;
                return def;
            }
        }

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
        public enum Kind { Attack, Move, Stunned, None }

        public Kind Type;
        public Unit? Target;
        public Position? MoveTo;
    }
}
