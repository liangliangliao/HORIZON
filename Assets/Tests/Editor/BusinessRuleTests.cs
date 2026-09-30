using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class BusinessRuleTests
    {
        private static void Prepare(GameSession s)
        { if (s.HasPredictionReview) s.MarkPredictionReviewed(); if (s.CanPredict) s.SkipPrediction(); }
        private static void Step(GameSession s, string card = null)
        { Prepare(s); s.Choose(card ?? s.Hand[2].Id); if (s.Day == 4) s.VisitStation(); s.Advance(); }
        private static void Complete(GameSession s)
        { while (s.Day < 12) Step(s); Prepare(s); s.Choose(s.Hand[2].Id); }
        private static GameSession Copy(GameSession s)
        { return GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(s.Snapshot()))); }

        [Test]
        public void PredictionAcknowledgementGrantsKnowledgeOnceAcrossReload()
        {
            var s = new GameSession(1, 15);
            while (s.Day < 4) Step(s);
            s.LockPrediction(0, 0, 0);
            while (s.Day < 7) Step(s);
            Assert.IsTrue(s.Prediction.accurate);
            var data = new ArchiveData { active = s.Snapshot(), calibrations = 2 };
            int money = s.Money, stars = data.wallet.stardust;
            Assert.IsTrue(data.ReviewPrediction(s));
            Assert.IsFalse(data.ReviewPrediction(s));
            Assert.AreEqual(3, data.calibrations);
            var restored = JsonUtility.FromJson<ArchiveData>(JsonUtility.ToJson(data)); restored.Repair();
            Assert.IsFalse(restored.ReviewPrediction(GameSession.Restore(restored.active)));
            Assert.AreEqual(3, restored.calibrations);
            Assert.AreEqual(money, s.Money); Assert.AreEqual(stars, restored.wallet.stardust);
        }

        [Test]
        public void LifeNumbersFollowTheLargestStoredIdentity()
        {
            var data = new ArchiveData(); Assert.AreEqual(1, data.NextRunNumber);
            data.runs.Add(new RunRecord { number = 8 }); data.runs.Add(new RunRecord { number = 2 });
            Assert.AreEqual(9, data.NextRunNumber);
            data.active = new GameSession(13, 15).Snapshot(); Assert.AreEqual(14, data.NextRunNumber);
            Assert.AreEqual(14, JsonUtility.FromJson<ArchiveData>(JsonUtility.ToJson(data)).NextRunNumber);
        }

        [Test]
        public void ReopenedObservationKeepsTheActualActivePrefixAndDoesNotSpendAnything()
        {
            var s = new GameSession(3, 41); Step(s, "portfolio");
            while (s.Day < 8) Step(s);
            var data = new ArchiveData { active = Copy(s).Snapshot() }; data.wallet.stardust = 19;
            string frozen = JsonUtility.ToJson(data);
            GameSession source = data.ObservationSource();
            Assert.AreEqual(8, source.Day); Assert.AreEqual(41, source.WorldSeed);
            ForecastRange range = ForecastSimulator.Sample(source, null, 30, 6);
            Assert.AreEqual(3, range.sourceRun); Assert.AreEqual(8, range.sourceDay);
            CollectionAssert.AreEqual(s.Actions.Select(a => a.cardId), range.example.Take(7).Select(a => a.cardId));
            Assert.AreEqual(frozen, JsonUtility.ToJson(data));
        }

        [Test]
        public void ObservationAfterDeadlineUsesLatestActualLifeIncludingMystery()
        {
            var s = new GameSession(7, 15); string origin = s.Hand[1].Id; Step(s, origin);
            while (s.Day < 6) Step(s); s.ApplyMystery(1, origin); Complete(s);
            var earlier = new GameSession(2, 15); Complete(earlier);
            var data = new ArchiveData(); data.runs.Add(s.CompletedRun); data.runs.Add(earlier.CompletedRun);
            string frozen = JsonUtility.ToJson(data);
            GameSession observed = data.ObservationSource();
            Assert.AreEqual(7, observed.RunNumber); Assert.AreEqual(s.Insight, observed.Insight);
            Assert.AreEqual(1, observed.Mysteries.Count);
            ForecastRange range = ForecastSimulator.Sample(observed, null, 30, 6);
            CollectionAssert.AreEqual(s.Actions.Select(a => a.cardId), range.example.Take(12).Select(a => a.cardId));
            Assert.AreEqual(frozen, JsonUtility.ToJson(data));
        }

        [Test]
        public void StationUsesActualMostFrequentBehaviorsAndRanksRealChains()
        {
            var s = new GameSession(3, 15);
            // Build recorded observations with two large growth chains; category
            // quotas must not displace the second one with an unrelated action.
            for (int day = 1; day <= 6; day++)
            {
                string id = day <= 3 ? "rest" : day <= 5 ? "practice" : "scroll";
                var a = new ActionRecord { day = day, cardId = id, cardName = id, nodeId = "a" + day,
                    kind = id == "rest" ? CardKind.Recovery : id == "practice" ? CardKind.Growth : CardKind.Temptation };
                s.Actions.Add(a); s.CausalNodes.Add(new CausalNode { id = a.nodeId, day = day, type = CausalNodeKind.Action });
            }
            foreach (int day in new[] { 4, 5 })
                for (int child = 0; child < 3; child++) s.CausalNodes.Add(new CausalNode {
                    id = "e" + day + child, parentId = "a" + day, day = 7 + child, type = CausalNodeKind.Echo });
            List<ActionRecord> choices = ExperienceContent.CommonBehaviors(s);
            CollectionAssert.AreEqual(new[] { "rest", "practice", "scroll" }, choices.Select(a => a.cardId));
            CollectionAssert.AreEqual(new[] { 5, 4, 6 }, ExperienceContent.StationMemories(s).Select(a => a.day));
            string before = JsonUtility.ToJson(s.Snapshot()); var data = new ArchiveData();
            data.ProtectBehavior(choices[0]); Assert.AreEqual("rest", data.preferredCardId);
            Assert.AreEqual(before, JsonUtility.ToJson(s.Snapshot()));
            s.Actions.RemoveAll(a => a.cardId != "rest");
            Assert.AreEqual(1, ExperienceContent.CommonBehaviors(s).Count);
        }

        [Test]
        public void MysteryPaysActualResultOnceAndHidesOriginUntilDayNine()
        {
            var s = new GameSession(3, 15); Step(s, "portfolio");
            while (s.Day < 6) Step(s);
            int insight = s.Insight; CausalNode result = s.ApplyMystery(1, "portfolio");
            Assert.AreEqual(1, s.Insight - insight); Assert.AreEqual(1, result.effect.insight);
            Assert.IsTrue(result.originHidden); Assert.AreEqual(0, CausalGraph.ObservedParents(result).Count);
            Assert.AreEqual(1, CausalGraph.Parents(result).Count);
            Assert.AreEqual(result.id, s.ApplyMystery().id); Assert.AreEqual(insight + 1, s.Insight);
            s = Copy(s); Assert.AreEqual(result.id, s.ApplyMystery().id);
            Assert.AreEqual(insight + 1, s.Insight);
            while (s.Day < 9) Step(s);
            result = s.CausalNodes.Find(n => n.id == result.id);
            Assert.IsFalse(result.originHidden); Assert.IsTrue(s.Mysteries[0].revealed);
            Assert.AreEqual(1, CausalGraph.ObservedParents(result).Count);
        }

        [Test]
        public void MysteryAtRecoveryCapsHasAVisibleResultAndPreservesLegacyReceipts()
        {
            var s = new GameSession(3, 15); while (s.Day < 6) Step(s);
            Assert.AreEqual(10, s.Energy); Assert.AreEqual(10, s.Mood);
            int insight = s.Insight; CausalNode result = s.ApplyMystery();
            Assert.AreEqual(1, result.effect.insight); Assert.AreEqual(insight + 1, s.Insight);
            Complete(s); RunRecord replay = GameSession.ReplayChoices(s.CompletedRun, new Dictionary<int, string>());
            Assert.AreEqual(s.Insight, replay.finalInsight); Assert.AreEqual(1, replay.mysteries[0].delta.insight);
            // Early version-6 receipts did not store a delta. Preserve their
            // original clamped result instead of retroactively granting the fix.
            var old = new GameSession(3, 15); while (old.Day < 6) Step(old);
            int oldInsight = old.Insight;
            result = old.ApplyMystery(0, null, null, true); old.Mysteries[0].delta = null;
            Assert.AreEqual(0, result.effect.energy + result.effect.mood + result.effect.insight);
            Assert.AreEqual(oldInsight, old.Insight);
            Complete(old); replay = GameSession.ReplayChoices(old.CompletedRun, new Dictionary<int, string>());
            Assert.AreEqual(old.Insight, replay.finalInsight); Assert.AreEqual(old.Energy, replay.finalEnergy);
        }

        [Test]
        public void MysteryReplayReproducesTheActualLifeAndDropsWhenItsCauseChanges()
        {
            var s = new GameSession(3, 15); Step(s, "portfolio");
            while (s.Day < 6) Step(s); s.ApplyMystery(1, "portfolio"); Complete(s);
            string frozen = JsonUtility.ToJson(s.CompletedRun);
            RunRecord same = GameSession.ReplayChoices(s.CompletedRun, new Dictionary<int, string>());
            Assert.AreEqual(1, same.mysteries.Count); Assert.AreEqual(s.Insight, same.finalInsight);
            Assert.AreEqual(s.Energy, same.finalEnergy); Assert.AreEqual(s.Mood, same.finalMood);
            CollectionAssert.AreEqual(s.Actions.Select(a => a.cardId), same.actions.Select(a => a.cardId));
            CardSpec other = GameSession.AlternativesForDay(s.CompletedRun, 1).First(c => c.Id != "portfolio");
            RunRecord branch = GameSession.ReplayAlternative(s.CompletedRun, 1, other.Id);
            Assert.AreEqual(0, branch.mysteries.Count);
            Assert.AreEqual(frozen, JsonUtility.ToJson(s.CompletedRun));
        }

        [Test]
        public void MysteryContributesToPredictionExplanationBeforeItsSourceIsRevealed()
        {
            var s = new GameSession(3, 15); Step(s, "portfolio");
            while (s.Day < 4) Step(s); s.LockPrediction(0, 0, 0);
            while (s.Day < 6) Step(s); s.ApplyMystery(1, "portfolio"); Step(s);
            List<CausalNode> causes = ObservationDesign.PredictionCauses(s);
            Assert.IsTrue(causes.Exists(n => n.type == CausalNodeKind.Mystery && n.originHidden));
            Assert.AreEqual(s.Prediction.actualEnergy, causes.Sum(n => n.effect.energy));
            Assert.AreEqual(s.Prediction.actualMood, causes.Sum(n => n.effect.mood));
            Assert.AreEqual(s.Prediction.actualInsight, causes.Sum(n => n.effect.insight));
        }

        [Test]
        public void PriorCatalogKeepsTheOriginalMemoryOnlyMystery()
        {
            RunSnapshot saved = new GameSession(3, 15).Snapshot(); saved.catalogVersion = 0; saved.rulesVersion = 5;
            GameSession old = GameSession.Restore(saved); Assert.AreEqual(3, old.CatalogVersion);
            Step(old, "portfolio"); while (old.Day < 6) Step(old);
            int insight = old.Insight;
            var moment = new RareMoment { type = 3, runNumber = 3, day = 6 };
            ExperienceContent.AttachMystery(moment, old);
            Assert.AreEqual(insight, old.Insight); Assert.AreEqual(0, old.Mysteries.Count);
            Assert.IsNotEmpty(moment.causeNodeId);
            Assert.IsNull(old.ApplyMystery());
        }

        [Test]
        public void FutureExamplesStartWithDifferentLegalChoicesWhenPossible()
        {
            var s = new GameSession(3, 15);
            ForecastRange range = ForecastSimulator.Sample(s, null, 4, 3);
            Assert.AreNotEqual(range.example[0].cardId, range.otherExample[0].cardId);
            Assert.IsTrue(s.CanPlay(CardCatalog.FindById(range.example[0].cardId)));
            Assert.IsTrue(s.CanPlay(CardCatalog.FindById(range.otherExample[0].cardId)));
            Assert.AreEqual(0, s.Actions.Count);
        }

        [Test]
        public void LifeTitlesDescribeRecordedBehaviorAndDoNotOverwriteEdits()
        {
            var s = new GameSession(1, 15); Complete(s);
            Assert.AreEqual("这一次，我给自己留出了空间", s.CompletedRun.title);
            s.CompletedRun.title = "我的纪念";
            var data = new ArchiveData(); data.runs.Add(s.CompletedRun);
            ArchiveData restored = JsonUtility.FromJson<ArchiveData>(JsonUtility.ToJson(data)); restored.Repair();
            Assert.AreEqual("我的纪念", restored.runs[0].title);
        }

        [Test]
        public void CalibratedDirectionIsVisibleEvenBeforeHorizonTwo()
        {
            var echo = new PendingEcho { kind = CardKind.Growth, depth = 1, delta = new ResourceDelta(ability: 2) };
            Assert.AreEqual("尚未看清的回声", ObservationDesign.FocusClue(echo, 1, 0));
            Assert.That(ObservationDesign.FocusClue(echo, 1, 1), Does.Contain("能力↑"));
            Assert.That(ObservationDesign.FocusClue(echo, 1, 3), Does.Contain("能力明显↑"));
        }

        [Test]
        public void ThirtyDayUnlockAndObservationBudgetCannotBeBypassedFromHome()
        {
            var s = new GameSession(3, 15); var data = new ArchiveData { active = s.Snapshot() };
            for (int day = 1; day <= 6; day++) data.journey.Visit("2026-09-" + day.ToString("00"));
            Assert.IsFalse(data.TryThirtyDayObservation(null, out GameSession blocked)); Assert.IsNull(blocked);
            data.journey.Visit("2026-09-07");
            Assert.IsTrue(data.TryThirtyDayObservation(null, out GameSession view)); Assert.AreEqual(1, data.active.focusUses);
            Assert.AreEqual(s.Energy, view.Energy); Assert.AreEqual(s.WorldSeed, view.WorldSeed);
            Assert.IsFalse(data.TryThirtyDayObservation(null, out blocked));
            GameSession resumed = GameSession.Restore(data.active); Assert.IsFalse(resumed.TryFocus());
            Step(resumed); data.active = resumed.Snapshot();
            Assert.IsTrue(data.TryThirtyDayObservation(resumed, out view));
            Assert.AreEqual(1, resumed.FocusUses); Assert.AreEqual(2, view.Day);
        }
    }
}
