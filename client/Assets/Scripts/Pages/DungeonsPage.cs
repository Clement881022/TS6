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
            body.AddToClassList("page-centered");

            var row = new VisualElement();
            row.AddToClassList("dun-row");
            body.Add(row);
            foreach (var d in _dungeons)
            {
                var dungeon = d;
                bool open = d.IsOpen(v.Now);
                bool levelOk = v.Level >= d.MinPlayerLevel;
                int left = ResourceDungeons.Remaining(v.Raw, d, v.Now);
                bool cleared = v.ClearedStages.Contains(d.Id);
                string days = "週" + string.Join("", d.Weekdays.Select(x => WeekdayNames[x]));
                SplitName(d.Name, out string title, out string output);

                var card = new VisualElement();
                card.AddToClassList("dun-card");
                if (!open) card.AddToClassList("dun-closed");

                var head = new VisualElement { pickingMode = PickingMode.Ignore };
                head.AddToClassList("dun-head");
                head.Add(UiKit.Text(title, "dun-name"));
                if (output.Length > 0) head.Add(UiKit.Text("產出：" + output, "dun-sub"));
                card.Add(head);

                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("dun-art");
                art.Add(UiKit.RewardTiles(d.Reward).WithClass("dun-reward"));
                card.Add(art);

                var body2 = new VisualElement { pickingMode = PickingMode.Ignore };
                body2.AddToClassList("card-body");
                var tags = new VisualElement { pickingMode = PickingMode.Ignore };
                tags.AddToClassList("dun-meta");
                tags.Add(UiKit.Badge(open ? "今日開放" : "未開放", open ? "badge-up" : ""));
                tags.Add(UiKit.Badge(days, "badge-role"));
                body2.Add(tags);
                if (!levelOk) body2.Add(UiKit.Text($"帳號 Lv.{d.MinPlayerLevel} 解鎖", "dun-lock"));
                else body2.Add(UiKit.Text($"今日剩餘 {left}/{d.DailyLimit}", "line-sub").WithClass("dun-center"));
                var cost = UiKit.Cost("item_stamina", d.StaminaCost, v.Stamina);
                cost.AddToClassList("dun-cost");
                body2.Add(cost);
                card.Add(body2);

                var btns = new VisualElement();
                btns.AddToClassList("card-footer");
                if (!open) btns.Add(UiKit.DoneBtn($"{NextOpen(d, v.Now)}開放"));
                else if (!levelOk) btns.Add(UiKit.DoneBtn($"Lv.{d.MinPlayerLevel} 解鎖"));
                else if (left <= 0) btns.Add(UiKit.DoneBtn("今日次數已用完"));
                else
                {
                    btns.Add(UiKit.Btn("挑戰", () => EnterDungeon(dungeon.Id), primary: true));
                    if (cleared)
                    {
                        btns.Add(UiKit.Btn("掃蕩 ×1", () => _ = Act(() => GameSession.Backend.SweepDungeon(dungeon.Id, 1), "掃蕩完成")).WithClass("btn-sm"));
                        btns.Add(UiKit.Btn($"掃蕩 ×{ResourceDungeons.MaxSweepCount}",
                            () => _ = Act(() => GameSession.Backend.SweepDungeon(dungeon.Id, ResourceDungeons.MaxSweepCount), "掃蕩完成")).WithClass("btn-sm"));
                    }
                }
                card.Add(btns);
                row.Add(card);
            }
        }

        /// <summary>「糧倉護衛（金幣）」→ 標題「糧倉護衛」、產出「金幣」。</summary>
        private static void SplitName(string name, out string title, out string output)
        {
            int i = name.IndexOf('（');
            if (i < 0 || !name.EndsWith("）")) { title = name; output = ""; return; }
            title = name.Substring(0, i);
            output = name.Substring(i + 1, name.Length - i - 2);
        }

        /// <summary>下一個開放日（「明天」或「週三」）。</summary>
        private static string NextOpen(ResourceDungeonDef d, long now)
        {
            int today = DailyClock.Weekday(now);
            for (int i = 1; i <= 7; i++)
            {
                int w = (today + i) % 7;
                if (d.Weekdays.Contains(w)) return i == 1 ? "明天" : "週" + WeekdayNames[w];
            }
            return "";
        }
    }
}
