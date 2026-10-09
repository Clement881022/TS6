#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace SanGuo.Client.Editor
{
    public static class ProductionZhaoYun
    {
        private const string Output="Assets/Resources/ProductionCharacters";
        private static readonly Color Silver=new Color(.72f,.82f,.88f),Gold=new Color(.88f,.60f,.20f),Blue=new Color(.03f,.19f,.52f);
        public static void Bake()
        {
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/zhaoyun.prefab"));
            try
            {
                root.name="zhaoyun";root.transform.localScale=Vector3.one;var set=root.GetComponent<CharacterClipSet>();set.Idle!.SampleAnimation(root,0);
                foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Weapon_") || t.name.StartsWith("Design_Zhao")).ToArray())if(t!=null)UnityEngine.Object.DestroyImmediate(t.gameObject);
                var hair=root.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(r=>r.name=="HairRenderer");if(hair!=null)UnityEngine.Object.DestroyImmediate(hair.gameObject);
                Surface(root,"BodyRenderer","zhaoyun_body",.42f);Surface(root,"FaceRenderer","zhaoyun_face",.03f);
                Helmet(root);
                var prop=new GameObject("Design_ZhaoSpear").transform;prop.SetParent(Bone(root,"Bip001 R Hand"),false);Spear(prop);
                set.Idle=ProductionGuanYuGlaive.Author(root,set.Idle!,"Idle",false,false,prop.name,true);
                set.Attack=ProductionGuanYuGlaive.Author(root,set.Attack!,"Attack",true,false,prop.name,true);
                set.Cast=ProductionGuanYuGlaive.Author(root,set.Cast!,"Cast",false,false,prop.name,true);
                set.Hit=ProductionGuanYuGlaive.Author(root,set.Hit!,"Hit",false,true,prop.name,true);
                set.Idle.SampleAnimation(root,0);ProductionCharacterBaker.Normalize(root,set);ProductionCharacterAssembly.Validate(root);
                PrefabUtility.SaveAsPrefabAsset(root,Output+"/zhaoyun.prefab");AssetDatabase.SaveAssets();
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        private static Transform Bone(GameObject root,string name)=>root.GetComponentsInChildren<Transform>().First(t=>t.name==name);
        private static Transform Anchor(GameObject root,string bone,string name){var t=new GameObject(name).transform;t.SetParent(root.transform,false);t.SetParent(Bone(root,bone),true);return t;}
                private static Transform Piece(Transform parent,string name,Mesh mesh,Color color)
        {
            var part=ProductionCharacterProps.Piece(parent,"Zhao"+name,mesh,color,Output);
            if(color==Silver){var mat=part.GetComponent<MeshRenderer>().sharedMaterial;mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/zhaoyun_silver_engraving.png"));mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_MetalStrength",.65f);mat.SetFloat("_Smoothness",.5f);EditorUtility.SetDirty(mat);}return part;
        }
        private static Transform Tube(Transform parent,string name,IReadOnlyList<Vector3> points,float radius,Color color,int sides=24)=>Piece(parent,name,ProductionCharacterProps.Tube(points,radius,sides),color);
        private static void Surface(GameObject root,string renderer,string texture,float metal)
        {
            var m=root.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name==renderer).sharedMaterial;m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/"+texture+".png"));m.SetColor("_BaseColor",Color.white);m.SetFloat("_MetalStrength",metal);m.SetFloat("_Smoothness",.45f);EditorUtility.SetDirty(m);
        }
        private static void Helmet(GameObject root)
        {
            var face=ProductionInfantryDesign.SkinBounds(root,"FaceRenderer");var anchor=Anchor(root,"Bip001 Head","Design_ZhaoHelmet");
            var centre=face.center+Vector3.up*(face.size.y*.25f);float rx=face.size.x*.55f,ry=face.size.y*.52f,rz=face.size.z*.57f;
            const int rows=24,cols=64;var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
            Vector3 At(float a,float t){float theta=t*(1.56f+.27f*(1-Mathf.Sin(a)));return centre+new Vector3(rx*Mathf.Sin(theta)*Mathf.Cos(a),ry*Mathf.Cos(theta),rz*Mathf.Sin(theta)*Mathf.Sin(a));}
            for(int i=0;i<=rows;i++)for(int j=0;j<=cols;j++)
            {
                v.Add(At(j/(float)cols*Mathf.PI*2,i/(float)rows));uv.Add(new Vector2(j/(float)cols,i/(float)rows));
                if(i<rows && j<cols){int k=i*(cols+1)+j;tri.AddRange(new[]{k,k+1,k+cols+1,k+1,k+cols+2,k+cols+1});}
            }
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();Piece(anchor,"HelmetSilverDome",mesh,Silver);
            var hem=new List<Vector3>();for(int j=0;j<=96;j++)hem.Add(At(j/96f*Mathf.PI*2,1));Tube(anchor,"HelmetGoldHem",hem,.009f,Gold);
            foreach(float a in new[]{.35f,Mathf.PI-.35f}){var rib=new List<Vector3>();for(int i=0;i<=32;i++)rib.Add(At(a,i/32f)+Vector3.forward*.003f);Tube(anchor,"HelmetGoldRib"+a,rib,.005f,Gold);}
            foreach(int sign in new[]{-1,1})
            {
                var cheek=Piece(anchor,"HelmetCheek"+sign,ProductionCharacterProps.Plate(new[]{new Vector2(-.043f,.07f),new Vector2(.042f,.07f),new Vector2(.051f,-.06f),new Vector2(0,-.12f),new Vector2(-.05f,-.075f)},.008f),Silver);
                cheek.localPosition=centre+new Vector3(sign*rx,-.035f,.020f);cheek.localRotation=Quaternion.Euler(0,sign*70,0);
                Tube(cheek,"CheekGoldInlay"+sign,new[]{new Vector3(-.028f,.045f,.01f),new Vector3(.02f,.04f,.01f),new Vector3(.029f,-.045f,.01f),new Vector3(0,-.078f,.01f)},.004f,Gold,12);
            }
            var crest=Piece(anchor,"HelmetBlueCrest",ProductionCharacterProps.Plate(new[]{new Vector2(0,.075f),new Vector2(.06f,.01f),new Vector2(0,-.025f),new Vector2(-.06f,.01f)},.010f),Gold);crest.localPosition=centre+new Vector3(0,.05f,rz+.008f);
            var gem=Piece(crest,"HelmetCrestGem",ProductionCharacterProps.Plate(new[]{new Vector2(0,.038f),new Vector2(.021f,0),new Vector2(0,-.017f),new Vector2(-.021f,0)},.008f),Blue);gem.localPosition=new Vector3(0,.016f,.024f);
            var dragon=Piece(anchor,"HelmetDragonRelief",ProductionHuangZhong.Relief("huangzhong_bow_dragon",.105f,.105f,.015f),Color.white);dragon.localPosition=centre+new Vector3(0,.052f,rz+.035f);var dragonMat=dragon.GetComponent<MeshRenderer>().sharedMaterial;dragonMat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/huangzhong_bow_dragon.png"));EditorUtility.SetDirty(dragonMat);
            foreach(int sign in new[]{-1,1})
            {
                var browScroll=new List<Vector3>();for(int i=0;i<=48;i++){float t=i/48f;float a=t*Mathf.PI*2.5f;float radius=.012f+.020f*t;browScroll.Add(centre+new Vector3(sign*(.064f+radius*Mathf.Cos(a)),.018f+radius*Mathf.Sin(a),rz-.015f));}Tube(anchor,"HelmetBrowScroll"+sign,browScroll,.003f,Gold,10);
            }
            var mount=centre+Vector3.up*(ry+.01f);Tube(anchor,"PlumeSocket",new[]{mount,mount+Vector3.up*.04f},.026f,Gold);
            for(int i=-4;i<=4;i++)
            {
                var path=new List<Vector3>();for(int j=0;j<=32;j++){float t=j/32f;path.Add(mount+new Vector3(i*.006f*(1+t*2.2f)+.035f*Mathf.Sin(t*4+i*1.2f)*t,.025f+(.23f-.035f*Mathf.Abs(i)+.02f*Mathf.Sin(i*2))*Mathf.Sin(t*Mathf.PI*(.75f+.035f*i))-.03f*t,-(.32f+.06f*Mathf.Sin(i*1.9f))*t));}
                var plume=ProductionCharacterProps.Tube(path,.022f,12);var pv=plume.vertices;for(int j=0;j<path.Count;j++)for(int k=0;k<12;k++){int n=j*12+k;pv[n]=path[j]+(pv[n]-path[j])*Mathf.Max(.03f,Mathf.Pow(1-j/(float)(path.Count-1),.7f));}plume.vertices=pv;plume.RecalculateNormals();plume.RecalculateBounds();
                var strand=Piece(anchor,"WhitePlume"+i,plume,Color.white);var m=strand.GetComponent<MeshRenderer>().sharedMaterial;m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/huangzhong_hair_fibers.png"));m.SetFloat("_MetalStrength",.02f);EditorUtility.SetDirty(m);
            }
        }
        private static void Spear(Transform prop)
        {
            Tube(prop,"SpearShaft",new[]{new Vector3(0,0,-.67f),new Vector3(0,0,.67f)},.014f,new Color(.19f,.11f,.045f),32);
            Tube(prop,"SpearGrip",new[]{new Vector3(0,0,-.14f),new Vector3(0,0,.15f)},.017f,Blue);
            var wrap=new List<Vector3>();for(int i=0;i<=180;i++){float t=i/180f,a=t*Mathf.PI*20;wrap.Add(new Vector3(.0175f*Mathf.Cos(a),.0175f*Mathf.Sin(a),-.14f+t*.29f));}Tube(prop,"SpearGripWrap",wrap,.0018f,Gold,8);
            foreach(float z in new[]{-.66f,-.15f,.16f,.52f,.62f,.68f})Tube(prop,"SpearCollar"+z,new[]{new Vector3(0,0,z-.009f),new Vector3(0,0,z+.009f)},.022f,Gold);
            const int rows=36;var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
            for(int i=0;i<=rows;i++){float t=i/(float)rows,w=.040f*Mathf.Sin(Mathf.Pow(t,.66f)*Mathf.PI)+.007f*(1-t),thickness=.016f*Mathf.Sin(t*Mathf.PI)+.001f;v.AddRange(new[]{new Vector3(-w,0,.65f+t*.49f),new Vector3(0,thickness,.65f+t*.49f),new Vector3(w,0,.65f+t*.49f),new Vector3(0,-thickness,.65f+t*.49f)});uv.AddRange(new[]{new Vector2(0,t),new Vector2(.5f,t),new Vector2(1,t),new Vector2(.5f,t)});if(i<rows)for(int j=0;j<4;j++){int k=i*4+j,n=i*4+(j+1)%4;tri.AddRange(new[]{k,k+4,n,n,k+4,n+4});}}
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();Piece(prop,"SpearDiamondBlade",mesh,Silver);
            foreach(int sign in new[]{-1,1}){var inlay=new List<Vector3>();for(int i=0;i<=48;i++){float t=i/48f;inlay.Add(new Vector3(.018f*Mathf.Sin(t*Mathf.PI*4)*(1-t),sign*.017f,.69f+t*.22f));}Tube(prop,"SpearCloudInlay"+sign,inlay,.002f,Gold,8);}
            for(int i=-3;i<=3;i++){var path=new List<Vector3>();for(int j=0;j<=24;j++){float t=j/24f;path.Add(new Vector3(i*.009f*(1-t*.5f),.012f+.015f*Mathf.Sin(t*Mathf.PI),.66f-t*(.16f+.025f*Mathf.Cos(i))));}Tube(prop,"SpearWhiteTassel"+i,path,.009f,new Color(.90f,.91f,.84f),10);}
        }
    }
}

