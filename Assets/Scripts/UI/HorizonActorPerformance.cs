using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;

namespace Horizon.UI
{
    public enum HorizonBodyAction { Wait, Walk, Reach, Operate, Rest, Stumble, Stand, Celebrate, Point }

    // The director supplies progress: no private clock can drift through Hit Stop or pause.
    // RigBuilder's Animator graph synchronizes the procedural body pose and solves gaze.
    public sealed class HorizonActorPerformance : MonoBehaviour
    {
        private HorizonActor actor;
        private RigBuilder builder;
        private MultiAimConstraint gaze;
        private Transform target;
        private bool performing;
        public bool GraphReady { get { return builder!=null && builder.graph.IsValid(); } }
        public HorizonBodyAction CurrentAction { get; private set; }
        public static HorizonActorPerformance Attach(HorizonActor value)
        {
            var performance=value.GetComponent<HorizonActorPerformance>();
            if(performance==null) performance=value.gameObject.AddComponent<HorizonActorPerformance>();
            performance.Initialize(value); return performance;
        }
        private void Initialize(HorizonActor value)
        {
            if(actor!=null) return;
            actor=value;
            Animator animator=GetComponent<Animator>(); if(animator==null) animator=gameObject.AddComponent<Animator>();
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; animator.applyRootMotion=false;
            // The director writes the body pose in the scene. Feed those
            // transforms into the animation stream before solving gaze;
            // otherwise Animator restores its bind pose on every evaluation.
            foreach(Transform bone in new[] { actor.Head,actor.Chest,actor.LeftArm,actor.RightArm,actor.LeftLeg,actor.RightLeg })
                if(bone!=null && bone.GetComponent<RigTransform>()==null) bone.gameObject.AddComponent<RigTransform>();
            builder=gameObject.AddComponent<RigBuilder>(); builder.enabled=false;
            Transform rigRoot=new GameObject("Character performance rig").transform; rigRoot.SetParent(transform,false);
            Rig rig=rigRoot.gameObject.AddComponent<Rig>();
            target=new GameObject("Gaze target").transform; target.SetParent(transform,false); target.localPosition=new Vector3(0,1.65f,-3);
            var aim=new GameObject("Look toward the action"); aim.transform.SetParent(rigRoot,false);
            gaze=aim.AddComponent<MultiAimConstraint>();
            gaze.Reset();
            var data=gaze.data; data.constrainedObject=actor.Head;
            data.aimAxis=MultiAimConstraintData.Axis.Z_NEG; data.upAxis=MultiAimConstraintData.Axis.Y;
            var sources=new WeightedTransformArray(); sources.Add(new WeightedTransform(target,1)); data.sourceObjects=sources;
            data.maintainOffset=false; gaze.data=data; gaze.weight=.6f;
            builder.layers.Add(new RigLayer(rig));
        }
        public void Play(HorizonBodyAction action,float progress,float emphasis=1)
        {
            if(actor==null || actor.Head==null) return;
            performing=true; CurrentAction=action; actor.enabled=false;
            GetComponent<LODGroup>()?.ForceLOD(0);
            float p=Mathf.Clamp01(progress), e=Mathf.Clamp(emphasis,.25f,1.5f), swing=Mathf.Sin(p*Mathf.PI*8)*28*e;
            float left=0,right=0,leg=0,head=0,spread=5;
            switch(action)
            {
                case HorizonBodyAction.Walk:left=swing;right=-swing;leg=swing;break;
                case HorizonBodyAction.Reach:left=20;right=Mathf.Sin(p*Mathf.PI)*85*e;head=12;break;
                case HorizonBodyAction.Operate:left=48+Mathf.Sin(p*24)*8;right=55+Mathf.Cos(p*24)*12;head=16;break;
                case HorizonBodyAction.Rest:left=18;right=18;head=22-14*p;break;
                case HorizonBodyAction.Stumble:left=65*p;right=90*p;leg=-25*p;head=22*p;spread=12+20*p;break;
                case HorizonBodyAction.Stand:left=45*(1-p);right=55*(1-p);leg=-20*(1-p);head=20*(1-p);break;
                case HorizonBodyAction.Celebrate:left=65*Mathf.Sin(p*Mathf.PI*.5f);right=left;spread=22;head=-6;break;
                case HorizonBodyAction.Point:left=8;right=80*e;head=-3;break;
                default:head=Mathf.Sin(p*Mathf.PI*2)*3;left=right=Mathf.Sin(p*Mathf.PI*2)*2;break;
            }
            actor.LeftArm.localRotation=Quaternion.Euler(left,0,-spread);
            actor.RightArm.localRotation=Quaternion.Euler(right,0,spread);
            actor.LeftLeg.localRotation=Quaternion.Euler(-leg,0,0); actor.RightLeg.localRotation=Quaternion.Euler(leg,0,0);
            actor.Head.localRotation=Quaternion.Euler(head,0,0);
            actor.SampleBreath(p*4,action==HorizonBodyAction.Rest?p:0);
            target.localPosition=new Vector3(action==HorizonBodyAction.Point?.7f:0,1.65f-Mathf.Sin(head*Mathf.Deg2Rad)*3,-3);
            if(!GraphReady && gameObject.activeInHierarchy) { builder.Build(); if(GraphReady) builder.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); }
            if(GraphReady)
            {
                // Animator's root is not a syncable child bone. Its cached
                // transform must not replace the director's scene placement.
                Vector3 position=transform.localPosition,scale=transform.localScale;
                Quaternion rotation=transform.localRotation;
                builder.Evaluate(0);
                transform.SetLocalPositionAndRotation(position,rotation); transform.localScale=scale;
            }
        }
        public void Release()
        {
            if(!performing) return;
            performing=false; if(actor!=null) actor.enabled=true;
            LODGroup lod=GetComponent<LODGroup>(); if(lod!=null && lod.isActiveAndEnabled) lod.ForceLOD(-1);
            if(builder!=null) builder.Clear();
        }
        private void OnEnable() { if(!performing) GetComponent<LODGroup>()?.ForceLOD(-1); }
        private void OnDisable() { Release(); }
        private void OnDestroy() { if(builder!=null) builder.Clear(); }
    }
}
