#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace SanGuo.Client.Editor
{
    public static class ProductionHuangZhong
    {
        private const string Output="Assets/Resources/ProductionCharacters";
        private static readonly Color Gold=new Color(.83f,.57f,.2f),Bronze=new Color(.24f,.10f,.035f),Red=new Color(.65f,.035f,.025f);
        public static void Bake()
        {
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/huangzhong.prefab"));
            try
            {
                root.name="huangzhong";root.transform.localScale=Vector3.one;
                var set=root.GetComponent<CharacterClipSet>();
                set.Idle=Archive(set.Idle!,"Idle");set.Attack=Archive(set.Attack!,"Attack");set.Idle.SampleAnimation(root,0);
                foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Weapon_") || t.name.StartsWith("Design_Huang") || t.name=="PropBow" || t.name=="PropArrow").ToArray())if(t!=null)UnityEngine.Object.DestroyImmediate(t.gameObject);
                var hair=root.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(r=>r.name=="HairRenderer");if(hair!=null)UnityEngine.Object.DestroyImmediate(hair.gameObject);
                var rig=root.GetComponent<ArcherPoseRig>();if(rig!=null)UnityEngine.Object.DestroyImmediate(rig);
                Surface(root,"BodyRenderer","huangzhong_body",.47f,.5f);
                Surface(root,"FaceRenderer","huangzhong_face",.08f,.2f);
                Surface(root,"CosmeticRenderer","huangzhong_beard",.04f,.25f);
                Helmet(root);Bow(root);Quiver(root);
                ProductionArcherAnimation.Apply(root,"huangzhong",Output);
                set.SourceIdle=set.Idle;ProductionIdlePosture.Apply(root,"huangzhong",Output);
                set.Idle=StringTracks(root,set.Idle!,"Idle");set.Attack=StringTracks(root,set.Attack!,"Attack");
                set.Cast=Auxiliary(root,set.Cast!,"Cast");set.Hit=Auxiliary(root,set.Hit!,"Hit");set.Die=Auxiliary(root,set.Die!,"Die");
                set.Idle.SampleAnimation(root,0);ProductionCharacterBaker.Normalize(root,set);
                ProductionCharacterAssembly.Validate(root);PrefabUtility.SaveAsPrefabAsset(root,Output+"/huangzhong.prefab");AssetDatabase.SaveAssets();
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        private static AnimationClip Archive(AnimationClip source,string motion)
        {
            string path=Output+"/Animations/huangzhong_ArcherSource"+motion+".anim";var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(saved==null){saved=UnityEngine.Object.Instantiate(source);AssetDatabase.CreateAsset(saved,path);}return saved;
        }
        private static AnimationClip Auxiliary(GameObject root,AnimationClip source,string motion)
        {
            var original=Archive(source,motion);
            var result=ProductionArcherAnimation.Author(root,original,"huangzhong_Bow"+motion,Output,false);
            result=StringTracks(root,result,motion);
            var settings=AnimationUtility.GetAnimationClipSettings(result);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(result,settings);
            EditorUtility.SetDirty(result);return result;
        }
        private static void Surface(GameObject root,string renderer,string texture,float metal,float smooth)
        {
            var r=root.GetComponentsInChildren<SkinnedMeshRenderer>().First(x=>x.name==renderer);var material=r.sharedMaterial;
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/"+texture+".png"));material.SetColor("_BaseColor",Color.white);material.SetFloat("_MetalStrength",metal);material.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(material);
        }
        private static Transform Bone(GameObject root,string name)=>root.GetComponentsInChildren<Transform>().First(t=>t.name==name);
        private static Transform Anchor(GameObject root,string bone,string name)
        {
            var t=new GameObject(name).transform;t.SetParent(root.transform,false);t.SetParent(Bone(root,bone),true);return t;
        }
        private static Transform Piece(Transform parent,string name,Mesh mesh,Color color)=>ProductionCharacterProps.Piece(parent,"Huang"+name,mesh,color,Output);
        private static Mesh Map(Mesh mesh,Rect rect)
        {
            var uv=mesh.uv;for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(rect.x+uv[i].x*rect.width,rect.y+uv[i].y*rect.height);mesh.uv=uv;return mesh;
        }
        private static void Helmet(GameObject root)
        {
            var face=ProductionInfantryDesign.SkinBounds(root,"FaceRenderer");var head=Anchor(root,"Bip001 Head","Design_HuangHelmet");
            Vector3 centre=face.center+new Vector3(0,face.size.y*.28f,-.015f);float rx=face.size.x*.56f,rz=face.size.z*.59f,ry=face.size.y*.47f;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();const int rings=24,sides=64;
            for(int row=0;row<=rings;row++)for(int col=0;col<=sides;col++)
            {
                float a=col/(float)sides*Mathf.PI*2;
                float theta=row/(float)rings*(Mathf.PI*.5f+.36f*(1-Mathf.Sin(a)));
                vertices.Add(centre+new Vector3(rx*Mathf.Sin(theta)*Mathf.Cos(a),ry*Mathf.Cos(theta),rz*Mathf.Sin(theta)*Mathf.Sin(a)));
                uv.Add(new Vector2(col/(float)sides,row/(float)rings));
                if(row<rings && col<sides){int k=row*(sides+1)+col;triangles.AddRange(new[]{k,k+1,k+sides+1,k+1,k+sides+2,k+sides+1});}
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            var dome=Piece(head,"HelmetBronze",Map(mesh,new Rect(.52f,.19f,.05f,.025f)),Color.white);
            dome.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/huangzhong_helmet.png"));
            var ring=new List<Vector3>();for(int i=0;i<=96;i++){float a=i/96f*Mathf.PI*2;ring.Add(centre+new Vector3(rx*Mathf.Sin(Mathf.PI*.5f+.36f*(1-Mathf.Sin(a)))*Mathf.Cos(a),ry*Mathf.Cos(Mathf.PI*.5f+.36f*(1-Mathf.Sin(a))),rz*Mathf.Sin(Mathf.PI*.5f+.36f*(1-Mathf.Sin(a)))*Mathf.Sin(a)));}
            Piece(head,"HelmetGoldRim",ProductionCharacterProps.Tube(ring,.013f,16),Gold);
            foreach(int sign in new[]{-1,1})
            {
                var panel=Piece(head,"HelmetTemple"+sign,ProductionCharacterProps.Plate(new[]{new Vector2(-.048f,.06f),new Vector2(.048f,.07f),new Vector2(.056f,-.07f),new Vector2(0,-.10f),new Vector2(-.056f,-.07f)},.012f),Gold);
                panel.localPosition=centre+new Vector3(sign*rx,-.035f,.025f);panel.localRotation=Quaternion.Euler(0,sign*65,0);
                var templeMesh=panel.GetComponent<MeshFilter>().sharedMesh;var templeUv=templeMesh.uv;var bounds=templeMesh.bounds;
                for(int j=0;j<templeUv.Length;j++){var p=templeMesh.vertices[j];templeUv[j]=new Vector2(.04f+(p.x-bounds.min.x)/bounds.size.x*.18f,.13f+(p.y-bounds.min.y)/bounds.size.y*.23f);}templeMesh.uv=templeUv;EditorUtility.SetDirty(templeMesh);
                var templeMat=panel.GetComponent<MeshRenderer>().sharedMaterial;templeMat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/huangzhong_helmet.png"));templeMat.SetColor("_BaseColor",Color.white);EditorUtility.SetDirty(templeMat);
            }
            var crest=Piece(head,"HelmetDragonCrest",Relief("huangzhong_bow_dragon",.12f,.105f,.018f),Color.white);
            crest.localPosition=centre+new Vector3(0,.040f,rz+.012f);SetDragon(crest);
            var plumeTop=centre+Vector3.up*(ry+.005f);
            Piece(head,"PlumeMount",ProductionCharacterProps.Tube(new[]{plumeTop,plumeTop+Vector3.up*.040f},.031f,24),Gold);
            for(int i=-3;i<=3;i++)
            {
                var path=new List<Vector3>();for(int j=0;j<=36;j++){float t=j/36f;path.Add(plumeTop+new Vector3(i*.009f*(1+t*1.6f)+.024f*Mathf.Sin(t*4+i)*t,.02f+(.18f-.025f*Mathf.Abs(i))*Mathf.Sin(t*Mathf.PI*(.87f+.025f*i))-(.025f+.007f*i)*t,-(.29f+.045f*Mathf.Cos(i*1.8f))*t));}
                var plume=ProductionCharacterProps.Tube(path,.022f,14); // Geometric separated plume strands, rather than a flat card.
                var plumeVertices=plume.vertices;for(int j=0;j<path.Count;j++)for(int side=0;side<14;side++){int k=j*14+side;float taper=Mathf.Max(.03f,Mathf.Pow(1-j/(float)(path.Count-1),.7f));plumeVertices[k]=path[j]+(plumeVertices[k]-path[j])*taper;}plume.vertices=plumeVertices;plume.RecalculateNormals();plume.RecalculateBounds();
                var p=Piece(head,"HelmetPlume"+i,Map(plume,new Rect(.10f,.72f,.15f,.18f)),Color.white);
                var m=p.GetComponent<MeshRenderer>().sharedMaterial;m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/huangzhong_helmet.png"));m.SetFloat("_MetalStrength",.04f);EditorUtility.SetDirty(m);
            }
            var hairCapV=new List<Vector3>();var hairCapUv=new List<Vector2>();var hairCapTri=new List<int>();
            const int hairRows=12,hairSides=48;
            for(int row=0;row<=hairRows;row++)for(int col=0;col<=hairSides;col++)
            {
                float a=Mathf.PI+col/(float)hairSides*Mathf.PI,theta=Mathf.Lerp(1.85f,2.48f,row/(float)hairRows);
                hairCapV.Add(centre+new Vector3(rx*1.025f*Mathf.Sin(theta)*Mathf.Cos(a),ry*1.025f*Mathf.Cos(theta),rz*1.04f*Mathf.Sin(theta)*Mathf.Sin(a)));
                hairCapUv.Add(new Vector2(col/(float)hairSides,1-row/(float)hairRows));
                if(row<hairRows && col<hairSides){int k=row*(hairSides+1)+col;hairCapTri.AddRange(new[]{k,k+1,k+hairSides+1,k+1,k+hairSides+2,k+hairSides+1});}
            }
            var capMesh=new Mesh();capMesh.SetVertices(hairCapV);capMesh.SetUVs(0,hairCapUv);capMesh.SetTriangles(hairCapTri,0);capMesh.RecalculateNormals();capMesh.RecalculateBounds();
            var cap=Piece(head,"HairBackCap",capMesh,Color.white);var capMat=cap.GetComponent<MeshRenderer>().sharedMaterial;capMat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/huangzhong_hair_fibers.png"));capMat.SetFloat("_MetalStrength",.02f);EditorUtility.SetDirty(capMat);
            for(int i=-5;i<=5;i++)
            {
                var path=new List<Vector3>();for(int j=0;j<=24;j++){float t=j/24f;path.Add(centre+new Vector3(i*.019f*(1-t*.17f)+.021f*Mathf.Sin(t*3+i*2.1f)*t,-.005f-.006f*Mathf.Cos(i*1.7f)-t*(.14f+.033f*Mathf.Cos(i*1.4f)),-rz-.016f-.024f*Mathf.Sin(t*Mathf.PI*.8f)));}
                var hairMesh=ProductionCharacterProps.Tube(path,.022f,12);var hv=hairMesh.vertices;for(int j=0;j<path.Count;j++)for(int k=0;k<12;k++){int n=j*12+k;var offset=hv[n]-path[j];offset.z*=.40f;hv[n]=path[j]+offset*Mathf.Max(.04f,Mathf.Pow(1-j/(float)(path.Count-1),.5f));}hairMesh.vertices=hv;hairMesh.RecalculateNormals();hairMesh.RecalculateBounds();var hairUv=hairMesh.uv;for(int k=0;k<hairUv.Length;k++)hairUv[k]=new Vector2(hairUv[k].x,1-hairUv[k].y);hairMesh.uv=hairUv;var hair=Piece(head,"BackHairLock"+i,hairMesh,Color.white);
                var mat=hair.GetComponent<MeshRenderer>().sharedMaterial;mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/huangzhong_hair_fibers.png"));mat.SetFloat("_MetalStrength",.02f);EditorUtility.SetDirty(mat);
            }
        }
        private static void Bow(GameObject root)
        {
            var bow=new GameObject("PropBow").transform;bow.SetParent(Bone(root,"Bip001 L Hand"),false);
            var points=new List<Vector3>();for(int i=0;i<=64;i++){float t=i/64f;points.Add(new Vector3(.10f*Mathf.Sin(t*Mathf.PI)-.07f*Mathf.Pow(Mathf.Abs(2*t-1),6),Mathf.Lerp(-.40f,.40f,t),0));}
            Piece(bow,"BowBronzeLimb",ProductionCharacterProps.Tube(points,.024f,24),Bronze);
            Piece(bow,"BowGoldRidge",ProductionCharacterProps.Tube(points.Select(p=>p+Vector3.forward*.021f).ToArray(),.006f,12),Gold);
            Piece(bow,"BowGripLeather",ProductionCharacterProps.Tube(new[]{new Vector3(.1f,-.06f,0),new Vector3(.1f,.06f,0)},.029f,24),Red);
            foreach(float y in new[]{-.32f,-.09f,.09f,.32f})
            {
                float t=(y+.4f)/.8f,x=.10f*Mathf.Sin(t*Mathf.PI)-.07f*Mathf.Pow(Mathf.Abs(2*t-1),6);
                Piece(bow,"BowCollar"+y,ProductionCharacterProps.Tube(new[]{new Vector3(x,y-.012f,0),new Vector3(x,y+.012f,0)},.031f,24),Gold);
            }
            foreach(int sign in new[]{-1,1})
            {
                var ornament=Piece(bow,"BowDragon"+sign,Relief("huangzhong_bow_dragon",.15f,.13f,.025f),Color.white);
                ornament.localPosition=new Vector3(-.033f,sign*.32f,.018f);ornament.localRotation=Quaternion.Euler(0,0,sign<0?180:0);SetDragon(ornament);
            }
            foreach(string name in new[]{"Top","Bottom"})Piece(bow,"BowString"+name,ProductionCharacterProps.Tube(new[]{Vector3.zero,Vector3.forward},.002f,8),new Color(.82f,.74f,.52f));
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/r_archer.prefab").GetComponentsInChildren<Transform>().First(t=>t.name=="PropArrow");
            var arrow=UnityEngine.Object.Instantiate(source.gameObject,Bone(root,"Bip001 R Hand"),false);arrow.name="PropArrow";
        }
        private static void Quiver(GameObject root)
        {
            var anchor=Anchor(root,"Bip001 Spine","Design_HuangQuiver");
            // Body bounds include the animated trailing cape, which cannot locate the torso.
            var shoulders=root.transform.InverseTransformPoint((Bone(root,"Bip001 L UpperArm").position+Bone(root,"Bip001 R UpperArm").position)*.5f);
            var bottom=shoulders+new Vector3(.12f,-.30f,-.15f);var top=bottom+new Vector3(.07f,.32f,0);
                        var leather=Piece(anchor,"QuiverLeather",ProductionCharacterProps.Tube(new[]{bottom,top},.055f,32),new Color(.52f,.25f,.10f));
            var material=leather.GetComponent<MeshRenderer>().sharedMaterial;material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/equipment_dark_leather.png"));EditorUtility.SetDirty(material);
            var seamOffset=new Vector3(.039f,0,-.04f);
            Piece(anchor,"QuiverStitchedSeam",ProductionCharacterProps.Tube(new[]{bottom+seamOffset,top+seamOffset},.002f,8),new Color(.72f,.47f,.24f));
            for(int j=0;j<14;j++){var p=Vector3.Lerp(bottom,top,(j+1)/15f)+seamOffset;Piece(anchor,"QuiverStitch"+j,ProductionCharacterProps.Tube(new[]{p-Vector3.right*.007f,p+Vector3.right*.007f},.0015f,6),new Color(.88f,.71f,.42f));}
            var strap=new[]{shoulders+new Vector3(-.16f,-.025f,-.09f),shoulders+new Vector3(0,-.13f,-.14f),bottom+Vector3.up*.13f};
            Piece(anchor,"QuiverCarryStrap",FlatStrap(strap,.036f,.006f),new Color(.27f,.10f,.03f));
            foreach(float t in new[]{.06f,.9f}){var p=Vector3.Lerp(bottom,top,t);Piece(anchor,"QuiverGoldRim"+t,ProductionCharacterProps.Tube(new[]{p,p+Vector3.up*.025f},.059f,24),Gold);}
            for(int i=0;i<7;i++)
            {
                var p=top+new Vector3((i%3-1)*.022f,0,(i/3-1)*.022f);var end=p+new Vector3(.025f,.12f+(i%3)*.017f,0);
                Piece(anchor,"QuiverArrow"+i,ProductionCharacterProps.Tube(new[]{p,end},.004f,8),new Color(.40f,.20f,.065f));
                var feather=Piece(anchor,"QuiverFeather"+i,ProductionCharacterProps.Plate(new[]{new Vector2(0,.055f),new Vector2(.016f,.030f),new Vector2(.018f,0),new Vector2(0,-.012f)},.0015f),new Color(.91f,.90f,.82f));feather.localPosition=end;
            }
        }
        private static Mesh FlatStrap(Vector3[] points,float width,float depth)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int i=0;i<points.Length;i++)
            {
                var tangent=(points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(0,i-1)]).normalized;
                var side=Vector3.Cross(tangent,Vector3.forward).normalized*width*.5f;
                v.AddRange(new[]{points[i]-side-Vector3.forward*depth*.5f,points[i]+side-Vector3.forward*depth*.5f,points[i]+side+Vector3.forward*depth*.5f,points[i]-side+Vector3.forward*depth*.5f});
                uv.AddRange(new[]{new Vector2(0,i),new Vector2(1,i),new Vector2(1,i),new Vector2(0,i)});
                if(i<points.Length-1)for(int j=0;j<4;j++){int k=i*4+j,n=i*4+(j+1)%4;triangles.AddRange(new[]{k,n,k+4,n,n+4,k+4});}
            }
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        internal static Mesh Relief(string imageName,float width,float height,float depth)
        {
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false);image.LoadImage(File.ReadAllBytes(Output+"/Textures/"+imageName+".png"));
            const int size=64;var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();var opaque=new bool[(size+1)*(size+1)];
            for(int y=0;y<=size;y++)for(int x=0;x<=size;x++)
            {
                float u=x/(float)size,w=y/(float)size;var paint=image.GetPixelBilinear(u,w);float dome=Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow((u-.5f)*2,2)-Mathf.Pow((w-.5f)*2,2)));
                v.Add(new Vector3((u-.5f)*width,(w-.5f)*height,.002f+depth*(.6f*dome+.4f*paint.grayscale)));uv.Add(new Vector2(u,w));opaque[y*(size+1)+x]=paint.a>.7f;
            }
            for(int y=0;y<size;y++)for(int x=0;x<size;x++){int k=y*(size+1)+x;if(opaque[k]&&opaque[k+1]&&opaque[k+size+1])tri.AddRange(new[]{k,k+1,k+size+1});if(opaque[k+1]&&opaque[k+size+2]&&opaque[k+size+1])tri.AddRange(new[]{k+1,k+size+2,k+size+1});}
            // Separate back vertices preserve opposite normals instead of cancelling them.
            int frontCount=v.Count;v.AddRange(v.Take(frontCount).Select(p=>new Vector3(p.x,p.y,-.004f)).ToArray());uv.AddRange(uv.Take(frontCount).ToArray());
            var frontTriangles=tri.ToArray();for(int i=0;i<frontTriangles.Length;i+=3)tri.AddRange(new[]{frontTriangles[i]+frontCount,frontTriangles[i+2]+frontCount,frontTriangles[i+1]+frontCount});
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();UnityEngine.Object.DestroyImmediate(image);return mesh;
        }
        private static void SetDragon(Transform piece)
        {
            var m=piece.GetComponent<MeshRenderer>().sharedMaterial;m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/huangzhong_bow_dragon.png"));m.SetFloat("_MetalStrength",.60f);EditorUtility.SetDirty(m);
        }
        private static AnimationClip StringTracks(GameObject root,AnimationClip source,string motion)
        {
            var result=UnityEngine.Object.Instantiate(source);result.name="huangzhong_CompleteBow"+motion;
            var bow=Bone(root,"PropBow");var right=Bone(root,"Bip001 R Hand");var arrow=Bone(root,"PropArrow");var strings=new[]{Bone(root,"HuangBowStringTop"),Bone(root,"HuangBowStringBottom")};
            var all=root.GetComponentsInChildren<Transform>();var initial=all.Select(t=>(t,t.localPosition,t.localRotation,t.localScale)).ToArray();
            var curves=strings.ToDictionary(t=>t,t=>Enumerable.Range(0,10).Select(_=>new AnimationCurve()).ToArray());var arrowScale=new AnimationCurve();
            int frames=Mathf.CeilToInt(source.length*60);
            for(int i=0;i<=frames;i++)
            {
                foreach(var p in initial){p.t.localPosition=p.localPosition;p.t.localRotation=p.localRotation;p.t.localScale=p.localScale;}
                float time=source.length*i/frames;source.SampleAnimation(root,time);
                var nock=bow.InverseTransformPoint(arrow.position);
                for(int j=0;j<2;j++)
                {
                    var t=strings[j];var start=new Vector3(-.07f,j==0?.40f:-.40f,0);var delta=nock-start;
                    t.localPosition=start;t.localRotation=Quaternion.LookRotation(delta,Vector3.right);t.localScale=new Vector3(1,1,delta.magnitude);
                    var p=t.localPosition;var q=t.localRotation;var s=t.localScale;var c=curves[t];c[0].AddKey(time,p.x);c[1].AddKey(time,p.y);c[2].AddKey(time,p.z);c[3].AddKey(time,q.x);c[4].AddKey(time,q.y);c[5].AddKey(time,q.z);c[6].AddKey(time,q.w);c[7].AddKey(time,s.x);c[8].AddKey(time,s.y);c[9].AddKey(time,s.z);
                }
                float phase=i/(float)frames;arrowScale.AddKey(time,motion=="Attack" && phase>.53f && phase<.84f?.001f:1f);
            }
            foreach(var p in initial){p.t.localPosition=p.localPosition;p.t.localRotation=p.localRotation;p.t.localScale=p.localScale;}
            var names=new[]{"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalScale.x","m_LocalScale.y","m_LocalScale.z"};
            foreach(var t in strings)for(int i=0;i<10;i++)AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(t,root.transform),typeof(Transform),names[i]),curves[t][i]);
            // Preserve mirrored source prop scale while hiding the held arrow after release.
            foreach(string axis in new[]{"x","y","z"}){var scaled=new AnimationCurve(arrowScale.keys);for(int i=0;i<scaled.length;i++){var k=scaled.keys[i];k.value*=arrow.localScale.x;k.inTangent=k.outTangent=float.PositiveInfinity;scaled.MoveKey(i,k);}AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(arrow,root.transform),typeof(Transform),"m_LocalScale."+axis),scaled);}
            result.EnsureQuaternionContinuity();string path=Output+"/Animations/"+result.name+".anim";var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(saved==null){AssetDatabase.CreateAsset(result,path);saved=result;}else{EditorUtility.CopySerialized(result,saved);EditorUtility.SetDirty(saved);UnityEngine.Object.DestroyImmediate(result);}return saved;
        }
    }
}
