using System;
using System.Linq;

namespace Horizon.Game
{
    // A deterministic typography timeline driven by the same clock as the
    // camera, object animation, light, sound and haptics. It describes how the
    // existing event copy is revealed; it never creates a reward or changes a
    // receipt.
    public sealed class CinematicTextChoreography
    {
        private readonly RewardPlan plan;
        private readonly CinematicCue[] chapters;
        private readonly CinematicCue[] readingChapters;
        private readonly CinematicEnergy energy;
        private readonly LifeActionStoryboard action;
        public readonly float ImpactTime, MultiplierTime, SettlementTime;

        public CinematicTextChoreography(RewardPlan value)
        {
            plan=value ?? throw new ArgumentNullException(nameof(value));
            chapters=plan.Cues.Where(c=>!c.NodeHit).OrderBy(c=>c.Time).ToArray();
            readingChapters=chapters.Where(c=>CinematicReading.IsChapter(plan.Event,c.Phase)).ToArray();
            energy=new CinematicEnergy(plan);
            action=new LifeActionStoryboard(plan);
            ImpactTime=TimeOf(CinematicPhase.Impact,plan.Duration*.52f);
            MultiplierTime=TimeOf(CinematicPhase.Multiplication,ImpactTime);
            SettlementTime=TimeOf(CinematicPhase.Settlement,plan.Duration*.86f);
        }

        public CinematicTextFrame Sample(float seconds,bool reducedMotion=false)
        {
            if(float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            float time=Clamp(seconds,0,plan.Duration), ending=1-Smooth(plan.Duration*.965f,plan.Duration,time);
            CinematicCue chapter=chapters.Length>0?chapters[0]:null;
            float next=plan.Duration;
            for(int i=0;i<chapters.Length;i++)
            {
                if(chapters[i].Time<=time) chapter=chapters[i];
                else { next=chapters[i].Time; break; }
            }
            // Technical cues keep firing, but do not replace a sentence every
            // 120ms. A complete thought spans charge / detail / silent hold.
            CinematicCue reading=readingChapters.LastOrDefault(c=>c.Time<=time) ?? readingChapters.First();
            float readingNext=readingChapters.FirstOrDefault(c=>c.Time>time)?.Time ?? plan.Duration;
            float started=reading.Time;
            next=readingNext;
            float available=Math.Max(.12f,next-started);
            // A chapter change must never make its meaning disappear.  The
            // reference rhythm swaps copy on the beat, then brightens it; a
            // fade from zero made Impact and Settlement look unlabelled in a
            // still frame even though the strings were technically present.
            float entrance=Smooth(started,started+Math.Min(.22f,available*.32f),time);
            float readableEntrance=.56f+entrance*.44f;
            CinematicEnergyFrame e=energy.Sample(time,reducedMotion);
            float flash=0;
            foreach(CinematicCue cue in chapters)
            {
                if(cue.Phase!=CinematicPhase.Impact && cue.Phase!=CinematicPhase.SecondReveal && cue.Phase!=CinematicPhase.Jackpot) continue;
                float age=time-cue.Time;
                if(age>=0) flash=Math.Max(flash,(float)Math.Exp(-age/.38f));
            }
            bool held=chapter!=null && chapter.Phase==CinematicPhase.HitStop && time<ImpactTime;
            float tremor=reducedMotion || held?0:(float)Math.Pow(e.Inward,3.2f);
            float multiplierAge=time-MultiplierTime;
            float secondReveal=TimeOf(CinematicPhase.SecondReveal,SettlementTime);
            float multiplierLinger=1-Smooth(secondReveal-.36f,secondReveal-.08f,time);
            float multiplierAlpha=energy.Celebrates && plan.PresentationMultiplier>1 && multiplierAge>=0?
                Smooth(0,.16f,multiplierAge)*multiplierLinger*ending:0;
            float multiplierPunch=multiplierAge<0?0:(float)Math.Exp(-multiplierAge/Math.Max(.12f,plan.Duration*.025f));
            string narration=!string.IsNullOrEmpty(reading.Copy)?reading.Copy:plan.Event.detail;
            CinematicCue node=plan.Cues.LastOrDefault(c=>c.NodeHit && c.Time<=time);
            if(node!=null && time<ImpactTime)
            {
                RewardEvidence cause=plan.Event.receipt?.causes.OrderBy(c=>c.day).ElementAtOrDefault(node.NodeIndex);
                if(cause!=null) narration="第 "+cause.day+" 天 · "+PresentationCopy.Chinese(cause.label);
            }
            if(action.Enabled && time<MultiplierTime) narration=action.Sample(time,reducedMotion).Copy;
            float receiptStart=Math.Max(0,SettlementTime-.08f);
            float receiptEnd=Math.Min(plan.Duration,SettlementTime+.14f);
            return new CinematicTextFrame(time,chapter?.Phase??CinematicPhase.Anticipation,
                node!=null && time<ImpactTime?"真实来路 · 第 "+(node.NodeIndex+1)+" 个节点":PhaseCopy(reading.Phase),narration,
                readableEntrance*ending,Clamp(.12f+e.Inward*.72f+e.Afterglow*.24f+flash*.72f,0,1),
                tremor,flash,1-Smooth(receiptStart,receiptEnd,time),
                multiplierAlpha,.78f+.22f*Smooth(0,.24f,multiplierAge)+multiplierPunch*.24f,
                .9f+.1f*Smooth(0,.42f,time)+flash*.055f,
                .95f+.05f*entrance,ending,Smooth(started,started+Math.Min(.85f,available*.22f),time));
        }

        private float TimeOf(CinematicPhase phase,float fallback)
        { CinematicCue cue=chapters.FirstOrDefault(c=>c.Phase==phase); return cue?.Time??fallback; }

        public static string PhaseCopy(CinematicPhase phase)
        {
            switch(phase)
            {
                case CinematicPhase.Anticipation: return "即将发生";
                case CinematicPhase.Charge: return "光正在聚向核心";
                case CinematicPhase.Escalation: return "真实来路逐一点亮";
                case CinematicPhase.HeroMoment: return "主体特写";
                case CinematicPhase.HitStop: return "屏息 · 即将揭晓";
                case CinematicPhase.Impact: return "结果抵达";
                case CinematicPhase.Multiplication: return "变化逐一显现";
                case CinematicPhase.SecondReveal: return "第二重揭晓";
                case CinematicPhase.Jackpot: return "完整结果展开";
                case CinematicPhase.Settlement: return "结果已记录";
                default: return "回到时间线";
            }
        }

        private static float Smooth(float from,float to,float value)
        { float p=Clamp((value-from)/Math.Max(.0001f,to-from),0,1); return p*p*(3-2*p); }
        private static float Clamp(float value,float minimum,float maximum)
        { return Math.Max(minimum,Math.Min(maximum,value)); }
    }

    // Human reading chapters are deliberately fewer than sensory cues. Add
    // time to presentation only, preserve node spacing and the hit-stop gap.
    public static class CinematicReading
    {
        public static bool IsChapter(DomainEvent e,CinematicPhase phase)
        {
            if(phase==CinematicPhase.Anticipation || phase==CinematicPhase.Impact || phase==CinematicPhase.Settlement) return true;
            if(e.tier<RewardTier.Major) return false;
            return phase==CinematicPhase.SecondReveal || phase==CinematicPhase.HeroMoment &&
                e.kind!=DomainEventKind.Cascade && e.kind!=DomainEventKind.CausalSingularity;
        }
        public static float ReadSeconds(string copy)
        { return Math.Max(2.2f,Math.Min(6f,1.15f+(copy?.Length??0)*.085f)); }

        public static float Retime(DomainEvent e,System.Collections.Generic.List<CinematicCue> cues,float duration)
        {
            var anchors=cues.Where(c=>!c.NodeHit && (IsChapter(e,c.Phase) ||
                c.Phase==CinematicPhase.Multiplication || c.Phase==CinematicPhase.SecondReveal ||
                c.Phase==CinematicPhase.ReturnToGame)).OrderBy(c=>c.Time).ToArray();
            float[] original=anchors.Select(c=>c.Time).ToArray(), mapped=new float[anchors.Length];
            bool cascade=e.kind==DomainEventKind.Cascade || e.kind==DomainEventKind.CausalSingularity;
            for(int i=1;i<anchors.Length;i++)
            {
                CinematicCue prior=anchors[i-1];
                float span=original[i]-original[i-1];
                float minimum=prior.Phase==CinematicPhase.Multiplication?new CinematicOutcome(e).RevealSeconds+.12f*(int)e.tier:ReadSeconds(prior.Copy)+.12f*(int)e.tier;
                if(LifeActionStoryboard.ThemeFor(e)!=LifeActionTheme.None)
                {
                    // An actual action needs an establishing/action/arrival
                    // sequence, followed by a distinct sustained reveal build.
                    if(prior.Phase==CinematicPhase.Anticipation) minimum=Math.Max(minimum,12f);
                    if(prior.Phase==CinematicPhase.Impact) minimum=Math.Max(minimum,3.2f);
                }
                if(prior.Phase==CinematicPhase.SecondReveal) minimum=Math.Max(minimum,ReadSeconds(new CinematicOutcome(e).FutureMeaning)+.6f);
                if(prior.Phase==CinematicPhase.Settlement) minimum=Math.Max(minimum,Math.Max(3.4f,ReadSeconds(e.detail)));
                // Actual causal nodes retain their authored accelerating gaps.
                if(cascade && anchors[i].Phase==CinematicPhase.Impact) minimum=span;
                mapped[i]=mapped[i-1]+Math.Max(span,minimum);
            }
            float oldStop=cues.Single(c=>!c.NodeHit && c.Phase==CinematicPhase.HitStop).Time;
            float oldImpact=cues.Single(c=>!c.NodeHit && c.Phase==CinematicPhase.Impact).Time;
            for(int i=0;i<cues.Count;i++)
            {
                CinematicCue cue=cues[i]; int segment=0;
                while(segment+1<original.Length-1 && original[segment+1]<=cue.Time) segment++;
                float p=(cue.Time-original[segment])/Math.Max(.0001f,original[segment+1]-original[segment]);
                float time=mapped[segment]+p*(mapped[segment+1]-mapped[segment]);
                cues[i]=new CinematicCue(time,cue.Phase,cue.Copy,cue.NodeIndex);
            }
            float impact=cues.Single(c=>!c.NodeHit && c.Phase==CinematicPhase.Impact).Time;
            int stop=cues.FindIndex(c=>!c.NodeHit && c.Phase==CinematicPhase.HitStop);
            float hold=oldImpact-oldStop;
            if(LifeActionStoryboard.ThemeFor(e)!=LifeActionTheme.None) hold=Math.Max(hold,.62f);
            cues[stop]=new CinematicCue(impact-hold,CinematicPhase.HitStop,cues[stop].Copy);
            return mapped[mapped.Length-1];
        }
    }

    public readonly struct CinematicTextFrame
    {
        public readonly float Time,CopyAlpha,Glow,Tremor,Flash,ReceiptVeil,MultiplierAlpha,MultiplierScale,TitleScale,CopyScale,EndAlpha,CharacterProgress;
        public readonly CinematicPhase Phase;
        public readonly string PhaseCopy,Narration;
        public CinematicTextFrame(float time,CinematicPhase phase,string phaseCopy,string narration,float copyAlpha,float glow,
            float tremor,float flash,float receiptVeil,float multiplierAlpha,float multiplierScale,float titleScale,float copyScale,float endAlpha,float characterProgress)
        { Time=time; Phase=phase; PhaseCopy=phaseCopy; Narration=narration; CopyAlpha=copyAlpha; Glow=glow; Tremor=tremor;
            Flash=flash; ReceiptVeil=receiptVeil; MultiplierAlpha=multiplierAlpha; MultiplierScale=multiplierScale;
            TitleScale=titleScale; CopyScale=copyScale; EndAlpha=endAlpha; CharacterProgress=characterProgress; }
    }
}
