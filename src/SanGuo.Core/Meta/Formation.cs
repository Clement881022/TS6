using System.Collections.Generic;

namespace SanGuo.Core.Meta
{
    /// <summary>玩家編隊裡的一名武將與站位。客戶端開始關卡時送給伺服器，伺服器據此重建戰鬥來驗證。</summary>
    public sealed class FormationEntry
    {
        public string HeroId = "";
        public int Lane;
        public int Row;

        public FormationEntry() { }
        public FormationEntry(string heroId, int lane, int row) { HeroId = heroId; Lane = lane; Row = row; }
    }

    /// <summary>編隊規則：只能帶已擁有的武將、最多 <see cref="MaxTeamSize"/> 人、站位不重疊且在場內。</summary>
    public static class FormationRules
    {
        public const int MaxTeamSize = 4;

        private static readonly BreakthroughTable Breakthroughs = DemoBreakthroughs.Create();

        /// <summary>檢查編隊；合法回傳 null，否則回傳錯誤碼（invalid_formation）。</summary>
        public static string? Validate(PlayerProfile p, IReadOnlyList<FormationEntry>? formation, int lanes = 5, int rows = 2)
        {
            if (formation == null || formation.Count < 1 || formation.Count > MaxTeamSize) return "invalid_formation";
            var heroes = new HashSet<string>();
            var cells = new HashSet<(int, int)>();
            foreach (var e in formation)
            {
                if (!p.Heroes.ContainsKey(e.HeroId) || DemoContent.Roster().Find(h => h.Id == e.HeroId) == null) return "invalid_formation";
                if (e.Lane < 0 || e.Lane >= lanes || e.Row < 0 || e.Row >= rows) return "invalid_formation";
                if (!heroes.Add(e.HeroId) || !cells.Add((e.Lane, e.Row))) return "invalid_formation";
            }
            return null;
        }

        /// <summary>
        /// 把編隊套進戰鬥設定：取代關卡原本的我方，並依玩家的養成（等級、突破、卡牌強化）縮放武將。
        /// 順序固定照編隊列表，確保客戶端與伺服器建出一樣的戰鬥（單位 id 一致）。
        /// </summary>
        public static void Apply(BattleSetup setup, PlayerProfile p, IReadOnlyList<FormationEntry> formation)
        {
            var roster = DemoContent.Roster();
            setup.Heroes.Clear();
            foreach (var e in formation)
            {
                var def = roster.Find(h => h.Id == e.HeroId)!;
                setup.Heroes.Add(HeroGrowth.BuildSlot(def, p.Heroes[e.HeroId], new Position(e.Lane, e.Row), Breakthroughs));
            }
        }
    }
}
