using SanGuo.Core;
using SanGuo.Core.Data;

/// <summary>
/// 專家機器人（代表「很會玩的真人」）：每回合產生多個候選打法（啟發式、隨機變體、一般自動），
/// 各自在戰鬥複本上打完這回合再往後推演幾回合，挑評分最高的那個實際打出。
/// 戰鬥是確定性的，複本 = 同一份設定重播目前為止的操作。只用在對照實驗（成本高）。
/// </summary>
public sealed class ExpertBot
{
    public int Candidates = 8;
    public int Horizon = 3;
    private readonly Func<BattleSetup> _setup;
    private readonly Func<Battle, double> _eval;

    /// <param name="setup">每次呼叫都要回傳全新的同一份設定（Battle 可能改動設定物件）。</param>
    /// <param name="eval">非終局時的評分；null = 預設（我方血量比例 − 敵方血量比例）。</param>
    public ExpertBot(Func<BattleSetup> setup, Func<Battle, double> eval = null)
    {
        _setup = setup;
        _eval = eval ?? DefaultEval;
    }

    public static double DefaultEval(Battle b)
    {
        double mine = b.Units.Where(u => u.Side == Side.Player).Sum(u => Math.Max(0, u.Hp) / (double)Math.Max(1, u.MaxHp));
        double foes = b.Units.Where(u => u.Side == Side.Enemy).Sum(u => Math.Max(0, u.Hp) / (double)Math.Max(1, u.MaxHp));
        return mine * 100 - foes * 100;
    }

    private double Score(Battle b)
    {
        if (b.Result == BattleResult.Won) return 100000 - b.Turn * 100;
        if (b.Result == BattleResult.Lost) return -100000 + b.Turn * 100 + _eval(b);
        return _eval(b);
    }

    private Battle Rebuild(IReadOnlyList<ReplayAction> actions)
    {
        var r = ReplayVerifier.Verify(_setup(), actions);
        return r.Battle;
    }

    public void PlayTurn(ReplayRecorder rec)
    {
        List<ReplayAction> best = null;
        double bestScore = double.MinValue;
        for (int k = 0; k <= Candidates; k++)
        {
            var clone = Rebuild(rec.Actions);
            if (clone == null || clone.Result != BattleResult.Ongoing) return;
            var crec = new ReplayRecorder(clone);
            if (k == Candidates)
            {
                // 一般自動戰鬥這回合
                while (clone.Result == BattleResult.Ongoing)
                {
                    var (card, target, mover) = AutoPlayer.Pick(clone);
                    if (card == null) break;
                    crec.Play(card, target, mover);
                }
                crec.EndTurn();
            }
            else new SmartBot("expert", null) { Random = k == 0 ? null : new Random(k * 7919 + rec.Actions.Count) }.PlayTurn(crec);
            var planned = new List<ReplayAction>(crec.Actions);
            // 往後推演：用啟發式打 Horizon 回合
            var roll = new SmartBot("expert", null);
            for (int i = 0; i < Horizon && clone.Result == BattleResult.Ongoing; i++) roll.PlayTurn(crec);
            double s = Score(clone);
            if (s > bestScore) { bestScore = s; best = planned; }
        }
        Apply(rec, best);
    }

    private static void Apply(ReplayRecorder rec, List<ReplayAction> actions)
    {
        var b = rec.Battle;
        foreach (var a in actions)
        {
            if (b.Result != BattleResult.Ongoing) return;
            if (a.Kind == ReplayActionKind.EndTurn) { rec.EndTurn(); continue; }
            var card = b.Hand.First(c => c.Id == a.CardId);
            Position? target = a.Lane >= 0 && a.Row >= 0 ? new Position(a.Lane, a.Row) : null;
            Unit mover = a.UnitId >= 0 ? b.Units.First(u => u.Id == a.UnitId) : null;
            rec.Play(card, target, mover);
        }
    }
}
