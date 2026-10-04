using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
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
        public void CascadeAcceleratesAndStopsFor150msBeforeNetworkImpact()
        {
            var receipt=new RewardReceipt(); for(int i=0;i<16;i++) receipt.causes.Add(new RewardEvidence { id="node:"+i,label="D"+i,day=i });
            RewardPlan plan=RewardDirector.Direct(new DomainEvent { id="chain",kind=DomainEventKind.Cascade,tier=RewardTier.Epic,chainSize=16,receipt=receipt });
            float[] hits=plan.Cues.Where(x=>x.NodeHit).Select(x=>x.Time).ToArray();
            Assert.AreEqual(.35f,hits[1]-hits[0],.001f); Assert.AreEqual(.28f,hits[2]-hits[1],.001f); Assert.AreEqual(.21f,hits[3]-hits[2],.001f); Assert.AreEqual(.15f,hits[4]-hits[3],.001f);
            float freeze=plan.Cues.Single(x=>!x.NodeHit && x.Phase==CinematicPhase.HitStop).Time;
            Assert.AreEqual(.15f,plan.Cues.Single(x=>!x.NodeHit && x.Phase==CinematicPhase.Impact).Time-freeze,.001f);
            Assert.Greater(freeze,hits.Last());
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
    }
}
