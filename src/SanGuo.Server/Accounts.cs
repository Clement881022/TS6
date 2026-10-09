using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace SanGuo.Server;

/// <summary>登入成功的結果：給客戶端的 token 與帳號資訊。</summary>
public sealed record Session(string Token, string AccountId, string Nickname, string? Username, long ExpiresAt);

/// <summary>帳號資訊（不含任何憑證）。</summary>
public sealed record AccountInfo(string AccountId, string Nickname, string? Username);

/// <summary>
/// 帳號與登入（SQLite）：
/// 遊客 = 客戶端自己產生的隨機金鑰（存在裝置上，不是硬體識別碼），刪掉遊戲就找不回，所以提供「綁定帳號密碼」。
/// 密碼以 PBKDF2-SHA256 加鹽雜湊；登入後發隨機 token，資料庫只存 token 的 SHA-256。
/// 帳號 id（u_ 開頭）只在伺服器內部使用，對外顯示一律用暱稱。
/// </summary>
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

    // ---- 規則 ----

    /// <summary>帳號：4–20 字元，英數與底線，不分大小寫（一律存小寫）。</summary>
    public static string? NormalizeUsername(string? username)
    {
        if (username == null) return null;
        var u = username.Trim().ToLowerInvariant();
        if (u.Length < 4 || u.Length > 20) return null;
        foreach (char c in u)
            if (!(c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c == '_')) return null;
        return u;
    }

    /// <summary>密碼：8–64 字元。</summary>
    public static bool ValidPassword(string? password) => password != null && password.Length >= 8 && password.Length <= 64;

    /// <summary>暱稱：去頭尾空白後 2–12 字，不可含控制字元。</summary>
    public static string? NormalizeNickname(string? nickname)
    {
        if (nickname == null) return null;
        var n = nickname.Trim();
        if (n.Length < 2 || n.Length > 12) return null;
        foreach (char c in n)
            if (char.IsControl(c)) return null;
        return n;
    }

    /// <summary>遊客金鑰：客戶端產生的隨機字串，至少 16 字元。</summary>
    public static bool ValidGuestKey(string? key) => key != null && key.Length >= 16 && key.Length <= 128;

    // ---- 登入 ----

    /// <summary>遊客登入：這把金鑰第一次出現就建立新帳號。</summary>
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

    /// <summary>註冊帳號密碼（新帳號）。帳號已被使用回傳 null。</summary>
    public Session? Register(string username, string password)
    {
        using var conn = Open();
        if (Scalar(conn, "SELECT account_id FROM accounts WHERE username = $u", ("$u", username)) != null) return null;
        string id = NewAccountId();
        Exec(conn, "INSERT INTO accounts (account_id, username, password_hash, nickname, created_at) VALUES ($id, $u, $p, $n, $at)",
            ("$id", id), ("$u", username), ("$p", HashPassword(password)), ("$n", DefaultNickname()), ("$at", Now));
        return IssueSession(conn, id);
    }

    /// <summary>帳號密碼登入；帳號不存在或密碼錯誤都回傳 null（不透露是哪一個錯）。</summary>
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
            // 帳號不存在時也做一次雜湊，回應時間不洩漏帳號是否存在。
            VerifyPassword(password, DummyHash);
            return null;
        }
        return VerifyPassword(password, hash) ? IssueSession(conn, id) : null;
    }

    /// <summary>把帳號密碼綁到既有帳號（遊客升級）。已綁過回傳 "already_bound"，帳號被用走回傳 "username_taken"。</summary>
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
        catch (SqliteException e) when (e.SqliteErrorCode == 19) // UNIQUE：同時有人搶註
        {
            return "username_taken";
        }
        return null;
    }

    /// <summary>以 token 找帳號；過期或不存在回傳 null。</summary>
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

    /// <summary>暱稱（排行榜等公開場合用）；沒有這個帳號回傳 null。</summary>
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

    // ---- 內部 ----

    private Session IssueSession(SqliteConnection conn, string accountId)
    {
        long now = Now;
        // 順手清掉這個帳號已過期的 session，避免資料表無限長大。
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
