using System.Text;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using SanGuo.Server;

public static class Meta
{
    private static readonly Dictionary<string, int> CardPlays = new();
    private static readonly Dictionary<string, int> Teams = new();
    private static readonly Dictionary<string, (int n, int won, int turns)> ByKind = new();
    private static readonly Dictionary<int, (int n, int smart, int auto, int basic)> Counter = new();
    private static readonly List<(string stage, bool smart, bool auto, bool basic)> CfList = new();
    private static int _turns, _turnsWithChoice, _startPlayableSum, _playedSum, _leftoverSum, _handSum;
    private static readonly Dictionary<string, int> Objectives = new();
    private static readonly Dictionary<string, int> DeathsByRole = new();
    private static readonly Dictionary<string, int> UnitsByRole = new();

    private static readonly Dictionary<string, double> LastPow = new();
    private static int _att, _attLoss, _lossSamePow, _lossUp, _winSamePow, _winAfterLossUp, _winsAfterLoss;
    public static void RecordAttempt(string stage, double pow, bool won)
    {
        string k = stage + "#" + System.Threading.Thread.CurrentThread.ManagedThreadId + "#" + _ctx;
        bool retry = LastPow.TryGetValue(k, out double last);
        double gain = retry ? pow / last - 1 : 0;
        _att++;
        if (!won) { _attLoss++; if (retry && gain < 0.005) _lossSamePow++; else if (retry) _lossUp++; }
        if (won && retry) { _winsAfterLoss++; if (gain < 0.005) _winSamePow++; else _winAfterLossUp++; }
        if (won) LastPow.Remove(k); else LastPow[k] = pow;
        if (retry) { _gainSum += gain; _gainN++; }
    }
    public static string _ctx = "";
    private static double _gainSum; private static int _gainN;
    public static string AttemptReport() =>
        $"- 主線挑戰 {_att} 次，敗 {_attLoss} 次。重打 {_gainN} 次，平均每次重打前戰力提升 {100 * _gainSum / Math.Max(1, _gainN):0.0}%\n" +
        $"- 重打時戰力幾乎沒變（<0.5%）而輸：{_lossSamePow}；戰力有提升仍輸：{_lossUp}\n" +
        $"- 輸過後終於打贏的 {_winsAfterLoss} 次中：戰力沒變、純粹換種子贏 {_winSamePow}；戰力有提升才贏 {_winAfterLossUp}\n";

    public static void RecordCard(string kind, CardInstance c)
    {
        if (kind == "basic") return;
        string key = $"{kind}|{(c.Def.Basic ? "基本" : "特殊")}|{c.Owner?.Hero?.Name}|{c.Def.Name}";
        CardPlays[key] = CardPlays.GetValueOrDefault(key) + 1;
    }

    public static void RecordTurn(int startPlayable, int played, int leftover, int hand)
    {
        _turns++;
        _startPlayableSum += startPlayable; _playedSum += played; _leftoverSum += leftover; _handSum += hand;
        if (leftover > 0) _turnsWithChoice++;
    }

    public static void RecordBattle(string kind, string stage, List<FormationEntry> team, Battle b, bool won)
    {
        var t = ByKind.GetValueOrDefault(kind);
        ByKind[kind] = (t.n + 1, t.won + (won ? 1 : 0), t.turns + b.Turn);
        string comp = string.Join("+", team.Select(e => HeroRoster.Find(e.HeroId)!.Role.ToString()).OrderBy(x => x));
        Teams[comp] = Teams.GetValueOrDefault(comp) + 1;
        if (kind == "story") Objectives[b.Setup.Objective.ToString()] = Objectives.GetValueOrDefault(b.Setup.Objective.ToString()) + 1;
        foreach (var u in b.Units.Where(u => u.Side == Side.Player && u.Hero != null && !u.Protected))
        {
            string r = u.Hero!.Role.ToString();
            UnitsByRole[r] = UnitsByRole.GetValueOrDefault(r) + 1;
            if (!u.Alive) DeathsByRole[r] = DeathsByRole.GetValueOrDefault(r) + 1;
        }
    }

    public static void RecordCounterfactual(string stage, bool smart, bool auto, bool basic)
    {
        Campaign.TryParse(stage, out int ch, out _);
        var c = Counter.GetValueOrDefault(ch);
        Counter[ch] = (c.n + 1, c.smart + (smart ? 1 : 0), c.auto + (auto ? 1 : 0), c.basic + (basic ? 1 : 0));
        CfList.Add((stage, smart, auto, basic));
    }

    public static string Summary()
    {
        int names = CardPlays.Keys.Select(k => k.Split('|')[3]).Distinct().Count();
        int total = CardPlays.Values.Sum();
        int special = CardPlays.Where(k => k.Key.Contains("|特殊|")).Sum(k => k.Value);
        return $"distinctCards={names}\nspecialShare={100.0 * special / Math.Max(1, total):0}\nretryGain={100 * _gainSum / Math.Max(1, _gainN):0.0}\nwinsAfterLoss={_winsAfterLoss}\nwinsSamePower={_winSamePow}\n";
    }

    public static string Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 重打分析");
        sb.Append(AttemptReport());
        sb.AppendLine("## 戰鬥類型");
        foreach (var kv in ByKind) sb.AppendLine($"- {kv.Key}: {kv.Value.n} 場，勝率 {100.0 * kv.Value.won / kv.Value.n:0}% ，平均 {(double)kv.Value.turns / kv.Value.n:0.0} 回合");
        sb.AppendLine("## 主線目標類型（場次）");
        foreach (var kv in Objectives) sb.AppendLine($"- {kv.Key}: {kv.Value}");
        sb.AppendLine("## 主線同一場（同種子同隊伍）三種打法勝率：聰明打 / 一般自動 / 只出基本牌");
        foreach (var kv in Counter.OrderBy(k => k.Key))
            sb.AppendLine($"- 第{kv.Key}章：{kv.Value.n} 場，聰明 {100.0 * kv.Value.smart / kv.Value.n:0}%／自動 {100.0 * kv.Value.auto / kv.Value.n:0}%／基本牌 {100.0 * kv.Value.basic / kv.Value.n:0}%");
        int smartOnly = CfList.Count(x => x.smart && !x.auto), autoOnly = CfList.Count(x => x.auto && !x.smart);
        sb.AppendLine($"- 聰明打贏但自動輸：{smartOnly} 場；自動贏但聰明輸：{autoOnly} 場；只出基本牌也贏：{CfList.Count(x => x.basic)} / {CfList.Count}");
        sb.AppendLine("## 每回合決策");
        if (_turns > 0)
            sb.AppendLine($"- 共 {_turns} 回合；回合開始平均可出牌 {(double)_startPlayableSum / _turns:0.0} 張、實際出 {(double)_playedSum / _turns:0.0} 張、平均手牌 {(double)_handSum / _turns:0.0}；回合末仍留特殊牌（有取捨）的回合 {100.0 * _turnsWithChoice / _turns:0}%");
        sb.AppendLine("## 隊伍組成（職業，場次）");
        foreach (var kv in Teams.OrderByDescending(k => k.Value).Take(10)) sb.AppendLine($"- {kv.Key}: {kv.Value}");
        sb.AppendLine("## 我方陣亡率（依職業）");
        foreach (var kv in UnitsByRole) sb.AppendLine($"- {kv.Key}: {100.0 * DeathsByRole.GetValueOrDefault(kv.Key) / kv.Value:0}%（{kv.Value} 人次）");
        sb.AppendLine("## 出牌次數前 40");
        foreach (var kv in CardPlays.OrderByDescending(k => k.Value).Take(40)) sb.AppendLine($"- {kv.Key}: {kv.Value}");
        int total = CardPlays.Values.Sum();
        int special = CardPlays.Where(k => k.Key.Contains("|特殊|")).Sum(k => k.Value);
        sb.AppendLine($"- 共 {total} 次出牌，特殊牌占 {100.0 * special / Math.Max(1, total):0}%；出現過的不同卡牌 {CardPlays.Keys.Select(k => k.Split('|')[3]).Distinct().Count()} 種");
        return sb.ToString();
    }
}
