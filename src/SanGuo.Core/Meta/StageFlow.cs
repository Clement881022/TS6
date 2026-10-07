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
        /// <summary>資源副本掉落的素材（主線關卡為空）。</summary>
        public Dictionary<string, int> Materials = new Dictionary<string, int>();
    }

    /// <summary>
    /// 開始 / 結算關卡的共用流程（主線關卡與資源副本）：客戶端單機版與伺服器共用，規則只有一份。
    /// 開始：檢查條件、扣體力、記下進行中的關卡與種子；結算：重播操作紀錄自算勝負，客戶端無法自報。
    /// </summary>
    public static class StageFlow
    {
        public static bool IsDungeon(string stageId) => DemoMeta.FindDungeon(stageId) != null;

        public static StageStartOutcome Start(PlayerProfile p, string stageId, long now, ulong seed)
        {
            seed &= 0x7FFFFFFFFFFFFFFF; // 存成有號數字，不要溢位
            string code;
            var dungeon = DemoMeta.FindDungeon(stageId);
            if (dungeon != null)
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
            return new StageStartOutcome { Ok = true, Seed = seed };
        }

        public static StageFinishOutcome Finish(PlayerProfile p, string stageId, IReadOnlyList<ReplayAction> actions, long now)
        {
            if (p.PendingStageId == "" || p.PendingStageId != stageId) return new StageFinishOutcome { Code = "no_pending_stage" };
            var setup = DemoMeta.BuildSetup(stageId, (ulong)p.PendingSeed);
            p.PendingStageId = "";
            p.PendingSeed = 0;
            if (setup == null) return new StageFinishOutcome { Code = "unknown_stage", Persist = true };

            var replay = ReplayVerifier.Verify(setup, actions);
            if (!replay.Valid) return new StageFinishOutcome { Code = "invalid_replay", Persist = true };
            if (replay.Result != BattleResult.Won) return new StageFinishOutcome { Ok = true, Won = false };

            var dungeon = DemoMeta.FindDungeon(stageId);
            if (dungeon != null)
            {
                ResourceDungeons.ClaimWin(p, dungeon, now);
                var r = dungeon.Reward;
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
                LevelsGained = clear.LevelsGained,
            };
        }
    }
}
