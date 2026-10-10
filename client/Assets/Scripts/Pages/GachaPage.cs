#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public sealed class GachaPage : PageBase
    {
        private readonly List<GachaPool> _pools = DemoMeta.Pools();
        private string _poolId = DemoMeta.NewbiePoolId;
        private List<PullResult>? _last;
        private int _lastCount = 10;

        protected override Page Id => Page.Gacha;
        protected override string Title => "招募";

        public Task DebugTenPull() => Pull(10);

        private HeroDef? DefOf(string heroId) => GameSession.DefOf(heroId);

        protected override void BuildBody(VisualElement body)
        {
            var current = _pools.Find(p => p.Id == _poolId) ?? _pools[0];
            GameSession.View.Pools.TryGetValue(current.Id, out var state);
            var page = new VisualElement().WithClass("recruit-layout grow");
            body.Add(page);
            var selection = new VisualElement().WithClass("recruit-selection");
            foreach (var pool in _pools)
            {
                var selected = pool;
                var tab = UiKit.Btn("", () => { _poolId = selected.Id; _last = null; Rebuild(); }).WithClass("recruit-pool");
                tab.name = "pool-" + pool.Id;
                if (pool.Id == current.Id) tab.AddToClassList("recruit-pool-on");
                tab.Add(Banner(pool, "recruit-thumbnail"));
                tab.Add(UiKit.Text(pool.Name, "recruit-pool-name"));
                selection.Add(tab);
            }
            page.Add(selection);
            var main = new VisualElement().WithClass("recruit-main");
            var banner = Banner(current, "recruit-banner");
            banner.Add(UiKit.Text(current.Name, "recruit-title"));
            main.Add(banner);
            var status = UiKit.Row("recruit-status");
            if (current.HardPityUr > 0)
            {
                int since = state?.PullsSinceUr ?? 0;
                var pity = new VisualElement().WithClass("recruit-pity");
                pity.Add(UiKit.Text($"UR 保底 {since} / {current.HardPityUr}", "recruit-pity-text"));
                pity.Add(UiKit.Bar(100f * since / current.HardPityUr, "bar-gold"));
                status.Add(pity);
            }
            status.Add(UiKit.Btn("機率詳情", () => ShowRates(current)).WithClass("recruit-rates-button"));
            main.Add(status);
            var actions = UiKit.Row("recruit-actions");
            bool firstTen = current.FirstTenGuaranteesUr && (state?.TenPulls ?? 0) == 0;
            actions.Add(PullButton("單抽", current.SingleCost, 1, false, null));
            actions.Add(PullButton("十連", current.TenCost, 10, true, firstTen ? "首次必出 UR" : null));
            main.Add(actions);
            page.Add(main);
            if (_last != null) body.Add(BuildResults());
        }

        private VisualElement Banner(GachaPool pool, string cls)
        {
            var banner = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass(cls);
            var texture = Resources.Load<Texture2D>("RecruitBanners/" + pool.Id);
            if (texture != null) banner.style.backgroundImage = new StyleBackground(texture);
            var heroes = pool.UpUrs.Count > 0 ? pool.UpUrs : pool.UrHeroes.Take(3).ToList();
            var lineup = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("recruit-lineup");
            foreach (string id in heroes)
            {
                var art = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("recruit-character");
                var image = HeroArt.Full(id) ?? HeroArt.Face(id);
                if (image != null) art.style.backgroundImage = new StyleBackground(image);
                lineup.Add(art);
            }
            banner.Add(lineup);
            return banner;
        }

        private void ShowRates(GachaPool pool) => UiHelp.Dialog(Host, pool.Name + " · 機率詳情", body =>
        {
            foreach (var rate in pool.DisclosedRates().Where(kv => kv.Value > 0))
                body.Add(UiKit.Text($"{rate.Key}　{rate.Value:0.##}%", "ui-help-text"));
            body.Add(UiKit.Text(pool.PityDescription(), "ui-help-text"));
            var names = pool.UrHeroes.Select(id => DefOf(id)?.Name ?? id);
            body.Add(UiKit.Text("UR：" + string.Join("、", names), "ui-help-text"));
            if (pool.UpUrs.Count > 0) body.Add(UiKit.Text("UP：" + string.Join("、", pool.UpUrs.Select(id => DefOf(id)?.Name ?? id)), "ui-help-text"));
        });

        public void DebugSelectPool(string id) { _poolId = id; _last = null; Rebuild(); }
        public void DebugShowRates() => ShowRates(_pools.Find(p => p.Id == _poolId) ?? _pools[0]);

        private Button PullButton(string label, int cost, int count, bool primary, string? tag)
        {
            var b = UiKit.Btn("", () => _ = Pull(count), primary: primary).WithClass("btn-lg");
            b.SetEnabled(GameSession.View.Yuanbao >= cost);
            b.tooltip = GameSession.View.Yuanbao >= cost ? $"花費 {cost} 元寶招募 {count} 次" : $"元寶不足，需要 {cost} 元寶";
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("gacha-cost");
            row.Add(new Label(label) { pickingMode = PickingMode.Ignore }.WithClass("cost-chip-text").WithClass("gacha-cost-label"));
            row.Add(UiKit.ItemTile("item_yuanbao"));
            row.Add(new Label(cost.ToString()) { pickingMode = PickingMode.Ignore }.WithClass("cost-chip-text"));
            b.Add(row);
            if (tag != null) b.Add(new Label(tag) { pickingMode = PickingMode.Ignore }.WithClass("btn-tag"));
            return b;
        }

        private VisualElement BuildResults()
        {
            var overlay = new VisualElement();
            overlay.AddToClassList("pull-overlay");
            overlay.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("pull-ring"));
            overlay.Add(UiKit.Text("招募結果", "pull-title"));

            var rows = new VisualElement();
            rows.AddToClassList("pull-rows");
            var results = _last!;
            int perRow = results.Count > 5 ? 5 : results.Count;
            VisualElement? line = null;
            for (int i = 0; i < results.Count; i++)
            {
                if (i % perRow == 0)
                {
                    line = new VisualElement();
                    line.AddToClassList("pull-row2");
                    rows.Add(line);
                }
                var r = results[i];
                var d = DefOf(r.HeroId);
                if (d == null) continue;
                var tile = UiKit.HeroTile(d, 0, -1, () => { }, cls: "htile", extraClass: "htile-lg");
                tile.AddToClassList("pop");
                tile.AddToClassList("pop-hidden");
                if (r.Rarity == Rarity.UR) tile.AddToClassList("pull-ur");
                if (r.IsNew) tile.Add(new Label("NEW") { pickingMode = PickingMode.Ignore }.WithClass("pull-tag"));
                else if (r.Souls > 0) tile.Add(new Label($"將魂+{r.Souls}") { pickingMode = PickingMode.Ignore }.WithClass("pull-tag").WithClass("pull-tag-shard"));
                else if (r.Shards > 0) tile.Add(new Label($"信物+{r.Shards}") { pickingMode = PickingMode.Ignore }.WithClass("pull-tag").WithClass("pull-tag-shard"));
                line!.Add(tile);
                tile.schedule.Execute(() => tile.RemoveFromClassList("pop-hidden")).StartingIn(150 + i * 130);
            }
            overlay.Add(rows);

            var buttons = new VisualElement();
            buttons.AddToClassList("pull-buttons");
            buttons.Add(UiKit.Btn("確定", () => { _last = null; Rebuild(); }));
            var current = _pools.Find(p => p.Id == _poolId) ?? _pools[0];
            int repeatCost = _lastCount == 1 ? current.SingleCost : current.TenCost;
            var repeat = UiKit.Btn(_lastCount == 1 ? "再抽一次" : "再抽十連", () => _ = Pull(_lastCount), primary: true);
            repeat.SetEnabled(GameSession.View.Yuanbao >= repeatCost);
            repeat.tooltip = GameSession.View.Yuanbao >= repeatCost ? $"花費 {repeatCost} 元寶再次招募" : $"元寶不足，需要 {repeatCost} 元寶";
            buttons.Add(repeat);
            overlay.Add(buttons);
            return overlay;
        }

        private Task Pull(int count) => Act(async () =>
        {
            var r = await GameSession.Backend.Pull(_poolId, count);
            if (r.Ok) { _last = r.Results; _lastCount = count; }
            return (BackendResult)r;
        });
    }
}
