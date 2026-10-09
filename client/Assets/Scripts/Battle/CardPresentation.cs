#nullable enable
using System.Linq;
using SanGuo.Core;
namespace SanGuo.Client
{
    public static class CardPresentation
    {
        public static string EffectIcon(EffectDef effect)
        {
            switch (effect.Type)
            {
                case EffectType.Damage: return "damage";
                case EffectType.Heal: return "heal";
                case EffectType.Shield: return "armor";
                case EffectType.Draw: return "draw";
                case EffectType.GainCost: return "cost";
                case EffectType.Move: return "draw";
                default: return UiIcons.Status(effect.Status);
            }
        }
        public static string TypeIcon(CardDef card) => card.Target == TargetRule.MoveDest ? "draw" : card.Effects.Count == 0 ? "armor" : EffectIcon(card.Effects.FirstOrDefault(e => e.Type == EffectType.Damage) ?? card.Effects[0]);
        public static string Value(EffectDef effect)
        {
            switch (effect.Type)
            {
                case EffectType.Damage:
                case EffectType.Heal:
                case EffectType.Shield: return $"{effect.Multiplier * 100:0}%";
                case EffectType.Move: return "走位";
                case EffectType.ApplyStatus:
                    string power = effect.Status == StatusType.Taunt ? "" : effect.Status == StatusType.DefUp ? $"+{effect.Multiplier:0}" : effect.Status == StatusType.CritUp || effect.Status == StatusType.DodgeUp ? $"+{effect.Multiplier:0}%" : $"{effect.Multiplier*100:0}%";
                    return $"{power} · {effect.Amount}回合";
                default: return effect.Amount.ToString();
            }
        }
        public static string Label(EffectDef effect)
        {
            switch (effect.Type)
            {
                case EffectType.Damage: return effect.Kind == DamageKind.Magical ? "法術傷害" : "物理傷害";
                case EffectType.Heal: return "治療";
                case EffectType.Shield: return "護盾";
                case EffectType.Draw: return "抽牌";
                case EffectType.GainCost: return "費用";
                case EffectType.Move: return "移動";
                default: return CardText.StatusName(effect.Status);
            }
        }
    }
}
