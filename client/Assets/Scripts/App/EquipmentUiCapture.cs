#nullable enable
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using SanGuo.Core.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public sealed class EquipmentUiCapture : MonoBehaviour
    {
        public static void Begin(string directory)
        {
            var go = new GameObject("Equipment UI review");
            DontDestroyOnLoad(go);
            go.AddComponent<EquipmentUiCapture>().StartCoroutine(Run(directory));
            Application.logMessageReceived += (message, trace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception) Application.Quit(1);
            };
        }

        private static IEnumerator Run(string directory)
        {
            Directory.CreateDirectory(directory);
            yield return new WaitForSecondsRealtime(2);
            if (!(GameSession.Backend is LocalBackend backend) || GameSession.ShotDir == null)
                throw new InvalidOperationException("Disposable local profile required");
            var profile = (PlayerProfile)typeof(LocalBackend).GetField("_profile", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(backend);
            foreach (string id in new[] { "xiahoudun", "liubei", "zhangjiao" }) profile.Heroes[id] = new HeroState { HeroId = id };
            profile.ClearedStages.Add(GameSession.StageIdOf(0, 1));
            foreach (var slot in Equipment.Slots)
                for (int tier = 1; tier <= Equipment.MaxTier; tier++)
                    profile.AddMaterial(Equipment.ItemKey(slot, tier), 2);
            var refresh = GameSession.Refresh();
            while (!refresh.IsCompleted) yield return null;
            HeroesPage.LastSelectedHeroId = "xiahoudun";
            var root = PageHost.Current!.GetComponent<UIDocument>().rootVisualElement;
            Nav.Go(Page.Home);
            yield return new WaitForSecondsRealtime(.8f);
            var buttons = root.Query<Button>(className: "home-fn").ToList();
            var expected = new[] { "home-heroes", "home-equipment", "home-herogrowth", "home-quests", "home-dungeons", "home-gacha", "home-shop" };
            if (!buttons.Select(b => b.name).SequenceEqual(expected)) throw new InvalidOperationException("Home button order");
            foreach (var button in buttons) CheckBounds(root, button);
            yield return CommercialUiCapture.Capture(directory, "01-home-seven-buttons");
            Submit(root.Q<Button>("home-equipment"));
            yield return new WaitForSecondsRealtime(.8f);
            if (!(PageHost.Current.ActivePage is EquipmentPage)) throw new InvalidOperationException("Home equipment route");
            Validate(root);
            yield return CommercialUiCapture.Capture(directory, "02-equipment-inventory");
            Submit(root.Q<Button>("equipment-stock-Weapon-1"));
            yield return new WaitForSecondsRealtime(.4f);
            Validate(root);
            yield return CommercialUiCapture.Capture(directory, "03-selected-item");
            Submit(root.Q<Button>("equipment-equip"));
            yield return new WaitForSecondsRealtime(.5f);
            if (GameSession.View.Heroes["xiahoudun"].Equipment["Weapon"] != 1) throw new InvalidOperationException("Equip action");
            Submit(root.Q<Button>("equipment-stock-Weapon-5"));
            yield return new WaitForSecondsRealtime(.3f);
            Submit(root.Q<Button>("equipment-equip"));
            yield return new WaitForSecondsRealtime(.5f);
            if (GameSession.View.Heroes["xiahoudun"].Equipment["Weapon"] != 5 || GameSession.View.Material(Equipment.WeaponKey(SanGuo.Core.Role.Tank, 1)) != 1)
                throw new InvalidOperationException("Replacement returns previous item");
            Submit(root.Q<Button>("equipment-slot-Weapon"));
            yield return new WaitForSecondsRealtime(.3f);
            Validate(root);
            yield return CommercialUiCapture.Capture(directory, "04-worn-item");
            Submit(root.Q<Button>("equipment-unequip"));
            yield return new WaitForSecondsRealtime(.5f);
            if (GameSession.View.Heroes["xiahoudun"].Equipment.ContainsKey("Weapon")) throw new InvalidOperationException("Unequip action");
            Submit(root.Q<Button>("equipment-filter-Armor"));
            yield return new WaitForSecondsRealtime(.3f);
            Submit(root.Q<Button>("equipment-stock-Armor-3"));
            yield return new WaitForSecondsRealtime(.3f);
            int gold = GameSession.View.Gold;
            Submit(root.Q<Button>("equipment-dismantle"));
            yield return new WaitForSecondsRealtime(.5f);
            if (GameSession.View.Gold != gold + Equipment.DismantleGold(3) || GameSession.View.Material(Equipment.ItemKey(EquipSlot.Armor, 3)) != 1)
                throw new InvalidOperationException("Dismantle action");
            Validate(root);
            yield return CommercialUiCapture.Capture(directory, "05-armor-inventory");
            root.Q<DropdownField>("equipment-hero-selector").value = GameSession.DefOf("zhangjiao")!.Name;
            yield return new WaitForSecondsRealtime(.4f);
            Submit(root.Q<Button>("equipment-filter-Weapon"));
            yield return new WaitForSecondsRealtime(.3f);
            if (!root.Q<Label>(className: "equipment-hero-name").text.Contains("張角")) throw new InvalidOperationException("Hero selector");
            Validate(root);
            yield return CommercialUiCapture.Capture(directory, "06-another-hero");
            Nav.Go(Page.HeroGrowth);
            yield return new WaitForSecondsRealtime(.6f);
            var growth = (HeroesPage)PageHost.Current.ActivePage!;
            growth.DebugSelectHero("xiahoudun");
            yield return new WaitForSecondsRealtime(.3f);
            Submit(root.Query<Button>(className: "seg-tab").ToList().First(b => b.text == "裝備"));
            yield return new WaitForSecondsRealtime(.5f);
            if (!(PageHost.Current.ActivePage is EquipmentPage) || HeroesPage.LastSelectedHeroId != "xiahoudun")
                throw new InvalidOperationException("Growth equipment route");
            Submit(root.Q<Button>(className: "hdr-back"));
            yield return new WaitForSecondsRealtime(.5f);
            if (!(PageHost.Current.ActivePage is HeroGrowthPage) || !root.Q<Button>("roster-xiahoudun").ClassListContains("strategy-roster-selected"))
                throw new InvalidOperationException("Return preserves hero");
            yield return CommercialUiCapture.Capture(directory, "07-return-to-growth");
            profile.Materials.Clear();
            refresh = GameSession.Refresh();
            while (!refresh.IsCompleted) yield return null;
            EquipmentPage.OpenFrom(Page.Home, "xiahoudun");
            yield return new WaitForSecondsRealtime(.6f);
            if (root.Query<Button>(className: "equipment-stock-tile").ToList().Count != 0) throw new InvalidOperationException("Empty inventory");
            Validate(root);
            yield return CommercialUiCapture.Capture(directory, "08-empty-inventory");
            File.WriteAllText(Path.Combine(directory, "verification.txt"), "Seven home buttons and order; equipment entry; selectable inventory; equip, replace with old item return, unequip, dismantle; hero selection; growth entry and return preserving hero; empty inventory; panel containment passed. Disposable local profile only. Visual quality reviewed separately.");
            Application.Quit();
        }

        private static void Validate(VisualElement root)
        {
            foreach (string cls in new[] { "equipment-worn-panel", "equipment-inventory-panel", "equipment-detail-panel", "equipment-stock-tile", "equipment-detail-actions" })
                foreach (var element in root.Query(className: cls).ToList()) CheckBounds(root, element);
        }

        private static void CheckBounds(VisualElement root, VisualElement element)
        {
            var b = element.worldBound;
            var r = root.worldBound;
            if (b.width <= 0 || b.height <= 0 || b.xMin < r.xMin - 1 || b.xMax > r.xMax + 1 || b.yMin < r.yMin - 1 || b.yMax > r.yMax + 1)
                throw new InvalidOperationException("Element outside viewport: " + element.name);
        }

        private static void Submit(Button button)
        {
            if (button == null) throw new InvalidOperationException("Missing equipment control");
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = button; button.SendEvent(e); }
        }
    }
}
