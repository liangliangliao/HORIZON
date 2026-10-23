using System;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class CinematicEnergyTests
    {
        private static RewardPlan Plan(DomainEventKind kind,RewardTier tier)
        { return RewardDirector.Direct(new DomainEvent {id="energy",kind=kind,tier=tier,
            receipt=new RewardReceipt {resourcesRecorded=true,resources=new ResourceDelta(3),sourceDay=2}}); }

        [TestCase(RewardTier.Micro)] [TestCase(RewardTier.Local)] [TestCase(RewardTier.Combo)]
        [TestCase(RewardTier.Major)] [TestCase(RewardTier.Epic)] [TestCase(RewardTier.Mythic)]
        public void EveryEventSamplesAllTiersWithoutMutatingOrExtendingTheReceipt(RewardTier tier)
        {
            foreach(DomainEventKind kind in Enum.GetValues(typeof(DomainEventKind)))
            {
                RewardPlan plan=Plan(kind,tier); string saved=JsonUtility.ToJson(plan.Event);
                float duration=plan.Duration;
                var energy=new CinematicEnergy(plan);
                for(int frame=0;frame<=120;frame++)
                {
                    var value=energy.Sample(duration*frame/120);
                    Assert.That(value.Inward,Is.InRange(0f,1f)); Assert.That(value.Burst,Is.InRange(0f,1f));
                    Assert.That(value.Focus,Is.InRange(0f,1f)); Assert.That(value.Settlement,Is.InRange(0f,1f));
                    Assert.That(value.Recoil,Is.InRange(-1f,1f));
                    Assert.AreEqual(value.Burst,energy.Sample(value.Time).Burst,"A seek must reproduce the same frame.");
                }
                Assert.AreEqual(saved,JsonUtility.ToJson(plan.Event)); Assert.AreEqual(duration,plan.Duration);
                var end=energy.Sample(duration+10); Assert.AreEqual(0,end.Inward+end.Burst+end.Focus+end.Afterglow);
            }
        }
        [Test]
        public void RealCausalAndRealityEventsKeepTheirEvidenceAndQuietSemantics()
        {
            foreach(DomainEvent source in CinematicEventCoverageTests.RealEvents())
            {
                string saved=JsonUtility.ToJson(source); RewardPlan plan=RewardDirector.Direct(source);
                var energy=new CinematicEnergy(plan);
                foreach(CinematicCue cue in plan.Cues)
                {
                    var normal=energy.Sample(cue.Time); var reduced=energy.Sample(cue.Time,true);
                    Assert.AreEqual(0,reduced.Inward+reduced.Burst+reduced.Focus+reduced.Recoil);
                    if(!energy.Celebrates) Assert.AreEqual(0,normal.Inward+normal.Burst+normal.Recoil);
                }
                Assert.AreEqual(saved,JsonUtility.ToJson(source));
            }
        }
        [Test]
        public void SecondaryRevealsHaveFreshImpulsesAndSettleBackToQuiet()
        {
            var plan=Plan(DomainEventKind.Breakthrough,RewardTier.Mythic); var energy=new CinematicEnergy(plan);
            foreach(var cue in plan.Cues.Where(c=>c.Phase==CinematicPhase.Impact || c.Phase==CinematicPhase.SecondReveal || c.Phase==CinematicPhase.Jackpot))
            {
                Assert.Greater(energy.Sample(cue.Time).Burst,.7f);
                Assert.Greater(energy.Sample(cue.Time-.1f).Inward,.3f);
                Assert.Less(energy.Sample(cue.Time+.5f).Burst,energy.Sample(cue.Time).Burst);
            }
            Assert.AreEqual(0,energy.Sample(plan.Duration).Burst);
        }
        [TestCase(DomainEventKind.TimeEcho)] [TestCase(DomainEventKind.Breakthrough)]
        [TestCase(DomainEventKind.Synchronized)] [TestCase(DomainEventKind.RealityNode)]
        public void MajorRevealsNowHoldTheirPoseWithoutMovingTheRelease(DomainEventKind kind)
        {
            var plan=Plan(kind,RewardTier.Major); var tension=new CinematicTension(plan);
            Assert.IsTrue(tension.OpeningHold);
            float release=plan.Cues.Single(c=>c.Phase==CinematicPhase.Impact).Time;
            Assert.AreEqual(release,tension.FirstRelease);
            var a=tension.Sample(release-.09f); var b=tension.Sample(release-.01f);
            Assert.IsTrue(a.Silent && b.Silent); Assert.AreEqual(a.PoseTime,b.PoseTime);
            Assert.IsFalse(tension.Sample(release).Silent);
            Assert.IsFalse(tension.Sample(release-.05f,true).Silent);
        }
        [Test]
        public void MotionArrivesExactlyAndCountersNeverOvershoot()
        {
            float previous=0;
            for(int i=0;i<=1000;i++)
            {
                float p=i/1000f,value=ImmersiveMotion.Flight(p);
                Assert.That(value,Is.InRange(previous,1f)); previous=value;
            }
            Assert.AreEqual(0,ImmersiveMotion.Flight(-1)); Assert.AreEqual(1,ImmersiveMotion.Flight(2));
            Assert.AreEqual(1,ImmersiveMotion.Land(1)); Assert.Greater(ImmersiveMotion.Land(.8f),1);
            Assert.AreEqual(0,ImmersiveMotion.Punch(-1,.5f)); Assert.AreEqual(0,ImmersiveMotion.Punch(.5f,.5f));
            var energy=new CinematicEnergy(Plan(DomainEventKind.Breakthrough,RewardTier.Major));
            Assert.Throws<ArgumentOutOfRangeException>(()=>energy.Sample(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(()=>energy.Sample(float.PositiveInfinity));
        }
    }
}
