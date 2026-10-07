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
        private enum Tab { Level, Break, Cards }

        private readonly BreakthroughTable _breakthroughs = DemoBreakthroughs.Create();
        private string? _heroId;
        private Tab _tab = Tab.Level;
        private ModelStage? _stage;
        private string? _stageHero;

        protected override Page Id => Page.Heroes;
        protected override string Title => "武將";

        private void OnDestroy()
        {
            _stage?.Dispose();
            _stage = null;
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
            if (_heroId == null || !v.Heroes.ContainsKey(_heroId)) _heroId = owned[0].Id;
            var def = GameSession.DefOf(_heroId)!;
            var hero = v.Heroes[_heroId];

            // ---- 左：武將卡格 ----
            var left = new VisualElement();
            left.AddToClassList("hero-left");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            scroll.contentContainer.AddToClassList("hero-grid");
            foreach (var d in owned)
            {
                string id = d.Id;
                var st = v.Heroes[id];
                scroll.Add(UiKit.HeroTile(d, st.Level, st.Stars, () => { _heroId = id; Rebuild(); }, selected: id == _heroId));
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
            center.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("hero-emblem"));

            if (_stageHero != def.Id)
            {
                _stage?.Dispose();
                _stage = ModelStage.Create(def.Id, 520, 600);
                _stageHero = def.Id;
            }
            if (_stage != null)
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
            center.Add(bar);

            // 屬性：目前 → 升級後
            var now = HeroGrowth.ScaleStats(def.Base, hero, _breakthroughs);
            var next = HeroGrowth.ScaleStats(def.Base, new HeroState { HeroId = hero.HeroId, Level = hero.Level + 1, Stars = hero.Stars, CardLevels = hero.CardLevels }, _breakthroughs);
            bool canLevel = hero.Level < v.Level;
            var stats = new VisualElement { pickingMode = PickingMode.Ignore };
            stats.AddToClassList("hero-stats");
            stats.Add(StatLine("等級", hero.Level.ToString(), canLevel ? (hero.Level + 1).ToString() : null));
            stats.Add(StatLine("生命值", now.Hp.ToString(), canLevel ? next.Hp.ToString() : null));
            stats.Add(StatLine("攻擊", now.Atk.ToString(), canLevel ? next.Atk.ToString() : null));
            stats.Add(StatLine("防禦", now.Def.ToString(), canLevel ? next.Def.ToString() : null));
            center.Add(stats);

            var stars = UiKit.StarsRow(hero.Stars, HeroGrowth.MaxStars, "stars-lg");
            stars.style.marginBottom = 14;
            center.Add(stars);
            return center;
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
                arrow.style.color = new Color(0.4f, 0.95f, 0.6f);
                right.Add(arrow);
                var n = new Label(next) { pickingMode = PickingMode.Ignore };
                n.AddToClassList("stat-line-val");
                n.style.color = new Color(0.4f, 0.95f, 0.6f);
                right.Add(n);
            }
            line.Add(right);
            return line;
        }

        private VisualElement BuildRight(HeroDef def, HeroState hero, ProfileView v)
        {
            var right = new VisualElement();
            right.AddToClassList("hero-right");

            right.Add(SideTab("升級", Tab.Level));
            right.Add(SideTab("突破", Tab.Break));
            right.Add(SideTab("卡牌強化", Tab.Cards));

            var panel = new VisualElement();
            panel.AddToClassList("bpanel");
            panel.AddToClassList("side-panel");
            switch (_tab)
            {
                case Tab.Level: BuildLevel(panel, def, hero, v); break;
                case Tab.Break: BuildBreak(panel, def, hero, v); break;
                default: BuildCards(panel, def, hero); break;
            }
            right.Add(panel);
            return right;
        }

        private Button SideTab(string text, Tab tab)
        {
            var b = UiKit.Tab(text, () => { _tab = tab; Rebuild(); }, _tab == tab);
            b.AddToClassList("side-tab");
            return b;
        }

        private static VisualElement Cost(string icon, int have, int need)
        {
            var chip = new VisualElement { pickingMode = PickingMode.Ignore };
            chip.AddToClassList("cost-chip");
            if (have < need) chip.AddToClassList("cost-chip-bad");
            chip.Add(UiKit.ItemTile(icon));
            chip.Add(new Label($"{need}") { pickingMode = PickingMode.Ignore }.WithClass("cost-chip-text"));
            return chip;
        }

        private void BuildLevel(VisualElement panel, HeroDef def, HeroState hero, ProfileView v)
        {
            panel.Add(UiKit.Text("升級", "bpanel-title"));
            panel.Add(UiKit.Text($"目前 Lv.{hero.Level}　上限為帳號等級 {v.Level}", "line-sub"));
            int gold = HeroGrowth.LevelUpGold(hero.Level), books = HeroGrowth.LevelUpBooks(hero.Level);
            var cost = UiKit.Row();
            cost.Add(Cost("item_gold", v.Gold, gold));
            cost.Add(Cost("item_expbook", v.Material(HeroGrowth.ExpBook), books));
            panel.Add(cost);
            panel.Add(UiKit.Text($"持有：金幣 {v.Gold:N0}　經驗書 {v.Material(HeroGrowth.ExpBook)}", "line-sub"));
            var btn = UiKit.Btn("升級", () => _ = Act(() => GameSession.Backend.LevelUp(def.Id)), primary: true);
            btn.AddToClassList("btn-wide");
            panel.Add(btn);
        }

        private void BuildBreak(VisualElement panel, HeroDef def, HeroState hero, ProfileView v)
        {
            panel.Add(UiKit.Text("突破", "bpanel-title"));
            int shards = v.Material(HeroGrowth.ShardKey(def.Id));
            panel.Add(UiKit.Text($"{hero.Stars}/{HeroGrowth.MaxStars} 星　碎片 {shards}/{HeroGrowth.CopyShards}", "line-title"));
            panel.Add(UiKit.Bar(100f * shards / HeroGrowth.CopyShards, "bar-gold bar-slim"));
            foreach (var e in _breakthroughs.Get(def.Id))
                panel.Add(UiKit.Text($"{(e.Stars <= hero.Stars ? "●" : "○")} {e.Stars}★　{e.Description}", e.Stars <= hero.Stars ? "txt-good" : "line-sub"));
            if (hero.Stars < HeroGrowth.MaxStars)
            {
                var btn = UiKit.Btn("突破", () => _ = Act(() => GameSession.Backend.Breakthrough(def.Id)), primary: shards >= HeroGrowth.CopyShards);
                btn.AddToClassList("btn-wide");
                panel.Add(btn);
            }
            else panel.Add(UiKit.DoneBtn("已滿星"));
        }

        private void BuildCards(VisualElement panel, HeroDef def, HeroState hero)
        {
            panel.Add(UiKit.Text("卡牌強化", "bpanel-title"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            foreach (var card in def.Deck.GroupBy(c => c.Id).Select(g => g.First()))
            {
                hero.CardLevels.TryGetValue(card.Id, out int cl);
                int copies = def.Deck.Count(c => c.Id == card.Id);
                var row = new VisualElement();
                row.AddToClassList("card-row");
                var text = new VisualElement();
                text.AddToClassList("grow");
                text.Add(UiKit.Text($"{card.Name} ×{copies}", "card-row-name"));
                text.Add(UiKit.Text(CardText.Description(card), "card-row-desc"));
                text.Add(UiKit.Pips(cl, HeroGrowth.MaxCardLevel));
                row.Add(text);
                if (cl < HeroGrowth.MaxCardLevel)
                {
                    string cid = card.Id;
                    row.Add(UiKit.Btn($"強化 {HeroGrowth.CardUpgradeGold(cl)}", () => _ = Act(() => GameSession.Backend.Enhance(def.Id, cid))));
                }
                else row.Add(UiKit.DoneBtn("滿級"));
                scroll.Add(row);
            }
            panel.Add(scroll);
        }
    }
}
