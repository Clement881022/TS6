#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SanGuo.Client.Editor
{
    /// <summary>Author arm and prop tracks into animation assets; no runtime IK changes.</summary>
    public static class ProductionArcherAnimation
    {
        public static void Apply(GameObject root,string id,string output)
        {
            if(id!="r_archer")return;
            var set=root.GetComponent<CharacterClipSet>();
            var rig=root.GetComponent<ArcherPoseRig>();if(rig!=null)UnityEngine.Object.DestroyImmediate(rig);
            set.Idle=Author(root,set.Idle,id+"_BowIdle",output,false);
            set.Attack=Author(root,set.Attack,id+"_BowAttack",output,true);
            set.Idle.SampleAnimation(root,0);
        }

        private static AnimationClip Author(GameObject root,AnimationClip source,string name,string output,bool attack)
        {
            var all=root.GetComponentsInChildren<Transform>();
            Transform Bone(string n)=>all.First(t=>t.name==n);
            var left=Bone("Bip001 L Hand");var right=Bone("Bip001 R Hand");var bow=Bone("PropBow");var arrow=Bone("PropArrow");
            var targets=new[]{left.parent.parent,left.parent,left,right.parent.parent,right.parent,right,bow,arrow};
            var initial=all.ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
            var curves=targets.ToDictionary(t=>t,t=>Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray());
            float duration=Mathf.Max(.1f,source.length);int frames=Mathf.CeilToInt(duration*30);float bowScale=.72f,arrowScale=1;
            for(int frame=0;frame<=frames;frame++)
            {
                float time=duration*frame/frames;
                foreach(var t in all){var p=initial[t];t.localPosition=p.localPosition;t.localRotation=p.localRotation;t.localScale=p.localScale;}
                source.SampleAnimation(root,time);
                float phase=frame/(float)frames;
                float draw=attack?(phase<.45f?Mathf.Lerp(.65f,1,phase/.45f):Mathf.Lerp(1,.35f,Mathf.Clamp01((phase-.45f)/.2f))):.65f;
                float height=root.transform.InverseTransformPoint((left.parent.parent.position+right.parent.parent.position)*.5f).y;
                var leftTarget=root.transform.TransformPoint(new Vector3(-.235f,height-.015f,.235f));
                var rightTarget=root.transform.TransformPoint(new Vector3(Mathf.Lerp(.05f,.14f,draw),height+.015f,Mathf.Lerp(.17f,.075f,draw)));
                Reach(left,leftTarget,root.transform.TransformDirection(new Vector3(-.8f,-.7f,0)));
                Reach(right,rightTarget,root.transform.TransformDirection(new Vector3(.8f,-.65f,-.2f)));
                left.rotation=Quaternion.LookRotation(root.transform.forward,root.transform.up);
                // The imported hand bones use a uniform negative scale. Compensate
                // on props so a world-space aiming rotation does not mirror the arrow.
                bow.localRotation=Quaternion.identity;bow.localPosition=Vector3.zero;bow.localScale=Vector3.one*.72f;
                if(Vector3.Dot(bow.TransformVector(Vector3.up),root.transform.up)<0)bow.localScale*=-1;
                var aim=(left.position-right.position).normalized;
                arrow.position=right.position;arrow.rotation=Quaternion.LookRotation(aim,root.transform.up);arrow.localScale=Vector3.one;
                if(Vector3.Dot(arrow.TransformVector(Vector3.forward),aim)<0)arrow.localScale*=-1;
                bowScale=bow.localScale.x;arrowScale=arrow.localScale.x;
                if(Vector3.Dot(arrow.TransformVector(Vector3.forward).normalized,aim)<.999f)throw new InvalidOperationException("Mirrored arrow animation: "+name);
                foreach(var t in targets)
                {
                    var c=curves[t];var p=t.localPosition;var q=t.localRotation;
                    c[0].AddKey(time,p.x);c[1].AddKey(time,p.y);c[2].AddKey(time,p.z);
                    c[3].AddKey(time,q.x);c[4].AddKey(time,q.y);c[5].AddKey(time,q.z);c[6].AddKey(time,q.w);
                }
            }
            foreach(var t in all){var p=initial[t];t.localPosition=p.localPosition;t.localRotation=p.localRotation;t.localScale=p.localScale;}
            var result=UnityEngine.Object.Instantiate(source);result.name=name;
            var names=new[]{"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
            foreach(var t in targets)
                for(int k=0;k<names.Length;k++)
                    AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(t,root.transform),typeof(Transform),names[k]),curves[t][k]);
            foreach(var prop in new[]{bow,arrow})
                foreach(string axis in new[]{"x","y","z"})
                    AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(prop,root.transform),typeof(Transform),"m_LocalScale."+axis),AnimationCurve.Constant(0,duration,prop==bow?bowScale:arrowScale));
            result.EnsureQuaternionContinuity();
            var settings=AnimationUtility.GetAnimationClipSettings(result);settings.loopTime=!attack;AnimationUtility.SetAnimationClipSettings(result,settings);
            System.IO.Directory.CreateDirectory(output+"/Animations");string path=output+"/Animations/"+name+".anim";
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(existing==null){AssetDatabase.CreateAsset(result,path);existing=result;}else{EditorUtility.CopySerialized(result,existing);UnityEngine.Object.DestroyImmediate(result);EditorUtility.SetDirty(existing);}
            return existing;
        }

        private static void Reach(Transform hand,Vector3 target,Vector3 bend)
        {
            var forearm=hand.parent;var upper=forearm.parent;
            Vector3 start=upper.position;float a=Vector3.Distance(start,forearm.position),b=Vector3.Distance(forearm.position,hand.position);
            var delta=target-start;float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);
            var axis=delta.normalized;var side=Vector3.ProjectOnPlane(bend,axis).normalized;
            float along=(a*a-b*b+d*d)/(2*d);float perpendicular=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            var elbow=start+axis*along+side*perpendicular;
            upper.rotation=Quaternion.FromToRotation(forearm.position-start,elbow-start)*upper.rotation;
            var reachable=start+axis*d;
            forearm.rotation=Quaternion.FromToRotation(hand.position-forearm.position,reachable-forearm.position)*forearm.rotation;
        }
    }
}
