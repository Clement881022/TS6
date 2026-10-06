using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    public enum BreakthroughKind
    {
        /// <summary>屬性加成（百分比，加在等級成長之上）。</summary>
        StatBonus,
        /// <summary>把套牌中的某張卡替換成強化版（新效果，不是數值加成）。</summary>
        UpgradeCard,
        /// <summary>套牌新增一張專屬牌。</summary>
        AddCard,
        /// <summary>解鎖被動（被動由戰鬥核心依 id 實作，尚未接入）。</summary>
        Passive,
    }

    /// <summary>突破某一星帶來的獨特效果。</summary>
    public sealed class BreakthroughEffect
    {
        /// <summary>第幾星解鎖（1–5）。</summary>
        public int Stars;
        public BreakthroughKind Kind;
        /// <summary>UpgradeCard：被替換的卡牌 id。</summary>
        public string TargetCardId = "";
        /// <summary>UpgradeCard / AddCard：新卡牌。</summary>
        public CardDef? NewCard;
        /// <summary>Passive：被動 id。</summary>
        public string PassiveId = "";
        /// <summary>StatBonus：血量 / 攻擊 / 防禦加成百分比（10 = +10%）。</summary>
        public int HpPct;
        public int AtkPct;
        public int DefPct;
        /// <summary>顯示給玩家的說明。</summary>
        public string Description = "";
    }

    /// <summary>
    /// 各武將的突破效果表（資料驅動，之後由 JSON 載入）。
    /// 業界做法：屬性與特殊效果混搭。每名武將只需要設計少數幾星的特殊效果（預設 2★、4★），
    /// 其餘星級自動套用屬性模板（<see cref="DefaultStatEffect"/>），省下逐星設計的工。
    /// </summary>
    public sealed class BreakthroughTable
    {
        private readonly Dictionary<string, List<BreakthroughEffect>> _byHero =
            new Dictionary<string, List<BreakthroughEffect>>();

        /// <summary>屬性模板：1★ 血量 +10%、3★ 攻擊 +10%、5★ 全屬性 +15%；2★ / 4★ 預設留給特殊效果。</summary>
        public static BreakthroughEffect? DefaultStatEffect(int stars) => stars switch
        {
            1 => new BreakthroughEffect { Stars = 1, Kind = BreakthroughKind.StatBonus, HpPct = 10, Description = "血量 +10%" },
            3 => new BreakthroughEffect { Stars = 3, Kind = BreakthroughKind.StatBonus, AtkPct = 10, Description = "攻擊 +10%" },
            5 => new BreakthroughEffect { Stars = 5, Kind = BreakthroughKind.StatBonus, HpPct = 15, AtkPct = 15, DefPct = 15, Description = "血量 / 攻擊 / 防禦 +15%" },
            _ => null,
        };

        /// <summary>登錄武將的特殊效果（通常 2★、4★）；沒指定的星級自動補屬性模板，仍空缺的星級（如 2★ 沒有特殊效果）補血量 / 防禦加成。</summary>
        public void Register(string heroId, params BreakthroughEffect[] specials)
        {
            var list = specials.ToList();
            for (int star = 1; star <= HeroGrowth.MaxStars; star++)
            {
                if (list.Any(e => e.Stars == star)) continue;
                list.Add(DefaultStatEffect(star) ?? new BreakthroughEffect
                {
                    Stars = star, Kind = BreakthroughKind.StatBonus, HpPct = 8, DefPct = 8,
                    Description = "血量 / 防禦 +8%（尚未設計特殊效果）",
                });
            }
            _byHero[heroId] = list.OrderBy(e => e.Stars).ToList();
        }

        /// <summary>沒登錄的武將使用純屬性模板。</summary>
        public void RegisterStatOnly(string heroId) => Register(heroId);

        public IReadOnlyList<BreakthroughEffect> Get(string heroId) =>
            _byHero.TryGetValue(heroId, out var list) ? list : new List<BreakthroughEffect>();

        /// <summary>目前星級已解鎖的效果。</summary>
        public IEnumerable<BreakthroughEffect> Unlocked(string heroId, int stars) =>
            Get(heroId).Where(e => e.Stars <= stars);

        /// <summary>
        /// 依星級解出實際套牌：強化版取代原卡（同 id 的每一張都換），新增牌附在最後。
        /// 不修改原 <see cref="HeroDef"/>。
        /// </summary>
        public List<CardDef> ResolveDeck(HeroDef hero, int stars)
        {
            var deck = new List<CardDef>(hero.Deck);
            foreach (var e in Unlocked(hero.Id, stars))
            {
                if (e.NewCard == null) continue;
                if (e.Kind == BreakthroughKind.UpgradeCard)
                {
                    for (int i = 0; i < deck.Count; i++)
                        if (deck[i].Id == e.TargetCardId) deck[i] = e.NewCard;
                }
                else if (e.Kind == BreakthroughKind.AddCard)
                {
                    deck.Add(e.NewCard);
                }
            }
            return deck;
        }

        /// <summary>目前星級累計的屬性加成百分比（血量 / 攻擊 / 防禦）。</summary>
        public (int Hp, int Atk, int Def) StatBonusPct(string heroId, int stars)
        {
            var list = Unlocked(heroId, stars).Where(e => e.Kind == BreakthroughKind.StatBonus).ToList();
            return (list.Sum(e => e.HpPct), list.Sum(e => e.AtkPct), list.Sum(e => e.DefPct));
        }

        /// <summary>目前星級已解鎖的被動 id。</summary>
        public List<string> ActivePassives(string heroId, int stars) =>
            Unlocked(heroId, stars).Where(e => e.Kind == BreakthroughKind.Passive).Select(e => e.PassiveId).ToList();
    }

    /// <summary>Demo 武將的突破效果（佔位示範；每名武將只設計 2★、4★ 兩個特殊效果，其餘為屬性）。</summary>
    public static class DemoBreakthroughs
    {
        public static BreakthroughTable Create()
        {
            var table = new BreakthroughTable();
            table.Register("zhangfei",
                new BreakthroughEffect
                {
                    Stars = 2, Kind = BreakthroughKind.UpgradeCard, TargetCardId = "zf_taunt",
                    Description = "燕人怒吼 → 燕人怒吼・威：護甲提高並抽 1 張牌",
                    NewCard = new CardDef
                    {
                        Id = "zf_taunt_1", Name = "燕人怒吼・威", Cost = 1, Target = TargetRule.Self,
                        Keywords = CardKeywords.Innate,
                        Effects =
                        {
                            new EffectDef { Type = EffectType.ApplyStatus, Status = StatusType.Taunt, Amount = 3, OnSelf = true },
                            new EffectDef { Type = EffectType.Armor, Multiplier = 4.5, OnSelf = true },
                            new EffectDef { Type = EffectType.Draw, Amount = 1, OnSelf = true },
                        },
                    },
                },
                new BreakthroughEffect
                {
                    Stars = 4, Kind = BreakthroughKind.UpgradeCard, TargetCardId = "zf_roar",
                    Description = "當陽橋喝斷 → 萬夫莫敵：範圍擴大為全場",
                    NewCard = new CardDef
                    {
                        Id = "zf_roar_1", Name = "當陽橋喝斷・萬夫莫敵", Cost = 3, Target = TargetRule.EnemyFront,
                        Shape = Shape.All, Keywords = CardKeywords.Innate | CardKeywords.Retain,
                        Effects =
                        {
                            new EffectDef { Type = EffectType.Damage, Multiplier = 1.4 },
                            new EffectDef { Type = EffectType.StunGauge, Amount = 60 },
                        },
                    },
                });
            return table;
        }
    }
}
