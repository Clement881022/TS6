#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>招募：卡池切換、機率公示、保底資訊、單抽 / 十連與本次結果。</summary>
    public sealed class GachaPage : PageBase
    {
        private readonly List<GachaPool> _pools = DemoMeta.Pools();
        private string _poolId = DemoMeta.NewbiePoolId;
        private List<PullResult>? _last;

        protected override Page Id => Page.Gacha;
        protected override string Title => "招募";

        /// <summary>截圖 / 除錯用：直接抽一次十連。</summary>
        public Task DebugTenPull() => Pull(10);

        private string NameOf(string heroId) => GameSession.DefOf(heroId)?.Name ?? heroId;

        protected override void BuildBody(VisualElement body)
        {
            var tabs = new VisualElement();
            tabs.AddToClassList("tabs");
            foreach (var p in _pools)
            {
                var pool = p;
                tabs.Add(UiKit.Tab(pool.Name, () => { _poolId = pool.Id; _last = null; Rebuild(); }, pool.Id == _poolId));
            }
            body.Add(tabs);

            var current = _pools.Find(p => p.Id == _poolId) ?? _pools[0];
            GameSession.View.Pools.TryGetValue(current.Id, out var state);

            var info = UiKit.Panel();
            info.Add(UiKit.Text("機率公示", "txt-sub"));
            // 機率公示：與實際機率由同一份資料產生。
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
            if (pity.Length > 0) info.Add(UiKit.Text("保底：" + pity, "txt-dim"));
            if (current.HardPityUr > 0)
            {
                int since = state?.PullsSinceUr ?? 0;
                info.Add(UiKit.Text($"距離硬保底還有 {current.HardPityUr - since} 抽（累計 {state?.TotalPulls ?? 0} 抽）", "txt-dim"));
                info.Add(UiKit.Bar(100f * since / current.HardPityUr, "bar-gold bar-slim"));
            }
            if (current.FirstTenGuaranteesUr && (state?.TenPulls ?? 0) == 0)
                info.Add(UiKit.Text("首次十連必定獲得 1 名 UR", "txt-warn"));
            body.Add(info);

            var row = new VisualElement();
            row.AddToClassList("pull-row");
            row.Add(UiKit.Btn($"單抽　{current.SingleCost} 元寶", () => _ = Pull(1)).WithClass("btn-wide"));
            row.Add(UiKit.Btn($"十連　{current.TenCost} 元寶", () => _ = Pull(10), primary: true).WithClass("btn-wide"));
            body.Add(row);

            if (_last != null)
            {
                body.Add(UiKit.Section("本次結果"));
                var grid = new VisualElement();
                grid.AddToClassList("pull-grid");
                foreach (var r in _last)
                {
                    string rc = UiKit.RarityClass(r.Rarity);
                    var card = new VisualElement();
                    card.AddToClassList("pull-card");
                    card.AddToClassList("pull-card-" + rc);
                    card.Add(UiKit.Text(r.Rarity.ToString(), "pull-rarity pull-rarity-" + rc));
                    card.Add(UiKit.Text(NameOf(r.HeroId), "pull-card-name"));
                    var tags = UiKit.Row("row-center");
                    if (r.IsNew) tags.Add(UiKit.Badge("NEW", "badge-new"));
                    else if (r.Shards > 0) tags.Add(UiKit.Text($"碎片 +{r.Shards}", "pull-card-sub"));
                    if (r.IsUp) tags.Add(UiKit.Badge("UP", "badge-up"));
                    card.Add(tags);
                    grid.Add(card);
                }
                body.Add(grid);
            }
        }

        private Task Pull(int count) => Act(async () =>
        {
            var r = await GameSession.Backend.Pull(_poolId, count);
            if (r.Ok) _last = r.Results;
            return (BackendResult)r;
        });
    }
}
