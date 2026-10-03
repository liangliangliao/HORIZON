using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class ExpansionContractTests
    {
        private static void Ready(GameSession life)
        { while (life.HasPredictionReview) life.MarkPredictionReviewed(); if (life.CanPredict) life.SkipPrediction(); }
        private static void Next(GameSession life)
        { Ready(life); life.Choose(life.Hand.Last(c => life.CanPlay(c)).Id); if (life.NeedsStation) life.VisitStation(); if (life.CompletedRun == null) life.Advance(); }
        private static GameSession Reload(GameSession life)
        { return GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(life.Snapshot()))); }

        [Test]
        public void ReceivedExperienceBecomesADelayedEchoWithoutInventedResourceRewards()
        {
            var life = GameSession.StartMasterLife(1, 15, RunMode.ParallelLives);
            string before = JsonUtility.ToJson(life.Snapshot()); int energy = life.Energy, focus = life.Insight;
            Assert.IsTrue(life.ReceiveFutureMessage("room:message", "受挫后，我先恢复，再重新开始。", 2));
            Assert.AreEqual(energy, life.Energy); Assert.AreEqual(focus, life.Insight);
            Assert.IsFalse(life.ReceiveFutureMessage("room:message", "重复领取", 2));
            var echo = life.Pending.Single(e => e.cardId == "future-message"); Assert.AreEqual(3, echo.dueDay);
            Assert.IsFalse(life.CausalNodes.Single(n => n.id == echo.nodeId).resolved);
            life = Reload(life); Next(life); Next(life);
            CausalNode arrived = life.CausalNodes.Single(n => n.id == echo.nodeId);
            Assert.IsTrue(arrived.resolved); Assert.AreEqual(0, arrived.effect.energy);
            Assert.IsTrue(CausalGraph.Ancestors(life.CausalNodes, arrived.id).Any(n => n.type == CausalNodeKind.Memory));
            Assert.IsTrue(life.Master.events.Any(e => e.kind == DomainEventKind.TimeEcho && e.nodeId == arrived.id));
            Assert.IsFalse(life.ReceiveFutureMessage("room:message", "重复领取", 2));
        }

        [Test]
        public void HiddenCauseRequiresInsightAndRevealsARealParentWithoutChangingResources()
        {
            var low = GameSession.StartMasterLife(1, 15, RunMode.Quick);
            while (low.Day < 6) Next(low);
            low.ApplyMystery(); var mystery = low.Mysteries.First();
            Assert.IsFalse(low.RevealHiddenCause(mystery.consequenceNodeId));
            low.Master.insightPoints = 90; int focus = low.Insight, energy = low.Energy;
            Assert.IsTrue(low.RevealHiddenCause(mystery.consequenceNodeId));
            Assert.AreEqual(focus, low.Insight); Assert.AreEqual(energy, low.Energy);
            Assert.IsFalse(low.CausalNodes.Single(n => n.id == mystery.consequenceNodeId).originHidden);
            low = Reload(low); Assert.IsTrue(low.Mysteries.First().revealed);
            Assert.IsFalse(low.RevealHiddenCause(mystery.consequenceNodeId));
        }

        [TestCase("参加面试")]
        [TestCase("跑步锻炼")]
        [TestCase("修复朋友关系")]
        public void TwoSetbacksHaveGoalSpecificProcessCostsRecoveryAndCalibration(string goal)
        {
            ImagineRun run = ImaginationEngine.Begin("practice", "goal", goal, 2, 2, 2, "上次在第一次反馈后改换了路线");
            int budget = run.energy; ImaginationEngine.Continue(run);
            ImaginationEngine.Prepare(run, PreparationAction.SmallStep); Assert.Less(run.energy, budget);
            string firstFailure = null;
            for (int i = 0; run.phase != ImaginePhase.Complete && i < 64; i++)
            {
                if (run.phase == ImaginePhase.Failure && firstFailure == null) firstFailure = run.timeline.Last().text;
                if (run.phase == ImaginePhase.Recover) { int before = run.energy; ImaginationEngine.Recover(run, RecoveryAction.Rest); Assert.Greater(run.energy, before); }
                else ImaginationEngine.Continue(run);
            }
            Assert.AreEqual(ImaginePhase.Complete, run.phase); Assert.AreEqual(2, run.failures); Assert.AreEqual(2, run.recovered);
            Assert.That(firstFailure, Does.Contain("上次")); Assert.AreEqual(3, run.multiplier);
            Assert.AreEqual(2, run.memories.Count); Assert.Greater(run.memories.Last().value, run.memories.First().value);
            JsonUtility.FromJson<ImagineRun>(JsonUtility.ToJson(run)).Validate();
        }

        [Test]
        public void EightKnowledgeSkillsKeepTheirHighestEvidenceAcrossLives()
        {
            var previous = KnowledgeLibrary.All.Select(k => k.Copy()).ToList();
            previous[3].stage = KnowledgeStage.Experience; previous[3].simulationNodeId = "previous-action"; previous[3].realityNodeId = "reality:2026-10-03";
            var life = GameSession.StartMasterLife(3, 15, RunMode.ExperimentRun, knowledge: previous);
            Assert.AreEqual(8, life.Master.knowledge.Count); Assert.AreEqual(KnowledgeStage.Experience, life.Master.knowledge[3].stage);
            Assert.AreEqual("reality:2026-10-03", Reload(life).Master.knowledge[3].realityNodeId);
        }

        [TestCase(NarrativePurpose.FutureSelf)]
        [TestCase(NarrativePurpose.PersonalQuest)]
        [TestCase(NarrativePurpose.Pattern)]
        [TestCase(NarrativePurpose.Imagination)]
        [TestCase(NarrativePurpose.Knowledge)]
        [TestCase(NarrativePurpose.Npc)]
        public async Task EveryNarrativePurposeWorksOfflineAndCannotMutateTheRules(NarrativePurpose purpose)
        {
            var life = GameSession.StartMasterLife(1, 15, RunMode.Quick); string frozen = JsonUtility.ToJson(life.Snapshot());
            var context = new NarrativeContext("参加面试", "反复比较", NarrativeContext.From(life).Evidence, purpose);
            PersonalContent result = await new LocalContentAdapter().Personalize(context, CancellationToken.None);
            Assert.IsFalse(result.generatedByAI); Assert.IsNotEmpty(result.futureSelfLine); Assert.IsNotEmpty(result.quest); Assert.IsNotEmpty(result.patternExplanation);
            Assert.AreEqual(frozen, JsonUtility.ToJson(life.Snapshot()));
        }

        [Test]
        public void SoundAndHapticsHaveDistinctEchoRecoveryAndMythicPhrases()
        {
            float[] mythic = SoundLanguage.Render(DomainEventKind.PatternBroken);
            Assert.IsTrue(mythic.Take((int)(22050 * 0.60)).All(x => x == 0));
            Assert.IsTrue(mythic.Skip(14000).Any(x => Math.Abs(x) > 0.1));
            Assert.IsFalse(SoundLanguage.Render(DomainEventKind.TimeEcho).SequenceEqual(SoundLanguage.Render(DomainEventKind.FailAndAgain)));
            Assert.AreEqual(4, HapticLanguage.For(DomainEventKind.TimeEcho, RewardTier.Major).timings.Length);
            var haptic = HapticLanguage.For(DomainEventKind.PatternBroken, RewardTier.Mythic);
            Assert.AreEqual(600, haptic.timings[0]); Assert.AreEqual(0, haptic.amplitudes[0]); Assert.Greater(haptic.amplitudes.Last(), haptic.amplitudes[1]);
            Assert.IsNull(HapticLanguage.For(DomainEventKind.OrbitActivated, RewardTier.Local));
        }

        [Test]
        public void SharedTimelinesContainOnlyPublicChoicesAndARealTenSecondSoundtrack()
        {
            var life = GameSession.StartMasterLife(1, 15, RunMode.ParallelLives);
            while (life.CompletedRun == null) Next(life);
            SharedTimeline shared = SharedTimeline.From(life); string json = JsonUtility.ToJson(shared);
            Assert.AreEqual(12, shared.actions.Count); Assert.AreEqual(6, shared.resources.Length); Assert.IsTrue(shared.completed);
            Assert.That(json, Does.Not.Contain("patterns").And.Not.Contain("knowledge").And.Not.Contain("memberToken"));
            byte[] pcm = TimelineSoundtrack.Pcm(life.CompletedRun); Assert.AreEqual(10 * 22050 * 2, pcm.Length); Assert.IsTrue(pcm.Any(x => x != 0));
        }

        [Test]
        public async Task SocialClientUsesHttpsMembershipAndBoundedPublicPayloads()
        {
            var transport = new SocialTransport(); var client = new SocialClient("https://social.example.test", transport);
            var joined = await client.JoinRoom("ABCDEF0123", "玩家", CancellationToken.None); joined.room.Validate();
            var room = await client.ReadRoom("ABCDEF0123", "opaque-membership", CancellationToken.None);
            Assert.AreEqual("GET", transport.last.Method); Assert.AreEqual("Bearer opaque-membership", transport.last.Headers["Authorization"]);
            Assert.AreEqual(0, room.DivergenceDay());
            Assert.Throws<AIException>(() => new SocialClient("http://social.example.test", transport));
            Assert.ThrowsAsync<OperationCanceledException>(async () => await client.ReadRoom("ABCDEF0123", "opaque-membership", new CancellationToken(true)));
        }
        [Test]
        public void OverdriveBuildsFromActualExecutionHasADailyCapAndExpires()
        {
            var life = GameSession.StartMasterLife(1, 15, RunMode.Quick);
            for (int day = 1; day <= 4; day++)
            {
                Ready(life); CardSpec card = life.Hand.Last(c => life.CanPlay(c));
                life.LockDecision(card.Id);
                while (life.Master.decision.status != DecisionStatus.Ready) life.ExecuteDecisionStep();
                Assert.LessOrEqual(life.Master.overdriveGain, 25);
                life.Choose(card.Id); if (life.NeedsStation) life.VisitStation(); if (day < 4) life.Advance();
            }
            Assert.IsTrue(life.Master.events.Any(e => e.kind == DomainEventKind.Overdrive));
            Assert.AreEqual(6, life.Master.overdriveUntilDay);
            int energy = life.Energy, money = life.Money;
            int information = life.MasterHorizon; Assert.GreaterOrEqual(information, 3);
            life = Reload(life); Assert.AreEqual(energy, life.Energy); Assert.AreEqual(money, life.Money);
            life.Advance(); while (life.Day <= 6) Next(life);
            Assert.Greater(life.Day, life.Master.overdriveUntilDay);
        }

        [Test]
        public void CascadeAndSingularityComeFromRealMergedActionsInLongLives()
        {
            int cascades = 0, singularities = 0;
            for (int seed = 15; seed < 19; seed++)
            {
                var life = GameSession.StartMasterLife(3, seed, RunMode.LongRun);
                while (life.CompletedRun == null)
                {
                    Ready(life); var available = life.Hand.Where(life.CanPlay).ToArray();
                    CardSpec chosen = available.FirstOrDefault(c => c.GivesSupport && life.Day % 3 == 0) ??
                        available.FirstOrDefault(c => c.Kind == CardKind.Growth && life.Energy >= 4 && c.Id != "imagine") ?? available.Last();
                    life.Choose(chosen.Id); if (life.NeedsStation) life.VisitStation(); if (life.CompletedRun == null) life.Advance();
                }
                foreach (DomainEvent e in life.Master.events.Where(e => e.kind == DomainEventKind.Cascade || e.kind == DomainEventKind.CausalSingularity))
                {
                    Assert.GreaterOrEqual(CausalGraph.Ancestors(life.CausalNodes, e.nodeId).Where(n => n.type == CausalNodeKind.Action).Select(n => n.id).Distinct().Count(), 2);
                    if (e.kind == DomainEventKind.Cascade) cascades++; else singularities++;
                }
            }
            Assert.Greater(cascades, 0); Assert.Greater(singularities, 0);
        }

        private sealed class SocialTransport : IAITransport
        {
            public AIRequest last;
            public Task<AIResponse> Send(AIRequest request, CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested(); last = request;
                var room = new SharedRoom { code = "ABCDEF0123", worldSeed = 15, catalogVersion = 10, days = 12, runNumber = 1,
                    members = new[] { new SharedMember { id = "one", name = "玩家" } }, messages = Array.Empty<SharedMessage>() };
                string json = request.Method == "GET" ? JsonUtility.ToJson(room) : JsonUtility.ToJson(new SharedMembership { room = room, memberId = "one", memberToken = "opaque-membership" });
                return Task.FromResult(new AIResponse(200, json));
            }
        }
    }
}
