using System;
using System.Linq;

namespace Horizon.Game
{
    // Attention choreography only. Cue times, receipt values and movie length
    // stay owned by the reward director.
    public sealed class CinematicTension
    {
        public readonly bool Enabled, SourceCloseup, OpeningHold;
        public readonly float FirstRelease, FirstHold, PreviewStart, RevealHold, Reveal, ChargeAt;
        private readonly RewardPlan plan;
        public CinematicTension(RewardPlan value)
        {
            plan=value ?? throw new ArgumentNullException(nameof(value));
            var visual=new RewardSpectacle(value);
            Enabled=visual.HasHero && value.Event.tier>=RewardTier.Major;
            SourceCloseup=Enabled && value.Cues.Any(c=>c.NodeHit) &&
                (value.Event.kind==DomainEventKind.Cascade || value.Event.kind==DomainEventKind.CausalSingularity);
            FirstRelease=value.Cues.FirstOrDefault(c=>c.NodeHit)?.Time ?? visual.ImpactTime;
            // Legacy causal events without a source keep their established shot.
            OpeningHold=Enabled && (SourceCloseup || value.Event.kind!=DomainEventKind.Cascade && value.Event.kind!=DomainEventKind.CausalSingularity);
            float firstPause=value.Event.tier==RewardTier.Major?.32f:value.Event.tier==RewardTier.Epic?.45f:.58f;
            FirstHold=Math.Max(0,FirstRelease-Math.Min(firstPause,FirstRelease*.28f));
            ChargeAt=value.Cues.FirstOrDefault(c=>!c.NodeHit && c.Phase==CinematicPhase.Charge)?.Time ?? 0;
            Reveal=visual.HeroTime;
            float available=Reveal-visual.ImpactTime;
            float previewWindow=value.Event.tier==RewardTier.Mythic?2.1f:value.Event.tier==RewardTier.Epic?1.65f:1.25f;
            PreviewStart=OpeningHold && available>1?Math.Max(visual.ImpactTime+.55f,Reveal-previewWindow):Reveal;
            float revealPause=value.Event.tier==RewardTier.Mythic?.78f:value.Event.tier==RewardTier.Epic?.60f:.40f;
            RevealHold=Math.Max(PreviewStart,Reveal-revealPause);
        }
        public CinematicTensionFrame Sample(float seconds,bool reducedMotion=false)
        {
            if(float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            float time=Math.Max(0,Math.Min(plan.Duration,seconds));
            if(!Enabled || reducedMotion || time>=plan.Duration) return new CinematicTensionFrame(time,0,0,0,false);
            float opening=SourceCloseup?1-Ease(FirstRelease+.08f,FirstRelease+1.10f,time):
                OpeningHold?Ease(ChargeAt,FirstHold,time)*(1-Ease(FirstRelease,FirstRelease+.55f,time))*.7f:0;
            float preview=PreviewStart<Reveal && time>=PreviewStart && time<Reveal?Ease(PreviewStart,RevealHold,time):0;
            bool firstQuiet=OpeningHold && time>=FirstHold && time<FirstRelease;
            bool revealQuiet=PreviewStart<Reveal && time>=RevealHold && time<Reveal;
            // Ease into slow motion before becoming completely still. The clock
            // and cue schedule continue normally, so text, sound and the release
            // remain synchronized even though the subject's final detail lasts.
            float pose=time;
            float firstSlow=Math.Max(ChargeAt,FirstHold-.78f);
            if(OpeningHold && time>=firstSlow && time<FirstRelease)
                pose=firstSlow+(Math.Min(time,FirstHold)-firstSlow)*.38f;
            float revealSlow=Math.Max(PreviewStart,RevealHold-.92f);
            if(PreviewStart<Reveal && time>=revealSlow && time<Reveal)
                pose=revealSlow+(Math.Min(time,RevealHold)-revealSlow)*.34f;
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
