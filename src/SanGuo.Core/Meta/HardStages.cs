using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    public static class HardStages
    {
        public const string Prefix = "H";
        public const int FirstChapter = 1;
        public const int StaminaCost = 20;
        public const int MinEnemyLevel = 52, MaxEnemyLevel = 66;
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

        public static Role? BannedRole(int chapter, int level)
        {
            if (level != 6) return null;
            Role[] cycle = { Role.Healer, Role.Tank, Role.Warrior, Role.Ranger, Role.Mage, Role.Strategist };
            return cycle[(chapter - FirstChapter) % cycle.Length];
        }

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
                Exp = StaminaCost,
                Gold = normal.Gold * 2,
                FirstClearYuanbao = boss ? 150 : 50,
                StarTurnPar = Campaign.TurnPar(chapter, level),
                Materials = new Dictionary<string, int> { [HeroGrowth.Soul] = RepeatSouls },
                FirstClearMaterials = new Dictionary<string, int> { [HeroGrowth.Soul] = FirstClearSouls },
            };
        }

        public static BattleSetup Setup(int chapter, int level, ulong seed)
        {
            var setup = Campaign.Setup(chapter, level, seed);
            int normalBase = Campaign.EnemyLevelOf(chapter, level), hard = EnemyLevelOf(chapter, level);
            foreach (var e in setup.Enemies) e.Level = hard + (e.Level - normalBase);
            foreach (var h in setup.Heroes.Where(h => h.IsProtected)) h.Level = hard;
            int limit = TurnLimit(chapter, level);
            if (limit > 0) setup.TurnLimit = setup.TurnLimit > 0 ? System.Math.Min(setup.TurnLimit, limit) : limit;
            return setup;
        }

        public static string? CheckFormation(string stageId, IReadOnlyList<FormationEntry>? formation)
        {
            if (!TryParse(stageId, out int c, out int l) || formation == null) return null;
            var banned = BannedRole(c, l);
            if (banned == null) return null;
            return formation.Any(e => HeroRoster.Find(e.HeroId)?.Role == banned) ? "banned_role" : null;
        }
    }
}
