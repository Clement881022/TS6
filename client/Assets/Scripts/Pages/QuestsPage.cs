#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public sealed class QuestsPage : PageBase
    {
        private bool _claiming;
        private int? _reviewMilestonePoints;
        private bool _reviewMilestonesClaimed;
        public void DebugReviewMilestones(int? points, bool claimed = false)
        {
            _reviewMilestonePoints = points; _reviewMilestonesClaimed = claimed; _tab = QuestKind.SevenDay; Rebuild();
        }
        private QuestKind _tab = QuestKind.Daily;
        private bool _sevenDayTab => _tab == QuestKind.SevenDay;

        protected override Page Id => Page.Quests;
        protected override string Title => "任務";

        public void DebugShowSevenDay()
        {
            _tab = QuestKind.SevenDay;
            Rebuild();
        }

        protected override void BuildBody(VisualElement body)
        {
            var v = GameSession.View;
            var book = DemoQuests.Book;
            var p = v.Raw;
            int day = Quests.DayNumber(p, v.Now);

            bool dailyReady = book.Quests.Any(q => q.Kind == QuestKind.Daily && Claimable(p, q));
            bool weeklyReady = book.Quests.Any(q => q.Kind == QuestKind.Weekly && Claimable(p, q));
            bool sevenReady = book.Quests.Any(q => q.Kind == QuestKind.SevenDay && q.Day <= Math.Max(day, 1) && Claimable(p, q))
                || book.Milestones.Any(m => Quests.SevenDayPoints(p) >= m.Points && !p.SevenDayClaimed.Contains("milestone:" + m.Points));

            var frame = new VisualElement().WithClass("quest-frame");
            body.Add(frame);
            var side = new VisualElement();
            side.AddToClassList("quest-side");
            var beginner = SideTab("七日目標", QuestKind.SevenDay, sevenReady);
            beginner.AddToClassList("quest-beginner-tab");
            beginner.Add(UiKit.Text("新手！", "quest-beginner"));
            side.Add(beginner);
            side.Add(SideTab("每日任務", QuestKind.Daily, dailyReady));
            side.Add(SideTab("每週任務", QuestKind.Weekly, weeklyReady));
            frame.Add(side);

            var main = new VisualElement();
            main.AddToClassList("quest-main");
            frame.Add(main);

            if (_sevenDayTab)
            {
                int points = Quests.SevenDayPoints(p);
                main.Add(UiKit.Text($"第 {Math.Min(day, Quests.SevenDays)} 天　/　共 {Quests.SevenDays} 天", "quest-day"));
                main.Add(BuildMilestones(p, _reviewMilestonePoints ?? points));
            }
            bool any = book.Quests.Any(q => q.Kind == _tab && (!_sevenDayTab || q.Day <= Math.Max(day, 1)) && Claimable(p, q))
                || (_sevenDayTab && book.Milestones.Any(m => Quests.SevenDayPoints(p) >= m.Points && !p.SevenDayClaimed.Contains("milestone:" + m.Points)));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            scroll.contentContainer.AddToClassList("quest-grid");
            main.Add(scroll);

            IEnumerable<QuestDef> list = _sevenDayTab
                ? book.Quests.Where(x => x.Kind == QuestKind.SevenDay && x.Day <= Math.Max(day, 1))
                : book.Quests.Where(x => x.Kind == _tab);
            foreach (var q in list.OrderBy(x => Claimable(p, x) ? 0 : IsClaimed(p, x) ? 2 : 1)) AddQuestCard(scroll, q, p);
            if (_sevenDayTab)
            {
                int later = book.Quests.Count(x => x.Kind == QuestKind.SevenDay && x.Day > day);
                if (later > 0) scroll.Add(UiKit.Text($"另有 {later} 項任務將於之後開放", "quest-more"));
            }
            var footer = UiKit.Row("quest-footer");
            var claim = UiKit.Btn("一鍵領取", () => _ = ClaimAll(), primary: true).WithClass("quest-claim-all");
            claim.SetEnabled(any && !_claiming);
            footer.Add(claim);
            main.Add(footer);
        }

        private VisualElement BuildMilestones(PlayerProfile p, int points)
        {
            var panel = new VisualElement().WithClass("milestone-panel");
            panel.Add(UiKit.Text($"{points} / {DemoQuests.Book.Milestones.Max(m => m.Points)}", "milestone-points"));
            var track = new VisualElement().WithClass("milestone-track");
            int maximum = DemoQuests.Book.Milestones.Max(m => m.Points);
            track.Add(UiKit.Bar(100f * points / maximum, "bar-gold milestone-line"));
            foreach (var m in DemoQuests.Book.Milestones)
            {
                int threshold = m.Points;
                bool claimed = _reviewMilestonePoints.HasValue ? _reviewMilestonesClaimed && points >= threshold : p.SevenDayClaimed.Contains("milestone:" + threshold);
                bool ready = points >= threshold;
                var node = UiKit.Btn("", () =>
                {
                    if (ready && !claimed) _ = Act(() => GameSession.Backend.ClaimMilestone(threshold), "已領取");
                    else UiHelp.Dialog(Host, $"{threshold} 點獎勵", body => body.Add(UiKit.RewardTiles(m.Reward)));
                }).WithClass("milestone-node");
                node.style.left = UnityEngine.UIElements.Length.Percent(100f * threshold / maximum);
                node.AddToClassList(claimed ? "milestone-claimed" : ready ? "milestone-ready" : "milestone-locked");
                if (m.Reward.Heroes.Count > 0) node.Add(PortraitArt.Create(m.Reward.Heroes[0], "milestone-hero"));
                else node.Add(UiKit.ItemTile(m.Reward.Yuanbao > 0 ? "item_yuanbao" : "item_chest"));
                node.Add(UiKit.Text(threshold.ToString(), "milestone-threshold"));
                node.Add(UiKit.Text(claimed ? "✓" : ready ? "領取" : "", "milestone-state"));
                track.Add(node);
            }
            panel.Add(track);
            return panel;
        }

        private async Task ClaimAll()
        {
            if (_claiming || Busy) return;
            _claiming = true;
            var pageRoot = Host.parent;
            pageRoot.SetEnabled(false);
            try
            {
                var backend = GameSession.Backend;
                var result = await QuestClaimBatch.Run(_tab, backend.GetProfile, backend.ClaimQuest, backend.ClaimMilestone);
                await GameSession.Refresh();
                if (this == null || PageHost.Current?.ActivePage != this) return;
                Rebuild();
                Toast(result.Error != null ? $"已領取 {result.Claimed} 項 · {UiText.ExplainBackend(result.Error)}" : result.Claimed > 0 ? $"已領取 {result.Claimed} 項獎勵" : "沒有可領取的獎勵");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                await GameSession.Refresh();
                if (this != null && PageHost.Current?.ActivePage == this) { Rebuild(); Toast(UiText.ExplainBackend("network")); }
            }
            finally
            {
                _claiming = false;
                pageRoot.SetEnabled(true);
                if (this != null && PageHost.Current?.ActivePage == this) Rebuild();
            }
        }

        private static bool IsClaimed(PlayerProfile p, QuestDef q) => Quests.Claimed(p, q).Contains(q.Id);

        private static bool Claimable(PlayerProfile p, QuestDef q) => !IsClaimed(p, q) && Quests.Progress(p, q) >= q.Target;

        private Button SideTab(string text, QuestKind kind, bool reddot)
        {
            var b = UiKit.Tab(text, () => { _tab = kind; Rebuild(); }, _tab == kind);
            b.AddToClassList("side-tab");
            return b.RedDot(reddot);
        }

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
