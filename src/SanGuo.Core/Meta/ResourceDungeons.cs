using System.Collections.Generic;

namespace SanGuo.Core.Meta
{
    public enum DungeonEntryResult
    {
        Ok,
        Locked,
        NotEnoughStamina,
        InvalidCount,
        NotCleared,
    }

    public sealed class ResourceDungeonDef
    {
        public string Id = "";
        public string Name = "";
        public int Tier = 1;
        public int StaminaCost = 20;
        public string UnlockStageId = "";
        public Reward Reward = new Reward();
        public int EquipmentDrops = 1;
    }

    public static class ResourceDungeons
    {
        public const int MaxSweepCount = 10;

        public static bool IsUnlocked(PlayerProfile p, ResourceDungeonDef d) =>
            d.UnlockStageId == "" || p.ClearedStages.Contains(d.UnlockStageId);

        public static DungeonEntryResult TryEnter(PlayerProfile p, ResourceDungeonDef d, long now) =>
            Consume(p, d, 1, now);

        public static Reward ClaimWin(PlayerProfile p, ResourceDungeonDef d, long now, ulong seed)
        {
            var reward = WithDrops(d, 1, new Rng(seed));
            p.Grant(reward, now);
            p.AddExp(d.StaminaCost, now);
            p.ClearedStages.Add(d.Id);
            Quests.Report(p, Quests.Events.ResourceRun, 1, now);
            return reward;
        }

        public static DungeonEntryResult TrySweep(PlayerProfile p, ResourceDungeonDef d, int count, long now, out Reward? reward)
        {
            reward = null;
            if (count < 1 || count > MaxSweepCount) return DungeonEntryResult.InvalidCount;
            if (!p.ClearedStages.Contains(d.Id)) return DungeonEntryResult.NotCleared;
            var r = Consume(p, d, count, now);
            if (r != DungeonEntryResult.Ok) return r;
            reward = WithDrops(d, count, new Rng((ulong)now * 2654435761UL + (ulong)count));
            p.Grant(reward, now);
            p.AddExp(d.StaminaCost * count, now);
            Quests.Report(p, Quests.Events.ResourceRun, count, now);
            Quests.Report(p, Quests.Events.Sweep, count, now);
            return DungeonEntryResult.Ok;
        }

        private static Reward WithDrops(ResourceDungeonDef d, int count, Rng rng)
        {
            var reward = d.Reward.Times(count);
            foreach (var drop in Equipment.RollDrops(d.Tier, d.EquipmentDrops * count, rng))
                reward.With(drop.Key, drop.Value);
            return reward;
        }

        private static DungeonEntryResult Consume(PlayerProfile p, ResourceDungeonDef d, int count, long now)
        {
            if (!IsUnlocked(p, d)) return DungeonEntryResult.Locked;
            if (!p.Stamina.TrySpend(d.StaminaCost * count, now)) return DungeonEntryResult.NotEnoughStamina;
            BattlePass.AddPoints(p, d.StaminaCost * count, now);
            return DungeonEntryResult.Ok;
        }
    }

    public static class DemoResourceDungeons
    {
        private static readonly string[] Names = { "糧倉護衛", "校場操練", "兵器鋪", "軍械庫", "中軍帳" };
        private static readonly int[] Stamina = { 20, 25, 30, 35, 40 };
        private static readonly string[] Unlock = { "0-4", "res_1", "res_2", "res_3", "res_4" };

        public static string IdOf(int tier) => "res_" + tier;

        public static List<ResourceDungeonDef> Create()
        {
            var list = new List<ResourceDungeonDef>();
            for (int tier = 1; tier <= 5; tier++)
            {
                var reward = new Reward(yuanbao: 5 * tier, gold: 2000 * tier).With(HeroGrowth.HeroExp, 800 * tier);
                list.Add(new ResourceDungeonDef
                {
                    Id = IdOf(tier), Name = $"第{"零一二三四五"[tier]}階　{Names[tier - 1]}", Tier = tier,
                    StaminaCost = Stamina[tier - 1], UnlockStageId = Unlock[tier - 1], Reward = reward,
                });
            }
            return list;
        }
    }
}
