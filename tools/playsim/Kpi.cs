using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SanGuo.Core;

/// <summary>
/// 多輪模擬與驗收指標（docs/adjust-plan.md §1）。
/// multi：先跑一批（只有模擬帳號自己）→ 依付費比例抽樣出 100 名背景玩家的 Boss 成績 → 帶著族群再跑一批 → 彙總 kpi.md。
/// </summary>
public static class Kpi
{
    static readonly (string User, string Label)[] Personas =
    {
        ("f2p_player01", "無課"), ("light_spender01", "小課"), ("heavy_spender01", "大課"), ("whale_hypo01", "鯨魚"),
    };

    /// <summary>背景族群的付費比例（暫定）：無課 70%、小課 25%、大課 5%。</summary>
    static readonly (string User, int Weight)[] PopulationMix = { ("f2p_player01", 70), ("light_spender01", 25), ("heavy_spender01", 5) };
    const int PopulationSize = 100;

    public static async Task Multi(int runs, int days, string outDir)
    {
        Directory.CreateDirectory(outDir);
        string pre = Path.Combine(outDir, "pre");
        await RunBatch(runs, days, pre, null);
        string pop = Path.Combine(outDir, "population.csv");
        BuildPopulation(pre, pop);
        await RunBatch(runs, days, outDir, pop);
        Write(outDir);
    }

    static async Task RunBatch(int runs, int days, string dir, string pop)
    {
        string exe = Environment.ProcessPath!;
        bool viaDotnet = Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        var tasks = new List<Task>();
        for (int i = 1; i <= runs; i++)
        {
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            if (viaDotnet) psi.ArgumentList.Add(typeof(Kpi).Assembly.Location);
            psi.ArgumentList.Add(days.ToString());
            psi.ArgumentList.Add(Path.Combine(dir, "r" + i));
            if (pop != null) { psi.ArgumentList.Add("--pop"); psi.ArgumentList.Add(pop); }
            var proc = Process.Start(psi)!;
            tasks.Add(Task.Run(async () =>
            {
                // 兩個管線要同時讀，不然子程序寫滿其中一個就會卡住
                var outTask = proc.StandardOutput.ReadToEndAsync();
                var errTask = proc.StandardError.ReadToEndAsync();
                await Task.WhenAll(outTask, errTask);
                string err = errTask.Result;
                await proc.WaitForExitAsync();
                if (proc.ExitCode != 0) Console.Error.WriteLine($"run failed ({dir}): {err}");
            }));
        }
        await Task.WhenAll(tasks);
        Console.WriteLine($"完成 {runs} 輪：{dir}");
    }

    // ------------------------------------------------------------------ data

    sealed class Row : Dictionary<string, string>
    {
        public int I(string k) => int.Parse(this[k], CultureInfo.InvariantCulture);
        public double D(string k) => double.Parse(this[k], CultureInfo.InvariantCulture);
    }

    static List<Row> Load(string file)
    {
        var lines = File.ReadAllLines(file);
        var head = lines[0].Split(',');
        var rows = new List<Row>();
        foreach (var line in lines.Skip(1))
        {
            // 最後一欄（team）可能含逗號以外的字元，但不含逗號
            var cells = line.Split(',');
            var r = new Row();
            for (int i = 0; i < head.Length && i < cells.Length; i++) r[head[i]] = cells[i];
            rows.Add(r);
        }
        return rows;
    }

    static Dictionary<string, string> Summary(string file) =>
        File.Exists(file) ? File.ReadAllLines(file).Where(l => l.Contains('=')).ToDictionary(l => l[..l.IndexOf('=')], l => l[(l.IndexOf('=') + 1)..]) : new();

    static IEnumerable<string> RunDirs(string outDir) =>
        Directory.GetDirectories(outDir, "r*").Where(d => File.Exists(Path.Combine(d, "report.md"))).OrderBy(d => d);

    static void BuildPopulation(string preDir, string popFile)
    {
        var rng = new Random(20261009);
        var bySeason = new Dictionary<string, Dictionary<string, List<long>>>();
        foreach (var dir in RunDirs(preDir))
            foreach (var (user, _) in PopulationMix)
            {
                var rows = Load(Path.Combine(dir, $"daily-{user}.csv"));
                // 第 31 天 = 10/31（10 月賽季最終）；最後一天 = 11 月賽季到目前為止
                void Add(string season, Row r)
                {
                    if (!bySeason.TryGetValue(season, out var m)) bySeason[season] = m = new();
                    if (!m.TryGetValue(user, out var l)) m[user] = l = new();
                    l.Add(long.Parse(r["bossBest"]));
                }
                if (rows.Count >= 31) Add("2026-10", rows[30]);
                if (rows.Count > 31) Add("2026-11", rows[^1]);
            }
        var sb = new StringBuilder();
        int totalWeight = PopulationMix.Sum(m => m.Weight);
        foreach (var (season, m) in bySeason)
            for (int i = 0; i < PopulationSize; i++)
            {
                int roll = rng.Next(totalWeight), acc = 0;
                string user = PopulationMix.First(x => (acc += x.Weight) > roll).User;
                var list = m[user];
                // 背景玩家投入程度不一：在模擬帳號成績的 70%–105% 之間
                long score = (long)(list[rng.Next(list.Count)] * (0.70 + 0.35 * rng.NextDouble()));
                sb.AppendLine($"{season},{score}");
            }
        File.WriteAllText(popFile, sb.ToString());
    }

    // ------------------------------------------------------------------ report

    static int Cleared(string frontier)
    {
        if (frontier == "全通") return 70;
        var p = frontier.Split('-');
        return int.Parse(p[0]) * 10 + int.Parse(p[1]) - 1;
    }

    static string StageOf(double cleared)
    {
        int n = (int)Math.Round(cleared);
        if (n >= 70) return "全通";
        return $"{n / 10}-{n % 10 + 1}";
    }

    static string Fmt(IEnumerable<double> values, string f = "0")
    {
        var v = values.ToList();
        if (v.Count == 0) return "—";
        return $"{v.Average().ToString(f)}（{v.Min().ToString(f)}–{v.Max().ToString(f)}）";
    }

    static double Percentile(List<int> v, double q)
    {
        if (v.Count == 0) return 0;
        v.Sort();
        return v[Math.Min(v.Count - 1, (int)Math.Ceiling(q * v.Count) - 1)];
    }

    public static void Write(string outDir)
    {
        var dirs = RunDirs(outDir).ToList();
        var data = Personas.ToDictionary(p => p.User, p => dirs.Select(d => Load(Path.Combine(d, $"daily-{p.User}.csv"))).ToList());
        var sums = Personas.ToDictionary(p => p.User, p => dirs.Select(d => Summary(Path.Combine(d, $"summary-{p.User}.txt"))).ToList());
        var metas = dirs.Select(d => Summary(Path.Combine(d, "meta.txt"))).ToList();
        var f2p = data["f2p_player01"];
        int days = f2p.Min(r => r.Count);

        string AtDay(List<List<Row>> runs, int day, Func<Row, double> sel, string f = "0") =>
            day <= days ? Fmt(runs.Select(r => sel(r[day - 1])), f) : "—";
        string Progress(int day) => day <= days ? StageOf(f2p.Average(r => Cleared(r[day - 1]["frontier"]))) : "—";
        string Minutes(int a, int b, Func<Row, double> sel) =>
            a <= days ? Fmt(f2p.Select(r => r.Where(x => x.I("day") >= a && x.I("day") <= Math.Min(b, days)).Average(sel))) : "—";
        double Gear(Row r)
        {
            var m = Regex.Matches(r["team"], @"g(\d)(\d)(\d)");
            var t = m.SelectMany(x => new[] { x.Groups[1].Value, x.Groups[2].Value, x.Groups[3].Value }).Select(int.Parse).ToList();
            return t.Count == 0 ? 0 : t.Average();
        }

        var lossRatio = f2p.Select(r => { double l = r.Sum(x => x.I("storyLosses")); return 100 * l / Math.Max(1, l + r.Sum(x => x.I("newClears"))); });
        var attempts = sums["f2p_player01"].SelectMany(s => (s.GetValueOrDefault("attempts") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Select(x => int.Parse(x.Split(':')[1])).ToList();

        string Pulls(string user, int a, int b) =>
            b <= days ? Fmt(data[user].Select(r => (double)(r[b - 1].I("pulls") - (a > 0 ? r[a - 1].I("pulls") : 0)))) : "—";

        // 世界 Boss 名次：10 月結算（第 32 天後才有）
        int orderOk = 0, orderN = 0;
        for (int i = 0; i < dirs.Count; i++)
        {
            int Rank(string u) => int.TryParse(sums[u][i].GetValueOrDefault("settleRank"), out int x) ? x : 0;
            int rf = Rank("f2p_player01"), rl = Rank("light_spender01"), rh = Rank("heavy_spender01");
            if (rf == 0 || rl == 0 || rh == 0) continue;
            orderN++;
            if (rh < rl && rl < rf) orderOk++;
        }
        string Pct(string u) => Fmt(sums[u].Where(s => int.TryParse(s.GetValueOrDefault("settleRank"), out int r) && r > 0)
            .Select(s => 100.0 * int.Parse(s["settleRank"]) / int.Parse(s["settleTotal"])));

        // ---- 對照實验
        var exp = ExperimentKpis();

        var sb = new StringBuilder();
        sb.AppendLine($"# 驗收指標（{dirs.Count} 輪 × {days} 天；無課為主，括號為最小–最大）");
        sb.AppendLine();
        sb.AppendLine("| 類別 | 指標 | 現況 | 目標 |");
        sb.AppendLine("|---|---|---|---|");
        sb.AppendLine($"| 節奏 | 主線進度：第 1／7／14／30 天 | {Progress(1)}／{Progress(7)}／{Progress(14)}／{Progress(30)} | 0-10／2-x／4-x／全通（±3 天） |");
        sb.AppendLine($"| 節奏 | 主線全通日 | {Fmt(f2p.Select(r => (double)(r.FirstOrDefault(x => x["frontier"] == "全通")?.I("day") ?? 99)))} | 約第 30 天 |");
        sb.AppendLine($"| 節奏 | 帳號等級：第 7／14／30 天 | {AtDay(f2p, 7, r => r.I("level"))}／{AtDay(f2p, 14, r => r.I("level"))}／{AtDay(f2p, 30, r => r.I("level"))} | 約 25／32／40 |");
        sb.AppendLine($"| 節奏 | 主線隊平均裝備階：第 14／30 天 | {AtDay(f2p, 14, Gear, "0.0")}／{AtDay(f2p, 30, Gear, "0.0")} | 第 30 天 3–4 階 |");
        sb.AppendLine($"| 時間 | 每日分鐘：第 1 天／2–3／4–7／8–30 | {Minutes(1, 1, x => x.D("minutes"))}／{Minutes(2, 3, x => x.D("minutes"))}／{Minutes(4, 7, x => x.D("minutes"))}／{Minutes(8, 30, x => x.D("minutes"))} | 約 180／60–120／60／30 |");
        sb.AppendLine($"| 時間 | 第 31 天後每日分鐘（不含世界 Boss） | {Minutes(31, 999, x => x.D("minutes") - x.D("bossMin"))} | ≥ 15 |");
        sb.AppendLine($"| 終局 | 困難主線通關數（共 60）：第 30／45／60 天 | {AtDay(f2p, 30, r => r.I("hardCleared"))}／{AtDay(f2p, 45, r => r.I("hardCleared"))}／{AtDay(f2p, 60, r => r.I("hardCleared"))} | 第 2 個月持續有進度 |");
        sb.AppendLine($"| 挫折 | 主線敗場佔比 % | {Fmt(lossRatio)} | ≤ 30 |");
        sb.AppendLine($"| 挫折 | 單關挑戰次數 95 百分位 | {Percentile(attempts, 0.95)}（最多 {(attempts.Count > 0 ? attempts.Max() : 0)}） | ≤ 5 |");
        sb.AppendLine($"| 經濟 | 第 1 個月抽數：無課／小課／大課（鯨魚見補充） | {Pulls("f2p_player01", 0, 30)}／{Pulls("light_spender01", 0, 30)}／{Pulls("heavy_spender01", 0, 30)} | 約 100／120–135／500+ |");
        sb.AppendLine($"| 經濟 | 第 2 個月抽數（第 31–60 天）：無課／小課／大課 | {Pulls("f2p_player01", 30, 60)}／{Pulls("light_spender01", 30, 60)}／{Pulls("heavy_spender01", 30, 60)} | 約 70／90+／500+ |");
        sb.AppendLine($"| 課金 | 最佳 Boss 隊：可用 UR ÷ 只有 SR（滿養成；SR 最佳隊同職業全換 UR） | {exp.UrSrA:0.00}／{exp.UrSrB:0.00} | ≥ 1.25 |");
        sb.AppendLine($"| 課金 | SR 最佳隊換上剛抽到的 0★ UR（取最好的一次替換）÷ 原本 | {exp.Ur0Sr5:0.00} | ≥ 1.0 |");
        sb.AppendLine($"| 課金 | 10 月結算名次「大課 > 小課 > 無課」的比例 | {orderOk}/{orderN} | ≥ 8 成 |");
        sb.AppendLine($"| 課金 | 10 月結算百分位（越小越前）：無課／小課／大課 | {Pct("f2p_player01")}／{Pct("light_spender01")}／{Pct("heavy_spender01")} | 依序變好 |");
        sb.AppendLine($"| 技術 | SR 最佳 Boss 隊 ÷ UR 坦補戰戰（不懂配隊）的 Boss 傷害 | {exp.SkillVsMoney:0.00} | > 1（保留技術空間），但不宜 > 1.5 |");
        sb.AppendLine($"| 隨機 | 同隊 Boss 傷害變異係數（不同種子） | {exp.BossCv:0}% | ≤ 3%（P3 每日固定種子後同日為 0） |");
        sb.AppendLine($"| 深度 | 章末關勝率：專家／啟發式／自動（4-10、5-10、6-10 平均） | {exp.Expert:0}%／{exp.Smart:0}%／{exp.Auto:0}% | 專家 − 自動 ≥ 15 |");
        sb.AppendLine($"| 深度 | 出現過的不同卡牌數 | {Fmt(metas.Select(m => double.Parse(m.GetValueOrDefault("distinctCards") ?? "0")))} | ≥ 40 |");
        sb.AppendLine($"| 深度 | 主線最佳組合／Boss 最佳組合 | {exp.BestStory}／{exp.BestBoss} | 兩者不同 |");
        sb.AppendLine();
        sb.AppendLine("## 補充");
        sb.AppendLine($"- 世界 Boss 最佳隊：只有 SR = {exp.BestSrBoss}；可用 UR = {exp.BestUrBoss}");
        sb.AppendLine($"- 輸過後再贏的場次中，戰力沒變、靠換種子贏的比例：{Fmt(metas.Select(m => 100.0 * double.Parse(m.GetValueOrDefault("winsSamePower") ?? "0") / Math.Max(1, double.Parse(m.GetValueOrDefault("winsAfterLoss") ?? "1"))))}%");
        sb.AppendLine($"- 每次重打前的平均戰力提升：{Fmt(metas.Select(m => double.Parse(m.GetValueOrDefault("retryGain") ?? "0")), "0.0")}%");
        foreach (var (user, label) in Personas)
        {
            var runs = data[user];
            sb.AppendLine($"- {label}：全通日 {Fmt(runs.Select(r => (double)(r.FirstOrDefault(x => x["frontier"] == "全通")?.I("day") ?? 99)))}、第 30 天等級 {AtDay(runs, 30, r => r.I("level"))}、"
                + $"第 30 天裝備 {AtDay(runs, 30, Gear, "0.0")}、10 月 Boss 最佳 {AtDay(runs, 31, r => r.D("bossBest"))}、"
                + $"Boss 隊：{string.Join("｜", sums[user].Select(s => s.GetValueOrDefault("bossTeam")).Distinct().Take(3))}");
        }
        sb.AppendLine();
        sb.AppendLine("專家機器人：每回合 8 個隨機候選＋啟發式＋一般自動，各自往後推演 3 回合取最佳。背景族群：100 人，無課 70%／小課 25%／大課 5%，成績取模擬帳號的 70%–105%。");
        File.WriteAllText(Path.Combine(outDir, "kpi.md"), sb.ToString());
        Console.WriteLine(sb.ToString());
    }

    sealed class ExpResult
    {
        public double UrSrA, UrSrB, Ur0Sr5, SkillVsMoney, BossCv, Expert, Smart, Auto;
        public string BestStory = "", BestBoss = "", BestSrBoss = "", BestUrBoss = "";
    }

    static string UrOfSameRole(string id)
    {
        var role = HeroRoster.Find(id)!.Role;
        return HeroRoster.All().Where(h => h.Rarity == Rarity.UR && h.Role == role).Select(h => h.Id).FirstOrDefault();
    }

    /// <summary>指定武將（依職業自動站位）、Lv60、5 階裝，星級由 stars 決定；回傳世界 Boss 平均傷害。</summary>
    static double TeamBoss(List<string> ids, Func<string, int> stars, int seeds)
    {
        var p = SanGuo.Core.Meta.PlayerProfile.CreateNew(0);
        p.Level = 60;
        foreach (var id in ids)
        {
            var h = new SanGuo.Core.Meta.HeroState { HeroId = id, Level = 60, Stars = stars(id) };
            foreach (var s in SanGuo.Core.Meta.Equipment.Slots) h.Equipment[s.ToString()] = 5;
            p.Heroes[id] = h;
        }
        return Experiments.Boss(p, PlayerSim.Place(ids), true, seeds).Avg;
    }

    /// <summary>在可用武將中找世界 Boss 傷害最高的「坦 + 補 + 兩名輸出」（滿養成）。</summary>
    static (double Damage, List<string> Team) BestBossTeam(bool urAllowed)
    {
        var pool = HeroRoster.All().Where(h => h.Rarity == Rarity.SR || (urAllowed && h.Rarity == Rarity.UR)).ToList();
        var tanks = pool.Where(h => h.Role == Role.Tank).Select(h => h.Id).ToList();
        var healers = pool.Where(h => h.Role == Role.Healer).Select(h => h.Id).ToList();
        var dps = pool.Where(h => h.Role != Role.Tank && h.Role != Role.Healer).Select(h => h.Id).ToList();
        double best = -1; List<string> bestTeam = null;
        foreach (var t in tanks)
            foreach (var hl in healers)
                for (int a = 0; a < dps.Count; a++)
                    for (int b = a + 1; b < dps.Count; b++)
                    {
                        var ids = new List<string> { t, hl, dps[a], dps[b] };
                        double d = TeamBoss(ids, _ => 5, 4);
                        if (d > best) { best = d; bestTeam = ids; }
                    }
        return (TeamBoss(bestTeam!, _ => 5, 20), bestTeam!);
    }

    static ExpResult ExperimentKpis()
    {
        var r = new ExpResult();
        var tw = Experiments.Comps[0].Roles; // 坦補戰戰
        var tb = Experiments.Comps[2].Roles; // 坦補弓法
        double B(Role[] roles, bool ur, int lv, int st, int gear)
        {
            var (p, t) = Experiments.Team(roles, ur, lv, st, gear);
            return Experiments.Boss(p, t, true, 30).Avg;
        }
        // 課金上限：能用 UR 時的最佳 Boss 隊 ÷ 只有 SR 時的最佳 Boss 隊（同為 Lv60／5★／5 階）。
        // UR 有刷圖型與 Boss 型之分，不能拿同職業硬比，所以各自在可用武將中找最佳組合。
        var (srBest, srTeam) = BestBossTeam(urAllowed: false);
        var (urBest, urTeam) = BestBossTeam(urAllowed: true);
        r.UrSrA = urBest / srBest;
        r.BestSrBoss = string.Join("+", srTeam.Select(id => HeroRoster.Find(id)!.Name));
        r.BestUrBoss = string.Join("+", urTeam.Select(id => HeroRoster.Find(id)!.Name));
        // 剛抽到 UR：在 SR 最佳隊裡，把同職業的 5★ SR 換成 0★ UR（取各職業中最好的那次替換）
        double bestSwap = 0;
        foreach (var urId in HeroRoster.All().Where(h => h.Rarity == Rarity.UR).Select(h => h.Id))
        {
            var role = HeroRoster.Find(urId)!.Role;
            int i = srTeam.FindIndex(id => HeroRoster.Find(id)!.Role == role);
            if (i < 0) continue;
            var swapped = new List<string>(srTeam) { [i] = urId };
            double dmg = TeamBoss(swapped, id => id == urId ? 0 : 5, 10);
            bestSwap = Math.Max(bestSwap, dmg / srBest);
        }
        r.Ur0Sr5 = bestSwap;
        var used = new HashSet<string>();
        var allUr = srTeam.Select(id => { var u = UrOfSameRole(id); return u != null && used.Add(u) ? u : id; }).ToList();
        r.UrSrB = TeamBoss(allUr, _ => 5, 10) / srBest; // SR 最佳隊同職業換 UR（同一隻 UR 只換一次）
        r.SkillVsMoney = srBest / B(tw, true, 60, 5, 5);
        {
            var (p, t) = Experiments.Team(tw, false, 60, 5, 5);
            var bo = Experiments.Boss(p, t, true, 30);
            r.BossCv = 100 * bo.Sd / bo.Avg;
        }

        // 深度：專家 vs 啟發式 vs 自動
        {
            var (p, team) = Experiments.Team(tw, false, 36, 3, 4);
            var stages = new[] { "4-10", "5-10", "6-10" };
            int n = 12, we = 0, ws = 0, wa = 0;
            foreach (var s in stages)
                for (ulong seed = 1; seed <= (ulong)n; seed++)
                {
                    ulong sd = seed * 7919;
                    BattleSetup Make() => SanGuo.Core.Meta.DemoMeta.BuildSetup(s, sd, p, team)!;
                    var b = new Battle(Make());
                    var rec = new SanGuo.Core.Data.ReplayRecorder(b);
                    var ex = new ExpertBot(Make);
                    for (int i = 0; i < 60 && b.Result == BattleResult.Ongoing; i++) ex.PlayTurn(rec);
                    if (b.Result == BattleResult.Won) we++;
                }
            foreach (var s in stages)
            {
                ws += (int)Math.Round(Experiments.Rate(s, p, team, true, n).Win * n / 100);
                wa += (int)Math.Round(Experiments.Rate(s, p, team, false, n).Win * n / 100);
            }
            int total = stages.Length * n;
            r.Expert = 100.0 * we / total; r.Smart = 100.0 * ws / total; r.Auto = 100.0 * wa / total;
        }

        // 主線最佳組合（6-10 勝率）與 Boss 最佳組合（傷害），SR、第 5 章末養成
        double bestS = -1, bestB = -1;
        foreach (var (name, roles) in Experiments.Comps)
        {
            var (p, t) = Experiments.Team(roles, false, 36, 3, 4);
            double win = Experiments.Rate("6-10", p, t, true, 20).Win;
            if (win > bestS) { bestS = win; r.BestStory = name; }
            var (pm, tm) = Experiments.Team(roles, false, 60, 5, 5);
            double dmg = Experiments.Boss(pm, tm, true, 20).Avg;
            if (dmg > bestB) { bestB = dmg; r.BestBoss = name; }
        }
        return r;
    }
}
