using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class PlayGuideTests
    {
        private static void AssertReplayState(RunRecord original, RunRecord replay)
        {
            // Complete() also produces a counterfactual story. A replay omits
            // recursive counterfactual generation; compare the played life.
            Assert.AreEqual(JsonUtility.ToJson(original.master), JsonUtility.ToJson(replay.master));
            Assert.AreEqual(original.actions.Count, replay.actions.Count);
            for (int i = 0; i < original.actions.Count; i++)
                Assert.AreEqual(JsonUtility.ToJson(original.actions[i]), JsonUtility.ToJson(replay.actions[i]));
            Assert.AreEqual(original.causalNodes.Count, replay.causalNodes.Count);
            for (int i = 0; i < original.causalNodes.Count; i++)
                Assert.AreEqual(JsonUtility.ToJson(original.causalNodes[i]), JsonUtility.ToJson(replay.causalNodes[i]));
            CollectionAssert.AreEqual(new[] { original.finalEnergy, original.finalMood, original.finalInsight, original.finalRelation, original.finalMoney, original.finalAbility, original.boss.passed },
                new[] { replay.finalEnergy, replay.finalMood, replay.finalInsight, replay.finalRelation, replay.finalMoney, replay.finalAbility, replay.boss.passed });
        }
        private static void Step(GameSession life, string id = null)
        {
            while (life.HasPredictionReview) life.MarkPredictionReviewed();
            if (life.CanPredict) life.SkipPrediction();
            life.Choose(id ?? life.Hand[2].Id);
            if (life.NeedsStation) life.VisitStation();
            if (life.Day < life.Deadline) life.Advance();
        }

        [Test]
        public void NewQuickLifeGraduallyIntroducesFamiliesWithoutRemovingGrowthOrRecovery()
        {
            var life = new GameSession(1, 15);
            var introduced = new List<string>();
            while (life.CompletedRun == null)
            {
                Assert.IsTrue(life.Hand.Any(c => c.Kind == CardKind.Growth));
                Assert.IsTrue(life.Hand.Any(c => c.Kind == CardKind.Recovery));
                if (life.Day < 5) Assert.IsFalse(life.Hand.Any(c => MasterContent.Actions.Contains(c)));
                introduced.AddRange(life.Hand.Where(c => MasterContent.Actions.Contains(c)).Select(c => c.Id));
                Step(life);
            }
            CollectionAssert.AreEquivalent(new[] { "trigger", "commit", "forge", "imagine" }, introduced);
        }

        [Test]
        public void VersionSevenLifeRetainsItsOriginalDeckAndReplay()
        {
            var life = new GameSession(1, 15, 7);
            while (life.CompletedRun == null)
            {
                Assert.IsFalse(life.Hand.Any(c => MasterContent.Actions.Contains(c)));
                Step(life);
            }
            var replay = GameSession.ReplayChoices(life.CompletedRun, new Dictionary<int, string>());
            AssertReplayState(life.CompletedRun, replay);
            var restored = GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(new GameSession(1, 15, 7).Snapshot())));
            Assert.AreEqual(7, restored.CatalogVersion);
        }

        [Test]
        public void ScheduleExcludesHiddenSecondOrderCausesAndDoesNotChangeTheSimulation()
        {
            var life = new GameSession(1, 15);
            Step(life, life.Hand[1].Id);
            life.Pending.Add(new PendingEcho { depth = 2, sourceDay = 1, dueDay = 3, cardName = "隐藏来源", parentNodeId = life.Actions[0].nodeId });
            string before = JsonUtility.ToJson(life.Snapshot());
            var planned = PlayGuide.Scheduled(life);
            Assert.AreEqual(1, planned.Count);
            Assert.AreEqual(life.Actions[0].echoDay, planned[0].dueDay);
            Assert.IsFalse(planned.Any(e => e.cardName == "隐藏来源"));
            PlayGuide.Today(life); PlayGuide.Next(life);
            Assert.AreEqual(before, JsonUtility.ToJson(life.Snapshot()));
        }

        [Test]
        public void ImagineCardRequiresCompletedFailureAndRecoveryOnTheCurrentDay()
        {
            var life = new GameSession(1, 15);
            while (life.Day < 11) Step(life);
            CardSpec card = life.Hand.First(c => c.Id == "imagine");
            Assert.IsFalse(life.CanPlay(card)); Assert.IsTrue(life.CanPrepareImagination(card));
            Assert.Throws<System.InvalidOperationException>(() => life.Choose(card.Id));
            var run = ImaginationEngine.Begin("prepared", card.Id, card.Name);
            Assert.Throws<System.InvalidOperationException>(() => life.AttachImagination(run));
            while (run.phase != ImaginePhase.Complete)
                if (run.phase == ImaginePhase.Recover) ImaginationEngine.Recover(run, RecoveryAction.Rest); else ImaginationEngine.Continue(run);
            life.AttachImagination(run);
            Assert.AreEqual(1, run.failures); Assert.AreEqual(1, run.recovered);
            life = GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(life.Snapshot())));
            Assert.IsTrue(life.CanPlay(card)); Assert.AreEqual(1, life.Master.memories.Count);
            Step(life, card.Id); Step(life);
            RunRecord replay = GameSession.ReplayChoices(life.CompletedRun, new Dictionary<int, string>());
            AssertReplayState(life.CompletedRun, replay);
        }

        [Test]
        public void TriggerCardEquipsARealEnvironmentAndLinksItToTheChosenAction()
        {
            var life = new GameSession(1, 15);
            while (life.Day < 5) Step(life);
            Step(life, "trigger");
            Assert.Contains("alarm", life.Master.triggers);
            Assert.AreEqual(2, life.Master.engine.trigger);
            CausalNode trigger = life.CausalNodes.Last(n => n.type == CausalNodeKind.Trigger);
            Assert.Contains(life.Actions[4].nodeId, CausalGraph.Parents(trigger));
            life = GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(life.Snapshot())));
            Assert.AreEqual(1, life.Master.triggers.Count);
            while (life.CompletedRun == null) Step(life);
            var replay = GameSession.ReplayChoices(life.CompletedRun, new Dictionary<int, string>());
            AssertReplayState(life.CompletedRun, replay);
        }

        [Test]
        public void FailureOffersRecoveryAndComebackGuidanceFromTheActualState()
        {
            var life = new GameSession(1, 15);
            Step(life, "scroll"); Step(life, life.Hand[1].Id);
            Assert.IsTrue(life.Master.awaitingComeback);
            StringAssert.Contains("重新开始", PlayGuide.Today(life));
            Assert.IsTrue(life.Hand.Any(c => c.Kind == CardKind.Recovery && life.CanPlay(c)));
            Step(life, life.Hand[2].Id);
            Assert.IsFalse(life.Master.awaitingComeback);
            Assert.IsTrue(life.Master.events.Any(e => e.kind == DomainEventKind.Comeback));
        }

        [Test]
        public void FirstChoiceGuideAndPreparedImaginationSurviveArchiveSerialization()
        {
            var archive = new ArchiveData { playGuide = new PlayGuideProgress { run = 1, page = 1, preparedRun = 1, preparedDay = 3, preparedCardId = "practice", imagineFromBoard = true } };
            archive = JsonUtility.FromJson<ArchiveData>(JsonUtility.ToJson(archive)); archive.Repair();
            Assert.AreEqual(1, archive.playGuide.page); Assert.IsFalse(archive.playGuide.completed);
            Assert.AreEqual("practice", archive.playGuide.preparedCardId);
            Assert.IsTrue(archive.playGuide.imagineFromBoard);
        }
    }
}
