using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class ObservationDesignTests
    {
        private static void Prepare(GameSession s)
        { if (s.HasPredictionReview) s.MarkPredictionReviewed(); if (s.CanPredict) s.SkipPrediction(); }
        private static void Complete(GameSession s)
        {
            while (true) { Prepare(s); s.Choose(s.Hand[2].Id);
                if (s.Day == 4) s.VisitStation(); if (s.Day == 12) break; s.Advance(); }
        }

        [Test]
        public void OldCatalogKeepsItsWeatherAndEntertainmentRules()
        {
            RunSnapshot saved = new GameSession(3, 41, 4).Snapshot(); saved.catalogVersion = 2; saved.rulesVersion = 4;
            GameSession old = GameSession.Restore(saved); Complete(old);
            Assert.IsFalse(old.CausalNodes.Exists(n => n.type == CausalNodeKind.World));
            Assert.AreEqual(-1, CardCatalog.ForDay(10, 3, 2)[0].Later.mood);
            Assert.AreEqual(1, CardCatalog.ForDay(10, 3, 3)[0].Later.mood);
            RunRecord replay = GameSession.ReplayChoices(old.CompletedRun, new Dictionary<int, string>());
            Assert.AreEqual(old.CompletedRun.finalRelation, replay.finalRelation);
            CollectionAssert.AreEqual(old.Actions.Select(a => a.cardId), replay.actions.Select(a => a.cardId));
        }

        [Test]
        public void WeatherActuallyVariesAndNeverRemovesPlayableRecovery()
        {
            foreach (WorldEventSpec spec in WorldEvents.All)
            {
                var outcomes = new HashSet<bool>();
                for (int seed = 0; seed < 24; seed++)
                {
                    var session = new GameSession(3, seed, 4);
                    while (session.Day < spec.Day) { Prepare(session); session.Choose(session.Hand[2].Id);
                        if (session.Day == 4) session.VisitStation(); session.Advance(); }
                    Prepare(session);
                    Assert.IsTrue(session.CanPlay(session.Hand[2]), "Recovery must remain usable after a world event.");
                    outcomes.Add(session.CausalNodes.Exists(n => n.type == CausalNodeKind.World && n.day == spec.Day));
                }
                Assert.AreEqual(2, outcomes.Count, spec.Name);
            }
        }

        [Test]
        public void SaveAndCounterfactualReplayKeepTheSameWorld()
        {
            var s = new GameSession(3, 41, 4);
            while (s.Day < 6) { Prepare(s); s.Choose(s.Hand[2].Id); if (s.Day == 4) s.VisitStation(); s.Advance(); }
            GameSession restored = GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(s.Snapshot())));
            Assert.AreEqual(41, restored.WorldSeed);
            Complete(s); Complete(restored);
            string frozen = JsonUtility.ToJson(s.CompletedRun);
            RunRecord replay = GameSession.ReplayChoices(s.CompletedRun, new Dictionary<int, string>());
            CollectionAssert.AreEqual(s.Actions.Select(a => a.cardId), restored.Actions.Select(a => a.cardId));
            CollectionAssert.AreEqual(s.CausalNodes.Where(n => n.type == CausalNodeKind.World).Select(n => n.day),
                replay.causalNodes.Where(n => n.type == CausalNodeKind.World).Select(n => n.day));
            Assert.AreEqual(s.CompletedRun.finalEnergy, replay.finalEnergy);
            Assert.AreEqual(frozen, JsonUtility.ToJson(s.CompletedRun));
        }

        [Test]
        public void HorizonTwoRevealsOneTypeAndChaptersOnlyChangeObservation()
        {
            var first = new GameSession(1, 15, 4); var second = new GameSession(2, 15, 4);
            string frozen = JsonUtility.ToJson(second.Snapshot());
            Assert.AreEqual(0, ObservationDesign.VisibleTypes(first, 1));
            Assert.AreEqual(1, ObservationDesign.VisibleTypes(second, 1));
            Assert.IsTrue(ObservationDesign.CanCompare(second, 6));
            Assert.IsFalse(ObservationDesign.CanCompare(second, 5));
            Assert.IsNotNull(second.ProjectFuture(second.Hand[1].Id, true));
            Assert.AreEqual(frozen, JsonUtility.ToJson(second.Snapshot()));
        }

        [Test]
        public void PredictionExplanationAddsUpToActualClampedResults()
        {
            var s = new GameSession(3, 41, 4);
            while (s.Day < 4) { s.Choose(s.Hand[2].Id); s.Advance(); }
            s.LockPrediction(0, 0, 0);
            while (s.Day < 7) { s.Choose(s.Hand[2].Id); if (s.Day == 4) s.VisitStation(); s.Advance(); }
            string frozen = JsonUtility.ToJson(s.Snapshot());
            List<CausalNode> causes = ObservationDesign.PredictionCauses(s);
            Assert.IsNotEmpty(causes);
            Assert.AreEqual(s.Prediction.actualEnergy, causes.Sum(n => n.effect.energy));
            Assert.AreEqual(s.Prediction.actualMood, causes.Sum(n => n.effect.mood));
            Assert.AreEqual(s.Prediction.actualInsight, causes.Sum(n => n.effect.insight));
            Assert.IsFalse(causes.Exists(n => !n.resolved || n.day < 4 || n.day > 7));
            Assert.AreEqual(frozen, JsonUtility.ToJson(s.Snapshot()));
        }

        [Test]
        public void ThirtyDayContinuationUsesNewContentAndConcreteDifferentPaths()
        {
            var source = new GameSession(3, 41, 4); Complete(source);
            string frozen = JsonUtility.ToJson(source.Snapshot());
            ForecastRange range = ForecastSimulator.Sample(source, null, 30, 6);
            Assert.AreEqual(30, range.example.Count); Assert.AreEqual(30, range.otherExample.Count);
            Assert.IsFalse(range.example.Select(a => a.cardId).SequenceEqual(range.otherExample.Select(a => a.cardId)));
            foreach (int day in new[] { 14, 21, 28 })
                foreach (ActionRecord a in new[] { range.example[day - 1], range.otherExample[day - 1] })
                    Assert.IsTrue(CardCatalog.ForOutlookDay(day).Any(c => c.Id == a.cardId));
            CollectionAssert.AreEqual(source.Actions.Select(a => a.cardId), range.example.Take(12).Select(a => a.cardId));
            Assert.AreEqual(frozen, JsonUtility.ToJson(source.Snapshot()));
        }

        [Test]
        public void UncertainForecastDoesNotRerollOrResolveTheRealLife()
        {
            var s = new GameSession(3, 41, 4); s.Choose("portfolio"); s.Advance();
            string frozen = JsonUtility.ToJson(s.Snapshot());
            ForecastRange a = ForecastSimulator.Sample(s, null, 12, 12);
            ForecastRange b = ForecastSimulator.Sample(s, null, 12, 12);
            Assert.That(a.assumption, Does.Contain("天气"));
            Assert.AreEqual(a.energyMin, b.energyMin); Assert.AreEqual(a.supportPass, b.supportPass);
            Assert.AreEqual(frozen, JsonUtility.ToJson(s.Snapshot())); Assert.IsFalse(s.Actions[0].echoed);
        }
    }
}
