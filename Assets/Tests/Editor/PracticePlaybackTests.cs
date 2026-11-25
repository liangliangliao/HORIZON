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
    internal static class PracticeTouch
    {
        public static IEnumerator Finish(RectTransform root)
        {
            if(root.Find("Practice film")==null) yield break;
            for(int frame=0;frame<180;frame++)
            {
                Transform film=root.Find("Practice film");
                if(film==null || !film.gameObject.activeInHierarchy) yield break;
                Button next=film.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.IsInteractable() &&
                    (b.name=="Practice difficulty 2" || b.name=="Practice route B" || b.name=="Next shot" || b.name=="Finish practice"));
                if(next!=null) next.onClick.Invoke();
                yield return null;
            }
            Assert.Fail("The practice did not reach its receipt after the player made each required choice.");
        }
    }

    public class PracticePlaybackTests
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [UnityTest]
        public IEnumerator WalkPlaysThroughRealChoicesBeforeAwardingAndExportsThePortraitFilm()
        {
            yield return new EnterPlayMode();
            var app=Object.FindObjectOfType<HorizonApp>() ?? new GameObject("Practice playback").AddComponent<HorizonApp>();
            yield return null;
            var life=GameSession.StartMasterLife(1,17,RunMode.Quick);
            while(!life.Hand.Any(c=>c.Id=="walk"))
            {
                if(life.HasPredictionReview) life.MarkPredictionReviewed();
                if(life.CanPredict) life.SkipPrediction();
                life.Choose(life.Hand[2].Id); if(life.NeedsStation) life.VisitStation(); life.Advance();
            }
            var setup=life.Snapshot(); setup.energy=5; setup.mood=4; life=GameSession.Restore(setup);
            var archive=new ArchiveData { active=life.Snapshot(),nextRareRun=99,seenSecondLife=true }; archive.Repair(); archive.playGuide.completed=true;
            var type=typeof(HorizonApp);
            type.GetField("archive",Private).SetValue(app,archive); type.GetField("session",Private).SetValue(app,life);
            type.GetMethod("BuildBoard",Private).Invoke(app,null);
            yield return null;
            var root=(RectTransform)type.GetField("root",Private).GetValue(app);
            var world=(HorizonWorld3D)type.GetField("world",Private).GetValue(app);
            var card=root.GetComponentsInChildren<HorizonCardDrag>().Single(x=>x.name=="walk");
            type.GetMethod("CardPlayed",Private).Invoke(app,new object[]{card});
            var film=root.Find("Practice film");
            Assert.NotNull(film); Assert.IsFalse(life.HasChosen);
            Assert.IsFalse(ShaderUtil.ShaderHasError(Resources.Load<Shader>("HorizonPracticeRays")));
            string path="artifacts/practice-frames"; Directory.CreateDirectory(path);
            var canvas=root.GetComponentInParent<Canvas>();
            RenderMode mode=canvas.renderMode; Camera old=canvas.worldCamera; float distance=canvas.planeDistance;
            var target=new RenderTexture(540,960,24); target.Create();
            var pixels=new Texture2D(540,960,TextureFormat.RGB24,false);
            var ui=new GameObject("Practice portrait camera",typeof(Camera)).GetComponent<Camera>();
            ui.enabled=false; ui.cullingMask=1<<5; ui.nearClipPlane=.1f; ui.farClipPlane=20; ui.targetTexture=target;
            int rate=Time.captureFramerate; Time.captureFramerate=16;
            bool selected=false, achieved=false, sawCharge=false, finished=false;
            int frame=0, waiting=0, balance=life.AxisTicks(0), lastFrame=-1;
            try
            {
                while(frame<560)
                {
                    if(Time.frameCount==lastFrame) { yield return null; continue; }
                    lastFrame=Time.frameCount;
                    var buttons=film.GetComponentsInChildren<Button>();
                    var difficulty=buttons.FirstOrDefault(b=>b.name=="Practice difficulty 1");
                    var fork=buttons.FirstOrDefault(b=>b.name=="Practice route B");
                    var heading=film.Find("Scene heading").GetComponent<Text>().text;
                    if(difficulty!=null && frame>18) difficulty.onClick.Invoke();
                    if(fork!=null && ++waiting>=20) { Assert.IsFalse(life.HasChosen); fork.onClick.Invoke(); selected=true; }
                    if(heading.StartsWith("完成")) achieved=true;
                    if(heading.Contains("蓄光")) { Assert.That(selected && achieved); sawCharge=true; }
                    if(!sawCharge) { Assert.IsFalse(life.HasChosen); Assert.AreEqual(balance,life.AxisTicks(0)); }
                    foreach(Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=5;
                    canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=ui; canvas.planeDistance=5;
                    Canvas.ForceUpdateCanvases(); HorizonPortraitRenderer.Render(world,ui,canvas,target);
                    RenderTexture prior=RenderTexture.active; RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,540,960),0,0); pixels.Apply(); RenderTexture.active=prior;
                    File.WriteAllBytes(Path.Combine(path,"frame-"+frame.ToString("D5")+".jpg"),pixels.EncodeToJPG(92));
                    frame++;
                    if(buttons.Any(b=>b.name=="Finish practice"))
                    { finished=true; break; }
                    yield return null;
                }
                Assert.That(achieved && selected && sawCharge && finished);
                var last=life.Actions.Last();
                Assert.AreEqual("B",last.practice.routes);
                var landings=world.GetComponentsInChildren<Transform>().Where(x=>x.name.EndsWith(" landing") && x.parent.name=="Reward case at finished work").ToArray();
                Assert.AreEqual(last.actualNow.ticks.Count(v=>v!=0),landings.Length);
                Assert.That(landings.All(x=>x.Find("Column of light").gameObject.activeInHierarchy));
                Assert.AreEqual(landings.Length,landings.Select(x=>x.localPosition).Distinct().Count());
                Assert.AreEqual(life.AxisTicks(0),GameSession.Restore(archive.active).AxisTicks(0));
                Assert.Greater(life.AxisTicks(0),balance);
            }
            finally
            {
                Time.captureFramerate=rate; canvas.renderMode=mode; canvas.worldCamera=old; canvas.planeDistance=distance;
                ui.targetTexture=null; target.Release(); Object.Destroy(ui.gameObject); Object.Destroy(target); Object.Destroy(pixels);
            }
            yield return new ExitPlayMode();
        }
    }
}
