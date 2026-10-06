using System;
using System.Collections.Generic;
using SanGuo.Core.Meta;

namespace SanGuo.Core.Data
{
    /// <summary>
    /// 玩家存檔 &lt;-&gt; JSON。客戶端本機快取與伺服器儲存共用同一格式。
    /// 格式帶版本號：讀到比自己新的版本會拒絕（避免舊客戶端誤覆寫新資料）；缺少的欄位用預設值，方便日後加欄位。
    /// </summary>
    public static class ProfileSerializer
    {
        public const int CurrentVersion = 1;

        public static string ToJson(PlayerProfile p, bool indent = false) => MiniJson.Write(ToObject(p), indent);

        public static PlayerProfile FromJson(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object?> root))
                throw new FormatException("存檔根節點必須是物件");
            return FromObject(root);
        }

        public static Dictionary<string, object?> ToObject(PlayerProfile p)
        {
            var stages = new List<string>(p.ClearedStages);
            stages.Sort(StringComparer.Ordinal);

            var heroes = new Dictionary<string, object?>();
            foreach (var kv in p.Heroes)
            {
                var cards = new Dictionary<string, object?>();
                foreach (var c in kv.Value.CardLevels) cards[c.Key] = (long)c.Value;
                heroes[kv.Key] = new Dictionary<string, object?>
                {
                    ["level"] = (long)kv.Value.Level,
                    ["stars"] = (long)kv.Value.Stars,
                    ["cards"] = cards,
                };
            }

            var materials = new Dictionary<string, object?>();
            foreach (var m in p.Materials) materials[m.Key] = (long)m.Value;

            var pools = new Dictionary<string, object?>();
            foreach (var kv in p.PoolStates)
            {
                pools[kv.Key] = new Dictionary<string, object?>
                {
                    ["pullsSinceUr"] = (long)kv.Value.PullsSinceUr,
                    ["upGuaranteed"] = kv.Value.UpGuaranteed,
                    ["totalPulls"] = (long)kv.Value.TotalPulls,
                    ["tenPulls"] = (long)kv.Value.TenPulls,
                };
            }

            return new Dictionary<string, object?>
            {
                ["version"] = (long)CurrentVersion,
                ["level"] = (long)p.Level,
                ["exp"] = (long)p.Exp,
                ["yuanbao"] = (long)p.Yuanbao,
                ["gold"] = (long)p.Gold,
                ["stamina"] = new Dictionary<string, object?>
                {
                    ["cap"] = (long)p.Stamina.Cap,
                    ["regenSeconds"] = (long)p.Stamina.RegenSeconds,
                    ["current"] = (long)p.Stamina.Current,
                    ["anchor"] = p.Stamina.Anchor,
                },
                ["clearedStages"] = stages.ConvertAll<object?>(s => s),
                ["heroes"] = heroes,
                ["materials"] = materials,
                ["pools"] = pools,
            };
        }

        public static PlayerProfile FromObject(Dictionary<string, object?> root)
        {
            int version = (int)Int(root, "version", 0);
            if (version > CurrentVersion)
                throw new FormatException($"存檔版本 {version} 比此程式支援的 {CurrentVersion} 新");

            var p = new PlayerProfile
            {
                Level = (int)Int(root, "level", 1),
                Exp = (int)Int(root, "exp", 0),
                Yuanbao = (int)Int(root, "yuanbao", 0),
                Gold = (int)Int(root, "gold", 0),
            };

            if (root.TryGetValue("stamina", out var st) && st is Dictionary<string, object?> sd)
            {
                p.Stamina = new StaminaClock
                {
                    Cap = (int)Int(sd, "cap", 120),
                    RegenSeconds = (int)Int(sd, "regenSeconds", 360),
                    Current = (int)Int(sd, "current", 0),
                    Anchor = Int(sd, "anchor", 0),
                };
            }

            if (root.TryGetValue("clearedStages", out var cs) && cs is List<object?> stages)
                foreach (var s in stages)
                    if (s is string id) p.ClearedStages.Add(id);

            if (root.TryGetValue("heroes", out var hs) && hs is Dictionary<string, object?> heroes)
            {
                foreach (var kv in heroes)
                {
                    if (!(kv.Value is Dictionary<string, object?> hd)) continue;
                    var hero = new HeroState
                    {
                        HeroId = kv.Key,
                        Level = (int)Int(hd, "level", 1),
                        Stars = (int)Int(hd, "stars", 0),
                    };
                    if (hd.TryGetValue("cards", out var cv) && cv is Dictionary<string, object?> cards)
                        foreach (var c in cards) hero.CardLevels[c.Key] = (int)ToLong(c.Value);
                    p.Heroes[kv.Key] = hero;
                }
            }

            if (root.TryGetValue("materials", out var ms) && ms is Dictionary<string, object?> materials)
                foreach (var m in materials) p.Materials[m.Key] = (int)ToLong(m.Value);

            if (root.TryGetValue("pools", out var ps) && ps is Dictionary<string, object?> pools)
            {
                foreach (var kv in pools)
                {
                    if (!(kv.Value is Dictionary<string, object?> pd)) continue;
                    p.PoolStates[kv.Key] = new PoolState
                    {
                        PullsSinceUr = (int)Int(pd, "pullsSinceUr", 0),
                        UpGuaranteed = pd.TryGetValue("upGuaranteed", out var ug) && ug is bool b && b,
                        TotalPulls = (int)Int(pd, "totalPulls", 0),
                        TenPulls = (int)Int(pd, "tenPulls", 0),
                    };
                }
            }

            return p;
        }

        private static long Int(Dictionary<string, object?> d, string key, long fallback) =>
            d.TryGetValue(key, out var v) && v != null ? ToLong(v) : fallback;

        private static long ToLong(object? v) => v switch
        {
            long l => l,
            double d => (long)d,
            _ => throw new FormatException("預期數字"),
        };
    }
}
