#nullable enable
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SanGuo.Client
{
    /// <summary>
    /// 把 Resources/Characters/&lt;名稱&gt; 的 3D 角色畫到一張 RenderTexture，給 UI 當立繪用（背景透明）。
    /// 模型放在遠處的專用圖層、用自己的相機與燈光，不影響主場景；循環播放待機動作，Cheer() 播一次施法動作。
    /// 不需要時呼叫 Dispose。
    /// </summary>
    public sealed class ModelStage : MonoBehaviour
    {
        /// <summary>專用圖層（相機與燈光只看這層）。</summary>
        private const int StageLayer = 31;
        private static readonly Vector3 Origin = new Vector3(4000f, 0f, 4000f);

        public RenderTexture Texture { get; private set; } = null!;

        private Camera _camera = null!;
        private GameObject _model = null!;
        private PlayableGraph _graph;
        private AnimationClipPlayable _playable;
        private CharacterClipSet? _clips;
        private AnimationClip? _current;
        private bool _once;

        public static ModelStage? Create(string characterName, int width = 512, int height = 640)
        {
            var prefab = Resources.Load<GameObject>("Characters/" + characterName);
            if (prefab == null) return null;

            var go = new GameObject("ModelStage_" + characterName);
            var stage = go.AddComponent<ModelStage>();
            stage.Build(prefab, width, height);
            return stage;
        }

        private void Build(GameObject prefab, int width, int height)
        {
            transform.position = Origin;
            _model = Instantiate(prefab, Origin, Quaternion.identity, transform);
            SetLayer(_model, StageLayer);
            _clips = _model.GetComponent<CharacterClipSet>();

            // 以模型的外框決定相機距離與高度
            var bounds = new Bounds(Origin, Vector3.zero);
            bool first = true;
            foreach (var r in _model.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            float fov = 24f;
            float size = Mathf.Max(bounds.size.y, bounds.size.x * height / (float)width, 0.1f);
            float dist = size * 0.5f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 1.12f;

            Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "ModelStageRT" };
            Texture.Create();

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.position = bounds.center + new Vector3(0f, size * 0.02f, dist);
            camGo.transform.LookAt(bounds.center);
            _camera = camGo.AddComponent<Camera>();
            _camera.fieldOfView = fov;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.cullingMask = 1 << StageLayer;
            _camera.targetTexture = Texture;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = dist + size * 4f;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = 1 << StageLayer;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.97f, 0.92f);

            StartClip(_clips != null ? _clips.Idle : null, loop: true);
        }

        private static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        private void StartClip(AnimationClip? clip, bool loop)
        {
            if (clip == null) return;
            var animator = _model.GetComponentInChildren<Animator>();
            if (animator == null) return;
            if (!_graph.IsValid())
            {
                _graph = PlayableGraph.Create("ModelStage");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            }
            else if (_playable.IsValid()) _graph.DestroyPlayable(_playable);
            _playable = AnimationClipPlayable.Create(_graph, clip);
            var output = AnimationPlayableOutput.Create(_graph, "Anim", animator);
            output.SetSourcePlayable(_playable);
            _graph.Play();
            _current = clip;
            _once = !loop;
        }

        /// <summary>播一次歡呼 / 施法動作，結束後回到待機。</summary>
        public void Cheer()
        {
            if (_clips != null && _clips.Cast != null) StartClip(_clips.Cast, loop: false);
        }

        private void Update()
        {
            if (!_playable.IsValid() || _current == null) return;
            if (_playable.GetTime() >= _current.length - 0.02f)
            {
                if (_once && _clips != null && _clips.Idle != null) StartClip(_clips.Idle, loop: true);
                else _playable.SetTime(0);
            }
        }

        public void Dispose()
        {
            if (_graph.IsValid()) _graph.Destroy();
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }
}
