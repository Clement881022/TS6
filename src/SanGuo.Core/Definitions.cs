using System.Collections.Generic;

namespace SanGuo.Core
{
    public struct Position
    {
        /// <summary>路（0 起算，雙方同路相對）。</summary>
        public int Lane;
        /// <summary>排（0 = 前排，數字越大越後排）。</summary>
        public int Row;

        public Position(int lane, int row)
        {
            Lane = lane;
            Row = row;
        }

        public override string ToString() => $"({Lane},{Row})";
    }

    public sealed class Stats
    {
        public int Hp;
        public int Atk;
        public int Def;
        /// <summary>閃避，百分比。</summary>
        public int Dodge;
        /// <summary>速度 = 移動卡可移動的格數（1–3，隨職業固定）。</summary>
        public int Speed = 1;
        /// <summary>爆擊率，百分比。</summary>
        public int Crit;
        /// <summary>爆擊傷害，百分比（150 = 1.5 倍）。</summary>
        public int CritDmg = 150;

        public Stats Clone() => (Stats)MemberwiseClone();
    }

    public sealed class EffectDef
    {
        public EffectType Type;
        /// <summary>傷害 / 治療 / 護甲 / 狀態威力的攻擊力倍率。</summary>
        public double Multiplier;
        public StatusType Status;
        /// <summary>狀態持續回合；Draw / GainCost 則為張數 / 點數。</summary>
        public int Amount;
        /// <summary>true 時作用於施放者自己，而非卡牌的目標。</summary>
        public bool OnSelf;
    }

    public sealed class CardDef
    {
        public string Id = "";
        public string Name = "";
        /// <summary>基礎牌（普攻 / 防禦 / 治療）；false 為武將專屬技能牌。僅用於顯示與日後強化分類。</summary>
        public bool Basic;
        public int Cost;
        public CardKeywords Keywords;
        public TargetRule Target = TargetRule.EnemyFront;
        public Shape Shape = Shape.Single;
        public List<EffectDef> Effects = new List<EffectDef>();
    }

    public sealed class HeroDef
    {
        public string Id = "";
        public string Name = "";
        public Role Role;
        public Rarity Rarity = Rarity.R;
        public AttackType AttackType = AttackType.Melee;
        public Stats Base = new Stats();
        /// <summary>固定套牌（同一張卡可重複出現）。</summary>
        public List<CardDef> Deck = new List<CardDef>();
    }

    public sealed class EnemyDef
    {
        public string Id = "";
        public string Name = "";
        public AttackType AttackType = AttackType.Melee;
        public Stats Base = new Stats();
        /// <summary>普通攻擊的攻擊力倍率。</summary>
        public double AttackMultiplier = 1.0;
    }

    public sealed class HeroSlot
    {
        public HeroDef Def;
        public Position Pos;
        public int Level = 1;

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

        public EnemySlot(EnemyDef def, Position pos)
        {
            Def = def;
            Pos = pos;
        }
    }

    public sealed class BattleSetup
    {
        public int Lanes = 5;
        public int Rows = 2;
        public ulong Seed = 1;
        public int HandSize = 5;
        public int CostPerTurn = 3;
        public int CostCap = 10;
        /// <summary>全隊共用移動按鈕（已停用，預設 0 次；站位改為戰前編隊，推拉 / 換位日後做成特定武將的技能）。</summary>
        public int MoveCost = 1;
        public int MovesPerTurn = 0;
        /// <summary>0 = 無回合限制。</summary>
        public int TurnLimit;
        public List<HeroSlot> Heroes = new List<HeroSlot>();
        public List<EnemySlot> Enemies = new List<EnemySlot>();
    }
}
