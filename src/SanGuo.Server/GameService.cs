using System.Collections.Concurrent;
using System.Security.Cryptography;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;

namespace SanGuo.Server;

public sealed class ServerOptions
{
    public int StartingYuanbao { get; set; } = 2000;
    public int StartingGold { get; set; } = 5000;
    public int StartingHeroExp { get; set; } = 3000;

    public bool EnableDevEndpoints { get; set; }
}

public sealed record ApiResult(bool Ok, string Code, object? Data = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Persist { get; init; }

    public static ApiResult Success(object? data = null) => new(true, "ok", data);
    public static ApiResult Fail(string code) => new(false, code);
}

public sealed class GameService
{
    private readonly IProfileStore _store;
    private readonly IWorldBossBoard _board;
    private readonly TimeProvider _time;
    private readonly ServerOptions _options;
    private readonly SqliteAccountStore? _accounts;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private readonly Dictionary<string, GachaPool> _pools = DemoMeta.Pools().ToDictionary(p => p.Id);
    private readonly Dictionary<string, ResourceDungeonDef> _dungeons = DemoResourceDungeons.Create().ToDictionary(d => d.Id);

    public GameService(IProfileStore store, IWorldBossBoard board, TimeProvider time, ServerOptions options, SqliteAccountStore? accounts = null)
    {
        _accounts = accounts;
        _store = store;
        _board = board;
        _time = time;
        _options = options;
    }

    public bool DevEndpointsEnabled => _options.EnableDevEndpoints;

    private long Now => _time.GetUtcNow().ToUnixTimeSeconds();

    private async Task<ApiResult> Run(string accountId, Func<PlayerProfile, long, ApiResult> action, bool createIfMissing = false)
    {
        var gate = _locks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            long now = Now;
            var profile = await _store.LoadAsync(accountId);
            if (profile == null)
            {
                if (!createIfMissing) return ApiResult.Fail("no_profile");
                profile = PlayerProfile.CreateNew(now);
                profile.Yuanbao = _options.StartingYuanbao;
                profile.Gold = _options.StartingGold;
                profile.AddMaterial(HeroGrowth.HeroExp, _options.StartingHeroExp);
            }
            WorldBoss.SettlePending(profile, _board, now);
            var result = action(profile, now);
            if (result.Ok || result.Persist) await _store.SaveAsync(accountId, profile);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    private object View(PlayerProfile p, long now)
    {
        var obj = ProfileSerializer.ToObject(p);
        obj["stamina"] = new Dictionary<string, object?>
        {
            ["current"] = (long)p.Stamina.Get(now),
            ["cap"] = (long)p.Stamina.Cap,
            ["regenSeconds"] = (long)p.Stamina.RegenSeconds,
        };
        obj["sevenDayPoints"] = (long)Quests.SevenDayPoints(p);
        obj["dayNumber"] = (long)Quests.DayNumber(p, now);
        return obj;
    }

    public Task<ApiResult> Login(string accountId) => Run(accountId, (p, now) =>
    {
        p.OnLogin(now);
        return ApiResult.Success(View(p, now));
    }, createIfMissing: true);

    public Task<ApiResult> GetProfile(string accountId) => Run(accountId, (p, now) =>
    {
        p.EnsureDaily(now);
        return ApiResult.Success(View(p, now));
    });

    public Task<ApiResult> Pull(string accountId, string poolId, int count) => Run(accountId, (p, now) =>
    {
        if (!_pools.TryGetValue(poolId, out var pool)) return ApiResult.Fail("unknown_pool");
        var outcome = Gacha.Pull(p, pool, count, new Rng(RandomSeed()));
        if (outcome.Status != PullStatus.Ok) return ApiResult.Fail(outcome.Status.ToString());
        Quests.Report(p, Quests.Events.GachaPull, count, now);
        var results = outcome.Results.Select(r => new
        {
            heroId = r.HeroId, rarity = r.Rarity.ToString(), isNew = r.IsNew, shards = r.Shards, souls = r.Souls,
            isUp = r.IsUp, fromPity = r.FromPity,
        });
        return ApiResult.Success(new { results, yuanbao = p.Yuanbao });
    });

    public Task<ApiResult> LevelUp(string accountId, string heroId) => Run(accountId, (p, now) =>
    {
        var r = HeroGrowth.LevelUp(p, heroId);
        if (r != GrowthResult.Ok) return ApiResult.Fail(r.ToString());
        Quests.Report(p, Quests.Events.HeroLevelUp, 1, now);
        return ApiResult.Success(new { level = p.Heroes[heroId].Level, gold = p.Gold });
    });

    public Task<ApiResult> Equip(string accountId, string heroId, string slot, int tier) => Run(accountId, (p, now) =>
    {
        if (!Enum.TryParse<EquipSlot>(slot, true, out var s)) return ApiResult.Fail("invalid_slot");
        var r = Equipment.Equip(p, heroId, s, tier);
        if (r != EquipResult.Ok) return ApiResult.Fail(r.ToString());
        Quests.Report(p, Quests.Events.Equip, 1, now);
        return ApiResult.Success();
    });

    public Task<ApiResult> Unequip(string accountId, string heroId, string slot) => Run(accountId, (p, now) =>
    {
        if (!Enum.TryParse<EquipSlot>(slot, true, out var s)) return ApiResult.Fail("invalid_slot");
        var r = Equipment.Unequip(p, heroId, s);
        return r == EquipResult.Ok ? ApiResult.Success() : ApiResult.Fail(r.ToString());
    });

    public Task<ApiResult> Dismantle(string accountId, string slot, int tier, int count) => Run(accountId, (p, now) =>
    {
        if (!Equipment.TryParseStockSlot(slot, out var s, out var role)) return ApiResult.Fail("invalid_slot");
        var r = Equipment.Dismantle(p, s, tier, count, role);
        return r == EquipResult.Ok ? ApiResult.Success(new { gold = p.Gold }) : ApiResult.Fail(r.ToString());
    });

    public Task<ApiResult> BuySoulItem(string accountId, string itemId) => Run(accountId, (p, now) =>
    {
        var r = SoulShop.Buy(p, itemId, now);
        return r == SoulShopResult.Ok ? ApiResult.Success(new { souls = p.GetMaterial(HeroGrowth.Soul) }) : ApiResult.Fail(r.ToString());
    });

    public Task<ApiResult> Breakthrough(string accountId, string heroId) => Run(accountId, (p, now) =>
    {
        var r = HeroGrowth.Breakthrough(p, heroId);
        if (r != GrowthResult.Ok) return ApiResult.Fail(r.ToString());
        Quests.Report(p, Quests.Events.Breakthrough, 1, now);
        return ApiResult.Success(new { stars = p.Heroes[heroId].Stars });
    });

    public Task<ApiResult> SweepStage(string accountId, string stageId, int count) => Run(accountId, (p, now) =>
    {
        var stage = DemoMeta.FindStage(stageId);
        if (stage == null) return ApiResult.Fail("unknown_stage");
        var r = p.TrySweep(stage, count, now, out var result);
        if (r != SweepResult.Ok) return ApiResult.Fail(r.ToString());
        return ApiResult.Success(new { gold = result!.GoldGained, exp = result.ExpGained, levelsGained = result.LevelsGained });
    });

    public Task<ApiResult> SweepDungeon(string accountId, string dungeonId, int count) => Run(accountId, (p, now) =>
    {
        if (!_dungeons.TryGetValue(dungeonId, out var d)) return ApiResult.Fail("unknown_dungeon");
        var r = ResourceDungeons.TrySweep(p, d, count, now, out var reward);
        return r == DungeonEntryResult.Ok
            ? ApiResult.Success(new { materials = reward!.Materials, gold = reward.Gold, yuanbao = reward.Yuanbao })
            : ApiResult.Fail(r.ToString());
    });

    public Task<ApiResult> ClaimQuest(string accountId, string questId) => Run(accountId, (p, now) =>
    {
        var r = Quests.Claim(p, questId, now);
        return r == QuestClaimResult.Ok ? ApiResult.Success() : ApiResult.Fail(r.ToString());
    });

    public Task<ApiResult> ClaimMilestone(string accountId, int points) => Run(accountId, (p, now) =>
    {
        var r = Quests.ClaimMilestone(p, points, now);
        return r == QuestClaimResult.Ok ? ApiResult.Success() : ApiResult.Fail(r.ToString());
    });

    public Task<ApiResult> StartStage(string accountId, string stageId, IReadOnlyList<FormationEntry>? formation = null) => Run(accountId, (p, now) =>
    {
        var r = StageFlow.Start(p, stageId, now, RandomSeed(), formation);
        return r.Ok ? ApiResult.Success(new { stageId, seed = (long)r.Seed }) : ApiResult.Fail(r.Code);
    });

    public Task<ApiResult> FinishStage(string accountId, string stageId, IReadOnlyList<ReplayAction> actions) => Run(accountId, (p, now) =>
    {
        var r = StageFlow.Finish(p, stageId, actions, now);
        if (!r.Ok) return ApiResult.Fail(r.Code) with { Persist = r.Persist };
        if (stageId == WorldBoss.StageId)
        {
            if (r.NewBest) _board.Submit(p.WorldBoss.Season, accountId, r.BestDamage);
            var (rank, total) = _board.RankOf(p.WorldBoss.Season, p.WorldBoss.Best);
            return ApiResult.Success(new { won = r.Won, damage = r.Damage, bestDamage = r.BestDamage, newBest = r.NewBest, rank, total });
        }
        if (!r.Won) return ApiResult.Success(new { won = false, result = BattleResult.Lost.ToString() });
        return ApiResult.Success(new
        {
            won = true, stars = r.Stars, firstClear = r.FirstClear, exp = r.Exp, gold = r.Gold,
            yuanbao = r.Yuanbao, levelsGained = r.LevelsGained, heroGained = r.HeroGained, duplicatesGained = r.DuplicatesGained, materials = r.Materials,
        });
    });

    public Task<ApiResult> ClaimPass(string accountId, int level, bool paid) => Run(accountId, (p, now) =>
    {
        var r = BattlePass.Claim(p, level, paid, now);
        return r == PassClaimResult.Ok ? ApiResult.Success(View(p, now)) : ApiResult.Fail(r.ToString());
    });

    public Task<ApiResult> ClaimPassAll(string accountId) => Run(accountId, (p, now) =>
    {
        int n = BattlePass.ClaimAll(p, now);
        return n > 0 ? ApiResult.Success(new { claimed = n }) : ApiResult.Fail("NothingToClaim");
    });

    public Task<ApiResult> GetWorldBoss(string accountId) => Run(accountId, (p, now) =>
    {
        var s = p.WorldBoss;
        int left = WorldBoss.AttemptsLeft(p, now);
        var (rank, total) = s.Best > 0 ? _board.RankOf(s.Season, s.Best) : (0, 0);
        var top = _board.Top(s.Season, 10).Select(t => new Dictionary<string, object?>
        {
            ["account"] = t.AccountId == accountId ? "我" : PublicName(t.AccountId), ["best"] = t.Best,
        }).ToList();
        return ApiResult.Success(new
        {
            season = s.Season, unlocked = WorldBoss.IsUnlocked(p), attemptsLeft = left, best = s.Best, rank, total, top,
            lastSeason = s.LastSeason, lastRank = s.LastRank, lastTotal = s.LastTotal, lastReward = s.LastReward, title = s.Title,
        });
    });

    public Task<ApiResult> DevClearStage(string accountId, string stageId, int stars) => Run(accountId, (p, now) =>
    {
        if (!_options.EnableDevEndpoints) return ApiResult.Fail("disabled");
        var stage = DemoMeta.FindStage(stageId);
        if (stage == null) return ApiResult.Fail("unknown_stage");
        p.ClaimClear(stage, now, stars);
        return ApiResult.Success(new { stars = p.StageStars[stageId] });
    });

    public Task<ApiResult> CreateOrder(string accountId, string productId) => Run(accountId, (p, now) =>
    {
        string orderId = Guid.NewGuid().ToString("N");
        var r = Shop.CreateOrder(p, productId, orderId, now);
        return r == ShopResult.Ok ? ApiResult.Success(new { orderId, productId }) : ApiResult.Fail(r.ToString());
    });

    public Task<ApiResult> DevPay(string accountId, string orderId) => Run(accountId, (p, now) =>
    {
        if (!_options.EnableDevEndpoints) return ApiResult.Fail("disabled");
        var r = Shop.Fulfill(p, orderId, now);
        return r == ShopResult.Ok ? ApiResult.Success() : ApiResult.Fail(r.ToString());
    });

    public Task<ApiResult> ClaimMonthCard(string accountId, string cardId) => Run(accountId, (p, now) =>
    {
        var r = Shop.ClaimMonthCardDaily(p, cardId, now);
        return r == ShopResult.Ok ? ApiResult.Success(new { yuanbao = p.Yuanbao }) : ApiResult.Fail(r.ToString());
    });

    private string PublicName(string accountId) =>
        _accounts?.Nickname(accountId) ?? "玩家" +Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(accountId)), 0, 3);

    private static ulong RandomSeed()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return BitConverter.ToUInt64(bytes);
    }
}
