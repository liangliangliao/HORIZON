using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Horizon.Game;
using Horizon.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Horizon.Tests
{
    // These frames use receipts produced by the rules, not fabricated rewards.
    // Screenshots provide review evidence; they do not measure mobile performance.
    public sealed class CinematicEvidenceTests
    {
        [Serializable] private sealed class FrameEvidence
        {
            public string eventId, kind, title, explanation, resources, file, shot;
            public float time;
            public Vector3 camera, player;
        }
        [Serializable] private sealed class CaptureEvidence
        {
            public string source = "Actual GameSession and RealityConstellation domain events";
            public List<FrameEvidence> frames = new List<FrameEvidence>();
        }
        [UnityTest]
        public IEnumerator RealDomainEventsRetainCopyAndRenderDistinctPortraitStages()
        {
            yield return new EnterPlayMode();
            var app = Object.FindObjectOfType<HorizonApp>();
            if (app == null) app = new GameObject("Cinematic evidence app").AddComponent<HorizonApp>();
            yield return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var world = (HorizonWorld3D)typeof(HorizonApp).GetField("world", flags).GetValue(app);
            var root = (RectTransform)typeof(HorizonApp).GetField("root", flags).GetValue(app);
            var archive = (ArchiveData)typeof(HorizonApp).GetField("archive", flags).GetValue(app);
            archive.preferences.sound = false; archive.preferences.haptics = false;
            archive.preferences.reducedMotion = false; archive.preferences.batterySaver = false;
            typeof(HorizonApp).GetMethod("ApplyPreferences", flags).Invoke(app, null);
            var events = CinematicEventCoverageTests.RealEvents();
            var evidence = new CaptureEvidence();
            var target = new RenderTexture(540, 960, 24); target.Create();
            var pixels = new Texture2D(540, 960, TextureFormat.RGB24, false);
            var ui = new GameObject("Cinematic evidence camera", typeof(Camera)).GetComponent<Camera>();
            ui.enabled=false; ui.cullingMask=1<<5; ui.nearClipPlane=.1f; ui.farClipPlane=20; ui.targetTexture=target;
            Canvas canvas = root.GetComponentInParent<Canvas>();
            RenderMode mode=canvas.renderMode; Camera oldCamera=canvas.worldCamera; float distance=canvas.planeDistance;
            string directory=Path.Combine(Directory.GetCurrentDirectory(),"artifacts","cinematics");
            Directory.CreateDirectory(directory);
            try
            {
                foreach (DomainEvent source in events)
                {
                    DomainEvent e=source.Copy();
                    typeof(HorizonApp).GetMethod("PlayMasterSpectacle",flags).Invoke(app,new object[] { e,new Action(delegate {}) });
                    world.Cinematics.enabled=false;
                    RewardPlan plan=world.Cinematics.Current;
                    byte[] prior=null;
                    for (int frame=0; frame<3; frame++)
                    {
                        float time=plan.Duration*(frame==0?.12f:frame==1?.54f:.86f);
                        world.Cinematics.Advance(Mathf.Max(0,time-world.Cinematics.Elapsed));
                        yield return null;
                        Assert.IsTrue(world.Cinematics.IsPlaying);
                        Assert.AreEqual(source.title,root.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Reward event title").text);
                        Assert.AreEqual(plan.ResourceCopy,root.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Reward actual resources").text);
                        Assert.AreEqual(source.detail,root.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Reward original explanation").text);
                        if(source.receipt!=null && source.receipt.predictionRecorded)
                            Assert.AreEqual(CinematicCaption.PredictionCopy(source.receipt),root.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Reward original prediction").text);
                        foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=5;
                        canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=ui; canvas.planeDistance=5;
                        Canvas.ForceUpdateCanvases(); HorizonPortraitRenderer.Render(world,ui,canvas,target);
                        RenderTexture previous=RenderTexture.active; RenderTexture.active=target;
                        pixels.ReadPixels(new Rect(0,0,540,960),0,0); pixels.Apply(); RenderTexture.active=previous;
                        byte[] png=pixels.EncodeToPNG(); Assert.Greater(png.Length,6000,"The captured stage must contain a rendered scene and readable copy.");
                        if (prior!=null) Assert.IsFalse(prior.SequenceEqual(png),"Different story stages must not be a static slide.");
                        prior=png;
                        string file=source.kind+"-"+frame+".png";
                        File.WriteAllBytes(Path.Combine(directory,file),png);
                        evidence.frames.Add(new FrameEvidence { eventId=source.id,kind=source.kind.ToString(),title=source.title,
                            explanation=source.detail,resources=plan.ResourceCopy,time=time,file=file,
                            shot=world.CurrentCinematicShot,camera=world.WorldCamera.transform.position,
                            player=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Cinematic player").position });
                    }
                    world.Cinematics.Skip();
                    typeof(HorizonApp).GetMethod("CloseMasterPage",flags).Invoke(app,null);
                    Assert.IsFalse(source.acknowledged,"Rendering a copied receipt must not acknowledge the source event.");
                }
                File.WriteAllText(Path.Combine(directory,"frames.json"),JsonUtility.ToJson(evidence,true));
            }
            finally
            {
                canvas.renderMode=mode; canvas.worldCamera=oldCamera; canvas.planeDistance=distance;
                world.Cinematics.enabled=true; world.Cinematics.CancelAll();
                target.Release(); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(ui.gameObject);
            }
            yield return new ExitPlayMode();
        }
    }
}
