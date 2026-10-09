using System;
using System.Linq;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    /// <summary>P5 經濟（企劃 2026-10-09）：每週任務與元寶儲值。</summary>
    public class EconomyTests
    {
        // 2026-10-05 是星期一；北京時間 06:00 已過 5 點重置。
        private static long At(int day, int hour = 6) =>
            new DateTimeOffset(2026, 10, day, hour, 0, 0, TimeSpan.FromHours(8)).ToUnixTimeSeconds();

        [Fact]
        public void WeeklyLogin_CountsDaysNotSessions()
        {
            var p = PlayerProfile.CreateNew(At(5));
            var q = DemoQuests.Book.Find("w_login")!;
            p.OnLogin(At(5, 8));
            p.OnLogin(At(5, 13));
            p.OnLogin(At(5, 21));
            Assert.Equal(1, Quests.Progress(p, q));
            p.OnLogin(At(6, 8));
            Assert.Equal(2, Quests.Progress(p, q));
        }

        [Fact]
        public void WeeklyQuests_ResetOnMonday_AndPayYuanbao()
        {
            var p = PlayerProfile.CreateNew(At(5));
            for (int d = 5; d <= 9; d++) p.OnLogin(At(d));
            var q = DemoQuests.Book.Find("w_login")!;
            Assert.Equal(QuestClaimResult.Ok, Quests.Claim(p, "w_login", At(9)));
            Assert.Equal(q.Reward.Yuanbao, p.Yuanbao);
            Assert.Equal(QuestClaimResult.AlreadyClaimed, Quests.Claim(p, "w_login", At(11)));
            p.OnLogin(At(12)); // 下週一
            Assert.Equal(1, Quests.Progress(p, q));
            Assert.Empty(p.WeeklyClaimed);
        }

        [Fact]
        public void WeeklyQuests_PayAboutOneThousandYuanbao()
        {
            int total = DemoQuests.Book.Quests.Where(q => q.Kind == QuestKind.Weekly).Sum(q => q.Reward.Yuanbao);
            Assert.InRange(total, 900, 1100);
        }

        [Fact]
        public void Recharge_FirstPurchaseOfEachTierIsDoubled()
        {
            var p = PlayerProfile.CreateNew(At(5));
            string id = Shop.RechargeId(30);
            Assert.Equal(ShopResult.Ok, Shop.CreateOrder(p, id, "o1", At(5)));
            Assert.Equal(ShopResult.Ok, Shop.Fulfill(p, "o1", At(5)));
            Assert.Equal(600, p.Yuanbao);
            Assert.Equal(ShopResult.Ok, Shop.Fulfill(p, "o1", At(5))); // 重複通知不重複發
            Assert.Equal(600, p.Yuanbao);
            Assert.Equal(ShopResult.Ok, Shop.CreateOrder(p, id, "o2", At(5)));
            Assert.Equal(ShopResult.Ok, Shop.Fulfill(p, "o2", At(5)));
            Assert.Equal(900, p.Yuanbao);
            Assert.Equal(6, Shop.Products().Count(x => x.Kind == ProductKind.Recharge));
        }

        [Fact]
        public void WeeklyAndRechargeState_SurviveSaveAndLoad()
        {
            var p = PlayerProfile.CreateNew(At(5));
            p.OnLogin(At(5));
            p.RechargeBought.Add(Shop.RechargeId(6));
            p.WeeklyClaimed.Add("w_stage");
            var back = ProfileSerializer.FromJson(ProfileSerializer.ToJson(p));
            Assert.Equal(p.WeeklyWeek, back.WeeklyWeek);
            Assert.Equal(1, back.WeeklyProgress["w_login"]);
            Assert.Contains("w_stage", back.WeeklyClaimed);
            Assert.Contains(Shop.RechargeId(6), back.RechargeBought);
        }
    }
}
