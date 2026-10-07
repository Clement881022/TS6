#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using UnityEngine;

namespace SanGuo.Client
{
    /// <summary>
    /// 單機後端：存檔放 PlayerPrefs，規則與伺服器共用 SanGuo.Core，
    /// 結算同樣以「重播操作紀錄」自算勝負與星數（跟伺服器走一樣的路徑）。
    /// </summary>
    public sealed class LocalBackend : IGameBackend
    {
        private const string SaveKey = "sanguo_local_profile";
        private const int StartingYuanbao = 2000;
        private const int StartingGold = 5000;

        private PlayerProfile _profile;
        private readonly bool _persist;

        public string Name => "單機";

        /// <param name="persist">false = 不讀不寫 PlayerPrefs，每次都是全新存檔（自動截圖 / 測試用）。</param>
        public LocalBackend(bool persist = true)
        {
            _persist = persist;
            _profile = (persist ? Load() : null) ?? NewProfile();
        }

        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private static PlayerProfile NewProfile()
        {
            var p = PlayerProfile.CreateNew(Now);
            p.Yuanbao = StartingYuanbao;
            p.Gold = StartingGold;
            // 開發用起始素材（資源副本的客戶端介面還沒做，否則升級與強化無從測試）。
            p.AddMaterial(HeroGrowth.ExpBook, 30);
            p.AddMaterial(HeroGrowth.CardMaterial, 40);
            return p;
        }

        private static PlayerProfile? Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(SaveKey, "");
                return json.Length == 0 ? null : ProfileSerializer.FromJson(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("單機存檔讀取失敗，改用新存檔：" + e.Message);
                return null;
            }
        }

        private void Save()
        {
            if (!_persist) return;
            PlayerPrefs.SetString(SaveKey, ProfileSerializer.ToJson(_profile));
            PlayerPrefs.Save();
        }

        public Task<ProfileView?> GetProfile()
        {
            long now = Now;
            _profile.OnLogin(now);
            Save();
            return Task.FromResult<ProfileView?>(ProfileView.From(_profile, now));
        }

        public Task<StartStageResult> StartStage(string stageId, IReadOnlyList<FormationEntry>? formation = null)
        {
            var bytes = new byte[8];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var r = StageFlow.Start(_profile, stageId, Now, BitConverter.ToUInt64(bytes, 0), formation);
            if (r.Ok) Save();
            return Task.FromResult(new StartStageResult { Ok = r.Ok, Code = r.Code, Seed = r.Seed });
        }

        public Task<FinishStageResult> FinishStage(string stageId, IReadOnlyList<ReplayAction> actions)
        {
            var r = StageFlow.Finish(_profile, stageId, actions, Now);
            if (r.Ok || r.Persist) Save();
            return Task.FromResult(new FinishStageResult
            {
                Ok = r.Ok, Code = r.Code, Won = r.Won, Stars = r.Stars, FirstClear = r.FirstClear,
                Exp = r.Exp, Gold = r.Gold, Yuanbao = r.Yuanbao, LevelsGained = r.LevelsGained, HeroGained = r.HeroGained, Materials = r.Materials,
            });
        }

        public Task<FinishStageResult> DebugWin(string stageId)
        {
            if (!Debug.isDebugBuild) return Task.FromResult(new FinishStageResult { Code = "debug_only" });
            if (_profile.PendingStageId != stageId) return Task.FromResult(new FinishStageResult { Code = "no_pending_stage" });
            _profile.PendingStageId = "";
            _profile.PendingSeed = 0;
            _profile.PendingFormation = new List<FormationEntry>();
            var dungeon = DemoMeta.FindDungeon(stageId);
            FinishStageResult result;
            if (dungeon != null)
            {
                ResourceDungeons.ClaimWin(_profile, dungeon, Now);
                var r = dungeon.Reward;
                result = new FinishStageResult
                {
                    Ok = true, Won = true, Gold = r.Gold, Yuanbao = r.Yuanbao, Materials = new Dictionary<string, int>(r.Materials),
                };
            }
            else
            {
                var stage = DemoMeta.FindStage(stageId);
                if (stage == null) return Task.FromResult(new FinishStageResult { Code = "unknown_stage" });
                var clear = _profile.ClaimClear(stage, Now, 3);
                result = new FinishStageResult
                {
                    Ok = true, Won = true, Stars = 3, FirstClear = clear.FirstClear, Exp = clear.ExpGained, Gold = clear.GoldGained,
                    Yuanbao = clear.YuanbaoGained, LevelsGained = clear.LevelsGained, HeroGained = clear.HeroGained,
                };
            }
            Save();
            return Task.FromResult(result);
        }

        public Task<BackendResult> SweepDungeon(string dungeonId, int count)
        {
            var d = DemoMeta.FindDungeon(dungeonId);
            if (d == null) return Task.FromResult(new BackendResult { Code = "unknown_dungeon" });
            var r = ResourceDungeons.TrySweep(_profile, d, count, Now);
            if (r != DungeonEntryResult.Ok) return Task.FromResult(new BackendResult { Code = r.ToString() });
            Save();
            return Task.FromResult(new BackendResult { Ok = true });
        }

        private Task<BackendResult> Claimed(QuestClaimResult r) => Claimed(r == QuestClaimResult.Ok, r.ToString());

        private Task<BackendResult> Claimed(ShopResult r) => Claimed(r == ShopResult.Ok, r.ToString());

        private Task<BackendResult> Claimed(bool ok, string code)
        {
            if (!ok) return Task.FromResult(new BackendResult { Code = code });
            Save();
            return Task.FromResult(new BackendResult { Ok = true });
        }

        public Task<BackendResult> ClaimQuest(string questId) => Claimed(Quests.Claim(_profile, questId, Now));

        public Task<BackendResult> ClaimMilestone(int points) => Claimed(Quests.ClaimMilestone(_profile, points, Now));

        public Task<BackendResult> BuyWithTestPayment(string productId)
        {
            string orderId = Guid.NewGuid().ToString("N");
            var r = Shop.CreateOrder(_profile, productId, orderId);
            if (r == ShopResult.Ok) r = Shop.Fulfill(_profile, orderId, Now);
            return Claimed(r);
        }

        public Task<BackendResult> ClaimMonthCard(string cardId) => Claimed(Shop.ClaimMonthCardDaily(_profile, cardId, Now));

        public Task<BackendResult> ClaimGrowthFund(int level) => Claimed(Shop.ClaimGrowthFund(_profile, level));

        public Task<PullOutcomeResult> Pull(string poolId, int count)
        {
            var pool = DemoMeta.Pools().Find(x => x.Id == poolId);
            if (pool == null) return Task.FromResult(new PullOutcomeResult { Code = "unknown_pool" });
            var bytes = new byte[8];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var outcome = Gacha.Pull(_profile, pool, count, new Rng(BitConverter.ToUInt64(bytes, 0)));
            if (outcome.Status != PullStatus.Ok) return Task.FromResult(new PullOutcomeResult { Code = outcome.Status.ToString() });
            Quests.Report(_profile, Quests.Events.GachaPull, count, Now);
            Save();
            return Task.FromResult(new PullOutcomeResult { Ok = true, Results = outcome.Results });
        }

        private Task<BackendResult> Growth(GrowthResult r, string questEvent)
        {
            if (r != GrowthResult.Ok) return Task.FromResult(new BackendResult { Code = r.ToString() });
            Quests.Report(_profile, questEvent, 1, Now);
            Save();
            return Task.FromResult(new BackendResult { Ok = true });
        }

        public Task<BackendResult> LevelUp(string heroId) =>
            Growth(HeroGrowth.LevelUp(_profile, heroId), Quests.Events.HeroLevelUp);

        public Task<BackendResult> Enhance(string heroId, string cardId)
        {
            var def = DemoContent.Roster().Find(h => h.Id == heroId);
            if (def == null) return Task.FromResult(new BackendResult { Code = GrowthResult.UnknownHero.ToString() });
            return Growth(HeroGrowth.EnhanceCard(_profile, heroId, cardId, def.Deck.ConvertAll(c => c.Id)), Quests.Events.CardEnhance);
        }

        public Task<BackendResult> Breakthrough(string heroId) =>
            Growth(HeroGrowth.Breakthrough(_profile, heroId), Quests.Events.Breakthrough);

        public Task<SweepOutcome> Sweep(string stageId, int count)
        {
            var stage = DemoMeta.FindStage(stageId);
            if (stage == null) return Task.FromResult(new SweepOutcome { Code = "unknown_stage" });
            var r = _profile.TrySweep(stage, count, Now, out var result);
            if (r != SweepResult.Ok) return Task.FromResult(new SweepOutcome { Code = r.ToString() });
            Save();
            return Task.FromResult(new SweepOutcome
            {
                Ok = true, Exp = result!.ExpGained, Gold = result.GoldGained, LevelsGained = result.LevelsGained,
            });
        }
    }
}
