using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class MasterSpecificationTests
    {
        private static void Prepare(GameSession s) { while (s.HasPredictionReview) s.MarkPredictionReviewed(); if (s.CanPredict) s.SkipPrediction(); }
        private static void Step(GameSession s, string card = null)
        { Prepare(s); s.Choose(card ?? s.Hand[2].Id); if (s.NeedsStation) s.VisitStation(); if (s.Day < s.Deadline) s.Advance(); }
        private static GameSession Reload(GameSession s) { return GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(s.Snapshot()))); }
        private static ImagineRun Imagine(int difficulty = 1)
        {
            var r = ImaginationEngine.Begin("imagine-test", "interview", "参加一次面试", difficulty);
            while (r.phase != ImaginePhase.Complete)
                if (r.phase == ImaginePhase.Recover) ImaginationEngine.Recover(r, RecoveryAction.Rest); else ImaginationEngine.Continue(r);
            return r;
        }

        [Test]
        public void LegacyLifeNeverAcquiresMasterRulesOnReload()
        {
            var s = new GameSession(3, 15, 6); Step(s); string before = JsonUtility.ToJson(s.Snapshot());
            s = Reload(s); Assert.IsFalse(s.UsesMasterRules); Assert.IsNull(s.Master); Assert.AreEqual(before, JsonUtility.ToJson(s.Snapshot()));
        }
        [Test]
        public void LockedDecisionRequiresExecutionAndSurvivesReload()
        {
            var s = new GameSession(2, 41); string card = s.Hand[1].Id; s.LockDecision(card);
            Assert.IsFalse(s.CanPlay(s.Hand[2])); Assert.Throws<InvalidOperationException>(() => s.Choose(card));
            s.ExecuteDecisionStep(); s = Reload(s); Assert.AreEqual(1, s.Master.decision.step);
            while (s.Master.decision.status != DecisionStatus.Ready) s.ExecuteDecisionStep();
            Assert.IsTrue(s.CanPlay(CardCatalog.FindById(card))); s.Choose(card);
            Assert.AreEqual(DecisionStatus.Completed, s.Master.decision.status);
            Assert.IsTrue(CausalGraph.Ancestors(s.CausalNodes, s.Actions[0].nodeId).Any(n => n.type == CausalNodeKind.Decision));
        }
        [Test]
        public void UnlockIsRecordedAsExecutionPhaseReopenedDecision()
        {
            var s = new GameSession(2, 41); s.LockDecision(s.Hand[1].Id); s.ExecuteDecisionStep(); s.UnlockDecision();
            Assert.AreEqual(1, s.Master.decision.reopens); Assert.IsTrue(s.CanPlay(s.Hand[2]));
            Assert.IsFalse(s.Master.observations.Single().continued);
            Assert.That(s.Master.observations.Single().key, Is.EqualTo("decision-reopen"));
        }
        [Test]
        public void RepeatedPatternBreakUsesActualPriorFailuresAndCapsMythicRewards()
        {
            var model = new PlayerBehavioralModel();
            model.Observe(new[] { new BehaviorObservation { id = "a", run = 1, day = 1, key = "decision-reopen", nodeId = "x" },
                new BehaviorObservation { id = "b", run = 2, day = 1, key = "decision-reopen", nodeId = "y" } });
            var s = GameSession.StartMasterLife(3, 41, RunMode.MirrorRun, model);
            s.LockDecision(s.Hand[1].Id); for (int i = 0; i < 4; i++) s.ExecuteDecisionStep(); s.Choose(s.Master.decision.cardId);
            DomainEvent broken = s.Master.events.Single(e => e.kind == DomainEventKind.PatternBroken);
            Assert.AreEqual(RewardTier.Mythic, broken.tier);
            Assert.AreEqual(2, CausalGraph.Ancestors(s.CausalNodes, broken.nodeId).Count(n => n.type == CausalNodeKind.Memory));
            for (int i = 0; i < 4; i++) RewardEngine.Emit(s.Master, 3, 1, DomainEventKind.PatternBroken, null, "pattern");
            Assert.AreEqual(2, s.Master.events.Count(e => e.tier == RewardTier.Mythic));
            model.Observe(s.Master.observations); Assert.IsTrue(model.patterns.Single().broken);
        }
        [TestCase(1, 1)] [TestCase(2, 3)] [TestCase(3, 4)]
        public void ImaginationCannotReachVictoryWithoutFailuresAndRecovery(int difficulty, int required)
        {
            ImagineRun run = Imagine(difficulty); Assert.AreEqual(required, run.failures); Assert.AreEqual(required, run.recovered);
            Assert.AreEqual(required + 1, run.multiplier); Assert.AreEqual(required, run.memories.Count);
            Assert.IsTrue(run.timeline.Any(b => b.phase == ImaginePhase.Effort));
            var saved = JsonUtility.FromJson<ImagineRun>(JsonUtility.ToJson(run)); saved.Validate();
            Assert.Throws<InvalidOperationException>(() => ImaginationEngine.Recover(run, RecoveryAction.AskHelp));
        }
        [Test]
        public void ImaginationIsIsolatedUntilAnExplicitCompletedPathIsAttached()
        {
            var s = GameSession.StartMasterLife(2, 41, RunMode.Quick, new PlayerBehavioralModel { failedRuns = 1 }); string original = JsonUtility.ToJson(s.Snapshot());
            ImagineRun run = Imagine(); Assert.AreEqual(original, JsonUtility.ToJson(s.Snapshot()));
            s.AttachImagination(run); int count = s.CausalNodes.Count; s.AttachImagination(run);
            Assert.AreEqual(count, s.CausalNodes.Count); Assert.AreEqual(1, s.Master.memories.Count);
            s = Reload(s); s.Choose(s.Hand[2].Id);
            Assert.IsTrue(s.Master.events.Any(e => e.kind == DomainEventKind.DejaVu));
            Assert.AreEqual(s.Actions[0].nodeId, s.Master.memories[0].simulationNodeId);
        }
        [Test]
        public void RealityQuestIsOncePerDateAndConvergenceNeedsAllThreeKindsOfEvidence()
        {
            var reality = new RealityConstellation(); var date = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
            var memory = new FutureMemory { id = "m", goalId = "goal", imaginationNodeId = "i", simulationNodeId = "s", simulationRun = 2, simulationDay = 4 };
            var q = reality.Offer(date, "goal", "打开招聘软件", "m");
            Assert.AreSame(q, reality.Offer(date, "other", "穿上鞋"));
            Assert.IsTrue(reality.Complete(q.id, date, new[] { memory })); Assert.IsFalse(reality.Complete(q.id, date, new[] { memory }));
            Assert.AreEqual(1, reality.events.Count(e => e.kind == DomainEventKind.RealityConvergence)); Assert.AreEqual(3, reality.nodes.Count);
            var next = reality.Offer(date.AddDays(1), "goal", "投递一次", "m"); Assert.IsTrue(reality.Complete(next.id, date.AddDays(1), new[] { memory }));
            Assert.AreEqual(1, reality.events.Count(e => e.kind == DomainEventKind.RealityConvergence));
            var late = reality.Offer(date.AddDays(2), "goal", "准备一次"); Assert.IsFalse(reality.Complete(late.id, date.AddDays(3), null));
        }
        [Test]
        public void AnImaginedMemoryAloneCannotCauseRealityConvergence()
        {
            var c = new RealityConstellation(); var date = new DateTime(2026, 10, 3);
            var q = c.Offer(date, "goal", "站起来", "m"); c.Complete(q.id, date, new[] { new FutureMemory { id = "m", goalId = "goal", imaginationNodeId = "i" } });
            Assert.IsFalse(c.events.Any(e => e.kind == DomainEventKind.RealityConvergence)); Assert.AreEqual(1, c.nodes.Count);
        }
        [Test]
        public void EventSubscriberFailureCannotUndoRuleTransaction()
        {
            var s = new GameSession(2, 41); s.DomainEventRaised += e => { throw new Exception("view failed"); };
            s.Choose(s.Hand[2].Id); Assert.IsTrue(s.HasChosen); Assert.AreEqual(1, s.Actions.Count);
        }
        [Test]
        public void ReplayingCommandsReproducesResourcesAndDecisionCausality()
        {
            var s = GameSession.StartMasterLife(3, 15, RunMode.ExperimentRun); s.EquipTrigger("alarm"); s.LowerFriction(); s.AttachImagination(Imagine());
            s.LockDecision(s.Hand[1].Id); for (int i = 0; i < 4; i++) s.ExecuteDecisionStep(); Step(s, s.Master.decision.cardId);
            while (s.CompletedRun == null) Step(s);
            RunRecord replay = GameSession.ReplayChoices(s.CompletedRun, new Dictionary<int, string>());
            Assert.AreEqual(s.CompletedRun.finalEnergy, replay.finalEnergy); Assert.AreEqual(s.CompletedRun.finalAbility, replay.finalAbility);
            CollectionAssert.AreEqual(s.CompletedRun.causalNodes.Select(n => n.label), replay.causalNodes.Select(n => n.label));
            CollectionAssert.AreEqual(s.Master.events.Select(e => e.kind), replay.master.events.Select(e => e.kind));
            Assert.AreEqual(s.Master.orbitBits, replay.master.orbitBits);
        }
        [Test]
        public void KnowledgeCannotSkipFromKnowingToRealExperience()
        {
            var k = KnowledgeForge.Learn("face", "遇到不确定", "先做一步");
            Assert.Throws<InvalidOperationException>(() => KnowledgeForge.Advance(k, KnowledgeStage.Experience, "fake"));
            KnowledgeForge.Advance(k, KnowledgeStage.Recognize, "thought"); KnowledgeForge.Advance(k, KnowledgeStage.Simulate, "game");
            KnowledgeForge.Advance(k, KnowledgeStage.Execute, "reality"); KnowledgeForge.Advance(k, KnowledgeStage.Experience, "reflection");
            Assert.AreEqual("reality", k.realityNodeId);
        }
        [Test]
        public void MasterSnapshotsAreIndependentAndRewardAcknowledgementIsIdempotent()
        {
            var s = new GameSession(2, 41); s.LockDecision(s.Hand[1].Id); RunSnapshot snap = s.Snapshot(); s.ExecuteDecisionStep();
            Assert.AreEqual(0, snap.master.decision.step); string id = s.Master.events.First().id;
            Assert.IsTrue(RewardEngine.Acknowledge(s.Master, id)); Assert.IsFalse(RewardEngine.Acknowledge(s.Master, id));
            Assert.IsTrue(Reload(s).Master.events.First().acknowledged);
        }
        [Test]
        public void AllModesHaveAnAffordableRecoveryAndCanCompleteAcrossSeeds()
        {
            foreach (RunMode mode in Enum.GetValues(typeof(RunMode))) for (int seed = 0; seed < 8; seed++)
            {
                var s = GameSession.StartMasterLife(3, seed, mode);
                while (s.CompletedRun == null)
                {
                    Prepare(s); Assert.IsTrue(s.CanPlay(s.Hand[2]), mode + "/" + seed + "/" + s.Day);
                    string card = s.Day % 3 == 0 && s.CanPlay(s.Hand[1]) ? s.Hand[1].Id : s.Hand[2].Id;
                    Step(s, card); if (s.CompletedRun == null && s.Day % 4 == 0) s = Reload(s);
                }
                Assert.AreEqual(s.Deadline, s.CompletedRun.actions.Count); s.Master.Validate(s.Day, s.Deadline);
                Assert.IsTrue(s.CausalNodes.All(n => CausalGraph.Parents(n).All(id => s.CausalNodes.Any(p => p.id == id))));
            }
        }
        [Test]
        public void TimelineCompareFindsFirstActualDivergence()
        {
            TimelineDivergence d = ImaginationEngine.Compare(new[] { "A", "B", "C" }, new[] { "A", "B", "X", "Y" });
            Assert.AreEqual(2, d.index); Assert.AreEqual("C", d.imagined); Assert.AreEqual("X", d.actual); Assert.IsFalse(d.converged);
        }
    }
}
