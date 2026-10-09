#nullable enable
using System.Linq;
using UnityEngine;

namespace SanGuo.Client
{
    [DefaultExecutionOrder(200)]
    public sealed class ArcherPoseRig : MonoBehaviour
    {
        private Transform? _left,_right,_bow,_arrow;
        private float _shot=-1;
        private void Awake()
        {
            var all=GetComponentsInChildren<Transform>();
            _left=all.FirstOrDefault(t=>t.name=="Bip001 L Hand");
            _right=all.FirstOrDefault(t=>t.name=="Bip001 R Hand");
            _bow=all.FirstOrDefault(t=>t.name=="PropBow");
            _arrow=all.FirstOrDefault(t=>t.name=="PropArrow");
        }
        public void Shoot()=>_shot=0;
        private void LateUpdate()
        {
            if(_left==null || _right==null || _bow==null) return;
            if(_shot>=0){_shot+=Time.deltaTime;if(_shot>.85f)_shot=-1;}
            float draw=_shot<0 ? .55f : _shot<.38f ? Mathf.Lerp(.55f,1,_shot/.38f) : Mathf.Lerp(1,.1f,Mathf.Clamp01((_shot-.38f)/.20f));
            var leftUpper=_left.parent?.parent;var rightUpper=_right.parent?.parent;
            if(leftUpper==null || rightUpper==null) return;
            float height=transform.InverseTransformPoint((leftUpper.position+rightUpper.position)*.5f).y;
            var leftTarget=transform.TransformPoint(new Vector3(.30f,height-.02f,.32f));
            var rightTarget=transform.TransformPoint(new Vector3(Mathf.Lerp(.04f,-.18f,draw),height-.04f,.13f));
            Reach(_left,leftTarget,transform.TransformDirection(new Vector3(.8f,-.6f,-.4f)));
            Reach(_right,rightTarget,transform.TransformDirection(new Vector3(-.8f,-.6f,-.4f)));
            _left.rotation=Quaternion.LookRotation(transform.forward,transform.up);
            _bow.localRotation=Quaternion.identity;_bow.localPosition=new Vector3(-.10f,0,0);
            if(_arrow!=null)
            {
                _arrow.position=_right.position;
                _arrow.rotation=Quaternion.LookRotation((_left.position-_right.position).normalized,transform.up);
            }
        }
        private static void Reach(Transform hand,Vector3 target,Vector3 bend)
        {
            var forearm=hand.parent;var upper=forearm?.parent;
            if(forearm==null || upper==null) return;
            Vector3 start=upper.position;float a=Vector3.Distance(start,forearm.position),b=Vector3.Distance(forearm.position,hand.position);
            var delta=target-start;float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);
            var axis=delta.normalized;var side=Vector3.ProjectOnPlane(bend,axis).normalized;
            float along=(a*a-b*b+d*d)/(2*d);float perpendicular=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            var elbow=start+axis*along+side*perpendicular;
            upper.rotation=Quaternion.FromToRotation(forearm.position-start,elbow-start)*upper.rotation;
            forearm.rotation=Quaternion.FromToRotation(hand.position-forearm.position,target-forearm.position)*forearm.rotation;
        }
    }
}
