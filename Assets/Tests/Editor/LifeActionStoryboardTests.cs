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
            Assert.GreaterOrEqual(story.ActionEnd,4.5f);
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
        [TestCase("CAUSAL SINGULARITY","因果汇聚")]
        [TestCase("D7 · SURPRISE","第 7 天 · 发现预料之外的变化")]
        public void LegacyDisplayCopyIsChineseWithoutRewritingTheSave(string original,string expected)
        { Assert.AreEqual(expected,PresentationCopy.Chinese(original)); Assert.AreEqual(expected,PresentationCopy.Chinese(expected)); }
    }
}
