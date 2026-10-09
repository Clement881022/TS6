#nullable enable
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// 商店：「儲值」是首儲禮包與月卡（測試付款）；「通行證」每月一季，消耗體力升級，免費線與付費線各自領取；
    /// 「將魂商店」用滿突後溢出的重複武將轉成的將魂兌換重複份、武將經驗、金幣與裝備，各項每月限購。
    /// </summary>
    public sealed class ShopPage : PageBase
    {
        private enum Tab { Pay, Soul, Pass }

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
            seg.Add(UiKit.Tab("通行證", () => { _tab = Tab.Pass; Rebuild(); }, _tab == Tab.Pass).WithClass("seg-tab"));
            seg.Add(UiKit.Tab("將魂商店", () => { _tab = Tab.Soul; Rebuild(); }, _tab == Tab.Soul).WithClass("seg-tab"));
            body.Add(seg);

            if (_tab == Tab.Pay) BuildPay(body);
            else if (_tab == Tab.Pass) BuildPass(body);
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

            // 首儲禮包（買過就不再顯示）
            if (!p.FirstPackBought && Shop.Find(Shop.FirstPack) is ProductDef first)
            {
                var card = new VisualElement();
                card.AddToClassList("shop-card");
                card.AddToClassList("shop-card-gold");
                var head = new VisualElement { pickingMode = PickingMode.Ignore };
                head.AddToClassList("shop-head");
                head.Add(UiKit.Text(first.Name, "shop-name"));
                card.Add(head);
                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("shop-art");
                art.Add(UiKit.RewardTiles(Shop.FirstPackReward()));
                card.Add(art);
                var text = new VisualElement { pickingMode = PickingMode.Ignore };
                text.AddToClassList("card-body");
                text.Add(UiKit.Text("每個帳號限購一次", "line-title").WithClass("dun-center"));
                card.Add(text);
                var btns = new VisualElement();
                btns.AddToClassList("card-footer");
                btns.Add(UiKit.Btn($"購買　¥{first.PriceCny}", () => _ = Act(() => GameSession.Backend.BuyWithTestPayment(Shop.FirstPack), "購買成功"), primary: true));
                card.Add(btns);
                row.Add(card);
            }

            foreach (var product in Shop.Products())
            {
                var pr = product;
                if (pr.Kind != ProductKind.MonthCard) continue;
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

        public void DebugShowPass() { _tab = Tab.Pass; Rebuild(); }

        private void BuildPass(VisualElement body)
        {
            var v = GameSession.View;
            var p = v.Raw;
            // 顯示用：存檔停在上一季時視為新的一季（實際重置以後端為準）。
            bool stale = p.Pass.Season != BattlePass.SeasonOf(v.Now);
            int points = stale ? 0 : p.Pass.Points;
            string tier = stale ? "" : p.Pass.Tier;
            int level = System.Math.Min(BattlePass.MaxLevel, points / BattlePass.PointsPerLevel);

            var top = UiKit.Panel();
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;
            top.style.marginBottom = 8;
            var info = new VisualElement();
            info.style.flexGrow = 1;
            info.Add(UiKit.Text($"{WorldBoss.SeasonName(BattlePass.SeasonOf(v.Now))}通行證　Lv.{level} / {BattlePass.MaxLevel}　" +
                (tier == BattlePass.Luxury ? "（豪華）" : tier == BattlePass.Basic ? "（已購買）" : "（免費）"), "line-title"));
            info.Add(UiKit.Bar(level >= BattlePass.MaxLevel ? 100f : 100f * (points % BattlePass.PointsPerLevel) / BattlePass.PointsPerLevel, "bar-gold bar-slim"));
            info.Add(UiKit.Text(level >= BattlePass.MaxLevel ? "已滿級" : $"下一級 {points % BattlePass.PointsPerLevel} / {BattlePass.PointsPerLevel}　｜　消耗 1 點體力 = 1 點通行證經驗", "line-sub"));
            top.Add(info);
            top.Add(UiKit.Btn("一鍵領取", () => _ = Act(() => GameSession.Backend.ClaimPassAll(), "已領取"), primary: true).WithClass("btn-sm"));
            if (tier == "")
            {
                foreach (var id in new[] { Shop.PassBasic, Shop.PassLuxury })
                {
                    var pr = Shop.Find(id)!;
                    var buy = UiKit.Btn($"{pr.Name}　¥{pr.PriceCny}", () => _ = Act(() => GameSession.Backend.BuyWithTestPayment(pr.Id), "購買成功")).WithClass("btn-sm");
                    buy.tooltip = id == Shop.PassLuxury ? $"解鎖付費獎勵，立即 +{BattlePass.LuxuryBonusLevels} 級並送 {pr.ImmediateYuanbao} 元寶" : "解鎖付費獎勵";
                    buy.style.marginLeft = 8;
                    top.Add(buy);
                }
            }
            body.Add(top);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            scroll.AddToClassList("shop-soul-scroll");
            for (int lv = 1; lv <= BattlePass.MaxLevel; lv++)
            {
                int l = lv;
                var row = new VisualElement();
                row.AddToClassList("card-row");
                var head = new VisualElement();
                head.AddToClassList("card-row-head");
                var lvText = UiKit.Text($"Lv.{l}", level >= l ? "txt-gold" : "card-row-name");
                lvText.style.width = 80;
                lvText.style.flexShrink = 0;
                lvText.style.whiteSpace = WhiteSpace.NoWrap;
                head.Add(lvText);
                head.Add(PassCell(UiKit.RewardTiles(BattlePass.FreeReward(l)), "免費", level >= l, !stale && p.Pass.ClaimedFree.Contains(l), true,
                    () => _ = Act(() => GameSession.Backend.ClaimPass(l, false), "已領取")));
                head.Add(PassCell(UiKit.RewardTiles(BattlePass.PaidReward(l)), "付費", level >= l, !stale && p.Pass.ClaimedPaid.Contains(l), tier != "",
                    () => _ = Act(() => GameSession.Backend.ClaimPass(l, true), "已領取")));
                row.Add(head);
                scroll.Add(row);
            }
            body.Add(scroll);
            body.Add(UiKit.Text("測試環境：購買不會實際扣款", "foot-note"));
        }

        /// <summary>通行證一格：獎勵圖示 + 領取按鈕（未達成 / 未購買 / 已領取時停用）。</summary>
        private static VisualElement PassCell(VisualElement tiles, string label, bool reached, bool claimed, bool owned, System.Action claim)
        {
            var cell = new VisualElement();
            cell.style.flexDirection = FlexDirection.Row;
            cell.style.alignItems = Align.Center;
            cell.style.flexGrow = 1;
            cell.style.marginLeft = 32;
            var tag = UiKit.Text(label, "line-sub");
            tag.style.flexShrink = 0;
            tag.style.whiteSpace = WhiteSpace.NoWrap;
            cell.Add(tag);
            tiles.style.marginLeft = 8;
            tiles.style.marginRight = 8;
            tiles.style.flexDirection = FlexDirection.Row;
            tiles.style.flexWrap = Wrap.NoWrap;
            tiles.style.width = 240;
            tiles.style.flexShrink = 0;
            cell.Add(tiles);
            Button btn = claimed ? UiKit.DoneBtn("已領取") : !owned ? UiKit.DoneBtn("未購買") : !reached ? UiKit.DoneBtn("未達成") : UiKit.Btn("領取", claim, primary: true);
            btn.AddToClassList("btn-sm");
            btn.style.flexShrink = 0;
            cell.Add(btn);
            return cell;
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
