#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using SanGuo.Core.Meta;

namespace SanGuo.Client
{
    public static class QuestClaimBatch
    {
        public sealed class Result
        {
            public int Claimed;
            public string? Error;
        }

        public static async Task<Result> Run(QuestKind kind, Func<Task<ProfileView?>> refresh,
            Func<string, Task<BackendResult>> claimQuest, Func<int, Task<BackendResult>> claimMilestone)
        {
            var result = new Result();
            var view = await refresh();
            if (view == null) { result.Error = "network"; return result; }
            int day = Math.Max(1, Quests.DayNumber(view.Raw, view.Now));
            var ids = DemoQuests.Book.Quests.Where(q => q.Kind == kind
                && (kind != QuestKind.SevenDay || q.Day <= day)
                && !Quests.Claimed(view.Raw, q).Contains(q.Id)
                && Quests.Progress(view.Raw, q) >= q.Target).Select(q => q.Id).ToArray();
            foreach (string id in ids)
            {
                var response = await claimQuest(id);
                if (!response.Ok) { result.Error = response.Code; return result; }
                result.Claimed++;
            }
            if (kind != QuestKind.SevenDay) return result;
            view = await refresh();
            if (view == null) { result.Error = "network"; return result; }
            int points = Quests.SevenDayPoints(view.Raw);
            foreach (var milestone in DemoQuests.Book.Milestones.OrderBy(m => m.Points))
            {
                if (points < milestone.Points || view.Raw.SevenDayClaimed.Contains("milestone:" + milestone.Points)) continue;
                var response = await claimMilestone(milestone.Points);
                if (!response.Ok) { result.Error = response.Code; return result; }
                result.Claimed++;
            }
            return result;
        }
    }
}
