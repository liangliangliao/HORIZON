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
        private readonly CinematicEnergy energy;
        public readonly float ImpactTime, MultiplierTime, SettlementTime;

        public CinematicTextChoreography(RewardPlan value)
        {
            plan=value ?? throw new ArgumentNullException(nameof(value));
            chapters=plan.Cues.Where(c=>!c.NodeHit).OrderBy(c=>c.Time).ToArray();
            energy=new CinematicEnergy(plan);
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
            float started=chapter?.Time??0;
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
            float multiplierLinger=1-Smooth(Math.Max(.72f,plan.Duration*.065f),Math.Max(1.12f,plan.Duration*.11f),multiplierAge);
            float multiplierAlpha=energy.Celebrates && plan.PresentationMultiplier>1 && multiplierAge>=0?
                Smooth(0,.16f,multiplierAge)*multiplierLinger*ending:0;
            float multiplierPunch=multiplierAge<0?0:(float)Math.Exp(-multiplierAge/Math.Max(.12f,plan.Duration*.025f));
            string narration=chapter!=null && !string.IsNullOrEmpty(chapter.Copy)?chapter.Copy:plan.Event.detail;
            float receiptStart=Math.Max(0,SettlementTime-.08f);
            float receiptEnd=Math.Min(plan.Duration,SettlementTime+.14f);
            return new CinematicTextFrame(time,chapter?.Phase??CinematicPhase.Anticipation,
                PhaseCopy(chapter?.Phase??CinematicPhase.Anticipation),narration,
                readableEntrance*ending,Clamp(.12f+e.Inward*.72f+e.Afterglow*.24f+flash*.72f,0,1),
                tremor,flash,1-Smooth(receiptStart,receiptEnd,time),
                multiplierAlpha,.78f+.22f*Smooth(0,.24f,multiplierAge)+multiplierPunch*.24f,
                .9f+.1f*Smooth(0,.42f,time)+flash*.055f,
                .95f+.05f*entrance,ending);
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
                case CinematicPhase.Multiplication: return "演出倍率显现";
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

    public readonly struct CinematicTextFrame
    {
        public readonly float Time,CopyAlpha,Glow,Tremor,Flash,ReceiptVeil,MultiplierAlpha,MultiplierScale,TitleScale,CopyScale,EndAlpha;
        public readonly CinematicPhase Phase;
        public readonly string PhaseCopy,Narration;
        public CinematicTextFrame(float time,CinematicPhase phase,string phaseCopy,string narration,float copyAlpha,float glow,
            float tremor,float flash,float receiptVeil,float multiplierAlpha,float multiplierScale,float titleScale,float copyScale,float endAlpha)
        { Time=time; Phase=phase; PhaseCopy=phaseCopy; Narration=narration; CopyAlpha=copyAlpha; Glow=glow; Tremor=tremor;
            Flash=flash; ReceiptVeil=receiptVeil; MultiplierAlpha=multiplierAlpha; MultiplierScale=multiplierScale;
            TitleScale=titleScale; CopyScale=copyScale; EndAlpha=endAlpha; }
    }
}
