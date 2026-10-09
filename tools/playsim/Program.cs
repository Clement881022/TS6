// 用法：dotnet run --project tools/playsim -- <天數> <輸出資料夾>（模擬帳號遊玩）；dotnet run --project tools/playsim -- exp <輸出檔>（對照實驗）。
// 報告見 docs/playtest-sim.md。
using System.Text;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using SanGuo.Server;

// ============================================================================
// 三國將星傳：玩家視角模擬試玩
// 透過真實的 SqliteAccountStore 註冊帳號、GameService（伺服器規則入口）遊玩：
// 開打取得種子 → 本機建立同一場戰鬥由「玩家機器人」出牌並錄製 → 伺服器重播驗證結算。
// ============================================================================

Console.OutputEncoding = Encoding.UTF8;
if (args.Length > 0 && args[0] == "exp") { var t = Experiments.Run(); File.WriteAllText(args.Length > 1 ? args[1] : "experiments.md", t); Console.WriteLine(t); return; }
int days = args.Length > 0 ? int.Parse(args[0]) : 60;
string outDir = args.Length > 1 ? args[1] : ".";
Directory.CreateDirectory(outDir);

string db = Path.Combine(Path.GetTempPath(), $"playsim-{Guid.NewGuid():N}.db");
var clock = new SimClock { Current = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.FromHours(8)) };
var store = new SqliteProfileStore($"Data Source={db}");
var board = new SqliteWorldBossBoard($"Data Source={db}");
var accounts = new SqliteAccountStore($"Data Source={db}", clock);
var game = new GameService(store, board, clock, new ServerOptions { EnableDevEndpoints = true }, accounts);

var personas = new List<Persona>
{
    new Persona("無課", "f2p_player01", 0),
    new Persona("小課", "light_spender01", 1),
    new Persona("大課", "heavy_spender01", 2),
    new Persona("假設鯨魚", "whale_hypo01", 3),
};
foreach (var pe in personas)
{
    var s = accounts.Register(pe.Username, "Playtest#2026");
    pe.AccountId = s!.AccountId;
    accounts.SetNickname(pe.AccountId, pe.Label);
    pe.Sim = new PlayerSim(pe, game, store, board, clock);
}

int[] sessionHours = { 8, 13, 21 };
var start = clock.Current.Date;
for (int d = 0; d < days; d++)
{
    foreach (int h in sessionHours)
    {
        clock.Current = new DateTimeOffset(start.AddDays(d).AddHours(h), TimeSpan.FromHours(8));
        foreach (var pe in personas) await pe.Sim.Session(d + 1, h);
    }
    foreach (var pe in personas) await pe.Sim.EndOfDay(d + 1);
}

// ---- 報表 ----
var sb = new StringBuilder();
foreach (var pe in personas) sb.Append(pe.Sim.Report());
sb.AppendLine();
sb.AppendLine("# 戰鬥 meta（全部帳號合計）");
sb.Append(Meta.Report());
File.WriteAllText(Path.Combine(outDir, "report.md"), sb.ToString());
foreach (var pe in personas) File.WriteAllText(Path.Combine(outDir, $"daily-{pe.Username}.csv"), pe.Sim.DailyCsv());
Console.WriteLine(sb.ToString());
Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
try { File.Delete(db); } catch { }

// ============================================================================

public sealed class SimClock : TimeProvider
{
    public DateTimeOffset Current;
    public override DateTimeOffset GetUtcNow() => Current.ToUniversalTime();
}

public sealed class Persona
{
    public string Label, Username, AccountId;
    /// <summary>0 無課；1 小課（首儲 + 小月卡 + 通行證，每月 ¥66）；2 大課（全部商品，每月 ¥202）；3 大課 + 假設每月可直購 20000 元寶（約 ¥2000，遊戲目前沒有這個商品）。</summary>
    public int Tier;
    public PlayerSim Sim;
    public Persona(string label, string user, int tier) { Label = label; Username = user; Tier = tier; }
}

/// <summary>每日統計。</summary>
public sealed class DayStat
{
    public int Day;
    public double Seconds;
    public double BattleSeconds, StorySeconds, MenuSeconds, BossSeconds;
    public int Battles, Losses, NewClears, Sweeps;
    public int StaminaSpent, StaminaWasted;
    public int Level;
    public string Frontier = "";
    public string Team = "";
    public double Power;
    public int Yuanbao, PullsTotal, UrOwned;
    public long BossBest, BossToday;
    public int SpendCny;
    public string Notes = "";
}

public sealed class PlayerSim
{
    private readonly Persona _pe;
    private readonly GameService _g;
    private readonly IProfileStore _store;
    private readonly IWorldBossBoard _board;
    private readonly SimClock _clock;
    private DayStat _today;
    private readonly List<DayStat> _days = new();
    private int _pulls, _spendTotal, _lastStaminaEnd = -1;
    private long _lastSessionTime;
    private string _lastMonthInjected = "";
    private readonly List<string> _events = new();
    private readonly Dictionary<string, int> _attemptsPerStage = new();
    private readonly List<long> _bossScores = new();
    private int _clearedDay6 = -1;
    private readonly Dictionary<int, int> _chapterDoneDay = new();
    private readonly HashSet<string> _seenStory = new();
    private string _lastSettle = "";

    public PlayerSim(Persona pe, GameService g, IProfileStore store, IWorldBossBoard board, SimClock clock)
    { _pe = pe; _g = g; _store = store; _board = board; _clock = clock; }

    private string Id => _pe.AccountId;
    private long Now => _clock.GetUtcNow().ToUnixTimeSeconds();
    private Task<PlayerProfile> P() => _store.LoadAsync(Id)!;
    private void Menu(double s) { _today.Seconds += s; _today.MenuSeconds += s; }

    // ------------------------------------------------------------------ session

    public async Task Session(int day, int hour)
    {
        if (_today == null || _today.Day != day) _today = new DayStat { Day = day };
        var login = await _g.Login(Id);
        Menu(30); // 開遊戲、進主城、看紅點
        var p = await P();

        // 體力溢出（上次離線時體力已滿 / 快滿，自然回復被浪費）
        int cur = p.Stamina.Get(Now);
        if (_lastStaminaEnd >= 0)
        {
            int regen = (int)((Now - _lastSessionTime) / PlayerLevelCurve.StaminaRegenSeconds);
            int theoretical = _lastStaminaEnd + regen;
            if (_lastStaminaEnd < p.Stamina.Cap && theoretical > p.Stamina.Cap) _today.StaminaWasted += theoretical - p.Stamina.Cap;
            else if (_lastStaminaEnd >= p.Stamina.Cap) _today.StaminaWasted += regen;
        }

        await Purchases(day);
        await ClaimAll();

        await Growth();
        await PushStory();
        await WorldBossFights();
        await DailyStageQuest();
        await Dungeons();
        await Growth();
        await Gacha();
        await Growth();
        await ClaimAll();
        // 領完任務 / 通行證後可能又有體力或資源：再刷一輪
        await Dungeons();

        p = await P();
        _lastStaminaEnd = p.Stamina.Get(Now);
        _lastSessionTime = Now;
    }

    public async Task EndOfDay(int day)
    {
        var p = await P();
        _today.Level = p.Level;
        var (c, l) = Campaign.Frontier(p.ClearedStages);
        bool all = p.ClearedStages.Contains("6-10");
        _today.Frontier = all ? "全通" : $"{c}-{l}";
        var team = ChooseTeam(p);
        _today.Team = string.Join("/", team.Select(t => $"{Short(t.HeroId)}{p.Heroes[t.HeroId].Level}★{p.Heroes[t.HeroId].Stars}g{GearStr(p.Heroes[t.HeroId])}"));
        _today.Power = TeamPower(p, team);
        _today.Yuanbao = p.Yuanbao;
        _today.PullsTotal = _pulls;
        _today.UrOwned = p.Heroes.Keys.Count(k => HeroGrowth.RarityOf(k) == Rarity.UR);
        _today.BossBest = p.WorldBoss.Best;
        _today.SpendCny = _spendTotal;
        if (p.WorldBoss.LastSeason != "") _lastSettle = $"{p.WorldBoss.LastSeason} 第 {p.WorldBoss.LastRank}/{p.WorldBoss.LastTotal} 名，獎勵 {p.WorldBoss.LastReward} 元寶 {p.WorldBoss.Title}";
        if (all && _clearedDay6 < 0) _clearedDay6 = day;
        for (int ch = 0; ch <= 6; ch++)
            if (!_chapterDoneDay.ContainsKey(ch) && p.ClearedStages.Contains($"{ch}-10")) _chapterDoneDay[ch] = day;
        _days.Add(_today);
    }

    // ------------------------------------------------------------------ shop

    private async Task Buy(string product, int cny)
    {
        var o = await _g.CreateOrder(Id, product);
        if (!o.Ok) return;
        string orderId = (string)o.Data!.GetType().GetProperty("orderId")!.GetValue(o.Data)!;
        var r = await _g.DevPay(Id, orderId);
        if (r.Ok) { _spendTotal += cny; Menu(20); _events.Add($"D{_today.Day} 購買 {product} ¥{cny}"); }
    }

    private async Task Purchases(int day)
    {
        if (_pe.Tier == 0) return;
        var p = await P();
        long now = Now;
        if (!p.FirstPackBought) await Buy(Shop.FirstPack, 6);
        if (Shop.MonthCardDaysLeft(p, Shop.MonthSmall, now) <= 1) await Buy(Shop.MonthSmall, 30);
        p = await P();
        if (_pe.Tier >= 2 && Shop.MonthCardDaysLeft(p, Shop.MonthBig, now) <= 1) await Buy(Shop.MonthBig, 68);
        p = await P();
        BattlePass.Roll(p, now);
        if (p.Pass.Tier == "") await Buy(_pe.Tier >= 2 ? Shop.PassLuxury : Shop.PassBasic, _pe.Tier >= 2 ? 98 : 30);
        // 假設的直購元寶（遊戲目前沒有這類商品）：直接寫入存檔，模擬每月 ¥2000
        string month = DailyClock.MonthKey(now);
        if (_pe.Tier == 3 && _lastMonthInjected != month)
        {
            _lastMonthInjected = month;
            var q = await P();
            q.Yuanbao += 20000;
            await _store.SaveAsync(Id, q);
            _spendTotal += 2000;
            _events.Add($"D{day} 假設直購 20000 元寶 ¥2000");
        }
    }

    private async Task ClaimAll()
    {
        foreach (var card in new[] { Shop.MonthSmall, Shop.MonthBig })
            if ((await _g.ClaimMonthCard(Id, card)).Ok) Menu(2);
        foreach (var q in DemoQuests.Book.Quests)
            if ((await _g.ClaimQuest(Id, q.Id)).Ok) Menu(2);
        foreach (var m in DemoQuests.Book.Milestones)
            if ((await _g.ClaimMilestone(Id, m.Points)).Ok) { Menu(3); _events.Add($"D{_today.Day} 七日里程碑 {m.Points}"); }
        if ((await _g.ClaimPassAll(Id)).Ok) Menu(4);
    }

    // ------------------------------------------------------------------ team & growth

    private static string Short(string id) => HeroRoster.Find(id) is { } h ? (h.Rarity == Rarity.UR ? "UR" + h.Name.Split('．').Last() : h.Name) : id;
    private static string GearStr(HeroState h) => string.Concat(Equipment.Slots.Select(s => h.Equipment.TryGetValue(s.ToString(), out int t) ? t.ToString() : "0"));

    private static double Score(PlayerProfile p, string id, bool potential)
    {
        var def = HeroRoster.Find(id)!;
        var st = p.Heroes[id];
        var probe = new HeroState { HeroId = id, Level = potential ? Math.Max(st.Level, p.Level) : st.Level, Stars = st.Stars, Equipment = st.Equipment };
        var s = HeroGrowth.ScaleStats(def, probe);
        double rw = def.Rarity == Rarity.UR ? 1.35 : def.Rarity == Rarity.SR ? 1.15 : 1.0; // 稀有度只提高卡牌倍率，玩家會優先上高稀有度
        return Math.Sqrt(s.Hp * (double)Math.Max(Math.Max(s.Atk, s.Int), 1) + s.Def * 50.0) * rw;
    }

    public static double TeamPower(PlayerProfile p, List<FormationEntry> team) => team.Sum(t => Score(p, t.HeroId, false));

    /// <summary>坦克 + 補師 + 兩名最強輸出（以「拉到帳號等級時」的潛力比較，模擬玩家換上新抽到的強角）。</summary>
    public static List<FormationEntry> ChooseTeam(PlayerProfile p)
    {
        var owned = p.Heroes.Keys.Where(k => HeroRoster.Find(k) != null).ToList();
        Role R(string id) => HeroRoster.Find(id)!.Role;
        var picked = new List<string>();
        string Best(Func<string, bool> f) => owned.Where(x => !picked.Contains(x) && f(x)).OrderByDescending(x => Score(p, x, true)).FirstOrDefault();
        var tank = Best(x => R(x) == Role.Tank); if (tank != null) picked.Add(tank);
        var healer = Best(x => R(x) == Role.Healer); if (healer != null) picked.Add(healer);
        while (picked.Count < 4)
        {
            var x = Best(_ => true);
            if (x == null) break;
            picked.Add(x);
        }
        var front = new Queue<(int, int)>(new[] { (2, 3), (1, 3), (3, 3) });
        var back = new Queue<(int, int)>(new[] { (2, 4), (1, 4), (3, 4) });
        var team = new List<FormationEntry>();
        foreach (var id in picked.OrderBy(x => R(x) == Role.Tank ? 0 : R(x) == Role.Warrior ? 1 : 2))
        {
            bool melee = R(id) == Role.Tank || R(id) == Role.Warrior;
            var q = melee && front.Count > 0 ? front : back.Count > 0 ? back : front;
            var (l, r) = q.Dequeue();
            team.Add(new FormationEntry(id, l, r));
        }
        return team;
    }

    private async Task Growth()
    {
        var p = await P();
        var team = ChooseTeam(p);
        var ids = team.Select(t => t.HeroId).ToList();
        if (ids.Count == 0) return;

        // 將魂商店：先換隊伍成員的重複份，再換經驗 / 金幣
        foreach (var id in ids.OrderByDescending(i => HeroGrowth.RarityOf(i)))
            if ((await _g.BuySoulItem(Id, "shard:" + id)).Ok) Menu(3);
        foreach (var it in new[] { "hero_exp", "gold" })
            while ((await _g.BuySoulItem(Id, it)).Ok) Menu(2);

        // 突破
        foreach (var id in ids)
            while ((await _g.Breakthrough(Id, id)).Ok) { Menu(6); _events.Add($"D{_today.Day} 突破 {Short(id)}"); }

        // 裝備：各部位穿最高階
        p = await P();
        foreach (var id in ids)
            foreach (var slot in Equipment.Slots)
            {
                p.Heroes[id].Equipment.TryGetValue(slot.ToString(), out int curTier);
                for (int t = Equipment.MaxTier; t > curTier; t--)
                    if (Equipment.Count(p, slot, t) > 0) { if ((await _g.Equip(Id, id, slot.ToString(), t)).Ok) Menu(3); p = await P(); break; }
            }

        // 分解比隊伍已穿戴最低階還低的庫存裝備
        p = await P();
        foreach (var slot in Equipment.Slots)
        {
            int min = ids.Min(i => p.Heroes[i].Equipment.TryGetValue(slot.ToString(), out int t) ? t : 0);
            for (int t = 1; t < min; t++)
            {
                int n = Equipment.Count(p, slot, t);
                if (n > 0 && (await _g.Dismantle(Id, slot.ToString(), t, n)).Ok) Menu(3);
            }
        }

        // 升級：一次升最低等的隊員（保持平均）
        int ups = 0;
        while (true)
        {
            p = await P();
            var target = ids.OrderBy(i => p.Heroes[i].Level).FirstOrDefault(i => p.Heroes[i].Level < p.Level);
            if (target == null) break;
            if (!(await _g.LevelUp(Id, target)).Ok) break;
            ups++;
        }
        // 假設升級介面可「升 1 / 升 5 / 升到上限」：每 5 級約 2 秒
        if (ups > 0) Menu(4 + ups * 0.4);
    }

    // ------------------------------------------------------------------ battles

    private static int StoryLines(string stageId, bool after)
    {
        if (!Campaign.TryParse(stageId, out int c, out int l)) return 0;
        return (after ? CampaignStory.After(c, l) : CampaignStory.Before(c, l)).Count;
    }

    /// <summary>開打 → 本機打完並錄製 → 伺服器重播結算。回傳 (已結算, 勝利, 回合, 伺服器結果)。</summary>
    private async Task<(bool Ok, bool Won, int Turns, ApiResult R, Battle B)> Fight(string stageId, bool manual, string kind)
    {
        var p = await P();
        var team = ChooseTeam(p);
        var start = await _g.StartStage(Id, stageId, team);
        if (!start.Ok) return (false, false, 0, start, null);
        ulong seed = (ulong)(long)start.Data!.GetType().GetProperty("seed")!.GetValue(start.Data)!;
        p = await P();
        var setup = DemoMeta.BuildSetup(stageId, seed, p, team)!;
        var battle = new Battle(setup);
        var rec = new ReplayRecorder(battle);
        var bot = new SmartBot(kind, team.Select(t => t.HeroId).ToList());
        int limit = setup.TurnLimit > 0 ? setup.TurnLimit + 1 : 60;
        for (int i = 0; i < 60 && battle.Result == BattleResult.Ongoing; i++) bot.PlayTurn(rec);
        var fin = await _g.FinishStage(Id, stageId, rec.Actions);
        bool won = fin.Ok && (bool)fin.Data!.GetType().GetProperty("won")!.GetValue(fin.Data)!;
        int turns = battle.Turn;

        // 計時：手動每回合約 30 秒（看牌、選目標、演出），自動每回合約 8 秒
        double secs = manual ? 20 + turns * 30 : 10 + turns * 8;
        _today.Seconds += secs; _today.BattleSeconds += secs; _today.Battles++;
        if (!won) _today.Losses++;
        Meta.RecordBattle(kind, stageId, team, battle, won);

        // 反事實：同一場用「一般自動戰鬥」與「只出基本牌」會不會贏（衡量操作 / 卡牌選擇是否重要）
        if (kind == "story")
        {
            var auto = new Battle(DemoMeta.BuildSetup(stageId, seed, p, team)!);
            for (int i = 0; i < 60 && auto.Result == BattleResult.Ongoing; i++) AutoPlayer.PlayTurn(auto);
            var basic = new Battle(DemoMeta.BuildSetup(stageId, seed, p, team)!);
            var bb = new SmartBot("basic", null) { BasicOnly = true };
            var brec = new ReplayRecorder(basic);
            for (int i = 0; i < 60 && basic.Result == BattleResult.Ongoing; i++) bb.PlayTurn(brec);
            Meta.RecordCounterfactual(stageId, won, auto.Result == BattleResult.Won, basic.Result == BattleResult.Won);
        }
        return (fin.Ok, won, turns, fin, battle);
    }

    private async Task PushStory()
    {
        for (int guard = 0; guard < 80; guard++)
        {
            var p = await P();
            if (p.ClearedStages.Contains("6-10")) return;
            if (p.Stamina.Get(Now) < 10) return;
            var (c, l) = Campaign.Frontier(p.ClearedStages);
            string sid = Campaign.StageId(c, l);
            _attemptsPerStage.TryGetValue(sid, out int tries);
            // 劇情：第一次進關播放戰前對白，首通後播放戰後對白；每句約 3 秒
            if (_seenStory.Add(sid))
            {
                double s = StoryLines(sid, false) * 3;
                _today.Seconds += s; _today.StorySeconds += s;
            }
            var (ok, won, turns, _, _) = await Fight(sid, true, "story");
            if (!ok) return;
            _attemptsPerStage[sid] = tries + 1;
            if (won)
            {
                double s = StoryLines(sid, true) * 3;
                _today.Seconds += s; _today.StorySeconds += s;
                _today.NewClears++;
                if (tries > 0) _events.Add($"D{_today.Day} {sid} 第 {tries + 1} 次才過");
                await Growth();
                continue;
            }
            // 輸了：養成一下再試一次；再輸就這個時段不推主線
            await Growth();
            var (ok2, won2, _, _, _) = await Fight(sid, true, "story");
            _attemptsPerStage[sid] = tries + 2;
            if (ok2 && won2) { _today.NewClears++; _events.Add($"D{_today.Day} {sid} 第 {tries + 2} 次才過"); await Growth(); continue; }
            return;
        }
    }

    private async Task WorldBossFights()
    {
        var p = await P();
        if (!WorldBoss.IsUnlocked(p)) return;
        while (WorldBoss.AttemptsLeft(await P(), Now) > 0)
        {
            var before = _today.Seconds;
            var (ok, _, turns, r, b) = await Fight(WorldBoss.StageId, true, "boss");
            if (!ok) break;
            long dmg = (long)r.Data!.GetType().GetProperty("damage")!.GetValue(r.Data)!;
            _bossScores.Add(dmg);
            _today.BossToday = Math.Max(_today.BossToday, dmg);
            // 世界 Boss 為了分數手動打，換算成手動時間（Fight 已計入，改歸類）
            double spent = _today.Seconds - before;
            _today.BattleSeconds -= spent; _today.BossSeconds += spent;
        }
    }

    private static readonly int[] ResStamina = { 20, 25, 30, 35, 40 };

    private async Task DailyStageQuest()
    {
        var p = await P();
        var q = DemoQuests.Book.Find("d_stage")!;
        int have = Quests.Progress(p, q);
        if (p.DailyTaskClaimed.Contains("d_stage") || have >= q.Target) return;
        var best = p.StageStars.Where(kv => kv.Value >= 3).Select(kv => kv.Key)
            .OrderByDescending(s => { Campaign.TryParse(s, out int c, out int l); return c * 10 + l; }).FirstOrDefault();
        if (best == null) return;
        int n = q.Target - have;
        if (p.Stamina.Get(Now) < 10 * n) return;
        if ((await _g.SweepStage(Id, best, n)).Ok) { Menu(6); _today.Sweeps += n; _today.StaminaSpent += 10 * n; }
    }

    private async Task Dungeons()
    {
        for (int guard = 0; guard < 20; guard++)
        {
            var p = await P();
            int stamina = p.Stamina.Get(Now);
            var defs = DemoResourceDungeons.Create();
            var unlocked = defs.Where(d => ResourceDungeons.IsUnlocked(p, d)).ToList();
            if (unlocked.Count == 0) return;
            var top = unlocked.Last();
            if (!p.ClearedStages.Contains(top.Id) && stamina >= top.StaminaCost)
            {
                // 新階首次要打一場（自動）；一天最多試 2 次
                _attemptsPerStage.TryGetValue(top.Id + "@" + _today.Day, out int t);
                if (t < 2)
                {
                    _attemptsPerStage[top.Id + "@" + _today.Day] = t + 1;
                    var (ok, won, _, _, _) = await Fight(top.Id, false, "dungeon");
                    if (ok) _today.StaminaSpent += top.StaminaCost;
                    if (won) _events.Add($"D{_today.Day} 解鎖並通關 {top.Name}");
                    continue;
                }
            }
            var target = unlocked.LastOrDefault(d => p.ClearedStages.Contains(d.Id));
            if (target == null)
            {
                // 連第一階都沒打過
                if (stamina >= unlocked[0].StaminaCost) { var (ok, _, _, _, _) = await Fight(unlocked[0].Id, false, "dungeon"); if (ok) _today.StaminaSpent += unlocked[0].StaminaCost; continue; }
                return;
            }
            int n = Math.Min(ResourceDungeons.MaxSweepCount, stamina / target.StaminaCost);
            if (n <= 0)
            {
                // 剩下的體力打不起高階，就掃低一階
                var cheaper = unlocked.Where(d => p.ClearedStages.Contains(d.Id) && d.StaminaCost <= stamina).LastOrDefault();
                if (cheaper == null) return;
                target = cheaper; n = Math.Min(10, stamina / target.StaminaCost);
            }
            var r = await _g.SweepDungeon(Id, target.Id, n);
            if (!r.Ok) return;
            Menu(6); _today.Sweeps += n; _today.StaminaSpent += n * target.StaminaCost;
        }
    }

    private async Task Gacha()
    {
        var p = await P();
        p.PoolStates.TryGetValue(DemoMeta.NewbiePoolId, out var nb);
        while (true)
        {
            p = await P();
            string pool = (nb == null || nb.TenPulls == 0) ? DemoMeta.NewbiePoolId : DemoMeta.UpPoolId;
            if (p.Yuanbao < 2000) break;
            var r = await _g.Pull(Id, pool, 10);
            if (!r.Ok) break;
            _pulls += 10; Menu(25);
            p = await P(); p.PoolStates.TryGetValue(DemoMeta.NewbiePoolId, out nb);
            var results = (System.Collections.IEnumerable)r.Data!.GetType().GetProperty("results")!.GetValue(r.Data)!;
            foreach (var x in results)
            {
                var rar = (string)x.GetType().GetProperty("rarity")!.GetValue(x)!;
                var hid = (string)x.GetType().GetProperty("heroId")!.GetValue(x)!;
                if (rar == "UR") _events.Add($"D{_today.Day} 抽到 UR {Short(hid)}（第 {_pulls} 抽內）");
            }
        }
        // 每日任務「抽卡 1 次」：手上有 200 以上、但湊不到十連時，只有付費玩家會單抽
        p = await P();
        if (!p.DailyTaskClaimed.Contains("d_gacha") && Quests.Progress(p, DemoQuests.Book.Find("d_gacha")!) < 1 && _pe.Tier >= 2 && p.Yuanbao >= 200)
            if ((await _g.Pull(Id, DemoMeta.UpPoolId, 1)).Ok) { _pulls++; Menu(8); }
    }

    // ------------------------------------------------------------------ report

    public string DailyCsv()
    {
        var sb = new StringBuilder("day,minutes,battleMin,storyMin,bossMin,menuMin,battles,losses,newClears,sweeps,staminaSpent,staminaWasted,level,frontier,power,yuanbao,pulls,ur,bossBest,spend,bossToday,team\n");
        foreach (var d in _days)
            sb.AppendLine($"{d.Day},{d.Seconds / 60:0.0},{d.BattleSeconds / 60:0.0},{d.StorySeconds / 60:0.0},{d.BossSeconds / 60:0.0},{d.MenuSeconds / 60:0.0},{d.Battles},{d.Losses},{d.NewClears},{d.Sweeps},{d.StaminaSpent},{d.StaminaWasted},{d.Level},{d.Frontier},{d.Power:0},{d.Yuanbao},{d.PullsTotal},{d.UrOwned},{d.BossBest},{d.SpendCny},{d.BossToday},{d.Team}");
        return sb.ToString();
    }

    public string Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {_pe.Label}（{_pe.Username}）累計花費 ¥{_spendTotal}");
        sb.AppendLine();
        sb.AppendLine("| 天 | 分鐘 | 戰鬥 | 劇情 | Boss | 介面 | 新通關 | 敗場 | 等級 | 進度 | 戰力 | 抽數 | UR | Boss最佳 | 隊伍 |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var d in _days)
            sb.AppendLine($"| {d.Day} | {d.Seconds / 60:0} | {d.BattleSeconds / 60:0} | {d.StorySeconds / 60:0} | {d.BossSeconds / 60:0} | {d.MenuSeconds / 60:0} | {d.NewClears} | {d.Losses} | {d.Level} | {d.Frontier} | {d.Power:0} | {d.PullsTotal} | {d.UrOwned} | {d.BossBest} | {d.Team} |");
        sb.AppendLine();
        sb.AppendLine("章節完成日：" + string.Join("、", _chapterDoneDay.OrderBy(k => k.Key).Select(k => $"第{k.Key}章 D{k.Value}")));
        sb.AppendLine($"主線全通：{(_clearedDay6 > 0 ? "D" + _clearedDay6 : "未完成")}");
        var hard = _attemptsPerStage.Where(kv => !kv.Key.Contains('@') && kv.Value >= 3).OrderByDescending(kv => kv.Value).Take(12);
        sb.AppendLine("嘗試 3 次以上的關卡：" + string.Join("、", hard.Select(kv => $"{kv.Key}×{kv.Value}")));
        sb.AppendLine($"世界 Boss 上季結算：{_lastSettle}");
        sb.AppendLine($"世界 Boss 場次 {_bossScores.Count}，最高 {(_bossScores.Count > 0 ? _bossScores.Max() : 0)}");
        sb.AppendLine();
        sb.AppendLine("事件：");
        foreach (var e in _events.Where(e => !e.Contains("突破")).Take(80)) sb.AppendLine("- " + e);
        sb.AppendLine($"- 突破次數：{_events.Count(e => e.Contains("突破"))}");
        sb.AppendLine();
        return sb.ToString();
    }
}

/// <summary>
/// 玩家機器人：沿用 TutorialBalanceTests 的「照教學打」策略（會挑目標、補血看血量、嘲諷看時機），透過錄製器出牌。
/// BasicOnly = 只出基本牌（衡量特殊牌是否重要）。
/// </summary>
public sealed class SmartBot
{
    public bool BasicOnly;
    private readonly string _kind;
    public SmartBot(string kind, List<string> team) { _kind = kind; }

    public void PlayTurn(ReplayRecorder rec)
    {
        var b = rec.Battle;
        int startPlayable = b.Hand.Count(c => c.Def.Target != TargetRule.MoveDest && b.CanPlay(c) == PlayResult.Ok);
        int played = 0;
        for (int guard = 0; guard < 60 && b.Result == BattleResult.Ongoing; guard++)
        {
            if (PlayOne(rec)) { played++; continue; }
            var move = b.Hand.FirstOrDefault(c => c.Def.Target == TargetRule.MoveDest && b.CanPlay(c) == PlayResult.Ok);
            var adv = move == null ? null : b.AliveUnits(Side.Player)
                .Where(h => !h.Protected && h.Hero != null && b.CanMoveUnit(h) && AutoPlayer.ChooseMove(b, h) != null)
                .OrderBy(h => h.Hero!.Role == Role.Tank ? 1 : 0).FirstOrDefault();
            if (move != null && adv != null) { rec.Play(move, AutoPlayer.ChooseMove(b, adv), adv); continue; }
            break;
        }
        if (_kind != "basic" && b.Result == BattleResult.Ongoing)
        {
            // 回合末仍「可出卻沒出」的非移動牌：代表費用 / 時機限制造成取捨
            int leftover = b.Hand.Count(c => c.Def.Target != TargetRule.MoveDest && !c.Def.Basic);
            Meta.RecordTurn(startPlayable, played, leftover, b.Hand.Count);
        }
        rec.EndTurn();
    }

    private bool PlayOne(ReplayRecorder rec)
    {
        var b = rec.Battle;
        var options = b.Hand.Where(c => c.Def.Target != TargetRule.MoveDest && b.CanPlay(c) == PlayResult.Ok)
            .Where(c => !BasicOnly || c.Def.Basic)
            .OrderByDescending(c => c.Def.Basic ? 1 : 2).ToList();
        foreach (var c in options)
        {
            var owner = c.Owner!;
            var effect = c.Def.Effects.Count > 0 ? c.Def.Effects[0] : null;
            if (c.Def.Target == TargetRule.Ally)
            {
                var hurt = b.AliveUnits(Side.Player).Any(u => u.Hp < u.MaxHp * 0.75);
                if (!hurt && effect != null && effect.Type == EffectType.Heal) continue;
                if (b.ResolveTargets(owner, c.Def) == null) continue;
                if (rec.Play(c) == PlayResult.Ok) { Meta.RecordCard(_kind, c); return true; }
                continue;
            }
            if (c.Def.Target == TargetRule.Enemy)
            {
                var inRange = b.AliveUnits(Side.Enemy).Where(e => Position.Distance(owner.Pos, e.Pos) <= owner.AttackRange).ToList();
                if (inRange.Count == 0) continue;
                Unit pick;
                bool debuff = c.Def.Effects.Any(e => e.Status == StatusType.ArmorBreak || e.Status == StatusType.Burn);
                if (debuff) pick = inRange.OrderByDescending(e => e.EffectiveDef).ThenByDescending(e => e.Hp).First();
                else pick = inRange.OrderByDescending(e => e.IsObjective ? 2 : e.Stats.Range > 1 && e.Stats.Hp < 350 ? 1 : 0).ThenBy(e => e.Hp).First();
                if (c.Def.Shape == Shape.Row3)
                    pick = inRange.OrderByDescending(e => b.AliveUnits(Side.Enemy).Count(o => o.Pos.Row == e.Pos.Row && Math.Abs(o.Pos.Lane - e.Pos.Lane) <= 1)).First();
                if (rec.Play(c, pick.Pos) == PlayResult.Ok) { Meta.RecordCard(_kind, c); return true; }
                continue;
            }
            if (c.Def.Target == TargetRule.AllEnemies && effect != null && effect.Status == StatusType.Taunt)
            {
                var foes = b.AliveUnits(Side.Enemy);
                if (!foes.Any(e => !e.Has(StatusType.Taunt) || e.Charging)) continue;
            }
            if (c.Def.Target == TargetRule.AllAllies && b.AliveUnits(Side.Enemy).Count == 0) continue;
            if (rec.Play(c) == PlayResult.Ok) { Meta.RecordCard(_kind, c); return true; }
        }
        return false;
    }
}

/// <summary>戰鬥 meta 統計。</summary>
public static class Meta
{
    private static readonly Dictionary<string, int> CardPlays = new();
    private static readonly Dictionary<string, int> Teams = new();
    private static readonly Dictionary<string, (int n, int won, int turns)> ByKind = new();
    private static readonly Dictionary<int, (int n, int smart, int auto, int basic)> Counter = new();
    private static readonly List<(string stage, bool smart, bool auto, bool basic)> CfList = new();
    private static int _turns, _turnsWithChoice, _startPlayableSum, _playedSum, _leftoverSum, _handSum;
    private static readonly Dictionary<string, int> Objectives = new();
    private static readonly Dictionary<string, int> DeathsByRole = new();
    private static readonly Dictionary<string, int> UnitsByRole = new();

    public static void RecordCard(string kind, CardInstance c)
    {
        if (kind == "basic") return;
        string key = $"{kind}|{(c.Def.Basic ? "基本" : "特殊")}|{c.Owner?.Hero?.Name}|{c.Def.Name}";
        CardPlays[key] = CardPlays.GetValueOrDefault(key) + 1;
    }

    public static void RecordTurn(int startPlayable, int played, int leftover, int hand)
    {
        _turns++;
        _startPlayableSum += startPlayable; _playedSum += played; _leftoverSum += leftover; _handSum += hand;
        if (leftover > 0) _turnsWithChoice++;
    }

    public static void RecordBattle(string kind, string stage, List<FormationEntry> team, Battle b, bool won)
    {
        var t = ByKind.GetValueOrDefault(kind);
        ByKind[kind] = (t.n + 1, t.won + (won ? 1 : 0), t.turns + b.Turn);
        string comp = string.Join("+", team.Select(e => HeroRoster.Find(e.HeroId)!.Role.ToString()).OrderBy(x => x));
        Teams[comp] = Teams.GetValueOrDefault(comp) + 1;
        if (kind == "story") Objectives[b.Setup.Objective.ToString()] = Objectives.GetValueOrDefault(b.Setup.Objective.ToString()) + 1;
        foreach (var u in b.Units.Where(u => u.Side == Side.Player && u.Hero != null && !u.Protected))
        {
            string r = u.Hero!.Role.ToString();
            UnitsByRole[r] = UnitsByRole.GetValueOrDefault(r) + 1;
            if (!u.Alive) DeathsByRole[r] = DeathsByRole.GetValueOrDefault(r) + 1;
        }
    }

    public static void RecordCounterfactual(string stage, bool smart, bool auto, bool basic)
    {
        Campaign.TryParse(stage, out int ch, out _);
        var c = Counter.GetValueOrDefault(ch);
        Counter[ch] = (c.n + 1, c.smart + (smart ? 1 : 0), c.auto + (auto ? 1 : 0), c.basic + (basic ? 1 : 0));
        CfList.Add((stage, smart, auto, basic));
    }

    public static string Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 戰鬥類型");
        foreach (var kv in ByKind) sb.AppendLine($"- {kv.Key}: {kv.Value.n} 場，勝率 {100.0 * kv.Value.won / kv.Value.n:0}% ，平均 {(double)kv.Value.turns / kv.Value.n:0.0} 回合");
        sb.AppendLine("## 主線目標類型（場次）");
        foreach (var kv in Objectives) sb.AppendLine($"- {kv.Key}: {kv.Value}");
        sb.AppendLine("## 主線同一場（同種子同隊伍）三種打法勝率：聰明打 / 一般自動 / 只出基本牌");
        foreach (var kv in Counter.OrderBy(k => k.Key))
            sb.AppendLine($"- 第{kv.Key}章：{kv.Value.n} 場，聰明 {100.0 * kv.Value.smart / kv.Value.n:0}%／自動 {100.0 * kv.Value.auto / kv.Value.n:0}%／基本牌 {100.0 * kv.Value.basic / kv.Value.n:0}%");
        int smartOnly = CfList.Count(x => x.smart && !x.auto), autoOnly = CfList.Count(x => x.auto && !x.smart);
        sb.AppendLine($"- 聰明打贏但自動輸：{smartOnly} 場；自動贏但聰明輸：{autoOnly} 場；只出基本牌也贏：{CfList.Count(x => x.basic)} / {CfList.Count}");
        sb.AppendLine("## 每回合決策");
        if (_turns > 0)
            sb.AppendLine($"- 共 {_turns} 回合；回合開始平均可出牌 {(double)_startPlayableSum / _turns:0.0} 張、實際出 {(double)_playedSum / _turns:0.0} 張、平均手牌 {(double)_handSum / _turns:0.0}；回合末仍留特殊牌（有取捨）的回合 {100.0 * _turnsWithChoice / _turns:0}%");
        sb.AppendLine("## 隊伍組成（職業，場次）");
        foreach (var kv in Teams.OrderByDescending(k => k.Value).Take(10)) sb.AppendLine($"- {kv.Key}: {kv.Value}");
        sb.AppendLine("## 我方陣亡率（依職業）");
        foreach (var kv in UnitsByRole) sb.AppendLine($"- {kv.Key}: {100.0 * DeathsByRole.GetValueOrDefault(kv.Key) / kv.Value:0}%（{kv.Value} 人次）");
        sb.AppendLine("## 出牌次數前 40");
        foreach (var kv in CardPlays.OrderByDescending(k => k.Value).Take(40)) sb.AppendLine($"- {kv.Key}: {kv.Value}");
        int total = CardPlays.Values.Sum();
        int special = CardPlays.Where(k => k.Key.Contains("|特殊|")).Sum(k => k.Value);
        sb.AppendLine($"- 共 {total} 次出牌，特殊牌占 {100.0 * special / Math.Max(1, total):0}%；出現過的不同卡牌 {CardPlays.Keys.Select(k => k.Split('|')[3]).Distinct().Count()} 種");
        return sb.ToString();
    }
}
