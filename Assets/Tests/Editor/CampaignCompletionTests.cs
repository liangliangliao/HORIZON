using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class CampaignCompletionTests
    {
        private static void Ready(GameSession s)
        { while (s.HasPredictionReview) s.MarkPredictionReviewed(); if (s.CanPredict) s.SkipPrediction(); }
        private static void Until(GameSession s, int day)
        {
            while (s.Day < day) {
                Ready(s); s.Choose(s.Hand[2].Id); if (s.NeedsStation) s.VisitStation(); s.Advance();
            }
        }

        [Test]
        public void OverlappingPredictionsReturnInDueOrderAndSurviveReload()
        {
            var s = new GameSession(3, 531); Until(s, 4);
            s.LockPrediction(new ResourceDelta(0, 0, 0, 0, 0, 0), 7); Until(s, 8);
            Assert.IsTrue(s.CanPredict, "An outstanding prediction must not erase the second decision.");
            s.LockPrediction(new ResourceDelta(0, 0, 0, 1), 1); Until(s, 9);
            s = GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(s.Snapshot())));
            Assert.IsTrue(s.HasPredictionReview); Assert.AreEqual(8, s.Prediction.sourceDay);
            var data = new ArchiveData(); s.Prediction.accurate = true;
            Assert.IsTrue(data.ReviewPrediction(s)); Assert.IsFalse(data.ReviewPrediction(s));
            Assert.AreEqual(1, data.calibrations);
            Until(s, 11); Assert.IsTrue(s.HasPredictionReview); Assert.AreEqual(4, s.Prediction.sourceDay);
            Assert.AreEqual(2, s.Predictions.Count); Assert.IsTrue(s.Predictions.Find(p => p.sourceDay == 8).reviewed);
            Assert.IsFalse(s.CanPlay(s.Hand[2]), "An unread result must be acknowledged first.");
        }

        [Test]
        public void SixAxesExplainEveryActualChangeIncludingCaps()
        {
            var s = new GameSession(3, 277); Until(s, 4); Assert.Throws<ArgumentException>(() => s.LockPrediction(new ResourceDelta(), 2));
            s.LockPrediction(new ResourceDelta(), 3); Until(s, 7);
            PredictionRecord p = s.Prediction; Assert.IsTrue(p.sixAxes);
            List<CausalNode> causes = ObservationDesign.PredictionCauses(s);
            Assert.AreEqual(p.actualEnergy, causes.Sum(n => n.effect.energy));
            Assert.AreEqual(p.actualMood, causes.Sum(n => n.effect.mood));
            Assert.AreEqual(p.actualInsight, causes.Sum(n => n.effect.insight));
            Assert.AreEqual(p.actualRelation, causes.Sum(n => n.effect.relation));
            Assert.AreEqual(p.actualMoney, causes.Sum(n => n.effect.money));
            Assert.AreEqual(p.actualAbility, causes.Sum(n => n.effect.ability));
        }

        [Test]
        public void OldCatalogStillHasOneThreeDayPredictionAndOneStation()
        {
            var s = new GameSession(3, 73, 5); Until(s, 4); s.LockPrediction(0, 0, 0); Until(s, 8);
            Assert.IsFalse(s.CanPredict); Assert.IsFalse(s.UsesSixPredictionAxes);
            Assert.AreEqual(1, s.Predictions.Count); CollectionAssert.AreEqual(new[] { 4 }, s.StationDays);
            Assert.AreEqual(7, s.Prediction.dueDay);
        }

        [Test]
        public void RealThirtyDayLifeResumesPastTwelveAndReplaysItsWholeHistory()
        {
            var s = GameSession.StartLongLife(4, 914); Until(s, 13);
            Assert.IsNull(s.CompletedRun); Assert.AreEqual(30, s.Deadline);
            s = GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(s.Snapshot())));
            Assert.AreEqual(13, s.Day); Assert.AreEqual(30, s.Deadline);
            Until(s, 24); s = GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(s.Snapshot())));
            Until(s, 30); Ready(s); s.Choose(s.Hand[2].Id);
            RunRecord run = s.CompletedRun; Assert.AreEqual(30, run.actions.Count);
            CollectionAssert.AreEqual(new[] { 4, 12, 14, 21, 28 }, run.stationDays);
            Assert.Greater(run.causalNodes.Count(n => n.day > 12 && n.resolved), 18);
            string original = JsonUtility.ToJson(run);
            RunRecord replay = GameSession.ReplayChoices(run, new Dictionary<int, string>());
            CollectionAssert.AreEqual(run.actions.Select(a => a.cardId), replay.actions.Select(a => a.cardId));
            Assert.AreEqual(run.finalEnergy, replay.finalEnergy); Assert.AreEqual(run.finalRelation, replay.finalRelation);
            Assert.AreEqual(run.finalMood, replay.finalMood); Assert.AreEqual(run.finalMoney, replay.finalMoney);
            Assert.AreEqual(run.finalInsight, replay.finalInsight); Assert.AreEqual(run.finalAbility, replay.finalAbility);
            Assert.AreEqual(run.boss.passed, replay.boss.passed);
            CardSpec alt = GameSession.AlternativesForDay(run, 19).First(c => c.Id != run.actions[18].cardId);
            Assert.IsNotNull(GameSession.ReplayAlternative(run, 19, alt.Id));
            Assert.AreEqual(original, JsonUtility.ToJson(run));
            var data = new ArchiveData(); data.runs.Add(run);
            Assert.IsTrue(ArchiveStore.TryDecode(ArchiveStore.Encode(data), out ArchiveData imported, out string error), error);
            Assert.AreEqual(30, imported.runs[0].deadline);
        }

        [Test]
        public void LongGatesRequireContinuedPreparationAfterDayTwelve()
        {
            var s = GameSession.StartLongLife(4, 184); Until(s, 13);
            Assert.AreEqual(0, ProductExperience.GrowthEvidence(s));
            Assert.AreEqual(0, ProductExperience.RecoveryEvidence(s));
            Assert.AreEqual(0, ProductExperience.SupportEvidence(s));
            Assert.AreEqual(3, ProductExperience.EvidenceNeeded(s));
            Assert.IsFalse(ProductExperience.GateReady(s, 1));
            Assert.That(PlayExperience.GateReason(s, 1), Does.Contain("/3"));
        }

        [Test]
        public void PositiveJoyCanOpenARealCreativeChoice()
        {
            RunSnapshot state = new GameSession(3, 87).Snapshot(); state.day = 10; state.mood = 7;
            var s = GameSession.Restore(state); s.Choose("play"); s.Advance();
            PendingEcho creative = s.Pending.Find(e => e.replacementId == "create");
            Assert.IsNotNull(creative); Assert.AreEqual(12, creative.dueDay);
            string source = creative.parentNodeId; Ready(s); s.Choose(s.Hand[2].Id); s.Advance();
            Assert.AreEqual("create", s.Hand[1].Id);
            CausalNode choice = s.CausalNodes.Find(n => n.id == creative.nodeId);
            Assert.IsTrue(choice.resolved); Assert.Contains(source, CausalGraph.Parents(choice));
        }

        [Test]
        public void RecoveryFinishesThirtyDaysForThirtyTwoSeedsWithoutInflation()
        {
            for (int seed = 0; seed < 32; seed++) {
                var s = GameSession.ForkForSimulation(GameSession.StartLongLife(4, seed).Snapshot(), 30);
                Until(s, 30); Ready(s); Assert.IsTrue(s.CanPlay(s.Hand[2])); s.Choose(s.Hand[2].Id);
                Assert.AreEqual(30, s.CompletedRun.actions.Count);
                Assert.That(new[] { s.Energy, s.Mood, s.Insight, s.Relation, s.Money, s.Ability }, Is.All.InRange(0, 10));
            }
        }

        [Test]
        public void SeveralLongRoutesCanPassThreeGatesAcrossEightSeeds()
        {
            var report = new List<string> { "seed,winning_plans,distinct_plans" };
            for (int seed = 0; seed < 8; seed++) {
                var beam = new List<GameSession> { GameSession.ForkForSimulation(GameSession.StartLongLife(4, seed).Snapshot(), 30) };
                for (int day = 1; day <= 30; day++) {
                    var next = new List<GameSession>();
                    foreach (GameSession life in beam) {
                        Ready(life);
                        foreach (CardSpec card in life.Hand.Where(life.CanPlay)) {
                            GameSession branch = GameSession.ForkForSimulation(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(life.Snapshot())), 30);
                            branch.Choose(card.Id); if (branch.NeedsStation) branch.VisitStation();
                            if (day < 30) branch.Advance(); next.Add(branch);
                        }
                    }
                    beam = next.OrderByDescending(Score).Take(24).ToList();
                }
                int distinct = beam.Where(s => s.CompletedRun.boss.passed == 3)
                    .Select(s => string.Join("/", s.Actions.Select(a => a.cardId))).Distinct().Count();
                Assert.GreaterOrEqual(distinct, 2, "Long life has too few routes at seed " + seed);
                report.Add(seed + "," + beam.Count(s => s.CompletedRun.boss.passed == 3) + "," + distinct);
            }
            Directory.CreateDirectory("artifacts"); File.WriteAllLines("artifacts/balance-v6.csv", report);
        }

        private static float Score(GameSession s)
        {
            if (s.CompletedRun != null) return s.CompletedRun.boss.passed * 10000 + s.Energy + s.Mood;
            int future = s.Ability + s.Pending.Where(e => e.depth == 1 && e.dueDay <= 30).Sum(e => e.delta.ability);
            int growth = ProductExperience.GrowthEvidence(s) + s.Pending.Count(e => e.depth == 1 && e.sourceDay > 12 && e.delta.ability > 0);
            return Math.Min(6, future) * 20 + Math.Min(3, growth) * 110 + Math.Min(3, ProductExperience.SupportEvidence(s)) * 100 +
                Math.Min(3, ProductExperience.RecoveryEvidence(s)) * 45 + Math.Min(6, s.Relation) * 12 +
                Math.Min(4, s.Energy) * 10 + Math.Min(4, s.Mood) * 10 + Math.Min(2, s.Money) * 10 + s.Energy * 0.1f;
        }
    }
}
