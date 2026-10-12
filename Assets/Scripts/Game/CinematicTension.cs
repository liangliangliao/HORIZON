using System;
using System.Linq;

namespace Horizon.Game
{
    // Attention choreography only. Cue times, receipt values and movie length
    // stay owned by the original reward director.
    public sealed class CinematicTension
    {
        public readonly bool Enabled, SourceCloseup;
        public readonly float FirstRelease, FirstHold, PreviewStart, RevealHold, Reveal, ChargeAt;
        private readonly RewardPlan plan;
        public CinematicTension(RewardPlan value)
        {
            plan=value ?? throw new ArgumentNullException(nameof(value));
            var visual=new RewardSpectacle(value);
            Enabled=visual.HasHero && value.Event.tier>=RewardTier.Epic;
            SourceCloseup=Enabled && (value.Event.kind==DomainEventKind.Cascade || value.Event.kind==DomainEventKind.CausalSingularity);
            FirstRelease=value.Cues.FirstOrDefault(c=>c.NodeHit)?.Time ?? visual.ImpactTime;
            FirstHold=Math.Max(0,FirstRelease-Math.Min(.22f,FirstRelease*.18f));
            ChargeAt=value.Cues.FirstOrDefault(c=>!c.NodeHit && c.Phase==CinematicPhase.Charge)?.Time ?? 0;
            Reveal=visual.HeroTime;
            float available=Reveal-visual.ImpactTime;
            PreviewStart=SourceCloseup && available>1?Math.Max(visual.ImpactTime+.65f,Reveal-1.25f):Reveal;
            RevealHold=Math.Max(PreviewStart,Reveal-(value.Event.tier==RewardTier.Mythic?.48f:.34f));
        }
        public CinematicTensionFrame Sample(float seconds,bool reducedMotion=false)
        {
            if(float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            float time=Math.Max(0,Math.Min(plan.Duration,seconds));
            if(!Enabled || reducedMotion || time>=plan.Duration) return new CinematicTensionFrame(time,0,0,0,false);
            float opening=SourceCloseup?1-Ease(FirstRelease+.08f,FirstRelease+1.10f,time):0;
            float preview=PreviewStart<Reveal && time>=PreviewStart && time<Reveal?Ease(PreviewStart,RevealHold,time):0;
            bool firstQuiet=SourceCloseup && time>=FirstHold && time<FirstRelease;
            bool revealQuiet=PreviewStart<Reveal && time>=RevealHold && time<Reveal;
            float pose=firstQuiet?FirstHold:revealQuiet?RevealHold:time;
            return new CinematicTensionFrame(pose,Math.Max(opening,preview),preview,
                Ease(ChargeAt,FirstHold,pose),firstQuiet || revealQuiet);
        }
        private static float Ease(float from,float to,float time)
        { float p=Math.Max(0,Math.Min(1,(time-from)/Math.Max(.001f,to-from))); return p*p*(3-2*p); }
    }
    public readonly struct CinematicTensionFrame
    {
        public readonly float PoseTime, Focus, Preview, Charge;
        public readonly bool Silent;
        public CinematicTensionFrame(float pose,float focus,float preview,float charge,bool silent)
        { PoseTime=pose; Focus=focus; Preview=preview; Charge=charge; Silent=silent; }
    }
}
