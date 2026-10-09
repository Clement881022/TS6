#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SanGuo.Client.Editor
{
    public static class ProductionInfantryCostume
    {
        public static void Apply(GameObject root,string id,string output)
        {
            if(id!="r_shield" && id!="r_archer" && id!="r_sword" && !ProductionInfantryDesign.IsBandit(id))return;
            var r=root.GetComponentsInChildren<SkinnedMeshRenderer>().First(x=>x.name=="BodyRenderer");
            var mesh=UnityEngine.Object.Instantiate(r.sharedMesh);var vertices=mesh.vertices;var weights=mesh.boneWeights;var uv=mesh.uv;
            var matrices=r.bones.Select((b,i)=>b.localToWorldMatrix*mesh.bindposes[i]).ToArray();
            int pelvis=Array.FindIndex(r.bones,b=>b.name=="Bip001 Pelvis");
            int leftThigh=Array.FindIndex(r.bones,b=>b.name=="Bip001 L Thigh"),rightThigh=Array.FindIndex(r.bones,b=>b.name=="Bip001 R Thigh");
            for(int i=0;i<vertices.Length;i++)
            {
                var skin=SkinMatrix(weights[i],matrices);var p=skin.MultiplyPoint3x4(vertices[i]);
                int primary=weights[i].weight0>=weights[i].weight1?weights[i].boneIndex0:weights[i].boneIndex1;
                string boneName=r.bones[primary].name;
                bool lowerGarment=boneName.Contains("Thigh") || boneName.Contains("Calf") || boneName.Contains("Foot") || boneName.Contains("Pelvis") || boneName.Contains("Spine")
                    || boneName.StartsWith("Bip001-SL") || boneName.StartsWith("Bip001-SR") || boneName.StartsWith("Bip001-SF") || boneName.StartsWith("Bip001-SB");
                // A lowered sleeve may occupy the same height and atlas band as the hem.
                // Never transfer arm or hand vertices to the pelvis when shortening the skirt.
                if(lowerGarment && p.y<.33f && uv[i].y<.65f)
                {
                    float blend=Mathf.Clamp01((.33f-p.y)/.16f);
                    p.y=.18f+p.y*.45f;p.x*=Mathf.Lerp(1,.65f,blend);p.z*=Mathf.Lerp(1,.65f,blend);
                    if(pelvis>=0 && leftThigh>=0 && rightThigh>=0)
                    {
                        int leg=(p-r.bones[leftThigh].position).sqrMagnitude<(p-r.bones[rightThigh].position).sqrMagnitude?leftThigh:rightThigh;
                        weights[i]=new BoneWeight{boneIndex0=pelvis,weight0=.75f,boneIndex1=leg,weight1=.25f};skin=SkinMatrix(weights[i],matrices);
                    }
                    vertices[i]=skin.inverse.MultiplyPoint3x4(p);
                }
                bool cuff=boneName.Contains("-LHC") || boneName.Contains("-RHC");
                if((boneName.Contains("Forearm") || cuff) && !(uv[i].x<.22f && uv[i].y<.30f))
                {
                    string side=boneName.Contains(" L ") || boneName.Contains("-LHC")?"L":"R";
                    var elbow=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip001 "+side+" Forearm").position;var hand=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip001 "+side+" Hand").position;
                    var line=hand-elbow;float t=Mathf.Clamp01(Vector3.Dot(p-elbow,line)/line.sqrMagnitude);
                    var centre=elbow+line*t;var delta=p-centre;
                    if(delta.magnitude>.055f)
                    {
                        p=centre+delta.normalized*.055f;
                        if(cuff)
                        {
                            int forearm=Array.FindIndex(r.bones,b=>b.name=="Bip001 "+side+" Forearm");
                            int wrist=Array.FindIndex(r.bones,b=>b.name=="Bip001 "+side+" Hand");
                            weights[i]=new BoneWeight{boneIndex0=forearm,weight0=1-t*.3f,boneIndex1=wrist,weight1=t*.3f};skin=SkinMatrix(weights[i],matrices);
                        }
                        vertices[i]=skin.inverse.MultiplyPoint3x4(p);
                    }
                }
            }
            mesh.vertices=vertices;mesh.boneWeights=weights;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            EditorUtility.CopySerialized(mesh,r.sharedMesh);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(r.sharedMesh);
            foreach(string side in new[]{"L","R"}){Leg(root,id,side,output);Boot(root,id,side,output);}
        }

        private static void Leg(GameObject root,string id,string side,string output)
        {
            var thigh=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip001 "+side+" Thigh");
            var calf=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip001 "+side+" Calf");
            var foot=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip001 "+side+" Foot");
            var go=new GameObject("Costume_"+side+"Leg");go.transform.SetParent(root.transform,false);
            var v=new List<Vector3>();var uv=new List<Vector2>();var weights=new List<BoneWeight>();var tri=new List<int>();
            const int rings=25,sides=24;
            for(int i=0;i<rings;i++)
            {
                float t=i/(float)(rings-1);bool upper=t<=.5f;float f=upper?t*2:(t-.5f)*2;
                var centre=upper?Vector3.Lerp(thigh.position,calf.position,f):Vector3.Lerp(calf.position,foot.position,f);
                var tangent=upper?(calf.position-thigh.position).normalized:(foot.position-calf.position).normalized;
                var x=Vector3.Cross(tangent,Vector3.forward).normalized;var z=Vector3.Cross(tangent,x).normalized;
                float radius=upper?Mathf.Lerp(.053f,.047f,f):Mathf.Lerp(.047f,.035f,f);
                radius*=1+.045f*Mathf.Sin(t*32);
                for(int j=0;j<=sides;j++)
                {
                    float a=j/(float)sides*Mathf.PI*2;
                    v.Add(root.transform.InverseTransformPoint(centre+(x*Mathf.Cos(a)+z*Mathf.Sin(a))*radius));uv.Add(new Vector2(j/(float)sides,t));
                    weights.Add(new BoneWeight{boneIndex0=upper?0:1,weight0=1-f,boneIndex1=upper?1:2,weight1=f});
                    if(i<rings-1 && j<sides){int k=i*(sides+1)+j;tri.AddRange(new[]{k,k+1,k+sides+1,k+1,k+sides+2,k+sides+1});}
                }
            }
            var mesh=new Mesh{name=id+"_"+side+"Pants"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.boneWeights=weights.ToArray();
            var bones=new[]{thigh,calf,foot};mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            string path=output+"/Meshes/"+mesh.name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);}
            var renderer=go.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=saved;renderer.bones=bones;renderer.rootBone=thigh;renderer.updateWhenOffscreen=true;
            string matPath=output+"/Materials/"+id+"_Pants.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(mat==null){mat=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/Shaders/CharacterSurface.shader"));AssetDatabase.CreateAsset(mat,matPath);}
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(output+"/Textures/"+(id=="r_archer"?"equipment_blue_pants":"equipment_dark_leather")+".png");
            mat.SetTexture("_BaseMap",texture);mat.SetColor("_BaseColor",texture!=null?Color.white:id=="r_archer"?new Color(.08f,.18f,.48f):new Color(.14f,.055f,.035f));mat.SetFloat("_MetalStrength",.1f);EditorUtility.SetDirty(mat);renderer.sharedMaterial=mat;
        }

        private static void Boot(GameObject root,string id,string side,string output)
        {
            var calf=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip001 "+side+" Calf");
            var foot=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip001 "+side+" Foot");
            var go=new GameObject("Design_"+side+"Boot");go.transform.SetParent(root.transform,false);go.transform.SetParent(calf,true);
            var low=foot.position;var high=Vector3.Lerp(foot.position,calf.position,.69f);
            var shell=ProductionCharacterProps.Piece(go.transform,id+side+"BootLeather",ProductionCharacterProps.Tube(new[]{low,Vector3.Lerp(low,high,.2f),high},.050f,24),new Color(.18f,.07f,.027f),output);
            foreach(float t in new[]{.12f,.96f})
            {
                var direction=(high-low).normalized;var p=Vector3.Lerp(low,high,t);
                ProductionCharacterProps.Piece(go.transform,id+side+"BootGold"+t,ProductionCharacterProps.Tube(new[]{p-direction*.005f,p+direction*.005f},.053f,24),new Color(.80f,.52f,.16f),output);
            }
        }

        [MenuItem("SanGuo/檢視鄉勇衣裝底材")]
        public static void Audit()
        {
            foreach(string id in new[]{"r_shield","r_archer"})
            {
                var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ProductionCharacters/"+id+".prefab"));
                root.transform.localScale=Vector3.one;root.GetComponent<CharacterClipSet>().Idle.SampleAnimation(root,0);
                var r=root.GetComponentsInChildren<SkinnedMeshRenderer>().First(x=>x.name=="BodyRenderer");
                var m=r.sharedMesh;var weights=m.boneWeights;var v=m.vertices;
                var matrices=r.bones.Select((b,i)=>b.localToWorldMatrix*m.bindposes[i]).ToArray();
                var positions=v.Select((p,i)=>SkinMatrix(weights[i],matrices).MultiplyPoint3x4(p)).ToArray();
                string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../model-quality/costume-audit"));Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder,id+".json"),JsonUtility.ToJson(new AuditData{vertices=positions,uv=m.uv,triangles=m.triangles,boneNames=r.bones.Select(b=>b.name).ToArray(),bones=r.bones.Select(b=>b.position).ToArray()},true));
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Matrix4x4 SkinMatrix(BoneWeight w,Matrix4x4[] matrices)
        {
            var m=new Matrix4x4();
            for(int i=0;i<16;i++)m[i]=matrices[w.boneIndex0][i]*w.weight0+matrices[w.boneIndex1][i]*w.weight1+matrices[w.boneIndex2][i]*w.weight2+matrices[w.boneIndex3][i]*w.weight3;
            return m;
        }

        [Serializable]private class AuditData
        {
            public Vector3[] vertices=Array.Empty<Vector3>();public Vector2[] uv=Array.Empty<Vector2>();public int[] triangles=Array.Empty<int>();public string[] boneNames=Array.Empty<string>();public Vector3[] bones=Array.Empty<Vector3>();
        }
    }
}
