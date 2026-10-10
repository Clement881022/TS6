using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    public enum SoulItemKind { HeroShard, HeroExp, Gold, Equipment }

    public sealed class SoulShopItem
    {
        public string Id = "";
        public string Name = "";
        public SoulItemKind Kind;
        public int Cost;
        public int MonthlyLimit;
        public string HeroId = "";
        public int Amount;
        public EquipSlot Slot;
        public int Tier;
        public Role? EquipmentRole;
    }

    public enum SoulShopResult
    {
        Ok,
        UnknownItem,
        NotEnoughSouls,
        LimitReached,
        HeroNotOwned,
        HeroMaxed,
    }

    public static class SoulShop
    {
        public static List<SoulShopItem> Items()
        {
            var items = new List<SoulShopItem>();
            foreach (var h in HeroRoster.All().Where(h => h.Rarity != Rarity.R && !HeroRoster.StoryHeroIds.Contains(h.Id)))
            {
                items.Add(new SoulShopItem
                {
                    Id = "shard:" + h.Id, Name = h.Name + "　信物", Kind = SoulItemKind.HeroShard, HeroId = h.Id,
                    Cost = h.Rarity == Rarity.UR ? 300 : 100, MonthlyLimit = h.Rarity == Rarity.UR ? 1 : 3,
                });
            }
            items.Add(new SoulShopItem { Id = "hero_exp", Name = "武將經驗 ×1000", Kind = SoulItemKind.HeroExp, Amount = 1000, Cost = 30, MonthlyLimit = 5 });
            items.Add(new SoulShopItem { Id = "gold", Name = "金幣 ×5000", Kind = SoulItemKind.Gold, Amount = 5000, Cost = 30, MonthlyLimit = 5 });
            foreach (var slot in Equipment.Slots)
            {
                if (slot == EquipSlot.Accessory)
                {
                    foreach (bool crit in new[] { true, false })
                    {
                        var role = crit ? Role.Warrior : Role.Tank;
                        items.Add(new SoulShopItem { Id = Equipment.AccessoryKey(crit, 3), Name = Equipment.Name(slot, 3, role), Kind = SoulItemKind.Equipment,
                            Slot = slot, Tier = 3, EquipmentRole = role, Cost = 80, MonthlyLimit = 2 });
                    }
                    continue;
                }
                items.Add(new SoulShopItem
                {
                    Id = "eq:" + slot.ToString().ToLowerInvariant() + ":3", Name = Equipment.Name(slot, 3), Kind = SoulItemKind.Equipment,
                    Slot = slot, Tier = 3, Cost = 80, MonthlyLimit = 2,
                });
            }
            return items;
        }

        public static SoulShopItem? Find(string id) => Items().FirstOrDefault(i => i.Id == id);

        public static void EnsureMonth(PlayerProfile p, long now)
        {
            string key = DailyClock.MonthKey(now);
            if (p.SoulShopMonth == key)
            {
                if (p.SoulShopBought.TryGetValue("eq:accessory:3", out int old))
                {
                    p.SoulShopBought.Remove("eq:accessory:3");
                    string crit = Equipment.AccessoryKey(true, 3);
                    string dodge = Equipment.AccessoryKey(false, 3);
                    p.SoulShopBought[crit] = (p.SoulShopBought.TryGetValue(crit, out int a) ? a : 0) + old / 2 + old % 2;
                    p.SoulShopBought[dodge] = (p.SoulShopBought.TryGetValue(dodge, out int b) ? b : 0) + old / 2;
                }
                return;
            }
            p.SoulShopMonth = key;
            p.SoulShopBought.Clear();
        }

        public static int Bought(PlayerProfile p, string itemId, long now)
        {
            EnsureMonth(p, now);
            return p.SoulShopBought.TryGetValue(itemId, out int n) ? n : 0;
        }

        public static SoulShopResult Buy(PlayerProfile p, string itemId, long now)
        {
            var item = Find(itemId);
            if (item == null) return SoulShopResult.UnknownItem;
            EnsureMonth(p, now);
            if (Bought(p, itemId, now) >= item.MonthlyLimit) return SoulShopResult.LimitReached;
            if (p.GetMaterial(HeroGrowth.Soul) < item.Cost) return SoulShopResult.NotEnoughSouls;

            switch (item.Kind)
            {
                case SoulItemKind.HeroShard:
                    if (!p.Heroes.TryGetValue(item.HeroId, out var hero)) return SoulShopResult.HeroNotOwned;
                    if (hero.Stars + HeroGrowth.Shards(p, item.HeroId) >= HeroGrowth.MaxStars) return SoulShopResult.HeroMaxed;
                    p.AddMaterial(HeroGrowth.ShardKey(item.HeroId), 1);
                    break;
                case SoulItemKind.HeroExp:
                    p.AddMaterial(HeroGrowth.HeroExp, item.Amount);
                    break;
                case SoulItemKind.Gold:
                    p.Gold += item.Amount;
                    break;
                case SoulItemKind.Equipment:
                    p.AddMaterial(item.Id, 1);
                    break;
            }
            p.AddMaterial(HeroGrowth.Soul, -item.Cost);
            p.SoulShopBought[itemId] = Bought(p, itemId, now) + 1;
            return SoulShopResult.Ok;
        }
    }
}
