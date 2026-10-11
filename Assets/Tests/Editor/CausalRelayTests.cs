using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class CausalRelayTests
    {
        internal static DomainEvent ActualCascade(DomainEventKind kind)
        {
            for(int seed=15;seed<19;seed++)
            {
                var life=GameSession.StartMasterLife(3,seed,RunMode.LongRun);
                while(life.CompletedRun==null)
                {
                    while(life.HasPredictionReview) life.MarkPredictionReviewed();
                    if(life.CanPredict) life.SkipPrediction();
                    if(life.NeedsStation) life.VisitStation();
                    if(life.HasChosen) { life.Advance(); continue; }
                    var available=life.Hand.Where(life.CanPlay).ToArray();
                    CardSpec chosen=available.FirstOrDefault(c=>c.GivesSupport && life.Day%3==0) ??
                        available.FirstOrDefault(c=>c.Kind==CardKind.Growth && life.Energy>=4 && c.Id!="imagine") ?? available.Last();
                    life.Choose(chosen.Id);
                    DomainEvent result=life.Master.events.FirstOrDefault(e=>e.kind==kind);
                    if(result!=null) return result.Copy();
                }
            }
            Assert.Fail("The original long-life rules must reach "+kind); return null;
        }

        private static RewardPlan Merge()
        {
            var graph=new List<CausalNode> {
                new CausalNode { id="learn",day=1,type=CausalNodeKind.Action,label="练习" },
                new CausalNode { id="support",day=1,type=CausalNodeKind.Action,label="请教" },
                new CausalNode { id="merge",day=4,type=CausalNodeKind.Echo,label="共同准备",parentIds=new List<string> {"learn","support"} }
            };
            RewardReceipt receipt=RewardReceipt.Capture(DomainEventKind.Cascade,graph[2],graph,0,1,null);
            return RewardDirector.Direct(new DomainEvent { id="merge-movie",kind=DomainEventKind.Cascade,tier=RewardTier.Epic,chainSize=5,receipt=receipt });
        }

        [Test]
        public void IndependentActionsBranchAndMergeWithoutBecomingAFalseSequentialChain()
        {
            var relay=new CausalRelayPlan(Merge());
            CollectionAssert.AreEquivalent(new[] {"learn→merge","support→merge"},relay.Edges.Select(e=>relay.Nodes[e.From].Evidence.id+"→"+relay.Nodes[e.To].Evidence.id));
            Assert.AreNotEqual(relay.Nodes[0].X,relay.Nodes[1].X);
        }
        [TestCase(DomainEventKind.Cascade)] [TestCase(DomainEventKind.CausalSingularity)]
        public void ActualLongLifeEventsKeepObservedEdgesAndTheirExactCueTimes(DomainEventKind kind)
        {
            DomainEvent source=ActualCascade(kind); string before=JsonUtility.ToJson(source);
            RewardPlan plan=RewardDirector.Direct(source); var relay=new CausalRelayPlan(plan);
            Assert.IsNotEmpty(relay.Edges);
            foreach(CausalRelayEdge edge in relay.Edges)
                CollectionAssert.Contains(relay.Nodes[edge.To].Evidence.parentIds,relay.Nodes[edge.From].Evidence.id);
            foreach(CinematicCue cue in plan.Cues.Where(c=>c.NodeHit))
            {
                Assert.AreEqual(cue.Time,relay.Nodes[cue.NodeIndex].ActivationTime);
                Assert.IsFalse(relay.Sample(cue.NodeIndex,cue.Time-.001f,false).Lit);
                Assert.IsTrue(relay.Sample(cue.NodeIndex,cue.Time,false).Lit);
            }
            Assert.AreEqual(before,JsonUtility.ToJson(source));
        }
        [Test]
        public void CopySaveAndReloadPreserveParentEdgesWithoutSharingMutableLists()
        {
            RewardPlan plan=Merge(); RewardReceipt original=plan.Event.receipt;
            RewardReceipt copy=original.Copy(); copy.causes.Last().parentIds.Clear();
            Assert.AreEqual(2,original.causes.Last().parentIds.Count);
            RewardReceipt restored=JsonUtility.FromJson<RewardReceipt>(JsonUtility.ToJson(original));
            CollectionAssert.AreEqual(original.causes.Last().parentIds,restored.causes.Last().parentIds);
        }
        [Test]
        public void LegacyReceiptsKeepNodesWithoutInventingEdges()
        {
            RewardPlan plan=Merge(); foreach(RewardEvidence source in plan.Event.receipt.causes) source.parentIds=null;
            var relay=new CausalRelayPlan(plan);
            Assert.AreEqual(3,relay.Nodes.Count); Assert.IsEmpty(relay.Edges);
        }
        [Test]
        public void HiddenOriginCannotEscapeThroughTheNewEdgeSnapshot()
        {
            var origin=new CausalNode { id="private",day=1,type=CausalNodeKind.Action,label="隐藏来源" };
            var hidden=new CausalNode { id="echo",day=6,type=CausalNodeKind.Echo,originHidden=true,parentId=origin.id };
            var receipt=RewardReceipt.Capture(DomainEventKind.Cascade,hidden,new[] {origin,hidden},0,1,null);
            Assert.IsFalse(receipt.causes.Any(n=>n.id==origin.id));
            Assert.IsTrue(receipt.causes.All(n=>!n.parentIds.Contains(origin.id)));
        }
        [Test]
        public void PulseFreezesWithTheDirectorAndReductionKeepsEverySource()
        {
            RewardPlan plan=Merge(); var relay=new CausalRelayPlan(plan); var clock=new CinematicClock(plan);
            clock.Advance(relay.Nodes[0].ActivationTime+.05f,c=>{});
            CausalRelayFrame before=relay.Sample(0,clock.Elapsed,false);
            clock.Pause(true); clock.Advance(5,c=>{});
            CausalRelayFrame after=relay.Sample(0,clock.Elapsed,false);
            Assert.AreEqual(before.Pulse,after.Pulse); Assert.AreEqual(before.Beam,after.Beam);
            CausalRelayFrame reduced=relay.Sample(0,clock.Elapsed,true);
            Assert.IsTrue(reduced.Lit); Assert.AreEqual(0,reduced.Beam); Assert.AreEqual(0,reduced.Pulse);
            Assert.AreEqual(0,relay.Sample(0,plan.Duration,false).Fade);
            Assert.Throws<ArgumentOutOfRangeException>(()=>relay.Sample(0,float.NaN,false));
        }
        [Test]
        public void DenseReceiptHasBoundedVisualObjectsAndOnlyRecordedEdges()
        {
            RewardPlan plan=Merge();
            for(int i=0;i<90;i++) plan.Event.receipt.causes.Add(new RewardEvidence { id="n"+i,day=i+5,parentIds=plan.Event.receipt.causes.Select(x=>x.id).ToList() });
            var relay=new CausalRelayPlan(plan);
            Assert.AreEqual(CausalRelayPlan.MaximumNodes,relay.Nodes.Count);
            Assert.LessOrEqual(relay.Edges.Count,CausalRelayPlan.MaximumEdges);
        }
    }
}
