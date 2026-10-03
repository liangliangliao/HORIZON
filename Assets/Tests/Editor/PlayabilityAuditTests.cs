using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class PlayabilityAuditTests
    {
        private static GameSession Reload(GameSession life) { return GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(life.Snapshot()))); }
        private static ImagineRun Imagine(string id, string goalId, RecoveryAction recovery = RecoveryAction.Rest)
        {
            ImagineRun run = ImaginationEngine.Begin(id, goalId, CardCatalog.FindById(goalId).Name, 1, 1);
            while (run.phase != ImaginePhase.Complete)
            {
                if (run.phase == ImaginePhase.Preparation) ImaginationEngine.Prepare(run, PreparationAction.SmallStep);
                else if (run.phase == ImaginePhase.Recover) ImaginationEngine.Recover(run, recovery);
                else ImaginationEngine.Continue(run);
            }
            return run;
        }
        private static void Ready(GameSession life) { for (int i = 0; i < 4; i++) life.ExecuteDecisionStep(); }

        private static bool HasCycle(List<CausalNode> graph)
        {
            var nodes = graph.ToDictionary(n => n.id); var visited = new HashSet<string>(); var active = new HashSet<string>();
            Func<string, bool> visit = null;
            visit = id => {
                if (active.Contains(id)) return true;
                if (visited.Contains(id)) return false;
                Assert.IsTrue(nodes.ContainsKey(id), "Missing cause " + id); active.Add(id);
                foreach (string parent in CausalGraph.Parents(nodes[id])) if (visit(parent)) return true;
                active.Remove(id); visited.Add(id); return false;
            };
            return nodes.Keys.Any(visit);
        }

        [Test]
        public void PreparationConsumesFocusAndActuallySavesEnergyWithMatchingEquipment()
        {
            var life = new GameSession(1, 17); string card = life.Hand[1].Id;
            life.LockDecision(card); life.EquipTrigger("alarm"); Assert.IsTrue(life.LowerFriction());
            Assert.AreEqual(1, life.Insight); Assert.IsTrue(life.PreparationWillSave(CardCatalog.FindById(card)));
            life = Reload(life); Ready(life); life = Reload(life);
            Assert.AreEqual(1, life.PreparationSaving(CardCatalog.FindById(card)));
            int energy = life.Energy; ActionRecord taken = life.Choose(card);
            Assert.AreEqual(energy - 1, life.Energy);
            Assert.That(CausalGraph.Ancestors(life.CausalNodes, taken.nodeId).Any(n => n.type == CausalNodeKind.Trigger));
        }

        [Test]
        public void UnmatchedReminderDoesNotGiveAnExecutionDiscount()
        {
            var life = new GameSession(1, 17); string card = life.Hand[1].Id;
            life.LockDecision(card); life.EquipTrigger("friend"); life.LowerFriction(); Ready(life);
            Assert.AreEqual(0, life.PreparationSaving(CardCatalog.FindById(card)));
            int energy = life.Energy; life.Choose(card); Assert.AreEqual(energy - 2, life.Energy);
        }

        [Test]
        public void PaidTriggersAndFocusPreparationRejectUnaffordableRequestsWithoutMutation()
        {
            var life = new GameSession(1, 1); int money = life.Money;
            Assert.IsTrue(life.EquipTrigger("ticket")); Assert.AreEqual(money - 1, life.Money);
            RunSnapshot saved = life.Snapshot(); saved.money = 0; saved.insight = 0; life = GameSession.Restore(saved);
            string before = JsonUtility.ToJson(life.Snapshot());
            Assert.IsFalse(life.EquipTrigger("deposit")); Assert.IsFalse(life.LowerFriction());
            Assert.AreEqual(before, JsonUtility.ToJson(life.Snapshot()));
        }

        [TestCase(7)] [TestCase(8)]
        public void LegacyLivesKeepFreeEquipmentAndOriginalExecutionCost(int catalog)
        {
            var life = new GameSession(1, 17, catalog); string card = life.Hand[1].Id;
            life.LockDecision(card); life.EquipTrigger("ticket"); life.EquipTrigger("alarm"); life.LowerFriction(); Ready(life);
            Assert.AreEqual(5, life.Money); Assert.AreEqual(2, life.Insight);
            Assert.AreEqual("准备材料", life.Master.decision.steps[0]);
            Assert.AreEqual(-2, life.ImmediateEffect(CardCatalog.FindById(card)).energy);
        }

        [Test]
        public void OneActionWithManyPreparationNodesCannotBecomeASuperCombo()
        {
            var life = new GameSession(1, 17); string card = life.Hand[1].Id;
            life.LockDecision(card); life.EquipTrigger("alarm"); life.LowerFriction(); Ready(life); life.Choose(card); life.Advance();
            while (life.Day < 4) { life.Choose(life.Hand[2].Id); life.Advance(); }
            Assert.That(life.Master.events.Any(e => e.kind == DomainEventKind.TimeEcho));
            Assert.IsFalse(life.Master.events.Any(e => e.kind == DomainEventKind.Cascade || e.kind == DomainEventKind.CausalSingularity));
        }

        [Test]
        public void ImaginedPreparationBranchesPersistAndCannotSkipTheFailure()
        {
            var run = ImaginationEngine.Begin("branch", "practice", "完成一次练习", 1, 1);
            ImaginationEngine.Continue(run);
            Assert.Throws<InvalidOperationException>(() => ImaginationEngine.Continue(run));
            ImaginationEngine.Prepare(run, PreparationAction.WithSupport);
            run = JsonUtility.FromJson<ImagineRun>(JsonUtility.ToJson(run)); run.Validate();
            ImaginationEngine.Continue(run); Assert.That(run.timeline.Last().text, Does.Contain("没空"));
            Assert.AreEqual(ImaginePhase.Failure, run.phase);
        }

        [Test]
        public void ActualChoiceDivergenceUpdatesRecentModelOnceAndDoesNotInventReality()
        {
            var life = new GameSession(1, 17); var archive = new ArchiveData(); archive.Repair();
            string goal = life.Hand[1].Id; ImagineRun imagined = Imagine("different", goal);
            life.AttachImagination(imagined); archive.KeepImagination(imagined); archive.TrackImagination(imagined, life, goal);
            life.Choose(life.Hand[2].Id); archive.CaptureMaster(life); archive.CaptureMaster(life);
            Assert.IsTrue(archive.imaginationComparisons[0].actionObserved);
            Assert.IsFalse(archive.imaginationComparisons[0].actionMatched);
            Assert.AreEqual(1, archive.me.imaginationDifferences); Assert.AreEqual(0, archive.reality.nodes.Count);
            archive = JsonUtility.FromJson<ArchiveData>(JsonUtility.ToJson(archive)); archive.Repair(); archive.CaptureMaster(Reload(life));
            Assert.AreEqual(1, archive.me.imaginationDifferences);
        }

        [Test]
        public void NoActualSetbackRemainsPendingInsteadOfBeingMarkedAsADivergence()
        {
            var life = new GameSession(1, 17); var archive = new ArchiveData(); archive.Repair();
            string goal = life.Hand[1].Id; ImagineRun imagined = Imagine("pending", goal);
            life.AttachImagination(imagined); archive.TrackImagination(imagined, life, goal); life.Choose(goal); archive.CaptureMaster(life);
            Assert.IsTrue(archive.imaginationComparisons[0].actionMatched);
            Assert.IsFalse(archive.imaginationComparisons[0].recoveryObserved);
            Assert.AreEqual(0, archive.me.imaginationDifferences); Assert.AreEqual(1, archive.me.imaginationMatches);
        }

        [Test]
        public void RealSetbackAndNextActionProduceARecoveryComparison()
        {
            var life = new GameSession(1, 17); var archive = new ArchiveData(); archive.Repair();
            string goal = life.Hand[0].Id; ImagineRun imagined = Imagine("recovery", goal);
            life.AttachImagination(imagined); archive.TrackImagination(imagined, life, goal); life.Choose(goal); life.Advance();
            life.Choose(life.Hand[0].Id); life.Advance(); archive.CaptureMaster(life);
            Assert.Greater(archive.imaginationComparisons[0].failureDay, 0);
            life.Choose(life.Hand[2].Id); archive.CaptureMaster(life);
            Assert.IsTrue(archive.imaginationComparisons[0].recoveryMatched);
            Assert.AreEqual(2, archive.me.imaginationMatches); Assert.IsNotEmpty(archive.imaginationComparisons[0].recoveryNodeId);
        }

        [TestCase(RunMode.Quick)] [TestCase(RunMode.ThirtyDays)] [TestCase(RunMode.LongRun)] [TestCase(RunMode.ParallelLives)]
        [TestCase(RunMode.ImaginationRun)] [TestCase(RunMode.ExperimentRun)] [TestCase(RunMode.MirrorRun)] [TestCase(RunMode.ChaosRun)]
        public void EveryModeCompletesBalancedAndRecoveringLivesWithResumeAndReplay(RunMode mode)
        {
            var report = new List<string> { "mode,seed,strategy,days,passed,choices,predictions" };
            foreach (int seed in new[] { 1, 17, 53, 97 }) foreach (bool balanced in new[] { false, true })
            {
                GameSession life = GameSession.StartMasterLife(1, seed, mode);
                while (life.CompletedRun == null)
                {
                    while (life.HasPredictionReview) life.MarkPredictionReviewed();
                    if (life.CanPredict) life.LockPrediction(0, 0, 0);
                    var hand = life.Hand.Where(life.CanPlay).ToArray(); Assert.IsNotEmpty(hand, mode + " D" + life.Day);
                    CardSpec card = hand.Last();
                    if (balanced)
                    {
                        CardSpec support = hand.FirstOrDefault(c => c.GivesSupport);
                        CardSpec growth = hand.FirstOrDefault(c => c.Kind == CardKind.Growth && c.Id != "imagine");
                        if (support != null && ProductExperience.SupportEvidence(life) < ProductExperience.EvidenceNeeded(life)) card = support;
                        else if (growth != null && life.Energy >= 4 && life.Mood >= 4 && life.Day <= life.Deadline - growth.Delay &&
                            (life.Ability < 6 || ProductExperience.GrowthEvidence(life) < ProductExperience.EvidenceNeeded(life))) card = growth;
                    }
                    life.Choose(card.Id);
                    if (life.CompletedRun != null) break;
                    life = Reload(life);
                    if (life.NeedsStation) life.VisitStation(); life.Advance(); life = Reload(life);
                }
                RunRecord result = life.CompletedRun, replay = GameSession.ReplayChoices(result, new Dictionary<int, string>());
                Assert.AreEqual(life.Deadline, result.actions.Count); Assert.IsFalse(HasCycle(result.causalNodes));
                Assert.AreEqual(result.boss.passed, replay.boss.passed); Assert.AreEqual(result.finalEnergy, replay.finalEnergy);
                Assert.AreEqual(result.finalAbility, replay.finalAbility); Assert.AreEqual(result.finalRelation, replay.finalRelation);
                if (balanced) Assert.AreEqual(3, result.boss.passed, "Balanced route must be winnable: " + mode + " seed " + seed);
                report.Add(mode + "," + seed + "," + (balanced ? "balanced" : "recovery") + "," + life.Deadline + "," + result.boss.passed + "," + result.actions.Count + "," + result.predictions.Count);
            }
            Directory.CreateDirectory("artifacts/playability"); File.WriteAllLines("artifacts/playability/" + mode + ".csv", report);
        }
    }
}
