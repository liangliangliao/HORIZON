using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class StoryChapterTests
    {
        private static void Ready(GameSession s)
        { while (s.HasPredictionReview) s.MarkPredictionReviewed(); if (s.CanPredict) s.SkipPrediction(); }
        private static void Day(GameSession s, bool recovery = false)
        {
            Ready(s);
            if (s.ChapterNeedsPreparation) Assert.IsTrue(s.PrepareChapter("help"));
            CardSpec card = recovery || s.Energy < 4 ? s.Hand[2] : s.Hand[1];
            Assert.IsTrue(s.CanPlay(card)); s.Choose(card.Id);
            if (s.NeedsStation) s.VisitStation(); if (s.Day < s.Deadline) s.Advance();
        }
        private static GameSession AtBoss(string id, bool reload = false, bool recover = true)
        {
            GameSession s = GameSession.StartMasterLife(2, 41, RunMode.Quick); s.BeginChapter(id);
            while (!s.ChapterNeedsBoss)
            {
                Day(s, s.Day == 5 && recover);
                if (reload) s = GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(s.Snapshot())));
            }
            Ready(s); return s;
        }
        private static RunRecord Finish(GameSession s)
        { while (s.CompletedRun == null) Day(s, true); return s.CompletedRun; }

        [TestCase("uncertainty", "help", 12)]
        [TestCase("tomorrow", "help", 10)]
        [TestCase("perfection", "draft", 12)]
        public void DistinctBossesNeedActualRecoveryAndLeadToRecordedOutcomes(string id, string route, int window)
        {
            GameSession s = AtBoss(id); Assert.AreEqual(window, s.Day); Assert.IsNotEmpty(s.Master.chapter.recoveryNode); Assert.IsNotEmpty(s.Master.chapter.returnNode);
            Assert.IsTrue(s.CanResolveChapter(route), "Ability=" + s.Ability + " Energy=" + s.Energy + " Relation=" + s.Relation);
            Assert.IsTrue(s.ResolveChapter(route)); RunRecord run = Finish(s);
            Assert.AreEqual(ChapterOutcome.Arrived, run.master.chapter.outcome); Assert.AreEqual(route, run.master.chapter.route);
            Assert.IsTrue(CausalGraph.Ancestors(GameSession.GraphForRun(run), run.master.chapter.outcomeNode).Any(n => n.id == run.master.chapter.failureNode));
            Assert.IsTrue(s.Master.events.Any(e => e.kind == DomainEventKind.Comeback));
        }

        [TestCase("step", -1, -1)] [TestCase("help", -1, 0)] [TestCase("delay", -2, -2)]
        public void PreparationChangesTheActualSetbackCost(string response, int energy, int mood)
        {
            var s = GameSession.StartMasterLife(2, 41, RunMode.Quick); s.BeginChapter("uncertainty"); Day(s, true); Day(s, true); Ready(s);
            ResourceDelta fee = s.ChapterPreparationCost(response); int money = s.Money, insight = s.Insight;
            Assert.IsTrue(s.PrepareChapter(response)); Assert.AreEqual(money + fee.money, s.Money); Assert.AreEqual(insight + fee.insight, s.Insight);
            Day(s, true); Day(s, true);
            CausalNode failure = s.CausalNodes.Single(n => n.id == s.Master.chapter.failureNode);
            Assert.AreEqual(energy, failure.effect.energy); Assert.AreEqual(mood, failure.effect.mood);
            Assert.Contains(s.Master.chapter.preparationNode, CausalGraph.Parents(failure));
        }

        [Test]
        public void BossCannotBeWonBySkippingRecoveryAndRestart()
        {
            var s = GameSession.StartMasterLife(2, 41, RunMode.Quick); s.BeginChapter("uncertainty");
            while (!s.ChapterNeedsBoss) Day(s, true);
            Ready(s); string before = JsonUtility.ToJson(s.Snapshot());
            Assert.IsFalse(s.ResolveChapter("help")); Assert.IsFalse(s.ResolveChapter("act")); Assert.AreEqual(before, JsonUtility.ToJson(s.Snapshot()));
            Assert.IsTrue(s.ResolveChapter("leave")); Assert.AreEqual(ChapterOutcome.WindowClosed, s.Master.chapter.outcome);
        }

        [Test]
        public void UnresolvedEncountersBlockCardsButInvalidRoutesDoNotMutateState()
        {
            var s = GameSession.StartMasterLife(2, 41, RunMode.Quick); s.BeginChapter("tomorrow"); Day(s, true); Day(s, true); Ready(s);
            Assert.IsTrue(s.ChapterNeedsChoice); Assert.IsTrue(s.Hand.All(c => !s.CanPlay(c)));
            string before = JsonUtility.ToJson(s.Snapshot()); Assert.IsFalse(s.PrepareChapter("unknown")); Assert.AreEqual(before, JsonUtility.ToJson(s.Snapshot()));
            Assert.IsTrue(s.PrepareChapter("delay")); Assert.IsFalse(s.ChapterNeedsChoice); Assert.IsTrue(s.CanPlay(s.Hand[2]));
        }

        [TestCase("uncertainty", "help")] [TestCase("tomorrow", "help")] [TestCase("perfection", "draft")]
        public void SaveResumeAndRuleReplayPreserveChapterEvidence(string id, string route)
        {
            var s = AtBoss(id, true); Assert.IsTrue(s.ResolveChapter(route)); RunRecord run = Finish(s);
            RunRecord replay = GameSession.ReplayChoices(run, new Dictionary<int, string>());
            Assert.AreEqual(run.master.chapter.outcome, replay.master.chapter.outcome); Assert.AreEqual(run.master.chapter.route, replay.master.chapter.route);
            Assert.AreEqual(JsonUtility.ToJson(run.master.chapter), JsonUtility.ToJson(replay.master.chapter));
            Assert.AreEqual(run.finalAbility, replay.finalAbility); Assert.AreEqual(run.finalEnergy, replay.finalEnergy);
        }

        [Test]
        public void ReplayChangingRecoveryChangesTheOutcomeWithoutInventingEvidence()
        {
            var s = AtBoss("uncertainty"); s.ResolveChapter("help"); RunRecord run = Finish(s);
            var changes = new Dictionary<int, string>();
            for (int day = 5; day <= 12; day++)
            {
                var choices = GameSession.AlternativesForDay(run, day, changes); Assert.IsNotEmpty(choices, "Day " + day);
                changes[day] = choices.Last(c => c.Kind == CardKind.Recovery).Id;
            }
            RunRecord replay = GameSession.ReplayChoices(run, changes);
            Assert.IsNotNull(replay);
            Assert.AreEqual(ChapterOutcome.WindowClosed, replay.master.chapter.outcome);
            Assert.IsTrue(string.IsNullOrEmpty(replay.master.chapter.returnNode));
        }

        [Test]
        public void StoryPathsUseRealEdgesAndKeepHiddenProvenanceHidden()
        {
            var s = AtBoss("uncertainty"); s.ResolveChapter("help"); RunRecord run = Finish(s);
            var paths = CausalStoryPaths.For(run); Assert.IsNotEmpty(paths); Assert.AreEqual(run.master.chapter.outcomeNode, paths[0].nodes.Last().id);
            foreach (var path in paths) for (int i = 1; i < path.nodes.Count; i++) Assert.Contains(path.nodes[i - 1].id, CausalGraph.Parents(path.nodes[i]));
            CausalNode outcome = GameSession.GraphForRun(run).Single(n => n.id == run.master.chapter.outcomeNode); outcome.originHidden = true;
            Assert.IsFalse(CausalStoryPaths.For(run).Any(p => p.nodes.Any(n => n.id == outcome.id)), "A hidden root cannot disclose its parents.");
        }

        [Test]
        public void LateStartStillHasValidFailureAndRecoveryDays()
        {
            var s = GameSession.StartMasterLife(2, 41, RunMode.Quick); for (int i = 0; i < 4; i++) Day(s, true); Ready(s);
            s.BeginChapter("tomorrow"); s.Master.chapter.Validate(s.Deadline);
            s = GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(s.Snapshot()))); Assert.AreEqual(10, s.Master.chapter.windowDay);
        }

        [Test]
        public void BehavioralModelUsesStoryOutcomeInsteadOfGenericThreeGates()
        {
            var s = AtBoss("uncertainty"); s.ResolveChapter("help"); Finish(s); var archive = new ArchiveData(); archive.Repair();
            archive.me.failedRuns = 2; archive.CaptureMaster(s); Assert.AreEqual(0, archive.me.failedRuns);
        }

        [Test]
        public void RepeatedSetbackPatternBreaksAfterRecoveryAndActualRestart()
        {
            var model = new PlayerBehavioralModel(); model.Observe(new[] {
                new BehaviorObservation { id = "past1", run = 1, day = 5, key = "setback", nodeId = "f1" },
                new BehaviorObservation { id = "past2", run = 2, day = 5, key = "setback", nodeId = "f2" } });
            var s = GameSession.StartMasterLife(3, 41, RunMode.MirrorRun, model); s.BeginChapter("uncertainty");
            while (s.Day < 5) Day(s, true); Day(s, true);
            Assert.IsFalse(s.Master.events.Any(e => e.kind == DomainEventKind.PatternBroken));
            Day(s); Assert.IsNotEmpty(s.Master.chapter.returnNode);
            Assert.IsTrue(s.Master.events.Any(e => e.kind == DomainEventKind.PatternBroken));
        }

        [Test]
        public void NoAttemptProducesAnUnstartedPreparationSetbackInsteadOfFictitiousWorkFeedback()
        {
            var s = GameSession.StartMasterLife(2, 41, RunMode.Quick); s.BeginChapter("uncertainty");
            while (s.Day < 5) Day(s, true);
            CausalNode node = s.CausalNodes.Single(n => n.id == s.Master.chapter.failureNode);
            Assert.That(node.label, Does.Contain("还没有开始"));
            Assert.IsFalse(CausalGraph.Ancestors(s.CausalNodes, node.id).Any(n => n.type == CausalNodeKind.Action));
        }

        [Test]
        public void OldCompletedLifeDoesNotBecomeAnEmptyStoryAfterUnitySerialization()
        {
            var s = GameSession.StartMasterLife(2, 41, RunMode.Quick); RunRecord run = Finish(s);
            var archive = new ArchiveData(); archive.runs.Add(run);
            var restored = JsonUtility.FromJson<ArchiveData>(JsonUtility.ToJson(archive));
            // Exercise the Unity inline-null representation in both harnesses.
            restored.runs[0].master.chapter = new StoryChapter(); restored.Repair();
            Assert.IsNull(restored.runs[0].master.chapter);
            Assert.IsNull(GameSession.ReplayChoices(restored.runs[0], new Dictionary<int, string>()).master.chapter);
        }
    }
}
