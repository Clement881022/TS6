#nullable enable
using System;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public static class UiKit
    {
        private static UnityEngine.TextCore.Text.FontAsset? DisplayFont;
        private static readonly string[] DisplayClasses = { "strategy-brand-title", "strategy-expedition-title", "hdr-title", "home-fn-label", "home-chapter-title",
            "hero-namebar-name", "strategy-summary-name", "bpanel-title", "popup-title", "dun-name", "shop-name", "btn", "tab", "header-title", "bl-end", "bl-d-name", "sts-card-name", "home-campaign-title", "home-primary-title", "account-heading", "stage-title", "recruit-title", "recruit-pool-name", "quest-toolbar-title", "challenge-tab", "ui-help-title" };

        public static void ApplyDisplayFont(VisualElement element)
        {
            if (!(element is TextElement text)) return;
            bool display = false;
            foreach (var cls in DisplayClasses) if (text.ClassListContains(cls)) { display = true; break; }
            if (!display) return;
            if (DisplayFont == null)
            {
                var font = UnityEngine.Resources.Load<UnityEngine.Font>("Fonts/ChibiDisplay");
                if (font == null) return;
                DisplayFont = UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(font, 80, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048,
                    UnityEngine.TextCore.Text.AtlasPopulationMode.Dynamic, true);
            }
            text.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(DisplayFont));
        }

        public static T WithClass<T>(this T el, string cls) where T : VisualElement
        {
            foreach (var c in cls.Split(' ')) if (c.Length > 0) el.AddToClassList(c);
            ApplyDisplayFont(el);
            return el;
        }

        public static Label Text(string text, string cls = "txt")
        {
            var l = new Label(text);
            foreach (var c in cls.Split(' ')) if (c.Length > 0) l.AddToClassList(c);
            ApplyDisplayFont(l);
            return l;
        }

        public static Button Btn(string text, Action onClick, bool primary = false, bool on = false)
        {
            var b = new Button(() => { AudioManager.PlaySfx(Sfx.Click); onClick(); }) { text = text };
            b.AddToClassList("btn");
            if (primary) b.AddToClassList("btn-primary");
            if (on) b.AddToClassList("btn-on");
            ApplyDisplayFont(b);
            return b;
        }

        public static Button DoneBtn(string text)
        {
            var b = new Button { text = text, pickingMode = PickingMode.Ignore };
            b.AddToClassList("btn");
            b.AddToClassList("btn-done");
            ApplyDisplayFont(b);
            return b;
        }

        public static Button Tab(string text, Action onClick, bool on)
        {
            var b = new Button(() => { AudioManager.PlaySfx(Sfx.Click); onClick(); }) { text = text };
            b.AddToClassList("tab");
            if (on) b.AddToClassList("tab-on");
            ApplyDisplayFont(b);
            return b;
        }

        public static T RedDot<T>(this T el, bool show) where T : VisualElement
        {
            if (show) el.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("reddot"));
            return el;
        }

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

        public static VisualElement Panel(string extraClass = "")
        {
            var p = new VisualElement();
            p.AddToClassList("panel");
            if (extraClass.Length > 0) p.AddToClassList(extraClass);
            return p;
        }

        public static VisualElement Section(string text)
        {
            var s = new VisualElement();
            s.AddToClassList("section");
            s.Add(new VisualElement().WithClass("section-line"));
            s.Add(new Label(text).WithClass("section-text"));
            s.Add(new VisualElement().WithClass("section-line"));
            return s;
        }

        public static Label Hint(string text, bool warn = false)
        {
            var l = new Label(text);
            l.AddToClassList("hint");
            if (warn) l.AddToClassList("hint-warn");
            return l;
        }

        public static VisualElement Bar(float percent, string cls = "")
        {
            var bg = new VisualElement();
            bg.AddToClassList("bar-bg");
            var fill = new VisualElement();
            fill.AddToClassList("bar-fill");
            foreach (var c in cls.Split(' ')) if (c.Length > 0) { bg.AddToClassList(c); fill.AddToClassList(c); }
            fill.style.width = Length.Percent(Math.Max(0f, Math.Min(100f, percent)));
            bg.Add(fill);
            return bg;
        }

        public static Label Badge(string text, string cls = "")
        {
            var l = new Label(text);
            l.AddToClassList("badge");
            if (cls.Length > 0) l.AddToClassList(cls);
            return l;
        }

        public static string RarityClass(Rarity r) => r.ToString().ToLowerInvariant();

        public static Label RarityBadge(Rarity r) => Badge(r.ToString(), "badge-" + RarityClass(r));

        public static VisualElement Avatar(string name, Rarity rarity, bool large = false, string? heroId = null)
        {
            var a = heroId != null ? PortraitArt.Create(heroId, "avatar") : new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("avatar");
            a.AddToClassList("avatar-" + RarityClass(rarity));
            if (large) a.AddToClassList("avatar-lg");
            var face = heroId != null ? HeroArt.Face(heroId) : null;
            if (face != null)
            {
                a.AddToClassList("avatar-art");
            }
            else a.Add(new Label(name.Length > 0 ? name.Substring(0, 1) : "?") { pickingMode = PickingMode.Ignore }.WithClass("avatar-text"));
            return a;
        }

        public static VisualElement Stat(string label, string value)
        {
            var s = new VisualElement();
            s.AddToClassList("stat");
            s.Add(Text(label, "stat-label"));
            s.Add(Text(value, "stat-value"));
            return s;
        }

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

        public static UnityEngine.Texture2D? SkinTex(string name)
        {
            if (!SkinCache.TryGetValue(name, out var t))
            {
                t = UnityEngine.Resources.Load<UnityEngine.Texture2D>("ChibiSkin/" + name)
                    ?? UnityEngine.Resources.Load<UnityEngine.Texture2D>("UiSkin/" + name);
                SkinCache[name] = t;
            }
            return t;
        }

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

        public static VisualElement RewardTiles(Reward r)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("reward-tiles");
            if (r.Yuanbao > 0) row.Add(ItemTile("item_yuanbao", r.Yuanbao.ToString()));
            if (r.Gold > 0) row.Add(ItemTile("item_gold", r.Gold.ToString()));
            if (r.Stamina > 0) row.Add(ItemTile("item_stamina", r.Stamina.ToString()));
            foreach (var m in r.Materials)
            {
                string icon = m.Key == HeroGrowth.HeroExp ? "item_expbook" : m.Key.StartsWith("eq:") ? "item_chest" : "item_shard";
                row.Add(ItemTile(icon, m.Value.ToString()));
            }
            foreach (var h in r.Heroes) row.Add(ItemTile("item_chest", "1"));
            return row;
        }

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

        public static Button HeroTile(HeroDef def, int level, int stars, Action onClick, bool selected = false, string cls = "htile", string extraClass = "")
        {
            var b = new Button(onClick);
            b.AddToClassList(cls);
            if (extraClass.Length > 0) b.AddToClassList(extraClass);
            b.AddToClassList(cls + "-" + RarityClass(def.Rarity));
            if (selected) b.AddToClassList(cls + "-on");
            var art = PortraitArt.Create(def.Id, cls + "-art");
            b.Add(art);
            if (stars >= 0)
            {
                var top = StarsRow(stars, HeroGrowth.MaxStars, "htile-stars");
                b.Add(top);
            }
            b.Add(new Label(def.Rarity.ToString()) { pickingMode = PickingMode.Ignore }.WithClass("htile-rarity").WithClass("htile-rarity-" + RarityClass(def.Rarity)));
            var role = new VisualElement { pickingMode = PickingMode.Ignore };
            role.AddToClassList("htile-role");
            var rt = UiIcons.Get(UiIcons.RoleIcon(def.Role));
            if (rt != null) role.style.backgroundImage = new StyleBackground(rt);
            b.Add(role);
            b.Add(new Label(def.Name) { pickingMode = PickingMode.Ignore }.WithClass("htile-name"));
            if (level > 0) b.Add(new Label("Lv." + level) { pickingMode = PickingMode.Ignore }.WithClass("htile-level"));
            return b;
        }

        public static VisualElement Frame(VisualElement host, string title, Page back, Page page = Page.Home)
        {
            var bar = new VisualElement();
            bar.AddToClassList("hdr");

            var backBtn = new Button(() => Nav.Go(back));
            backBtn.text = "‹";
            backBtn.AddToClassList("hdr-back");
            bar.Add(backBtn);
            bar.Add(new Label(title) { pickingMode = PickingMode.Ignore }.WithClass("hdr-title"));
            bar.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("grow"));

            var v = GameSession.View;
            var res = new VisualElement { pickingMode = PickingMode.Ignore };
            res.AddToClassList("hdr-res");
            res.Add(ResPill("item_stamina", $"{v.Stamina}/{v.StaminaCap}"));
            res.Add(ResPill("item_gold", v.Gold.ToString("N0")));
            res.Add(ResPill("item_yuanbao", v.Yuanbao.ToString("N0")));
            if (page == Page.Shop) res.Add(ResPill("item_shard", v.Material(HeroGrowth.Soul).ToString("N0")).WithClass("shop-soul-resource"));
            bar.Add(res);

            if (page != Page.Login) bar.Add(UiHelp.Button(host, page));
            var close = new Button(() => Nav.Go(Page.Home));
            close.AddToClassList("hdr-close");
            close.text = "×";
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
