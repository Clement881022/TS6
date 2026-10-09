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
        public static void PrepareParts(GameObject root,string id)
        {
            if(id == "r_shield" || id == "r_archer")
            {
                CopyPart(root,"FaceRenderer","r_militia");
                var hair=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name=="HairRenderer");
                UnityEngine.Object.DestroyImmediate(hair.gameObject);
            }
            if(id == "r_healer") TrimCrown(root);
        }

        public static void AddDesign(GameObject root,string id,string output)
        {
            if(id == "r_shield") { ShortHair(root,id,output);Helmet(root,output);ShortSpear(root,output); }
            if(id == "r_archer") { ShortHair(root,id,output);Headscarf(root,output);Quiver(root,output); }
            if(id == "r_healer") { HealerCrown(root,output);MedicineGourd(root,output); }
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
                var w=weights[i];var p=matrices[w.boneIndex0].MultiplyPoint3x4(vertices[i])*w.weight0
                    +matrices[w.boneIndex1].MultiplyPoint3x4(vertices[i])*w.weight1
                    +matrices[w.boneIndex2].MultiplyPoint3x4(vertices[i])*w.weight2
                    +matrices[w.boneIndex3].MultiplyPoint3x4(vertices[i])*w.weight3;
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

        private static Transform Add(Transform parent,string name,Mesh mesh,Color color,string output)=>ProductionCharacterProps.Piece(parent,name,mesh,color,output);
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
            foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Weapon_")).ToArray())
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
