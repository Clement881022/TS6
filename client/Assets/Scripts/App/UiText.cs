#nullable enable
using System.Collections.Generic;
using SanGuo.Core;
using SanGuo.Core.Meta;

namespace SanGuo.Client
{
    /// <summary>各頁共用的文字轉換（失敗原因、素材名、獎勵描述、星數）。</summary>
    public static class UiText
    {
        public static string Stars(int n) => new string('★', n) + new string('☆', 3 - n);

        public static string MaterialName(string key)
        {
            switch (key)
            {
                case HeroGrowth.HeroExp: return "武將經驗";
                case HeroGrowth.Soul: return "將魂";
            }
            if (Equipment.TryParseKey(key, out var slot, out int tier)) return Equipment.Name(slot, tier);
            if (key.StartsWith("shard:"))
            {
                var hero = HeroRoster.Find(key.Substring(6));
                return (hero?.Name ?? "武將") + "重複份";
            }
            return key;
        }

        public static string RewardText(Reward r)
        {
            var parts = new List<string>();
            if (r.Yuanbao > 0) parts.Add($"元寶 {r.Yuanbao}");
            if (r.Gold > 0) parts.Add($"金幣 {r.Gold}");
            if (r.Stamina > 0) parts.Add($"體力 {r.Stamina}");
            foreach (var m in r.Materials) parts.Add($"{MaterialName(m.Key)} {m.Value}");
            foreach (var h in r.Heroes) parts.Add($"武將 {h}");
            return string.Join("、", parts);
        }

        public static string ExplainBackend(string code)
        {
            switch (code)
            {
                case "NotEnoughStamina": return "體力不足";
                case "NotThreeStars": return "三星通關才能掃蕩";
                case "InvalidCount": return "掃蕩次數不合法";
                case "NotEnoughYuanbao": return "元寶不足";
                case "NotEnoughGold": return "金幣不足";
                case "NotEnoughMaterial": return "素材不足";
                case "NeedsPlayerLevel": return "武將等級不能超過帳號等級";
                case "AtCap": return "已達上限";
                case "Locked": return "尚未通關解鎖這個副本的主線關卡";
                case "NotEnoughSouls": return "將魂不足";
                case "LimitReached": return "本月限購次數已用完";
                case "HeroNotOwned": return "尚未擁有這名武將";
                case "HeroMaxed": return "這名武將的突破已經足夠，不需要更多重複份";
                case "NotOwned": return "庫存中沒有這件裝備";
                case "NothingEquipped": return "這個部位沒有裝備";
                case "InvalidTier": return "裝備品階不合法";
                case "invalid_slot": return "沒有這個裝備部位";
                case "UnknownItem": return "沒有這個商品";
                case "NotCleared": return "尚未通關，無法掃蕩";
                case "NotComplete": return "尚未達成";
                case "AlreadyClaimed": return "已經領取過了";
                case "NotUnlocked": return "尚未開放";
                case "unknown_dungeon": return "沒有這個副本";
                case "NotActive": return "月卡尚未生效或已到期";
                case "AlreadyClaimedToday": return "今天已經領過了";
                case "UnknownProduct": return "沒有這個商品";
                case "UnknownOrder": return "找不到訂單";
                case "disabled": return "測試付款未開啟";
                case "unknown_stage": return "沒有這個關卡";
                case "locked": return "通關第二章後開放世界 Boss";
                case "no_attempts": return "今天的挑戰次數已用完，明天 5 點重置";
                case "invalid_formation": return "編隊不合法：請至少派 1 名已擁有的武將上場（最多 4 人）";
                case "no_pending_stage": return "沒有進行中的關卡";
                case "invalid_replay": return "操作紀錄驗證失敗，本局無效";
                case "network": return "連線失敗，請稍後再試";
                default: return "失敗：" + code;
            }
        }
    }
}
