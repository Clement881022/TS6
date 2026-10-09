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

    /// <summary>
    /// 裝備（GDD 09）：每名武將 3 個部位（武器、防具、飾品），品階 1–5，每高一階主屬性約 +10%（線性），屬性固定、無隨機詞條。
    /// 取得自素材副本，重複或淘汰的裝備分解為金幣。裝備名稱、數量、職業綁定等為待決事項，暫以「部位 + 品階」通用裝備實作。
    /// 庫存存放於 <see cref="PlayerProfile.Materials"/>（鍵 eq:部位:品階）；武將身上的裝備記為部位 → 品階。
    /// </summary>
    public static class Equipment
    {
        public const int MaxTier = 5;
        public const int TierPercent = 10;

        public static readonly EquipSlot[] Slots = { EquipSlot.Weapon, EquipSlot.Armor, EquipSlot.Accessory };

        public static string ItemKey(EquipSlot slot, int tier) => $"eq:{slot.ToString().ToLowerInvariant()}:{tier}";

        public static bool TryParseKey(string key, out EquipSlot slot, out int tier)
        {
            slot = EquipSlot.Weapon;
            tier = 0;
            var parts = key.Split(':');
            if (parts.Length != 3 || parts[0] != "eq") return false;
            if (!Enum.TryParse(parts[1], true, out slot)) return false;
            return int.TryParse(parts[2], out tier) && tier >= 1 && tier <= MaxTier;
        }

        public static string SlotName(EquipSlot slot) =>
            slot == EquipSlot.Weapon ? "武器" : slot == EquipSlot.Armor ? "防具" : "飾品";

        private static readonly string[] TierNames = { "", "一", "二", "三", "四", "五" };

        public static string Name(EquipSlot slot, int tier) => $"{TierNames[Math.Max(1, Math.Min(MaxTier, tier))]}階{SlotName(slot)}";

        /// <summary>分解可得的金幣（暫定：品階 × 300）。</summary>
        public static int DismantleGold(int tier) => 300 * tier;

        /// <summary>武器的主屬性：吃謀略的職業為謀略，其餘為攻擊。</summary>
        public static bool WeaponBoostsInt(Role role) =>
            role == Role.Mage || role == Role.Strategist || role == Role.Healer;

        /// <summary>
        /// 武將身上裝備的累計屬性修正：武器 = 攻擊 / 謀略 +10%×階；防具 = 生命與防禦 +10%×階；
        /// 飾品（暫定）= 戰士與遊俠爆擊 +3 點×階，其餘職業閃避 +2 點×階。
        /// </summary>
        public static StatMods Mods(Role role, IReadOnlyDictionary<string, int> equipped)
        {
            var m = StatMods.Identity;
            foreach (var kv in equipped)
            {
                if (!Enum.TryParse<EquipSlot>(kv.Key, true, out var slot)) continue;
                int tier = Math.Max(0, Math.Min(MaxTier, kv.Value));
                if (tier == 0) continue;
                double pct = tier * TierPercent / 100.0;
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
                        if (role == Role.Warrior || role == Role.Ranger) m.Crit += 3 * tier; else m.Dodge += 2 * tier;
                        break;
                }
            }
            return m;
        }

        public static int Count(PlayerProfile p, EquipSlot slot, int tier) => p.GetMaterial(ItemKey(slot, tier));

        /// <summary>穿上庫存中的裝備；該部位原本的裝備退回庫存。</summary>
        public static EquipResult Equip(PlayerProfile p, string heroId, EquipSlot slot, int tier)
        {
            if (!p.Heroes.TryGetValue(heroId, out var hero)) return EquipResult.UnknownHero;
            if (tier < 1 || tier > MaxTier) return EquipResult.InvalidTier;
            if (Count(p, slot, tier) < 1) return EquipResult.NotOwned;
            p.AddMaterial(ItemKey(slot, tier), -1);
            string key = slot.ToString();
            if (hero.Equipment.TryGetValue(key, out int old) && old > 0) p.AddMaterial(ItemKey(slot, old), 1);
            hero.Equipment[key] = tier;
            return EquipResult.Ok;
        }

        public static EquipResult Unequip(PlayerProfile p, string heroId, EquipSlot slot)
        {
            if (!p.Heroes.TryGetValue(heroId, out var hero)) return EquipResult.UnknownHero;
            string key = slot.ToString();
            if (!hero.Equipment.TryGetValue(key, out int old) || old <= 0) return EquipResult.NothingEquipped;
            hero.Equipment.Remove(key);
            p.AddMaterial(ItemKey(slot, old), 1);
            return EquipResult.Ok;
        }

        /// <summary>分解庫存中的裝備換金幣。</summary>
        public static EquipResult Dismantle(PlayerProfile p, EquipSlot slot, int tier, int count)
        {
            if (tier < 1 || tier > MaxTier || count < 1) return EquipResult.InvalidTier;
            if (Count(p, slot, tier) < count) return EquipResult.NotOwned;
            p.AddMaterial(ItemKey(slot, tier), -count);
            p.Gold += DismantleGold(tier) * count;
            return EquipResult.Ok;
        }

        /// <summary>
        /// 各階副本掉出「本階」裝備的機率（企劃 2026-10-09：機率掉落、越高階越難掉；數值以 tools/playsim 校準）。
        /// 沒掉到就沒有裝備（第 1 階必掉）。原本「沒掉到給低一階」會讓高階副本變成穩定的次一階來源，裝備仍然太快畢業。
        /// </summary>
        public static readonly double[] DropChance = { 1.0, 0.4, 0.2, 0.1, 0.05 };

        public static double DropChanceOf(int tier) => DropChance[Math.Max(1, Math.Min(MaxTier, tier)) - 1];

        /// <summary>素材副本的裝備掉落：每次最多 1 件、隨機部位，掉率見 <see cref="DropChance"/>。回傳「庫存鍵 → 數量」。</summary>
        public static Dictionary<string, int> RollDrops(int tier, int count, Rng rng)
        {
            var drops = new Dictionary<string, int>();
            for (int i = 0; i < count; i++)
            {
                if (rng.Next(10000) >= (int)System.Math.Round(DropChanceOf(tier) * 10000)) continue;
                string key = ItemKey(Slots[rng.Next(Slots.Length)], tier);
                drops[key] = drops.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            return drops;
        }
    }
}
