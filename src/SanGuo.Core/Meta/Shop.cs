using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    public enum ProductKind { MonthCard }

    /// <summary>付費商品（GDD 05 §9）。價格單位：人民幣元；1 元 = 10 元寶（草案）。1.0 付費點為月卡、通行證、首儲禮包，通行證與首儲的內容與定價待決，尚未實作。</summary>
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

    public enum ShopResult
    {
        Ok,
        UnknownProduct,
        UnknownOrder,
        NotActive,
        AlreadyClaimedToday,
    }

    public static class Shop
    {
        public const string MonthSmall = "month_small";
        public const string MonthBig = "month_big";

        public static List<ProductDef> Products() => new List<ProductDef>
        {
            new ProductDef { Id = MonthSmall, Name = "小月卡", Kind = ProductKind.MonthCard, PriceCny = 30, ImmediateYuanbao = 300, DailyYuanbao = 100 },
            new ProductDef { Id = MonthBig, Name = "大月卡", Kind = ProductKind.MonthCard, PriceCny = 68, ImmediateYuanbao = 680, DailyYuanbao = 200 },
        };

        public static ProductDef? Find(string productId) => Products().FirstOrDefault(x => x.Id == productId);

        // ---- 訂單：建立（待付款）→ 付款回呼（冪等發貨）----

        /// <summary>建立訂單。月卡可疊加延長。付款前不發任何東西。</summary>
        public static ShopResult CreateOrder(PlayerProfile p, string productId, string orderId)
        {
            var product = Find(productId);
            if (product == null) return ShopResult.UnknownProduct;
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

            p.Yuanbao += product.ImmediateYuanbao;
            long today = DailyClock.DayIndex(now);
            p.MonthCardExpiry.TryGetValue(product.Id, out long expiry);
            // 還沒過期就接在原本到期日之後（續購不浪費），否則從今天算起。
            p.MonthCardExpiry[product.Id] = System.Math.Max(expiry, today) + product.Days;
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
    }
}
