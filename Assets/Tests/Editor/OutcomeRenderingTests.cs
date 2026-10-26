using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Horizon.Game;
using Horizon.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Horizon.Tests
{
    public sealed class OutcomeRenderingTests
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [Test]
        public void EmbossAndMobileLightShadersCompile()
        {
            foreach(string name in new[] {"HorizonCaption","HorizonBloom","HorizonImmersion"})
            {
                Shader shader=Resources.Load<Shader>(name); Assert.IsNotNull(shader,name);
                Assert.IsFalse(ShaderUtil.ShaderHasError(shader),name);
            }
        }
        [Test]
        public void RehearsalRetryShowsActualRecoveryMeaningWithoutChangingItsSavedEvidence()
        {
            var run=ImaginationEngine.Begin("actual-retry","portfolio","整理作品",1,2);
            ImaginationEngine.Continue(run);
            ImaginationEngine.Prepare(run,PreparationAction.SmallStep);
            ImaginationEngine.Continue(run); ImaginationEngine.Continue(run);
            ImaginationEngine.Recover(run,RecoveryAction.Rest);
            string evidence=JsonUtility.ToJson(run), reading=ImaginationStoryboard.Reading(run);
            Assert.That(reading,Does.StartWith(run.memories.Last().text));
            Assert.That(reading,Does.Contain("再次行动 1 次"));
            Assert.That(reading,Does.Not.Contain("×").And.Not.Contain("AGAIN"));
            Assert.AreEqual(evidence,JsonUtility.ToJson(run));
        }
        [UnityTest]
        public IEnumerator ActualActionRevealsItsChangesAndDateWithoutAnyFictionalMultiplier()
        { return CaptureActualOutcome(false); }
        [UnityTest]
        public IEnumerator ActualWalkPlaysTheHomeParkSuspenseAndRecordedRecoveryInFullColor()
        { return CaptureActualOutcome(true); }
        private IEnumerator CaptureActualOutcome(bool walk)
        {
            yield return new EnterPlayMode();
            var app=Object.FindObjectOfType<HorizonApp>(); if(app==null) app=new GameObject("Outcome capture app").AddComponent<HorizonApp>();
            yield return null; yield return null;
            Assert.IsNotNull(app,"The real application must finish startup before recording.");
            var life=walk?CreateWalkLife():CreateActualLife();
            string prefix=walk?"walk-outcome":"actual-outcome";
            Assert.IsNotNull(life); Assert.IsNotNull(life.Master);
            DomainEvent source=life.Master.events.Last(e=>e.kind==DomainEventKind.ActionTaken);
            string receipt=JsonUtility.ToJson(source.receipt);
            var world=(HorizonWorld3D)typeof(HorizonApp).GetField("world",Private).GetValue(app);
            var root=(RectTransform)typeof(HorizonApp).GetField("root",Private).GetValue(app);
            Assert.IsNotNull(world,"Recording requires the initialized scene world."); Assert.IsNotNull(root,"Recording requires the initialized phone canvas.");
            var archive=new ArchiveData {active=life.Snapshot(),seenSecondLife=true,nextRareRun=99}; archive.Repair();
            archive.preferences.sound=false; archive.preferences.haptics=false;
            typeof(HorizonApp).GetField("archive",Private).SetValue(app,archive);
            typeof(HorizonApp).GetField("session",Private).SetValue(app,life);
            typeof(HorizonApp).GetMethod("ApplyPreferences",Private).Invoke(app,null);
            typeof(HorizonApp).GetMethod("Clear",Private).Invoke(app,new object[] {false});
            Canvas canvas=root.GetComponentInParent<Canvas>();
            Assert.IsNotNull(canvas); Assert.IsNotNull(world.Cinematics);
            typeof(HorizonApp).GetMethod("PlayMasterSpectacle",Private).Invoke(app,new object[] {source,(Action)(()=>{})});
            world.Cinematics.enabled=false;
            RewardPlan plan=world.Cinematics.Current;
            Assert.IsNotNull(plan,"The actual action must be scheduled, not replaced by a startup page.");
            var actionStory=new LifeActionStoryboard(plan);
            Assert.IsTrue(actionStory.Enabled,"The recorded action needs its authored action scene before the result.");
            world.Cinematics.Advance(actionStory.Impact-.25f); yield return null; yield return null;
            Assert.AreEqual(LifeActionShot.Suspense,actionStory.Sample(world.Cinematics.Elapsed).Shot);
            foreach(string labelName in new[] {"Reward event title","Reward story"})
            {
                Text label=root.GetComponentsInChildren<Text>(true).Single(t=>t.name==labelName);
                Assert.IsNotEmpty(label.text,labelName);
                var face=label.GetComponentsInChildren<TextMeshProUGUI>(true).Single(t=>t.name=="Cinematic TMP text");
                Assert.AreEqual(label.text,face.text,labelName);
                Assert.GreaterOrEqual(face.maxVisibleCharacters,label.text.Length,
                    labelName+" must remain completely readable throughout the breath-hold before reveal.");
                var depth=label.transform.Find("Embossed text depth")?.GetComponent<TextMeshProUGUI>();
                if(depth!=null) Assert.GreaterOrEqual(depth.maxVisibleCharacters,label.text.Length,
                    labelName+" embossed depth must follow the complete face, not a stale partial reveal.");
            }
            float reveal=plan.Cues.Single(c=>!c.NodeHit && c.Phase==CinematicPhase.Multiplication).Time;
            float future=plan.Cues.Single(c=>!c.NodeHit && c.Phase==CinematicPhase.SecondReveal).Time;
            var target=new RenderTexture(540,960,24); target.Create(); var pixels=new Texture2D(540,960,TextureFormat.RGB24,false);
            var ui=new GameObject("Outcome portrait UI",typeof(Camera)).GetComponent<Camera>();
            ui.enabled=false; ui.cullingMask=1<<5; ui.nearClipPlane=.1f; ui.farClipPlane=20; ui.targetTexture=target;
            RenderMode mode=canvas.renderMode; Camera prior=canvas.worldCamera; float distance=canvas.planeDistance;
            try
            {
                for(int i=0;i<=plan.Outcome.Changes.Count;i++)
                {
                    bool next=i==plan.Outcome.Changes.Count;
                    float time=next?future+1:reveal+i*CinematicOutcome.ChangeSeconds+1;
                    world.Cinematics.Advance(time-world.Cinematics.Elapsed); yield return null; yield return null;
                    Text main=root.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Reward resilience");
                    Assert.AreEqual(next?plan.Outcome.FutureMeaning:plan.Outcome.Changes[i].Copy(),main.text);
                    Assert.That(main.text,Does.Not.Contain("×"));
                    if(!next)
                    {
                        var resources=plan.Objects.Where(o=>o.Class==RewardObjectClass.Resource).ToArray();
                        var subject=(MaterialReward)typeof(HorizonWorld3D).GetField("activeHeroReward",Private).GetValue(world);
                        Assert.AreEqual(resources[i],subject,"Ordinary action copy must have its own actual resource close-up.");
                        var hero=(Transform)typeof(HorizonWorld3D).GetField("showcaseHero",Private).GetValue(world);
                        Assert.IsTrue(hero.gameObject.activeInHierarchy);
                        Assert.IsTrue(hero.GetComponentsInChildren<Renderer>().Any(r=>r.enabled));
                        AssertSubjectAperture(world,hero,HorizonCinemaLayout.SubjectAperture);
                        Transform actor=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Cinematic player");
                        Assert.IsTrue(actor.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled));
                    }
                    var rendered=main.GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.name=="Cinematic TMP text");
                    Assert.AreEqual("HORIZON/Embossed Outcome SDF",rendered.fontSharedMaterial.shader.name);
                    Assert.AreEqual(main.text,rendered.text); Assert.IsNotNull(main.transform.Find("Embossed text depth"));
                    foreach(Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=5;
                    canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=ui; canvas.planeDistance=5;
                    Canvas.ForceUpdateCanvases(); HorizonPortraitRenderer.Render(world,ui,canvas,target);
                    RenderTexture old=RenderTexture.active; RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,540,960),0,0); pixels.Apply(); RenderTexture.active=old;
                    Directory.CreateDirectory("artifacts/cinematics");
                    File.WriteAllBytes("artifacts/cinematics/"+prefix+"-"+(next?"future":i.ToString())+".png",pixels.EncodeToPNG());
                }
                Assert.AreEqual(new Rect(0,0,1,1),world.WorldCamera.rect);
                world.Cinematics.Skip(); Assert.AreEqual(receipt,JsonUtility.ToJson(source.receipt));
                // Record the complete actual-content sequence too. Static key
                // frames alone cannot prove counter timing or readable holds.
                typeof(HorizonApp).GetMethod("PlayMasterSpectacle",Private).Invoke(app,new object[] {source.Copy(),(Action)(()=>{})});
                world.Cinematics.enabled=false;
                foreach(Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=5;
                canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=ui; canvas.planeDistance=5;
                float duration=world.Cinematics.Current.Duration; int frames=Mathf.CeilToInt(duration*20);
                string lossless="artifacts/cinematics/frames-"+prefix; Directory.CreateDirectory(lossless);
                using(var stream=File.Create("artifacts/cinematics/"+prefix+"-motion.gif"))
                using(var gif=new TimelineGifWriter(stream,540,960))
                    for(int frame=0;frame<frames;frame++)
                    {
                        if(frame>0) world.Cinematics.Advance(duration/frames);
                        yield return null; yield return null;
                        Canvas.ForceUpdateCanvases(); HorizonPortraitRenderer.Render(world,ui,canvas,target);
                        RenderTexture old=RenderTexture.active; RenderTexture.active=target;
                        pixels.ReadPixels(new Rect(0,0,540,960),0,0); pixels.Apply(); RenderTexture.active=old;
                        gif.Frame(pixels.GetPixels32(),5);
                        File.WriteAllBytes(Path.Combine(lossless,"frame-"+frame.ToString("D5")+".png"),pixels.EncodeToPNG());
                    }
                File.WriteAllBytes("artifacts/cinematics/"+prefix+"-motion.pcm",world.RecordedCinemaPcm());
                Assert.AreEqual(receipt,JsonUtility.ToJson(source.receipt));
            }
            finally
            {
                canvas.renderMode=mode; canvas.worldCamera=prior; canvas.planeDistance=distance;
                world.Cinematics.enabled=true; world.Cinematics.CancelAll();
                target.Release(); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(ui.gameObject);
            }
            yield return new ExitPlayMode();
        }
        private static GameSession CreateActualLife()
        {
            // Keep the captured predicate OUT of the EnterPlayMode coroutine.
            // Its display class would otherwise be allocated before the domain
            // reload even though the source declaration follows that yield.
            var life=GameSession.StartMasterLife(1,15,RunMode.Quick);
            CardSpec card=life.Hand.First(c=>life.CanPlay(c) && !life.NeedsImagination(c) && c.Delay>0);
            life.Choose(card.Id); return life;
        }
        private static GameSession CreateWalkLife()
        {
            // Real hand and real Choose, never a fabricated +3 receipt.
            for(int seed=0;seed<1024;seed++)
            {
                var life=GameSession.StartMasterLife(2,seed,RunMode.Quick);
                CardSpec walk=life.Hand.FirstOrDefault(c=>c.Id=="walk" && life.CanPlay(c) && !life.NeedsImagination(c));
                if(walk==null) continue;
                life.Choose(walk.Id); return life;
            }
            Assert.Fail("A recorded walk must come from a playable hand."); return null;
        }
        private static void AssertSubjectAperture(HorizonWorld3D world,Transform subject,Rect aperture)
        {
            foreach(Renderer renderer in subject.GetComponentsInChildren<Renderer>())
            {
                if(!renderer.enabled || renderer is LineRenderer || renderer is ParticleSystemRenderer) continue;
                Bounds bounds=renderer.bounds;
                for(int corner=0;corner<8;corner++)
                {
                    Vector3 p=bounds.center+Vector3.Scale(bounds.extents,new Vector3(
                        (corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                    Vector3 point=world.WorldCamera.WorldToViewportPoint(p);
                    Assert.Greater(point.z,world.WorldCamera.nearClipPlane,renderer.name);
                    Assert.That(point.x,Is.InRange(aperture.xMin-.006f,aperture.xMax+.006f),renderer.name+" horizontal crop");
                    Assert.That(point.y,Is.InRange(aperture.yMin-.006f,aperture.yMax+.006f),renderer.name+" invades the reading bands");
                }
            }
        }
        [UnityTest]
        public IEnumerator MovingRehearsalActorsStayInsideTheirReadingApertureOnBothPhoneShapes()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Portrait subject framing regression").AddComponent<HorizonWorld3D>(); world.Initialize();
            var run=ImaginationEngine.Begin("portrait-framing","portfolio","找工作",1,2);
            try
            {
                for(int step=0;step<20;step++)
                {
                    string before=JsonUtility.ToJson(run);
                    foreach(float aspect in new[] {9f/16,9f/20})
                        foreach(bool reduced in new[] {false,true})
                        {
                            world.WorldCamera.aspect=aspect;
                            world.ApplyPreferences(new PlayerPreferences {sound=false,haptics=false,reducedMotion=reduced});
                            world.ShowImaginationScene(run);
                            Transform actor=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Cinematic player");
                            foreach(float time in new[] {0f,.5f,1.5f,2.9f,3.4f,6.5f,10f})
                            {
                                world.SampleImaginationAt(time);
                                AssertSubjectAperture(world,actor,HorizonCinemaLayout.ImaginationAperture);
                                Vector3 head=world.WorldCamera.WorldToViewportPoint(actor.position+Vector3.up*1.3f);
                                Assert.That(head.x,Is.InRange(.09f,.91f),run.phase+" native focus point");
                            }
                            world.EndImaginationScene();
                        }
                    Assert.AreEqual(before,JsonUtility.ToJson(run));
                    if(run.phase==ImaginePhase.Complete) break;
                    if(run.phase==ImaginePhase.Preparation) ImaginationEngine.Prepare(run,PreparationAction.SmallStep);
                    else if(run.phase==ImaginePhase.Recover) ImaginationEngine.Recover(run,RecoveryAction.Rest);
                    else ImaginationEngine.Continue(run);
                }
                Assert.AreEqual(ImaginePhase.Complete,run.phase);
            }
            finally {world.EndImaginationScene(); Object.Destroy(world.gameObject);}
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator RehearsalUsesTheWholePhoneAndRetainsItsCompleteThought()
        {
            yield return new EnterPlayMode();
            var app=Object.FindObjectOfType<HorizonApp>(); if(app==null) app=new GameObject("Rehearsal outcome app").AddComponent<HorizonApp>();
            yield return null;
            var archive=(ArchiveData)typeof(HorizonApp).GetField("archive",Private).GetValue(app);
            var run=ImaginationEngine.Begin("outcome-rehearsal","portfolio","整理作品",1,2);
            archive.imagination=run;
            typeof(HorizonApp).GetMethod("ShowImagineRun",Private).Invoke(app,null);
            var world=(HorizonWorld3D)typeof(HorizonApp).GetField("world",Private).GetValue(app);
            Assert.AreEqual(new Rect(0,0,1,1),world.WorldCamera.rect);
            world.SampleImaginationAt(3.4f); yield return null; yield return null;
            var root=(RectTransform)typeof(HorizonApp).GetField("root",Private).GetValue(app);
            Text story=root.GetComponentsInChildren<Text>().Single(t=>t.name=="Imagine beat");
            Assert.AreEqual(run.timeline.Last().text,story.text);
            Assert.That(root.GetComponentsInChildren<Text>().Single(t=>t.name=="Imagine resources").text,Does.Contain("预演"));
            Assert.IsFalse(root.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("×")));
            world.SampleImaginationAt(10); yield return null;
            Assert.AreEqual(run.timeline.Last().text,story.text,"An internal camera cut must not replace a half-read thought.");
            yield return new ExitPlayMode();
        }
    }
}
