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
            var tabs = UiKit.Row("row-center");
            foreach (var p in _pools)
            {
                var pool = p;
                tabs.Add(UiKit.Btn(pool.Name, () => { _poolId = pool.Id; _last = null; Rebuild(); }, on: pool.Id == _poolId));
            }
            body.Add(tabs);

            var current = _pools.Find(p => p.Id == _poolId) ?? _pools[0];
            GameSession.View.Pools.TryGetValue(current.Id, out var state);

            var info = UiKit.Panel();
            // 機率公示：與實際機率由同一份資料產生。
            var rates = current.DisclosedRates();
            info.Add(UiKit.Text("機率公示：" + string.Join("　", rates.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key} {kv.Value:0.##}%"))));
            string pity = current.PityDescription();
            if (pity.Length > 0) info.Add(UiKit.Text("保底：" + pity, "txt-dim"));
            if (current.HardPityUr > 0)
                info.Add(UiKit.Text($"距離硬保底還有 {current.HardPityUr - (state?.PullsSinceUr ?? 0)} 抽（累計 {state?.TotalPulls ?? 0} 抽）", "txt-dim"));
            if (current.FirstTenGuaranteesUr && (state?.TenPulls ?? 0) == 0)
                info.Add(UiKit.Text("首次十連必定獲得 1 名 UR", "txt-warn"));
            body.Add(info);

            var row = UiKit.Row("row-center");
            row.Add(UiKit.Btn($"單抽（{current.SingleCost} 元寶）", () => _ = Pull(1)));
            row.Add(UiKit.Btn($"十連（{current.TenCost} 元寶）", () => _ = Pull(10), primary: true));
            body.Add(row);

            if (_last != null)
            {
                body.Add(UiKit.Text("本次結果", "txt-sub"));
                var list = UiKit.Row("row-center");
                foreach (var r in _last)
                {
                    string label = $"{r.Rarity} {NameOf(r.HeroId)}";
                    if (r.IsNew) label += "　NEW";
                    else if (r.Shards > 0) label += $"　碎片 +{r.Shards}";
                    if (r.IsUp) label += "　UP";
                    var chip = UiKit.Btn(label, () => { });
                    chip.AddToClassList("formation-chip");
                    if (r.Rarity == Rarity.UR) chip.AddToClassList("btn-primary");
                    list.Add(chip);
                }
                body.Add(list);
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
