using System.Text;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using SanGuo.Server;

public sealed class SimClock : TimeProvider
{
    public DateTimeOffset Current;
    public override DateTimeOffset GetUtcNow() => Current.ToUniversalTime();
}

public sealed class Persona
{
    public string Label, Username, AccountId;
    public int Tier;
    public PlayerSim Sim;
    public Persona(string label, string user, int tier) { Label = label; Username = user; Tier = tier; }
}

public sealed class DayStat
{
    public int Day;
    public double Seconds;
    public double BattleSeconds, StorySeconds, MenuSeconds, BossSeconds;
    public int Battles, Losses, StoryLosses, NewClears, Sweeps;
    public int StaminaSpent, StaminaWasted;
    public int Level;
    public string Frontier = "";
    public int HardCleared;
    public string Team = "";
    public double Power;
    public int Yuanbao, PullsTotal, UrOwned;
    public long BossBest, BossToday;
    public int SpendCny;
}
