using System;
using System.Collections;
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
    public sealed class MasterPlayableFlowTests
    {
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Get<T>(HorizonApp app, string field) { return (T)typeof(HorizonApp).GetField(field, Private).GetValue(app); }
        private static void Set(HorizonApp app, string field, object value) { typeof(HorizonApp).GetField(field, Private).SetValue(app, value); }
        private static void Call(HorizonApp app, string method, params object[] args) { typeof(HorizonApp).GetMethod(method, Private).Invoke(app, args); }
        private static Button Button(HorizonApp app, string name)
        { var b = Get<RectTransform>(app, "root").GetComponentsInChildren<Button>().FirstOrDefault(x => x.name == name); Assert.IsNotNull(b, name); return b; }

        [SetUp]
        public void IsolateStorage()
        {
            PlayerPrefs.DeleteKey("HORIZON.PROTOTYPE.V1"); Directory.CreateDirectory(Application.persistentDataPath);
            foreach (string file in Directory.GetFiles(Application.persistentDataPath, "HORIZON.life.json*")) File.Delete(file);
        }

        [UnityTest]
        public IEnumerator MasterFeaturesCanBePlayedThroughTheActualPortraitUI()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>(); if (app == null) app = new GameObject("Master product flow").AddComponent<HorizonApp>();
            yield return null;
            var life = GameSession.StartMasterLife(2, 15, RunMode.ExperimentRun);
            var archive = new ArchiveData { active = life.Snapshot(), nextRareRun = 99, seenSecondLife = true };
            archive.preferences.reducedMotion = true; archive.preferences.sound = false;
            Set(app, "session", life); Set(app, "archive", archive); Call(app, "ApplyPreferences"); Call(app, "BuildBoard");
            yield return new WaitForSecondsRealtime(0.4f);
            HorizonCardDrag card = Get<RectTransform>(app, "root").GetComponentsInChildren<HorizonCardDrag>().First(x => x.name == life.Hand[1].Id);
            Call(app, "CardTapped", card); yield return null;
            Button(app, "Lock decision").onClick.Invoke(); yield return null;
            Assert.IsTrue(life.InExecutionMode);
            Button(app, "Equip triggers").onClick.Invoke(); yield return null;
            Button(app, "Trigger alarm").onClick.Invoke(); yield return null;
            Assert.Contains("alarm", life.Master.triggers);
            Button(app, "Master back").onClick.Invoke(); yield return null;
            yield return Capture(app, "40-master-execution");
            for (int i = 0; i < 4; i++) { Button(app, "Execute next step").onClick.Invoke(); yield return null; }
            Assert.AreEqual(DecisionStatus.Ready, life.Master.decision.status);
            // Leave the completed decision available, then train an imagined setback.
            Button(app, "Master back").onClick.Invoke(); yield return null;
            Button(app, "Master feature 0").onClick.Invoke(); yield return null;
            Button(app, "Start imagination").onClick.Invoke(); yield return null;
            Assert.AreEqual(ImaginePhase.VictoryAnchor, archive.imagination.phase);
            yield return Capture(app, "41-victory-anchor");
            int safety = 0;
            while (archive.imagination.phase != ImaginePhase.Complete && safety++ < 40)
            {
                if (archive.imagination.phase == ImaginePhase.Recover) { yield return Capture(app, "42-imagine-recovery"); Button(app, "Recovery action 1").onClick.Invoke(); }
                else Button(app, "Continue imagination").onClick.Invoke();
                yield return null;
            }
            Assert.AreEqual(1, archive.imagination.failures); Assert.AreEqual(1, archive.imagination.recovered);
            Button(app, "Keep future memory").onClick.Invoke(); yield return null;
            Assert.AreEqual(1, life.Master.memories.Count); Assert.AreEqual(1, archive.futureMemories.Count);
            Button(app, "Master feature 4").onClick.Invoke(); yield return null;
            yield return Capture(app, "43-future-orbit");
            Button(app, "Master back").onClick.Invoke(); yield return null;
            Button(app, "Master feature 5").onClick.Invoke(); yield return null;
            Button(app, "Accept reality quest").onClick.Invoke(); yield return null;
            Button(app, "Complete reality quest").onClick.Invoke(); yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(1, archive.reality.quests.Count(q => q.completed));
            Assert.IsFalse(archive.reality.events.Any(e => e.kind == DomainEventKind.RealityConvergence), "An imagined path still needs a simulated action.");
            Button(app, "Continue master event").onClick.Invoke(); yield return null;
            Assert.IsFalse(Get<RectTransform>(app, "root").GetComponentsInChildren<Button>().Any(b => b.name == "Complete reality quest"));
            Button(app, "Reality constellation").onClick.Invoke(); yield return null;
            yield return Capture(app, "44-reality-constellation");
            yield return new ExitPlayMode();
        }
        private static IEnumerator Capture(HorizonApp app, string name)
        {
            var world = Get<HorizonWorld3D>(app, "world"); Canvas canvas = Get<RectTransform>(app, "root").GetComponentInParent<Canvas>();
            var image = new RenderTexture(1080, 1920, 24); image.Create();
            var cameraObject = new GameObject("Master portrait capture", typeof(Camera)); Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Depth; camera.cullingMask = 1 << 5; camera.nearClipPlane = 0.1f; camera.farClipPlane = 20; camera.targetTexture = image;
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 5;
            world.WorldCamera.targetTexture = image; world.SnapCamera(); yield return null; yield return null;
            Canvas.ForceUpdateCanvases(); View.RefreshText(canvas.transform); world.WorldCamera.Render(); camera.Render();
            RenderTexture previous = RenderTexture.active; RenderTexture.active = image;
            var pixels = new Texture2D(1080, 1920, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); pixels.Apply();
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "visuals"); Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
            RenderTexture.active = previous; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; world.WorldCamera.targetTexture = null;
            image.Release(); Object.Destroy(image); Object.Destroy(pixels); Object.Destroy(cameraObject);
        }
    }
}
