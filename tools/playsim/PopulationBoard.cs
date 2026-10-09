using SanGuo.Core.Meta;

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
