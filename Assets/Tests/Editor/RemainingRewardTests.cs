using System;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class RemainingRewardTests
    {
        [Test]
        public void MemoryFramesFollowRealBeatsAndCopiesCannotRewriteReceipts()
        {
            var run=ImaginationEngine.Begin("film-copy","career","准备面试",1);
            var frame=new MemoryFrame { runId=run.id,beatIndex=0,phase=run.phase,actionKey="anchor",width=128,height=96,jpeg="/9j/2Q==" };
            MemoryFrame.Record(run,frame); frame.actionKey="rewritten";
            Assert.AreEqual("anchor",run.frames.Single().actionKey);
            MemoryFrame.Record(run,new MemoryFrame { runId="another-run",beatIndex=0,phase=run.phase,width=128,height=96,jpeg="/9j/2Q==" });
            Assert.AreEqual(1,run.frames.Count);
            while(run.phase!=ImaginePhase.Complete)
                if(run.phase==ImaginePhase.Recover) ImaginationEngine.Recover(run,RecoveryAction.Rest);
                else ImaginationEngine.Continue(run);
            var life=GameSession.StartMasterLife(3,41,RunMode.MirrorRun);
            life.AttachImagination(run);
            var reward=life.Master.events.Single(e=>e.kind==DomainEventKind.FutureMemory);
            run.frames[0].actionKey="changed later";
            Assert.AreEqual("anchor",reward.receipt.frames.Single().actionKey);
            var collection=new RewardCollection(); Assert.IsTrue(collection.Capture(reward,3));
            reward.Copy().receipt.frames[0].actionKey="observer edit";
            Assert.AreEqual("anchor",collection.items.Single().frames.Single().actionKey);
            var restored=JsonUtility.FromJson<RewardCollection>(JsonUtility.ToJson(collection));
            Assert.AreEqual("/9j/2Q==",restored.items.Single().frames.Single().jpeg);
        }
        [Test]
        public void LegacyAndOversizedMemoryFramesDoNotBlockGameplayOrBecomeTextures()
        {
            var legacy=new RewardReceipt { frames=null }; Assert.IsEmpty(legacy.Copy().frames);
            var frame=new MemoryFrame { runId="x",width=193,height=96,jpeg="/9j/2Q==" };
            Assert.IsFalse(frame.IsValid);
            frame.width=128; frame.jpeg=new string('A',MemoryFrame.MaximumEncodedLength+1);
            Assert.IsFalse(frame.IsValid);
            Assert.IsEmpty(MemoryFrame.CopyFrames(new[] { frame }));
        }
        [Test]
        public void FrameStatisticsUseElapsedTimeAndRetainHitches()
        {
            var window=new FramePerformanceWindow();
            for(int i=0;i<90;i++) window.Record(1f/60,60);
            for(int i=0;i<10;i++) window.Record(.05f,60);
            window.Record(float.NaN,60); window.Record(-1,60);
            FramePerformanceSummary summary=window.Summarize("Board","Normal60",60);
            Assert.AreEqual(100,summary.frames); Assert.That(summary.averageFps,Is.EqualTo(50).Within(.001));
            Assert.That(summary.p95Milliseconds,Is.EqualTo(50).Within(.001));
            Assert.AreEqual(10,summary.missedBudgetPercent);
            window.Record(1,60); Assert.AreEqual(1000,window.Summarize("Board","Normal60",60).worstMilliseconds);
        }
        [Test]
        public void AdaptiveQualityNeedsSustainedPressureAndRecoversWithHysteresis()
        {
            var budget=new AdaptiveFrameBudget();
            for(int i=0;i<100;i++) budget.Record(.04f,60);
            Assert.AreEqual(0,budget.Level,"A brief burst must not repeatedly change quality.");
            for(int i=0;i<210;i++) budget.Record(.04f,60);
            Assert.AreEqual(2,budget.Level);
            for(int i=0;i<480;i++) budget.Record(1f/60,60);
            Assert.AreEqual(2,budget.Level,"Recovery should be slower than degradation.");
            for(int i=0;i<1600;i++) budget.Record(1f/60,60);
            Assert.AreEqual(0,budget.Level);
        }
    }
}
