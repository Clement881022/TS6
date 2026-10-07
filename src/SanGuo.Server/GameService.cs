using System.Collections.Concurrent;
using System.Security.Cryptography;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;

namespace SanGuo.Server;

public sealed class ServerOptions
{
    /// <summary>新帳號的開局資源（開發用預設值；正式的新手紅利尚待設計，見 days-1-7.md）。</summary>
    public int StartingYuanbao { get; set; } = 2000;
    public int StartingGold { get; set; } = 5000;
    /// <summary>開發用起始素材（資源副本的客戶端介面完成前，升級與卡牌強化無從測試）。</summary>
    public int StartingExpBooks { get; set; } = 30;
    public int StartingCardMaterials { get; set; } = 40;

    /// <summary>開發用端點（直接標記通關等）。預設關閉；沒有戰鬥重播驗證前，正式環境不可開。</summary>
    public bool EnableDevEndpoints { get; set; }
}

/// <summary>一次操作的結果。Ok = false 時，Code 是機器可讀的原因，客戶端據此顯示提示。</summary>
public sealed record ApiResult(bool Ok, string Code, object? Data = null)
{
    /// <summary>失敗但仍要存檔（例如作弊的結算要清掉進行中的關卡）。</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Persist { get; init; }

    public static ApiResult Success(object? data = null) => new(true, "ok", data);
    public static ApiResult Fail(string code) => new(false, code);
}

/// <summary>
/// 遊戲規則的伺服器入口：每個帳號同一時間只處理一個操作（讀 → 改 → 存），
/// 規則本身全在 SanGuo.Core（客戶端與伺服器共用）。
/// </summary>
public sealed class GameService
{
    private readonly IProfileStore _store;
    private readonly TimeProvider _time;
    private readonly ServerOptions _options;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private readonly Dictionary<string, GachaPool> _pools = DemoMeta.Pools().ToDictionary(p => p.Id);
    private readonly Dictionary<string, HeroDef> _heroes = DemoContent.Roster().ToDictionary(h => h.Id);
    private readonly Dictionary<string, ResourceDungeonDef> _dungeons = DemoResourceDungeons.Create().ToDictionary(d => d.Id);

    public GameService(IProfileStore store, TimeProvider time, ServerOptions options)
    {
        _store = store;
        _time = time;
        _options = options;
    }

    public bool DevEndpointsEnabled => _options.EnableDevEndpoints;

    private long Now => _time.GetUtcNow().ToUnixTimeSeconds();

    /// <summary>取得帳號鎖後讀檔、執行、成功才存檔。</summary>
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
                profile.AddMaterial(HeroGrowth.ExpBook, _options.StartingExpBooks);
                profile.AddMaterial(HeroGrowth.CardMaterial, _options.StartingCardMaterials);
            }
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
        // 只讀，但換日要即時反映；不存檔（Ok 才存，所以這裡回傳成功仍會存一次，成本很低）。
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
            heroId = r.HeroId, rarity = r.Rarity.ToString(), isNew = r.IsNew, shards = r.Shards,
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

    public Task<ApiResult> Enhance(string accountId, string heroId, string cardId) => Run(accountId, (p, now) =>
    {
        if (!_heroes.TryGetValue(heroId, out var def)) return ApiResult.Fail(GrowthResult.UnknownHero.ToString());
        var r = HeroGrowth.EnhanceCard(p, heroId, cardId, def.Deck.Select(c => c.Id).Distinct());
        if (r != GrowthResult.Ok) return ApiResult.Fail(r.ToString());
        Quests.Report(p, Quests.Events.CardEnhance, 1, now);
        return ApiResult.Success(new { cardLevel = p.Heroes[heroId].CardLevels[cardId] });
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
        var r = ResourceDungeons.TrySweep(p, d, count, now);
        return r == DungeonEntryResult.Ok
            ? ApiResult.Success(new { remaining = ResourceDungeons.Remaining(p, d, now) })
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

    /// <summary>
    /// 開始關卡：檢查等級門檻、扣體力，伺服器發亂數種子並記為「進行中」。
    /// 開放編隊的關卡 / 副本要帶玩家編隊（只能用已擁有的武將），伺服器記下來結算時重建同一場戰鬥。
    /// 客戶端用這個種子建立戰鬥；再開始別的關卡會取代進行中的關卡（舊的體力不退）。
    /// </summary>
    public Task<ApiResult> StartStage(string accountId, string stageId, IReadOnlyList<FormationEntry>? formation = null) => Run(accountId, (p, now) =>
    {
        var r = StageFlow.Start(p, stageId, now, RandomSeed(), formation);
        return r.Ok ? ApiResult.Success(new { stageId, seed = (long)r.Seed }) : ApiResult.Fail(r.Code);
    });

    /// <summary>
    /// 結算關卡（主線或資源副本）：用伺服器發的種子把客戶端的操作紀錄重播一次，由伺服器自己算出勝負與星數。
    /// 客戶端無法自報結果；紀錄不合法就沒有任何獎勵，進行中的關卡也會被清掉。
    /// </summary>
    public Task<ApiResult> FinishStage(string accountId, string stageId, IReadOnlyList<ReplayAction> actions) => Run(accountId, (p, now) =>
    {
        var r = StageFlow.Finish(p, stageId, actions, now);
        if (!r.Ok) return ApiResult.Fail(r.Code) with { Persist = r.Persist };
        if (!r.Won) return ApiResult.Success(new { won = false, result = BattleResult.Lost.ToString() });
        return ApiResult.Success(new
        {
            won = true, stars = r.Stars, firstClear = r.FirstClear, exp = r.Exp, gold = r.Gold,
            yuanbao = r.Yuanbao, levelsGained = r.LevelsGained, heroGained = r.HeroGained, materials = r.Materials,
        });
    });

    /// <summary>
    /// 開發用：直接把關卡標為通關（含星數）。正式的通關結算必須由伺服器重播戰鬥驗證
    /// （戰鬥核心是確定性的，客戶端回傳種子與操作紀錄即可重播），這個驗證尚未實作。
    /// </summary>
    public Task<ApiResult> DevClearStage(string accountId, string stageId, int stars) => Run(accountId, (p, now) =>
    {
        if (!_options.EnableDevEndpoints) return ApiResult.Fail("disabled");
        var stage = DemoMeta.FindStage(stageId);
        if (stage == null) return ApiResult.Fail("unknown_stage");
        p.ClaimClear(stage, now, stars);
        return ApiResult.Success(new { stars = p.StageStars[stageId] });
    });

    // ---- 商店（M4）：訂單 → 付款回呼（冪等發貨）→ 月卡 / 成長基金領取 ----

    /// <summary>建立訂單（待付款），回傳訂單 id；付款前不發任何東西。</summary>
    public Task<ApiResult> CreateOrder(string accountId, string productId) => Run(accountId, (p, now) =>
    {
        string orderId = Guid.NewGuid().ToString("N");
        var r = Shop.CreateOrder(p, productId, orderId);
        return r == ShopResult.Ok ? ApiResult.Success(new { orderId, productId }) : ApiResult.Fail(r.ToString());
    });

    /// <summary>
    /// 開發用的「模擬付款成功」：正式版這一步由支付平台的伺服器通知觸發並驗簽，
    /// 在串接真正的支付渠道之前只在 <see cref="ServerOptions.EnableDevEndpoints"/> 開啟時存在。
    /// </summary>
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

    public Task<ApiResult> ClaimGrowthFund(string accountId, int level) => Run(accountId, (p, now) =>
    {
        var r = Shop.ClaimGrowthFund(p, level);
        return r == ShopResult.Ok ? ApiResult.Success(new { yuanbao = p.Yuanbao }) : ApiResult.Fail(r.ToString());
    });

    private static ulong RandomSeed()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return BitConverter.ToUInt64(bytes);
    }
}
