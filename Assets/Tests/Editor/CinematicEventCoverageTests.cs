using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class CinematicEventCoverageTests
    {
        internal static List<DomainEvent> RealEvents()
        {
            var model = new PlayerBehavioralModel();
            model.Observe(new[] {
                new BehaviorObservation { id="old:1",run=1,day=1,key="decision-reopen",nodeId="old:first" },
                new BehaviorObservation { id="old:2",run=2,day=1,key="decision-reopen",nodeId="old:second" } });
            var life = GameSession.StartMasterLife(3, 41, RunMode.MirrorRun, model);
            ImagineRun imagined=ImaginationEngine.Begin("capture-imagination","help","在困难后恢复，再行动",1);
            while(imagined.phase!=ImaginePhase.Complete)
                if(imagined.phase==ImaginePhase.Recover) ImaginationEngine.Recover(imagined,RecoveryAction.Rest);
                else ImaginationEngine.Continue(imagined);
            life.AttachImagination(imagined);
            life.LockDecision(life.Hand[2].Id);
            while (life.Master.decision.status != DecisionStatus.Ready) life.ExecuteDecisionStep();
            life.Choose(life.Master.decision.cardId);
            while (life.Day < life.Deadline)
            {
                if (life.NeedsStation) life.VisitStation();
                life.Advance();
                while (life.HasPredictionReview) life.MarkPredictionReviewed();
                if (life.CanPredict)
                    if (life.Day == 4) life.LockPrediction(-3, -3, -3);
                    else life.LockPrediction(0, 0, 0);
                life.Choose(life.Hand[2].Id);
            }
            var events = life.Master.events.GroupBy(e => e.kind).Select(g => g.First().Copy()).ToList();
            Assert.IsTrue(events.Any(e => e.kind == DomainEventKind.PatternBroken));
            Assert.IsTrue(events.Any(e => e.kind == DomainEventKind.TimeEcho));
            Assert.IsTrue(events.Any(e => e.kind == DomainEventKind.Surprise));
            var reality = new RealityConstellation();
            FutureMemory memory=life.Master.memories.Single(m=>!string.IsNullOrEmpty(m.simulationNodeId));
            Assert.IsTrue(life.CausalNodes.Any(n=>n.id==memory.imaginationNodeId));
            Assert.IsTrue(life.CausalNodes.Any(n=>n.id==memory.simulationNodeId));
            DateTime day = new DateTime(2026, 10, 5);
            RealityQuest quest = reality.Offer(day, "help", "亲自向一位朋友求助", memory.id);
            Assert.IsTrue(reality.Complete(quest.id, day, new[] { memory }));
            events.AddRange(reality.events.Select(e => e.Copy()));
            return events;
        }
        [Test]
        public void ActualPlaythroughConnectsImaginationSimulationAndRealityWithoutPresentationSideEffects()
        {
            List<DomainEvent> events=RealEvents();
            var kinds=events.Select(e=>e.kind).ToArray();
            CollectionAssert.IsSubsetOf(new[] { DomainEventKind.FutureMemory,DomainEventKind.DejaVu,
                DomainEventKind.PatternBroken,DomainEventKind.TimeEcho,DomainEventKind.PredictionLocked,
                DomainEventKind.Surprise,DomainEventKind.Synchronized,DomainEventKind.RealityNode,DomainEventKind.RealityConvergence },kinds);
            foreach(DomainEvent e in events)
            {
                string before=JsonUtility.ToJson(e);
                RewardPlan plan=RewardDirector.Direct(e);
                var clock=new CinematicClock(plan); clock.Advance(plan.Duration,c=>{}); clock.Skip();
                Assert.AreEqual(before,JsonUtility.ToJson(e));
                Assert.IsTrue(plan.Cues.All(c=>c.Time>=0 && c.Time<=plan.Duration));
            }
        }
    }
}
