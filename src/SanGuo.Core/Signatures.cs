using System.Collections.Generic;

namespace SanGuo.Core
{
    /// <summary>被動的顯示名稱、說明與數值（規則在 <see cref="Battle"/> 的觸發點）。</summary>
    public static class Passives
    {
        public const int WanRenDiDef = 30;
        public const int GangLieDef = 80, GangLieTurns = 3;
        public const int LvBuCrit = 30, LvBuTurns = 2;
        public const double TaiPingShare = 0.5;
        public const double WeiZhenSplash = 0.5;
        public const double WuQinHeal = 0.3;
        public const double RenJunHeal = 0.15;
        public const int MeiRanCrit = 15;
        public const double MeiRanIgnoreDef = 0.3;
        public const double ChangBanReduce = 0.15;

        public static string Name(PassiveKind p)
        {
            switch (p)
            {
                case PassiveKind.WanRenDi: return "萬人敵";
                case PassiveKind.GangLie: return "剛烈不屈";
                case PassiveKind.RenZhongLvBu: return "人中呂布";
                case PassiveKind.JuZhong: return "居中持重";
                case PassiveKind.BaiMa: return "白馬將軍";
                case PassiveKind.TaiPing: return "太平道";
                case PassiveKind.WeiZhen: return "威震華夏";
                case PassiveKind.WuQin: return "五禽戲";
                case PassiveKind.RenJun: return "仁君";
                case PassiveKind.MeiRan: return "美髯公";
                case PassiveKind.ChangBan: return "長坂斷後";
                default: return "";
            }
        }

        public static string Description(PassiveKind p)
        {
            switch (p)
            {
                case PassiveKind.WanRenDi: return $"我方回合開始時，若有敵人處於嘲諷狀態，自身防禦 +{WanRenDiDef}（1 回合）";
                case PassiveKind.GangLie: return $"生命首次低於 50% 時，自身防禦 +{GangLieDef}（{GangLieTurns} 回合）";
                case PassiveKind.RenZhongLvBu: return $"擊敗敵人後，自身爆擊率 +{LvBuCrit}%（{LvBuTurns} 回合）";
                case PassiveKind.JuZhong: return "從第 2 回合起，每回合多抽 1 張";
                case PassiveKind.BaiMa: return "每回合第一次擊敗敵人時回 1 費";
                case PassiveKind.TaiPing: return "燃燒中的敵人被擊敗時，剩餘燃燒層數的一半轉移給每名相鄰的敵人";
                case PassiveKind.WeiZhen: return "攻擊擊敗敵人時，對其相鄰的敵人造成該次傷害 50% 的濺射";
                case PassiveKind.WuQin: return "我方回合結束時，治療生命比例最低的友軍（謀略 30%）";
                case PassiveKind.RenJun: return "存活時，我方受到的治療 +15%";
                case PassiveKind.MeiRan: return $"爆擊率 +{MeiRanCrit}%；爆擊時無視目標 30% 防禦";
                case PassiveKind.ChangBan: return "自身施放的嘲諷仍在任一敵人身上時，受到的傷害 −15%";
                default: return "";
            }
        }
    }

    /// <summary>
    /// SR／UR 專屬牌與被動（企劃 2026-10-09 定案：SR／UR 的 2 張特殊牌全部換成專屬牌，UR 與劉關張 5★ 解鎖被動）。
    /// 第一批：UR 8 名與劉關張（docs/signature-cards-batch1.md）；第二批：可抽 SR 12 名（docs/signature-cards-batch2.md，SR 沒有被動）。
    /// </summary>
    public static class Signatures
    {
        private static EffectDef Dmg(double mult, DamageKind kind = DamageKind.Physical) =>
            new EffectDef { Type = EffectType.Damage, Multiplier = mult, Kind = kind };
        private static EffectDef Heal(double mult) => new EffectDef { Type = EffectType.Heal, Multiplier = mult };
        private static EffectDef Shield(double mult) => new EffectDef { Type = EffectType.Shield, Multiplier = mult };
        private static EffectDef Status(StatusType type, double power, int turns) =>
            new EffectDef { Type = EffectType.ApplyStatus, Status = type, Multiplier = power, Amount = turns };
        private static EffectDef Self(EffectDef e) { e.OnSelf = true; return e; }
        private static EffectDef Allies(EffectDef e) { e.OnAllies = true; return e; }
        private static EffectDef Draw(int n) => new EffectDef { Type = EffectType.Draw, Amount = n };
        private static EffectDef Gain(int n) => new EffectDef { Type = EffectType.GainCost, Amount = n };

        private static CardDef Card(string id, string name, int cost, TargetRule target, Shape shape, params EffectDef[] effects) =>
            new CardDef { Id = id, Name = name, Cost = cost, Target = target, Shape = shape, Effects = new List<EffectDef>(effects) };
        private static CardDef Refund(CardDef c) { c.KillRefund = 1; return c; }
        private static CardDef Far(CardDef c) { c.Unlimited = true; return c; }
        private static EffectDef Bonus(EffectDef e, double perDebuff) { e.BonusPerDebuff = perDebuff; return e; }

        private sealed class Entry
        {
            public HeroFocus Focus;
            public PassiveKind Passive;
            public System.Func<string, List<CardDef>> Cards = _ => new List<CardDef>();
        }

        private static readonly Dictionary<string, Entry> Table = new Dictionary<string, Entry>
        {
            ["ur_zhangfei"] = new Entry
            {
                Focus = HeroFocus.Farming, Passive = PassiveKind.WanRenDi,
                Cards = p => new List<CardDef>
                {
                    Card(p + "_dangyang", "當陽怒吼", 1, TargetRule.AllEnemies, Shape.All,
                        Status(StatusType.Taunt, 0, 2), Self(Status(StatusType.DefUp, 50, 1))),
                    Card(p + "_duanqiao", "據水斷橋", 1, TargetRule.AllEnemies, Shape.All,
                        Status(StatusType.Taunt, 0, 1), Self(Status(StatusType.DefUp, 75, 2)), Draw(1)),
                },
            },
            ["xiahoudun"] = new Entry
            {
                Focus = HeroFocus.Boss, Passive = PassiveKind.GangLie,
                Cards = p => new List<CardDef>
                {
                    new CardDef
                    {
                        Id = p + "_ganglie", Name = "剛烈", Cost = 1, Target = TargetRule.Enemy, Shape = Shape.Single, Unlimited = true,
                        Effects = new List<EffectDef> { Status(StatusType.Taunt, 0, 2), Self(Status(StatusType.DefUp, 100, 2)), Draw(1) },
                    },
                    Card(p + "_bashi", "拔矢啖睛", 2, TargetRule.AllAllies, Shape.All,
                        Status(StatusType.DefUp, 50, 2), Self(Status(StatusType.DefUp, 75, 2))),
                },
            },
            ["lvbu"] = new Entry
            {
                Focus = HeroFocus.Boss, Passive = PassiveKind.RenZhongLvBu,
                Cards = p => new List<CardDef>
                {
                    Card(p + "_wushuang", "無雙", 2, TargetRule.Enemy, Shape.Single, Dmg(2.33), Self(Status(StatusType.CritUp, 50, 2))),
                    Card(p + "_tianxia", "天下無雙", 1, TargetRule.Enemy, Shape.Single, new EffectDef
                    {
                        Type = EffectType.Damage, Multiplier = 1.6, Kind = DamageKind.Physical, BonusPerDebuff = 0.3,
                    }),
                },
            },
            ["xunyu"] = new Entry
            {
                Focus = HeroFocus.Boss, Passive = PassiveKind.JuZhong,
                Cards = p => new List<CardDef>
                {
                    Card(p + "_wangzuo", "王佐", 1, TargetRule.AllAllies, Shape.All,
                        Status(StatusType.AtkUp, 0.25, 2), Status(StatusType.IntUp, 0.25, 2), Draw(1)),
                    Card(p + "_yunchou", "運籌帷幄", 2, TargetRule.Self, Shape.Single, Gain(3), Draw(3)),
                },
            },
            ["gongsunzan"] = new Entry
            {
                Focus = HeroFocus.Farming, Passive = PassiveKind.BaiMa,
                Cards = p => new List<CardDef>
                {
                    Card(p + "_arrows", "白馬箭雨", 2, TargetRule.Enemy, Shape.Cross, Dmg(1.5)),
                    new CardDef
                    {
                        Id = p + "_youqi", Name = "游騎", Cost = 1, Target = TargetRule.Enemy, Shape = Shape.Single, KillRefund = 1,
                        Effects = new List<EffectDef> { Dmg(1.0), Self(Status(StatusType.DodgeUp, 20, 2)) },
                    },
                },
            },
            ["zhangjiao"] = new Entry
            {
                Focus = HeroFocus.Farming, Passive = PassiveKind.TaiPing,
                Cards = p => new List<CardDef>
                {
                    Card(p + "_huangtian", "黃天當立", 3, TargetRule.AllEnemies, Shape.All, Dmg(1.22, DamageKind.Magical)),
                    Card(p + "_leigong", "雷公助我", 2, TargetRule.Enemy, Shape.Cross, Status(StatusType.Burn, 1.5, 0)),
                },
            },
            ["ur_guanyu"] = new Entry
            {
                Focus = HeroFocus.General, Passive = PassiveKind.WeiZhen,
                Cards = p => new List<CardDef>
                {
                    Card(p + "_qinglong", "青龍偃月", 2, TargetRule.Enemy, Shape.Row3, Dmg(2.0)),
                    new CardDef
                    {
                        Id = p + "_guoguan", Name = "過關斬將", Cost = 2, Target = TargetRule.Enemy, Shape = Shape.Single, KillRefund = 1,
                        Effects = new List<EffectDef> { Dmg(2.67) },
                    },
                },
            },
            ["huatuo"] = new Entry
            {
                Focus = HeroFocus.General, Passive = PassiveKind.WuQin,
                Cards = p => new List<CardDef>
                {
                    Card(p + "_qingnang", "青囊", 2, TargetRule.AllAllies, Shape.All, Heal(1.0)),
                    Card(p + "_mafei", "麻沸散", 1, TargetRule.Ally, Shape.Single, Heal(1.33), Shield(0.67)),
                },
            },
            ["liubei"] = new Entry
            {
                Focus = HeroFocus.General, Passive = PassiveKind.RenJun,
                Cards = p => new List<CardDef>
                {
                    Card(p + "_rende", "仁德", 1, TargetRule.AllAllies, Shape.All, Heal(0.56)),
                    Card(p + "_yide", "以德服人", 1, TargetRule.Ally, Shape.Single, Draw(1), Gain(1), Heal(0.67)),
                },
            },
            ["guanyu"] = new Entry
            {
                Focus = HeroFocus.General, Passive = PassiveKind.MeiRan,
                Cards = p => new List<CardDef>
                {
                    Card(p + "_tuodao", "拖刀計", 2, TargetRule.Enemy, Shape.Single, Dmg(2.33)),
                    Card(p + "_wenjiu", "溫酒斬將", 2, TargetRule.Enemy, Shape.Single, new EffectDef
                    {
                        Type = EffectType.Damage, Multiplier = 1.6, Kind = DamageKind.Physical, EliteBossMultiplier = 1.5,
                    }),
                },
            },
            ["zhangfei"] = new Entry
            {
                Focus = HeroFocus.General, Passive = PassiveKind.ChangBan,
                Cards = p => new List<CardDef>
                {
                    Card(p + "_yanren", "燕人怒吼", 1, TargetRule.AllEnemies, Shape.All,
                        Status(StatusType.Taunt, 0, 1), Self(Status(StatusType.DefUp, 50, 2))),
                    Card(p + "_dangyangqiao", "當陽橋喝斷", 2, TargetRule.AllEnemies, Shape.All,
                        Status(StatusType.Taunt, 0, 1), Allies(Status(StatusType.DefUp, 50, 1)), Draw(1)),
                },
            },
        };

        static Signatures()
        {
            // ---- 第二批：可抽 SR（每職業刷圖型、Boss 型各 1） ----
            Sr("zhoucang", HeroFocus.Farming, p => new List<CardDef>
            {
                Card(p + "_kangdao", "扛刀護主", 1, TargetRule.AllEnemies, Shape.All, Status(StatusType.Taunt, 0, 1), Self(Status(StatusType.DefUp, 50, 2))),
                Card(p + "_likang", "力扛千斤", 1, TargetRule.AllAllies, Shape.All, Status(StatusType.DefUp, 50, 1), Self(Status(StatusType.DefUp, 25, 2)), Draw(1)),
            });
            Sr("huangfusong", HeroFocus.Boss, p => new List<CardDef>
            {
                Far(Card(p + "_jianbi", "堅壁", 1, TargetRule.Enemy, Shape.Single, Status(StatusType.Taunt, 0, 2), Self(Status(StatusType.DefUp, 75, 2)))),
                Card(p + "_chizhong", "持重", 2, TargetRule.AllAllies, Shape.All, Status(StatusType.DefUp, 50, 2), Self(Status(StatusType.DefUp, 25, 2))),
            });
            Sr("huaxiong", HeroFocus.Farming, p => new List<CardDef>
            {
                Card(p + "_sishui", "汜水橫刀", 2, TargetRule.Enemy, Shape.Row3, Dmg(1.56)),
                Refund(Card(p + "_xiaoqi", "驍騎突陣", 1, TargetRule.Enemy, Shape.Column3, Dmg(0.89))),
            });
            Sr("zhujun", HeroFocus.Boss, p => new List<CardDef>
            {
                Card(p + "_pozhen", "破陣重擊", 2, TargetRule.Enemy, Shape.Single, Dmg(2.17), Status(StatusType.ArmorBreak, 0.25, 2)),
                Card(p + "_fenyong", "奮勇", 1, TargetRule.Enemy, Shape.Single, Dmg(1.0), Self(Status(StatusType.CritUp, 50, 2))),
            });
            Sr("zoujing", HeroFocus.Farming, p => new List<CardDef>
            {
                Card(p + "_lianzhu", "連珠箭雨", 2, TargetRule.Enemy, Shape.Cross, Dmg(1.17)),
                Refund(Card(p + "_youji", "游擊", 1, TargetRule.Enemy, Shape.Single, Dmg(1.33))),
            });
            Sr("handang", HeroFocus.Boss, p => new List<CardDef>
            {
                Card(p + "_chuanjia", "穿甲箭", 2, TargetRule.Enemy, Shape.Single, Dmg(2.0), Status(StatusType.ArmorBreak, 0.4, 2)),
                Card(p + "_yingyan", "鷹眼", 1, TargetRule.Enemy, Shape.Single, Dmg(1.0), Self(Status(StatusType.CritUp, 50, 2))),
            });
            Sr("zhangbao", HeroFocus.Farming, p => new List<CardDef>
            {
                Card(p + "_digong", "地公妖術", 3, TargetRule.AllEnemies, Shape.All, Dmg(1.0, DamageKind.Magical)),
                Card(p + "_huolong", "火龍陣", 2, TargetRule.Enemy, Shape.Row3, Status(StatusType.Burn, 1.56, 0)),
            });
            Sr("yuji", HeroFocus.Boss, p => new List<CardDef>
            {
                Card(p + "_fushui", "符水咒", 2, TargetRule.Enemy, Shape.Single, Status(StatusType.Burn, 2.33, 0)),
                Card(p + "_taiping", "太平青領", 1, TargetRule.Enemy, Shape.Single, Bonus(Dmg(1.33, DamageKind.Magical), 0.3)),
            });
            Sr("jianyong", HeroFocus.Farming, p => new List<CardDef>
            {
                Card(p + "_tanxiao", "談笑風生", 1, TargetRule.Self, Shape.Single, Draw(3), Gain(1)),
                Card(p + "_shuoxiang", "說降", 1, TargetRule.AllAllies, Shape.All, Status(StatusType.AtkUp, 0.25, 2), Draw(2)),
            });
            Sr("luzhi", HeroFocus.Boss, p => new List<CardDef>
            {
                Card(p + "_bingfa", "兵法傳授", 1, TargetRule.AllAllies, Shape.All, Status(StatusType.AtkUp, 0.2, 2), Status(StatusType.IntUp, 0.2, 2)),
                Card(p + "_duzhan", "督戰", 2, TargetRule.AllAllies, Shape.All, Status(StatusType.AtkUp, 0.5, 2), Draw(1)),
            });
            Sr("ganfuren", HeroFocus.Farming, p => new List<CardDef>
            {
                Card(p + "_cixin", "慈心", 1, TargetRule.AllAllies, Shape.All, Heal(0.56)),
                Card(p + "_anfu", "安撫", 1, TargetRule.AllAllies, Shape.All, Shield(0.37)),
            });
            Sr("zhangzhongjing", HeroFocus.Boss, p => new List<CardDef>
            {
                Card(p + "_shanghan", "傷寒雜病論", 1, TargetRule.Ally, Shape.Single, Heal(1.67)),
                Card(p + "_zuotang", "坐堂行醫", 2, TargetRule.Ally, Shape.Single, Shield(1.0), Heal(0.83)),
            });
        }

        private static void Sr(string id, HeroFocus focus, System.Func<string, List<CardDef>> cards) =>
            Table[id] = new Entry { Focus = focus, Passive = PassiveKind.None, Cards = cards };

        public static bool Has(string heroId) => Table.ContainsKey(heroId);

        /// <summary>把專屬牌、被動與定位套到武將定義上（沒有專屬設計的武將維持職業標竿卡）。</summary>
        public static void Apply(HeroDef def, string prefix)
        {
            if (!Table.TryGetValue(def.Id, out var e)) return;
            def.Focus = e.Focus;
            def.Passive = e.Passive;
            def.Deck.RemoveAll(c => !c.Basic);
            def.Deck.AddRange(e.Cards(prefix));
        }
    }
}
