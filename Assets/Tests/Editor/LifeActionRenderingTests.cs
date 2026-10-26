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
                Transform aperture=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Architectural horizon aperture");
                Vector3 start=actor.localPosition;
                foreach(float time in new[] {0,story.ActionEnd*.45f,story.ActionEnd*.85f,story.Impact-.1f,story.Settlement+1})
                {
                    world.Cinematics.Advance(time-world.Cinematics.Elapsed); yield return null;
                    bool subjectClose=world.CurrentLifeActionShot==LifeActionShot.Action || world.CurrentLifeActionShot==LifeActionShot.Suspense;
                    Assert.IsTrue(aperture.GetComponentsInChildren<Renderer>(true).All(r=>r.enabled!=subjectClose),
                        "The architectural aperture must clear action/tension close-ups and return for every wide/reveal shot.");
                    if(world.CurrentLifeActionShot==LifeActionShot.Arrive)
                    {
                        Assert.Less(Vector3.Distance(actor.localPosition,new Vector3(.65f,0,4.35f)),.001f,
                            "The stop-and-breathe caption must not appear while the actor is still travelling.");
                        Assert.AreEqual(HorizonBodyAction.Rest,actor.GetComponent<HorizonActorPerformance>().CurrentAction,
                            "Arrival copy and the actual performance must share a cut point.");
                    }
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
                    Vector3 sight=actor.position+Vector3.up*1.3f;
                    var ray=new Ray(world.WorldCamera.transform.position,(sight-world.WorldCamera.transform.position).normalized);
                    foreach(Renderer wall in set.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Home rear wall" || r.name=="Home left wall"))
                    {
                        bool intersects=wall.bounds.IntersectRay(ray,out float distance);
                        Assert.IsFalse(intersects && distance<Vector3.Distance(sight,ray.origin)-.15f,
                            "A geometrically fitted shot must not hide the actor behind "+wall.name);
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
            Text hint=Field<RectTransform>(app,"root").GetComponentsInChildren<Text>().Single(t=>t.name=="Story checkpoint hint");
            Assert.Less(hint.rectTransform.anchorMax.y,((RectTransform)next.transform).anchorMin.y,
                "The waiting hint belongs below the continue button, not on the third gate result.");
            Assert.IsTrue(next.interactable); next.onClick.Invoke(); yield return null; yield return null;
            Assert.IsTrue(completion.Done);
            var life=new GameSession(1,15);
            while(life.CompletedRun==null)
            {
                while(life.HasPredictionReview) life.MarkPredictionReviewed(); if(life.CanPredict) life.SkipPrediction();
                life.Choose(life.Hand[2].Id); if(life.NeedsStation) life.VisitStation(); if(life.CompletedRun==null) life.Advance();
            }
            RunRecord run=life.CompletedRun; string evidence=JsonUtility.ToJson(run);
            typeof(HorizonApp).GetMethod("Clear",Private).Invoke(app,new object[] {false});
            typeof(HorizonApp).GetMethod("DrawDeadlineConstellation",Private).Invoke(app,new object[] {run});
            yield return null;
            var actualGraph=CausalGraph.ObservedGraph(GameSession.GraphForRun(run));
            TimeThreadGraphic[] dayLinks=Field<RectTransform>(app,"root").GetComponentsInChildren<TimeThreadGraphic>();
            var expectedPairs=actualGraph.SelectMany(node=>CausalGraph.Parents(node).Select(parentId=>
                new {Parent=actualGraph.Find(n=>n.id==parentId),Child=node})).Where(pair=>pair.Parent!=null && pair.Parent.day!=pair.Child.day &&
                    pair.Parent.day>=1 && pair.Parent.day<=GameSession.RunLength(run) && pair.Child.day>=1 && pair.Child.day<=GameSession.RunLength(run))
                .Select(pair=>"Recorded day cause "+pair.Parent.day+":"+pair.Child.day).Distinct().ToArray();
            CollectionAssert.AreEquivalent(expectedPairs,dayLinks.Select(l=>l.name));
            Assert.IsTrue(dayLinks.All(l=>l.Arrow && !l.raycastTarget));
            Field<RectTransform>(app,"root").GetComponentsInChildren<Button>().Single(b=>b.name=="Trace day 1").onClick.Invoke();
            Assert.IsTrue(Field<RectTransform>(app,"root").GetComponentsInChildren<Text>().Single(t=>t.name=="Constellation connection guide").text.Contains("第 1 天"));
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

        [UnityTest]
        public IEnumerator OpticalAndEchoCamerasNeverEnterTheNarrativeBody()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Safe narrative camera acceptance").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences {sound=false,haptics=false}); world.Cinematics.enabled=false;
            world.WorldCamera.aspect=.45f;
            foreach(DomainEventKind kind in new[] {DomainEventKind.HorizonChanged,DomainEventKind.Overdrive,DomainEventKind.TimeEcho})
            {
                world.Cinematics.Enqueue(new DomainEvent {id="camera-"+kind,kind=kind,tier=RewardTier.Epic,receipt=new RewardReceipt {horizonLevel=4}},true);
                float duration=world.Cinematics.Current.Duration;
                foreach(float fraction in new[] {.08f,.22f,.38f,.49f,.55f,.94f})
                {
                    world.Cinematics.Advance(duration*fraction-world.Cinematics.Elapsed); yield return null;
                    Transform actor=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Cinematic player");
                    foreach(Renderer part in actor.GetComponentsInChildren<Renderer>().Where(r=>r.enabled))
                    {
                        Bounds b=part.bounds;
                        Assert.IsFalse(b.Contains(world.WorldCamera.transform.position),kind+" must not put the camera inside "+part.name);
                        for(int corner=0;corner<8;corner++)
                        {
                            Vector3 point=b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                            Vector3 v=world.WorldCamera.WorldToViewportPoint(point);
                            Assert.Greater(v.z,0,kind+" · "+part.name);
                            Assert.That(v.x,Is.InRange(.02f,.98f),kind+" must retain the whole body, not a cropped back.");
                            Assert.That(v.y,Is.InRange(.20f,.80f),kind+" must retain the body between readable caption bands.");
                        }
                    }
                }
                world.Cinematics.Skip();
            }
            Object.Destroy(world.gameObject); yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ActualEchoResultDoesNotDismissUntilThePlayerContinues()
        {
            yield return new EnterPlayMode();
            HorizonApp app=Object.FindObjectOfType<HorizonApp>(); if(app==null) app=new GameObject("Echo reading acceptance").AddComponent<HorizonApp>();
            yield return null;
            var life=new GameSession(2,15);
            typeof(HorizonApp).GetField("session",Private).SetValue(app,life);
            var echo=new PendingEcho {sourceDay=1,dueDay=3,cardName="出去走走",echoName="已获得恢复",delta=new ResourceDelta(2)};
            var completion=new Completion();
            app.StartCoroutine(Observe((IEnumerator)typeof(HorizonApp).GetMethod("EchoSequence",Private).Invoke(app,new object[] {echo}),completion));
            yield return new WaitForSecondsRealtime(8.5f);
            Assert.IsFalse(completion.Done,"An echo result must not auto-dismiss even after its animation has ended.");
            Button next=Field<RectTransform>(app,"root").GetComponentsInChildren<Button>().Single(b=>b.name=="Continue story checkpoint");
            Assert.IsTrue(next.interactable); next.onClick.Invoke(); yield return null; yield return null;
            Assert.IsTrue(completion.Done);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CardHasADeepMetalEdgeAndIdleMotionRespectsPauseAndReduction()
        {
            yield return new EnterPlayMode();
            var canvas=new GameObject("Card acceptance canvas",typeof(Canvas)); canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var go=new GameObject("Sculpted card acceptance",typeof(RectTransform),typeof(CanvasRenderer),typeof(HorizonCardSurface));
            go.transform.SetParent(canvas.transform,false);
            var rect=go.GetComponent<RectTransform>(); rect.sizeDelta=new Vector2(290,500);
            HorizonCardSurface surface=go.GetComponent<HorizonCardSurface>();
            VisualPreferences.Paused=false; VisualPreferences.ReducedMotion=false;
            yield return null;
            Quaternion before=rect.localRotation; yield return new WaitForSecondsRealtime(.6f);
            Assert.Greater(Quaternion.Angle(before,rect.localRotation),.05f);
            surface.Rebuild(CanvasUpdate.PreRender); Mesh mesh=surface.canvasRenderer.GetMesh(); Assert.IsNotNull(mesh);
            Assert.GreaterOrEqual(mesh.bounds.size.z,24,"A real extruded edge must remain in card geometry.");
            VisualPreferences.Paused=true; Quaternion held=rect.localRotation;
            yield return new WaitForSecondsRealtime(.3f); Assert.AreEqual(held,rect.localRotation);
            VisualPreferences.Paused=false; VisualPreferences.ReducedMotion=true;
            yield return null; held=rect.localRotation; yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(held,rect.localRotation);
            VisualPreferences.ReducedMotion=false; Object.Destroy(canvas);
            yield return new ExitPlayMode();
        }
        private sealed class Completion { public bool Done; }
        private static IEnumerator Observe(IEnumerator routine,Completion completion) { yield return routine; completion.Done=true; }
    }
}
