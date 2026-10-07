using System.Collections.Generic;
using SanGuo.Core;

namespace SanGuo.Client
{
    /// <summary>把規則資料轉成玩家看得懂的中文文字（顯示名稱與規則欄位分離）。</summary>
    public static class CardText
    {
        public static string StatusName(StatusType type)
        {
            switch (type)
            {
                case StatusType.Burn: return "火攻";
                case StatusType.Poison: return "瘟毒";
                case StatusType.Stun: return "昏亂";
                case StatusType.ArmorBreak: return "破甲";
                case StatusType.Taunt: return "挑釁";
                case StatusType.DefUp: return "防禦提升";
                case StatusType.AtkUp: return "攻擊提升";
                case StatusType.CritUp: return "暴擊提升";
                default: return type.ToString();
            }
        }

        public static string RoleName(Role role)
        {
            switch (role)
            {
                case Role.Tank: return "坦克";
                case Role.Warrior: return "戰士";
                case Role.Mage: return "法師";
                case Role.Archer: return "弓手";
                case Role.Healer: return "醫療";
                case Role.Strategist: return "軍師";
                default: return role.ToString();
            }
        }

        /// <summary>屬性名稱（懸停面板用）。謀略 = 法系（法師 / 醫療 / 軍師）的治療、法傷與增減益強度。</summary>
        public const string AtkName = "攻擊", IntName = "謀略", DefName = "防禦", MoveName = "移動力";

        public static string Keywords(CardKeywords kw)
        {
            var list = new List<string>();
            if ((kw & CardKeywords.Exhaust) != 0) list.Add("破釜");
            if ((kw & CardKeywords.Retain) != 0) list.Add("蓄勢");
            if ((kw & CardKeywords.Innate) != 0) list.Add("先登");
            return string.Join(" ", list);
        }

        public static string Target(CardDef def)
        {
            string who;
            switch (def.Target)
            {
                case TargetRule.Enemy: who = $"{def.Range} 格內任一格（點選，可空放）"; break;
                case TargetRule.EnemyLowestHp: who = $"{def.Range} 格內血量最低的敵人"; break;
                case TargetRule.MoveDest: return "選一名武將，移動到其移動力內的空格";
                case TargetRule.Self: return "自己";
                case TargetRule.AllyLowestHp: return $"{def.Range} 格內血量比例最低的隊友";
                case TargetRule.AllAllies: return "全體隊友";
                case TargetRule.AllEnemies: return "全體敵人";
                default: who = ""; break;
            }
            string shape;
            switch (def.Shape)
            {
                case Shape.Row: shape = "（整排）"; break;
                case Shape.Column: shape = "（整欄）"; break;
                case Shape.Cross: shape = "（十字）"; break;
                case Shape.All: shape = "（全體）"; break;
                default: shape = ""; break;
            }
            return who + shape;
        }

        public static string Description(CardDef def)
        {
            var parts = new List<string>();
            foreach (var e in def.Effects)
            {
                string who = e.OnSelf && def.Target != TargetRule.Self ? "自身" : "";
                switch (e.Type)
                {
                    case EffectType.Damage: parts.Add($"{who}傷害 {Pct(e.Multiplier)}"); break;
                    case EffectType.Heal: parts.Add($"{who}治療 {Pct(e.Multiplier)}"); break;
                    case EffectType.Armor: parts.Add($"{who}護甲 {Pct(e.Multiplier)}"); break;
                    case EffectType.ApplyStatus when e.Status == StatusType.ArmorBreak:
                        parts.Add($"{who}破甲：防禦 -{Pct(e.Multiplier)}・{e.Amount}回合");
                        break;
                    case EffectType.ApplyStatus:
                        string power = e.Multiplier > 0 ? $" {Pct(e.Multiplier)}" : "";
                        parts.Add($"{who}{StatusName(e.Status)}{power}・{e.Amount}回合");
                        break;
                    case EffectType.Draw: parts.Add($"抽 {e.Amount} 張"); break;
                    case EffectType.GainCost: parts.Add($"獲得 {e.Amount} 費"); break;
                    case EffectType.Move: parts.Add("移動至多「移動力」格"); break;
                }
            }
            return string.Join("\n", parts);
        }

        private static string Pct(double multiplier) => $"{multiplier * 100:0}%";
    }
}
