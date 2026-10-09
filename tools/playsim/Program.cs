using System.Text;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using SanGuo.Server;

Console.OutputEncoding = Encoding.UTF8;
if (args.Length > 0 && args[0] == "exp") { var t = Experiments.Run(); File.WriteAllText(args.Length > 1 ? args[1] : "experiments.md", t); Console.WriteLine(t); return; }
if (args.Length > 0 && args[0] == "multi") { await Kpi.Multi(int.Parse(args[1]), int.Parse(args[2]), args[3]); return; }
if (args.Length > 0 && args[0] == "kpi") { Kpi.Write(args[1]); return; }
if (args.Length > 0 && args[0] == "winat") { Console.WriteLine($"{args[1]} Lv{args[2]} {args[3]}★ {args[4]}階：{Calibrate.WinAt(args[1], int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4])):0}%"); return; }
if (args.Length > 0 && args[0] == "calib") { foreach (var o in args.Skip(1)) Console.WriteLine(Calibrate.Run(o.Split(',').Select(int.Parse).ToArray())); return; }
int days = args.Length > 0 ? int.Parse(args[0]) : 60;
string outDir = args.Length > 1 ? args[1] : ".";
int popIndex = Array.IndexOf(args, "--pop");
string popFile = popIndex >= 0 && popIndex + 1 < args.Length ? args[popIndex + 1] : null;
Directory.CreateDirectory(outDir);

string db = Path.Combine(Path.GetTempPath(), $"playsim-{Guid.NewGuid():N}.db");
var clock = new SimClock { Current = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.FromHours(8)) };
var store = new SqliteProfileStore($"Data Source={db}");
IWorldBossBoard board = new PopulationBoard(new SqliteWorldBossBoard($"Data Source={db}"), popFile);
var accounts = new SqliteAccountStore($"Data Source={db}", clock);
var game = new GameService(store, board, clock, new ServerOptions { EnableDevEndpoints = true, StartingHeroExp = 0 } , accounts);

var personas = new List<Persona>
{
    new Persona("無課", "f2p_player01", 0),
    new Persona("小課", "light_spender01", 1),
    new Persona("大課", "heavy_spender01", 2),
    new Persona("鯨魚", "whale_hypo01", 3),
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

var sb = new StringBuilder();
foreach (var pe in personas) sb.Append(pe.Sim.Report());
sb.AppendLine();
sb.AppendLine("# 戰鬥 meta（全部帳號合計）");
sb.Append(Meta.Report());
File.WriteAllText(Path.Combine(outDir, "report.md"), sb.ToString());
foreach (var pe in personas)
{
    File.WriteAllText(Path.Combine(outDir, $"daily-{pe.Username}.csv"), pe.Sim.DailyCsv());
    File.WriteAllText(Path.Combine(outDir, $"summary-{pe.Username}.txt"), await pe.Sim.Summary());
}
File.WriteAllText(Path.Combine(outDir, "meta.txt"), Meta.Summary());
Console.WriteLine(sb.ToString());
Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
try { File.Delete(db); } catch { }
