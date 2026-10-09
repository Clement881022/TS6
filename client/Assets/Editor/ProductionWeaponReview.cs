#nullable enable
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace SanGuo.Client.Editor
{
    public static class ProductionWeaponReview
    {
        public static void Inspect()
        {
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ProductionCharacters/guanyu.prefab"));
            root.transform.localScale=Vector3.one;
            root.GetComponent<CharacterClipSet>().Idle!.SampleAnimation(root,0);
            foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("Hand") || t.name.Contains("Finger") || t.name.Contains("Weapon")))
                Debug.Log("GRIP_INSPECT "+t.name+" pos="+root.transform.InverseTransformPoint(t.position).ToString("F4")+" local="+t.localPosition.ToString("F4")+" scale="+t.lossyScale.ToString("F3")+" X="+root.transform.InverseTransformVector(t.TransformVector(Vector3.right)).normalized.ToString("F3")+" Y="+root.transform.InverseTransformVector(t.TransformVector(Vector3.up)).normalized.ToString("F3")+" Z="+root.transform.InverseTransformVector(t.TransformVector(Vector3.forward)).normalized.ToString("F3"));
            foreach(var r in root.GetComponentsInChildren<MeshFilter>())
                Debug.Log("WEAPON_MESH "+r.name+" "+r.sharedMesh.bounds+" local="+r.transform.localPosition+" rot="+r.transform.localEulerAngles+" scale="+r.transform.localScale);
            UnityEngine.Object.DestroyImmediate(root);
            EditorApplication.Exit(0);
        }
    }
}
