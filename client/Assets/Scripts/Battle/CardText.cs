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
                case StatusType.Burn: return "燃燒";
                case StatusType.ArmorBreak: return "破甲";
                case StatusType.Taunt: return "嘲諷";
                case StatusType.DefUp: return "防禦提升";
                case StatusType.AtkUp: return "攻擊提升";
                case StatusType.IntUp: return "謀略提升";
                case StatusType.DodgeUp: return "閃避提升";
                default: return type.ToString();
            }
        }

        public static string RoleName(Role role)
        {
            switch (role)
            {
                case Role.Tank: return "坦克";
                case Role.Warrior: return "戰士";
                case Role.Ranger: return "遊俠";
                case Role.Mage: return "術士";
                case Role.Healer: return "醫者";
                case Role.Strategist: return "軍師";
                default: return role.ToString();
            }
        }

        /// <summary>屬性名稱（懸停面板用）。謀略 = 法術傷害、治療、護盾與燃燒層數的計算基礎。</summary>
        public const string AtkName = "攻擊", IntName = "謀略", DefName = "防禦", MoveName = "移動力";

        /// <param name="range">施放者的攻擊範圍（單體牌的射程）。</param>
        public static string Target(CardDef def, int range)
        {
            string who;
            switch (def.Target)
            {
                case TargetRule.Enemy: who = $"{range} 格內的敵人（點選；範圍內沒有敵人不可施放）"; break;
                case TargetRule.MoveDest: return "選一名武將，移動到其移動力內的空格";
                case TargetRule.Self: return "自己";
                case TargetRule.Ally: return $"{range} 格內任一隊友（點選，未指定則血量比例最低者）";
                case TargetRule.AllAllies: return "全體隊友";
                case TargetRule.AllEnemies: return "全體敵人";
                default: who = ""; break;
            }
            string shape;
            switch (def.Shape)
            {
                case Shape.Row3: shape = "（橫向 3 格）"; break;
                case Shape.Column3: shape = "（縱向 3 格）"; break;
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
                    case EffectType.Damage:
                        parts.Add($"{who}{(e.Kind == DamageKind.Magical ? "法術傷害（謀略）" : "物理傷害（攻擊）")} {Pct(e.Multiplier)}");
                        break;
                    case EffectType.Heal: parts.Add($"{who}治療（謀略）{Pct(e.Multiplier)}"); break;
                    case EffectType.Shield: parts.Add($"{who}護盾（謀略）{Pct(e.Multiplier)}"); break;
                    case EffectType.ApplyStatus when e.Status == StatusType.Burn:
                        parts.Add($"{who}燃燒：層數 = 謀略 {Pct(e.Multiplier)}");
                        break;
                    case EffectType.ApplyStatus when e.Status == StatusType.DefUp || e.Status == StatusType.DodgeUp:
                        parts.Add($"{who}{StatusName(e.Status)} +{e.Multiplier:0}・{e.Amount}回合");
                        break;
                    case EffectType.ApplyStatus when e.Status == StatusType.Taunt:
                        parts.Add($"{who}嘲諷：敵人以施放者為目標・{e.Amount}回合");
                        break;
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
