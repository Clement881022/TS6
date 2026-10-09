using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SanGuo.Core.Meta;
using SanGuo.Server;

namespace SanGuo.Server.Tests;

/// <summary>帳號系統：遊客登入、註冊 / 登入、綁定、token 驗證、暱稱，以及正式模式不接受 X-Account。</summary>
public sealed class AccountTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sanguo-acct-{Guid.NewGuid():N}.db");
    private readonly TestTime _time = new();
    private readonly List<WebApplicationFactory<Program>> _factories = new();

    private WebApplicationFactory<Program> Factory(bool dev = false)
    {
        var f = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
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
                s.AddSingleton(new ServerOptions { EnableDevEndpoints = dev });
            }));
        _factories.Add(f);
        return f;
    }

    public void Dispose()
    {
        foreach (var f in _factories) f.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch (IOException) { }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static HttpClient WithToken(WebApplicationFactory<Program> f, string token)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private static async Task<string> Token(HttpResponseMessage r)
    {
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await Json(r)).GetProperty("data").GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Guest_SameKeySameAccount_TokenWorksForGame()
    {
        var f = Factory();
        var anon = f.CreateClient();
        string key = Guid.NewGuid().ToString("N");
        var first = (await Json(await anon.PostAsJsonAsync("/auth/guest", new { guestKey = key }))).GetProperty("data");
        var again = (await Json(await anon.PostAsJsonAsync("/auth/guest", new { guestKey = key }))).GetProperty("data");
        Assert.Equal(first.GetProperty("accountId").GetString(), again.GetProperty("accountId").GetString());
        Assert.False(first.GetProperty("bound").GetBoolean());
        Assert.StartsWith("主公", first.GetProperty("nickname").GetString());

        var c = WithToken(f, first.GetProperty("token").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync("/login", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/profile")).StatusCode);

        var other = (await Json(await anon.PostAsJsonAsync("/auth/guest", new { guestKey = Guid.NewGuid().ToString("N") }))).GetProperty("data");
        Assert.NotEqual(first.GetProperty("accountId").GetString(), other.GetProperty("accountId").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync("/auth/guest", new { guestKey = "short" })).StatusCode);
    }

    [Fact]
    public async Task Register_Login_WrongPassword_Taken()
    {
        var f = Factory();
        var anon = f.CreateClient();
        var reg = await Token(await anon.PostAsJsonAsync("/auth/register", new { username = "Liu_Bei", password = "taoyuan123" }));
        Assert.Equal(HttpStatusCode.OK, (await WithToken(f, reg).PostAsync("/login", null)).StatusCode);

        // 帳號不分大小寫
        await Token(await anon.PostAsJsonAsync("/auth/login", new { username = "liu_bei", password = "taoyuan123" }));
        var wrong = await anon.PostAsJsonAsync("/auth/login", new { username = "liu_bei", password = "wrongpass1" });
        Assert.Equal("wrong_credentials", (await Json(wrong)).GetProperty("code").GetString());
        var missing = await anon.PostAsJsonAsync("/auth/login", new { username = "nobody", password = "taoyuan123" });
        Assert.Equal("wrong_credentials", (await Json(missing)).GetProperty("code").GetString());

        var taken = await anon.PostAsJsonAsync("/auth/register", new { username = "LIU_BEI", password = "another123" });
        Assert.Equal("username_taken", (await Json(taken)).GetProperty("code").GetString());
        var badName = await anon.PostAsJsonAsync("/auth/register", new { username = "劉備", password = "taoyuan123" });
        Assert.Equal("invalid_username", (await Json(badName)).GetProperty("code").GetString());
        var shortPw = await anon.PostAsJsonAsync("/auth/register", new { username = "guanyu", password = "123" });
        Assert.Equal("invalid_password", (await Json(shortPw)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Guest_BindThenLoginElsewhere_KeepsProgress()
    {
        var f = Factory();
        var anon = f.CreateClient();
        var guest = WithToken(f, await Token(await anon.PostAsJsonAsync("/auth/guest", new { guestKey = Guid.NewGuid().ToString("N") })));
        await guest.PostAsync("/login", null);
        var pull = await guest.PostAsJsonAsync("/gacha/pull", new { poolId = "standard", count = 1 });
        Assert.Equal(HttpStatusCode.OK, pull.StatusCode);
        int yuanbao = (await Json(await guest.GetAsync("/profile"))).GetProperty("data").GetProperty("yuanbao").GetInt32();

        Assert.Equal(HttpStatusCode.OK, (await guest.PostAsJsonAsync("/auth/bind", new { username = "zhaoyun", password = "changban1" })).StatusCode);
        var again = await guest.PostAsJsonAsync("/auth/bind", new { username = "zhaoyun2", password = "changban1" });
        Assert.Equal("already_bound", (await Json(again)).GetProperty("code").GetString());

        // 換一台裝置用帳號密碼登入，看到同一份存檔。
        var device2 = WithToken(f, await Token(await anon.PostAsJsonAsync("/auth/login", new { username = "zhaoyun", password = "changban1" })));
        var profile = (await Json(await device2.GetAsync("/profile"))).GetProperty("data");
        Assert.Equal(yuanbao, profile.GetProperty("yuanbao").GetInt32());
        var me = (await Json(await device2.GetAsync("/auth/me"))).GetProperty("data");
        Assert.True(me.GetProperty("bound").GetBoolean());
        Assert.Equal("zhaoyun", me.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Token_InvalidAfterLogoutOrExpiry()
    {
        var f = Factory();
        var anon = f.CreateClient();
        string token = await Token(await anon.PostAsJsonAsync("/auth/guest", new { guestKey = Guid.NewGuid().ToString("N") }));
        var c = WithToken(f, token);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync("/login", null)).StatusCode);
        await c.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/profile")).StatusCode);

        var c2 = WithToken(f, await Token(await anon.PostAsJsonAsync("/auth/guest", new { guestKey = Guid.NewGuid().ToString("N") })));
        Assert.Equal(HttpStatusCode.OK, (await c2.PostAsync("/login", null)).StatusCode);
        _time.Current = _time.Current.AddDays(SqliteAccountStore.SessionDays + 1);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c2.GetAsync("/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await WithToken(f, "forged-token").GetAsync("/profile")).StatusCode);
    }

    [Fact]
    public async Task XAccountHeader_RejectedOutsideDevMode()
    {
        var prod = Factory(dev: false).CreateClient();
        prod.DefaultRequestHeaders.Add("X-Account", "alice");
        Assert.Equal(HttpStatusCode.Unauthorized, (await prod.PostAsync("/login", null)).StatusCode);

        var dev = Factory(dev: true).CreateClient();
        dev.DefaultRequestHeaders.Add("X-Account", "alice");
        Assert.Equal(HttpStatusCode.OK, (await dev.PostAsync("/login", null)).StatusCode);
    }

    [Fact]
    public async Task Nickname_ShownOnLeaderboard()
    {
        var f = Factory();
        var anon = f.CreateClient();
        var bob = (await Json(await anon.PostAsJsonAsync("/auth/guest", new { guestKey = Guid.NewGuid().ToString("N") }))).GetProperty("data");
        var bobClient = WithToken(f, bob.GetProperty("token").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await bobClient.PostAsJsonAsync("/account/nickname", new { nickname = "常山趙子龍" })).StatusCode);
        var bad = await bobClient.PostAsJsonAsync("/account/nickname", new { nickname = "a" });
        Assert.Equal("invalid_nickname", (await Json(bad)).GetProperty("code").GetString());
        f.Services.GetRequiredService<IWorldBossBoard>().Submit("2026-10", bob.GetProperty("accountId").GetString()!, 500);

        var alice = WithToken(f, await Token(await anon.PostAsJsonAsync("/auth/guest", new { guestKey = Guid.NewGuid().ToString("N") })));
        await alice.PostAsync("/login", null);
        var top = (await Json(await alice.GetAsync("/worldboss"))).GetProperty("data").GetProperty("top");
        Assert.Equal("常山趙子龍", top[0].GetProperty("account").GetString());
    }

    [Fact]
    public void Password_HashVerifies_AndIsSalted()
    {
        string a = SqliteAccountStore.HashPassword("taoyuan123"), b = SqliteAccountStore.HashPassword("taoyuan123");
        Assert.NotEqual(a, b);
        Assert.True(SqliteAccountStore.VerifyPassword("taoyuan123", a));
        Assert.False(SqliteAccountStore.VerifyPassword("taoyuan124", a));
    }
}
