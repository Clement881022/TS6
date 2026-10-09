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
    public enum StatusType { Burn, ArmorBreak, Taunt, DefUp, AtkUp, IntUp, DodgeUp, CritUp }

    /// <summary>
    /// 武將被動（5★ 解鎖，UR 與劇情劉關張各一個；見 docs/signature-cards-batch1.md）。
    /// 規則寫在 <see cref="Battle"/> 的觸發點（回合開始／結束、擊敗、受傷、治療、爆擊），名稱與說明見 <see cref="Passives"/>。
    /// </summary>
    public enum PassiveKind
    {
        None,
        /// <summary>燕人．張飛「萬人敵」：我方回合開始時，若有敵人處於嘲諷狀態，自身防禦 +30（1 回合）。</summary>
        WanRenDi,
        /// <summary>夏侯惇「剛烈不屈」：生命首次低於 50% 時，自身防禦 +80（3 回合）。</summary>
        GangLie,
        /// <summary>呂布「人中呂布」：擊敗敵人後，自身爆擊率 +30%（2 回合）。</summary>
        RenZhongLvBu,
        /// <summary>荀彧「居中持重」：從第 2 回合起，每回合多抽 1 張。</summary>
        JuZhong,
        /// <summary>公孫瓚「白馬將軍」：每回合第一次擊敗敵人時回 1 費。</summary>
        BaiMa,
        /// <summary>張角「太平道」：燃燒中的敵人被擊敗時，剩餘燃燒層數的一半轉移給每名相鄰的敵人。</summary>
        TaiPing,
        /// <summary>武聖．關羽「威震華夏」：攻擊擊敗敵人時，對其相鄰的敵人造成該次傷害 50% 的濺射。</summary>
        WeiZhen,
        /// <summary>華佗「五禽戲」：我方回合結束時，治療生命比例最低的友軍（謀略 ×0.3）。</summary>
        WuQin,
        /// <summary>劉備「仁君」：劉備存活時，我方受到的治療 +15%。</summary>
        RenJun,
        /// <summary>關羽「美髯公」：爆擊率 +15%；爆擊時無視目標 30% 防禦。</summary>
        MeiRan,
        /// <summary>張飛「長坂斷後」：自身施放的嘲諷仍在任一敵人身上時，受到的傷害 −15%。</summary>
        ChangBan,
    }

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
        /// <summary>Boss 進入第二階段（Value = 新階段）。</summary>
        EnemyPhase,
        BattleEnd,
        /// <summary>被動觸發（Text = 被動名稱），供表現層顯示。</summary>
        PassiveTriggered,
    }
}
