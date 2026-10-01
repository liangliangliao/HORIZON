using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class CausalPresentationTests
    {
        private static void Ready(GameSession life)
        { while (life.HasPredictionReview) life.MarkPredictionReviewed(); if (life.CanPredict) life.SkipPrediction(); }
        private static void Finish(GameSession life)
        { while (life.CompletedRun == null) { Ready(life); life.Choose(life.Hand[2].Id);
            if (life.NeedsStation) life.VisitStation(); if (life.CompletedRun == null) life.Advance(); } }

        [Test]
        public void OneObservationContractControlsDistanceTypesAndCapabilities()
        {
            var life = new GameSession(1, 15, 4);
            string frozen = JsonUtility.ToJson(life.Snapshot());
            int[] distances = { 0, 1, 3, 3, 7, 7, 30 };
            var journey = new JourneyProgress();
            for (int stage = 1; stage <= 7; stage++)
            {
                journey.Visit("2026-10-" + stage.ToString("00"));
                HorizonProgress p = HorizonProgress.Resolve(life, journey, 0);
                Assert.AreEqual(stage, p.Stage); Assert.AreEqual(distances[stage - 1], p.Days);
                Assert.AreEqual(stage >= 4, p.SecondOrder); Assert.AreEqual(stage >= 5, p.Probability);
                Assert.AreEqual(stage >= 6, p.Compare); Assert.AreEqual(stage == 7, p.ThirtyDays);
                Assert.AreEqual(p.VisibleTypes, ObservationDesign.VisibleTypes(life, stage));
                Assert.AreEqual(p.Compare, ObservationDesign.CanCompare(life, stage));
            }
            Assert.AreEqual(frozen, JsonUtility.ToJson(life.Snapshot()), "Observation must not enable different weather or choices in an old life.");
        }

        [Test]
        public void InformationRespectsDistanceAndCalibrationWithoutLeakingHiddenOrigins()
        {
            var echo = new PendingEcho { dueDay = 5, sourceDay = 1, depth = 2, echoName = "错过邀约",
                kind = CardKind.Temptation, delta = new ResourceDelta(-2) };
            Assert.That(new HorizonProgress(2, 10).Clue(echo, 0, 1), Does.Not.Contain("D1"));
            Assert.That(new HorizonProgress(5, 0).Clue(echo, 0, 1), Does.Not.Contain("精力"));
            Assert.That(new HorizonProgress(5, 1).Clue(echo, 0, 1), Does.Contain("精力↓").And.Not.Contain("明显"));
            Assert.That(new HorizonProgress(5, 3).Clue(echo, 0, 1), Does.Contain("明显"));
            Assert.That(new HorizonProgress(5, 10).Clue(echo, 0, 1), Does.Contain("源自 D1"));
            Assert.That(new HorizonProgress(5, 10).Clue(echo, 2, 1), Does.Not.Contain("源自"));
        }

        [Test]
        public void UnderstandingPersistsAcrossLivesAndNonconsecutiveReturnDates()
        {
            var journey = new JourneyProgress(); journey.Visit("2026-01-01"); journey.Visit("2026-08-20");
            journey.Visit("2026-08-20"); journey.Observe(new GameSession(4, 15));
            foreach (LifeLesson lesson in new[] { LifeLesson.Returns, LifeLesson.Uncertainty, LifeLesson.Prediction,
                LifeLesson.Preparation, LifeLesson.Identity }) { journey.Remember(lesson); journey.Remember(lesson); }
            journey = JsonUtility.FromJson<JourneyProgress>(JsonUtility.ToJson(journey)); journey.Repair();
            Assert.AreEqual(2, journey.Chapter); Assert.AreEqual(31, journey.lessonBits);
            Assert.AreEqual(6, HorizonProgress.Resolve(new GameSession(1, 15), journey).Stage);
            Assert.IsTrue(journey.Knows(LifeLesson.Identity));
        }

        [Test]
        public void StationContainsRealMultiSourceEdgesAndKeepsMysteryProvenanceHidden()
        {
            var root = new ActionRecord { day = 1, nodeId = "a", cardName = "学习" };
            var graph = new List<CausalNode> {
                new CausalNode { id = "a", day = 1, type = CausalNodeKind.Action, label = "学习", resolved = true },
                new CausalNode { id = "b", day = 2, type = CausalNodeKind.Action, label = "交朋友", resolved = true },
                new CausalNode { id = "c", day = 4, parentId = "a", type = CausalNodeKind.Echo, label = "能力", resolved = true },
                new CausalNode { id = "d", day = 5, parentIds = new List<string> { "b", "c" },
                    type = CausalNodeKind.Choice, label = "合作机会", resolved = false },
                new CausalNode { id = "secret", day = 6, parentId = "a", originHidden = true,
                    type = CausalNodeKind.Mystery, label = "尚未认出的余力", resolved = true }
            };
            string frozen = JsonUtility.ToJson(new RunSnapshot { causalNodes = graph });
            var memory = new MemoryChain(root, graph);
            CollectionAssert.AreEquivalent(new[] { "a", "b", "c", "d" }, memory.Nodes.Select(n => n.id));
            CollectionAssert.AreEquivalent(new[] { "b", "c" }, CausalGraph.ObservedParents(memory.Nodes.Last()));
            Assert.That(CausalPresentation.NodeMeaning(memory.Nodes.Last()), Does.Contain("还在路上"));
            Assert.AreEqual(frozen, JsonUtility.ToJson(new RunSnapshot { causalNodes = graph }));
        }

        [TestCase(12)]
        [TestCase(30)]
        public void EveryGhostBeatComesFromReplayAndItsFinalStateMatchesAllSixResources(int days)
        {
            GameSession life = days == 30 ? GameSession.StartLongLife(4, 15) : new GameSession(1, 15);
            Finish(life); RunRecord original = life.CompletedRun;
            Assert.Less(original.boss.passed, 3); Assert.IsNotNull(original.boss.ghostTimeline);
            string frozen = JsonUtility.ToJson(original);
            GhostStory story = CausalPresentation.Ghost(original); Assert.IsNotNull(story);
            Assert.AreEqual(original.boss.ghostTimeline.sourceDay, story.Beats.First().Day);
            Assert.AreEqual(days, story.Beats.Last().Day); Assert.IsTrue(story.Beats.Last().Deadline);
            Assert.That(story.Beats.Select(b => b.Day), Is.Ordered.Ascending);
            Assert.That(story.Beats.Select(b => b.Day), Is.Unique);
            GhostBeat last = story.Beats.Last(); RunRecord alternate = story.Alternative;
            Assert.AreEqual(original.finalEnergy, last.Before.energy); Assert.AreEqual(original.finalMood, last.Before.mood);
            Assert.AreEqual(original.finalAbility, last.Before.ability); Assert.AreEqual(original.finalRelation, last.Before.relation);
            Assert.AreEqual(original.finalMoney, last.Before.money); Assert.AreEqual(original.finalInsight, last.Before.insight);
            Assert.AreEqual(alternate.finalEnergy, last.After.energy); Assert.AreEqual(alternate.finalMood, last.After.mood);
            Assert.AreEqual(alternate.finalAbility, last.After.ability); Assert.AreEqual(alternate.finalRelation, last.After.relation);
            Assert.AreEqual(alternate.finalMoney, last.After.money); Assert.AreEqual(alternate.finalInsight, last.After.insight);
            Assert.AreEqual(frozen, JsonUtility.ToJson(original));
        }

        [Test]
        public void AlternativeGateTextReportsBothOpenedAndLostGatesHonestly()
        {
            var before = new BossResult { ability = true, state = false, support = false, passed = 1 };
            var after = new BossResult { ability = false, state = true, support = false, passed = 1 };
            string text = CausalPresentation.GateChange(before, after);
            Assert.That(text, Does.Contain("状态门：这次打开了"));
            Assert.That(text, Does.Contain("能力门：这次尚未打开"));
            Assert.That(text, Does.Not.Contain("一个选择，改变了后来的抵达"));
        }
    }
}
