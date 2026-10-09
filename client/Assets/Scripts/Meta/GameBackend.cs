#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SanGuo.Core.Data;
using SanGuo.Core;
using SanGuo.Core.Meta;

namespace SanGuo.Client
{
    public sealed class ProfileView
    {
        public int Level = 1;
        public int Exp;
        public int Yuanbao;
        public int Gold;
        public int Stamina;
        public int StaminaCap = 62;
        public HashSet<string> ClearedStages = new HashSet<string>();
        public Dictionary<string, int> StageStars = new Dictionary<string, int>();
        public Dictionary<string, HeroState> Heroes = new Dictionary<string, HeroState>();
        public Dictionary<string, int> Materials = new Dictionary<string, int>();
        public Dictionary<string, PoolState> Pools = new Dictionary<string, PoolState>();
        public PlayerProfile Raw = new PlayerProfile();
        public long Now;

        public int ExpToNext => PlayerLevelCurve.ExpToNext(Level);

        public int StarsOf(string stageId) => StageStars.TryGetValue(stageId, out int n) ? n : 0;

        public int Material(string key) => Materials.TryGetValue(key, out int n) ? n : 0;

        public static ProfileView From(PlayerProfile p, long now) => new ProfileView
        {
            Level = p.Level, Exp = p.Exp, Yuanbao = p.Yuanbao, Gold = p.Gold,
            Stamina = p.Stamina.Get(now), StaminaCap = p.Stamina.Cap,
            ClearedStages = new HashSet<string>(p.ClearedStages),
            StageStars = new Dictionary<string, int>(p.StageStars),
            Heroes = new Dictionary<string, HeroState>(p.Heroes),
            Materials = new Dictionary<string, int>(p.Materials),
            Pools = new Dictionary<string, PoolState>(p.PoolStates),
            Raw = ProfileSerializer.FromJson(ProfileSerializer.ToJson(p)),
            Now = now == 0 ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : now,
        };
    }

    public class BackendResult
    {
        public bool Ok;
        public string Code = "ok";
    }

    public sealed class StartStageResult : BackendResult
    {
        public ulong Seed;
    }

    public sealed class FinishStageResult : BackendResult
    {
        public bool Won;
        public int Stars;
        public bool FirstClear;
        public int Exp;
        public int Gold;
        public int Yuanbao;
        public int LevelsGained;
        public string HeroGained = "";
        public List<string> DuplicatesGained = new List<string>();
        public Dictionary<string, int> Materials = new Dictionary<string, int>();
        public long Damage;
        public long BestDamage;
        public bool NewBest;
        public int Rank;
        public int Total;
    }

    public sealed class WorldBossView
    {
        public string Season = "";
        public bool Unlocked;
        public int AttemptsLeft;
        public long Best;
        public int Rank;
        public int Total;
        public List<(string Name, long Best)> Top = new List<(string, long)>();
        public string LastSeason = "";
        public int LastRank;
        public int LastTotal;
        public int LastReward;
        public string Title = "";
    }

    public sealed class SweepOutcome : BackendResult
    {
        public int Exp;
        public int Gold;
        public int LevelsGained;
    }

    public sealed class PullOutcomeResult : BackendResult
    {
        public List<PullResult> Results = new List<PullResult>();
    }

    public interface IGameBackend
    {
        string Name { get; }
        Task<ProfileView?> GetProfile();
        Task<WorldBossView?> GetWorldBoss();
        Task<StartStageResult> StartStage(string stageId, IReadOnlyList<FormationEntry>? formation = null);
        Task<FinishStageResult> FinishStage(string stageId, IReadOnlyList<ReplayAction> actions);
        Task<FinishStageResult> DebugWin(string stageId);
        Task<SweepOutcome> Sweep(string stageId, int count);
        Task<BackendResult> SweepDungeon(string dungeonId, int count);
        Task<BackendResult> ClaimQuest(string questId);
        Task<BackendResult> ClaimMilestone(int points);
        Task<BackendResult> BuyWithTestPayment(string productId);
        Task<BackendResult> ClaimMonthCard(string cardId);
        Task<BackendResult> ClaimPass(int level, bool paid);
        Task<BackendResult> ClaimPassAll();
        Task<PullOutcomeResult> Pull(string poolId, int count);
        Task<BackendResult> LevelUp(string heroId);
        Task<BackendResult> Breakthrough(string heroId);
        Task<BackendResult> Equip(string heroId, string slot, int tier);
        Task<BackendResult> Unequip(string heroId, string slot);
        Task<BackendResult> Dismantle(string slot, int tier, int count);
        Task<BackendResult> BuySoulItem(string itemId);
    }
}
