using System;
using System.Collections.Generic;

namespace SanGuo.Core.Data
{
    public static class ContentSerializer
    {
        public static string HeroesToJson(IEnumerable<HeroDef> heroes, bool indent = true)
        {
            var list = new List<object?>();
            foreach (var h in heroes) list.Add(HeroToObject(h));
            return MiniJson.Write(new Dictionary<string, object?> { ["heroes"] = list }, indent);
        }

        public static string SetupToJson(BattleSetup setup, bool indent = true) =>
            MiniJson.Write(SetupToObject(setup), indent);

        public static Dictionary<string, object?> StatsToObject(Stats s) => new Dictionary<string, object?>
        {
            ["hp"] = (long)s.Hp, ["atk"] = (long)s.Atk, ["def"] = (long)s.Def, ["dodge"] = (long)s.Dodge,
            ["move"] = (long)s.Move, ["int"] = (long)s.Int, ["crit"] = (long)s.Crit, ["critDmg"] = (long)s.CritDmg,
            ["range"] = (long)s.Range,
        };

        public static Dictionary<string, object?> CardToObject(CardDef c)
        {
            var effects = new List<object?>();
            foreach (var e in c.Effects)
            {
                effects.Add(new Dictionary<string, object?>
                {
                    ["type"] = e.Type.ToString(),
                    ["multiplier"] = e.Multiplier,
                    ["kind"] = e.Kind.ToString(),
                    ["status"] = e.Status.ToString(),
                    ["amount"] = (long)e.Amount,
                    ["onSelf"] = e.OnSelf,
                    ["onAllies"] = e.OnAllies,
                    ["bonusPerDebuff"] = e.BonusPerDebuff,
                    ["eliteBossMultiplier"] = e.EliteBossMultiplier,
                });
            }
            return new Dictionary<string, object?>
            {
                ["id"] = c.Id, ["name"] = c.Name, ["basic"] = c.Basic, ["cost"] = (long)c.Cost,
                ["target"] = c.Target.ToString(), ["shape"] = c.Shape.ToString(),
                ["unlimited"] = c.Unlimited, ["killRefund"] = (long)c.KillRefund,
                ["effects"] = effects,
            };
        }

        public static Dictionary<string, object?> HeroToObject(HeroDef h)
        {
            var deck = new List<object?>();
            foreach (var c in h.Deck) deck.Add(CardToObject(c));
            return new Dictionary<string, object?>
            {
                ["id"] = h.Id, ["name"] = h.Name, ["role"] = h.Role.ToString(), ["rarity"] = h.Rarity.ToString(),
                ["attackType"] = h.AttackType.ToString(), ["base"] = StatsToObject(h.Base), ["deck"] = deck,
                ["passive"] = h.Passive.ToString(), ["focus"] = h.Focus.ToString(),
            };
        }

        public static Dictionary<string, object?> EnemyToObject(EnemyDef e) => new Dictionary<string, object?>
        {
            ["id"] = e.Id, ["name"] = e.Name, ["role"] = e.Role.ToString(), ["tier"] = e.Tier.ToString(),
            ["attackType"] = e.AttackType.ToString(), ["magical"] = e.Magical,
            ["base"] = StatsToObject(e.Base), ["attackMultiplier"] = e.AttackMultiplier,
            ["chargeTurns"] = (long)e.ChargeTurns, ["chargeInterval"] = (long)e.ChargeInterval, ["chargePower"] = e.ChargePower,
            ["burnResist"] = e.BurnResist,
        };

        private static Dictionary<string, object?> PosToObject(Position p) =>
            new Dictionary<string, object?> { ["lane"] = (long)p.Lane, ["row"] = (long)p.Row };

        public static Dictionary<string, object?> SetupToObject(BattleSetup s)
        {
            var heroes = new List<object?>();
            foreach (var h in s.Heroes)
            {
                heroes.Add(new Dictionary<string, object?>
                {
                    ["def"] = HeroToObject(h.Def), ["pos"] = PosToObject(h.Pos), ["level"] = (long)h.Level,
                    ["protected"] = h.IsProtected, ["startHpPercent"] = (long)h.StartHpPercent,
                });
            }
            var enemies = new List<object?>();
            foreach (var e in s.Enemies)
                enemies.Add(new Dictionary<string, object?>
                {
                    ["def"] = EnemyToObject(e.Def), ["pos"] = PosToObject(e.Pos), ["level"] = (long)e.Level, ["objective"] = e.IsObjective,
                });
            var draw = new List<object?>();
            foreach (var id in s.ScriptedDraw) draw.Add(id);
            return new Dictionary<string, object?>
            {
                ["lanes"] = (long)s.Lanes, ["rows"] = (long)s.Rows, ["seed"] = (long)s.Seed,
                ["firstTurnRandom"] = (long)s.FirstTurnRandom, ["firstTurnMoves"] = (long)s.FirstTurnMoves,
                ["costPerTurn"] = (long)s.CostPerTurn, ["costCap"] = (long)s.CostCap, ["drawPerTurn"] = (long)s.DrawPerTurn,
                ["turnLimit"] = (long)s.TurnLimit, ["objective"] = s.Objective.ToString(), ["surviveTurns"] = (long)s.SurviveTurns,
                ["autoAllowed"] = s.AutoAllowed,
                ["formationLocked"] = s.FormationLocked, ["scriptedDraw"] = draw, ["noRandomness"] = s.NoRandomness,
                ["heroes"] = heroes, ["enemies"] = enemies,
            };
        }

        public static List<HeroDef> HeroesFromJson(string json)
        {
            var root = Obj(MiniJson.Parse(json), "根節點");
            var result = new List<HeroDef>();
            foreach (var h in List(root, "heroes")) result.Add(HeroFromObject(Obj(h, "heroes[]")));
            return result;
        }

        public static BattleSetup SetupFromJson(string json) => SetupFromObject(Obj(MiniJson.Parse(json), "根節點"));

        public static Stats StatsFromObject(Dictionary<string, object?> d)
        {
            var s = new Stats();
            s.Hp = I(d, "hp", s.Hp); s.Atk = I(d, "atk", s.Atk); s.Def = I(d, "def", s.Def);
            s.Dodge = I(d, "dodge", s.Dodge); s.Move = I(d, "move", s.Move); s.Int = I(d, "int", s.Int);
            s.Crit = I(d, "crit", s.Crit); s.CritDmg = I(d, "critDmg", s.CritDmg); s.Range = I(d, "range", s.Range);
            return s;
        }

        public static CardDef CardFromObject(Dictionary<string, object?> d)
        {
            var c = new CardDef
            {
                Id = S(d, "id", ""), Name = S(d, "name", ""), Basic = B(d, "basic", false), Cost = I(d, "cost", 0),
                Target = E(d, "target", TargetRule.Enemy),
                Shape = E(d, "shape", Shape.Single),
                Unlimited = B(d, "unlimited", false), KillRefund = I(d, "killRefund", 0),
            };
            foreach (var eo in List(d, "effects"))
            {
                var ed = Obj(eo, "effects[]");
                c.Effects.Add(new EffectDef
                {
                    Type = E(ed, "type", EffectType.Damage), Multiplier = Dbl(ed, "multiplier", 0), Kind = E(ed, "kind", DamageKind.Physical),
                    Status = E(ed, "status", StatusType.Burn), Amount = I(ed, "amount", 0), OnSelf = B(ed, "onSelf", false),
                    OnAllies = B(ed, "onAllies", false), BonusPerDebuff = Dbl(ed, "bonusPerDebuff", 0),
                    EliteBossMultiplier = Dbl(ed, "eliteBossMultiplier", 0),
                });
            }
            return c;
        }

        public static HeroDef HeroFromObject(Dictionary<string, object?> d)
        {
            var h = new HeroDef
            {
                Id = S(d, "id", ""), Name = S(d, "name", ""), Role = E(d, "role", Role.Warrior),
                Rarity = E(d, "rarity", Rarity.R), AttackType = E(d, "attackType", AttackType.Melee),
                Passive = E(d, "passive", PassiveKind.None), Focus = E(d, "focus", HeroFocus.General),
            };
            if (d.TryGetValue("base", out var b) && b is Dictionary<string, object?> bd) h.Base = StatsFromObject(bd);
            foreach (var c in List(d, "deck")) h.Deck.Add(CardFromObject(Obj(c, "deck[]")));
            return h;
        }

        public static EnemyDef EnemyFromObject(Dictionary<string, object?> d)
        {
            var e = new EnemyDef
            {
                Id = S(d, "id", ""), Name = S(d, "name", ""), Role = E(d, "role", Role.Warrior), Tier = E(d, "tier", EnemyTier.Normal),
                AttackType = E(d, "attackType", AttackType.Melee), Magical = B(d, "magical", false),
                AttackMultiplier = Dbl(d, "attackMultiplier", 1.0),
                ChargeTurns = I(d, "chargeTurns", 0), ChargeInterval = I(d, "chargeInterval", 1), ChargePower = Dbl(d, "chargePower", 2.0),
                BurnResist = Dbl(d, "burnResist", 0),
            };
            if (d.TryGetValue("base", out var b) && b is Dictionary<string, object?> bd) e.Base = StatsFromObject(bd);
            return e;
        }

        private static Position PosFromObject(Dictionary<string, object?> d, string key)
        {
            var p = Obj(d.TryGetValue(key, out var v) ? v : null, key);
            return new Position(I(p, "lane", 0), I(p, "row", 0));
        }

        public static BattleSetup SetupFromObject(Dictionary<string, object?> d)
        {
            var s = new BattleSetup
            {
                Lanes = I(d, "lanes", 5), Rows = I(d, "rows", 5), Seed = (ulong)L(d, "seed", 1),
                FirstTurnRandom = I(d, "firstTurnRandom", 5), FirstTurnMoves = I(d, "firstTurnMoves", 2), CostPerTurn = I(d, "costPerTurn", 3), CostCap = I(d, "costCap", 10),
                DrawPerTurn = I(d, "drawPerTurn", 3),
                TurnLimit = I(d, "turnLimit", 0), Objective = E(d, "objective", Objective.Annihilate), SurviveTurns = I(d, "surviveTurns", 0), AutoAllowed = B(d, "autoAllowed", true),
                FormationLocked = B(d, "formationLocked", false), NoRandomness = B(d, "noRandomness", false),
            };
            foreach (var id in List(d, "scriptedDraw"))
                if (id is string str) s.ScriptedDraw.Add(str);
            foreach (var ho in List(d, "heroes"))
            {
                var hd = Obj(ho, "heroes[]");
                var def = HeroFromObject(Obj(hd.TryGetValue("def", out var dv) ? dv : null, "heroes[].def"));
                s.Heroes.Add(new HeroSlot(def, PosFromObject(hd, "pos"), I(hd, "level", 1))
                {
                    IsProtected = B(hd, "protected", false),
                    StartHpPercent = I(hd, "startHpPercent", 100),
                });
            }
            foreach (var eo in List(d, "enemies"))
            {
                var ed = Obj(eo, "enemies[]");
                var def = EnemyFromObject(Obj(ed.TryGetValue("def", out var dv) ? dv : null, "enemies[].def"));
                s.Enemies.Add(new EnemySlot(def, PosFromObject(ed, "pos"), I(ed, "level", 1)) { IsObjective = B(ed, "objective", false) });
            }
            return s;
        }

        private static Dictionary<string, object?> Obj(object? v, string what) =>
            v as Dictionary<string, object?> ?? throw new FormatException(what + " 必須是物件");

        private static List<object?> List(Dictionary<string, object?> d, string key) =>
            d.TryGetValue(key, out var v) && v is List<object?> l ? l : new List<object?>();

        private static string S(Dictionary<string, object?> d, string key, string fallback) =>
            d.TryGetValue(key, out var v) && v is string s ? s : fallback;

        private static bool B(Dictionary<string, object?> d, string key, bool fallback) =>
            d.TryGetValue(key, out var v) && v is bool b ? b : fallback;

        private static long L(Dictionary<string, object?> d, string key, long fallback) =>
            d.TryGetValue(key, out var v) ? v switch { long l => l, double x => (long)x, _ => fallback } : fallback;

        private static int I(Dictionary<string, object?> d, string key, int fallback) => (int)L(d, key, fallback);

        private static double Dbl(Dictionary<string, object?> d, string key, double fallback) =>
            d.TryGetValue(key, out var v) ? v switch { long l => l, double x => x, _ => fallback } : fallback;

        private static T E<T>(Dictionary<string, object?> d, string key, T fallback) where T : struct
        {
            if (!d.TryGetValue(key, out var v) || !(v is string s)) return fallback;
            if (Enum.TryParse<T>(s, out var parsed)) return parsed;
            throw new FormatException($"未知的 {typeof(T).Name}：{s}");
        }
    }
}
