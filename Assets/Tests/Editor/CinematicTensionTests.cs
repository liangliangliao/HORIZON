using System;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class CinematicTensionTests
    {
        [TestCase(DomainEventKind.Cascade)] [TestCase(DomainEventKind.CausalSingularity)]
        public void ActualEventsHaveAQuietHeldSubjectBeforeTheirOriginalRelease(DomainEventKind kind)
        {
            DomainEvent source=CausalRelayTests.ActualCascade(kind); string saved=JsonUtility.ToJson(source);
            RewardPlan plan=RewardDirector.Direct(source); string cues=string.Join("|",plan.Cues.Select(c=>c.Time+":"+c.Phase+":"+c.NodeIndex));
            var tension=new CinematicTension(plan);
            Assert.IsTrue(tension.SourceCloseup); Assert.Greater(tension.Sample(0).Focus,.99f);
            Assert.IsTrue(tension.Sample(tension.FirstRelease-.1f).Silent);
            Assert.IsFalse(tension.Sample(tension.FirstRelease+.01f).Silent);
            Assert.Less(tension.PreviewStart,tension.RevealHold);
            Assert.GreaterOrEqual(tension.Reveal-tension.RevealHold,.33f);
            var a=tension.Sample(tension.RevealHold+.01f); var b=tension.Sample(tension.Reveal-.01f);
            Assert.IsTrue(a.Silent && b.Silent); Assert.AreEqual(a.PoseTime,b.PoseTime); Assert.AreEqual(a.Preview,b.Preview);
            Assert.IsFalse(tension.Sample(tension.Reveal).Silent);
            Assert.AreEqual(cues,string.Join("|",plan.Cues.Select(c=>c.Time+":"+c.Phase+":"+c.NodeIndex)));
            float stopped=plan.Cues.Single(c=>c.Phase==CinematicPhase.HitStop && !c.NodeHit).Time;
            float released=plan.Cues.Single(c=>c.Phase==CinematicPhase.Impact && !c.NodeHit).Time;
            Assert.AreEqual(.15f,released-stopped,.001f);
            Assert.AreEqual(saved,JsonUtility.ToJson(source));
        }
        [Test]
        public void ReductionAndNonCelebratoryEventsDoNotReceiveSuspenseMotion()
        {
            RewardPlan plan=RewardDirector.Direct(CausalRelayTests.ActualCascade(DomainEventKind.CausalSingularity));
            var tension=new CinematicTension(plan);var reduced=tension.Sample(tension.Reveal-.1f,true);
            Assert.IsFalse(reduced.Silent); Assert.AreEqual(0,reduced.Focus); Assert.AreEqual(0,reduced.Preview);
            foreach(DomainEventKind kind in new[] {DomainEventKind.FailAndAgain,DomainEventKind.PredictionLocked})
                Assert.IsFalse(new CinematicTension(RewardDirector.Direct(new DomainEvent { id="quiet",tier=RewardTier.Mythic,kind=kind })).Enabled);
            var legacy=new CinematicTension(RewardDirector.Direct(new DomainEvent {id="no-source",kind=DomainEventKind.Cascade,tier=RewardTier.Epic}));
            Assert.IsFalse(legacy.SourceCloseup); Assert.AreEqual(0,legacy.Sample(0).Focus);
            Assert.AreEqual(0,tension.Sample(plan.Duration).Focus);
            Assert.Throws<ArgumentOutOfRangeException>(()=>tension.Sample(float.NaN));
        }
    }
}
