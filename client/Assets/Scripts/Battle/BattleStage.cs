#nullable enable
using System;
using System.Collections.Generic;
using SanGuo.Core;
using UnityEngine;
using UnityEngine.UIElements;
using Position = SanGuo.Core.Position;

namespace SanGuo.Client
{
    public enum TileState { None, Target, Reach, Owner }

    /// <summary>掛在每塊地磚上，記錄它對應的棋盤格（點擊選格用）。</summary>
    public sealed class TileTag : MonoBehaviour
    {
        public Side Side;
        public Position Pos;
        public Renderer Renderer = null!;
        public Color BaseColor;
    }

    /// <summary>
    /// 3D 戰場：45 度俯視的正交攝影機、地磚、角色模型。
    /// UI（UI Toolkit）畫在 3D 之上；血條等資訊由 BattleScreen 依 WorldToPanel 貼在角色頭上。
    /// </summary>
    public sealed class BattleStage : MonoBehaviour
    {
        public const float CameraPitchDegrees = 50f;   // 俯視角（等角視角約 45–55）
        public const float BoardLeftBias = 0.05f;
        public const float CameraYawDegrees = 45f;     // 左右旋轉 45° = 等角視角，地磚變菱形（0 = 正面平視棋盤）
        private const float TilePitch = 1.25f;     // 路與路之間
        private const float TilePitchX = 1.25f;    // 排與排之間
        private const float CenterGap = 0.8f;
        private const float TileTop = 0.08f;
        private const float ModelScale = 0.85f;
        private const float UnitHeadHeight = 1.2f;
        private const float ViewYawDegrees = 38f;      // 面向對手的同時微微轉向鏡頭

        private sealed class UnitView
        {
            public GameObject Anchor = null!;
            public CharacterView View = null!;
            public bool Placed;
            public Vector3 Facing;
        }

        private Camera _camera = null!;
        private Battle? _battle;
        private VisualElement? _panelRoot;
        private VisualElement? _field;
        private readonly Dictionary<int, UnitView> _views = new Dictionary<int, UnitView>();
        private readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        private readonly Dictionary<(Side, int, int), TileTag> _tiles = new Dictionary<(Side, int, int), TileTag>();
        private GameObject? _tileRoot;

        private void Awake()
        {
            _camera = Camera.main != null ? Camera.main : new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(24f / 255f, 26f / 255f, 34f / 255f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 80f;

            if (FindFirstObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Key Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.color = new Color(1f, 0.97f, 0.92f);
                lightGo.transform.rotation = Quaternion.Euler(52f, -25f, 0f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.56f, 0.56f, 0.62f);
        }

        // ------------------------------------------------------------ 綁定戰鬥

        public void Bind(Battle battle, VisualElement panelRoot, VisualElement field)
        {
            foreach (var kv in _views) Destroy(kv.Value.Anchor);
            _views.Clear();
            if (_tileRoot != null) Destroy(_tileRoot);
            _tiles.Clear();

            _battle = battle;
            _panelRoot = panelRoot;
            _field = field;
            BuildTiles();
        }

        public CharacterView? ViewOf(int unitId) => _views.TryGetValue(unitId, out var v) ? v.View : null;

        private void BuildTiles()
        {
            _tileRoot = new GameObject("Tiles");
            _tileRoot.transform.SetParent(transform, false);
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                var side = sideIndex == 0 ? Side.Player : Side.Enemy;
                for (int lane = 0; lane < _battle!.Setup.Lanes; lane++)
                {
                    for (int row = 0; row < _battle.Setup.Rows; row++)
                    {
                        var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        tile.name = $"Tile_{side}_{lane}_{row}";
                        tile.transform.SetParent(_tileRoot.transform, false);
                        var pos = new Position(lane, row);
                        var center = TileWorld(side, pos);
                        tile.transform.position = center + Vector3.down * (TileTop * 0.5f);
                        tile.transform.localScale = new Vector3(TilePitchX * 0.9f, TileTop, TilePitch * 0.88f);

                        var tag = tile.AddComponent<TileTag>();
                        tag.Side = side;
                        tag.Pos = pos;
                        tag.Renderer = tile.GetComponent<Renderer>();
                        if (TileShader != null) tag.Renderer.sharedMaterial = new Material(TileShader);
                        bool alt = (lane + row) % 2 == 0;
                        tag.BaseColor = side == Side.Player
                            ? (alt ? new Color(0.20f, 0.29f, 0.42f) : new Color(0.17f, 0.25f, 0.37f))
                            : (alt ? new Color(0.42f, 0.22f, 0.24f) : new Color(0.36f, 0.19f, 0.21f));
                        tag.Renderer.material.color = tag.BaseColor;
                        _tiles[(side, lane, row)] = tag;
                    }
                }
            }
        }

        /// <summary>棋盤格的世界座標（地磚頂面中心）。雙方左右相對，前排靠近中線；第 1 路在遠端。</summary>
        public Vector3 TileWorld(Side side, Position pos)
        {
            int lanes = _battle != null ? _battle.Setup.Lanes : 5;
            float x = CenterGap * 0.5f + 0.5f * TilePitchX + pos.Row * TilePitchX;
            if (side == Side.Player) x = -x;
            float z = ((lanes - 1) * 0.5f - pos.Lane) * TilePitch;
            return new Vector3(x, 0f, z);
        }

        public void SetTileStates(IEnumerable<(Side side, Position pos, TileState state)> states)
        {
            foreach (var tag in _tiles.Values) tag.Renderer.material.color = tag.BaseColor;
            foreach (var (side, pos, state) in states)
            {
                if (!_tiles.TryGetValue((side, pos.Lane, pos.Row), out var tag)) continue;
                switch (state)
                {
                    case TileState.Target: tag.Renderer.material.color = new Color(0.95f, 0.78f, 0.2f); break;
                    case TileState.Reach: tag.Renderer.material.color = new Color(0.25f, 0.75f, 0.4f); break;
                    case TileState.Owner: tag.Renderer.material.color = new Color(0.9f, 0.9f, 0.95f); break;
                }
            }
        }

        // ------------------------------------------------------------ 座標轉換

        public Vector2 WorldToPanel(Vector3 world)
        {
            var sp = _camera.WorldToScreenPoint(world);
            float w = _panelRoot != null ? _panelRoot.worldBound.width : Screen.width;
            float h = _panelRoot != null ? _panelRoot.worldBound.height : Screen.height;
            return new Vector2(sp.x / Screen.width * w, (Screen.height - sp.y) / Screen.height * h);
        }

        /// <summary>某個棋盤格上方（約角色頭頂）的面板座標，飄字用。</summary>
        public Vector2 TileHeadPanel(Side side, Position pos) =>
            WorldToPanel(TileWorld(side, pos) + Vector3.up * UnitHeadHeight);

        /// <summary>角色目前（含移動動畫）頭頂的面板座標，血條用；找不到回傳 null。</summary>
        public Vector2? UnitHeadPanel(Unit unit)
        {
            if (!_views.TryGetValue(unit.Id, out var uv) || !uv.Anchor.activeSelf) return null;
            return WorldToPanel(uv.Anchor.transform.position + Vector3.up * UnitHeadHeight);
        }

        /// <summary>點擊選格：面板座標 → 射線打到的地磚。</summary>
        public bool TryPick(Vector2 panelPoint, out Side side, out Position pos)
        {
            side = Side.Player;
            pos = default;
            if (_panelRoot == null) return false;
            float w = _panelRoot.worldBound.width, h = _panelRoot.worldBound.height;
            var screen = new Vector3(panelPoint.x / w * Screen.width, Screen.height - panelPoint.y / h * Screen.height, 0f);
            var ray = _camera.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out var hit, 200f)) return false;
            var tag = hit.collider.GetComponent<TileTag>();
            if (tag == null) return false;
            side = tag.Side;
            pos = tag.Pos;
            return true;
        }

        // ------------------------------------------------------------ 模型

        // URP 專案裡 CreatePrimitive 的預設材質是 Built-in Standard（會變粉紅），地磚改用 URP/Lit。
        private static Shader? TileShader => Shader.Find("Universal Render Pipeline/Lit");

        private UnitView? Ensure(Unit unit)
        {
            if (_views.TryGetValue(unit.Id, out var existing)) return existing;
            if (!_prefabs.TryGetValue(unit.DefId, out var prefab))
            {
                prefab = Resources.Load<GameObject>("Characters/" + unit.DefId);
                _prefabs[unit.DefId] = prefab;
                if (prefab == null) Debug.LogWarning("找不到角色模型：Characters/" + unit.DefId);
            }
            if (prefab == null) return null;

            var anchor = new GameObject("Unit_" + unit.Name);
            anchor.transform.SetParent(transform, false);
            anchor.transform.localScale = Vector3.one * ModelScale;
            var model = Instantiate(prefab, anchor.transform);
            var view = model.AddComponent<CharacterView>();
            view.Init(model.transform);

            float dir = unit.Side == Side.Player ? 1f : -1f;
            float yaw = Mathf.Deg2Rad * ViewYawDegrees;
            var uv = new UnitView
            {
                Anchor = anchor,
                View = view,
                Facing = new Vector3(Mathf.Cos(yaw) * dir, 0f, -Mathf.Sin(yaw)).normalized,
            };
            _views[unit.Id] = uv;
            return uv;
        }

        // ------------------------------------------------------------ 每幀

        private void LateUpdate()
        {
            if (_battle == null || _panelRoot == null || _field == null) return;
            FitCamera();

            foreach (var unit in _battle.Units)
            {
                var uv = Ensure(unit);
                if (uv == null) continue;
                if (uv.View.Finished) { uv.Anchor.SetActive(false); continue; }

                var target = TileWorld(unit.Side, unit.Pos);
                if (!uv.Placed)
                {
                    uv.Anchor.transform.position = target;
                    uv.Placed = true;
                }
                else
                {
                    uv.Anchor.transform.position = Vector3.Lerp(uv.Anchor.transform.position, target,
                        1f - Mathf.Exp(-14f * Time.deltaTime));
                }
                uv.View.Tick(Time.deltaTime, uv.Facing, 1f, uv.Anchor.transform.position);
            }
        }

        /// <summary>把棋盤整個框進 UI 中間那塊（field）的範圍：以攝影機空間的包圍盒算出縮放與平移。</summary>
        private void FitCamera()
        {
            var pb = _panelRoot!.worldBound;
            var fb = _field!.worldBound;
            if (pb.width <= 1f || pb.height <= 1f || fb.width <= 1f || fb.height <= 1f) return;

            float sx = Screen.width / pb.width, sy = Screen.height / pb.height;
            float fieldW = fb.width * sx, fieldH = fb.height * sy;
            var fieldCenter = new Vector2((fb.x + fb.width * 0.5f) * sx, Screen.height - (fb.y + fb.height * 0.5f) * sy);
            fieldCenter.x -= fieldW * BoardLeftBias;   // 棋盤略往左，右下角留給手牌與按鈕

            var rot = Quaternion.Euler(CameraPitchDegrees, CameraYawDegrees, 0f);
            var inv = Quaternion.Inverse(rot);

            // 所有地磚的四角（地面與頭頂高度）投影到攝影機空間，求包圍盒。
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var key in _tiles.Keys)
            {
                var center = TileWorld(key.Item1, new Position(key.Item2, key.Item3));
                for (int cx = -1; cx <= 1; cx += 2)
                {
                    for (int cz = -1; cz <= 1; cz += 2)
                    {
                        for (int h = 0; h < 2; h++)
                        {
                            var p = center + new Vector3(cx * TilePitchX * 0.5f, h * UnitHeadHeight, cz * TilePitch * 0.5f);
                            var c = inv * p;
                            minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                            minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
                        }
                    }
                }
            }
            float needW = maxX - minX + 0.4f;
            float needH = maxY - minY + 0.3f;
            float worldPerPx = Mathf.Max(needH / fieldH, needW / fieldW);
            _camera.orthographicSize = worldPerPx * Screen.height * 0.5f;

            // 包圍盒中心要落在 field 中心：在攝影機空間反向平移。
            float dx = (fieldCenter.x - Screen.width * 0.5f) * worldPerPx;
            float dy = (fieldCenter.y - Screen.height * 0.5f) * worldPerPx;
            var camLocal = new Vector3((minX + maxX) * 0.5f - dx, (minY + maxY) * 0.5f - dy, -40f);
            _camera.transform.rotation = rot;
            _camera.transform.position = rot * camLocal;
        }
    }
}
