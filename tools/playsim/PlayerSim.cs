using System.Text;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using SanGuo.Server;

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

    private string Id { get { Meta._ctx = _pe.AccountId; return _pe.AccountId; } }
    private long Now => _clock.GetUtcNow().ToUnixTimeSeconds();
    private Task<PlayerProfile> P() => _store.LoadAsync(Id)!;
    private void Menu(double s) { _today.Seconds += s; _today.MenuSeconds += s; }

    public async Task Session(int day, int hour)
    {
        if (_today == null || _today.Day != day) _today = new DayStat { Day = day };
        var login = await _g.Login(Id);
        Menu(30);
        var p = await P();

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
        await PushHard();
        await WorldBossFights();
        await DailyStageQuest();
        await Dungeons();
        await Growth();
        await Gacha();
        await Growth();
        await ClaimAll();
        await Growth();
        await PushStory();
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
        _today.HardCleared = p.ClearedStages.Count(HardStages.IsHard);
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
        string month = DailyClock.MonthKey(now);
        if (_pe.Tier >= 2 && _lastMonthInjected != month)
        {
            _lastMonthInjected = month;
            int times = _pe.Tier == 3 ? 5 : 2;
            for (int i = 0; i < times; i++) await Buy(Shop.RechargeId(648), 648);
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

    private static string Short(string id) => HeroRoster.Find(id) is { } h ? (h.Rarity == Rarity.UR ? "UR" + h.Name.Split('．').Last() : h.Name) : id;
    private static string GearStr(HeroState h) => string.Concat(Equipment.Slots.Select(s => h.Equipment.TryGetValue(s.ToString(), out int t) ? t.ToString() : "0"));

    private static double Score(PlayerProfile p, string id, bool potential)
    {
        var def = HeroRoster.Find(id)!;
        var st = p.Heroes[id];
        var probe = new HeroState { HeroId = id, Level = potential ? Math.Max(st.Level, p.Level) : st.Level, Stars = st.Stars, Equipment = st.Equipment };
        var s = HeroGrowth.ScaleStats(def, probe);
        double rw = def.Rarity == Rarity.UR ? 1.1 : def.Rarity == Rarity.SR ? 1.05 : 1.0;
        return Math.Sqrt(s.Hp * (double)Math.Max(Math.Max(s.Atk, s.Int), 1) + s.Def * 50.0) * rw;
    }

    public static double TeamPower(PlayerProfile p, List<FormationEntry> team) => team.Sum(t => Score(p, t.HeroId, false));

    private readonly Dictionary<string, List<string>> _stageTeam = new();

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
            var x = Best(y => R(y) != Role.Tank && R(y) != Role.Healer) ?? Best(_ => true);
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

    private List<FormationEntry> StoryTeam(PlayerProfile p, string stageId)
    {
        if (_stageTeam.TryGetValue(stageId, out var ids) && ids.All(p.Heroes.ContainsKey)) return Place(ids);
        var team = ChooseTeam(p);
        if (HardStages.TryParse(stageId, out int hc, out int hl) && HardStages.BannedRole(hc, hl) is Role banned)
        {
            var keep = team.Select(e => e.HeroId).Where(id => HeroRoster.Find(id)!.Role != banned).ToList();
            foreach (var id in p.Heroes.Keys.Where(k => HeroRoster.Find(k) is { } h && h.Role != banned && !keep.Contains(k))
                         .OrderByDescending(k => Score(p, k, true)))
            {
                if (keep.Count >= 4) break;
                keep.Add(id);
            }
            team = Place(keep);
        }
        return team;
    }

    private async Task PushHard()
    {
        for (int guard = 0; guard < 40; guard++)
        {
            var p = await P();
            if (p.Stamina.Get(Now) < HardStages.StaminaCost) return;
            string sid = null;
            for (int c = HardStages.FirstChapter; c <= Campaign.LastChapter && sid == null; c++)
                for (int l = 1; l <= Campaign.LevelsPerChapter && sid == null; l++)
                    if (!p.ClearedStages.Contains(HardStages.StageId(c, l)))
                        sid = HardStages.IsUnlocked(p.ClearedStages, c, l) ? HardStages.StageId(c, l) : "";
            if (string.IsNullOrEmpty(sid)) return;
            _attemptsPerStage.TryGetValue(sid, out int tries);
            if (_lostAt.TryGetValue(sid, out var lost))
            {
                double powNow = TeamPower(p, ChooseTeam(p));
                bool stronger = powNow >= lost.Pow * (1 + RetryPowerGain) || p.Level > lost.Level;
                if (!stronger && _today.Day <= lost.Day) return;
            }
            double powBefore = TeamPower(p, ChooseTeam(p));
            var (ok, won, _, _, _) = await Fight(sid, true, "hard");
            if (!ok) return;
            _attemptsPerStage[sid] = tries + 1;
            if (won) { _lostAt.Remove(sid); _today.NewClears++; await Growth(); continue; }
            _lostAt[sid] = (powBefore, p.Level, _today.Day);
            if ((tries + 1) % 2 == 0) TryOtherTeams(await P(), sid);
            await Growth();
            return;
        }
    }

    private void TryOtherTeams(PlayerProfile p, string stageId)
    {
        Role R(string id) => HeroRoster.Find(id)!.Role;
        var owned = p.Heroes.Keys.Where(k => HeroRoster.Find(k) != null).ToList();
        var byRole = owned.GroupBy(R).ToDictionary(g => g.Key, g => g.OrderByDescending(x => Score(p, x, true)).Take(2).ToList());
        var pool = byRole.Values.SelectMany(x => x).Distinct().ToList();
        var probe = new PlayerProfile { Level = p.Level };
        foreach (var id in pool)
        {
            var h = p.Heroes[id];
            probe.Heroes[id] = new HeroState { HeroId = id, Level = h.Level, Stars = h.Stars, Equipment = new Dictionary<string, int>(h.Equipment) };
        }
        List<string> best = null; double bestWins = -1;
        var combos = new List<List<string>>();
        for (int a = 0; a < pool.Count; a++)
            for (int b = a + 1; b < pool.Count; b++)
                for (int c = b + 1; c < pool.Count; c++)
                    for (int d = c + 1; d < pool.Count; d++)
                        combos.Add(new List<string> { pool[a], pool[b], pool[c], pool[d] });
        combos = combos.GroupBy(x => string.Join("+", x.Select(R).OrderBy(r => r))).Select(g => g.First()).ToList();
        foreach (var ids in combos)
        {
            var team = Place(ids);
            int wins = 0;
            for (ulong seed = 1; seed <= 4; seed++)
            {
                var setup = DemoMeta.BuildSetup(stageId, seed * 104729, probe, team);
                if (setup == null) break;
                var b = new Battle(setup);
                var rec = new ReplayRecorder(b);
                var bot = new SmartBot("trial", null);
                for (int i = 0; i < 60 && b.Result == BattleResult.Ongoing; i++) bot.PlayTurn(rec);
                if (b.Result == BattleResult.Won) wins++;
            }
            if (wins > bestWins) { bestWins = wins; best = ids; }
        }
        if (best != null) { _stageTeam[stageId] = best; _events.Add($"D{_today.Day} {stageId} 換隊伍：{string.Join("+", best.Select(Short))}（試打 {bestWins}/4）"); }
    }

    private List<FormationEntry> _bossTeam;
    private int _bossTeamDay = -99, _bossTeamRoster = -1;

    private List<FormationEntry> BossTeam(PlayerProfile p)
    {
        if (!WorldBoss.IsUnlocked(p)) return ChooseTeam(p);
        int day = _today?.Day ?? 0;
        if (_bossTeam != null && day - _bossTeamDay < 7 && _bossTeamRoster == p.Heroes.Count) return _bossTeam;
        _bossTeamDay = day; _bossTeamRoster = p.Heroes.Count;
        var story = ChooseTeam(p);
        Role R(string id) => HeroRoster.Find(id)!.Role;
        var owned = p.Heroes.Keys.Where(k => HeroRoster.Find(k) != null).ToList();
        string tank = owned.Where(x => R(x) == Role.Tank).OrderByDescending(x => Score(p, x, true)).FirstOrDefault();
        string healer = owned.Where(x => R(x) == Role.Healer).OrderByDescending(x => Score(p, x, true)).FirstOrDefault();
        var dps = owned.Where(x => R(x) != Role.Tank && R(x) != Role.Healer)
            .GroupBy(R).SelectMany(g => g.OrderByDescending(x => Score(p, x, true)).Take(2)).ToList();
        var best = story;
        double bestDmg = BossTrial(p, story);
        for (int i = 0; i < dps.Count; i++)
            for (int j = i + 1; j < dps.Count; j++)
            {
                var ids = new[] { tank, healer, dps[i], dps[j] }.Where(x => x != null).Distinct().ToList();
                var team = Place(ids);
                double dmg = BossTrial(p, team);
                if (dmg > bestDmg) { bestDmg = dmg; best = team; }
            }
        _bossTeam = best;
        return best;
    }

    private static double BossTrial(PlayerProfile p, List<FormationEntry> team)
    {
        var probe = new PlayerProfile { Level = p.Level };
        probe.WorldBoss.Season = p.WorldBoss.Season == "" ? "2026-10" : p.WorldBoss.Season;
        foreach (var e in team)
        {
            var h = p.Heroes[e.HeroId];
            probe.Heroes[e.HeroId] = new HeroState { HeroId = h.HeroId, Level = Math.Max(h.Level, p.Level), Stars = h.Stars, Equipment = new Dictionary<string, int>(h.Equipment) };
        }
        double sum = 0;
        for (ulong seed = 1; seed <= 3; seed++)
        {
            var b = new Battle(DemoMeta.BuildSetup(WorldBoss.StageId, seed * 7777, probe, team)!);
            var rec = new ReplayRecorder(b);
            var bot = new SmartBot("trial", null);
            for (int i = 0; i < 60 && b.Result == BattleResult.Ongoing; i++) bot.PlayTurn(rec);
            sum += WorldBoss.Score(b);
        }
        return sum / 3;
    }

    public static List<FormationEntry> Place(List<string> ids)
    {
        Role R(string id) => HeroRoster.Find(id)!.Role;
        var front = new Queue<(int, int)>(new[] { (2, 3), (1, 3), (3, 3) });
        var back = new Queue<(int, int)>(new[] { (2, 4), (1, 4), (3, 4) });
        var team = new List<FormationEntry>();
        foreach (var id in ids.OrderBy(x => R(x) == Role.Tank ? 0 : R(x) == Role.Warrior ? 1 : 2))
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
        var ids = team.Select(t => t.HeroId).Concat(BossTeam(p).Select(t => t.HeroId)).Distinct().ToList();
        if (ids.Count == 0) return;

        foreach (var id in ids.OrderByDescending(i => HeroGrowth.RarityOf(i)))
            if ((await _g.BuySoulItem(Id, "shard:" + id)).Ok) Menu(3);
        foreach (var it in new[] { "hero_exp", "gold" })
            while ((await _g.BuySoulItem(Id, it)).Ok) Menu(2);

        foreach (var id in ids)
            while ((await _g.Breakthrough(Id, id)).Ok) { Menu(6); _events.Add($"D{_today.Day} 突破 {Short(id)}"); }

        p = await P();
        foreach (var id in ids)
            foreach (var slot in Equipment.Slots)
            {
                p.Heroes[id].Equipment.TryGetValue(slot.ToString(), out int curTier);
                for (int t = Equipment.MaxTier; t > curTier; t--)
                    if (Equipment.CountFor(p, HeroRoster.Find(id)!.Role, slot, t) > 0) { if ((await _g.Equip(Id, id, slot.ToString(), t)).Ok) Menu(3); p = await P(); break; }
            }

        p = await P();
        foreach (var slot in Equipment.Slots)
        {
            int min = ids.Min(i => p.Heroes.TryGetValue(i, out var h) && h.Equipment.TryGetValue(slot.ToString(), out int t) ? t : 0);
            for (int t = 1; t < min; t++)
            {
                int n = Equipment.Count(p, slot, t);
                if (n > 0 && (await _g.Dismantle(Id, slot.ToString(), t, n)).Ok) Menu(3);
            }
        }

        int ups = 0;
        while (true)
        {
            p = await P();
            var target = ids.OrderBy(i => p.Heroes[i].Level).FirstOrDefault(i => p.Heroes[i].Level < p.Level);
            if (target == null) break;
            if (!(await _g.LevelUp(Id, target)).Ok) break;
            ups++;
        }
        if (ups > 0) Menu(4 + ups * 0.4);
    }

    private static int StoryLines(string stageId, bool after)
    {
        if (!Campaign.TryParse(stageId, out int c, out int l)) return 0;
        return (after ? CampaignStory.After(c, l) : CampaignStory.Before(c, l)).Count;
    }

    private async Task<(bool Ok, bool Won, int Turns, ApiResult R, Battle B)> Fight(string stageId, bool manual, string kind)
    {
        var p = await P();
        var team = kind == "boss" ? BossTeam(p) : kind == "story" || kind == "hard" ? StoryTeam(p, stageId) : ChooseTeam(p);
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

        double secs = manual ? 20 + turns * 30 : 10 + turns * 8;
        _today.Seconds += secs; _today.BattleSeconds += secs; _today.Battles++;
        if (!won) _today.Losses++;
        if (!won && kind == "story") _today.StoryLosses++;
        Meta.RecordBattle(kind, stageId, team, battle, won);

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

    private readonly Dictionary<string, (double Pow, int Level, int Day)> _lostAt = new();
    public const double RetryPowerGain = 0.05;

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
            if (_lostAt.TryGetValue(sid, out var lost))
            {
                double powNow = TeamPower(p, ChooseTeam(p));
                bool stronger = powNow >= lost.Pow * (1 + RetryPowerGain) || p.Level > lost.Level;
                bool nextDay = _today.Day > lost.Day;
                if (!stronger && !nextDay) return;
            }
            if (_seenStory.Add(sid))
            {
                double s = StoryLines(sid, false) * 3;
                _today.Seconds += s; _today.StorySeconds += s;
            }
            double powBefore = TeamPower(p, ChooseTeam(p));
            var (ok, won, turns, _, _) = await Fight(sid, true, "story");
            if (!ok) return;
            Meta.RecordAttempt(sid, powBefore, won);
            _attemptsPerStage[sid] = tries + 1;
            if (won)
            {
                _lostAt.Remove(sid);
                double s = StoryLines(sid, true) * 3;
                _today.Seconds += s; _today.StorySeconds += s;
                _today.NewClears++;
                if (tries > 0) _events.Add($"D{_today.Day} {sid} 第 {tries + 1} 次才過");
                await Growth();
                continue;
            }
            _lostAt[sid] = (powBefore, p.Level, _today.Day);
            if (tries + 1 >= 2 && (tries + 1) % 2 == 0) TryOtherTeams(await P(), sid);
            await Growth();
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
            double spent = _today.Seconds - before;
            _today.BattleSeconds -= spent; _today.BossSeconds += spent;
        }
    }

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
            var target = PickFarmTier(p, unlocked.Where(d => p.ClearedStages.Contains(d.Id)).ToList());
            if (target == null)
            {
                if (stamina >= unlocked[0].StaminaCost) { var (ok, _, _, _, _) = await Fight(unlocked[0].Id, false, "dungeon"); if (ok) _today.StaminaSpent += unlocked[0].StaminaCost; continue; }
                return;
            }
            int n = Math.Min(ResourceDungeons.MaxSweepCount, stamina / target.StaminaCost);
            if (n <= 0)
            {
                var cheaper = unlocked.Where(d => p.ClearedStages.Contains(d.Id) && d.StaminaCost <= stamina).LastOrDefault();
                if (cheaper == null) return;
                target = cheaper; n = Math.Min(10, stamina / target.StaminaCost);
            }
            var r = await _g.SweepDungeon(Id, target.Id, n);
            if (!r.Ok) return;
            Menu(6); _today.Sweeps += n; _today.StaminaSpent += n * target.StaminaCost;
        }
    }

    private ResourceDungeonDef PickFarmTier(PlayerProfile p, List<ResourceDungeonDef> cleared)
    {
        if (cleared.Count == 0) return null;
        var ids = ChooseTeam(p).Select(t => t.HeroId).Concat(BossTeam(p).Select(t => t.HeroId)).Distinct().Where(i => p.Heroes.ContainsKey(i)).ToList();
        var cur = new List<int>();
        foreach (var id in ids)
            foreach (var slot in Equipment.Slots)
                cur.Add(p.Heroes[id].Equipment.TryGetValue(slot.ToString(), out int t) ? t : 0);
        ResourceDungeonDef best = null; double bestEff = 0;
        foreach (var d in cleared)
        {
            double perRun = Equipment.UsesShards(d.Tier) ? 1.0 / Equipment.ShardCostOf(d.Tier) : Equipment.DropChanceOf(d.Tier);
            double gain = cur.Count == 0 ? 0 : cur.Average(c => Math.Max(0, Equipment.PercentOf(d.Tier) - (c > 0 ? Equipment.PercentOf(c) : 0)));
            double eff = gain * perRun / d.StaminaCost;
            if (eff > bestEff) { bestEff = eff; best = d; }
        }
        return best ?? cleared.Last();
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
        p = await P();
        if (!p.DailyTaskClaimed.Contains("d_gacha") && Quests.Progress(p, DemoQuests.Book.Find("d_gacha")!) < 1 && _pe.Tier >= 2 && p.Yuanbao >= 200)
            if ((await _g.Pull(Id, DemoMeta.UpPoolId, 1)).Ok) { _pulls++; Menu(8); }
    }

    public string DailyCsv()
    {
        var sb = new StringBuilder("day,minutes,battleMin,storyMin,bossMin,menuMin,battles,losses,storyLosses,newClears,sweeps,staminaSpent,staminaWasted,level,frontier,hardCleared,power,yuanbao,pulls,ur,bossBest,spend,bossToday,team\n");
        foreach (var d in _days)
            sb.AppendLine($"{d.Day},{d.Seconds / 60:0.0},{d.BattleSeconds / 60:0.0},{d.StorySeconds / 60:0.0},{d.BossSeconds / 60:0.0},{d.MenuSeconds / 60:0.0},{d.Battles},{d.Losses},{d.StoryLosses},{d.NewClears},{d.Sweeps},{d.StaminaSpent},{d.StaminaWasted},{d.Level},{d.Frontier},{d.HardCleared},{d.Power:0},{d.Yuanbao},{d.PullsTotal},{d.UrOwned},{d.BossBest},{d.SpendCny},{d.BossToday},{d.Team}");
        return sb.ToString();
    }

    public async Task<string> Summary()
    {
        var p = await P();
        var sb = new StringBuilder();
        sb.AppendLine($"persona={_pe.Label}");
        sb.AppendLine($"spend={_spendTotal}");
        sb.AppendLine($"settleSeason={p.WorldBoss.LastSeason}");
        sb.AppendLine($"settleRank={p.WorldBoss.LastRank}");
        sb.AppendLine($"settleTotal={p.WorldBoss.LastTotal}");
        sb.AppendLine($"settleReward={p.WorldBoss.LastReward}");
        sb.AppendLine("attempts=" + string.Join(";", _attemptsPerStage.Where(kv => !kv.Key.Contains('@') && !kv.Key.StartsWith("res_")).Select(kv => $"{kv.Key}:{kv.Value}")));
        sb.AppendLine("bossTeam=" + string.Join("+", (_bossTeam ?? ChooseTeam(p)).Select(e => Short(e.HeroId))));
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
