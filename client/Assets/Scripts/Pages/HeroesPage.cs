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

        /// <summary>截圖 / 除錯用：切到指定分頁（0 升級、1 突破、2 卡牌強化）。</summary>
        public void DebugSetTab(int tab)
        {
            _tab = (Tab)tab;
            Rebuild();
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
            bar.Add(new Label($"Lv.{hero.Level}") { pickingMode = PickingMode.Ignore }.WithClass("hero-namebar-level"));
            center.Add(bar);

            // 屬性：目前 → 升級後（只有隨等級成長的四項會顯示預覽）
            var now = HeroGrowth.ScaleStats(def.Base, hero, _breakthroughs);
            var next = HeroGrowth.ScaleStats(def.Base, new HeroState { HeroId = hero.HeroId, Level = hero.Level + 1, Stars = hero.Stars, CardLevels = hero.CardLevels }, _breakthroughs);
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
            stats.Add(colA);
            stats.Add(colB);
            center.Add(stats);

            // 品階（突破星級）：星星下方標示數字，沒突破時暗星也看得到。
            var grade = new VisualElement { pickingMode = PickingMode.Ignore };
            grade.AddToClassList("hero-grade");
            grade.Add(UiKit.StarsRow(hero.Stars, HeroGrowth.MaxStars, "stars-lg"));
            grade.Add(new Label($"突破 {hero.Stars}/{HeroGrowth.MaxStars}") { pickingMode = PickingMode.Ignore }.WithClass("hero-grade-text"));
            center.Add(grade);
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

            var seg = new VisualElement();
            seg.AddToClassList("seg");
            seg.Add(SegTab("升級", Tab.Level));
            seg.Add(SegTab("突破", Tab.Break));
            seg.Add(SegTab("卡牌強化", Tab.Cards));
            right.Add(seg);

            // 面板分上下兩塊：內容（撐滿）與底部主要操作（貼底），三個分頁的按鈕位置一致。
            var panel = new VisualElement();
            panel.AddToClassList("bpanel");
            panel.AddToClassList("side-panel");
            if (_tab == Tab.Cards) panel.AddToClassList("side-panel-fill"); // 卡牌清單長，撐滿；其餘分頁依內容高度
            var content = new VisualElement();
            content.AddToClassList("side-body");
            var footer = new VisualElement();
            footer.AddToClassList("side-footer");
            switch (_tab)
            {
                case Tab.Level: BuildLevel(content, footer, def, hero, v); break;
                case Tab.Break: BuildBreak(content, footer, def, hero, v); break;
                default: BuildCards(content, def, hero, v); break;
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
            int gold = HeroGrowth.LevelUpGold(hero.Level), books = HeroGrowth.LevelUpBooks(hero.Level);
            bool atCap = hero.Level >= v.Level;
            content.Add(UiKit.Text($"Lv.{hero.Level}  /  帳號等級上限 {v.Level}", "side-caption"));
            content.Add(UiKit.Text("升級消耗", "txt-sub"));
            var cost = new VisualElement();
            cost.AddToClassList("cost-line");
            cost.Add(UiKit.Cost("item_gold", gold, v.Gold));
            cost.Add(UiKit.Cost("item_expbook", books, v.Material(HeroGrowth.ExpBook)));
            content.Add(cost);

            if (atCap) footer.Add(UiKit.DoneBtn("已達等級上限").WithClass("btn-lg").WithClass("btn-block"));
            else footer.Add(UiKit.Btn("升級", () => _ = Act(() => GameSession.Backend.LevelUp(def.Id)), primary: true).WithClass("btn-lg").WithClass("btn-block"));
        }

        private void BuildBreak(VisualElement content, VisualElement footer, HeroDef def, HeroState hero, ProfileView v)
        {
            int shards = v.Material(HeroGrowth.ShardKey(def.Id));
            content.Add(UiKit.Text($"{hero.Stars}/{HeroGrowth.MaxStars} 星　碎片 {shards}/{HeroGrowth.CopyShards}", "line-title"));
            content.Add(UiKit.Bar(100f * shards / HeroGrowth.CopyShards, "bar-gold"));
            foreach (var e in _breakthroughs.Get(def.Id))
            {
                bool got = e.Stars <= hero.Stars;
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("break-row");
                if (got) row.AddToClassList("break-row-on");
                row.Add(new Label(got ? "●" : "○") { pickingMode = PickingMode.Ignore }.WithClass("break-row-mark"));
                row.Add(new Label($"{e.Stars}★　{e.Description}") { pickingMode = PickingMode.Ignore }.WithClass("break-row-text"));
                content.Add(row);
            }
            if (hero.Stars < HeroGrowth.MaxStars)
                footer.Add(UiKit.Btn("突破", () => _ = Act(() => GameSession.Backend.Breakthrough(def.Id)), primary: shards >= HeroGrowth.CopyShards).WithClass("btn-lg").WithClass("btn-block"));
            else footer.Add(UiKit.DoneBtn("已滿星").WithClass("btn-lg").WithClass("btn-block"));
        }

        private void BuildCards(VisualElement content, HeroDef def, HeroState hero, ProfileView v)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            foreach (var card in def.Deck.GroupBy(c => c.Id).Select(g => g.First()))
            {
                hero.CardLevels.TryGetValue(card.Id, out int cl);
                int copies = def.Deck.Count(c => c.Id == card.Id);
                // 上排：名稱 + 強化按鈕；下排：說明與等級點，整列寬度都給說明用。
                var row = new VisualElement();
                row.AddToClassList("card-row");
                var head = new VisualElement();
                head.AddToClassList("card-row-head");
                head.Add(UiKit.Text($"{card.Name} ×{copies}", "card-row-name"));
                if (cl < HeroGrowth.MaxCardLevel)
                {
                    string cid = card.Id;
                    int need = HeroGrowth.CardUpgradeGold(cl);
                    head.Add(UiKit.Btn($"強化 {need:N0}", () => _ = Act(() => GameSession.Backend.Enhance(def.Id, cid)), primary: v.Gold >= need).WithClass("btn-sm"));
                }
                else head.Add(UiKit.DoneBtn("滿級").WithClass("btn-sm"));
                row.Add(head);
                row.Add(UiKit.Text(CardText.Description(card), "card-row-desc"));
                row.Add(UiKit.Pips(cl, HeroGrowth.MaxCardLevel));
                scroll.Add(row);
            }
            content.Add(scroll);
        }
    }
}
