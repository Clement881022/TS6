using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    /// <summary>Demo 的養成面內容（卡池、關卡獎勵；數值為建議值，之後改由 JSON 載入）。</summary>
    public static class DemoMeta
    {
        public const string StandardPoolId = "standard";
        public const string NewbiePoolId = "newbie";

        /// <summary>Demo 目前只有 UR 與 R（尚無 SR），所以 SR 機率為 0、十連不保底 SR。</summary>
        public static List<GachaPool> Pools()
        {
            var roster = DemoContent.Roster();
            var ur = roster.Where(h => h.Rarity == Rarity.UR).Select(h => h.Id).ToList();
            var r = roster.Where(h => h.Rarity == Rarity.R && h.Id != "r_villager").Select(h => h.Id).ToList();
            return new List<GachaPool>
            {
                new GachaPool
                {
                    Id = StandardPoolId, Name = "常駐招募", UrRateBp = 300, SrRateBp = 0,
                    UrHeroes = ur, RHeroes = r, TenPullGuaranteesSr = false,
                },
                new GachaPool
                {
                    Id = NewbiePoolId, Name = "新手招募", UrRateBp = 300, SrRateBp = 0,
                    UrHeroes = ur, RHeroes = r, TenPullGuaranteesSr = false, FirstTenGuaranteesUr = true,
                },
            };
        }

        public static string StageId(int chapter, int level) => $"{chapter}-{level}";

        /// <summary>第一章關卡獎勵：教學關（1–4）2 點體力、其餘 8 點（見 days-1-7.md 6）。</summary>
        public static StageReward Chapter1Stage(int level) => new StageReward
        {
            StageId = StageId(1, level),
            Chapter = 1,
            StaminaCost = level <= 4 ? 2 : 8,
            Exp = 20 + 10 * level,
            Gold = 200 + 100 * level,
            FirstClearYuanbao = level == DemoContent.ChapterLevelCount ? 300 : 60,
            StarTurnPar = 12,
        };

        public static ResourceDungeonDef? FindDungeon(string id) =>
            DemoResourceDungeons.Create().Find(d => d.Id == id);

        /// <summary>
        /// 資源副本的戰鬥設定。<b>占位</b>：暫時沿用第 1 關的敵我配置（開放自動戰鬥），
        /// 各副本的正式敵人配置尚待設計。
        /// </summary>
        public static BattleSetup DungeonSetup(ulong seed)
        {
            var setup = DemoContent.Level(1, seed);
            setup.AutoAllowed = true;
            return setup;
        }

        /// <summary>關卡 id（如 "1-3"）或資源副本 id 對應的戰鬥設定；種子由伺服器發放。</summary>
        public static BattleSetup? BuildSetup(string stageId, ulong seed)
        {
            if (FindDungeon(stageId) != null) return DungeonSetup(seed);
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
                if (StageId(1, level) == stageId) return DemoContent.Level(level, seed);
            return null;
        }

        public static StageReward? FindStage(string stageId)
        {
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
                if (StageId(1, level) == stageId) return Chapter1Stage(level);
            return null;
        }
    }
}
