using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
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
            Assert.IsNotNull(life); Assert.IsNotNull(life.Master);
            RunSnapshot initial = life.Snapshot(); Assert.IsNotNull(initial.master);
            var archive = new ArchiveData { active = initial, nextRareRun = 99, seenSecondLife = true,
                preferences = new PlayerPreferences { reducedMotion = true, sound = false } };
            archive.Repair();
            Set(app, "session", life); Set(app, "archive", archive); Call(app, "ApplyPreferences"); Call(app, "BuildBoard");
            yield return new WaitForSecondsRealtime(0.4f);
            // A captured GameSession cannot be reconstructed when EnterPlayMode reloads this coroutine.
            HorizonCardDrag card = null;
            foreach (HorizonCardDrag candidate in Get<RectTransform>(app, "root").GetComponentsInChildren<HorizonCardDrag>())
                if (candidate.name == life.Hand[1].Id) { card = candidate; break; }
            Assert.IsNotNull(card);
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
            camera.depth = 20;
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 5;
            world.WorldCamera.targetTexture = image; world.SnapCamera(); yield return null; yield return null;
            Canvas.ForceUpdateCanvases(); View.RefreshText(canvas.transform); world.WorldCamera.Render(); camera.Render();
            RenderTexture previous = RenderTexture.active; RenderTexture.active = image;
            var pixels = new Texture2D(1080, 1920, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); pixels.Apply();
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "visuals"); Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
            RenderTexture.active = previous; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; world.WorldCamera.targetTexture = null;
            image.Release(); Object.Destroy(image); Object.Destroy(pixels); Object.Destroy(cameraObject);
            yield return null; yield return null;
        }

        private sealed class TestAITransport : IAITransport
        {
            public AIRequest last;
            public Task<AIResponse> Send(AIRequest request, CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested(); last = request;
                string content = JsonUtility.ToJson(new PersonalContent { futureSelfLine = "你已经练过失败后的下一步。现在想怎样继续？",
                    quest = "打开一份材料并读两分钟", patternExplanation = "近期模式可以被新的选择改变。" });
                string escaped = content.Replace("\\", "\\\\").Replace("\"", "\\\"");
                return Task.FromResult(new AIResponse(200, "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"" + escaped + "\"}}]}"));
            }
        }

        [UnityTest]
        public IEnumerator OnlineAISettingsAndFutureSelfUseActualUIWithoutPersistingCredentials()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>(); if (app == null) app = new GameObject("AI product flow").AddComponent<HorizonApp>();
            yield return null;
            var life = GameSession.StartMasterLife(2, 15, RunMode.Quick);
            var archive = new ArchiveData { active = life.Snapshot(), nextRareRun = 99, seenSecondLife = true };
            archive.preferences.reducedMotion = true; archive.preferences.sound = false;
            var transport = new TestAITransport(); Set(app, "aiTransport", transport);
            Set(app, "session", life); Set(app, "archive", archive); Call(app, "BuildBoard"); Call(app, "ShowSettings"); yield return null;
            Button(app, "Online AI settings").onClick.Invoke(); yield return null;
            Button(app, "AI provider 1").onClick.Invoke(); yield return null;
            InputField key = Get<RectTransform>(app, "root").GetComponentsInChildren<InputField>().Single(x => x.name == "AI provider key");
            Assert.AreEqual(InputField.ContentType.Password, key.contentType); key.text = "dummy-deepseek-session-key";
            yield return Capture(app, "45-ai-deepseek");
            Button(app, "Test AI connection").onClick.Invoke(); yield return null;
            Assert.That(transport.last.Url, Does.Contain("api.deepseek.com"));
            Button(app, "AI provider 2").onClick.Invoke(); yield return null;
            foreach (InputField field in Get<RectTransform>(app, "root").GetComponentsInChildren<InputField>())
            {
                if (field.name == "AI Azure endpoint") field.text = "https://example.openai.azure.com";
                if (field.name == "AI Azure deployment") field.text = "test-deployment";
                if (field.name == "AI provider key") field.text = "dummy-azure-session-key";
            }
            yield return Capture(app, "46-ai-azure");
            Button(app, "Save AI settings").onClick.Invoke(); yield return null;
            Assert.AreEqual(AIProvider.AzureOpenAI, archive.ai.provider);
            Assert.That(ArchiveStore.Encode(archive), Does.Not.Contain("dummy-azure-session-key").And.Not.Contain("dummy-deepseek-session-key"));
            Button(app, "Close settings").onClick.Invoke(); yield return null;
            Call(app, "ShowFutureSelfDialogue"); yield return null;
            string before = JsonUtility.ToJson(life.Snapshot());
            Button(app, "Generate personal content").onClick.Invoke(); yield return null;
            Assert.That(transport.last.Url, Does.Contain("/deployments/test-deployment/"));
            Assert.AreEqual(before, JsonUtility.ToJson(life.Snapshot())); Assert.AreEqual(0, archive.reality.quests.Count);
            yield return Capture(app, "47-ai-future-self");
            Button(app, "Accept AI reality suggestion").onClick.Invoke(); yield return null;
            Assert.AreEqual(1, archive.reality.quests.Count); Assert.IsFalse(archive.reality.quests[0].completed);
            Assert.IsTrue(string.IsNullOrEmpty(archive.reality.quests[0].memoryId), "Generated suggestions cannot invent a convergence.");
            yield return new ExitPlayMode();
        }
    }
}
