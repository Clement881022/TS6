#nullable enable
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>商店（測試付款）：直立商品卡，月卡與成長基金；成長基金購買後顯示各階段領取。</summary>
    public sealed class ShopPage : PageBase
    {
        protected override Page Id => Page.Shop;
        protected override string Title => "商店";

        protected override void BuildBody(VisualElement body)
        {
            var v = GameSession.View;
            var p = v.Raw;
            body.style.flexDirection = FlexDirection.Column;
            body.AddToClassList("page-centered");

            var row = new VisualElement();
            row.AddToClassList("dun-row");
            body.Add(row);

            foreach (var product in Shop.Products())
            {
                var pr = product;
                bool month = pr.Kind == ProductKind.MonthCard;
                var card = new VisualElement();
                card.AddToClassList("shop-card");
                card.AddToClassList(month ? (pr.PriceCny >= 60 ? "shop-card-gold" : "shop-card-blue") : "shop-card-purple");

                var head = new VisualElement { pickingMode = PickingMode.Ignore };
                head.AddToClassList("shop-head");
                head.Add(UiKit.Text(pr.Name, "shop-name"));
                card.Add(head);

                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("shop-art");
                art.Add(UiKit.ItemTile("item_yuanbao", month ? pr.ImmediateYuanbao.ToString() : Shop.GrowthFundTiers().Sum(t => t.Yuanbao).ToString(), "shop-icon"));
                card.Add(art);

                var text = new VisualElement { pickingMode = PickingMode.Ignore };
                text.AddToClassList("card-body");
                var btns = new VisualElement();
                btns.AddToClassList("card-footer");
                if (month)
                {
                    int left = Shop.MonthCardDaysLeft(p, pr.Id, v.Now);
                    bool claimed = Shop.MonthCardClaimedToday(p, pr.Id, v.Now);
                    text.Add(UiKit.Text($"購買即得 {pr.ImmediateYuanbao} 元寶", "line-title").WithClass("dun-center"));
                    text.Add(UiKit.Text($"每日領 {pr.DailyYuanbao}（{pr.Days} 天）", "line-sub").WithClass("dun-center"));
                    text.Add(UiKit.Text(left > 0 ? $"剩餘 {left} 天" : "未持有", left > 0 ? "txt-good" : "line-sub").WithClass("dun-center"));
                    if (left > 0)
                    {
                        if (claimed) btns.Add(UiKit.DoneBtn("今日已領"));
                        else btns.Add(UiKit.Btn($"領取 {pr.DailyYuanbao}", () => _ = Act(() => GameSession.Backend.ClaimMonthCard(pr.Id), "已領取"), primary: true));
                    }
                    btns.Add(UiKit.Btn($"{(left > 0 ? "續購" : "購買")}　¥{pr.PriceCny}", () => _ = Act(() => GameSession.Backend.BuyWithTestPayment(pr.Id), "購買成功"), primary: left <= 0));
                }
                else
                {
                    text.Add(UiKit.Text("依帳號等級分階段領取", "line-title").WithClass("dun-center"));
                    if (!p.GrowthFundOwned)
                    {
                        text.Add(UiKit.Text($"合計 {Shop.GrowthFundTiers().Sum(t => t.Yuanbao)} 元寶", "line-sub").WithClass("dun-center"));
                        btns.Add(UiKit.Btn($"購買　¥{pr.PriceCny}", () => _ = Act(() => GameSession.Backend.BuyWithTestPayment(pr.Id), "購買成功"), primary: true));
                    }
                    else
                    {
                        foreach (var t in Shop.GrowthFundTiers())
                        {
                            int level = t.PlayerLevel;
                            bool claimed = p.GrowthFundClaimed.Contains(level);
                            bool ready = v.Level >= level;
                            var b = claimed
                                ? UiKit.DoneBtn($"Lv.{level} 已領")
                                : UiKit.Btn($"Lv.{level}　{t.Yuanbao}", () => _ = Act(() => GameSession.Backend.ClaimGrowthFund(level), "已領取"), primary: ready);
                            b.AddToClassList("btn-sm");
                            b.AddToClassList("shop-tier-btn");
                            btns.Add(b);
                        }
                    }
                }
                card.Add(text);
                card.Add(btns);
                row.Add(card);
            }

            body.Add(UiKit.Text("測試環境：購買不會實際扣款", "foot-note"));
        }
    }
}
