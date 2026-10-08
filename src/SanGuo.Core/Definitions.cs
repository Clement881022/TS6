using System.Collections.Generic;

namespace SanGuo.Core
{
    /// <summary>共用 5x5 棋盤上的格子：Lane = 欄（0–4，左到右）、Row = 列（0–4，上到下；敵方在上、我方在下）。</summary>
    public struct Position : System.IEquatable<Position>
    {
        public int Lane;
        public int Row;

        public Position(int lane, int row)
        {
            Lane = lane;
            Row = row;
        }

        /// <summary>曼哈頓格距。</summary>
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
        /// <summary>閃避，百分比（有效值上限 <see cref="DamageCalc.DodgeCap"/>）。</summary>
        public int Dodge;
        /// <summary>移動力 = 移動牌可移動的格數。</summary>
        public int Move = 1;
        /// <summary>謀略：法術傷害、治療、護盾、燃燒層數的計算基礎。</summary>
        public int Int;
        /// <summary>爆擊率，百分比（上限 100）。</summary>
        public int Crit;
        /// <summary>爆擊傷害，百分比（150 = 1.5 倍），無上限。</summary>
        public int CritDmg = 150;
        /// <summary>攻擊範圍：決定技能可選擇的主目標範圍（曼哈頓格距）。</summary>
        public int Range = 1;

        public Stats Clone() => (Stats)MemberwiseClone();
    }

    public sealed class EffectDef
    {
        public EffectType Type;
        /// <summary>
        /// 威力倍率。Damage = 傷害倍率；Heal / Shield = 謀略倍率；
        /// ApplyStatus：Burn = 燃燒層數的謀略倍率、ArmorBreak = 降低防禦的比例（0.25 = 25%）、
        /// DefUp / DodgeUp = 固定加成值（50 = 防禦 +50）、AtkUp / IntUp = 加成比例（0.25 = +25%）、Taunt 不使用。
        /// </summary>
        public double Multiplier;
        /// <summary>Damage 的傷害類型。</summary>
        public DamageKind Kind = DamageKind.Physical;
        public StatusType Status;
        /// <summary>狀態持續回合；Draw / GainCost 則為張數 / 點數。</summary>
        public int Amount;
        /// <summary>true 時作用於施放者自己，而非卡牌的目標。</summary>
        public bool OnSelf;

        public EffectDef Clone() => (EffectDef)MemberwiseClone();
    }

    public sealed class CardDef
    {
        public string Id = "";
        public string Name = "";
        /// <summary>基本攻擊卡（每名武將 3 張）；false 為職業特殊卡。僅用於顯示與分類。</summary>
        public bool Basic;
        public int Cost;
        public TargetRule Target = TargetRule.Enemy;
        public Shape Shape = Shape.Single;
        public List<EffectDef> Effects = new List<EffectDef>();

        /// <summary>通用移動牌（0 費、不屬於任何武將）：每名武將在開局時洗入一張；打出時指定一名武將與目的地，格數 = 該武將移動力。</summary>
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
        /// <summary>固定套牌：3 張基本攻擊 + 2 張職業特殊卡（同一張卡可重複出現）。</summary>
        public List<CardDef> Deck = new List<CardDef>();
    }

    public sealed class EnemyDef
    {
        public string Id = "";
        public string Name = "";
        /// <summary>借用的模型 / 立繪 id（空字串 = 用 <see cref="Id"/>）；新敵人尚無專屬模型時使用。</summary>
        public string Art = "";
        /// <summary>職業體系，讓玩家辨識敵人的定位（GDD 04 §2.1）。</summary>
        public Role Role = Role.Warrior;
        public EnemyTier Tier = EnemyTier.Normal;
        public AttackType AttackType = AttackType.Melee;
        /// <summary>true = 法術攻擊（吃謀略、無視防禦、不爆擊）。</summary>
        public bool Magical;
        /// <summary>同職業武將 1 級基準的數值；實際強度由 <see cref="EnemySlot.Level"/> 與層級倍率縮放。</summary>
        public Stats Base = new Stats();
        /// <summary>普通攻擊的倍率。</summary>
        public double AttackMultiplier = 1.0;
        /// <summary>蓄力回合數（0 = 沒有蓄力大招）。開始蓄力後經過這麼多次行動，於下一次輪到行動時釋放。</summary>
        public int ChargeTurns;
        /// <summary>兩次蓄力之間的普通行動次數（開場後也先普通行動這麼多次才開始蓄力）。</summary>
        public int ChargeInterval = 1;
        /// <summary>蓄力大招的倍率；大招攻擊我方全體存活武將。</summary>
        public double ChargePower = 2.0;

        // ---- Boss 第二階段（GDD 04 §2.2：切換條件與效果由各 Boss 決定；1.0 只調整蓄力參數）----

        /// <summary>生命降到這個百分比以下時進入第二階段（0 = 沒有階段）。只切換一次。</summary>
        public int PhaseHpPercent;
        /// <summary>第二階段的蓄力回合數（0 = 沿用）。</summary>
        public int Phase2ChargeTurns;
        /// <summary>第二階段兩次蓄力之間的普通行動次數（-1 = 沿用）。</summary>
        public int Phase2ChargeInterval = -1;
        /// <summary>第二階段的蓄力大招倍率（0 = 沿用）。</summary>
        public double Phase2ChargePower;
    }

    public sealed class HeroSlot
    {
        public HeroDef Def;
        public Position Pos;
        public int Level = 1;
        /// <summary>保護目標：這個單位陣亡就算失敗（護送 / 守城關卡）。</summary>
        public bool IsProtected;
        /// <summary>開局血量百分比（100 = 滿血；護送關卡的傷者用）。</summary>
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
        /// <summary>敵人等級：只縮放屬性（見 <see cref="Battle.EnemyLevelFactor"/>）。</summary>
        public int Level = 1;
        /// <summary>擊殺指定目標關卡的目標。</summary>
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
        /// <summary>棋盤（敵我共用）：5 欄 × 5 列；敵方起始在上方、我方在下方。</summary>
        public int Lanes = 5;
        public int Rows = 5;
        /// <summary>我方入場區：橫 3 格 × 直 2 格（欄 1–3、列 3–4），共 6 格。</summary>
        public const int FormationMinLane = 1, FormationMaxLane = 3, FormationMinRow = 3, FormationMaxRow = 4;
        public ulong Seed = 1;
        /// <summary>第 1 回合隨機抽幾張，另外固定抽 <see cref="FirstTurnMoves"/> 張移動牌；之後每回合抽 <see cref="DrawPerTurn"/> 張。</summary>
        public int FirstTurnRandom = 5;
        public int FirstTurnMoves = 2;
        public int CostPerTurn = 3;
        public int CostCap = 10;
        public int DrawPerTurn = 3;
        /// <summary>0 = 無回合限制；到達回合數仍未獲勝即失敗（限時）。</summary>
        public int TurnLimit;
        public Objective Objective = Objective.Annihilate;
        /// <summary>護送 / 守城：保護目標需存活的回合數。</summary>
        public int SurviveTurns;
        /// <summary>是否開放自動戰鬥（教學關關閉，讓玩家親手體驗該關要教的機制）。</summary>
        public bool AutoAllowed = true;
        /// <summary>true = 隊伍與站位由關卡決定，玩家不能編隊（教學關）。</summary>
        public bool FormationLocked;
        /// <summary>
        /// 教學關用的寫死牌序：開局時這些卡牌 id 依序排在最前面，
        /// 其餘維持套牌順序，不隨機洗牌。空 = 一般隨機洗牌。
        /// </summary>
        public List<string> ScriptedDraw = new List<string>();
        /// <summary>true = 沒有爆擊與閃避（教學關：結果完全可重現）。</summary>
        public bool NoRandomness;
        public List<HeroSlot> Heroes = new List<HeroSlot>();
        public List<EnemySlot> Enemies = new List<EnemySlot>();
    }
}
