using System.Linq;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using Xunit;

namespace SanGuo.Core.Tests
{
    public class AccessorySplitTests
    {
        [Fact]
        public void TwoTypes_CannotBeWornByTheWrongClass()
        {
            var p = PlayerProfile.CreateNew(1);
            p.Heroes["guanyu"] = new HeroState { HeroId = "guanyu" };
            p.Heroes["xiahoudun"] = new HeroState { HeroId = "xiahoudun" };
            p.AddMaterial(Equipment.AccessoryKey(true, 3), 1);
            Assert.Equal(EquipResult.NotOwned, Equipment.Equip(p, "xiahoudun", EquipSlot.Accessory, 3));
            Assert.Equal(EquipResult.Ok, Equipment.Equip(p, "guanyu", EquipSlot.Accessory, 3));
            p.AddMaterial(Equipment.AccessoryKey(false, 3), 1);
            Assert.Equal(EquipResult.NotOwned, Equipment.Equip(p, "guanyu", EquipSlot.Accessory, 3));
            Assert.Equal(EquipResult.Ok, Equipment.Equip(p, "xiahoudun", EquipSlot.Accessory, 3));
            Assert.Equal(EquipResult.Ok, Equipment.Unequip(p, "guanyu", EquipSlot.Accessory));
            Assert.Equal(EquipResult.Ok, Equipment.Unequip(p, "xiahoudun", EquipSlot.Accessory));
            Assert.Equal(1, p.GetMaterial(Equipment.AccessoryKey(true, 3)));
            Assert.Equal(1, p.GetMaterial(Equipment.AccessoryKey(false, 3)));
        }

        [Fact]
        public void LegacyInventory_IsConvertedOnceWithoutChangingTotal()
        {
            var p = PlayerProfile.CreateNew(1);
            p.AddMaterial("eq:accessory:3", 5);
            p.AddMaterial("eqs:accessory:4", 9);
            p.Heroes["xiahoudun"] = new HeroState { HeroId = "xiahoudun" };
            p.Heroes["xiahoudun"].Equipment["Accessory"] = 2;
            var back = ProfileSerializer.FromJson(ProfileSerializer.ToJson(p));
            Assert.Equal(3, back.GetMaterial(Equipment.AccessoryKey(true, 3)));
            Assert.Equal(2, back.GetMaterial(Equipment.AccessoryKey(false, 3)));
            Assert.Equal(5, back.GetMaterial(Equipment.AccessoryShardKey(true, 4)));
            Assert.Equal(4, back.GetMaterial(Equipment.AccessoryShardKey(false, 4)));
            Equipment.NormalizeLegacyAccessories(back);
            Assert.Equal(5, Equipment.Count(back, EquipSlot.Accessory, 3));
            Assert.DoesNotContain("eq:accessory:3", back.Materials.Keys);
            Equipment.Unequip(back, "xiahoudun", EquipSlot.Accessory);
            Assert.Equal(1, back.GetMaterial(Equipment.AccessoryKey(false, 2)));
        }

        [Fact]
        public void TypedDismantle_DoesNotConsumeTheOtherType()
        {
            var p = PlayerProfile.CreateNew(1);
            p.AddMaterial(Equipment.AccessoryKey(true, 3), 2);
            p.AddMaterial(Equipment.AccessoryKey(false, 3), 1);
            Assert.Equal(EquipResult.Ok, Equipment.Dismantle(p, EquipSlot.Accessory, 3, 1, Role.Tank));
            Assert.Equal(2, p.GetMaterial(Equipment.AccessoryKey(true, 3)));
            Assert.Equal(0, p.GetMaterial(Equipment.AccessoryKey(false, 3)));
            Assert.Equal(EquipResult.NotOwned, Equipment.Dismantle(p, EquipSlot.Accessory, 3, 1, Role.Tank));
        }

        [Fact]
        public void LegacyMonthlyPurchases_AreConvertedWithoutResettingCounts()
        {
            var p = PlayerProfile.CreateNew(1);
            p.SoulShopMonth = DailyClock.MonthKey(1);
            p.SoulShopBought["eq:accessory:3"] = 2;
            Assert.Equal(1, SoulShop.Bought(p, Equipment.AccessoryKey(true, 3), 1));
            Assert.Equal(1, SoulShop.Bought(p, Equipment.AccessoryKey(false, 3), 1));
            Assert.DoesNotContain("eq:accessory:3", p.SoulShopBought.Keys);
            Assert.Equal(1, SoulShop.Bought(p, Equipment.AccessoryKey(true, 3), 1));
        }

        [Fact]
        public void ShopPassDropsAndForge_UseSeparateKeys()
        {
            var products = SoulShop.Items().Where(i => i.Slot == EquipSlot.Accessory && i.Kind == SoulItemKind.Equipment).ToArray();
            Assert.Equal(2, products.Length);
            Assert.NotEqual(products[0].Name, products[1].Name);
            Assert.Contains(Equipment.AccessoryKey(true, 3), BattlePass.PaidReward(15).Materials.Keys);
            Assert.Contains(Equipment.AccessoryKey(false, 3), BattlePass.PaidReward(30).Materials.Keys);
            var drops = Equipment.RollDrops(5, 1000, new Rng(44));
            Assert.Contains(Equipment.AccessoryShardKey(true, 5), drops.Keys);
            Assert.Contains(Equipment.AccessoryShardKey(false, 5), drops.Keys);
            var p = PlayerProfile.CreateNew(1);
            p.AddMaterial(Equipment.AccessoryShardKey(false, 4), Equipment.ShardCostOf(4));
            Assert.Equal(1, Equipment.AutoForge(p));
            Assert.Equal(1, p.GetMaterial(Equipment.AccessoryKey(false, 4)));
            Assert.Equal(0, p.GetMaterial(Equipment.AccessoryKey(true, 4)));
        }
    }
}
