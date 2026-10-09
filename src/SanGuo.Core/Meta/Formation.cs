using System.Collections.Generic;

namespace SanGuo.Core.Meta
{
    public sealed class FormationEntry
    {
        public string HeroId = "";
        public int Lane;
        public int Row;

        public FormationEntry() { }
        public FormationEntry(string heroId, int lane, int row) { HeroId = heroId; Lane = lane; Row = row; }
    }

    public static class FormationRules
    {
        public const int MaxTeamSize = 4;

        public static bool InFormationZone(int lane, int row) =>
            lane >= BattleSetup.FormationMinLane && lane <= BattleSetup.FormationMaxLane
            && row >= BattleSetup.FormationMinRow && row <= BattleSetup.FormationMaxRow;

        public static string? Validate(PlayerProfile p, IReadOnlyList<FormationEntry>? formation)
        {
            if (formation == null || formation.Count < 1 || formation.Count > MaxTeamSize) return "invalid_formation";
            var heroes = new HashSet<string>();
            var cells = new HashSet<(int, int)>();
            foreach (var e in formation)
            {
                if (!p.Heroes.ContainsKey(e.HeroId) || DemoContent.Roster().Find(h => h.Id == e.HeroId) == null) return "invalid_formation";
                if (!InFormationZone(e.Lane, e.Row)) return "invalid_formation";
                if (!heroes.Add(e.HeroId) || !cells.Add((e.Lane, e.Row))) return "invalid_formation";
            }
            return null;
        }

        public static void Apply(BattleSetup setup, PlayerProfile p, IReadOnlyList<FormationEntry> formation)
        {
            var roster = DemoContent.Roster();
            setup.Heroes.RemoveAll(h => !h.IsProtected);
            foreach (var e in formation)
            {
                var def = roster.Find(h => h.Id == e.HeroId)!;
                setup.Heroes.Add(HeroGrowth.BuildSlot(def, p.Heroes[e.HeroId], new Position(e.Lane, e.Row)));
            }
        }
    }
}
