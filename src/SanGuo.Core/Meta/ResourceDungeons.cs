using System.Collections.Generic;

namespace SanGuo.Core.Meta
{
    public enum DungeonEntryResult
    {
        Ok,
        /// <summary>尚未通關解鎖該階的主線關卡。</summary>
        Locked,
        NotEnoughStamina,
        InvalidCount,
        NotCleared,
    }

    /// <summary>
    /// 素材副本（GDD 05 §7）：分五階，與裝備品階 1–5 對應，體力 20／25／30／35／40；
    /// 產出金幣、武將經驗、裝備與少量元寶。各階隨章節解鎖。沒有每日次數限制。
    /// </summary>
    public sealed class ResourceDungeonDef
    {
        public string Id = "";
        public string Name = "";
        /// <summary>副本階數 1–5，同時決定掉落的裝備品階。</summary>
        public int Tier = 1;
        public int StaminaCost = 20;
        /// <summary>通關這個主線關卡後解鎖（空字串 = 一開始就開放）。</summary>
        public string UnlockStageId = "";
        /// <summary>每次通關固定獲得的獎勵（裝備掉落另計）。</summary>
        public Reward Reward = new Reward();
        /// <summary>每次掉落的裝備數量。</summary>
        public int EquipmentDrops = 1;
    }

    public static class ResourceDungeons
    {
        public const int MaxSweepCount = 10;

        public static bool IsUnlocked(PlayerProfile p, ResourceDungeonDef d) =>
            d.UnlockStageId == "" || p.ClearedStages.Contains(d.UnlockStageId);

        /// <summary>開打前檢查：已解鎖、體力夠；成功才扣體力。</summary>
        public static DungeonEntryResult TryEnter(PlayerProfile p, ResourceDungeonDef d, long now) =>
            Consume(p, d, 1, now);

        /// <summary>戰鬥勝利：發固定獎勵與裝備掉落（以種子決定部位），並記為已通關（之後可掃蕩）。回傳實際獲得的獎勵。</summary>
        public static Reward ClaimWin(PlayerProfile p, ResourceDungeonDef d, long now, ulong seed)
        {
            var reward = WithDrops(d, 1, new Rng(seed));
            p.Grant(reward, now);
            p.ClearedStages.Add(d.Id);
            Quests.Report(p, Quests.Events.ResourceRun, 1, now);
            return reward;
        }

        /// <summary>掃蕩：通關過的副本直接領獎勵，消耗與該關相同的體力。回傳實際獲得的獎勵。</summary>
        public static DungeonEntryResult TrySweep(PlayerProfile p, ResourceDungeonDef d, int count, long now, out Reward? reward)
        {
            reward = null;
            if (count < 1 || count > MaxSweepCount) return DungeonEntryResult.InvalidCount;
            if (!p.ClearedStages.Contains(d.Id)) return DungeonEntryResult.NotCleared;
            var r = Consume(p, d, count, now);
            if (r != DungeonEntryResult.Ok) return r;
            reward = WithDrops(d, count, new Rng((ulong)now * 2654435761UL + (ulong)count));
            p.Grant(reward, now);
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
            return DungeonEntryResult.Ok;
        }
    }

    /// <summary>素材副本表（五階；獎勵數值與解鎖關卡為暫定值，後 4 階的解鎖關卡待章節內容完成）。</summary>
    public static class DemoResourceDungeons
    {
        private static readonly string[] Names = { "糧倉護衛", "校場操練", "兵器鋪", "軍械庫", "中軍帳" };
        private static readonly int[] Stamina = { 20, 25, 30, 35, 40 };
        /// <summary>解鎖各階的主線關卡（第 1 階於第零章中段，其後於第 1–4 章通關後，讓第 N 章期間能刷到第 N 階裝備）。</summary>
        private static readonly string[] Unlock = { "0-4", "1-10", "2-10", "3-10", "4-10" };

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
