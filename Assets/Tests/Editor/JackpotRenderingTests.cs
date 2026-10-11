using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Horizon.Tests
{
    public sealed class JackpotRenderingTests
    {
        [Test]
        public void BakedGoldDigitsHaveClosedDepthAndFiniteVertices()
        {
            var meshes=new List<Mesh>(); var digits=new HorizonRewardDigits(mesh=>meshes.Add(mesh));
            try
            {
                foreach(char value in "×0123456789")
                {
                    Mesh mesh=digits.Mesh(value); Assert.IsNotNull(mesh,value.ToString());
                    Assert.Greater(mesh.bounds.size.z,.16f); Assert.Greater(mesh.triangles.Length,12);
                    Assert.IsTrue(mesh.vertices.All(v=>!float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)));
                    Assert.IsTrue(mesh.triangles.All(i=>i>=0 && i<mesh.vertexCount));
                }
            }
            finally { foreach(Mesh mesh in meshes) Object.DestroyImmediate(mesh); }
        }

        [UnityTest]
        public IEnumerator RealEventsRenderGoldTreasureAndMysteryWithPauseQualityAndReplayIsolation()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Jackpot rendering verification").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false }); world.Cinematics.enabled=false;
            List<DomainEvent> events=CinematicEventCoverageTests.RealEvents();
            DomainEvent[] subjects={events.First(e=>e.kind==DomainEventKind.PatternBroken),events.First(e=>e.kind==DomainEventKind.RealityConvergence),events.First(e=>e.kind==DomainEventKind.Surprise)};
            string[] files={"jackpot-pattern-gold","jackpot-convergence-gold","jackpot-mystery-violet"};
            try
            {
                for(int i=0;i<subjects.Length;i++)
                {
                    string before=JsonUtility.ToJson(subjects[i]);
                    Assert.IsTrue(world.Cinematics.Enqueue(subjects[i],true));
                    var visual=new RewardSpectacle(world.Cinematics.Current);
                    world.Cinematics.Advance(visual.HeroTime+.65f); yield return null;
                    Transform stage=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Pooled 3D reward spectacle");
                    Assert.IsTrue(stage.gameObject.activeInHierarchy);
                    Transform chest=stage.Find("Gold and crimson reward chest");
                    Assert.AreEqual(visual.Style!=RewardSpectacleStyle.Mystery,chest.gameObject.activeInHierarchy);
                    Transform lid=chest.Find("Treasure lid hinge"); Quaternion angle=lid.localRotation;
                    world.SetPaused(true); world.Cinematics.Advance(2); yield return null;
                    Assert.AreEqual(angle,lid.localRotation); world.SetPaused(false);
                    Capture(world,files[i]);
                    world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false,batterySaver=true });
                    world.Cinematics.Advance(.01f);
                    Assert.LessOrEqual(stage.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Decorative embossed gold coin")),8);
                    Capture(world,files[i]+"-battery");
                    world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false,reducedMotion=true });
                    world.Cinematics.Advance(.01f);
                    Assert.IsFalse(stage.GetComponentsInChildren<Transform>().Any(t=>t.name.StartsWith("Decorative embossed gold coin")));
                    Assert.Greater(Quaternion.Angle(Quaternion.identity,lid.localRotation),30);
                    Capture(world,files[i]+"-reduced");
                    world.Cinematics.Skip(); Assert.IsFalse(stage.gameObject.activeInHierarchy);
                    Assert.AreEqual(before,JsonUtility.ToJson(subjects[i]));
                    Assert.IsTrue(world.Cinematics.Enqueue(subjects[i],true)); world.Cinematics.Skip();
                    Assert.AreEqual(before,JsonUtility.ToJson(subjects[i]));
                    world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false });
                }
            }
            finally { world.Cinematics.CancelAll(); Object.Destroy(world.gameObject); }
            yield return new ExitPlayMode();
        }

        private static void Capture(HorizonWorld3D world,string name)
        {
            Camera camera=world.WorldCamera;
            var target=RenderTexture.GetTemporary(540,960,24,RenderTextureFormat.ARGB32);
            Rect rect=camera.rect; RenderTexture previous=camera.targetTexture,active=RenderTexture.active;
            Texture2D pixels=null;
            try
            {
                camera.rect=new Rect(0,0,1,1); camera.targetTexture=target; world.FrameCinematicLabels(); camera.Render();
                RenderTexture.active=target; pixels=new Texture2D(540,960,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,540,960),0,0); pixels.Apply();
                // Catch black render targets and missing GLES-compatible visual surfaces.
                Color32[] colors=pixels.GetPixels32(); Assert.Greater(colors.Count(c=>c.r>55 || c.g>55 || c.b>55),colors.Length/30);
                Directory.CreateDirectory("artifacts/cinematics"); File.WriteAllBytes("artifacts/cinematics/"+name+".png",pixels.EncodeToPNG());
            }
            finally
            { camera.rect=rect; camera.targetTexture=previous; RenderTexture.active=active; world.FrameCinematicLabels(); RenderTexture.ReleaseTemporary(target); if(pixels!=null) Object.Destroy(pixels); }
        }

        [UnityTest]
        public IEnumerator GoldAndVioletMoviesExportFromTheActualUnityCamera()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Animated jackpot preview recorder").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false }); world.Cinematics.enabled=false;
            List<DomainEvent> events=CinematicEventCoverageTests.RealEvents();
            DomainEvent[] subjects={events.First(e=>e.kind==DomainEventKind.PatternBroken),events.First(e=>e.kind==DomainEventKind.Surprise)};
            string[] names={"jackpot-gold-motion","jackpot-violet-motion"};
            var target=RenderTexture.GetTemporary(360,640,24,RenderTextureFormat.ARGB32);
            var pixels=new Texture2D(360,640,TextureFormat.RGB24,false);
            Camera camera=world.WorldCamera; Rect rect=camera.rect; RenderTexture prior=camera.targetTexture,active=RenderTexture.active;
            Directory.CreateDirectory("artifacts/cinematics");
            try
            {
                for(int movie=0;movie<subjects.Length;movie++)
                {
                    Assert.IsTrue(world.Cinematics.Enqueue(subjects[movie],true));
                    float duration=world.Cinematics.Current.Duration;
                    int frames=Mathf.CeilToInt(duration*20);
                    using(var stream=File.Create("artifacts/cinematics/"+names[movie]+".gif"))
                    using(var gif=new TimelineGifWriter(stream,360,640))
                    {
                        for(int frame=0;frame<frames;frame++)
                        {
                            if(frame>0) world.Cinematics.Advance(duration/frames);
                            camera.rect=new Rect(0,0,1,1); camera.targetTexture=target;
                            world.FrameCinematicLabels(); camera.Render(); RenderTexture.active=target;
                            pixels.ReadPixels(new Rect(0,0,360,640),0,0); pixels.Apply(); gif.Frame(pixels.GetPixels32(),5);
                            camera.rect=rect; camera.targetTexture=prior; world.FrameCinematicLabels();
                            yield return null;
                        }
                    }
                    world.Cinematics.Skip();
                }
            }
            finally
            { camera.rect=rect; camera.targetTexture=prior; RenderTexture.active=active; RenderTexture.ReleaseTemporary(target); Object.Destroy(pixels); world.Cinematics.CancelAll(); Object.Destroy(world.gameObject); }
            yield return new ExitPlayMode();
        }
    }
}
