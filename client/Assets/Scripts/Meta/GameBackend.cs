#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SanGuo.Core.Data;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;

namespace SanGuo.Client
{
    /// <summary>客戶端顯示用的玩家資料快照（本機與伺服器兩種後端都轉成這個）。</summary>
    public sealed class ProfileView
    {
        public int Level = 1;
        public int Exp;
        public int Yuanbao;
        public int Gold;
        public int Stamina;
        public int StaminaCap = 120;
        public HashSet<string> ClearedStages = new HashSet<string>();
        public Dictionary<string, int> StageStars = new Dictionary<string, int>();
        public Dictionary<string, HeroState> Heroes = new Dictionary<string, HeroState>();
        public Dictionary<string, int> Materials = new Dictionary<string, int>();
        public Dictionary<string, PoolState> Pools = new Dictionary<string, PoolState>();
        /// <summary>完整存檔的複本（任務進度、每日次數等唯讀顯示用；修改它不影響真正的存檔）。</summary>
        public PlayerProfile Raw = new PlayerProfile();
        /// <summary>顯示用的目前時間（Unix 秒，客戶端時鐘；實際判定一律以後端為準）。</summary>
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

    /// <summary>機器可讀的失敗原因（Code）；Ok 時為 "ok"。連線失敗為 "network"。</summary>
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
        /// <summary>資源副本掉落的素材。</summary>
        public Dictionary<string, int> Materials = new Dictionary<string, int>();
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

    /// <summary>
    /// 客戶端與「遊戲規則所在處」之間的介面：本機版直接跑 SanGuo.Core（離線 / 單機），
    /// 伺服器版呼叫 SanGuo.Server。兩者的流程相同：開始關卡取得種子 → 打 → 交出操作紀錄結算。
    /// </summary>
    public interface IGameBackend
    {
        string Name { get; }
        Task<ProfileView?> GetProfile();
        Task<StartStageResult> StartStage(string stageId);
        Task<FinishStageResult> FinishStage(string stageId, IReadOnlyList<ReplayAction> actions);
        Task<SweepOutcome> Sweep(string stageId, int count);
        Task<BackendResult> SweepDungeon(string dungeonId, int count);
        Task<BackendResult> ClaimQuest(string questId);
        Task<BackendResult> ClaimMilestone(int points);
        /// <summary>測試付款購買：建立訂單並模擬付款成功（正式版改走支付渠道，付款完成由伺服器發貨）。</summary>
        Task<BackendResult> BuyWithTestPayment(string productId);
        Task<BackendResult> ClaimMonthCard(string cardId);
        Task<BackendResult> ClaimGrowthFund(int level);
        Task<PullOutcomeResult> Pull(string poolId, int count);
        Task<BackendResult> LevelUp(string heroId);
        Task<BackendResult> Enhance(string heroId, string cardId);
        Task<BackendResult> Breakthrough(string heroId);
    }
}
