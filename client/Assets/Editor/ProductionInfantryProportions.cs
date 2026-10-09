#nullable enable
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SanGuo.Client.Editor
{
    /// <summary>Targeted infantry silhouette correction; retain UVs, weights and action clips.</summary>
    public static class ProductionInfantryProportions
    {
        private const string Output="Assets/Resources/ProductionCharacters";
        public static float HeadScaleFor(string id)=>id=="r_shield" || id=="r_archer" ? .80f : 1f;

        [MenuItem("SanGuo/修正弓盾兵成人Q版比例")]
        public static void Bake()
        {
            foreach(string id in new[]{"r_shield","r_archer"})
            {
                string path=Output+"/"+id+".prefab";
                var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                try
                {
                    root.name=id;
                    Apply(root,id);
                    var clips=root.GetComponent<CharacterClipSet>();
                    ProductionCharacterBaker.Normalize(root,clips);
                    ProductionCharacterAssembly.Validate(root);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            AssetDatabase.SaveAssets();
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }

        public static void Apply(GameObject root,string id)
        {
            if(id!="r_shield" && id!="r_archer")return;
            var clips=root.GetComponent<CharacterClipSet>();
            clips.HeadScale=HeadScaleFor(id);
            if(clips.ProportionVersion>=2)return;
            var savedScale=root.transform.localScale;
            root.transform.localScale=Vector3.one;
            clips.Idle.SampleAnimation(root,0);
            var renderer=root.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="BodyRenderer");
            var mesh=renderer.sharedMesh;
            var vertices=mesh.vertices;var weights=mesh.boneWeights;
            var matrices=renderer.bones.Select((b,i)=>b.localToWorldMatrix*mesh.bindposes[i]).ToArray();
            var spine=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip001 Spine");
            float centreX=spine.position.x;
            int changed=0;
            for(int v=0;v<vertices.Length;v++)
            {
                var w=weights[v];var skin=new Matrix4x4();float torsoWeight=0;
                void Add(int bone,float weight)
                {
                    if(weight<=0)return;
                    for(int n=0;n<16;n++)skin[n]+=matrices[bone][n]*weight;
                    string name=renderer.bones[bone].name;
                    if(name.Contains("Spine") || name.Contains("Clavicle"))torsoWeight+=weight;
                    else if(name.Contains("UpperArm"))torsoWeight+=weight*.35f;
                }
                Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);
                Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);
                var p=skin.MultiplyPoint3x4(vertices[v]);
                float envelope=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.39f,.51f,p.y))
                    *(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.66f,.73f,p.y)));
                float influence=envelope*torsoWeight;
                if(influence<=.001f)continue;
                p.x=centreX+(p.x-centreX)*(1+.16f*influence);
                p.z=spine.position.z+(p.z-spine.position.z)*(1+.08f*influence);
                vertices[v]=skin.inverse.MultiplyPoint3x4(p);changed++;
            }
            if(changed==0)throw new InvalidOperationException("No chest vertices corrected: "+id);
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            root.transform.localScale=savedScale;
            clips.ProportionVersion=2;
            Debug.Log("ART_INFANTRY_PROPORTIONS "+id+" headScale="+clips.HeadScale+" chestVertices="+changed+" version=2");
        }
    }
}
