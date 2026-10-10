#nullable enable
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using SanGuo.Core.Meta;

namespace SanGuo.Client
{
    public sealed class UnifiedUiCapture : MonoBehaviour
    {
        public static void Begin(string directory)
        {
            var go = new GameObject("Unified UI review");
            DontDestroyOnLoad(go);
            go.AddComponent<UnifiedUiCapture>().StartCoroutine(Run(directory));
            Application.logMessageReceived += (message, trace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception) Application.Quit(1);
            };
        }

        private static IEnumerator Run(string directory)
        {
            Directory.CreateDirectory(directory);
            yield return new WaitForSecondsRealtime(2);
            var root = PageHost.Current!.GetComponent<UIDocument>().rootVisualElement;
            Nav.Go(Page.Gacha);
            yield return new WaitForSecondsRealtime(1);
            var seed = ((GachaPage)PageHost.Current.ActivePage!).DebugTenPull();
            while (!seed.IsCompleted) yield return null;
            if (GameSession.OwnedHeroes().Count == 0) throw new InvalidOperationException("Review roster is empty");
            yield return new WaitForSecondsRealtime(2);
            yield return CommercialUiCapture.Capture(directory, "gacha-results");
            foreach (var page in new[] { Page.Home, Page.Map, Page.Heroes, Page.HeroGrowth, Page.Equipment, Page.Gacha,
                Page.Dungeons, Page.WorldBoss, Page.Quests, Page.Shop, Page.Formation, Page.Account, Page.Login })
            {
                if (page == Page.Formation) GameSession.FormationStageId = GameSession.StageIdOf(0, DemoMeta.FirstOpenFormationLevel);
                Nav.Go(page);
                yield return new WaitForSecondsRealtime(1.2f);
                Validate(root, page);
                yield return CommercialUiCapture.Capture(directory, page.ToString().ToLowerInvariant());
                var active = PageHost.Current.ActivePage;
                if (active is HeroesPage heroes)
                {
                    for (int tab = 1; tab <= 2; tab++)
                    {
                        heroes.DebugSetTab(tab);
                        yield return new WaitForSecondsRealtime(.5f);
                        Validate(root, page);
                        yield return CommercialUiCapture.Capture(directory, page.ToString().ToLowerInvariant() + "-tab-" + tab);
                    }
                }
                else if (active is ShopPage shop)
                {
                    var equipmentTile = root.Query(className: "item-tile").ToList().Find(tile => tile.tooltip.Contains("可在裝備頁配戴"));
                    if (equipmentTile == null) throw new InvalidOperationException("Shop equipment details missing");
                    using (var click = ClickEvent.GetPooled()) { click.target = equipmentTile; equipmentTile.SendEvent(click); }
                    yield return new WaitForSecondsRealtime(.3f);
                    if (root.Q(className: "ui-help-overlay") == null) throw new InvalidOperationException("Shop item click did not open details");
                    if (!root.Q<Label>(className: "ui-help-text").text.Contains("攻擊 +11%")) throw new InvalidOperationException("Shop weapon effect missing");
                    yield return CommercialUiCapture.Capture(directory, "shop-equipment-detail");
                    root.Q(className: "ui-help-overlay").RemoveFromHierarchy();
                    for (int tab = 1; tab <= 3; tab++)
                    {
                        shop.DebugSetTab(tab);
                        yield return new WaitForSecondsRealtime(.5f);
                        Validate(root, page);
                        yield return CommercialUiCapture.Capture(directory, "shop-tab-" + tab);
                        if (tab == 1)
                        {
                            var accessory = root.Query(className: "shop-equipment-item").ToList().Find(tile => tile.tooltip.Contains("飾品"));
                            if (accessory == null) throw new InvalidOperationException("Shop accessory missing");
                            using (var click = ClickEvent.GetPooled()) { click.target = accessory; accessory.SendEvent(click); }
                            yield return new WaitForSecondsRealtime(.3f);
                            var text = root.Q<Label>(className: "ui-help-text").text;
                            if (!text.Contains("爆擊率 +7 個百分點") || !text.Contains("閃避率 +4 個百分點")) throw new InvalidOperationException("Shop accessory effects missing");
                            yield return CommercialUiCapture.Capture(directory, "shop-accessory-detail");
                            root.Q(className: "ui-help-overlay").RemoveFromHierarchy();
                        }
                        if (tab == 2)
                        {
                            var scroll = root.Q<ScrollView>(className: "shop-pass-scroll");
                            yield return ValidateShopScroll(root, scroll);
                            scroll.horizontalScroller.value = scroll.horizontalScroller.highValue;
                            yield return new WaitForSecondsRealtime(.5f);
                            Validate(root, page);
                            yield return CommercialUiCapture.Capture(directory, "shop-pass-end");
                        }
                    }
                }
                else if (active is GachaPage gacha)
                {
                    gacha.DebugShowRates();
                    yield return new WaitForSecondsRealtime(.5f);
                    yield return CommercialUiCapture.Capture(directory, "gacha-rates");
                    root.Q(className: "ui-help-overlay")?.RemoveFromHierarchy();
                }
                else if (active is MapPage map)
                {
                    map.DebugOpenStage(1);
                    yield return new WaitForSecondsRealtime(.5f);
                    yield return CommercialUiCapture.Capture(directory, "map-stage");
                }
                else if (active is QuestsPage quests)
                {
                    yield return ValidateQuestDrag(root);
                    quests.DebugShowSevenDay();
                    yield return new WaitForSecondsRealtime(.5f);
                    Validate(root, page);
                    yield return CommercialUiCapture.Capture(directory, "quests-seven-day");
                }
                if (page == Page.Account)
                {
                    UiHelp.Show(root.Q(className: "page-host"), "操作提示", UiHelp.Text(page));
                    yield return new WaitForSecondsRealtime(.5f);
                    yield return CommercialUiCapture.Capture(directory, "help");
                }
            }
            GameSession.Select(0, 6);
            GameSession.Ticket = null;
            Nav.Go(Page.Battle);
            yield return new WaitForSecondsRealtime(1.5f);
            var battle = ((BattlePage)PageHost.Current.ActivePage!).Screen!;
            battle.DebugReviewScenario(6);
            yield return new WaitForSecondsRealtime(1);
            battle.DebugValidateCommercialArt();
            yield return CommercialUiCapture.Capture(directory, "battle-reference");
            File.WriteAllText(Path.Combine(directory, "verification.txt"),
                "Thirteen pages, roster/growth tabs, four shop tabs, recruitment rates/results, stage detail, seven-day quests and help captured. Header resource margins, panel colors and layout bounds passed. Battle commercial-art structure passed. Visual quality requires screenshot review; account is offline mode.");
            Application.Quit();
        }

        private static IEnumerator ValidateShopScroll(VisualElement root, ScrollView scroll)
        {
            var viewport = scroll.contentViewport;
            var tile = scroll.Q(className: "item-tile");
            if (scroll.Q(className: "shop-pass-card").worldBound.yMax > viewport.worldBound.yMax + 1)
                throw new InvalidOperationException("Pass reward card is clipped vertically");
            using (var wheel = WheelEvent.GetPooled(new Event { type = EventType.ScrollWheel, delta = new Vector2(0, 3), mousePosition = tile.worldBound.center }))
            {
                wheel.target = tile;
                tile.SendEvent(wheel);
            }
            yield return new WaitForSecondsRealtime(.2f);
            if (scroll.scrollOffset.x < 100) throw new InvalidOperationException("Shop vertical wheel did not scroll horizontally");
            scroll.scrollOffset = Vector2.zero;
            yield return new WaitForSecondsRealtime(.2f);
            var card = scroll.Q(className: "shop-pass-card");
            var from = card.worldBound.center;
            Drag(card, viewport, from, from - new Vector2(120, 0));
            if (scroll.scrollOffset.x < 119) throw new InvalidOperationException("Shop card drag did not scroll");
            float released = scroll.scrollOffset.x;
            using (var move = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseMove, mousePosition = from }))
            {
                move.target = viewport;
                viewport.SendEvent(move);
            }
            if (Mathf.Abs(scroll.scrollOffset.x - released) > 1) throw new InvalidOperationException("Shop drag continued after release");
            scroll.scrollOffset = Vector2.zero;
            yield return new WaitForSecondsRealtime(.2f);
            from = tile.worldBound.center;
            Drag(tile, viewport, from, from - new Vector2(100, 0));
            if (scroll.scrollOffset.x < 99) throw new InvalidOperationException("Shop item drag did not scroll");
            using (var click = ClickEvent.GetPooled()) { click.target = tile; tile.SendEvent(click); }
            if (root.Q(className: "ui-help-overlay") != null) throw new InvalidOperationException("Dragging shop item opened details");
            scroll.scrollOffset = Vector2.zero;
            yield return new WaitForSecondsRealtime(.2f);
            var thumb = scroll.horizontalScroller.Q(className: "unity-base-slider__dragger");
            var track = scroll.horizontalScroller.Q(className: "unity-base-slider__drag-container");
            if (thumb.worldBound.height < 12 || track.worldBound.width < viewport.worldBound.width - 4)
                throw new InvalidOperationException("Shop scrollbar geometry");
            from = thumb.worldBound.center;
            Drag(thumb, thumb, from, from + new Vector2(180, 0));
            yield return new WaitForSecondsRealtime(.2f);
            if (scroll.scrollOffset.x < 100) throw new InvalidOperationException("Shop scrollbar thumb drag did not scroll");
            scroll.scrollOffset = Vector2.zero;
            yield return new WaitForSecondsRealtime(.2f);
            from = tile.worldBound.center;
            Drag(tile, tile, from, from);
            using (var click = ClickEvent.GetPooled()) { click.target = tile; tile.SendEvent(click); }
            if (root.Q(className: "ui-help-overlay") == null) throw new InvalidOperationException("Normal item click after dragging did not open details");
            root.Q(className: "ui-help-overlay").RemoveFromHierarchy();
            Debug.Log("[shop] horizontal wheel, card drag, item drag, release, click suppression and thumb drag passed");
        }

        private static void Drag(VisualElement target, VisualElement receiver, Vector2 from, Vector2 to)
        {
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = from }))
            {
                down.target = target;
                target.SendEvent(down);
            }
            using (var move = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, button = 0, mousePosition = to }))
            {
                move.target = receiver;
                receiver.SendEvent(move);
            }
            using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = to }))
            {
                up.target = receiver;
                receiver.SendEvent(up);
            }
        }

        private static IEnumerator ValidateQuestDrag(VisualElement root)
        {
            var scroll = root.Q<ScrollView>(className: "quest-scroll");
            scroll.style.height = 160;
            scroll.style.flexGrow = 0;
            yield return new WaitForSecondsRealtime(.3f);
            if (scroll.verticalScroller.highValue <= 0) throw new InvalidOperationException("Quest drag review requires overflow");
            var viewport = scroll.contentViewport;
            var card = scroll.Q(className: "quest-card2");
            var from = card.worldBound.center;
            var destination = from - new Vector2(0, 80);
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = from }))
            {
                down.target = card;
                card.SendEvent(down);
            }
            using (var move = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, button = 0, mousePosition = destination }))
            {
                move.target = viewport;
                viewport.SendEvent(move);
            }
            using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = destination }))
            {
                up.target = viewport;
                viewport.SendEvent(up);
            }
            if (scroll.scrollOffset.y < 79) throw new InvalidOperationException("Quest card mouse drag did not scroll");
            float releasedOffset = scroll.scrollOffset.y;
            using (var move = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseMove, mousePosition = from }))
            {
                move.target = viewport;
                viewport.SendEvent(move);
            }
            if (Mathf.Abs(scroll.scrollOffset.y - releasedOffset) > 1) throw new InvalidOperationException("Quest drag continued after release");
            scroll.style.height = StyleKeyword.Null;
            scroll.style.flexGrow = StyleKeyword.Null;
            scroll.scrollOffset = Vector2.zero;
            yield return new WaitForSecondsRealtime(.3f);
            Debug.Log("[quests] mouse card drag and release passed");
        }

        private static void Validate(VisualElement root, Page page)
        {
            if (page == Page.Home)
            {
                if (root.Q(className: "unified-ui") != null) throw new InvalidOperationException("Reference home style was overridden");
                return;
            }
            var host = root.Q(className: "unified-ui");
            if (host == null) throw new InvalidOperationException("Missing unified theme: " + page);
            var header = host.Q(className: "hdr");
            if (header != null) CheckColor(header, new Color(29 / 255f, 29 / 255f, 33 / 255f, .96f), page);
            if (header != null)
                foreach (var resource in header.Query(className: "res-pill").ToList())
                {
                    var box = resource.worldBound;
                    var bounds = header.worldBound;
                    if (box.yMin < bounds.yMin + 12 || box.yMax > bounds.yMax - 12
                        || box.xMin < bounds.xMin || box.xMax > bounds.xMax)
                        throw new InvalidOperationException("Header resource margin: " + page);
                }
            foreach (string cls in new[] { "hero-left", "hero-right", "recruit-selection", "recruit-banner", "quest-claim-all", "account-panel", "hdr" })
            {
                var element = host.Q(className: cls);
                if (element == null) continue;
                var box = element.worldBound;
                var bounds = root.worldBound;
                if (box.width <= 0 || box.height <= 0 || box.xMin < bounds.xMin - 1 || box.xMax > bounds.xMax + 1
                    || box.yMin < bounds.yMin - 1 || box.yMax > bounds.yMax + 1)
                    throw new InvalidOperationException("UI outside viewport: " + page + "/" + cls);
            }
            var panel = host.Q(className: "panel") ?? host.Q(className: "account-panel");
            if (panel != null) CheckColor(panel, new Color(35 / 255f, 35 / 255f, 39 / 255f, .96f), page);
            foreach (var node in host.Query<Button>(className: "milestone-node").ToList())
                if (node.worldBound.xMin < root.worldBound.xMin || node.worldBound.xMax > root.worldBound.xMax)
                    throw new InvalidOperationException("Milestone outside viewport");
            if (page == Page.Shop)
            {
                var frame = host.Q(className: "shop-frame");
                var tabs = host.Q(className: "shop-tabs");
                if (frame == null || tabs == null || frame.worldBound.yMax > root.worldBound.yMax - 8)
                    throw new InvalidOperationException("Shop frame or bottom margin missing");
                if (host.Q(className: "shop-soul-resource")?.Q(className: "res-pill-icon").resolvedStyle.backgroundImage.texture == null || host.Q(className: "shop-soul-note") != null)
                    throw new InvalidOperationException("Shop soul resource icon or header placement missing");
                foreach (var name in host.Query<Label>(className: "card-row-name").ToList())
                    if (name.text.Contains("重複份")) throw new InvalidOperationException("Shop token naming");
                foreach (var cost in host.Query<Button>(className: "shop-soul-cost").ToList())
                    if (cost.Q(className: "shop-soul-cost-icon") == null) throw new InvalidOperationException("Shop soul cost icon missing");
                var claim = host.Q<Button>(className: "shop-pass-claim-all");
                if (claim != null && (claim.worldBound.xMax < frame.worldBound.xMax - 40 || claim.worldBound.yMax < frame.worldBound.yMax - 90))
                    throw new InvalidOperationException("Pass claim all is not at bottom right");
                foreach (var scroll in host.Query<ScrollView>().ToList())
                    if (tabs.worldBound.yMax > scroll.worldBound.yMin + 1)
                        throw new InvalidOperationException("Shop tabs overlap scrolling products");
                foreach (string cls in new[] { "shop-pass-scroll", "shop-exchange-scroll" })
                {
                    var track = host.Q<ScrollView>(className: cls);
                    if (track != null && (track.mode != ScrollViewMode.Horizontal || (cls == "shop-pass-scroll" && track.horizontalScroller.highValue <= 0)))
                        throw new InvalidOperationException("Shop horizontal track missing: " + cls);
                }
                var cards = host.Query(className: "shop-card").ToList();
                if (cards.Count == 6)
                    for (int i = 3; i < 6; i++)
                        if (cards[i].worldBound.yMin - cards[i - 3].worldBound.yMax < 20)
                            throw new InvalidOperationException("Recharge rows lack spacing");
            }
        }

        private static void CheckColor(VisualElement element, Color expected, Page page)
        {
            var actual = element.resolvedStyle.backgroundColor;
            if (Mathf.Abs(actual.r - expected.r) > .01f || Mathf.Abs(actual.g - expected.g) > .01f
                || Mathf.Abs(actual.b - expected.b) > .01f || Mathf.Abs(actual.a - expected.a) > .01f)
                throw new InvalidOperationException("Theme color mismatch: " + page + "/" + element.GetClasses());
        }
    }
}
