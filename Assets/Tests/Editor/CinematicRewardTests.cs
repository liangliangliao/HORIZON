using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class V3RewardProfilesTests
    {
        [Test]
        public void OrbitGroupLightsRecordedMasksAndRetainsOneTimeline()
        {
            int[] directions = { OrbitCinematic.Ability, OrbitCinematic.Relationship, OrbitCinematic.Wealth };
            IReadOnlyList<DomainEvent> events = ActualOrbit();
            int bits = directions.Aggregate(0, (mask, direction) => mask | 1 << direction);
            var plan = RewardDirector.DirectOrbit(events);
            var orbit = plan.Orbit;
            Assert.AreEqual(0, orbit.PreviousBits);
            Assert.AreEqual(bits, orbit.FinalBits);
            CollectionAssert.AreEqual(directions, orbit.Beats.Select(beat => beat.Direction));
            for (int i = 0; i < directions.Length; i++)
            {
                var beat = orbit.Beats[i];
                int bit = 1 << directions[i];
                Assert.AreEqual(0, orbit.BitsAt(beat.Time - .001f) & bit);
                Assert.AreEqual(bit, orbit.BitsAt(beat.Time) & bit);
                Assert.AreEqual(events[i].id, beat.EventId);
            }
            Assert.AreEqual(bits, orbit.BitsAt(plan.Duration));
            Assert.IsEmpty(plan.Outcome.Changes);
            Assert.IsFalse(events.Any(entry => entry.acknowledged));
            Assert.That(orbit.ReceiptCopy, Does.Contain("能力").And.Contain("财富").And.Contain("关系"));
        }

        internal static IReadOnlyList<DomainEvent> ActualOrbit()
        {
            var life = PracticeFilmTests.CaseSampleLife("ask", out CardSpec card);
            life.BeginPractice(card, 2);
            life.PickPracticeRoute(true);
            life.PickPracticeRoute(false);
            life.FinishPractice();
            return life.Master.events.Where(OrbitCinematic.HasRecordedNode).ToArray();
        }

        [Test]
        public void ARecordedEchoMovesItsRevealCueWithoutChangingReceiptOrLegacyEvidence()
        {
            var source = new DomainEvent { id = "echo-cue", day = 5, kind = DomainEventKind.TimeEcho, tier = RewardTier.Major,
                receipt = new RewardReceipt { sourceDay = 2, action = "整理作品", resourcesRecorded = true,
                    resources = PracticeReward.FromTicks(new[] { 0, 0, 1321, 0, 0, 840 }) } };
            RewardPlan actual = RewardDirector.Direct(source);
            var cue = new RewardSpectacle(actual);
            Assert.IsTrue(actual.RecordedEcho);
            Assert.AreEqual(RecordedEchoStory.ImpactAt,cue.ImpactTime);
            Assert.AreEqual(RecordedEchoStory.Duration,actual.Duration);
            float held=actual.Cues.Single(item=>item.Phase==CinematicPhase.HitStop).Time;
            Assert.That(cue.ImpactTime-held,Is.InRange(.20f,.30f));
            var tension=new CinematicTension(actual);
            Assert.AreEqual(tension.Sample(held+.04f).PoseTime,tension.Sample(cue.ImpactTime-.01f).PoseTime);
            var steps=new[] { 0f,RecordedEchoStory.PrepareAt,RecordedEchoStory.DevelopAt,RecordedEchoStory.DockAt,
                RecordedEchoStory.ImpactAt,RecordedEchoStory.PrizeAt,RecordedEchoStory.SettleAt };
            CollectionAssert.AreEqual(System.Enum.GetValues(typeof(RecordedEchoBeat)),
                steps.Select(time=>RecordedEchoStory.Sample(time,false).Beat));
            Assert.IsTrue(RecordedEchoStory.Sample(held+.01f,false).Held);
            Assert.Less(RecordedEchoStory.Sample(held+.01f,false).Progress,1f);
            Assert.AreEqual(1,RecordedEchoStory.Sample(cue.ImpactTime,false).Progress);
            Assert.Greater(cue.MultiplierTime-cue.ImpactTime,EnergyRelease.PrizeDelay);
            var energy=new CinematicEnergy(actual);
            float previousLoad=0, previousCharge=energy.Sample(.50f).Inward;
            for(int i=0;i<RecordedEchoStory.GuideCount;i++)
            {
                float at=RecordedEchoStory.GuideAt(i);
                Assert.That(at,Is.InRange(RecordedEchoStory.DevelopAt,RecordedEchoStory.DockAt));
                Assert.Greater(RecordedEchoStory.Sample(at+.08f,false).Progress,RecordedEchoStory.Sample(at-.08f,false).Progress);
                if(i>0) Assert.Greater(at-RecordedEchoStory.GuideAt(i-1),1.08f);
                float load=RecordedEchoStory.Sample(at+.12f,false).ReaderLoad;
                float charge=energy.Sample(at+.24f).Inward;
                Assert.That(load,Is.GreaterThan(previousLoad+.16f));
                Assert.That(RecordedEchoStory.Sample(at+.22f,false).ReaderLoad,Is.EqualTo(load).Within(.005f));
                Assert.That(RecordedEchoStory.Sample(at+.64f,false).ReaderLoad,Is.LessThan(load*.5f));
                Assert.That(charge,Is.GreaterThan(previousCharge+.12f));
                Assert.That(energy.Sample(at+.74f).Inward,Is.LessThan(charge));
                previousLoad=load;
                previousCharge=charge;
            }
            Assert.That(energy.Sample(held+.05f).Inward,Is.EqualTo(energy.Sample(cue.ImpactTime-.01f).Inward));
            Assert.AreEqual(0,RecordedEchoStory.Sample(held,true).ReaderLoad);
            Assert.AreEqual(0,energy.Sample(held,true).Inward);
            Assert.Greater(RecordedEchoStory.ResultTurnAt(1,6)-RecordedEchoStory.ResultContactAt(0,6),.75f);
            Assert.Greater(RecordedEchoStory.SettleFor(6)-RecordedEchoStory.ConvergenceAt(6),.30f);
            float second = actual.Cues.Single(item => item.Phase == CinematicPhase.SecondReveal).Time;
            string Read(float at,bool reduced=false) => actual.Outcome.SampleRecordedEcho(at,cue.MultiplierTime,second,cue.SettlementTime,reduced).Copy;
            float firstContact=RecordedEchoStory.ResultContactAt(0,2), lastContact=RecordedEchoStory.ResultContactAt(1,2);
            const string final="专注 +0.1321　能力 +0.0840";
            Assert.IsEmpty(Read(cue.ImpactTime+.50f));
            Assert.IsEmpty(Read(RecordedEchoStory.PrizeAt+.20f));
            Assert.AreEqual("专注 +0.0001",Read(cue.MultiplierTime));
            Assert.That(Read(firstContact-.01f),Does.Not.Contain("+0.1321"));
            Assert.That(Read(firstContact),Does.Contain("+0.1321").And.Not.Contain("+0.0840"));
            Assert.AreEqual("专注 +0.1321",Read(RecordedEchoStory.ResultTurnAt(1,2)-.04f));
            Assert.AreEqual("专注 +0.1321　能力 +0.0001",Read(RecordedEchoStory.ResultShownAt(1,2)));
            Assert.That(Read(lastContact-.01f),Does.Not.Contain("+0.0840"));
            Assert.AreEqual(final,Read(lastContact));
            Assert.AreEqual(final,Read(actual.Duration));
            Assert.AreEqual(final,Read(0,true));
            Assert.AreEqual(0,energy.Sample(lastContact).Release.Fire);
            Assert.Greater(energy.Sample(cue.ImpactTime+.40f).Release.Wave,.8f);
            var prizeFrame=energy.Sample(RecordedEchoStory.PrizeAt).Release;
            Assert.AreEqual(0,prizeFrame.Flash+prizeFrame.Fire+prizeFrame.Wave);
            Assert.AreEqual(0,energy.Sample(RecordedEchoStory.ConvergenceAt(2)+.32f).Release.Wave);
            Assert.AreEqual(1321, source.receipt.resources.ticks[2]);
            source.receipt.sourceDay = 0;
            RewardPlan hidden = RewardDirector.Direct(source);
            Assert.IsFalse(hidden.RecordedEcho);
            var legacy = new RewardSpectacle(hidden);
            Assert.Greater(legacy.MultiplierTime - legacy.ImpactTime, .35f);
        }
    }

    public sealed class CinematicRewardTests
    {
        [Test]
        public void AllSixResourceObjectsDescribeTheActualSignedReceipt()
        {
            var receipt=new RewardReceipt { resourcesRecorded=true,resources=new ResourceDelta(5,2,3,2,9,1) };
            var e=new DomainEvent { id="six",kind=DomainEventKind.ActionTaken,tier=RewardTier.Local,receipt=receipt };
            RewardPlan plan=RewardDirector.Direct(e);
            Assert.AreEqual(6,plan.Objects.Count);
            CollectionAssert.AreEquivalent(new[] { RewardObjectKind.EnergyCell,RewardObjectKind.WarmLamp,RewardObjectKind.FocusLens,
                RewardObjectKind.ConnectionRing,RewardObjectKind.MoneyWallet,RewardObjectKind.ToolKit },plan.Objects.Select(x=>x.Kind));
            Assert.AreEqual("精力 +5",plan.Objects.Single(x=>x.Kind==RewardObjectKind.EnergyCell).Copy);
            Assert.AreEqual(5,plan.Objects.Single(x=>x.Kind==RewardObjectKind.EnergyCell).VisualCount);
            receipt.resources.energy=100; Assert.AreEqual(5,plan.Event.receipt.resources.energy);
        }
        [Test]
        public void MaturedFractionProducesSignedPhysicalRewardsAtTheirActualSize()
        {
            var receipt = new RewardReceipt { resourcesRecorded=true,
                resources=PracticeReward.FromTicks(new[] { -9,0,0,0,0,187 }) };
            var plan = RewardDirector.Direct(new DomainEvent { kind=DomainEventKind.TimeEcho, receipt=receipt });
            var tool = plan.Objects.Single(o=>o.Kind==RewardObjectKind.ToolKit);
            Assert.AreEqual(.0187m,tool.Amount);
            Assert.AreEqual(1,tool.VisualCount);
            Assert.AreEqual("能力 +0.0187",tool.Copy);
            Assert.AreEqual(-.0009m,plan.Objects.Single(o=>o.Kind==RewardObjectKind.EnergyCell).Amount);
        }

        [TestCase(1,1,0)] [TestCase(5,5,1)] [TestCase(20,1,2)] [TestCase(50,1,3)] [TestCase(100,1,4)] [TestCase(500,1,4)]
        public void LargeResourceReceiptsBecomeDevicesWithoutMultiplyingYield(int actual,int meshes,int scale)
        {
            var item=new MaterialReward(RewardObjectKind.EnergyCell,RewardObjectClass.Resource,"精力 +"+actual,actual);
            Assert.AreEqual(actual,item.Amount); Assert.AreEqual(meshes,item.VisualCount); Assert.AreEqual(scale,item.Scale);
        }
        [Test]
        public void LossesDoNotBecomeGainsAndNoReceiptInventsAResourceReward()
        {
            var e=new DomainEvent { id="loss",kind=DomainEventKind.TimeEcho,tier=RewardTier.Major,
                receipt=new RewardReceipt { resourcesRecorded=true,resources=new ResourceDelta(-2,-1) } };
            RewardPlan plan=RewardDirector.Direct(e); Assert.AreEqual("精力 -2",plan.Objects[0].Copy); Assert.AreEqual(-2,plan.Objects[0].Amount);
            e.receipt.resourcesRecorded=false; Assert.IsEmpty(RewardDirector.Direct(e).Objects);
        }
        [Test]
        public void RealActionClampingSurvivesObserversPlansSavesAndReplay()
        {
            var life=new GameSession(2,15); ActionRecord action=life.Choose(life.Hand[2].Id);
            DomainEvent e=life.Master.events.Single(x=>x.kind==DomainEventKind.ActionTaken);
            Assert.IsTrue(e.receipt.resourcesRecorded); Assert.AreEqual(action.actualNow.energy,e.receipt.resources.energy);
            string before=JsonUtility.ToJson(life.Snapshot()); RewardPlan plan=RewardDirector.Direct(e);
            var collection=new RewardCollection(); collection.Capture(e,life.RunNumber);
            var clock=new CinematicClock(plan); clock.Advance(plan.Duration,c=>{}); clock.Skip();
            Assert.AreEqual(before,JsonUtility.ToJson(life.Snapshot()));
            var restored=GameSession.Restore(JsonUtility.FromJson<RunSnapshot>(before));
            Assert.AreEqual(e.receipt.resources.energy,restored.Master.events.Single(x=>x.id==e.id).receipt.resources.energy);
        }
        [Test]
        public void PatternMovieUsesOnlyRecordedFailuresAndTheChosenAction()
        {
            var model=new PlayerBehavioralModel(); model.Observe(new[] {
                new BehaviorObservation { id="a",run=1,day=1,key="decision-reopen",nodeId="x" },
                new BehaviorObservation { id="b",run=2,day=1,key="decision-reopen",nodeId="y" } });
            var life=GameSession.StartMasterLife(3,41,RunMode.MirrorRun,model);
            life.LockDecision(life.Hand[1].Id); for(int i=0;i<4;i++) life.ExecuteDecisionStep(); life.Choose(life.Master.decision.cardId);
            DomainEvent broken=life.Master.events.Single(x=>x.kind==DomainEventKind.PatternBroken);
            Assert.AreEqual(2,broken.receipt.pastFailures.Count); Assert.IsTrue(broken.receipt.pastFailures.All(x=>x.label.Contains("曾在这里停下")));
            Assert.IsNotEmpty(broken.receipt.action);
            Assert.AreEqual(RewardTier.Mythic,RewardDirector.Direct(broken).Event.tier);
            var old=broken.Copy(); old.receipt=null; Assert.IsNull(RewardDirector.Direct(old).Event.receipt);
        }
        [Test]
        public void RealityMovieContainsThreeExistingEvidenceIdsAndCannotBeFarmed()
        {
            var reality=new RealityConstellation(); DateTime now=new DateTime(2026,10,4);
            var memory=new FutureMemory { id="memory",goalId="goal",imaginationRun=1,imaginationNodeId="imagine",
                simulationRun=2,simulationNodeId="simulation",simulationDay=3,text="失败以后求助" };
            RealityQuest quest=reality.Offer(now,"goal","亲自求助",memory.id);
            Assert.IsTrue(reality.Complete(quest.id,now,new[] { memory }));
            DomainEvent e=reality.events.Single(x=>x.kind==DomainEventKind.RealityConvergence);
            CollectionAssert.AreEquivalent(new[] { CausalNodeKind.Imagination,CausalNodeKind.Action,CausalNodeKind.Reality },e.receipt.causes.Select(x=>x.kind));
            Assert.IsTrue(e.receipt.causes.All(x=>reality.nodes.Any(n=>n.id==x.id)));
            Assert.IsFalse(reality.Complete(quest.id,now,new[] { memory })); Assert.AreEqual(2,reality.events.Count);
            Assert.AreEqual(RewardObjectKind.ConvergencePrism,RewardDirector.Direct(e).Objects.Single().Kind);
        }
        [TestCase(DomainEventKind.PatternBroken,RewardTier.Mythic)]
        [TestCase(DomainEventKind.RealityConvergence,RewardTier.Mythic)]
        [TestCase(DomainEventKind.Cascade,RewardTier.Epic)]
        [TestCase(DomainEventKind.Overdrive,RewardTier.Epic)]
        public void EpicAndMythicShareOrderedDirectorGrammar(DomainEventKind kind,RewardTier tier)
        {
            var e=new DomainEvent { id="grammar",kind=kind,tier=tier,chainSize=9,receipt=new RewardReceipt() };
            RewardPlan plan=RewardDirector.Direct(e);
            CollectionAssert.AreEqual(Enum.GetValues(typeof(CinematicPhase)),plan.Cues.Where(x=>!x.NodeHit).Select(x=>x.Phase));
            Assert.IsTrue(plan.Cues.All(x=>x.Time>=0 && x.Time<=plan.Duration));
            Assert.AreEqual(1,plan.Cues.Count(x=>!x.NodeHit && x.Phase==CinematicPhase.Impact));
        }
        [Test]
        public void CascadeGivesEveryRecordedNodeAReadableIncrementBeforeNetworkImpact()
        {
            var receipt=new RewardReceipt(); for(int i=0;i<16;i++) receipt.causes.Add(new RewardEvidence { id="node:"+i,label="D"+i,day=i });
            RewardPlan plan=RewardDirector.Direct(new DomainEvent { id="chain",kind=DomainEventKind.Cascade,tier=RewardTier.Epic,chainSize=16,receipt=receipt });
            float[] hits=plan.Cues.Where(x=>x.NodeHit).Select(x=>x.Time).ToArray();
            for(int i=0;i<hits.Length-1;i++) Assert.GreaterOrEqual(hits[i+1]-hits[i],2.6f-.001f);
            float freeze=plan.Cues.Single(x=>!x.NodeHit && x.Phase==CinematicPhase.HitStop).Time;
            Assert.AreEqual(.42f,plan.Cues.Single(x=>!x.NodeHit && x.Phase==CinematicPhase.Impact).Time-freeze,.001f);
            Assert.GreaterOrEqual(freeze-hits.Last(),2.6f-.001f);
            var text=new CinematicTextChoreography(plan);
            for(int i=0;i<hits.Length;i++)
            {
                Assert.That(text.Sample(hits[i]+.2f).Narration,Does.Contain("第 "+i+" 天"));
                Assert.AreEqual(text.Sample(hits[i]+.2f).Narration,text.Sample(hits[i]+2.3f).Narration);
            }
        }
        [Test]
        public void PauseSlowFramesAndSkipNeverRepeatAnImpact()
        {
            var plan=RewardDirector.Direct(new DomainEvent { id="pause",kind=DomainEventKind.PatternBroken,tier=RewardTier.Mythic });
            var clock=new CinematicClock(plan); var cues=new List<CinematicCue>(); clock.Advance(1,cues.Add);
            clock.Pause(true); clock.Advance(60,cues.Add); Assert.AreEqual(1,clock.Elapsed);
            clock.Pause(false); clock.Advance(60,cues.Add); Assert.IsTrue(clock.Finished);
            Assert.AreEqual(1,cues.Count(x=>x.Phase==CinematicPhase.Impact)); clock.Advance(60,cues.Add);
            Assert.AreEqual(plan.Cues.Count,cues.Count);
            var skipped=new CinematicClock(plan); skipped.Skip(); skipped.Advance(60,c=>Assert.Fail("Skipped sensory cue fired"));
            Assert.IsTrue(skipped.Finished);
        }
        [Test]
        public void MementosArePermanentDeduplicatedAndMigrateWithoutASecondEconomy()
        {
            var e=new DomainEvent { id="real:event",nodeId="real",kind=DomainEventKind.RealityNode,tier=RewardTier.Major,day=1,title="REALITY NODE" };
            var archive=new ArchiveData(); archive.reality.events.Add(e); archive.Repair(); archive.Repair();
            Assert.AreEqual(1,archive.rewardCollection.items.Count); int balance=archive.wallet.stardust;
            Assert.IsFalse(archive.rewardCollection.Capture(e,0));
            var restored=JsonUtility.FromJson<ArchiveData>(JsonUtility.ToJson(archive)); restored.Repair();
            Assert.AreEqual(1,restored.rewardCollection.items.Count); Assert.AreEqual(balance,restored.wallet.stardust);
        }
        [Test]
        public void DomainObserversReceiveAnIsolatedRewardReceipt()
        {
            var life=new GameSession(2,15); life.DomainEventRaised+=e=> { if(e.receipt!=null) { e.receipt.resources.energy=999; e.receipt.causes.Clear(); } };
            ActionRecord action=life.Choose(life.Hand[2].Id);
            Assert.AreEqual(action.actualNow.energy,life.Master.events.Single(x=>x.kind==DomainEventKind.ActionTaken).receipt.resources.energy);
        }
        [Test]
        public void TemporaryOverdriveDoesNotInventPermanentHorizonGrowth()
        {
            var life=new GameSession(2,15); int before=life.MasterHorizon; life.Master.overdriveEnergy=99;
            life.LockDecision(life.Hand[1].Id); Assert.Greater(life.MasterHorizon,before);
            Assert.IsTrue(life.Master.events.Any(x=>x.kind==DomainEventKind.Overdrive));
            Assert.IsFalse(life.Master.events.Any(x=>x.kind==DomainEventKind.HorizonChanged));
            Assert.AreEqual(before,life.Master.presentationHorizon);
        }
        [Test]
        public void HiddenProvenanceStaysHiddenInRewardMovies()
        {
            var origin=new CausalNode { id="unknown",type=CausalNodeKind.Action,day=9,label="未揭晓的行动" };
            var hidden=new CausalNode { id="hidden",type=CausalNodeKind.Echo,day=6,label="一份未见来路的回声",parentId=origin.id,originHidden=true };
            RewardReceipt receipt=RewardReceipt.Capture(DomainEventKind.TimeEcho,hidden,new[] { origin,hidden },0,1,null);
            Assert.AreEqual(0,receipt.sourceDay); Assert.IsFalse(receipt.causes.Any(x=>x.id==origin.id));
            var plan=RewardDirector.Direct(new DomainEvent { id="hidden",kind=DomainEventKind.TimeEcho,tier=RewardTier.Major,detail="来路暂未清晰。",receipt=receipt });
            Assert.IsFalse(plan.Cues.Any(x=>x.Copy.Contains("D9") || x.Copy.Contains("D0")));
        }
    }
}
