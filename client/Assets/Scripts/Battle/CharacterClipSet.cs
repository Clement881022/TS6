#nullable enable
using UnityEngine;

namespace SanGuo.Client
{
    public sealed class CharacterClipSet : MonoBehaviour
    {
        public string MotionProfile = "sword";
        public float HeadScale = 1f;
        public float NeckExtension;
        public int ProportionVersion;
        public AnimationClip? SourceIdle;
        public AnimationClip? Idle;
        public AnimationClip? Attack;
        public AnimationClip? Cast;
        public AnimationClip? Hit;
        public AnimationClip? Die;
    }
}
