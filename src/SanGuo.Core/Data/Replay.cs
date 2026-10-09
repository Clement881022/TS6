using System;
using System.Collections.Generic;

namespace SanGuo.Core.Data
{
    public enum ReplayActionKind { Play, EndTurn }

    public sealed class ReplayAction
    {
        public ReplayActionKind Kind;
        public int CardId;
        public int Lane = -1;
        public int Row = -1;
        public int UnitId = -1;

        public static ReplayAction Play(int cardId, int lane = -1, int row = -1, int unitId = -1) =>
            new ReplayAction { Kind = ReplayActionKind.Play, CardId = cardId, Lane = lane, Row = row, UnitId = unitId };
        public static ReplayAction EndTurn() => new ReplayAction { Kind = ReplayActionKind.EndTurn };
    }

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

    public static class ReplayVerifier
    {
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
                        Position? target = a.Lane >= 0 && a.Row >= 0 ? new Position(a.Lane, a.Row) : (Position?)null;
                        Unit? mover = null;
                        if (a.UnitId >= 0)
                        {
                            mover = battle.Units.Find(u => u.Id == a.UnitId && u.Side == Side.Player);
                            if (mover == null) return Invalid($"沒有我方單位 {a.UnitId}", battle);
                        }
                        var pr = battle.PlayCard(card, target, mover);
                        if (pr != PlayResult.Ok) return Invalid($"出牌失敗：{pr}", battle);
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

    public sealed class ReplayRecorder
    {
        public Battle Battle { get; }
        public List<ReplayAction> Actions { get; } = new List<ReplayAction>();

        public ReplayRecorder(Battle battle) { Battle = battle; }

        public PlayResult Play(CardInstance card, Position? target = null, Unit? mover = null)
        {
            var r = Battle.PlayCard(card, target, mover);
            if (r == PlayResult.Ok)
            {
                bool useTarget = target != null && (card.Def.Target == TargetRule.Enemy || card.Def.Target == TargetRule.MoveDest);
                bool useMover = mover != null && card.Def.Target == TargetRule.MoveDest;
                Actions.Add(ReplayAction.Play(card.Id, useTarget ? target!.Value.Lane : -1, useTarget ? target!.Value.Row : -1, useMover ? mover!.Id : -1));
            }
            return r;
        }

        public void EndTurn()
        {
            if (Battle.Result != BattleResult.Ongoing) return;
            Battle.EndTurn();
            Actions.Add(ReplayAction.EndTurn());
        }

        public void PlayAuto(Func<Battle, CardInstance, int>? priority = null)
        {
            while (Battle.Result == BattleResult.Ongoing)
            {
                var (pick, target, mover) = AutoPlayer.Pick(Battle, priority);
                if (pick == null) break;
                Play(pick, target, mover);
            }
            EndTurn();
        }
    }
}
