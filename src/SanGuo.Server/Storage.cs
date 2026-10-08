using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;

namespace SanGuo.Server;

/// <summary>玩家存檔儲存層。上線前要換成 PostgreSQL 時，只需要另寫一個實作。</summary>
public interface IProfileStore
{
    Task<PlayerProfile?> LoadAsync(string accountId);
    Task SaveAsync(string accountId, PlayerProfile profile);
}

public sealed class InMemoryProfileStore : IProfileStore
{
    private readonly ConcurrentDictionary<string, string> _json = new();

    public Task<PlayerProfile?> LoadAsync(string accountId) =>
        Task.FromResult(_json.TryGetValue(accountId, out var json) ? ProfileSerializer.FromJson(json) : null);

    public Task SaveAsync(string accountId, PlayerProfile profile)
    {
        _json[accountId] = ProfileSerializer.ToJson(profile);
        return Task.CompletedTask;
    }
}

/// <summary>以 SQLite 儲存：一個帳號一列，存整份存檔 JSON（含版本號，欄位演進由序列化器處理）。</summary>
public sealed class SqliteProfileStore : IProfileStore
{
    private readonly string _connectionString;

    public SqliteProfileStore(string connectionString)
    {
        _connectionString = connectionString;
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS profiles (
                account_id TEXT PRIMARY KEY,
                json TEXT NOT NULL,
                updated_at INTEGER NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public async Task<PlayerProfile?> LoadAsync(string accountId)
    {
        await using var conn = Open();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT json FROM profiles WHERE account_id = $id";
        cmd.Parameters.AddWithValue("$id", accountId);
        var json = await cmd.ExecuteScalarAsync() as string;
        return json == null ? null : ProfileSerializer.FromJson(json);
    }

    public async Task SaveAsync(string accountId, PlayerProfile profile)
    {
        await using var conn = Open();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO profiles (account_id, json, updated_at) VALUES ($id, $json, $at)
            ON CONFLICT(account_id) DO UPDATE SET json = $json, updated_at = $at;
            """;
        cmd.Parameters.AddWithValue("$id", accountId);
        cmd.Parameters.AddWithValue("$json", ProfileSerializer.ToJson(profile));
        cmd.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await cmd.ExecuteNonQueryAsync();
    }
}

/// <summary>世界 Boss 排行榜（SQLite）：每季每帳號一列，只保留最高分；歷季資料保留，換季結算查上一季。</summary>
public sealed class SqliteWorldBossBoard : IWorldBossBoard
{
    private readonly string _connectionString;

    public SqliteWorldBossBoard(string connectionString)
    {
        _connectionString = connectionString;
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS world_boss (
                season TEXT NOT NULL,
                account_id TEXT NOT NULL,
                best INTEGER NOT NULL,
                PRIMARY KEY (season, account_id)
            );
            CREATE INDEX IF NOT EXISTS world_boss_rank ON world_boss (season, best DESC);
            """;
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public void Submit(string season, string accountId, long best)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO world_boss (season, account_id, best) VALUES ($s, $a, $b)
            ON CONFLICT(season, account_id) DO UPDATE SET best = MAX(best, $b);
            """;
        cmd.Parameters.AddWithValue("$s", season);
        cmd.Parameters.AddWithValue("$a", accountId);
        cmd.Parameters.AddWithValue("$b", best);
        cmd.ExecuteNonQuery();
    }

    public (int Rank, int Total) RankOf(string season, long score)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*), COALESCE(SUM(CASE WHEN best > $b THEN 1 ELSE 0 END), 0) FROM world_boss WHERE season = $s";
        cmd.Parameters.AddWithValue("$s", season);
        cmd.Parameters.AddWithValue("$b", score);
        using var r = cmd.ExecuteReader();
        r.Read();
        long total = r.GetInt64(0), higher = r.GetInt64(1);
        return total == 0 ? (1, 1) : ((int)higher + 1, (int)total);
    }

    public List<(string AccountId, long Best)> Top(string season, int count)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT account_id, best FROM world_boss WHERE season = $s ORDER BY best DESC, account_id LIMIT $n";
        cmd.Parameters.AddWithValue("$s", season);
        cmd.Parameters.AddWithValue("$n", count);
        var list = new List<(string, long)>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add((r.GetString(0), r.GetInt64(1)));
        return list;
    }
}
