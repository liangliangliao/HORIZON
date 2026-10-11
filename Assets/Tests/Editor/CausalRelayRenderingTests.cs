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
            var target=new RenderTexture(360,640,24,RenderTextureFormat.ARGB32); target.Create();
            var pixels=new Texture2D(360,640,TextureFormat.RGB24,false);
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
                    var beam=stage.GetComponentsInChildren<Renderer>().First(r=>r.name.StartsWith("Director driven causal light shaft"));
                    Vector3 scale=beam.transform.localScale; var properties=new MaterialPropertyBlock(); beam.GetPropertyBlock(properties);
                    float clock=properties.GetFloat("_Clock");
                    world.Cinematics.SetPaused(true); world.Cinematics.Advance(1); yield return null;
                    beam.GetPropertyBlock(properties); Assert.AreEqual(clock,properties.GetFloat("_Clock")); Assert.AreEqual(scale,beam.transform.localScale);
                    world.Cinematics.SetPaused(false);
                    world.ApplyPreferences(new PlayerPreferences {sound=false,haptics=false,reducedMotion=true}); world.Cinematics.Advance(.001f);
                    Assert.IsFalse(stage.GetComponentsInChildren<Renderer>().Any(r=>r.name.StartsWith("Director driven causal light shaft")));
                    Assert.AreEqual(relay.Nodes.Count,stage.Cast<Transform>().Count(t=>t.gameObject.activeSelf && t.name.StartsWith("Causal relay ·")));
                    world.Cinematics.Skip(); Assert.IsFalse(stage.gameObject.activeInHierarchy);
                    world.ApplyPreferences(new PlayerPreferences {sound=false,haptics=false});
                    typeof(HorizonApp).GetMethod("CloseMasterPage",flags).Invoke(app,null);
                    typeof(HorizonApp).GetMethod("PlayMasterSpectacle",flags).Invoke(app,new object[] {source.Copy(),(Action)(()=>{})});
                    foreach(Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=5;
                    canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=ui; canvas.planeDistance=5;
                    float duration=world.Cinematics.Current.Duration; int frames=Mathf.CeilToInt(duration*20);
                    using(var stream=File.Create("artifacts/cinematics/causal-relays-"+kind+".gif"))
                    using(var gif=new TimelineGifWriter(stream,360,640))
                    {
                        for(int frame=0;frame<frames;frame++)
                        {
                            if(frame>0) world.Cinematics.Advance(duration/frames);
                            Canvas.ForceUpdateCanvases(); HorizonPortraitRenderer.Render(world,ui,canvas,target); RenderTexture.active=target;
                            pixels.ReadPixels(new Rect(0,0,360,640),0,0); pixels.Apply(); gif.Frame(pixels.GetPixels32(),5);
                            if(frame==Mathf.CeilToInt(first*20)+2 || frame==Mathf.CeilToInt(relay.ImpactTime*20)+2)
                                File.WriteAllBytes("artifacts/cinematics/causal-relays-"+kind+"-"+frame+".png",pixels.EncodeToPNG());
                            yield return null;
                        }
                    }
                    Assert.AreEqual(source.title,root.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Reward event title").text);
                    Assert.AreEqual(source.detail,root.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Reward original explanation").text);
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
    }
}
