#nullable enable
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SanGuo.Client
{
    /// <summary>
    /// 角色模型的程式動畫（待機 / 攻擊 / 施法 / 受擊 / 倒下）。
    /// 不使用骨架動畫：Blender 匯出的模型有 pivot_torso / pivot_head / pivot_arm_R / pivot_arm_L 幾個樞紐，
    /// 這裡直接轉動樞紐與位移整個模型。由 BattleStage 每幀呼叫 Tick，順序固定。
    /// </summary>
    public sealed class CharacterView : MonoBehaviour
    {
        private const float AttackDuration = 0.85f;
        private const float CastDuration = 0.65f;
        private const float HitDuration = 0.55f;
        private const float DieDuration = 1.1f;

        private Transform _model = null!;
        private Quaternion _baseRot = Quaternion.identity;   // 匯入後模型根節點自帶的旋轉（Z-up → Y-up 轉換）
        private Vector3 _baseScale = Vector3.one;
        private Transform? _torso, _head, _armR, _armL;
        private Quaternion _torsoBase, _headBase, _armRBase, _armLBase;
        private Vector3 _headScale = Vector3.one;
        private Vector3 _headOffset;
        private bool _headOffsetApplied;
        private Renderer[] _renderers = System.Array.Empty<Renderer>();
        private Color[] _baseColors = System.Array.Empty<Color>();
        private MaterialPropertyBlock _block = null!;
        private float _phase;
        private string _motion = "sword";
        private Vector3? _attackFacing;
        public string MotionProfile => _motion;

        private float _attackT = -1f, _castT = -1f, _hitT = -1f, _dieT = -1f;
        private float _hitSign = 1f;

        // 骨架動畫模式（模型帶有 CharacterClipSet 時）：以 Playables 播放公司動作 clip。
        private enum Clip { Idle, Attack, Cast, Hit, Die }
        private CharacterClipSet? _clips;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private readonly AnimationClip?[] _clipOf = new AnimationClip?[5];
        private Clip _oneShot = Clip.Idle;
        private float _oneShotT = -1f;
        private float _fade = 1f;

        public bool IsDead => _dieT >= 0f;
        /// <summary>倒下動畫播完（可隱藏）。</summary>
        public bool Finished => _clips != null
            ? _oneShot == Clip.Die && _oneShotT >= (_clipOf[(int)Clip.Die]?.length ?? DieDuration) + 0.35f
            : _dieT >= DieDuration;

        public void Init(Transform model)
        {
            _model = model;
            _baseRot = model.localRotation;
            _baseScale = model.localScale;
            _phase = Random.value * 6.28f;
            foreach (var role in new[] { "archer", "caster", "guard", "polearm", "sword" })
                if (Find("pose_" + role) != null) _motion = role;
            _clips = model.GetComponent<CharacterClipSet>();
            if (_clips != null)
            {
                _motion = _clips.MotionProfile;
                _head = Find("Bip001 Head");
                if (_head != null)
                {
                    _headScale = _head.localScale;
                    _headOffset = _head.localPosition * _clips.NeckExtension;
                }
                InitClips(); return;
            }
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

        public void Attack(CharacterView? target = null)
        {
            if (IsDead) return;
            if (_model.TryGetComponent<ArcherPoseRig>(out var bowRig)) bowRig.Shoot();
            _attackFacing = null;
            if (target != null)
            {
                var delta = target._model.position - _model.position; delta.y = 0;
                if (delta.sqrMagnitude > .01f) _attackFacing = delta.normalized;
            }
            _attackT = 0f; _castT = -1f; PlayOneShot(Clip.Attack);
        }
        public void Cast() { if (!IsDead && _attackT < 0f) { _castT = 0f; PlayOneShot(Clip.Cast); } }

        public void Hit()
        {
            if (IsDead) return;
            if (_clips != null && _oneShotT < 0f) PlayOneShot(Clip.Hit);
            _hitT = 0f;
            _hitSign = -_hitSign;
        }

        public void Die()
        {
            if (IsDead) return;
            if (_model.TryGetComponent<ArcherPoseRig>(out var bowRig)) bowRig.enabled = false;
            _dieT = 0f;
            _attackT = _castT = _hitT = -1f;
            PlayOneShot(Clip.Die);
        }

        // ------------------------------------------------------------ 骨架動畫

        private void InitClips()
        {
            var set = _clips!;
            _clipOf[(int)Clip.Idle] = set.Idle;
            _clipOf[(int)Clip.Attack] = set.Attack;
            _clipOf[(int)Clip.Cast] = set.Cast;
            _clipOf[(int)Clip.Hit] = set.Hit;
            _clipOf[(int)Clip.Die] = set.Die;

            var animator = _model.GetComponentInChildren<Animator>();
            if (animator == null) { Debug.LogWarning("角色缺少 Animator：" + _model.name); return; }
            animator.applyRootMotion = false;

            _graph = PlayableGraph.Create("Character_" + _model.name);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _mixer = AnimationMixerPlayable.Create(_graph, _clipOf.Length);
            for (int i = 0; i < _clipOf.Length; i++)
            {
                if (_clipOf[i] == null) continue;
                var cp = AnimationClipPlayable.Create(_graph, _clipOf[i]);
                _graph.Connect(cp, 0, _mixer, i);
            }
            AnimationPlayableOutput.Create(_graph, "Anim", animator).SetSourcePlayable(_mixer);
            _graph.Play();
            _mixer.SetInputWeight((int)Clip.Idle, 1f);
        }

        private void PlayOneShot(Clip clip)
        {
            if (_clips == null || _clipOf[(int)clip] == null) return;
            if (_oneShot == Clip.Die && _oneShotT >= 0f) return;   // 倒下後不再被其他動作覆蓋
            _oneShot = clip;
            _oneShotT = 0f;
            _fade = 0f;
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        private void TickClips(float dt, Vector3 facing, Vector3 basePos)
        {
            _model.position = basePos;
            _model.rotation = Quaternion.LookRotation(facing, Vector3.up) * ModelYawFix;

            if (!_graph.IsValid()) return;
            var idle = _clipOf[(int)Clip.Idle];
            if (idle != null)
                _mixer.GetInput((int)Clip.Idle).SetTime((Time.time + _phase) % Mathf.Max(0.01f, idle.length));

            float oneShotWeight = 0f;
            if (_oneShotT >= 0f)
            {
                var c = _clipOf[(int)_oneShot]!;
                _oneShotT += dt;
                _fade = Mathf.Min(1f, _fade + dt / 0.1f);
                bool isDie = _oneShot == Clip.Die;
                float t = isDie ? Mathf.Min(_oneShotT, c.length) : _oneShotT;
                if (!isDie && _oneShotT >= c.length) { _oneShotT = -1f; }
                else
                {
                    _mixer.GetInput((int)_oneShot).SetTime(t);
                    oneShotWeight = _fade;
                }
            }
            for (int i = 1; i < _clipOf.Length; i++)
                _mixer.SetInputWeight(i, _oneShotT >= 0f && i == (int)_oneShot ? oneShotWeight : 0f);
            _mixer.SetInputWeight((int)Clip.Idle, 1f - oneShotWeight);
            // Remove last frame's art offset before evaluation. Clips without translation
            // tracks must not accumulate the offset; clips with tracks retain their motion.
            if (_head != null && _headOffsetApplied) _head.localPosition -= _headOffset;
            _graph.Evaluate(0f);
            if (_head != null)
            {
                _head.localScale = _headScale * _clips!.HeadScale;
                _head.localPosition += _headOffset;
                _headOffsetApplied = true;
            }
        }

        // ------------------------------------------------------------ 每幀

        /// <param name="facing">角色面向的世界方向（水平）。</param>
        /// <param name="scale">模型目前的縮放（位移單位為模型座標，要乘上它換成世界單位）。</param>
        /// <param name="basePos">這一幀模型的基準世界位置（腳底）。</param>
        public void Tick(float dt, Vector3 facing, float scale, Vector3 basePos)
        {
            if (_clips != null) { TickClips(dt, facing, basePos); return; }
            if (_attackT >= 0 && _attackFacing.HasValue)
            {
                float k = _attackT / AttackDuration;
                float turn = Mathf.Min(Mathf.Clamp01(k / .15f), 1f - Mathf.Clamp01((k - .7f) / .3f));
                facing = Vector3.Slerp(facing, _attackFacing.Value, Mathf.SmoothStep(0, 1, turn)).normalized;
            }
            float t = Time.time + _phase;
            // 擺動軸：讓正角度 = 手臂向前揮。
            Vector3 axis = Vector3.Cross(facing, Vector3.up).normalized;

            float lunge = 0f, jump = 0f, armRAngle = 0f, armLAngle = 0f, headAngle = 0f, shake = 0f, tiltBack = 0f;
            float squash = 0f;

            // 待機：呼吸起伏、輕微擺手與點頭。
            jump += 0.012f * Mathf.Sin(t * 2.4f);
            squash += 0.012f * Mathf.Sin(t * 2.4f + 1.2f);
            float idleSpeed = _motion == "caster" ? 1.1f : _motion == "guard" ? 1.5f : 1.9f;
            float idleSwing = _motion == "archer" ? 1.5f : _motion == "guard" ? 2f : 4f;
            armRAngle += idleSwing * Mathf.Sin(t * idleSpeed);
            armLAngle += idleSwing * Mathf.Sin(t * idleSpeed + 1.7f);
            headAngle += 2.5f * Mathf.Sin(t * 1.3f);

            if (_attackT >= 0f)
            {
                _attackT += dt;
                float k = _attackT / AttackDuration;
                if (k >= 1f) _attackT = -1f;
                else
                {
                    if (_motion == "archer")
                    {
                        armRAngle += -22f * Mathf.Sin(Mathf.Min(k / .45f, 1f) * Mathf.PI / 2f) * (1f - Mathf.Clamp01((k - .5f) / .5f));
                        armLAngle += 3f * Mathf.Sin(k * Mathf.PI);
                        headAngle += -4f * Mathf.Sin(k * Mathf.PI);
                    }
                    else if (_motion == "caster")
                    {
                        armRAngle += -38f * Mathf.Sin(k * Mathf.PI);
                        armLAngle += -18f * Mathf.Sin(k * Mathf.PI);
                        jump += .06f * Mathf.Sin(k * Mathf.PI);
                    }
                    else if (_motion == "guard")
                    {
                        armLAngle += -20f * Mathf.Sin(k * Mathf.PI);
                        armRAngle += 42f * Mathf.Sin(k * Mathf.PI);
                        lunge = .28f * Mathf.Sin(k * Mathf.PI);
                    }
                    else if (_motion == "polearm")
                    {
                        armRAngle += -32f * Mathf.Sin(k * Mathf.PI * 2f);
                        armLAngle += 16f * Mathf.Sin(k * Mathf.PI);
                        lunge = .38f * Mathf.Sin(k * Mathf.PI);
                    }
                    else if (k < 0.3f)          // 蓄力：後仰、舉手
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
                    armRAngle += (_motion == "caster" ? -42f : -24f) * arc;
                    armLAngle += (_motion == "caster" ? -30f : -16f) * arc;
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
