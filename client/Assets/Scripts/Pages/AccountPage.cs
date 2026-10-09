#nullable enable
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// 帳號頁：暱稱、帳號類型、遊客綁定帳號密碼、登出。單機版沒有帳號，只顯示說明。
    /// 畫面為程式佔位，正式美術待美術組提供。
    /// </summary>
    public sealed class AccountPage : PageBase
    {
        private Label? _status;

        protected override Page Id => Page.Account;
        protected override string Title => "帳號";

        protected override async void OnReady()
        {
            var accounts = GameSession.Accounts;
            if (accounts == null) return;
            var r = await accounts.GetAccount();
            if (this == null || !r.Ok) return;
            GameSession.Account = r.Account;
            Rebuild();
        }

        protected override void BuildBody(VisualElement body)
        {
            // 內容可能比畫面高（遊客多了綁定表單）：放進滾動區，面板水平置中。
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.alignSelf = Align.Stretch;
            scroll.contentContainer.style.alignItems = Align.Center;
            scroll.contentContainer.style.paddingBottom = 32;
            body.Add(scroll);
            var panel = AccountUi.Panel(720);
            panel.style.marginTop = 24;
            scroll.Add(panel);

            var accounts = GameSession.Accounts;
            if (accounts == null)
            {
                panel.Add(AccountUi.Heading("單機模式", 40));
                panel.Add(AccountUi.Note("目前是單機存檔，沒有帳號。連線伺服器時才需要登入。"));
                return;
            }

            var account = GameSession.Account;
            panel.Add(AccountUi.Heading(GameSession.DisplayName, 44));
            panel.Add(AccountUi.Note(account == null ? "讀取帳號資料中…"
                : account.Bound ? $"已綁定帳號：{account.Username}" : "遊客帳號（尚未綁定）"));

            panel.Add(AccountUi.Divider());
            var nickname = AccountUi.Field("暱稱（2–12 字，顯示在排行榜）");
            nickname.value = account?.Nickname ?? "";
            panel.Add(nickname);
            panel.Add(AccountUi.Wide(UiKit.Btn("修改暱稱", () => _ = Run(a => a.SetNickname(nickname.value), "暱稱已更新"))));

            if (account != null && !account.Bound)
            {
                panel.Add(AccountUi.Divider());
                panel.Add(AccountUi.Note("綁定帳號密碼後，換裝置或重裝遊戲都能用帳號登入找回進度。"));
                var username = AccountUi.Field("帳號（4–20 字，英數或底線）");
                var password = AccountUi.Field("密碼（8–64 字）", password: true);
                var confirm = AccountUi.Field("再輸入一次密碼", password: true);
                panel.Add(username);
                panel.Add(password);
                panel.Add(confirm);
                panel.Add(AccountUi.Wide(UiKit.Btn("綁定帳號", () =>
                {
                    if (password.value != confirm.value) { SetStatus(UiText.ExplainBackend("password_mismatch")); return; }
                    _ = Run(a => a.Bind(username.value, password.value), "綁定完成");
                }, primary: true)));
            }

            panel.Add(AccountUi.Divider());
            if (account != null && !account.Bound)
                panel.Add(AccountUi.Note("注意：遊客帳號登出後，只能在這台裝置以「遊客進入」回來。"));
            panel.Add(AccountUi.Wide(UiKit.Btn("登出", () => _ = LogoutAsync())));

            _status = AccountUi.Note("");
            _status.style.color = new Color(1f, 0.55f, 0.45f);
            panel.Add(_status);
        }

        private void SetStatus(string text)
        {
            if (_status != null) _status.text = text;
        }

        private async Task Run(Func<IAccountBackend, Task<AccountResult>> action, string success)
        {
            var accounts = GameSession.Accounts;
            if (accounts == null || Busy) return;
            try
            {
                var r = await action(accounts);
                if (this == null) return;
                if (!r.Ok) { SetStatus(UiText.ExplainBackend(r.Code)); return; }
                GameSession.Account = r.Account;
                Rebuild();
                Toast(success);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetStatus(UiText.ExplainBackend("network"));
            }
        }

        private async Task LogoutAsync()
        {
            var accounts = GameSession.Accounts;
            if (accounts == null) return;
            try { await accounts.Logout(); }
            catch (Exception e) { Debug.LogException(e); }
            GameSession.Account = null;
            Nav.Go(Page.Login);
        }
    }
}
