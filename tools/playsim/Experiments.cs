using System.Text;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;

/// <summary>對照實驗：固定養成，比較職業組合、稀有度、打法對勝率與世界 Boss 傷害的影響（離線戰鬥，不經伺服器）。</summary>
public static class Experiments
{
    static readonly Dictionary<Role, (string Sr, string Ur)> Ids = new()
    {
        [Role.Tank] = ("zhangfei", "xiahoudun"),
        [Role.Warrior] = ("guanyu", "lvbu"),
        [Role.Ranger] = ("handang", "gongsunzan"),
        [Role.Mage] = ("zhangbao", "zhangjiao"),
        [Role.Strategist] = ("luzhi", "xunyu"),
        [Role.Healer] = ("liubei", "huatuo"),
    };
    static readonly Dictionary<Role, string> AltSr = new()
    {
        [Role.Tank] = "zhoucang", [Role.Warrior] = "huaxiong", [Role.Ranger] = "zoujing",
        [Role.Mage] = "yuji", [Role.Strategist] = "jianyong", [Role.Healer] = "ganfuren",
    };

    public static readonly (string Name, Role[] Roles)[] Comps =
    {
        ("坦補戰戰", new[] { Role.Tank, Role.Healer, Role.Warrior, Role.Warrior }),
        ("坦補戰弓", new[] { Role.Tank, Role.Healer, Role.Warrior, Role.Ranger }),
        ("坦補弓法", new[] { Role.Tank, Role.Healer, Role.Ranger, Role.Mage }),
        ("坦補法謀", new[] { Role.Tank, Role.Healer, Role.Mage, Role.Strategist }),
        ("坦補戰謀", new[] { Role.Tank, Role.Healer, Role.Warrior, Role.Strategist }),
        ("坦戰戰弓(無補)", new[] { Role.Tank, Role.Warrior, Role.Warrior, Role.Ranger }),
        ("補戰戰弓(無坦)", new[] { Role.Healer, Role.Warrior, Role.Warrior, Role.Ranger }),
        ("雙坦補戰", new[] { Role.Tank, Role.Tank, Role.Healer, Role.Warrior }),
    };

    public static (PlayerProfile, List<FormationEntry>) Team(Role[] roles, bool ur, int level, int stars, int gear)
    {
        var p = PlayerProfile.CreateNew(0);
        p.Level = 60;
        var team = new List<FormationEntry>();
        var front = new Queue<(int, int)>(new[] { (2, 3), (1, 3), (3, 3) });
        var back = new Queue<(int, int)>(new[] { (2, 4), (1, 4), (3, 4) });
        var used = new HashSet<string>();
        foreach (var r in roles.OrderBy(r => r == Role.Tank ? 0 : r == Role.Warrior ? 1 : 2))
        {
            string id = ur ? Ids[r].Ur : Ids[r].Sr;
            if (!used.Add(id)) id = ur ? Ids[r].Sr : AltSr[r]; // 同職業第二隻
            used.Add(id);
            var h = new HeroState { HeroId = id, Level = level, Stars = stars };
            if (gear > 0) foreach (var s in Equipment.Slots) h.Equipment[s.ToString()] = gear;
            p.Heroes[id] = h;
            bool melee = r == Role.Tank || r == Role.Warrior;
            var q = melee && front.Count > 0 ? front : back.Count > 0 ? back : front;
            var (l, row) = q.Dequeue();
            team.Add(new FormationEntry(id, l, row));
        }
        return (p, team);
    }

    public static (double Win, double AvgTurns) Rate(string stage, PlayerProfile p, List<FormationEntry> team, bool smart, int runs)
    {
        int wins = 0, turns = 0;
        for (ulong seed = 1; seed <= (ulong)runs; seed++)
        {
            var b = new Battle(DemoMeta.BuildSetup(stage, seed * 7919, p, team)!);
            var rec = new ReplayRecorder(b);
            var bot = new SmartBot("exp", null);
            for (int i = 0; i < 60 && b.Result == BattleResult.Ongoing; i++) { if (smart) bot.PlayTurn(rec); else AutoPlayer.PlayTurn(b); }
            if (b.Result == BattleResult.Won) wins++;
            turns += b.Turn;
        }
        return (100.0 * wins / runs, (double)turns / runs);
    }

    public static (double Avg, long Min, long Max, double Sd) Boss(PlayerProfile p, List<FormationEntry> team, bool smart, int runs, string season = "2026-10")
    {
        p.WorldBoss.Season = season;
        var list = new List<long>();
        for (ulong seed = 1; seed <= (ulong)runs; seed++)
        {
            var b = new Battle(DemoMeta.BuildSetup(WorldBoss.StageId, seed * 104729, p, team)!);
            var rec = new ReplayRecorder(b);
            var bot = new SmartBot("exp", null);
            for (int i = 0; i < 60 && b.Result == BattleResult.Ongoing; i++) { if (smart) bot.PlayTurn(rec); else AutoPlayer.PlayTurn(b); }
            list.Add(WorldBoss.Score(b));
        }
        double avg = list.Average();
        return (avg, list.Min(), list.Max(), Math.Sqrt(list.Average(x => (x - avg) * (x - avg))));
    }

    public static string Run()
    {
        var sb = new StringBuilder();
        string[] stages = { "3-10", "4-10", "5-10", "6-10" };
        var midGrowth = (Level: 36, Stars: 3, Gear: 4); // 第 5 章末的預期養成（CampaignBalanceTests）
        sb.AppendLine("# 對照實驗（離線戰鬥，每格 40 場）");
        sb.AppendLine($"## A. 職業組合 × 稀有度：章末關勝率（養成固定 Lv{midGrowth.Level}／{midGrowth.Stars}★／{midGrowth.Gear} 階裝，聰明打）");
        sb.AppendLine("| 組合 | 稀有度 | " + string.Join(" | ", stages) + " | Boss 平均傷害（滿養成 Lv60/5★/5階） |");
        sb.AppendLine("|---|---|" + string.Concat(stages.Select(_ => "---|")) + "---|");
        foreach (var (name, roles) in Comps)
            foreach (bool ur in new[] { false, true })
            {
                var (p, team) = Team(roles, ur, midGrowth.Level, midGrowth.Stars, midGrowth.Gear);
                var cells = stages.Select(s => $"{Rate(s, p, team, true, 40).Win:0}%");
                var (pm, tm) = Team(roles, ur, 60, 5, 5);
                var boss = Boss(pm, tm, true, 40);
                sb.AppendLine($"| {name} | {(ur ? "UR" : "SR")} | {string.Join(" | ", cells)} | {boss.Avg:0}（±{boss.Sd:0}） |");
            }

        sb.AppendLine();
        sb.AppendLine("## B. 世界 Boss：同一隊（坦補戰戰）不同養成的傷害（聰明打，40 場）");
        sb.AppendLine("| 養成 | SR 隊 | UR 隊 | UR/SR |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var g in new[] { (40, 0, 4), (40, 4, 4), (50, 5, 5), (60, 0, 5), (60, 5, 5) })
        {
            var (ps, ts) = Team(Comps[0].Roles, false, g.Item1, g.Item2, g.Item3);
            var (pu, tu) = Team(Comps[0].Roles, true, g.Item1, g.Item2, g.Item3);
            var bs = Boss(ps, ts, true, 40); var bu = Boss(pu, tu, true, 40);
            sb.AppendLine($"| Lv{g.Item1}/{g.Item2}★/{g.Item3}階 | {bs.Avg:0}（{bs.Min}–{bs.Max}） | {bu.Avg:0}（{bu.Min}–{bu.Max}） | {bu.Avg / bs.Avg:0.00} |");
        }
        // 混合：UR 0★ vs SR 5★（大課剛抽到 UR 時的取捨）
        {
            var (ps, ts) = Team(Comps[0].Roles, false, 60, 5, 5);
            var (pu, tu) = Team(Comps[0].Roles, true, 60, 0, 5);
            sb.AppendLine($"| 對照：SR 5★ vs UR 0★（皆 Lv60/5階） | {Boss(ps, ts, true, 40).Avg:0} | {Boss(pu, tu, true, 40).Avg:0} | |");
        }

        sb.AppendLine();
        sb.AppendLine("## C. 打法：聰明打 vs 一般自動（坦補戰戰 SR，Lv36/3★/4階）");
        {
            var (p, team) = Team(Comps[0].Roles, false, 36, 3, 4);
            foreach (var s in stages)
            {
                var a = Rate(s, p, team, true, 60); var b = Rate(s, p, team, false, 60);
                sb.AppendLine($"- {s}：聰明 {a.Win:0}%（{a.AvgTurns:0.0} 回合）／自動 {b.Win:0}%（{b.AvgTurns:0.0} 回合）");
            }
            var (pm, tm) = Team(Comps[0].Roles, false, 60, 5, 5);
            sb.AppendLine($"- 世界 Boss（滿養成）：聰明 {Boss(pm, tm, true, 40).Avg:0}／自動 {Boss(pm, tm, false, 40).Avg:0}");
        }

        sb.AppendLine();
        sb.AppendLine("## D. 世界 Boss 輪替：各月 Boss 對滿養成 SR 隊與 UR 隊的傷害");
        foreach (var season in new[] { "2026-10", "2026-11", "2026-12", "2027-01", "2027-02", "2027-03" })
        {
            var (ps, ts) = Team(Comps[0].Roles, false, 60, 5, 5);
            var (pu, tu) = Team(Comps[0].Roles, true, 60, 5, 5);
            var bs = Boss(ps, ts, true, 30, season); var bu = Boss(pu, tu, true, 30, season);
            sb.AppendLine($"- {season} {WorldBoss.BossOf(season).Name}（HP {WorldBoss.BossOf(season).Base.Hp}×等級倍率）：SR {bs.Avg:0}／UR {bu.Avg:0}");
        }

        sb.AppendLine();
        sb.AppendLine("## E. 同職業的卡組是否不同（SR 與 UR 各取一隻比較卡名）");
        foreach (var r in Ids.Keys)
        {
            var a = HeroRoster.Find(Ids[r].Sr)!; var b = HeroRoster.Find(Ids[r].Ur)!; var c = HeroRoster.Find(AltSr[r])!;
            string Deck(HeroDef h) => string.Join("、", h.Deck.GroupBy(x => x.Name).Select(g => g.Count() > 1 ? $"{g.Key}×{g.Count()}" : g.Key));
            sb.AppendLine($"- {r}：{a.Name}［{Deck(a)}］／{c.Name}［{Deck(c)}］／{b.Name}［{Deck(b)}］");
        }
        return sb.ToString();
    }
}
