using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class ProductReadinessTests
    {
        private string folder;
        [SetUp] public void Setup()
        { folder = Path.Combine(Path.GetTempPath(), "HORIZON-store-" + Guid.NewGuid()); Directory.CreateDirectory(folder); }
        [TearDown] public void Cleanup() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

        [Test]
        public void BackupRecoversTruncatedPrimaryAndPreservesUnreadableEvidence()
        {
            var store = new ArchiveStore(Path.Combine(folder, "life.json"));
            var data = new ArchiveData { active = new GameSession(2, 891).Snapshot() };
            data.wallet.Claim("first", 3); Assert.IsTrue(store.Save(data));
            data.wallet.Claim("second", 5); Assert.IsTrue(store.Save(data));
            File.WriteAllText(store.Path, "{\"format\":\"HORIZON-LIFE\"");
            ArchiveData restored = store.Load();
            Assert.AreEqual(3, restored.wallet.stardust); Assert.AreEqual(891, restored.active.worldSeed);
            Assert.That(store.Notice, Does.Contain("恢复"));
            Assert.IsTrue(store.Save(restored));
            Assert.AreEqual(1, Directory.GetFiles(folder, "life.json.unreadable.*").Length);
            Assert.AreEqual(3, new ArchiveStore(store.Path).Load().wallet.stardust);
            Assert.AreEqual(3, new ArchiveStore(store.BackupPath).Load().wallet.stardust);
        }

        [Test]
        public void ExportImportKeepsReadingPositionClaimsSeedAndPreferences()
        {
            var life = new GameSession(2, 736); life.Choose(life.Hand[1].Id);
            var data = new ArchiveData { active = life.Snapshot(), pendingFeedback = new FeedbackRecord {
                kind = FeedbackKind.Choice, runNumber = 2, day = 1, page = 1, presented = true, presentedPages = 3 } };
            data.preferences.reducedMotion = true; data.preferences.sound = false;
            data.wallet.Claim("run:2:action:1", 1);
            string export = ArchiveStore.Encode(data);
            var store = new ArchiveStore(Path.Combine(folder, "import.json"));
            Assert.IsTrue(store.Save(new ArchiveData()));
            ArchiveData imported; string error;
            Assert.IsTrue(store.Import(export, out imported, out error));
            Assert.AreEqual(736, imported.active.worldSeed); Assert.AreEqual(5, imported.active.catalogVersion);
            Assert.IsTrue(imported.preferences.reducedMotion); Assert.IsFalse(imported.preferences.sound);
            Assert.AreEqual(1, imported.pendingFeedback.page); Assert.AreEqual(3, imported.pendingFeedback.presentedPages);
            Assert.AreEqual(0, imported.wallet.Claim("run:2:action:1", 1));
            Assert.IsTrue(File.Exists(store.Path + ".before-import"));
            Assert.AreEqual(0, new ArchiveStore(store.Path + ".before-import").Load().wallet.stardust);
        }

        [Test]
        public void TamperedAndNewerBackupsNeverOverwriteTheCurrentLife()
        {
            var store = new ArchiveStore(Path.Combine(folder, "life.json"));
            var data = new ArchiveData(); data.wallet.Claim("a", 7); store.Save(data);
            string original = File.ReadAllText(store.Path);
            ArchiveEnvelope envelope = JsonUtility.FromJson<ArchiveEnvelope>(original);
            envelope.payload = envelope.payload.Replace("\"stardust\":7", "\"stardust\":999");
            ArchiveData imported; string error;
            Assert.IsFalse(store.Import(JsonUtility.ToJson(envelope), out imported, out error));
            Assert.AreEqual(original, File.ReadAllText(store.Path));
            envelope.version = 99;
            File.WriteAllText(store.Path, JsonUtility.ToJson(envelope));
            var incompatible = new ArchiveStore(store.Path); incompatible.Load();
            Assert.IsTrue(incompatible.WriteBlocked); Assert.IsFalse(incompatible.Save(new ArchiveData()));
            Assert.That(File.ReadAllText(store.Path), Does.Contain("99"));
        }

        [Test]
        public void LegacyArchiveMigratesWithoutChangingItsCatalogOrTimeline()
        {
            var life = new GameSession(3, 41, 4); life.Choose("portfolio");
            var data = new ArchiveData { active = life.Snapshot() };
            data.active.rulesVersion = 6;
            ArchiveData restored; string error;
            Assert.IsTrue(ArchiveStore.TryDecode(JsonUtility.ToJson(data), out restored, out error));
            var old = GameSession.Restore(restored.active); old.Advance();
            Assert.AreEqual(4, old.CatalogVersion);
            Assert.AreEqual(CardCatalog.ForDay(2, 3, 4)[1].Id, old.Hand[1].Id);
            Assert.AreEqual("portfolio", old.Actions[0].cardId);
        }

        [Test]
        public void NewHandsVaryAcrossLivesAndAreIdenticalAfterReloadOrObservation()
        {
            var patterns = new HashSet<string>();
            for (int seed = 0; seed < 64; seed++)
            {
                var s = new GameSession(2, seed);
                string pattern = string.Join("/", Enumerable.Range(1, 12).Select(day =>
                    string.Join(",", CardCatalog.ForDay(day, 2, 5, seed).Select(c => c.Id))));
                patterns.Add(pattern);
                GameSession restored = GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(s.Snapshot())));
                CollectionAssert.AreEqual(s.Hand.Select(c => c.Id), restored.Hand.Select(c => c.Id));
                s.TryFocus();
                CollectionAssert.AreEqual(s.Hand.Select(c => c.Id), restored.Hand.Select(c => c.Id));
                Assert.AreEqual(5, s.CatalogVersion);
            }
            Assert.Greater(patterns.Count, 55);
            CollectionAssert.AreEqual(new[] { "scroll", "practice", "rest" }, new GameSession(1, 992).Hand.Select(c => c.Id));
        }

        [Test]
        public void RangeForecastKeepsTheCurrentDeckWhileSamplingUncertainWorldEvents()
        {
            var life = new GameSession(3, 773);
            string frozen = JsonUtility.ToJson(life.Snapshot());
            string selected = life.Hand[1].Id;
            ForecastRange range = ForecastSimulator.Sample(life, selected, 4, 9);
            Assert.AreEqual(selected, range.example[0].cardId);
            Assert.AreEqual(frozen, JsonUtility.ToJson(life.Snapshot()));
            RunSnapshot future = life.Snapshot(); future.worldSeed += 104729;
            var fork = GameSession.ForkForSimulation(future, 12);
            CollectionAssert.AreEqual(life.Hand.Select(c => c.Id), fork.Hand.Select(c => c.Id));
            Assert.AreEqual(life.DeckSeed, fork.DeckSeed);
        }

        [Test]
        public void EveryNewSeedCanFinishWithARecoveryAndNoResourceInflation()
        {
            for (int seed = 0; seed < 96; seed++)
            {
                GameSession s = GameSession.ForkForSimulation(new GameSession(3, seed).Snapshot(), 12);
                while (true)
                {
                    Prepare(s);
                    Assert.IsTrue(s.CanPlay(s.Hand[2]), "Recovery blocked at seed " + seed + " D" + s.Day);
                    s.Choose(s.Hand[2].Id);
                    Assert.That(new[] { s.Energy, s.Mood, s.Insight, s.Relation, s.Money, s.Ability },
                        Is.All.InRange(0, 10));
                    if (s.Day == 12) break;
                    if (s.Day == 4) s.VisitStation(); s.Advance();
                }
                Assert.AreEqual(12, s.CompletedRun.actions.Count);
                Assert.AreEqual(5, s.CompletedRun.catalogVersion);
                if (seed < 3)
                {
                    RunRecord replay = GameSession.ReplayChoices(s.CompletedRun, new Dictionary<int, string>());
                    CollectionAssert.AreEqual(s.Actions.Select(a => a.cardId), replay.actions.Select(a => a.cardId));
                    Assert.AreEqual(s.Energy, replay.finalEnergy); Assert.AreEqual(s.Ability, replay.finalAbility);
                    Assert.AreEqual(s.Mood, replay.finalMood); Assert.AreEqual(s.Relation, replay.finalRelation);
                }
            }
        }

        [Test]
        public void SeveralDifferentPlansCanOpenAllGatesAcrossTheNewDecks()
        {
            var report = new List<string> { "seed,winning_prefixes,distinct_winning_plans" };
            // Search actual rules without the production failure-analysis pass.
            // The beam keeps diverse prefixes and values arriving growth, support
            // and recovery. This checks reachable outcomes, not a fixed recipe.
            for (int seed = 0; seed < 12; seed++)
            {
                var beam = new List<GameSession> { GameSession.ForkForSimulation(new GameSession(3, seed).Snapshot(), 12) };
                for (int day = 1; day <= 12; day++)
                {
                    var next = new List<GameSession>();
                    foreach (GameSession life in beam)
                    {
                        Prepare(life);
                        foreach (CardSpec card in life.Hand.Where(life.CanPlay))
                        {
                            GameSession branch = GameSession.ForkForSimulation(
                                JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(life.Snapshot())), 12);
                            branch.Choose(card.Id);
                            if (day == 4) branch.VisitStation();
                            if (day < 12) branch.Advance();
                            next.Add(branch);
                        }
                    }
                    beam = next.OrderByDescending(PlanScore).Take(18).ToList();
                }
                Assert.GreaterOrEqual(beam.Count(s => s.CompletedRun.boss.passed == 3), 2, "Not enough viable plans for seed " + seed);
                int winning = beam.Count(s => s.CompletedRun.boss.passed == 3);
                int distinct = beam.Where(s => s.CompletedRun.boss.passed == 3).Select(s =>
                    string.Join("/", s.Actions.Select(a => a.cardId))).Distinct().Count();
                Assert.GreaterOrEqual(distinct, 2);
                report.Add(seed + "," + winning + "," + distinct);
            }
            Directory.CreateDirectory("artifacts"); File.WriteAllLines("artifacts/balance-v5.csv", report);
        }

        private static float PlanScore(GameSession s)
        {
            if (s.CompletedRun != null) return s.CompletedRun.boss.passed * 10000 + s.Energy + s.Mood;
            int futureAbility = s.Ability + s.Pending.Where(e => e.dueDay <= 12).Sum(e => e.delta?.ability ?? 0);
            int futureGrowth = s.Actions.Count(a => a.echoed && a.later?.ability > 0) +
                s.Pending.Count(e => e.depth == 1 && e.delta?.ability > 0);
            return Math.Min(6, futureAbility) * 20 + Math.Min(2, futureGrowth) * 80 +
                Math.Min(2, s.SupportActions) * 80 + Math.Min(6, s.Relation) * 8 +
                Math.Min(4, s.Energy) * 5 + Math.Min(4, s.Mood) * 5 +
                Math.Min(2, s.Actions.Count(a => a.kind == CardKind.Recovery)) * 30 + s.Energy * 0.1f;
        }

        private static void Prepare(GameSession s)
        { if (s.CanPredict) s.SkipPrediction(); if (s.HasPredictionReview) s.MarkPredictionReviewed(); }
    }
}
