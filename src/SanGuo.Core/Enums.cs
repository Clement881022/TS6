using System;

namespace SanGuo.Core
{
    public enum Side { Player, Enemy }

    public enum Role { Tank, Warrior, Mage, Archer, Healer, Strategist }

    /// <summary>稀有度（N 為素材，不上場）。</summary>
    public enum Rarity { R, SR, UR }

    public enum AttackType { Melee, Ranged }

    /// <summary>敵人的特殊行動（其餘為普通攻擊）。Healer = 每回合治療血量比例最低的友軍；Charger = 蓄力一回合、下回合放大招（被昏亂會打斷）。</summary>
    [Flags]
    public enum EnemyAbility { None = 0, Healer = 1, Charger = 2, Summoner = 4 }

    /// <summary>卡牌如何選出中心目標（見 docs/combat.md 4.1）；單體目標都受卡牌 <see cref="CardDef.Range"/>（格距）限制。</summary>
    public enum TargetRule
    {
        /// <summary>玩家點選範圍內的一名敵人（沒指定時自動挑範圍內最近者）。</summary>
        Enemy,
        Self,
        /// <summary>範圍內血量比例最低的友軍（含自己）。</summary>
        AllyLowestHp,
        AllAllies,
        AllEnemies,
        /// <summary>範圍內血量最低的敵人（自動選取）。</summary>
        EnemyLowestHp,
        /// <summary>移動卡：玩家點選一格可到達的空格（步數不超過移動力）。</summary>
        MoveDest,
    }

    /// <summary>以中心目標展開的範圍形狀。</summary>
    public enum Shape { Single, Row, Column, Cross, All }

    public enum EffectType { Damage, Heal, Armor, ApplyStatus, Draw, GainCost, StunGauge, Detonate, Move }

    /// <summary>
    /// DefUp / AtkUp / CritUp 為增益：<see cref="EffectDef.Multiplier"/> 是加成比例（DefUp / AtkUp 的 0.3 = +30%；CritUp 的 0.25 = +25 個百分點爆擊率），
    /// 同種增益重複施加時取較大的加成與較長的回合數，不疊加。
    /// </summary>
    public enum StatusType { Burn, Poison, Stun, ArmorBreak, Taunt, DefUp, AtkUp, CritUp }

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
        /// <summary>指定的目標 / 格子不在範圍內或不合法。</summary>
        OutOfRange,
        /// <summary>移動卡指定的武將不能移動（陣亡 / 昏亂 / 四周沒有空格 / 不是我方）。</summary>
        InvalidMover,
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
        /// <summary>敵人開始蓄力（意圖預告下回合大招）。</summary>
        EnemyCharge,
        /// <summary>敵人召喚了新單位（Source = 召喚者、Target = 新單位）。</summary>
        EnemySummon,
        /// <summary>昏亂條變動：Value = 目前值，Text = 上限。</summary>
        StunGauge,
        BattleEnd,
    }
}
