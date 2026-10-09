#nullable enable
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// 武將：左側直立武將卡格，中央 3D 角色與屬性（升級後的數值預覽），右側「升級 / 突破 / 卡牌」功能分頁。
    /// 版型參考 TS6Client 的武將介面。
    /// </summary>
    public sealed class HeroesPage : PageBase
    {
        private enum Tab { Level, Break, Equip }

        private string? _heroId;
        private Tab _tab = Tab.Level;
        private ModelStage? _stage;
        private string? _stageHero;
        private bool _showModel;

        protected override Page Id => Page.Heroes;
        protected override string Title => "武將";

        private void OnDestroy()
        {
            _stage?.Dispose();
            _stage = null;
        }

        /// <summary>截圖 / 除錯用：切到指定分頁（0 升級、1 突破、2 裝備）。</summary>
        public void DebugSetTab(int tab)
        {
            _tab = (Tab)tab;
            Rebuild();
        }

        public void DebugScrollRosterEnd()
        {
            var scroll = Host.Q<ScrollView>();
            if (scroll != null) scroll.verticalScroller.value = scroll.verticalScroller.highValue;
        }

        public void DebugSelectLastOwned()
        {
            _heroId = GameSession.OwnedHeroes().OrderByDescending(d => d.Name.Length).First().Id;
            _tab = Tab.Level;
            Rebuild();
        }

        public void DebugToggleModel()
        {
            _showModel = !_showModel; Rebuild();
        }

        protected override void BuildBody(VisualElement body)
        {
            var v = GameSession.View;
            var owned = GameSession.Roster.Where(d => v.Heroes.ContainsKey(d.Id)).ToList();
            if (owned.Count == 0)
            {
                var empty = new VisualElement();
                empty.AddToClassList("grow");
                empty.style.alignItems = Align.Center;
                empty.style.justifyContent = Justify.Center;
                empty.Add(UiKit.Text("尚未擁有武將，先去招募吧", "txt-sub"));
                empty.Add(UiKit.Btn("前往招募", () => Nav.Go(Page.Gacha), primary: true).WithClass("btn-wide"));
                body.Add(empty);
                return;
            }
            if (_heroId == null || !v.Heroes.ContainsKey(_heroId))
                _heroId = owned.OrderByDescending(d => HeroArt.Full(d.Id) != null).ThenByDescending(d => d.Rarity).First().Id;
            var def = GameSession.DefOf(_heroId)!;
            var hero = v.Heroes[_heroId];

            // ---- 左：武將卡格 ----
            var left = new VisualElement();
            left.AddToClassList("hero-left");
            left.Add(UiKit.Text("麾下武將", "strategy-roster-title"));
            left.Add(UiKit.Text($"已擁有 {owned.Count} 位", "strategy-roster-count"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            scroll.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            scroll.contentContainer.AddToClassList("hero-grid");
            foreach (var d in owned)
            {
                string id = d.Id;
                var st = v.Heroes[id];
                scroll.Add(RosterEntry(d, st, () => { _heroId = id; Rebuild(); }, id == _heroId));
            }
            left.Add(scroll);
            body.Add(left);

            // ---- 中：3D 角色 + 屬性 ----
            body.Add(BuildCenter(def, hero, v));

            // ---- 右：功能分頁 ----
            body.Add(BuildRight(def, hero, v));
        }

        private VisualElement BuildCenter(HeroDef def, HeroState hero, ProfileView v)
        {
            var center = new VisualElement();
            center.AddToClassList("hero-center");
            var full = HeroArt.Bust(def.Id) ?? HeroArt.Full(def.Id) ?? HeroArt.Face(def.Id);
            if (full != null && !_showModel)
            {
                _stage?.Dispose();
                _stage = null;
                _stageHero = null;
                var art = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("strategy-hero-art");
                if (HeroArt.Full(def.Id) == null) art.AddToClassList("strategy-hero-portrait");
                art.style.backgroundImage = new StyleBackground(full);
                center.Add(art);
            }

            if ((full == null || _showModel) && _stageHero != def.Id)
            {
                _stage?.Dispose();
                _stage = ModelStage.Create(def.Id, 768, 900);
                _stageHero = def.Id;
            }
            if ((full == null || _showModel) && _stage != null)
            {
                var model = new Button(() => _stage?.Cheer());
                model.AddToClassList("hero-model");
                model.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_stage.Texture));
                model.style.backgroundColor = new StyleColor(Color.clear);
                model.style.borderTopWidth = model.style.borderBottomWidth = model.style.borderLeftWidth = model.style.borderRightWidth = 0;
                center.Add(model);
            }

            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("hero-namebar");
            bar.Add(UiKit.RarityBadge(def.Rarity));
            bar.Add(new Label(def.Name) { pickingMode = PickingMode.Ignore }.WithClass("hero-namebar-name"));
            bar.Add(UiKit.Badge(CardText.RoleName(def.Role), "badge-role"));
            bar.Add(new Label($"Lv.{hero.Level}") { pickingMode = PickingMode.Ignore }.WithClass("hero-namebar-level"));
            center.Add(bar);

            if (full != null)
            {
                center.Add(UiKit.Btn(_showModel ? "切換立繪" : "切換模型", () => { _showModel = !_showModel; Rebuild(); }).WithClass("strategy-hero-view"));
            }
            center.Add(UiKit.Text("運籌帷幄 · 將星入陣", "strategy-hero-caption"));
            return center;
        }

        private static Button RosterEntry(HeroDef def, HeroState hero, System.Action choose, bool selected)
        {
            var row = new Button(choose).WithClass("strategy-roster-entry");
            if (selected) row.AddToClassList("strategy-roster-selected");
            var face = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("strategy-roster-face");
            var image = HeroArt.Face(def.Id);
            if (image != null) face.style.backgroundImage = new StyleBackground(image);
            else face.Add(UiKit.Text(def.Name.Substring(0, 1), "strategy-roster-name"));
            row.Add(face);
            var text = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("strategy-roster-text");
            text.Add(UiKit.Text(def.Name, "strategy-roster-name"));
            text.Add(UiKit.Text($"{def.Rarity}  ·  Lv.{hero.Level}", "strategy-roster-level"));
            text.Add(UiKit.StarsRow(hero.Stars, HeroGrowth.MaxStars, "strategy-roster-stars"));
            row.Add(text);
            return row;
        }

        private static VisualElement BuildStats(HeroDef def, HeroState hero, ProfileView v)
        {
            var summary = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("strategy-hero-summary");
            var heading = new VisualElement().WithClass("strategy-summary-heading");
            heading.Add(UiKit.RarityBadge(def.Rarity));
            heading.Add(UiKit.Text(def.Name, "strategy-summary-name"));
            heading.Add(UiKit.Text($"Lv.{hero.Level}", "hero-namebar-level"));
            heading.Add(UiKit.Badge(CardText.RoleName(def.Role), "badge-role"));
            if (def.Rarity != Rarity.R) heading.Add(UiKit.Badge(CardText.FocusName(def.Focus), "badge-role"));
            summary.Add(heading);

            // 屬性：目前 → 升級後（只有隨等級成長的四項會顯示預覽）
            var now = HeroGrowth.ScaleStats(def, hero);
            var next = HeroGrowth.ScaleStats(def, new HeroState { HeroId = hero.HeroId, Level = hero.Level + 1, Stars = hero.Stars, Equipment = hero.Equipment });
            bool canLevel = hero.Level < v.Level;
            var stats = new VisualElement { pickingMode = PickingMode.Ignore };
            stats.AddToClassList("hero-stats");
            var colA = new VisualElement { pickingMode = PickingMode.Ignore };
            colA.AddToClassList("hero-stats-col");
            colA.Add(StatLine("生命值", now.Hp.ToString(), canLevel ? next.Hp.ToString() : null));
            colA.Add(StatLine("攻擊", now.Atk.ToString(), canLevel ? next.Atk.ToString() : null));
            colA.Add(StatLine("防禦", now.Def.ToString(), canLevel ? next.Def.ToString() : null));
            colA.Add(StatLine("謀略", now.Int.ToString(), canLevel ? next.Int.ToString() : null));
            var colB = new VisualElement { pickingMode = PickingMode.Ignore };
            colB.AddToClassList("hero-stats-col");
            colB.Add(StatLine("爆擊率", now.Crit + "%", null));
            colB.Add(StatLine("爆擊傷害", now.CritDmg + "%", null));
            colB.Add(StatLine("閃避", now.Dodge + "%", null));
            colB.Add(StatLine("移動力", now.Move.ToString(), null));
            colB.Add(StatLine("攻擊範圍", now.Range.ToString(), null));
            stats.Add(colA);
            stats.Add(colB);
            summary.Add(stats);

            // 品階（突破星級）：星星下方標示數字，沒突破時暗星也看得到。
            var grade = new VisualElement { pickingMode = PickingMode.Ignore };
            grade.AddToClassList("hero-grade");
            grade.Add(UiKit.StarsRow(hero.Stars, HeroGrowth.MaxStars, "stars-lg"));
            grade.Add(new Label($"突破 {hero.Stars}/{HeroGrowth.MaxStars}") { pickingMode = PickingMode.Ignore }.WithClass("hero-grade-text"));
            summary.Add(grade);
            return summary;
        }

        private static VisualElement StatLine(string name, string now, string? next)
        {
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("stat-line");
            line.Add(new Label(name) { pickingMode = PickingMode.Ignore }.WithClass("stat-line-name"));
            var right = new VisualElement { pickingMode = PickingMode.Ignore };
            right.style.flexDirection = FlexDirection.Row;
            right.Add(new Label(now) { pickingMode = PickingMode.Ignore }.WithClass("stat-line-val"));
            if (next != null)
            {
                var arrow = new Label("  ▶  ") { pickingMode = PickingMode.Ignore };
                arrow.AddToClassList("stat-line-name");
                arrow.style.color = new Color(0.16f, 0.47f, 0.31f);
                right.Add(arrow);
                var n = new Label(next) { pickingMode = PickingMode.Ignore };
                n.AddToClassList("stat-line-val");
                n.style.color = new Color(0.16f, 0.47f, 0.31f);
                right.Add(n);
            }
            line.Add(right);
            return line;
        }

        private VisualElement BuildRight(HeroDef def, HeroState hero, ProfileView v)
        {
            var right = new VisualElement();
            right.AddToClassList("hero-right");
            right.Add(BuildStats(def, hero, v));

            var seg = new VisualElement();
            seg.AddToClassList("seg");
            seg.Add(SegTab("升級", Tab.Level));
            seg.Add(SegTab("突破", Tab.Break));
            seg.Add(SegTab("裝備", Tab.Equip));
            right.Add(seg);

            // 面板分上下兩塊：內容（撐滿）與底部主要操作（貼底），三個分頁的按鈕位置一致。
            var panel = new VisualElement();
            panel.AddToClassList("bpanel");
            panel.AddToClassList("side-panel");
            panel.AddToClassList("side-panel-fill");
            var content = new VisualElement();
            content.AddToClassList("side-body");
            var footer = new VisualElement();
            footer.AddToClassList("side-footer");
            switch (_tab)
            {
                case Tab.Level: BuildLevel(content, footer, def, hero, v); break;
                case Tab.Break: BuildBreak(content, footer, def, hero, v); break;
                default: BuildEquip(content, def, hero, v); break;
            }
            panel.Add(content);
            if (footer.childCount > 0) panel.Add(footer);
            right.Add(panel);
            return right;
        }

        private Button SegTab(string text, Tab tab) =>
            UiKit.Tab(text, () => { _tab = tab; Rebuild(); }, _tab == tab).WithClass("seg-tab");

        private void BuildLevel(VisualElement content, VisualElement footer, HeroDef def, HeroState hero, ProfileView v)
        {
            int gold = HeroGrowth.LevelUpGold(hero.Level), exp = HeroGrowth.LevelUpExp(hero.Level);
            bool atCap = hero.Level >= v.Level;
            content.Add(UiKit.Text($"Lv.{hero.Level}  /  帳號等級上限 {v.Level}", "side-caption"));
            content.Add(UiKit.Text("升級消耗", "txt-sub"));
            var cost = new VisualElement();
            cost.AddToClassList("cost-line");
            cost.Add(UiKit.Cost("item_gold", gold, v.Gold));
            cost.Add(UiKit.Cost("item_expbook", exp, v.Material(HeroGrowth.HeroExp)));
            content.Add(cost);

            if (atCap) footer.Add(UiKit.DoneBtn("已達等級上限").WithClass("btn-lg").WithClass("btn-block"));
            else footer.Add(UiKit.Btn("升級", () => _ = Act(() => GameSession.Backend.LevelUp(def.Id)), primary: true).WithClass("btn-lg").WithClass("btn-block"));
        }

        private void BuildBreak(VisualElement content, VisualElement footer, HeroDef def, HeroState hero, ProfileView v)
        {
            int shards = v.Material(HeroGrowth.ShardKey(def.Id));
            content.Add(UiKit.Text($"{hero.Stars}/{HeroGrowth.MaxStars} 突　重複武將 {shards}", "line-title"));
            if (hero.Stars < HeroGrowth.MaxStars)
            {
                int gold = HeroGrowth.BreakthroughGold(def.Rarity, hero.Stars + 1);
                var cost = new VisualElement();
                cost.AddToClassList("cost-line");
                cost.Add(UiKit.Cost("item_shard", 1, shards));
                cost.Add(UiKit.Cost("item_gold", gold, v.Gold));
                content.Add(cost);
            }
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            foreach (var e in Breakthroughs.For(def))
            {
                bool got = e.Stars <= hero.Stars;
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("break-row");
                if (got) row.AddToClassList("break-row-on");
                row.Add(new Label(got ? "●" : "○") { pickingMode = PickingMode.Ignore }.WithClass("break-row-mark"));
                string detail = e.Kind == BreakthroughKind.UpgradeCard && e.NewCard != null
                    ? $"{e.Description}\n{CardText.Description(e.NewCard)}" : e.Description;
                row.Add(new Label($"{e.Stars} 突　{detail}") { pickingMode = PickingMode.Ignore }.WithClass("break-row-text"));
                scroll.Add(row);
            }
            content.Add(scroll);
            if (hero.Stars < HeroGrowth.MaxStars)
            {
                bool can = shards >= 1 && v.Gold >= HeroGrowth.BreakthroughGold(def.Rarity, hero.Stars + 1);
                footer.Add(UiKit.Btn("突破", () => _ = Act(() => GameSession.Backend.Breakthrough(def.Id)), primary: can).WithClass("btn-lg").WithClass("btn-block"));
            }
            else footer.Add(UiKit.DoneBtn("已滿突").WithClass("btn-lg").WithClass("btn-block"));
        }

        private void BuildEquip(VisualElement content, HeroDef def, HeroState hero, ProfileView v)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            foreach (var slot in Equipment.Slots)
            {
                var sl = slot;
                hero.Equipment.TryGetValue(sl.ToString(), out int worn);
                var row = new VisualElement();
                row.AddToClassList("card-row");
                var head = new VisualElement();
                head.AddToClassList("card-row-head");
                head.Add(UiKit.Text(worn > 0 ? $"{Equipment.SlotName(sl)}：{Equipment.Name(sl, worn)}" : $"{Equipment.SlotName(sl)}：未配戴", "card-row-name"));
                if (worn > 0) head.Add(UiKit.Btn("卸下", () => _ = Act(() => GameSession.Backend.Unequip(def.Id, sl.ToString()))).WithClass("btn-sm"));
                row.Add(head);
                row.Add(UiKit.Text(SlotEffect(def, sl), "card-row-desc"));
                // 庫存：每個品階一顆按鈕，點了穿上（原本的退回庫存）。
                var stock = new VisualElement();
                stock.style.flexDirection = FlexDirection.Row;
                stock.style.flexWrap = Wrap.Wrap;
                bool any = false;
                for (int tier = 1; tier <= Equipment.MaxTier; tier++)
                {
                    int have = v.Material(Equipment.ItemKey(sl, tier));
                    if (have <= 0) continue;
                    any = true;
                    int t = tier;
                    stock.Add(UiKit.Btn($"穿 {t} 階 ×{have}", () => _ = Act(() => GameSession.Backend.Equip(def.Id, sl.ToString(), t)), primary: t > worn).WithClass("btn-sm"));
                    stock.Add(UiKit.Btn($"分解 +{Equipment.DismantleGold(t)}", () => _ = Act(() => GameSession.Backend.Dismantle(sl.ToString(), t, 1))).WithClass("btn-sm"));
                }
                if (!any) stock.Add(UiKit.Text("庫存沒有這個部位的裝備（素材副本可取得）", "card-row-desc"));
                row.Add(stock);
                scroll.Add(row);
            }
            content.Add(scroll);
        }

        private static string SlotEffect(HeroDef def, EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Weapon: return (Equipment.WeaponBoostsInt(def.Role) ? "謀略" : "攻擊") + " 每階 +10%";
                case EquipSlot.Armor: return "生命、防禦 每階 +10%";
                default: return def.Role == Role.Warrior || def.Role == Role.Ranger ? "爆擊率 每階 +3" : "閃避 每階 +2";
            }
        }
    }
}
