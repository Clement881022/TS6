using System;
using System.Collections.Generic;
using SanGuo.Core.Meta;

namespace SanGuo.Core.Data
{
    public static class ProfileSerializer
    {
        public const int CurrentVersion = 3;

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
                var equipment = new Dictionary<string, object?>();
                foreach (var e in kv.Value.Equipment) equipment[e.Key] = (long)e.Value;
                heroes[kv.Key] = new Dictionary<string, object?>
                {
                    ["level"] = (long)kv.Value.Level,
                    ["stars"] = (long)kv.Value.Stars,
                    ["equipment"] = equipment,
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
                ["pendingStageId"] = p.PendingStageId,
                ["pendingSeed"] = p.PendingSeed,
                ["pendingFormation"] = p.PendingFormation.ConvertAll<object?>(e => new Dictionary<string, object?>
                {
                    ["heroId"] = e.HeroId, ["lane"] = (long)e.Lane, ["row"] = (long)e.Row,
                }),
                ["stageStars"] = IntMap(p.StageStars),
                ["createdDay"] = p.CreatedDay,
                ["dailyDay"] = p.DailyDay == long.MinValue ? (object?)null : p.DailyDay,
                ["dailyTaskProgress"] = IntMap(p.DailyTaskProgress),
                ["dailyTaskClaimed"] = SortedList(p.DailyTaskClaimed),
                ["sevenDayProgress"] = IntMap(p.SevenDayProgress),
                ["sevenDayClaimed"] = SortedList(p.SevenDayClaimed),
                ["weeklyWeek"] = p.WeeklyWeek == long.MinValue ? (object?)null : p.WeeklyWeek,
                ["weeklyProgress"] = IntMap(p.WeeklyProgress),
                ["weeklyClaimed"] = SortedList(p.WeeklyClaimed),
                ["rechargeBought"] = SortedList(p.RechargeBought),
                ["monthCardExpiry"] = LongMap(p.MonthCardExpiry),
                ["monthCardClaimedDay"] = LongMap(p.MonthCardClaimedDay),
                ["soulShopBought"] = IntMap(p.SoulShopBought),
                ["soulShopMonth"] = p.SoulShopMonth,
                ["pass"] = new Dictionary<string, object?>
                {
                    ["season"] = p.Pass.Season, ["points"] = (long)p.Pass.Points, ["tier"] = p.Pass.Tier,
                    ["claimedFree"] = SortedInts(p.Pass.ClaimedFree), ["claimedPaid"] = SortedInts(p.Pass.ClaimedPaid),
                },
                ["firstPackBought"] = p.FirstPackBought,
                ["worldBoss"] = new Dictionary<string, object?>
                {
                    ["season"] = p.WorldBoss.Season, ["best"] = p.WorldBoss.Best,
                    ["day"] = p.WorldBoss.Day == long.MinValue ? (object?)null : p.WorldBoss.Day, ["used"] = (long)p.WorldBoss.Used, ["fightSeason"] = p.WorldBoss.FightSeason,
                    ["pendingSeason"] = p.WorldBoss.PendingSeason, ["pendingBest"] = p.WorldBoss.PendingBest,
                    ["lastSeason"] = p.WorldBoss.LastSeason, ["lastRank"] = (long)p.WorldBoss.LastRank,
                    ["lastTotal"] = (long)p.WorldBoss.LastTotal, ["lastReward"] = (long)p.WorldBoss.LastReward,
                    ["title"] = p.WorldBoss.Title,
                },
                ["orders"] = StringMap(p.Orders),
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
                    if (hd.TryGetValue("equipment", out var ev) && ev is Dictionary<string, object?> equipment)
                        foreach (var e in equipment) hero.Equipment[e.Key] = (int)ToLong(e.Value);
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

            p.PendingStageId = root.TryGetValue("pendingStageId", out var psi) && psi is string psid ? psid : "";
            p.PendingSeed = Int(root, "pendingSeed", 0);
            if (root.TryGetValue("pendingFormation", out var pf) && pf is List<object?> pfl)
                foreach (var x in pfl)
                    if (x is Dictionary<string, object?> fd)
                        p.PendingFormation.Add(new FormationEntry(
                            fd.TryGetValue("heroId", out var hid) && hid is string heroIdStr ? heroIdStr : "",
                            (int)Int(fd, "lane", 0), (int)Int(fd, "row", 0)));
            ReadIntMap(root, "stageStars", p.StageStars);
            p.CreatedDay = Int(root, "createdDay", 0);
            p.DailyDay = root.TryGetValue("dailyDay", out var dd) && dd != null ? ToLong(dd) : long.MinValue;
            ReadIntMap(root, "dailyTaskProgress", p.DailyTaskProgress);
            ReadStringSet(root, "dailyTaskClaimed", p.DailyTaskClaimed);
            ReadIntMap(root, "sevenDayProgress", p.SevenDayProgress);
            ReadStringSet(root, "sevenDayClaimed", p.SevenDayClaimed);
            p.WeeklyWeek = root.TryGetValue("weeklyWeek", out var ww) && ww != null ? ToLong(ww) : long.MinValue;
            ReadIntMap(root, "weeklyProgress", p.WeeklyProgress);
            ReadStringSet(root, "weeklyClaimed", p.WeeklyClaimed);
            ReadStringSet(root, "rechargeBought", p.RechargeBought);
            ReadLongMap(root, "monthCardExpiry", p.MonthCardExpiry);
            ReadLongMap(root, "monthCardClaimedDay", p.MonthCardClaimedDay);
            ReadIntMap(root, "soulShopBought", p.SoulShopBought);
            p.SoulShopMonth = root.TryGetValue("soulShopMonth", out var ssm) && ssm is string ssms ? ssms : "";
            if (root.TryGetValue("pass", out var pso) && pso is Dictionary<string, object?> passD)
            {
                p.Pass = new PassState
                {
                    Season = passD.TryGetValue("season", out var pss) && pss is string pssv ? pssv : "",
                    Points = (int)Int(passD, "points", 0),
                    Tier = passD.TryGetValue("tier", out var pt) && pt is string ptv ? ptv : "",
                };
                ReadInts(passD, "claimedFree", p.Pass.ClaimedFree);
                ReadInts(passD, "claimedPaid", p.Pass.ClaimedPaid);
            }
            p.FirstPackBought = root.TryGetValue("firstPackBought", out var fpb) && fpb is bool fpbv && fpbv;
            if (root.TryGetValue("worldBoss", out var wbo) && wbo is Dictionary<string, object?> wb)
            {
                string Str(string key) => wb.TryGetValue(key, out var v) && v is string sv ? sv : "";
                p.WorldBoss = new WorldBossState
                {
                    Season = Str("season"), Best = Int(wb, "best", 0),
                    Day = wb.TryGetValue("day", out var wbd) && wbd != null ? ToLong(wbd) : long.MinValue, Used = (int)Int(wb, "used", 0), FightSeason = Str("fightSeason"),
                    PendingSeason = Str("pendingSeason"), PendingBest = Int(wb, "pendingBest", 0),
                    LastSeason = Str("lastSeason"), LastRank = (int)Int(wb, "lastRank", 0),
                    LastTotal = (int)Int(wb, "lastTotal", 0), LastReward = (int)Int(wb, "lastReward", 0),
                    Title = Str("title"),
                };
            }
            if (root.TryGetValue("orders", out var ord) && ord is Dictionary<string, object?> od)
                foreach (var kv in od)
                    if (kv.Value is string os) p.Orders[kv.Key] = os;

            if (version < 3) MigrateChapterZeroIds(p);
            return p;
        }

        private static void MigrateChapterZeroIds(PlayerProfile p)
        {
            static string Fix(string id) => id.StartsWith("1-", StringComparison.Ordinal) ? "0-" + id.Substring(2) : id;
            var cleared = new List<string>(p.ClearedStages);
            p.ClearedStages.Clear();
            foreach (var id in cleared) p.ClearedStages.Add(Fix(id));
            var stars = new Dictionary<string, int>(p.StageStars);
            p.StageStars.Clear();
            foreach (var kv in stars) p.StageStars[Fix(kv.Key)] = kv.Value;
            p.PendingStageId = Fix(p.PendingStageId);
        }

        private static Dictionary<string, object?> IntMap(Dictionary<string, int> map)
        {
            var d = new Dictionary<string, object?>();
            foreach (var kv in map) d[kv.Key] = (long)kv.Value;
            return d;
        }

        private static Dictionary<string, object?> LongMap(Dictionary<string, long> map)
        {
            var d = new Dictionary<string, object?>();
            foreach (var kv in map) d[kv.Key] = kv.Value;
            return d;
        }

        private static Dictionary<string, object?> StringMap(Dictionary<string, string> map)
        {
            var d = new Dictionary<string, object?>();
            foreach (var kv in map) d[kv.Key] = kv.Value;
            return d;
        }

        private static void ReadLongMap(Dictionary<string, object?> root, string key, Dictionary<string, long> target)
        {
            if (root.TryGetValue(key, out var v) && v is Dictionary<string, object?> d)
                foreach (var kv in d) target[kv.Key] = ToLong(kv.Value);
        }

        private static List<object?> SortedInts(HashSet<int> set)
        {
            var list = new List<int>(set);
            list.Sort();
            return list.ConvertAll<object?>(x => (long)x);
        }

        private static void ReadInts(Dictionary<string, object?> d, string key, HashSet<int> target)
        {
            if (d.TryGetValue(key, out var v) && v is List<object?> list)
                foreach (var x in list)
                    if (x != null) target.Add((int)ToLong(x));
        }

        private static List<object?> SortedList(HashSet<string> set)
        {
            var l = new List<string>(set);
            l.Sort(StringComparer.Ordinal);
            return l.ConvertAll<object?>(s => s);
        }

        private static void ReadIntMap(Dictionary<string, object?> root, string key, Dictionary<string, int> target)
        {
            if (root.TryGetValue(key, out var v) && v is Dictionary<string, object?> d)
                foreach (var kv in d) target[kv.Key] = (int)ToLong(kv.Value);
        }

        private static void ReadStringSet(Dictionary<string, object?> root, string key, HashSet<string> target)
        {
            if (root.TryGetValue(key, out var v) && v is List<object?> l)
                foreach (var x in l)
                    if (x is string s) target.Add(s);
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
