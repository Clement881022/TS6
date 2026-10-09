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
            Assert.Equal(2, StarRating.Rate(true, 1, 3, 5));   // 通關 + 限定回合內（有人陣亡）
            Assert.Equal(1, StarRating.Rate(true, 1, 9, 5));
            Assert.Equal(2, StarRating.Rate(true, 0, 9, 5));   // 通關 + 全員存活（超過回合）
            Assert.Equal(3, StarRating.Rate(true, 0, 5, 5));
            Assert.Equal(3, StarRating.Rate(true, 0, 99, 0));
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
            p.Level = 30; // 避免掃蕩中途升級補滿體力，干擾扣體力的驗證
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

        private static ResourceDungeonDef Tier(int tier) => DemoResourceDungeons.Create().First(d => d.Tier == tier);

        private static PlayerProfile Unlocked(long now)
        {
            var p = PlayerProfile.CreateNew(now);
            p.Level = 60;
            p.Stamina.Add(500, now);
            foreach (var d in DemoResourceDungeons.Create())
                if (d.UnlockStageId != "") p.ClearedStages.Add(d.UnlockStageId);
            return p;
        }

        [Fact]
        public void Dungeons_FiveTiers_StaminaAndEquipmentTierMatchGdd()
        {
            var all = DemoResourceDungeons.Create();
            Assert.Equal(new[] { 20, 25, 30, 35, 40 }, all.Select(d => d.StaminaCost).ToArray());
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, all.Select(d => d.Tier).ToArray());
        }

        [Fact]
        public void Dungeon_UnlocksTierByTier_WithoutChapterGates()
        {
            var p = PlayerProfile.CreateNew(At(5));
            p.Stamina.Add(500, At(5));
            p.ClearedStages.Add("0-4");
            Assert.Equal(DungeonEntryResult.Ok, ResourceDungeons.TryEnter(p, Tier(1), At(5)));
            Assert.Equal(DungeonEntryResult.Locked, ResourceDungeons.TryEnter(p, Tier(2), At(5)));
            ResourceDungeons.ClaimWin(p, Tier(1), At(5), 1); // 打贏第 1 階
            Assert.Equal(DungeonEntryResult.Ok, ResourceDungeons.TryEnter(p, Tier(2), At(5)));
            Assert.Equal(DungeonEntryResult.Locked, ResourceDungeons.TryEnter(p, Tier(3), At(5)));
        }

        [Fact]
        public void HigherTierDrops_AreRarer()
        {
            // 1–3 階直接掉裝備，掉率逐階降低；4–5 階每次掉 1 個碎片，合成所需碎片逐階增加
            for (int tier = 2; tier <= 3; tier++)
            {
                var drops = Equipment.RollDrops(tier, 2000, new Rng(7));
                double rate = drops.Where(kv => kv.Key.EndsWith(":" + tier)).Sum(kv => kv.Value) / 2000.0;
                Assert.InRange(rate, Equipment.DropChanceOf(tier) - 0.04, Equipment.DropChanceOf(tier) + 0.04);
                Assert.True(Equipment.DropChanceOf(tier) < Equipment.DropChanceOf(tier - 1));
            }
            for (int tier = 4; tier <= 5; tier++)
            {
                Assert.True(Equipment.UsesShards(tier));
                var drops = Equipment.RollDrops(tier, 300, new Rng(7));
                Assert.Equal(300, drops.Where(kv => kv.Key.StartsWith("eqs:") && kv.Key.EndsWith(":" + tier)).Sum(kv => kv.Value));
                Assert.DoesNotContain(drops.Keys, k => k.StartsWith("eq:"));
            }
            Assert.True(Equipment.ShardCostOf(5) > Equipment.ShardCostOf(4));
            Assert.False(Equipment.UsesShards(3));
        }

        [Fact]
        public void Dungeon_IsAlwaysOpen_ButLockedUntilItsStageIsCleared()
        {
            var p = PlayerProfile.CreateNew(At(5));
            p.Stamina.Add(500, At(5));
            Assert.Equal(DungeonEntryResult.Locked, ResourceDungeons.TryEnter(p, Tier(1), At(5)));
            p.ClearedStages.Add(Tier(1).UnlockStageId);
            // 沒有星期輪替與每日次數限制：任何一天都能一直打。
            for (int i = 0; i < 5; i++)
                Assert.Equal(DungeonEntryResult.Ok, ResourceDungeons.TryEnter(p, Tier(1), At(5 + i % 3)));
        }

        [Fact]
        public void Dungeon_WinGrantsGoldHeroExpYuanbaoAndOneEquipmentOfItsTier()
        {
            long now = At(5);
            var p = Unlocked(now);
            var d = Tier(3);
            var reward = ResourceDungeons.ClaimWin(p, d, now, 42);
            Assert.Equal(6000, reward.Gold);
            Assert.Equal(6000, p.Gold);
            Assert.Equal(2400, p.GetMaterial(HeroGrowth.HeroExp));
            Assert.Equal(15, p.Yuanbao);
            // 每次最多 1 件本階裝備（機率 Equipment.DropChance），沒掉到就沒有
            int items = Equipment.Slots.Sum(s => Equipment.Count(p, s, 3));
            Assert.InRange(items, 0, 1);
            Assert.Equal(0, Equipment.Slots.Sum(s => Equipment.Count(p, s, 2) + Equipment.Count(p, s, 1)));
            Assert.Contains(d.Id, p.ClearedStages);
        }

        [Fact]
        public void Dungeon_Sweep_NeedsPriorClear_SpendsSameStamina_AndDropsPerRun()
        {
            long now = At(5);
            var p = Unlocked(now);
            var d = Tier(5); // Unlocked() 已打贏第 1–4 階，第 5 階已開放但尚未通關
            Assert.Equal(DungeonEntryResult.NotCleared, ResourceDungeons.TrySweep(p, d, 1, now, out _));
            ResourceDungeons.ClaimWin(p, d, now, 1);
            int stamina = p.Stamina.Get(now);
            Assert.Equal(DungeonEntryResult.Ok, ResourceDungeons.TrySweep(p, d, 4, now, out var reward));
            Assert.Equal(stamina - 40 * 4, p.Stamina.Get(now));
            Assert.Equal(4, reward!.Materials.Where(m => m.Key.StartsWith("eqs:")).Sum(m => m.Value)); // 第 5 階每場掉 1 個碎片
            Assert.Equal(DungeonEntryResult.InvalidCount, ResourceDungeons.TrySweep(p, d, 0, now, out _));
            Assert.Equal(DungeonEntryResult.InvalidCount, ResourceDungeons.TrySweep(p, d, ResourceDungeons.MaxSweepCount + 1, now, out _));
        }

        [Fact]
        public void Dungeon_NotEnoughStamina_DoesNotConsume()
        {
            long now = At(5);
            var p = Unlocked(now);
            p.Stamina.TrySpend(p.Stamina.Get(now) - 10, now);
            Assert.Equal(DungeonEntryResult.NotEnoughStamina, ResourceDungeons.TryEnter(p, Tier(1), now));
            Assert.Equal(10, p.Stamina.Get(now));
        }

        // ---- 將魂商店 ----

        [Fact]
        public void SoulShop_BuysWithinMonthlyLimit_AndResetsNextMonth()
        {
            long oct = At(5);
            var p = PlayerProfile.CreateNew(oct);
            p.AddMaterial(HeroGrowth.Soul, 1000);
            for (int i = 0; i < 5; i++) Assert.Equal(SoulShopResult.Ok, SoulShop.Buy(p, "gold", oct));
            Assert.Equal(SoulShopResult.LimitReached, SoulShop.Buy(p, "gold", oct));
            Assert.Equal(25_000, p.Gold);
            Assert.Equal(1000 - 150, p.GetMaterial(HeroGrowth.Soul));

            long nov = new DateTimeOffset(2026, 11, 2, 6, 0, 0, TimeSpan.FromHours(8)).ToUnixTimeSeconds();
            Assert.Equal(SoulShopResult.Ok, SoulShop.Buy(p, "gold", nov));
        }

        [Fact]
        public void SoulShop_HeroShards_PriceByRarity_AndRespectCap()
        {
            long now = At(5);
            var p = PlayerProfile.CreateNew(now);
            p.AddMaterial(HeroGrowth.Soul, 2000);
            Assert.Equal(SoulShopResult.HeroNotOwned, SoulShop.Buy(p, "shard:zhoucang", now));
            p.Heroes["zhoucang"] = new HeroState { HeroId = "zhoucang" };
            p.Heroes["xiahoudun"] = new HeroState { HeroId = "xiahoudun" };
            Assert.Equal(SoulShopResult.Ok, SoulShop.Buy(p, "shard:zhoucang", now));
            Assert.Equal(1, HeroGrowth.Shards(p, "zhoucang"));
            Assert.Equal(SoulShopResult.Ok, SoulShop.Buy(p, "shard:xiahoudun", now));
            Assert.Equal(2000 - 100 - 300, p.GetMaterial(HeroGrowth.Soul));
            Assert.Equal(SoulShopResult.LimitReached, SoulShop.Buy(p, "shard:xiahoudun", now));

            p.Heroes["zhoucang"].Stars = 5;                    // 已滿突：不再賣重複份
            p.Materials.Remove(HeroGrowth.ShardKey("zhoucang"));
            Assert.Equal(SoulShopResult.HeroMaxed, SoulShop.Buy(p, "shard:zhoucang", now));
        }

        [Fact]
        public void SoulShop_RejectsStoryHeroesUnknownItemsAndPoverty()
        {
            long now = At(5);
            var p = PlayerProfile.CreateNew(now);
            Assert.Equal(SoulShopResult.UnknownItem, SoulShop.Buy(p, "shard:liubei", now));
            Assert.Equal(SoulShopResult.UnknownItem, SoulShop.Buy(p, "nope", now));
            Assert.Equal(SoulShopResult.NotEnoughSouls, SoulShop.Buy(p, "gold", now));
        }

        [Fact]
        public void SoulShop_EquipmentItem_GoesToInventory()
        {
            long now = At(5);
            var p = PlayerProfile.CreateNew(now);
            p.AddMaterial(HeroGrowth.Soul, 80);
            Assert.Equal(SoulShopResult.Ok, SoulShop.Buy(p, "eq:armor:3", now));
            Assert.Equal(1, Equipment.Count(p, EquipSlot.Armor, 3));
        }

        [Fact]
        public void MonthKey_UsesGameDay()
        {
            Assert.Equal("2026-10", DailyClock.MonthKey(At(5)));
            long lateNight = new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.FromHours(8)).ToUnixTimeSeconds(); // 5 點前仍算 10 月
            Assert.Equal("2026-10", DailyClock.MonthKey(lateNight));
            Assert.Equal("2026-11", DailyClock.MonthKey(lateNight + 3 * 3600));
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
        public void Milestone_NeedsPoints_ThenGrantsHero_AndDuplicateBecomesShard()
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

            p.Grant(new Reward().WithHero("zhangfei"), d7);     // 已擁有 → 1 份重複份
            Assert.Equal(1, p.GetMaterial(HeroGrowth.ShardKey("zhangfei")));
        }

        [Fact]
        public void Quest_SurvivesSaveLoad()
        {
            long d1 = At(5);
            var p = PlayerProfile.CreateNew(d1);
            p.Level = 10;
            for (int i = 0; i < 3; i++) p.ClaimClear(Stage, d1, 3);
            Quests.Claim(p, "d_stage", d1);

            var loaded = ProfileSerializer.FromJson(ProfileSerializer.ToJson(p));

            Assert.Equal(3, loaded.StageStars["1-1"]);
            Assert.Contains("d_stage", loaded.DailyTaskClaimed);
            Assert.Equal(p.CreatedDay, loaded.CreatedDay);
            Assert.Equal(p.DailyDay, loaded.DailyDay);
            Assert.Equal(ProfileSerializer.ToJson(p), ProfileSerializer.ToJson(loaded));
        }
    }
}
