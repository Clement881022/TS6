#nullable enable
using System.Collections.Generic;
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>資源副本：每日輪替、每天固定次數；通關一次後可掃蕩。</summary>
    public sealed class DungeonsPage : PageBase
    {
        private static readonly string[] WeekdayNames = { "一", "二", "三", "四", "五", "六", "日" };
        private readonly List<ResourceDungeonDef> _dungeons = DemoResourceDungeons.Create();

        protected override Page Id => Page.Dungeons;
        protected override string Title => "資源副本";

        protected override void BuildBody(VisualElement body)
        {
            var v = GameSession.View;
            body.Add(UiKit.Hint($"體力 {v.Stamina}/{v.StaminaCap}　每日輪替、每天固定次數；通關一次後可掃蕩"));

            var grid = UiKit.CardGrid();
            body.Add(grid);
            foreach (var d in _dungeons)
            {
                var dungeon = d;
                bool open = d.IsOpen(v.Now);
                int left = ResourceDungeons.Remaining(v.Raw, d, v.Now);
                bool cleared = v.ClearedStages.Contains(d.Id);
                string days = "週" + string.Join("", d.Weekdays.Select(x => WeekdayNames[x]));

                var card = UiKit.Panel();
                if (!open) card.AddToClassList("panel-dim");
                card.Add(UiKit.Text(d.Name, "txt-sub"));
                var tags = UiKit.Row();
                tags.Add(UiKit.Badge(open ? "今日開放" : "今日未開放", open ? "badge-up" : ""));
                tags.Add(UiKit.Badge(days, "badge-role"));
                card.Add(tags);
                card.Add(UiKit.Text($"剩餘 {left}/{d.DailyLimit}　體力 {d.StaminaCost}　需帳號 Lv.{d.MinPlayerLevel}", "txt-dim"));
                card.Add(UiKit.Bar(d.DailyLimit == 0 ? 0 : 100f * left / d.DailyLimit, "bar-gold bar-slim"));
                card.Add(UiKit.Text("獎勵：" + UiText.RewardText(d.Reward), "reward-text").WithClass("grow"));
                var btns = UiKit.Row();
                if (open)
                {
                    btns.Add(UiKit.Btn("挑戰", () => _ = StartBattle(dungeon.Id), primary: true));
                    if (cleared)
                    {
                        btns.Add(UiKit.Btn("掃蕩 ×1", () => _ = Act(() => GameSession.Backend.SweepDungeon(dungeon.Id, 1), "掃蕩完成")));
                        btns.Add(UiKit.Btn($"掃蕩 ×{ResourceDungeons.MaxSweepCount}",
                            () => _ = Act(() => GameSession.Backend.SweepDungeon(dungeon.Id, ResourceDungeons.MaxSweepCount), "掃蕩完成")));
                    }
                }
                card.Add(btns);
                grid.Add(card);
            }
        }
    }
}
