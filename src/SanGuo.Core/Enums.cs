using System;

namespace SanGuo.Core
{
    public enum Side { Player, Enemy }

    /// <summary>六個職業（GDD 03 §2）：坦克、戰士、遊俠、術士、軍師、醫者。</summary>
    public enum Role { Tank, Warrior, Ranger, Mage, Strategist, Healer }

    public enum Rarity { R, SR, UR }

    /// <summary>外觀 / 動畫分類（近戰、遠程），不影響規則；攻擊距離由 <see cref="Stats.Range"/> 決定。</summary>
    public enum AttackType { Melee, Ranged }

    /// <summary>敵人層級（GDD 04 §2.2）。</summary>
    public enum EnemyTier { Normal, Elite, Boss }

    /// <summary>傷害類型：物理吃攻擊與防禦、可爆擊；法術吃謀略，不受防禦影響、不爆擊（GDD 01 §4）。</summary>
    public enum DamageKind { Physical, Magical }

    /// <summary>卡牌如何選出中心目標；單體目標都受施放者攻擊範圍（格距）限制。</summary>
    public enum TargetRule
    {
        /// <summary>玩家點選範圍內的一格（範圍內必須有敵人，不能空揮；沒指定時自動挑範圍內最近的敵人）。</summary>
        Enemy,
        Self,
        /// <summary>友軍單體：玩家點選範圍內的友軍；沒指定時挑範圍內血量比例最低者（含自己）。</summary>
        Ally,
        /// <summary>我方全部存活武將。</summary>
        AllAllies,
        /// <summary>全場所有敵人，不受攻擊範圍限制。</summary>
        AllEnemies,
        /// <summary>移動牌：玩家點選一格可到達的空格（步數不超過移動力）。</summary>
        MoveDest,
    }

    /// <summary>以中心目標展開的範圍形狀（GDD 02 §2）。</summary>
    public enum Shape
    {
        Single,
        /// <summary>橫向 3 格（中心與左右各 1 格）。</summary>
        Row3,
        /// <summary>縱向 3 格（中心與上下各 1 格）。</summary>
        Column3,
        /// <summary>十字：中心加上下左右。</summary>
        Cross,
        All,
    }

    public enum EffectType { Damage, Heal, Shield, ApplyStatus, Draw, GainCost, Move }

    /// <summary>
    /// 狀態種類。Burn（燃燒層數）、ArmorBreak（破甲）、Taunt（嘲諷）為敵方減益；DefUp / AtkUp / IntUp / DodgeUp 為增益，可疊加。
    /// </summary>
    public enum StatusType { Burn, ArmorBreak, Taunt, DefUp, AtkUp, IntUp, DodgeUp }

    public enum BattleResult { Ongoing, Won, Lost }

    /// <summary>關卡目標類型（GDD 04 §1）。限時以 <see cref="BattleSetup.TurnLimit"/> 表示，可與任一目標並用。</summary>
    public enum Objective
    {
        /// <summary>全滅敵人。</summary>
        Annihilate,
        /// <summary>護送：保護目標存活 <see cref="BattleSetup.SurviveTurns"/> 回合即勝利，目標陣亡則失敗。</summary>
        Escort,
        /// <summary>守城：同護送，保護的是據點目標。</summary>
        Defend,
        /// <summary>擊殺指定目標（<see cref="EnemySlot.IsObjective"/>）即勝利。</summary>
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
        /// <summary>指定的目標 / 格子不在範圍內或不合法。</summary>
        OutOfRange,
        /// <summary>移動牌指定的武將不能移動（陣亡 / 四周沒有空格 / 不是我方）。</summary>
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
        /// <summary>敵人開始蓄力。</summary>
        EnemyCharge,
        /// <summary>蓄力被嘲諷打斷。</summary>
        EnemyChargeBreak,
        BattleEnd,
    }
}
