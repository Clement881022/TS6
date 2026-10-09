using SanGuo.Core.Meta;

/// <summary>
/// 世界 Boss 排行榜 + 背景玩家族群：名次與參賽人數把族群檔裡的分數也算進去，讓百分位獎勵有意義。
/// 族群檔每行「賽季,分數」，由 <see cref="Kpi"/> 用前一批模擬的成績依付費比例抽樣產生；沒有族群檔就只有模擬帳號自己。
/// </summary>
public sealed class PopulationBoard : IWorldBossBoard
{
    private readonly IWorldBossBoard _inner;
    private readonly Dictionary<string, List<long>> _synthetic = new();

    public PopulationBoard(IWorldBossBoard inner, string file)
    {
        _inner = inner;
        if (file == null || !File.Exists(file)) return;
        foreach (var line in File.ReadAllLines(file))
        {
            var parts = line.Split(',');
            if (parts.Length != 2 || !long.TryParse(parts[1], out long score)) continue;
            if (!_synthetic.TryGetValue(parts[0], out var list)) _synthetic[parts[0]] = list = new List<long>();
            list.Add(score);
        }
    }

    public void Submit(string season, string accountId, long best) => _inner.Submit(season, accountId, best);

    public (int Rank, int Total) RankOf(string season, long score)
    {
        var (rank, total) = _inner.RankOf(season, score);
        if (!_synthetic.TryGetValue(season, out var list)) return (rank, total);
        return (rank + list.Count(s => s > score), total + list.Count);
    }

    public List<(string AccountId, long Best)> Top(string season, int count) => _inner.Top(season, count);
}
