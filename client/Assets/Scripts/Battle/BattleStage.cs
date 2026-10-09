#nullable enable
using System;
using System.Collections.Generic;
using SanGuo.Core;
using UnityEngine;
using UnityEngine.UIElements;
using Position = SanGuo.Core.Position;

namespace SanGuo.Client
{
    public enum TileState { None, Target, Reach, Range, Owner }

    /// <summary>掛在每塊地磚上，記錄它對應的棋盤格（點擊選格用）。</summary>
    public sealed class TileTag : MonoBehaviour
    {
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
        public const float CameraPitchDegrees = 36f;   // 俯視角（等角視角約 45–55）
        public const float BoardLeftBias = 0f;
        private const float BoardZoom = 1.0f;         // 棋盤完整放進 field（手牌區不再蓋住棋盤）
        public const float CameraYawDegrees = 60f;     // 棋盤繞 Y 軸轉 30°（原本 90° = 軸對齊；轉向相反就改成 120）
        private const float TilePitch = 1.85f;      // 欄與欄之間（螢幕上下方向；參考 TS6Client 角色間距 1.5）
        private const float TilePitchX = 1.55f;     // 列與列之間（螢幕左右方向）
        private const float TileTop = 0.03f;
        private const float ModelScale = 0.90f;
        private const float UnitHeadHeight = 2.4f;     // 模型縮小後的頭頂高度（ModelScale 1.5 時為 3.1）
        private const float TagRoomAbove = 0.5f;       // 頭頂血量標籤的預留高度（世界單位），避免被切到畫面外
        private const float TagRoomBelow = 0.35f;       // 我方標籤在腳下
        private const float ViewYawDegrees = 65f;      // 面向對手的同時微微轉向鏡頭

        private sealed class UnitView
        {
            public GameObject Anchor = null!;
            public CharacterView View = null!;
            public Transform? LeftFoot, RightFoot;
            public bool Placed;
            public Vector3 Facing;
        }

        // 視角：滾輪縮放、拖曳平移（BattleScreen 轉送輸入）。預設比「剛好塞進戰場區」再近一些，讓棋盤與角色更大。
        private const float DefaultZoom = 1.3f, MinZoom = 0.7f, MaxZoom = 3.2f;
        private float _zoom = DefaultZoom;
        private Vector2 _panScreenPx;

        public void ZoomBy(float factor) => _zoom = Mathf.Clamp(_zoom * factor, MinZoom, MaxZoom);

        /// <summary>拖曳平移（面板座標的位移量；往右拖 = 棋盤往右）。</summary>
        public void PanBy(Vector2 panelDelta)
        {
            float k = _panelRoot != null && _panelRoot.worldBound.width > 1f ? Screen.width / _panelRoot.worldBound.width : 1f;
            _panScreenPx += panelDelta * k;
            _panScreenPx = new Vector2(Mathf.Clamp(_panScreenPx.x, -Screen.width, Screen.width), Mathf.Clamp(_panScreenPx.y, -Screen.height, Screen.height));
        }

        public void ResetView() { _zoom = DefaultZoom; _panScreenPx = Vector2.zero; }

        private Camera _camera = null!;
        private Battle? _battle;
        private VisualElement? _panelRoot;
        private VisualElement? _field;
        private readonly Dictionary<int, UnitView> _views = new Dictionary<int, UnitView>();
        private readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        private readonly Dictionary<(int, int), TileTag> _tiles = new Dictionary<(int, int), TileTag>();
        private GameObject? _tileRoot;
        private readonly List<UnityEngine.Object> _boardResources = new List<UnityEngine.Object>();
        private void ReleaseBoardResources()
        {
            foreach (var resource in _boardResources) if (resource != null) Destroy(resource);
            _boardResources.Clear();
            foreach (var tile in _tiles.Values) if (tile.Renderer != null) Destroy(tile.Renderer.sharedMaterial);
        }
        private void OnDestroy() => ReleaseBoardResources();

        private void Awake()
        {
            _camera = Camera.main != null ? Camera.main : new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(24f / 255f, 26f / 255f, 34f / 255f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 80f;

            // 劇情 / 主城的 3D 立繪（ModelStage）自帶燈光，但它們隨時會被銷毀：戰場只認不屬於立繪的燈，沒有就自己補一盞。
            bool hasKeyLight = false;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.GetComponentInParent<ModelStage>() == null) { hasKeyLight = true; break; }
            if (!hasKeyLight)
            {
                var lightGo = new GameObject("Key Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = .28f;
                light.color = new Color(1f, 0.97f, 0.92f);
                lightGo.transform.rotation = Quaternion.Euler(52f, -25f, 0f);
            }
            CreateBackdrop();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.64f, 0.62f, 0.56f);
        }

        // ------------------------------------------------------------ 綁定戰鬥

        private Transform? _backdrop;
        private const float BackdropDistance = 60f;

        /// <summary>戰場背景：貼圖貼在攝影機正後方的一張 Quad，跟著鏡頭縮放、永遠填滿畫面（Resources/UiBg/battle）。</summary>
        private void CreateBackdrop()
        {
            var tex = Resources.Load<Texture2D>("ChibiSkin/battle");
            var shader = Resources.Load<Shader>("Shaders/Backdrop");
            if (tex == null || shader == null) return;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Backdrop";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(_camera.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, BackdropDistance);
            quad.transform.localRotation = Quaternion.identity;
            var mat = new Material(shader) { mainTexture = tex, color = Color.white };
            quad.GetComponent<Renderer>().sharedMaterial = mat;
            _backdrop = quad.transform;
        }

        private void UpdateBackdrop()
        {
            if (_backdrop == null) return;
            float h = _camera.orthographicSize * 2f;
            float w = h * _camera.aspect;
            // 貼圖是 16:9：以「填滿」的方式放大（寬或高其中一邊剛好、另一邊裁掉）
            float imageAspect = 16f / 9f;
            if (_camera.aspect > imageAspect) h = w / imageAspect; else w = h * imageAspect;
            _backdrop.localScale = new Vector3(w, h, 1f);
        }

        public void Bind(Battle battle, VisualElement panelRoot, VisualElement field)
        {
            foreach (var kv in _views) Destroy(kv.Value.Anchor);
            _views.Clear();
            if (_tileRoot != null) Destroy(_tileRoot);
            ReleaseBoardResources();
            _tiles.Clear();

            ResetView();
            _battle = battle;
            _panelRoot = panelRoot;
            _field = field;
            BuildTiles();
        }

        public CharacterView? ViewOf(int unitId) => _views.TryGetValue(unitId, out var v) ? v.View : null;

        /// <summary>敵我共用的 5x5 棋盤，全部是中立格；底下鋪一塊石板地板與木框，做成戰鬥場地。</summary>
        private void BuildTiles()
        {
            _tileRoot = new GameObject("Tiles");
            _tileRoot.transform.SetParent(transform, false);
            BuildFloor(_tileRoot.transform);
            for (int lane = 0; lane < _battle!.Setup.Lanes; lane++)
            {
                for (int row = 0; row < _battle.Setup.Rows; row++)
                {
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = $"Tile_{lane}_{row}";
                    tile.transform.SetParent(_tileRoot.transform, false);
                    var pos = new Position(lane, row);
                    var center = TileWorld(pos);
                    tile.transform.position = center + Vector3.up * (0.006f - TileTop * 0.5f);
                    tile.transform.localScale = new Vector3(TilePitchX * 0.94f, TileTop, TilePitch * 0.92f);

                    var tag = tile.AddComponent<TileTag>();
                    tag.Pos = pos;
                    tag.Renderer = tile.GetComponent<Renderer>();
                    if (TileShader != null) tag.Renderer.sharedMaterial = new Material(TileShader);
                    // 共用棋盤沒有敵我領土：所有格子都是中立色（只有技能預覽才會上色）。
                    tag.BaseColor = new Color(0.82f, 0.78f, 0.60f, 0f);
                    tag.Renderer.material.SetFloat("_Border", 0.024f);
                    tag.Renderer.material.color = tag.BaseColor;
                    _tiles[(lane, row)] = tag;
                }
            }
        }

        private static Shader? FloorShader => Resources.Load<Shader>("Shaders/BattleFloor");

        private void BuildFloor(Transform parent)
        {
            int lanes = _battle!.Setup.Lanes, rows = _battle.Setup.Rows;
            if(lanes==5 && rows==5)
            {
                var craftedBoard=Resources.Load<GameObject>("ProductionBoard/CourtyardBoard_5x5");
                if(craftedBoard!=null)
                {
                    Instantiate(craftedBoard,parent,false);
                    return;
                }
            }
            float w = rows * TilePitchX, d = lanes * TilePitch;
            var jade = new Color(.17f, .28f, .27f);
            var bronze = new Color(.72f, .51f, .23f);
            Solid(parent, "Jade foundation", Vector3.down * .24f, new Vector3(w + .64f, .30f, d + .64f), jade, .08f);
            Solid(parent, "Bronze reveal", Vector3.down * .09f, new Vector3(w + .42f, .06f, d + .42f), bronze, .025f);
            Solid(parent, "Recessed grout", Vector3.down * .045f, new Vector3(w + .28f, .04f, d + .28f), jade, .025f);
            for (int lane = 0; lane < lanes; lane++)
            for (int row = 0; row < rows; row++)
            {
                var stone = (lane + row) % 2 == 0 ? new Color(.89f, .86f, .75f) : new Color(.78f, .83f, .77f);
                Solid(parent, $"Carved stone {lane}-{row}", TileWorld(new Position(lane, row)) + Vector3.down * .045f,
                    new Vector3(TilePitchX * .97f, .10f, TilePitch * .97f), stone, .035f);
            }
            foreach (int sign in new[] { -1, 1 })
            {
                Solid(parent, "Bronze edge", new Vector3(sign * (w / 2f + .20f), -.015f, 0), new Vector3(.08f, .08f, d + .46f), bronze, .018f);
                Solid(parent, "Bronze edge", new Vector3(0, -.015f, sign * (d / 2f + .20f)), new Vector3(w + .46f, .08f, .08f), bronze, .018f);
                foreach (int other in new[] { -1, 1 })
                {
                    var corner = new Vector3(sign * (w / 2f + .17f), .005f, other * (d / 2f + .17f));
                    Solid(parent, "Corner seal", corner, new Vector3(.34f, .10f, .34f), bronze, .045f);
                    Solid(parent, "Jade seal", corner + Vector3.up * .055f, new Vector3(.17f, .025f, .17f), jade, .025f);
                }
            }
        }

        // Real bevel geometry gives the board a thin crafted edge and individual stone relief.
        private void Solid(Transform parent, string name, Vector3 center, Vector3 size, Color color, float bevel)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var mesh = new Mesh { name = name };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float x = size.x * .5f, y = size.y * .5f, z = size.z * .5f;
            float by = Mathf.Min(bevel, y * .65f);
            var rings = new Vector3[4][];
            for (int r = 0; r < 4; r++)
            {
                float inset = r == 0 || r == 3 ? bevel : 0;
                float xx = x - inset, zz = z - inset;
                float yy = r == 0 ? -y : r == 1 ? -y + by : r == 2 ? y - by : y;
                rings[r] = new[] { new Vector3(-xx, yy, -zz), new Vector3(-xx, yy, zz), new Vector3(xx, yy, zz), new Vector3(xx, yy, -zz) };
            }
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int n = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
            }
            for (int r = 0; r < 3; r++)
            for (int j = 0; j < 4; j++) Quad(rings[r][j], rings[r][(j + 1) % 4], rings[r + 1][(j + 1) % 4], rings[r + 1][j]);
            Quad(rings[3][0], rings[3][1], rings[3][2], rings[3][3]);
            Quad(rings[0][3], rings[0][2], rings[0][1], rings[0][0]);
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var shader = Resources.Load<Shader>("Shaders/ToonLit")!;
            var mat = new Material(shader) { color = color };
            _boardResources.Add(mesh); _boardResources.Add(mat);
            mat.SetFloat("_OutlineWidth", 0f); mat.SetFloat("_RimStrength", .04f);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>棋盤格的世界座標（地磚頂面中心）。第 0 列（敵方底線）在右、第 4 列（我方底線）在左；第 0 欄在遠端。</summary>
        public Vector3 TileWorld(Position pos)
        {
            int lanes = _battle != null ? _battle.Setup.Lanes : 5;
            int rows = _battle != null ? _battle.Setup.Rows : 5;
            float x = ((rows - 1) * 0.5f - pos.Row) * TilePitchX;
            float z = ((lanes - 1) * 0.5f - pos.Lane) * TilePitch;
            return new Vector3(x, 0f, z);
        }

        public void SetTileStates(IEnumerable<(Position pos, TileState state)> states)
        {
            foreach (var tag in _tiles.Values) tag.Renderer.material.color = tag.BaseColor;
            foreach (var (pos, state) in states)
            {
                if (!_tiles.TryGetValue((pos.Lane, pos.Row), out var tag)) continue;
                switch (state)
                {
                    case TileState.Range: tag.Renderer.material.color = new Color(0.55f, 0.75f, 1.00f, 0.38f); break;
                    case TileState.Target: tag.Renderer.material.color = new Color(1.00f, 0.86f, 0.20f, 0.55f); break;
                    case TileState.Reach: tag.Renderer.material.color = new Color(0.30f, 0.90f, 0.50f, 0.50f); break;
                    case TileState.Owner: tag.Renderer.material.color = new Color(1.00f, 1.00f, 1.00f, 0.45f); break;
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
            WorldToPanel(TileWorld(pos) + Vector3.up * UnitHeadHeight);

        /// <summary>角色目前（含移動動畫）腳下的面板座標；我方標籤放這裡（頭頂方向是敵方區域）。</summary>
        public Vector2? UnitFootPanel(Unit unit)
        {
            if (!_views.TryGetValue(unit.Id, out var uv) || !uv.Anchor.activeSelf) return null;
            if(uv.LeftFoot!=null && uv.RightFoot!=null)
            {
                var left=WorldToPanel(uv.LeftFoot.position+Vector3.down*.12f);
                var right=WorldToPanel(uv.RightFoot.position+Vector3.down*.12f);
                return new Vector2((left.x+right.x)*.5f,Mathf.Max(left.y,right.y));
            }
            return WorldToPanel(uv.Anchor.transform.position + Vector3.up * 0.05f);
        }

        /// <summary>角色目前（含移動動畫）頭頂的面板座標，血條用；找不到回傳 null。</summary>
        public Vector2? UnitHeadPanel(Unit unit)
        {
            if (!_views.TryGetValue(unit.Id, out var uv) || !uv.Anchor.activeSelf) return null;
            return WorldToPanel(uv.Anchor.transform.position + Vector3.up * UnitHeadHeight);
        }

        /// <summary>點擊選格：面板座標 → 射線打到的地磚。</summary>
        public bool TryPick(Vector2 panelPoint, out Position pos)
        {
            pos = default;
            if (_panelRoot == null) return false;
            float w = _panelRoot.worldBound.width, h = _panelRoot.worldBound.height;
            var screen = new Vector3(panelPoint.x / w * Screen.width, Screen.height - panelPoint.y / h * Screen.height, 0f);
            var ray = _camera.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out var hit, 200f)) return false;
            var tag = hit.collider.GetComponent<TileTag>();
            if (tag == null) return false;
            pos = tag.Pos;
            return true;
        }

        public void DebugValidateGridPicking()
        {
            foreach(var tile in _tiles.Values)
            {
                var projected=WorldToPanel(TileWorld(tile.Pos));
                if(!TryPick(projected,out var picked) || picked.Lane!=tile.Pos.Lane || picked.Row!=tile.Pos.Row)
                    throw new InvalidOperationException("Battle grid raycast does not match its rendered center: "+tile.Pos);
            }
            Debug.Log("[shot] Battle grid center raycasts verified: "+_tiles.Count);
        }

        // ------------------------------------------------------------ 模型

        // URP 專案裡 CreatePrimitive 的預設材質是 Built-in Standard（會變粉紅），地磚改用 URP/Lit。
        private static Shader? TileShader => Resources.Load<Shader>("Shaders/TileOverlay");

        private UnitView? Ensure(Unit unit)
        {
            if (_views.TryGetValue(unit.Id, out var existing)) return existing;
            // 敵人可借用模型（ArtId）；護送 / 守城目標沒有專屬模型時借用村民。
            string art = unit.Protected && !HeroArt.HasOwnModel(unit.ArtId) ? "r_villager" : unit.ArtId;
            if (!_prefabs.TryGetValue(art, out var prefab))
            {
                prefab = HeroArt.Model(art);
                _prefabs[art] = prefab;
                if (prefab == null) Debug.LogWarning("找不到角色模型：" + art);
            }
            if (prefab == null) return null;

            var anchor = new GameObject("Unit_" + unit.Name);
            anchor.transform.SetParent(transform, false);
            anchor.transform.localScale = Vector3.one * ModelScale;
            var model = Instantiate(prefab, anchor.transform);
            var view = model.AddComponent<CharacterView>();
            view.Init(model.transform);

            var uv = new UnitView
            {
                Anchor = anchor,
                View = view,
                Facing = unit.Side == Side.Player ? new Vector3(-.65f, 0f, -.76f).normalized : new Vector3(-.90f, 0f, -.44f).normalized,
            };
            foreach(var bone in model.GetComponentsInChildren<Transform>())
            {
                if(bone.name=="Bip001 L Foot")uv.LeftFoot=bone;
                if(bone.name=="Bip001 R Foot")uv.RightFoot=bone;
            }
            _views[unit.Id] = uv;
            return uv;
        }

        // ------------------------------------------------------------ 每幀

        private void LateUpdate()
        {
            if (_battle == null || _panelRoot == null || _field == null) return;
            FitCamera();
            UpdateBackdrop();

            foreach (var unit in _battle.Units)
            {
                var uv = Ensure(unit);
                if (uv == null) continue;
                if (uv.View.Finished) { uv.Anchor.SetActive(false); continue; }

                var target = TileWorld(unit.Pos);
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
                var center = TileWorld(new Position(key.Item1, key.Item2));
                for (int cx = -1; cx <= 1; cx += 2)
                {
                    for (int cz = -1; cz <= 1; cz += 2)
                    {
                        for (int h = 0; h < 2; h++) // 0 = 腳下（再往下留給我方標籤），1 = 頭頂（再往上留給敵方標籤）
                        {
                            var p = center + new Vector3(cx * TilePitchX * 0.5f, h == 0 ? -TagRoomBelow : UnitHeadHeight + TagRoomAbove, cz * TilePitch * 0.5f);
                            var c = inv * p;
                            minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                            minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
                        }
                    }
                }
            }
            float needW = (maxX - minX) / BoardZoom + 0.4f;
            float needH = (maxY - minY) / BoardZoom + 0.3f;
            // Tall displays have more vertical room but less room beside the board.
            // Reduce the initial framing magnification while preserving user zoom.
            float framingZoom = _zoom * Mathf.Min(1f, _camera.aspect / (16f / 9f));
            float worldPerPx = Mathf.Max(needH / fieldH, needW / fieldW) / framingZoom;
            _camera.orthographicSize = worldPerPx * Screen.height * 0.5f;

            // 包圍盒中心要落在 field 中心：在攝影機空間反向平移。
            float dx = (fieldCenter.x - Screen.width * 0.5f) * worldPerPx;
            float dy = (fieldCenter.y - Screen.height * 0.5f) * worldPerPx;
            // 拖曳平移：棋盤往右拖 → 鏡頭往左；往下拖 → 鏡頭往上（攝影機空間 y 向上）。
            var camLocal = new Vector3((minX + maxX) * 0.5f - dx - _panScreenPx.x * worldPerPx, (minY + maxY) * 0.5f - dy + _panScreenPx.y * worldPerPx, -40f);
            _camera.transform.rotation = rot;
            _camera.transform.position = rot * camLocal;
        }
    }
}
