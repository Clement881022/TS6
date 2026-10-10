using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    public class BattlePassTests
    {
        private const long Oct = 1792036800, Nov = 1793592000;

        private static PlayerProfile Fresh()
        {
            var p = PlayerProfile.CreateNew(Oct);
            p.Stamina.Add(5000, Oct);
            return p;
        }

        private static bool Buy(PlayerProfile p, string productId, long now)
        {
            string order = System.Guid.NewGuid().ToString("N");
            return Shop.CreateOrder(p, productId, order, now) == ShopResult.Ok && Shop.Fulfill(p, order, now) == ShopResult.Ok;
        }

        [Fact]
        public void SpendingStamina_EarnsPassPoints()
        {
            var p = Fresh();
            Assert.Equal(StageEntryResult.Ok, p.TryEnterStage(DemoMeta.Stage(0, 1), Oct));
            Assert.Equal(10, p.Pass.Points);
            p.ClaimClear(DemoMeta.Stage(0, 1), Oct, 3);
            Assert.Equal(SweepResult.Ok, p.TrySweep(DemoMeta.Stage(0, 1), 5, Oct, out _));
            Assert.Equal(60, p.Pass.Points);
        }

        [Fact]
        public void FreeTrack_HasNoYuanbao_PaidTrackNeedsPurchase()
        {
            var p = Fresh();
            BattlePass.AddPoints(p, BattlePass.PointsPerLevel * 3, Oct);
            Assert.Equal(3, BattlePass.Level(p));
            int yuanbao = p.Yuanbao, gold = p.Gold;
            Assert.Equal(PassClaimResult.Ok, BattlePass.Claim(p, 1, false, Oct));
            Assert.Equal(yuanbao, p.Yuanbao);
            Assert.True(p.Gold > gold);
            Assert.Equal(PassClaimResult.AlreadyClaimed, BattlePass.Claim(p, 1, false, Oct));
            Assert.Equal(PassClaimResult.NotReached, BattlePass.Claim(p, 4, false, Oct));
            Assert.Equal(PassClaimResult.NotPurchased, BattlePass.Claim(p, 1, true, Oct));

            Assert.True(Buy(p, Shop.PassBasic, Oct));
            Assert.Equal(PassClaimResult.Ok, BattlePass.Claim(p, 1, true, Oct));
            Assert.Equal(yuanbao + 50, p.Yuanbao);
        }

        [Fact]
        public void PaidTrack_TotalsFifteenHundredYuanbao()
        {
            int total = 0;
            for (int lv = 1; lv <= BattlePass.MaxLevel; lv++) total += BattlePass.PaidReward(lv).Yuanbao;
            Assert.Equal(1500, total);
            for (int lv = 1; lv <= BattlePass.MaxLevel; lv++) Assert.Equal(0, BattlePass.FreeReward(lv).Yuanbao);
        }

        [Fact]
        public void OnlyBasicPassIsSold_WithoutBonusLevelsOrYuanbao()
        {
            var p = Fresh();
            int yuanbao = p.Yuanbao;
            Assert.Null(Shop.Find("pass_luxury"));
            Assert.Equal(ShopResult.UnknownProduct, Shop.CreateOrder(p, "pass_luxury", "removed", Oct));
            p.Orders["legacy"] = "pending:pass_luxury";
            Assert.Equal(ShopResult.UnknownProduct, Shop.Fulfill(p, "legacy", Oct));
            Assert.True(Buy(p, Shop.PassBasic, Oct));
            Assert.Equal(BattlePass.Basic, p.Pass.Tier);
            Assert.Equal(0, BattlePass.Level(p));
            Assert.Equal(yuanbao, p.Yuanbao);
            Assert.Equal(ShopResult.AlreadyPurchased, Shop.CreateOrder(p, Shop.PassBasic, "x", Oct));
            Assert.Equal(0, BattlePass.ClaimAll(p, Oct));
        }

        [Fact]
        public void LegacyPaidPass_RetainsProgressAndClaimsAsBasic()
        {
            var p = Fresh();
            BattlePass.AddPoints(p, 1500, Oct);
            p.Pass.Tier = "luxury";
            p.Pass.ClaimedPaid.Add(1);
            BattlePass.Roll(p, Oct);
            Assert.Equal(BattlePass.Basic, p.Pass.Tier);
            Assert.Equal(1500, p.Pass.Points);
            Assert.Contains(1, p.Pass.ClaimedPaid);
            Assert.Equal(PassClaimResult.Ok, BattlePass.Claim(p, 2, true, Oct));
        }

        [Fact]
        public void NewSeason_ResetsProgressAndPurchase()
        {
            var p = Fresh();
            Buy(p, Shop.PassBasic, Oct);
            BattlePass.AddPoints(p, 900, Oct);
            BattlePass.Roll(p, Nov);
            Assert.Equal("2026-11", p.Pass.Season);
            Assert.Equal(0, p.Pass.Points);
            Assert.Equal("", p.Pass.Tier);
            Assert.True(Buy(p, Shop.PassBasic, Nov));
        }

        [Fact]
        public void FirstPack_OncePerAccount()
        {
            var p = Fresh();
            int yuanbao = p.Yuanbao;
            Assert.True(Buy(p, Shop.FirstPack, Oct));
            Assert.Equal(yuanbao + 600, p.Yuanbao);
            Assert.Equal(1, Equipment.Count(p, EquipSlot.Weapon, 2));
            Assert.Equal(ShopResult.AlreadyPurchased, Shop.CreateOrder(p, Shop.FirstPack, "again", Oct));
        }

        [Fact]
        public void PassState_SurvivesSerialization()
        {
            var p = Fresh();
            Buy(p, Shop.PassBasic, Oct);
            Buy(p, Shop.FirstPack, Oct);
            BattlePass.AddPoints(p, 400, Oct);
            BattlePass.Claim(p, 2, true, Oct);
            var back = ProfileSerializer.FromJson(ProfileSerializer.ToJson(p));
            Assert.Equal(p.Pass.Season, back.Pass.Season);
            Assert.Equal(400, back.Pass.Points);
            Assert.Equal(BattlePass.Basic, back.Pass.Tier);
            Assert.Contains(2, back.Pass.ClaimedPaid);
            Assert.True(back.FirstPackBought);
        }
    }
}
