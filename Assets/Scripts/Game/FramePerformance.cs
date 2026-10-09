using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    [Serializable]
    public sealed class FramePerformanceSummary
    {
        public string scene, quality;
        public int targetFps, frames, percentileSamples;
        public float seconds, averageFps, p95Milliseconds, worstMilliseconds, missedBudgetPercent;
    }
    public sealed class FramePerformanceWindow
    {
        private readonly Queue<float> recent=new Queue<float>();
        private int count,missed;
        private double total;
        private float worst;
        public void Record(float seconds,int targetFps)
        {
            if(float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds<=0 || targetFps<=0) return;
            count++; total+=seconds; worst=Math.Max(worst,seconds);
            if(seconds>1f/targetFps*1.1f) missed++;
            if(recent.Count==1800) recent.Dequeue(); recent.Enqueue(seconds);
        }
        public FramePerformanceSummary Summarize(string scene,string quality,int targetFps)
        {
            float[] values=recent.OrderBy(x=>x).ToArray();
            return new FramePerformanceSummary { scene=scene,quality=quality,targetFps=targetFps,frames=count,
                seconds=(float)total,averageFps=total>0?(float)(count/total):0,percentileSamples=values.Length,
                p95Milliseconds=values.Length>0?values[Math.Max(0,(int)Math.Ceiling(values.Length*.95)-1)]*1000:0,
                worstMilliseconds=worst*1000,missedBudgetPercent=count>0?missed*100f/count:0 };
        }
    }
    // Two-second windows with separate recovery hysteresis. Only presentation
    // quality changes; story clocks, rewards and touch targets never do.
    public sealed class AdaptiveFrameBudget
    {
        public int Level { get; private set; }
        private float elapsed;
        private int count,slow,healthy;
        public void Reset() { Level=0; elapsed=0; count=slow=healthy=0; }
        public bool Record(float seconds,int targetFps)
        {
            if(seconds<=0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || targetFps<=0) return false;
            elapsed+=seconds; count++; if(elapsed<2) return false;
            float mean=elapsed/count; elapsed=0; count=0;
            if(mean>1f/targetFps*1.15f) { slow++; healthy=0; }
            else if(mean<1f/targetFps*1.04f) { healthy++; slow=0; }
            else { slow=healthy=0; }
            if(slow>=3 && Level<2) { Level++; slow=healthy=0; return true; }
            if(healthy>=8 && Level>0) { Level--; slow=healthy=0; return true; }
            return false;
        }
    }
}
