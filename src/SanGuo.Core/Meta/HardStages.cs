using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    /// <summary>
    /// 困難主線（P6 終局內容，企劃 2026-10-09 定案）：重用第 1–6 章 60 關的配置，敵人更強，部分關卡加上特殊條件；
    /// 消耗體力（也算帳號經驗），產出將魂、金幣與首通元寶，3 星後可掃蕩。
    /// 暫定值（待企劃確認）：主線 6-10 通關後開放；敵人基準等級依序號由 52 線性升到 66（保留普通版的逐隻微調）；每次 20 體力、首通 50 元寶（章末 150）＋ 20 將魂、每次 3 將魂；
    /// 每章第 3、9 關限回合（三星門檻 + 2），第 6 關禁用一個職業（依章輪替）。
    /// 2026-10-09 模擬：原本「普通版通關即開放、等級 46–62 線性」會讓玩家把體力花在困難關而拖慢主線（全通延到第 39 天），且第 1 章組成弱、太簡單。
    /// </summary>
    public static class HardStages
    {
        public const string Prefix = "H";
        public const int FirstChapter = 1;
        public const int StaminaCost = 20;
        /// <summary>困難 1-1 與 6-10 的敵人基準等級（中間依關卡序號線性內插）。「普通版 + 固定等級」會讓前幾章太簡單、第 6 章太硬。</summary>
        public const int MinEnemyLevel = 52, MaxEnemyLevel = 66;
        /// <summary>開放困難主線所需通關的主線關卡（主線全通）。</summary>
        public const string UnlockStage = "6-10";
        public const int FirstClearSouls = 20, RepeatSouls = 3;

        public static string StageId(int chapter, int level) => $"{Prefix}{chapter}-{level}";

        public static bool TryParse(string stageId, out int chapter, out int level)
        {
            chapter = level = 0;
            if (stageId == null || !stageId.StartsWith(Prefix)) return false;
            return Campaign.TryParse(stageId.Substring(Prefix.Length), out chapter, out level) && chapter >= FirstChapter;
        }

        public static bool IsHard(string stageId) => TryParse(stageId, out _, out _);

        /// <summary>開放條件：主線全通（6-10），且前一個困難關卡已通關（困難 1-1 為第一關）。</summary>
        public static bool IsUnlocked(ICollection<string> cleared, int chapter, int level)
        {
            if (chapter < FirstChapter || !Campaign.IsValid(chapter, level)) return false;
            if (!cleared.Contains(UnlockStage)) return false;
            if (chapter == FirstChapter && level == 1) return true;
            Campaign.Previous(chapter, level, out int pc, out int pl);
            return cleared.Contains(StageId(pc, pl));
        }

        private static int Index(int chapter, int level) => (chapter - FirstChapter) * Campaign.LevelsPerChapter + (level - 1);

        public static int EnemyLevelOf(int chapter, int level)
        {
            int last = (Campaign.LastChapter - FirstChapter + 1) * Campaign.LevelsPerChapter - 1;
            return MinEnemyLevel + (int)System.Math.Round((MaxEnemyLevel - MinEnemyLevel) * Index(chapter, level) / (double)last);
        }

        /// <summary>第 6 關禁用的職業（依章輪替）；其餘關卡不禁。</summary>
        public static Role? BannedRole(int chapter, int level)
        {
            if (level != 6) return null;
            Role[] cycle = { Role.Healer, Role.Tank, Role.Warrior, Role.Ranger, Role.Mage, Role.Strategist };
            return cycle[(chapter - FirstChapter) % cycle.Length];
        }

        /// <summary>第 3、9 關限回合（三星門檻 + 2；原本已限時則取較嚴者）。</summary>
        public static int TurnLimit(int chapter, int level) =>
            level == 3 || level == 9 ? Campaign.TurnPar(chapter, level) + 2 : 0;

        public static StageReward Stage(int chapter, int level)
        {
            var normal = DemoMeta.Stage(chapter, level);
            bool boss = level == Campaign.LevelsPerChapter;
            return new StageReward
            {
                StageId = StageId(chapter, level),
                Chapter = chapter,
                StaminaCost = StaminaCost,
                Exp = StaminaCost, // 帳號經驗 = 消耗的體力
                Gold = normal.Gold * 2,
                FirstClearYuanbao = boss ? 150 : 50,
                StarTurnPar = Campaign.TurnPar(chapter, level),
                Materials = new Dictionary<string, int> { [HeroGrowth.Soul] = RepeatSouls },
                FirstClearMaterials = new Dictionary<string, int> { [HeroGrowth.Soul] = FirstClearSouls },
            };
        }

        /// <summary>困難版的戰鬥設定（編隊前）：沿用普通版配置，敵人換成困難等級，加上限回合條件。</summary>
        public static BattleSetup Setup(int chapter, int level, ulong seed)
        {
            var setup = Campaign.Setup(chapter, level, seed);
            int normalBase = Campaign.EnemyLevelOf(chapter, level), hard = EnemyLevelOf(chapter, level);
            foreach (var e in setup.Enemies) e.Level = hard + (e.Level - normalBase); // 保留逐隻微調
            foreach (var h in setup.Heroes.Where(h => h.IsProtected)) h.Level = hard; // 護送目標跟著變硬
            int limit = TurnLimit(chapter, level);
            if (limit > 0) setup.TurnLimit = setup.TurnLimit > 0 ? System.Math.Min(setup.TurnLimit, limit) : limit;
            return setup;
        }

        /// <summary>編隊是否違反禁用職業；違反回傳錯誤碼 banned_role，否則 null。</summary>
        public static string? CheckFormation(string stageId, IReadOnlyList<FormationEntry>? formation)
        {
            if (!TryParse(stageId, out int c, out int l) || formation == null) return null;
            var banned = BannedRole(c, l);
            if (banned == null) return null;
            return formation.Any(e => HeroRoster.Find(e.HeroId)?.Role == banned) ? "banned_role" : null;
        }
    }
}
