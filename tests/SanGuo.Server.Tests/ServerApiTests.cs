using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        var early = await c.PostAsJsonAsync("/stage/sweep", new { id = "1-5", count = 2 });
        Assert.Equal("NotThreeStars", (await Json(early)).GetProperty("code").GetString());

        await c.PostAsJsonAsync("/dev/clear", new { stageId = "1-5", stars = 3 });
        var ok = await Json(await c.PostAsJsonAsync("/stage/sweep", new { id = "1-5", count = 2 }));
        Assert.True(ok.GetProperty("ok").GetBoolean());
        Assert.Equal(1400, ok.GetProperty("data").GetProperty("gold").GetInt32()); // 700 × 2
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
            }));
        var c = plain.CreateClient();
        c.DefaultRequestHeaders.Add("X-Account", "x");
        var r = await c.PostAsJsonAsync("/dev/clear", new { stageId = "1-1", stars = 3 });
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
}
