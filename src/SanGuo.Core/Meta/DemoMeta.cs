using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    public static class DemoMeta
    {
        public const string StandardPoolId = "standard";
        public const string NewbiePoolId = "newbie";
        public const string UpPoolId = "up_first";

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

        public static readonly string[] StoryHeroes = { "liubei", "zhangfei", "guanyu" };

        public static StageReward Stage(int chapter, int level)
        {
            int index = chapter * Campaign.LevelsPerChapter + level;
            return new StageReward
            {
                StageId = StageId(chapter, level),
                Chapter = chapter,
                StaminaCost = 10,
                Exp = 10,
                Gold = 200 + 100 * index,
                FirstClearYuanbao = level == Campaign.LevelsPerChapter ? 300 : 60,
                StarTurnPar = Campaign.TurnPar(chapter, level),
                FirstClearHero = chapter != 0 ? "" : level == 1 ? "liubei" : level == 2 ? "zhangfei" : level == 3 ? "guanyu" : "",
                FirstClearDuplicates = chapter >= 2 && level == Campaign.LevelsPerChapter ? StoryHeroes : System.Array.Empty<string>(),
            };
        }

        public static ResourceDungeonDef? FindDungeon(string id) =>
            DemoResourceDungeons.Create().Find(d => d.Id == id);

        public const int FirstOpenFormationLevel = 9;

        public static bool FormationLocked(int chapter, int level) => chapter == 0 && level < FirstOpenFormationLevel;

        public static bool UsesPlayerFormation(string stageId) =>
            stageId == WorldBoss.StageId || FindDungeon(stageId) != null || HardStages.IsHard(stageId)
            || (Campaign.TryParse(stageId, out int ch, out int lv) && !FormationLocked(ch, lv));

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

        public static readonly int[] DungeonEnemyLevels = { 6, 28, 34, 39, 43 };

        public static BattleSetup DungeonSetup(string dungeonId, ulong seed)
        {
            var d = FindDungeon(dungeonId);
            int tier = d?.Tier ?? 1;
            int lv = DungeonEnemyLevels[tier - 1];
            var setup = new BattleSetup { Seed = seed, AutoAllowed = true };
            void Add(EnemyDef def, int lane, int row) => setup.Enemies.Add(new EnemySlot(def, DemoContent.EnemyPos(lane, row), lv));
            switch (tier)
            {
                case 1:
                    Add(DemoContent.BanditGrunt(), 1, 0); Add(DemoContent.BanditGrunt(), 2, 0); Add(DemoContent.BanditGrunt(), 3, 0);
                    Add(DemoContent.BanditArcher(), 1, 1); Add(DemoContent.BanditArcher(), 3, 1);
                    break;
                case 2:
                    Add(DemoContent.BanditIronBrute(), 2, 0); Add(DemoContent.BanditGrunt(), 1, 0); Add(DemoContent.BanditGrunt(), 3, 0);
                    Add(DemoContent.BanditShaman(), 2, 1);
                    break;
                case 3:
                    Add(Content.Enemies.YtCaptain(), 1, 0); Add(Content.Enemies.YtBrute(), 2, 0); Add(Content.Enemies.YtSoldier(), 3, 0);
                    Add(Content.Enemies.YtSorcerer(), 2, 1); Add(Content.Enemies.YtArcher(), 0, 1);
                    break;
                case 4:
                    Add(Content.Enemies.Guard(), 1, 0); Add(Content.Enemies.Guard(), 3, 0); Add(Content.Enemies.HanSoldier(), 2, 0);
                    Add(Content.Enemies.HanArcher(), 0, 1); Add(Content.Enemies.Eunuch(), 4, 1);
                    break;
                default:
                    Add(Content.Enemies.XlCaptain(), 2, 0); Add(Content.Enemies.Cavalry(), 1, 0); Add(Content.Enemies.Cavalry(), 3, 0);
                    Add(Content.Enemies.HorseArcher(), 0, 1); Add(Content.Enemies.HorseArcher(), 4, 1);
                    break;
            }
            return setup;
        }

        public static BattleSetup? BuildSetup(string stageId, ulong seed,
            PlayerProfile? profile = null, IReadOnlyList<FormationEntry>? formation = null)
        {
            BattleSetup setup;
            var dungeon = FindDungeon(stageId);
            if (dungeon != null) setup = DungeonSetup(dungeon.Id, seed);
            else if (stageId == WorldBoss.StageId)
            {
                if (profile == null) return null;
                var wb = profile.WorldBoss;
                setup = WorldBoss.Setup(wb.FightSeason != "" ? wb.FightSeason : wb.Season, seed);
            }
            else if (HardStages.TryParse(stageId, out int hc, out int hl)) setup = HardStages.Setup(hc, hl, seed);
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
            HardStages.TryParse(stageId, out int hc, out int hl) ? HardStages.Stage(hc, hl)
            : Campaign.TryParse(stageId, out int chapter, out int level) ? Stage(chapter, level) : null;
    }
}
