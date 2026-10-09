#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace SanGuo.Client.Editor
{
    /// <summary>Art assets only: a sculpted long glaive and baked two-hand motion tracks.</summary>
    public static class ProductionGuanYuGlaive
    {
        private const string Output="Assets/Resources/ProductionCharacters";
        public static void Bake()
        {
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/guanyu.prefab"));
            try
            {
                root.name="guanyu";root.transform.localScale=Vector3.one;
                var set=root.GetComponent<CharacterClipSet>();
                set.Idle!.SampleAnimation(root,0);
                foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Weapon_00029" || t.name=="Design_GuanYuGlaive").ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
                var hand=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip001 R Hand");
                var prop=new GameObject("Design_GuanYuGlaive").transform;prop.SetParent(hand,false);
                Sculpt(prop);
                set.Idle=Author(root,set.Idle!,"Idle",false);
                set.Attack=Author(root,set.Attack!,"Attack",true);
                if(set.Cast!=null)set.Cast=Author(root,set.Cast,"Cast",false);
                if(set.Hit!=null)set.Hit=Author(root,set.Hit,"Hit",false,true);
                // The falling source retains its body/arm motion. The glaive follows
                // the right palm; the support hand releases rather than extending into air.
                set.Idle.SampleAnimation(root,0);
                ProductionCharacterBaker.Normalize(root,set);
                ProductionCharacterAssembly.Validate(root);
                PrefabUtility.SaveAsPrefabAsset(root,Output+"/guanyu.prefab");
                AssetDatabase.SaveAssets();
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        private static void Sculpt(Transform prop)
        {
            var red=new Color(.18f,.035f,.022f);var gold=new Color(.83f,.57f,.20f);var jade=new Color(.025f,.21f,.12f);
            void Tube(string name,Vector3[] p,float r,Color c,int sides=24)=>ProductionCharacterProps.Piece(prop,"GuanyuGlaive"+name,ProductionCharacterProps.Tube(p,r,sides),c,Output);
            Tube("Shaft",new[]{new Vector3(0,0,-.58f),new Vector3(0,0,.55f)},.017f,red,32);
            Tube("GripLeather",new[]{new Vector3(0,0,-.10f),new Vector3(0,0,.11f)},.020f,new Color(.07f,.035f,.021f));
            // Separate raised spirals give leather wrap a readable silhouette.
            var wrap=new List<Vector3>();for(int i=0;i<=240;i++){float t=i/240f,a=t*Mathf.PI*24;wrap.Add(new Vector3(Mathf.Cos(a)*.0203f,Mathf.Sin(a)*.0203f,-.1f+t*.21f));}
            Tube("GripLeatherWrap",wrap.ToArray(),.0017f,new Color(.32f,.17f,.075f),8);
            foreach(float z in new[]{-.57f,-.47f,-.12f,.12f,.42f,.48f,.53f})Tube("Collar"+z,new[]{new Vector3(0,0,z-.009f),new Vector3(0,0,z+.009f)},.023f,gold);
            Tube("Pommel",new[]{new Vector3(0,0,-.62f),new Vector3(0,0,-.58f)},.023f,gold);
            Tube("DragonSocket",new[]{new Vector3(0,0,.46f),new Vector3(0,0,.59f)},.032f,jade,32);
            var dragon=new List<Vector3>();for(int i=0;i<=96;i++){float t=i/96f,a=t*Mathf.PI*4;dragon.Add(new Vector3(Mathf.Cos(a)*.034f,Mathf.Sin(a)*.034f,.47f+t*.11f));}
            Tube("DragonCoil",dragon.ToArray(),.008f,gold,12);
            // Convex bevelled cross sections: an actual blade ridge and silver edge,
            // rather than a flat plate or a relief claim made from texture alone.
            const int rings=44;var v=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int i=0;i<=rings;i++)
            {
                float t=i/(float)rings,z=.56f+t*.47f;
                float spine=-.010f-.16f*t*t;
                float width=.09f*Mathf.Sin(Mathf.Pow(t,.63f)*Mathf.PI)+.028f*(1-t);
                float ridge=.014f*Mathf.Sin(t*Mathf.PI)+.002f;
                v.AddRange(new[]{new Vector3(spine,0,z),new Vector3(spine+width*.45f,ridge,z),new Vector3(spine+width,0,z),new Vector3(spine+width*.45f,-ridge,z)});
                uv.AddRange(new[]{new Vector2(0,t),new Vector2(.45f,t),new Vector2(1,t),new Vector2(.45f,t)});
                if(i<rings)for(int side=0;side<4;side++){int a=i*4+side,b=i*4+(side+1)%4;triangles.AddRange(new[]{a,a+4,b,b,a+4,b+4});}
            }
            triangles.AddRange(new[]{0,1,2,0,2,3,rings*4,rings*4+2,rings*4+1,rings*4,rings*4+3,rings*4+2});
            var mesh=new Mesh{name="GuanyuGlaiveBlade"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            var blade=ProductionCharacterProps.Piece(prop,"GuanyuGlaiveBlade",mesh,Color.white,Output);
            blade.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"/Textures/guanyu_glaive_blade.png"));
            var ridgePath=new List<Vector3>();for(int i=0;i<=44;i++){float t=i/44f;ridgePath.Add(new Vector3(-.01f-.16f*t*t,.002f,.56f+t*.47f));}
            Tube("GoldSpine",ridgePath.ToArray(),.005f,gold,10);
            foreach(int sign in new[]{-1,1})
            {
                var ornament=new List<Vector3>();for(int i=0;i<=72;i++){float t=i/72f;ornament.Add(new Vector3(.02f+.021f*Mathf.Sin(t*Mathf.PI*3)-.05f*t*t,sign*.016f,.60f+t*.22f));}
                Tube("CloudInlay"+sign,ornament.ToArray(),.0025f,gold,8);
            }
            foreach(var r in prop.GetComponentsInChildren<MeshRenderer>())
            {
                var m=r.sharedMaterial;m.SetFloat("_Smoothness",r.name.Contains("Blade")?.6f:.36f);m.SetFloat("_MetalStrength",r.name.Contains("Shaft") || r.name.Contains("Leather")?.08f:.72f);EditorUtility.SetDirty(m);
            }
        }
        private static AnimationClip Author(GameObject root,AnimationClip source,string motion,bool attack,bool hit=false)
        {
            // Read the original clip, even when re-baking an existing authored asset.
            string originalPath=Output+"/Animations/guanyu_GlaiveSource"+motion+".anim";
            var original=AssetDatabase.LoadAssetAtPath<AnimationClip>(originalPath);
            if(original==null){original=UnityEngine.Object.Instantiate(source);original.name="guanyu_GlaiveSource"+motion;AssetDatabase.CreateAsset(original,originalPath);}
            var result=UnityEngine.Object.Instantiate(original);result.name="guanyu_Glaive"+motion;
            var all=root.GetComponentsInChildren<Transform>();Transform Find(string n)=>all.First(t=>t.name==n);
            var right=Find("Bip001 R Hand");var left=Find("Bip001 L Hand");var prop=Find("Design_GuanYuGlaive");
            var targets=new[]{left.parent.parent,left.parent,left,right.parent.parent,right.parent,right,prop};
            var curves=targets.ToDictionary(t=>t,t=>Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray());
            var initial=all.Select(t=>(t,t.localPosition,t.localRotation,t.localScale)).ToArray();
            float duration=Mathf.Max(.1f,original.length);int frames=Mathf.CeilToInt(duration*30);
            float maxError=0;
            for(int i=0;i<=frames;i++)
            {
                foreach(var p in initial){p.t.localPosition=p.localPosition;p.t.localRotation=p.localRotation;p.t.localScale=p.localScale;}
                float time=duration*i/frames,phase=i/(float)frames;original.SampleAnimation(root,time);
                var axis=new Vector3(-.68f,.73f,.06f).normalized;
                if(attack)
                {
                    float swing=Mathf.SmoothStep(0,1,Mathf.Clamp01((phase-.18f)/.43f));
                    float recover=Mathf.SmoothStep(0,1,Mathf.Clamp01((phase-.68f)/.32f));
                    axis=Vector3.Slerp(axis,new Vector3(-.78f,-.32f,.54f).normalized,swing*(1-recover));
                }
                var worldAxis=root.transform.TransformDirection(axis);
                const float halfSpacing=.11f;
                var rightShoulder=right.parent.parent;var leftShoulder=left.parent.parent;
                float rightReach=(Vector3.Distance(rightShoulder.position,right.parent.position)+Vector3.Distance(right.parent.position,right.position))*.97f;
                float leftReach=(Vector3.Distance(leftShoulder.position,left.parent.position)+Vector3.Distance(left.parent.position,left.position))*.97f;
                var rightCentre=rightShoulder.position+worldAxis*halfSpacing;
                var leftCentre=leftShoulder.position-worldAxis*halfSpacing;
                var centre=(rightShoulder.position+leftShoulder.position)*.5f+root.transform.forward*.12f-root.transform.up*.05f;
                if(hit)centre-=root.transform.forward*.03f*Mathf.Sin(phase*Mathf.PI);
                // Project the shared shaft centre into BOTH arms' reach volumes.
                // Independent clamping turns the shaft upright during a slash.
                for(int pass=0;pass<16;pass++)
                {
                    centre=rightCentre+Vector3.ClampMagnitude(centre-rightCentre,rightReach);
                    centre=leftCentre+Vector3.ClampMagnitude(centre-leftCentre,leftReach);
                }
                var rightTarget=centre-worldAxis*halfSpacing;
                var leftTarget=centre+worldAxis*halfSpacing;
                Reach(right,rightTarget,root.transform.TransformDirection(new Vector3(.8f,-.6f,0)));
                Reach(left,leftTarget,root.transform.TransformDirection(new Vector3(-.8f,-.5f,0)));
                // Hand X runs from fingers back to wrist; Y traverses the closed fist.
                right.rotation=Quaternion.LookRotation(root.transform.forward,worldAxis);
                left.rotation=Quaternion.LookRotation(root.transform.forward,worldAxis);
                // The palm is beyond the wrist joint. Place the shaft through the
                // palm centre and aim it at the support palm, not at the wrist origins.
                var palmOffset=new Vector3(-.033f,0,0);
                prop.position=right.TransformPoint(palmOffset);
                var support=left.TransformPoint(palmOffset);
                worldAxis=(support-prop.position).normalized;
                prop.rotation=Quaternion.LookRotation(worldAxis,root.transform.forward);
                prop.localScale=Vector3.one;
                if(Vector3.Dot(prop.TransformVector(Vector3.forward),worldAxis)<0)prop.localScale=-Vector3.one;
                float error=Vector3.Cross(support-prop.position,prop.TransformVector(Vector3.forward).normalized).magnitude;
                maxError=Mathf.Max(maxError,error);
                foreach(var t in targets)
                {
                    var c=curves[t];var p=t.localPosition;var q=t.localRotation;
                    c[0].AddKey(time,p.x);c[1].AddKey(time,p.y);c[2].AddKey(time,p.z);c[3].AddKey(time,q.x);c[4].AddKey(time,q.y);c[5].AddKey(time,q.z);c[6].AddKey(time,q.w);
                }
            }
            foreach(var p in initial){p.t.localPosition=p.localPosition;p.t.localRotation=p.localRotation;p.t.localScale=p.localScale;}
            foreach(var t in targets)
            {
                string path=AnimationUtility.CalculateTransformPath(t,root.transform);
                foreach(var binding in AnimationUtility.GetCurveBindings(result).Where(b=>b.path==path && (b.propertyName.Contains("Rotation") || b.propertyName.Contains("Euler") || b.propertyName.Contains("Position"))).ToArray())AnimationUtility.SetEditorCurve(result,binding,null);
                for(int k=0;k<7;k++)AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve(path,typeof(Transform),k<3?"m_LocalPosition."+"xyz"[k]:"m_LocalRotation."+"xyzw"[k-3]),curves[t][k]);
            }
            result.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(result);settings.loopTime=motion=="Idle";AnimationUtility.SetAnimationClipSettings(result,settings);
            string pathOut=Output+"/Animations/"+result.name+".anim";var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(pathOut);
            if(saved==null){AssetDatabase.CreateAsset(result,pathOut);saved=result;}else{EditorUtility.CopySerialized(result,saved);EditorUtility.SetDirty(saved);UnityEngine.Object.DestroyImmediate(result);}
            Debug.Log("ART_GLAIVE_GRIP "+motion+" sampledFrames="+(frames+1)+" maxSupportAxisError="+maxError+" source="+AssetDatabase.GetAssetPath(original));
            return saved;
        }
        private static void Reach(Transform hand,Vector3 target,Vector3 bend)
        {
            var elbow=hand.parent;var shoulder=elbow.parent;var start=shoulder.position;
            float a=Vector3.Distance(start,elbow.position),b=Vector3.Distance(elbow.position,hand.position);
            var delta=target-start;float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.0001f,a+b-.0001f);var axis=delta.normalized;
            float along=(a*a-b*b+d*d)/(2*d);
            var desired=start+axis*along+Vector3.ProjectOnPlane(bend,axis).normalized*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            shoulder.rotation=Quaternion.FromToRotation(elbow.position-start,desired-start)*shoulder.rotation;
            elbow.rotation=Quaternion.FromToRotation(hand.position-elbow.position,start+axis*d-elbow.position)*elbow.rotation;
        }
    }
}
