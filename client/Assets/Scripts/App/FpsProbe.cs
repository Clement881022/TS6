#nullable enable
using System;
using UnityEngine;

namespace SanGuo.Client
{
    public sealed class FpsProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sanguoFps") < 0) return;
            var go = new GameObject("FpsProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<FpsProbe>();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sanguoUncap") >= 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
            }
        }

        private float _sum, _max;
        private int _frames;
        private float _next;

        private void Update()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sanguoUncap") >= 0 && Application.targetFrameRate != -1) Application.targetFrameRate = -1;
            float dt = Time.unscaledDeltaTime;
            _sum += dt; _frames++;
            if (dt > _max) _max = dt;
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            string page = PageHost.Current != null && PageHost.Current.ActivePage != null ? PageHost.Current.ActivePage.GetType().Name : "-";
            Debug.Log($"[FPS] {page} avg={_sum / _frames * 1000f:F1}ms ({_frames / _sum:F0}fps) max={_max * 1000f:F1}ms");
            _sum = 0; _max = 0; _frames = 0;
        }
    }
}
