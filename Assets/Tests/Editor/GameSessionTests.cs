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
            Assert.IsNotNull(session.CompletedRun.boss.ghostTimeline);
            Assert.IsFalse(session.CompletedRun.boss.ghostTimeline.gateOpens);
            Assert.That(session.CompletedRun.boss.ghost, Does.Contain("仍未通过"));
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
            Assert.AreEqual(session.Relation, restored.Relation);
            Assert.AreEqual(session.Money, restored.Money);
            Assert.AreEqual(session.Ability, restored.Ability);
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

        [Test]
        public void HorizonThreeUnlocksOnlyAfterTheThirdFutureStation()
        {
            var session = new GameSession(3);
            Assert.AreEqual(2, session.HorizonLevel);
            Assert.Throws<InvalidOperationException>(() => session.ProjectFuture(session.Hand[0].Id));
            for (int day = 1; day <= 3; day++)
            {
                session.Choose(session.Hand[2].Id);
                session.Advance();
            }
            session.LockPrediction(0, 0, 0);
            session.Choose(session.Hand[2].Id);
            Assert.AreEqual(2, session.HorizonLevel);
            session.VisitStation();
            Assert.AreEqual(3, session.HorizonLevel);
            GameSession restored = GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(
                JsonUtility.ToJson(session.Snapshot())));
            Assert.AreEqual(3, restored.HorizonLevel);
            restored.Advance();
            Assert.AreEqual(3, restored.HorizonLevel);
            Assert.AreEqual(3, new GameSession(4).HorizonLevel);
        }

        [Test]
        public void TwoFutureProjectionsAreConditionalAndDoNotPlayCards()
        {
            var session = new GameSession(3);
            for (int day = 1; day <= 3; day++)
            {
                session.Choose(session.Hand[2].Id);
                session.Advance();
            }
            session.LockPrediction(0, 0, 0);
            session.Choose(session.Hand[2].Id);
            session.VisitStation();
            session.Advance();

            int energy = session.Energy;
            int mood = session.Mood;
            int insight = session.Insight;
            int relation = session.Relation;
            int money = session.Money;
            int ability = session.Ability;
            int pending = session.Pending.Count;
            FutureProjection temptation = session.ProjectFuture(session.Hand[0].Id);
            FutureProjection growth = session.ProjectFuture(session.Hand[1].Id);
            Assert.IsTrue(temptation.Available);
            Assert.IsTrue(growth.Available);
            Assert.AreEqual(8, temptation.TargetDay);
            Assert.AreEqual(7, temptation.EchoDay);
            Assert.AreEqual(8, growth.EchoDay);
            Assert.Less(temptation.Energy, energy);
            Assert.Greater(growth.Insight, insight);
            Assert.Greater(growth.Ability, ability);
            Assert.AreEqual(energy, session.Energy);
            Assert.AreEqual(mood, session.Mood);
            Assert.AreEqual(insight, session.Insight);
            Assert.AreEqual(relation, session.Relation);
            Assert.AreEqual(money, session.Money);
            Assert.AreEqual(ability, session.Ability);
            Assert.AreEqual(pending, session.Pending.Count);
            Assert.IsFalse(session.HasChosen);
            Assert.Throws<ArgumentException>(() => session.ProjectFuture("not-in-hand"));

            RunSnapshot low = session.Snapshot();
            low.energy = 0;
            GameSession exhausted = GameSession.Restore(low);
            Assert.IsFalse(exhausted.ProjectFuture(exhausted.Hand[0].Id).Available);
            Assert.IsTrue(exhausted.ProjectFuture(exhausted.Hand[2].Id).Available);
        }

        [Test]
        public void BossLightsActionEvidenceAndReplayedGhostReallyOpensAGate()
        {
            var session = new GameSession(1);
            for (int day = 1; day <= 12; day++)
            {
                if (session.HasPredictionReview) session.MarkPredictionReviewed();
                if (day == 4) session.LockPrediction(0, 0, 0);
                session.Choose(day == 1 ? "practice" : session.Hand[2].Id);
                if (day == 4) session.VisitStation();
                if (day < 12) session.Advance();
            }
            RunRecord original = session.CompletedRun;
            Assert.IsFalse(original.boss.ability);
            Assert.IsTrue(original.boss.state);
            Assert.IsTrue(original.boss.support);
            CollectionAssert.Contains(original.boss.abilityDays, 1);
            CollectionAssert.Contains(original.boss.stateDays, 1);
            Assert.GreaterOrEqual(original.boss.supportDays.Count, 2);

            GhostTimeline ghost = original.boss.ghostTimeline;
            Assert.IsNotNull(ghost);
            Assert.IsTrue(ghost.gateOpens);
            Assert.AreEqual("能力", ghost.gateName);
            RunRecord replayed = GameSession.ReplayAlternative(original, ghost.sourceDay, ghost.alternativeId);
            Assert.IsNotNull(replayed);
            Assert.IsTrue(replayed.boss.ability);
            Assert.AreEqual(ghost.afterPassed, replayed.boss.passed);
            Assert.AreEqual(ghost.finalEnergy, replayed.finalEnergy);
            Assert.AreEqual(ghost.finalInsight, replayed.finalInsight);
            Assert.AreEqual(ghost.finalRelation, replayed.finalRelation);
            Assert.AreEqual(ghost.finalMoney, replayed.finalMoney);
            Assert.AreEqual(ghost.finalAbility, replayed.finalAbility);
            for (int day = 0; day < ghost.sourceDay - 1; day++)
                Assert.AreEqual(original.actions[day].cardId, replayed.actions[day].cardId);
            Assert.AreEqual(original.boss.passed, ghost.beforePassed);
            Assert.AreEqual(12, original.actions.Count);

            RunRecord saved = JsonUtility.FromJson<RunRecord>(JsonUtility.ToJson(original));
            Assert.AreEqual(ghost.alternativeId, saved.boss.ghostTimeline.alternativeId);
            CollectionAssert.Contains(saved.boss.abilityDays, 1);
            Assert.IsNull(GameSession.ReplayAlternative(original, 1, "not-in-hand"));
        }

        [Test]
        public void SixResourcesPayImmediateCostsAndDelayedAbilityAndOpportunity()
        {
            var session = new GameSession(1);
            Assert.AreEqual(4, session.Relation);
            Assert.AreEqual(5, session.Money);
            Assert.AreEqual(2, session.Ability);
            session.Choose("practice");
            session.Advance();
            session.Choose("friend");
            Assert.AreEqual(6, session.Relation);
            session.Advance();
            session.Choose("portfolio");
            Assert.AreEqual(5, session.Money);
            Assert.AreEqual(2, session.Ability);
            DayTransition dayFour = session.Advance();
            Assert.AreEqual(2, dayFour.Echos.Count);
            Assert.AreEqual(4, session.Ability);
            Assert.AreEqual(7, session.Relation);
            session.LockPrediction(0, 0, 0);
            session.Choose(session.Hand[2].Id);
            session.VisitStation();
            session.Advance();
            session.Choose(session.Hand[2].Id);
            DayTransition daySix = session.Advance();
            Assert.AreEqual("portfolio", daySix.Echos[0].cardId);
            Assert.AreEqual(6, session.Ability);
            Assert.AreEqual(7, session.Money);
        }

        [Test]
        public void MoneyBlocksSpendingButNeverBlocksRecovery()
        {
            RunSnapshot low = new GameSession(2).Snapshot();
            low.money = 1;
            var session = GameSession.Restore(low);
            Assert.AreEqual("impulse", session.Hand[0].Id);
            Assert.IsFalse(session.CanPlay(session.Hand[0]));
            Assert.IsTrue(session.CanPlay(session.Hand[2]));
            Assert.Throws<InvalidOperationException>(() => session.Choose("impulse"));
            session.Choose(session.Hand[2].Id);
            Assert.AreEqual(1, session.Money);
        }

        [Test]
        public void BossAbilityAndSupportReadSixResourcesAndRecordedActions()
        {
            var session = new GameSession(1);
            for (int day = 1; day <= GameSession.LastDay; day++)
            {
                if (session.HasPredictionReview) session.MarkPredictionReviewed();
                if (day == 4) session.LockPrediction(0, 0, 0);
                session.Choose(day == 1 ? "practice" : day == 3 ? "portfolio" : session.Hand[2].Id);
                if (day == 4) session.VisitStation();
                if (day < GameSession.LastDay) session.Advance();
            }
            Assert.AreEqual(6, session.Ability);
            Assert.IsTrue(session.CompletedRun.boss.ability);
            Assert.IsTrue(session.CompletedRun.boss.support);
            CollectionAssert.Contains(session.CompletedRun.boss.abilityDays, 3);
            CollectionAssert.Contains(session.CompletedRun.boss.supportDays, 2);

            var deprived = new GameSession(1);
            for (int day = 1; day < GameSession.LastDay; day++)
            {
                if (deprived.HasPredictionReview) deprived.MarkPredictionReviewed();
                if (day == 4) deprived.LockPrediction(0, 0, 0);
                deprived.Choose(deprived.Hand[2].Id);
                if (day == 4) deprived.VisitStation();
                deprived.Advance();
            }
            RunSnapshot low = deprived.Snapshot();
            Assert.GreaterOrEqual(low.supportActions, 2);
            low.money = 1;
            low.relation = 2;
            deprived = GameSession.Restore(low);
            deprived.Choose(deprived.Hand[2].Id);
            Assert.IsFalse(deprived.CompletedRun.boss.support);
        }

        [Test]
        public void OldThreeResourceSnapshotRebuildsPaidAndPendingEchoes()
        {
            var session = new GameSession(1);
            session.Choose("practice");
            session.Advance();
            session.Choose("friend");
            session.Advance();
            session.Choose("portfolio");
            session.Advance();
            RunSnapshot old = JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(session.Snapshot()));
            old.rulesVersion = 0;
            old.relation = old.money = old.ability = 0;
            foreach (ActionRecord action in old.actions)
            {
                action.now = new ResourceDelta(action.now.energy, action.now.mood, action.now.insight);
                action.later = new ResourceDelta(action.later.energy, action.later.mood, action.later.insight);
            }
            foreach (PendingEcho echo in old.pending)
                echo.delta = new ResourceDelta(echo.delta.energy, echo.delta.mood, echo.delta.insight);

            GameSession restored = GameSession.Restore(old);
            Assert.AreEqual(GameSession.RulesVersion, restored.Snapshot().rulesVersion);
            Assert.AreEqual(session.Energy, restored.Energy);
            Assert.AreEqual(7, restored.Relation);
            Assert.AreEqual(5, restored.Money);
            Assert.AreEqual(4, restored.Ability);
            PendingEcho opportunity = restored.Pending.Find(e => e.cardId == "portfolio");
            Assert.IsNotNull(opportunity);
            Assert.AreEqual(2, opportunity.delta.money);
            Assert.AreEqual(2, opportunity.delta.ability);
            Assert.AreEqual(2, restored.Actions[0].later.ability);
        }

        [Test]
        public void GrowthEchoChangesAChoiceAndThatChoiceBuildsALongerSavedChain()
        {
            var session = new GameSession(3);
            session.Choose("portfolio");
            session.Advance();
            session.Choose("review");
            session.Advance();
            session.Choose(session.Hand[2].Id);
            DayTransition fourth = session.Advance();
            Assert.AreEqual(2, fourth.Echos.Count);
            Assert.AreEqual(8, session.Insight);
            Assert.AreEqual(1, session.Pending.FindAll(e => e.depth == 2 && e.dueDay == 5 &&
                e.replacementSlot == CardKind.Growth).Count);

            session.LockPrediction(0, 0, 0);
            session.Choose(session.Hand[2].Id);
            session.VisitStation();
            DayTransition fifth = session.Advance();
            PendingEcho changedHand = fifth.Echos.Find(e => e.replacementId == "opportunity");
            Assert.IsNotNull(changedHand);
            Assert.AreEqual(3, session.CausalPath(changedHand.nodeId).Count);
            Assert.AreEqual("opportunity", session.Hand[1].Id);
            Assert.AreEqual(3, session.Hand.Length);
            Assert.IsTrue(session.CanPlay(session.Hand[2]));

            session.Choose("opportunity");
            Assert.AreEqual(changedHand.nodeId, session.Actions[4].parentNodeId);
            session.Advance();
            session.Choose(session.Hand[2].Id);
            session.Advance();
            session.MarkPredictionReviewed();
            PendingEcho opportunityEcho = session.Pending.Find(e => e.cardId == "opportunity" && e.depth == 1);
            // The echo is due now, so it has moved out of Pending but remains in the graph.
            Assert.IsNull(opportunityEcho);
            CausalNode echoNode = session.CausalNodes.Find(n => n.cardId == "opportunity" &&
                n.type == CausalNodeKind.Echo);
            Assert.IsNotNull(echoNode);
            Assert.AreEqual(5, session.CausalPath(echoNode.id).Count);
            Assert.IsTrue(echoNode.resolved);

            session.Choose(session.Hand[2].Id);
            DayTransition eighth = session.Advance();
            PendingEcho nextChoice = eighth.Echos.Find(e => e.replacementId == "opportunity");
            Assert.IsNotNull(nextChoice);
            Assert.AreEqual(6, session.CausalPath(nextChoice.nodeId).Count);
            session.Choose("opportunity");
            Assert.AreEqual(7, session.CausalPath(session.Actions[7].nodeId).Count);

            RunSnapshot saved = JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(session.Snapshot()));
            GameSession restored = GameSession.Restore(saved);
            Assert.AreEqual(7, restored.CausalPath(restored.Actions[7].nodeId).Count);
            Assert.AreEqual(session.Pending.Count, restored.Pending.Count);
            restored.Advance();
            restored.Choose(restored.Hand[2].Id);
            restored.Advance();
            restored.Choose(restored.Hand[2].Id);
            DayTransition eleventh = restored.Advance();
            PendingEcho longChain = eleventh.Echos.Find(e => e.replacementId == "opportunity");
            Assert.IsNotNull(longChain);
            Assert.AreEqual(9, restored.CausalPath(longChain.nodeId).Count);
        }

        [Test]
        public void SupportResponseOpensARecoverySafeCollaborativeChoice()
        {
            var session = new GameSession(3);
            session.Choose(session.Hand[2].Id);
            session.Advance();
            session.Choose(session.Hand[2].Id);
            session.Advance();
            session.Choose(session.Hand[2].Id);
            session.Advance();
            session.LockPrediction(0, 0, 0);
            session.Choose(session.Hand[2].Id);
            session.VisitStation();
            session.Advance();
            session.Choose(session.Hand[2].Id);
            session.Advance();
            Assert.AreEqual("friend", session.Hand[2].Id);
            session.Choose("friend");
            session.Advance();
            session.MarkPredictionReviewed();
            session.Choose(session.Hand[2].Id);
            DayTransition eighth = session.Advance();
            Assert.GreaterOrEqual(session.Relation, 7);
            Assert.IsNotNull(eighth.Echos.Find(e => e.cardId == "friend"));
            session.Choose(session.Hand[2].Id);
            DayTransition ninth = session.Advance();
            Assert.IsNotNull(ninth.Echos.Find(e => e.replacementId == "together"));
            Assert.AreEqual("together", session.Hand[1].Id);
            Assert.IsTrue(session.CanPlay(session.Hand[2]));
        }

        [Test]
        public void VersionTwoSaveRebuildsPendingAndResolvedCausalLinksWithoutChangingResources()
        {
            var session = new GameSession(3);
            session.Choose("episode");
            session.Advance();
            session.Choose("avoid");
            session.Advance();
            RunSnapshot old = JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(session.Snapshot()));
            old.rulesVersion = 2;
            old.causalNodes = null;
            int energy = old.energy, money = old.money, relation = old.relation;
            GameSession restored = GameSession.Restore(old);
            Assert.AreEqual(GameSession.RulesVersion, restored.Snapshot().rulesVersion);
            Assert.AreEqual(energy, restored.Energy);
            Assert.AreEqual(money, restored.Money);
            Assert.AreEqual(relation, restored.Relation);
            PendingEcho missed = restored.Pending.Find(e => e.depth == 2);
            Assert.IsNotNull(missed);
            Assert.AreEqual(3, restored.CausalPath(missed.nodeId).Count);
            Assert.AreEqual("solo", missed.replacementId);
        }
    }
}
