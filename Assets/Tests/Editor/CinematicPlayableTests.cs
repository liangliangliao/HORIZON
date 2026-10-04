using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Horizon.Tests
{
    public sealed class CinematicPlayableTests
    {
        // EnterPlayMode reloads the test's iterator. Compiler-generated local
        // closures are not restored; create an explicit probe after that yield.
        private sealed class CueProbe
        {
            public int Completions, Impacts;
            public readonly List<CinematicPhase> Phases = new List<CinematicPhase>();
            public void Completed() { Completions++; }
            public void Cue(CinematicCue cue)
            {
                if (cue.NodeHit) return;
                Phases.Add(cue.Phase);
                if (cue.Phase == CinematicPhase.Impact) Impacts++;
            }
        }
        private static DomainEvent Pattern()
        {
            var model=new PlayerBehavioralModel(); model.Observe(new[] {
                new BehaviorObservation { id="a",run=1,day=1,key="decision-reopen",nodeId="first" },
                new BehaviorObservation { id="b",run=2,day=1,key="decision-reopen",nodeId="second" } });
            var life=GameSession.StartMasterLife(3,41,RunMode.MirrorRun,model); life.LockDecision(life.Hand[1].Id);
            for(int i=0;i<4;i++) life.ExecuteDecisionStep(); life.Choose(life.Master.decision.cardId);
            return life.Master.events.Single(x=>x.kind==DomainEventKind.PatternBroken);
        }
        [UnityTest]
        public IEnumerator TransparentMasterPagesHideHomeAndRestoreItAfterRepeatedNavigation()
        {
            yield return new EnterPlayMode();
            yield return null;
            var app=Object.FindObjectOfType<HorizonApp>();
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var life=GameSession.StartMasterLife(2,41,RunMode.Quick);
            var archive=new ArchiveData { active=life.Snapshot(),seenSecondLife=true,nextRareRun=99 };
            archive.Repair(); archive.preferences.sound=false; archive.preferences.haptics=false;
            typeof(HorizonApp).GetField("archive",flags).SetValue(app,archive);
            typeof(HorizonApp).GetField("session",flags).SetValue(app,life);
            typeof(HorizonApp).GetMethod("ApplyPreferences",flags).Invoke(app,null);
            typeof(HorizonApp).GetMethod("ShowHome",flags).Invoke(app,null);
            yield return null;
            var root=(RectTransform)typeof(HorizonApp).GetField("root",flags).GetValue(app);
            var world=(HorizonWorld3D)typeof(HorizonApp).GetField("world",flags).GetValue(app);
            GameObject[] home=root.Cast<Transform>().Where(t=>t.gameObject.activeSelf).Select(t=>t.gameObject).ToArray();
            Rect original=world.WorldCamera.rect;
            for(int visit=0;visit<2;visit++)
            {
                typeof(HorizonApp).GetMethod("ShowImagineSetup",flags).Invoke(app,null);
                root.GetComponentsInChildren<Button>().Single(b=>b.name==
                    (archive.imagination==null?"Start imagination":"Resume imagination")).onClick.Invoke();
                yield return null;
                for(int step=0;step<16;step++)
                {
                    RectTransform page=(RectTransform)typeof(HorizonApp).GetField("overlay",flags).GetValue(app);
                    Assert.AreEqual("Imagination run",page.name);
                    Assert.IsTrue(page.gameObject.activeInHierarchy);
                    Assert.IsTrue(home.All(g=>g!=null && !g.activeInHierarchy),"A transparent movie page must hide the home buttons and headings.");
                    Assert.AreEqual(1,root.Cast<Transform>().Count(t=>t.gameObject.activeInHierarchy));
                    Assert.IsTrue(world.WorldCamera.enabled);
                    if(archive.imagination.phase==ImaginePhase.Complete) break;
                    Button action=page.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.interactable &&
                        (b.name=="Continue imagination" || b.name=="Preparation action 0" || b.name=="Recovery action 0"));
                    Assert.IsNotNull(action); action.onClick.Invoke(); yield return null;
                }
                Assert.AreEqual(ImaginePhase.Complete,archive.imagination.phase);
                typeof(HorizonApp).GetMethod("CloseMasterPage",flags).Invoke(app,null);
                yield return null;
                Assert.IsTrue(home.All(g=>g.activeInHierarchy)); Assert.AreEqual(original,world.WorldCamera.rect);
                Assert.IsNotNull(root.GetComponentsInChildren<Button>().Single(b=>b.name=="Continue"));
            }
            typeof(HorizonApp).GetMethod("ShowSettings",flags).Invoke(app,null);
            yield return null;
            Vector3 position=world.WorldCamera.transform.position;
            Assert.IsTrue(VisualPreferences.Paused); Assert.IsTrue(world.WorldCamera.enabled);
            yield return null; Assert.AreEqual(position,world.WorldCamera.transform.position);
            typeof(HorizonApp).GetMethod("CloseSettings",flags).Invoke(app,null);
            yield return null;
            Assert.IsTrue(home.All(g=>g.activeInHierarchy)); Assert.IsFalse(VisualPreferences.Paused);
            Assert.AreEqual(original,world.WorldCamera.rect);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator PausingSkippingAndCancellingRestoreTheCameraAndLeaveRulesUntouched()
        {
            yield return new EnterPlayMode();
            // Let the app's Start finish before manually advancing its timeline.
            yield return null;
            var world=new GameObject("Cinematic contract world").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false,reducedMotion=true,batterySaver=true });
            world.ShowBoard(); world.SnapCamera(); Rect rect=world.WorldCamera.rect; float fov=world.WorldCamera.fieldOfView;
            DomainEvent e=Pattern(); string before=JsonUtility.ToJson(e); var probe=new CueProbe();
            var director=world.Cinematics; director.enabled=false;
            director.Cue+=probe.Cue;
            Assert.IsTrue(director.Enqueue(e,true,probe.Completed)); Assert.IsFalse(director.Enqueue(e,true,probe.Completed));
            float stop=director.Current.Cues.Single(c=>c.Phase==CinematicPhase.HitStop).Time;
            director.Advance(stop); Vector3 position=world.WorldCamera.transform.position;
            director.SetPaused(true);
            var actor=world.GetComponentsInChildren<HorizonActor>().Single(x=>x.name=="Cinematic player");
            Quaternion head=actor.Head.localRotation; yield return null; Assert.AreEqual(head,actor.Head.localRotation);
            director.Advance(10); Assert.AreEqual(stop,director.Elapsed);
            Assert.AreEqual(position,world.WorldCamera.transform.position); Assert.AreEqual(0,probe.Impacts);
            director.SetPaused(false); director.Advance(.5f); Assert.AreEqual(1,probe.Impacts);
            director.Skip(); director.Skip(); Assert.AreEqual(1,probe.Completions); Assert.AreEqual(rect,world.WorldCamera.rect); Assert.AreEqual(fov,world.WorldCamera.fieldOfView);
            Assert.AreEqual(before,JsonUtility.ToJson(e));
            Assert.IsTrue(director.Enqueue(e,true,probe.Completed)); director.CancelAll(); Assert.AreEqual(1,probe.Completions); Assert.AreEqual(rect,world.WorldCamera.rect);
            director.SetPaused(true); Assert.IsTrue(director.Enqueue(e,true)); Assert.AreEqual(0,actor.MotionRate);
            director.CancelAll(); director.SetPaused(false);
            Object.Destroy(world.gameObject); yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator LowQualityKeepsAllShotsObjectsAndCopyWithBoundedParticles()
        {
            yield return new EnterPlayMode();
            yield return null;
            var world=new GameObject("Low quality cinema").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false,batterySaver=true });
            DomainEvent e=Pattern(); var director=world.Cinematics; director.enabled=false;
            var probe=new CueProbe(); director.Cue+=probe.Cue;
            director.Enqueue(e,true); float impact=director.Current.Cues.Single(c=>c.Phase==CinematicPhase.Impact).Time;
            director.Advance(impact); Assert.LessOrEqual(world.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount),12);
            Assert.AreEqual(2,world.GetComponentsInChildren<LineRenderer>().Count(x=>x.name.Contains("曾在这里停下")));
            Assert.IsTrue(world.GetComponentsInChildren<HorizonActor>().Any(x=>x.name=="Cinematic player"));
            director.Advance(20); CollectionAssert.AreEqual(Enum.GetValues(typeof(CinematicPhase)),probe.Phases);
            director.Enqueue(new DomainEvent { id="reservoir-reuse",kind=DomainEventKind.Breakthrough,tier=RewardTier.Major }); director.Advance(20);
            world.ShowReservoirScene(new List<InvestmentPool>());
            Transform core=world.GetComponentsInChildren<Transform>().Single(x=>x.name==RewardObjectKind.ReservoirCore.ToString());
            Assert.AreEqual(.55f,core.Find("Storage lid").localPosition.y,.001f,"Inspecting saved energy must not inherit an open reward core.");
            Object.Destroy(world.gameObject); yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator RealPatternAndConvergenceProducePortraitFramesAndPersistentMementos()
        {
            yield return new EnterPlayMode();
            var app=Object.FindObjectOfType<HorizonApp>(); if(app==null) app=new GameObject("Cinematic portrait app").AddComponent<HorizonApp>();
            yield return null;
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var world=(HorizonWorld3D)typeof(HorizonApp).GetField("world",flags).GetValue(app);
            var root=(RectTransform)typeof(HorizonApp).GetField("root",flags).GetValue(app); Canvas canvas=root.GetComponentInParent<Canvas>();
            var archive=(ArchiveData)typeof(HorizonApp).GetField("archive",flags).GetValue(app); archive.preferences.sound=false; archive.preferences.haptics=false;
            typeof(HorizonApp).GetMethod("ApplyPreferences",flags).Invoke(app,null);
            var reality=new RealityConstellation(); DateTime today=new DateTime(2026,10,4);
            var memory=new FutureMemory { id="film",goalId="help",imaginationRun=1,imaginationNodeId="imagine",simulationRun=2,simulationNodeId="again",simulationDay=3,text="失败后，我请求帮助。" };
            var quest=reality.Offer(today,"help","现实中请求一次帮助",memory.id); Assert.IsTrue(reality.Complete(quest.id,today,new[] { memory }));
            DomainEvent[] events={Pattern(),reality.events.Last()}; string[] names={"90-pattern-3d","91-reality-convergence-3d"};
            var saved=new ArchiveData();
            var image=new RenderTexture(540,960,24); image.Create(); var pixels=new Texture2D(540,960,TextureFormat.RGB24,false);
            Camera ui=new GameObject("Cinematic portrait camera",typeof(Camera)).GetComponent<Camera>(); ui.enabled=false; ui.cullingMask=1<<5; ui.nearClipPlane=.1f; ui.farClipPlane=20; ui.targetTexture=image;
            RenderMode mode=canvas.renderMode; Camera old=canvas.worldCamera; float distance=canvas.planeDistance;
            string directory=Path.Combine(Directory.GetCurrentDirectory(),"artifacts","visuals"); Directory.CreateDirectory(directory);
            try
            {
                for(int i=0;i<events.Length;i++)
                {
                    typeof(HorizonApp).GetMethod("PlayMasterSpectacle",flags).Invoke(app,new object[] { events[i],(Action)(()=>{}) });
                    world.Cinematics.enabled=false; float reveal=world.Cinematics.Current.Cues.Single(c=>c.Phase==CinematicPhase.SecondReveal).Time;
                    world.Cinematics.Advance(reveal+.1f); yield return null;
                    foreach(Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=5;
                    canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=ui; canvas.planeDistance=5;
                    Canvas.ForceUpdateCanvases(); HorizonPortraitRenderer.Render(world,ui,canvas,image);
                    RenderTexture previous=RenderTexture.active; RenderTexture.active=image; pixels.ReadPixels(new Rect(0,0,540,960),0,0); pixels.Apply(); RenderTexture.active=previous;
                    File.WriteAllBytes(Path.Combine(directory,names[i]+".png"),pixels.EncodeToPNG());
                    Assert.IsNotEmpty(root.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Reward story").text);
                    Assert.IsTrue(saved.rewardCollection.Capture(events[i],i==0?3:0)); Assert.IsFalse(saved.rewardCollection.Capture(events[i],i==0?3:0));
                    world.Cinematics.Skip(); Assert.IsTrue(events[i].acknowledged);
                }
                var restored=JsonUtility.FromJson<ArchiveData>(JsonUtility.ToJson(saved)); restored.RepairMasterArchive();
                Assert.AreEqual(2,restored.rewardCollection.items.Count);
            }
            finally
            { canvas.renderMode=mode; canvas.worldCamera=old; canvas.planeDistance=distance; world.Cinematics.enabled=true; world.Cinematics.CancelAll(); image.Release(); Object.Destroy(image); Object.Destroy(pixels); Object.Destroy(ui.gameObject); }
            yield return new ExitPlayMode();
        }
    }
}
