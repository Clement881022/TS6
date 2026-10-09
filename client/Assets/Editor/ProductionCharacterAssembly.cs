#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SanGuo.Client.Editor
{
    /// <summary>Art export step: consolidate rigid equipment per bone and surface.</summary>
    public static class ProductionCharacterAssembly
    {
        public static void Consolidate(GameObject root,string id,string output)
        {
            var anchors=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Design_") || t.name=="PropBow" || t.name=="PropArrow" || t.name=="PropShield").ToArray();
            int before=root.GetComponentsInChildren<Renderer>().Length;
            foreach(var anchor in anchors)
            {
                var parts=anchor.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponent<MeshFilter>()!=null).ToArray();
                var groups=parts.GroupBy(r=>SurfaceKey(r.sharedMaterial)).ToArray();int index=0;
                foreach(var group in groups)
                {
                    string name=id+"_"+anchor.name+"_Surface"+index++;
                    var mesh=new Mesh{name=name};
                    mesh.CombineMeshes(group.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=anchor.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray(),true,true);
                    string path=output+"/Meshes/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
                    var go=new GameObject(name);go.transform.SetParent(anchor,false);go.AddComponent<MeshFilter>().sharedMesh=saved;
                    go.AddComponent<MeshRenderer>().sharedMaterial=group.First().sharedMaterial;
                }
                foreach(var part in parts)if(part!=null)UnityEngine.Object.DestroyImmediate(part.gameObject);
            }
            Debug.Log("ART_ASSEMBLY "+id+" renderers="+before+" -> "+root.GetComponentsInChildren<Renderer>().Length);
        }

        public static void Validate(GameObject root)
        {
            var meshes=root.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.sharedMesh)
                .Concat(root.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh));
            foreach(var mesh in meshes)
            {
                if(mesh==null)throw new InvalidOperationException("Missing art mesh: "+root.name);
                foreach(var p in mesh.vertices)
                    if(!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z))
                        throw new InvalidOperationException("Invalid art vertex: "+root.name+" / "+mesh.name);
                foreach(int i in mesh.triangles)
                    if(i<0 || i>=mesh.vertexCount)throw new InvalidOperationException("Invalid art triangle: "+mesh.name);
            }
        }

        private static string SurfaceKey(Material m)=>AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap"))+"|"+m.GetColor("_BaseColor").ToString("F5")+"|"+m.GetFloat("_MetalStrength").ToString("R")+"|"+m.GetFloat("_Smoothness").ToString("R");
    }
}
