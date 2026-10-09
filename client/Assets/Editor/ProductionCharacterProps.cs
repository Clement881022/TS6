#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SanGuo.Client.Editor
{
    /// <summary>Separate wearable, articulated props; no geometry is baked into the character's face or body.</summary>
    public static class ProductionCharacterProps
    {
        public static void Bow(GameObject root,string output)
        {
            var hand=Bone(root,"Bip001 L Hand");if(hand == null) throw new InvalidOperationException("Archer has no left hand.");
            var grip=new GameObject("PropBow");grip.transform.SetParent(hand,false);
            grip.transform.localRotation=Quaternion.Euler(0,90,90);
            var points=new List<Vector3>();
            for(int i=0;i<=40;i++)
            {
                float t=i/40f,y=Mathf.Lerp(-.40f,.40f,t);
                float x=.10f*Mathf.Sin(t*Mathf.PI)-.07f*Mathf.Pow(Mathf.Abs(2*t-1),6);
                points.Add(new Vector3(x,y,0));
            }
            Piece(grip.transform,"PropBowWood",Tube(points,.025f,12),new Color(.30f,.12f,.045f),output);
            Piece(grip.transform,"PropBowString",Tube(new[]{points[0],new Vector3(-.035f,0,0),points[^1]},.0025f,6),new Color(.90f,.82f,.58f),output);
            foreach(float y in new[]{-.33f,-.09f,.09f,.33f})
            {
                Vector3 At(float atY) { float t=(atY+.4f)/.8f;return new Vector3(.10f*Mathf.Sin(t*Mathf.PI)-.07f*Mathf.Pow(Mathf.Abs(2*t-1),6),atY,0); }
                Piece(grip.transform,"PropBowBinding"+y.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture),Tube(new[]{At(y-.012f),At(y+.012f)},.028f,12),new Color(.87f,.61f,.20f),output);
            }
            var right=Bone(root,"Bip001 R Hand");
            if(right!=null)
            {
                var arrow=new GameObject("PropArrow");arrow.transform.SetParent(right,false);arrow.transform.localRotation=Quaternion.Euler(0,90,0);
                Piece(arrow.transform,"PropArrowShaft",Tube(new[]{new Vector3(0,0,-.04f),new Vector3(0,0,.43f)},.005f,6),new Color(.26f,.13f,.06f),output);
                var tip=Piece(arrow.transform,"PropArrowTip",Plate(new[]{new Vector2(0,.07f),new Vector2(-.022f,0),new Vector2(.022f,0)},.002f),new Color(.72f,.79f,.83f),output);
                tip.localPosition=new Vector3(0,0,.43f);tip.localRotation=Quaternion.Euler(90,0,0);
                for(int i=0;i<3;i++)
                {
                    var feather=Piece(arrow.transform,"PropArrowFeather"+i,Plate(new[]{new Vector2(0,.05f),new Vector2(.025f,0),new Vector2(.025f,-.04f),new Vector2(0,-.06f)},.001f),new Color(.85f,.88f,.80f),output);
                    feather.localPosition=new Vector3(0,0,.01f);feather.localRotation=Quaternion.Euler(90,0,i*120);
                }
            }
        }

        public static void Shield(GameObject root,string output)
        {
            var hand=Bone(root,"Bip001 L Hand");if(hand == null) throw new InvalidOperationException("Guard has no left hand.");
            var grip=new GameObject("PropShield");grip.transform.SetParent(hand,false);
            grip.transform.localRotation=Quaternion.Euler(90,0,90)*Quaternion.Euler(0,180,0);
            grip.transform.localPosition=grip.transform.localRotation*Vector3.forward*.15f;
            var contour=new[]{new Vector2(-.19f,.27f),new Vector2(-.13f,.31f),new Vector2(.13f,.31f),new Vector2(.19f,.27f),new Vector2(.22f,-.08f),new Vector2(.16f,-.22f),new Vector2(0,-.33f),new Vector2(-.16f,-.22f),new Vector2(-.22f,-.08f)};
            Piece(grip.transform,"PropShieldRim",Plate(contour,.045f),new Color(.87f,.61f,.20f),output);
            var inner=contour.Select(p=>p*.88f).ToArray();
            var face=Piece(grip.transform,"PropShieldFace",Plate(inner,.045f),new Color(.35f,.055f,.065f),output);
            face.localPosition=Vector3.forward*.012f;
            var back=Piece(grip.transform,"PropShieldBack",Plate(inner,.02f),new Color(.20f,.10f,.045f),output);
            back.localPosition=Vector3.back*.055f;
            foreach(float x in new[]{-.065f,.065f})
                Piece(grip.transform,"PropShieldStrap"+x.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture),Tube(new[]{new Vector3(x,-.11f,-.08f),new Vector3(x,-.06f,-.13f),new Vector3(x,.06f,-.13f),new Vector3(x,.11f,-.08f)},.013f,8),new Color(.13f,.06f,.03f),output);
            foreach(int i in new[]{0,2,4,6,8})
            {
                var p=contour[i]*.88f;
                Piece(grip.transform,"PropShieldRivet"+i,Tube(new[]{new Vector3(p.x,p.y,.045f),new Vector3(p.x,p.y,.07f)},.012f,12),new Color(.95f,.69f,.25f),output);
            }
            Piece(grip.transform,"PropShieldBoss",Tube(new[]{new Vector3(0,0,.035f),new Vector3(0,0,.050f)},.010f,20),new Color(.87f,.61f,.20f),output);
        }

        private static Transform? Bone(GameObject root,string name)=>root.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name == name);

        public static Transform Piece(Transform parent,string name,Mesh mesh,Color color,string output)
        {
            string meshPath=output+"/Meshes/"+name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(existing==null){AssetDatabase.CreateAsset(mesh,meshPath);existing=mesh;}
            else{EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);}
            string materialPath=output+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null)
            {
                material=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/Shaders/CharacterSurface.shader"));
                AssetDatabase.CreateAsset(material,materialPath);
            }
            // 既有材質也要套用目前顏色，否則改色後重新烘焙仍沿用舊色。
            material.SetColor("_BaseColor",color);material.SetFloat("_MetalStrength",name.Contains("Cloth") || name.Contains("Hair") || name.Contains("Plume") || name.Contains("Tassel") || name.Contains("Leather") ? .12f:.75f);
            string? surface = name=="ShieldHelmetDome" ? "equipment_helmet" : name.Contains("BootLeather") || name=="ArcherQuiverLeather" ? "equipment_dark_leather" : name.Contains("Cloth") ? "equipment_blue_silk" : name.Contains("HairCap") || name.Contains("HairLock") || name.Contains("HairBackLock") || name.Contains("HairTemple") ? "equipment_hair" : name=="PropShieldFace" ? "equipment_shield" : name=="HealerMedicineGourd" ? "equipment_gourd" : null;
            var painted=surface==null?null:AssetDatabase.LoadAssetAtPath<Texture2D>(output+"/Textures/"+surface+".png");
            if(painted!=null){material.SetTexture("_BaseMap",painted);material.SetColor("_BaseColor",Color.white);}
            EditorUtility.SetDirty(material);
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=existing;go.AddComponent<MeshRenderer>().sharedMaterial=material;
            return go.transform;
        }

        public static Mesh Plate(Vector2[] contour,float thickness)
        {
            int n=contour.Length;var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int side=0;side<2;side++)
                foreach(var p in contour){v.Add(new Vector3(p.x,p.y,side == 0 ? -thickness : thickness));uv.Add(p+Vector2.one*.5f);}
            v.Add(new Vector3(0,0,-thickness));uv.Add(Vector2.one*.5f);v.Add(new Vector3(0,0,thickness+.012f));uv.Add(Vector2.one*.5f);
            for(int i=0;i<n;i++)
            {
                int j=(i+1)%n;
                t.AddRange(new[]{2*n,i,j,2*n+1,j+n,i+n,i,i+n,j+n,i,j+n,j});
            }
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateTangents();return mesh;
        }

        public static Mesh Tube(IReadOnlyList<Vector3> points,float radius,int sides)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int i=0;i<points.Count;i++)
            {
                var tangent=(points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)]).normalized;
                var x=Vector3.Cross(tangent,Mathf.Abs(Vector3.Dot(tangent,Vector3.forward))>.9f?Vector3.up:Vector3.forward).normalized;
                var y=Vector3.Cross(tangent,x).normalized;
                float taper=points.Count>10?Mathf.Lerp(.40f,1,Mathf.Sin(i/(float)(points.Count-1)*Mathf.PI)) : 1;
                for(int j=0;j<sides;j++)
                {
                    float a=j/(float)sides*Mathf.PI*2;v.Add(points[i]+(x*Mathf.Cos(a)+y*Mathf.Sin(a))*radius*taper);uv.Add(new Vector2(j/(float)sides,i/(float)(points.Count-1)));
                    if(i<points.Count-1){int k=i*sides+j,n=i*sides+(j+1)%sides;t.AddRange(new[]{k,n,k+sides,n,n+sides,k+sides});}
                }
            }
            int start=v.Count;v.Add(points[0]);uv.Add(Vector2.one*.5f);int end=v.Count;v.Add(points[^1]);uv.Add(Vector2.one*.5f);
            for(int j=0;j<sides;j++){int n=(j+1)%sides;t.AddRange(new[]{start,n,j,end,(points.Count-1)*sides+j,(points.Count-1)*sides+n});}
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateTangents();return mesh;
        }
    }
}
