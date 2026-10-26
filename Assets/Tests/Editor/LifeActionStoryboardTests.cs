using System.Linq;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class LifeActionStoryboardTests
    {
        internal static DomainEvent Walk() => new DomainEvent {id="walk-story",kind=DomainEventKind.ActionTaken,tier=RewardTier.Local,
            day=2,title="出去走走",detail="给身体留出恢复的时间。",receipt=new RewardReceipt {resourcesRecorded=true,
                action="出去走走",sourceDay=2,destinationDay=4,futureMeaning="精力会慢慢恢复",resources=new ResourceDelta(2,1)}};

        [TestCase("出去走走",LifeActionTheme.Walk)]
        [TestCase("早点休息",LifeActionTheme.Rest)]
        [TestCase("借一本好书",LifeActionTheme.Read)]
        [TestCase("给朋友发消息",LifeActionTheme.Connect)]
        [TestCase("整理作品",LifeActionTheme.Work)]
        [TestCase("写下计划",LifeActionTheme.Prepare)]
        [TestCase("买点安慰",LifeActionTheme.Spend)]
        [TestCase("认真复盘",LifeActionTheme.Practice)]
        public void TheRecordedActionChoosesItsScene(string action,LifeActionTheme theme)
        {
            DomainEvent e=Walk(); e.receipt.action=action;
            Assert.AreEqual(theme,new LifeActionStoryboard(RewardDirector.Direct(e)).Theme);
        }

        [Test]
        public void ActionArrivalSuspenseAndTrueChangesHaveSeparateOrderedReadingTime()
        {
            DomainEvent source=Walk(); string evidence=JsonUtility.ToJson(source);
            RewardPlan plan=RewardDirector.Direct(source); var story=new LifeActionStoryboard(plan);
            Assert.GreaterOrEqual(story.ActionEnd,7.1f);
            Assert.GreaterOrEqual(story.ActionEnd*.32f,2.2f,"The door-opening sentence needs a readable hold.");
            Assert.GreaterOrEqual(story.ActionEnd*.28f,2f,"Arrival copy must not flash past.");
            Assert.GreaterOrEqual(story.Impact-story.ActionEnd,4f);
            Assert.GreaterOrEqual(story.Reveal-story.Impact,3.2f-.001f);
            Assert.AreEqual(LifeActionShot.Establish,story.Sample(0).Shot);
            Assert.AreEqual(LifeActionShot.Action,story.Sample(story.ActionEnd*.45f).Shot);
            Assert.AreEqual(LifeActionShot.Arrive,story.Sample(story.ActionEnd*.85f).Shot);
            Assert.AreEqual(LifeActionShot.Suspense,story.Sample(story.Impact-.1f).Shot);
            Assert.Greater(story.Sample(story.Impact-.1f).Pressure,.9f);
            Assert.AreEqual(0,story.Sample(story.Impact-.1f,true).Pressure);
            Assert.AreEqual(LifeActionShot.Reveal,story.Sample(story.Reveal+.01f).Shot);
            Assert.AreEqual(LifeActionShot.Overview,story.Sample(story.Settlement+.1f).Shot);
            var text=new CinematicTextChoreography(plan);
            Assert.That(text.Sample(story.ActionEnd*.45f).Narration,Does.Contain("公园"));
            Assert.That(text.Sample(story.Impact-.1f).Narration,Does.Contain("变化"));
            Assert.AreEqual("精力 +2",plan.Outcome.Sample(story.Reveal+1,story.Reveal,
                plan.Cues.Single(c=>c.Phase==CinematicPhase.SecondReveal).Time,story.Settlement).Copy);
            Assert.AreEqual(evidence,JsonUtility.ToJson(source));
        }
        [Test]
        public void HiddenEchoesNeverInventAnEverydaySourceScene()
        {
            DomainEvent e=Walk(); e.kind=DomainEventKind.TimeEcho; e.receipt.sourceDay=0;
            Assert.IsFalse(new LifeActionStoryboard(RewardDirector.Direct(e)).Enabled);
            e.receipt.sourceDay=2; Assert.IsTrue(new LifeActionStoryboard(RewardDirector.Direct(e)).Enabled);
        }
        [TestCase("TIME ECHO · Day 5","时间回声 · 第 5 天")]
        [TestCase("NOW","当下")]
        [TestCase("KNOW · 我知道","知道这个方法")]
        [TestCase("CAUSAL SINGULARITY","因果汇聚")]
        [TestCase("D7 · SURPRISE","第 7 天 · 发现预料之外的变化")]
        [TestCase("v0.4.2 · SEE FARTHER. IMAGINE DEEPER.","v0.4.2 · 看得更远，想得更深。")]
        [TestCase("HORIZON VIII · Day +3","远见 · 第 8 层 · 3 天后")]
        [TestCase("IF · 先停一下\nTHEN · 再决定","如果 · 先停一下\n那么 · 再决定")]
        [TestCase("ALL LINKED\nFuture Memory","未来方向已连接\n未来记忆")]
        [TestCase("IMAGINATION → SIMULATION → REALITY","想象 → 游戏预演 → 现实")]
        [TestCase("RUN 003 / D7","第 3 段人生 / 第 7 天")]
        [TestCase("Future Message · 留一段经验","未来来信 · 留一段经验")]
        public void LegacyDisplayCopyIsChineseWithoutRewritingTheSave(string original,string expected)
        { Assert.AreEqual(expected,PresentationCopy.Chinese(original)); Assert.AreEqual(expected,PresentationCopy.Chinese(expected)); }
        [Test]
        public void RealActionPlaysBeforeItsOrbitAndEpicWithoutRewritingEmitOrderOrPayout()
        {
            var life=GameSession.StartMasterLife(2,0,RunMode.Quick);
            foreach(var old in life.Master.events) old.acknowledged=true;
            CardSpec walk=life.Hand.Single(c=>c.Id=="walk"); life.Choose(walk.Id);
            DomainEvent action=life.Master.events.Last(e=>e.kind==DomainEventKind.ActionTaken);
            DomainEvent orbit=life.Master.events.Last(e=>e.kind==DomainEventKind.OrbitActivated);
            Assert.Less(life.Master.events.IndexOf(orbit),life.Master.events.IndexOf(action),"Reproduce the real bookkeeping order.");
            string before=JsonUtility.ToJson(life.Snapshot());
            Assert.AreSame(action,RewardPlayback.Pending(life.Master.events).First());
            Assert.AreSame(action,RewardPlayback.Prerequisite(life.Master.events,orbit));
            DomainEvent epic=orbit.Copy(); epic.kind=DomainEventKind.Breakthrough; epic.tier=RewardTier.Epic;
            Assert.AreSame(action,RewardPlayback.Prerequisite(life.Master.events,epic));
            Assert.AreEqual(before,JsonUtility.ToJson(life.Snapshot()));
            action.acknowledged=true; Assert.AreSame(orbit,RewardPlayback.Prerequisite(life.Master.events,orbit));
        }
    }
}
