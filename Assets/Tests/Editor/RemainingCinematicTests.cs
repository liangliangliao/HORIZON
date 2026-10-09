using System;
using System.Collections;
using System.IO;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Horizon.Tests
{
    public sealed class RemainingCinematicTests
    {
        private static Texture2D Capture(Camera camera,string name)
        {
            var target=RenderTexture.GetTemporary(512,384,24,RenderTextureFormat.ARGB32);
            Rect rect=camera.rect; RenderTexture prior=camera.targetTexture,active=RenderTexture.active;
            try
            {
                camera.rect=new Rect(0,0,1,1); camera.targetTexture=target; camera.Render();
                RenderTexture.active=target; var image=new Texture2D(512,384,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,512,384),0,0); image.Apply();
                Directory.CreateDirectory("artifacts/cinematics");
                File.WriteAllBytes("artifacts/cinematics/remaining-"+name+".png",image.EncodeToPNG()); return image;
            }
            finally { camera.rect=rect; camera.targetTexture=prior; RenderTexture.active=active; RenderTexture.ReleaseTemporary(target); }
        }
        [UnityTest]
        public IEnumerator ActualImaginationFramesPersistAndRenderInTheFilmstrip()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Recorded future memory contract").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false });
            var run=ImaginationEngine.Begin("rendered-film","career","参加面试",1,2);
            while(run.phase!=ImaginePhase.Complete)
            {
                world.ShowImaginationScene(run); world.SampleImaginationAt(2.6f); yield return null;
                world.RecordImaginationFrame(run);
                if(run.phase==ImaginePhase.Recover) ImaginationEngine.Recover(run,RecoveryAction.ChangeMethod);
                else if(run.phase==ImaginePhase.Preparation) ImaginationEngine.Prepare(run,PreparationAction.FixedTime);
                else ImaginationEngine.Continue(run);
            }
            Assert.AreEqual(5,run.frames.Count);
            Directory.CreateDirectory("artifacts/cinematics");
            Assert.GreaterOrEqual(run.frames.Select(f=>f.jpeg).Distinct().Count(),4,"Key actions must be different rendered frames.");
            foreach(MemoryFrame frame in run.frames)
            {
                Assert.AreEqual(run.timeline[frame.beatIndex].phase,frame.phase);
                Assert.IsTrue(HorizonWorld3D.IsMemoryJpeg(Convert.FromBase64String(frame.jpeg),128,96));
                File.WriteAllBytes("artifacts/cinematics/memory-"+frame.phase+".jpg",Convert.FromBase64String(frame.jpeg));
            }
            ImagineRun restored=JsonUtility.FromJson<ImagineRun>(JsonUtility.ToJson(run)); restored.Validate();
            Assert.AreEqual("ChangeMethod",restored.memories.Single().frame.actionKey);
            var life=GameSession.StartMasterLife(3,41,RunMode.MirrorRun); life.AttachImagination(restored);
            var reward=life.Master.events.Single(e=>e.kind==DomainEventKind.FutureMemory);
            var collection=new RewardCollection(); collection.Capture(reward,3);
            world.ShowMementoScene(collection.items.Single());
            var cells=world.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Recorded scene ")).ToArray();
            Assert.AreEqual(5,cells.Length); Assert.IsTrue(cells.All(r=>r.sharedMaterial.mainTexture!=null));
            Texture2D film=Capture(world.WorldCamera,"memory-filmstrip"); Object.Destroy(film);
            world.EndImaginationScene(); world.Cinematics.enabled=false; world.Cinematics.Enqueue(reward,true);
            world.Cinematics.Advance(world.Cinematics.Current.Duration*.14f);
            Transform frozen=world.GetComponentsInChildren<Transform>().Single(t=>t.name=="Frozen previsualization frame");
            Assert.IsNotNull(frozen.GetComponent<Renderer>().sharedMaterial.mainTexture);
            film=Capture(world.WorldCamera,"memory-freeze"); Object.Destroy(film);
            world.Cinematics.Skip(); Object.Destroy(world.gameObject); yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator ProjectionMaterializesFiveLayersAndUsesCompiledShaderGraph()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Projected world contract").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false });
            var run=ImaginationEngine.Begin("projection","learning","学习写作",1,2);
            world.ShowImaginationScene(run); world.SampleImaginationAt(.75f);
            var layers=world.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Projection layer ")).ToArray();
            Assert.AreEqual(5,layers.Length); Assert.Less(layers.Count(t=>t.gameObject.activeSelf),5);
            Shader graph=Resources.Load<Shader>("HorizonProjection"); Assert.IsNotNull(graph);
            Assert.IsFalse(ShaderUtil.ShaderHasError(graph));
            bool graphVisible=false;
            foreach(Transform layer in layers) foreach(Renderer renderer in layer.GetComponentsInChildren<Renderer>())
                if(renderer.sharedMaterial.shader==graph) graphVisible=true;
            Assert.IsTrue(graphVisible);
            Texture2D image=Capture(world.WorldCamera,"projecting"); Object.Destroy(image);
            world.SampleImaginationAt(4); Assert.IsTrue(layers.All(t=>t.gameObject.activeSelf && t.localScale.y>.99f));
            image=Capture(world.WorldCamera,"projected-world"); Object.Destroy(image);
            foreach(Transform layer in layers) foreach(Renderer renderer in layer.GetComponentsInChildren<Renderer>())
                Assert.AreNotSame(graph,renderer.sharedMaterial.shader,"Completed layers must regain their solid materials.");
            world.EndImaginationScene(); Assert.IsTrue(layers.All(t=>!t.gameObject.activeInHierarchy));
            Object.Destroy(world.gameObject); yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator DepthFocusChangesRenderedPixelsAndClearsAfterSkipping()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Depth focus contract").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false }); world.Cinematics.enabled=false;
            var reward=new DomainEvent { id="focus-render-contract",kind=DomainEventKind.ActionTaken,tier=RewardTier.Local,
                receipt=new RewardReceipt { resourcesRecorded=true,resources=new ResourceDelta(0,0,3) } };
            world.Cinematics.Enqueue(reward,true);
            HorizonOptics optics=world.WorldCamera.GetComponent<HorizonOptics>();
            Assert.Greater(optics.Defocus,.9f);
            Texture2D blurred=Capture(world.WorldCamera,"focus-blurred"); optics.Clear();
            Texture2D sharp=Capture(world.WorldCamera,"focus-sharp");
            Color32[] a=blurred.GetPixels32(),b=sharp.GetPixels32();
            int changed=0;
            for(int i=0;i<a.Length;i++)
                if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>6) changed++;
            Assert.Greater(changed,100);
            world.Cinematics.Advance(world.Cinematics.Current.Duration*.9f); Assert.Less(optics.Defocus,.01f);
            world.Cinematics.Skip(); Assert.IsFalse(optics.Active);
            Object.Destroy(blurred); Object.Destroy(sharp); Object.Destroy(world.gameObject); yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator SpecializedEffectsUseDifferentGeometryAndSurfaces()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Effect vocabulary contract").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false }); world.Cinematics.enabled=false;
            var receipt=new RewardReceipt { resourcesRecorded=true,resources=new ResourceDelta(5),orbitBits=5,horizonLevel=4 };
            for(int i=0;i<3;i++) receipt.causes.Add(new RewardEvidence { id="effect-fixture-"+i,day=i+1,label="Recorded cause "+i });
            // Rendering fixtures exercise the library; gameplay reachability is
            // separately covered by RealDomainEventsRetainCopyAndRenderDistinctPortraitStages.
            foreach(DomainEventKind kind in new[] { DomainEventKind.TimeEcho,DomainEventKind.PatternBroken,
                DomainEventKind.FailAndAgain,DomainEventKind.OrbitActivated,DomainEventKind.Breakthrough,DomainEventKind.Overdrive,DomainEventKind.RealityConvergence })
            {
                var e=new DomainEvent { id="effect-fixture-"+kind,kind=kind,tier=kind==DomainEventKind.TimeEcho?RewardTier.Major:RewardTier.Epic,receipt=receipt };
                world.Cinematics.Enqueue(e,true);
                float hit=world.Cinematics.Current.Cues.First(c=>!c.NodeHit && c.Phase==CinematicPhase.Impact).Time;
                if(kind==DomainEventKind.PatternBroken)
                {
                    world.Cinematics.Advance(world.Cinematics.Current.Duration*.405f);
                    Assert.IsTrue(world.GetComponentsInChildren<LineRenderer>().Any(l=>l.name.StartsWith("Pattern crack")));
                }
                world.Cinematics.Advance(Mathf.Max(0,hit+.15f-world.Cinematics.Elapsed));
                var lines=world.GetComponentsInChildren<LineRenderer>();
                if(kind==DomainEventKind.TimeEcho)
                {
                    Assert.AreEqual(3,lines.Count(l=>l.name.StartsWith("Energy trail ")));
                    Assert.AreEqual(3,lines.Count(l=>l.name.StartsWith("Time trail ")));
                    Assert.IsTrue(lines.Any(l=>l.name=="Ground shockwave"));
                    Assert.IsTrue(lines.Any(l=>l.name=="Activation halo"));
                    Assert.IsTrue(lines.Any(l=>l.name=="Cause beam 1"));
                }
                if(kind==DomainEventKind.PatternBroken)
                {
                    Assert.Greater(world.WorldCamera.GetComponent<HorizonOptics>().Distortion,0);
                    Assert.IsTrue(world.GetComponentsInChildren<Renderer>().Any(r=>r.name=="Historical interruption" && r.sharedMaterial.shader==Resources.Load<Shader>("HorizonProjection")));
                }
                if(kind==DomainEventKind.FailAndAgain)
                    Assert.AreEqual(8,world.GetComponentsInChildren<Renderer>().Count(r=>r.name.StartsWith("Time fracture ")));
                if(kind==DomainEventKind.OrbitActivated) Assert.AreEqual(2,lines.Count(l=>l.name.StartsWith("Orbit trail ")));
                if(kind==DomainEventKind.Breakthrough)
                {
                    var fluid=world.GetComponentsInChildren<Renderer>().Single(r=>r.name=="Baked reservoir energy");
                    Assert.AreSame(Resources.Load<Texture2D>("HorizonFlowFlipbook"),fluid.sharedMaterial.mainTexture);
                }
                if(kind==DomainEventKind.Overdrive)
                {
                    Assert.AreEqual(3,lines.Count(l=>l.name.StartsWith("Overdrive horizon glow ")));
                    float reveal=world.Cinematics.Current.Cues.First(c=>!c.NodeHit && c.Phase==CinematicPhase.SecondReveal).Time;
                    world.Cinematics.Advance(Mathf.Max(0,reveal+.01f-world.Cinematics.Elapsed));
                    var future=world.GetComponentsInChildren<HorizonActor>().Single(a=>a.name=="Future self cut in");
                    Assert.AreSame(Resources.Load<Shader>("HorizonProjection"),future.Chest.GetComponent<Renderer>().sharedMaterial.shader);
                }
                if(kind==DomainEventKind.RealityConvergence)
                {
                    CollectionAssert.IsSubsetOf(new[] { "IMAGINATION","SIMULATION","REALITY" },lines.Select(l=>l.name));
                    Assert.AreEqual(3,world.GetComponentsInChildren<Renderer>().Count(r=>r.name.StartsWith("Reality impact dust ")));
                }
                Texture2D image=Capture(world.WorldCamera,"effect-"+kind); Object.Destroy(image);
                world.Cinematics.Skip();
                Assert.IsFalse(world.WorldCamera.GetComponent<HorizonOptics>().Active);
            }
            Object.Destroy(world.gameObject); yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator BakedEffectsLodAndBodyBreathingRespectPauseAndReduction()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Mobile effect resources contract").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false }); world.Cinematics.enabled=false;
            foreach(string name in new[] { "HorizonDustFlipbook","HorizonFlowFlipbook" })
            { Texture2D atlas=Resources.Load<Texture2D>(name); Assert.IsNotNull(atlas); Assert.AreEqual(256,atlas.width); }
            LODGroup lod=world.Avatar.GetComponent<LODGroup>(); Assert.AreEqual(3,lod.lodCount);
            Assert.Less(lod.GetLODs()[2].renderers.Length,lod.GetLODs()[0].renderers.Length/2);
            HorizonActor actor=world.Avatar.GetComponent<HorizonActor>();
            var performance=HorizonActorPerformance.Attach(actor);
            performance.Play(HorizonBodyAction.Rest,.1f); Vector3 early=actor.Chest.localScale;
            performance.Play(HorizonBodyAction.Rest,.8f); Assert.Greater(Vector3.Distance(early,actor.Chest.localScale),.001f);
            performance.Release();
            DomainEvent reality=CinematicEventCoverageTests.RealEvents().Single(e=>e.kind==DomainEventKind.RealityConvergence);
            world.Cinematics.Enqueue(reality,true);
            float impact=world.Cinematics.Current.Cues.First(c=>!c.NodeHit && c.Phase==CinematicPhase.Impact).Time;
            world.Cinematics.Advance(impact+.15f);
            var dust=world.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Reality impact dust ")).ToArray(); Assert.AreEqual(3,dust.Length);
            var block=new MaterialPropertyBlock(); dust[0].GetPropertyBlock(block); float frame=block.GetFloat("_Frame");
            world.SetPaused(true); world.Cinematics.Advance(.3f); dust[0].GetPropertyBlock(block); Assert.AreEqual(frame,block.GetFloat("_Frame"));
            world.SetPaused(false); world.Cinematics.Skip();
            world.ApplyPreferences(new PlayerPreferences { reducedMotion=true,batterySaver=true,sound=false,haptics=false });
            world.Cinematics.Enqueue(reality,true); world.Cinematics.Advance(impact+.15f);
            Assert.IsFalse(world.WorldCamera.GetComponent<HorizonOptics>().Active);
            Assert.IsFalse(world.GetComponentsInChildren<Renderer>().Any(r=>r.name.StartsWith("Reality impact dust ")));
            Assert.AreEqual(30,Application.targetFrameRate);
            world.Cinematics.Skip(); Object.Destroy(world.gameObject); yield return new ExitPlayMode();
        }
    }
}
