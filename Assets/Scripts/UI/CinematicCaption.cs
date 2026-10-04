using Horizon.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon.UI
{
    public sealed class CinematicCaption : MonoBehaviour
    {
        public Text Title, Story, Multiplier, Resources;
        public Image Flash;
        public string EventId;
        private CinematicDirector director;
        private float flashAmount;
        public void Bind(CinematicDirector value)
        {
            CinematicTypography.Upgrade(Title); CinematicTypography.Upgrade(Story); CinematicTypography.Upgrade(Multiplier); CinematicTypography.Upgrade(Resources);
            director=value; director.Started+=OnStarted; director.Cue+=OnCue;
            if(director.Current!=null) OnStarted(director.Current);
        }
        private void OnStarted(RewardPlan plan)
        {
            if(!string.IsNullOrEmpty(EventId) && plan.Event.id!=EventId) return;
            if(Title!=null) Title.text=plan.Event.title;
            if(Story!=null) Story.text=plan.Event.detail;
            if(Resources!=null) Resources.text=plan.ResourceCopy;
            if(Multiplier!=null) Multiplier.text=plan.PresentationMultiplier>1?
                (plan.Event.kind==DomainEventKind.Cascade || plan.Event.kind==DomainEventKind.CausalSingularity?"真实因果深度 ×":"韧性链 ×")+plan.PresentationMultiplier:"";
        }
        private void OnCue(CinematicCue cue)
        {
            if(director.Current==null || !string.IsNullOrEmpty(EventId) && director.Current.Event.id!=EventId) return;
            if(Story!=null && !string.IsNullOrEmpty(cue.Copy)) Story.text=cue.Copy;
            if(cue.Phase==CinematicPhase.Impact && !cue.NodeHit && !VisualPreferences.ReducedMotion) flashAmount=.18f;
            if(cue.Phase==CinematicPhase.Settlement && Story!=null) Story.text=director.Current.Event.detail;
        }
        private void Update()
        {
            if(Flash==null || VisualPreferences.Paused) return;
            flashAmount=Mathf.MoveTowards(flashAmount,0,Time.unscaledDeltaTime*1.6f);
            Flash.color=new Color(.72f,1,.91f,flashAmount);
        }
        private void OnDestroy()
        { if(director==null) return; director.Started-=OnStarted; director.Cue-=OnCue; }
    }
}
