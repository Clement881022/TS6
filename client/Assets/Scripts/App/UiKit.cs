#nullable enable
using System;
using SanGuo.Core;
using SanGuo.Core.Meta;
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
            var b = new Button(() => { AudioManager.PlaySfx(Sfx.Click); onClick(); }) { text = text };
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
            var b = new Button(() => { AudioManager.PlaySfx(Sfx.Click); onClick(); }) { text = text };
            b.AddToClassList("tab");
            if (on) b.AddToClassList("tab-on");
            return b;
        }

        /// <summary>在按鈕 / 分頁右上角加紅點（有可領取 / 可操作的項目）。</summary>
        public static T RedDot<T>(this T el, bool show) where T : VisualElement
        {
            if (show) el.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("reddot"));
            return el;
        }

        /// <summary>費用：圖示 + 需要數量，後面接暗色的「/持有量」（have &lt; 0 不顯示）；不足時整組變紅。</summary>
        public static VisualElement Cost(string icon, int need, int have = -1)
        {
            var chip = new VisualElement { pickingMode = PickingMode.Ignore };
            chip.AddToClassList("cost-chip");
            if (have >= 0 && have < need) chip.AddToClassList("cost-chip-bad");
            chip.Add(ItemTile(icon));
            chip.Add(new Label(need.ToString("N0")) { pickingMode = PickingMode.Ignore }.WithClass("cost-chip-text"));
            if (have >= 0) chip.Add(new Label("/" + have.ToString("N0")) { pickingMode = PickingMode.Ignore }.WithClass("cost-chip-have"));
            return chip;
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

        /// <summary>武將圓形頭像：有美術（HeroArt）就顯示頭像，否則用姓氏代替；依稀有度上框色。</summary>
        public static VisualElement Avatar(string name, Rarity rarity, bool large = false, string? heroId = null)
        {
            var a = new VisualElement { pickingMode = PickingMode.Ignore };
            a.AddToClassList("avatar");
            a.AddToClassList("avatar-" + RarityClass(rarity));
            if (large) a.AddToClassList("avatar-lg");
            var face = heroId != null ? HeroArt.Face(heroId) : null;
            if (face != null)
            {
                a.style.backgroundImage = new StyleBackground(face);
                a.AddToClassList("avatar-art");
            }
            else a.Add(new Label(name.Length > 0 ? name.Substring(0, 1) : "?") { pickingMode = PickingMode.Ignore }.WithClass("avatar-text"));
            return a;
        }

        /// <summary>方形武將卡（頭像在上、名字在下），依稀有度上框色；sub 為第二行小字。空位用 empty = true。</summary>
        public static Button HeroCard(string name, Rarity rarity, string? heroId, string sub, Action onClick,
            bool selected = false, bool empty = false, string cls = "hcard")
        {
            var b = new Button(onClick);
            b.AddToClassList(cls);
            if (empty)
            {
                b.AddToClassList(cls + "-empty");
                b.Add(new Label("＋") { pickingMode = PickingMode.Ignore }.WithClass("hcard-plus"));
                b.Add(new Label(sub) { pickingMode = PickingMode.Ignore }.WithClass("hcard-sub"));
                return b;
            }
            b.AddToClassList(cls + "-" + RarityClass(rarity));
            if (selected) b.AddToClassList(cls + "-on");
            b.Add(Avatar(name, rarity, false, heroId));
            b.Add(new Label(name) { pickingMode = PickingMode.Ignore }.WithClass("hcard-name"));
            b.Add(new Label(sub) { pickingMode = PickingMode.Ignore }.WithClass("hcard-sub"));
            return b;
        }

        /// <summary>卡片格線容器（方塊卡片排成多欄）。</summary>
        public static VisualElement CardGrid()
        {
            var g = new VisualElement();
            g.AddToClassList("card-grid");
            return g;
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

        /// <summary>資源膠囊（右上角）：圖示 + 數字。icon 是 UiSkin 的檔名（不含副檔名）。</summary>
        public static VisualElement ResPill(string icon, string text)
        {
            var pill = new VisualElement { pickingMode = PickingMode.Ignore };
            pill.AddToClassList("res-pill");
            var ic = new VisualElement { pickingMode = PickingMode.Ignore };
            ic.AddToClassList("res-pill-icon");
            var tex = SkinTex(icon);
            if (tex != null) ic.style.backgroundImage = new StyleBackground(tex);
            pill.Add(ic);
            pill.Add(new Label(text) { pickingMode = PickingMode.Ignore }.WithClass("res-pill-text"));
            return pill;
        }

        private static readonly System.Collections.Generic.Dictionary<string, UnityEngine.Texture2D?> SkinCache =
            new System.Collections.Generic.Dictionary<string, UnityEngine.Texture2D?>();

        /// <summary>Resources/UiSkin 下的貼圖（有快取）。</summary>
        public static UnityEngine.Texture2D? SkinTex(string name)
        {
            if (!SkinCache.TryGetValue(name, out var t))
            {
                t = UnityEngine.Resources.Load<UnityEngine.Texture2D>("UiSkin/" + name);
                SkinCache[name] = t;
            }
            return t;
        }

        /// <summary>物品圖示方塊（獎勵、素材）：圖 + 右下角數量。</summary>
        public static VisualElement ItemTile(string icon, string count = "", string cls = "")
        {
            var t = new VisualElement { pickingMode = PickingMode.Ignore };
            t.AddToClassList("item-tile");
            if (cls.Length > 0) t.AddToClassList(cls);
            var tex = SkinTex(icon);
            if (tex != null) t.style.backgroundImage = new StyleBackground(tex);
            if (count.Length > 0) t.Add(new Label(count) { pickingMode = PickingMode.Ignore }.WithClass("item-count"));
            return t;
        }

        /// <summary>把獎勵轉成一排物品圖示（元寶、金幣、體力、素材、武將）。</summary>
        public static VisualElement RewardTiles(Reward r)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("reward-tiles");
            if (r.Yuanbao > 0) row.Add(ItemTile("item_yuanbao", r.Yuanbao.ToString()));
            if (r.Gold > 0) row.Add(ItemTile("item_gold", r.Gold.ToString()));
            if (r.Stamina > 0) row.Add(ItemTile("item_stamina", r.Stamina.ToString()));
            foreach (var m in r.Materials)
            {
                string icon = m.Key == HeroGrowth.ExpBook ? "item_expbook" : m.Key == HeroGrowth.CardMaterial ? "item_cardmat" : "item_shard";
                row.Add(ItemTile(icon, m.Value.ToString()));
            }
            foreach (var h in r.Heroes) row.Add(ItemTile("item_chest", "1"));
            return row;
        }

        /// <summary>星星列：on 顆亮、其餘暗；total 為總顆數。</summary>
        public static VisualElement StarsRow(int on, int total, string cls = "")
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("stars-row");
            if (cls.Length > 0) row.AddToClassList(cls);
            for (int i = 0; i < total; i++)
            {
                var star = new VisualElement { pickingMode = PickingMode.Ignore };
                star.AddToClassList("star");
                var tex = SkinTex(i < on ? "star_on" : "star_off");
                if (tex != null) star.style.backgroundImage = new StyleBackground(tex);
                row.Add(star);
            }
            return row;
        }

        /// <summary>
        /// 直立武將卡（參考 TS6Client）：稀有度色框、上方星數、頭像、右下等級、右上職業圖示。
        /// level &lt;= 0 不顯示等級；stars &lt; 0 不顯示星數；empty 為空格子。
        /// </summary>
        public static Button HeroTile(HeroDef def, int level, int stars, Action onClick, bool selected = false, string cls = "htile", string extraClass = "")
        {
            var b = new Button(onClick);
            b.AddToClassList(cls);
            if (extraClass.Length > 0) b.AddToClassList(extraClass);
            b.AddToClassList(cls + "-" + RarityClass(def.Rarity));
            if (selected) b.AddToClassList(cls + "-on");
            var art = new VisualElement { pickingMode = PickingMode.Ignore };
            art.AddToClassList(cls + "-art");
            var face = HeroArt.Face(def.Id);
            if (face != null) art.style.backgroundImage = new StyleBackground(face);
            else art.Add(new Label(def.Name.Substring(0, 1)) { pickingMode = PickingMode.Ignore }.WithClass("avatar-text"));
            b.Add(art);
            if (stars >= 0)
            {
                var top = StarsRow(stars, HeroGrowth.MaxStars, "htile-stars");
                b.Add(top);
            }
            var role = new VisualElement { pickingMode = PickingMode.Ignore };
            role.AddToClassList("htile-role");
            var rt = UiIcons.Get(UiIcons.RoleIcon(def.Role));
            if (rt != null) role.style.backgroundImage = new StyleBackground(rt);
            b.Add(role);
            b.Add(new Label(def.Name) { pickingMode = PickingMode.Ignore }.WithClass("htile-name"));
            if (level > 0) b.Add(new Label("Lv." + level) { pickingMode = PickingMode.Ignore }.WithClass("htile-level")); // 要在名牌之後加，才會畫在名牌上面
            return b;
        }

        /// <summary>
        /// 建出頁面外框並回傳內容區：標題列（返回鍵 + 金字標題 + 右上資源 + 關閉），內容區是一塊填滿的容器，頁面自己排版。
        /// back = 返回鍵去的頁面；關閉鍵一律回主城。
        /// </summary>
        public static VisualElement Frame(VisualElement host, string title, Page back)
        {
            var bar = new VisualElement();
            bar.AddToClassList("hdr");

            var backBtn = new Button(() => Nav.Go(back));
            backBtn.AddToClassList("hdr-back");
            backBtn.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("hdr-back-icon"));
            bar.Add(backBtn);
            bar.Add(new Label(title) { pickingMode = PickingMode.Ignore }.WithClass("hdr-title"));
            bar.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("grow"));

            var v = GameSession.View;
            var res = new VisualElement { pickingMode = PickingMode.Ignore };
            res.AddToClassList("hdr-res");
            res.Add(ResPill("item_stamina", $"{v.Stamina}/{v.StaminaCap}"));
            res.Add(ResPill("item_gold", v.Gold.ToString("N0")));
            res.Add(ResPill("item_yuanbao", v.Yuanbao.ToString("N0")));
            bar.Add(res);

            var close = new Button(() => Nav.Go(Page.Home));
            close.AddToClassList("hdr-close");
            bar.Add(close);
            host.Add(bar);

            var body = new VisualElement();
            body.AddToClassList("page-content");
            host.Add(body);
            return body;
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
