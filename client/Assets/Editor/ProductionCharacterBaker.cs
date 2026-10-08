#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SanGuo.Client.Editor
{
    /// <summary>Refine the existing UV-mapped, skinned source into separate review assets.</summary>
    public static class ProductionCharacterBaker
    {
        private const string Output = "Assets/Resources/ProductionCharacters";

        [MenuItem("SanGuo/建立關羽品質樣板")]
        public static void BakeSample()
        {
            Directory.CreateDirectory(Output + "/Meshes");
            Directory.CreateDirectory(Output + "/Materials");
            AssetDatabase.Refresh();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Characters/guanyu.prefab");
            if (source == null) throw new InvalidOperationException("Missing textured Guanyu source.");
            var root = UnityEngine.Object.Instantiate(source);
            try
            {
                root.name = "guanyu";
                int triangles = 0;
                foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer.sharedMesh == null) continue;
                    string meshPath = Output + "/Meshes/guanyu_" + renderer.name + ".asset";
                    var refined = Refine(renderer.sharedMesh, renderer.name == "FaceRenderer" ? 2 : 1);
                    refined.name = "Guanyu_" + renderer.name + "_Refined";
                    triangles += refined.triangles.Length / 3;
                    SaveAsset(refined, meshPath);
                    renderer.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    renderer.updateWhenOffscreen = true;
                    var mats = renderer.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        mats[i] = Surface(mats[i], "guanyu_" + renderer.name + "_" + i, renderer.name == "BodyRenderer");
                    renderer.sharedMaterials = mats;
                }
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = renderer.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = Surface(mats[i], "guanyu_weapon_" + i, false);
                    renderer.sharedMaterials = mats;
                }
                var clips = root.GetComponent<CharacterClipSet>();
                if (clips == null || clips.Idle == null || clips.Attack == null)
                    throw new InvalidOperationException("Sample requires an actual skeleton and animation clips.");
                clips.MotionProfile = "polearm";
                clips.HeadScale = 1.22f;
                // The source model is roughly 1.2m high; the board uses a consistent ~2.3 unit silhouette.
                root.transform.localScale = Vector3.one * 1.90f;
                PrefabUtility.SaveAsPrefabAsset(root, Output + "/guanyu.prefab");
                AssetDatabase.SaveAssets();
                Debug.Log("PRODUCTION_SAMPLE guanyu triangles=" + triangles + " skinned=true textured=true");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static Material Surface(Material? source, string name, bool body)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/Shaders/CharacterSurface.shader");
            if (shader == null) throw new InvalidOperationException("Missing character surface shader.");
            var mat = new Material(shader) { name = name };
            Texture? texture = source != null && source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : null;
            if (texture == null && source != null && source.HasProperty("_MainTex")) texture = source.GetTexture("_MainTex");
            if (body)
            {
                var painted = AssetDatabase.LoadAssetAtPath<Texture2D>(Output + "/Textures/guanyu_body.png");
                if (painted != null) texture = painted;
            }
            foreach (string slot in new[] { "Hair", "Face" })
                if (name.Contains(slot+"Renderer"))
                {
                    var painted=AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/guanyu_"+slot.ToLowerInvariant()+".png");
                    if(painted!=null) texture=painted;
                }
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", name.Contains("CosmeticRenderer") ? new Color(.23f,.23f,.25f) : Color.white);
            mat.SetFloat("_Smoothness", name.Contains("Face") ? .22f : .42f);
            mat.SetFloat("_MetalStrength", name.Contains("Face") ? .12f : .60f);
            string path = Output + "/Materials/" + name + ".mat";
            SaveAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static void SaveAsset(UnityEngine.Object asset, string path)
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null) AssetDatabase.CreateAsset(asset, path);
            else { EditorUtility.CopySerialized(asset, existing); UnityEngine.Object.DestroyImmediate(asset); }
        }

        private readonly struct Edge : IEquatable<Edge>
        {
            public readonly int A, B;
            public Edge(int a, int b) { A = Math.Min(a, b); B = Math.Max(a, b); }
            public bool Equals(Edge e) => A == e.A && B == e.B;
            public override bool Equals(object? o) => o is Edge e && Equals(e);
            public override int GetHashCode() => unchecked(A * 397 ^ B);
        }

        private static Mesh Refine(Mesh source, int levels)
        {
            Mesh mesh = UnityEngine.Object.Instantiate(source);
            for (int pass = 0; pass < levels; pass++)
            {
                var next = Subdivide(mesh);
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = next;
            }
            return mesh;
        }

        private static Mesh Subdivide(Mesh source)
        {
            var pos = source.vertices; var uv = source.uv; var weights = source.boneWeights;
            bool skinned = weights.Length == pos.Length;
            var vertices = pos.ToList();
            var texcoords = (uv.Length == pos.Length ? uv : new Vector2[pos.Length]).ToList();
            var skin = (skinned ? weights : new BoneWeight[pos.Length]).ToList();
            var adjacent = new Dictionary<Edge, List<int>>();
            var neighbors = Enumerable.Range(0, pos.Length).Select(_ => new HashSet<int>()).ToArray();
            // Weld topology for smoothing across UV seams while retaining separate UV vertices.
            var weld = new int[pos.Length]; var welded = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < pos.Length; i++)
            {
                var p = pos[i]; var key = new Vector3Int(Mathf.RoundToInt(p.x*100000), Mathf.RoundToInt(p.y*100000), Mathf.RoundToInt(p.z*100000));
                if (!welded.TryGetValue(key, out int index)) { index = i; welded.Add(key, i); }
                weld[i] = index;
            }
            void AddEdge(int a, int b, int opposite)
            {
                a=weld[a]; b=weld[b]; opposite=weld[opposite];
                if(a==b) return;
                var edge = new Edge(a,b);
                if (!adjacent.TryGetValue(edge,out var list)) { list=new List<int>(); adjacent.Add(edge,list); }
                if(!list.Contains(opposite)) list.Add(opposite);
                neighbors[a].Add(b); neighbors[b].Add(a);
            }
            for(int s=0;s<source.subMeshCount;s++)
            {
                var t=source.GetTriangles(s);
                for(int i=0;i<t.Length;i+=3) { AddEdge(t[i],t[i+1],t[i+2]); AddEdge(t[i+1],t[i+2],t[i]); AddEdge(t[i+2],t[i],t[i+1]); }
            }
            for(int i=0;i<pos.Length;i++)
            {
                int vi=weld[i]; var ns=neighbors[vi]; if(ns.Count<3) continue;
                var boundary=ns.Where(n=>adjacent[new Edge(vi,n)].Count==1).ToArray();
                Vector3 smooth;
                if(boundary.Length==2) smooth=pos[vi]*.75f+(pos[boundary[0]]+pos[boundary[1]])*.125f;
                else
                {
                    float beta=ns.Count==3 ? 3f/16f : 3f/(8f*ns.Count);
                    smooth=pos[vi]*(1-ns.Count*beta);
                    foreach(int n in ns) smooth+=pos[n]*beta;
                }
                // A partial Loop step rounds the faceting without collapsing armor silhouettes.
                vertices[i]=Vector3.Lerp(pos[i],smooth,.45f);
            }
            var mids = new Dictionary<Edge,int>();
            int Mid(int a,int b)
            {
                var edge=new Edge(a,b);
                if(mids.TryGetValue(edge,out int index)) return index;
                Vector3 p=(pos[a]+pos[b])*.5f;
                if(adjacent.TryGetValue(new Edge(weld[a],weld[b]),out var opp) && opp.Count==2)
                {
                    Vector3 smooth=(pos[a]+pos[b])*.375f+(pos[opp[0]]+pos[opp[1]])*.125f;
                    p=Vector3.Lerp(p,smooth,.45f);
                }
                index=vertices.Count;vertices.Add(p);texcoords.Add((texcoords[a]+texcoords[b])*.5f);
                skin.Add(skinned?Blend(weights[a],weights[b]):default);mids.Add(edge,index);return index;
            }
            var triangles=new List<int>[source.subMeshCount];
            for(int s=0;s<source.subMeshCount;s++)
            {
                triangles[s]=new List<int>();var t=source.GetTriangles(s);
                for(int i=0;i<t.Length;i+=3)
                {
                    int a=t[i],b=t[i+1],c=t[i+2],ab=Mid(a,b),bc=Mid(b,c),ca=Mid(c,a);
                    triangles[s].AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});
                }
            }
            var result=new Mesh {name=source.name+"_Surface",indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            result.SetVertices(vertices);result.SetUVs(0,texcoords);result.subMeshCount=source.subMeshCount;
            for(int s=0;s<triangles.Length;s++) result.SetTriangles(triangles[s],s);
            if(skinned) { result.boneWeights=skin.ToArray();result.bindposes=source.bindposes; }
            result.RecalculateNormals();result.RecalculateTangents();result.RecalculateBounds();
            return result;
        }

        private static BoneWeight Blend(BoneWeight a,BoneWeight b)
        {
            var totals=new Dictionary<int,float>();
            void Add(int bone,float weight) { if(weight<=0) return; totals[bone]=totals.TryGetValue(bone,out float w)?w+weight*.5f:weight*.5f; }
            foreach(var v in new[]{a,b}) { Add(v.boneIndex0,v.weight0);Add(v.boneIndex1,v.weight1);Add(v.boneIndex2,v.weight2);Add(v.boneIndex3,v.weight3); }
            var sorted=totals.OrderByDescending(k=>k.Value).Take(4).ToArray();float sum=sorted.Sum(k=>k.Value);var w=new BoneWeight();
            for(int i=0;i<sorted.Length;i++)
            {
                int bone=sorted[i].Key;float value=sorted[i].Value/sum;
                if(i==0){w.boneIndex0=bone;w.weight0=value;}else if(i==1){w.boneIndex1=bone;w.weight1=value;}else if(i==2){w.boneIndex2=bone;w.weight2=value;}else{w.boneIndex3=bone;w.weight3=value;}
            }
            return w;
        }
    }
}
