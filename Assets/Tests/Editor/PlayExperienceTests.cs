using Horizon.Game;
using Horizon.UI;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class PlayExperienceTests
    {
        [Test]
        public void EmptyOptionalSaveObjectsDoNotBecomePhantomResults()
        {
            var archive = JsonUtility.FromJson<ArchiveData>(JsonUtility.ToJson(new ArchiveData()));
            archive.Repair();
            Assert.IsNull(archive.active);
            Assert.IsNull(archive.pendingFeedback);
            Assert.AreEqual(0, archive.wallet.stardust);
            var session = GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(
                JsonUtility.ToJson(new GameSession(1).Snapshot())));
            Assert.IsNull(session.Prediction);
        }

        [Test]
        public void RewardReceiptsSurviveSaveAndCannotBeCollectedTwice()
        {
            var archive = new ArchiveData();
            Assert.AreEqual(3, archive.wallet.Claim("run:1:echo:4", 3));
            archive.pendingFeedback = new FeedbackRecord
            {
                kind = FeedbackKind.Echoes, runNumber = 1, day = 4,
                title = "回声回来了", description = "D1 → D4", stardust = 3
            };
            ArchiveData restored = JsonUtility.FromJson<ArchiveData>(JsonUtility.ToJson(archive));
            Assert.AreEqual(FeedbackKind.Echoes, restored.pendingFeedback.kind);
            Assert.AreEqual("D1 → D4", restored.pendingFeedback.description);
            Assert.AreEqual(0, restored.wallet.Claim("run:1:echo:4", 3));
            Assert.AreEqual(3, restored.wallet.stardust);
            Assert.AreEqual(1, restored.wallet.claimed.Count);
        }

        [Test]
        public void SceneryPurchaseRequiresCurrencyAndOnlyChargesOnce()
        {
            var wallet = new RewardWallet();
            Assert.IsFalse(wallet.SelectTheme(1));
            Assert.AreEqual(0, wallet.theme);
            wallet.Claim("completed-run", 30);
            Assert.IsTrue(wallet.SelectTheme(1));
            Assert.AreEqual(5, wallet.stardust);
            Assert.IsTrue(wallet.SelectTheme(0));
            Assert.IsTrue(wallet.SelectTheme(1));
            Assert.AreEqual(5, wallet.stardust);
            Assert.IsFalse(wallet.SelectTheme(9));
        }

        [Test]
        public void SkippingPredictionIsSavedAndNeverBlocksDaySeven()
        {
            var session = new GameSession(1);
            for (int day = 1; day < 4; day++)
            {
                session.Choose(session.Hand[2].Id);
                session.Advance();
            }
            Assert.IsTrue(session.CanPredict);
            session.SkipPrediction();
            session = GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(session.Snapshot())));
            Assert.IsTrue(session.PredictionSkipped);
            Assert.IsFalse(session.CanPredict);
            for (int day = 4; day < 7; day++)
            {
                session.Choose(session.Hand[2].Id);
                if (day == 4) session.VisitStation();
                session.Advance();
            }
            Assert.IsFalse(session.HasPredictionReview);
            Assert.IsTrue(session.CanPlay(session.Hand[2]));
        }

        [Test]
        public void DropAcceptanceMatchesTheVisibleEllipseIncludingItsEdges()
        {
            var go = new GameObject("Drop ring", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)go.transform;
                rect.position = new Vector3(400, 600, 0);
                rect.sizeDelta = new Vector2(300, 120);
                Assert.IsTrue(DropTarget.Contains(rect, new Vector2(400, 600)));
                Assert.IsTrue(DropTarget.Contains(rect, new Vector2(550, 600)));
                Assert.IsFalse(DropTarget.Contains(rect, new Vector2(551, 600)));
                Assert.IsFalse(DropTarget.Contains(rect, new Vector2(540, 650)));
                go.SetActive(false);
                Assert.IsFalse(DropTarget.Contains(rect, new Vector2(400, 600)));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void CardDatesStayFixedAndExplainEchoesBeyondTheDeadline()
        {
            CardSpec practice = CardCatalog.FindById("practice");
            Assert.That(PlayExperience.FutureLabel(practice, 1), Does.Contain("第 4 天"));
            Assert.That(PlayExperience.FutureLabel(practice, 11), Does.Contain("超过本局截止日"));
            Assert.That(PlayExperience.NowLabel(practice.Now), Does.Contain("精力 -2"));
            Assert.That(PlayExperience.NowLabel(new ResourceDelta()), Does.Contain("保持不变"));
            CardSpec ask = CardCatalog.FindById("ask");
            Assert.AreEqual(0, ask.Later.ability);
            Assert.That(PlayExperience.FutureMeaning(ask), Does.Not.Contain("能力"));
        }
    }
}
