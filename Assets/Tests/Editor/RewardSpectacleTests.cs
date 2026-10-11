using System;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class RewardSpectacleTests
    {
        private static RewardPlan Plan(RewardTier tier,DomainEventKind kind=DomainEventKind.Breakthrough,int energy=5)
        {
            return RewardDirector.Direct(new DomainEvent { id="spectacle",tier=tier,kind=kind,day=7,
                receipt=new RewardReceipt { resourcesRecorded=true,sourceDay=3,resources=new ResourceDelta(energy) } });
        }

        [TestCase(RewardTier.Micro)] [TestCase(RewardTier.Local)] [TestCase(RewardTier.Combo)]
        [TestCase(RewardTier.Major)] [TestCase(RewardTier.Epic)] [TestCase(RewardTier.Mythic)]
        public void EveryTierHasBoundedEffectsAndKeepsTheOriginalReceipt(RewardTier tier)
        {
            RewardPlan plan=Plan(tier); string original=JsonUtility.ToJson(plan.Event);
            var visual=new RewardSpectacle(plan);
            Assert.LessOrEqual(visual.MoteBudget(false,0,false),48);
            Assert.LessOrEqual(visual.MoteBudget(true,0,false),8);
            Assert.LessOrEqual(visual.MoteBudget(false,2,false),12);
            Assert.AreEqual(0,visual.MoteBudget(false,0,true));
            for(int i=0;i<=200;i++)
            {
                RewardSpectacleFrame frame=visual.Sample(plan.Duration*i/200);
                Assert.That(frame.Hero,Is.InRange(0f,1f)); Assert.That(frame.Fade,Is.InRange(0f,1f));
                Assert.AreEqual(plan.PresentationMultiplierAt(frame.Time),frame.Multiplier);
            }
            Assert.AreEqual(5,plan.Objects.Single(o=>o.Kind==RewardObjectKind.EnergyCell).Amount);
            Assert.AreEqual(original,JsonUtility.ToJson(plan.Event));
        }

        [TestCase(DomainEventKind.TimeEcho)] [TestCase(DomainEventKind.Breakthrough)] [TestCase(DomainEventKind.FailAndAgain)]
        public void LossesDoNotCreateAHeroOrSprayRewardTokens(DomainEventKind kind)
        {
            var visual=new RewardSpectacle(Plan(RewardTier.Mythic,kind,-4));
            Assert.AreEqual(RewardSpectacleStyle.Loss,visual.Style); Assert.IsFalse(visual.HasHero);
            Assert.AreEqual(0,visual.MoteBudget(false,0,false));
            Assert.IsFalse(visual.Sample(visual.Plan.Duration*.8f).ShowMultiplier);
        }

        [TestCase(DomainEventKind.PredictionLocked)] [TestCase(DomainEventKind.DecisionLocked)]
        [TestCase(DomainEventKind.VictoryAnchor)] [TestCase(DomainEventKind.TriggerEquipped)]
        public void PreparationKeepsItsQuietSemanticPresentation(DomainEventKind kind)
        {
            var visual=new RewardSpectacle(Plan(RewardTier.Major,kind,0));
            Assert.AreEqual(RewardSpectacleStyle.Quiet,visual.Style); Assert.IsFalse(visual.HasHero);
            Assert.AreEqual(0,visual.MoteBudget(false,0,false));
            Assert.IsFalse(visual.Sample(visual.Plan.Duration*.8f).ShowMultiplier);
        }

        [Test]
        public void HiddenEchoUsesItsActualResourceWithMysteryLightingWithoutInventingAnOrigin()
        {
            var source=new DomainEvent { id="hidden",kind=DomainEventKind.TimeEcho,tier=RewardTier.Major,day=6,
                receipt=new RewardReceipt { sourceDay=0,resourcesRecorded=true,resources=new ResourceDelta(2) } };
            var visual=new RewardSpectacle(RewardDirector.Direct(source));
            Assert.AreEqual(RewardSpectacleStyle.Mystery,visual.Style); Assert.IsTrue(visual.HasHero);
            Assert.IsFalse(visual.HasMotes); Assert.AreEqual(0,visual.Plan.Event.receipt.sourceDay);
            Assert.AreEqual(RewardObjectKind.EnergyCell,visual.HeroReward.Kind); Assert.AreEqual(2,visual.HeroReward.Amount);
        }

        [Test]
        public void HeroFollowsTheSecondRevealAndAllEffectsVanishAtCompletion()
        {
            var visual=new RewardSpectacle(Plan(RewardTier.Epic));
            Assert.AreEqual(0,visual.Sample(visual.ImpactTime+.05f).Hero);
            Assert.Greater(visual.Sample(visual.HeroTime+.4f).Hero,.5f);
            RewardSpectacleFrame end=visual.Sample(1000);
            Assert.AreEqual(0,end.Hero); Assert.AreEqual(0,end.Fade); Assert.AreEqual(0,end.Burst);
            Assert.IsFalse(end.ShowMultiplier);
        }

        [Test]
        public void PausedDirectorCannotAdvanceTheHeroAndReducedMotionHasNoFlash()
        {
            var visual=new RewardSpectacle(Plan(RewardTier.Epic)); var clock=new CinematicClock(visual.Plan);
            clock.Advance(visual.ImpactTime,c=>{}); RewardSpectacleFrame before=visual.Sample(clock.Elapsed);
            clock.Pause(true); clock.Advance(10,c=>{}); RewardSpectacleFrame paused=visual.Sample(clock.Elapsed);
            Assert.AreEqual(before.Time,paused.Time); Assert.AreEqual(before.Hero,paused.Hero); Assert.AreEqual(before.Burst,paused.Burst);
            Assert.AreEqual(0,visual.Sample(visual.ImpactTime,true).Burst);
            clock.Pause(false); clock.Skip(); Assert.AreEqual(0,visual.Sample(clock.Elapsed).Fade);
        }

        [Test]
        public void InvalidTimeCannotPolluteMeshTransforms()
        {
            var visual=new RewardSpectacle(Plan(RewardTier.Major));
            Assert.Throws<ArgumentOutOfRangeException>(()=>visual.Sample(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(()=>visual.Sample(float.PositiveInfinity));
            Assert.AreEqual(0,visual.Sample(-1).Time);
        }

        [TestCase(DomainEventKind.PatternBroken,RewardObjectKind.BrokenLink)]
        [TestCase(DomainEventKind.RealityConvergence,RewardObjectKind.ConvergencePrism)]
        [TestCase(DomainEventKind.Surprise,RewardObjectKind.InsightPrism)]
        [TestCase(DomainEventKind.Breakthrough,RewardObjectKind.ReservoirCore)]
        [TestCase(DomainEventKind.FutureMemory,RewardObjectKind.MemoryFilm)]
        [TestCase(DomainEventKind.Comeback,RewardObjectKind.RepairKit)]
        public void HeroModelsFollowTheGameEventInsteadOfBorrowingReferenceImageProps(DomainEventKind kind,RewardObjectKind asset)
        {
            var visual=new RewardSpectacle(Plan(RewardTier.Major,kind));
            Assert.AreEqual(asset,visual.HeroReward.Kind);
        }
    }
}
