using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core
{
    /// <summary>武將名單（GDD 08）：R 6、SR 15（劉備、關羽、張飛為劇情固定、不可抽取）、UR 8。同職業同稀有度的基礎屬性相同。</summary>
    public static class HeroRoster
    {
        /// <summary>劇情固定武將（SR，不在卡池內）。</summary>
        public static readonly string[] StoryHeroIds = { "liubei", "guanyu", "zhangfei" };

        private static HeroDef Make(string id, string name, string prefix, Role role, Rarity rarity) => new HeroDef
        {
            Id = id, Name = name, Role = role, Rarity = rarity,
            AttackType = CardLibrary.AttackTypeOf(role),
            Base = CardLibrary.RoleStats(role),
            Deck = CardLibrary.BuildDeck(prefix, role, rarity),
        };

        // ---- R：義勇系列 ----

        public static HeroDef MilitiaShield() => Make("r_shield", "義勇盾兵", "r_shd", Role.Tank, Rarity.R);
        public static HeroDef MilitiaSword() => Make("r_sword", "義勇劍兵", "r_swd", Role.Warrior, Rarity.R);
        public static HeroDef MilitiaArcher() => Make("r_archer", "義勇弓兵", "r_arc", Role.Ranger, Rarity.R);
        public static HeroDef MilitiaMage() => Make("r_mage", "義勇術士", "r_mag", Role.Mage, Rarity.R);
        public static HeroDef MilitiaHealer() => Make("r_healer", "義勇醫士", "r_hlr", Role.Healer, Rarity.R);
        public static HeroDef MilitiaStrategist() => Make("r_strategist", "義勇謀士", "r_str", Role.Strategist, Rarity.R);

        // ---- SR：劇情固定 ----

        public static HeroDef LiuBei() => Make("liubei", "劉備", "lb", Role.Healer, Rarity.SR);
        public static HeroDef GuanYu() => Make("guanyu", "關羽", "gy", Role.Warrior, Rarity.SR);
        public static HeroDef ZhangFei() => Make("zhangfei", "張飛", "zf", Role.Tank, Rarity.SR);

        /// <summary>第 6 關的保護目標「馬商張世平」：沒有牌，只能被保護；不在名單內。</summary>
        public static HeroDef Villager() => new HeroDef
        {
            Id = "r_villager", Name = "馬商張世平", Role = Role.Tank, Rarity = Rarity.R, AttackType = AttackType.Melee,
            Base = new Stats { Hp = 800, Atk = 0, Def = 0, Move = 1, Crit = 0, CritDmg = 150, Range = 1 },
            Deck = new List<CardDef>(),
        };

        private static List<HeroDef>? _all;

        /// <summary>全部 29 名武將（R、SR、UR 依序）。</summary>
        public static List<HeroDef> All() => _all ??= Build();

        public static HeroDef? Find(string id) => All().FirstOrDefault(h => h.Id == id);

        private static List<HeroDef> Build()
        {
            var list = new List<HeroDef>
            {
                MilitiaShield(), MilitiaSword(), MilitiaArcher(), MilitiaMage(), MilitiaHealer(), MilitiaStrategist(),

                // SR 劇情固定
                ZhangFei(), GuanYu(), LiuBei(),
                // SR 可抽取（每職業 2 隻）
                Make("zhoucang", "周倉", "zc", Role.Tank, Rarity.SR),
                Make("huangfusong", "皇甫嵩", "hfs", Role.Tank, Rarity.SR),
                Make("huaxiong", "華雄", "hx", Role.Warrior, Rarity.SR),
                Make("zhujun", "朱儁", "zj", Role.Warrior, Rarity.SR),
                Make("handang", "韓當", "hd", Role.Ranger, Rarity.SR),
                Make("zoujing", "鄒靖", "zjg", Role.Ranger, Rarity.SR),
                Make("zhangbao", "張寶", "zb", Role.Mage, Rarity.SR),
                Make("yuji", "于吉", "yj", Role.Mage, Rarity.SR),
                Make("jianyong", "簡雍", "jy", Role.Strategist, Rarity.SR),
                Make("luzhi", "盧植", "lz", Role.Strategist, Rarity.SR),
                Make("zhangzhongjing", "張仲景", "zzj", Role.Healer, Rarity.SR),
                Make("ganfuren", "甘夫人", "gfr", Role.Healer, Rarity.SR),

                // UR 首個 UP 池
                Make("ur_zhangfei", "燕人．張飛", "uzf", Role.Tank, Rarity.UR),
                Make("ur_guanyu", "武聖．關羽", "ugy", Role.Warrior, Rarity.UR),
                // UR 常駐／新手池
                Make("xiahoudun", "獨眼猛將．夏侯惇", "xhd", Role.Tank, Rarity.UR),
                Make("lvbu", "飛將．呂布", "lb2", Role.Warrior, Rarity.UR),
                Make("gongsunzan", "白馬義從．公孫瓚", "gsz", Role.Ranger, Rarity.UR),
                Make("zhangjiao", "天公將軍．張角", "zjiao", Role.Mage, Rarity.UR),
                Make("xunyu", "王佐之才．荀彧", "xy", Role.Strategist, Rarity.UR),
                Make("huatuo", "神醫．華佗", "ht", Role.Healer, Rarity.UR),
            };
            return list;
        }

        /// <summary>常駐／新手池的 UR（每職業 1 隻，共 6 隻）。</summary>
        public static readonly string[] StandardUrIds = { "xiahoudun", "lvbu", "gongsunzan", "zhangjiao", "xunyu", "huatuo" };

        /// <summary>首個 UP 池的 UR（張飛、關羽）。</summary>
        public static readonly string[] FirstUpUrIds = { "ur_zhangfei", "ur_guanyu" };

        /// <summary>可抽取的 SR（不含劇情固定的劉備、關羽、張飛），每職業 2 隻。</summary>
        public static List<string> DrawableSrIds() =>
            All().Where(h => h.Rarity == Rarity.SR && !StoryHeroIds.Contains(h.Id)).Select(h => h.Id).ToList();
    }
}
