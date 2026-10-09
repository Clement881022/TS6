using System.Text;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using SanGuo.Server;

public sealed class SmartBot
{
    public bool BasicOnly;
    public Random Random;
    private readonly string _kind;
    private bool Tracked => _kind == "story" || _kind == "boss" || _kind == "dungeon";
    public SmartBot(string kind, List<string> team) { _kind = kind; }

    public void PlayTurn(ReplayRecorder rec)
    {
        var b = rec.Battle;
        int startPlayable = b.Hand.Count(c => c.Def.Target != TargetRule.MoveDest && b.CanPlay(c) == PlayResult.Ok);
        int played = 0;
        for (int guard = 0; guard < 60 && b.Result == BattleResult.Ongoing; guard++)
        {
            if (PlayOne(rec)) { played++; continue; }
            var move = b.Hand.FirstOrDefault(c => c.Def.Target == TargetRule.MoveDest && b.CanPlay(c) == PlayResult.Ok);
            var adv = move == null ? null : b.AliveUnits(Side.Player)
                .Where(h => !h.Protected && h.Hero != null && b.CanMoveUnit(h) && AutoPlayer.ChooseMove(b, h) != null)
                .OrderBy(h => h.Hero!.Role == Role.Tank ? 1 : 0).FirstOrDefault();
            if (move != null && adv != null) { rec.Play(move, AutoPlayer.ChooseMove(b, adv), adv); continue; }
            break;
        }
        if (Tracked && b.Result == BattleResult.Ongoing)
        {
            int leftover = b.Hand.Count(c => c.Def.Target != TargetRule.MoveDest && !c.Def.Basic);
            Meta.RecordTurn(startPlayable, played, leftover, b.Hand.Count);
        }
        rec.EndTurn();
    }

    private void RecordCard(CardInstance c) { if (Tracked) Meta.RecordCard(_kind, c); }

    private bool PlayOne(ReplayRecorder rec)
    {
        var b = rec.Battle;
        var options = b.Hand.Where(c => c.Def.Target != TargetRule.MoveDest && b.CanPlay(c) == PlayResult.Ok)
            .Where(c => !BasicOnly || c.Def.Basic)
            .OrderByDescending(c => c.Def.Basic ? 1 : 2).ToList();
        if (Random != null)
        {
            options = options.OrderBy(_ => Random.Next()).ToList();
            if (options.Count > 0 && Random.NextDouble() < 0.15) return false;
        }
        foreach (var c in options)
        {
            var owner = c.Owner!;
            var effect = c.Def.Effects.Count > 0 ? c.Def.Effects[0] : null;
            if (c.Def.Target == TargetRule.Ally)
            {
                var hurt = b.AliveUnits(Side.Player).Any(u => u.Hp < u.MaxHp * 0.75);
                if (!hurt && effect != null && effect.Type == EffectType.Heal) continue;
                if (b.ResolveTargets(owner, c.Def) == null) continue;
                if (rec.Play(c) == PlayResult.Ok) { RecordCard(c); return true; }
                continue;
            }
            if (c.Def.Target == TargetRule.Enemy)
            {
                var inRange = b.AliveUnits(Side.Enemy).Where(e => Position.Distance(owner.Pos, e.Pos) <= Battle.CardRange(owner, c.Def)).ToList();
                if (inRange.Count == 0) continue;
                Unit pick;
                bool debuff = c.Def.Effects.Any(e => e.Status == StatusType.ArmorBreak || e.Status == StatusType.Burn);
                if (debuff) pick = inRange.OrderByDescending(e => e.EffectiveDef).ThenByDescending(e => e.Hp).First();
                else pick = inRange.OrderByDescending(e => e.IsObjective ? 2 : e.Stats.Range > 1 && e.Stats.Hp < 350 ? 1 : 0).ThenBy(e => e.Hp).First();
                if (Random != null && Random.NextDouble() < 0.5) pick = inRange[Random.Next(inRange.Count)];
                if (c.Def.Shape == Shape.Row3)
                    pick = inRange.OrderByDescending(e => b.AliveUnits(Side.Enemy).Count(o => o.Pos.Row == e.Pos.Row && Math.Abs(o.Pos.Lane - e.Pos.Lane) <= 1)).First();
                if (rec.Play(c, pick.Pos) == PlayResult.Ok) { RecordCard(c); return true; }
                continue;
            }
            if (c.Def.Target == TargetRule.AllEnemies && effect != null && effect.Status == StatusType.Taunt)
            {
                var foes = b.AliveUnits(Side.Enemy);
                if (!foes.Any(e => !e.Statuses.ContainsKey(StatusType.Taunt) || e.Charging)) continue;
            }
            if (c.Def.Target == TargetRule.AllAllies && b.AliveUnits(Side.Enemy).Count == 0) continue;
            if (rec.Play(c) == PlayResult.Ok) { RecordCard(c); return true; }
        }
        return false;
    }
}
