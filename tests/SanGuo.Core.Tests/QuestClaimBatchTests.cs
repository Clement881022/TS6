using SanGuo.Client;
using SanGuo.Core.Meta;

namespace SanGuo.Core.Tests;

public class QuestClaimBatchTests
{
    private const long Now = 1791504000;

    private sealed class Claims
    {
        public PlayerProfile Profile = new();
        public List<string> Calls = new();
        public string? FailId;
        public int Refreshes;

        public Claims(int day = 1)
        {
            Profile.CreatedDay = DailyClock.DayIndex(Now) - day + 1;
            Profile.EnsureDaily(Now);
        }

        public Task<ProfileView?> Refresh()
        {
            Refreshes++;
            return Task.FromResult<ProfileView?>(ProfileView.From(Profile, Now));
        }

        public Task<BackendResult> Quest(string id)
        {
            Calls.Add(id);
            bool ok = id != FailId && Quests.Claim(Profile, id, Now) == QuestClaimResult.Ok;
            return Task.FromResult(new BackendResult { Ok = ok, Code = ok ? "ok" : "network" });
        }

        public Task<BackendResult> Milestone(int threshold)
        {
            Calls.Add("milestone:" + threshold);
            bool ok = Quests.ClaimMilestone(Profile, threshold, Now) == QuestClaimResult.Ok;
            return Task.FromResult(new BackendResult { Ok = ok, Code = ok ? "ok" : "not_complete" });
        }

        public Task<QuestClaimBatch.Result> Run(QuestKind kind) => QuestClaimBatch.Run(kind, Refresh, Quest, Milestone);

        public void Complete(QuestKind kind)
        {
            foreach (var q in DemoQuests.Book.Quests.Where(q => q.Kind == kind))
                (kind == QuestKind.Daily ? Profile.DailyTaskProgress : kind == QuestKind.Weekly ? Profile.WeeklyProgress : Profile.SevenDayProgress)[q.Id] = q.Target;
        }
    }

    [Fact]
    public async Task EmptyPageMakesNoClaimCalls()
    {
        var claims = new Claims();
        var result = await claims.Run(QuestKind.Daily);
        Assert.Equal(0, result.Claimed);
        Assert.Null(result.Error);
        Assert.Empty(claims.Calls);
    }

    [Fact]
    public async Task DailyDoesNotClaimOtherPagesOrPreviouslyClaimedTasks()
    {
        var claims = new Claims(7);
        claims.Complete(QuestKind.Daily);
        claims.Complete(QuestKind.Weekly);
        claims.Complete(QuestKind.SevenDay);
        var first = DemoQuests.Book.Quests.First(q => q.Kind == QuestKind.Daily);
        await claims.Quest(first.Id);
        claims.Calls.Clear();
        var result = await claims.Run(QuestKind.Daily);
        Assert.Null(result.Error);
        Assert.DoesNotContain(first.Id, claims.Calls);
        Assert.All(claims.Calls, id => Assert.Equal(QuestKind.Daily, DemoQuests.Book.Find(id)!.Kind));
        Assert.Empty(claims.Profile.WeeklyClaimed);
        Assert.Empty(claims.Profile.SevenDayClaimed);
    }

    [Fact]
    public async Task SevenDayRefreshesPointsBeforeMilestonesAndSkipsLockedDays()
    {
        var claims = new Claims(3);
        claims.Complete(QuestKind.SevenDay);
        var result = await claims.Run(QuestKind.SevenDay);
        Assert.Null(result.Error);
        Assert.Equal(2, claims.Refreshes);
        Assert.DoesNotContain(DemoQuests.Book.Quests.Where(q => q.Kind == QuestKind.SevenDay && q.Day > 3), q => claims.Calls.Contains(q.Id));
        int points = Quests.SevenDayPoints(claims.Profile);
        Assert.All(DemoQuests.Book.Milestones.Where(m => m.Points <= points), m => Assert.Contains("milestone:" + m.Points, claims.Calls));
        int firstMilestone = claims.Calls.FindIndex(id => id.StartsWith("milestone:"));
        Assert.True(firstMilestone > 0);
        Assert.All(claims.Calls.Skip(firstMilestone), id => Assert.StartsWith("milestone:", id));
    }

    [Fact]
    public async Task PartialFailureStopsAndRetryOnlyClaimsRemainingRewards()
    {
        var claims = new Claims(7);
        claims.Complete(QuestKind.SevenDay);
        var tasks = DemoQuests.Book.Quests.Where(q => q.Kind == QuestKind.SevenDay).ToArray();
        claims.FailId = tasks[1].Id;
        var first = await claims.Run(QuestKind.SevenDay);
        Assert.Equal(1, first.Claimed);
        Assert.Equal("network", first.Error);
        Assert.Equal(new[] { tasks[0].Id, tasks[1].Id }, claims.Calls);
        claims.FailId = null;
        claims.Calls.Clear();
        var second = await claims.Run(QuestKind.SevenDay);
        Assert.Null(second.Error);
        Assert.DoesNotContain(tasks[0].Id, claims.Calls);
        Assert.All(DemoQuests.Book.Milestones, m => Assert.Contains("milestone:" + m.Points, claims.Profile.SevenDayClaimed));
        claims.Calls.Clear();
        Assert.Equal(0, (await claims.Run(QuestKind.SevenDay)).Claimed);
        Assert.Empty(claims.Calls);
    }

    [Fact]
    public async Task MissingFreshProfileDoesNotIssueClaims()
    {
        int calls = 0;
        var result = await QuestClaimBatch.Run(QuestKind.SevenDay, () => Task.FromResult<ProfileView?>(null),
            _ => { calls++; return Task.FromResult(new BackendResult { Ok = true }); },
            _ => { calls++; return Task.FromResult(new BackendResult { Ok = true }); });
        Assert.Equal("network", result.Error);
        Assert.Equal(0, calls);
    }
}
