using System;
using System.Linq;
using Horizon.Game;
using NUnit.Framework;

namespace Horizon.Tests
{
    public class PracticeFilmTests
    {
        internal static GameSession CaseSampleLife(string id, out CardSpec card)
        {
            for (int seed = 1; seed <= 512; seed++)
            {
                var life = GameSession.StartMasterLife(2, seed, RunMode.Quick);
                card = life.Hand.FirstOrDefault(candidate => candidate.Id == id && life.CanPlay(candidate));
                if (card != null) return life;
            }
            throw new InvalidOperationException("No playable opening hand for " + id);
        }

        [TestCase("ask")]
        [TestCase("portfolio")]
        public void CaseReleaseShowsTheActualReceiptWithinAQuarterSecond(string id)
        {
            var life = CaseSampleLife(id, out CardSpec card);
            life.BeginPractice(card, 2);
            life.PickPracticeRoute(true);
            life.PickPracticeRoute(false);
            ActionRecord action = life.FinishPractice();
            var film = new PracticeRewardCinematic(action);
            int[] actual = PracticeReward.Ticks(action.actualNow);
            int[] balances = Enumerable.Range(0, 6).Select(life.AxisTicks).ToArray();
            Assert.IsTrue(film.HasGain);
            Assert.AreEqual(action.nodeId, film.EventId);
            Assert.LessOrEqual(PracticeRewardCinematic.GainTime - PracticeRewardCinematic.ReleaseTime, .35f);
            Assert.IsEmpty(film.Changes(PracticeRewardCinematic.ReleaseTime, true, false));
            Assert.IsNotEmpty(film.Changes(PracticeRewardCinematic.GainTime, true, false));
            Assert.IsEmpty(film.Changes(PracticeRewardCinematic.GainTime, false, false));
            Assert.Greater(film.Sample(PracticeRewardCinematic.ReleaseTime, false).Release.Flash, 0);
            Assert.Greater(film.Sample(PracticeRewardCinematic.GainTime, false).Release.Fire, 0);
            for (int axis = 0; axis < 6; axis++)
            {
                if (actual[axis] > 0)
                {
                    int previous = 0;
                    for (int frame = 0; frame < 120; frame++)
                    {
                        int current = film.DisplayTicks(axis, PracticeRewardCinematic.GainTime + frame / 60f, false);
                        Assert.That(current, Is.InRange(Math.Max(1, previous), actual[axis]));
                        previous = current;
                    }
                    Assert.AreEqual(actual[axis], film.DisplayTicks(axis, PracticeRewardCinematic.GainTime, true));
                }
                Assert.AreEqual(actual[axis], film.DisplayTicks(axis, PracticeRewardCinematic.Duration, false));
            }
            CollectionAssert.AreEqual(balances, Enumerable.Range(0, 6).Select(life.AxisTicks));
            Assert.Throws<InvalidOperationException>(() => life.FinishPractice());
        }

        [Test]
        public void CasePoseIsSeekableAndMotionReductionKeepsTheFinalValues()
        {
            var receipt = new ActionRecord { nodeId = "case-pose", actualNow = PracticeReward.FromTicks(new[] { -2, 0, 7, 0, 0, 0 }) };
            var film = new PracticeRewardCinematic(receipt);
            var first = film.Sample(PracticeRewardCinematic.LatchTime(0), false);
            var second = film.Sample(PracticeRewardCinematic.LatchTime(1), false);
            var third = film.Sample(PracticeRewardCinematic.LatchTime(2), false);
            Assert.Greater(first.Charge, film.Sample(0, false).Charge);
            Assert.Less(first.Tilt, 0);
            Assert.Greater(second.Tilt, 0);
            Assert.Less(third.Tilt, first.Tilt);
            Assert.AreEqual(second.Tilt, film.Sample(PracticeRewardCinematic.LatchTime(1), false).Tilt);
            float firstGap=PracticeRewardCinematic.LatchTime(1)-PracticeRewardCinematic.LatchTime(0);
            float secondGap=PracticeRewardCinematic.LatchTime(2)-PracticeRewardCinematic.LatchTime(1);
            Assert.Less(secondGap,firstGap);
            var held=film.Sample(PracticeRewardCinematic.HoldTime,false);
            var heldLast=film.Sample(PracticeRewardCinematic.ReleaseTime-.01f,false);
            Assert.AreEqual(held.Compression,heldLast.Compression);
            Assert.AreEqual(held.Tilt,heldLast.Tilt);
            Assert.AreEqual(0,heldLast.Lid);
            Assert.That(film.Sample(PracticeRewardCinematic.ReleaseTime+.14f,false).Lid,Is.GreaterThan(.9f));
            var release = film.Sample(PracticeRewardCinematic.GainTime, false);
            Assert.Greater(release.Lid, 0);
            Assert.Greater(release.Release.Fire, 0);
            Assert.Greater(release.Release.Wave, 0);

            receipt.actualNow.ticks[2] = 9000;
            var still = film.Sample(PracticeRewardCinematic.GainTime, true);
            Assert.AreEqual(0, still.Tilt + still.Compression + still.Charge + still.Release.Fire + still.Release.Wave);
            Assert.AreEqual(1, still.Lid);
            Assert.AreEqual("专注 +0.0007", film.Changes(PracticeRewardCinematic.GainTime, true, true));
            Assert.AreEqual("精力 −0.0002", film.Changes(PracticeRewardCinematic.CostTime, false, true));

            receipt.actualNow = PracticeReward.FromTicks(new[] { -1, 0, 0, 0, 0, 0 });
            var costOnly = new PracticeRewardCinematic(receipt);
            Assert.IsFalse(costOnly.HasGain);
            Assert.AreEqual(0, costOnly.Sample(PracticeRewardCinematic.ReleaseTime, false).Release.Flash);
            Assert.IsEmpty(costOnly.Changes(PracticeRewardCinematic.Duration, true, false));
        }

        [Test]
        public void EachNumberLocksAtItsOwnPhysicalContact()
        {
            var record=new ActionRecord { nodeId="timed-contact",
                actualNow=PracticeReward.FromTicks(new[] {100,300,0,500,0,-2}) };
            var film=new PracticeRewardCinematic(record);
            float first=film.ContactTime(0), next=film.ContactTime(1);
            Assert.Less(first,next);
            Assert.Greater(film.DisplayTicks(0,PracticeRewardCinematic.GainTime+.34f,false),70);
            Assert.Less(film.DisplayTicks(0,first-.02f,false),100);
            Assert.AreEqual(100,film.DisplayTicks(0,first,false));
            Assert.Less(film.DisplayTicks(1,first,false),300);
            Assert.AreEqual(300,film.DisplayTicks(1,next,false));
            Assert.AreEqual(0,film.DisplayTicks(5,film.ContactTime(3),false));
            Assert.AreEqual(-2,film.DisplayTicks(5,PracticeRewardCinematic.CostTime,false));
        }

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
        public void DeferredSkillDoesNotPayOnCompletionOrMoveIntoTheLastDay()
        {
            var life = new GameSession(1, 17);
            var card = life.Hand.Single(c => c.Id == "practice");
            life.BeginPractice(card,1); life.PickPracticeRoute(false);
            var take = life.Practice.Copy();
            var result = life.FinishPractice();
            Assert.True(result.practice.settleOnDueDay);
            Assert.False(result.actualNow.ticks.Any(v => v > 0));
            Assert.Less(result.actualNow.ticks[0], 0);
            Assert.Greater(result.later.ticks[5], 0);
            CollectionAssert.AreEqual(result.later.ticks,life.Pending.Single(e => e.cardId==card.Id).delta.ticks);
            Assert.AreEqual("成果在以后", PracticeReward.Timing(card,card.Now));
            Assert.AreEqual("当日结清", PracticeReward.Timing(CardCatalog.SoloRecovery,CardCatalog.SoloRecovery.Now));
            Assert.AreEqual("后续有代价", PracticeReward.Timing(life.Hand[0],life.Hand[0].Now));
            Assert.That(PracticeReward.EchoReceipt(result,life.Deadline), Does.Contain("第 4 天"));
            take.day = life.Deadline;
            Assert.AreSame(card.Later, PracticeReward.DeferredWeights(take,card,life.Deadline));
            Assert.That(PracticeReward.EchoPreview(card,take.day,life.Deadline), Does.Contain("超出本局"));
            take.settleOnDueDay = false;
            Assert.IsNull(PracticeReward.DeferredWeights(take,card,life.Deadline));
            PracticeReward.Allocate(take, result.practiceBudget, card.Now, card.Later, out var oldNow, out _);
            Assert.True(oldNow.ticks.Any(v => v > 0), "Historical performances retain their advance.");
        }

        [Test]
        public void SignedFractionalCostsAreVisibleWhenAllWholeFieldsAreZero()
        {
            var cost=PracticeReward.FromTicks(new[] { -7,0,0,0,0,0 });
            Assert.True(PlayExperience.IsDifficult(cost));
            Assert.That(PlayExperience.NowLabel(cost), Does.Contain("-0.0007"));
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
        public void MapAndDetourUseTheSameDrawButTrainDifferentResources()
        {
            var card=CardCatalog.FindById("walk");
            var a=new PracticeTake { cardId=card.Id, day=3, difficulty=1, routes="A" };
            var b=a.Copy(); b.routes="B";
            int budget=PracticeReward.Draw(17,1,a);
            Assert.AreEqual(budget,PracticeReward.Draw(17,1,b));
            PracticeReward.Allocate(a,budget,card.Now,null,out var inspect,out _);
            PracticeReward.Allocate(b,budget,card.Now,null,out var detour,out _);
            Assert.Greater(inspect.ticks[2],detour.ticks[2]);
            Assert.Greater(detour.ticks[0],inspect.ticks[0]);
            Assert.AreEqual(budget,inspect.ticks.Sum()); Assert.AreEqual(budget,detour.ticks.Sum());
        }

        [Test]
        public void ADelayedPracticeEchoSettlesExactlyOnceAfterRestore()
        {
            var life=new GameSession(1,17);
            var card=life.Hand.First(c=>c.Delay>0 && c.Kind==CardKind.Growth);
            life.BeginPractice(card,2); life.PickPracticeRoute(false); life.PickPracticeRoute(true);
            var chosen=life.FinishPractice(); int due=chosen.echoDay;
            life=GameSession.Restore(life.Snapshot());
            while(life.Day<due)
            {
                if(!life.HasChosen)
                {
                    if(life.HasPredictionReview) life.MarkPredictionReviewed();
                    if(life.CanPredict) life.SkipPrediction();
                    life.Choose(life.Hand[2].Id);
                }
                if(life.NeedsStation) life.VisitStation();
                life.Advance();
            }
            var settled=life.Actions.First();
            Assert.That(settled.echoed);
            Assert.IsTrue(settled.actualLaterRecorded);
            Assert.IsTrue(settled.actualLater.ticks.Any(v=>v!=0));
            Assert.IsFalse(life.Pending.Any(e=>e.sourceDay==chosen.day && e.depth==1));
            Assert.AreEqual(settled.actualLater.ticks,GameSession.Restore(life.Snapshot()).Actions.First().actualLater.ticks);
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
