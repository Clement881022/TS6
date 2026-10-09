#nullable enable
using System;
using System.Threading.Tasks;

namespace SanGuo.Client
{
    public sealed class AccountInfo
    {
        public string Nickname = "";
        public string? Username;
        public bool Bound => Username != null;
    }

    public sealed class AccountResult : BackendResult
    {
        public AccountInfo? Account;
    }

    public interface IAccountBackend
    {
        bool HasSession { get; }
        event Action? SessionLost;

        Task<AccountResult> LoginGuest();
        Task<AccountResult> LoginPassword(string username, string password);
        Task<AccountResult> Register(string username, string password);
        Task<AccountResult> Bind(string username, string password);
        Task<AccountResult> GetAccount();
        Task<AccountResult> SetNickname(string nickname);
        Task Logout();
    }
}
