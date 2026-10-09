using System;

namespace SanGuo.Core
{
    public enum Side { Player, Enemy }

    public enum Role { Tank, Warrior, Ranger, Mage, Strategist, Healer }

    public enum Rarity { R, SR, UR }

    public enum AttackType { Melee, Ranged }

    public enum EnemyTier { Normal, Elite, Boss }

    public enum DamageKind { Physical, Magical }

    public enum TargetRule
    {
        Enemy,
        Self,
        Ally,
        AllAllies,
        AllEnemies,
        MoveDest,
    }

    public enum Shape
    {
        Single,
        Row3,
        Column3,
        Cross,
        All,
    }

    public enum EffectType { Damage, Heal, Shield, ApplyStatus, Draw, GainCost, Move }

    public enum StatusType { Burn, ArmorBreak, Taunt, DefUp, AtkUp, IntUp, DodgeUp, CritUp }

    public enum PassiveKind
    {
        None,
        WanRenDi,
        GangLie,
        RenZhongLvBu,
        JuZhong,
        BaiMa,
        TaiPing,
        WeiZhen,
        WuQin,
        RenJun,
        MeiRan,
        ChangBan,
    }

    public enum BattleResult { Ongoing, Won, Lost }

    public enum Objective
    {
        Annihilate,
        Escort,
        Defend,
        KillTarget,
    }

    public enum PlayResult
    {
        Ok,
        BattleOver,
        NotInHand,
        NotEnoughCost,
        OwnerDead,
        NoTarget,
        OutOfRange,
        InvalidMover,
    }

    public enum EventType
    {
        TurnStart,
        CardPlayed,
        Damage,
        Dodge,
        Heal,
        Shield,
        StatusApplied,
        Draw,
        GainCost,
        Move,
        Death,
        EnemyAttack,
        EnemyMove,
        EnemyCharge,
        EnemyChargeBreak,
        EnemyPhase,
        BattleEnd,
        PassiveTriggered,
    }
}
