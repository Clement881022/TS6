#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SanGuo.Client.Editor
{
    /// <summary>
    /// 把公司的模組化角色（Avatar 骨架 + Body / Hair / Face 部件）烘焙成 Resources/Characters/&lt;defId&gt;.prefab，
    /// 取代 Blender 程式化生成的占位 FBX。部件以骨頭名稱重新綁到 Avatar 骨架上，並填入 CharacterClipSet。
    /// 選單：SanGuo / 烘焙公司角色。
    /// </summary>
    public static class CompanyCharacterBaker
    {
        private const string ModelRoot = "Assets/Arts/Models";
        private const string OutDir = "Assets/Resources/Characters";

        private readonly struct Look
        {
            public readonly int Avatar, Body, Hair, Face;
            public Look(int avatar, int body, int hair, int face) { Avatar = avatar; Body = body; Hair = hair; Face = face; }
        }

        // defId → 外觀。公司資源目前只有 7 套 Avatar，先以不同的 Body / Hair / Face 組合區分武將，之後可逐一調整。
        private static readonly Dictionary<string, Look> Looks = new Dictionary<string, Look>
        {
            ["liubei"] = new Look(1, 1, 1, 1),
            ["guanyu"] = new Look(1, 2, 2, 2),
            ["zhangfei"] = new Look(1, 3, 3, 3),
            ["zhaoyun"] = new Look(2, 1, 2, 1),
            ["huangzhong"] = new Look(2, 4, 4, 2),
            ["zhugeliang"] = new Look(3, 2, 1, 3),
            ["pangtong"] = new Look(3, 1, 2, 2),
            ["zhangjiao"] = new Look(3, 5, 5, 4),
            ["r_militia"] = new Look(4, 1, 3, 1),
            ["r_villager"] = new Look(4, 2, 4, 2),
            ["r_shield"] = new Look(4, 3, 5, 3),
            ["r_archer"] = new Look(5, 1, 2, 4),
            ["r_healer"] = new Look(5, 2, 1, 5),
            ["yt_soldier"] = new Look(2, 3, 5, 1),
            ["yt_archer"] = new Look(5, 4, 3, 2),
            ["yt_brute"] = new Look(1, 4, 5, 3),
            ["yt_ironbrute"] = new Look(1, 5, 4, 4),
            ["yt_lieutenant"] = new Look(2, 5, 2, 5),
            ["yt_chief"] = new Look(3, 4, 3, 1),
            ["yt_priest"] = new Look(3, 3, 4, 2),
            ["yt_sharpshooter"] = new Look(5, 5, 5, 3),
            ["yt_warlock"] = new Look(3, 1, 5, 4),
            ["yt_zhangjiao"] = new Look(3, 5, 5, 4),
        };

        // defId → Weapon 編號（Weapon_000NN）。弓手沒有對應的弓模型，維持空手。
        private static readonly Dictionary<string, int> Weapons = new Dictionary<string, int>
        {
            ["liubei"] = 12, ["guanyu"] = 21, ["zhangfei"] = 8, ["zhaoyun"] = 2, ["huangzhong"] = 14,
            ["zhugeliang"] = 30, ["pangtong"] = 30, ["zhangjiao"] = 18,
            ["r_militia"] = 11, ["r_villager"] = 17, ["r_shield"] = 15, ["r_healer"] = 4,
            ["yt_soldier"] = 10, ["yt_brute"] = 16, ["yt_ironbrute"] = 5, ["yt_lieutenant"] = 13,
            ["yt_chief"] = 32, ["yt_priest"] = 19, ["yt_warlock"] = 20, ["yt_zhangjiao"] = 31,
        };

        private const string HandBone = "Bip001 R Hand";
        private static readonly Vector3 WeaponPos = Vector3.zero;
        private static readonly Vector3 WeaponEuler = Vector3.zero;

        /// <summary>吉祥物「巴豆妖」：Avatar_10001 骨架 + 對應部件，烘成 Resources/Characters/badou.prefab（教學對話用）。</summary>
        [MenuItem("SanGuo/烘焙巴豆妖")]
        public static void BakeMascot()
        {
            Directory.CreateDirectory(OutDir);
            bool ok = Bake("badou", new Look(10001, 10001, 0, 0));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("巴豆妖烘焙：" + (ok ? "成功" : "失敗"));
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        [MenuItem("SanGuo/烘焙公司角色")]
        public static void BakeAll()
        {
            Directory.CreateDirectory(OutDir);
            int ok = 0;
            foreach (var kv in Looks)
                if (Bake(kv.Key, kv.Value)) ok++;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"公司角色烘焙完成：{ok}/{Looks.Count}");
        }

        private static bool Bake(string defId, Look look)
        {
            string avatarId = $"Avatar_{look.Avatar:00000}";
            var avatarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelRoot}/Avatar/{avatarId}/{avatarId}.prefab");
            if (avatarPrefab == null) { Debug.LogError("找不到 Avatar prefab：" + avatarId); return false; }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(avatarPrefab);
            try
            {
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = defId;

                var bones = new Dictionary<string, Transform>();
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
                var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

                bool good = Attach(renderers, bones, "BodyRenderer", $"Body/Body_{look.Body:00000}/Body_{look.Body:00000}_Fbx.fbx")
                          & (look.Hair <= 0 || Attach(renderers, bones, "HairRenderer", $"Hair/Hair_{look.Hair:00000}/Hair_{look.Hair:00000}_Fbx.fbx"))
                          & (look.Face <= 0 || Attach(renderers, bones, "FaceRenderer", $"Face/Face_{look.Face:00000}/Face_{look.Face:00000}_Fbx.fbx"));
                if (!good) return false;

                // 沒有衣飾部件的 renderer 留空 mesh 會出錯，直接關掉。
                foreach (var r in renderers) if (r.sharedMesh == null) r.gameObject.SetActive(false);

                if (Weapons.TryGetValue(defId, out int weaponId)) AttachWeapon(bones, weaponId);

                var clips = root.AddComponent<CharacterClipSet>();
                string a = $"{ModelRoot}/Avatar/{avatarId}/{avatarId}_Style0_";
                clips.Idle = LoadClip(a + "FightStandby.fbx");
                clips.Attack = LoadClip(a + "Fight1.fbx");
                clips.Cast = LoadClip(a + "Magic.fbx");
                clips.Hit = LoadClip(a + "Beaten.fbx");
                clips.Die = LoadClip(a + "Die.fbx");

                PrefabUtility.SaveAsPrefabAsset(root, $"{OutDir}/{defId}.prefab");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static bool Attach(SkinnedMeshRenderer[] targets, Dictionary<string, Transform> bones, string slot, string partFbx)
        {
            string path = $"{ModelRoot}/{partFbx}";
            var partPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (partPrefab == null) { Debug.LogError("找不到部件：" + path); return false; }
            var target = targets.FirstOrDefault(r => r.name == slot);
            if (target == null) { Debug.LogError("Avatar 缺少 " + slot); return false; }
            var src = partPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (src == null) { Debug.LogError("部件沒有 SkinnedMeshRenderer：" + path); return false; }

            var mapped = new Transform[src.bones.Length];
            for (int i = 0; i < mapped.Length; i++)
            {
                string n = src.bones[i] != null ? src.bones[i].name : "";
                if (!bones.TryGetValue(n, out var bone))
                {
                    // 部件自帶的額外骨頭（胸甲、披風等）：沿部件的階層往上找到 Avatar 上已有的祖先，補建缺的那段。
                    bone = src.bones[i] != null ? EnsureBone(src.bones[i], bones) : null;
                    if (bone == null)
                    {
                        Debug.LogError($"{path} 的骨頭「{n}」在 Avatar 骨架上找不到");
                        return false;
                    }
                }
                mapped[i] = bone;
            }
            target.sharedMesh = src.sharedMesh;
            target.sharedMaterials = src.sharedMaterials;
            target.bones = mapped;
            target.rootBone = src.rootBone != null && bones.TryGetValue(src.rootBone.name, out var rb) ? rb : null;
            target.localBounds = src.localBounds;
            return true;
        }

        private static void AttachWeapon(Dictionary<string, Transform> bones, int weaponId)
        {
            string id = $"Weapon_{weaponId:00000}";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelRoot}/Weapon/{id}/{id}_Fbx.fbx");
            if (prefab == null || !bones.TryGetValue(HandBone, out var hand))
            {
                Debug.LogWarning("武器掛載失敗：" + id);
                return;
            }
            var w = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(w, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            w.name = id;
            w.transform.SetParent(hand, false);
            w.transform.localPosition = WeaponPos;
            w.transform.localRotation = Quaternion.Euler(WeaponEuler);
        }

        private static Transform? EnsureBone(Transform partBone, Dictionary<string, Transform> bones)
        {
            if (bones.TryGetValue(partBone.name, out var existing)) return existing;
            if (partBone.parent == null) return null;
            var parent = EnsureBone(partBone.parent, bones);
            if (parent == null) return null;
            var go = new GameObject(partBone.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = partBone.localPosition;
            go.transform.localRotation = partBone.localRotation;
            go.transform.localScale = partBone.localScale;
            bones[partBone.name] = go.transform;
            return go.transform;
        }

        private static AnimationClip? LoadClip(string fbxPath)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null) Debug.LogWarning("找不到動畫：" + fbxPath);
            return clip;
        }
    }
}
