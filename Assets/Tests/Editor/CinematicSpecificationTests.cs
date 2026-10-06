using System;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class CinematicSpecificationTests
    {
        [TestCase(1,RewardObjectForm.Cell)] [TestCase(2,RewardObjectForm.ParallelCells)]
        [TestCase(5,RewardObjectForm.CellPack)] [TestCase(10,RewardObjectForm.BatteryPack)]
        [TestCase(20,RewardObjectForm.StorageUnit)] [TestCase(100,RewardObjectForm.EnergyCore)]
        public void PhysicalScaleUsesTheActualReceiptWithoutChangingItsValue(int value,RewardObjectForm form)
        {
            var item=new MaterialReward(RewardObjectKind.EnergyCell,RewardObjectClass.Resource,"精力 +"+value,value);
            Assert.AreEqual(value,item.Amount); Assert.AreEqual(form,item.Form); Assert.LessOrEqual(item.VisualCount,5);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(16)]
        public void SparseAndFullCascadeReceiptsKeepOrderedPhasesAnd150msSilence(int count)
        {
            var receipt=new RewardReceipt();
            for(int i=0;i<count;i++) receipt.causes.Add(new RewardEvidence { id="cause"+i,day=i+1,label="D"+(i+1) });
            var e=new DomainEvent { id="cascade",kind=DomainEventKind.Cascade,tier=RewardTier.Epic,receipt=receipt,chainSize=count };
            RewardPlan plan=RewardDirector.Direct(e);
            CollectionAssert.AreEqual(Enum.GetValues(typeof(CinematicPhase)),plan.Cues.Where(c=>!c.NodeHit).Select(c=>c.Phase));
            float stop=plan.Cues.Single(c=>c.Phase==CinematicPhase.HitStop).Time;
            Assert.AreEqual(.15f,plan.Cues.Single(c=>c.Phase==CinematicPhase.Impact).Time-stop,.0001f);
            Assert.IsTrue(plan.Cues.All(c=>c.Time>=0 && c.Time<=plan.Duration));
        }
        [Test]
        public void MythicBudgetDowngradeStillHasAPlayablePatternStoryboard()
        {
            var state=new MasterRunState(); DomainEvent e=null;
            for(int i=0;i<3;i++) e=RewardEngine.Emit(state,1,1,DomainEventKind.PatternBroken,null,"PATTERN BROKEN");
            Assert.AreEqual(RewardTier.Major,e.tier);
            RewardPlan plan=RewardDirector.Direct(e); var shots=CinematicStoryboard.Pattern(plan);
            Assert.AreEqual(12,shots.Count); Assert.IsTrue(shots.All(s=>s.Time>=0 && s.Time<plan.Duration));
            Assert.IsTrue(plan.Cues.Any(c=>c.Phase==CinematicPhase.HitStop));
        }
        [Test]
        public void DowngradedConvergenceRetainsThe200msSilence()
        {
            var state=new MasterRunState(); DomainEvent e=null;
            for(int i=0;i<3;i++) e=RewardEngine.Emit(state,1,1,DomainEventKind.RealityConvergence,null,"REALITY CONVERGENCE");
            Assert.AreEqual(RewardTier.Major,e.tier);
            RewardPlan plan=RewardDirector.Direct(e);
            Assert.AreEqual(.2f,plan.Cues.Single(c=>c.Phase==CinematicPhase.Impact).Time-
                plan.Cues.Single(c=>c.Phase==CinematicPhase.HitStop).Time,.0001f);
        }
        [Test]
        public void PresentationLadderNeverMultipliesResourcesOrClaimsCausalDepth()
        {
            var e=new DomainEvent { id="pattern",kind=DomainEventKind.PatternBroken,tier=RewardTier.Mythic,
                chainSize=3,receipt=new RewardReceipt { resourcesRecorded=true,resources=new ResourceDelta(5) } };
            string original=JsonUtility.ToJson(e); RewardPlan plan=RewardDirector.Direct(e);
            CollectionAssert.AreEqual(new[] { 2,5,20,100 },plan.PresentationBeats.Select(b=>b.Multiplier));
            Assert.AreEqual(5,plan.Objects.Single(o=>o.Kind==RewardObjectKind.EnergyCell).Amount);
            Assert.AreEqual(3,plan.Event.chainSize); Assert.AreEqual(original,JsonUtility.ToJson(e));
        }
        [Test]
        public void PredictionMoviesKeepSeparateImmutableExpectedAndActualValues()
        {
            var events=CinematicEventCoverageTests.RealEvents();
            foreach(DomainEvent e in events.Where(e=>e.kind==DomainEventKind.Synchronized || e.kind==DomainEventKind.Surprise))
            {
                Assert.IsTrue(e.receipt.predictionRecorded); Assert.IsTrue(e.receipt.predictionResolved);
                RewardPlan plan=RewardDirector.Direct(e); int before=plan.Event.receipt.predicted.energy;
                e.receipt.predicted.energy=999; Assert.AreEqual(before,plan.Event.receipt.predicted.energy);
                Assert.IsTrue(plan.Objects.Any(o=>o.Kind==RewardObjectKind.PredictionPanel));
                Assert.IsTrue(plan.Objects.Any(o=>o.Kind==RewardObjectKind.InsightPrism));
            }
        }
        [Test]
        public void InsightRevealDoesNotBorrowAnUnrelatedPredictionSettledToday()
        {
            var life=GameSession.StartMasterLife(1,15,RunMode.Quick);
            while(life.Day<7)
            {
                while(life.HasPredictionReview) life.MarkPredictionReviewed();
                if(life.CanPredict) life.LockPrediction(0,0,0);
                if(life.Day==6) life.ApplyMystery();
                life.Choose(life.Hand.Last(c=>life.CanPlay(c)).Id);
                if(life.NeedsStation) life.VisitStation();
                life.Advance();
            }
            Assert.IsTrue(life.Predictions.Any(p=>p.evaluated && p.dueDay==life.Day));
            life.Master.insightPoints=90;
            var mystery=life.Mysteries.First(); Assert.IsTrue(life.RevealHiddenCause(mystery.consequenceNodeId));
            DomainEvent reveal=life.Master.events.Last(e=>e.kind==DomainEventKind.Synchronized);
            Assert.IsFalse(reveal.receipt.predictionRecorded);
            RewardPlan plan=RewardDirector.Direct(reveal);
            Assert.IsFalse(plan.Objects.Any(o=>o.Kind==RewardObjectKind.PredictionPanel));
            Assert.IsTrue(plan.Objects.Any(o=>o.Kind==RewardObjectKind.InsightPrism));
        }
        [Test]
        public void HiddenOriginsNeverInventARewindDate()
        {
            var e=new DomainEvent { day=6,receipt=new RewardReceipt { sourceDay=0 } };
            Assert.AreEqual(6,CinematicStoryboard.DayDuringRewind(e,.8f));
            Assert.IsFalse(CinematicStoryboard.AllowsExtremeWide(DomainEventKind.ActionTaken));
            Assert.IsTrue(CinematicStoryboard.AllowsExtremeWide(DomainEventKind.PatternBroken));
        }
    }
}
