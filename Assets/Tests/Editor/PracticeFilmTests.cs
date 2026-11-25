using System;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;

namespace Horizon.Tests
{
    public class PracticeFilmTests
    {
        [Test]
        public void CatalogHasDistinctThemesAndTwoPerformableRoutesPerObstacle()
        {
            Assert.AreEqual(54,PracticeFilms.Ids.Count());
            Assert.AreEqual(54,PracticeFilms.Ids.Select(id=>PracticeFilms.For(id).Success).Distinct().Count());
            foreach(string id in PracticeFilms.Ids)
            {
                var film=PracticeFilms.For(id);
                Assert.That(film.Goal.Length,Is.GreaterThan(8),id);
                for(int beat=0;beat<3;beat++)
                {
                    Assert.That(film.Hurdle(beat).Length,Is.GreaterThan(8),id);
                    Assert.AreNotEqual(film.Move(beat,false),film.Move(beat,true),id);
                    Assert.AreNotEqual(film.Route(beat,false),film.Route(beat,true),id);
                }
            }
        }

        [Test]
        public void SavedChoiceRestoresAndCommitsExactSignedAmountsOnce()
        {
            var life=new GameSession(1,19);
            var card=life.Hand.First(c=>c.Kind==CardKind.Growth);
            life.BeginPractice(card,2); life.PickPracticeRoute(false);
            var mid=life.Snapshot();
            Assert.Throws<InvalidOperationException>(()=>life.FinishPractice());
            var resumed=GameSession.Restore(mid);
            Assert.AreEqual("A",resumed.Practice.routes);
            resumed.PickPracticeRoute(true);
            var preview=resumed.PreviewPractice(card);
            var balance=Enumerable.Range(0,6).Select(resumed.AxisTicks).ToArray();
            var action=resumed.FinishPractice();
            CollectionAssert.AreEqual(preview.ticks,action.actualNow.ticks);
            Assert.AreEqual("AB",action.practice.routes);
            Assert.AreEqual("A",mid.practice.routes);
            Assert.That(action.actualNow.ticks.Any(v=>v<0));
            Assert.That(action.practiceBudget,Is.InRange(1,PracticeReward.Ceiling(2)));
            Assert.Throws<InvalidOperationException>(()=>resumed.FinishPractice());
            var loaded=GameSession.Restore(resumed.Snapshot());
            for(int i=0;i<6;i++) Assert.AreEqual(balance[i]+preview.ticks[i],loaded.AxisTicks(i));
            Assert.IsNull(loaded.Practice);
            Assert.That(PlayExperience.NowLabel(action.actualNow),Does.Match(@"[+-]0\.\d{4}"));
            Assert.That(action.now.ticks.Concat(action.later.ticks).Sum(v=>Math.Abs(v)),Is.EqualTo(action.practiceBudget));
        }

        [Test]
        public void NoRewardForAbandoningAndRerollsAreStable()
        {
            var life=new GameSession(1,448);
            var card=life.Hand[2];
            int previous=0;
            foreach(int level in new[]{1,2,3})
            {
                life.BeginPractice(card,level);
                for(int i=0;i<level;i++) life.PickPracticeRoute(true);
                int budget=life.PracticeBudget;
                Assert.That(budget,Is.InRange(previous+1,PracticeReward.Ceiling(level))); previous=budget;
                var restored=GameSession.Restore(life.Snapshot()); Assert.AreEqual(budget,restored.PracticeBudget);
                life.AbandonPractice(); Assert.AreEqual(0,life.Actions.Count); Assert.AreEqual("6.0000",life.Balance(0));
            }
        }

        [Test]
        public void CapacityAndEchoesUseActualTicksIncludingAtBounds()
        {
            var legacy=new GameSession(1,123).Snapshot(); legacy.energy=10; legacy.mood=10;
            var life=GameSession.Restore(legacy);
            var card=life.Hand[2]; life.BeginPractice(card,1); life.PickPracticeRoute(true);
            var receipt=life.FinishPractice();
            Assert.AreEqual(0,receipt.actualNow.ticks[0]); Assert.AreEqual("10.0000",life.Balance(0));
            if(card.Delay == 0) return;
            Assert.AreEqual(receipt.later.ticks,life.Pending.First().delta.ticks);
        }
    }
}
