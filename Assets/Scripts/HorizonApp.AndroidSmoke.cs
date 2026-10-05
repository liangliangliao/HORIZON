#if UNITY_ANDROID && DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    // Only the separately identified development APK accepts this test intent.
    // The preview APK does not compile this code or touch the test archive.
    internal static class AndroidSmoke
    {
        [Serializable] internal sealed class Report
        {
            public string status, error, graphics, device;
            public int width, height;
            public string[] frames, errors;
        }
        internal static bool Active, SawPause, SawResume;
        internal static readonly List<string> Errors = new List<string>();
        internal static readonly List<string> Frames = new List<string>();
        internal static string DirectoryPath => Path.Combine(Application.persistentDataPath, "android-smoke");
        internal static void Begin()
        {
            if (Active || Application.identifier != "com.liangliangliao.horizon.smoke") return;
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                Active = intent.Call<bool>("getBooleanExtra", "horizonSmoke", false);
            if (!Active) return;
            Directory.CreateDirectory(DirectoryPath);
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                { Errors.Add(message + "\n" + stack); Write("failed", message); }
            };
            Write("starting");
        }
        internal static void NotePause(bool paused)
        { if (!Active) return; if (paused) SawPause=true; else if (SawPause) SawResume=true; }
        internal static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        internal static void Write(string status, string error = "")
        {
            var report=new Report { status=status,error=error,graphics=SystemInfo.graphicsDeviceType.ToString(),
                device=SystemInfo.graphicsDeviceName,width=Screen.width,height=Screen.height,
                frames=Frames.ToArray(),errors=Errors.ToArray() };
            string file=Path.Combine(DirectoryPath,"report.json"), temporary=file+".tmp";
            File.WriteAllText(temporary,JsonUtility.ToJson(report,true));
            if (File.Exists(file)) File.Delete(file); File.Move(temporary,file);
        }
    }

    public sealed partial class HorizonApp
    {
        private bool androidSmokeRewardReturned;
        private void AndroidSmokeFinishReward() { androidSmokeRewardReturned=true; }
        private IEnumerator RunAndroidSmoke()
        {
            IEnumerator flow=AndroidSmokeFlow();
            while (true)
            {
                bool more;
                try { more=flow.MoveNext(); }
                catch (Exception error) { AndroidSmoke.Write("failed",error.ToString()); yield break; }
                if (!more)
                {
                    AndroidSmoke.Write(AndroidSmoke.Errors.Count==0?"passed":"failed",
                        AndroidSmoke.Errors.Count==0?"":"Unity logged a runtime error.");
                    yield break;
                }
                yield return flow.Current;
            }
        }
        private IEnumerator AndroidSmokeFlow()
        {
            AndroidSmoke.Require(world!=null && world.Avatar!=null && root!=null,"Android startup did not finish.");
            AndroidSmoke.Require(root.GetComponentInParent<Canvas>().renderMode==RenderMode.ScreenSpaceOverlay,
                "Smoke must exercise the normal phone canvas.");
            AndroidSmoke.Require(world.WorldCamera.targetTexture==null,"Smoke must render to the phone backbuffer.");
            AndroidSmoke.Require(!world.WorldCamera.allowMSAA && QualitySettings.antiAliasing==0,
                "GLES scene/backbuffer multisampling is inconsistent.");
            session=GameSession.StartMasterLife(2,41,RunMode.Quick);
            archive=new ArchiveData { active=session.Snapshot(),seenSecondLife=true,nextRareRun=99 };
            archive.Repair(); archive.preferences.sound=false; archive.preferences.haptics=false;
            archive.wallet.Claim("native-smoke-fixture",142); ApplyPreferences(); ShowHome(); world.SnapCamera();
            yield return new WaitForSecondsRealtime(.5f); yield return new WaitForEndOfFrame();
            CaptureAndroidFrame("01-home",world.Avatar);
            GameObject[] home=root.Cast<Transform>().Where(t=>t.gameObject.activeSelf).Select(t=>t.gameObject).ToArray();
            string actual=JsonUtility.ToJson(session.Snapshot()); Rect viewport=world.WorldCamera.rect;
            SmokeButton("Master hub"); yield return null; SmokeButton("Master feature 0"); yield return null;
            imagineGoal="找工作"; imagineFailures=1; ShowImagineSetup(); SmokeButton("Start imagination");
            for (int step=0;step<16;step++)
            {
                yield return new WaitForSecondsRealtime(.5f); yield return new WaitForEndOfFrame();
                AndroidSmoke.Require(overlay!=null && overlay.name=="Imagination run","Imagination page was replaced unexpectedly.");
                AndroidSmoke.Require(home.All(g=>g!=null && !g.activeInHierarchy),"Home controls leaked through the transparent page.");
                AndroidSmoke.Require(root.Cast<Transform>().Count(t=>t.gameObject.activeInHierarchy)==1,"Multiple full-screen pages are active.");
                HorizonActor actor=world.GetComponentsInChildren<HorizonActor>().Single(a=>a.name=="Cinematic player");
                CaptureAndroidFrame("02-imagine-"+step+"-"+archive.imagination.phase,actor.transform);
                if (archive.imagination.phase==ImaginePhase.Complete) break;
                Button next=overlay.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.interactable &&
                    (b.name=="Continue imagination" || b.name=="Preparation action 0" || b.name=="Recovery action 0"));
                AndroidSmoke.Require(next!=null,"The next imagination action is unavailable."); next.onClick.Invoke();
            }
            AndroidSmoke.Require(archive.imagination.phase==ImaginePhase.Complete,"The imagination sequence did not complete.");
            AndroidSmoke.Require(JsonUtility.ToJson(session.Snapshot())==actual,"Imagination changed the real life.");
            SmokeButton("Master back"); yield return null; SmokeButton("Master back"); yield return null;
            AndroidSmoke.Require(home.All(g=>g.activeInHierarchy),"Home was not restored after returning.");
            AndroidSmoke.Require(world.WorldCamera.rect==viewport,"The home viewport was not restored.");
            SmokeButton("Settings"); yield return null;
            AndroidSmoke.Require(VisualPreferences.Paused && world.WorldCamera.enabled,"Pause removed the scene camera.");
            Vector3 position=world.WorldCamera.transform.position;
            yield return new WaitForSecondsRealtime(.5f);
            AndroidSmoke.Require(world.WorldCamera.transform.position==position,"The paused camera moved.");
            SmokeButton("Close settings"); yield return new WaitForSecondsRealtime(.5f); yield return new WaitForEndOfFrame();
            AndroidSmoke.Require(!VisualPreferences.Paused,"Settings did not resume the scene.");
            CaptureAndroidFrame("03-returned-home",world.Avatar);
            AndroidSmoke.SawPause=false; AndroidSmoke.SawResume=false;
            AndroidSmoke.Write("await_background");
            float deadline=Time.realtimeSinceStartup+90;
            while (!AndroidSmoke.SawResume && Time.realtimeSinceStartup<deadline) yield return null;
            AndroidSmoke.Require(AndroidSmoke.SawPause && AndroidSmoke.SawResume,"Android did not complete a background/foreground transition.");
            yield return new WaitForSecondsRealtime(.5f); yield return new WaitForEndOfFrame();
            AndroidSmoke.Require(world.WorldCamera.enabled && home.All(g=>g.activeInHierarchy),"Resume left the phone blank.");
            CaptureAndroidFrame("04-resumed-home",world.Avatar);
            AndroidSmoke.Require(TMPro.TMP_Settings.instance!=null,"TextMeshPro settings are missing from the APK.");
            // Reproduce the completed reward's system-back path with a real
            // generated pattern event, and inject KEYCODE_BACK from the host.
            var model=new PlayerBehavioralModel(); model.Observe(new[] {
                new BehaviorObservation { id="native-a",run=1,day=1,key="decision-reopen",nodeId="first" },
                new BehaviorObservation { id="native-b",run=2,day=1,key="decision-reopen",nodeId="second" } });
            var rewardLife=GameSession.StartMasterLife(3,41,RunMode.MirrorRun,model);
            rewardLife.LockDecision(rewardLife.Hand[1].Id);
            for(int i=0;i<4;i++) rewardLife.ExecuteDecisionStep(); rewardLife.Choose(rewardLife.Master.decision.cardId);
            DomainEvent reward=rewardLife.Master.events.Single(e=>e.kind==DomainEventKind.PatternBroken);
            androidSmokeRewardReturned=false; PlayMasterSpectacle(reward,AndroidSmokeFinishReward);
            AndroidSmoke.Require(overlay.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Length>=4,
                "The reward did not create its cinematic captions.");
            yield return new WaitForSecondsRealtime(.5f); yield return new WaitForEndOfFrame();
            AndroidSmoke.Require(home.All(g=>!g.activeInHierarchy),"Home leaked through the reward movie.");
            CaptureAndroidFrame("05-reward-visible",world.GetComponentsInChildren<HorizonActor>().Single(a=>a.name=="Cinematic player").transform);
            SmokeButton("Skip cinematic"); yield return null;
            AndroidSmoke.Write("await_back"); deadline=Time.realtimeSinceStartup+30;
            while (!androidSmokeRewardReturned && Time.realtimeSinceStartup<deadline) yield return null;
            AndroidSmoke.Require(androidSmokeRewardReturned && overlay==null && home.All(g=>g.activeInHierarchy),
                "Android back bypassed the reward continuation and left the interface hidden.");
            AndroidSmoke.Require(reward.acknowledged && world.WorldCamera.rect==viewport,"Reward back did not restore the scene.");
            yield return new WaitForSecondsRealtime(.5f); yield return new WaitForEndOfFrame();
            CaptureAndroidFrame("06-reward-back-home",world.Avatar);
        }
        private void SmokeButton(string name)
        {
            Button button=root.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name==name && b.interactable);
            AndroidSmoke.Require(button!=null,"Missing visible button: "+name); button.onClick.Invoke();
        }
        private void CaptureAndroidFrame(string name,Transform actor)
        {
            AndroidSmoke.Require(world.WorldCamera.enabled && actor.gameObject.activeInHierarchy,"The scene actor/camera is hidden.");
            Texture2D frame=ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                AndroidSmoke.Require(frame!=null && frame.width>100 && frame.height>200,"The phone did not produce a frame.");
                // Retain the normal backbuffer frame even if the visibility
                // assertion fails, so the actual rendering failure is reviewable.
                File.WriteAllBytes(Path.Combine(AndroidSmoke.DirectoryPath,name+".png"),frame.EncodeToPNG());
                if (name=="01-home")
                {
                    Rect sky=world.WorldCamera.rect;
                    Color sample=frame.GetPixel((int)(frame.width*.04f),(int)(frame.height*(sky.yMax-.02f)));
                    AndroidSmoke.Require(sample.b>sample.r && sample.b>.025f,
                        "The Android night sky lost its blue channel: "+sample);
                }
                Vector3 point=world.WorldCamera.WorldToScreenPoint(actor.position+Vector3.up*1.3f);
                AndroidSmoke.Require(point.z>0 && point.x>0 && point.x<frame.width && point.y>0 && point.y<frame.height,"The scene actor is outside the phone viewport.");
                float minimum=1,maximum=0;
                int radius=Mathf.Max(12,frame.width/24);
                for(int y=Mathf.Max(0,(int)point.y-radius);y<Mathf.Min(frame.height,(int)point.y+radius);y+=3)
                    for(int x=Mathf.Max(0,(int)point.x-radius);x<Mathf.Min(frame.width,(int)point.x+radius);x+=3)
                    { Color c=frame.GetPixel(x,y); float light=Mathf.Max(c.r,c.g,c.b); minimum=Mathf.Min(minimum,light); maximum=Mathf.Max(maximum,light); }
                AndroidSmoke.Require(maximum-minimum>.045f && maximum>.12f,"The Android 3D viewport is blank or uniformly gray: "+name);
                AndroidSmoke.Frames.Add(name); AndroidSmoke.Write("running");
            }
            finally { if (frame!=null) Destroy(frame); }
        }
    }
}
#endif
