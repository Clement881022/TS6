#nullable enable
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public sealed class LoginPage : PageBase
    {
        private TextField? _username;
        private TextField? _password;
        private Label? _status;

        protected override Page Id => Page.Login;
        protected override string Title => "登入";
        protected override bool UseFrame => false;
        protected override bool ShowNav => false;
        protected override bool NeedsProfile => false;

        protected override void BuildBody(VisualElement root)
        {
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;

            var panel = AccountUi.Panel(560);
            panel.Add(AccountUi.Heading("三國將星傳", 64));

            var guest = UiKit.Btn("遊客進入", () => _ = Run(a => a.LoginGuest()), primary: true);
            AccountUi.Wide(guest);
            panel.Add(guest);
            panel.Add(AccountUi.Note("遊客帳號只存在這台裝置，刪除遊戲後無法找回；進入後可在「帳號」綁定帳號密碼。"));

            panel.Add(AccountUi.Divider());
            _username = AccountUi.Field("帳號（4–20 字，英數或底線）");
            _password = AccountUi.Field("密碼（8–64 字）", password: true);
            panel.Add(_username);
            panel.Add(_password);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginTop = 12;
            var login = UiKit.Btn("登入", () => _ = Run(a => a.LoginPassword(_username.value, _password.value)));
            var register = UiKit.Btn("註冊新帳號", () => _ = Run(a => a.Register(_username.value, _password.value)));
            login.style.flexGrow = 1;
            register.style.flexGrow = 1;
            login.style.marginRight = 8;
            row.Add(login);
            row.Add(register);
            panel.Add(row);

            _status = AccountUi.Note("");
            _status.style.color = new Color(1f, 0.55f, 0.45f);
            panel.Add(_status);
            root.Add(panel);
        }

        private async Task Run(Func<IAccountBackend, Task<AccountResult>> action)
        {
            var accounts = GameSession.Accounts;
            if (accounts == null) { Nav.Go(Page.Home); return; }
            if (_status != null) _status.text = "連線中…";
            try
            {
                var r = await action(accounts);
                if (this == null) return;
                if (!r.Ok)
                {
                    if (_status != null) _status.text = UiText.ExplainBackend(r.Code);
                    return;
                }
                GameSession.Account = r.Account;
                Nav.Go(Page.Home);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (_status != null) _status.text = UiText.ExplainBackend("network");
            }
        }
    }

    internal static class AccountUi
    {
        public static VisualElement Panel(float width)
        {
            var p = new VisualElement().WithClass("account-panel");
            p.style.width = width;
            p.style.maxWidth = Length.Percent(92);
            p.style.paddingLeft = p.style.paddingRight = 40;
            p.style.paddingTop = p.style.paddingBottom = 32;
            return p;
        }

        public static Label Heading(string text, int size)
        {
            var l = UiKit.Text(text, "account-heading");
            l.style.fontSize = size;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.marginBottom = 4;
            return l;
        }

        public static Label Note(string text)
        {
            var l = UiKit.Text(text, "account-note");
            l.style.fontSize = 22;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.marginTop = l.style.marginBottom = 8;
            return l;
        }

        public static VisualElement Divider()
        {
            var d = new VisualElement();
            d.style.height = 2;
            d.style.marginTop = d.style.marginBottom = 16;
            d.style.backgroundColor = new Color(0.78f, 0.62f, 0.33f, 0.4f);
            return d;
        }

        public static T Wide<T>(T el) where T : VisualElement
        {
            el.style.marginTop = 16;
            el.style.alignSelf = Align.Stretch;
            return el;
        }

        public static TextField Field(string label, bool password = false)
        {
            var f = new TextField(label) { isPasswordField = password, maxLength = 64 };
            f.style.flexDirection = FlexDirection.Column;
            f.style.marginTop = 10;
            f.labelElement.style.fontSize = 22;
            f.labelElement.style.marginBottom = 4;
            var input = f.Q(className: "unity-base-text-field__input");
            if (input != null)
            {
                input.style.minHeight = 56;
                input.style.fontSize = 26;
                input.style.unityTextAlign = TextAnchor.MiddleLeft;
                input.style.justifyContent = Justify.Center;
                foreach (var text in input.Query<TextElement>().ToList()) text.style.unityTextAlign = TextAnchor.MiddleLeft;
                input.style.paddingLeft = input.style.paddingRight = 12;
            }
            return f;
        }
    }
}
