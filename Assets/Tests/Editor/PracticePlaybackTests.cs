using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
            Transform film=root.Find("Practice film");
            if(film==null || !film.gameObject.activeInHierarchy) yield break;
            // The EditMode runner may poll nested enumerators many times between
            // two player frames. Give touch events and the app's film coroutine
            // the same player loop that they have on a phone.
            var app=Object.FindObjectOfType<HorizonApp>();
            string lastInput="none";
            var driver=app.StartCoroutine(Drive(film, name=>lastInput=name));
            float deadline=Time.realtimeSinceStartup+20;
            while(film!=null && film.gameObject.activeInHierarchy && Time.realtimeSinceStartup<deadline)
                yield return new WaitForSecondsRealtime(.08f);
            if(driver!=null && app!=null) app.StopCoroutine(driver);
            if(film!=null && film.gameObject.activeInHierarchy)
            {
                string controls=string.Join(", ",film.GetComponentsInChildren<Button>().Select(b=>b.name+"="+b.IsInteractable()));
                Assert.Fail("Practice stalled at "+film.Find("Scene heading").GetComponent<Text>().text+
                    "; last touch="+lastInput+"; visible controls="+controls);
            }
        }

        private static IEnumerator Drive(Transform film,System.Action<string> touched)
        {
            while(film!=null && film.gameObject.activeInHierarchy)
            {
                Button next=film.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.IsInteractable() &&
                    (b.name=="Practice difficulty 2" || b.name=="Practice route B" || b.name=="Next shot" || b.name=="Finish practice"));
                if(next!=null) { touched(next.name); next.onClick.Invoke(); }
                yield return null;
            }
        }
    }

    public class PracticePlaybackTests
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

        [UnityTest, Timeout(600000)]
        public IEnumerator V3CaseCapturesOneCommittedActionInThreeQualityModes()
        {
            yield return new EnterPlayMode();
            var app=Object.FindObjectOfType<HorizonApp>() ?? new GameObject("V3 case playback").AddComponent<HorizonApp>();
            yield return null;
            var type=typeof(HorizonApp);
            var root=(RectTransform)type.GetField("root",Private).GetValue(app);
            var world=(HorizonWorld3D)type.GetField("world",Private).GetValue(app);
            Canvas canvas=root.GetComponentInParent<Canvas>();
            RenderMode canvasMode=canvas.renderMode;
            Camera canvasCamera=canvas.worldCamera;
            float canvasDistance=canvas.planeDistance;
            int captureRate=Time.captureFramerate;
            Time.captureFramerate=20;
            var initial=PracticeFilmTests.CaseSampleLife("ask",out _).Snapshot();
            const string path="artifacts/energy-v3";
            Directory.CreateDirectory(path);
            int[] expected=null;
            try
            {
                Shader shader=Resources.Load<Shader>("HorizonCaseEnergy");
                Assert.NotNull(shader);
                Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
                var revealShader=Resources.Load<Shader>("HorizonRevealField");
                Assert.NotNull(revealShader);
                Assert.IsFalse(ShaderUtil.ShaderHasError(revealShader));
                var atlas=Resources.Load<Texture2D>("HorizonCaseFlame");
                Assert.NotNull(atlas);
                Assert.AreEqual(512,atlas.width);
                Assert.AreEqual(512,atlas.height);

                foreach(string mode in new[] {"normal","battery","reduced"})
                {
                    bool reduced=mode=="reduced", battery=mode=="battery";
                    int height=mode=="normal"?960:1200;
                    var life=GameSession.Restore(initial);
                    var archive=new ArchiveData {active=life.Snapshot(),nextRareRun=99,seenSecondLife=true};
                    archive.Repair(); archive.playGuide.completed=true;
                    archive.preferences.sound=false; archive.preferences.haptics=false;
                    archive.preferences.batterySaver=battery; archive.preferences.reducedMotion=reduced;
                    type.GetField("archive",Private).SetValue(app,archive);
                    type.GetField("session",Private).SetValue(app,life);
                    type.GetMethod("SetPaused",Private).Invoke(app,new object[]{false});
                    type.GetMethod("ApplyPreferences",Private).Invoke(app,null);
                    type.GetMethod("BuildBoard",Private).Invoke(app,null);
                    world.WorldCamera.aspect=540f/height;
                    yield return null;
                    var card=root.GetComponentsInChildren<HorizonCardDrag>().Single(x=>x.name=="ask");
                    type.GetMethod("CardPlayed",Private).Invoke(app,new object[]{card});
                    Transform film=root.Find("Practice film");
                    Assert.NotNull(film);
                    var heading=film.Find("Scene heading").GetComponent<Text>();
                    var counter=film.Find("Reward counter").GetComponent<Text>();
                    const BindingFlags fields=BindingFlags.Instance|BindingFlags.NonPublic;
                    var caseLightField=typeof(HorizonWorld3D).GetField("caseLight",fields);
                    var driver=app.StartCoroutine(ChoosePracticeRoutes(film));
                    float deadline=Time.realtimeSinceStartup+30;
                    try
                    {
                        while(film!=null && caseLightField.GetValue(world)==null && Time.realtimeSinceStartup<deadline)
                        {
                            if(film.GetComponentsInChildren<Button>().Any(button=>button.name=="Finish practice")) break;
                            yield return new WaitForSecondsRealtime(.06f);
                        }
                    }
                    finally { if(driver!=null) app.StopCoroutine(driver); }
                    Assert.That(caseLightField.GetValue(world),Is.Not.Null,
                        film==null?"The practice page closed before the reveal.":"Practice stopped at: "+heading.text);
                    Assert.That(life.HasChosen && archive.pendingFeedback!=null);
                    ActionRecord action=life.Actions.Single();
                    var receipt=new PracticeRewardCinematic(action);
                    if(expected==null) expected=PracticeReward.Ticks(action.actualNow);
                    CollectionAssert.AreEqual(expected,PracticeReward.Ticks(action.actualNow));
                    int[] balance=Enumerable.Range(0,6).Select(life.AxisTicks).ToArray();
                    var light=(Light)caseLightField.GetValue(world);
                    Transform screenField=film.Find("Screen energy release");
                    Assert.NotNull(screenField);
                    var chamber=world.GetComponentsInChildren<Transform>().Single(x=>x.name=="Reward case at finished work");
                    var lid=chamber.Find("Hinged left leaf");
                    var core=(Renderer)typeof(HorizonWorld3D).GetField("caseCore",fields).GetValue(world);
                    var wave=(Renderer)typeof(HorizonWorld3D).GetField("caseWave",fields).GetValue(world);
                    var fire=(Renderer[])typeof(HorizonWorld3D).GetField("caseFire",fields).GetValue(world);
                    var body=chamber.Find("Enamel case");
                    Assert.AreEqual(new Rect(0,0,1,1),world.WorldCamera.rect);

                    string folder=Path.Combine(path,mode); Directory.CreateDirectory(folder);
                    var log=new StringBuilder("frame,videoSeconds,stage,counter,flameLayers,core,wave,light,bodyX,bodyY,lidDegrees,adaptiveLevel\n");
                    var ui=new GameObject("V3 portrait UI camera",typeof(Camera)).GetComponent<Camera>();
                    ui.enabled=false; ui.cullingMask=1<<5; ui.nearClipPlane=.1f; ui.farClipPlane=20;
                    var target=new RenderTexture(540,height,24); target.Create(); ui.targetTexture=target;
                    var pixels=new Texture2D(540,height,TextureFormat.RGB24,false);
                    int release=-1,reveal=-1,maxFlames=0,frame=0,lastFrame=-1;
                    int fieldAfterOpen=0, flameFrames=0;
                    bool sawWave=false,skipped=false;
                    string finalGain="",finalCost="";
                    try
                    {
                        foreach(Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=5;
                        canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=ui; canvas.planeDistance=5;
                        while(frame<140)
                        {
                            if(Time.frameCount==lastFrame) { yield return null; continue; }
                            lastFrame=Time.frameCount;
                            if(film.GetComponentsInChildren<Button>().Any(b=>b.name=="Finish practice")) break;
                            if(release<0 && heading.text.Contains("开匣")) release=frame;
                            if(reveal<0 && counter.text.Length>0) reveal=frame;
                            if(counter.text.Length>0) finalGain=counter.text;
                            string cost=film.Find("Practice cost").GetComponent<Text>().text;
                            if(cost.Length>0) finalCost=cost;
                            int flames=fire.Count(layer=>layer.gameObject.activeInHierarchy);
                            maxFlames=Mathf.Max(maxFlames,flames);
                            if(flames>0) flameFrames++;
                            if(release>=0 && screenField.gameObject.activeInHierarchy) fieldAfterOpen++;
                            sawWave|=wave.gameObject.activeInHierarchy;
                            Vector3 bodyPosition=world.WorldCamera.WorldToViewportPoint(body.position);
                            Assert.That(bodyPosition.x,Is.InRange(.2f,.8f));
                            Assert.That(bodyPosition.y,Is.InRange(.39f,.75f));
                            log.AppendLine(System.FormattableString.Invariant($"{frame},{frame/20f:F2},{heading.text},{counter.text.Replace('\n','|')},{flames},{core.gameObject.activeInHierarchy},{wave.gameObject.activeInHierarchy},{light.intensity:F3},{bodyPosition.x:F4},{bodyPosition.y:F4},{lid.localEulerAngles.z:F2},{world.AdaptiveQualityLevel}"));
                            HorizonPortraitRenderer.Render(world,ui,canvas,target);
                            RenderTexture previous=RenderTexture.active;
                            try
                            {
                                RenderTexture.active=target;
                                pixels.ReadPixels(new Rect(0,0,540,height),0,0); pixels.Apply();
                            }
                            finally { RenderTexture.active=previous; }
                            File.WriteAllBytes(Path.Combine(folder,$"frame-{frame++:D5}.jpg"),pixels.EncodeToJPG(92));
                            if(reduced && finalCost.Length>0 && !skipped)
                            {
                                film.Find("Skip practice reward").GetComponent<Button>().onClick.Invoke();
                                skipped=true;
                            }
                            yield return null;
                        }
                    }
                    finally
                    {
                        canvas.renderMode=canvasMode; canvas.worldCamera=canvasCamera; canvas.planeDistance=canvasDistance;
                        ui.targetTexture=null; target.Release(); Object.Destroy(pixels); Object.Destroy(target); Object.Destroy(ui.gameObject);
                    }
                    Assert.GreaterOrEqual(reveal,0);
                    Assert.IsTrue(film.GetComponentsInChildren<Button>().Any(b=>b.name=="Finish practice"));
                    if(reduced) { Assert.AreEqual(0,maxFlames); Assert.IsFalse(sawWave); Assert.IsTrue(skipped);
                        Assert.IsFalse(screenField.gameObject.activeSelf); }
                    else
                    {
                        Assert.GreaterOrEqual(release,0);
                        Assert.That((reveal-release)/20f,Is.InRange(.05f,.35f));
                        Assert.That(maxFlames,Is.InRange(1,battery?1:3));
                        Assert.IsTrue(sawWave);
                        Assert.GreaterOrEqual(fieldAfterOpen,33,"The direct screen field must remain visible as the numbers settle.");
                        Assert.GreaterOrEqual(flameFrames,23,"The flame must survive for at least a second at 20 fps.");
                    }
                    Assert.AreEqual(receipt.Changes(PracticeRewardCinematic.Duration,true,reduced),finalGain);
                    Assert.That(finalCost,Does.Contain(receipt.Changes(PracticeRewardCinematic.Duration,false,reduced)));
                    CollectionAssert.AreEqual(balance,Enumerable.Range(0,6).Select(life.AxisTicks));
                    File.WriteAllText(Path.Combine(folder,"trace.csv"),log.ToString());
                    File.WriteAllText(Path.Combine(folder,"receipt.json"),JsonUtility.ToJson(action,true));
                    File.WriteAllText(Path.Combine(folder,"complete.txt"),frame.ToString());
                    File.WriteAllText(Path.Combine(folder,"capture.txt"),$"{mode}, 540x{height}, 20 fps\nUnity {Application.unityVersion}\n{SystemInfo.deviceModel}\n{SystemInfo.graphicsDeviceName}\n{SystemInfo.graphicsDeviceType}\n");
                    type.GetMethod("CloseMasterPage",Private).Invoke(app,null);
                    type.GetField("practicePage",Private).SetValue(app,null);
                    yield return null;
                }
            }
            finally
            {
                Time.captureFramerate=captureRate;
                world.WorldCamera.ResetAspect();
                type.GetMethod("CloseMasterPage",Private).Invoke(app,null);
                type.GetField("practicePage",Private).SetValue(app,null);
                type.GetField("busy",Private).SetValue(app,false);
            }
            yield return new ExitPlayMode();
        }

        private static IEnumerator ChoosePracticeRoutes(Transform film)
        {
            while(film!=null && film.gameObject.activeInHierarchy)
            {
                Button next=film.GetComponentsInChildren<Button>().FirstOrDefault(button=>button.IsInteractable() &&
                    (button.name=="Practice difficulty 2" || button.name=="Practice route B" || button.name=="Next shot"));
                if(next!=null) next.onClick.Invoke();
                yield return null;
            }
        }

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
                    if(heading.Contains("蓄光"))
                    {
                        Assert.That(selected && achieved);
                        Assert.That(life.HasChosen, "The actual result must be durable before the reveal.");
                        Assert.NotNull(archive.pendingFeedback);
                        sawCharge=true;
                    }
                    if(!achieved) { Assert.IsFalse(life.HasChosen); Assert.AreEqual(balance,life.AxisTicks(0)); }
                    var counter=film.Find("Reward counter").GetComponent<Text>();
                    Assert.That(counter.text,Does.Not.Contain("×"));
                    if(!sawCharge) Assert.That(counter.text,Is.Empty);
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
                File.WriteAllText(Path.Combine(path,"complete.txt"),frame.ToString());
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
