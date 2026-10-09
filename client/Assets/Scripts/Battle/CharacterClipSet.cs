#nullable enable
using UnityEngine;

namespace SanGuo.Client
{
    /// <summary>
    /// 公司骨架動畫角色的戰鬥動作對應（由 Editor 的 CompanyCharacterBaker 烘焙時填入）。
    /// CharacterView 發現模型身上有它，就改用 Playables 播放這些 clip，而不是轉動程式樞紐。
    /// </summary>
    public sealed class CharacterClipSet : MonoBehaviour
    {
        public string MotionProfile = "sword";
        public float HeadScale = 1f;
        // Extend the existing neck-to-head bone span; skin weights keep the neck continuous.
        public float NeckExtension;
        public int ProportionVersion;
        public AnimationClip? Idle;
        public AnimationClip? Attack;
        public AnimationClip? Cast;
        public AnimationClip? Hit;
        public AnimationClip? Die;
    }
}
