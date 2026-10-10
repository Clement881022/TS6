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
    public sealed class LocalBackend : IGameBackend
    {
        private const string SaveKey = "sanguo_local_profile";
        private const int StartingYuanbao = 2000;
        private const int StartingGold = 5000;

        private PlayerProfile _profile;
        private readonly bool _persist;

        public string Name => "單機";

        public LocalBackend(bool persist = true)
        {
            _persist = persist;
            _profile = (persist ? Load() : null) ?? NewProfile();
            string? clearTo = GameSession.CommandLineValue("-sanguoClearTo");
            if (clearTo != null && Campaign.TryParse(clearTo, out int toChapter, out int toLevel)) DebugClearTo(toChapter, toLevel);
        }

        private void DebugClearTo(int chapter, int level)
        {
            for (int c = Campaign.FirstChapter; c <= Campaign.LastChapter; c++)
                for (int l = 1; l <= Campaign.LevelsPerChapter; l++)
                {
                    if (c > chapter || (c == chapter && l >= level)) continue;
                    _profile.ClearedStages.Add(Campaign.StageId(c, l));
                    _profile.StageStars[Campaign.StageId(c, l)] = 3;
                    string hero = DemoMeta.Stage(c, l).FirstClearHero;
                    if (hero != "" && !_profile.Heroes.ContainsKey(hero)) _profile.Grant(new Reward().WithHero(hero), Now);
                    foreach (var dup in DemoMeta.Stage(c, l).FirstClearDuplicates) _profile.Grant(new Reward().WithHero(dup), Now);
                }
            int heroLevel = Campaign.ChapterEndLevel[Math.Max(0, chapter - 1)];
            foreach (var hero in _profile.Heroes.Values) hero.Level = Math.Max(hero.Level, heroLevel);
        }

        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private sealed class SoloBoard : IWorldBossBoard
        {
            public void Submit(string season, string accountId, long best) { }
            public (int Rank, int Total) RankOf(string season, long score) => (1, 1);
            public List<(string AccountId, long Best)> Top(string season, int count) => new List<(string, long)>();
        }

        private readonly SoloBoard _board = new SoloBoard();

        private static PlayerProfile NewProfile()
        {
            var p = PlayerProfile.CreateNew(Now);
            p.Yuanbao = StartingYuanbao;
            p.Gold = StartingGold;
            p.AddMaterial(HeroGrowth.HeroExp, 3000);
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
            WorldBoss.SettlePending(_profile, _board, now);
            Save();
            return Task.FromResult<ProfileView?>(ProfileView.From(_profile, now));
        }

        public Task<WorldBossView?> GetWorldBoss()
        {
            long now = Now;
            WorldBoss.SettlePending(_profile, _board, now);
            var s = _profile.WorldBoss;
            var view = new WorldBossView
            {
                Season = s.Season, Unlocked = WorldBoss.IsUnlocked(_profile), AttemptsLeft = WorldBoss.AttemptsLeft(_profile, now),
                Best = s.Best, Rank = s.Best > 0 ? 1 : 0, Total = s.Best > 0 ? 1 : 0,
                LastSeason = s.LastSeason, LastRank = s.LastRank, LastTotal = s.LastTotal, LastReward = s.LastReward, Title = s.Title,
            };
            if (s.Best > 0) view.Top.Add(("我", s.Best));
            Save();
            return Task.FromResult<WorldBossView?>(view);
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
                Exp = r.Exp, Gold = r.Gold, Yuanbao = r.Yuanbao, LevelsGained = r.LevelsGained, HeroGained = r.HeroGained, DuplicatesGained = r.DuplicatesGained, Materials = r.Materials,
                Damage = r.Damage, BestDamage = r.BestDamage, NewBest = r.NewBest, Rank = r.BestDamage > 0 ? 1 : 0, Total = r.BestDamage > 0 ? 1 : 0,
            });
        }

        public Task<FinishStageResult> DebugWin(string stageId)
        {
            if (!Debug.isDebugBuild) return Task.FromResult(new FinishStageResult { Code = "debug_only" });
            if (_profile.PendingStageId != stageId) return Task.FromResult(new FinishStageResult { Code = "no_pending_stage" });
            ulong seed = (ulong)_profile.PendingSeed;
            _profile.PendingStageId = "";
            _profile.PendingSeed = 0;
            _profile.PendingFormation = new List<FormationEntry>();
            var dungeon = DemoMeta.FindDungeon(stageId);
            FinishStageResult result;
            if (dungeon != null)
            {
                var r = ResourceDungeons.ClaimWin(_profile, dungeon, Now, seed);
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
                    Yuanbao = clear.YuanbaoGained, LevelsGained = clear.LevelsGained, HeroGained = clear.HeroGained, DuplicatesGained = clear.DuplicatesGained,
                };
            }
            Save();
            return Task.FromResult(result);
        }

        public Task<BackendResult> SweepDungeon(string dungeonId, int count)
        {
            var d = DemoMeta.FindDungeon(dungeonId);
            if (d == null) return Task.FromResult(new BackendResult { Code = "unknown_dungeon" });
            var r = ResourceDungeons.TrySweep(_profile, d, count, Now, out _);
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
            var r = Shop.CreateOrder(_profile, productId, orderId, Now);
            if (r == ShopResult.Ok) r = Shop.Fulfill(_profile, orderId, Now);
            return Claimed(r);
        }

        public Task<BackendResult> ClaimMonthCard(string cardId) => Claimed(Shop.ClaimMonthCardDaily(_profile, cardId, Now));

        public Task<BackendResult> ClaimPass(int level, bool paid)
        {
            var r = BattlePass.Claim(_profile, level, paid, Now);
            if (r == PassClaimResult.Ok) Save();
            return Task.FromResult(new BackendResult { Ok = r == PassClaimResult.Ok, Code = r == PassClaimResult.Ok ? "ok" : r.ToString() });
        }

        public Task<BackendResult> ClaimPassAll()
        {
            int n = BattlePass.ClaimAll(_profile, Now);
            if (n > 0) Save();
            return Task.FromResult(new BackendResult { Ok = n > 0, Code = n > 0 ? "ok" : "NothingToClaim" });
        }

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

        public Task<BackendResult> Equip(string heroId, string slot, int tier)
        {
            if (!Enum.TryParse<EquipSlot>(slot, true, out var s)) return Task.FromResult(new BackendResult { Code = "invalid_slot" });
            var r = Equipment.Equip(_profile, heroId, s, tier);
            if (r != EquipResult.Ok) return Task.FromResult(new BackendResult { Code = r.ToString() });
            Quests.Report(_profile, Quests.Events.Equip, 1, Now);
            Save();
            return Task.FromResult(new BackendResult { Ok = true });
        }

        public Task<BackendResult> Unequip(string heroId, string slot)
        {
            if (!Enum.TryParse<EquipSlot>(slot, true, out var s)) return Task.FromResult(new BackendResult { Code = "invalid_slot" });
            var r = Equipment.Unequip(_profile, heroId, s);
            return Claimed(r == EquipResult.Ok, r.ToString());
        }

        public Task<BackendResult> Dismantle(string slot, int tier, int count)
        {
            if (!Equipment.TryParseStockSlot(slot, out var s, out var role)) return Task.FromResult(new BackendResult { Code = "invalid_slot" });
            var r = Equipment.Dismantle(_profile, s, tier, count, role);
            return Claimed(r == EquipResult.Ok, r.ToString());
        }

        public Task<BackendResult> BuySoulItem(string itemId)
        {
            var r = SoulShop.Buy(_profile, itemId, Now);
            return Claimed(r == SoulShopResult.Ok, r.ToString());
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
