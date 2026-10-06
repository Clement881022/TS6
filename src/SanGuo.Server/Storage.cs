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
