using System;
using System.Linq;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    public class ProgressionSystemsTests
    {
        // 2026-10-05 是星期一。以北京時間 06:00（已過 5 點重置）為基準。
        private static long At(int day, int hour = 6) =>
            new DateTimeOffset(2026, 10, day, hour, 0, 0, TimeSpan.FromHours(8)).ToUnixTimeSeconds();

        private static readonly StageReward Stage = new StageReward
        {
            StageId = "1-1", Chapter = 1, StaminaCost = 8, Exp = 30, Gold = 100, FirstClearYuanbao = 50,
        };

        // ---- 日界線 ----

        [Fact]
        public void DailyClock_ResetsAtFiveAm_Beijing()
        {
            Assert.Equal(0, DailyClock.Weekday(At(5)));
            Assert.Equal(0, DailyClock.Weekday(At(6, 4)));     // 週二凌晨 4 點仍算週一
            Assert.Equal(1, DailyClock.Weekday(At(6, 5)));     // 5 點換日
            Assert.Equal(DailyClock.DayIndex(At(5)), DailyClock.DayIndex(At(6, 4)));
            Assert.Equal(DailyClock.DayIndex(At(5)) + 1, DailyClock.DayIndex(At(6, 5)));
            Assert.Equal(6, DailyClock.Weekday(At(11)));       // 週日
        }

        // ---- 星級與掃蕩 ----

        [Fact]
        public void StarRating_Rules()
        {
            Assert.Equal(0, StarRating.Rate(false, 0, 1, 0));
            Assert.Equal(1, StarRating.Rate(true, 1, 3, 5));
            Assert.Equal(2, StarRating.Rate(true, 0, 9, 5));
            Assert.Equal(3, StarRating.Rate(true, 0, 5, 5));
            Assert.Equal(3, StarRating.Rate(true, 0, 99, 0));
            Assert.Equal(1, StarRating.Rate(true, 2, 1, 5));
        }

        [Fact]
        public void StarRating_FromRealBattle()
        {
            var battle = new Battle(DemoContent.Level(1));
            AutoPlayer.RunToEnd(battle, 100);
            int stars = StarRating.Rate(battle, 0);
            Assert.InRange(stars, 0, 3);
            Assert.Equal(battle.Result == BattleResult.Won, stars > 0);
        }

        [Fact]
        public void Sweep_RequiresThreeStars_AndSpendsStaminaOnce()
        {
            long now = At(5);
            var p = PlayerProfile.CreateNew(now);
            p.ClaimClear(Stage, now, stars: 2);
            Assert.Equal(SweepResult.NotThreeStars, p.TrySweep(Stage, 1, now, out _));

            p.ClaimClear(Stage, now, stars: 3);
            int goldBefore = p.Gold;
            int staminaBefore = p.Stamina.Get(now);
            Assert.Equal(SweepResult.Ok, p.TrySweep(Stage, 3, now, out var res));
            Assert.Equal(300, res!.GoldGained);
            Assert.Equal(goldBefore + 300, p.Gold);
            Assert.Equal(staminaBefore - 24, p.Stamina.Get(now));
            Assert.Equal(0, res.YuanbaoGained);
        }

        [Fact]
        public void Sweep_Rejections_DoNotConsume()
        {
            long now = At(5);
            var p = PlayerProfile.CreateNew(now);
            p.ClaimClear(Stage, now, stars: 3);
            int stamina = p.Stamina.Get(now);
            Assert.Equal(SweepResult.InvalidCount, p.TrySweep(Stage, 0, now, out _));
            Assert.Equal(SweepResult.InvalidCount, p.TrySweep(Stage, PlayerProfile.MaxSweepCount + 1, now, out _));
            p.Stamina.TrySpend(p.Stamina.Get(now) - 5, now);
            Assert.Equal(SweepResult.NotEnoughStamina, p.TrySweep(Stage, 1, now, out _));
            Assert.Equal(5, p.Stamina.Get(now));
            Assert.True(stamina > 5);
        }

        [Fact]
        public void StageStars_OnlyGoUp()
        {
            long now = At(5);
            var p = PlayerProfile.CreateNew(now);
            p.ClaimClear(Stage, now, stars: 3);
            p.ClaimClear(Stage, now, stars: 1);
            Assert.Equal(3, p.StageStars["1-1"]);
        }

        // ---- 資源副本 ----

        private static ResourceDungeonDef Gold() => DemoResourceDungeons.Create().First(d => d.Id == "res_gold");

        [Fact]
        public void Dungeon_OnlyOpenOnItsWeekdays()
        {
            var p = PlayerProfile.CreateNew(At(5));
            p.Level = 10;
            var gold = Gold();                                  // 週一、三、五、日
            Assert.Equal(DungeonEntryResult.Ok, ResourceDungeons.TryEnter(p, gold, At(5)));
            Assert.Equal(DungeonEntryResult.NotOpenToday, ResourceDungeons.TryEnter(p, gold, At(6)));
        }

        [Fact]
        public void Dungeon_DailyLimit_ResetsNextDay()
        {
            long mon = At(5);
            var p = PlayerProfile.CreateNew(mon);
            p.Level = 10;
            var gold = Gold();
            for (int i = 0; i < gold.DailyLimit; i++)
                Assert.Equal(DungeonEntryResult.Ok, ResourceDungeons.TryEnter(p, gold, mon));
            Assert.Equal(DungeonEntryResult.LimitReached, ResourceDungeons.TryEnter(p, gold, mon));
            Assert.Equal(0, ResourceDungeons.Remaining(p, gold, mon));

            long wed = At(7);                                   // 週三也開放，次數重置
            Assert.Equal(gold.DailyLimit, ResourceDungeons.Remaining(p, gold, wed));
            Assert.Equal(DungeonEntryResult.Ok, ResourceDungeons.TryEnter(p, gold, wed));
        }

        [Fact]
        public void Dungeon_Sweep_NeedsPriorClear_AndUsesDailyCount()
        {
            long mon = At(5);
            var p = PlayerProfile.CreateNew(mon);
            p.Level = 10;
            var gold = Gold();
            Assert.Equal(DungeonEntryResult.NotCleared, ResourceDungeons.TrySweep(p, gold, 1, mon));

            Assert.Equal(DungeonEntryResult.Ok, ResourceDungeons.TryEnter(p, gold, mon));
            ResourceDungeons.ClaimWin(p, gold, mon);
            Assert.Equal(4000, p.Gold);

            Assert.Equal(DungeonEntryResult.Ok, ResourceDungeons.TrySweep(p, gold, 2, mon));
            Assert.Equal(12000, p.Gold);
            Assert.Equal(DungeonEntryResult.LimitReached, ResourceDungeons.TrySweep(p, gold, 1, mon));
        }

        [Fact]
        public void Dungeon_LevelGate_AndStamina()
        {
            long mon = At(5);
            var p = PlayerProfile.CreateNew(mon);
            Assert.Equal(DungeonEntryResult.LevelTooLow, ResourceDungeons.TryEnter(p, Gold(), mon));
            p.Level = 10;
            p.Stamina.TrySpend(p.Stamina.Get(mon) - 5, mon);
            Assert.Equal(DungeonEntryResult.NotEnoughStamina, ResourceDungeons.TryEnter(p, Gold(), mon));
            Assert.Equal(3, ResourceDungeons.Remaining(p, Gold(), mon));
        }

        // ---- 任務 ----

        [Fact]
        public void DailyQuest_ProgressClaimAndReset()
        {
            long d1 = At(5);
            var p = PlayerProfile.CreateNew(d1);
            Assert.Equal(QuestClaimResult.NotComplete, Quests.Claim(p, "d_stage", d1));

            for (int i = 0; i < 3; i++) p.ClaimClear(Stage, d1);
            Assert.Equal(QuestClaimResult.Ok, Quests.Claim(p, "d_stage", d1));
            Assert.Equal(QuestClaimResult.AlreadyClaimed, Quests.Claim(p, "d_stage", d1));
            int yuanbaoAfterQuest = p.Yuanbao;

            long d2 = At(6);                                    // 隔天：進度與領取狀態重置
            p.OnLogin(d2);
            Assert.Equal(QuestClaimResult.NotComplete, Quests.Claim(p, "d_stage", d2));
            Assert.Equal(QuestClaimResult.Ok, Quests.Claim(p, "d_login", d2));
            Assert.Equal(yuanbaoAfterQuest, p.Yuanbao);
        }

        [Fact]
        public void SevenDay_DayLocked_UntilThatDay()
        {
            long d1 = At(5);
            var p = PlayerProfile.CreateNew(d1);
            Quests.Report(p, Quests.Events.Sweep, 3, d1);       // 第 4 天的任務，第 1 天還沒開放，不計進度
            Assert.Equal(0, p.SevenDayProgress.GetValueOrDefault("s4_sweep"));

            long d4 = At(8);
            Quests.Report(p, Quests.Events.Sweep, 3, d4);
            Assert.Equal(3, p.SevenDayProgress["s4_sweep"]);
            Assert.Equal(QuestClaimResult.Ok, Quests.Claim(p, "s4_sweep", d4));
            Assert.Equal(20, Quests.SevenDayPoints(p));
        }

        [Fact]
        public void SevenDay_NotCountedAfterDay7()
        {
            var p = PlayerProfile.CreateNew(At(5));
            Quests.Report(p, Quests.Events.StageClear, 1, At(5 + 8));   // 第 9 天
            Assert.False(p.SevenDayProgress.ContainsKey("s1_stage"));
        }

        [Fact]
        public void Milestone_NeedsPoints_ThenGrantsHero_AndDuplicateBecomesShards()
        {
            long d1 = At(5);
            var p = PlayerProfile.CreateNew(d1);
            Assert.Equal(QuestClaimResult.NotComplete, Quests.ClaimMilestone(p, 60, d1));

            // 直接塞滿進度並領取全部七日任務（逐日推進到第 7 天）。
            long d7 = At(11);
            foreach (var q in DemoQuests.Book.Quests.Where(q => q.Kind == QuestKind.SevenDay))
                p.SevenDayProgress[q.Id] = q.Target;
            foreach (var q in DemoQuests.Book.Quests.Where(q => q.Kind == QuestKind.SevenDay))
                Assert.Equal(QuestClaimResult.Ok, Quests.Claim(p, q.Id, d7));
            Assert.Equal(220, Quests.SevenDayPoints(p));

            Assert.Equal(QuestClaimResult.Ok, Quests.ClaimMilestone(p, 200, d7));
            Assert.True(p.Heroes.ContainsKey("zhangfei"));
            Assert.Equal(QuestClaimResult.AlreadyClaimed, Quests.ClaimMilestone(p, 200, d7));
            Assert.Equal(QuestClaimResult.Unknown, Quests.ClaimMilestone(p, 7, d7));

            p.Grant(new Reward().WithHero("zhangfei"), d7);     // 已擁有 → 碎片
            Assert.Equal(HeroGrowth.CopyShards, p.GetMaterial(HeroGrowth.ShardKey("zhangfei")));
        }

        [Fact]
        public void Quest_SurvivesSaveLoad()
        {
            long d1 = At(5);
            var p = PlayerProfile.CreateNew(d1);
            p.Level = 10;
            for (int i = 0; i < 3; i++) p.ClaimClear(Stage, d1, 3);
            ResourceDungeons.TryEnter(p, Gold(), d1);
            Quests.Claim(p, "d_stage", d1);

            var loaded = ProfileSerializer.FromJson(ProfileSerializer.ToJson(p));

            Assert.Equal(3, loaded.StageStars["1-1"]);
            Assert.Equal(1, loaded.DailyCounters["res_gold"]);
            Assert.Contains("d_stage", loaded.DailyTaskClaimed);
            Assert.Equal(p.CreatedDay, loaded.CreatedDay);
            Assert.Equal(p.DailyDay, loaded.DailyDay);
            Assert.Equal(ProfileSerializer.ToJson(p), ProfileSerializer.ToJson(loaded));
        }
    }
}
