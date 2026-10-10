#nullable enable
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public sealed class EquipmentPage : PageBase
    {
        private static Page _returnPage = Page.Home;
        private EquipSlot _slot = EquipSlot.Weapon;
        private int _tier;
        private bool _worn;
        protected override Page Id => Page.Equipment;
        protected override string Title => "裝備";
        protected override Page BackPage => _returnPage;

        public static void OpenFrom(Page origin, string? heroId = null)
        {
            _returnPage = origin;
            if (heroId != null) HeroesPage.LastSelectedHeroId = heroId;
            Nav.Go(Page.Equipment);
        }

        protected override void BuildBody(VisualElement body)
        {
            body.AddToClassList("equipment-page");
            var view = GameSession.View;
            var owned = GameSession.OwnedHeroes();
            if (owned.Count == 0)
            {
                body.Add(UiKit.Text("尚未擁有武將", "txt-sub"));
                body.Add(UiKit.Btn("前往招募", () => Nav.Go(Page.Gacha), primary: true));
                return;
            }
            var def = owned.FirstOrDefault(d => d.Id == HeroesPage.LastSelectedHeroId) ?? owned[0];
            HeroesPage.LastSelectedHeroId = def.Id;
            var hero = view.Heroes[def.Id];
            if (_worn) hero.Equipment.TryGetValue(_slot.ToString(), out _tier);
            var left = new VisualElement().WithClass("equipment-worn-panel bpanel");
            left.name = "equipment-worn-panel";
            var selector = UiKit.Btn("更換武將  ▾", ShowHeroPicker);
            selector.name = "equipment-hero-selector";
            selector.AddToClassList("equipment-hero-selector");
            left.Add(selector);
            var identity = new VisualElement().WithClass("equipment-hero-identity");
            identity.Add(PortraitArt.Create(def.Id, "equipment-hero-face"));
            var name = new VisualElement();
            name.Add(UiKit.Text(def.Name, "equipment-hero-name"));
            name.Add(UiKit.Text($"Lv.{hero.Level}", "txt-sub"));
            identity.Add(name);
            left.Add(identity);
            left.Add(UiKit.Text("已配戴", "equipment-section-title"));
            foreach (var slot in Equipment.Slots)
            {
                var sl = slot;
                hero.Equipment.TryGetValue(sl.ToString(), out int tier);
                var button = new Button(() => { _slot = sl; _tier = tier; _worn = true; Rebuild(); }).WithClass("equipment-slot-button");
                button.name = "equipment-slot-" + sl;
                if (_slot == sl && _worn) button.AddToClassList("equipment-selected");
                button.Add(HeroesPage.EquipmentCard(def, sl, tier));
                left.Add(button);
            }
            body.Add(left);
            var inventory = new VisualElement().WithClass("equipment-inventory-panel bpanel");
            inventory.name = "equipment-inventory-panel";
            inventory.Add(UiKit.Text("可用裝備", "equipment-section-title"));
            var tabs = new VisualElement().WithClass("seg equipment-filters");
            foreach (var slot in Equipment.Slots)
            {
                var sl = slot;
                var tab = UiKit.Tab(Equipment.SlotName(sl), () => { _slot = sl; _tier = 0; _worn = false; Rebuild(); }, _slot == sl).WithClass("seg-tab");
                tab.name = "equipment-filter-" + sl;
                tabs.Add(tab);
            }
            inventory.Add(tabs);
            var scroll = new ScrollView(ScrollViewMode.Vertical).WithClass("equipment-inventory-scroll grow");
            scroll.contentContainer.AddToClassList("equipment-grid");
            int count = 0;
            for (int tier = 1; tier <= Equipment.MaxTier; tier++)
            {
                int have = Count(view, def, _slot, tier);
                if (have == 0) continue;
                count++;
                int t = tier;
                var tile = new Button(() => { _tier = t; _worn = false; Rebuild(); }).WithClass("equipment-stock-tile equipment-tier-" + tier);
                tile.name = "equipment-stock-" + _slot + "-" + tier;
                if (!_worn && _tier == tier) tile.AddToClassList("equipment-selected");
                var card = HeroesPage.EquipmentCard(def, _slot, tier);
                tile.Add(card.Q(className: "growth-equipment-art"));
                tile.Add(UiKit.Text(Equipment.Name(_slot, tier, def.Role), "equipment-item-name"));
                tile.Add(UiKit.Text(Equipment.TierLabel(tier) + " · ×" + have, "equipment-item-count"));
                scroll.Add(tile);
            }
            if (count == 0) scroll.Add(UiKit.Text("目前沒有可用的" + (_slot == EquipSlot.Weapon ? Equipment.WeaponTypeName(def.Role) : Equipment.SlotName(_slot)), "equipment-empty"));
            inventory.Add(scroll);
            for (int tier = 1; tier <= Equipment.MaxTier; tier++)
            {
                int shards = view.Material(Equipment.ShardKey(_slot, tier, def.Role));
                if (shards > 0) inventory.Add(UiKit.Text($"{Equipment.ShardName(_slot, tier, def.Role)} {shards}/{Equipment.ShardCostOf(tier)}", "equipment-shards"));
            }
            body.Add(inventory);
            body.Add(Details(def, hero, view));
        }

        private void ShowHeroPicker()
        {
            var overlay = UiHelp.Dialog(Host, "選擇武將", content =>
            {
                foreach (var hero in GameSession.OwnedHeroes())
                {
                    var def = hero;
                    var row = new Button(() =>
                    {
                        HeroesPage.LastSelectedHeroId = def.Id;
                        _tier = 0;
                        _worn = false;
                        Rebuild();
                    }).WithClass("equipment-hero-choice");
                    row.name = "equipment-choose-" + def.Id;
                    if (def.Id == HeroesPage.LastSelectedHeroId) row.AddToClassList("equipment-selected");
                    row.Add(PortraitArt.Create(def.Id, "equipment-hero-face"));
                    row.Add(UiKit.Text(def.Name, "equipment-hero-name"));
                    content.Add(row);
                }
            });
            overlay.name = "equipment-hero-picker";
            overlay.AddToClassList("equipment-hero-picker");
            overlay.Q<Button>(className: "ui-help-close").name = "equipment-hero-picker-close";
        }

        private VisualElement Details(HeroDef def, HeroState hero, ProfileView view)
        {
            var panel = new VisualElement().WithClass("equipment-detail-panel bpanel");
            panel.name = "equipment-detail-panel";
            panel.Add(UiKit.Text("裝備詳情", "equipment-section-title"));
            if (_tier == 0 || (!_worn && Count(view, def, _slot, _tier) == 0))
            {
                panel.Add(UiKit.Text(_worn ? "這個部位尚未配戴" : "點選裝備查看加成", "equipment-empty"));
                return panel;
            }
            panel.Add(UiKit.Text(Equipment.Name(_slot, _tier, def.Role), "equipment-detail-name"));
            panel.Add(UiKit.Text(Equipment.TierLabel(_tier), "equipment-item-count"));
            panel.Add(HeroesPage.EquipmentCard(def, _slot, _tier));
            var actions = new VisualElement().WithClass("equipment-detail-actions");
            int tier = _tier;
            var slot = _slot;
            if (_worn)
            {
                var unequip = UiKit.Btn("卸下", () => _ = Act(() => GameSession.Backend.Unequip(def.Id, slot.ToString()))).WithClass("btn-lg");
                unequip.name = "equipment-unequip";
                actions.Add(unequip);
            }
            else
            {
                hero.Equipment.TryGetValue(slot.ToString(), out int current);
                panel.Add(UiKit.Text(current > 0 ? "目前配戴：" + Equipment.Name(slot, current, def.Role) : "目前未配戴", "equipment-current"));
                var equip = UiKit.Btn(current > 0 ? "替換裝備" : "配戴", () => _ = Act(() => GameSession.Backend.Equip(def.Id, slot.ToString(), tier)), primary: true).WithClass("btn-lg");
                equip.name = "equipment-equip";
                actions.Add(equip);
                var dismantle = UiKit.Btn("分解  +" + Equipment.DismantleGold(tier) + " 銅錢", () => _ = Act(() => GameSession.Backend.Dismantle(slot.ToString(), tier, 1))).WithClass("btn-lg");
                dismantle.name = "equipment-dismantle";
                actions.Add(dismantle);
            }
            panel.Add(actions);
            return panel;
        }

        private static int Count(ProfileView view, HeroDef def, EquipSlot slot, int tier) =>
            view.Material(Equipment.ItemKey(slot, tier)) + (slot == EquipSlot.Weapon ? view.Material(Equipment.WeaponKey(def.Role, tier)) : 0);
    }
}
