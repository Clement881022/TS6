#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SanGuo.Client.Editor
{
    /// <summary>Art-only floor kit. No battle state, input, or runtime layout changes.</summary>
    public static class ProductionBoardBaker
    {
        private const string Output="Assets/Resources/ProductionBoard";
        private const float PitchX=1.55f,PitchZ=1.85f;

        [MenuItem("SanGuo/製作正式棋盤美術素材")]
        public static void Bake()
        {
            Directory.CreateDirectory(Output+"/Meshes");Directory.CreateDirectory(Output+"/Materials");AssetDatabase.Refresh();
            var root=new GameObject("CourtyardBoard_5x5");
            try
            {
                var stoneA=Material("WarmStone",new Color(.87f,.83f,.73f),.07f,0);
                var stoneB=Material("CeladonStone",new Color(.75f,.81f,.76f),.06f,0);
                var jade=Material("JadeFoundation",new Color(.17f,.31f,.30f),.025f,.12f);
                var gold=Material("AntiqueGold",new Color(.75f,.52f,.23f),.012f,.8f);
                float w=PitchX*5,d=PitchZ*5;
                Part(root.transform,"Foundation",new Vector3(0,-.19f,0),new Vector3(w+.58f,.25f,d+.58f),.07f,jade);
                Part(root.transform,"GoldReveal",new Vector3(0,-.08f,0),new Vector3(w+.37f,.035f,d+.37f),.016f,gold);
                Part(root.transform,"Grout",new Vector3(0,-.055f,0),new Vector3(w+.24f,.045f,d+.24f),.022f,jade);
                for(int lane=0;lane<5;lane++)for(int row=0;row<5;row++)
                    Part(root.transform,"Stone_"+lane+"_"+row,new Vector3((2-row)*PitchX,-.04f,(2-lane)*PitchZ),new Vector3(PitchX*.96f,.08f,PitchZ*.96f),.026f,(lane+row)%2==0?stoneA:stoneB);
                foreach(int sign in new[]{-1,1})
                {
                    Part(root.transform,"EdgeX"+sign,new Vector3(sign*(w/2+.17f),-.025f,0),new Vector3(.10f,.065f,d+.39f),.015f,gold);
                    Part(root.transform,"EdgeZ"+sign,new Vector3(0,-.025f,sign*(d/2+.17f)),new Vector3(w+.39f,.065f,.10f),.015f,gold);
                    for(int i=-2;i<=2;i++)
                    {
                        Part(root.transform,"SideSealX"+sign+i,new Vector3(sign*(w/2+.294f),-.18f,i*PitchZ),new Vector3(.018f,.060f,.29f),.005f,gold);
                        Part(root.transform,"SideSealZ"+sign+i,new Vector3(i*PitchX,-.18f,sign*(d/2+.294f)),new Vector3(.29f,.060f,.018f),.005f,gold);
                    }
                    foreach(int other in new[]{-1,1})
                    {
                        var c=new Vector3(sign*(w/2+.13f),0,other*(d/2+.13f));
                        Part(root.transform,"CornerSeal"+sign+other,c,new Vector3(.39f,.075f,.39f),.035f,gold);
                        Part(root.transform,"CornerJade"+sign+other,c+Vector3.up*.037f,new Vector3(.29f,.020f,.29f),.014f,jade);
                        for(int ring=0;ring<3;ring++)
                        {
                            float size=.23f-ring*.055f,offset=ring*.0275f;
                            Part(root.transform,"SealCarvingH"+sign+other+ring,c+new Vector3(offset,.053f,-size/2),new Vector3(size,.012f,.012f),.004f,gold);
                            Part(root.transform,"SealCarvingV"+sign+other+ring,c+new Vector3(size/2,.053f,offset),new Vector3(.012f,.012f,size),.004f,gold);
                        }
                    }
                }
                Combine(root);PrefabUtility.SaveAsPrefabAsset(root,Output+"/CourtyardBoard_5x5.prefab");
                AssetDatabase.SaveAssets();Debug.Log("PRODUCTION_BOARD built 5x5 pitch=1.55x1.85 surfaceY=0 renderers="+root.GetComponentsInChildren<Renderer>().Length);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            if(Application.isBatchMode){RenderPreview();EditorApplication.Exit(0);}
        }

        public static void RenderPreview()
        {
            var board=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/CourtyardBoard_5x5.prefab"));
            var cameraObject=new GameObject("BoardArtReviewCamera");var camera=cameraObject.AddComponent<Camera>();
            var target=new RenderTexture(1600,1200,24,RenderTextureFormat.ARGB32);var pixels=new Texture2D(1600,1200,TextureFormat.RGBA32,false);
            try
            {
                ProductionCharacterAssembly.Validate(board);
                foreach(var t in board.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
                camera.cullingMask=1<<30;camera.orthographic=true;camera.orthographicSize=6.3f;camera.nearClipPlane=.1f;camera.farClipPlane=100;
                camera.transform.position=new Vector3(8,13,16);camera.transform.LookAt(Vector3.zero);camera.backgroundColor=new Color(.08f,.12f,.14f,1);camera.clearFlags=CameraClearFlags.SolidColor;camera.targetTexture=target;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
                if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("Board art preview requires URP single-camera render support.");
                RenderPipeline.SubmitRenderRequest(camera,request);
                var previous=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1600,1200),0,0);pixels.Apply();RenderTexture.active=previous;
                Directory.CreateDirectory("../model-quality/board-stage8");File.WriteAllBytes("../model-quality/board-stage8/board-preview.png",pixels.EncodeToPNG());
                Debug.Log("PRODUCTION_BOARD_PREVIEW exported actual prefab 1600x1200");
            }
            finally{UnityEngine.Object.DestroyImmediate(board);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(pixels);target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }

        private static Material Material(string name,Color color,float grain,float metal)
        {
            var path=Output+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/Shaders/ProductionBoardSurface.shader"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Grain",grain);m.SetFloat("_Metal",metal);EditorUtility.SetDirty(m);return m;
        }

        private static void Part(Transform parent,string name,Vector3 center,Vector3 size,float bevel,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=center;
            go.AddComponent<MeshFilter>().sharedMesh=ChamferBox(size,bevel);go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        private static Mesh ChamferBox(Vector3 size,float bevel)
        {
            float b=Mathf.Min(bevel,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.45f),hx=size.x/2,hz=size.z/2;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int ring=0;ring<4;ring++)
            {
                float inset=ring==0 || ring==3?b:0,y=ring==0?-size.y/2:ring==1?-size.y/2+b:ring==2?size.y/2-b:size.y/2;
                float x=hx-inset,z=hz-inset,c=b*.75f;
                var contour=new[]{new Vector2(-x+c,-z),new Vector2(x-c,-z),new Vector2(x,-z+c),new Vector2(x,z-c),new Vector2(x-c,z),new Vector2(-x+c,z),new Vector2(-x,z-c),new Vector2(-x,-z+c)};
                foreach(var p in contour){vertices.Add(new Vector3(p.x,y,p.y));uv.Add(new Vector2(p.x/size.x+.5f,p.y/size.z+.5f));}
            }
            for(int ring=0;ring<3;ring++)for(int i=0;i<8;i++){int a=ring*8+i,bnext=ring*8+(i+1)%8;triangles.AddRange(new[]{a,a+8,bnext,bnext,a+8,bnext+8});}
            int bottom=vertices.Count;vertices.Add(new Vector3(0,-size.y/2,0));uv.Add(Vector2.one*.5f);
            int top=vertices.Count;vertices.Add(new Vector3(0,size.y/2,0));uv.Add(Vector2.one*.5f);
            for(int i=0;i<8;i++){int next=(i+1)%8;triangles.AddRange(new[]{bottom,i,next,top,24+next,24+i});}
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private static void Combine(GameObject root)
        {
            var parts=root.GetComponentsInChildren<MeshRenderer>();
            foreach(var group in parts.GroupBy(r=>r.sharedMaterial))
            {
                string name=group.Key.name;var mesh=new Mesh{name=name};
                mesh.CombineMeshes(group.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=root.transform.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray());
                string path=Output+"/Meshes/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{saved.Clear(false);EditorUtility.CopySerialized(mesh,saved);EditorUtility.SetDirty(saved);UnityEngine.Object.DestroyImmediate(mesh);}
                var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=saved;go.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
            foreach(var part in parts){var mesh=part.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(part.gameObject);UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
