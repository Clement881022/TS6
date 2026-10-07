#nullable enable
using System;
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

        protected override void BuildBody(VisualElement body)
        {
            var v = GameSession.View;
            var book = DemoQuests.Book;
            var p = v.Raw;
            int day = Quests.DayNumber(p, v.Now);

            var side = new VisualElement();
            side.AddToClassList("quest-side");
            side.Add(SideTab("每日任務", false));
            side.Add(SideTab($"七日目標 第{Math.Min(day, Quests.SevenDays)}天", true));
            body.Add(side);

            var main = new VisualElement();
            main.AddToClassList("quest-main");
            body.Add(main);

            if (_sevenDayTab)
            {
                int points = Quests.SevenDayPoints(p);
                var ms = new VisualElement();
                ms.AddToClassList("bpanel");
                ms.AddToClassList("quest-ms");
                ms.Add(UiKit.Text($"里程碑獎勵　目前 {points} 點", "bpanel-title"));
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
                    if (claimed) tier.Add(UiKit.DoneBtn("已領"));
                    else if (ready) tier.Add(UiKit.Btn("領取", () => _ = Act(() => GameSession.Backend.ClaimMilestone(need), "已領取"), primary: true));
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

            if (!_sevenDayTab)
            {
                foreach (var q in book.Quests.Where(x => x.Kind == QuestKind.Daily)) AddQuestCard(scroll, q, p);
                return;
            }
            foreach (var q in book.Quests.Where(x => x.Kind == QuestKind.SevenDay && x.Day <= Math.Max(day, 1))) AddQuestCard(scroll, q, p);
            int later = book.Quests.Count(x => x.Kind == QuestKind.SevenDay && x.Day > day);
            if (later > 0) scroll.Add(UiKit.Text($"還有 {later} 項任務會在之後幾天開放", "line-sub"));
        }

        private Button SideTab(string text, bool seven)
        {
            var b = UiKit.Tab(text, () => { _sevenDayTab = seven; Rebuild(); }, _sevenDayTab == seven);
            b.AddToClassList("side-tab");
            return b;
        }

        private void AddQuestCard(VisualElement host, QuestDef q, PlayerProfile p)
        {
            int have = Quests.Progress(p, q);
            var claimedSet = q.Kind == QuestKind.Daily ? p.DailyTaskClaimed : p.SevenDayClaimed;
            bool claimed = claimedSet.Contains(q.Id);
            bool done = have >= q.Target;

            var card = new VisualElement();
            card.AddToClassList("bpanel");
            card.AddToClassList("quest-card2");
            if (done && !claimed) card.AddToClassList("quest-done2");
            if (claimed) card.AddToClassList("quest-claimed2");
            card.Add(UiKit.Text(q.Description, "line-title"));
            card.Add(UiKit.Bar(100f * Math.Min(have, q.Target) / Math.Max(1, q.Target), (done ? "" : "bar-gold ") + "bar-slim"));
            var bottom = new VisualElement();
            bottom.AddToClassList("quest-bottom");
            var rewards = UiKit.RewardTiles(q.Reward);
            rewards.AddToClassList("grow");
            bottom.Add(rewards);
            if (q.Kind == QuestKind.SevenDay) bottom.Add(UiKit.Badge($"+{q.Points} 點", "badge-up"));
            bottom.Add(new Label($"{Math.Min(have, q.Target)}/{q.Target}") { pickingMode = PickingMode.Ignore }.WithClass("line-sub"));
            card.Add(bottom);
            if (claimed) card.Add(UiKit.DoneBtn("已領取"));
            else if (done)
            {
                string id = q.Id;
                card.Add(UiKit.Btn("領取", () => _ = Act(() => GameSession.Backend.ClaimQuest(id), "已領取"), primary: true));
            }
            else card.Add(UiKit.DoneBtn("進行中"));
            host.Add(card);
        }
    }
}
