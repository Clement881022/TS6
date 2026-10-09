using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    public enum ProductKind { MonthCard, Pass, FirstPack, Recharge }

    /// <summary>
    /// 付費商品（GDD 05 §9）。價格單位：人民幣元；1 元 = 10 元寶（草案）。1.0 付費點為月卡、通行證、首儲禮包。
    /// 通行證與首儲禮包的內容與定價為暫定（2026-10-09 先行假設，待確認），見 <see cref="BattlePass"/> 與 <see cref="Shop.FirstPackReward"/>。
    /// </summary>
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
        /// <summary>通行證：啟用的付費版（<see cref="BattlePass.Basic"/> / <see cref="BattlePass.Luxury"/>）。</summary>
        public string PassTier = "";
    }

    public enum ShopResult
    {
        Ok,
        UnknownProduct,
        UnknownOrder,
        NotActive,
        AlreadyClaimedToday,
        /// <summary>首儲禮包已買過 / 本季已有通行證。</summary>
        AlreadyPurchased,
    }

    public static class Shop
    {
        public const string MonthSmall = "month_small";
        public const string MonthBig = "month_big";
        public const string PassBasic = "pass_basic";
        public const string PassLuxury = "pass_luxury";
        public const string FirstPack = "first_pack";
        /// <summary>元寶儲值檔位（企劃 2026-10-09）：1 元 = 10 元寶，每檔第一次購買雙倍。</summary>
        public static readonly int[] RechargeTiers = { 6, 30, 98, 198, 328, 648 };
        public static string RechargeId(int cny) => "recharge_" + cny;
        public static bool RechargeFirstTime(PlayerProfile p, string productId) => !p.RechargeBought.Contains(productId);

        /// <summary>首儲禮包（¥6，每帳號一次）：600 元寶、金幣 20000、武將經驗 5000、二階武器／防具／飾品各 1。</summary>
        public static Reward FirstPackReward()
        {
            var r = new Reward(yuanbao: 600, gold: 20000).With(HeroGrowth.HeroExp, 5000);
            foreach (var slot in Equipment.Slots) r.With(Equipment.ItemKey(slot, 2), 1);
            return r;
        }

        public static List<ProductDef> Products()
        {
            var list = BaseProducts();
            foreach (int cny in RechargeTiers)
                list.Add(new ProductDef { Id = RechargeId(cny), Name = $"{cny * 10} 元寶", Kind = ProductKind.Recharge, PriceCny = cny, ImmediateYuanbao = cny * 10 });
            return list;
        }

        private static List<ProductDef> BaseProducts() => new List<ProductDef>
        {
            new ProductDef { Id = MonthSmall, Name = "小月卡", Kind = ProductKind.MonthCard, PriceCny = 30, ImmediateYuanbao = 300, DailyYuanbao = 100 },
            new ProductDef { Id = MonthBig, Name = "大月卡", Kind = ProductKind.MonthCard, PriceCny = 68, ImmediateYuanbao = 680, DailyYuanbao = 200 },
            new ProductDef { Id = PassBasic, Name = "通行證", Kind = ProductKind.Pass, PriceCny = 30, PassTier = BattlePass.Basic },
            new ProductDef { Id = PassLuxury, Name = "豪華通行證", Kind = ProductKind.Pass, PriceCny = 98, ImmediateYuanbao = 980, PassTier = BattlePass.Luxury },
            new ProductDef { Id = FirstPack, Name = "首儲禮包", Kind = ProductKind.FirstPack, PriceCny = 6 },
        };

        public static ProductDef? Find(string productId) => Products().FirstOrDefault(x => x.Id == productId);

        // ---- 訂單：建立（待付款）→ 付款回呼（冪等發貨）----

        /// <summary>建立訂單。月卡可疊加延長。付款前不發任何東西。</summary>
        public static ShopResult CreateOrder(PlayerProfile p, string productId, string orderId, long now = 0)
        {
            var product = Find(productId);
            if (product == null) return ShopResult.UnknownProduct;
            if (product.Kind == ProductKind.FirstPack && p.FirstPackBought) return ShopResult.AlreadyPurchased;
            if (product.Kind == ProductKind.Pass)
            {
                BattlePass.Roll(p, now);
                if (p.Pass.Tier != "") return ShopResult.AlreadyPurchased;
            }
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
            switch (product.Kind)
            {
                case ProductKind.FirstPack:
                    // 重複下單時付款仍成立但不重複發（交由客服退款）。
                    if (!p.FirstPackBought) { p.FirstPackBought = true; p.Grant(FirstPackReward(), now); }
                    p.Orders[orderId] = "paid:" + productId;
                    return ShopResult.Ok;
                case ProductKind.Pass:
                    BattlePass.Roll(p, now);
                    if (p.Pass.Tier == "") BattlePass.Activate(p, product.PassTier, now);
                    p.Orders[orderId] = "paid:" + productId;
                    return ShopResult.Ok;
                case ProductKind.Recharge:
                    // 每檔第一次購買雙倍（加送同額元寶）
                    if (p.RechargeBought.Add(product.Id)) p.Yuanbao += product.ImmediateYuanbao;
                    p.Orders[orderId] = "paid:" + productId;
                    return ShopResult.Ok;
            }
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
