using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Horizon.Game;
using Horizon.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Horizon.Tests
{
    public sealed class CausalRelayRenderingTests
    {
        [Test]
        public void CausalBeamShaderImportsWithoutErrors()
        { Shader shader=Resources.Load<Shader>("HorizonCausalBeam"); Assert.IsNotNull(shader); Assert.IsFalse(ShaderUtil.ShaderHasError(shader)); }

        [Test]
        public void OpticalCinemaBackdropImportsWithoutErrors()
        { Shader shader=Resources.Load<Shader>("HorizonCinemaBackdrop"); Assert.IsNotNull(shader); Assert.IsFalse(ShaderUtil.ShaderHasError(shader)); }

        [Test]
        public void AttentionIrisImportsWithoutErrors()
        { Shader shader=Resources.Load<Shader>("HorizonCinemaIris"); Assert.IsNotNull(shader); Assert.IsFalse(ShaderUtil.ShaderHasError(shader)); }

        [UnityTest]
        public IEnumerator ActualCascadeRelaysPauseReduceAndExportWithTheirOriginalPhoneCopy()
        {
            yield return new EnterPlayMode(); yield return null;
            var app=Object.FindObjectOfType<HorizonApp>();
            if(app==null) app=new GameObject("Causal relay phone verification").AddComponent<HorizonApp>();
            yield return null;
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var world=(HorizonWorld3D)typeof(HorizonApp).GetField("world",flags).GetValue(app);
            var root=(RectTransform)typeof(HorizonApp).GetField("root",flags).GetValue(app);
            Canvas canvas=root.GetComponentInParent<Canvas>();
            var archive=(ArchiveData)typeof(HorizonApp).GetField("archive",flags).GetValue(app);
            archive.preferences.sound=false; archive.preferences.haptics=false;
            typeof(HorizonApp).GetMethod("ApplyPreferences",flags).Invoke(app,null);
            world.Cinematics.enabled=false;
            var target=new RenderTexture(540,960,24,RenderTextureFormat.ARGB32); target.Create();
            var pixels=new Texture2D(540,960,TextureFormat.RGB24,false);
            var ui=new GameObject("Causal relay UI capture",typeof(Camera)).GetComponent<Camera>();
            ui.enabled=false; ui.cullingMask=1<<5; ui.nearClipPlane=.1f; ui.farClipPlane=20; ui.targetTexture=target;
            RenderMode mode=canvas.renderMode; Camera previous=canvas.worldCamera; float distance=canvas.planeDistance;
            RenderTexture active=RenderTexture.active;
            Directory.CreateDirectory("artifacts/cinematics");
            try
            {
                foreach(DomainEventKind kind in new[] {DomainEventKind.Cascade,DomainEventKind.CausalSingularity})
                {
                    DomainEvent source=CausalRelayTests.ActualCascade(kind); string before=JsonUtility.ToJson(source);
                    CameraClearFlags clear=world.WorldCamera.clearFlags; Color background=world.WorldCamera.backgroundColor;
                    typeof(HorizonApp).GetMethod("PlayMasterSpectacle",flags).Invoke(app,new object[] {source.Copy(),(Action)(()=>{})});
                    var relay=new CausalRelayPlan(world.Cinematics.Current);
                    Transform stage=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Pooled actual causal relays");
                    Assert.AreEqual(relay.Nodes.Count,stage.Cast<Transform>().Count(t=>t.gameObject.activeSelf && t.name.StartsWith("Causal relay ·")));
                    Assert.AreEqual(relay.Edges.Count,stage.GetComponentsInChildren<LineRenderer>().Count(l=>l.name.StartsWith("Recorded cause ·")));
                    Assert.IsFalse(world.GetComponentsInChildren<LineRenderer>().Any(l=>l.name.StartsWith("Cause beam")),"Independent chronological neighbours must not be linked.");
                    float first=relay.Nodes[0].ActivationTime;
                    world.Cinematics.Advance(first-.01f);
                    Assert.IsFalse(stage.GetComponentsInChildren<Renderer>().Any(r=>r.name.StartsWith("Director driven causal light shaft")));
                    world.Cinematics.Advance(.04f); yield return null;
                    AssertSourceInsideCamera(world);
                    var beam=stage.GetComponentsInChildren<Renderer>().First(r=>r.name.StartsWith("Director driven causal light shaft"));
                    Vector3 scale=beam.transform.localScale; var properties=new MaterialPropertyBlock(); beam.GetPropertyBlock(properties);
                    float clock=properties.GetFloat("_Clock");
                    world.Cinematics.SetPaused(true); world.Cinematics.Advance(1); yield return null;
                    beam.GetPropertyBlock(properties); Assert.AreEqual(clock,properties.GetFloat("_Clock")); Assert.AreEqual(scale,beam.transform.localScale);
                    Transform production=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="HORIZON optical cinema production");
                    Transform rotor=production.Find("Architectural horizon aperture/Rotating time coordinate rim"); Quaternion rotation=rotor.localRotation;
                    world.Cinematics.Advance(.7f); yield return null; Assert.AreEqual(rotation,rotor.localRotation);
                    world.Cinematics.SetPaused(false);
                    var tension=new CinematicTension(world.Cinematics.Current);
                    world.Cinematics.Advance(tension.RevealHold+.02f-world.Cinematics.Elapsed); yield return null;
                    Transform subject=world.GetComponentsInChildren<Transform>().Single(t=>t.name=="Reward showcase CausalChain");
                    Assert.IsTrue(subject.gameObject.activeInHierarchy);
                    Vector3 heldPosition=subject.localPosition; Quaternion heldAngle=subject.localRotation;
                    Vector3 heldCamera=world.WorldCamera.transform.position; float heldFov=world.WorldCamera.fieldOfView;
                    world.Cinematics.Advance(.15f); yield return null;
                    Assert.AreEqual(heldPosition,subject.localPosition); Assert.AreEqual(heldAngle,subject.localRotation);
                    Assert.AreEqual(heldCamera,world.WorldCamera.transform.position); Assert.AreEqual(heldFov,world.WorldCamera.fieldOfView);
                    world.ApplyPreferences(new PlayerPreferences {sound=false,haptics=false,reducedMotion=true}); world.Cinematics.Advance(.001f);
                    Assert.IsFalse(stage.GetComponentsInChildren<Renderer>().Any(r=>r.name.StartsWith("Director driven causal light shaft")));
                    Assert.AreEqual(relay.Nodes.Count,stage.Cast<Transform>().Count(t=>t.gameObject.activeSelf && t.name.StartsWith("Causal relay ·")));
                    Assert.IsFalse(production.GetComponentsInChildren<LineRenderer>().Any(l=>l.name.StartsWith("Cinematic energy ribbon")));
                    world.Cinematics.Skip(); Assert.IsFalse(stage.gameObject.activeInHierarchy);
                    Assert.IsFalse(production.gameObject.activeInHierarchy); Assert.AreEqual(clear,world.WorldCamera.clearFlags); Assert.AreEqual(background,world.WorldCamera.backgroundColor);
                    world.ApplyPreferences(new PlayerPreferences {sound=false,haptics=false});
                    typeof(HorizonApp).GetMethod("CloseMasterPage",flags).Invoke(app,null);
                    typeof(HorizonApp).GetMethod("PlayMasterSpectacle",flags).Invoke(app,new object[] {source.Copy(),(Action)(()=>{})});
                    foreach(Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=5;
                    canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=ui; canvas.planeDistance=5;
                    float duration=world.Cinematics.Current.Duration; int frames=Mathf.CeilToInt(duration*20);
                    string lossless="artifacts/cinematics/frames-"+kind; Directory.CreateDirectory(lossless);
                    using(var stream=File.Create("artifacts/cinematics/causal-relays-"+kind+".gif"))
                    using(var gif=new TimelineGifWriter(stream,540,960))
                    {
                        for(int frame=0;frame<frames;frame++)
                        {
                            if(frame>0) world.Cinematics.Advance(duration/frames);
                            Canvas.ForceUpdateCanvases(); HorizonPortraitRenderer.Render(world,ui,canvas,target); RenderTexture.active=target;
                            pixels.ReadPixels(new Rect(0,0,540,960),0,0); pixels.Apply(); gif.Frame(pixels.GetPixels32(),5);
                            // Preserve the actual camera's full color depth for
                            // MP4 review; the compact GIF has a limited palette.
                            File.WriteAllBytes(Path.Combine(lossless,"frame-"+frame.ToString("D5")+".png"),pixels.EncodeToPNG());
                            float heroTime=new RewardSpectacle(world.Cinematics.Current).HeroTime;
                            if(frame==Mathf.CeilToInt(heroTime*20)+8)
                            {
                                Transform plaque=world.GetComponentsInChildren<Transform>().Single(t=>t.name=="3D presentation multiplier plaque");
                                foreach(Renderer carrier in stage.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Evidence metal base"))
                                    Assert.Greater(world.WorldCamera.WorldToViewportPoint(carrier.bounds.center).z,world.WorldCamera.WorldToViewportPoint(plaque.position).z+.3f,"Evidence carriers must not hide the physical multiplier.");
                            }
                            if(frame==Mathf.CeilToInt(first*20)+2 || frame==Mathf.CeilToInt(relay.ImpactTime*20)+2 || frame==Mathf.CeilToInt(heroTime*20)+8)
                                File.WriteAllBytes("artifacts/cinematics/causal-relays-"+kind+"-"+frame+".png",pixels.EncodeToPNG());
                            yield return null;
                        }
                    }
                    byte[] pcm=world.RecordedCinemaPcm();
                    File.WriteAllBytes("artifacts/cinematics/causal-relays-"+kind+".pcm",pcm);
                    Assert.AreEqual(Mathf.CeilToInt(duration*22050)*2,pcm.Length);
                    Assert.AreEqual(0,PcmPeak(pcm,tension.RevealHold+.05f,tension.Reveal-.05f),"The held subject must have a real silent interval.");
                    Assert.Greater(PcmPeak(pcm,tension.PreviewStart+.1f,tension.RevealHold-.05f),50,"Charge must be audible before silence.");
                    Assert.Greater(PcmPeak(pcm,tension.Reveal,tension.Reveal+.15f),50,"Release must restore the node cue sound.");
                    Assert.AreEqual(source.title,root.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Reward event title").text);
                    Assert.AreEqual(source.detail,root.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Reward original explanation").text);
                    foreach(string name in new[] {"Reward event title","Reward original explanation","Reward actual resources"})
                    {
                        Text text=root.GetComponentsInChildren<Text>(true).Single(t=>t.name==name);
                        Assert.GreaterOrEqual(text.rectTransform.rect.height+.5f,text.preferredHeight,name+" must fit without clipping the original copy.");
                    }
                    world.Cinematics.Skip(); typeof(HorizonApp).GetMethod("CloseMasterPage",flags).Invoke(app,null);
                    Assert.AreEqual(before,JsonUtility.ToJson(source));
                }
            }
            finally
            {
                canvas.renderMode=mode; canvas.worldCamera=previous; canvas.planeDistance=distance; RenderTexture.active=active;
                world.Cinematics.CancelAll(); world.Cinematics.enabled=true;
                target.Release(); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(ui.gameObject);
            }
            yield return new ExitPlayMode();
        }

        private static int PcmPeak(byte[] pcm,float from,float to)
        {
            int peak=0;
            for(int i=Mathf.RoundToInt(from*22050)*2;i<Mathf.RoundToInt(to*22050)*2 && i+1<pcm.Length;i+=2)
                peak=Math.Max(peak,Math.Abs((int)(short)(pcm[i]|pcm[i+1]<<8)));
            return peak;
        }
        private static void AssertSourceInsideCamera(HorizonWorld3D world)
        {
            Camera camera=world.WorldCamera; Rect viewport=camera.rect; RenderTexture prior=camera.targetTexture;
            var target=RenderTexture.GetTemporary(540,614,24);
            try
            {
                camera.rect=new Rect(0,0,1,1); camera.targetTexture=target; world.FrameCinematicLabels();
                TextMesh source=world.GetComponentsInChildren<TextMesh>().Single(t=>t.name=="Cinematic evidence label 25");
                Bounds bounds=source.GetComponent<Renderer>().localBounds;
                for(int i=0;i<8;i++)
                {
                    Vector3 corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    Vector3 projected=camera.WorldToViewportPoint(source.transform.TransformPoint(corner));
                    Assert.That(projected.y,Is.InRange(.02f,.98f),"The full day and source must remain inside the rendered scene.");
                }
            }
            finally { camera.rect=viewport; camera.targetTexture=prior; world.FrameCinematicLabels(); RenderTexture.ReleaseTemporary(target); }
        }
    }
}
