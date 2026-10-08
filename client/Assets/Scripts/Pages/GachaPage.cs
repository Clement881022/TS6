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
    /// <summary>招募：卡池分頁、卡池主打武將展示、機率與保底、單抽 / 十連；結果以全螢幕卡片依序彈出。</summary>
    public sealed class GachaPage : PageBase
    {
        private readonly List<GachaPool> _pools = DemoMeta.Pools();
        private string _poolId = DemoMeta.NewbiePoolId;
        private List<PullResult>? _last;
        private int _lastCount = 10;

        protected override Page Id => Page.Gacha;
        protected override string Title => "招募";

        /// <summary>截圖 / 除錯用：直接抽一次十連。</summary>
        public Task DebugTenPull() => Pull(10);

        private HeroDef? DefOf(string heroId) => GameSession.DefOf(heroId);

        protected override void BuildBody(VisualElement body)
        {
            var page = new VisualElement();
            page.AddToClassList("gacha-page");
            page.AddToClassList("grow");
            body.Add(page);

            var current = _pools.Find(p => p.Id == _poolId) ?? _pools[0];
            GameSession.View.Pools.TryGetValue(current.Id, out var state);

            var tabs = new VisualElement();
            tabs.AddToClassList("gacha-tabs");
            foreach (var p in _pools)
            {
                var pool = p;
                tabs.Add(UiKit.Tab(pool.Name, () => { _poolId = pool.Id; _last = null; Rebuild(); }, pool.Id == _poolId));
            }
            page.Add(tabs);

            // ---- 中央：主打武將展示 + 機率資訊 ----
            var stage = new VisualElement();
            stage.AddToClassList("gacha-stage");
            var showcase = new VisualElement();
            showcase.AddToClassList("gacha-showcase");
            var heroes = current.UrHeroes.Select(DefOf).Where(d => d != null).Take(3).ToList();
            // 主打放正中間，其餘左右對稱。
            var order = Enumerable.Range(0, heroes.Count).ToList();
            if (order.Count == 3) order = new List<int> { 1, 0, 2 };
            foreach (int i in order)
            {
                var d = heroes[i]!;
                var card = new VisualElement { pickingMode = PickingMode.Ignore };
                card.AddToClassList("gacha-card");
                if (i == 0) card.AddToClassList("gacha-card-main");
                var tex = HeroArt.Full(d.Id) ?? HeroArt.Face(d.Id);
                if (tex != null) card.style.backgroundImage = new StyleBackground(tex);
                card.Add(new Label(d.Name) { pickingMode = PickingMode.Ignore }.WithClass("gacha-card-name"));
                showcase.Add(card);
            }
            stage.Add(showcase);

            var info = new VisualElement();
            info.AddToClassList("bpanel");
            info.AddToClassList("gacha-info");
            info.Add(UiKit.Text("機率公示", "bpanel-title"));
            var rates = new VisualElement();
            rates.AddToClassList("gacha-rates");
            foreach (var kv in current.DisclosedRates().Where(kv => kv.Value > 0))
            {
                var chip = new VisualElement();
                chip.AddToClassList("rate-chip");
                chip.Add(UiKit.Badge(kv.Key.ToString(), "badge-" + UiKit.RarityClass(kv.Key)));
                chip.Add(UiKit.Text($"{kv.Value:0.##}%", "txt-gold"));
                rates.Add(chip);
            }
            info.Add(rates);
            string pity = current.PityDescription();
            if (pity.Length > 0) info.Add(UiKit.Text(pity, "line-sub"));
            if (current.HardPityUr > 0)
            {
                int since = state?.PullsSinceUr ?? 0;
                info.Add(UiKit.Text($"距離保底還有 {current.HardPityUr - since} 抽（累計 {state?.TotalPulls ?? 0} 抽）", "line-title"));
                info.Add(UiKit.Bar(100f * since / current.HardPityUr, "bar-gold bar-slim"));
            }
            stage.Add(info);
            page.Add(stage);

            // ---- 底部：抽卡按鈕（首次十連保底 UR 以紅色角標提示）----
            var actions = new VisualElement();
            actions.AddToClassList("gacha-actions");
            bool firstTen = current.FirstTenGuaranteesUr && (state?.TenPulls ?? 0) == 0;
            actions.Add(PullButton("單抽", current.SingleCost, 1, false, null));
            actions.Add(PullButton("十連", current.TenCost, 10, true, firstTen ? "首次必出 UR" : null));
            page.Add(actions);

            if (_last != null) body.Add(BuildResults());
        }

        private Button PullButton(string label, int cost, int count, bool primary, string? tag)
        {
            var b = UiKit.Btn("", () => _ = Pull(count), primary: primary).WithClass("btn-lg");
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
                else if (r.Shards > 0) tile.Add(new Label($"重複+{r.Shards}") { pickingMode = PickingMode.Ignore }.WithClass("pull-tag").WithClass("pull-tag-shard"));
                line!.Add(tile);
                tile.schedule.Execute(() => tile.RemoveFromClassList("pop-hidden")).StartingIn(150 + i * 130);
            }
            overlay.Add(rows);

            var buttons = new VisualElement();
            buttons.AddToClassList("pull-buttons");
            buttons.Add(UiKit.Btn("確定", () => { _last = null; Rebuild(); }));
            buttons.Add(UiKit.Btn(_lastCount == 1 ? "再抽一次" : "再抽十連", () => _ = Pull(_lastCount), primary: true));
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
