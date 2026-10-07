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
            body.Add(UiKit.Text($"經驗書 {v.Material(HeroGrowth.ExpBook)}　卡牌強化素材 {v.Material(HeroGrowth.CardMaterial)}", "txt-dim"));

            if (v.Heroes.Count == 0)
            {
                body.Add(UiKit.Text("尚未擁有武將，先去招募吧"));
                body.Add(UiKit.Btn("前往招募", () => Nav.Go(Page.Gacha), primary: true));
                return;
            }
            if (_heroId == null || !v.Heroes.ContainsKey(_heroId)) _heroId = v.Heroes.Keys.First();

            var split = new VisualElement();
            split.AddToClassList("split");

            var list = new VisualElement();
            list.AddToClassList("split-list");
            foreach (var def in GameSession.Roster.Where(d => v.Heroes.ContainsKey(d.Id)))
            {
                var state = v.Heroes[def.Id];
                string id = def.Id;
                var item = new Button(() => { _heroId = id; Rebuild(); })
                {
                    text = $"{def.Name}\nLv.{state.Level} {new string('★', state.Stars)}　{def.Rarity}",
                };
                item.AddToClassList("list-item");
                if (id == _heroId) item.AddToClassList("list-item-on");
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
            panel.Add(UiKit.Text($"{def.Name}　{def.Rarity} {CardText.RoleName(def.Role)}", "popup-title"));
            panel.Add(UiKit.Text($"血量 {s.Hp}　攻擊 {s.Atk}　防禦 {s.Def}"));

            // 等級
            var lvRow = UiKit.Row();
            lvRow.Add(UiKit.Text($"等級 {hero.Level}（上限為帳號等級 {v.Level}）"));
            lvRow.Add(UiKit.Btn($"升級（金幣 {HeroGrowth.LevelUpGold(hero.Level)}、經驗書 {HeroGrowth.LevelUpBooks(hero.Level)}）",
                () => _ = Act(() => GameSession.Backend.LevelUp(def.Id))));
            panel.Add(lvRow);

            // 突破
            var shardKey = HeroGrowth.ShardKey(def.Id);
            var btRow = UiKit.Row();
            btRow.Add(UiKit.Text($"突破 {hero.Stars}/{HeroGrowth.MaxStars}★　碎片 {v.Material(shardKey)}/{HeroGrowth.CopyShards}"));
            if (hero.Stars < HeroGrowth.MaxStars)
                btRow.Add(UiKit.Btn("突破", () => _ = Act(() => GameSession.Backend.Breakthrough(def.Id)), primary: true));
            panel.Add(btRow);
            foreach (var e in _breakthroughs.Get(def.Id))
                panel.Add(UiKit.Text($"{(e.Stars <= hero.Stars ? "●" : "○")} {e.Stars}★　{e.Description}", "txt-dim"));

            // 卡牌強化
            panel.Add(UiKit.Text("卡牌強化", "txt-sub"));
            foreach (var card in def.Deck.GroupBy(c => c.Id).Select(g => g.First()))
            {
                hero.CardLevels.TryGetValue(card.Id, out int cl);
                int copies = def.Deck.Count(c => c.Id == card.Id);
                var row = UiKit.Row();
                row.Add(UiKit.Text($"《{card.Name}》×{copies}　{CardText.Description(card)}　強化 {cl}/{HeroGrowth.MaxCardLevel}"));
                if (cl < HeroGrowth.MaxCardLevel)
                {
                    string cid = card.Id;
                    row.Add(UiKit.Btn($"強化（金幣 {HeroGrowth.CardUpgradeGold(cl)}、素材 {HeroGrowth.CardUpgradeMaterial(cl)}）",
                        () => _ = Act(() => GameSession.Backend.Enhance(def.Id, cid))));
                }
                panel.Add(row);
            }
        }
    }
}
