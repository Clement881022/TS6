using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SanGuo.Core.Meta;
using SanGuo.Server;

namespace SanGuo.Server.Tests;

/// <summary>可手動撥時間的時鐘，用來測換日與體力。</summary>
public sealed class TestTime : TimeProvider
{
    public DateTimeOffset Current { get; set; } = new(2026, 10, 5, 6, 0, 0, TimeSpan.FromHours(8)); // 週一
    public override DateTimeOffset GetUtcNow() => Current.ToUniversalTime();
}

public sealed class ServerApiTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sanguo-test-{Guid.NewGuid():N}.db");
    private readonly TestTime _time = new();
    private readonly WebApplicationFactory<Program> _factory;

    public ServerApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureServices(s =>
            {
                s.RemoveAll<IProfileStore>();
                s.AddSingleton<IProfileStore>(_ => new SqliteProfileStore($"Data Source={_dbPath}"));
                s.RemoveAll<IWorldBossBoard>();
                s.AddSingleton<IWorldBossBoard>(_ => new SqliteWorldBossBoard($"Data Source={_dbPath}"));
                s.RemoveAll<SqliteAccountStore>();
                s.AddSingleton(_ => new SqliteAccountStore($"Data Source={_dbPath}", _time));
                s.RemoveAll<TimeProvider>();
                s.AddSingleton<TimeProvider>(_time);
                s.RemoveAll<ServerOptions>();
                s.AddSingleton(new ServerOptions { EnableDevEndpoints = true });
            }));
    }

    public void Dispose()
    {
        _factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch (IOException) { }
    }

    private HttpClient Client(string account = "alice")
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Account", account);
        return c;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Requests_WithoutAccount_AreUnauthorized()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/login", null)).StatusCode);
    }

    [Fact]
    public async Task Profile_BeforeLogin_IsRejected()
    {
        var r = await Client("nobody").GetAsync("/profile");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("no_profile", (await Json(r)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_CreatesProfile_AndPersistsAcrossRequests()
    {
        var c = Client();
        var login = await Json(await c.PostAsync("/login", null));
        Assert.True(login.GetProperty("ok").GetBoolean());
        Assert.Equal(2000, login.GetProperty("data").GetProperty("yuanbao").GetInt32());

        var profile = await Json(await c.GetAsync("/profile"));
        Assert.Equal(2000, profile.GetProperty("data").GetProperty("yuanbao").GetInt32());
    }

    [Fact]
    public async Task Pull_SpendsYuanbao_AndGrantsHeroes()
    {
        var c = Client();
        await c.PostAsync("/login", null);

        var r = await c.PostAsJsonAsync("/gacha/pull", new { poolId = "newbie", count = 10 });
        var body = await Json(r);
        Assert.True(body.GetProperty("ok").GetBoolean());
        var results = body.GetProperty("data").GetProperty("results");
        Assert.Equal(10, results.GetArrayLength());
        // 新手池首次十連保底 UR
        Assert.Contains(results.EnumerateArray(), x => x.GetProperty("rarity").GetString() == "UR");
        Assert.Equal(0, body.GetProperty("data").GetProperty("yuanbao").GetInt32());

        // 元寶不夠：不能再抽，且不改變狀態
        var again = await c.PostAsJsonAsync("/gacha/pull", new { poolId = "newbie", count = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal("NotEnoughYuanbao", (await Json(again)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Pull_UnknownPool_IsRejected()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        var r = await c.PostAsJsonAsync("/gacha/pull", new { poolId = "nope", count = 1 });
        Assert.Equal("unknown_pool", (await Json(r)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Accounts_AreIsolated()
    {
        await Client("a").PostAsync("/login", null);
        await Client("a").PostAsJsonAsync("/gacha/pull", new { poolId = "newbie", count = 10 });
        await Client("b").PostAsync("/login", null);
        var b = await Json(await Client("b").GetAsync("/profile"));
        Assert.Equal(2000, b.GetProperty("data").GetProperty("yuanbao").GetInt32());
    }

    [Fact]
    public async Task Sweep_NeedsThreeStars_ThenWorks()
    {
        var c = Client();
        await c.PostAsync("/login", null);

        var early = await c.PostAsJsonAsync("/stage/sweep", new { id = "0-5", count = 2 });
        Assert.Equal("NotThreeStars", (await Json(early)).GetProperty("code").GetString());

        await c.PostAsJsonAsync("/dev/clear", new { stageId = "0-5", stars = 3 });
        var ok = await Json(await c.PostAsJsonAsync("/stage/sweep", new { id = "0-5", count = 2 }));
        Assert.True(ok.GetProperty("ok").GetBoolean());
        Assert.Equal(1400, ok.GetProperty("data").GetProperty("gold").GetInt32()); // 700 × 2
    }

    /// <summary>扮演客戶端：抽卡取得武將，再從已擁有的武將排出編隊（最多 4 人）。</summary>
    private async Task<List<SanGuo.Core.Meta.FormationEntry>> BuildTeam(HttpClient c)
    {
        await c.PostAsJsonAsync("/gacha/pull", new { poolId = "newbie", count = 10 });
        var profile = SanGuo.Core.Data.ProfileSerializer.FromJson((await Json(await c.GetAsync("/profile"))).GetProperty("data").GetRawText());
        var cells = new[] { (1, 3), (2, 3), (3, 3), (2, 4) };
        return profile.Heroes.Keys.Take(cells.Length)
            .Select((id, i) => new SanGuo.Core.Meta.FormationEntry(id, cells[i].Item1, cells[i].Item2)).ToList();
    }

    /// <summary>扮演客戶端：向伺服器開始關卡、用伺服器給的種子自動打完並錄下操作。開放編隊的關卡 / 副本要帶 team。</summary>
    private async Task<List<object>> PlayStageAuto(HttpClient c, string stageId, List<SanGuo.Core.Meta.FormationEntry>? team = null)
    {
        var formation = team?.Select(f => new { heroId = f.HeroId, lane = f.Lane, row = f.Row }).ToList();
        var start = await Json(await c.PostAsJsonAsync("/stage/start", new { stageId, formation }));
        Assert.True(start.GetProperty("ok").GetBoolean());
        ulong seed = (ulong)start.GetProperty("data").GetProperty("seed").GetInt64();
        SanGuo.Core.Meta.PlayerProfile? profile = team == null ? null
            : SanGuo.Core.Data.ProfileSerializer.FromJson((await Json(await c.GetAsync("/profile"))).GetProperty("data").GetRawText());
        var rec = new SanGuo.Core.Data.ReplayRecorder(new SanGuo.Core.Battle(SanGuo.Core.Meta.DemoMeta.BuildSetup(stageId, seed, profile, team)!));
        for (int i = 0; i < 100 && rec.Battle.Result == SanGuo.Core.BattleResult.Ongoing; i++) rec.PlayAuto();
        return rec.Actions.Select(a => (object)new
        {
            kind = a.Kind == SanGuo.Core.Data.ReplayActionKind.Play ? "play" : "end",
            cardId = a.CardId, unitId = a.UnitId, lane = a.Lane, row = a.Row,
        }).ToList();
    }

    [Fact]
    public async Task Stage_StartThenFinish_VerifiedByReplay_GrantsRewards()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        var actions = await PlayStageAuto(c, "0-1");

        var done = await Json(await c.PostAsJsonAsync("/stage/finish", new { stageId = "0-1", actions }));
        Assert.True(done.GetProperty("ok").GetBoolean());
        var data = done.GetProperty("data");
        Assert.True(data.GetProperty("won").GetBoolean());
        Assert.True(data.GetProperty("firstClear").GetBoolean());
        Assert.Equal(60, data.GetProperty("yuanbao").GetInt32());

        var profile = await Json(await c.GetAsync("/profile"));
        Assert.Equal(2060, profile.GetProperty("data").GetProperty("yuanbao").GetInt32());
        Assert.True(profile.GetProperty("data").GetProperty("stageStars").GetProperty("0-1").GetInt32() >= 1);
    }

    [Fact]
    public async Task Dungeon_StartFinish_VerifiedByReplay_ThenSweepable()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        // 素材副本第 1 階要先通關主線第 1-4 關（開發端點直接標記通關）。
        await c.PostAsJsonAsync("/dev/clear", new { stageId = "0-4", stars = 3 });

        var team = await BuildTeam(c);
        var actions = await PlayStageAuto(c, "res_1", team);
        var done = await Json(await c.PostAsJsonAsync("/stage/finish", new { stageId = "res_1", actions }));
        Assert.True(done.GetProperty("ok").GetBoolean());
        Assert.True(done.GetProperty("data").GetProperty("won").GetBoolean());
        Assert.Equal(2000, done.GetProperty("data").GetProperty("gold").GetInt32());

        var sweep = await Json(await c.PostAsJsonAsync("/dungeon/sweep", new { id = "res_1", count = 1 }));
        Assert.True(sweep.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Dungeon_StartWithoutValidFormation_IsRejected_AndSpendsNothing()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        await c.PostAsJsonAsync("/dev/clear", new { stageId = "0-4", stars = 3 });

        // 沒帶編隊、帶了沒擁有的武將：都被拒絕，體力不扣。
        var before = (await Json(await c.GetAsync("/profile"))).GetProperty("data").GetProperty("stamina").GetProperty("current").GetInt32();
        var none = await Json(await c.PostAsJsonAsync("/stage/start", new { stageId = "res_1" }));
        Assert.Equal("invalid_formation", none.GetProperty("code").GetString());
        var stranger = await Json(await c.PostAsJsonAsync("/stage/start",
            new { stageId = "res_1", formation = new[] { new { heroId = "zhugeliang", lane = 0, row = 0 } } }));
        Assert.Equal("invalid_formation", stranger.GetProperty("code").GetString());
        var after = (await Json(await c.GetAsync("/profile"))).GetProperty("data").GetProperty("stamina").GetProperty("current").GetInt32();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Shop_OrderThenPay_GrantsMonthCardOnce_AndClaimsDaily()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        var order = await Json(await c.PostAsJsonAsync("/shop/order", new { productId = "month_small" }));
        Assert.True(order.GetProperty("ok").GetBoolean());
        string orderId = order.GetProperty("data").GetProperty("orderId").GetString()!;

        // 付款前什麼都沒有
        Assert.Equal(2000, (await Json(await c.GetAsync("/profile"))).GetProperty("data").GetProperty("yuanbao").GetInt32());

        Assert.True((await Json(await c.PostAsJsonAsync("/shop/dev/pay", new { orderId }))).GetProperty("ok").GetBoolean());
        await c.PostAsJsonAsync("/shop/dev/pay", new { orderId }); // 重複通知
        Assert.Equal(2300, (await Json(await c.GetAsync("/profile"))).GetProperty("data").GetProperty("yuanbao").GetInt32());

        var claim = await Json(await c.PostAsJsonAsync("/shop/month-card/claim", new { productId = "month_small" }));
        Assert.Equal(2400, claim.GetProperty("data").GetProperty("yuanbao").GetInt32());
        var again = await Json(await c.PostAsJsonAsync("/shop/month-card/claim", new { productId = "month_small" }));
        Assert.Equal("AlreadyClaimedToday", again.GetProperty("code").GetString());
    }

    [Fact]
    public async Task SoulShop_RejectsWhenNotEnoughSouls()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        var r = await Json(await c.PostAsJsonAsync("/soulshop/buy", new { itemId = "gold" }));
        Assert.Equal("NotEnoughSouls", r.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Equipment_EquipWithoutOwning_IsRejected()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        await c.PostAsJsonAsync("/gacha/pull", new { poolId = "newbie", count = 1 });
        var profile = SanGuo.Core.Data.ProfileSerializer.FromJson((await Json(await c.GetAsync("/profile"))).GetProperty("data").GetRawText());
        string heroId = profile.Heroes.Keys.First();
        var r = await Json(await c.PostAsJsonAsync("/hero/equip", new { heroId, slot = "Weapon", tier = 1 }));
        Assert.Equal("NotOwned", r.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Stage_Finish_WithoutStart_IsRejected()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        var r = await c.PostAsJsonAsync("/stage/finish", new { stageId = "0-1", actions = new object[0] });
        Assert.Equal("no_pending_stage", (await Json(r)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Stage_TamperedReplay_GivesNothing_AndCannotBeRetried()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        await PlayStageAuto(c, "0-1");

        var bad = new object[] { new { kind = "play", cardId = 424242 } };
        var r = await c.PostAsJsonAsync("/stage/finish", new { stageId = "0-1", actions = bad });
        Assert.Equal("invalid_replay", (await Json(r)).GetProperty("code").GetString());

        var profile = await Json(await c.GetAsync("/profile"));
        Assert.Equal(2000, profile.GetProperty("data").GetProperty("yuanbao").GetInt32());

        // 進行中的關卡已清掉：不能拿同一個種子再試
        var again = await c.PostAsJsonAsync("/stage/finish", new { stageId = "0-1", actions = new object[0] });
        Assert.Equal("no_pending_stage", (await Json(again)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Stage_IncompleteReplay_IsALoss_NoRewards()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        var actions = await PlayStageAuto(c, "0-1");
        var half = actions.Take(actions.Count / 2).ToList();

        var done = await Json(await c.PostAsJsonAsync("/stage/finish", new { stageId = "0-1", actions = half }));
        Assert.True(done.GetProperty("ok").GetBoolean());
        Assert.False(done.GetProperty("data").GetProperty("won").GetBoolean());
        var profile = await Json(await c.GetAsync("/profile"));
        Assert.Equal(2000, profile.GetProperty("data").GetProperty("yuanbao").GetInt32());
    }

    [Fact]
    public async Task Stage_Start_SpendsStamina_AndRejectsUnknownStage()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        var chapter1 = await Json(await c.PostAsJsonAsync("/stage/start", new { stageId = "0-1" }));
        Assert.True(chapter1.GetProperty("ok").GetBoolean());
        var profile = await Json(await c.GetAsync("/profile"));
        Assert.Equal(52, profile.GetProperty("data").GetProperty("stamina").GetProperty("current").GetInt32());

        var unknown = await Json(await c.PostAsJsonAsync("/stage/start", new { stageId = "7-1" }));
        Assert.Equal("unknown_stage", unknown.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Sweep_UnknownStage_IsRejected()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        var r = await c.PostAsJsonAsync("/stage/sweep", new { id = "9-9", count = 1 });
        Assert.Equal("unknown_stage", (await Json(r)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task LevelUp_RequiresPlayerLevel_AndMaterials()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        await c.PostAsJsonAsync("/gacha/pull", new { poolId = "newbie", count = 10 });
        var profile = await Json(await c.GetAsync("/profile"));
        string heroId = profile.GetProperty("data").GetProperty("heroes").EnumerateObject().First().Name;

        var r = await c.PostAsJsonAsync("/hero/levelup", new { heroId });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); // 帳號等級 1、沒有經驗書
    }

    [Fact]
    public async Task Quest_Claim_AfterReportedActions()
    {
        var c = Client();
        await c.PostAsync("/login", null);

        // 登入任務在登入時已回報
        var claim = await Json(await c.PostAsJsonAsync("/quest/claim", new { questId = "d_login" }));
        Assert.True(claim.GetProperty("ok").GetBoolean());
        var twice = await Json(await c.PostAsJsonAsync("/quest/claim", new { questId = "d_login" }));
        Assert.Equal("AlreadyClaimed", twice.GetProperty("code").GetString());

        // 抽卡後，每日抽卡任務可領
        await c.PostAsJsonAsync("/gacha/pull", new { poolId = "standard", count = 1 });
        var gacha = await Json(await c.PostAsJsonAsync("/quest/claim", new { questId = "d_gacha" }));
        Assert.True(gacha.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task NextDay_ResetsDailyQuests()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        await c.PostAsJsonAsync("/quest/claim", new { questId = "d_login" });

        _time.Current = _time.Current.AddDays(1);
        await c.PostAsync("/login", null);
        var again = await Json(await c.PostAsJsonAsync("/quest/claim", new { questId = "d_login" }));
        Assert.True(again.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task DevEndpoint_IsHidden_WhenDisabled()
    {
        var plain = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureServices(s =>
            {
                s.RemoveAll<IProfileStore>();
                s.AddSingleton<IProfileStore>(new InMemoryProfileStore());
                s.RemoveAll<IWorldBossBoard>();
                s.AddSingleton<IWorldBossBoard>(new InMemoryWorldBossBoard());
            }));
        var c = plain.CreateClient();
        c.DefaultRequestHeaders.Add("X-Account", "x");
        var r = await c.PostAsJsonAsync("/dev/clear", new { stageId = "0-1", stars = 3 });
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task ConcurrentPulls_NeverOverspend()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        // 2000 元寶只夠一次十連；同時送 5 個請求，只能成功 1 個
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => c.PostAsJsonAsync("/gacha/pull", new { poolId = "standard", count = 10 }));
        var responses = await Task.WhenAll(tasks);
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        var profile = await Json(await c.GetAsync("/profile"));
        Assert.Equal(0, profile.GetProperty("data").GetProperty("yuanbao").GetInt32());
    }

    [Fact]
    public async Task WorldBoss_UnlocksAfterChapter2_AndCountsAttempts()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        var locked = (await Json(await c.GetAsync("/worldboss"))).GetProperty("data");
        Assert.False(locked.GetProperty("unlocked").GetBoolean());

        foreach (var id in new[] { "0-1", "0-2", "0-3", "2-10" })
            await c.PostAsJsonAsync("/dev/clear", new { stageId = id, stars = 3 });
        var panel = (await Json(await c.GetAsync("/worldboss"))).GetProperty("data");
        Assert.True(panel.GetProperty("unlocked").GetBoolean());
        Assert.Equal(3, panel.GetProperty("attemptsLeft").GetInt32());
        Assert.Equal("2026-10", panel.GetProperty("season").GetString());

        var formation = new[] { new { heroId = "zhangfei", lane = 2, row = 3 }, new { heroId = "liubei", lane = 2, row = 4 } };
        var start = await Json(await c.PostAsJsonAsync("/stage/start", new { stageId = WorldBoss.StageId, formation }));
        Assert.True(start.GetProperty("ok").GetBoolean());
        // 只結束一回合就交卷：紀錄合法，傷害 0、不上榜。
        var finish = await Json(await c.PostAsJsonAsync("/stage/finish", new { stageId = WorldBoss.StageId, actions = new[] { new { kind = "end" } } }));
        Assert.True(finish.GetProperty("ok").GetBoolean());
        Assert.Equal(0, finish.GetProperty("data").GetProperty("damage").GetInt64());

        panel = (await Json(await c.GetAsync("/worldboss"))).GetProperty("data");
        Assert.Equal(2, panel.GetProperty("attemptsLeft").GetInt32());
        Assert.Equal(0, panel.GetProperty("top").GetArrayLength());
    }

    [Fact]
    public async Task WorldBoss_Leaderboard_DoesNotExposeOtherAccountIds()
    {
        var c = Client();
        await c.PostAsync("/login", null);
        _factory.Services.GetRequiredService<IWorldBossBoard>().Submit("2026-10", "bob-secret-id", 500);
        var top = (await Json(await c.GetAsync("/worldboss"))).GetProperty("data").GetProperty("top");
        Assert.Equal(1, top.GetArrayLength());
        string shown = top[0].GetProperty("account").GetString()!;
        Assert.DoesNotContain("bob", shown);
        Assert.StartsWith("玩家", shown);
    }
}
