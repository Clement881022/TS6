#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>任務：左側分頁（每日 / 七日目標），右側任務卡兩欄排列；七日目標上方有里程碑獎勵。</summary>
    public sealed class QuestsPage : PageBase
    {
        private bool _sevenDayTab;

        protected override Page Id => Page.Quests;
        protected override string Title => "任務";

        /// <summary>截圖 / 除錯用：切到七日目標分頁。</summary>
        public void DebugShowSevenDay()
        {
            _sevenDayTab = true;
            Rebuild();
        }

        protected override void BuildBody(VisualElement body)
        {
            var v = GameSession.View;
            var book = DemoQuests.Book;
            var p = v.Raw;
            int day = Quests.DayNumber(p, v.Now);

            bool dailyReady = book.Quests.Any(q => q.Kind == QuestKind.Daily && Claimable(p, q));
            bool sevenReady = book.Quests.Any(q => q.Kind == QuestKind.SevenDay && q.Day <= Math.Max(day, 1) && Claimable(p, q))
                || book.Milestones.Any(m => Quests.SevenDayPoints(p) >= m.Points && !p.SevenDayClaimed.Contains("milestone:" + m.Points));

            var side = new VisualElement();
            side.AddToClassList("quest-side");
            side.Add(SideTab("每日任務", false, dailyReady));
            side.Add(SideTab("七日目標", true, sevenReady));
            body.Add(side);

            var main = new VisualElement();
            main.AddToClassList("quest-main");
            body.Add(main);

            if (_sevenDayTab)
            {
                int points = Quests.SevenDayPoints(p);
                main.Add(UiKit.Text($"第 {Math.Min(day, Quests.SevenDays)} 天　/　共 {Quests.SevenDays} 天", "quest-day"));
                var ms = new VisualElement();
                ms.AddToClassList("bpanel");
                ms.AddToClassList("quest-ms");
                ms.Add(UiKit.Text($"里程碑獎勵　{points} 點", "bpanel-title"));
                var tiers = new VisualElement();
                tiers.AddToClassList("tier-row");
                foreach (var m in book.Milestones)
                {
                    int need = m.Points;
                    bool claimed = p.SevenDayClaimed.Contains("milestone:" + need);
                    bool ready = points >= need;
                    var tier = new VisualElement();
                    tier.AddToClassList("tier");
                    if (claimed) tier.AddToClassList("tier-claimed");
                    else if (ready) tier.AddToClassList("tier-ready");
                    tier.Add(UiKit.Text($"{need} 點", "tier-title"));
                    tier.Add(UiKit.RewardTiles(m.Reward));
                    if (claimed) tier.Add(UiKit.DoneBtn("已領").WithClass("btn-sm"));
                    else if (ready) tier.Add(UiKit.Btn("領取", () => _ = Act(() => GameSession.Backend.ClaimMilestone(need), "已領取"), primary: true).WithClass("btn-sm"));
                    else tier.Add(UiKit.Bar(100f * points / need, "bar-gold bar-slim"));
                    tiers.Add(tier);
                }
                ms.Add(tiers);
                main.Add(ms);
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            scroll.contentContainer.AddToClassList("quest-grid");
            main.Add(scroll);

            // 可領取的排最前面，其次進行中，已領取的沉到最後。
            IEnumerable<QuestDef> list = _sevenDayTab
                ? book.Quests.Where(x => x.Kind == QuestKind.SevenDay && x.Day <= Math.Max(day, 1))
                : book.Quests.Where(x => x.Kind == QuestKind.Daily);
            foreach (var q in list.OrderBy(x => Claimable(p, x) ? 0 : IsClaimed(p, x) ? 2 : 1)) AddQuestCard(scroll, q, p);
            if (_sevenDayTab)
            {
                int later = book.Quests.Count(x => x.Kind == QuestKind.SevenDay && x.Day > day);
                if (later > 0) scroll.Add(UiKit.Text($"另有 {later} 項任務將於之後開放", "quest-more"));
            }
        }

        private static bool IsClaimed(PlayerProfile p, QuestDef q) =>
            (q.Kind == QuestKind.Daily ? p.DailyTaskClaimed : p.SevenDayClaimed).Contains(q.Id);

        private static bool Claimable(PlayerProfile p, QuestDef q) => !IsClaimed(p, q) && Quests.Progress(p, q) >= q.Target;

        private Button SideTab(string text, bool seven, bool reddot)
        {
            var b = UiKit.Tab(text, () => { _sevenDayTab = seven; Rebuild(); }, _sevenDayTab == seven);
            b.AddToClassList("side-tab");
            return b.RedDot(reddot);
        }

        /// <summary>任務卡：左獎勵、中描述 + 進度條、右操作按鈕（寬度固定，不隨文字伸縮）。</summary>
        private void AddQuestCard(VisualElement host, QuestDef q, PlayerProfile p)
        {
            int have = Math.Min(Quests.Progress(p, q), q.Target);
            bool claimed = IsClaimed(p, q);
            bool done = have >= q.Target;

            var card = new VisualElement();
            card.AddToClassList("bpanel");
            card.AddToClassList("quest-card2");
            if (done && !claimed) card.AddToClassList("quest-done2");
            if (claimed) card.AddToClassList("quest-claimed2");

            var rewards = UiKit.RewardTiles(q.Reward);
            rewards.AddToClassList("quest-rewards");
            card.Add(rewards);

            var info = new VisualElement { pickingMode = PickingMode.Ignore };
            info.AddToClassList("quest-info");
            info.Add(new Label(q.Description) { pickingMode = PickingMode.Ignore }.WithClass("quest-desc"));
            info.Add(UiKit.Bar(100f * have / Math.Max(1, q.Target), done ? "" : "bar-gold"));
            var meta = new VisualElement { pickingMode = PickingMode.Ignore };
            meta.AddToClassList("quest-meta");
            meta.Add(new Label($"{have}/{q.Target}") { pickingMode = PickingMode.Ignore }.WithClass("quest-count"));
            if (q.Kind == QuestKind.SevenDay) meta.Add(UiKit.Badge($"+{q.Points} 點", "badge-up"));
            info.Add(meta);
            card.Add(info);

            var action = new VisualElement();
            action.AddToClassList("quest-action");
            if (claimed) action.Add(UiKit.DoneBtn("已領取").WithClass("btn-sm"));
            else if (done)
            {
                string id = q.Id;
                action.Add(UiKit.Btn("領取", () => _ = Act(() => GameSession.Backend.ClaimQuest(id), "已領取"), primary: true).WithClass("btn-sm"));
            }
            else action.Add(UiKit.DoneBtn("進行中").WithClass("btn-sm"));
            card.Add(action);
            host.Add(card);
        }
    }
}
