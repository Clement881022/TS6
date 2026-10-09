using System.Text;
using SanGuo.Core;
using SanGuo.Core.Meta;

/// <summary>
/// 校準工具：主線章末關的戰力門檻（同 CampaignBalanceTests 的隊伍與各章預期養成）。
/// 用法：calib [每章敵人等級偏移, 例 0,3,3,4,4,5,5]，印出各章末「照預期養成／停在上一章」的自動戰鬥勝率。
/// </summary>
public static class Calibrate
{
    static readonly (string Id, int Lane, int Row)[] Team = { ("zhangfei", 2, 3), ("guanyu", 1, 3), ("handang", 3, 4), ("liubei", 2, 4) };
    static readonly (int Level, int Stars, int Gear)[] Growth = { (12, 0, 0), (19, 0, 1), (25, 0, 2), (29, 1, 2), (32, 2, 3), (36, 3, 4), (40, 4, 4) };

    static double Win(string stage, (int Level, int Stars, int Gear) g, int runs = 40)
    {
        var p = PlayerProfile.CreateNew(0);
        var team = new List<FormationEntry>();
        foreach (var (id, lane, row) in Team)
        {
            var h = new HeroState { HeroId = id, Level = g.Level, Stars = g.Stars };
            if (g.Gear > 0) foreach (var s in Equipment.Slots) h.Equipment[s.ToString()] = g.Gear;
            p.Heroes[id] = h;
            team.Add(new FormationEntry(id, lane, row));
        }
        int wins = 0;
        for (ulong seed = 1; seed <= (ulong)runs; seed++)
        {
            var b = new Battle(DemoMeta.BuildSetup(stage, seed, p, team)!);
            for (int i = 0; i < 60 && b.Result == BattleResult.Ongoing; i++) AutoPlayer.PlayTurn(b);
            if (b.Result == BattleResult.Won) wins++;
        }
        return 100.0 * wins / runs;
    }

    public static string Run(int[] offsets)
    {
        var original = (int[])Campaign.ChapterEndLevel.Clone();
        for (int i = 0; i < offsets.Length && i < Campaign.ChapterEndLevel.Length; i++) Campaign.ChapterEndLevel[i] = original[i] + offsets[i];
        var sb = new StringBuilder($"敵人章末等級 {string.Join(",", Campaign.ChapterEndLevel)}\n");
        for (int ch = 1; ch <= Campaign.LastChapter; ch++)
        {
            string id = Campaign.StageId(ch, Campaign.LevelsPerChapter);
            double grown = Win(id, Growth[ch]), behind = Win(id, Growth[ch - 1]);
            int minStage = 100; string worst = "";
            for (int lv = 1; lv <= Campaign.LevelsPerChapter; lv++)
            {
                var sid = Campaign.StageId(ch, lv);
                double r = Win(sid, Growth[ch], 20);
                if (r < minStage) { minStage = (int)r; worst = sid; }
            }
            sb.AppendLine($"{id}：章末養成 {grown:0}%　上一章 {behind:0}%　（本章最難 {worst} {minStage}%）{(grown >= 55 && behind <= 40 && minStage >= 30 ? "" : "  ← 未達")}");
        }
        for (int i = 0; i < original.Length; i++) Campaign.ChapterEndLevel[i] = original[i];
        return sb.ToString();
    }
}
