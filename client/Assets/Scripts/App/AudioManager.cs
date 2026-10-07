#nullable enable
using System;
using UnityEngine;

namespace SanGuo.Client
{
    public enum Sfx { Click, Cast, Hit, Crit, Dodge, Heal, Armor, Death, Gacha, Win, Lose }

    public enum Bgm { None, Home, Battle }

    /// <summary>
    /// 最小音效系統：BGM 讀 Resources/Audio，音效用程式合成（不依賴音檔）。
    /// 第一次使用時自動建立常駐物件，不需要擺進場景。
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int Rate = 44100;
        private static AudioManager? _inst;
        private static AudioManager Inst
        {
            get
            {
                if (_inst != null) return _inst;
                var go = new GameObject("AudioManager");
                DontDestroyOnLoad(go);
                return _inst = go.AddComponent<AudioManager>();
            }
        }

        private AudioSource _bgm = null!;
        private AudioSource _sfx = null!;
        private readonly AudioClip?[] _clips = new AudioClip?[Enum.GetValues(typeof(Sfx)).Length];
        private Bgm _bgmNow;

        public static float BgmVolume { get; set; } = 0.35f;
        public static float SfxVolume { get; set; } = 0.8f;

        /// <summary>啟動時就建立（含 AudioListener）：場景裡沒有任何 AudioListener，Editor 會每幀印一次警告拖慢編輯器，玩家版也聽不到聲音。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => _ = Inst;

        private void Awake()
        {
            if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.loop = true;
            _sfx = gameObject.AddComponent<AudioSource>();
        }

        public static void PlaySfx(Sfx id) => Inst.DoPlay(id);

        public static void PlayBgm(Bgm bgm) => Inst.DoBgm(bgm);

        private void DoBgm(Bgm bgm)
        {
            if (bgm == _bgmNow) return;
            _bgmNow = bgm;
            _bgm.Stop();
            if (bgm == Bgm.None) return;
            var clip = Resources.Load<AudioClip>(bgm == Bgm.Battle ? "Audio/BgmBattle" : "Audio/BgmHome");
            if (clip == null) { Debug.LogWarning($"找不到 BGM：{bgm}"); return; }
            _bgm.clip = clip;
            _bgm.volume = BgmVolume;
            _bgm.Play();
        }

        private void DoPlay(Sfx id)
        {
            var clip = _clips[(int)id] ??= Build(id);
            _sfx.PlayOneShot(clip, SfxVolume);
        }

        // ---------- 合成 ----------

        private static AudioClip Build(Sfx id)
        {
            switch (id)
            {
                case Sfx.Click: return Make(id, 0.06f, (t, n) => Tone(t, 1300) * Env(t, 0.06f, 30));
                case Sfx.Cast: return Make(id, 0.22f, (t, n) => Noise(n) * 0.5f * Sin01(t / 0.22f) * 0.7f + Tone(t, 300 + 900 * t / 0.22f) * 0.3f * Env(t, 0.22f, 6));
                case Sfx.Hit: return Make(id, 0.14f, (t, n) => (Noise(n) * 0.6f + Tone(t, 140)) * Env(t, 0.14f, 22));
                case Sfx.Crit: return Make(id, 0.28f, (t, n) => (Noise(n) * 0.5f + Tone(t, 110) + Tone(t, 880) * 0.5f) * Env(t, 0.28f, 12));
                case Sfx.Dodge: return Make(id, 0.15f, (t, n) => Noise(n) * 0.35f * Sin01(t / 0.15f));
                case Sfx.Heal: return Make(id, 0.35f, (t, n) => Tone(t, t < 0.15f ? 660 : 880) * Env(t, 0.35f, 7) * 0.7f);
                case Sfx.Armor: return Make(id, 0.2f, (t, n) => (Tone(t, 820) + Tone(t, 1330) * 0.6f) * Env(t, 0.2f, 16) * 0.6f);
                case Sfx.Death: return Make(id, 0.4f, (t, n) => Tone(t, 320 - 220 * t / 0.4f) * Env(t, 0.4f, 6));
                case Sfx.Gacha: return Arp(id, new[] { 784f, 988f, 1175f, 1568f, 1976f }, 0.09f, 0.35f);
                case Sfx.Win: return Arp(id, new[] { 523f, 659f, 784f, 1047f }, 0.16f, 0.5f);
                default: return Arp(id, new[] { 392f, 330f, 262f, 196f }, 0.2f, 0.45f);
            }
        }

        private static AudioClip Arp(Sfx id, float[] notes, float step, float tail)
        {
            float len = step * notes.Length + tail;
            return Make(id, len, (t, n) =>
            {
                int i = Mathf.Min(notes.Length - 1, (int)(t / step));
                float local = t - i * step;
                return Tone(t, notes[i]) * Env(local, len - i * step, 5) * 0.6f;
            });
        }

        private static AudioClip Make(Sfx id, float seconds, Func<float, int, float> sample)
        {
            int count = (int)(seconds * Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(sample(i / (float)Rate, i) * 0.8f, -1f, 1f);
            var clip = AudioClip.Create("sfx_" + id, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Tone(float t, float hz) => Mathf.Sin(2f * Mathf.PI * hz * t);
        private static float Env(float t, float length, float decay) => Mathf.Exp(-decay * t / Mathf.Max(0.01f, length)) * Mathf.Min(1f, t * 400f);
        private static float Sin01(float x) => Mathf.Sin(Mathf.Clamp01(x) * Mathf.PI);

        // 決定性雜訊，避免每次合成結果不同
        private static float Noise(int n)
        {
            uint x = (uint)n * 747796405u + 2891336453u;
            x = ((x >> (int)((x >> 28) + 4)) ^ x) * 277803737u;
            x = (x >> 22) ^ x;
            return (x / (float)uint.MaxValue) * 2f - 1f;
        }
    }
}
