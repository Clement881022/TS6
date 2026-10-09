#nullable enable
using System;
using System.Threading.Tasks;

namespace SanGuo.Client
{
    /// <summary>目前登入的帳號（顯示用，不含任何憑證）。</summary>
    public sealed class AccountInfo
    {
        public string Nickname = "";
        /// <summary>綁定的帳號名稱；遊客為 null。</summary>
        public string? Username;
        public bool Bound => Username != null;
    }

    public sealed class AccountResult : BackendResult
    {
        public AccountInfo? Account;
    }

    /// <summary>
    /// 帳號功能（只有伺服器後端有；單機版沒有帳號）。
    /// 登入成功後 token 存在裝置上，下次開遊戲自動沿用；token 失效時觸發 <see cref="SessionLost"/>，畫面回到登入頁。
    /// </summary>
    public interface IAccountBackend
    {
        /// <summary>裝置上有登入 token（不保證伺服器仍接受，失效時會觸發 <see cref="SessionLost"/>）。</summary>
        bool HasSession { get; }
        /// <summary>伺服器回應 401：token 已過期或被登出。</summary>
        event Action? SessionLost;

        /// <summary>遊客進入：用這台裝置上保存的遊客金鑰（第一次自動產生）。</summary>
        Task<AccountResult> LoginGuest();
        Task<AccountResult> LoginPassword(string username, string password);
        Task<AccountResult> Register(string username, string password);
        /// <summary>把帳號密碼綁到目前的遊客帳號。</summary>
        Task<AccountResult> Bind(string username, string password);
        Task<AccountResult> GetAccount();
        Task<AccountResult> SetNickname(string nickname);
        Task Logout();
    }
}
