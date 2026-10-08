using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    /// <summary>Demo 的養成面內容（卡池、關卡獎勵；數值為建議值，之後改由 JSON 載入）。</summary>
    public static class DemoMeta
    {
        public const string StandardPoolId = "standard";
        public const string NewbiePoolId = "newbie";

        /// <summary>常駐池與新手池：UR 6 隻（每職業 1 隻）、可抽取 SR 12 隻、R 6 隻。</summary>
        public static List<GachaPool> Pools()
        {
            return new List<GachaPool>
            {
                new GachaPool
                {
                    Id = StandardPoolId, Name = "常駐招募",
                    UrHeroes = HeroRoster.StandardUrIds.ToList(), SrHeroes = HeroRoster.DrawableSrIds(), RHeroes = RIds(),
                },
                new GachaPool
                {
                    Id = NewbiePoolId, Name = "新手招募", FirstTenGuaranteesUr = true,
                    UrHeroes = HeroRoster.StandardUrIds.ToList(), SrHeroes = HeroRoster.DrawableSrIds(), RHeroes = RIds(),
                },
            };
        }

        private static List<string> RIds() =>
            HeroRoster.All().Where(h => h.Rarity == Rarity.R).Select(h => h.Id).ToList();

        public static string StageId(int chapter, int level) => $"{chapter}-{level}";

        /// <summary>第零章關卡獎勵（關卡 id 沿用 "1-N"）：教學關（1–4）2 點體力、其餘 8 點；第 1–3 關首通依序送劉備、張飛、關羽。</summary>
        public static StageReward Chapter1Stage(int level) => new StageReward
        {
            StageId = StageId(1, level),
            Chapter = 1,
            StaminaCost = level <= 4 ? 2 : 8,
            Exp = 20 + 10 * level,
            Gold = 200 + 100 * level,
            FirstClearYuanbao = level == DemoContent.ChapterLevelCount ? 300 : 60,
            StarTurnPar = 12,
            FirstClearHero = level == 1 ? "liubei" : level == 2 ? "zhangfei" : level == 3 ? "guanyu" : "",
        };

        public static ResourceDungeonDef? FindDungeon(string id) =>
            DemoResourceDungeons.Create().Find(d => d.Id == id);

        /// <summary>第一章前 8 關是教學關（固定隊伍）；從這一關起改用玩家的編隊與養成。</summary>
        public const int FirstOpenFormationLevel = 9;

        /// <summary>主線關卡編號（"1-3" → 3）；不是主線關卡回傳 0。</summary>
        public static int LevelOf(string stageId)
        {
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
                if (StageId(1, level) == stageId) return level;
            return 0;
        }

        /// <summary>這個關卡 / 副本是否由玩家編隊上場（資源副本與教學後的主線關卡）。</summary>
        public static bool UsesPlayerFormation(string stageId) =>
            FindDungeon(stageId) != null || LevelOf(stageId) >= FirstOpenFormationLevel;

        /// <summary>教學關：隊伍固定，不經過編隊畫面。</summary>
        public static bool FormationLocked(int level) => level < FirstOpenFormationLevel;

        /// <summary>
        /// 開放編隊的主線關卡：沿用教學版的敵人配置，但我方改由玩家編隊決定，
        /// 並取消教學專用的限制（寫死牌序、無爆擊閃避、禁用自動戰鬥）。我方在套用編隊前是空的。
        /// </summary>
        public static BattleSetup OpenLevel(int level, ulong seed)
        {
            var setup = DemoContent.Level(level, seed);
            setup.Heroes.Clear();
            setup.FormationLocked = false;
            setup.NoRandomness = false;
            setup.ScriptedDraw = new List<string>();
            setup.AutoAllowed = true;
            return setup;
        }

        /// <summary>
        /// 資源副本的戰鬥設定：每個副本有自己的敵人配置，我方由玩家編隊決定（套用編隊前是空的），開放自動戰鬥。
        /// </summary>
        /// <summary>資源副本的敵人等級（暫定，之後依副本階數與戰力門檻調整）。</summary>
        public const int DungeonEnemyLevel = 10;

        public static BattleSetup DungeonSetup(string dungeonId, ulong seed)
        {
            var setup = new BattleSetup { Seed = seed, AutoAllowed = true };
            switch (dungeonId)
            {
                case "res_exp": // 校場操練：鐵甲悍匪擋路，後排巫師放法術
                    setup.Enemies.Add(new EnemySlot(DemoContent.BanditIronBrute(), DemoContent.EnemyPos(2, 0), DungeonEnemyLevel));
                    setup.Enemies.Add(new EnemySlot(DemoContent.BanditGrunt(), DemoContent.EnemyPos(1, 0), DungeonEnemyLevel));
                    setup.Enemies.Add(new EnemySlot(DemoContent.BanditGrunt(), DemoContent.EnemyPos(3, 0), DungeonEnemyLevel));
                    setup.Enemies.Add(new EnemySlot(DemoContent.BanditShaman(), DemoContent.EnemyPos(2, 1), DungeonEnemyLevel));
                    break;
                case "res_card": // 兵器鋪：二當家與副寨主蓄力，要靠嘲諷或集火打斷
                    setup.Enemies.Add(new EnemySlot(DemoContent.BanditSecondChief(), DemoContent.EnemyPos(1, 0), DungeonEnemyLevel));
                    setup.Enemies.Add(new EnemySlot(DemoContent.BanditDeputy(), DemoContent.EnemyPos(3, 0), DungeonEnemyLevel));
                    setup.Enemies.Add(new EnemySlot(DemoContent.BanditGrunt(), DemoContent.EnemyPos(2, 0), DungeonEnemyLevel));
                    setup.Enemies.Add(new EnemySlot(DemoContent.BanditGrunt(), DemoContent.EnemyPos(0, 0), DungeonEnemyLevel));
                    break;
                default: // res_gold 糧倉護衛：山賊衝陣，兩名弓手在後排放箭
                    setup.Enemies.Add(new EnemySlot(DemoContent.BanditGrunt(), DemoContent.EnemyPos(1, 0), DungeonEnemyLevel));
                    setup.Enemies.Add(new EnemySlot(DemoContent.BanditGrunt(), DemoContent.EnemyPos(2, 0), DungeonEnemyLevel));
                    setup.Enemies.Add(new EnemySlot(DemoContent.BanditGrunt(), DemoContent.EnemyPos(3, 0), DungeonEnemyLevel));
                    setup.Enemies.Add(new EnemySlot(DemoContent.BanditArcher(), DemoContent.EnemyPos(1, 1), DungeonEnemyLevel));
                    setup.Enemies.Add(new EnemySlot(DemoContent.BanditArcher(), DemoContent.EnemyPos(3, 1), DungeonEnemyLevel));
                    break;
            }
            return setup;
        }

        /// <summary>
        /// 關卡 id（如 "1-3"）或資源副本 id 對應的戰鬥設定；種子由伺服器發放。
        /// 開放編隊的關卡 / 副本要給玩家資料與編隊（否則回傳 null）；教學關不需要。
        /// </summary>
        public static BattleSetup? BuildSetup(string stageId, ulong seed,
            PlayerProfile? profile = null, IReadOnlyList<FormationEntry>? formation = null)
        {
            BattleSetup setup;
            var dungeon = FindDungeon(stageId);
            if (dungeon != null) setup = DungeonSetup(dungeon.Id, seed);
            else
            {
                int level = LevelOf(stageId);
                if (level == 0) return null;
                setup = level >= FirstOpenFormationLevel ? OpenLevel(level, seed) : DemoContent.Level(level, seed);
            }
            if (setup.FormationLocked) return setup;
            if (profile == null || formation == null || FormationRules.Validate(profile, formation) != null)
                return null;
            FormationRules.Apply(setup, profile, formation);
            return setup;
        }

        public static StageReward? FindStage(string stageId)
        {
            for (int level = 1; level <= DemoContent.ChapterLevelCount; level++)
                if (StageId(1, level) == stageId) return Chapter1Stage(level);
            return null;
        }
    }
}
