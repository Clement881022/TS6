#nullable enable
using System;
using SanGuo.Core;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>各頁共用的 UI 元件：文字、按鈕、頁面外框（頂欄 + 內容 + 底部導覽）、提示、頭像、進度條等。樣式在 Resources/UI/Pages.uss。</summary>
    public static class UiKit
    {
        private static readonly (Page page, string glyph, string label)[] Tabs =
        {
            (Page.Home, "城", "主城"), (Page.Map, "戰", "征戰"), (Page.Heroes, "將", "武將"), (Page.Gacha, "募", "招募"),
            (Page.Dungeons, "副", "副本"), (Page.Quests, "任", "任務"), (Page.Shop, "商", "商店"),
        };

        public static T WithClass<T>(this T el, string cls) where T : VisualElement
        {
            el.AddToClassList(cls);
            return el;
        }

        /// <summary>cls：可用空格放多個 class。txt（內文）、txt-dim（次要）、txt-sub（小標）、txt-warn（警示）、txt-good、txt-gold。</summary>
        public static Label Text(string text, string cls = "txt")
        {
            var l = new Label(text);
            foreach (var c in cls.Split(' ')) if (c.Length > 0) l.AddToClassList(c);
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

        /// <summary>已完成 / 已領取等不可再按的按鈕（灰階）。</summary>
        public static Button DoneBtn(string text)
        {
            var b = new Button { text = text, pickingMode = PickingMode.Ignore };
            b.AddToClassList("btn");
            b.AddToClassList("btn-done");
            return b;
        }

        /// <summary>分頁籤（卡池、任務類型）。</summary>
        public static Button Tab(string text, Action onClick, bool on)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("tab");
            if (on) b.AddToClassList("tab-on");
            return b;
        }

        public static VisualElement Row(string extraClass = "")
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            if (extraClass.Length > 0) row.AddToClassList(extraClass);
            return row;
        }

        /// <summary>圓角面板（卡片底，頂部金線）。</summary>
        public static VisualElement Panel(string extraClass = "")
        {
            var p = new VisualElement();
            p.AddToClassList("panel");
            if (extraClass.Length > 0) p.AddToClassList(extraClass);
            return p;
        }

        /// <summary>小節標題：文字兩側各一條細線。</summary>
        public static VisualElement Section(string text)
        {
            var s = new VisualElement();
            s.AddToClassList("section");
            s.Add(new VisualElement().WithClass("section-line"));
            s.Add(new Label(text).WithClass("section-text"));
            s.Add(new VisualElement().WithClass("section-line"));
            return s;
        }

        /// <summary>提示條（左側金線）；warn = 橘色警示。</summary>
        public static Label Hint(string text, bool warn = false)
        {
            var l = new Label(text);
            l.AddToClassList("hint");
            if (warn) l.AddToClassList("hint-warn");
            return l;
        }

        /// <summary>進度條；cls 可用 bar-gold / bar-red / bar-slim。</summary>
        public static VisualElement Bar(float percent, string cls = "")
        {
            var bg = new VisualElement();
            bg.AddToClassList("bar-bg");
            var fill = new VisualElement();
            fill.AddToClassList("bar-fill");
            if (cls.Length > 0) { bg.AddToClassList(cls); fill.AddToClassList(cls); }
            fill.style.width = Length.Percent(Math.Max(0f, Math.Min(100f, percent)));
            bg.Add(fill);
            return bg;
        }

        /// <summary>小徽章；cls 例如 badge-ur / badge-new / badge-role。</summary>
        public static Label Badge(string text, string cls = "")
        {
            var l = new Label(text);
            l.AddToClassList("badge");
            if (cls.Length > 0) l.AddToClassList(cls);
            return l;
        }

        public static string RarityClass(Rarity r) => r.ToString().ToLowerInvariant();

        public static Label RarityBadge(Rarity r) => Badge(r.ToString(), "badge-" + RarityClass(r));

        /// <summary>武將圓形頭像（尚無立繪時用姓氏代替），依稀有度上色。</summary>
        public static VisualElement Avatar(string name, Rarity rarity, bool large = false)
        {
            var a = new VisualElement { pickingMode = PickingMode.Ignore };
            a.AddToClassList("avatar");
            a.AddToClassList("avatar-" + RarityClass(rarity));
            if (large) a.AddToClassList("avatar-lg");
            a.Add(new Label(name.Length > 0 ? name.Substring(0, 1) : "?") { pickingMode = PickingMode.Ignore }.WithClass("avatar-text"));
            return a;
        }

        /// <summary>圓點進度（例如卡牌強化 3/5）。</summary>
        public static VisualElement Pips(int on, int total)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("pips");
            for (int i = 0; i < total; i++)
            {
                var pip = new VisualElement();
                pip.AddToClassList("pip");
                if (i < on) pip.AddToClassList("pip-on");
                row.Add(pip);
            }
            return row;
        }

        /// <summary>數值方塊（小標籤 + 大數字）。</summary>
        public static VisualElement Stat(string label, string value)
        {
            var s = new VisualElement();
            s.AddToClassList("stat");
            s.Add(Text(label, "stat-label"));
            s.Add(Text(value, "stat-value"));
            return s;
        }

        /// <summary>資源膠囊：彩色圓點 + 文字。kind：gold / yuanbao / stamina / level。</summary>
        public static VisualElement Pill(string text, string kind = "")
        {
            var pill = new VisualElement();
            pill.AddToClassList("pill");
            if (kind.Length > 0) pill.AddToClassList("pill-" + kind);
            pill.Add(new VisualElement().WithClass("pill-dot"));
            pill.Add(Text(text, "pill-text"));
            return pill;
        }

        /// <summary>
        /// 建出頁面外框並回傳內容區：頂欄（標題 + 帳號資源）、可捲動內容、底部導覽（showNav 為 false 則不顯示）。
        /// </summary>
        public static VisualElement Frame(VisualElement host, string title, Page? current, bool showNav)
        {
            var bar = new VisualElement();
            bar.AddToClassList("topbar");
            var left = new VisualElement();
            left.AddToClassList("topbar-left");
            left.Add(new VisualElement().WithClass("topbar-seal"));
            left.Add(Text(title, "topbar-title"));
            bar.Add(left);

            var v = GameSession.View;
            var res = new VisualElement();
            res.AddToClassList("topbar-res");
            res.Add(Pill($"Lv.{v.Level}", "level"));
            res.Add(Pill($"體力 {v.Stamina}/{v.StaminaCap}", "stamina"));
            res.Add(Pill($"{v.Gold:N0}", "gold"));
            res.Add(Pill($"{v.Yuanbao:N0}", "yuanbao"));
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
                foreach (var (page, _, label) in Tabs)
                {
                    var target = page;
                    var tab = new Button(() => { if (target != current) Nav.Go(target); });
                    tab.AddToClassList("nav-tab");
                    tab.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("nav-icon").WithClass("nav-icon-" + page.ToString().ToLowerInvariant()));
                    tab.Add(new Label(label) { pickingMode = PickingMode.Ignore }.WithClass("nav-label"));
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
            toast.schedule.Execute(() => toast.RemoveFromHierarchy()).StartingIn(1600);
        }
    }
}
