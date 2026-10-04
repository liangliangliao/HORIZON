using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class ExperienceContentTests
    {
        [Test]
        public void PreviousCatalogSurvivesSaveUpgradeAndCounterfactualReplay()
        {
            RunSnapshot old = new GameSession(2, 15, 4).Snapshot();
            old.rulesVersion = 3; old.catalogVersion = 0;
            GameSession session = GameSession.Restore(old);
            Assert.AreEqual(1, session.CatalogVersion);
            Complete(session);
            RunRecord original = session.CompletedRun;
            Assert.IsFalse(original.actions.Exists(a => a.cardId == "nightwalk"));
            RunRecord copy = GameSession.ReplayChoices(original, new Dictionary<int, string>());
            Assert.AreEqual(original.finalEnergy, copy.finalEnergy);
            Assert.AreEqual(original.finalAbility, copy.finalAbility);
            Assert.AreEqual(original.boss.passed, copy.boss.passed);
            CollectionAssert.AreEqual(original.actions.Select(a => a.cardId), copy.actions.Select(a => a.cardId));
            Assert.Throws<ArgumentException>(() => { old.catalogVersion = 99; GameSession.Restore(old); });
        }

        [Test]
        public void SecondLifeAddsThreeCardsAndFourSituationsWithoutBlockingRecovery()
        {
            Assert.AreEqual("mentor", CardCatalog.ForDay(8, 2)[1].Id);
            Assert.AreEqual("play", CardCatalog.ForDay(10, 2)[0].Id);
            Assert.AreEqual("nightwalk", CardCatalog.ForDay(11, 2)[2].Id);
            var session = new GameSession(2, 15, 4);
            Complete(session);
            Assert.AreEqual(4, session.CausalNodes.Count(n => n.type == CausalNodeKind.Situation));
            Assert.AreEqual("friend", session.Actions[11].cardId);
            Assert.IsNotEmpty(session.Actions[11].parentNodeId);
            foreach (int day in new[] { 7, 9, 10, 12 })
                Assert.IsTrue(GameSession.AlternativesForDay(session.CompletedRun, day).Any(c => c.Kind == CardKind.Recovery));
            var first = new GameSession(1, 15, 4);
            Complete(first);
            Assert.IsFalse(first.CausalNodes.Exists(n => n.type == CausalNodeKind.Situation));
        }

        [Test]
        public void ResourceReceiptsRecordActualClampedChangesAcrossSave()
        {
            RunSnapshot full = new GameSession(1, 15, 4).Snapshot(); full.energy = full.mood = 10;
            GameSession session = GameSession.Restore(full);
            session.Choose("rest");
            Assert.AreEqual(0, session.Actions[0].actualNow.energy);
            Assert.AreEqual(0, session.Actions[0].actualNow.mood);
            Assert.IsTrue(session.Actions[0].actualNowRecorded);
            GameSession restored = GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(session.Snapshot())));
            Assert.AreEqual(0, restored.Actions[0].actualNow.energy);
            restored.Advance(); restored.Choose("friend"); restored.Advance();
            restored.Choose("walk");
            DayTransition fourth = restored.Advance();
            PendingEcho response = fourth.Echos.Find(e => e.cardId == "friend");
            Assert.IsNotNull(response);
            Assert.AreEqual(0, response.actualDelta.mood);
            Assert.AreEqual(0, restored.Actions[1].actualLater.mood);
            Assert.IsTrue(restored.Actions[1].actualLaterRecorded);
        }

        [Test]
        public void ACollaborativeOpportunityMergesTwoActualHistoriesAndSurvivesSave()
        {
            var session = new GameSession(3, 15, 4);
            Complete(session, s => s.Day == 1 ? "portfolio" : s.Hand[2].Id);
            CausalNode collaboration = session.CausalNodes.Find(n => n.type == CausalNodeKind.Choice && n.replacementId == "together");
            Assert.IsNotNull(collaboration);
            Assert.GreaterOrEqual(CausalGraph.Parents(collaboration).Count, 2);
            List<CausalNode> ancestors = CausalGraph.Ancestors(session.CausalNodes, collaboration.id);
            Assert.IsTrue(ancestors.Exists(n => n.cardId == "portfolio"));
            Assert.IsTrue(ancestors.Exists(n => n.type == CausalNodeKind.Action &&
                CardCatalog.FindById(n.cardId)?.GivesSupport == true));
            RunRecord saved = JsonUtility.FromJson<RunRecord>(JsonUtility.ToJson(session.CompletedRun));
            Assert.AreEqual(ancestors.Count, CausalGraph.Ancestors(saved.causalNodes, collaboration.id).Count);
            // Malformed imported cycles must remain finite when inspected.
            CausalGraph.Link(saved.causalNodes.Find(n => n.id == ancestors[0].id), collaboration.id);
            Assert.LessOrEqual(CausalGraph.Ancestors(saved.causalNodes, collaboration.id).Count, saved.causalNodes.Count);
        }

        [Test]
        public void DeadlineRequiresHistoricalPreparationAndConnectsItsEvidence()
        {
            var session = new GameSession(1, 15, 4);
            Complete(session, s => s.Day == 1 ? "practice" : s.Day == 3 ? "portfolio" : s.Hand[2].Id);
            Assert.AreEqual(3, session.CausalNodes.Count(n => n.type == CausalNodeKind.Gate));
            CausalNode gate = session.CausalNodes.Find(n => n.type == CausalNodeKind.Gate && n.label.StartsWith("能力"));
            Assert.IsTrue(gate.gatePassed);
            Assert.GreaterOrEqual(CausalGraph.Parents(gate).Count, 2);
            Assert.IsTrue(CausalGraph.Ancestors(session.CausalNodes, gate.id).Exists(n => n.cardId == "practice"));

            RunSnapshot numericOnly = new GameSession(1, 15, 4).Snapshot();
            numericOnly.day = 12; numericOnly.ability = numericOnly.energy = numericOnly.mood = 10;
            GameSession unprepared = GameSession.ForkForSimulation(numericOnly, 12);
            unprepared.Choose(unprepared.Hand[2].Id);
            Assert.IsFalse(unprepared.CompletedRun.boss.ability);
            Assert.IsFalse(unprepared.CompletedRun.boss.state);
        }

        [Test]
        public void MultiChoiceBranchesUseRealAvailableActionsAndLeaveOriginalUntouched()
        {
            var session = new GameSession(1, 15, 4); Complete(session);
            RunRecord original = session.CompletedRun;
            string frozen = JsonUtility.ToJson(original);
            var changes = new Dictionary<int, string> { { 1, "practice" }, { 3, "portfolio" } };
            RunRecord other = GameSession.ReplayChoices(original, changes);
            Assert.IsTrue(other.boss.ability);
            Assert.AreEqual("practice", other.actions[0].cardId);
            Assert.AreEqual("portfolio", other.actions[2].cardId);
            Assert.AreEqual(frozen, JsonUtility.ToJson(original));
            changes[3] = "mentor";
            Assert.IsNull(GameSession.ReplayChoices(original, changes));
        }

        [Test]
        public void BranchCanSelectSituationCardRatherThanTheBaseCatalog()
        {
            var session = new GameSession(2, 15, 4); Complete(session);
            Assert.IsTrue(GameSession.AlternativesForDay(session.CompletedRun, 7).Any(c => c.Id == "shortstudy"));
            RunRecord changed = GameSession.ReplayAlternative(session.CompletedRun, 7, "shortstudy");
            Assert.IsNotNull(changed);
            Assert.AreEqual("shortstudy", changed.actions[6].cardId);
        }

        [Test]
        public void ForecastsReuseRulesAndNeverMutateLiveEchoesOrResources()
        {
            var session = new GameSession(3, 15, 4); session.Choose("portfolio"); session.Advance();
            string frozen = JsonUtility.ToJson(session.Snapshot());
            ForecastRange first = ForecastSimulator.Sample(session, null, 12, 12);
            ForecastRange again = ForecastSimulator.Sample(session, null, 12, 12);
            Assert.AreEqual(first.energyMin, again.energyMin); Assert.AreEqual(first.abilityPass, again.abilityPass);
            Assert.AreEqual(first.insightMin, again.insightMin); Assert.AreEqual(first.relationMax, again.relationMax);
            Assert.AreEqual(first.moneyMin, again.moneyMin);
            Assert.LessOrEqual(first.energyMin, first.energyMax); Assert.LessOrEqual(first.abilityMin, first.abilityMax);
            Assert.LessOrEqual(first.insightMin, first.insightMax); Assert.LessOrEqual(first.relationMin, first.relationMax);
            Assert.LessOrEqual(first.moneyMin, first.moneyMax);
            Assert.That(first.insightMin, Is.InRange(0, 10)); Assert.That(first.relationMax, Is.InRange(0, 10));
            Assert.That(first.moneyMax, Is.InRange(0, 10));
            Assert.That(first.supportPass, Is.InRange(0, 12));
            Assert.AreEqual(frozen, JsonUtility.ToJson(session.Snapshot()));
            Assert.IsFalse(session.Actions[0].echoed);
            Assert.Throws<ArgumentException>(() => ForecastSimulator.Sample(session, "not-in-hand", 12, 3));
        }

        [Test]
        public void ThirtyDayOutlookUsesAnExtendedRulesFork()
        {
            var source = new GameSession(4, 15, 4);
            ForecastRange outlook = ForecastSimulator.Sample(source, null, 30, 6);
            Assert.AreEqual(30, outlook.targetDay); Assert.AreEqual(6, outlook.samples);
            Assert.That(outlook.energyMin, Is.InRange(0, 10)); Assert.That(outlook.abilityMax, Is.InRange(0, 10));
            Assert.That(outlook.insightMin, Is.InRange(0, 10)); Assert.That(outlook.relationMax, Is.InRange(0, 10));
            Assert.That(outlook.moneyMin, Is.InRange(0, 10));
            Assert.AreEqual(1, source.Day); Assert.IsEmpty(source.Actions);
        }

        [Test]
        public void JourneyCountsDistinctVisitsAndKeepsProgressAfterGaps()
        {
            var journey = new JourneyProgress();
            Assert.IsTrue(journey.Visit("2026-09-01")); Assert.IsFalse(journey.Visit("2026-09-01"));
            Assert.IsTrue(journey.Visit("2026-09-30")); Assert.AreEqual(2, journey.Chapter);
            for (int day = 1; day <= 8; day++) journey.Visit("2026-10-" + day.ToString("00"));
            Assert.AreEqual(7, journey.Chapter);
            Assert.AreEqual(10, journey.activeDates.Count);
        }

        [Test]
        public void ArchiveSearchIncludesCardMemoriesAndFiltersCompletedOutcomes()
        {
            var runs = new List<RunRecord> {
                new RunRecord { number = 1, title = "一次安静的日子", boss = new BossResult { passed = 2 },
                    actions = new List<ActionRecord> { new ActionRecord { cardName = "刻意练习" } } },
                new RunRecord { number = 2, title = "接住了未来", boss = new BossResult { passed = 3 } }
            };
            Assert.AreEqual(1, ExperienceContent.Search(runs, "练习", 0)[0].number);
            Assert.AreEqual(2, ExperienceContent.Search(runs, "", 1)[0].number);
            Assert.AreEqual(1, ExperienceContent.Search(runs, "", 2)[0].number);
            Assert.IsEmpty(ExperienceContent.Search(runs, "不存在", 0));
        }

        [Test]
        public void RareMomentsVaryAndUseAnActualPriorLifeMemory()
        {
            var kinds = new HashSet<int>(); var gaps = new HashSet<int>();
            for (int run = 3; run < 60; run++)
            { kinds.Add(ExperienceContent.Moment(run, 6, null).type); gaps.Add(ExperienceContent.RareGap(run)); }
            Assert.AreEqual(4, kinds.Count); CollectionAssert.AreEquivalent(new[] { 3, 4, 5 }, gaps);
            var past = new RunRecord { number = 14, actions = new List<ActionRecord> {
                new ActionRecord { day = 3, cardName = "刻意练习" } } };
            Assert.That(ExperienceContent.Moment(8, 6, new List<RunRecord> { past }).memory, Does.Contain("RUN 014"));
        }

        [Test]
        public void GifCompressionProducesAnIndependentDecoderFixture()
        {
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "visuals");
            Directory.CreateDirectory(directory);
            using (var file = File.Create(Path.Combine(directory, "gif-codec-fixture.gif")))
            using (var gif = new TimelineGifWriter(file, 96, 96))
            {
                int[] shades = { 0, 8, 20, 38, 68, 110, 175, 255 }, blues = { 0, 20, 80, 200 };
                for (int frame = 0; frame < 3; frame++)
                {
                    var pixels = new Color32[96 * 96]; uint state = (uint)(frame + 1);
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        unchecked { state = state * 1664525u + 1013904223u; }
                        int index = (int)(state >> 24);
                        pixels[i] = new Color32((byte)shades[index >> 5], (byte)shades[(index >> 2) & 7], (byte)blues[index & 3], 255);
                    }
                    gif.Frame(pixels, 20);
                }
            }
            Assert.Greater(new FileInfo(Path.Combine(directory, "gif-codec-fixture.gif")).Length, 20000);
        }

        private static void Complete(GameSession session, Func<GameSession, string> choose = null)
        {
            while (true)
            {
                if (session.HasPredictionReview) session.MarkPredictionReviewed();
                if (session.CanPredict) session.SkipPrediction();
                string card = choose == null ? session.Hand[2].Id : choose(session);
                session.Choose(card);
                if (session.Day == 4) session.VisitStation();
                if (session.Day == 12) break;
                session.Advance();
            }
        }
    }
}
