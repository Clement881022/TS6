#nullable enable
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public class HeroesPage : PageBase
    {
        private enum Tab { Level, Break, Equip }
        public static string? LastSelectedHeroId { get; set; }

        private string? _heroId;
        private Tab _tab = Tab.Level;
        private ModelStage? _stage;
        private string? _stageHero;
        private bool _showModel;
        private bool _showAttributes = true;
        private float _rosterScrollOffset;

        protected virtual bool GrowthMode => false;
        protected override Page Id => Page.Heroes;
        protected override string Title => "武將";

        protected virtual void OnDestroy()
        {
            _stage?.Dispose();
            _stage = null;
        }

        public void DebugSetTab(int tab)
        {
            _tab = GrowthMode && tab > 1 ? Tab.Level : (Tab)tab;
            Rebuild();
        }

        public void DebugSelectHero(string id) { _heroId = id; _tab = Tab.Level; Rebuild(); }

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

        public void DebugSetAttributes(bool show)
        {
            _showAttributes = show;
            Rebuild();
        }

        protected override void BuildBody(VisualElement body)
        {
            var v = GameSession.View;
            var owned = GameSession.Roster.Where(d => v.Heroes.ContainsKey(d.Id))
                .OrderByDescending(d => d.Rarity).ThenBy(d => d.Id, System.StringComparer.Ordinal).ToList();
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
                _heroId = owned.FirstOrDefault(d => d.Id == LastSelectedHeroId)?.Id ?? owned[0].Id;
            LastSelectedHeroId = _heroId;
            var def = GameSession.DefOf(_heroId)!;
            var hero = v.Heroes[_heroId];

            var left = new VisualElement();
            left.AddToClassList("hero-left");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            float savedScrollOffset = _rosterScrollOffset;
            scroll.AddToClassList("roster-scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            scroll.contentContainer.AddToClassList("hero-grid");
            scroll.verticalScroller.valueChanged += value => _rosterScrollOffset = value;
            scroll.RegisterCallbackOnce<GeometryChangedEvent>(_ =>
                scroll.schedule.Execute(() => scroll.scrollOffset = new Vector2(0, savedScrollOffset)));
            foreach (var d in owned)
            {
                string id = d.Id;
                var st = v.Heroes[id];
                scroll.Add(RosterEntry(d, st, () => { _heroId = id; Rebuild(); }, id == _heroId));
            }
            left.Add(scroll);
            body.Add(left);

            body.Add(BuildCenter(def, hero, v));

            body.Add(BuildRight(def, hero, v));
        }

        private VisualElement BuildCenter(HeroDef def, HeroState hero, ProfileView v)
        {
            var center = new VisualElement();
            center.AddToClassList("hero-center");
            if (GrowthMode)
            {
                center.AddToClassList("growth-center");
                var tabs = new VisualElement().WithClass("seg growth-view-tabs");
                tabs.Add(UiKit.Tab("角色", () => { _showAttributes = false; Rebuild(); }, !_showAttributes).WithClass("seg-tab"));
                tabs.Add(UiKit.Tab("屬性", () => { _showAttributes = true; Rebuild(); }, _showAttributes).WithClass("seg-tab"));
                center.Add(tabs);
                var identity = new VisualElement().WithClass("growth-identity");
                identity.Add(UiKit.RarityBadge(def.Rarity));
                identity.Add(UiKit.Text(def.Name, "growth-identity-name"));
                identity.Add(UiKit.Badge(CardText.RoleName(def.Role), "badge-role"));
                identity.Add(UiKit.Text($"Lv.{hero.Level}", "hero-namebar-level"));
                center.Add(identity);
                if (_showAttributes)
                {
                    _stage?.Dispose();
                    _stage = null;
                    _stageHero = null;
                    var attributes = BuildStats(def, hero, v, _tab == Tab.Level, false);
                    attributes.AddToClassList("growth-attributes");
                    center.Add(attributes);
                    return center;
                }
            }
            var full = HeroArt.Full(def.Id) ?? HeroArt.Bust(def.Id) ?? HeroArt.Face(def.Id);
            if (full != null && !_showModel)
            {
                _stage?.Dispose();
                _stage = null;
                _stageHero = null;
                VisualElement art = HeroArt.Full(def.Id) != null ? new HeroFigure(def.Id, "strategy-hero-art") : new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("strategy-hero-art");
                if (HeroArt.Full(def.Id) == null) art.AddToClassList("strategy-hero-portrait");
                if (!(art is HeroFigure)) art.style.backgroundImage = new StyleBackground(full);
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
            if (!GrowthMode) center.Add(bar);

            if (full != null)
            {
                var toggle = UiKit.Btn("", () => { _showModel = !_showModel; Rebuild(); }).WithClass("strategy-hero-view");
                toggle.tooltip = _showModel ? "切換立繪" : "切換模型";
                toggle.Add(UiKit.Text("⇄", "hero-view-icon"));
                center.Add(toggle);
            }

            return center;
        }

        private static Button RosterEntry(HeroDef def, HeroState hero, System.Action choose, bool selected)
        {
            var row = new Button(choose).WithClass("strategy-roster-entry");
            row.name = "roster-" + def.Id;
            row.tooltip = $"{def.Name} · {def.Rarity} · Lv.{hero.Level} · {hero.Stars} 突";
            if (selected) row.AddToClassList("strategy-roster-selected");
            var face = PortraitArt.Create(def.Id, "strategy-roster-face");
            face.AddToClassList("roster-rarity-" + UiKit.RarityClass(def.Rarity));
            row.Add(face);
            var text = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("strategy-roster-text");
            text.Add(BreakthroughStars(hero.Stars, "strategy-roster-stars"));
            text.Add(UiKit.Text(def.Name, "strategy-roster-name"));
            text.Add(UiKit.Text($"Lv.{hero.Level}", "strategy-roster-level"));
            row.Add(text);
            return row;
        }

        private static VisualElement BreakthroughStars(int stars, string cls)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("hero-breakthrough-stars " + cls);
            row.tooltip = $"突破 {stars}/{HeroGrowth.MaxStars}";
            for (int i = 0; i < HeroGrowth.MaxStars; i++)
                row.Add(UiKit.Text(i < stars ? "★" : "☆", "hero-breakthrough-star " + (i < stars ? "hero-breakthrough-star-on" : "hero-breakthrough-star-off")));
            return row;
        }

        private static VisualElement BuildStats(HeroDef def, HeroState hero, ProfileView v, bool preview, bool showGrade = true)
        {
            var summary = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("strategy-hero-summary");
            var now = HeroGrowth.ScaleStats(def, hero);
            var next = HeroGrowth.ScaleStats(def, new HeroState { HeroId = hero.HeroId, Level = hero.Level + 1, Stars = hero.Stars, Equipment = hero.Equipment });
            bool canLevel = preview && hero.Level < v.Level;
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

            if (!showGrade) return summary;
            var grade = new VisualElement { pickingMode = PickingMode.Ignore };
            grade.AddToClassList("hero-grade");
            grade.Add(BreakthroughStars(hero.Stars, "stars-lg"));
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

            var seg = new VisualElement();
            seg.AddToClassList("seg");
            seg.Add(SegTab(GrowthMode ? "升級" : "狀態", Tab.Level));
            seg.Add(SegTab(GrowthMode ? "突破" : "牌組", Tab.Break));
            if (!GrowthMode) seg.Add(SegTab("裝備", Tab.Equip));
            right.Add(seg);

            var panel = new VisualElement();
            panel.AddToClassList("bpanel");
            panel.AddToClassList("side-panel");
            panel.AddToClassList("side-panel-fill");
            var content = new VisualElement();
            content.AddToClassList("side-body");
            var footer = new VisualElement();
            footer.AddToClassList("side-footer");
            if (!GrowthMode) BuildReadOnly(content, def, hero, v);
            else switch (_tab)
            {
                case Tab.Level: BuildLevel(content, footer, def, hero, v); break;
                case Tab.Break: BuildBreak(content, footer, def, hero, v); break;
                default: BuildLevel(content, footer, def, hero, v); break;
            }
            panel.Add(content);
            if (footer.childCount > 0) panel.Add(footer);
            right.Add(panel);
            return right;
        }

        private void BuildReadOnly(VisualElement content, HeroDef def, HeroState hero, ProfileView v)
        {
            if (_tab == Tab.Level)
            {
                content.Add(BuildStats(def, hero, v, false));
                return;
            }
            var scroll = new ScrollView(ScrollViewMode.Vertical).WithClass("grow");
            if (_tab == Tab.Break)
            {
                foreach (var card in HeroGrowth.BuildDef(def, hero).Deck)
                {
                    var row = new VisualElement().WithClass("card-row");
                    var head = UiKit.Row("card-row-head");
                    head.Add(UiKit.Text(card.Name, "card-row-name"));
                    head.Add(UiKit.Badge(card.Cost.ToString(), "badge-up"));
                    row.Add(head);
                    row.Add(UiKit.Text(CardText.Description(card), "card-row-desc"));
                    scroll.Add(row);
                }
            }
            else foreach (var slot in Equipment.Slots)
            {
                hero.Equipment.TryGetValue(slot.ToString(), out int tier);
                var row = new VisualElement().WithClass("card-row");
                row.Add(UiKit.Text(Equipment.SlotName(slot), "txt-sub"));
                row.Add(UiKit.Text(tier > 0 ? Equipment.Name(slot, tier, def.Role) + " · " + Equipment.TierLabel(tier) : "未配戴", "card-row-name"));
                scroll.Add(row);
            }
            content.Add(scroll);
        }

        private Button SegTab(string text, Tab tab) =>
            UiKit.Tab(text, () => { if (GrowthMode && tab == Tab.Equip) EquipmentPage.OpenFrom(Page.HeroGrowth, _heroId); else { _tab = tab; Rebuild(); } }, _tab == tab).WithClass("seg-tab");

        private void BuildLevel(VisualElement content, VisualElement footer, HeroDef def, HeroState hero, ProfileView v)
        {
            int gold = HeroGrowth.LevelUpGold(hero.Level), exp = HeroGrowth.LevelUpExp(hero.Level);
            bool atCap = hero.Level >= v.Level;
            content.Add(UiKit.Text($"Lv.{hero.Level} / {v.Level}", "side-caption"));
            content.Add(UiKit.Text("升級消耗", "txt-sub"));
            var cost = new VisualElement();
            cost.AddToClassList("cost-line");
            cost.Add(UiKit.Cost("item_gold", gold, v.Gold));
            cost.Add(UiKit.Cost("item_expbook", exp, v.Material(HeroGrowth.HeroExp)));
            content.Add(cost);

            if (atCap) footer.Add(UiKit.DoneBtn("已達等級上限").WithClass("btn-lg growth-action"));
            else
            {
                var upgrade = UiKit.Btn("升級", () => _ = Act(() => GameSession.Backend.LevelUp(def.Id)), primary: true).WithClass("btn-lg growth-action");
                upgrade.SetEnabled(v.Gold >= gold && v.Material(HeroGrowth.HeroExp) >= exp);
                footer.Add(upgrade);
            }
        }

        private void BuildBreak(VisualElement content, VisualElement footer, HeroDef def, HeroState hero, ProfileView v)
        {
            int shards = v.Material(HeroGrowth.ShardKey(def.Id));
            content.Add(BreakthroughStars(hero.Stars, "growth-break-stars"));
            if (hero.Stars < HeroGrowth.MaxStars)
            {
                int gold = HeroGrowth.BreakthroughGold(def.Rarity, hero.Stars + 1);
                var cost = new VisualElement();
                cost.AddToClassList("cost-line");
                var token = UiKit.Cost("item_shard", 1, shards);
                token.Insert(1, UiKit.Text(def.Name.Split('．').Last() + "信物", "growth-cost-name"));
                token.tooltip = "抽到重複武將可獲得該武將信物";
                cost.Add(token);
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
                var breakthrough = UiKit.Btn("突破", () => _ = Act(() => GameSession.Backend.Breakthrough(def.Id)), primary: true).WithClass("btn-lg growth-action");
                breakthrough.SetEnabled(can);
                footer.Add(breakthrough);
            }
            else footer.Add(UiKit.DoneBtn("已滿突").WithClass("btn-lg growth-action"));
        }

        internal static VisualElement EquipmentCard(HeroDef def, EquipSlot slot, int tier)
        {
            var card = new VisualElement().WithClass("growth-equipment-card equipment-tier-" + tier);
            string icon = slot == EquipSlot.Weapon ? "weapon_" + def.Role.ToString().ToLowerInvariant() : slot.ToString().ToLowerInvariant();
            var art = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("growth-equipment-art");
            var texture = Resources.Load<Texture2D>("EquipmentArt/" + icon);
            if (texture != null) art.style.backgroundImage = new StyleBackground(texture);
            card.Add(art);
            var details = new VisualElement().WithClass("growth-equipment-details");
            string type = slot == EquipSlot.Weapon ? Equipment.WeaponTypeName(def.Role) : Equipment.SlotName(slot);
            details.Add(UiKit.Text(type, "growth-equipment-type"));
            if (tier > 0)
            {
                int percent = Equipment.PercentOf(tier);
                var mods = Equipment.Mods(def.Role, new System.Collections.Generic.Dictionary<string, int> { [slot.ToString()] = tier });
                if (slot == EquipSlot.Weapon) details.Add(UiKit.Text($"{(Equipment.WeaponBoostsInt(def.Role) ? "謀略" : "攻擊")} +{percent}%", "growth-equipment-effect"));
                else if (slot == EquipSlot.Armor)
                {
                    details.Add(UiKit.Text($"生命 +{percent}%", "growth-equipment-effect"));
                    details.Add(UiKit.Text($"防禦 +{percent}%", "growth-equipment-effect"));
                }
                else details.Add(UiKit.Text(mods.Crit > 0 ? $"爆擊率 +{mods.Crit} 點" : $"閃避 +{mods.Dodge} 點", "growth-equipment-effect"));
                card.tooltip = $"{Equipment.Name(slot, tier, def.Role)} · {Equipment.TierLabel(tier)}";
            }
            else details.Add(UiKit.Text("未配戴", "growth-equipment-empty"));
            card.Add(details);
            return card;
        }
    }
}
