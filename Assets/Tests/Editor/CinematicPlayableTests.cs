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
        private static HorizonWorld3D LiveWorld()
        {
            var app=Object.FindObjectOfType<HorizonApp>();
            if(app==null) app=new GameObject("Cinematic contract app").AddComponent<HorizonApp>();
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var world=(HorizonWorld3D)typeof(HorizonApp).GetField("world",flags).GetValue(app);
            Assert.IsNotNull(world,"The running app must create its 3D world.");
            Assert.IsNotNull(world.WorldCamera);
            Assert.IsNotNull(world.Cinematics);
            return world;
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
        public IEnumerator PausingSkippingAndCancellingRestoreTheCameraAndLeaveRulesUntouched()
        {
            yield return new EnterPlayMode();
            // Let the app's Start finish before manually advancing its timeline.
            yield return null;
            var world=LiveWorld();
            world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false,reducedMotion=true,batterySaver=true });
            world.ShowBoard(); world.SnapCamera(); Rect rect=world.WorldCamera.rect; float fov=world.WorldCamera.fieldOfView;
            DomainEvent e=Pattern(); string before=JsonUtility.ToJson(e); int completed=0,impacts=0;
            var director=world.Cinematics; director.enabled=false;
            director.Cue+=cue=> { if(cue.Phase==CinematicPhase.Impact && !cue.NodeHit) impacts++; };
            Assert.IsTrue(director.Enqueue(e,true,()=>completed++)); Assert.IsFalse(director.Enqueue(e,true,()=>completed++));
            float stop=director.Current.Cues.Single(c=>c.Phase==CinematicPhase.HitStop).Time;
            director.Advance(stop); Vector3 position=world.WorldCamera.transform.position;
            director.SetPaused(true);
            var actor=world.GetComponentsInChildren<HorizonActor>().Single(x=>x.name=="Cinematic player");
            Quaternion head=actor.Head.localRotation; yield return null; Assert.AreEqual(head,actor.Head.localRotation);
            director.Advance(10); Assert.AreEqual(stop,director.Elapsed);
            Assert.AreEqual(position,world.WorldCamera.transform.position); Assert.AreEqual(0,impacts);
            director.SetPaused(false); director.Advance(.5f); Assert.AreEqual(1,impacts);
            director.Skip(); director.Skip(); Assert.AreEqual(1,completed); Assert.AreEqual(rect,world.WorldCamera.rect); Assert.AreEqual(fov,world.WorldCamera.fieldOfView);
            Assert.AreEqual(before,JsonUtility.ToJson(e));
            Assert.IsTrue(director.Enqueue(e,true,()=>completed++)); director.CancelAll(); Assert.AreEqual(1,completed); Assert.AreEqual(rect,world.WorldCamera.rect);
            director.SetPaused(true); Assert.IsTrue(director.Enqueue(e,true)); Assert.AreEqual(0,actor.MotionRate);
            director.CancelAll(); director.SetPaused(false);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator LowQualityKeepsAllShotsObjectsAndCopyWithBoundedParticles()
        {
            yield return new EnterPlayMode();
            yield return null;
            var world=LiveWorld();
            world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false,batterySaver=true });
            DomainEvent e=Pattern(); var director=world.Cinematics; director.enabled=false;
            var phases=new List<CinematicPhase>(); director.Cue+=cue=> { if(!cue.NodeHit) phases.Add(cue.Phase); };
            director.Enqueue(e,true); float impact=director.Current.Cues.Single(c=>c.Phase==CinematicPhase.Impact).Time;
            director.Advance(impact); Assert.LessOrEqual(world.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount),12);
            Assert.AreEqual(2,world.GetComponentsInChildren<LineRenderer>().Count(x=>x.name.Contains("曾在这里停下")));
            Assert.IsTrue(world.GetComponentsInChildren<HorizonActor>().Any(x=>x.name=="Cinematic player"));
            director.Advance(20); CollectionAssert.AreEqual(Enum.GetValues(typeof(CinematicPhase)),phases);
            director.Enqueue(new DomainEvent { id="reservoir-reuse",kind=DomainEventKind.Breakthrough,tier=RewardTier.Major }); director.Advance(20);
            world.ShowReservoirScene(new List<InvestmentPool>());
            Transform core=world.GetComponentsInChildren<Transform>().Single(x=>x.name==RewardObjectKind.ReservoirCore.ToString());
            Assert.AreEqual(.55f,core.Find("Storage lid").localPosition.y,.001f,"Inspecting saved energy must not inherit an open reward core.");
            yield return new ExitPlayMode();
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
