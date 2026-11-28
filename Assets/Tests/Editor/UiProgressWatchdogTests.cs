using System;
using NUnit.Framework;

namespace Horizon.Tests
{
    // Test harness only. Observe discrete UI milestones, never frame ticks,
    // animation elapsed time, camera movement or attempted button clicks.
    internal sealed class UiProgressWatchdog
    {
        private readonly float started,idleBudget,totalBudget;
        private float lastProgress,lastObservation;
        private string state;
        public string Failure { get; private set; }
        public UiProgressWatchdog(float now,float idleSeconds,float totalSeconds)
        {
            if(float.IsNaN(now) || float.IsInfinity(now) || idleSeconds<=0 || totalSeconds<idleSeconds)
                throw new ArgumentOutOfRangeException(nameof(now));
            started=lastProgress=lastObservation=now; idleBudget=idleSeconds; totalBudget=totalSeconds;
        }
        public string Observe(float now,string milestone)
        {
            if(Failure!=null) return Failure;
            if(float.IsNaN(now) || float.IsInfinity(now) || now<lastObservation)
                return Failure="UI watchdog received an invalid or reversed clock.";
            lastObservation=now;
            if(now-started>=totalBudget)
                return Failure="UI flow exceeded its bounded total budget of "+totalBudget+"s. Last milestone: "+state;
            if(!string.Equals(state,milestone,StringComparison.Ordinal)) { state=milestone; lastProgress=now; }
            if(now-lastProgress>=idleBudget)
                return Failure="UI flow made no progress for "+idleBudget+"s. Last milestone: "+state;
            return null;
        }
    }

    public sealed class UiProgressWatchdogTests
    {
        [Test]
        public void ReadableProgressCanContinuePastTheOld150SecondWall()
        {
            var guard=new UiProgressWatchdog(0,15,360);
            for(int node=0;node<=24;node++) Assert.IsNull(guard.Observe(node*10,"node "+node));
        }
        [Test]
        public void AClockThatKeepsTickingDoesNotHideAStuckPage()
        {
            var guard=new UiProgressWatchdog(0,15,360);
            Assert.IsNull(guard.Observe(0,"day 17 | same page"));
            Assert.IsNull(guard.Observe(14.9f,"day 17 | same page"));
            StringAssert.Contains("no progress",guard.Observe(15,"day 17 | same page"));
            Assert.IsNotNull(guard.Observe(16,"late page change"),"A failed watchdog must not resurrect itself.");
        }
        [Test]
        public void EndlessPageChangesStillHitTheAbsoluteBudget()
        {
            var guard=new UiProgressWatchdog(0,15,40);
            for(int node=0;node<4;node++) Assert.IsNull(guard.Observe(node*10,"page "+node));
            StringAssert.Contains("total budget",guard.Observe(40,"page 4"));
        }
    }
}
