using System;
using System.Collections;
using System.IO;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Horizon.Tests
{
    public sealed class ImmersionRenderingTests
    {
        [Test]
        public void ImmersionShaderImportsForTheActualPipeline()
        {
            Shader shader=Resources.Load<Shader>("HorizonImmersion");
            Assert.IsNotNull(shader); Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
        }

        [UnityTest]
        public IEnumerator EveryEventRendersAndRestoresWithDeterministicDepthAndReducedMotion()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Immersion event coverage").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences {sound=false,haptics=false}); world.Cinematics.enabled=false;
            var actual=CinematicEventCoverageTests.RealEvents();
            try
            {
                foreach(DomainEventKind kind in Enum.GetValues(typeof(DomainEventKind)))
                {
                    DomainEvent source=actual.FirstOrDefault(e=>e.kind==kind)?.Copy() ?? new DomainEvent {
                        id="preview-fixture-"+kind,kind=kind,tier=RewardTier.Major,title=kind.ToString(),
                        receipt=new RewardReceipt {resourcesRecorded=true,sourceDay=2,resources=new ResourceDelta(3)} };
                    string before=JsonUtility.ToJson(source);
                    world.Cinematics.Enqueue(source,true);
                    var plan=world.Cinematics.Current; var visual=new RewardSpectacle(plan);
                    float at=Mathf.Min(plan.Duration*.9f,visual.HeroTime+.25f);
                    world.Cinematics.Advance(at); yield return null;
                    Transform stage=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="HORIZON inward release depth field");
                    if(stage.gameObject.activeInHierarchy)
                    {
                        var renderer=stage.GetComponentsInChildren<Renderer>().First();
                        float clock=renderer.sharedMaterial.GetFloat("_Clock"); Vector3 position=renderer.transform.position;
                        world.Cinematics.SetPaused(true); world.Cinematics.Advance(1); yield return null;
                        Assert.AreEqual(clock,renderer.sharedMaterial.GetFloat("_Clock"));
                        Assert.AreEqual(position,renderer.transform.position); world.Cinematics.SetPaused(false);
                    }
                    Capture(world,kind+"-"+(source.id.StartsWith("preview-fixture")?"fixture":"actual"));
                    world.ApplyPreferences(new PlayerPreferences {sound=false,haptics=false,batterySaver=true});
                    Assert.LessOrEqual(stage.GetComponentsInChildren<Renderer>().Count(r=>r.gameObject.activeInHierarchy),1);
                    world.ApplyPreferences(new PlayerPreferences {sound=false,haptics=false,reducedMotion=true});
                    Assert.IsFalse(stage.gameObject.activeInHierarchy,"Reduced motion must remove moving optical membranes.");
                    world.Cinematics.Skip(); Assert.IsFalse(stage.gameObject.activeInHierarchy);
                    Assert.AreEqual(before,JsonUtility.ToJson(source));
                    world.ApplyPreferences(new PlayerPreferences {sound=false,haptics=false});
                }
            }
            finally { world.Cinematics.CancelAll(); Object.Destroy(world.gameObject); }
            yield return new ExitPlayMode();
        }

        private static void Capture(HorizonWorld3D world,string name)
        {
            Camera camera=world.WorldCamera; Rect viewport=camera.rect;
            RenderTexture prior=camera.targetTexture,active=RenderTexture.active;
            var target=RenderTexture.GetTemporary(540,960,24,RenderTextureFormat.ARGB32);
            Texture2D pixels=null;
            try
            {
                camera.rect=new Rect(0,0,1,1); camera.targetTexture=target;
                world.FrameCinematicLabels(); camera.Render(); RenderTexture.active=target;
                pixels=new Texture2D(540,960,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,540,960),0,0); pixels.Apply();
                Assert.Greater(pixels.GetPixels32().Count(p=>p.r>40 || p.g>40 || p.b>40),540*960/100);
                Directory.CreateDirectory("artifacts/cinematics/immersion");
                File.WriteAllBytes("artifacts/cinematics/immersion/"+name+".png",pixels.EncodeToPNG());
            }
            finally
            {
                camera.rect=viewport; camera.targetTexture=prior; RenderTexture.active=active;
                world.FrameCinematicLabels(); RenderTexture.ReleaseTemporary(target);
                if(pixels!=null) Object.Destroy(pixels);
            }
        }
    }
}
