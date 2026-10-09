using System;
using System.Collections.Generic;

namespace SanGuo.Core.Meta
{
    public enum EquipSlot { Weapon, Armor, Accessory }

    public enum EquipResult
    {
        Ok,
        UnknownHero,
        InvalidTier,
        NotOwned,
        NothingEquipped,
    }

    public static class Equipment
    {
        public const int MaxTier = 5;

        public static readonly EquipSlot[] Slots = { EquipSlot.Weapon, EquipSlot.Armor, EquipSlot.Accessory };

        public static readonly int[] Percents = { 0, 5, 11, 22, 40, 65 };

        public static int PercentOf(int tier) => Percents[Math.Max(1, Math.Min(MaxTier, tier))];

        public static string ItemKey(EquipSlot slot, int tier) => $"eq:{slot.ToString().ToLowerInvariant()}:{tier}";

        public static string WeaponKey(Role role, int tier) => $"eq:weapon:{role.ToString().ToLowerInvariant()}:{tier}";

        public static string ShardKey(EquipSlot slot, int tier, Role? role = null) =>
            slot == EquipSlot.Weapon && role.HasValue
                ? $"eqs:weapon:{role.Value.ToString().ToLowerInvariant()}:{tier}"
                : $"eqs:{slot.ToString().ToLowerInvariant()}:{tier}";

        public static bool TryParseKey(string key, out EquipSlot slot, out int tier) => TryParseKey(key, out slot, out tier, out _);

        public static bool TryParseKey(string key, out EquipSlot slot, out int tier, out Role? role)
        {
            slot = EquipSlot.Weapon;
            tier = 0;
            role = null;
            var parts = key.Split(':');
            if (parts[0] != "eq" || (parts.Length != 3 && parts.Length != 4)) return false;
            if (!Enum.TryParse(parts[1], true, out slot)) return false;
            if (parts.Length == 4)
            {
                if (slot != EquipSlot.Weapon || !Enum.TryParse<Role>(parts[2], true, out var r)) return false;
                role = r;
            }
            return int.TryParse(parts[parts.Length - 1], out tier) && tier >= 1 && tier <= MaxTier;
        }

        public static bool TryParseShardKey(string key, out EquipSlot slot, out int tier) => TryParseShardKey(key, out slot, out tier, out _);

        public static bool TryParseShardKey(string key, out EquipSlot slot, out int tier, out Role? role)
        {
            slot = EquipSlot.Weapon;
            tier = 0;
            role = null;
            var parts = key.Split(':');
            if (parts[0] != "eqs" || (parts.Length != 3 && parts.Length != 4)) return false;
            if (!Enum.TryParse(parts[1], true, out slot)) return false;
            if (parts.Length == 4)
            {
                if (slot != EquipSlot.Weapon || !Enum.TryParse<Role>(parts[2], true, out var r)) return false;
                role = r;
            }
            return int.TryParse(parts[parts.Length - 1], out tier) && tier >= 1 && tier <= MaxTier;
        }

        public static string SlotName(EquipSlot slot) =>
            slot == EquipSlot.Weapon ? "武器" : slot == EquipSlot.Armor ? "防具" : "飾品";

        public static readonly string[] TierLabels = { "", "凡品", "良品", "上品", "極品", "神品" };

        public static string TierLabel(int tier) => TierLabels[Math.Max(1, Math.Min(MaxTier, tier))];

        public static string WeaponTypeName(Role role)
        {
            switch (role)
            {
                case Role.Tank: return "重盾";
                case Role.Warrior: return "戰刀";
                case Role.Ranger: return "長弓";
                case Role.Mage: return "法杖";
                case Role.Strategist: return "羽扇";
                default: return "藥杖";
            }
        }

        private static readonly string[][] WeaponNames =
        {
            new[] { "木盾", "鐵葉盾", "玄鐵塔盾", "虎紋巨盾", "鎮嶽玄武盾" },
            new[] { "環首刀", "百鍊鋼刀", "破陣戟", "斬馬長刀", "裂天戟" },
            new[] { "獵弓", "角弓", "長梢弓", "落雁弓", "射日神弓" },
            new[] { "桃木杖", "棗心杖", "青玉法杖", "九節雷杖", "天罡劫雷杖" },
            new[] { "竹骨扇", "鶴羽扇", "烏木羽扇", "八卦羽扇", "臥龍天機扇" },
            new[] { "藥鋤", "銅鈴杖", "青囊杖", "懸壺杖", "妙手回春杖" },
        };
        private static readonly string[] BlankNames = { "凡品武器自選匣", "良品武器自選匣", "上品武器自選匣", "極品武器自選匣", "神品武器自選匣" };
        private static readonly string[] ArmorNames = { "麻布戰袍", "硬皮甲", "鐵札甲", "明光鎧", "龍鱗寶甲" };
        private static readonly string[] AccessoryNames = { "麻繩護符", "銅虎符", "青玉環", "金絲玉珮", "麒麟玄玉" };

        public static string Name(EquipSlot slot, int tier, Role? role = null)
        {
            int i = Math.Max(1, Math.Min(MaxTier, tier)) - 1;
            switch (slot)
            {
                case EquipSlot.Weapon: return role.HasValue ? WeaponNames[(int)role.Value][i] : BlankNames[i];
                case EquipSlot.Armor: return ArmorNames[i];
                default: return AccessoryNames[i];
            }
        }

        public static string ShardName(EquipSlot slot, int tier, Role? role = null) =>
            TierLabel(tier) + (slot == EquipSlot.Weapon ? (role.HasValue ? WeaponTypeName(role.Value) : "武器") : SlotName(slot)) + "碎片";

        public static int DismantleGold(int tier) => 300 * tier;

        public static bool WeaponBoostsInt(Role role) =>
            role == Role.Mage || role == Role.Strategist || role == Role.Healer;

        public static StatMods Mods(Role role, IReadOnlyDictionary<string, int> equipped)
        {
            var m = StatMods.Identity;
            foreach (var kv in equipped)
            {
                if (!Enum.TryParse<EquipSlot>(kv.Key, true, out var slot)) continue;
                int tier = Math.Max(0, Math.Min(MaxTier, kv.Value));
                if (tier == 0) continue;
                int percent = PercentOf(tier);
                double pct = percent / 100.0;
                switch (slot)
                {
                    case EquipSlot.Weapon:
                        if (WeaponBoostsInt(role)) m.Int += pct; else m.Atk += pct;
                        break;
                    case EquipSlot.Armor:
                        m.Hp += pct;
                        m.Def += pct;
                        break;
                    default:
                        if (role == Role.Warrior || role == Role.Ranger) m.Crit += (int)Math.Round(percent * 0.3); else m.Dodge += (int)Math.Round(percent * 0.2);
                        break;
                }
            }
            return m;
        }

        public static int Count(PlayerProfile p, EquipSlot slot, int tier)
        {
            int n = p.GetMaterial(ItemKey(slot, tier));
            if (slot == EquipSlot.Weapon)
                foreach (Role r in Enum.GetValues(typeof(Role))) n += p.GetMaterial(WeaponKey(r, tier));
            return n;
        }

        public static int CountFor(PlayerProfile p, Role role, EquipSlot slot, int tier)
        {
            int n = p.GetMaterial(ItemKey(slot, tier));
            if (slot == EquipSlot.Weapon) n += p.GetMaterial(WeaponKey(role, tier));
            return n;
        }

        public static EquipResult Equip(PlayerProfile p, string heroId, EquipSlot slot, int tier)
        {
            if (!p.Heroes.TryGetValue(heroId, out var hero)) return EquipResult.UnknownHero;
            var def = HeroRoster.Find(heroId);
            if (def == null) return EquipResult.UnknownHero;
            if (tier < 1 || tier > MaxTier) return EquipResult.InvalidTier;
            string key = ItemKey(slot, tier);
            if (slot == EquipSlot.Weapon && p.GetMaterial(WeaponKey(def.Role, tier)) > 0) key = WeaponKey(def.Role, tier);
            if (p.GetMaterial(key) < 1) return EquipResult.NotOwned;
            p.AddMaterial(key, -1);
            string slotKey = slot.ToString();
            if (hero.Equipment.TryGetValue(slotKey, out int old) && old > 0) p.AddMaterial(StockKeyOf(def.Role, slot, old), 1);
            hero.Equipment[slotKey] = tier;
            return EquipResult.Ok;
        }

        public static EquipResult Unequip(PlayerProfile p, string heroId, EquipSlot slot)
        {
            if (!p.Heroes.TryGetValue(heroId, out var hero)) return EquipResult.UnknownHero;
            var def = HeroRoster.Find(heroId);
            if (def == null) return EquipResult.UnknownHero;
            string slotKey = slot.ToString();
            if (!hero.Equipment.TryGetValue(slotKey, out int old) || old <= 0) return EquipResult.NothingEquipped;
            hero.Equipment.Remove(slotKey);
            p.AddMaterial(StockKeyOf(def.Role, slot, old), 1);
            return EquipResult.Ok;
        }

        private static string StockKeyOf(Role role, EquipSlot slot, int tier) =>
            slot == EquipSlot.Weapon ? WeaponKey(role, tier) : ItemKey(slot, tier);

        public static EquipResult Dismantle(PlayerProfile p, EquipSlot slot, int tier, int count)
        {
            if (tier < 1 || tier > MaxTier || count < 1) return EquipResult.InvalidTier;
            if (Count(p, slot, tier) < count) return EquipResult.NotOwned;
            int left = count;
            var keys = new List<string> { ItemKey(slot, tier) };
            if (slot == EquipSlot.Weapon) foreach (Role r in Enum.GetValues(typeof(Role))) keys.Add(WeaponKey(r, tier));
            foreach (var key in keys)
            {
                int take = Math.Min(left, p.GetMaterial(key));
                if (take <= 0) continue;
                p.AddMaterial(key, -take);
                left -= take;
                if (left == 0) break;
            }
            p.Gold += DismantleGold(tier) * count;
            return EquipResult.Ok;
        }

        public static readonly double[] DropChance = { 1.0, 0.4, 0.2, 0, 0 };

        public static readonly int[] ShardCost = { 0, 0, 0, 9, 30 };

        public static bool UsesShards(int tier) => ShardCostOf(tier) > 0;

        public static int ShardCostOf(int tier) => ShardCost[Math.Max(1, Math.Min(MaxTier, tier)) - 1];

        public static double DropChanceOf(int tier) => DropChance[Math.Max(1, Math.Min(MaxTier, tier)) - 1];

        public static Dictionary<string, int> RollDrops(int tier, int count, Rng rng)
        {
            var drops = new Dictionary<string, int>();
            bool shards = UsesShards(tier);
            var roles = (Role[])Enum.GetValues(typeof(Role));
            for (int i = 0; i < count; i++)
            {
                if (!shards && rng.Next(10000) >= (int)Math.Round(DropChanceOf(tier) * 10000)) continue;
                var slot = Slots[rng.Next(Slots.Length)];
                Role? role = slot == EquipSlot.Weapon ? roles[rng.Next(roles.Length)] : (Role?)null;
                string key = shards ? ShardKey(slot, tier, role) : role.HasValue ? WeaponKey(role.Value, tier) : ItemKey(slot, tier);
                drops[key] = drops.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            return drops;
        }

        public static int AutoForge(PlayerProfile p)
        {
            int made = 0;
            var roles = (Role[])Enum.GetValues(typeof(Role));
            for (int tier = 1; tier <= MaxTier; tier++)
            {
                int cost = ShardCostOf(tier);
                if (cost <= 0) continue;
                foreach (var slot in Slots)
                {
                    if (slot == EquipSlot.Weapon)
                    {
                        foreach (var role in roles) made += Forge(p, ShardKey(slot, tier, role), WeaponKey(role, tier), cost);
                    }
                    else made += Forge(p, ShardKey(slot, tier), ItemKey(slot, tier), cost);
                }
            }
            return made;
        }

        private static int Forge(PlayerProfile p, string shardKey, string itemKey, int cost)
        {
            int n = p.GetMaterial(shardKey) / cost;
            if (n <= 0) return 0;
            p.AddMaterial(shardKey, -n * cost);
            p.AddMaterial(itemKey, n);
            return n;
        }
    }
}
