#nullable enable
using System;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>各頁共用的 UI 元件：文字、按鈕、頁面外框（頂欄 + 內容 + 底部導覽）、提示。樣式在 Resources/UI/Pages.uss。</summary>
    public static class UiKit
    {
        private static readonly (Page page, string label)[] Tabs =
        {
            (Page.Home, "主城"), (Page.Map, "征戰"), (Page.Heroes, "武將"), (Page.Gacha, "招募"),
            (Page.Dungeons, "副本"), (Page.Quests, "任務"), (Page.Shop, "商店"),
        };

        /// <summary>cls：txt（內文）、txt-dim（次要）、txt-sub（小標）、txt-warn（警示）。</summary>
        public static Label Text(string text, string cls = "txt")
        {
            var l = new Label(text);
            l.AddToClassList(cls);
            return l;
        }

        public static Button Btn(string text, Action onClick, bool primary = false, bool on = false)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("btn");
            if (primary) b.AddToClassList("btn-primary");
            if (on) b.AddToClassList("btn-on");
            return b;
        }

        public static VisualElement Row(string extraClass = "")
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            if (extraClass.Length > 0) row.AddToClassList(extraClass);
            return row;
        }

        /// <summary>圓角面板（卡片底）。</summary>
        public static VisualElement Panel(string extraClass = "")
        {
            var p = new VisualElement();
            p.AddToClassList("panel");
            if (extraClass.Length > 0) p.AddToClassList(extraClass);
            return p;
        }

        public static Label Pill(string text)
        {
            var l = new Label(text);
            l.AddToClassList("pill");
            return l;
        }

        /// <summary>
        /// 建出頁面外框並回傳內容區：頂欄（標題 + 帳號資源）、可捲動內容、底部導覽（nav 為 null 則不顯示）。
        /// </summary>
        public static VisualElement Frame(VisualElement host, string title, Page? current, bool showNav)
        {
            var bar = new VisualElement();
            bar.AddToClassList("topbar");
            bar.Add(Text(title, "topbar-title"));
            var v = GameSession.View;
            var res = new VisualElement();
            res.AddToClassList("topbar-res");
            res.Add(Pill($"Lv.{v.Level}"));
            res.Add(Pill($"體力 {v.Stamina}/{v.StaminaCap}"));
            res.Add(Pill($"金幣 {v.Gold}"));
            res.Add(Pill($"元寶 {v.Yuanbao}"));
            bar.Add(res);
            host.Add(bar);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("page-scroll");
            scroll.contentContainer.AddToClassList("page-body");
            host.Add(scroll);

            if (showNav)
            {
                var nav = new VisualElement();
                nav.AddToClassList("nav");
                foreach (var (page, label) in Tabs)
                {
                    var target = page;
                    var tab = new Button(() => { if (target != current) Nav.Go(target); }) { text = label };
                    tab.AddToClassList("nav-tab");
                    if (page == current) tab.AddToClassList("nav-tab-on");
                    nav.Add(tab);
                }
                host.Add(nav);
            }
            return scroll.contentContainer;
        }

        public static void Toast(VisualElement layer, string message)
        {
            var toast = new Label(message);
            toast.AddToClassList("toast");
            toast.pickingMode = PickingMode.Ignore;
            layer.Add(toast);
            toast.schedule.Execute(() => toast.RemoveFromHierarchy()).StartingIn(1300);
        }
    }
}
