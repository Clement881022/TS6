using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    public enum ProductKind { MonthCard, Pass, FirstPack, Recharge }

    public sealed class ProductDef
    {
        public string Id = "";
        public string Name = "";
        public ProductKind Kind;
        public int PriceCny;
        public int ImmediateYuanbao;
        public int DailyYuanbao;
        public int DailyStamina;
        public int Days = 30;
        public string PassTier = "";
    }

    public enum ShopResult
    {
        Ok,
        UnknownProduct,
        UnknownOrder,
        NotActive,
        AlreadyClaimedToday,
        AlreadyPurchased,
    }

    public static class Shop
    {
        public const string MonthSmall = "month_small";
        public const string MonthBig = "month_big";
        public const string PassBasic = "pass_basic";
        public const string PassLuxury = "pass_luxury";
        public const string FirstPack = "first_pack";
        public static readonly int[] RechargeTiers = { 6, 30, 98, 198, 328, 648 };
        public static string RechargeId(int cny) => "recharge_" + cny;
        public static bool RechargeFirstTime(PlayerProfile p, string productId) => !p.RechargeBought.Contains(productId);

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
            new ProductDef { Id = MonthSmall, Name = "小月卡", Kind = ProductKind.MonthCard, PriceCny = 30, ImmediateYuanbao = 300, DailyYuanbao = 100, DailyStamina = 60 },
            new ProductDef { Id = MonthBig, Name = "大月卡", Kind = ProductKind.MonthCard, PriceCny = 68, ImmediateYuanbao = 680, DailyYuanbao = 200, DailyStamina = 120 },
            new ProductDef { Id = PassBasic, Name = "通行證", Kind = ProductKind.Pass, PriceCny = 30, PassTier = BattlePass.Basic },
            new ProductDef { Id = PassLuxury, Name = "豪華通行證", Kind = ProductKind.Pass, PriceCny = 98, ImmediateYuanbao = 980, PassTier = BattlePass.Luxury },
            new ProductDef { Id = FirstPack, Name = "首儲禮包", Kind = ProductKind.FirstPack, PriceCny = 6 },
        };

        public static ProductDef? Find(string productId) => Products().FirstOrDefault(x => x.Id == productId);

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
                    if (!p.FirstPackBought) { p.FirstPackBought = true; p.Grant(FirstPackReward(), now); }
                    p.Orders[orderId] = "paid:" + productId;
                    return ShopResult.Ok;
                case ProductKind.Pass:
                    BattlePass.Roll(p, now);
                    if (p.Pass.Tier == "") BattlePass.Activate(p, product.PassTier, now);
                    p.Orders[orderId] = "paid:" + productId;
                    return ShopResult.Ok;
                case ProductKind.Recharge:
                    if (p.RechargeBought.Add(product.Id)) p.Yuanbao += product.ImmediateYuanbao;
                    p.Orders[orderId] = "paid:" + productId;
                    return ShopResult.Ok;
            }
            long today = DailyClock.DayIndex(now);
            p.MonthCardExpiry.TryGetValue(product.Id, out long expiry);
            p.MonthCardExpiry[product.Id] = System.Math.Max(expiry, today) + product.Days;
            p.Orders[orderId] = "paid:" + productId;
            return ShopResult.Ok;
        }

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
            if (product.DailyStamina > 0) p.Stamina.Add(product.DailyStamina, now);
            return ShopResult.Ok;
        }
    }
}
