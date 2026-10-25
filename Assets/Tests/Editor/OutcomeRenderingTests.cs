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
        [UnityTest]
        public IEnumerator ActualActionRevealsItsChangesAndDateWithoutAnyFictionalMultiplier()
        {
            yield return new EnterPlayMode();
            var app=Object.FindObjectOfType<HorizonApp>(); if(app==null) app=new GameObject("Outcome capture app").AddComponent<HorizonApp>();
            yield return null;
            var life=GameSession.StartMasterLife(1,15,RunMode.Quick);
            CardSpec card=life.Hand.First(c=>life.CanPlay(c) && !life.NeedsImagination(c) && c.Delay>0);
            life.Choose(card.Id); DomainEvent source=life.Master.events.Last(e=>e.kind==DomainEventKind.ActionTaken);
            string receipt=JsonUtility.ToJson(source.receipt);
            var world=(HorizonWorld3D)typeof(HorizonApp).GetField("world",Private).GetValue(app);
            var root=(RectTransform)typeof(HorizonApp).GetField("root",Private).GetValue(app);
            Canvas canvas=root.GetComponentInParent<Canvas>();
            typeof(HorizonApp).GetMethod("PlayMasterSpectacle",Private).Invoke(app,new object[] {source,(Action)(()=>{})});
            world.Cinematics.enabled=false;
            RewardPlan plan=world.Cinematics.Current;
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
                    var rendered=main.GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.name=="Cinematic TMP text");
                    Assert.AreEqual("HORIZON/Embossed Outcome SDF",rendered.fontSharedMaterial.shader.name);
                    Assert.AreEqual(main.text,rendered.text); Assert.IsNotNull(main.transform.Find("Embossed text depth"));
                    foreach(Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=5;
                    canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=ui; canvas.planeDistance=5;
                    Canvas.ForceUpdateCanvases(); HorizonPortraitRenderer.Render(world,ui,canvas,target);
                    RenderTexture old=RenderTexture.active; RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,540,960),0,0); pixels.Apply(); RenderTexture.active=old;
                    Directory.CreateDirectory("artifacts/cinematics");
                    File.WriteAllBytes("artifacts/cinematics/actual-outcome-"+(next?"future":i.ToString())+".png",pixels.EncodeToPNG());
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
                using(var stream=File.Create("artifacts/cinematics/actual-outcome-motion.gif"))
                using(var gif=new TimelineGifWriter(stream,540,960))
                    for(int frame=0;frame<frames;frame++)
                    {
                        if(frame>0) world.Cinematics.Advance(duration/frames);
                        yield return null; yield return null;
                        Canvas.ForceUpdateCanvases(); HorizonPortraitRenderer.Render(world,ui,canvas,target);
                        RenderTexture old=RenderTexture.active; RenderTexture.active=target;
                        pixels.ReadPixels(new Rect(0,0,540,960),0,0); pixels.Apply(); RenderTexture.active=old;
                        gif.Frame(pixels.GetPixels32(),5);
                    }
                File.WriteAllBytes("artifacts/cinematics/actual-outcome-motion.pcm",world.RecordedCinemaPcm());
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
