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
        /// <summary>失敗但仍要存檔（作弊的結算要清掉進行中的關卡）。</summary>
        public bool Persist;
        public bool Won;
        public int Stars;
        public bool FirstClear;
        public int Exp;
        public int Gold;
        public int Yuanbao;
        public int LevelsGained;
        /// <summary>首通獲得的武將 id（沒有則空字串）。</summary>
        public string HeroGained = "";
        /// <summary>首通獲得的重複份（武將 id）。</summary>
        public List<string> DuplicatesGained = new List<string>();
        /// <summary>資源副本掉落的素材（主線關卡為空）。</summary>
        public Dictionary<string, int> Materials = new Dictionary<string, int>();
        /// <summary>世界 Boss：這場對 Boss 的傷害、本季最佳、是否刷新最佳。</summary>
        public long Damage;
        public long BestDamage;
        public bool NewBest;
    }

    /// <summary>
    /// 開始 / 結算關卡的共用流程（主線關卡與資源副本）：客戶端單機版與伺服器共用，規則只有一份。
    /// 開始：檢查條件、扣體力、記下進行中的關卡與種子；結算：重播操作紀錄自算勝負，客戶端無法自報。
    /// </summary>
    public static class StageFlow
    {
        public static bool IsDungeon(string stageId) => DemoMeta.FindDungeon(stageId) != null;

        /// <param name="formation">開放編隊的關卡 / 副本必須帶（見 <see cref="DemoMeta.UsesPlayerFormation"/>）；教學關忽略。</param>
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
            seed &= 0x7FFFFFFFFFFFFFFF; // 存成有號數字，不要溢位
            if (stageId == WorldBoss.StageId) seed = WorldBoss.DailySeed(now); // 世界 Boss：同一天所有人同一個種子
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
                // 打滿回合或全滅都照樣計分；擊倒 Boss 算勝利。跨月才結算的那場屬於上一季（已轉入待結算），不計入新賽季。
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
                // 輸了也已經花掉體力：帳號經驗 = 消耗的體力，照樣入帳。
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
