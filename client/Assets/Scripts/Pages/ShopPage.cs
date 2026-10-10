#nullable enable
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public sealed class ShopPage : PageBase
    {
        private enum Tab { Pay, Soul, Pass, Recharge }

        private Tab _tab = Tab.Pay;

        protected override Page Id => Page.Shop;
        protected override string Title => "商店";

        protected override void BuildBody(VisualElement body)
        {
            body.style.flexDirection = FlexDirection.Column;
            body.AddToClassList("page-centered");
            body.AddToClassList("shop-page");
            var frame = new VisualElement().WithClass("shop-frame");
            body.Add(frame);
            body = frame;

            var seg = new VisualElement();
            seg.AddToClassList("seg");
            seg.AddToClassList("shop-tabs");
            seg.Add(UiKit.Tab("禮包", () => { _tab = Tab.Pay; Rebuild(); }, _tab == Tab.Pay).WithClass("seg-tab"));
            seg.Add(UiKit.Tab("元寶", () => { _tab = Tab.Recharge; Rebuild(); }, _tab == Tab.Recharge).WithClass("seg-tab"));
            seg.Add(UiKit.Tab("通行證", () => { _tab = Tab.Pass; Rebuild(); }, _tab == Tab.Pass).WithClass("seg-tab"));
            seg.Add(UiKit.Tab("將魂商店", () => { _tab = Tab.Soul; Rebuild(); }, _tab == Tab.Soul).WithClass("seg-tab"));
            body.Add(seg);

            if (_tab == Tab.Pay || _tab == Tab.Recharge)
            {
                var scroll = new ScrollView(ScrollViewMode.Vertical).WithClass("shop-products-scroll");
                body.Add(scroll);
                body = scroll.contentContainer;
            }

            if (_tab == Tab.Pay) BuildPay(body);
            else if (_tab == Tab.Recharge) BuildRecharge(body);
            else if (_tab == Tab.Pass) BuildPass(body);
            else BuildSoulShop(body);
        }

        public void DebugSetTab(int tab) { _tab = (Tab)tab; Rebuild(); }

        private void BuildRecharge(VisualElement body)
        {
            var v = GameSession.View;
            var p = v.Raw;
            var recharge = new VisualElement();
            recharge.AddToClassList("dun-row");
            recharge.AddToClassList("shop-recharge-grid");
            recharge.style.flexWrap = Wrap.Wrap;
            recharge.style.justifyContent = Justify.Center;
            body.Add(recharge);
            foreach (var product in Shop.Products())
            {
                var pr = product;
                if (pr.Kind != ProductKind.Recharge) continue;
                bool doubled = Shop.RechargeFirstTime(p, pr.Id);
                var card = new VisualElement();
                card.AddToClassList("shop-card");
                card.AddToClassList(doubled ? "shop-card-gold" : "shop-card-blue");
                var head = new VisualElement { pickingMode = PickingMode.Ignore };
                head.AddToClassList("shop-head");
                head.Add(UiKit.Text(pr.Name, "shop-name"));
                card.Add(head);
                var art = new VisualElement();
                art.AddToClassList("shop-art");
                art.Add(Item("item_yuanbao", "元寶", "用於招募武將與遊戲內消費。", doubled ? pr.ImmediateYuanbao * 2 : pr.ImmediateYuanbao).WithClass("shop-icon"));
                card.Add(art);
                var text = new VisualElement { pickingMode = PickingMode.Ignore };
                text.AddToClassList("card-body");
                text.Add(UiKit.Text(doubled ? "首次購買雙倍" : $"{pr.ImmediateYuanbao} 元寶", doubled ? "txt-good" : "line-sub").WithClass("dun-center"));
                card.Add(text);
                var btns = new VisualElement();
                btns.AddToClassList("card-footer");
                btns.Add(UiKit.Btn($"¥{pr.PriceCny}", () => _ = Act(() => GameSession.Backend.BuyWithTestPayment(pr.Id), "儲值成功"), primary: doubled));
                card.Add(btns);
                recharge.Add(card);
            }
        }

        private void BuildPay(VisualElement body)
        {
            var v = GameSession.View;
            var p = v.Raw;

            var row = new VisualElement();
            row.AddToClassList("dun-row");
            body.Add(row);

            if (!p.FirstPackBought && Shop.Find(Shop.FirstPack) is ProductDef first)
            {
                var card = new VisualElement();
                card.AddToClassList("shop-card");
                card.AddToClassList("shop-card-gold");
                var head = new VisualElement { pickingMode = PickingMode.Ignore };
                head.AddToClassList("shop-head");
                head.Add(UiKit.Text(first.Name, "shop-name"));
                card.Add(head);
                var art = new VisualElement();
                art.AddToClassList("shop-art");
                art.Add(Rewards(Shop.FirstPackReward()));
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

                var art = new VisualElement();
                art.AddToClassList("shop-art");
                var formula = new VisualElement().WithClass("shop-formula");
                formula.Add(Item("item_yuanbao", "元寶", "購買月卡立即取得。", pr.ImmediateYuanbao));
                formula.Add(UiKit.Text("＋", "shop-operator"));
                formula.Add(Item("item_yuanbao", "每日元寶", $"每天可領取一次，持續 {pr.Days} 天。", pr.DailyYuanbao));
                formula.Add(UiKit.Text($"× {pr.Days}", "shop-operator"));
                art.Add(formula);
                var stamina = new VisualElement().WithClass("shop-formula");
                stamina.Add(Item("item_stamina", "每日體力", $"每天可領取一次，持續 {pr.Days} 天。", pr.DailyStamina));
                stamina.Add(UiKit.Text($"× {pr.Days}", "shop-operator"));
                art.Add(stamina);
                card.Add(art);

                var text = new VisualElement { pickingMode = PickingMode.Ignore };
                text.AddToClassList("card-body");
                var btns = new VisualElement();
                btns.AddToClassList("card-footer");
                int left = Shop.MonthCardDaysLeft(p, pr.Id, v.Now);
                bool claimed = Shop.MonthCardClaimedToday(p, pr.Id, v.Now);
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
            top.AddToClassList("shop-pass-top");
            info.Add(UiKit.Text($"{WorldBoss.SeasonName(BattlePass.SeasonOf(v.Now))}　Lv.{level} / {BattlePass.MaxLevel}　" +
                (tier != "" ? "（已購買）" : "（免費）"), "line-title"));
            info.Add(UiKit.Bar(level >= BattlePass.MaxLevel ? 100f : 100f * (points % BattlePass.PointsPerLevel) / BattlePass.PointsPerLevel, "bar-gold bar-slim"));
            info.Add(UiKit.Text(level >= BattlePass.MaxLevel ? "已滿級" : $"{points % BattlePass.PointsPerLevel} / {BattlePass.PointsPerLevel}", "line-sub"));
            info.tooltip = "消耗 1 點體力獲得 1 點通行證經驗";
            top.Add(info);
            top.Add(UiKit.Btn("一鍵領取", () => _ = Act(() => GameSession.Backend.ClaimPassAll(), "已領取"), primary: true).WithClass("btn-sm"));
            if (tier == "")
            {
                foreach (var id in new[] { Shop.PassBasic })
                {
                    var pr = Shop.Find(id)!;
                    var buy = UiKit.Btn($"{pr.Name}　¥{pr.PriceCny}", () => _ = Act(() => GameSession.Backend.BuyWithTestPayment(pr.Id), "購買成功")).WithClass("btn-sm");
                    buy.tooltip = "解鎖付費獎勵";
                    buy.style.marginLeft = 8;
                    top.Add(buy);
                }
            }
            body.Add(top);

            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.AddToClassList("grow");
            scroll.AddToClassList("shop-pass-scroll");
            for (int lv = 1; lv <= BattlePass.MaxLevel; lv++)
            {
                int l = lv;
                var row = new VisualElement();
                row.AddToClassList("shop-pass-card");
                var head = new VisualElement();
                head.AddToClassList("shop-pass-level");
                var lvText = UiKit.Text($"Lv.{l}", level >= l ? "txt-gold" : "card-row-name");
                lvText.style.width = 80;
                lvText.style.flexShrink = 0;
                lvText.style.whiteSpace = WhiteSpace.NoWrap;
                head.Add(lvText);
                head.Add(PassCell(Rewards(BattlePass.FreeReward(l)), "免費", level >= l, !stale && p.Pass.ClaimedFree.Contains(l), true,
                    () => _ = Act(() => GameSession.Backend.ClaimPass(l, false), "已領取")));
                head.Add(PassCell(Rewards(BattlePass.PaidReward(l)), "付費", level >= l, !stale && p.Pass.ClaimedPaid.Contains(l), tier != "",
                    () => _ = Act(() => GameSession.Backend.ClaimPass(l, true), "已領取")));
                row.Add(head);
                scroll.Add(row);
            }
            body.Add(scroll);
            body.Add(UiKit.Text("測試環境：購買不會實際扣款", "foot-note"));
        }

        private static VisualElement PassCell(VisualElement tiles, string label, bool reached, bool claimed, bool owned, System.Action claim)
        {
            var cell = new VisualElement();
            cell.AddToClassList("shop-pass-cell");
            cell.style.flexDirection = FlexDirection.Column;
            cell.style.alignItems = Align.Center;
            cell.style.flexGrow = 1;
            cell.style.marginLeft = 0;
            var tag = UiKit.Text(label, "line-sub");
            tag.style.flexShrink = 0;
            tag.style.whiteSpace = WhiteSpace.NoWrap;
            cell.Add(tag);
            tiles.style.marginLeft = 8;
            tiles.style.marginRight = 8;
            tiles.style.flexDirection = FlexDirection.Row;
            tiles.style.flexWrap = Wrap.Wrap;
            tiles.AddToClassList("pass-reward-tiles");
            tiles.style.width = 252;
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
            body.Add(UiKit.Text($"將魂 {souls}", "line-title shop-soul-note"));

            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.AddToClassList("grow");
            scroll.AddToClassList("shop-exchange-scroll");
            VisualElement? column = null;
            int visible = 0;
            foreach (var item in SoulShop.Items())
            {
                var it = item;
                if (it.Kind == SoulItemKind.HeroShard)
                {
                    if (!v.Heroes.TryGetValue(it.HeroId, out var hero)) continue;
                    if (hero.Stars + v.Material(HeroGrowth.ShardKey(it.HeroId)) >= HeroGrowth.MaxStars) continue;
                }
                int bought = SoulShop.Bought(v.Raw, it.Id, now);
                bool soldOut = bought >= it.MonthlyLimit;

                var row = new VisualElement();
                row.AddToClassList("shop-exchange-card");
                var head = new VisualElement();
                head.AddToClassList("shop-exchange-head");
                head.Add(UiKit.Text(it.Name, "card-row-name"));
                if (it.Kind == SoulItemKind.HeroShard)
                {
                    var portrait = Item("item_shard", it.Name, "武將專屬信物，用於突破。", 1);
                    var face = HeroArt.Face(it.HeroId);
                    if (face != null) portrait.style.backgroundImage = new StyleBackground(face);
                    head.Add(portrait.WithClass("shop-exchange-icon"));
                }
                else if (it.Kind == SoulItemKind.Equipment)
                    head.Add(Material(Equipment.ItemKey(it.Slot, it.Tier), 1).WithClass("shop-exchange-icon"));
                else
                    head.Add(Item(it.Kind == SoulItemKind.Gold ? "item_gold" : "item_expbook", it.Name,
                        it.Kind == SoulItemKind.Gold ? "用於武將養成與裝備強化。" : "用於提升武將等級。", it.Amount).WithClass("shop-exchange-icon"));
                var purchase = soldOut
                    ? UiKit.DoneBtn("本月已兌完").WithClass("btn-sm")
                    : UiKit.Btn($"{it.Cost} 將魂", () => _ = Act(() => GameSession.Backend.BuySoulItem(it.Id), "兌換成功"), primary: souls >= it.Cost).WithClass("btn-sm");
                purchase.SetEnabled(!soldOut && souls >= it.Cost);
                purchase.tooltip = souls >= it.Cost ? "兌換此項目" : $"將魂不足，需要 {it.Cost} 將魂";
                head.Add(purchase);
                row.Add(head);
                row.Add(UiKit.Text($"本月 {bought}/{it.MonthlyLimit}", "card-row-desc"));
                if (visible++ % 2 == 0)
                {
                    column = new VisualElement().WithClass("shop-exchange-column");
                    scroll.Add(column);
                }
                column!.Add(row);
            }
            body.Add(scroll);
        }

        private VisualElement Item(string icon, string name, string description, int count)
        {
            var tile = UiKit.ItemTile(icon, count.ToString());
            tile.pickingMode = PickingMode.Position;
            tile.focusable = true;
            tile.tooltip = $"{name} ×{count}\n{description}";
            void Show() => UiHelp.Show(Host, name, $"數量：{count}\n{description}");
            tile.RegisterCallback<ClickEvent>(e => { Show(); e.StopPropagation(); });
            tile.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == UnityEngine.KeyCode.Return || e.keyCode == UnityEngine.KeyCode.Space) { Show(); e.StopPropagation(); }
            });
            return tile;
        }

        private VisualElement Material(string key, int amount)
        {
            if (Equipment.TryParseKey(key, out var slot, out int tier))
            {
                var tile = Item("item_chest", Equipment.Name(slot, tier), $"{Equipment.SlotName(slot)}，可在裝備頁配戴。", amount);
                string icon = slot == EquipSlot.Weapon ? "weapon_warrior" : slot == EquipSlot.Armor ? "armor" : "accessory";
                var tex = UnityEngine.Resources.Load<UnityEngine.Texture2D>("EquipmentArt/" + icon);
                if (tex != null) tile.style.backgroundImage = new StyleBackground(tex);
                return tile;
            }
            return Item(key == HeroGrowth.HeroExp ? "item_expbook" : "item_shard", UiText.MaterialName(key),
                key == HeroGrowth.HeroExp ? "用於提升武將等級。" : "用於將魂商店兌換商品。", amount);
        }

        private VisualElement Rewards(Reward reward)
        {
            var row = new VisualElement().WithClass("reward-tiles");
            if (reward.Yuanbao > 0) row.Add(Item("item_yuanbao", "元寶", "用於招募武將與遊戲內消費。", reward.Yuanbao));
            if (reward.Gold > 0) row.Add(Item("item_gold", "金幣", "用於武將養成與裝備強化。", reward.Gold));
            if (reward.Stamina > 0) row.Add(Item("item_stamina", "體力", "用於出征與副本。", reward.Stamina));
            foreach (var material in reward.Materials) row.Add(Material(material.Key, material.Value));
            return row;
        }
    }
}
