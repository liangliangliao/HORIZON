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
        public IEnumerator RealEventsRenderTheirOwnMementosWithPauseQualityAndReplayIsolation()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Jackpot rendering verification").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false }); world.Cinematics.enabled=false;
            List<DomainEvent> events=CinematicEventCoverageTests.RealEvents();
            DomainEvent[] subjects={events.First(e=>e.kind==DomainEventKind.PatternBroken),events.First(e=>e.kind==DomainEventKind.RealityConvergence),events.First(e=>e.kind==DomainEventKind.Surprise)};
            string[] files={"showcase-pattern-link","showcase-convergence-prism","showcase-insight-prism"};
            try
            {
                for(int i=0;i<subjects.Length;i++)
                {
                    string before=JsonUtility.ToJson(subjects[i]);
                    Assert.IsTrue(world.Cinematics.Enqueue(subjects[i],true));
                    Transform actor=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Cinematic player");
                    var originalRenderers=actor.GetComponentsInChildren<Renderer>(true).ToDictionary(r=>r,r=>r.enabled);
                    var visual=new RewardSpectacle(world.Cinematics.Current);
                    world.Cinematics.Advance(visual.HeroTime+.65f); yield return null;
                    Transform stage=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Pooled 3D reward spectacle");
                    Assert.IsTrue(stage.gameObject.activeInHierarchy);
                    Transform asset=stage.Find("Reward showcase "+visual.HeroReward.Kind);
                    Assert.IsNotNull(asset); Assert.IsTrue(asset.gameObject.activeInHierarchy);
                    Assert.IsTrue(actor.gameObject.activeInHierarchy,"The narrative actor must retain its state for subsequent shots.");
                    Assert.IsTrue(actor.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled),
                        "The completed showcase shot must not draw the narrative actor in front of the actual asset.");
                    Quaternion angle=asset.localRotation; Vector3 position=asset.localPosition;
                    world.SetPaused(true); world.Cinematics.Advance(2); yield return null;
                    Assert.AreEqual(angle,asset.localRotation); Assert.AreEqual(position,asset.localPosition); world.SetPaused(false);
                    AssertPortraitSafeArea(world,stage,asset);
                    Capture(world,files[i]);
                    world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false,batterySaver=true });
                    world.Cinematics.Advance(.01f);
                    Assert.LessOrEqual(stage.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Decorative reward")),8);
                    Capture(world,files[i]+"-battery");
                    world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false,reducedMotion=true });
                    world.Cinematics.Advance(.01f);
                    Assert.IsFalse(stage.GetComponentsInChildren<Transform>().Any(t=>t.name.StartsWith("Decorative reward")));
                    Assert.AreEqual(Quaternion.Euler(0,-15,0),asset.localRotation);
                    Capture(world,files[i]+"-reduced");
                    world.Cinematics.Skip(); Assert.IsFalse(stage.gameObject.activeInHierarchy);
                    foreach(var state in originalRenderers) Assert.AreEqual(state.Value,state.Key.enabled,"Leaving a showcase must restore the original narrative renderer state.");
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

        private static void AssertPortraitSafeArea(HorizonWorld3D world,Transform stage,Transform asset)
        {
            Camera camera=world.WorldCamera; Rect viewport=camera.rect;
            RenderTexture prior=camera.targetTexture;
            var target=RenderTexture.GetTemporary(540,Mathf.RoundToInt(960*viewport.height),24);
            try
            {
                camera.rect=new Rect(0,0,1,1); camera.targetTexture=target; world.FrameCinematicLabels();
                Transform plaque=stage.Find("3D presentation multiplier plaque");
                Assert.IsFalse(plaque.gameObject.activeSelf,"A decorative multiplier must not replace the actual outcome.");
                Assert.IsEmpty(plaque.GetComponentsInChildren<Renderer>(true),"Do not merely hide old digits under new text.");
                Bounds hero=asset.GetComponentsInChildren<Renderer>().First().bounds;
                foreach(Renderer renderer in asset.GetComponentsInChildren<Renderer>()) hero.Encapsulate(renderer.bounds);
                float center=viewport.y+camera.WorldToViewportPoint(hero.center).y*viewport.height;
                Assert.Greater(center,.56f,"The physical reward must be visible above the receipt panel.");
                Assert.Less(center,.735f);
            }
            finally { camera.rect=viewport; camera.targetTexture=prior; world.FrameCinematicLabels(); RenderTexture.ReleaseTemporary(target); }
        }

        [UnityTest]
        public IEnumerator SixResourcesHighlightTheirOwnPhysicalAssets()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Resource material rendering verification").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false }); world.Cinematics.enabled=false;
            ResourceDelta[] changes={new ResourceDelta(energy:2),new ResourceDelta(mood:2),new ResourceDelta(insight:2),
                new ResourceDelta(money:2),new ResourceDelta(relation:2),new ResourceDelta(ability:2)};
            RewardObjectKind[] kinds={RewardObjectKind.EnergyCell,RewardObjectKind.WarmLamp,RewardObjectKind.FocusLens,
                RewardObjectKind.MoneyWallet,RewardObjectKind.ConnectionRing,RewardObjectKind.ToolKit};
            try
            {
                for(int i=0;i<changes.Length;i++)
                {
                    var source=new DomainEvent { id="resource-render-"+i,kind=DomainEventKind.TimeEcho,tier=RewardTier.Major,day=7,
                        receipt=new RewardReceipt { resourcesRecorded=true,resources=changes[i],sourceDay=3,action="实际行动" } };
                    string before=JsonUtility.ToJson(source);
                    Assert.IsTrue(world.Cinematics.Enqueue(source,true));
                    float impact=world.Cinematics.Current.Cues.First(c=>!c.NodeHit && c.Phase==CinematicPhase.Impact).Time;
                    world.Cinematics.Advance(impact+.25f);
                    Transform stage=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Pooled 3D reward spectacle");
                    Assert.IsNotNull(stage.Find("Reward showcase "+kinds[i]));
                    Assert.IsTrue(stage.Find("Reward showcase "+kinds[i]).gameObject.activeInHierarchy);
                    Capture(world,"showcase-resource-"+kinds[i]);
                    Assert.AreEqual(before,JsonUtility.ToJson(source)); world.Cinematics.Skip();
                }
            }
            finally { world.Cinematics.CancelAll(); Object.Destroy(world.gameObject); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator GoldAndVioletMoviesExportFromTheActualUnityCamera()
        {
            yield return new EnterPlayMode(); yield return null;
            var app=Object.FindObjectOfType<HorizonApp>(); if(app==null) app=new GameObject("Showcase phone preview recorder").AddComponent<HorizonApp>();
            yield return null;
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var world=(HorizonWorld3D)typeof(HorizonApp).GetField("world",flags).GetValue(app);
            var root=(RectTransform)typeof(HorizonApp).GetField("root",flags).GetValue(app); Canvas canvas=root.GetComponentInParent<Canvas>();
            var archive=(ArchiveData)typeof(HorizonApp).GetField("archive",flags).GetValue(app);
            archive.preferences.sound=false; archive.preferences.haptics=false;
            typeof(HorizonApp).GetMethod("ApplyPreferences",flags).Invoke(app,null); world.Cinematics.enabled=false;
            List<DomainEvent> events=CinematicEventCoverageTests.RealEvents();
            DomainEvent[] subjects={events.First(e=>e.kind==DomainEventKind.PatternBroken),events.First(e=>e.kind==DomainEventKind.Surprise)};
            string[] names={"showcase-causal-link-motion","showcase-insight-prism-motion"};
            var target=new RenderTexture(360,640,24,RenderTextureFormat.ARGB32); target.Create();
            var pixels=new Texture2D(360,640,TextureFormat.RGB24,false);
            Camera ui=new GameObject("Showcase UI recording camera",typeof(Camera)).GetComponent<Camera>();
            ui.enabled=false; ui.cullingMask=1<<5; ui.nearClipPlane=.1f; ui.farClipPlane=20; ui.targetTexture=target;
            RenderMode mode=canvas.renderMode; Camera prior=canvas.worldCamera; float distance=canvas.planeDistance;
            RenderTexture active=RenderTexture.active;
            Directory.CreateDirectory("artifacts/cinematics");
            try
            {
                for(int movie=0;movie<subjects.Length;movie++)
                {
                    typeof(HorizonApp).GetMethod("PlayMasterSpectacle",flags).Invoke(app,new object[] { subjects[movie],(System.Action)(()=>{}) });
                    Assert.IsNotNull(world.Cinematics.Current);
                    foreach(Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=5;
                    canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=ui; canvas.planeDistance=5;
                    float duration=world.Cinematics.Current.Duration;
                    int frames=Mathf.CeilToInt(duration*20);
                    using(var stream=File.Create("artifacts/cinematics/"+names[movie]+".gif"))
                    using(var gif=new TimelineGifWriter(stream,360,640))
                    {
                        for(int frame=0;frame<frames;frame++)
                        {
                            if(frame>0) world.Cinematics.Advance(duration/frames);
                            Canvas.ForceUpdateCanvases(); HorizonPortraitRenderer.Render(world,ui,canvas,target); RenderTexture.active=target;
                            pixels.ReadPixels(new Rect(0,0,360,640),0,0); pixels.Apply(); gif.Frame(pixels.GetPixels32(),5);
                            yield return null;
                        }
                    }
                    world.Cinematics.Skip();
                }
            }
            finally
            { canvas.renderMode=mode; canvas.worldCamera=prior; canvas.planeDistance=distance; RenderTexture.active=active;
                target.Release(); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(ui.gameObject); world.Cinematics.enabled=true; world.Cinematics.CancelAll(); }
            yield return new ExitPlayMode();
        }
    }
}
