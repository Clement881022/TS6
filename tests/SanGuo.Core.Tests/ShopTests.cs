using System.Linq;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    public class ShopTests
    {
        private const long Day = 86400;
        private const long T0 = 1_700_000_000;

        private static PlayerProfile Player() => PlayerProfile.CreateNew(T0);

        private static void Buy(PlayerProfile p, string product, string order, long now)
        {
            Assert.Equal(ShopResult.Ok, Shop.CreateOrder(p, product, order));
            Assert.Equal(ShopResult.Ok, Shop.Fulfill(p, order, now));
        }

        [Fact]
        public void PendingOrder_GrantsNothingUntilPaid()
        {
            var p = Player();
            Shop.CreateOrder(p, Shop.MonthSmall, "o1");
            Assert.Equal(0, p.Yuanbao);
            Assert.Equal(0, Shop.MonthCardDaysLeft(p, Shop.MonthSmall, T0));
        }

        [Fact]
        public void Fulfill_IsIdempotent()
        {
            var p = Player();
            Buy(p, Shop.MonthSmall, "o1", T0);
            Assert.Equal(300, p.Yuanbao);
            Assert.Equal(ShopResult.Ok, Shop.Fulfill(p, "o1", T0)); // 支付平台重複通知
            Assert.Equal(300, p.Yuanbao);
            Assert.Equal(30, Shop.MonthCardDaysLeft(p, Shop.MonthSmall, T0));
        }

        [Fact]
        public void UnknownProductAndOrder_AreRejected()
        {
            var p = Player();
            Assert.Equal(ShopResult.UnknownProduct, Shop.CreateOrder(p, "nope", "o1"));
            Assert.Equal(ShopResult.UnknownOrder, Shop.Fulfill(p, "ghost", T0));
        }

        [Fact]
        public void MonthCard_DailyClaimOncePerDay_ThroughExpiry()
        {
            var p = Player();
            Buy(p, Shop.MonthBig, "o1", T0);
            Assert.Equal(680, p.Yuanbao);
            Assert.Equal(ShopResult.Ok, Shop.ClaimMonthCardDaily(p, Shop.MonthBig, T0));
            Assert.Equal(880, p.Yuanbao);
            Assert.Equal(ShopResult.AlreadyClaimedToday, Shop.ClaimMonthCardDaily(p, Shop.MonthBig, T0));
            Assert.Equal(ShopResult.Ok, Shop.ClaimMonthCardDaily(p, Shop.MonthBig, T0 + Day));
            // 含購買當天共 30 日：第 30 天還能領，第 31 天到期。
            Assert.Equal(ShopResult.Ok, Shop.ClaimMonthCardDaily(p, Shop.MonthBig, T0 + 29 * Day));
            Assert.Equal(ShopResult.NotActive, Shop.ClaimMonthCardDaily(p, Shop.MonthBig, T0 + 30 * Day));
        }

        [Fact]
        public void MonthCard_NotOwned_CannotClaim()
        {
            Assert.Equal(ShopResult.NotActive, Shop.ClaimMonthCardDaily(Player(), Shop.MonthSmall, T0));
        }

        [Fact]
        public void MonthCard_RenewalStacksOnRemainingDays()
        {
            var p = Player();
            Buy(p, Shop.MonthSmall, "o1", T0);
            Buy(p, Shop.MonthSmall, "o2", T0 + 10 * Day);
            Assert.Equal(50, Shop.MonthCardDaysLeft(p, Shop.MonthSmall, T0 + 10 * Day)); // 剩 20 + 新 30
        }

        [Fact]
        public void GrowthFund_RequiresPurchase_LevelAndOnlyOnce()
        {
            var p = Player();
            p.Level = 12;
            Assert.Equal(ShopResult.NotPaid, Shop.ClaimGrowthFund(p, 5));
            Buy(p, Shop.GrowthFund, "o1", T0);
            Assert.Equal(ShopResult.Ok, Shop.ClaimGrowthFund(p, 5));
            Assert.Equal(ShopResult.AlreadyClaimed, Shop.ClaimGrowthFund(p, 5));
            Assert.Equal(ShopResult.Ok, Shop.ClaimGrowthFund(p, 10));
            Assert.Equal(ShopResult.LevelTooLow, Shop.ClaimGrowthFund(p, 15));
            Assert.Equal(2500, p.Yuanbao);
        }

        [Fact]
        public void GrowthFund_CannotBeBoughtTwice_AndTiersSumTo10000()
        {
            var p = Player();
            Buy(p, Shop.GrowthFund, "o1", T0);
            Assert.Equal(ShopResult.AlreadyOwned, Shop.CreateOrder(p, Shop.GrowthFund, "o2"));
            Assert.Equal(10000, Shop.GrowthFundTiers().Sum(t => t.Yuanbao));
        }

        [Fact]
        public void ShopState_SurvivesSaveAndLoad()
        {
            var p = Player();
            p.Level = 6;
            Buy(p, Shop.MonthSmall, "o1", T0);
            Buy(p, Shop.GrowthFund, "o2", T0);
            Shop.ClaimMonthCardDaily(p, Shop.MonthSmall, T0);
            Shop.ClaimGrowthFund(p, 5);
            var back = ProfileSerializer.FromJson(ProfileSerializer.ToJson(p));
            Assert.Equal(p.MonthCardExpiry, back.MonthCardExpiry);
            Assert.Equal(p.MonthCardClaimedDay, back.MonthCardClaimedDay);
            Assert.True(back.GrowthFundOwned);
            Assert.Equal(p.GrowthFundClaimed, back.GrowthFundClaimed);
            Assert.Equal(p.Orders, back.Orders);
            Assert.Equal(ShopResult.Ok, Shop.Fulfill(back, "o1", T0)); // 還原後仍冪等
            Assert.Equal(p.Yuanbao, back.Yuanbao);
        }
    }
}
