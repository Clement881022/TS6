using SanGuo.Server;

var builder = WebApplication.CreateBuilder(args);

var options = builder.Configuration.GetSection("Game").Get<ServerOptions>() ?? new ServerOptions();
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(TimeProvider.System);
var dbPath = builder.Configuration["Database:Path"] ?? "sanguo.db";
builder.Services.AddSingleton<IProfileStore>(_ => new SqliteProfileStore($"Data Source={dbPath}"));
builder.Services.AddSingleton<GameService>();

var app = builder.Build();

// 帳號辨識目前是占位：以 X-Account 標頭當帳號 id。正式版要換成真正的登入與 token 驗證。
static string? Account(HttpRequest req) =>
    req.Headers.TryGetValue("X-Account", out var v) && !string.IsNullOrWhiteSpace(v) ? v.ToString() : null;

static IResult Respond(ApiResult r) => r.Ok ? Results.Ok(r) : Results.BadRequest(r);

static async Task<IResult> Handle(HttpRequest req, Func<string, Task<ApiResult>> action)
{
    var account = Account(req);
    if (account == null) return Results.Unauthorized();
    return Respond(await action(account));
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/login", (HttpRequest req, GameService g) => Handle(req, g.Login));
app.MapGet("/profile", (HttpRequest req, GameService g) => Handle(req, g.GetProfile));

app.MapPost("/gacha/pull", (HttpRequest req, GameService g, PullRequest body) =>
    Handle(req, a => g.Pull(a, body.PoolId, body.Count)));

app.MapPost("/hero/levelup", (HttpRequest req, GameService g, HeroRequest body) =>
    Handle(req, a => g.LevelUp(a, body.HeroId)));
app.MapPost("/hero/enhance", (HttpRequest req, GameService g, EnhanceRequest body) =>
    Handle(req, a => g.Enhance(a, body.HeroId, body.CardId)));
app.MapPost("/hero/breakthrough", (HttpRequest req, GameService g, HeroRequest body) =>
    Handle(req, a => g.Breakthrough(a, body.HeroId)));

app.MapPost("/stage/sweep", (HttpRequest req, GameService g, SweepRequest body) =>
    Handle(req, a => g.SweepStage(a, body.Id, body.Count)));
app.MapPost("/dungeon/sweep", (HttpRequest req, GameService g, SweepRequest body) =>
    Handle(req, a => g.SweepDungeon(a, body.Id, body.Count)));

app.MapPost("/quest/claim", (HttpRequest req, GameService g, QuestRequest body) =>
    Handle(req, a => g.ClaimQuest(a, body.QuestId)));
app.MapPost("/quest/milestone", (HttpRequest req, GameService g, MilestoneRequest body) =>
    Handle(req, a => g.ClaimMilestone(a, body.Points)));

app.MapPost("/dev/clear", (HttpRequest req, GameService g, DevClearRequest body) =>
    g.DevEndpointsEnabled ? Handle(req, a => g.DevClearStage(a, body.StageId, body.Stars)) : Task.FromResult(Results.NotFound()));

app.Run();

public sealed record PullRequest(string PoolId, int Count);
public sealed record HeroRequest(string HeroId);
public sealed record EnhanceRequest(string HeroId, string CardId);
public sealed record SweepRequest(string Id, int Count);
public sealed record QuestRequest(string QuestId);
public sealed record MilestoneRequest(int Points);
public sealed record DevClearRequest(string StageId, int Stars);

public partial class Program { }
