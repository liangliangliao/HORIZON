using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    // The reference's number reveal is a staging analogy, never a HORIZON payout.
    // These immutable reading beats contain ONLY recorded changes and disclosed
    // future information. Sampling has no session, wallet, RNG or Apply access.
    public sealed class CinematicOutcome
    {
        public const float ChangeSeconds = 2.8f;
        public readonly IReadOnlyList<OutcomeChange> Changes;
        public readonly string Title, Origin, Heading, Meaning, FutureHeading, FutureMeaning;
        public readonly bool Recorded;
        public float RevealSeconds => Math.Max(3.2f, Changes.Count * ChangeSeconds);

        public CinematicOutcome(DomainEvent e)
        {
            RewardReceipt r=e.receipt;
            Title=EventTitle(e); Recorded=r?.resourcesRecorded==true;
            Origin=r?.sourceDay>0 && !string.IsNullOrWhiteSpace(r.action)?"第 "+r.sourceDay+" 天 · 「"+r.action+"」":
                !string.IsNullOrWhiteSpace(r?.action)?"「"+r.action+"」":"";
            Heading=e.kind==DomainEventKind.ActionTaken?"今天 · 已发生":e.kind==DomainEventKind.TimeEcho?"回声抵达 · 实际变化":"这一次 · 已记录";
            Meaning=!string.IsNullOrWhiteSpace(e.detail)?Readable(e.detail):!string.IsNullOrWhiteSpace(r?.action)?Readable(r.action):Title;
            FutureHeading=r?.destinationDay>e.day?"第 "+r.destinationDay+" 天 · 回声":e.kind==DomainEventKind.TimeEcho?"从过去，到今天":"留下这次变化";
            FutureMeaning=!string.IsNullOrWhiteSpace(r?.futureMeaning)?r.futureMeaning:
                e.kind==DomainEventKind.TimeEcho && !string.IsNullOrWhiteSpace(Origin)?Origin:Meaning;
            var changes=new List<OutcomeChange>();
            if(Recorded)
            {
                int[] values=ResourceMath.Axes(r.resources);
                string[] names={"精力","心情","专注","金钱","关系","能力"};
                for(int i=0;i<values.Length;i++) if(values[i]!=0) changes.Add(new OutcomeChange(i,names[i],values[i]));
            }
            Changes=changes.AsReadOnly();
        }

        public OutcomeFrame Sample(float seconds,float reveal,float secondReveal,float settlement,bool reducedMotion=false)
        {
            if(float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            bool visible=seconds>=reveal && seconds<settlement;
            bool future=seconds>=secondReveal;
            int index=Changes.Count==0?-1:Math.Min(Changes.Count-1,Math.Max(0,(int)((seconds-reveal)/ChangeSeconds)));
            float age=future?seconds-secondReveal:seconds-reveal-(index<0?0:index*ChangeSeconds);
            float p=reducedMotion?1:Ease(age/.78f);
            string copy=future?FutureMeaning:index>=0?Changes[index].Copy(p):Recorded?"状态保持不变":Title;
            return new OutcomeFrame(visible,future,index,future?FutureHeading:Heading,copy,
                future?Meaning:Origin,age,p,index>=0 && !future && Changes[index].Amount<0);
        }

        public static string Readable(string text)
        {
            // Old saved event titles may contain resilience/causal × counters.
            // Preserve the evidence, not the obsolete multiplier notation.
            return (text??"").Replace("倍率代表重新开始的韧性链，提升理解与演出。","你在挫折之后重新开始了。")
                .Replace("×"," · ");
        }
        public static string EventTitle(DomainEvent e)
        {
            switch(e.kind)
            {
                case DomainEventKind.ActionTaken:return Readable(e.title);
                case DomainEventKind.TimeEcho:return "过去的行动，回来了";
                case DomainEventKind.OrbitActivated:
                    string direction=e.receipt?.orbitIndex>=0 && e.receipt.orbitIndex<MasterSpecification.OrbitNames.Length?
                        MasterSpecification.OrbitNames[e.receipt.orbitIndex]:Readable(e.title).Split('·').Last().Trim();
                    return direction+"方向已点亮";
                case DomainEventKind.AllLinked:return "八个未来方向，已连接";
                case DomainEventKind.PatternBroken:return "这一次，你继续向前";
                case DomainEventKind.Comeback:return "挫折之后，再次开始";
                case DomainEventKind.Cascade:return "过去的选择，连成了路";
                case DomainEventKind.CausalSingularity:return "多条因果，在此汇聚";
                case DomainEventKind.FutureMemory:return "把恢复的方法，带回今天";
                case DomainEventKind.VictoryAnchor:return "先看见你想抵达的未来";
                case DomainEventKind.Breakthrough:return "过去的积累，开始兑现";
                case DomainEventKind.RealityConvergence:return "想象、预演与现实重合";
                default:return Readable(e.title);
            }
        }
        public static string EventHeading(DomainEvent e)
        {
            switch(e.kind)
            {
                case DomainEventKind.PatternBroken:return "模式突破";
                case DomainEventKind.TimeEcho:return "时间回声";
                case DomainEventKind.Cascade:return "因果连锁";
                case DomainEventKind.CausalSingularity:return "因果汇聚";
                case DomainEventKind.Comeback:return "再次开始";
                case DomainEventKind.FutureMemory:return "未来记忆";
                case DomainEventKind.RealityConvergence:return "现实汇流";
                case DomainEventKind.OrbitActivated:return "未来轨道";
                default:return Readable(e.title);
            }
        }
        private static float Ease(float p) { p=Math.Max(0,Math.Min(1,p)); return p*p*(3-2*p); }
    }
    public sealed class OutcomeChange
    {
        public readonly int Axis, Amount;
        public readonly string Name;
        public OutcomeChange(int axis,string name,int amount) { Axis=axis; Name=name; Amount=amount; }
        public string Copy(float progress=1)
        {
            long magnitude=Math.Abs((long)Amount);
            long value=progress>=1?magnitude:(long)Math.Floor(magnitude*Math.Max(0,progress));
            return Name+" "+(Amount<0?"−":"+")+value;
        }
    }
    public readonly struct OutcomeFrame
    {
        public readonly bool Visible,Future,Negative;
        public readonly int Index;
        public readonly string Heading,Copy,Context;
        public readonly float Age,Progress;
        public OutcomeFrame(bool visible,bool future,int index,string heading,string copy,string context,float age,float progress,bool negative)
        { Visible=visible; Future=future; Index=index; Heading=heading; Copy=copy; Context=context; Age=age; Progress=progress; Negative=negative; }
    }
}
