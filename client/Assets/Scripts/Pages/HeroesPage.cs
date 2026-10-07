#nullable enable
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>武將：左側武將清單，右側詳情（等級、突破、卡牌強化）。</summary>
    public sealed class HeroesPage : PageBase
    {
        private readonly BreakthroughTable _breakthroughs = DemoBreakthroughs.Create();
        private string? _heroId;

        protected override Page Id => Page.Heroes;
        protected override string Title => "武將";

        protected override void BuildBody(VisualElement body)
        {
            var v = GameSession.View;
            body.Add(UiKit.Hint($"經驗書 {v.Material(HeroGrowth.ExpBook)}　　卡牌強化素材 {v.Material(HeroGrowth.CardMaterial)}"));

            if (v.Heroes.Count == 0)
            {
                body.Add(UiKit.Hint("尚未擁有武將，先去招募吧", warn: true));
                body.Add(UiKit.Btn("前往招募", () => Nav.Go(Page.Gacha), primary: true).WithClass("btn-wide"));
                return;
            }
            if (_heroId == null || !v.Heroes.ContainsKey(_heroId)) _heroId = GameSession.Roster.First(d => v.Heroes.ContainsKey(d.Id)).Id;

            var split = new VisualElement();
            split.AddToClassList("split");

            var list = new VisualElement();
            list.AddToClassList("split-list");
            foreach (var def in GameSession.Roster.Where(d => v.Heroes.ContainsKey(d.Id)))
            {
                var state = v.Heroes[def.Id];
                string id = def.Id;
                var item = new Button(() => { _heroId = id; Rebuild(); });
                item.AddToClassList("hero-item");
                item.AddToClassList("hero-item-" + UiKit.RarityClass(def.Rarity));
                if (id == _heroId) item.AddToClassList("hero-item-on");
                item.Add(UiKit.Avatar(def.Name, def.Rarity, false, def.Id));
                var info = new VisualElement { pickingMode = PickingMode.Ignore };
                info.AddToClassList("hero-item-text");
                info.Add(new Label(def.Name) { pickingMode = PickingMode.Ignore }.WithClass("hero-item-name"));
                info.Add(new Label($"Lv.{state.Level}　{CardText.RoleName(def.Role)}") { pickingMode = PickingMode.Ignore }.WithClass("hero-item-sub"));
                info.Add(new Label(new string('★', state.Stars) + new string('☆', HeroGrowth.MaxStars - state.Stars)) { pickingMode = PickingMode.Ignore }.WithClass("star-text"));
                item.Add(info);
                list.Add(item);
            }
            split.Add(list);

            var detail = UiKit.Panel("split-detail");
            if (GameSession.DefOf(_heroId) is HeroDef def2 && v.Heroes.TryGetValue(_heroId, out var hero))
                BuildDetail(detail, def2, hero, v);
            split.Add(detail);
            body.Add(split);
        }

        private void BuildDetail(VisualElement panel, HeroDef def, HeroState hero, ProfileView v)
        {
            var s = HeroGrowth.ScaleStats(def.Base, hero, _breakthroughs);

            // 有全身立繪就放在左邊，內容在右邊
            var full = HeroArt.Full(def.Id);
            if (full != null)
            {
                panel.style.flexDirection = FlexDirection.Row;
                var art = new VisualElement();
                art.AddToClassList("hero-full");
                art.AddToClassList("hero-full-" + UiKit.RarityClass(def.Rarity));
                art.style.backgroundImage = new StyleBackground(full);
                panel.Add(art);
                var column = new VisualElement();
                column.AddToClassList("grow");
                panel.Add(column);
                panel = column;
            }

            var head = new VisualElement();
            head.AddToClassList("hero-head");
            if (full == null) head.Add(UiKit.Avatar(def.Name, def.Rarity, true, def.Id));
            var title = new VisualElement();
            title.AddToClassList("grow");
            title.Add(UiKit.Text(def.Name, "hero-name"));
            var badges = UiKit.Row();
            badges.Add(UiKit.RarityBadge(def.Rarity));
            badges.Add(UiKit.Badge(CardText.RoleName(def.Role), "badge-role"));
            badges.Add(UiKit.Text($"Lv.{hero.Level}", "txt-gold"));
            title.Add(badges);
            head.Add(title);
            panel.Add(head);

            var stats = new VisualElement();
            stats.AddToClassList("stat-row");
            stats.Add(UiKit.Stat("血量", s.Hp.ToString()));
            stats.Add(UiKit.Stat("攻擊", s.Atk.ToString()));
            stats.Add(UiKit.Stat("防禦", s.Def.ToString()));
            panel.Add(stats);

            // 等級
            panel.Add(UiKit.Section("等級"));
            var lvRow = UiKit.Row();
            lvRow.AddToClassList("panel-row");
            lvRow.Add(UiKit.Text($"Lv.{hero.Level}　上限為帳號等級 {v.Level}").WithClass("grow"));
            lvRow.Add(UiKit.Btn($"升級　金幣 {HeroGrowth.LevelUpGold(hero.Level)}・經驗書 {HeroGrowth.LevelUpBooks(hero.Level)}",
                () => _ = Act(() => GameSession.Backend.LevelUp(def.Id))));
            panel.Add(lvRow);

            // 突破
            panel.Add(UiKit.Section("突破"));
            var shardKey = HeroGrowth.ShardKey(def.Id);
            int shards = v.Material(shardKey);
            var btRow = UiKit.Row();
            btRow.AddToClassList("panel-row");
            btRow.Add(UiKit.Text($"{hero.Stars}/{HeroGrowth.MaxStars}★　碎片 {shards}/{HeroGrowth.CopyShards}").WithClass("grow"));
            if (hero.Stars < HeroGrowth.MaxStars)
                btRow.Add(UiKit.Btn("突破", () => _ = Act(() => GameSession.Backend.Breakthrough(def.Id)), primary: shards >= HeroGrowth.CopyShards));
            else
                btRow.Add(UiKit.DoneBtn("已滿星"));
            panel.Add(btRow);
            panel.Add(UiKit.Bar(100f * shards / HeroGrowth.CopyShards, "bar-gold bar-slim"));
            foreach (var e in _breakthroughs.Get(def.Id))
                panel.Add(UiKit.Text($"{(e.Stars <= hero.Stars ? "●" : "○")} {e.Stars}★　{e.Description}", e.Stars <= hero.Stars ? "txt-good" : "txt-dim"));

            // 卡牌強化
            panel.Add(UiKit.Section("卡牌強化"));
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
                    row.Add(UiKit.Btn($"強化　金幣 {HeroGrowth.CardUpgradeGold(cl)}・素材 {HeroGrowth.CardUpgradeMaterial(cl)}",
                        () => _ = Act(() => GameSession.Backend.Enhance(def.Id, cid))));
                }
                else row.Add(UiKit.DoneBtn("已滿級"));
                panel.Add(row);
            }
        }
    }
}
