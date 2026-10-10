using Horizon.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon.UI
{
    public sealed class CinematicCaption : MonoBehaviour
    {
        public Text Title, Story, Multiplier, Resources, Explanation, Prediction;
        public Image Flash;
        public string EventId;
        private CinematicDirector director;
        private float flashAmount, numberPunch;
        public void Bind(CinematicDirector value)
        {
            CinematicTypography.Upgrade(Title); CinematicTypography.Upgrade(Story); CinematicTypography.Upgrade(Multiplier); CinematicTypography.Upgrade(Resources); CinematicTypography.Upgrade(Explanation);
            CinematicTypography.Upgrade(Prediction);
            director=value; director.Started+=OnStarted; director.Cue+=OnCue;
            if(director.Current!=null) OnStarted(director.Current);
        }
        private void OnStarted(RewardPlan plan)
        {
            if(!string.IsNullOrEmpty(EventId) && plan.Event.id!=EventId) return;
            if(Title!=null) Title.text=plan.Event.title;
            if(Story!=null) Story.text=plan.Event.detail;
            if(Resources!=null) Resources.text=plan.ResourceCopy;
            if(Explanation!=null) Explanation.text=plan.Event.detail;
            if(Prediction!=null) Prediction.text=PredictionCopy(plan.Event.receipt);
            UpdateMultiplier(plan);

        }
        public static string PredictionCopy(RewardReceipt receipt)
        {
            if(receipt==null || !receipt.predictionRecorded) return "";
            string copy="原预测 · "+AxesCopy(receipt.predicted);
            return receipt.predictionResolved?copy+"\n实际变化 · "+AxesCopy(receipt.actual):copy;
        }
        private static string AxesCopy(ResourceDelta values)
        {
            string[] names={"精力","心情","专注","金钱","关系","能力"};
            int[] axes=ResourceMath.Axes(values); var copy=new string[axes.Length];
            for(int i=0;i<axes.Length;i++) copy[i]=names[i]+(axes[i]>=0?"+":"")+axes[i];
            return string.Join("  ",copy);
        }
        private void OnCue(CinematicCue cue)
        {
            if(director.Current==null || !string.IsNullOrEmpty(EventId) && director.Current.Event.id!=EventId) return;
            if(Story!=null && !string.IsNullOrEmpty(cue.Copy)) Story.text=cue.Copy;
            if(cue.Phase==CinematicPhase.Impact && !cue.NodeHit && !VisualPreferences.ReducedMotion) flashAmount=.18f;
            if(cue.Phase==CinematicPhase.Impact && !cue.NodeHit) numberPunch=.16f;
            UpdateMultiplier(director.Current);
        }
        private void UpdateMultiplier(RewardPlan plan)
        {
            if(Multiplier==null) return;
            int strength=plan.PresentationMultiplierAt(director.Elapsed);
            string actual=plan.Event.kind==DomainEventKind.Cascade || plan.Event.kind==DomainEventKind.CausalSingularity ? " · 因果深度 "+plan.Event.chainSize :
                plan.Event.kind==DomainEventKind.Comeback ? " · 韧性链 "+plan.Event.multiplier : "";
            Multiplier.text=strength>1?"演出 ×"+strength+actual:"";
        }
        private void Update()
        {
            if(VisualPreferences.Paused) return;
            if(director!=null && director.Current!=null) UpdateMultiplier(director.Current);
            numberPunch=Mathf.MoveTowards(numberPunch,0,Time.unscaledDeltaTime*.8f);
            if(Multiplier!=null) Multiplier.transform.localScale=Vector3.one*(1+numberPunch);
            if(Flash==null) return;
            flashAmount=Mathf.MoveTowards(flashAmount,0,Time.unscaledDeltaTime*1.6f);
            Flash.color=new Color(.72f,1,.91f,flashAmount);
        }
        private void OnDestroy()
        { if(director==null) return; director.Started-=OnStarted; director.Cue-=OnCue; }
    }
}
