#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SanGuo.Client.Editor
{
    /// <summary>Art-only assembly: modular skins, custom sculpted equipment and costume silhouettes.</summary>
    public static class ProductionInfantryDesign
    {
        private static readonly Color Gold=new Color(.80f,.52f,.16f),Bronze=new Color(.30f,.22f,.13f),Red=new Color(.58f,.018f,.026f),Jade=new Color(.07f,.43f,.23f);
        private static string _prefix="";
        public static bool IsBandit(string id)=>id=="bandit_grunt" || id=="bandit_archer" || id=="bandit_ironbrute" || id=="bandit_marksman";
        public static void PrepareParts(GameObject root,string id)
        {
            if(id == "r_shield" || id == "r_archer" || id == "r_sword" || id=="zhangfei" || IsBandit(id))
            {
                CopyPart(root,"FaceRenderer","r_militia");
                var hair=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name=="HairRenderer");
                UnityEngine.Object.DestroyImmediate(hair.gameObject);
            }
            if(id == "r_healer") TrimCrown(root);
        }

        public static void AddDesign(GameObject root,string id,string output)
        {
            _prefix=IsBandit(id) || id=="zhangfei"?id+"_":"";
            try
            {
            if(id == "r_shield") { ShortHair(root,id,output);Helmet(root,output);ShortSpear(root,output); }
            if(id == "r_archer") { ClearWeapons(root);ShortHair(root,id,output);Headscarf(root,output);Quiver(root,output); }
            if(id == "r_healer") { HealerCrown(root,output);MedicineGourd(root,output); }
            if(id == "r_sword") { ShortHair(root,id,output);SwordHeadband(root,id,output);Sword(root,id,output); }
            if(id == "zhangfei") { ClearWeapons(root);ShortHair(root,id,output);SwordHeadband(root,id,output);ZhangfeiDesign(root,output); }
            if(IsBandit(id))
            {
                ClearWeapons(root);ShortHair(root,id,output);
                if(id=="bandit_ironbrute"){Helmet(root,output);ShortSpear(root,output);BanditBeard(root,id,output);}
                else if(id=="bandit_marksman"){SwordHeadband(root,id,output);HunterTopknot(root,id,output);Quiver(root,output);}
                else{BanditTurban(root,id,output);if(id=="bandit_archer")Quiver(root,output);else Sword(root,id,output);}
            }
            }
            finally{_prefix="";}
        }

        private static void ZhangfeiDesign(GameObject root,string output)
        {
            const string id="zhangfei";
            var face=SkinBounds(root,"FaceRenderer");
            var head=Anchor(root,"Bip001 Head","Design_ZhangfeiHair");
            var crown=new Vector3(face.center.x,face.max.y-.035f,face.center.z);
            for(int i=-3;i<=3;i++)
            {
                float x=i*.026f;
                Add(head,"HeroHairLock"+i,SmoothSweep(new[]{crown+new Vector3(x,-.025f,.042f),crown+new Vector3(x*.95f,.050f-Mathf.Abs(i)*.008f,-.025f),crown+new Vector3(x*.8f+.055f,.110f-Mathf.Abs(i)*.018f,-.092f)},.045f,.85f),Color.black,output);
            }
            BanditBeard(root,id,output);
            var beard=Anchor(root,"Bip001 Head","Design_ZhangfeiMoustache");
            for(int sign=-1;sign<=1;sign+=2)
            {
                var lip=new Vector3(face.center.x+sign*.011f,face.min.y+face.size.y*.17f,face.max.z+.018f);
                Add(beard,"MoustacheHairLock"+sign,SmoothSweep(new[]{lip,lip+new Vector3(sign*.041f,.006f,.007f),lip+new Vector3(sign*.078f,-.024f,-.003f),lip+new Vector3(sign*.090f,-.047f,-.020f)},.024f,.9f),Color.black,output);
                for(int i=0;i<3;i++)
                {
                    var cheek=new Vector3(face.center.x+sign*(face.size.x*.41f),face.min.y+.070f+i*.027f,face.max.z-.047f);
                    Add(beard,"SideburnHairLock"+sign+"_"+i,SmoothSweep(new[]{cheek,cheek+new Vector3(sign*.008f,-.04f,.016f),cheek+new Vector3(-sign*.038f,-.094f,.026f)},.029f,.9f),Color.black,output);
                }
            }
            var spear=Anchor(root,"Bip001 R Hand","Design_SnakeSpear");
            spear.localPosition=Vector3.zero;
            spear.rotation=Quaternion.LookRotation((root.transform.up+root.transform.right*.32f).normalized,root.transform.forward);
            Add(spear,"SnakeSpearShaft",Tube(new[]{new Vector3(0,0,-.30f),new Vector3(0,0,.59f)},.020f,24),Red,output);
            foreach(float z in new[]{-.28f,-.13f,.13f,.31f,.50f,.58f})
                Add(spear,"SnakeSpearGoldCollar"+z,Tube(new[]{new Vector3(0,0,z-.009f),new Vector3(0,0,z+.009f)},.027f,24),Gold,output);
            var blade=SnakeBlade();
            Add(spear,"SnakeSpearBlade",blade,new Color(.81f,.86f,.88f),output);
            var edgePath=new List<Vector3>();
            for(int i=0;i<=24;i++){float t=i/24f;edgePath.Add(new Vector3(Mathf.Sin(t*Mathf.PI*3)*.035f,0,.58f+t*.46f));}
            Add(spear,"SnakeSpearBladeGoldRidge",Tube(edgePath,.007f,8),Gold,output);
            for(int i=0;i<4;i++)
            {
                var path=new[]{new Vector3(0,0,.55f),new Vector3(.055f+i*.012f,.012f,.44f),new Vector3(.085f+i*.018f,-.012f,.29f)};
                var tassel=Ribbon(path,.023f);var points=tassel.vertices;
                for(int j=0;j<points.Length;j++){var p=path[(j%(path.Length*2))/2];var d=points[j]-p;points[j]=p+new Vector3(d.y,d.x,d.z);}
                tassel.vertices=points;tassel.RecalculateNormals();
                Add(spear,"SnakeSpearScarf"+i,tassel,Red,output);
            }
            var cape=Anchor(root,"Bip001 Spine","Design_ZhangfeiCape");
            var body=SkinBounds(root,"BodyRenderer");
            var v=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
            for(int row=0;row<=20;row++)for(int col=0;col<=16;col++)
            {
                float t=row/20f,u=col/16f,s=(u-.5f)*2;
                v.Add(new Vector3(body.center.x+s*Mathf.Lerp(.17f,.26f,t),face.min.y-.045f-t*.43f,body.min.z-.022f-.13f*t+.021f*Mathf.Sin(u*Mathf.PI*8)*t));
                uv.Add(new Vector2(.16f+u*.26f,.44f+t*.26f));
                if(row<20 && col<16){int k=row*17+col;tris.AddRange(new[]{k,k+17,k+1,k+1,k+17,k+18,k+1,k+17,k,k+18,k+17,k+1});}
            }
            var capeMesh=new Mesh{name="ZhangfeiCape"};capeMesh.SetVertices(v);capeMesh.SetUVs(0,uv);capeMesh.SetTriangles(tris,0);capeMesh.RecalculateNormals();capeMesh.RecalculateBounds();
            var capePart=Add(cape,"HeroCape",capeMesh,Red,output);
            var mat=capePart.GetComponent<MeshRenderer>().sharedMaterial;
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(output+"/Textures/zhangfei_body.png"));mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_MetalStrength",.04f);EditorUtility.SetDirty(mat);
        }

        private static Mesh SnakeBlade()
        {
            const int segments=24;var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int i=0;i<=segments;i++)
            {
                float t=i/(float)segments,x=Mathf.Sin(t*Mathf.PI*3)*.035f,z=.58f+t*.46f;
                float width=.042f*Mathf.Pow(1-t,.55f)+.002f;
                vertices.AddRange(new[]{new Vector3(x-width,0,z),new Vector3(x,.012f*(1-t),z),new Vector3(x+width,0,z),new Vector3(x,-.012f*(1-t),z)});
                uv.AddRange(new[]{new Vector2(0,t),new Vector2(.5f,t),new Vector2(1,t),new Vector2(.5f,t)});
                if(i<segments){int k=i*4;triangles.AddRange(new[]{k,k+4,k+1,k+1,k+4,k+5,k+1,k+5,k+2,k+2,k+5,k+6,k+2,k+6,k+3,k+3,k+6,k+7,k+3,k+7,k,k,k+7,k+4});}
            }
            var mesh=new Mesh{name="SnakeSpearBlade"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();return mesh;
        }

        private static void HunterTopknot(GameObject root,string id,string output)
        {
            var face=SkinBounds(root,"FaceRenderer");var anchor=Anchor(root,"Bip001 Head","Design_HunterTopknot");
            var centre=new Vector3(face.center.x,face.max.y-.012f,face.center.z-.075f);
            Add(anchor,"HunterHairCap",Dome(centre,.065f,.060f,.090f),new Color(.20f,.07f,.025f),output);
            for(int i=-3;i<=3;i++)
                Add(anchor,"HunterHairLock"+i,Sweep(new[]{centre+new Vector3(i*.014f,.035f,-.015f),centre+new Vector3(i*.018f,.073f,-.08f),centre+new Vector3(i*.020f,.016f,-.14f)},.016f,.65f),new Color(.20f,.07f,.025f),output);
            Add(anchor,"HunterYellowHeadbandTie",Tube(new[]{centre-Vector3.up*.005f,centre+Vector3.up*.012f},.039f,24),new Color(.9f,.55f,.06f),output);
        }

        private static void BanditTurban(GameObject root,string id,string output)
        {
            var face=SkinBounds(root,"FaceRenderer");var anchor=Anchor(root,"Bip001 Head","Design_BanditTurban");
            var centre=new Vector3(face.center.x,face.max.y-face.size.y*.25f,face.center.z-.015f);
            float rx=face.size.x*.60f,rz=face.size.z*.82f,height=face.size.y*.42f;
            var yellow=new Color(.92f,.55f,.055f);
            Add(anchor,"TurbanCrown",Dome(centre,rx,rz,height),yellow,output);
            for(int ring=0;ring<3;ring++)
            {
                float y=centre.y-.008f+ring*.030f;
                var strip=Lathe(new[]{new Vector2(rx,y-.014f),new Vector2(rx+.012f,y),new Vector2(rx,y+.021f)},64);
                var vertices=strip.vertices;
                for(int i=0;i<vertices.Length;i++){vertices[i].z*=rz/rx;vertices[i].x+=centre.x;vertices[i].z+=centre.z;vertices[i].y+=.012f*Mathf.Sin(Mathf.Atan2(vertices[i].x-centre.x,vertices[i].z-centre.z)+ring*.35f);}
                strip.vertices=vertices;strip.RecalculateNormals();SmoothSeam(strip,64);
                Add(anchor,"TurbanWrap"+ring,strip,ring%2==0?yellow:new Color(.78f,.43f,.027f),output);
            }
            Add(anchor,"TurbanTopKnot",Dome(centre+Vector3.up*(height-.01f),rx*.42f,rz*.42f,.052f),yellow,output);
            for(int sign=-1;sign<=1;sign+=2)
            {
                var path=new List<Vector3>();for(int i=0;i<=24;i++){float t=i/24f;path.Add(centre+new Vector3(sign*(rx*.82f+.09f*t),-.10f*t+.018f*Mathf.Sin(t*Mathf.PI),-rz*.52f-.11f*t));}
                Add(anchor,"TurbanTail"+sign,Ribbon(path,.041f),yellow,output);
            }
        }

        private static void BanditBeard(GameObject root,string id,string output)
        {
            var face=SkinBounds(root,"FaceRenderer");var anchor=Anchor(root,"Bip001 Head","Design_BanditBeard");
            var profile=new[]{new Vector2(0,-.090f),new Vector2(.039f,-.081f),new Vector2(.074f,-.050f),new Vector2(.089f,-.015f),new Vector2(.079f,.009f),new Vector2(.062f,.025f),new Vector2(0,.027f)};
            var volume=Lathe(SmoothProfile(profile),64);var vertices=volume.vertices;
            for(int i=0;i<vertices.Length;i++){vertices[i].x+=face.center.x;vertices[i].y+=face.min.y;vertices[i].z=vertices[i].z*.42f+face.max.z+.004f;}
            volume.vertices=vertices;volume.RecalculateNormals();SmoothSeam(volume,64);
            Add(anchor,"BeardHairCap",volume,new Color(.05f,.045f,.04f),output);
            for(int i=-4;i<=4;i++)
            {
                float x=i*.017f;var p=new Vector3(face.center.x+x,face.min.y+.010f,face.max.z+.035f);
                var path=new[]{p,p+new Vector3(x*.04f,-.032f,.014f),p+new Vector3(-x*.16f,-.077f+.008f*Mathf.Abs(i),-.001f)};
                Add(anchor,"BeardHairLock"+i,id=="zhangfei"?SmoothSweep(path,.015f,.85f):Sweep(path,.010f,.8f),new Color(.05f,.045f,.04f),output);
            }
        }

        private static void SwordHeadband(GameObject root,string id,string output)
        {
            var face=SkinBounds(root,"FaceRenderer");var anchor=Anchor(root,"Bip001 Head","Design_SwordHeadband");
            var centre=new Vector3(face.center.x,face.max.y-face.size.y*.27f,face.center.z-.014f);
            float rx=face.size.x*.565f,rz=face.size.z*.76f;
            var rings=new[]{new Vector2(rx,centre.y-.012f),new Vector2(rx*1.02f,centre.y),new Vector2(rx,centre.y+.028f)};
            var strip=Lathe(rings,64);var vertices=strip.vertices;
            for(int i=0;i<vertices.Length;i++){vertices[i].z*=rz/rx;vertices[i].x+=centre.x;vertices[i].z+=centre.z;}
            strip.vertices=vertices;strip.RecalculateNormals();SmoothSeam(strip,64);
            Add(anchor,id+"YellowHeadband",strip,new Color(.92f,.59f,.08f),output);
            for(int side=0;side<2;side++)
            {
                var path=new List<Vector3>();for(int i=0;i<=24;i++){float t=i/24f;path.Add(centre+new Vector3(-rx*.78f-(.12f+side*.05f)*t,.012f-.055f*t+.045f*Mathf.Sin(t*Mathf.PI),-rz*.55f-.15f*t));}
                Add(anchor,id+"YellowHeadbandTail"+side,Ribbon(path,.029f),new Color(.90f,.53f,.05f),output);
            }
        }

        private static void Sword(GameObject root,string id,string output)
        {
            ClearWeapons(root);var anchor=Anchor(root,"Bip001 R Hand","Design_Sword");
            anchor.localPosition=Vector3.zero;anchor.rotation=Quaternion.LookRotation((root.transform.up+root.transform.right*.55f).normalized,root.transform.forward);
            Add(anchor,id+"SwordGrip",Tube(new[]{new Vector3(0,0,-.065f),new Vector3(0,0,.065f)},.016f,16),new Color(.13f,.055f,.025f),output);
            Add(anchor,id+"SwordPommel",Tube(new[]{new Vector3(0,0,-.075f),new Vector3(0,0,-.060f)},.024f,16),Gold,output);
            Add(anchor,id+"SwordGuard",Tube(new[]{new Vector3(-.065f,0,.074f),new Vector3(0,0,.088f),new Vector3(.065f,0,.074f)},.016f,16),Gold,output);
            var blade=new Mesh{name=id+"SwordBlade"};
            blade.vertices=new[]{new Vector3(-.032f,0,.09f),new Vector3(.032f,0,.09f),new Vector3(-.026f,0,.45f),new Vector3(.026f,0,.45f),new Vector3(0,0,.53f),new Vector3(0,.016f,.09f),new Vector3(0,.012f,.44f),new Vector3(0,-.016f,.09f),new Vector3(0,-.012f,.44f)};
            blade.uv=blade.vertices.Select(v=>new Vector2(v.x/.064f+.5f,(v.z-.09f)/.44f)).ToArray();
            blade.triangles=new[]{0,5,2,5,6,2,5,1,6,1,3,6,2,6,4,6,3,4,0,2,7,7,2,8,7,8,1,1,8,3,2,4,8,8,4,3,0,7,5,5,7,1};blade.RecalculateNormals();blade.RecalculateTangents();blade.RecalculateBounds();
            Add(anchor,id+"SwordBlade",blade,new Color(.72f,.79f,.84f),output);
            var scarf=Anchor(root,"Bip001 Neck","Design_SwordScarf");var neck=scarf.parent.position;
            var paths=new[]{new[]{neck+new Vector3(-.07f,0,-.025f),neck+new Vector3(-.16f,.025f,-.09f),neck+new Vector3(-.23f,-.045f,-.13f)},new[]{neck+new Vector3(.04f,-.014f,-.03f),neck+new Vector3(.12f,.015f,-.08f),neck+new Vector3(.20f,-.055f,-.14f)}};
            for(int i=0;i<paths.Length;i++)Add(scarf,id+"RedScarfTail"+i,Ribbon(paths[i],.036f),Red,output);
        }

        private static void CopyPart(GameObject root,string slot,string sourceId)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Characters/"+sourceId+".prefab").GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name==slot);
            var target=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name==slot);
            var bones=root.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            target.sharedMesh=source.sharedMesh;target.sharedMaterials=source.sharedMaterials;
            target.bones=source.bones.Select(b=>bones[b.name]).ToArray();
            target.rootBone=source.rootBone!=null?bones[source.rootBone.name]:null;target.localBounds=source.localBounds;
        }

        private static void TrimByColor(GameObject root,string slot,Func<Color,bool> keep)
        {
            var renderer=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name==slot);
            var material=renderer.sharedMaterial;
            var texture=material.HasProperty("_BaseMap")?material.GetTexture("_BaseMap"):material.mainTexture;
            if(texture==null)return;
            string path=AssetDatabase.GetAssetPath(texture);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)return;
            // Called in the isolated art-bake project. Source importer changes are never copied back.
            if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            var pixels=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var mesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);var uv=mesh.uv;
            for(int s=0;s<mesh.subMeshCount;s++)
            {
                var input=mesh.GetTriangles(s);var retained=new List<int>();
                for(int i=0;i<input.Length;i+=3)
                {
                    var centre=(uv[input[i]]+uv[input[i+1]]+uv[input[i+2]])/3;
                    if(keep(pixels.GetPixelBilinear(centre.x,centre.y)))retained.AddRange(new[]{input[i],input[i+1],input[i+2]});
                }
                mesh.SetTriangles(retained,s);
            }
            renderer.sharedMesh=mesh;
        }

        private static void TrimCrown(GameObject root)
        {
            var r=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(x=>x.name=="HairRenderer");
            var mesh=UnityEngine.Object.Instantiate(r.sharedMesh);var uv=mesh.uv;
            for(int s=0;s<mesh.subMeshCount;s++)
            {
                var input=mesh.GetTriangles(s);var retained=new List<int>();
                for(int i=0;i<input.Length;i+=3)
                {
                    var centre=(uv[input[i]]+uv[input[i+1]]+uv[input[i+2]])/3;
                    if(!(centre.x>.57f && centre.y>.66f))retained.AddRange(new[]{input[i],input[i+1],input[i+2]});
                }
                mesh.SetTriangles(retained,s);
            }
            r.sharedMesh=mesh;
        }

        public static Bounds SkinBounds(GameObject root,string name)
        {
            var renderer=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name==name);
            var mesh=renderer.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
            var bind=mesh.bindposes;var matrices=renderer.bones.Select((b,i)=>b.localToWorldMatrix*bind[i]).ToArray();
            Bounds bounds=new Bounds();bool first=true;
            foreach(int i in mesh.triangles.Distinct())
            {
                if(i>=weights.Length)throw new InvalidOperationException("Missing vertex weights: "+root.name+" / "+name+" vertices="+vertices.Length+" weights="+weights.Length);
                var w=weights[i];var p=Vector3.zero;
                void AddWeight(int bone,float weight)
                {
                    if(weight<=0)return;
                    if(bone>=matrices.Length)throw new InvalidOperationException("Invalid weighted bone: "+root.name+" / "+name+" bone="+bone+" bones="+matrices.Length+" mesh="+mesh.name);
                    p+=matrices[bone].MultiplyPoint3x4(vertices[i])*weight;
                }
                AddWeight(w.boneIndex0,w.weight0);AddWeight(w.boneIndex1,w.weight1);AddWeight(w.boneIndex2,w.weight2);AddWeight(w.boneIndex3,w.weight3);
                if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
            }
            Debug.Log("ART_PART_BOUNDS "+root.name+" "+name+" "+bounds);
            return bounds;
        }

        private static Transform Anchor(GameObject root,string bone,string name)
        {
            var parent=root.GetComponentsInChildren<Transform>(true).First(t=>t.name==bone);
            var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.SetParent(parent,true);
            return go.transform;
        }

        private static Transform Add(Transform parent,string name,Mesh mesh,Color color,string output)
        {
            bool fabric=_prefix!="" && (name.Contains("Turban") || name.Contains("Scarf") || name.Contains("Headband"));
            if(fabric)
            {
                var uv=mesh.uv;for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(.16f+uv[i].x*.26f,.44f+uv[i].y*.26f);mesh.uv=uv;
            }
            if(_prefix.StartsWith("bandit_") && (name.Contains("Plume") && !name.Contains("Mount") || name.Contains("Scarf")))color=new Color(.90f,.53f,.045f);
            var part=ProductionCharacterProps.Piece(parent,_prefix+name,mesh,color,output);
            if(fabric)
            {
                var mat=part.GetComponent<MeshRenderer>().sharedMaterial;mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(output+"/Textures/"+(_prefix.StartsWith("zhangfei_")?"zhangfei_body":"bandit_body")+".png"));mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_MetalStrength",.06f);EditorUtility.SetDirty(mat);
            }
            if(_prefix!="" && name.Contains("Hair"))
            {
                var mat=part.GetComponent<MeshRenderer>().sharedMaterial;
                var black=AssetDatabase.LoadAssetAtPath<Texture2D>(output+"/Textures/"+(_prefix.StartsWith("bandit_marksman")?"equipment_hair":"equipment_black_hair")+".png");
                if(black!=null){mat.SetTexture("_BaseMap",black);mat.SetColor("_BaseColor",Color.white);}else mat.SetColor("_BaseColor",new Color(.20f,.23f,.27f));
                mat.SetFloat("_MetalStrength",.05f);EditorUtility.SetDirty(mat);
            }
            return part;
        }
        private static Mesh Tube(IReadOnlyList<Vector3> points,float radius,int sides=12)=>ProductionCharacterProps.Tube(points,radius,sides);

        private static void ShortHair(GameObject root,string id,string output)
        {
            var face=SkinBounds(root,"FaceRenderer");var anchor=Anchor(root,"Bip001 Head","Design_ShortHair");
            float rx=face.size.x*.55f,rz=face.size.z*.68f;float y=face.max.y-face.size.y*.28f;
            var c=new Vector3(face.center.x,y,face.center.z-.025f);
            Add(anchor,id+"HairCap",Dome(c,rx,rz,face.size.y*.33f),new Color(.13f,.046f,.015f),output);
            for(int i=0;i<13;i++)
            {
                float x=(i-6)/6f*rx*.88f;var path=new List<Vector3>();
                for(int j=0;j<=20;j++)
                {
                    float t=j/20f;path.Add(c+new Vector3(x+rx*.14f*Mathf.Sin(t*Mathf.PI),.055f-.10f*t-.025f*Mathf.Abs(x/rx),rz*.85f+.025f*Mathf.Sin(t*Mathf.PI)));
                }
                Add(anchor,id+"HairLock"+i,Sweep(path,.017f,.85f),i%3==0?new Color(.25f,.095f,.025f):new Color(.16f,.055f,.018f),output);
            }
            foreach(int sign in new[]{-1,1})
                Add(anchor,id+"HairTemple"+sign,Sweep(new[]{c+new Vector3(sign*rx*.94f,0,.01f),c+new Vector3(sign*rx*.97f,-.065f,.026f),c+new Vector3(sign*rx*.92f,-.12f,.023f)},.022f,.6f),new Color(.15f,.052f,.018f),output);
            for(int i=0;i<15;i++)
            {
                float x=(i-7)/7f*rx*.92f;var path=new List<Vector3>();
                for(int j=0;j<=24;j++)
                {
                    float t=j/24f;
                    path.Add(c+new Vector3(x,.05f-.20f*t,-rz*Mathf.Sqrt(Mathf.Max(.1f,1-x*x/(rx*rx)))-.015f*Mathf.Sin(t*Mathf.PI)));
                }
                Add(anchor,id+"HairBackLock"+i,Sweep(path,.024f,.95f),new Color(.16f,.055f,.018f),output);
            }
        }

        private static void Helmet(GameObject root,string output)
        {
            var face=SkinBounds(root,"FaceRenderer");var anchor=Anchor(root,"Bip001 Head","Design_ShieldHelmet");
            Vector3 centre=face.center;float rx=face.size.x*.58f,rz=Mathf.Max(face.size.z*.7f,rx*.84f),height=face.size.y*.62f;
            float baseY=face.max.y-face.size.y*.26f;
            Debug.Log("ART_HEAD_FIT r_shield face="+face+" helmet="+rx+","+rz+","+height);
            Add(anchor,"ShieldHelmetDome",Dome(new Vector3(centre.x,baseY,centre.z),rx,rz,height),Bronze,output);
            var rim=new List<Vector3>();for(int i=0;i<=64;i++){float a=i/64f*Mathf.PI*2;rim.Add(new Vector3(centre.x+rx*Mathf.Sin(a),baseY,centre.z+rz*Mathf.Cos(a)));}
            Add(anchor,"ShieldHelmetRim",Tube(rim,.012f),Gold,output);
            for(int j=0;j<4;j++)
            {
                var rib=new List<Vector3>();float a=j*Mathf.PI*.5f;
                for(int i=0;i<=24;i++){float t=i/24f*Mathf.PI*.5f;rib.Add(new Vector3(centre.x+(rx+.003f)*Mathf.Cos(t)*Mathf.Sin(a),baseY+height*Mathf.Sin(t),centre.z+(rz+.003f)*Mathf.Cos(t)*Mathf.Cos(a)));}
                Add(anchor,"ShieldHelmetRib"+j,Tube(rib,.006f),Gold,output);
            }
            var diamond=Add(anchor,"ShieldHelmetJewel",ProductionCharacterProps.Plate(new[]{new Vector2(0,.032f),new Vector2(.024f,0),new Vector2(0,-.032f),new Vector2(-.024f,0)},.008f),Gold,output);
            diamond.localPosition=new Vector3(centre.x,baseY+.025f,centre.z+rz+.012f);
            foreach(int sign in new[]{-1,1})
            {
                var shape=new[]{new Vector2(-.031f,.056f),new Vector2(.036f,.040f),new Vector2(.043f,-.05f),new Vector2(-.02f,-.08f)};
                var wing=Add(anchor,"ShieldHelmetCheek"+sign,ProductionCharacterProps.Plate(shape,.008f),Gold,output);
                wing.localPosition=new Vector3(centre.x+sign*rx*.90f,baseY-.044f,centre.z+rz*.30f);wing.localRotation=Quaternion.Euler(0,sign*65,0);
                var inset=Add(wing,"ShieldHelmetCheekInset"+sign,ProductionCharacterProps.Plate(shape.Select(p=>p*.80f).ToArray(),.008f),Bronze,output);inset.localPosition=Vector3.forward*.004f;
            }
            for(int i=0;i<7;i++)
            {
                float side=(i-3)*.009f;var points=new List<Vector3>();
                for(int j=0;j<=32;j++)
                {
                    float t=j/32f;points.Add(new Vector3(centre.x+side*(1+t),baseY+height+height*(.08f+.95f*Mathf.Sin(t*Mathf.PI*.84f)),centre.z+rz*.05f-rz*2.3f*t));
                }
                Add(anchor,"ShieldHelmetPlume"+i,Sweep(points,.022f,.5f),i%2==0?Red:new Color(.76f,.035f,.045f),output);
            }
            Add(anchor,"ShieldHelmetPlumeMount",Tube(new[]{new Vector3(centre.x,baseY+height-.014f,centre.z),new Vector3(centre.x,baseY+height+.025f,centre.z)},.040f,24),Gold,output);
        }

        private static void ClearWeapons(GameObject root)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Weapon_") || t.name=="WeaponRenderer").ToArray())
                if(t!=null)UnityEngine.Object.DestroyImmediate(t.gameObject);
        }

        private static void Headscarf(GameObject root,string output)
        {
            var face=SkinBounds(root,"FaceRenderer");var anchor=Anchor(root,"Bip001 Head","Design_ArcherHeadscarf");
            var centre=new Vector3(face.center.x,face.max.y-face.size.y*.24f,face.center.z);
            float rx=face.size.x*.59f,rz=Mathf.Max(face.size.z*.75f,rx*.88f),height=face.size.y*.60f;
            var cap=Dome(centre,rx,rz,height);var v=cap.vertices;
            for(int i=0;i<v.Length;i++)
            {
                var delta=v[i]-centre;float a=Mathf.Atan2(delta.x,delta.z);
                float fold=.005f*Mathf.Sin(a*7+delta.y*28)*Mathf.Clamp01(delta.y/height);
                v[i]+=new Vector3(Mathf.Sin(a)*fold,0,Mathf.Cos(a)*fold);
            }
            cap.vertices=v;cap.RecalculateNormals();SmoothSeam(cap,64);
            Add(anchor,"ArcherClothHeadscarf",cap,new Color(.025f,.10f,.55f),output);
            var band=new List<Vector3>();for(int i=0;i<=64;i++){float a=i/64f*Mathf.PI*2;band.Add(centre+new Vector3((rx+.003f)*Mathf.Sin(a),.008f,(rz+.003f)*Mathf.Cos(a)));}
            Add(anchor,"ArcherHeadscarfGoldBorder",Tube(band,.0055f),Gold,output);
            for(int side=0;side<2;side++)
            {
                var path=new List<Vector3>();for(int i=0;i<=30;i++){float t=i/30f;path.Add(centre+new Vector3((side==0?-.05f:.05f)+(side==0?-.20f:.13f)*t,.040f-.12f*t+.03f*Mathf.Sin(t*Mathf.PI),-rz-.14f*t));}
                Add(anchor,"ArcherClothTie"+side,Ribbon(path,.045f),new Color(.035f,.15f,.68f),output);
            }
        }

        private static void ShortSpear(GameObject root,string output)
        {
            ClearWeapons(root);var anchor=Anchor(root,"Bip001 R Hand","Design_ShieldSpear");
            var hand=anchor.parent;anchor.position=hand.position;anchor.rotation=Quaternion.LookRotation(root.transform.up,root.transform.forward);
            Add(anchor,"ShieldSpearShaft",Tube(new[]{new Vector3(0,0,-.16f),new Vector3(0,0,.40f)},.014f),new Color(.14f,.055f,.025f),output);
            Add(anchor,"ShieldSpearBlade",SpearBlade(),new Color(.72f,.78f,.82f),output);
            foreach(float z in new[]{-.12f,.26f,.36f})Add(anchor,"ShieldSpearGold"+z.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture),Tube(new[]{new Vector3(0,0,z-.012f),new Vector3(0,0,z+.012f)},.022f),Gold,output);
            for(int i=0;i<4;i++)Add(anchor,"ShieldSpearTassel"+i,Sweep(new[]{new Vector3(.008f*i,0,.28f),new Vector3(.02f+i*.006f,-.06f,.20f),new Vector3(.04f+i*.008f,-.10f,.12f)},.012f,.40f),Red,output);
        }

        private static void Quiver(GameObject root,string output)
        {
            var body=SkinBounds(root,"BodyRenderer");var anchor=Anchor(root,"Bip001 Spine","Design_ArcherQuiver");
            var centre=new Vector3(body.center.x-.14f,body.center.y+.03f,body.min.z-.035f);
            var low=centre+new Vector3(-.035f,-.15f,0);var high=centre+new Vector3(.035f,.17f,0);
            Add(anchor,"ArcherQuiverLeather",Tube(new[]{low,high},.052f,24),new Color(.20f,.085f,.035f),output);
            var direction=(high-low).normalized;
            foreach(float t in new[]{.07f,.90f}){var p=Vector3.Lerp(low,high,t);Add(anchor,"ArcherQuiverBand"+t,Tube(new[]{p-direction*.009f,p+direction*.009f},.055f,24),Gold,output);}
            for(int i=0;i<5;i++)
            {
                var p=high+new Vector3((i-2)*.018f,.02f,Mathf.Abs(i-2)*.008f);
                var tip=p+direction*(.13f+i%2*.02f);
                Add(anchor,"ArcherQuiverArrow"+i,Tube(new[]{p-direction*.12f,tip},.003f,8),new Color(.26f,.12f,.045f),output);
                Add(anchor,"ArcherQuiverFeather"+i,Sweep(new[]{tip-direction*.08f,tip-direction*.025f,tip+direction*.01f},.015f,.28f),new Color(.86f,.86f,.76f),output);
            }
        }

        private static void HealerCrown(GameObject root,string output)
        {
            var face=SkinBounds(root,"FaceRenderer");var anchor=Anchor(root,"Bip001 Head","Design_HealerJade");
            float radius=face.size.x*.55f;var c=new Vector3(face.center.x,face.max.y+.015f,face.center.z);
            var band=new List<Vector3>();for(int i=0;i<=64;i++){float a=i/64f*Mathf.PI*2;band.Add(c+new Vector3(radius*Mathf.Sin(a),.026f*Mathf.Cos(a),radius*.8f*Mathf.Cos(a)));}
            Add(anchor,"HealerGoldHairband",Tube(band,.006f),Gold,output);
            for(int i=-2;i<=2;i++)
            {
                float a=i*.30f;var p=c+new Vector3(radius*Mathf.Sin(a),.026f*Mathf.Cos(a),radius*.8f*Mathf.Cos(a));
                var gem=Add(anchor,"HealerJadeGem"+i,Dome(Vector3.zero,.020f,.008f,.023f),Jade,output);gem.localPosition=p;gem.localRotation=Quaternion.Euler(90,0,0);
            }
            foreach(int sign in new[]{-1,1})
            {
                var leaf=Add(anchor,"HealerHairLeaf"+sign,ProductionCharacterProps.Plate(new[]{new Vector2(0,.040f),new Vector2(.015f,.012f),new Vector2(0,-.035f),new Vector2(-.015f,-.012f)},.003f),Jade,output);
                leaf.localPosition=c+new Vector3(sign*radius*.82f,-.013f,radius*.65f);leaf.localRotation=Quaternion.Euler(0,0,sign*45);
            }
        }

        private static void MedicineGourd(GameObject root,string output)
        {
            ClearWeapons(root);var anchor=Anchor(root,"Bip001 R Hand","Design_HealerGourd");
            anchor.position=anchor.parent.position-root.transform.up*.064f+root.transform.forward*.065f;anchor.rotation=root.transform.rotation;
            var profiles=new[]{new Vector2(0,-.11f),new Vector2(.065f,-.095f),new Vector2(.093f,-.050f),new Vector2(.086f,.015f),new Vector2(.044f,.064f),new Vector2(.058f,.104f),new Vector2(.052f,.154f),new Vector2(.027f,.176f),new Vector2(.019f,.202f),new Vector2(0,.202f)};
            Add(anchor,"HealerMedicineGourd",Lathe(SmoothProfile(profiles),64),Jade,output);
            foreach(float y in new[]{.064f,.182f})Add(anchor,"HealerGourdGold"+y,Tube(new[]{new Vector3(0,y-.006f,0),new Vector3(0,y+.006f,0)},y>.1f?.027f:.046f,24),Gold,output);
            for(int i=0;i<3;i++)
            {
                var cloud=new List<Vector3>();for(int j=0;j<=30;j++){float t=j/30f*Mathf.PI*1.8f;float a=i*2*Mathf.PI/3+.17f*Mathf.Sin(t);cloud.Add(new Vector3(.093f*Mathf.Sin(a),-.025f+.025f*Mathf.Cos(t),.093f*Mathf.Cos(a)));}
                Add(anchor,"HealerGourdCloud"+i,Tube(cloud,.0025f,8),Gold,output);
            }
            for(int i=0;i<4;i++)Add(anchor,"HealerGourdTassel"+i,Sweep(new[]{new Vector3(.047f,0,0),new Vector3(.09f,-.07f,i*.004f),new Vector3(.07f,-.16f,i*.005f)},.008f,.4f),Red,output);
        }

        private static Mesh SpearBlade()
        {
            var v=new[]{new Vector3(0,0,.64f),new Vector3(-.054f,0,.40f),new Vector3(0,0,.36f),new Vector3(.054f,0,.40f),new Vector3(0,.016f,.44f),new Vector3(0,-.016f,.44f)};
            var mesh=new Mesh{vertices=v,triangles=new[]{0,1,4,1,2,4,2,3,4,3,0,4,1,0,5,2,1,5,3,2,5,0,3,5},uv=new[]{new Vector2(.5f,1),new Vector2(0,.2f),new Vector2(.5f,0),new Vector2(1,.2f),new Vector2(.5f,.5f),new Vector2(.5f,.5f)}};
            mesh.RecalculateNormals();mesh.RecalculateTangents();return mesh;
        }

        private static Mesh Dome(Vector3 centre,float rx,float rz,float height)
        {
            var profiles=new List<Vector2>();for(int i=0;i<=16;i++){float t=i/16f*Mathf.PI*.5f;profiles.Add(new Vector2(Mathf.Max(.001f,Mathf.Cos(t)),Mathf.Sin(t)));}
            var mesh=Lathe(profiles.ToArray(),64);var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++){var p=vertices[i];vertices[i]=centre+new Vector3(p.x*rx,p.y*height,p.z*rz);}
            mesh.vertices=vertices;mesh.RecalculateNormals();SmoothSeam(mesh,64);mesh.RecalculateBounds();return mesh;
        }

        private static Vector2[] SmoothProfile(Vector2[] profile)
        {
            var dense=new List<Vector2>();
            for(int i=0;i<profile.Length-1;i++)for(int j=0;j<6;j++)
            {
                float t=j/6f;var a=profile[Mathf.Max(0,i-1)];var b=profile[i];var c=profile[i+1];var d=profile[Mathf.Min(profile.Length-1,i+2)];
                var p=.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
                p.x=Mathf.Max(0,p.x);dense.Add(p);
            }
            dense.Add(profile[^1]);return dense.ToArray();
        }

        private static Mesh Lathe(Vector2[] profile,int sides)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
            for(int i=0;i<profile.Length;i++)for(int j=0;j<=sides;j++)
            {
                float a=j/(float)sides*Mathf.PI*2+Mathf.PI;v.Add(new Vector3(profile[i].x*Mathf.Sin(a),profile[i].y,profile[i].x*Mathf.Cos(a)));uv.Add(new Vector2(j/(float)sides,i/(float)(profile.Length-1)));
                if(i<profile.Length-1 && j<sides){int k=i*(sides+1)+j;tris.AddRange(new[]{k,k+1,k+sides+1,k+1,k+sides+2,k+sides+1});}
            }
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();SmoothSeam(mesh,sides);mesh.RecalculateTangents();return mesh;
        }

        private static void SmoothSeam(Mesh mesh,int sides)
        {
            var normals=mesh.normals;
            for(int i=0;i<normals.Length;i+=sides+1)
            {
                var normal=(normals[i]+normals[i+sides]).normalized;
                normals[i]=normal;normals[i+sides]=normal;
            }
            mesh.normals=normals;
        }

        private static Mesh SmoothSweep(IReadOnlyList<Vector3> points,float radius,float flatten)
        {
            var dense=new List<Vector3>();
            for(int i=0;i<points.Count-1;i++)for(int j=0;j<12;j++)
            {
                float t=j/12f;var a=points[Mathf.Max(0,i-1)];var b=points[i];var c=points[i+1];var d=points[Mathf.Min(points.Count-1,i+2)];
                dense.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));
            }
            dense.Add(points[^1]);return Sweep(dense,radius,flatten);
        }

        private static Mesh Sweep(IReadOnlyList<Vector3> points,float radius,float flatten)
        {
            var mesh=Tube(points,radius,12);var vertices=mesh.vertices;
            for(int i=0;i<points.Count;i++)for(int j=0;j<12;j++)
            {
                int index=i*12+j;var d=vertices[index]-points[i];float t=i/(float)(points.Count-1);
                d*=Mathf.Max(.025f,Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.60f));d.x*=flatten;vertices[index]=points[i]+d;
            }
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private static Mesh Ribbon(IReadOnlyList<Vector3> path,float width)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int i=0;i<path.Count;i++)
            {
                float t=i/(float)(path.Count-1),w=width*Mathf.Lerp(1,.22f,Mathf.Pow(t,4));
                v.Add(path[i]+new Vector3(0,w,.006f*Mathf.Sin(t*10)));v.Add(path[i]-new Vector3(0,w,.006f*Mathf.Sin(t*10)));
                uv.Add(new Vector2(t,0));uv.Add(new Vector2(t,1));
                if(i<path.Count-1){int k=i*2;triangles.AddRange(new[]{k,k+1,k+2,k+1,k+3,k+2});}
            }
            int front=v.Count;v.AddRange(v.ToArray());uv.AddRange(uv.ToArray());
            var back=triangles.ToArray();for(int i=0;i<back.Length;i+=3)triangles.AddRange(new[]{back[i+2]+front,back[i+1]+front,back[i]+front});
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();return mesh;
        }
    }
}
