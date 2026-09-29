using System;
using Horizon.Game;
using NUnit.Framework;

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
            Assert.IsFalse(session.CanPlay(session.Hand[1]));
            Assert.IsTrue(session.CanPlay(session.Hand[2]));
        }

        [Test]
        public void RunCompletesAfterTwelfthActionAndKeepsTwelveNodes()
        {
            var session = new GameSession(1);
            for (int day = 1; day <= 12; day++)
            {
                session.Choose(session.Hand[2].Id);
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
    }
}

