#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace SanGuo.Client.Editor
{
    public static class ProductionRosterCoverage
    {
        private const string Output="Assets/Resources/ProductionCharacters";
        private static readonly Color Gold=new Color(.84f,.58f,.20f),Steel=new Color(.74f,.82f,.86f),Black=new Color(.12f,.10f,.09f);
        private sealed class Look
        {
            public readonly string Id,Source,Style,Palette;
            public Look(string id,string source,string style,string palette=""){Id=id;Source=source;Style=style;Palette=palette;}
        }
        private static readonly Look[] Looks={
            new Look("xiahoudun","guanyu","eyepatch","blue"),new Look("lvbu","guanyu","halberd","red"),
            new Look("gongsunzan","guanyu","whitearcher","white"),new Look("huaxiong","guanyu","heavyblade","red"),
            new Look("zhoucang","r_shield","beardshield"),new Look("huangfusong","r_shield","officershield"),
            new Look("zhujun","r_sword","officersword"),new Look("handang","r_archer","veteranarcher"),
            new Look("zoujing","r_archer","helmetarcher"),new Look("xunyu","pangtong","fan"),
            new Look("jianyong","liubei","book"),new Look("luzhi","zhugeliang","book"),
            new Look("r_mage","pangtong","staff"),new Look("r_strategist","zhugeliang","fan"),
            new Look("zhangbao","pangtong","ritualstaff"),new Look("yuji","zhangjiao","whitestaff"),
            new Look("huatuo","zhangjiao","medicine"),new Look("zhangzhongjing","zhugeliang","medicine"),
            new Look("ganfuren","r_healer","femalehealer"),
            new Look("tutorial_hunter","r_archer","alias"),new Look("tutorial_scholar","zhugeliang","alias"),
            new Look("tutorial_wanderer","r_sword","alias"),new Look("ur_guanyu","guanyu","alias"),new Look("ur_zhangfei","zhangfei","alias")
        };
        public static void BakeMissing()
        {
            foreach(var look in Looks)Bake(look);
            AssetDatabase.SaveAssets();if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        public static void BakeRoleBasics()
        {
            foreach(string id in new[]{"liubei","yt_archer","yt_sharpshooter","bandit_drummer"})
            {
                var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/"+id+".prefab"));
                try
                {
                    root.name=id;root.transform.localScale=Vector3.one;var set=root.GetComponent<CharacterClipSet>();set.Idle!.SampleAnimation(root,0);OwnMaterials(root,id);ClearWeapons(root);
                    if(id=="liubei")
                    {
                        SetSurface(root,"BodyRenderer","liubei_body");SetSurface(root,"FaceRenderer","liubei_face");SetSurface(root,"HairRenderer","liubei_hair");
                        var hair=root.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="HairRenderer").sharedMaterial;hair.SetFloat("_MetalStrength",.04f);EditorUtility.SetDirty(hair);
                        var beard=root.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(r=>r.name=="CosmeticRenderer");if(beard!=null){beard.sharedMaterial.SetColor("_BaseColor",new Color(.18f,.075f,.03f));beard.sharedMaterial.SetFloat("_MetalStrength",.02f);EditorUtility.SetDirty(beard.sharedMaterial);}
                        Sword(root,"L",false);Sword(root,"R",false);
                    }
                    else if(id=="bandit_drummer")Drum(root);
                    else Archer(root);
                    set.Idle!.SampleAnimation(root,0);ProductionCharacterBaker.Normalize(root,set);ProductionCharacterAssembly.Validate(root);PrefabUtility.SaveAsPrefabAsset(root,Output+"/"+id+".prefab");
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            AssetDatabase.SaveAssets();if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        private static void Bake(Look look)
        {
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/"+look.Source+".prefab"));
            try
            {
                root.name=look.Id;root.transform.localScale=Vector3.one;var set=root.GetComponent<CharacterClipSet>();set.Idle!.SampleAnimation(root,0);
                OwnMaterials(root,look.Id);
                if(look.Style=="alias"){}
                else if(look.Palette!="")
                {
                    SetSurface(root,"BodyRenderer","baseline_"+look.Palette+"_body");CopyFace(root,"zhaoyun");
                    ClearHead(root);ClearWeapons(root);ImportedHair(root);
                    if(look.Style=="eyepatch"){Beard(root,false);EyePatch(root);Sword(root,"R",true);set.MotionProfile="sword";}
                    if(look.Style=="heavyblade"){Beard(root,false);Helmet(root,false,false);Sword(root,"R",true);set.MotionProfile="sword";}
                    if(look.Style=="halberd"){Helmet(root,true,true);Halberd(root);set.MotionProfile="polearm";set.Idle=ProductionGuanYuGlaive.Author(root,set.Idle!,"Idle",false,false,"Design_CoverageHalberd");set.Attack=ProductionGuanYuGlaive.Author(root,set.Attack!,"Attack",true,false,"Design_CoverageHalberd");set.Cast=ProductionGuanYuGlaive.Author(root,set.Cast!,"Cast",false,false,"Design_CoverageHalberd");set.Hit=ProductionGuanYuGlaive.Author(root,set.Hit!,"Hit",false,true,"Design_CoverageHalberd");}
                    if(look.Style=="whitearcher"){Helmet(root,false,true);Archer(root);}
                }
                else if(look.Style.Contains("archer"))
                {
                    if(look.Style=="veteranarcher")Beard(root,false);
                    if(look.Style=="helmetarcher")Helmet(root,false,false);
                }
                else if(look.Style.Contains("shield") || look.Style=="officersword")
                {
                    Beard(root,look.Style=="beardshield");Badge(root,look.Style=="beardshield"?new Color(.14f,.26f,.12f):new Color(.60f,.08f,.035f));
                }
                else if(look.Style=="femalehealer")
                {
                    Badge(root,new Color(.16f,.32f,.25f));
                }
                else
                {
                    ClearWeapons(root);
                    if(look.Style=="medicine")
                    {
                        if(look.Id=="huatuo"){ClearHead(root);Hair(root,"white");Beard(root,true,true);SetSourceSurface(root,"BodyRenderer",5,6);}
                        Medicine(root);
                    }
                    else if(look.Style=="fan")Fan(root);
                    else if(look.Style=="book")Book(root);
                    else Staff(root,look.Style=="ritualstaff" || look.Style=="whitestaff");
                    if(look.Id=="yuji")SetSourceSurface(root,"BodyRenderer",5,3);
                    if(look.Id=="zhangbao")Badge(root,new Color(.78f,.49f,.06f));
                    set.MotionProfile="caster";
                }
                set.Idle!.SampleAnimation(root,0);ProductionCharacterBaker.Normalize(root,set);ProductionCharacterAssembly.Validate(root);
                PrefabUtility.SaveAsPrefabAsset(root,Output+"/"+look.Id+".prefab");Debug.Log("ART_BASE_COVERAGE "+look.Id+" source="+look.Source+" style="+look.Style+" commercialApproval=false");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        private static Transform Bone(GameObject root,string name)=>root.GetComponentsInChildren<Transform>().First(t=>t.name==name);
        private static Transform Anchor(GameObject root,string bone,string name){var t=new GameObject(name).transform;t.SetParent(root.transform,false);t.SetParent(Bone(root,bone),true);return t;}
        private static Transform Piece(GameObject root,Transform parent,string name,Mesh mesh,Color color)
        {
            var part=ProductionCharacterProps.Piece(parent,root.name+"_Coverage"+name,mesh,color,Output);
            if(color==Steel){var m=part.GetComponent<MeshRenderer>().sharedMaterial;m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/zhaoyun_silver_engraving.png"));m.SetColor("_BaseColor",Color.white);EditorUtility.SetDirty(m);}
            if(name.Contains("HelmetDome")){var m=part.GetComponent<MeshRenderer>().sharedMaterial;m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/zhaoyun_silver_engraving.png"));m.SetColor("_BaseColor",color);EditorUtility.SetDirty(m);}
            if(name.Contains("Hair") || name.Contains("Beard")){var m=part.GetComponent<MeshRenderer>().sharedMaterial;m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/"+(color==Color.white?"huangzhong_hair_fibers":"equipment_black_hair")+".png"));m.SetColor("_BaseColor",Color.white);m.SetFloat("_MetalStrength",.02f);EditorUtility.SetDirty(m);}return part;
        }
        private static Transform Tube(GameObject root,Transform parent,string name,Vector3[] points,float radius,Color color,int sides=16)
        {
            if(name.Contains("HairTuft") || name.Contains("BeardLock"))
            {
                var dense=new List<Vector3>();for(int i=0;i<points.Length-1;i++)for(int j=0;j<12;j++)dense.Add(Vector3.Lerp(points[i],points[i+1],j/12f));dense.Add(points.Last());points=dense.ToArray();
                var mesh=ProductionCharacterProps.Tube(points,radius,sides);var v=mesh.vertices;
                for(int i=0;i<points.Length;i++)for(int j=0;j<sides;j++){int k=i*sides+j;v[k]=points[i]+(v[k]-points[i])*Mathf.Max(.01f,Mathf.Pow(1-i/(float)(points.Length-1),.65f));}
                mesh.vertices=v;mesh.RecalculateNormals();mesh.RecalculateBounds();return Piece(root,parent,name,mesh,color);
            }
            return Piece(root,parent,name,ProductionCharacterProps.Tube(points,radius,sides),color);
        }
        private static void OwnMaterials(GameObject root,string id)
        {
            var map=new Dictionary<Material,Material>();int index=0;
            foreach(var r in root.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterials=r.sharedMaterials.Select(m=>{if(map.TryGetValue(m,out var existing))return existing;string path=Output+"/Materials/"+id+"_CoverageSurface"+(index++)+".mat";var saved=AssetDatabase.LoadAssetAtPath<Material>(path);if(saved==null){saved=new Material(m);AssetDatabase.CreateAsset(saved,path);}else EditorUtility.CopySerialized(m,saved);map[m]=saved;return saved;}).ToArray();
            }
        }
        private static void SetSurface(GameObject root,string renderer,string texture){var m=root.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name==renderer).sharedMaterial;m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/"+texture+".png"));m.SetColor("_BaseColor",Color.white);EditorUtility.SetDirty(m);}
        private static void SetSourceSurface(GameObject root,string renderer,int body,int variant){var m=root.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name==renderer).sharedMaterial;string stem="Body_"+body.ToString("00000");m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Arts/Models/Body/"+stem+"/"+stem+"_"+variant.ToString("000")+"_T.png"));m.SetColor("_BaseColor",Color.white);EditorUtility.SetDirty(m);}
        private static void CopyFace(GameObject root,string donor)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/"+donor+".prefab").GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="FaceRenderer");var target=root.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="FaceRenderer");
            target.sharedMesh=source.sharedMesh;target.bones=source.bones.Select(b=>Bone(root,b.name)).ToArray();target.rootBone=Bone(root,source.rootBone.name);
            string path=Output+"/Materials/"+root.name+"_CoverageFace.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(source.sharedMaterial);AssetDatabase.CreateAsset(mat,path);}else EditorUtility.CopySerialized(source.sharedMaterial,mat);target.sharedMaterial=mat;
        }
        private static void ImportedHair(GameObject root)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Characters/zhaoyun.prefab").GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="HairRenderer");
            var go=new GameObject("HairRenderer");go.transform.SetParent(root.transform,false);var r=go.AddComponent<SkinnedMeshRenderer>();r.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Output+"/Meshes/zhaoyun_HairRenderer.asset");r.bones=source.bones.Select(b=>Bone(root,b.name)).ToArray();r.rootBone=Bone(root,source.rootBone.name);r.updateWhenOffscreen=true;
            string path=Output+"/Materials/"+root.name+"_CoverageHair.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);var donor=AssetDatabase.LoadAssetAtPath<Material>(Output+"/Materials/zhaoyun_HairRenderer_0.mat");if(mat==null){mat=new Material(donor);AssetDatabase.CreateAsset(mat,path);}else EditorUtility.CopySerialized(donor,mat);mat.SetFloat("_MetalStrength",.03f);mat.SetColor("_BaseColor",Color.white);r.sharedMaterial=mat;
        }
        private static void ClearHead(GameObject root)
        {
            foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.name=="HairRenderer" || r.name=="CosmeticRenderer").ToArray())UnityEngine.Object.DestroyImmediate(r.gameObject);
            foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("Helmet") || t.name.Contains("Moustache") || t.name.Contains("BanditBeard") || t.name.Contains("Topknot") || t.name.Contains("ShortHair") || t.name.Contains("Headband") || t.name.Contains("Design_ZhangfeiHair")).ToArray())if(t!=null)UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
        private static void ClearWeapons(GameObject root)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Weapon_") || t.name=="PropBow" || t.name=="PropArrow" || t.name=="PropShield" || t.name.Contains("Glaive") || t.name.Contains("SnakeSpear") || t.name.Contains("HealerGourd") || t.name.Contains("HuangQuiver")).ToArray())if(t!=null)UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
        private static Mesh Ellipsoid(Vector3 centre,Vector3 radius,bool cap=false)
        {
            const int rows=20,cols=48;var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
            for(int i=0;i<=rows;i++)for(int j=0;j<=cols;j++){float a=j/(float)cols*Mathf.PI*2,b=i/(float)rows*Mathf.PI*(cap?.62f:1);v.Add(centre+Vector3.Scale(radius,new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a))));uv.Add(new Vector2(j/(float)cols,i/(float)rows));if(i<rows && j<cols){int k=i*(cols+1)+j;tri.AddRange(new[]{k,k+1,k+cols+1,k+1,k+cols+2,k+cols+1});}}
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static void Hair(GameObject root,string style)
        {
            var f=ProductionInfantryDesign.SkinBounds(root,"FaceRenderer");var anchor=Anchor(root,"Bip001 Head","Design_CoverageHair");var c=f.center+Vector3.up*f.size.y*.24f;var color=style=="white"?Color.white:Black;
            Piece(root,anchor,"HairCap",Ellipsoid(c,new Vector3(f.size.x*.54f,f.size.y*.46f,f.size.z*.58f),true),color);
            for(int i=0;i<14;i++){float a=i/14f*Mathf.PI*2;var p=c+new Vector3(Mathf.Cos(a)*f.size.x*.35f,f.size.y*.3f,Mathf.Sin(a)*f.size.z*.38f);var tip=p+new Vector3(Mathf.Cos(a)*.07f,.045f+.035f*Mathf.Sin(i*1.7f),Mathf.Sin(a)*.075f-.025f);Tube(root,anchor,"HairTuft"+i,new[]{p,Vector3.Lerp(p,tip,.55f)+Vector3.up*.025f,tip},style=="short"?.013f:.025f,color,12);}
        }
        private static void Beard(GameObject root,bool longBeard,bool white=false)
        {
            var f=ProductionInfantryDesign.SkinBounds(root,"FaceRenderer");var anchor=Anchor(root,"Bip001 Head","Design_CoverageBeard");var color=white?Color.white:Black;float length=longBeard?.15f:.07f;
            Piece(root,anchor,"BeardVolume",Ellipsoid(new Vector3(f.center.x,f.min.y-.01f,f.max.z+.004f),new Vector3(f.size.x*.23f,length*.45f,.028f)),color);
            for(int i=-3;i<=3;i++){var p=new Vector3(f.center.x+i*.023f,f.min.y+.016f,f.max.z+.018f);Tube(root,anchor,"BeardLock"+i,new[]{p,p+new Vector3(0,-length*.45f,.020f),p+new Vector3(-i*.009f,-length,0)},.018f,color,12);}
            foreach(int sign in new[]{-1,1}){var p=new Vector3(f.center.x+sign*.015f,f.min.y+f.size.y*.17f,f.max.z+.022f);Tube(root,anchor,"BeardMoustache"+sign,new[]{p,p+new Vector3(sign*.034f,.003f,.002f),p+new Vector3(sign*.054f,-.009f,-.004f)},.008f,color,12);}
        }
        private static void EyePatch(GameObject root)
        {
            var f=ProductionInfantryDesign.SkinBounds(root,"FaceRenderer");var anchor=Anchor(root,"Bip001 Head","Design_CoverageEyepatch");var c=f.center+new Vector3(f.size.x*.21f,-.032f,f.size.z*.5f+.016f);
            Piece(root,anchor,"EyepatchLeather",Ellipsoid(c,new Vector3(f.size.x*.12f,f.size.y*.12f,.012f)),new Color(.055f,.043f,.035f));
            Tube(root,anchor,"EyepatchStrap",new[]{c+new Vector3(-.026f,.019f,-.004f),new Vector3(f.min.x+.02f,f.max.y-.035f,f.center.z+.075f),new Vector3(f.min.x-.008f,f.center.y+.04f,f.center.z)},.005f,new Color(.10f,.07f,.04f),12);
        }
        private static void Helmet(GameObject root,bool doublePlumes,bool plume)
        {
            var f=ProductionInfantryDesign.SkinBounds(root,"FaceRenderer");var anchor=Anchor(root,"Bip001 Head","Design_CoverageHelmet");var c=f.center+Vector3.up*f.size.y*.29f;
            Piece(root,anchor,"HelmetDome",Ellipsoid(c,new Vector3(f.size.x*.56f,f.size.y*.50f,f.size.z*.60f),true),doublePlumes?Gold:Steel);
            var rim=new List<Vector3>();for(int i=0;i<=64;i++){float a=i/64f*Mathf.PI*2;rim.Add(c+new Vector3(f.size.x*.53f*Mathf.Cos(a),-.01f,f.size.z*.57f*Mathf.Sin(a)));}Tube(root,anchor,"HelmetRim",rim.ToArray(),.008f,Gold);
            var crest=Piece(root,anchor,"HelmetCrest",ProductionCharacterProps.Plate(new[]{new Vector2(0,.065f),new Vector2(.038f,0),new Vector2(0,-.024f),new Vector2(-.038f,0)},.008f),doublePlumes?new Color(.60f,.03f,.03f):Gold);crest.localPosition=c+new Vector3(0,.03f,f.size.z*.59f);
            if(!plume && !doublePlumes)return;
            foreach(int sign in doublePlumes?new[]{-1,1}:new[]{0}){var path=new List<Vector3>();for(int i=0;i<=48;i++){float t=i/48f;path.Add(c+new Vector3(sign*(.055f+.1f*t),f.size.y*.47f+.34f*Mathf.Sin(t*Mathf.PI*.65f),-.28f*t));}Tube(root,anchor,doublePlumes?"HelmetRedPlume"+sign:"HelmetWhitePlume",path.ToArray(),doublePlumes?.015f:.024f,doublePlumes?new Color(.56f,.02f,.025f):Color.white,14);}
        }
        private static Transform HandProp(GameObject root,string side,string name)
        {
            var t=new GameObject(name).transform;t.SetParent(Bone(root,"Bip001 "+side+" Hand"),false);t.localPosition=new Vector3(-.033f,0,0);t.localRotation=Quaternion.LookRotation(Vector3.up,Vector3.forward);t.localScale=-Vector3.one;return t;
        }
        private static Mesh Blade(float length,float width,bool curved)
        {
            const int rows=36;var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
            for(int i=0;i<=rows;i++){float t=i/(float)rows,w=width*(.45f+.55f*Mathf.Sin(t*Mathf.PI))*(1-Mathf.Pow(t,7)),x=curved?-.08f*t*t:0,z=.09f+t*length;v.AddRange(new[]{new Vector3(x-w,0,z),new Vector3(x,.012f,z),new Vector3(x+w,0,z),new Vector3(x,-.012f,z)});uv.AddRange(new[]{new Vector2(0,t),new Vector2(.5f,t),new Vector2(1,t),new Vector2(.5f,t)});if(i<rows)for(int j=0;j<4;j++){int k=i*4+j,n=i*4+(j+1)%4;tri.AddRange(new[]{k,k+4,n,n,k+4,n+4});}}
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static void Sword(GameObject root,string side,bool curved)
        {
            var prop=HandProp(root,side,"Design_CoverageSword"+side);Tube(root,prop,"SwordGrip"+side,new[]{new Vector3(0,0,-.08f),new Vector3(0,0,.07f)},.018f,Black);Tube(root,prop,"SwordGuard"+side,new[]{new Vector3(-.08f,0,.08f),new Vector3(.08f,0,.08f)},.014f,Gold);Piece(root,prop,"SwordBlade"+side,Blade(.55f,curved?.055f:.035f,curved),Steel);
        }
        private static void Halberd(GameObject root)
        {
            var prop=HandProp(root,"R","Design_CoverageHalberd");Tube(root,prop,"HalberdShaft",new[]{new Vector3(0,0,-.65f),new Vector3(0,0,.70f)},.017f,new Color(.25f,.055f,.03f));var blade=Piece(root,prop,"HalberdTip",Blade(.32f,.036f,false),Steel);blade.localPosition=Vector3.forward*.62f;
            foreach(int sign in new[]{-1,1}){var axe=Piece(root,prop,"HalberdCrescent"+sign,ProductionCharacterProps.Plate(new[]{new Vector2(0,-.13f),new Vector2(.09f,-.16f),new Vector2(.16f,-.10f),new Vector2(.13f,.16f),new Vector2(.07f,.19f),new Vector2(.095f,.045f),new Vector2(0,.02f)},.009f),Steel);axe.localPosition=Vector3.forward*.69f;axe.localRotation=Quaternion.Euler(90,0,sign>0?0:180);}
        }
        private static void Archer(GameObject root)
        {
            var set=root.GetComponent<CharacterClipSet>();var rig=root.GetComponent<ArcherPoseRig>();if(rig!=null)UnityEngine.Object.DestroyImmediate(rig);ProductionCharacterProps.Bow(root,Output);
            set.Idle=ProductionArcherAnimation.Author(root,set.Idle!,root.name+"_CoverageBowIdle",Output,false);set.Attack=ProductionArcherAnimation.Author(root,set.Attack!,root.name+"_CoverageBowAttack",Output,true);set.MotionProfile="archer";
        }
        private static void Badge(GameObject root,Color color)
        {
            var f=ProductionInfantryDesign.SkinBounds(root,"FaceRenderer");var anchor=Anchor(root,"Bip001 Spine","Design_CoverageOfficerBadge");var p=Piece(root,anchor,"OfficerSash",ProductionCharacterProps.Plate(new[]{new Vector2(-.04f,.10f),new Vector2(.04f,.10f),new Vector2(.06f,-.10f),new Vector2(0,-.14f),new Vector2(-.06f,-.10f)},.004f),color);p.localPosition=new Vector3(.14f,f.min.y-.15f,.13f);
        }
        private static void Fan(GameObject root)
        {
            var prop=HandProp(root,"R","Design_CoverageFan");Tube(root,prop,"FanHandle",new[]{new Vector3(0,0,-.07f),new Vector3(0,0,.12f)},.010f,Gold);
            for(int i=-4;i<=4;i++){var feather=Piece(root,prop,"FanFeather"+i,ProductionCharacterProps.Plate(new[]{new Vector2(0,0),new Vector2(-.02f,.12f),new Vector2(0,.21f),new Vector2(.02f,.12f)},.002f),new Color(.86f,.88f,.78f));feather.localPosition=Vector3.forward*.10f;feather.localRotation=Quaternion.Euler(90,0,i*12);}
        }
        private static void Book(GameObject root)
        {
            var prop=HandProp(root,"L","Design_CoverageBook");var book=Piece(root,prop,"BookPages",ProductionCharacterProps.Plate(new[]{new Vector2(-.07f,-.09f),new Vector2(.07f,-.09f),new Vector2(.07f,.09f),new Vector2(-.07f,.09f)},.022f),new Color(.85f,.80f,.62f));book.localRotation=Quaternion.Euler(90,0,0);var cover=Piece(root,book,"BookCover",ProductionCharacterProps.Plate(new[]{new Vector2(-.075f,-.095f),new Vector2(.075f,-.095f),new Vector2(.075f,.095f),new Vector2(-.075f,.095f)},.002f),new Color(.20f,.12f,.07f));cover.localPosition=Vector3.forward*.026f;Fan(root);
        }
        private static void Staff(GameObject root,bool ritual)
        {
            var prop=HandProp(root,"R","Design_CoverageStaff");Tube(root,prop,"StaffWood",new[]{new Vector3(0,0,-.34f),new Vector3(0,0,.58f)},.017f,new Color(.24f,.11f,.035f));Piece(root,prop,"StaffJade",Ellipsoid(new Vector3(0,0,.59f),new Vector3(.035f,.025f,.04f)),new Color(.09f,.39f,.23f));if(ritual){var ring=new List<Vector3>();for(int i=0;i<=48;i++){float a=i/48f*Mathf.PI*2;ring.Add(new Vector3(.07f*Mathf.Cos(a),.07f*Mathf.Sin(a),.59f));}Tube(root,prop,"StaffGoldRing",ring.ToArray(),.007f,Gold);}
        }
        private static void Medicine(GameObject root)
        {
            var prop=HandProp(root,"R","Design_CoverageMedicine");Piece(root,prop,"MedicineGourdLower",Ellipsoid(new Vector3(0,0,.04f),new Vector3(.065f,.055f,.08f)),new Color(.37f,.24f,.07f));Piece(root,prop,"MedicineGourdUpper",Ellipsoid(new Vector3(0,0,.14f),new Vector3(.037f,.035f,.04f)),new Color(.47f,.32f,.12f));Tube(root,prop,"MedicineStopper",new[]{new Vector3(0,0,.16f),new Vector3(0,0,.19f)},.017f,Gold);var scroll=HandProp(root,"L","Design_CoverageMedicineScroll");Tube(root,scroll,"MedicineScroll",new[]{new Vector3(0,0,-.10f),new Vector3(0,0,.10f)},.030f,new Color(.84f,.80f,.65f));
        }
        private static void Drum(GameObject root)
        {
            var shoulders=root.transform.InverseTransformPoint((Bone(root,"Bip001 L UpperArm").position+Bone(root,"Bip001 R UpperArm").position)*.5f);var anchor=Anchor(root,"Bip001 Spine","Design_CoverageDrum");var c=shoulders+new Vector3(-.06f,-.23f,.20f);
            Tube(root,anchor,"DrumShell",new[]{c-Vector3.forward*.07f,c+Vector3.forward*.07f},.115f,new Color(.42f,.08f,.035f),48);
            Tube(root,anchor,"DrumHead",new[]{c+Vector3.forward*.073f,c+Vector3.forward*.075f},.104f,new Color(.85f,.78f,.59f),48);
            foreach(int sign in new[]{-1,1})Tube(root,anchor,"DrumGoldRim"+sign,new[]{c+Vector3.forward*(sign*.063f),c+Vector3.forward*(sign*.077f)},.118f,Gold,48);
            for(int i=0;i<12;i++){float a=i/12f*Mathf.PI*2;var p=c+new Vector3(.119f*Mathf.Cos(a),.119f*Mathf.Sin(a),0);Tube(root,anchor,"DrumLacing"+i,new[]{p-Vector3.forward*.06f,p+Vector3.forward*.06f},.003f,new Color(.73f,.56f,.30f),8);}
            Tube(root,anchor,"DrumCarryStrap",new[]{shoulders+new Vector3(.12f,0,.09f),c+Vector3.up*.10f},.010f,new Color(.24f,.12f,.035f));
            foreach(string side in new[]{"L","R"}){var stick=HandProp(root,side,"Design_CoverageDrumstick"+side);Tube(root,stick,"Drumstick"+side,new[]{new Vector3(0,0,-.07f),new Vector3(0,0,.17f)},.007f,new Color(.53f,.32f,.13f),12);}
        }
    }
}

