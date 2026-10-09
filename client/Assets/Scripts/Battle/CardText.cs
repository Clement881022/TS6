using System.Collections.Generic;
using System.Linq;
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
                case StatusType.CritUp: return "爆擊率提升";
                default: return type.ToString();
            }
        }

        /// <summary>武將定位（刷圖型／Boss 型／泛用）。</summary>
        public static string FocusName(HeroFocus focus)
        {
            switch (focus)
            {
                case HeroFocus.Farming: return "刷圖型";
                case HeroFocus.Boss: return "Boss 型";
                default: return "泛用";
            }
        }

        /// <summary>被動說明（未解鎖時註明 5★ 解鎖）。</summary>
        public static string Passive(HeroDef def, bool unlocked) =>
            def.Passive == PassiveKind.None ? "" :
            $"被動「{Passives.Name(def.Passive)}」{(unlocked ? "" : "（5★ 解鎖）")}：{Passives.Description(def.Passive)}";

        private static string Who(EffectDef e, CardDef def) =>
            e.OnAllies ? "全隊" : e.OnSelf && def.Target != TargetRule.Self ? "自身" : "";

        private static string Conditions(EffectDef e)
        {
            var parts = new List<string>();
            if (e.BonusPerDebuff > 0) parts.Add($"目標每有 1 種減益（燃燒／破甲／嘲諷）+{Pct(e.BonusPerDebuff)}");
            if (e.EliteBossMultiplier > 0) parts.Add($"對精英、Boss ×{e.EliteBossMultiplier:0.##}");
            return parts.Count == 0 ? "" : "（" + string.Join("；", parts) + "）";
        }

        private static bool Flat(StatusType s) => s == StatusType.DefUp || s == StatusType.DodgeUp || s == StatusType.CritUp;
        private static string FlatValue(EffectDef e) => e.Status == StatusType.CritUp || e.Status == StatusType.DodgeUp ? $"+{e.Multiplier:0}%" : $"+{e.Multiplier:0}";

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
                case TargetRule.Enemy:
                    who = def.Unlimited ? "任一敵人（點選，不受射程限制）" : $"{range} 格內的敵人（點選；範圍內沒有敵人不可施放）";
                    break;
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
                string who = Who(e, def);
                switch (e.Type)
                {
                    case EffectType.Damage:
                        parts.Add($"{who}{(e.Kind == DamageKind.Magical ? "法術傷害（謀略）" : "物理傷害（攻擊）")} {Pct(e.Multiplier)}{Conditions(e)}");
                        break;
                    case EffectType.Heal: parts.Add($"{who}治療（謀略）{Pct(e.Multiplier)}"); break;
                    case EffectType.Shield: parts.Add($"{who}護盾（謀略）{Pct(e.Multiplier)}"); break;
                    case EffectType.ApplyStatus when e.Status == StatusType.Burn:
                        parts.Add($"{who}燃燒：層數 = 謀略 {Pct(e.Multiplier)}");
                        break;
                    case EffectType.ApplyStatus when Flat(e.Status):
                        parts.Add($"{who}{StatusName(e.Status)} {FlatValue(e)}・{e.Amount}回合");
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
            if (def.KillRefund > 0) parts.Add($"擊敗目標時回 {def.KillRefund} 費");
            return string.Join("\n", parts);
        }

        private static string Pct(double multiplier) => $"{multiplier * 100:0}%";

        /// <summary>卡面用短句；完整規則保留於詳情和 tooltip，不讓長描述擠出牌框。</summary>
        public static string Summary(CardDef def)
        {
            var parts = new List<string>();
            foreach (var e in def.Effects)
            {
                string who = Who(e, def);
                switch (e.Type)
                {
                    case EffectType.Damage: parts.Add($"{who}{(e.Kind == DamageKind.Magical ? "法傷" : "物傷")} {Pct(e.Multiplier)}{(e.BonusPerDebuff > 0 || e.EliteBossMultiplier > 0 ? "＋" : "")}"); break;
                    case EffectType.Heal: parts.Add($"{who}治療 {Pct(e.Multiplier)}"); break;
                    case EffectType.Shield: parts.Add($"{who}護盾 {Pct(e.Multiplier)}"); break;
                    case EffectType.ApplyStatus:
                        if (e.Status == StatusType.Burn) { parts.Add($"{who}燃燒：謀略 {Pct(e.Multiplier)}"); break; }
                        string value = Flat(e.Status) ? FlatValue(e) : e.Status == StatusType.Taunt ? "" : e.Status == StatusType.ArmorBreak ? "-" + Pct(e.Multiplier) : e.Multiplier > 0 ? Pct(e.Multiplier) : "";
                        parts.Add($"{who}{StatusName(e.Status).Replace("提升", "")} {value} · {e.Amount}回合"); break;
                    case EffectType.Draw: parts.Add($"抽 {e.Amount} 張"); break;
                    case EffectType.GainCost: parts.Add($"獲得 {e.Amount} 費"); break;
                    case EffectType.Move: parts.Add("依移動力移動"); break;
                }
            }
            if (def.KillRefund > 0) parts.Add($"擊敗回 {def.KillRefund} 費");
            // 卡面只放得下 2 行短句：放得下就用原格式，多段效果的專屬牌改用精簡寫法，超過 2 段以「…」表示（完整規則在詳情與 tooltip）。
            if (parts.Count <= 2 && parts.All(p => Width(p) <= 18)) return string.Join("\n", parts);
            var compact = Compact(def);
            if (compact.Count > 2) compact = new List<string> { compact[0], compact[1] + "…" };
            return string.Join("\n", compact);
        }

        /// <summary>顯示寬度估算：全形字算 2、半形算 1。</summary>
        private static int Width(string s) => s.Sum(ch => ch > 0x7F ? 2 : 1);

        private static string ShortStatus(StatusType s)
        {
            switch (s)
            {
                case StatusType.DefUp: return "防";
                case StatusType.AtkUp: return "攻";
                case StatusType.IntUp: return "謀";
                case StatusType.DodgeUp: return "閃避";
                case StatusType.CritUp: return "爆擊";
                default: return StatusName(s);
            }
        }

        private static List<string> Compact(CardDef def)
        {
            var parts = new List<string>();
            foreach (var e in def.Effects)
            {
                string who = e.OnAllies ? "隊" : e.OnSelf && def.Target != TargetRule.Self ? "自" : "";
                switch (e.Type)
                {
                    case EffectType.Damage: parts.Add($"{who}{(e.Kind == DamageKind.Magical ? "法傷" : "物傷")}{Pct(e.Multiplier)}{(e.BonusPerDebuff > 0 || e.EliteBossMultiplier > 0 ? "＋" : "")}"); break;
                    case EffectType.Heal: parts.Add($"{who}治療{Pct(e.Multiplier)}"); break;
                    case EffectType.Shield: parts.Add($"{who}護盾{Pct(e.Multiplier)}"); break;
                    case EffectType.ApplyStatus:
                        if (e.Status == StatusType.Burn) { parts.Add($"{who}燃燒{Pct(e.Multiplier)}"); break; }
                        if (e.Status == StatusType.Taunt) { parts.Add($"嘲諷{e.Amount}回"); break; }
                        if (e.Status == StatusType.ArmorBreak) { parts.Add($"破甲{Pct(e.Multiplier)}·{e.Amount}回"); break; }
                        string value = Flat(e.Status) ? FlatValue(e) : "+" + Pct(e.Multiplier);
                        parts.Add($"{who}{ShortStatus(e.Status)}{value}·{e.Amount}回");
                        break;
                    case EffectType.Draw: parts.Add($"抽{e.Amount}"); break;
                    case EffectType.GainCost: parts.Add($"回{e.Amount}費"); break;
                    case EffectType.Move: parts.Add("依移動力移動"); break;
                }
            }
            if (def.KillRefund > 0) parts.Add($"擊敗回{def.KillRefund}費");
            return parts;
        }
    }
}
