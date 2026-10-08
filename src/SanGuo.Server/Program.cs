using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using SanGuo.Server;

var builder = WebApplication.CreateBuilder(args);

var options = builder.Configuration.GetSection("Game").Get<ServerOptions>() ?? new ServerOptions();
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(TimeProvider.System);
var dbPath = builder.Configuration["Database:Path"] ?? "sanguo.db";
builder.Services.AddSingleton<IProfileStore>(_ => new SqliteProfileStore($"Data Source={dbPath}"));
builder.Services.AddSingleton<IWorldBossBoard>(_ => new SqliteWorldBossBoard($"Data Source={dbPath}"));
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
app.MapPost("/hero/equip", (HttpRequest req, GameService g, EquipRequest body) =>
    Handle(req, a => g.Equip(a, body.HeroId, body.Slot, body.Tier)));
app.MapPost("/hero/unequip", (HttpRequest req, GameService g, UnequipRequest body) =>
    Handle(req, a => g.Unequip(a, body.HeroId, body.Slot)));
app.MapPost("/equipment/dismantle", (HttpRequest req, GameService g, DismantleRequest body) =>
    Handle(req, a => g.Dismantle(a, body.Slot, body.Tier, body.Count)));
app.MapPost("/soulshop/buy", (HttpRequest req, GameService g, SoulBuyRequest body) =>
    Handle(req, a => g.BuySoulItem(a, body.ItemId)));
app.MapPost("/hero/breakthrough", (HttpRequest req, GameService g, HeroRequest body) =>
    Handle(req, a => g.Breakthrough(a, body.HeroId)));

app.MapPost("/stage/start", (HttpRequest req, GameService g, StageRequest body) =>
    Handle(req, a => g.StartStage(a, body.StageId,
        body.Formation?.ConvertAll(f => new FormationEntry(f.HeroId, f.Lane, f.Row)))));
app.MapPost("/stage/finish", (HttpRequest req, GameService g, FinishRequest body) =>
{
    var actions = new List<ReplayAction>();
    foreach (var d in body.Actions ?? new List<ReplayActionDto>())
    {
        switch (d.Kind)
        {
            case "play": actions.Add(ReplayAction.Play(d.CardId, d.Lane, d.Row, d.UnitId)); break;
            case "end": actions.Add(ReplayAction.EndTurn()); break;
            default: return Task.FromResult(Respond(ApiResult.Fail("bad_action")));
        }
    }
    return Handle(req, a => g.FinishStage(a, body.StageId, actions));
});
app.MapPost("/stage/sweep", (HttpRequest req, GameService g, SweepRequest body) =>
    Handle(req, a => g.SweepStage(a, body.Id, body.Count)));
app.MapPost("/dungeon/sweep", (HttpRequest req, GameService g, SweepRequest body) =>
    Handle(req, a => g.SweepDungeon(a, body.Id, body.Count)));

app.MapPost("/quest/claim", (HttpRequest req, GameService g, QuestRequest body) =>
    Handle(req, a => g.ClaimQuest(a, body.QuestId)));
app.MapPost("/quest/milestone", (HttpRequest req, GameService g, MilestoneRequest body) =>
    Handle(req, a => g.ClaimMilestone(a, body.Points)));

app.MapPost("/shop/order", (HttpRequest req, GameService g, ProductRequest body) =>
    Handle(req, a => g.CreateOrder(a, body.ProductId)));
app.MapPost("/shop/dev/pay", (HttpRequest req, GameService g, OrderRequest body) =>
    g.DevEndpointsEnabled ? Handle(req, a => g.DevPay(a, body.OrderId)) : Task.FromResult(Results.NotFound()));
app.MapPost("/shop/month-card/claim", (HttpRequest req, GameService g, ProductRequest body) =>
    Handle(req, a => g.ClaimMonthCard(a, body.ProductId)));

app.MapGet("/worldboss", (HttpRequest req, GameService g) => Handle(req, g.GetWorldBoss));
app.MapPost("/dev/clear", (HttpRequest req, GameService g, DevClearRequest body) =>
    g.DevEndpointsEnabled ? Handle(req, a => g.DevClearStage(a, body.StageId, body.Stars)) : Task.FromResult(Results.NotFound()));

app.Run();

public sealed record PullRequest(string PoolId, int Count);
public sealed record FormationEntryDto(string HeroId, int Lane, int Row);
public sealed record StageRequest(string StageId, List<FormationEntryDto>? Formation = null);
public sealed record ReplayActionDto(string Kind, int CardId = 0, int UnitId = -1, int Lane = -1, int Row = -1);
public sealed record FinishRequest(string StageId, List<ReplayActionDto>? Actions);
public sealed record HeroRequest(string HeroId);
public sealed record EquipRequest(string HeroId, string Slot, int Tier);
public sealed record UnequipRequest(string HeroId, string Slot);
public sealed record DismantleRequest(string Slot, int Tier, int Count);
public sealed record SoulBuyRequest(string ItemId);
public sealed record SweepRequest(string Id, int Count);
public sealed record QuestRequest(string QuestId);
public sealed record MilestoneRequest(int Points);
public sealed record ProductRequest(string ProductId);
public sealed record OrderRequest(string OrderId);
public sealed record DevClearRequest(string StageId, int Stars);

public partial class Program { }
