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
            foreach(string name in new[] {"HorizonCaption","HorizonBloom","HorizonImmersion","HorizonResultLight"})
            {
                Shader shader=Resources.Load<Shader>(name); Assert.IsNotNull(shader,name);
                Assert.IsFalse(ShaderUtil.ShaderHasError(shader),name);
            }
        }
        [Test]
        public void RehearsalRetryShowsActualRecoveryMeaningWithoutChangingItsSavedEvidence()
        {
            var run=ImaginationEngine.Begin("actual-retry","portfolio","整理作品",1,2);
            ImaginationEngine.Continue(run);
            ImaginationEngine.Prepare(run,PreparationAction.SmallStep);
            ImaginationEngine.Continue(run); ImaginationEngine.Continue(run);
            ImaginationEngine.Recover(run,RecoveryAction.Rest);
            string evidence=JsonUtility.ToJson(run), reading=ImaginationStoryboard.Reading(run);
            Assert.That(reading,Does.StartWith(run.memories.Last().text));
            Assert.That(reading,Does.Contain("再次行动 1 次"));
            Assert.That(reading,Does.Not.Contain("×").And.Not.Contain("AGAIN"));
            Assert.AreEqual(evidence,JsonUtility.ToJson(run));
        }
        [UnityTest]
        public IEnumerator ActualActionRevealsItsChangesAndDateWithoutAnyFictionalMultiplier()
        { return CaptureActualOutcome(false); }
        [UnityTest]
        public IEnumerator ActualWalkPlaysTheHomeParkSuspenseAndRecordedRecoveryInFullColor()
        { return CaptureActualOutcome(true); }
        private IEnumerator CaptureActualOutcome(bool walk)
        {
            yield return new EnterPlayMode();
            var app=Object.FindObjectOfType<HorizonApp>(); if(app==null) app=new GameObject("Outcome capture app").AddComponent<HorizonApp>();
            yield return null; yield return null;
            Assert.IsNotNull(app,"The real application must finish startup before recording.");
            var life=walk?CreateWalkLife():CreateActualLife();
            string prefix=walk?"walk-outcome":"actual-outcome";
            Assert.IsNotNull(life); Assert.IsNotNull(life.Master);
            DomainEvent source=life.Master.events.Last(e=>e.kind==DomainEventKind.ActionTaken);
            string receipt=JsonUtility.ToJson(source.receipt);
            var world=(HorizonWorld3D)typeof(HorizonApp).GetField("world",Private).GetValue(app);
            var root=(RectTransform)typeof(HorizonApp).GetField("root",Private).GetValue(app);
            Assert.IsNotNull(world,"Recording requires the initialized scene world."); Assert.IsNotNull(root,"Recording requires the initialized phone canvas.");
            var archive=new ArchiveData {active=life.Snapshot(),seenSecondLife=true,nextRareRun=99}; archive.Repair();
            archive.preferences.sound=false; archive.preferences.haptics=false;
            typeof(HorizonApp).GetField("archive",Private).SetValue(app,archive);
            typeof(HorizonApp).GetField("session",Private).SetValue(app,life);
            typeof(HorizonApp).GetMethod("ApplyPreferences",Private).Invoke(app,null);
            typeof(HorizonApp).GetMethod("Clear",Private).Invoke(app,new object[] {false});
            Canvas canvas=root.GetComponentInParent<Canvas>();
            Assert.IsNotNull(canvas); Assert.IsNotNull(world.Cinematics);
            typeof(HorizonApp).GetMethod("PlayMasterSpectacle",Private).Invoke(app,new object[] {source,(Action)(()=>{})});
            world.Cinematics.enabled=false;
            RewardPlan plan=world.Cinematics.Current;
            Assert.IsNotNull(plan,"The actual action must be scheduled, not replaced by a startup page.");
            var actionStory=new LifeActionStoryboard(plan);
            Assert.IsTrue(actionStory.Enabled,"The recorded action needs its authored action scene before the result.");
            world.Cinematics.Advance(actionStory.Impact-.25f); yield return null; yield return null;
            Assert.AreEqual(LifeActionShot.Suspense,actionStory.Sample(world.Cinematics.Elapsed).Shot);
            foreach(string labelName in new[] {"Reward event title","Reward story"})
            {
                Text label=root.GetComponentsInChildren<Text>(true).Single(t=>t.name==labelName);
                Assert.IsNotEmpty(label.text,labelName);
                var face=label.GetComponentsInChildren<TextMeshProUGUI>(true).Single(t=>t.name=="Cinematic TMP text");
                Assert.AreEqual(label.text,face.text,labelName);
                Assert.GreaterOrEqual(face.maxVisibleCharacters,label.text.Length,
                    labelName+" must remain completely readable throughout the breath-hold before reveal.");
                var depth=label.transform.Find("Embossed text depth")?.GetComponent<TextMeshProUGUI>();
                if(depth!=null) Assert.GreaterOrEqual(depth.maxVisibleCharacters,label.text.Length,
                    labelName+" embossed depth must follow the complete face, not a stale partial reveal.");
            }
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
                    if(walk && !next)
                    {
                        Assert.IsTrue(plan.Outcome.WalkRecovery);
                        Transform park=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Park destination");
                        Transform actor=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Cinematic player");
                        Transform badge=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Recovery marks on coat");
                        Assert.IsTrue(park.gameObject.activeInHierarchy);
                        Assert.IsTrue(actor.GetComponentsInChildren<Renderer>().Any(r=>r.enabled));
                        Assert.IsTrue(badge.gameObject.activeInHierarchy);
                        Vector3 visible=world.WorldCamera.WorldToViewportPoint(badge.position);
                        Assert.Greater(visible.z,0);
                        Assert.That(visible.x,Is.InRange(.05f,.95f)); Assert.That(visible.y,Is.InRange(.25f,.82f));
                    }
                    else if(!next)
                    {
                        var resources=plan.Objects.Where(o=>o.Class==RewardObjectClass.Resource).ToArray();
                        var subject=(MaterialReward)typeof(HorizonWorld3D).GetField("activeHeroReward",Private).GetValue(world);
                        Assert.AreEqual(resources[i],subject,"Ordinary action copy must have its own actual resource close-up.");
                        var hero=(Transform)typeof(HorizonWorld3D).GetField("showcaseHero",Private).GetValue(world);
                        Assert.IsTrue(hero.gameObject.activeInHierarchy);
                        Assert.IsTrue(hero.GetComponentsInChildren<Renderer>().Any(r=>r.enabled));
                        AssertSubjectAperture(world,hero,HorizonCinemaLayout.SubjectAperture);
                        Transform actor=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Cinematic player");
                        Assert.IsTrue(actor.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled));
                    }
                    var rendered=main.GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.name=="Cinematic TMP text");
                    Assert.AreEqual("HORIZON/Embossed Outcome SDF",rendered.fontSharedMaterial.shader.name);
                    Assert.AreEqual(main.text,rendered.text); Assert.IsNotNull(main.transform.Find("Embossed text depth"));
                    foreach(Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=5;
                    canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=ui; canvas.planeDistance=5;
                    Canvas.ForceUpdateCanvases(); HorizonPortraitRenderer.Render(world,ui,canvas,target);
                    RenderTexture old=RenderTexture.active; RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,540,960),0,0); pixels.Apply(); RenderTexture.active=old;
                    Directory.CreateDirectory("artifacts/cinematics");
                    File.WriteAllBytes("artifacts/cinematics/"+prefix+"-"+(next?"future":i.ToString())+".png",pixels.EncodeToPNG());
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
                string lossless="artifacts/cinematics/frames-"+prefix; Directory.CreateDirectory(lossless);
                using(var stream=File.Create("artifacts/cinematics/"+prefix+"-motion.gif"))
                using(var gif=new TimelineGifWriter(stream,540,960))
                    for(int frame=0;frame<frames;frame++)
                    {
                        if(frame>0) world.Cinematics.Advance(duration/frames);
                        yield return null; yield return null;
                        Canvas.ForceUpdateCanvases(); HorizonPortraitRenderer.Render(world,ui,canvas,target);
                        RenderTexture old=RenderTexture.active; RenderTexture.active=target;
                        pixels.ReadPixels(new Rect(0,0,540,960),0,0); pixels.Apply(); RenderTexture.active=old;
                        gif.Frame(pixels.GetPixels32(),5);
                        File.WriteAllBytes(Path.Combine(lossless,"frame-"+frame.ToString("D5")+".png"),pixels.EncodeToPNG());
                    }
                File.WriteAllBytes("artifacts/cinematics/"+prefix+"-motion.pcm",world.RecordedCinemaPcm());
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
        public IEnumerator CompletedPracticeEchoRecordsTheRealReleaseAndFourDecimalReceipt()
        {
            yield return new EnterPlayMode();
            var app=Object.FindObjectOfType<HorizonApp>() ?? new GameObject("Time echo capture app").AddComponent<HorizonApp>();
            yield return null; yield return null;
            var life=CreateMaturedEchoLife();
            var source=life.Master.events.Single(e=>e.kind==DomainEventKind.TimeEcho && e.receipt?.sourceDay==1);
            var action=life.Actions[0];
            Assert.That(action.actualLater.ticks.Any(value=>value>0));
            CollectionAssert.AreEqual(action.actualLater.ticks,source.receipt.resources.ticks);
            Assert.AreEqual(1,source.receipt.sourceDay);
            Assert.AreEqual(action.echoDay,source.day);
            string receipt=JsonUtility.ToJson(source.receipt);
            var world=(HorizonWorld3D)typeof(HorizonApp).GetField("world",Private).GetValue(app);
            var root=(RectTransform)typeof(HorizonApp).GetField("root",Private).GetValue(app);
            Canvas canvas=root.GetComponentInParent<Canvas>();
            RenderMode mode=canvas.renderMode; Camera prior=canvas.worldCamera; float distance=canvas.planeDistance;
            var ui=new GameObject("Time echo portrait camera",typeof(Camera)).GetComponent<Camera>();
            ui.enabled=false; ui.cullingMask=1<<5; ui.nearClipPlane=.1f; ui.farClipPlane=20;
            const string path="artifacts/time-echo-frames";
            foreach(string name in new[] {"ArchiveReader","ArchiveFolio"})
            {
                var assets=Resources.LoadAll<GameObject>("EchoArchive/"+name);
                Assert.That(assets.Length,Is.EqualTo(name=="ArchiveReader"?3:4),name);
                Assert.That(assets.All(asset=>asset.GetComponentInChildren<MeshRenderer>()!=null),name);
            }
            Assert.IsFalse(ShaderUtil.ShaderHasError(Resources.Load<Shader>("HorizonLit")));
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path,"receipt.json"),JsonUtility.ToJson(source,true));
            File.WriteAllText(Path.Combine(path,"source-life.json"),JsonUtility.ToJson(life.Snapshot(),true));
            try
            {
                foreach(string profile in new[] {"normal","battery","reduced"})
                {
                    bool reduced=profile=="reduced";
                    int height=profile=="normal"?960:1200;
                    var archive=new ArchiveData {active=life.Snapshot(),nextRareRun=99}; archive.Repair();
                    archive.preferences.sound=false; archive.preferences.haptics=false;
                    archive.preferences.batterySaver=profile=="battery"; archive.preferences.reducedMotion=reduced;
                    typeof(HorizonApp).GetField("archive",Private).SetValue(app,archive);
                    typeof(HorizonApp).GetField("session",Private).SetValue(app,life);
                    typeof(HorizonApp).GetMethod("Clear",Private).Invoke(app,new object[] {false});
                    typeof(HorizonApp).GetMethod("ApplyPreferences",Private).Invoke(app,null);
                    world.WorldCamera.aspect=540f/height;
                    world.Cinematics.enabled=false;
                    typeof(HorizonApp).GetMethod("PlayMasterSpectacle",Private).Invoke(app,new object[] {source.Copy(),(Action)(()=>{})});
                    var plan=world.Cinematics.Current;
                    Assert.IsTrue(plan.RecordedEcho);
                    Assert.IsTrue(plan.Outcome.Changes.All(change=>change.Copy().Contains(".")));
                    var caption=root.GetComponentInChildren<CinematicCaption>();
                    var seal=world.GetComponentsInChildren<Transform>().Single(t=>t.name.StartsWith("Recorded action:"));
                    var seat=world.GetComponentsInChildren<Transform>().Single(t=>t.name=="Stable echo receipt anchor");
                    var actor=world.GetComponentsInChildren<HorizonActor>().Single(t=>t.name=="Cinematic player");
                    var gains=plan.Outcome.Changes.Where(change=>change.Amount>0).Select(change=>change.Copy()).ToArray();
                    var final=string.Join("\n",gains);
                    var guideFlags=Enumerable.Range(1,RecordedEchoStory.GuideCount).Select(index=>seal.parent
                        .Find("Echo guide "+index+"/Echo contact flag "+index)).ToArray();
                    var mounts=Enumerable.Range(1,gains.Length).Select(index=>seal.parent.Find("Echo result mount "+index)).ToArray();
                    var group=seal.parent.Find("Assembled actual resources");
                    var objects=plan.Objects.Where(item=>item.Class==RewardObjectClass.Resource && item.Amount>0)
                        .Select(item=>group.Find(item.Kind.ToString())).ToArray();
                    var shutters=Enumerable.Range(1,gains.Length).Select(index=>seal.parent.Find("Archive aperture "+index+"/Archive shutter")).ToArray();
                    var ducts=Enumerable.Range(1,gains.Length).Select(index=>seal.parent.Find("Echo conduit "+index)).ToArray();
                    Assert.AreEqual(gains.Length,objects.Length);
                    var field=caption.transform.Find("Screen energy release");
                    var core=world.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="White light at the seat");
                    var target=new RenderTexture(540,height,24); target.Create(); ui.targetTexture=target;
                    var pixels=new Texture2D(540,height,TextureFormat.RGB24,false);
                    string folder=Path.Combine(path,profile); Directory.CreateDirectory(folder);
                    var local=seal.parent.Find("Archive aperture halo");
                    var trace=new System.Text.StringBuilder("frame,seconds,beat,seatDistance,release,field,guides,opened,visible,landed,local,conduits,receipt\n");
                    var cues=new System.Text.StringBuilder("time,phase\n");
                    world.Cinematics.Cue+=OnCue;
                    void OnCue(CinematicCue cue)
                    {
                        cues.AppendLine(FormattableString.Invariant($"{cue.Time:F3},{cue.Phase}"));
                    }
                    int frames=Mathf.CeilToInt(plan.Duration*20);
                    float firstResult=-1;
                    Vector3 startCamera=world.WorldCamera.transform.position;
                    try
                    {
                        foreach(Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=5;
                        canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=ui; canvas.planeDistance=5;
                        for(int frame=0;frame<frames;frame++)
                        {
                            world.Cinematics.Advance(frame==0?0:.05f);
                            yield return null;
                            float time=world.Cinematics.Elapsed;
                            if(caption.Multiplier.text.Length>0 && firstResult<0) firstResult=time;
                            if(frame==23 && !reduced)
                            {
                                world.Cinematics.SetPaused(true);
                                Vector3 frozen=seal.position;
                                world.Cinematics.Advance(.50f);
                                Assert.AreEqual(time,world.Cinematics.Elapsed);
                                Assert.AreEqual(frozen,seal.position);
                                world.Cinematics.SetPaused(false);
                            }
                            float remaining=Vector3.Distance(seal.position,seat.position);
                            bool flash=core.gameObject.activeInHierarchy;
                            int illuminated=0, landed=0, opened=0, shown=0;
                            for(int i=0;i<guideFlags.Length;i++)
                            {
                                bool raised=Quaternion.Angle(Quaternion.identity,guideFlags[i].localRotation)>21;
                                if(raised) illuminated++;
                                float at=RecordedEchoStory.GuideAt(i);
                                if(reduced || time>=at+.16f) Assert.IsTrue(raised);
                                else if(time<at) Assert.IsFalse(raised);
                            }
                            for(int i=0;i<gains.Length;i++)
                            {
                                float turn=RecordedEchoStory.ResultTurnAt(i,gains.Length);
                                bool visible=objects[i].gameObject.activeInHierarchy;
                                if(visible) shown++;
                                if(!shutters[i].gameObject.activeSelf) opened++;
                                if(!reduced && time<turn-.001f)
                                {
                                    Assert.IsFalse(visible,"A sealed aperture gave away a future row");
                                    Assert.IsTrue(shutters[i].gameObject.activeSelf);
                                    Assert.IsFalse(caption.Multiplier.text.Contains(gains[i].Split(' ')[0]));
                                }
                                if(reduced || time>turn+.28f) Assert.IsTrue(visible);
                                float at=RecordedEchoStory.ResultContactAt(i,gains.Length);
                                if(!reduced && time<at+.17f) continue;
                                Assert.IsTrue(mounts[i].gameObject.activeInHierarchy);
                                Assert.That(caption.Multiplier.text,Does.Contain(gains[i]));
                                var renderers=objects[i].GetComponentsInChildren<Renderer>();
                                float bottom=renderers.Min(renderer=>renderer.bounds.min.y);
                                Vector3 glass=mounts[i].Find("Recessed contact glass").position;
                                Assert.That(bottom-glass.y,Is.InRange(-.015f,.035f),gains[i]);
                                landed++;
                            }
                            Assert.IsTrue(actor.gameObject.activeInHierarchy && seal.gameObject.activeInHierarchy);
                            if(reduced)
                            {
                                Assert.IsFalse(flash || field.gameObject.activeSelf);
                                Assert.AreEqual(final,caption.Multiplier.text);
                                Assert.That(Vector3.Distance(startCamera,world.WorldCamera.transform.position),Is.LessThan(.001f));
                            }
                            else if(time>=RecordedEchoStory.ImpactAt+.19f && time<=RecordedEchoStory.ImpactAt+.38f)
                            {
                                Assert.That(remaining,Is.LessThan(.012f));
                                var hero=(Transform)typeof(HorizonWorld3D).GetField("showcaseHero",Private).GetValue(world);
                                Assert.IsTrue(hero.gameObject.activeInHierarchy);
                                AssertSubjectAperture(world,hero,HorizonCinemaLayout.SubjectAperture);
                            }
                            if(time<RecordedEchoStory.ConvergenceAt(gains.Length)) Assert.IsFalse(field.gameObject.activeSelf);
                            trace.AppendLine(FormattableString.Invariant($"{frame},{time:F3},{world.CurrentCinematicShot},{remaining:F4},{flash},{field.gameObject.activeSelf},{illuminated},{opened},{shown},{landed},{local.gameObject.activeInHierarchy},{ducts.Count(duct=>duct.gameObject.activeInHierarchy)},{caption.Multiplier.text.Replace('\n','|')}"));
                            Canvas.ForceUpdateCanvases(); HorizonPortraitRenderer.Render(world,ui,canvas,target);
                            RenderTexture old=RenderTexture.active;
                            try
                            {
                                RenderTexture.active=target;
                                pixels.ReadPixels(new Rect(0,0,540,height),0,0);
                                pixels.Apply(); File.WriteAllBytes(Path.Combine(folder,"frame-"+frame.ToString("D5")+".jpg"),pixels.EncodeToJPG(91));
                            }
                            finally { RenderTexture.active=old; }
                        }
                        if(!reduced)
                            Assert.That(firstResult-RecordedEchoStory.ImpactAt,Is.InRange(.19f,.35f));
                        Assert.AreEqual(final,caption.Multiplier.text);
                        Assert.AreEqual(receipt,JsonUtility.ToJson(source.receipt));
                        File.WriteAllText(Path.Combine(folder,"complete.txt"),frames.ToString());
                        File.WriteAllText(Path.Combine(folder,"trace.csv"),trace.ToString());
                        File.WriteAllText(Path.Combine(folder,"cues.csv"),cues.ToString());
                        File.WriteAllBytes(Path.Combine(folder,"cues.pcm"),world.RecordedCinemaPcm());
                        File.WriteAllText(Path.Combine(folder,"capture.txt"),
                            $"actual {source.id} / {profile} / 540x{height} @ 20 fps / Unity {Application.unityVersion} / {SystemInfo.graphicsDeviceName} / {SystemInfo.graphicsDeviceType} / {SystemInfo.deviceModel}");
                        world.Cinematics.Skip(); Assert.IsFalse(world.Cinematics.IsPlaying);
                        Assert.AreEqual(final,caption.Multiplier.text);
                        var next=root.GetComponentsInChildren<Button>().Single(button=>button.name=="Continue master event");
                        Assert.IsTrue(next.interactable); next.onClick.Invoke(); yield return null;
                        CollectionAssert.AreEqual(action.actualLater.ticks,GameSession.Restore(archive.active).Actions[0].actualLater.ticks);
                    }
                    finally
                    {
                        world.Cinematics.Cue-=OnCue;
                        canvas.renderMode=mode; canvas.worldCamera=prior; canvas.planeDistance=distance;
                        ui.targetTexture=null; target.Release(); Object.Destroy(target); Object.Destroy(pixels);
                    }
                }
            }
            finally
            {
                world.Cinematics.CancelAll(); world.Cinematics.enabled=true; world.WorldCamera.ResetAspect();
                Object.Destroy(ui.gameObject);
            }
            yield return new ExitPlayMode();
        }

        private static GameSession CreateMaturedEchoLife()
        {
            var life=GameSession.StartMasterLife(1,17,RunMode.Quick);
            CardSpec practice=life.Hand.Single(card=>card.Id=="practice");
            life.BeginPractice(practice,1); life.PickPracticeRoute(false);
            var action=life.FinishPractice();
            while(life.Day<action.echoDay)
            {
                if(life.NeedsStation) life.VisitStation();
                life.Advance();
                if(life.Day==action.echoDay) break;
                if(life.HasPredictionReview) life.MarkPredictionReviewed();
                if(life.CanPredict) life.SkipPrediction();
                life.Choose(life.Hand[2].Id);
            }
            return life;
        }

        [UnityTest]
        public IEnumerator EachReceiptLightsItsOwnObjectAndMixedLossWithdrawsMoney()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Result contact regression").AddComponent<HorizonWorld3D>();
            world.Initialize(); world.WorldCamera.aspect=9f/20;
            var prefs=new PlayerPreferences { sound=false,haptics=false };
            world.ApplyPreferences(prefs); world.Cinematics.enabled=false;
            var source=new DomainEvent { id="mixed-resource",kind=DomainEventKind.ActionTaken,tier=RewardTier.Local,
                receipt=new RewardReceipt { action="去公园散步",resourcesRecorded=true,resources=new ResourceDelta(energy:2,money:-1) } };
            try
            {
                world.Cinematics.Enqueue(source,true);
                var plan=world.Cinematics.Current; float start=new RewardSpectacle(plan).MultiplierTime;
                world.Cinematics.Advance(start-.25f);
                var stage=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Pooled 3D reward spectacle");
                Assert.IsFalse(stage.Find("Reward showcase EnergyCell").gameObject.activeInHierarchy,
                    "The upcoming product must not intrude into the park before the receipt cut.");
                var membrane=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Optical depth membrane 0");
                Assert.Greater(membrane.GetComponent<Renderer>().sharedMaterial.GetFloat("_Charge"),.4f,
                    "Carry the charge through the interval between the actor's impact and the first result.");
                world.Cinematics.Advance(start+ImmersiveMotion.RevealContactSeconds-world.Cinematics.Elapsed);
                Transform field=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Resource reveal horizon");
                Material mat=field.GetComponent<Renderer>().sharedMaterial;
                Assert.IsTrue(field.gameObject.activeInHierarchy);
                Assert.Greater(mat.GetFloat("_Pulse"),.98f);
                AssertResultLightAffectsRenderedPixels(world,field);
                float second=start+CinematicOutcome.ChangeSeconds;
                world.Cinematics.Advance(second+.02f-world.Cinematics.Elapsed);
                Transform wallet=field.parent.Find("Reward showcase MoneyWallet");
                Transform coin=wallet.Find("Money arriving");
                Vector3 pocket=coin.localPosition;
                Assert.IsTrue(wallet.gameObject.activeInHierarchy);
                world.Cinematics.Advance(.25f);
                Assert.AreEqual(0,mat.GetFloat("_Pulse")); Assert.AreEqual(0,mat.GetFloat("_Gain"));
                Assert.Greater(coin.localPosition.y,pocket.y);
                float age=mat.GetFloat("_Age"); world.Cinematics.SetPaused(true);
                world.Cinematics.Advance(.8f); world.FrameCinematicLabels();
                Assert.AreEqual(age,mat.GetFloat("_Age")); world.Cinematics.SetPaused(false);
                prefs.reducedMotion=true; world.ApplyPreferences(prefs);
                Vector3 withdrawn=coin.localPosition; world.Cinematics.Advance(.36f);
                Assert.AreEqual(withdrawn,coin.localPosition); Assert.Greater(withdrawn.y,pocket.y+.8f);
                world.ToggleCinematicOverview(); Assert.IsFalse(field.gameObject.activeInHierarchy);
                world.Cinematics.Skip(); Assert.IsFalse(field.gameObject.activeInHierarchy);
            }
            finally { world.Cinematics.CancelAll(); Object.Destroy(world.gameObject); }
            yield return new ExitPlayMode();
        }

        private static void AssertResultLightAffectsRenderedPixels(HorizonWorld3D world,Transform field)
        {
            Camera camera=world.WorldCamera;
            RenderTexture prior=camera.targetTexture,active=RenderTexture.active;
            var target=RenderTexture.GetTemporary(180,400,24);
            var image=new Texture2D(180,400,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;
                world.FrameCinematicLabels();
                camera.Render(); RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,180,400),0,0); image.Apply();
                Color32[] lit=image.GetPixels32();
                field.gameObject.SetActive(false);
                camera.Render(); RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,180,400),0,0); image.Apply();
                Color32[] bare=image.GetPixels32();
                int brighter=0;
                for(int y=155;y<320;y++)
                    for(int x=5;x<175;x++)
                    {
                        int i=y*180+x;
                        if(lit[i].r>bare[i].r+12 || lit[i].g>bare[i].g+12 || lit[i].b>bare[i].b+12) brighter++;
                    }
                Assert.Greater(brighter,80,"The contact must actually light pixels around the subject.");
            }
            finally
            {
                camera.targetTexture=prior; RenderTexture.active=active; world.FrameCinematicLabels();
                RenderTexture.ReleaseTemporary(target); Object.Destroy(image);
            }
        }

        private static GameSession CreateActualLife()
        {
            // Keep the captured predicate OUT of the EnterPlayMode coroutine.
            // Its display class would otherwise be allocated before the domain
            // reload even though the source declaration follows that yield.
            var life=GameSession.StartMasterLife(1,15,RunMode.Quick);
            CardSpec card=life.Hand.First(c=>life.CanPlay(c) && !life.NeedsImagination(c) && c.Delay>0);
            life.Choose(card.Id); return life;
        }
        private static GameSession CreateWalkLife()
        {
            // Real hand and real Choose, never a fabricated +3 receipt.
            for(int seed=0;seed<1024;seed++)
            {
                var life=GameSession.StartMasterLife(2,seed,RunMode.Quick);
                CardSpec walk=life.Hand.FirstOrDefault(c=>c.Id=="walk" && life.CanPlay(c) && !life.NeedsImagination(c));
                if(walk==null) continue;
                life.Choose(walk.Id); return life;
            }
            Assert.Fail("A recorded walk must come from a playable hand."); return null;
        }
        private static void AssertSubjectAperture(HorizonWorld3D world,Transform subject,Rect aperture)
        {
            foreach(Renderer renderer in subject.GetComponentsInChildren<Renderer>())
            {
                if(!renderer.enabled || renderer is LineRenderer || renderer is ParticleSystemRenderer) continue;
                Bounds bounds=renderer.bounds;
                for(int corner=0;corner<8;corner++)
                {
                    Vector3 p=bounds.center+Vector3.Scale(bounds.extents,new Vector3(
                        (corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                    Vector3 point=world.WorldCamera.WorldToViewportPoint(p);
                    Assert.Greater(point.z,world.WorldCamera.nearClipPlane,renderer.name);
                    Assert.That(point.x,Is.InRange(aperture.xMin-.006f,aperture.xMax+.006f),renderer.name+" horizontal crop");
                    Assert.That(point.y,Is.InRange(aperture.yMin-.006f,aperture.yMax+.006f),renderer.name+" invades the reading bands");
                }
            }
        }
        [UnityTest]
        public IEnumerator MovingRehearsalActorsStayInsideTheirReadingApertureOnBothPhoneShapes()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Portrait subject framing regression").AddComponent<HorizonWorld3D>(); world.Initialize();
            var run=ImaginationEngine.Begin("portrait-framing","portfolio","找工作",1,2);
            try
            {
                for(int step=0;step<20;step++)
                {
                    string before=JsonUtility.ToJson(run);
                    foreach(float aspect in new[] {9f/16,9f/20})
                        foreach(bool reduced in new[] {false,true})
                        {
                            world.WorldCamera.aspect=aspect;
                            world.ApplyPreferences(new PlayerPreferences {sound=false,haptics=false,reducedMotion=reduced});
                            world.ShowImaginationScene(run);
                            Transform actor=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Cinematic player");
                            foreach(float time in new[] {0f,.5f,1.5f,2.9f,3.4f,6.5f,10f})
                            {
                                world.SampleImaginationAt(time);
                                AssertSubjectAperture(world,actor,HorizonCinemaLayout.ImaginationAperture);
                                Vector3 head=world.WorldCamera.WorldToViewportPoint(actor.position+Vector3.up*1.3f);
                                Assert.That(head.x,Is.InRange(.09f,.91f),run.phase+" native focus point");
                            }
                            world.EndImaginationScene();
                        }
                    Assert.AreEqual(before,JsonUtility.ToJson(run));
                    if(run.phase==ImaginePhase.Complete) break;
                    if(run.phase==ImaginePhase.Preparation) ImaginationEngine.Prepare(run,PreparationAction.SmallStep);
                    else if(run.phase==ImaginePhase.Recover) ImaginationEngine.Recover(run,RecoveryAction.Rest);
                    else ImaginationEngine.Continue(run);
                }
                Assert.AreEqual(ImaginePhase.Complete,run.phase);
            }
            finally {world.EndImaginationScene(); Object.Destroy(world.gameObject);}
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
