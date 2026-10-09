using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace SanGuo.Server;

public sealed record Session(string Token, string AccountId, string Nickname, string? Username, long ExpiresAt);

public sealed record AccountInfo(string AccountId, string Nickname, string? Username);

public sealed class SqliteAccountStore
{
    public const int SessionDays = 30;
    private const int Iterations = 100_000;

    private readonly string _connectionString;
    private readonly TimeProvider _time;

    public SqliteAccountStore(string connectionString, TimeProvider time)
    {
        _connectionString = connectionString;
        _time = time;
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS accounts (
                account_id TEXT PRIMARY KEY,
                username TEXT UNIQUE,
                password_hash TEXT,
                guest_key_hash TEXT UNIQUE,
                nickname TEXT NOT NULL,
                created_at INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS sessions (
                token_hash TEXT PRIMARY KEY,
                account_id TEXT NOT NULL,
                expires_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS sessions_account ON sessions (account_id);
            """;
        cmd.ExecuteNonQuery();
    }

    private long Now => _time.GetUtcNow().ToUnixTimeSeconds();

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public static string? NormalizeUsername(string? username)
    {
        if (username == null) return null;
        var u = username.Trim().ToLowerInvariant();
        if (u.Length < 4 || u.Length > 20) return null;
        foreach (char c in u)
            if (!(c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c == '_')) return null;
        return u;
    }

    public static bool ValidPassword(string? password) => password != null && password.Length >= 8 && password.Length <= 64;

    public static string? NormalizeNickname(string? nickname)
    {
        if (nickname == null) return null;
        var n = nickname.Trim();
        if (n.Length < 2 || n.Length > 12) return null;
        foreach (char c in n)
            if (char.IsControl(c)) return null;
        return n;
    }

    public static bool ValidGuestKey(string? key) => key != null && key.Length >= 16 && key.Length <= 128;

    public Session Guest(string guestKey)
    {
        using var conn = Open();
        string keyHash = Sha256(guestKey);
        string? id = Scalar(conn, "SELECT account_id FROM accounts WHERE guest_key_hash = $k", ("$k", keyHash));
        if (id == null)
        {
            id = NewAccountId();
            Exec(conn, "INSERT INTO accounts (account_id, guest_key_hash, nickname, created_at) VALUES ($id, $k, $n, $at)",
                ("$id", id), ("$k", keyHash), ("$n", DefaultNickname()), ("$at", Now));
        }
        return IssueSession(conn, id);
    }

    public Session? Register(string username, string password)
    {
        using var conn = Open();
        if (Scalar(conn, "SELECT account_id FROM accounts WHERE username = $u", ("$u", username)) != null) return null;
        string id = NewAccountId();
        Exec(conn, "INSERT INTO accounts (account_id, username, password_hash, nickname, created_at) VALUES ($id, $u, $p, $n, $at)",
            ("$id", id), ("$u", username), ("$p", HashPassword(password)), ("$n", DefaultNickname()), ("$at", Now));
        return IssueSession(conn, id);
    }

    public Session? Login(string username, string password)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT account_id, password_hash FROM accounts WHERE username = $u";
        cmd.Parameters.AddWithValue("$u", username);
        string? id = null, hash = null;
        using (var r = cmd.ExecuteReader())
            if (r.Read()) { id = r.GetString(0); hash = r.IsDBNull(1) ? null : r.GetString(1); }
        if (id == null || hash == null)
        {
            VerifyPassword(password, DummyHash);
            return null;
        }
        return VerifyPassword(password, hash) ? IssueSession(conn, id) : null;
    }

    public string? Bind(string accountId, string username, string password)
    {
        using var conn = Open();
        if (Scalar(conn, "SELECT username FROM accounts WHERE account_id = $id", ("$id", accountId)) != null) return "already_bound";
        if (Scalar(conn, "SELECT account_id FROM accounts WHERE username = $u", ("$u", username)) != null) return "username_taken";
        try
        {
            Exec(conn, "UPDATE accounts SET username = $u, password_hash = $p WHERE account_id = $id AND username IS NULL",
                ("$u", username), ("$p", HashPassword(password)), ("$id", accountId));
        }
        catch (SqliteException e) when (e.SqliteErrorCode == 19)
        {
            return "username_taken";
        }
        return null;
    }

    public string? Resolve(string token)
    {
        using var conn = Open();
        return Scalar(conn, "SELECT account_id FROM sessions WHERE token_hash = $t AND expires_at > $now",
            ("$t", Sha256(token)), ("$now", Now));
    }

    public void Logout(string token)
    {
        using var conn = Open();
        Exec(conn, "DELETE FROM sessions WHERE token_hash = $t", ("$t", Sha256(token)));
    }

    public AccountInfo? Info(string accountId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT nickname, username FROM accounts WHERE account_id = $id";
        cmd.Parameters.AddWithValue("$id", accountId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? new AccountInfo(accountId, r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1)) : null;
    }

    public string? Nickname(string accountId)
    {
        using var conn = Open();
        return Scalar(conn, "SELECT nickname FROM accounts WHERE account_id = $id", ("$id", accountId));
    }

    public bool SetNickname(string accountId, string nickname)
    {
        using var conn = Open();
        return Exec(conn, "UPDATE accounts SET nickname = $n WHERE account_id = $id", ("$n", nickname), ("$id", accountId)) > 0;
    }

    private Session IssueSession(SqliteConnection conn, string accountId)
    {
        long now = Now;
        Exec(conn, "DELETE FROM sessions WHERE account_id = $id AND expires_at <= $now", ("$id", accountId), ("$now", now));
        string token = Base64Url(RandomNumberGenerator.GetBytes(32));
        long expires = now + SessionDays * 86400L;
        Exec(conn, "INSERT INTO sessions (token_hash, account_id, expires_at) VALUES ($t, $id, $exp)",
            ("$t", Sha256(token)), ("$id", accountId), ("$exp", expires));
        var info = InfoOn(conn, accountId)!;
        return new Session(token, accountId, info.Nickname, info.Username, expires);
    }

    private static AccountInfo? InfoOn(SqliteConnection conn, string accountId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT nickname, username FROM accounts WHERE account_id = $id";
        cmd.Parameters.AddWithValue("$id", accountId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? new AccountInfo(accountId, r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1)) : null;
    }

    private static string NewAccountId() => "u_" + Guid.NewGuid().ToString("N");

    private static string DefaultNickname() => "主公" + RandomNumberGenerator.GetInt32(10000, 100000);

    private static readonly string DummyHash = HashPassword("dummy-password-for-timing");

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out int iterations)) return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static string Sha256(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string? Scalar(SqliteConnection conn, string sql, params (string, object)[] args)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v);
        return cmd.ExecuteScalar() as string;
    }

    private static int Exec(SqliteConnection conn, string sql, params (string, object)[] args)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v);
        return cmd.ExecuteNonQuery();
    }
}
