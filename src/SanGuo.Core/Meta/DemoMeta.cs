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

        public static string StageId(int chapter, int level) => Campaign.StageId(chapter, level);

        /// <summary>劇情固定武將（不在卡池，重複份由主線贈送）。</summary>
        public static readonly string[] StoryHeroes = { "liubei", "zhangfei", "guanyu" };

        /// <summary>
        /// 主線關卡獎勵（暫定）：每關體力 10；經驗與金幣依全主線的關卡序號（第零章 1–10、第一章 11–20…）線性遞增；
        /// 首通元寶每關 60、章末 300；第零章第 1–3 關首通依序送劉備、張飛、關羽；
        /// 第 2–6 章章末首通各送劉關張重複份 1 份（打完 1.0 剛好滿突，對應主線平衡的章末突破假設）。
        /// </summary>
        public static StageReward Stage(int chapter, int level)
        {
            int index = chapter * Campaign.LevelsPerChapter + level;
            return new StageReward
            {
                StageId = StageId(chapter, level),
                Chapter = chapter,
                StaminaCost = 10,
                Exp = 20 + 10 * index,
                Gold = 200 + 100 * index,
                FirstClearYuanbao = level == Campaign.LevelsPerChapter ? 300 : 60,
                StarTurnPar = Campaign.TurnPar(chapter, level),
                FirstClearHero = chapter != 0 ? "" : level == 1 ? "liubei" : level == 2 ? "zhangfei" : level == 3 ? "guanyu" : "",
                FirstClearDuplicates = chapter >= 2 && level == Campaign.LevelsPerChapter ? StoryHeroes : System.Array.Empty<string>(),
            };
        }

        public static ResourceDungeonDef? FindDungeon(string id) =>
            DemoResourceDungeons.Create().Find(d => d.Id == id);

        /// <summary>第零章前 8 關是教學關（固定隊伍）；從這一關起（含第 1–6 章）改用玩家的編隊與養成。</summary>
        public const int FirstOpenFormationLevel = 9;

        /// <summary>教學關：隊伍固定，不經過編隊畫面。</summary>
        public static bool FormationLocked(int chapter, int level) => chapter == 0 && level < FirstOpenFormationLevel;

        /// <summary>這個關卡 / 副本是否由玩家編隊上場（資源副本與教學關以外的主線關卡）。</summary>
        public static bool UsesPlayerFormation(string stageId) =>
            FindDungeon(stageId) != null || (Campaign.TryParse(stageId, out int ch, out int lv) && !FormationLocked(ch, lv));

        /// <summary>
        /// 開放編隊的主線關卡（編隊前的樣子）：第零章第 9–10 關沿用教學版的敵人配置，但取消教學專用的限制；
        /// 第 1–6 章本來就開放編隊。我方只剩護送 / 守城目標，玩家的編隊在 <see cref="FormationRules.Apply"/> 套入。
        /// </summary>
        public static BattleSetup OpenLevel(int chapter, int level, ulong seed)
        {
            var setup = Campaign.Setup(chapter, level, seed);
            if (chapter != 0) return setup;
            setup.Heroes.Clear();
            setup.FormationLocked = false;
            setup.NoRandomness = false;
            setup.ScriptedDraw = new List<string>();
            setup.AutoAllowed = true;
            return setup;
        }

        /// <summary>
        /// 各階素材副本的敵人等級（暫定）：第 1 階於第零章中段解鎖故較低；其後以自動戰鬥校準，
        /// 讓上一章章末養成剛解鎖時勝率約六成以上、再晚一章約九成以上（見 OpenStageBalanceTests）。
        /// </summary>
        public static readonly int[] DungeonEnemyLevels = { 6, 19, 25, 31, 33 };

        /// <summary>
        /// 資源副本的戰鬥設定：每階有自己的敵人配置（第 1–2 階為第零章盜匪，第 3–5 階沿用解鎖時那一章的主線敵人），我方由玩家編隊決定（套用編隊前是空的），開放自動戰鬥。
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
                case 3: // 兵器鋪：黃巾渠帥蓄力，要靠嘲諷或集火打斷；方士在後排放法術
                    Add(Content.Enemies.YtCaptain(), 1, 0); Add(Content.Enemies.YtBrute(), 2, 0); Add(Content.Enemies.YtSoldier(), 3, 0);
                    Add(Content.Enemies.YtSorcerer(), 2, 1); Add(Content.Enemies.YtArcher(), 0, 1);
                    break;
                case 4: // 軍械庫：禁軍甲士守門，宦官黨羽與弓手在後排
                    Add(Content.Enemies.Guard(), 1, 0); Add(Content.Enemies.Guard(), 3, 0); Add(Content.Enemies.HanSoldier(), 2, 0);
                    Add(Content.Enemies.HanArcher(), 0, 1); Add(Content.Enemies.Eunuch(), 4, 1);
                    break;
                default: // 中軍帳：西涼校尉坐鎮蓄力，鐵騎衝陣、弓騎在後
                    Add(Content.Enemies.XlCaptain(), 2, 0); Add(Content.Enemies.Cavalry(), 1, 0); Add(Content.Enemies.Cavalry(), 3, 0);
                    Add(Content.Enemies.HorseArcher(), 0, 1); Add(Content.Enemies.HorseArcher(), 4, 1);
                    break;
            }
            return setup;
        }

        /// <summary>
        /// 關卡 id（如 "0-3"、"2-7"）或資源副本 id 對應的戰鬥設定；種子由伺服器發放。
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
                if (!Campaign.TryParse(stageId, out int chapter, out int level)) return null;
                setup = FormationLocked(chapter, level) ? DemoContent.Level(level, seed) : OpenLevel(chapter, level, seed);
            }
            if (setup.FormationLocked) return setup;
            if (profile == null || formation == null || FormationRules.Validate(profile, formation) != null)
                return null;
            FormationRules.Apply(setup, profile, formation);
            return setup;
        }

        public static StageReward? FindStage(string stageId) =>
            Campaign.TryParse(stageId, out int chapter, out int level) ? Stage(chapter, level) : null;
    }
}
