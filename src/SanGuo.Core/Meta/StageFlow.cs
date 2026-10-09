using System.Collections.Generic;
using SanGuo.Core.Data;

namespace SanGuo.Core.Meta
{
    public sealed class StageStartOutcome
    {
        public bool Ok;
        public string Code = "ok";
        public ulong Seed;
    }

    public sealed class StageFinishOutcome
    {
        public bool Ok;
        public string Code = "ok";
        public bool Persist;
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
    }

    public static class StageFlow
    {
        public static StageStartOutcome Start(PlayerProfile p, string stageId, long now, ulong seed,
            IReadOnlyList<FormationEntry>? formation = null)
        {
            bool open = DemoMeta.UsesPlayerFormation(stageId);
            if (open)
            {
                string? bad = FormationRules.Validate(p, formation) ?? HardStages.CheckFormation(stageId, formation);
                if (bad != null) return new StageStartOutcome { Code = bad };
            }
            if (HardStages.TryParse(stageId, out int hc, out int hl) && !HardStages.IsUnlocked(p.ClearedStages, hc, hl))
                return new StageStartOutcome { Code = "hard_locked" };
            seed &= 0x7FFFFFFFFFFFFFFF;
            if (stageId == WorldBoss.StageId) seed = WorldBoss.DailySeed(now);
            string code;
            var dungeon = DemoMeta.FindDungeon(stageId);
            if (stageId == WorldBoss.StageId)
            {
                var r = WorldBoss.TryEnter(p, now);
                code = r == WorldBossEntry.Ok ? "ok" : r == WorldBossEntry.Locked ? "locked" : "no_attempts";
            }
            else if (dungeon != null)
            {
                var r = ResourceDungeons.TryEnter(p, dungeon, now);
                code = r == DungeonEntryResult.Ok ? "ok" : r.ToString();
            }
            else
            {
                var stage = DemoMeta.FindStage(stageId);
                if (stage == null) return new StageStartOutcome { Code = "unknown_stage" };
                var r = p.TryEnterStage(stage, now);
                code = r == StageEntryResult.Ok ? "ok" : r.ToString();
            }
            if (code != "ok") return new StageStartOutcome { Code = code };
            p.PendingStageId = stageId;
            p.PendingSeed = (long)seed;
            p.PendingFormation = open ? new List<FormationEntry>(formation!) : new List<FormationEntry>();
            return new StageStartOutcome { Ok = true, Seed = seed };
        }

        public static StageFinishOutcome Finish(PlayerProfile p, string stageId, IReadOnlyList<ReplayAction> actions, long now)
        {
            if (p.PendingStageId == "" || p.PendingStageId != stageId) return new StageFinishOutcome { Code = "no_pending_stage" };
            long pendingSeed = p.PendingSeed;
            var setup = DemoMeta.BuildSetup(stageId, (ulong)p.PendingSeed, p, p.PendingFormation);
            p.PendingStageId = "";
            p.PendingSeed = 0;
            p.PendingFormation = new List<FormationEntry>();
            if (setup == null) return new StageFinishOutcome { Code = "unknown_stage", Persist = true };

            var replay = ReplayVerifier.Verify(setup, actions);
            if (!replay.Valid) return new StageFinishOutcome { Code = "invalid_replay", Persist = true };
            if (stageId == WorldBoss.StageId)
            {
                long damage = WorldBoss.Score(replay.Battle!);
                WorldBoss.Roll(p, now);
                bool sameSeason = p.WorldBoss.FightSeason == "" || p.WorldBoss.FightSeason == p.WorldBoss.Season;
                p.WorldBoss.FightSeason = "";
                bool newBest = sameSeason && WorldBoss.Record(p, damage);
                Quests.Report(p, Quests.Events.WorldBossFight, 1, now);
                return new StageFinishOutcome
                {
                    Ok = true, Won = replay.Result == BattleResult.Won, Damage = damage, BestDamage = p.WorldBoss.Best, NewBest = newBest,
                };
            }
            if (replay.Result != BattleResult.Won)
            {
                var lostDungeon = DemoMeta.FindDungeon(stageId);
                int cost = lostDungeon?.StaminaCost ?? DemoMeta.FindStage(stageId)?.StaminaCost ?? 0;
                int levels = cost > 0 ? p.AddExp(cost, now) : 0;
                return new StageFinishOutcome { Ok = true, Won = false, Exp = cost, LevelsGained = levels };
            }

            var dungeon = DemoMeta.FindDungeon(stageId);
            if (dungeon != null)
            {
                var r = ResourceDungeons.ClaimWin(p, dungeon, now, (ulong)pendingSeed);
                return new StageFinishOutcome
                {
                    Ok = true, Won = true, Gold = r.Gold, Yuanbao = r.Yuanbao,
                    Materials = new Dictionary<string, int>(r.Materials),
                };
            }

            var stage = DemoMeta.FindStage(stageId)!;
            int stars = StarRating.Rate(replay.Battle!, stage.StarTurnPar);
            var clear = p.ClaimClear(stage, now, stars);
            return new StageFinishOutcome
            {
                Ok = true, Won = true, Stars = stars, FirstClear = clear.FirstClear,
                Exp = clear.ExpGained, Gold = clear.GoldGained, Yuanbao = clear.YuanbaoGained,
                LevelsGained = clear.LevelsGained, HeroGained = clear.HeroGained, DuplicatesGained = clear.DuplicatesGained,
            };
        }
    }
}
