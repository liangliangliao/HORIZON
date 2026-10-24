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

        [Test]
        public void EveryTierHasTimeToReadAndUsesTheCompleteSynchronizedTextGrammar()
        {
            float previous=0;
            foreach(RewardTier tier in Enum.GetValues(typeof(RewardTier)))
            {
                RewardPlan plan=Plan(DomainEventKind.Breakthrough,tier);
                Assert.GreaterOrEqual(plan.Duration,2.6f);
                Assert.Greater(plan.Duration,previous); previous=plan.Duration;
                CollectionAssert.AreEqual(Enum.GetValues(typeof(CinematicPhase)),
                    plan.Cues.Where(c=>!c.NodeHit).Select(c=>c.Phase));
                Assert.IsTrue(plan.Cues.Where(c=>!c.NodeHit && c.Phase!=CinematicPhase.ReturnToGame)
                    .All(c=>!string.IsNullOrWhiteSpace(c.Copy)));
            }
        }

        [Test]
        public void MultiplierWaitsForItsOwnRevealAndTypographyHoldsStillBeforeImpact()
        {
            RewardPlan plan=Plan(DomainEventKind.Breakthrough,RewardTier.Mythic);
            var text=new CinematicTextChoreography(plan);
            float stop=plan.Cues.Single(c=>!c.NodeHit && c.Phase==CinematicPhase.HitStop).Time;
            Assert.AreEqual(0,text.Sample(text.ImpactTime+.02f).MultiplierAlpha,
                "The impact must land before the multiplier takes over the screen.");
            Assert.Greater(text.Sample(text.MultiplierTime+.2f).MultiplierAlpha,.5f);
            CinematicTextFrame held=text.Sample(stop+.02f);
            Assert.AreEqual(CinematicPhase.HitStop,held.Phase); Assert.AreEqual(0,held.Tremor);
            Assert.IsNotEmpty(held.Narration); Assert.Greater(held.Glow,0);
            Assert.AreEqual(0,text.Sample(text.MultiplierTime+.2f,true).Tremor);
        }

        [Test]
        public void NewChaptersStayReadableOnTheirFirstFrameAndSettlementArrivesPromptly()
        {
            RewardPlan plan=Plan(DomainEventKind.PatternBroken,RewardTier.Mythic);
            var text=new CinematicTextChoreography(plan);
            foreach(CinematicCue cue in plan.Cues.Where(c=>!c.NodeHit && c.Phase!=CinematicPhase.ReturnToGame))
            {
                CinematicTextFrame frame=text.Sample(cue.Time);
                Assert.GreaterOrEqual(frame.CopyAlpha,.55f,cue.Phase+" must not disappear while its copy changes.");
                Assert.IsNotEmpty(frame.Narration,cue.Phase.ToString());
            }
            Assert.Less(text.Sample(text.SettlementTime+.08f).ReceiptVeil,.25f,
                "The exact receipt should already be legible shortly after the settlement beat.");
        }

        [Test]
        public void QuietAndLossEventsKeepMatchingCopyAndNeverPretendToAwardAMultiplier()
        {
            foreach(DomainEventKind kind in new[] {DomainEventKind.PredictionLocked,DomainEventKind.FailAndAgain,
                DomainEventKind.OpportunityExpired,DomainEventKind.MomentumExpired})
            {
                RewardPlan plan=Plan(kind,RewardTier.Mythic);
                var energy=new CinematicEnergy(plan); var text=new CinematicTextChoreography(plan);
                Assert.IsFalse(energy.Celebrates,kind.ToString());
                Assert.AreEqual(0,text.Sample(text.MultiplierTime+.2f).MultiplierAlpha,kind.ToString());
                Assert.IsFalse(plan.Cues.Any(c=>c.Copy=="整个时间网络被点亮。"),kind.ToString());
            }
        }

        [Test]
        public void ReadingChaptersHaveRealDwellTimeAndDoNotChangeOnTechnicalCues()
        {
            foreach(RewardTier tier in Enum.GetValues(typeof(RewardTier)))
            {
                RewardPlan plan=Plan(DomainEventKind.PatternBroken,tier);
                var text=new CinematicTextChoreography(plan);
                var reading=plan.Cues.Where(c=>!c.NodeHit && CinematicReading.IsChapter(plan.Event,c.Phase)).ToArray();
                for(int i=0;i<reading.Length;i++)
                {
                    float end=i+1<reading.Length?reading[i+1].Time:plan.Duration;
                    Assert.GreaterOrEqual(end-reading[i].Time,CinematicReading.ReadSeconds(reading[i].Copy)-.001f);
                    string sentence=text.Sample(reading[i].Time+.01f).Narration;
                    Assert.AreEqual(sentence,text.Sample(end-.1f).Narration,"A full thought must survive the technical sub-beats.");
                    Assert.AreEqual(1,text.Sample(end-.1f).CharacterProgress);
                }
                float reveal=plan.Cues.Single(c=>!c.NodeHit && c.Phase==CinematicPhase.SecondReveal).Time;
                if(plan.PresentationMultiplier>1)
                {
                    Assert.GreaterOrEqual(reveal-text.MultiplierTime,2.4f-.001f);
                    Assert.Greater(text.Sample(text.MultiplierTime+1.5f).MultiplierAlpha,.9f);
                }
                Assert.GreaterOrEqual(plan.Duration-text.SettlementTime,3.4f-.001f);
            }
        }

        [Test]
        public void TimeEchoRewindDayCanUseTheExpandedCueWindow()
        {
            var e=new DomainEvent {day=12,receipt=new RewardReceipt {sourceDay=4}};
            Assert.AreEqual(12,CinematicStoryboard.DayDuringRewind(e,1,1,4));
            Assert.AreEqual(4,CinematicStoryboard.DayDuringRewind(e,4,1,4));
            Assert.That(CinematicStoryboard.DayDuringRewind(e,2.5f,1,4),Is.InRange(7,9));
        }
    }
}
