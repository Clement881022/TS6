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

            var tabs = UiKit.Row("row-center");
            tabs.Add(UiKit.Btn("每日任務", () => { _sevenDayTab = false; Rebuild(); }, on: !_sevenDayTab));
            tabs.Add(UiKit.Btn($"七日目標（第 {Math.Min(day, Quests.SevenDays)} 天）", () => { _sevenDayTab = true; Rebuild(); }, on: _sevenDayTab));
            body.Add(tabs);

            if (!_sevenDayTab)
            {
                foreach (var q in book.Quests.Where(x => x.Kind == QuestKind.Daily))
                    AddQuestRow(body, q, p);
                return;
            }

            int points = Quests.SevenDayPoints(p);
            body.Add(UiKit.Text($"目標點數 {points}（領取任務獎勵後累計）"));
            var ms = UiKit.Row("row-center");
            foreach (var m in book.Milestones)
            {
                int need = m.Points;
                bool claimed = p.SevenDayClaimed.Contains("milestone:" + need);
                string label = $"{need} 點：{UiText.RewardText(m.Reward)}";
                if (claimed) ms.Add(UiKit.Btn(label + "（已領）", () => { }));
                else ms.Add(UiKit.Btn(label + (points >= need ? "　領取" : ""),
                    () => _ = Act(() => GameSession.Backend.ClaimMilestone(need), "已領取"), primary: points >= need));
            }
            body.Add(ms);
            foreach (var q in book.Quests.Where(x => x.Kind == QuestKind.SevenDay && x.Day <= Math.Max(day, 1)))
                AddQuestRow(body, q, p);
            int later = book.Quests.Count(x => x.Kind == QuestKind.SevenDay && x.Day > day);
            if (later > 0) body.Add(UiKit.Text($"還有 {later} 項任務會在之後幾天開放", "txt-dim"));
        }

        private void AddQuestRow(VisualElement body, QuestDef q, PlayerProfile p)
        {
            int have = Quests.Progress(p, q);
            var claimedSet = q.Kind == QuestKind.Daily ? p.DailyTaskClaimed : p.SevenDayClaimed;
            bool claimed = claimedSet.Contains(q.Id);
            bool done = have >= q.Target;

            var card = UiKit.Panel("panel-row");
            string extra = q.Kind == QuestKind.SevenDay ? $"　+{q.Points} 點" : "";
            var text = new VisualElement();
            text.AddToClassList("grow");
            text.Add(UiKit.Text(q.Description));
            text.Add(UiKit.Text($"{have}/{q.Target}　獎勵：{UiText.RewardText(q.Reward)}{extra}", "txt-dim"));
            card.Add(text);
            if (claimed) card.Add(UiKit.Btn("已領取", () => { }));
            else if (done)
            {
                string id = q.Id;
                card.Add(UiKit.Btn("領取", () => _ = Act(() => GameSession.Backend.ClaimQuest(id), "已領取"), primary: true));
            }
            body.Add(card);
        }
    }
}
