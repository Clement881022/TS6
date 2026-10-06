#nullable enable
using UnityEngine;

namespace SanGuo.Client
{
    /// <summary>
    /// 角色模型的程式動畫（待機 / 攻擊 / 施法 / 受擊 / 倒下）。
    /// 不使用骨架動畫：Blender 匯出的模型有 pivot_torso / pivot_head / pivot_arm_R / pivot_arm_L 幾個樞紐，
    /// 這裡直接轉動樞紐與位移整個模型。由 BattleStage 每幀呼叫 Tick，順序固定。
    /// </summary>
    public sealed class CharacterView : MonoBehaviour
    {
        private const float AttackDuration = 0.50f;
        private const float CastDuration = 0.38f;
        private const float HitDuration = 0.32f;
        private const float DieDuration = 0.75f;

        private Transform _model = null!;
        private Quaternion _baseRot = Quaternion.identity;   // 匯入後模型根節點自帶的旋轉（Z-up → Y-up 轉換）
        private Vector3 _baseScale = Vector3.one;
        private Transform? _torso, _head, _armR, _armL;
        private Quaternion _torsoBase, _headBase, _armRBase, _armLBase;
        private Renderer[] _renderers = System.Array.Empty<Renderer>();
        private Color[] _baseColors = System.Array.Empty<Color>();
        private MaterialPropertyBlock _block = null!;
        private float _phase;

        private float _attackT = -1f, _castT = -1f, _hitT = -1f, _dieT = -1f;
        private float _hitSign = 1f;

        public bool IsDead => _dieT >= 0f;
        /// <summary>倒下動畫播完（可隱藏）。</summary>
        public bool Finished => _dieT >= DieDuration;

        public void Init(Transform model)
        {
            _model = model;
            _baseRot = model.localRotation;
            _baseScale = model.localScale;
            _phase = Random.value * 6.28f;
            _torso = Find("pivot_torso");
            _head = Find("pivot_head");
            _armR = Find("pivot_arm_R");
            _armL = Find("pivot_arm_L");
            if (_torso != null) _torsoBase = _torso.localRotation;
            if (_head != null) _headBase = _head.localRotation;
            if (_armR != null) _armRBase = _armR.localRotation;
            if (_armL != null) _armLBase = _armL.localRotation;

            _renderers = model.GetComponentsInChildren<Renderer>();
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                var mat = _renderers[i].sharedMaterial;
                _baseColors[i] = mat != null && mat.HasProperty("_Color") ? mat.color : Color.white;
            }
            _block = new MaterialPropertyBlock();
            ApplyToon();
        }

        private static Shader? _toonShader;
        private static readonly System.Collections.Generic.Dictionary<Color, Material> _toonMaterials =
            new System.Collections.Generic.Dictionary<Color, Material>();

        /// <summary>把 FBX 內建的 Standard 材質換成卡通著色（保留各部件顏色）。找不到 shader 時維持原樣。</summary>
        private void ApplyToon()
        {
            if (_toonShader == null) _toonShader = Resources.Load<Shader>("Shaders/ToonLit");
            if (_toonShader == null) { Debug.LogWarning("找不到 Shaders/ToonLit，角色沿用原材質"); return; }
            for (int i = 0; i < _renderers.Length; i++)
            {
                var color = _baseColors[i];
                if (!_toonMaterials.TryGetValue(color, out var mat) || mat == null)
                {
                    mat = new Material(_toonShader) { color = color, name = "Toon_" + ColorUtility.ToHtmlStringRGB(color) };
                    _toonMaterials[color] = mat;
                }
                _renderers[i].sharedMaterial = mat;
            }
        }

        private Transform? Find(string name)
        {
            foreach (var t in _model.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // ------------------------------------------------------------ 觸發

        public void Attack() { if (!IsDead) { _attackT = 0f; _castT = -1f; } }
        public void Cast() { if (!IsDead && _attackT < 0f) _castT = 0f; }

        public void Hit()
        {
            if (IsDead) return;
            _hitT = 0f;
            _hitSign = -_hitSign;
        }

        public void Die()
        {
            if (IsDead) return;
            _dieT = 0f;
            _attackT = _castT = _hitT = -1f;
        }

        // ------------------------------------------------------------ 每幀

        /// <param name="facing">角色面向的世界方向（水平）。</param>
        /// <param name="scale">模型目前的縮放（位移單位為模型座標，要乘上它換成世界單位）。</param>
        /// <param name="basePos">這一幀模型的基準世界位置（腳底）。</param>
        public void Tick(float dt, Vector3 facing, float scale, Vector3 basePos)
        {
            float t = Time.time + _phase;
            // 擺動軸：讓正角度 = 手臂向前揮。
            Vector3 axis = Vector3.Cross(facing, Vector3.up).normalized;

            float lunge = 0f, jump = 0f, armRAngle = 0f, armLAngle = 0f, headAngle = 0f, shake = 0f, tiltBack = 0f;
            float squash = 0f;

            // 待機：呼吸起伏、輕微擺手與點頭。
            jump += 0.012f * Mathf.Sin(t * 2.4f);
            squash += 0.012f * Mathf.Sin(t * 2.4f + 1.2f);
            armRAngle += 4f * Mathf.Sin(t * 1.9f);
            armLAngle += 4f * Mathf.Sin(t * 1.9f + 1.7f);
            headAngle += 2.5f * Mathf.Sin(t * 1.3f);

            if (_attackT >= 0f)
            {
                _attackT += dt;
                float k = _attackT / AttackDuration;
                if (k >= 1f) _attackT = -1f;
                else
                {
                    if (k < 0.3f)          // 蓄力：後仰、舉手
                    {
                        float e = Mathf.SmoothStep(0, 1, k / 0.3f);
                        armRAngle += Mathf.Lerp(0f, -55f, e);
                        lunge = Mathf.Lerp(0f, -0.12f, e);
                    }
                    else if (k < 0.5f)     // 出手：前衝、揮臂
                    {
                        float e = Mathf.SmoothStep(0, 1, (k - 0.3f) / 0.2f);
                        armRAngle += Mathf.Lerp(-55f, 115f, e);
                        lunge = Mathf.Lerp(-0.12f, 0.95f, e);
                        headAngle += 8f * e;
                    }
                    else                   // 收招
                    {
                        float e = Mathf.SmoothStep(0, 1, (k - 0.5f) / 0.5f);
                        armRAngle += Mathf.Lerp(115f, 0f, e);
                        lunge = Mathf.Lerp(0.95f, 0f, e);
                    }
                }
            }
            else if (_castT >= 0f)
            {
                _castT += dt;
                float k = _castT / CastDuration;
                if (k >= 1f) _castT = -1f;
                else
                {
                    float arc = Mathf.Sin(k * Mathf.PI);
                    jump += 0.22f * arc;
                    armRAngle += 150f * arc;
                    armLAngle += 150f * arc;
                    squash -= 0.06f * Mathf.Sin(k * Mathf.PI * 2f);
                }
            }

            float flash = 0f;
            if (_hitT >= 0f)
            {
                _hitT += dt;
                float k = _hitT / HitDuration;
                if (k >= 1f) _hitT = -1f;
                else
                {
                    float decay = 1f - k;
                    shake = _hitSign * 0.12f * Mathf.Sin(k * 22f) * decay;
                    tiltBack = 14f * decay;
                    flash = k < 0.4f ? 1f - k / 0.4f : 0f;
                }
            }

            // 倒下：向後倒、縮小消失。
            float fall = 0f, shrink = 1f;
            if (_dieT >= 0f)
            {
                _dieT += dt;
                float k = Mathf.Clamp01(_dieT / DieDuration);
                fall = 88f * Mathf.SmoothStep(0, 1, Mathf.Clamp01(k / 0.55f));
                shrink = 1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01((k - 0.6f) / 0.4f));
                flash = Mathf.Max(flash, 0.5f * (1f - k));
            }

            // 套用位置：模型座標位移換算成世界單位。
            Vector3 side = Vector3.Cross(Vector3.up, facing).normalized;
            Vector3 offset = facing * (lunge * scale) + side * (shake * scale) + Vector3.up * (jump * scale);
            _model.position = basePos + offset;
            _model.rotation = Quaternion.LookRotation(facing, Vector3.up) * ModelYawFix * _baseRot;
            if (fall != 0f) _model.rotation = Quaternion.AngleAxis(-fall, axis) * _model.rotation;
            _model.localScale = Vector3.Scale(_baseScale, new Vector3(1f + squash, 1f - squash, 1f + squash)) * shrink;

            // 樞紐：從基準旋轉出發，繞世界軸轉動。
            Pose(_armR, _armRBase, axis, armRAngle);
            Pose(_armL, _armLBase, axis, armLAngle);
            Pose(_head, _headBase, axis, headAngle - tiltBack);

            ApplyFlash(flash);
        }

        /// <summary>FBX 匯入後模型的「正面」方向與 LookRotation 的 +Z 之間的修正（由截圖校準）。</summary>
        public static Quaternion ModelYawFix = Quaternion.identity;

        private static void Pose(Transform? pivot, Quaternion baseLocal, Vector3 axis, float angle)
        {
            if (pivot == null) return;
            pivot.localRotation = baseLocal;
            pivot.rotation = Quaternion.AngleAxis(angle, axis) * pivot.rotation;
        }

        private void ApplyFlash(float amount)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].GetPropertyBlock(_block);
                _block.SetColor("_Color", Color.Lerp(_baseColors[i], Color.white, amount * 0.85f));
                _renderers[i].SetPropertyBlock(_block);
            }
        }
    }
}
