using System;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class CinematicOutcomeTests
    {
        [Test]
        public void EveryActualAxisGetsItsOwnReadableRevealThenTheDisclosedFuture()
        {
            var source=new DomainEvent {id="actual",day=6,kind=DomainEventKind.ActionTaken,tier=RewardTier.Major,title="整理作品",
                receipt=new RewardReceipt {resourcesRecorded=true,resources=new ResourceDelta(-2,1,2,3,-1,4),
                    sourceDay=6,action="整理作品",destinationDay=9,futureMeaning="能力会成长"}};
            string json=JsonUtility.ToJson(source); RewardPlan plan=RewardDirector.Direct(source);
            var outcome=plan.Outcome;
            CollectionAssert.AreEqual(new[] {"精力","心情","专注","金钱","关系","能力"},outcome.Changes.Select(c=>c.Name));
            CollectionAssert.AreEqual(new[] {-2,1,2,-1,3,4},outcome.Changes.Select(c=>c.Amount));
            float start=plan.Cues.Single(c=>!c.NodeHit && c.Phase==CinematicPhase.Multiplication).Time;
            float future=plan.Cues.Single(c=>!c.NodeHit && c.Phase==CinematicPhase.SecondReveal).Time;
            float settled=plan.Cues.Single(c=>!c.NodeHit && c.Phase==CinematicPhase.Settlement).Time;
            Assert.GreaterOrEqual(future-start,outcome.Changes.Count*CinematicOutcome.ChangeSeconds);
            for(int i=0;i<outcome.Changes.Count;i++)
            {
                float t=start+i*CinematicOutcome.ChangeSeconds;
                Assert.IsFalse(outcome.Sample(t-.01f,start,future,settled).Future);
                var frame=outcome.Sample(t+1,start,future,settled);
                Assert.AreEqual(i,frame.Index); Assert.AreEqual(outcome.Changes[i].Copy(),frame.Copy);
                Assert.That(frame.Copy,Does.Not.Contain("×"));
                Assert.AreEqual(frame.Copy,outcome.Sample(t+2.5f,start,future,settled).Copy);
                Assert.AreEqual(frame.Copy,outcome.Sample(t+1,start,future,settled,true).Copy);
            }
            var next=outcome.Sample(future+.9f,start,future,settled);
            Assert.IsTrue(next.Future); Assert.AreEqual("第 9 天 · 回声",next.Heading); Assert.AreEqual("能力会成长",next.Copy);
            source.receipt.resources.energy=99; Assert.AreEqual(-2,outcome.Changes[0].Amount);
            source.receipt.resources.energy=-2; Assert.AreEqual(json,JsonUtility.ToJson(source));
        }

        [Test]
        public void RealActionsCaptureActualClampedChangesAndFutureMeaningBeforeObserversRun()
        {
            var life=GameSession.StartMasterLife(1,15,RunMode.Quick);
            CardSpec card=life.Hand.First(c=>life.CanPlay(c) && !life.NeedsImagination(c));
            life.Choose(card.Id);
            var e=life.Master.events.Last(x=>x.kind==DomainEventKind.ActionTaken);
            Assert.AreEqual(card.Delay==0?0:e.day+card.Delay,e.receipt.destinationDay);
            Assert.AreEqual(PlayExperience.FutureMeaning(card),e.receipt.futureMeaning);
            var restored=GameSession.Restore(life.Snapshot());
            Assert.AreEqual(e.receipt.futureMeaning,restored.Master.events.Single(x=>x.id==e.id).receipt.futureMeaning);
            Assert.IsTrue(RewardDirector.Direct(e).Outcome.Recorded);
        }

        [Test]
        public void OldHiddenAndZeroReceiptsNeverManufactureAmountsOrFutureDates()
        {
            foreach(var receipt in new[] {null,new RewardReceipt(),new RewardReceipt {resourcesRecorded=true}})
            {
                var e=new DomainEvent {id="old",kind=DomainEventKind.TimeEcho,tier=RewardTier.Major,title="TIME ECHO",detail="来路暂未清晰。",receipt=receipt};
                var outcome=RewardDirector.Direct(e).Outcome;
                Assert.IsEmpty(outcome.Changes); Assert.IsEmpty(outcome.Origin);
                var frame=outcome.Sample(2,1,4,8);
                Assert.That(frame.Copy,Does.Not.Contain("×").And.Not.Contain("+")); Assert.IsNotEmpty(frame.Copy);
                Assert.IsNotEmpty(outcome.FutureMeaning); Assert.That(outcome.FutureHeading,Does.Not.Contain("第 0 天"));
            }
        }

        [Test]
        public void OrbitAndRehearsalDescribeTheirEvidenceRatherThanADecorativePayout()
        {
            var e=new DomainEvent {kind=DomainEventKind.OrbitActivated,tier=RewardTier.Major,title="FUTURE ORBIT · 工作",receipt=new RewardReceipt {orbitIndex=0}};
            var outcome=RewardDirector.Direct(e).Outcome;
            Assert.AreEqual("工作方向已点亮",outcome.Title); Assert.IsEmpty(outcome.Changes);
            Assert.That(outcome.Sample(2,1,4,8).Copy,Does.Not.Contain("20").And.Not.Contain("×"));
            Assert.That(CinematicOutcome.Readable("FAIL & AGAIN ×3"),Does.Not.Contain("×"));
        }
    }
}
