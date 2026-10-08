using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    /// <summary>Demo 的養成面內容（卡池、關卡獎勵；數值為建議值，之後改由 JSON 載入）。</summary>
    public static class DemoMeta
    {
        public const string StandardPoolId = "standard";
        public const string NewbiePoolId = "newbie";
        public const string UpPoolId = "up_first";

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
                // 首個 UP 池：常駐 UR 6 隻加 UP 的張飛、關羽；出 UR 時 50% 為 UP 武將，未中則下一隻 UR 必為 UP。
                new GachaPool
                {
                    Id = UpPoolId, Name = "燕人武聖", UpUrs = HeroRoster.FirstUpUrIds.ToList(),
                    UrHeroes = HeroRoster.StandardUrIds.Concat(HeroRoster.FirstUpUrIds).ToList(),
                    SrHeroes = HeroRoster.DrawableSrIds(), RHeroes = RIds(),
                },
            };
        }

        private static List<string> RIds() =>
            HeroRoster.All().Where(h => h.Rarity == Rarity.R).Select(h => h.Id).ToList();

        public static string StageId(int chapter, int level) => $"{chapter}-{level}";

        /// <summary>第零章各關的星級回合數（第三星的限定回合；暫定值，依自動戰鬥模擬抓寬）。</summary>
        private static readonly int[] TurnPar = { 8, 10, 8, 12, 11, 8, 10, 12, 14, 20 };

        /// <summary>第零章關卡獎勵（關卡 id 沿用 "1-N"）：每關體力 10；第 1–3 關首通依序送劉備、張飛、關羽。</summary>
        public static StageReward Chapter1Stage(int level) => new StageReward
        {
            StageId = StageId(1, level),
            Chapter = 1,
            StaminaCost = 10,
            Exp = 20 + 10 * level,
            Gold = 200 + 100 * level,
            FirstClearYuanbao = level == DemoContent.ChapterLevelCount ? 300 : 60,
            StarTurnPar = TurnPar[level - 1],
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

        /// <summary>各階素材副本的敵人等級（暫定：第 1 階於教學章中段解鎖故較低，其後約對應各章末的玩家等級；之後依戰力門檻校準）。</summary>
        public static readonly int[] DungeonEnemyLevels = { 6, 19, 25, 32, 40 };

        /// <summary>
        /// 資源副本的戰鬥設定：每階有自己的敵人配置（暫以盜匪單位組成），我方由玩家編隊決定（套用編隊前是空的），開放自動戰鬥。
        /// </summary>
        public static BattleSetup DungeonSetup(string dungeonId, ulong seed)
        {
            var d = FindDungeon(dungeonId);
            int tier = d?.Tier ?? 1;
            int lv = DungeonEnemyLevels[tier - 1];
            var setup = new BattleSetup { Seed = seed, AutoAllowed = true };
            void Add(EnemyDef def, int lane, int row) => setup.Enemies.Add(new EnemySlot(def, DemoContent.EnemyPos(lane, row), lv));
            switch (tier)
            {
                case 1: // 糧倉護衛：山賊衝陣，兩名弓手在後排放箭
                    Add(DemoContent.BanditGrunt(), 1, 0); Add(DemoContent.BanditGrunt(), 2, 0); Add(DemoContent.BanditGrunt(), 3, 0);
                    Add(DemoContent.BanditArcher(), 1, 1); Add(DemoContent.BanditArcher(), 3, 1);
                    break;
                case 2: // 校場操練：披甲悍匪擋路，後排巫師放法術
                    Add(DemoContent.BanditIronBrute(), 2, 0); Add(DemoContent.BanditGrunt(), 1, 0); Add(DemoContent.BanditGrunt(), 3, 0);
                    Add(DemoContent.BanditShaman(), 2, 1);
                    break;
                case 3: // 兵器鋪：二當家與副寨主蓄力，要靠嘲諷或集火打斷
                    Add(DemoContent.BanditSecondChief(), 1, 0); Add(DemoContent.BanditDeputy(), 3, 0);
                    Add(DemoContent.BanditGrunt(), 2, 0); Add(DemoContent.BanditGrunt(), 0, 0);
                    break;
                case 4: // 軍械庫：雙悍匪守門，獵戶山賊專打後排
                    Add(DemoContent.BanditIronBrute(), 1, 0); Add(DemoContent.BanditIronBrute(), 3, 0);
                    Add(DemoContent.BanditMarksman(), 0, 1); Add(DemoContent.BanditMarksman(), 4, 1); Add(DemoContent.BanditShaman(), 2, 1);
                    break;
                default: // 中軍帳：山大王坐鎮，二當家與巫師護衛
                    Add(DemoContent.BanditKing(), 2, 0); Add(DemoContent.BanditSecondChief(), 1, 0);
                    Add(DemoContent.BanditShaman(), 0, 1); Add(DemoContent.BanditShaman(), 4, 1);
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
