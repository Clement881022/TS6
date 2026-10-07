#nullable enable
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>商店（M4，測試付款）：月卡與成長基金。</summary>
    public sealed class ShopPage : PageBase
    {
        protected override Page Id => Page.Shop;
        protected override string Title => "商店";

        protected override void BuildBody(VisualElement body)
        {
            var v = GameSession.View;
            var p = v.Raw;
            body.Add(UiKit.Text("目前為測試付款：按「購買」會直接模擬付款成功，不會真的扣款。", "txt-warn"));

            foreach (var product in Shop.Products())
            {
                var pr = product;
                var card = UiKit.Panel();
                if (pr.Kind == ProductKind.MonthCard)
                {
                    int left = Shop.MonthCardDaysLeft(p, pr.Id, v.Now);
                    bool claimed = Shop.MonthCardClaimedToday(p, pr.Id, v.Now);
                    card.Add(UiKit.Text($"{pr.Name}　¥{pr.PriceCny}", "txt-sub"));
                    card.Add(UiKit.Text($"購買即得 {pr.ImmediateYuanbao} 元寶，每日領 {pr.DailyYuanbao}（{pr.Days} 天）　" +
                        (left > 0 ? $"剩餘 {left} 天" : "未持有"), "txt-dim"));
                    var row = UiKit.Row();
                    row.Add(UiKit.Btn(left > 0 ? "續購" : "購買", () => _ = Act(() => GameSession.Backend.BuyWithTestPayment(pr.Id), "購買成功")));
                    if (left > 0)
                    {
                        if (claimed) row.Add(UiKit.Btn("今日已領", () => { }));
                        else row.Add(UiKit.Btn($"領取每日 {pr.DailyYuanbao} 元寶", () => _ = Act(() => GameSession.Backend.ClaimMonthCard(pr.Id), "已領取"), primary: true));
                    }
                    card.Add(row);
                }
                else
                {
                    card.Add(UiKit.Text($"{pr.Name}　¥{pr.PriceCny}", "txt-sub"));
                    card.Add(UiKit.Text($"依帳號等級分階段領取，合計 {Shop.GrowthFundTiers().Sum(t => t.Yuanbao)} 元寶　" +
                        (p.GrowthFundOwned ? "已購買" : "未購買"), "txt-dim"));
                    if (!p.GrowthFundOwned)
                    {
                        var row = UiKit.Row();
                        row.Add(UiKit.Btn("購買", () => _ = Act(() => GameSession.Backend.BuyWithTestPayment(pr.Id), "購買成功")));
                        card.Add(row);
                    }
                    else
                    {
                        var tiers = UiKit.Row();
                        foreach (var t in Shop.GrowthFundTiers())
                        {
                            int level = t.PlayerLevel;
                            bool claimed = p.GrowthFundClaimed.Contains(level);
                            bool ready = v.Level >= level;
                            string label = $"Lv.{level}：{t.Yuanbao} 元寶";
                            if (claimed) tiers.Add(UiKit.Btn(label + "（已領）", () => { }));
                            else tiers.Add(UiKit.Btn(label + (ready ? "　領取" : ""),
                                () => _ = Act(() => GameSession.Backend.ClaimGrowthFund(level), "已領取"), primary: ready));
                        }
                        card.Add(tiers);
                    }
                }
                body.Add(card);
            }
        }
    }
}
