using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    public enum ProductKind { MonthCard, GrowthFund }

    /// <summary>付費商品（價格與內容為 monetization.md 的建議值，待確認）。價格單位：人民幣元。</summary>
    public sealed class ProductDef
    {
        public string Id = "";
        public string Name = "";
        public ProductKind Kind;
        public int PriceCny;
        /// <summary>購買即得的元寶（月卡）。</summary>
        public int ImmediateYuanbao;
        /// <summary>月卡：每日領取的元寶與有效天數。</summary>
        public int DailyYuanbao;
        public int Days = 30;
    }

    /// <summary>成長基金的一個領取階段：帳號等級達標即可領。</summary>
    public sealed class GrowthFundTier
    {
        public int PlayerLevel;
        public int Yuanbao;
    }

    public enum ShopResult
    {
        Ok,
        UnknownProduct,
        UnknownOrder,
        AlreadyOwned,
        NotPaid,
        NotActive,
        AlreadyClaimedToday,
        AlreadyClaimed,
        LevelTooLow,
    }

    public static class Shop
    {
        public const string MonthSmall = "month_small";
        public const string MonthBig = "month_big";
        public const string GrowthFund = "growth_fund";

        public static List<ProductDef> Products() => new List<ProductDef>
        {
            new ProductDef { Id = MonthSmall, Name = "小月卡", Kind = ProductKind.MonthCard, PriceCny = 30, ImmediateYuanbao = 300, DailyYuanbao = 100 },
            new ProductDef { Id = MonthBig, Name = "大月卡", Kind = ProductKind.MonthCard, PriceCny = 68, ImmediateYuanbao = 680, DailyYuanbao = 200 },
            new ProductDef { Id = GrowthFund, Name = "成長基金", Kind = ProductKind.GrowthFund, PriceCny = 98 },
        };

        /// <summary>成長基金階段：合計 10,000 元寶（約 50 抽）。</summary>
        public static List<GrowthFundTier> GrowthFundTiers() => new List<GrowthFundTier>
        {
            new GrowthFundTier { PlayerLevel = 5, Yuanbao = 1000 },
            new GrowthFundTier { PlayerLevel = 10, Yuanbao = 1500 },
            new GrowthFundTier { PlayerLevel = 15, Yuanbao = 1500 },
            new GrowthFundTier { PlayerLevel = 20, Yuanbao = 2000 },
            new GrowthFundTier { PlayerLevel = 25, Yuanbao = 2000 },
            new GrowthFundTier { PlayerLevel = 30, Yuanbao = 2000 },
        };

        public static ProductDef? Find(string productId) => Products().FirstOrDefault(x => x.Id == productId);

        // ---- 訂單：建立（待付款）→ 付款回呼（冪等發貨）----

        /// <summary>建立訂單。成長基金已買過不能再買；月卡可疊加延長。付款前不發任何東西。</summary>
        public static ShopResult CreateOrder(PlayerProfile p, string productId, string orderId)
        {
            var product = Find(productId);
            if (product == null) return ShopResult.UnknownProduct;
            if (product.Kind == ProductKind.GrowthFund && p.GrowthFundOwned) return ShopResult.AlreadyOwned;
            p.Orders[orderId] = "pending:" + productId;
            return ShopResult.Ok;
        }

        /// <summary>
        /// 付款成功的回呼（正式版由支付平台通知並驗簽）：同一張訂單只發貨一次，重複通知回傳 Ok 但不再發。
        /// </summary>
        public static ShopResult Fulfill(PlayerProfile p, string orderId, long now)
        {
            if (!p.Orders.TryGetValue(orderId, out var state)) return ShopResult.UnknownOrder;
            if (state.StartsWith("paid:")) return ShopResult.Ok;
            string productId = state.Substring("pending:".Length);
            var product = Find(productId);
            if (product == null) return ShopResult.UnknownProduct;

            if (product.Kind == ProductKind.MonthCard)
            {
                p.Yuanbao += product.ImmediateYuanbao;
                long today = DailyClock.DayIndex(now);
                p.MonthCardExpiry.TryGetValue(product.Id, out long expiry);
                // 還沒過期就接在原本到期日之後（續購不浪費），否則從今天算起。
                p.MonthCardExpiry[product.Id] = System.Math.Max(expiry, today) + product.Days;
            }
            else
            {
                p.GrowthFundOwned = true; // 重複付款（理論上已在建立訂單時擋掉）不會重複發
            }
            p.Orders[orderId] = "paid:" + productId;
            return ShopResult.Ok;
        }

        // ---- 月卡每日領取 ----

        /// <summary>月卡剩餘天數（含今天；0 = 未持有或已到期）。</summary>
        public static int MonthCardDaysLeft(PlayerProfile p, string cardId, long now)
        {
            if (!p.MonthCardExpiry.TryGetValue(cardId, out long expiry)) return 0;
            long left = expiry - DailyClock.DayIndex(now);
            return left > 0 ? (int)left : 0;
        }

        public static bool MonthCardClaimedToday(PlayerProfile p, string cardId, long now) =>
            p.MonthCardClaimedDay.TryGetValue(cardId, out long day) && day == DailyClock.DayIndex(now);

        public static ShopResult ClaimMonthCardDaily(PlayerProfile p, string cardId, long now)
        {
            var product = Find(cardId);
            if (product == null || product.Kind != ProductKind.MonthCard) return ShopResult.UnknownProduct;
            if (MonthCardDaysLeft(p, cardId, now) <= 0) return ShopResult.NotActive;
            if (MonthCardClaimedToday(p, cardId, now)) return ShopResult.AlreadyClaimedToday;
            p.MonthCardClaimedDay[cardId] = DailyClock.DayIndex(now);
            p.Yuanbao += product.DailyYuanbao;
            return ShopResult.Ok;
        }

        // ---- 成長基金 ----

        public static ShopResult ClaimGrowthFund(PlayerProfile p, int playerLevel)
        {
            var tier = GrowthFundTiers().FirstOrDefault(t => t.PlayerLevel == playerLevel);
            if (tier == null) return ShopResult.UnknownProduct;
            if (!p.GrowthFundOwned) return ShopResult.NotPaid;
            if (p.GrowthFundClaimed.Contains(playerLevel)) return ShopResult.AlreadyClaimed;
            if (p.Level < tier.PlayerLevel) return ShopResult.LevelTooLow;
            p.GrowthFundClaimed.Add(playerLevel);
            p.Yuanbao += tier.Yuanbao;
            return ShopResult.Ok;
        }
    }
}
