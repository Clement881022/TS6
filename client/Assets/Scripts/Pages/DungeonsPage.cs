#nullable enable
using System.Collections.Generic;
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>資源副本：每日輪替、每天固定次數；三張直立大卡，通關一次後可掃蕩。</summary>
    public sealed class DungeonsPage : PageBase
    {
        private static readonly string[] WeekdayNames = { "一", "二", "三", "四", "五", "六", "日" };
        private readonly List<ResourceDungeonDef> _dungeons = DemoResourceDungeons.Create();

        protected override Page Id => Page.Dungeons;
        protected override string Title => "資源副本";

        protected override void BuildBody(VisualElement body)
        {
            var v = GameSession.View;
            body.style.flexDirection = FlexDirection.Column;
            var hint = UiKit.Hint("每日輪替、每天固定次數；通關一次後可掃蕩");
            hint.AddToClassList("form-hint");
            body.Add(hint);

            var row = new VisualElement();
            row.AddToClassList("dun-row");
            body.Add(row);
            foreach (var d in _dungeons)
            {
                var dungeon = d;
                bool open = d.IsOpen(v.Now);
                int left = ResourceDungeons.Remaining(v.Raw, d, v.Now);
                bool cleared = v.ClearedStages.Contains(d.Id);
                string days = "週" + string.Join("", d.Weekdays.Select(x => WeekdayNames[x]));

                var card = new VisualElement();
                card.AddToClassList("dun-card");
                if (!open) card.AddToClassList("dun-closed");

                var head = new VisualElement { pickingMode = PickingMode.Ignore };
                head.AddToClassList("dun-head");
                head.Add(UiKit.Text(d.Name, "dun-name"));
                card.Add(head);

                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("dun-art");
                art.Add(UiKit.RewardTiles(d.Reward).WithClass("dun-reward"));
                card.Add(art);

                var tags = UiKit.Row("row-center");
                tags.Add(UiKit.Badge(open ? "今日開放" : "今日未開放", open ? "badge-up" : ""));
                tags.Add(UiKit.Badge(days, "badge-role"));
                card.Add(tags);
                card.Add(UiKit.Text($"剩餘 {left}/{d.DailyLimit}　需帳號 Lv.{d.MinPlayerLevel}", "line-sub").WithClass("dun-center"));

                var cost = new VisualElement { pickingMode = PickingMode.Ignore };
                cost.AddToClassList("cost-chip");
                cost.AddToClassList("dun-cost");
                if (v.Stamina < d.StaminaCost) cost.AddToClassList("cost-chip-bad");
                cost.Add(UiKit.ItemTile("item_stamina"));
                cost.Add(new Label(d.StaminaCost.ToString()) { pickingMode = PickingMode.Ignore }.WithClass("cost-chip-text"));
                card.Add(cost);

                var btns = new VisualElement();
                btns.AddToClassList("dun-buttons");
                if (open)
                {
                    btns.Add(UiKit.Btn("挑戰", () => EnterDungeon(dungeon.Id), primary: true));
                    if (cleared)
                    {
                        btns.Add(UiKit.Btn("掃蕩 ×1", () => _ = Act(() => GameSession.Backend.SweepDungeon(dungeon.Id, 1), "掃蕩完成")));
                        btns.Add(UiKit.Btn($"×{ResourceDungeons.MaxSweepCount}",
                            () => _ = Act(() => GameSession.Backend.SweepDungeon(dungeon.Id, ResourceDungeons.MaxSweepCount), "掃蕩完成")));
                    }
                }
                else btns.Add(UiKit.DoneBtn("今日未開放"));
                card.Add(btns);
                row.Add(card);
            }
        }
    }
}
