using System.Collections.Generic;
using System.Linq;

namespace SanGuo.Core.Meta
{
    public enum BreakthroughKind
    {
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
        /// <summary>顯示給玩家的說明。</summary>
        public string Description = "";
    }

    /// <summary>各武將的突破效果表（資料驅動，之後由 JSON 載入）。</summary>
    public sealed class BreakthroughTable
    {
        private readonly Dictionary<string, List<BreakthroughEffect>> _byHero =
            new Dictionary<string, List<BreakthroughEffect>>();

        public void Register(string heroId, params BreakthroughEffect[] effects)
        {
            _byHero[heroId] = effects.OrderBy(e => e.Stars).ToList();
        }

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

        /// <summary>目前星級已解鎖的被動 id。</summary>
        public List<string> ActivePassives(string heroId, int stars) =>
            Unlocked(heroId, stars).Where(e => e.Kind == BreakthroughKind.Passive).Select(e => e.PassiveId).ToList();
    }

    /// <summary>Demo 武將的突破效果（佔位示範，張飛先做完整五星，其餘待設計）。</summary>
    public static class DemoBreakthroughs
    {
        public static BreakthroughTable Create()
        {
            var table = new BreakthroughTable();
            table.Register("zhangfei",
                new BreakthroughEffect
                {
                    Stars = 1, Kind = BreakthroughKind.UpgradeCard, TargetCardId = "zf_break",
                    Description = "蛇矛破陣 → 蛇矛・裂甲：破甲提高到 60%",
                    NewCard = new CardDef
                    {
                        Id = "zf_break_1", Name = "蛇矛・裂甲", Cost = 1, Target = TargetRule.EnemyFront,
                        Effects =
                        {
                            new EffectDef { Type = EffectType.Damage, Multiplier = 0.8 },
                            new EffectDef { Type = EffectType.ApplyStatus, Status = StatusType.ArmorBreak, Multiplier = 0.6, Amount = 4 },
                        },
                    },
                },
                new BreakthroughEffect
                {
                    Stars = 2, Kind = BreakthroughKind.AddCard,
                    Description = "新增專屬牌「長坂死守」：挑釁並獲得大量護甲（破釜）",
                    NewCard = new CardDef
                    {
                        Id = "zf_hold", Name = "長坂死守", Cost = 2, Target = TargetRule.Self,
                        Keywords = CardKeywords.Exhaust,
                        Effects =
                        {
                            new EffectDef { Type = EffectType.ApplyStatus, Status = StatusType.Taunt, Amount = 2, OnSelf = true },
                            new EffectDef { Type = EffectType.Armor, Multiplier = 4.0, OnSelf = true },
                        },
                    },
                },
                new BreakthroughEffect
                {
                    Stars = 3, Kind = BreakthroughKind.UpgradeCard, TargetCardId = "zf_taunt",
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
                    Stars = 4, Kind = BreakthroughKind.Passive, PassiveId = "zf_taunt_guard",
                    Description = "被動：挑釁期間受到的傷害額外 -10%（戰鬥核心尚未實作）",
                },
                new BreakthroughEffect
                {
                    Stars = 5, Kind = BreakthroughKind.UpgradeCard, TargetCardId = "zf_roar",
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
