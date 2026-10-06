using System;

namespace SanGuo.Core
{
    public enum Side { Player, Enemy }

    public enum Role { Tank, Warrior, Mage, Archer, Healer, Strategist }

    /// <summary>稀有度（N 為素材，不上場）。</summary>
    public enum Rarity { R, SR, UR }

    public enum AttackType { Melee, Ranged }

    /// <summary>卡牌如何選出中心目標（見 docs/combat.md 4.1）。</summary>
    public enum TargetRule
    {
        /// <summary>施放者同路，敵方最前排。</summary>
        EnemyFront,
        /// <summary>施放者同路，敵方最後排優先（弓手 / 遠程）。</summary>
        EnemyBack,
        Self,
        AllyLowestHp,
        AllAllies,
        AllEnemies,
    }

    /// <summary>以中心目標展開的範圍形狀。</summary>
    public enum Shape { Single, Row, Column, Cross, All }

    public enum EffectType { Damage, Heal, Armor, ApplyStatus, Draw, GainCost }

    public enum StatusType { Burn, Poison, Stun, ArmorBreak, Taunt }

    /// <summary>破釜 = Exhaust、蓄勢 = Retain、先登 = Innate。</summary>
    [Flags]
    public enum CardKeywords
    {
        None = 0,
        Exhaust = 1,
        Retain = 2,
        Innate = 4,
    }

    public enum BattleResult { Ongoing, Won, Lost }

    public enum PlayResult
    {
        Ok,
        BattleOver,
        NotInHand,
        NotEnoughCost,
        OwnerDead,
        Stunned,
        NoTarget,
        InvalidMove,
        /// <summary>本回合移動次數已用完。</summary>
        MoveUsed,
    }

    public enum EventType
    {
        TurnStart,
        CardPlayed,
        Damage,
        Dodge,
        Heal,
        Armor,
        StatusApplied,
        Draw,
        GainCost,
        Move,
        Death,
        EnemyAttack,
        EnemyMove,
        EnemySkip,
        BattleEnd,
    }
}
