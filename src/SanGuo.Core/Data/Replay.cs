using System;
using System.Collections.Generic;

namespace SanGuo.Core.Data
{
    public enum ReplayActionKind { Play, Move, EndTurn }

    /// <summary>玩家的一個操作。卡牌與單位以戰鬥內的 id 指定（同一個關卡設定與種子下 id 是確定的）。</summary>
    public sealed class ReplayAction
    {
        public ReplayActionKind Kind;
        /// <summary>Play：<see cref="CardInstance.Id"/>。</summary>
        public int CardId;
        /// <summary>Move：<see cref="Unit.Id"/>。</summary>
        public int UnitId;
        public int Lane;
        public int Row;
        /// <summary>Play：玩家指定的敵方目標 <see cref="Unit.Id"/>（<see cref="TargetRule.EnemyAny"/> 的牌用）；-1 = 沒指定。</summary>
        public int TargetId = -1;

        public static ReplayAction Play(int cardId, int targetId = -1) =>
            new ReplayAction { Kind = ReplayActionKind.Play, CardId = cardId, TargetId = targetId };
        public static ReplayAction Move(int unitId, Position dest) =>
            new ReplayAction { Kind = ReplayActionKind.Move, UnitId = unitId, Lane = dest.Lane, Row = dest.Row };
        public static ReplayAction EndTurn() => new ReplayAction { Kind = ReplayActionKind.EndTurn };
    }

    /// <summary>驗證結果。<see cref="Valid"/> = false 代表操作紀錄不合法（作弊或損毀），不應發任何獎勵。</summary>
    public sealed class ReplayResult
    {
        public bool Valid;
        public string Error = "";
        public BattleResult Result;
        public int Turns;
        public int HeroDeaths;
        public Battle? Battle;

        public bool Won => Valid && Result == BattleResult.Won;
    }

    /// <summary>
    /// 戰鬥重播：用同一份關卡設定（含伺服器發的種子）把玩家操作逐一重放，得出確定的結果。
    /// 戰鬥核心是確定性的，所以客戶端只需回傳操作，不需要也不能「自報」勝負。
    /// </summary>
    public static class ReplayVerifier
    {
        /// <summary>單場操作數上限，擋掉惡意的超長紀錄。</summary>
        public const int MaxActions = 5000;

        public static ReplayResult Verify(BattleSetup setup, IReadOnlyList<ReplayAction> actions)
        {
            if (actions.Count > MaxActions) return Invalid("操作過多");
            var battle = new Battle(setup);
            foreach (var a in actions)
            {
                if (battle.Result != BattleResult.Ongoing) return Invalid("戰鬥結束後還有操作", battle);
                switch (a.Kind)
                {
                    case ReplayActionKind.Play:
                        var card = FindCard(battle, a.CardId);
                        if (card == null) return Invalid($"手牌中沒有卡牌 {a.CardId}", battle);
                        Unit? target = null;
                        if (a.TargetId >= 0)
                        {
                            target = battle.Units.Find(u => u.Id == a.TargetId && u.Side == Side.Enemy && u.Alive);
                            if (target == null) return Invalid($"沒有可指定的敵方單位 {a.TargetId}", battle);
                        }
                        var pr = battle.PlayCard(card, target);
                        if (pr != PlayResult.Ok) return Invalid($"出牌失敗：{pr}", battle);
                        break;
                    case ReplayActionKind.Move:
                        var unit = battle.Units.Find(u => u.Id == a.UnitId && u.Side == Side.Player);
                        if (unit == null) return Invalid($"沒有我方單位 {a.UnitId}", battle);
                        var mr = battle.Move(unit, new Position(a.Lane, a.Row));
                        if (mr != PlayResult.Ok) return Invalid($"移動失敗：{mr}", battle);
                        break;
                    case ReplayActionKind.EndTurn:
                        battle.EndTurn();
                        break;
                    default:
                        return Invalid("未知的操作", battle);
                }
            }

            int deaths = 0;
            foreach (var u in battle.Units)
                if (u.Side == Side.Player && !u.Alive) deaths++;
            return new ReplayResult
            {
                Valid = true, Result = battle.Result, Turns = battle.Turn, HeroDeaths = deaths, Battle = battle,
            };
        }

        private static CardInstance? FindCard(Battle battle, int id)
        {
            foreach (var c in battle.Hand)
                if (c.Id == id) return c;
            return null;
        }

        private static ReplayResult Invalid(string error, Battle? battle = null) =>
            new ReplayResult { Valid = false, Error = error, Battle = battle };
    }

    /// <summary>
    /// 錄製器：客戶端把每個成功的操作透過它送進戰鬥，打完把 <see cref="Actions"/> 交給伺服器。
    /// 自動戰鬥也能錄（<see cref="PlayAuto"/>）。
    /// </summary>
    public sealed class ReplayRecorder
    {
        public Battle Battle { get; }
        public List<ReplayAction> Actions { get; } = new List<ReplayAction>();

        public ReplayRecorder(Battle battle) { Battle = battle; }

        /// <param name="target">指定的敵方目標（只有 <see cref="TargetRule.EnemyAny"/> 的牌會採用）。</param>
        public PlayResult Play(CardInstance card, Unit? target = null)
        {
            var r = Battle.PlayCard(card, target);
            bool used = card.Def.Target == TargetRule.EnemyAny && target != null;
            if (r == PlayResult.Ok) Actions.Add(ReplayAction.Play(card.Id, used ? target!.Id : -1));
            return r;
        }

        public PlayResult Move(Unit unit, Position dest)
        {
            var r = Battle.Move(unit, dest);
            if (r == PlayResult.Ok) Actions.Add(ReplayAction.Move(unit.Id, dest));
            return r;
        }

        public void EndTurn()
        {
            if (Battle.Result != BattleResult.Ongoing) return; // 已分出勝負：不再有操作
            Battle.EndTurn();
            Actions.Add(ReplayAction.EndTurn());
        }

        /// <summary>自動戰鬥一個回合（同 <see cref="AutoPlayer.PlayTurn"/>），並錄下操作。</summary>
        public void PlayAuto(Func<Battle, CardInstance, int>? priority = null)
        {
            while (Battle.Result == BattleResult.Ongoing)
            {
                CardInstance? pick = null;
                int best = int.MinValue;
                foreach (var c in Battle.Hand)
                {
                    if (Battle.CanPlay(c) != PlayResult.Ok) continue;
                    int score = priority == null ? 0 : priority(Battle, c);
                    if (priority != null && score < 0) continue;
                    if (priority == null) { pick = c; break; }
                    if (score > best) { best = score; pick = c; }
                }
                if (pick == null) break;
                Play(pick);
            }
            EndTurn();
        }
    }
}
