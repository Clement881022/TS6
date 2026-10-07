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
            public readonly int Avatar, Body, Hair, Face, BodyMat, HairMat, FaceMat, Cosmetic, Weapon;
            public Look(int avatar, int body, int hair, int face, int bodyMat = 1, int hairMat = 1, int faceMat = 1, int cosmetic = 0, int weapon = 0)
            {
                Avatar = avatar; Body = body; Hair = hair; Face = face;
                BodyMat = bodyMat; HairMat = hairMat; FaceMat = faceMat; Cosmetic = cosmetic; Weapon = weapon;
            }
        }

        // defId → 外觀。數字取自公司 CharacterSkin 表（體型 / 身體 / 臉 / 頭髮 / 鬍子 / 主手武器 與各自的材質版本）。
        // 武將本人用同名角色的外觀；鄉勇、黃巾沿用公司的雜兵外觀；公司沒有的龐統借用荀彧的外觀。
        private static readonly Dictionary<string, Look> Looks = new Dictionary<string, Look>
        {
            ["liubei"] = new Look(1, 1, 1, 1, cosmetic: 1, weapon: 1),
            ["guanyu"] = new Look(2, 2, 2, 2, cosmetic: 2, weapon: 29),
            ["zhangfei"] = new Look(3, 3, 3, 3, cosmetic: 3, weapon: 3),
            ["zhaoyun"] = new Look(2, 24, 24, 24, cosmetic: 24, weapon: 33),
            ["huangzhong"] = new Look(2, 25, 25, 25, cosmetic: 25, weapon: 10),
            ["zhugeliang"] = new Look(2, 37, 37, 37, cosmetic: 37, weapon: 30),
            ["pangtong"] = new Look(2, 23, 23, 23, cosmetic: 4, weapon: 30),
            ["zhangjiao"] = new Look(2, 5, 5, 5, cosmetic: 5, weapon: 6),
            ["yt_zhangjiao"] = new Look(2, 5, 5, 5, cosmetic: 5, weapon: 6),
            // 鄉勇（公司「男體1拚」）
            ["r_militia"] = new Look(1, 1, 10002, 10001, bodyMat: 2, cosmetic: 10006, weapon: 5),
            ["r_villager"] = new Look(1, 1, 10003, 10001, bodyMat: 3, cosmetic: 10006, weapon: 17),
            ["r_shield"] = new Look(1, 1, 10005, 10003, bodyMat: 4, cosmetic: 10006, weapon: 15),
            ["r_archer"] = new Look(1, 1, 10006, 10006, bodyMat: 6, cosmetic: 10006),
            ["r_healer"] = new Look(4, 14, 14, 14, cosmetic: 4, weapon: 4),
            // 黃巾（公司黃巾賊 / 黃巾頭目）
            ["yt_soldier"] = new Look(1, 1, 10002, 10005, bodyMat: 6, cosmetic: 10006, weapon: 11),
            ["yt_archer"] = new Look(1, 6, 20001, 20001, cosmetic: 10003),
            ["yt_brute"] = new Look(2, 2, 20001, 20005, bodyMat: 3, cosmetic: 20004, weapon: 5),
            ["yt_ironbrute"] = new Look(2, 10, 20003, 20001, bodyMat: 5, cosmetic: 10006, weapon: 10),
            ["yt_lieutenant"] = new Look(1, 7, 20002, 20003, cosmetic: 10006, weapon: 7),
            ["yt_chief"] = new Look(1, 12, 20004, 20004, bodyMat: 6, cosmetic: 10003, weapon: 12),
            ["yt_priest"] = new Look(1, 10, 20003, 20005, bodyMat: 6, cosmetic: 10006, weapon: 14),
            ["yt_sharpshooter"] = new Look(2, 2, 20001, 20005, bodyMat: 3, cosmetic: 20004),
            ["yt_warlock"] = new Look(1, 12, 20004, 20004, bodyMat: 6, cosmetic: 10003, weapon: 20),
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

                bool good = Attach(renderers, bones, "BodyRenderer", $"Body/Body_{look.Body:00000}/Body_{look.Body:00000}_Fbx.fbx", look.BodyMat)
                          & (look.Hair <= 0 || Attach(renderers, bones, "HairRenderer", $"Hair/Hair_{look.Hair:00000}/Hair_{look.Hair:00000}_Fbx.fbx", look.HairMat))
                          & (look.Face <= 0 || Attach(renderers, bones, "FaceRenderer", $"Face/Face_{look.Face:00000}/Face_{look.Face:00000}_Fbx.fbx", look.FaceMat));
                if (!good) return false;
                // 鬍子 / 配件：沒有或失敗都不致命，只是少一個部件。
                if (look.Cosmetic > 0)
                    Attach(renderers, bones, "CosmeticRenderer", $"Cosmetic/Cosmetic_{look.Cosmetic:00000}/Cosmetic_{look.Cosmetic:00000}_Fbx.fbx", 1);

                // 沒有衣飾部件的 renderer 留空 mesh 會出錯，直接關掉。
                foreach (var r in renderers) if (r.sharedMesh == null) r.gameObject.SetActive(false);

                if (look.Weapon > 0) AttachWeapon(bones, look.Weapon);

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

        private static bool Attach(SkinnedMeshRenderer[] targets, Dictionary<string, Transform> bones, string slot, string partFbx, int matVariant = 1)
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
            // 材質版本（公司表的「身體 / 臉 / 頭髮材質」）：<部件>_<版本>_Mat.mat；只有單一材質槽的部件才換。
            if (matVariant > 1 && src.sharedMaterials.Length == 1)
            {
                string dir = path.Substring(0, path.LastIndexOf('/'));
                string name = dir.Substring(dir.LastIndexOf('/') + 1);
                var variant = AssetDatabase.LoadAssetAtPath<Material>($"{dir}/{name}_{matVariant:000}_Mat.mat");
                if (variant != null) target.sharedMaterials = new[] { variant };
                else Debug.LogWarning($"{name} 沒有材質版本 {matVariant}，沿用預設");
            }
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
