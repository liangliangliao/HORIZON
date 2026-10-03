using System;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class ExpeditionTests
    {
        private static void Ready(GameSession life)
        { while (life.HasPredictionReview) life.MarkPredictionReviewed(); if (life.CanPredict) life.LockPrediction(new ResourceDelta(), 1); }
        private static void Advance(GameSession life, string id)
        { Ready(life); life.Choose(id); if (life.NeedsStation) life.VisitStation(); if (life.Day < life.Deadline) life.Advance(); }
        private static GameSession Reload(GameSession life)
        { return GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(life.Snapshot()))); }

        [TestCase("uncertainty")]
        [TestCase("tomorrow")]
        [TestCase("perfection")]
        [TestCase("deadline")]
        [TestCase("comfort")]
        [TestCase("gaze")]
        [TestCase("possibility")]
        [TestCase("fatigue")]
        [TestCase("waiting")]
        [TestCase("preparation")]
        public void EveryBossCanBePreparedRecoveredAndActuallyReached(string boss)
        {
            foreach (int seed in new[] { 15, 16, 71 })
            {
                var life = GameSession.StartMasterLife(1, seed, RunMode.Quick);
                life.BeginChapter(boss); life.ChooseLifeRoute("growth"); life.EquipTrigger("route"); life.EquipTrigger("deadline");
                bool locked = false;
                while (life.CompletedRun == null)
                {
                    Ready(life);
                    if (life.ChapterNeedsPreparation) Assert.IsTrue(life.PrepareChapter("help"));
                    if (life.ChapterNeedsBoss)
                    {
                        string route = boss == "perfection" ? "draft" : "help";
                        Assert.IsTrue(life.CanResolveChapter(route), boss + ": " + life.ChapterRouteCondition(route) + "; " + life.Energy + "/" + life.Ability + "/" + life.Relation);
                        Assert.IsTrue(life.ResolveChapter(route));
                    }
                    CardSpec growth = life.Hand.FirstOrDefault(c => c.Kind == CardKind.Growth && life.CanPlay(c));
                    bool wantGrowth = life.Day == 2 || life.Day == 3 || life.Day == 6 || life.Day == 8;
                    CardSpec choice = wantGrowth && growth != null && life.Energy >= 3 ? growth : life.Hand.Last(c => life.CanPlay(c));
                    if (!locked && choice.Kind == CardKind.Growth)
                    { life.LockDecision(choice.Id); while (life.Master.decision.status != DecisionStatus.Ready) life.ExecuteDecisionStep(); locked = true; }
                    Advance(life, choice.Id);
                    if (life.CompletedRun == null && life.Day == 7) life = Reload(life);
                }
                Assert.AreEqual(ChapterOutcome.Arrived, life.Master.chapter.outcome, boss);
                Assert.GreaterOrEqual(life.Master.chapter.failureDay, 4);
                Assert.IsTrue(life.Master.events.Any(e => e.kind == DomainEventKind.Comeback));
                CausalNode end = life.CausalNodes.Single(n => n.id == life.Master.chapter.outcomeNode);
                Assert.IsTrue(CausalGraph.Ancestors(life.CausalNodes, end.id).Any(n => n.id == life.Master.chapter.failureNode));
                Assert.AreEqual(12, life.CompletedRun.actions.Count);
            }
        }

        [Test]
        public void RouteTradeoffsAndEquipmentRemovalAreRealTransactionsAndSurviveReload()
        {
            var life = GameSession.StartMasterLife(1, 15, RunMode.Quick);
            Assert.IsTrue(life.ChooseLifeRoute("income")); int focus = life.Insight;
            Assert.IsTrue(life.ChooseLifeRoute("recovery")); Assert.AreEqual(focus - 1, life.Insight);
            Assert.IsTrue(life.EquipTrigger("appointment")); int money = life.Money;
            Assert.IsTrue(life.RemoveTrigger("appointment")); Assert.AreEqual(money, life.Money, "Removing a paid appointment must not farm a refund.");
            Assert.IsTrue(life.EquipTrigger("promise")); int relation = life.Relation;
            Assert.IsTrue(life.RemoveTrigger("promise")); Assert.AreEqual(relation - 1, life.Relation);
            life = Reload(life); Assert.AreEqual("recovery", life.Master.expedition.route); Assert.IsEmpty(life.Master.triggers);
            Assert.IsFalse(life.ChooseLifeRoute("unknown"));
        }

        [Test]
        public void PreparationUsesFocusAndCanExpireMomentumWithoutDeletingTheOpportunity()
        {
            var life = GameSession.StartMasterLife(1, 15, RunMode.Quick);
            life.Master.windows.Add(new OpportunityWindow { id = "fixture-window", cardId = life.Hand[1].Id, openedDay = 1, expiresDay = 2, momentumUntilDay = 1 });
            Assert.IsTrue(life.EquipTrigger("alarm")); Assert.IsTrue(life.EquipTrigger("place")); Assert.IsTrue(life.EquipTrigger("deadline"));
            Assert.AreEqual(0, life.Master.windows[0].momentumUntilDay); Assert.IsFalse(life.Master.windows[0].expired);
            Assert.IsTrue(life.Master.events.Any(e => e.kind == DomainEventKind.MomentumExpired));
            int focus = life.Insight; Assert.IsTrue(life.AdjustEnvironment("two-minutes")); Assert.AreEqual(focus - 1, life.Insight);
            Assert.Less(life.Master.engine.friction, 5);
        }

        [TestCase("possibility")]
        [TestCase("tomorrow")]
        [TestCase("perfect")]
        [TestCase("unsuitable")]
        [TestCase("comfort")]
        [TestCase("gaze")]
        public void EveryThoughtHasAReasonAndTradeoffRatherThanAThreat(string id)
        { ThoughtMonster thought = ThoughtMonsters.ById(id); Assert.IsNotEmpty(thought.reasonablePart); Assert.IsNotEmpty(thought.tradeoff); }

        [Test]
        public void ThoughtResponseIsPaidOnlyOnceAndActuallySupportsTheFollowingAction()
        {
            var life = GameSession.StartMasterLife(1, 15, RunMode.Quick); int focus = life.Insight;
            Assert.IsTrue(life.RespondToThought("act")); Assert.AreEqual(focus - 1, life.Insight);
            Assert.IsFalse(life.RespondToThought("act")); Assert.AreEqual(focus - 1, life.Insight);
            string node = life.Master.expedition.monsterNode; life.Choose(life.Hand[2].Id);
            Assert.IsTrue(CausalGraph.Parents(life.CausalNodes.Single(n => n.id == life.Actions[0].nodeId)).Contains(node));
        }

        [Test]
        public void CouncilWeightsHaveSixVoicesAndExplainEnvironmentMoneyAndFailure()
        {
            var life = GameSession.StartMasterLife(1, 15, RunMode.Quick); life.Master.awaitingComeback = true;
            var council = InnerCouncil.Explain(life); Assert.AreEqual(6, council.Count); Assert.AreEqual(100, council.Sum(v => v.weight));
            Assert.IsTrue(council.Single(v => v.name == "舒适").sources.Any(s => s.Contains("环境")));
            Assert.IsTrue(council.Single(v => v.name == "害怕失败").sources.Any(s => s.Contains("受挫")));
            Assert.IsTrue(council.Single(v => v.name == "金钱").sources.Any(s => s.Contains(life.Money.ToString())));
        }

        [Test]
        public void WorldviewsFormALimitedDeckAndBecomeCausalPerspectivesOnActualChoices()
        {
            var life = GameSession.StartMasterLife(1, 15, RunMode.Quick);
            Assert.IsTrue(life.SetWorldview("ACT", true)); Assert.IsTrue(life.SetWorldview("CBT", true)); Assert.IsTrue(life.SetWorldview("James", true));
            Assert.IsFalse(life.SetWorldview("Stoicism", true)); Assert.That(life.WorldviewPerspective(life.Hand[1]), Does.Contain("ACT").And.Contain(life.Hand[1].Name));
            life.Choose(life.Hand[1].Id);
            Assert.IsTrue(CausalGraph.Ancestors(life.CausalNodes, life.Actions[0].nodeId).Any(n => n.type == CausalNodeKind.Thought && n.label.StartsWith("ACT")));
            life = Reload(life); Assert.AreEqual(3, life.Master.expedition.worldviews.Count);
        }

        [Test]
        public void KnowledgeRequiresARealConditionAndAMatchingSimulationBeforeReality()
        {
            var life = GameSession.StartMasterLife(1, 15, RunMode.Quick); Assert.AreEqual(8, life.Master.knowledge.Count);
            Assert.IsFalse(life.RecognizeSkill("rest")); Assert.IsTrue(life.RecognizeSkill("connection"));
            CardSpec support = life.Hand.FirstOrDefault(c => c.GivesSupport);
            while (support == null) { Advance(life, life.Hand[2].Id); support = life.Hand.FirstOrDefault(c => c.GivesSupport); }
            Advance(life, support.Id); var skill = life.Master.knowledge.Single(k => k.id == "connection");
            Assert.AreEqual(KnowledgeStage.Simulate, skill.stage); Assert.IsNotEmpty(skill.simulationNodeId); Assert.IsNotEmpty(skill.recognitionNodeId);
            Assert.Throws<InvalidOperationException>(() => KnowledgeForge.Advance(skill, KnowledgeStage.Experience, "fake"));
            life = Reload(life); Assert.AreEqual(KnowledgeStage.Simulate, life.Master.knowledge.Single(k => k.id == "connection").stage);
        }

        [Test]
        public void MirrorRunRecreatesARepeatedNodeAndNeedsActualRecoveryThenGrowth()
        {
            var model = new PlayerBehavioralModel(); model.Observe(new[] {
                new BehaviorObservation { id = "old1", run = 1, day = 4, key = "decision-reopen", nodeId = "old1", continued = false },
                new BehaviorObservation { id = "old2", run = 2, day = 4, key = "decision-reopen", nodeId = "old2", continued = false } });
            var life = GameSession.StartMasterLife(3, 15, RunMode.MirrorRun, model);
            while (life.Day < 4) Advance(life, life.Hand[2].Id);
            Ready(life); Assert.IsTrue(life.MirrorEncounterPending); Assert.IsTrue(life.ResolveMirror("recover"));
            Advance(life, life.Hand[2].Id); Ready(life);
            CardSpec growth = life.Hand.First(c => c.Kind == CardKind.Growth && life.CanPlay(c)); Advance(life, growth.Id);
            Assert.IsTrue(life.Master.events.Any(e => e.kind == DomainEventKind.PatternBroken));
            Assert.IsFalse(life.Master.awaitingComeback); Assert.IsFalse(life.MirrorEncounterPending);
        }

        [Test]
        public void LongRunIsSixtyDaysAndRestoresAndReplaysItsDistinctLength()
        {
            var life = GameSession.StartMasterLife(1, 15, RunMode.LongRun); Assert.AreEqual(60, life.Deadline);
            while (life.CompletedRun == null) Advance(life, life.Hand[2].Id);
            Assert.AreEqual(60, GameSession.RunLength(life.CompletedRun)); Assert.AreEqual(60, life.CompletedRun.actions.Count);
            life.Master.Validate(60, 60);
            CardSpec alternate = GameSession.AlternativesForDay(life.CompletedRun, 1).First(c => c.Id != life.CompletedRun.actions[0].cardId);
            RunRecord replay = GameSession.ReplayAlternative(life.CompletedRun, 1, alternate.Id);
            Assert.IsNotNull(replay); Assert.AreEqual(60, replay.actions.Count);
        }

        [Test]
        public void RecoveryAndRelationshipsHaveIndependentPaidOnceReservoirs()
        {
            var life = GameSession.StartMasterLife(1, 15, RunMode.LongRun);
            for (int day = 1; day < 18; day++) Advance(life, life.Day % 3 == 0 && life.CanPlay(life.Hand[1]) ? life.Hand[1].Id : life.Hand[2].Id);
            Assert.IsTrue(life.Master.expedition.pools.Any(p => p.id == "recovery" || p.id == "connection"));
            int events = life.Master.events.Count(e => e.kind == DomainEventKind.Breakthrough); life = Reload(life);
            Assert.AreEqual(events, life.Master.events.Count(e => e.kind == DomainEventKind.Breakthrough));
            Assert.IsTrue(life.Master.expedition.pools.All(p => p.sources.All(id => life.CausalNodes.Any(n => n.id == id && n.type == CausalNodeKind.Action))));
        }
    }
}
