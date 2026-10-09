#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using SanGuo.Core;
using UnityEngine;
using UnityEngine.UIElements;
using Position = SanGuo.Core.Position;

namespace SanGuo.Client
{
    public enum TileState { None, Target, Reach, Range, Owner }

    public sealed class TileTag : MonoBehaviour
    {
        public Position Pos;
        public Renderer Renderer = null!;
        public Color BaseColor;
    }

    public sealed class BattleStage : MonoBehaviour
    {
        public const float CameraPitchDegrees = 36f;
        public const float BoardLeftBias = 0f;
        private const float BoardZoom = 1.0f;
        public const float CameraYawDegrees = 60f;
        private const float TilePitch = 1.85f;
        private const float TilePitchX = 1.55f;
        private const float TileTop = 0.03f;
        private const float ModelScale = 0.90f;
        private const float UnitHeadHeight = 2.4f;
        private const float TagRoomAbove = 0.5f;
        private const float TagRoomBelow = 0.35f;

        private sealed class UnitView
        {
            public GameObject Anchor = null!;
            public CharacterView View = null!;
            public Transform? LeftFoot, RightFoot;
            public bool Placed;
            public Vector3 Facing;
            public Renderer[]? Bodies;
        }

        private const float DefaultZoom = 1.3f, MinZoom = 0.7f, MaxZoom = 3.2f;
        private float _zoom = DefaultZoom;
        private Vector2 _panScreenPx;

        public void ZoomBy(float factor) => _zoom = Mathf.Clamp(_zoom * factor, MinZoom, MaxZoom);

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

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.64f, 0.62f, 0.56f);
        }

        private Transform? _backdrop;
        private const float BackdropDistance = 60f;

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

        private void BuildTiles()
        {
            _tileRoot = new GameObject("Tiles");
            _tileRoot.transform.SetParent(transform, false);
            BuildFloor(_tileRoot.transform);
            BuildArrowObject(_tileRoot.transform);
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
                    tag.BaseColor = new Color(0.82f, 0.78f, 0.60f, 0f);
                    tag.Renderer.material.SetFloat("_Border", 0.04f);
                    tag.Renderer.material.SetColor("_BorderColor", new Color(0.42f, 1f, 0.5f, 0.9f));
                    tag.Renderer.material.color = tag.BaseColor;
                    _tiles[(lane, row)] = tag;
                }
            }
        }

        private Mesh? _arrowMesh;

        private void BuildArrowObject(Transform parent)
        {
            var go = new GameObject("Enemy move arrows");
            go.transform.SetParent(parent, false);
            _arrowMesh = new Mesh { name = "Enemy move arrows" };
            go.AddComponent<MeshFilter>().sharedMesh = _arrowMesh;
            var material = new Material(TileShader!) { color = new Color(1f, 0.25f, 0.18f, 0.85f) };
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            _boardResources.Add(_arrowMesh);
            _boardResources.Add(material);
        }

        public void SetMoveArrows(List<List<Position>> paths)
        {
            if (_arrowMesh == null) return;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int n = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
            }
            const float shaft = 0.11f, headHalf = 0.3f, headLength = 0.55f, lift = 0.03f;
            foreach (var path in paths)
            {
                if (path.Count < 2) continue;
                var points = path.Select(p => TileWorld(p) + Vector3.up * lift).ToList();
                for (int i = 0; i + 1 < points.Count; i++)
                {
                    var direction = (points[i + 1] - points[i]).normalized;
                    var side = Vector3.Cross(Vector3.up, direction) * shaft;
                    var from = points[i] - direction * (i == 0 ? -0.3f : shaft);
                    var to = points[i + 1] + direction * (i + 2 == points.Count ? -headLength : shaft);
                    Quad(from - side, from + side, to + side, to - side);
                }
                var last = points[points.Count - 1];
                var lastDirection = (last - points[points.Count - 2]).normalized;
                var headSide = Vector3.Cross(Vector3.up, lastDirection) * headHalf;
                var baseCenter = last - lastDirection * headLength;
                var tip = last + lastDirection * 0.3f;
                Quad(baseCenter - headSide, baseCenter + headSide, tip, tip);
            }
            _arrowMesh.Clear();
            _arrowMesh.SetVertices(vertices);
            _arrowMesh.SetTriangles(triangles, 0);
            _arrowMesh.SetUVs(0, vertices.Select(_ => new Vector2(0.5f, 0.5f)).ToList());
        }

        private void BuildFloor(Transform parent)
        {
            float w = _battle!.Setup.Rows * TilePitchX + 60f, d = _battle.Setup.Lanes * TilePitch + 60f;
            var floor = new GameObject("Natural battlefield");
            floor.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "Continuous grass dirt ground" };
            mesh.vertices = new[] { new Vector3(-w/2,-.02f,-d/2), new Vector3(-w/2,-.02f,d/2), new Vector3(w/2,-.02f,d/2), new Vector3(w/2,-.02f,-d/2) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.uv = new[] { new Vector2(0,0), new Vector2(0,d/14), new Vector2(w/14,d/14), new Vector2(w/14,0) };
            mesh.RecalculateNormals();
            floor.AddComponent<MeshFilter>().sharedMesh = mesh;
            floor.AddComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>("BattleArt/NaturalGround");
            _boardResources.Add(mesh);
        }

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

        public Vector2 WorldToPanel(Vector3 world)
        {
            var sp = _camera.WorldToScreenPoint(world);
            float w = _panelRoot != null ? _panelRoot.worldBound.width : Screen.width;
            float h = _panelRoot != null ? _panelRoot.worldBound.height : Screen.height;
            return new Vector2(sp.x / Screen.width * w, (Screen.height - sp.y) / Screen.height * h);
        }

        public Vector2 TileHeadPanel(Side side, Position pos) =>
            WorldToPanel(TileWorld(pos) + Vector3.up * UnitHeadHeight);

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

        public Vector2? UnitTagPanel(Unit unit)
        {
            if (!_views.TryGetValue(unit.Id, out var uv) || !uv.Anchor.activeSelf) return null;
            if (uv.Bodies == null || uv.Bodies.Length == 0)
            {
                var skinned = uv.Anchor.GetComponentsInChildren<SkinnedMeshRenderer>();
                uv.Bodies = skinned.Length > 0 ? skinned : uv.Anchor.GetComponentsInChildren<Renderer>();
            }
            var origin = uv.Anchor.transform.position;
            float top = origin.y + UnitHeadHeight;
            if (uv.Bodies.Length > 0)
            {
                top = float.MinValue;
                foreach (var body in uv.Bodies) if (body != null) top = Mathf.Max(top, body.bounds.max.y);
                if (top == float.MinValue) top = origin.y + UnitHeadHeight;
            }
            return WorldToPanel(new Vector3(origin.x, top, origin.z));
        }

        public Vector2? UnitHeadPanel(Unit unit)
        {
            if (!_views.TryGetValue(unit.Id, out var uv) || !uv.Anchor.activeSelf) return null;
            return WorldToPanel(uv.Anchor.transform.position + Vector3.up * UnitHeadHeight);
        }

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

        private static Shader? TileShader => Resources.Load<Shader>("Shaders/TileOverlay");

        private UnitView? Ensure(Unit unit)
        {
            if (_views.TryGetValue(unit.Id, out var existing)) return existing;
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
                Facing = unit.Side == Side.Player ? Vector3.right : Vector3.left,
            };
            foreach(var bone in model.GetComponentsInChildren<Transform>())
            {
                if(bone.name=="Bip001 L Foot")uv.LeftFoot=bone;
                if(bone.name=="Bip001 R Foot")uv.RightFoot=bone;
            }
            _views[unit.Id] = uv;
            return uv;
        }

        private UnitView? _facingReview;
        private Vector3 _facingReviewDirection;

        public float DebugBeginFacingReview(bool enemy)
        {
            var attacker = _battle!.Units.Find(u => u.Alive && (enemy ? u.Side == Side.Enemy : u.Side == Side.Player));
            var target = _battle.Units.Find(u => u.Alive && u.Side != attacker!.Side && u.Pos.Lane != attacker.Pos.Lane)
                ?? _battle.Units.Find(u => u.Alive && u.Side != attacker!.Side);
            _facingReview = _views[attacker!.Id];
            var other = _views[target!.Id];
            _facingReviewDirection = other.Anchor.transform.position - _facingReview.Anchor.transform.position;
            _facingReviewDirection.y = 0; _facingReviewDirection.Normalize();
            _facingReview.View.Attack(other.View);
            return _facingReview.View.AttackLength;
        }

        public void DebugValidateAttackFacing()
        {
            if (_facingReview == null || Vector3.Dot(_facingReview.View.DisplayedFacing, _facingReviewDirection) < .98f)
                throw new InvalidOperationException("Attack animation does not face its target.");
            Debug.Log("[shot] Attack target direction verified.");
        }

        public void DebugValidateIdleFacing()
        {
            foreach (var unit in _battle!.Units)
            {
                if (!unit.Alive || !_views.TryGetValue(unit.Id, out var view) || view.View.IsAttacking) continue;
                if (Vector3.Dot(view.View.DisplayedFacing, unit.Side == Side.Player ? Vector3.right : Vector3.left) < .99f)
                    throw new InvalidOperationException("Unit does not face the opposing side: " + unit.DefId);
            }
        }

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

        private void FitCamera()
        {
            var pb = _panelRoot!.worldBound;
            var fb = _field!.worldBound;
            if (pb.width <= 1f || pb.height <= 1f || fb.width <= 1f || fb.height <= 1f) return;

            float sx = Screen.width / pb.width, sy = Screen.height / pb.height;
            float fieldW = fb.width * sx, fieldH = fb.height * sy;
            var fieldCenter = new Vector2((fb.x + fb.width * 0.5f) * sx, Screen.height - (fb.y + fb.height * 0.5f) * sy);
            fieldCenter.x -= fieldW * BoardLeftBias;

            var rot = Quaternion.Euler(CameraPitchDegrees, CameraYawDegrees, 0f);
            var inv = Quaternion.Inverse(rot);

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var key in _tiles.Keys)
            {
                var center = TileWorld(new Position(key.Item1, key.Item2));
                for (int cx = -1; cx <= 1; cx += 2)
                {
                    for (int cz = -1; cz <= 1; cz += 2)
                    {
                        for (int h = 0; h < 2; h++)
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
            float framingZoom = _zoom * Mathf.Min(1f, _camera.aspect / (16f / 9f));
            float worldPerPx = Mathf.Max(needH / fieldH, needW / fieldW) / framingZoom;
            _camera.orthographicSize = worldPerPx * Screen.height * 0.5f;

            float dx = (fieldCenter.x - Screen.width * 0.5f) * worldPerPx;
            float dy = (fieldCenter.y - Screen.height * 0.5f) * worldPerPx;
            var camLocal = new Vector3((minX + maxX) * 0.5f - dx - _panScreenPx.x * worldPerPx, (minY + maxY) * 0.5f - dy + _panScreenPx.y * worldPerPx, -40f);
            _camera.transform.rotation = rot;
            _camera.transform.position = rot * camLocal;
        }
    }
}
