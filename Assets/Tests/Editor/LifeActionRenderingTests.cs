using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Horizon.Game;
using Horizon.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Horizon.Tests
{
    public sealed class LifeActionRenderingTests
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static T Field<T>(HorizonApp app,string field)=>(T)typeof(HorizonApp).GetField(field,Private).GetValue(app);

        [UnityTest]
        public IEnumerator WalkCameraShowsTheWholeSetThenTheActionAndReturnsToAFullOverview()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Everyday camera acceptance").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences {sound=false,haptics=false}); world.Cinematics.enabled=false;
            foreach(bool reduced in new[] {false,true}) foreach(float aspect in new[] {.45f,.5625f})
            {
                world.ApplyPreferences(new PlayerPreferences {sound=false,haptics=false,reducedMotion=reduced});
                world.WorldCamera.aspect=aspect; DomainEvent source=LifeActionStoryboardTests.Walk();
                string receipt=JsonUtility.ToJson(source); world.Cinematics.Enqueue(source,true);
                var story=new LifeActionStoryboard(world.Cinematics.Current);
                Transform set=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Everyday action scene");
                Transform actor=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Cinematic player");
                Vector3 start=actor.localPosition;
                foreach(float time in new[] {0,story.ActionEnd*.45f,story.ActionEnd*.85f,story.Impact-.1f,story.Settlement+1})
                {
                    world.Cinematics.Advance(time-world.Cinematics.Elapsed); yield return null;
                    bool full=reduced || world.CurrentLifeActionShot==LifeActionShot.Establish ||
                        world.CurrentLifeActionShot==LifeActionShot.Arrive || world.CurrentLifeActionShot==LifeActionShot.Overview;
                    foreach(Renderer renderer in (full?set:actor).GetComponentsInChildren<Renderer>())
                    {
                        if(!renderer.enabled || renderer is LineRenderer || renderer is ParticleSystemRenderer) continue;
                        Bounds b=renderer.bounds;
                        for(int c=0;c<8;c++)
                        {
                            Vector3 p=b.center+Vector3.Scale(b.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1));
                            Vector3 v=world.WorldCamera.WorldToViewportPoint(p);
                            Assert.Greater(v.z,0,renderer.name);
                            Assert.That(v.x,Is.InRange(.03f,.97f),renderer.name+" must fit the phone width.");
                            Assert.That(v.y,Is.InRange(.22f,.795f),renderer.name+" must clear the copy areas.");
                        }
                    }
                }
                Assert.Greater(Vector3.Distance(start,actor.localPosition),4,"Walking must actually leave the home and reach the park.");
                world.Cinematics.Skip(); Assert.IsFalse(set.gameObject.activeSelf);
                Assert.AreEqual(receipt,JsonUtility.ToJson(source));
            }
            Object.Destroy(world.gameObject); yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ReadingCheckpointWaitsForARealClickAndTheMapNeverDrawsAnAllToAllWeb()
        {
            yield return new EnterPlayMode();
            HorizonApp app=Object.FindObjectOfType<HorizonApp>(); if(app==null) app=new GameObject("Reading checkpoint acceptance").AddComponent<HorizonApp>();
            yield return null;
            typeof(HorizonApp).GetMethod("Clear",Private).Invoke(app,new object[] {false});
            var completion=new Completion();
            IEnumerator checkpoint=(IEnumerator)typeof(HorizonApp).GetMethod("WaitForStoryCheckpoint",Private).Invoke(app,new object[] {"我看清了 · 继续"});
            app.StartCoroutine(Observe(checkpoint,completion));
            yield return new WaitForSecondsRealtime(3.2f);
            Assert.IsFalse(completion.Done,"A reading page must never auto-dismiss.");
            Button next=Field<RectTransform>(app,"root").GetComponentsInChildren<Button>().Single(b=>b.name=="Continue story checkpoint");
            Assert.IsTrue(next.interactable); next.onClick.Invoke(); yield return null; yield return null;
            Assert.IsTrue(completion.Done);
            var life=new GameSession(1,15);
            while(life.CompletedRun==null)
            {
                while(life.HasPredictionReview) life.MarkPredictionReviewed(); if(life.CanPredict) life.SkipPrediction();
                life.Choose(life.Hand[2].Id); if(life.NeedsStation) life.VisitStation(); if(life.CompletedRun==null) life.Advance();
            }
            RunRecord run=life.CompletedRun; string evidence=JsonUtility.ToJson(run);
            typeof(HorizonApp).GetMethod("ShowCausalNetworkOverview",Private).Invoke(app,new object[] {run,false});
            yield return null;
            RectTransform page=Field<RectTransform>(app,"overlay");
            Assert.AreEqual(0,page.GetComponentsInChildren<TimeThreadGraphic>().Length);
            ScrollRect scroll=page.GetComponentsInChildren<ScrollRect>().Single();
            Assert.IsFalse(scroll.horizontal); Assert.IsTrue(scroll.vertical);
            int groups=page.GetComponentsInChildren<Text>().Count(t=>t.name.StartsWith("Network day group "));
            Assert.Greater(groups,1); Assert.LessOrEqual(groups,GameSession.RunLength(run));
            Assert.IsFalse(page.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("SURPRISE") || t.text.Contains("PREDICTION")));
            page.GetComponentsInChildren<Button>().Single(b=>b.name=="Network relation filter").onClick.Invoke();
            yield return null; Assert.AreEqual(evidence,JsonUtility.ToJson(run));
            yield return new ExitPlayMode();
        }
        private sealed class Completion { public bool Done; }
        private static IEnumerator Observe(IEnumerator routine,Completion completion) { yield return routine; completion.Done=true; }
    }
}
