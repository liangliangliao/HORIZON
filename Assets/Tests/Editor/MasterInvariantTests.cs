using System;
using System.Linq;
using System.Collections.Generic;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class MasterInvariantTests
    {
        [Test]
        public void ReopeningRepeatedlyInOneLifeCannotInventAMultilifePattern()
        {
            var life = new GameSession(2, 15);
            for (int i = 0; i < 4; i++) { life.LockDecision(life.Hand[1].Id); life.UnlockDecision(); }
            life.LockDecision(life.Hand[1].Id); for (int i = 0; i < 4; i++) life.ExecuteDecisionStep(); life.Choose(life.Master.decision.cardId);
            Assert.IsFalse(life.Master.events.Any(e => e.kind == DomainEventKind.PatternBroken));
            Assert.LessOrEqual(life.Master.overdriveEnergy, 25);
            Assert.AreEqual(0, PatternEngine.Detect(life.Master.observations).Count);
        }
        [Test]
        public void AForecastCanFinishTheLockedExecutionWithoutTouchingThePlayer()
        {
            var life = new GameSession(3, 15); life.LockDecision(life.Hand[1].Id); life.ExecuteDecisionStep();
            string frozen = JsonUtility.ToJson(life.Snapshot());
            ForecastRange forecast = ForecastSimulator.Sample(life, life.Master.decision.cardId, 4, 3);
            Assert.AreEqual(4, forecast.targetDay); Assert.AreEqual(frozen, JsonUtility.ToJson(life.Snapshot()));
        }
        [Test]
        public void ReservoirPayoutHasSixRealParentsAndDoesNotRepeatOnReload()
        {
            var life = GameSession.StartMasterLife(3, 15, RunMode.LongRun);
            DomainEvent breakthrough = null;
            while (life.CompletedRun == null && breakthrough == null)
            {
                while (life.HasPredictionReview) life.MarkPredictionReviewed(); if (life.CanPredict) life.SkipPrediction();
                CardSpec choice = life.Day % 3 == 1 && life.CanPlay(life.Hand[1]) ? life.Hand[1] : life.Hand[2];
                life.Choose(choice.Id); if (life.NeedsStation) life.VisitStation(); if (life.Day < life.Deadline) life.Advance();
                breakthrough = life.Master.events.Find(e => e.kind == DomainEventKind.Breakthrough);
            }
            Assert.IsNotNull(breakthrough);
            CausalNode node = life.CausalNodes.Find(n => n.id == breakthrough.nodeId);
            Assert.AreEqual(6, CausalGraph.Parents(node).Count); Assert.IsTrue(node.effectRecorded);
            Assert.IsTrue(CausalGraph.Parents(node).All(id => life.CausalNodes.Any(n => n.id == id && n.type == CausalNodeKind.Action)));
            life = GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(life.Snapshot())));
            Assert.AreEqual(1, life.Master.events.Count(e => e.kind == DomainEventKind.Breakthrough));
        }
        [Test]
        public void ForesightKnowledgePersistsInTheBehaviorArchiveAcrossNewLives()
        {
            var archive = new ArchiveData(); var first = GameSession.StartMasterLife(2, 15, RunMode.Quick);
            first.Choose(first.Hand[2].Id); archive.CaptureMaster(first);
            var next = GameSession.StartMasterLife(3, 15, RunMode.Quick, archive.me);
            Assert.AreEqual(first.Master.insightPoints, next.Master.insightPoints);
            Assert.AreEqual(next.Master.insightPoints, next.Master.initialInsightPoints);
        }
        [Test]
        public void ACorruptCompletedImaginationCannotSkipItsFailureEvidence()
        {
            var run = new ImagineRun { id = "bad", goalId = "goal", goal = "fake victory", phase = ImaginePhase.Complete, failures = 1, recovered = 1, multiplier = 2,
                memories = new List<FutureMemory> { new FutureMemory { id = "fake" } } };
            Assert.Throws<ArgumentException>(() => run.Validate());
        }
        [Test]
        public void MomentumChangesActualCostAndExpiresAfterTheOpportunityDay()
        {
            var life = new GameSession(3, 15); string card = life.Hand[1].Id;
            life.Master.windows.Add(new OpportunityWindow { id = "window", cardId = card, openedDay = 1, expiresDay = 1, momentumUntilDay = 1 });
            int nominal = life.Hand[1].Now.energy;
            ActionRecord action = life.Choose(card); Assert.AreEqual(nominal + 1, action.actualNow.energy);
            life.Advance(); Assert.IsFalse(life.Master.events.Any(e => e.kind == DomainEventKind.OpportunityExpired));
            var untouched = new GameSession(3, 15);
            untouched.Master.windows.Add(new OpportunityWindow { id = "window", cardId = untouched.Hand[1].Id, openedDay = 1, expiresDay = 1, momentumUntilDay = 1 });
            untouched.Choose(untouched.Hand[2].Id); untouched.Advance();
            Assert.IsTrue(untouched.Master.events.Any(e => e.kind == DomainEventKind.MomentumExpired));
        }
    }
}
