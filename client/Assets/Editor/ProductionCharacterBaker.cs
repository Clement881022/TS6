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
            Prepare();
            Bake("guanyu", "polearm");
            Finish();
        }

        [MenuItem("SanGuo/建立骨架戰鬥角色組")]
        public static void BakeBattleSet()
        {
            Prepare();
            foreach (string path in Directory.GetFiles("Assets/Resources/Characters", "*.prefab"))
            {
                string id=Path.GetFileNameWithoutExtension(path);
                if(id == "badou") continue;
                string role=id.Contains("archer") || id.Contains("marksman") || id.Contains("sharpshooter") || id == "huangzhong" ? "archer"
                    : id.Contains("shaman") || id.Contains("warlock") || id.Contains("priest") || id == "r_healer" || id == "zhugeliang" || id == "pangtong" || id.Contains("zhangjiao") || id == "r_villager" ? "caster"
                    : id.Contains("shield") || id.Contains("ironbrute") || id == "yt_brute" ? "guard"
                    : id == "guanyu" || id == "zhangfei" || id == "zhaoyun" ? "polearm" : "sword";
                Bake(id,role);
            }
            Finish();
        }

        [MenuItem("SanGuo/重製鄉勇造型")]
        public static void BakeInfantryArt()
        {
            Prepare();
            Bake("r_shield","guard");Bake("r_archer","archer");Bake("r_healer","caster");
            Finish();
        }

        [MenuItem("SanGuo/建立鄉勇劍兵")]
        public static void BakeSwordArt()
        {
            Prepare();Bake("r_sword","sword");Finish();
        }

        [MenuItem("SanGuo/統一實際角色骨架尺寸")]
        public static void NormalizeWorldSizes()
        {
            Prepare();
            foreach(string path in Directory.GetFiles(Output,"*.prefab"))
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);var root=UnityEngine.Object.Instantiate(prefab);
                try
                {
                    root.name=Path.GetFileNameWithoutExtension(path);
                    var clips=root.GetComponent<CharacterClipSet>();
                    if(clips==null || clips.Idle==null)throw new InvalidOperationException("Missing skeletal idle: "+path);
                    clips.Idle.SampleAnimation(root,0);Normalize(root,clips);ProductionCharacterAssembly.Validate(root);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            Finish();
        }

        private static void Prepare()
        {
            Directory.CreateDirectory(Output + "/Meshes");
            Directory.CreateDirectory(Output + "/Materials");
            AssetDatabase.Refresh();
        }

        private static void Finish()
        {
            AssetDatabase.SaveAssets();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void Bake(string id,string role)
        {
            string sourceId=id=="r_sword"?"r_shield":id;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Characters/"+sourceId+".prefab");
            if (source == null) throw new InvalidOperationException("Missing textured character source: "+id);
            var root = UnityEngine.Object.Instantiate(source);
            try
            {
                root.name = id;
                ProductionInfantryDesign.PrepareParts(root,id);
                int triangles = 0;
                foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer.sharedMesh == null) continue;
                    string meshPath = Output + "/Meshes/"+id+"_" + renderer.name + ".asset";
                    var refined = Refine(renderer.sharedMesh, 1);
                    if(renderer.name == "FaceRenderer") SculptFace(refined);
                    refined.name = id+"_" + renderer.name + "_Refined";
                    triangles += refined.triangles.Length / 3;
                    SaveAsset(refined, meshPath);
                    renderer.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    renderer.updateWhenOffscreen = true;
                    var mats = renderer.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        mats[i] = Surface(mats[i], id, renderer.name, i);
                    renderer.sharedMaterials = mats;
                }
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = renderer.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = Surface(mats[i], id, "weapon", i);
                    renderer.sharedMaterials = mats;
                }
                var clips = root.GetComponent<CharacterClipSet>();
                if (clips == null || clips.Idle == null || clips.Attack == null)
                    throw new InvalidOperationException("Sample requires an actual skeleton and animation clips.");
                clips.MotionProfile = role;
                clips.HeadScale = role == "caster" ? 1.26f : 1.22f;
                string clipPath=AssetDatabase.GetAssetPath(clips.Idle);
                string motion=role == "archer" ? "Farfight1" : role == "caster" ? "Magic" : role == "guard" ? "Fight2" : role == "polearm" ? "Fight3" : "Fight1";
                // 待機檔名不含 FightStandby 時，替換會原樣回傳待機檔，攻擊就被換成待機動作。
                var attack=!clipPath.Contains("FightStandby") ? null : AssetDatabase.LoadAllAssetsAtPath(clipPath.Replace("FightStandby",motion)).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview__"));
                if(attack != null) clips.Attack=attack;
                if(id == "r_shield" || id == "r_archer" || id == "r_healer" || id == "r_sword") clips.Idle.SampleAnimation(root,0);
                ProductionInfantryCostume.Apply(root,id,Output);
                if(role == "archer") { ProductionCharacterProps.Bow(root,Output);root.AddComponent<ArcherPoseRig>(); }
                if(role == "guard") ProductionCharacterProps.Shield(root,Output);
                ProductionInfantryDesign.AddDesign(root,id,Output);
                ProductionArcherAnimation.Apply(root,id,Output);
                if(id == "r_shield" || id == "r_archer" || id == "r_healer" || id == "r_sword") ProductionCharacterAssembly.Consolidate(root,id,Output);
                if(id == "guanyu")
                    foreach(var t in root.GetComponentsInChildren<Transform>())
                        if(t.name == "Weapon_00029") t.localRotation = Quaternion.Euler(0,180,0);
                Normalize(root,clips);
                ProductionCharacterAssembly.Validate(root);
                PrefabUtility.SaveAsPrefabAsset(root, Output + "/"+id+".prefab");
                AssetDatabase.SaveAssets();
                Debug.Log("PRODUCTION_CHARACTER "+id+" triangles=" + triangles + " motion="+role+" skinned=true textured=true");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Normalize(GameObject root,CharacterClipSet clips)
        {
                var head=root.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Bip001 Head");
                var originalHeadScale=head!=null?head.localScale:Vector3.one;
                if(head!=null)head.localScale=originalHeadScale*clips.HeadScale;
                var bodyBounds=new Bounds();bool first=true;
                foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if(r.name.Contains("Weapon") || r.sharedMesh==null) continue;
                    var bounds=ProductionInfantryDesign.SkinBounds(root,r.name);
                    if(first){bodyBounds=bounds;first=false;}else bodyBounds.Encapsulate(bounds);
                }
                if(head!=null)head.localScale=originalHeadScale;
                if(first || bodyBounds.size.y<.05f)throw new InvalidOperationException("Empty posed character geometry: "+root.name);
                root.transform.localScale *= 2.30f/bodyBounds.size.y;
                Debug.Log("ART_WORLD_SIZE "+root.name+" posedHeight="+bodyBounds.size.y+" scale="+root.transform.localScale.x+" targetBodyHeight=2.3");
        }

        private static void SculptFace(Mesh mesh)
        {
            var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
            for(int i=0;i<vertices.Length;i++)
            {
                // Round the cheek volume in the main facial UV island; nose remains compact.
                float cheek=0;
                foreach(float x in new[]{.27f,.73f})
                {
                    float u=(uv[i].x-x)/.13f,v=(uv[i].y-.52f)/.11f;
                    cheek+=Mathf.Exp(-(u*u+v*v)*2f);
                }
                var p=vertices[i];p.x*=1.06f;
                vertices[i]=p+normals[i]*(cheek*.0013f);
            }
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
        }

        private static Material Surface(Material? source, string id, string part, int index)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/Shaders/CharacterSurface.shader");
            if (shader == null) throw new InvalidOperationException("Missing character surface shader.");
            string name = id + "_" + part + "_" + index;
            var mat = new Material(shader) { name = name };
            Texture? texture = source != null && source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : null;
            if (texture == null && source != null && source.HasProperty("_MainTex")) texture = source.GetTexture("_MainTex");
            if (part == "BodyRenderer")
            {
                var painted = AssetDatabase.LoadAssetAtPath<Texture2D>(Output + "/Textures/"+id+"_body.png");
                if(painted==null && id=="r_sword")painted=AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/r_shield_body.png");
                if (painted != null) texture = painted;
            }
            foreach (string slot in new[] { "Hair", "Face", "Cosmetic", "Weapon" })
                if (part == slot+"Renderer")
                {
                    var painted=AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/"+id+"_"+slot.ToLowerInvariant()+".png");
                    if(painted==null && id=="r_sword" && slot=="Face")painted=AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/r_shield_face.png");
                    if(painted!=null) texture=painted;
                }
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", part == "FaceRenderer" ? .22f : .42f);
            mat.SetFloat("_MetalStrength", part == "FaceRenderer" ? .12f : .60f);
            mat.SetFloat("_ReliefStrength", part == "BodyRenderer" || part == "WeaponRenderer" ? .65f : 0);
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
