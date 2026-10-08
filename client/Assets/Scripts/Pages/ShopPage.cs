#nullable enable
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// 商店：「儲值」是月卡（測試付款）；「將魂商店」用滿突後溢出的重複武將轉成的將魂兌換重複份、武將經驗、金幣與裝備，各項每月限購。
    /// 通行證與首儲禮包的內容與定價尚未決定，暫不開放。
    /// </summary>
    public sealed class ShopPage : PageBase
    {
        private enum Tab { Pay, Soul }

        private Tab _tab = Tab.Pay;

        protected override Page Id => Page.Shop;
        protected override string Title => "商店";

        protected override void BuildBody(VisualElement body)
        {
            body.style.flexDirection = FlexDirection.Column;
            body.AddToClassList("page-centered");
            body.AddToClassList("shop-page");

            var seg = new VisualElement();
            seg.AddToClassList("seg");
            seg.AddToClassList("shop-tabs");
            seg.Add(UiKit.Tab("儲值", () => { _tab = Tab.Pay; Rebuild(); }, _tab == Tab.Pay).WithClass("seg-tab"));
            seg.Add(UiKit.Tab("將魂商店", () => { _tab = Tab.Soul; Rebuild(); }, _tab == Tab.Soul).WithClass("seg-tab"));
            body.Add(seg);

            if (_tab == Tab.Pay) BuildPay(body);
            else BuildSoulShop(body);
        }

        public void DebugSetTab(int tab) { _tab = (Tab)tab; Rebuild(); }

        private void BuildPay(VisualElement body)
        {
            var v = GameSession.View;
            var p = v.Raw;

            var row = new VisualElement();
            row.AddToClassList("dun-row");
            body.Add(row);

            foreach (var product in Shop.Products())
            {
                var pr = product;
                var card = new VisualElement();
                card.AddToClassList("shop-card");
                card.AddToClassList(pr.PriceCny >= 60 ? "shop-card-gold" : "shop-card-blue");

                var head = new VisualElement { pickingMode = PickingMode.Ignore };
                head.AddToClassList("shop-head");
                head.Add(UiKit.Text(pr.Name, "shop-name"));
                card.Add(head);

                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("shop-art");
                art.Add(UiKit.ItemTile("item_yuanbao", pr.ImmediateYuanbao.ToString(), "shop-icon"));
                card.Add(art);

                var text = new VisualElement { pickingMode = PickingMode.Ignore };
                text.AddToClassList("card-body");
                var btns = new VisualElement();
                btns.AddToClassList("card-footer");
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
                card.Add(text);
                card.Add(btns);
                row.Add(card);
            }

            body.Add(UiKit.Text("測試環境：購買不會實際扣款", "foot-note"));
        }

        private void BuildSoulShop(VisualElement body)
        {
            var v = GameSession.View;
            long now = v.Now;
            int souls = v.Material(HeroGrowth.Soul);
            body.Add(UiKit.Text($"將魂 {souls}　｜　滿突武將的重複份轉為將魂，每月 1 日重置限購", "line-title shop-soul-note"));

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            scroll.AddToClassList("shop-soul-scroll");
            foreach (var item in SoulShop.Items())
            {
                var it = item;
                // 重複份只列出已擁有且尚未滿突的武將，避免清單過長。
                if (it.Kind == SoulItemKind.HeroShard)
                {
                    if (!v.Heroes.TryGetValue(it.HeroId, out var hero)) continue;
                    if (hero.Stars + v.Material(HeroGrowth.ShardKey(it.HeroId)) >= HeroGrowth.MaxStars) continue;
                }
                int bought = SoulShop.Bought(v.Raw, it.Id, now);
                bool soldOut = bought >= it.MonthlyLimit;

                var row = new VisualElement();
                row.AddToClassList("card-row");
                var head = new VisualElement();
                head.AddToClassList("card-row-head");
                head.Add(UiKit.Text(it.Name, "card-row-name"));
                var purchase = soldOut
                    ? UiKit.DoneBtn("本月已兌完").WithClass("btn-sm")
                    : UiKit.Btn($"{it.Cost} 將魂", () => _ = Act(() => GameSession.Backend.BuySoulItem(it.Id), "兌換成功"), primary: souls >= it.Cost).WithClass("btn-sm");
                purchase.SetEnabled(!soldOut && souls >= it.Cost);
                purchase.tooltip = souls >= it.Cost ? "兌換此項目" : $"將魂不足，需要 {it.Cost} 將魂";
                head.Add(purchase);
                row.Add(head);
                row.Add(UiKit.Text($"本月 {bought}/{it.MonthlyLimit}", "card-row-desc"));
                scroll.Add(row);
            }
            body.Add(scroll);
        }
    }
}
