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
            Assert.IsTrue(layers.SelectMany(t=>t.GetComponentsInChildren<Renderer>()).Any(r=>r.sharedMaterial.shader==graph));
            Texture2D image=Capture(world.WorldCamera,"projecting"); Object.Destroy(image);
            world.SampleImaginationAt(4); Assert.IsTrue(layers.All(t=>t.gameObject.activeSelf && t.localScale.y>.99f));
            image=Capture(world.WorldCamera,"projected-world"); Object.Destroy(image);
            Assert.IsFalse(layers.SelectMany(t=>t.GetComponentsInChildren<Renderer>()).Any(r=>r.sharedMaterial.shader==graph),"Completed layers must regain their solid materials.");
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
            Assert.Greater(a.Where((c,i)=>Math.Abs(c.r-b[i].r)+Math.Abs(c.g-b[i].g)+Math.Abs(c.b-b[i].b)>6).Count(),100);
            world.Cinematics.Advance(world.Cinematics.Current.Duration*.9f); Assert.Less(optics.Defocus,.01f);
            world.Cinematics.Skip(); Assert.IsFalse(optics.Active);
            Object.Destroy(blurred); Object.Destroy(sharp); Object.Destroy(world.gameObject); yield return new ExitPlayMode();
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
