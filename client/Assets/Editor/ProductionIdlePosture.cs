#nullable enable
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SanGuo.Client.Editor
{
    public static class ProductionIdlePosture
    {
        private const string Output="Assets/Resources/ProductionCharacters";

        public static void Bake()
        {
            foreach(string path in Directory.GetFiles(Output,"*.prefab"))
            {
                var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                try
                {
                    string id=Path.GetFileNameWithoutExtension(path);root.name=id;
                    Apply(root,id,Output);
                    ProductionCharacterBaker.Normalize(root,root.GetComponent<CharacterClipSet>());
                    ProductionCharacterAssembly.Validate(root);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            AssetDatabase.SaveAssets();
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }

        public static void Apply(GameObject root,string id,string output)
        {
            var set=root.GetComponent<CharacterClipSet>();
            if(set==null || set.Idle==null)throw new InvalidOperationException("Missing idle for posture: "+id);
            if(set.SourceIdle==null)set.SourceIdle=set.Idle;
            var source=set.SourceIdle;
            var all=root.GetComponentsInChildren<Transform>();
            var spine=all.First(t=>t.name=="Bip001 Spine");
            var head=all.First(t=>t.name=="Bip001 Head");
            var pelvis=all.First(t=>t.name=="Bip001 Pelvis");
            var feet=new[]{"Bip001 L Foot","Bip001 R Foot"}.Select(n=>all.First(t=>t.name==n)).ToArray();
            var initial=all.Select(t=>(t,t.localPosition,t.localRotation,t.localScale)).ToArray();
            var sampled=UnityEngine.Object.Instantiate(source);
            var targets=new[]{spine,head}.Concat(feet.SelectMany(f=>new[]{f.parent.parent,f.parent,f})).Distinct().ToArray();
            var curves=targets.ToDictionary(t=>t,t=>Enumerable.Range(0,4).Select(_=>new AnimationCurve()).ToArray());
            var pelvisCurves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
            float duration=Mathf.Max(.1f,source.length);int frames=Mathf.CeilToInt(duration*30);
            string role=set.MotionProfile;
            float chestOpening=role=="guard" ? 2f : role=="caster" ? 3f : 4f;
            float gazePitch=role=="caster" ? 9f : role=="polearm" ? 13f : 11f;
            set.NeckExtension=role=="caster" ? 0f : role=="guard" || role=="archer" ? .12f : .08f;
            for(int frame=0;frame<=frames;frame++)
            {
                float time=duration*frame/frames;
                foreach(var p in initial){p.t.localPosition=p.localPosition;p.t.localRotation=p.localRotation;p.t.localScale=p.localScale;}
                source.SampleAnimation(root,time);
                var footPos=feet.Select(f=>f.position).ToArray();
                var footRot=feet.Select(f=>f.rotation).ToArray();
                float lift=.035f*root.transform.lossyScale.y;
                foreach(var foot in feet)
                {
                    var hip=foot.parent.parent;var knee=foot.parent;
                    float length=Vector3.Distance(hip.position,knee.position)+Vector3.Distance(knee.position,foot.position);
                    var span=hip.position-foot.position;
                    float vertical=Vector3.Dot(span,root.transform.up);
                    float horizontalSq=Vector3.ProjectOnPlane(span,root.transform.up).sqrMagnitude;
                    float available=Mathf.Sqrt(Mathf.Max(0,length*length*.96f-horizontalSq))-vertical;
                    lift=Mathf.Min(lift,Mathf.Max(0,available));
                }
                pelvis.position+=root.transform.up*lift;
                for(int i=0;i<feet.Length;i++)
                {
                    var foot=feet[i];var knee=foot.parent;var hip=knee.parent;
                    var bend=Vector3.ProjectOnPlane(knee.position-(hip.position+footPos[i])*.5f,footPos[i]-hip.position).normalized;
                    SolveLeg(foot,footPos[i],bend);foot.rotation=footRot[i];
                    if(Vector3.Distance(foot.position,footPos[i])>.002f*root.transform.lossyScale.y)
                        throw new InvalidOperationException("Idle stance lost planted foot: "+id);
                }
                spine.rotation=Quaternion.AngleAxis(-chestOpening,root.transform.right)*spine.rotation;
                // This imported skeleton's local Y points through the face, rather
                // than Unity's usual local Z. Correct pitch without discarding yaw.
                var forward=head.TransformVector(Vector3.up).normalized;
                var horizontal=Vector3.ProjectOnPlane(forward,root.transform.up).normalized;
                float pitch=Mathf.Asin(Mathf.Clamp(Vector3.Dot(forward,root.transform.up),-1,1))*Mathf.Rad2Deg;
                var pitchAxis=Vector3.Cross(root.transform.up,horizontal).normalized;
                float breathing=.6f*Mathf.Sin(time/duration*Mathf.PI*2);
                head.rotation=Quaternion.AngleAxis(-(gazePitch+breathing-pitch),pitchAxis)*head.rotation;
                foreach(var t in curves.Keys)
                {
                    var q=t.localRotation;var c=curves[t];
                    c[0].AddKey(time,q.x);c[1].AddKey(time,q.y);c[2].AddKey(time,q.z);c[3].AddKey(time,q.w);
                }
                var pelvisPosition=pelvis.localPosition;
                pelvisCurves[0].AddKey(time,pelvisPosition.x);pelvisCurves[1].AddKey(time,pelvisPosition.y);pelvisCurves[2].AddKey(time,pelvisPosition.z);
            }
            foreach(var p in initial){p.t.localPosition=p.localPosition;p.t.localRotation=p.localRotation;p.t.localScale=p.localScale;}
            foreach(var t in curves.Keys)
            {
                string path=AnimationUtility.CalculateTransformPath(t,root.transform);
                foreach(string euler in new[]{"localEulerAnglesRaw.","localEulerAnglesBaked.","localEulerAngles."})
                    foreach(string axis in new[]{"x","y","z"})AnimationUtility.SetEditorCurve(sampled,EditorCurveBinding.FloatCurve(path,typeof(Transform),euler+axis),null);
                for(int i=0;i<4;i++)AnimationUtility.SetEditorCurve(sampled,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[i]),curves[t][i]);
            }
            string pelvisPath=AnimationUtility.CalculateTransformPath(pelvis,root.transform);
            for(int i=0;i<3;i++)AnimationUtility.SetEditorCurve(sampled,EditorCurveBinding.FloatCurve(pelvisPath,typeof(Transform),"m_LocalPosition."+"xyz"[i]),pelvisCurves[i]);
            sampled.name=id+"_UprightIdle";sampled.EnsureQuaternionContinuity();
            var settings=AnimationUtility.GetAnimationClipSettings(sampled);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(sampled,settings);
            Directory.CreateDirectory(output+"/Animations");string assetPath=output+"/Animations/"+sampled.name+".anim";
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if(existing==null){AssetDatabase.CreateAsset(sampled,assetPath);existing=sampled;}
            else{EditorUtility.CopySerialized(sampled,existing);UnityEngine.Object.DestroyImmediate(sampled);EditorUtility.SetDirty(existing);}
            set.Idle=existing;
            set.Idle.SampleAnimation(root,0);
            Debug.Log("ART_UPRIGHT_IDLE "+id+" chestOpening="+chestOpening+" gazePitch="+gazePitch+" preservesSource="+AssetDatabase.GetAssetPath(source));
        }

        private static void SolveLeg(Transform foot,Vector3 target,Vector3 bend)
        {
            var knee=foot.parent;var hip=knee.parent;var start=hip.position;
            float a=Vector3.Distance(start,knee.position),b=Vector3.Distance(knee.position,foot.position);
            var delta=target-start;float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.00001f,a+b-.00001f);
            var axis=delta.normalized;
            var side=Vector3.ProjectOnPlane(bend,axis).normalized;
            float along=(a*a-b*b+d*d)/(2*d);
            var desiredKnee=start+axis*along+side*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            hip.rotation=Quaternion.FromToRotation(knee.position-start,desiredKnee-start)*hip.rotation;
            knee.rotation=Quaternion.FromToRotation(foot.position-knee.position,target-knee.position)*knee.rotation;
        }

        public static void Inspect()
        {
            foreach(string id in new[]{"r_shield","r_archer","r_healer","guanyu","zhangfei"})
            {
                var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ProductionCharacters/"+id+".prefab"));
                try
                {
                    root.transform.localScale=Vector3.one;
                    var clips=root.GetComponent<CharacterClipSet>();clips.Idle!.SampleAnimation(root,0);
                    var all=root.GetComponentsInChildren<Transform>();
                    foreach(string name in new[]{"Bip001 Pelvis","Bip001 Spine","Bip001 Neck","Bip001 Head"})
                    {
                        var t=all.First(b=>b.name==name);
                        Debug.Log("POSTURE_INSPECT "+id+" "+name+" position="+root.transform.InverseTransformPoint(t.position).ToString("F4")
                            +" X="+root.transform.InverseTransformVector(t.TransformVector(Vector3.right)).normalized.ToString("F3")
                            +" Y="+root.transform.InverseTransformVector(t.TransformVector(Vector3.up)).normalized.ToString("F3")
                            +" Z="+root.transform.InverseTransformVector(t.TransformVector(Vector3.forward)).normalized.ToString("F3"));
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
    }
}
