#nullable enable
using System;
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>任務：每日任務與七日目標（含里程碑獎勵）。</summary>
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

            var tabs = new VisualElement();
            tabs.AddToClassList("tabs");
            tabs.Add(UiKit.Tab("每日任務", () => { _sevenDayTab = false; Rebuild(); }, !_sevenDayTab));
            tabs.Add(UiKit.Tab($"七日目標（第 {Math.Min(day, Quests.SevenDays)} 天）", () => { _sevenDayTab = true; Rebuild(); }, _sevenDayTab));
            body.Add(tabs);

            if (!_sevenDayTab)
            {
                var dailyGrid = UiKit.CardGrid();
                body.Add(dailyGrid);
                foreach (var q in book.Quests.Where(x => x.Kind == QuestKind.Daily))
                    AddQuestRow(dailyGrid, q, p);
                return;
            }

            int points = Quests.SevenDayPoints(p);
            body.Add(UiKit.Section($"里程碑獎勵　目前 {points} 點"));
            var ms = new VisualElement();
            ms.AddToClassList("tier-row");
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
                tier.Add(UiKit.Text(UiText.RewardText(m.Reward), "reward-text"));
                if (claimed) tier.Add(UiKit.DoneBtn("已領"));
                else if (ready) tier.Add(UiKit.Btn("領取", () => _ = Act(() => GameSession.Backend.ClaimMilestone(need), "已領取"), primary: true));
                else tier.Add(UiKit.Bar(100f * points / need, "bar-gold bar-slim"));
                ms.Add(tier);
            }
            body.Add(ms);
            body.Add(UiKit.Section("七日任務"));
            var sevenGrid = UiKit.CardGrid();
            body.Add(sevenGrid);
            foreach (var q in book.Quests.Where(x => x.Kind == QuestKind.SevenDay && x.Day <= Math.Max(day, 1)))
                AddQuestRow(sevenGrid, q, p);
            int later = book.Quests.Count(x => x.Kind == QuestKind.SevenDay && x.Day > day);
            if (later > 0) body.Add(UiKit.Text($"還有 {later} 項任務會在之後幾天開放", "txt-dim"));
        }

        private void AddQuestRow(VisualElement body, QuestDef q, PlayerProfile p)
        {
            int have = Quests.Progress(p, q);
            var claimedSet = q.Kind == QuestKind.Daily ? p.DailyTaskClaimed : p.SevenDayClaimed;
            bool claimed = claimedSet.Contains(q.Id);
            bool done = have >= q.Target;

            var card = UiKit.Panel("quest-card");
            if (done && !claimed) card.AddToClassList("quest-done");
            string extra = q.Kind == QuestKind.SevenDay ? $"　+{q.Points} 點" : "";
            var text = new VisualElement();
            text.AddToClassList("grow");
            text.Add(UiKit.Text(q.Description));
            text.Add(UiKit.Bar(100f * Math.Min(have, q.Target) / Math.Max(1, q.Target), (done ? "" : "bar-gold ") + "bar-slim"));
            text.Add(UiKit.Text($"{Math.Min(have, q.Target)}/{q.Target}　獎勵：{UiText.RewardText(q.Reward)}{extra}", "reward-text"));
            card.Add(text);
            if (claimed) card.Add(UiKit.DoneBtn("已領取"));
            else if (done)
            {
                string id = q.Id;
                card.Add(UiKit.Btn("領取", () => _ = Act(() => GameSession.Backend.ClaimQuest(id), "已領取"), primary: true));
            }
            else card.Add(UiKit.DoneBtn("進行中"));
            body.Add(card);
        }
    }
}
