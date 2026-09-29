using System;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class GameSessionTests
    {
        [Test]
        public void FirstDayHasExactlyThreeDistinctIntentions()
        {
            CardSpec[] hand = new GameSession(1).Hand;
            Assert.AreEqual(3, hand.Length);
            Assert.AreEqual(CardKind.Temptation, hand[0].Kind);
            Assert.AreEqual(CardKind.Growth, hand[1].Kind);
            Assert.AreEqual(CardKind.Recovery, hand[2].Kind);
        }

        [Test]
        public void GrowthEchoReturnsOnScheduledDayAndMarksItsSource()
        {
            var session = new GameSession(1);
            session.Choose("practice");
            Assert.AreEqual(2, session.Insight);
            Assert.AreEqual(4, session.Actions[0].echoDay);
            Assert.AreEqual(-2, session.Actions[0].now.energy);
            Assert.AreEqual(3, session.Actions[0].later.insight);
            session.Advance();
            session.Choose(session.Hand[2].Id);
            session.Advance();
            session.Choose(session.Hand[2].Id);
            DayTransition dayFour = session.Advance();
            Assert.AreEqual(4, dayFour.Day);
            Assert.AreEqual(1, dayFour.Echos[0].sourceDay);
            Assert.AreEqual(5, session.Insight);
            Assert.IsTrue(session.Actions[0].echoed);
        }

        [Test]
        public void ImmediatePleasureSchedulesAHiddenCost()
        {
            var session = new GameSession(1);
            session.Choose("scroll");
            Assert.AreEqual(4, session.Energy);
            Assert.AreEqual(9, session.Mood);
            session.Advance();
            session.Choose(session.Hand[2].Id);
            DayTransition dayThree = session.Advance();
            Assert.AreEqual("scroll", dayThree.Echos[0].cardId);
            Assert.AreEqual(4, session.Energy);
        }

        [Test]
        public void ARecoveryCardRemainsPlayableWhenEnergyIsEmpty()
        {
            var session = new GameSession(1);
            session.Choose("scroll");
            session.Advance();
            session.Choose("impulse");
            session.Advance();
            session.Choose("episode");
            session.Advance();
            session.LockPrediction(0, 0, 0);
            Assert.IsFalse(session.CanPlay(session.Hand[1]));
            Assert.IsTrue(session.CanPlay(session.Hand[2]));
        }

        [Test]
        public void RunCompletesAfterTwelfthActionAndKeepsTwelveNodes()
        {
            var session = new GameSession(1);
            for (int day = 1; day <= 12; day++)
            {
                if (session.HasPredictionReview) session.MarkPredictionReviewed();
                if (day == 4) session.LockPrediction(0, 0, 0);
                session.Choose(session.Hand[2].Id);
                if (day == 4) session.VisitStation();
                if (day < 12) session.Advance();
            }
            Assert.AreEqual(12, session.CompletedRun.actions.Count);
            Assert.IsFalse(session.CompletedRun.boss.ability);
            Assert.IsTrue(session.CompletedRun.boss.state);
            Assert.IsTrue(session.CompletedRun.boss.support);
            Assert.AreEqual(2, session.CompletedRun.boss.passed);
            Assert.Throws<InvalidOperationException>(() => session.Advance());
        }

        [Test]
        public void SnapshotRestoresResourcesAndOncePerDayFocus()
        {
            var session = new GameSession(2);
            Assert.IsTrue(session.TryFocus());
            session.Choose(session.Hand[1].Id);
            GameSession restored = GameSession.Restore(session.Snapshot());
            Assert.AreEqual(session.Energy, restored.Energy);
            Assert.AreEqual(session.Pending.Count, restored.Pending.Count);
            Assert.IsFalse(restored.TryFocus());
            restored.Advance();
            Assert.IsTrue(restored.TryFocus());
        }

        [Test]
        public void FourthDayPredictionIsSealedAndMeasuredAfterTheSeventhDayEchoes()
        {
            var session = new GameSession(1);
            for (int day = 1; day <= 3; day++)
            {
                session.Choose(session.Hand[2].Id);
                session.Advance();
            }
            Assert.AreEqual(4, session.Day);
            Assert.IsTrue(session.CanPredict);
            Assert.IsFalse(session.CanPlay(session.Hand[2]));
            session.LockPrediction(0, 1, 0);
            session.Choose(session.Hand[2].Id);
            Assert.Throws<InvalidOperationException>(() => session.Advance());
            session.VisitStation();
            GameSession restored = GameSession.Restore(session.Snapshot());
            Assert.IsTrue(restored.StationVisited);
            Assert.AreEqual(1, restored.Prediction.mood);
            restored.Advance();
            restored.Choose(restored.Hand[2].Id);
            restored.Advance();
            restored.Choose(restored.Hand[2].Id);
            DayTransition seventh = restored.Advance();
            Assert.AreEqual(7, seventh.Day);
            Assert.IsTrue(restored.Prediction.evaluated);
            Assert.IsTrue(restored.Prediction.accurate);
            Assert.AreEqual(1, restored.Prediction.actualMood);
            Assert.IsTrue(restored.HasPredictionReview);
            restored.MarkPredictionReviewed();
            Assert.IsFalse(restored.HasPredictionReview);
        }

        [Test]
        public void ASurprisingPredictionDoesNotChangeResourcesOrGrantPower()
        {
            var session = new GameSession(1);
            for (int day = 1; day <= 3; day++)
            {
                session.Choose(session.Hand[2].Id);
                session.Advance();
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => session.LockPrediction(4, 0, 0));
            int energy = session.Energy;
            session.LockPrediction(-3, -3, -3);
            Assert.AreEqual(energy, session.Energy);
            session.Choose(session.Hand[2].Id);
            session.VisitStation();
            for (int day = 5; day <= 7; day++)
            {
                session.Advance();
                if (day < 7) session.Choose(session.Hand[2].Id);
            }
            Assert.IsFalse(session.Prediction.accurate);
            Assert.IsTrue(session.HasPredictionReview);
        }

        [Test]
        public void ThirdRunEchoCanRemoveAnInvitationWithoutRemovingRecovery()
        {
            var session = new GameSession(3);
            session.Choose("episode");
            session.Advance();
            session.Choose("avoid");
            DayTransition firstEcho = session.Advance();
            Assert.AreEqual(3, firstEcho.Day);
            Assert.AreEqual(2, session.Energy);
            Assert.AreEqual(1, firstEcho.Echos[0].depth);
            Assert.AreEqual(6, session.Actions[0].secondaryDay);
            Assert.AreEqual(3, session.Pending.Find(e => e.depth == 2).parentDay);

            session.Choose(session.Hand[2].Id);
            session.Advance();
            session.LockPrediction(0, 0, 0);
            session.Choose(session.Hand[2].Id);
            session.VisitStation();
            session.Advance();
            session.Choose(session.Hand[2].Id);
            int moodBefore = session.Mood;
            DayTransition cascade = session.Advance();

            Assert.AreEqual(6, cascade.Day);
            Assert.AreEqual(2, cascade.Echos.Find(e => e.depth == 2).depth);
            Assert.AreEqual(moodBefore - 1, session.Mood);
            Assert.IsTrue(session.SocialUnavailableToday);
            Assert.IsTrue(session.Actions[0].secondaryResolved);
            Assert.AreEqual("solo", session.Hand[2].Id);
            Assert.IsFalse(session.Hand[2].GivesSupport);
            Assert.IsFalse(session.CanPlay(CardCatalog.ForDay(6, 3)[2]));
            Assert.IsTrue(session.CanPlay(session.Hand[2]));

            GameSession restored = GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(
                JsonUtility.ToJson(session.Snapshot())));
            Assert.IsTrue(restored.SocialUnavailableToday);
            Assert.AreEqual("solo", restored.Hand[2].Id);
            int supportBefore = restored.SupportActions;
            Assert.Throws<ArgumentException>(() => restored.Choose("friend"));
            restored.Choose("solo");
            Assert.AreEqual(supportBefore, restored.SupportActions);
            restored.Advance();
            Assert.IsFalse(restored.SocialUnavailableToday);
        }

        [Test]
        public void ALowEnergyEchoDoesNotLockFirstRunOrARecoveredThirdRun()
        {
            var first = new GameSession(1);
            first.Choose("scroll");
            first.Advance();
            first.Choose("impulse");
            first.Advance();
            Assert.AreEqual(1, first.Energy);
            Assert.IsFalse(first.Pending.Exists(e => e.depth >= 2));

            var recovered = new GameSession(3);
            recovered.Choose("episode");
            recovered.Advance();
            recovered.Choose(recovered.Hand[2].Id);
            recovered.Advance();
            Assert.Greater(recovered.Energy, 2);
            Assert.IsFalse(recovered.Pending.Exists(e => e.depth >= 2));
        }
    }
}
