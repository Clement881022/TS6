#nullable enable
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace SanGuo.Client.Editor
{
    /// <summary>Freeze pose samples in the isolated art review build, for real player rendering.</summary>
    public static class ProductionMotionProof
    {
        public static void BakeFrozenSamples()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/build/character-copy/"))throw new InvalidOperationException("Proof assets must only be baked in build/character-copy.");
            var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-artProofModels");
            string[] ids=(arg>=0?args[arg+1]:"guanyu").Split(',');
            const string output="Assets/Resources/ProductionCharacters";
            Directory.CreateDirectory(output+"/ProofScratch");
            foreach(string id in ids)
            {
                var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(output+"/"+id+".prefab"));
                var set=root.GetComponent<CharacterClipSet>();
                var poses=new[]{("Idle",set.Idle),("Attack",set.Attack),("Cast",set.Cast),("Hit",set.Hit),("Die",set.Die)};
                var initial=root.GetComponentsInChildren<Transform>().Select(t=>(t,t.localPosition,t.localRotation,t.localScale)).ToArray();
                foreach(var pose in poses)
                {
                    if(pose.Item2==null)continue;
                    foreach(int percent in new[]{0,25,50,75,99})
                    {
                        foreach(var p in initial){p.t.localPosition=p.localPosition;p.t.localRotation=p.localRotation;p.t.localScale=p.localScale;}
                        var source=pose.Item2;float time=source.length*percent/100f;
                        var frozen=UnityEngine.Object.Instantiate(source);string name="proof_"+id+"_"+pose.Item1+"_"+percent.ToString("00");frozen.name=name;
                        foreach(var binding in AnimationUtility.GetCurveBindings(frozen))
                        {
                            float value=AnimationUtility.GetEditorCurve(source,binding).Evaluate(time);
                            AnimationUtility.SetEditorCurve(frozen,binding,AnimationCurve.Constant(0,1,value));
                        }
                        foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(frozen))
                        {
                            var sourceKeys=AnimationUtility.GetObjectReferenceCurve(source,binding);
                            var chosen=sourceKeys.LastOrDefault(k=>k.time<=time).value;
                            AnimationUtility.SetObjectReferenceCurve(frozen,binding,new[]{new ObjectReferenceKeyframe{time=0,value=chosen},new ObjectReferenceKeyframe{time=1,value=chosen}});
                        }
                        AnimationUtility.SetAnimationEvents(frozen,Array.Empty<AnimationEvent>());
                        var settings=AnimationUtility.GetAnimationClipSettings(frozen);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(frozen,settings);
                        string clipPath=output+"/ProofScratch/"+name+".anim";var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                        if(saved==null){AssetDatabase.CreateAsset(frozen,clipPath);saved=frozen;}else{EditorUtility.CopySerialized(frozen,saved);UnityEngine.Object.DestroyImmediate(frozen);EditorUtility.SetDirty(saved);}
                        set.Idle=saved;set.Attack=saved;root.name=name;
                        saved.SampleAnimation(root,0);
                        PrefabUtility.SaveAsPrefabAsset(root,output+"/"+name+".prefab");
                        Debug.Log("ART_FROZEN_PROOF "+name+" source="+AssetDatabase.GetAssetPath(source)+" phase="+percent);
                    }
                }
                UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();if(Application.isBatchMode)EditorApplication.Exit(0);
        }
    }
}
