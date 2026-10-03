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

        private static HorizonCardDrag Card(HorizonApp app, string id)
        { return Get<RectTransform>(app, "root").GetComponentsInChildren<HorizonCardDrag>().Single(x => x.name == id); }

        private static IEnumerator FinishDailyFeedback(HorizonApp app, int fromDay)
        {
            float until = Time.realtimeSinceStartup + 15;
            while (Time.realtimeSinceStartup < until)
            {
                if (!Get<bool>(app, "busy") && Get<ArchiveData>(app, "archive").pendingFeedback == null && Get<GameSession>(app, "session").Day > fromDay) yield break;
                var next = Get<RectTransform>(app, "root").GetComponentsInChildren<Button>().FirstOrDefault(b =>
                    b.interactable && (b.name == "Continue result" || b.name == "Continue master event"));
                if (next != null) next.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.15f);
            }
            Assert.Fail("Daily feedback did not return to the next choice.");
        }

        [UnityTest]
        public IEnumerator FirstLifeTeachesTimeAndPreparesAChoiceWithoutVisitingTheFeatureHub()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>(); if (app == null) app = new GameObject("First life experience").AddComponent<HorizonApp>();
            yield return null;
            var archive = new ArchiveData { nextRareRun = 99, preferences = new PlayerPreferences { reducedMotion = true, sound = false } }; archive.Repair();
            Set(app, "archive", archive); Call(app, "ApplyPreferences"); Call(app, "StartNewRun"); yield return null;
            GameSession life = Get<GameSession>(app, "session");
            Assert.AreEqual(1, life.RunNumber); Assert.AreEqual(0, life.Actions.Count); Assert.AreEqual(0, archive.wallet.stardust);
            yield return Capture(app, "50-first-life-goal");
            Button(app, "Guide next").onClick.Invoke(); yield return null;
            yield return Capture(app, "51-first-life-choice");
            archive = JsonUtility.FromJson<ArchiveData>(JsonUtility.ToJson(archive)); archive.Repair(); Set(app, "archive", archive);
            Call(app, "ContinueRun"); yield return null; life = Get<GameSession>(app, "session");
            Assert.AreEqual(1, archive.playGuide.page);
            Button(app, "Guide start").onClick.Invoke(); yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsTrue(archive.playGuide.completed);
            Call(app, "RevealResourceNumbers", false);
            Assert.IsTrue(Get<RectTransform>(app, "root").GetComponentsInChildren<Text>().Where(t => t.name == "Resource number").All(t => t.text.Contains("/10")));
            yield return Capture(app, "52-first-life-board");
            Call(app, "CardTapped", Card(app, life.Hand[1].Id)); yield return null;
            Button(app, "Use card").onClick.Invoke();
            yield return FinishDailyFeedback(app, 1);
            Assert.AreEqual(2, life.Day); Assert.AreEqual(1, life.Actions.Count);
            string before = JsonUtility.ToJson(life.Snapshot());
            Button(app, "Future plans").onClick.Invoke(); yield return null;
            yield return Capture(app, "53-first-life-future-schedule");
            Assert.AreEqual(before, JsonUtility.ToJson(life.Snapshot()));
            Button(app, "Master back").onClick.Invoke(); yield return null;
            Call(app, "CardTapped", Card(app, life.Hand[2].Id)); yield return null;
            Button(app, "Use card").onClick.Invoke(); yield return FinishDailyFeedback(app, 2);
            Assert.AreEqual(3, life.Day);
            string selected = life.Hand[1].Id;
            Call(app, "CardTapped", Card(app, selected)); yield return null;
            Button(app, "Imagine this choice").onClick.Invoke(); yield return null;
            Button(app, "Start imagination").onClick.Invoke(); yield return null;
            while (archive.imagination.phase != ImaginePhase.Complete)
            {
                if (archive.imagination.phase == ImaginePhase.Recover) Button(app, "Recovery action 1").onClick.Invoke();
                else Button(app, "Continue imagination").onClick.Invoke();
                yield return null;
            }
            Button(app, "Keep future memory").onClick.Invoke(); yield return null;
            Assert.AreEqual(3, life.Day); Assert.AreEqual(2, life.Actions.Count); Assert.AreEqual(1, life.Master.memories.Count);
            Assert.AreEqual(CardCatalog.FindById(selected).Name, Get<RectTransform>(app, "overlay").GetComponentsInChildren<Text>().Single(t => t.name == "Name").text);
            Button(app, "Lock decision").onClick.Invoke(); yield return null;
            Button(app, "Equip triggers").onClick.Invoke(); yield return null;
            Button(app, "Trigger alarm").onClick.Invoke(); yield return null;
            Button(app, "Master back").onClick.Invoke(); yield return null;
            yield return Capture(app, "54-first-life-decision");
            for (int i = 0; i < 4; i++) { Button(app, "Execute next step").onClick.Invoke(); yield return null; }
            Button(app, "Execute next step").onClick.Invoke(); yield return FinishDailyFeedback(app, 3);
            Assert.IsTrue(life.CanPredict);
            Button(app, "Prediction decrease 0").onClick.Invoke(); Button(app, "Prediction decrease 0").onClick.Invoke();
            Button(app, "Prediction increase 1").onClick.Invoke(); yield return null;
            CollectionAssert.AreEqual(new[] { -2, 1, 0 }, Get<int[]>(app, "forecastOffsets").Take(3));
            yield return Capture(app, "55-first-life-prediction");
            Button(app, "Lock prediction").onClick.Invoke(); yield return null;
            Assert.AreEqual(-2, life.Prediction.energy);
            // Later day fixtures exercise the same default mode, not an experimental menu.
            while (life.Day < 11)
            {
                while (life.HasPredictionReview) life.MarkPredictionReviewed();
                if (life.CanPredict) life.SkipPrediction();
                life.Choose(life.Hand[2].Id); if (life.NeedsStation) life.VisitStation(); life.Advance();
                archive.active = life.Snapshot();
                if (life.Day == 5) { Call(app, "BuildBoard"); yield return null; yield return Capture(app, "56-first-life-new-family"); }
            }
            while (life.HasPredictionReview) life.MarkPredictionReviewed(); if (life.CanPredict) life.SkipPrediction();
            archive.active = life.Snapshot(); Call(app, "BuildBoard"); yield return null;
            Call(app, "CardTapped", Card(app, "imagine")); yield return null;
            Button(app, "Use card").onClick.Invoke(); yield return null;
            Button(app, "Start imagination").onClick.Invoke(); yield return null;
            while (archive.imagination.phase != ImaginePhase.Recover) { Button(app, "Continue imagination").onClick.Invoke(); yield return null; }
            yield return Capture(app, "57-imagine-card-recovery");
            Button(app, "Recovery action 2").onClick.Invoke(); yield return null;
            while (archive.imagination.phase != ImaginePhase.Complete) { Button(app, "Continue imagination").onClick.Invoke(); yield return null; }
            Button(app, "Keep future memory").onClick.Invoke(); yield return null;
            Assert.AreEqual(11, life.Day); Assert.AreEqual(10, life.Actions.Count);
            Assert.IsTrue(life.CanPlay(CardCatalog.FindById("imagine")));
            Assert.AreEqual(0, archive.reality.quests.Count, "Imagination never creates a real-world achievement automatically.");
            yield return new ExitPlayMode();
        }

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
            Button(app, "Master hub").onClick.Invoke(); yield return null;
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
            public readonly System.Collections.Generic.List<AIRequest> calls = new System.Collections.Generic.List<AIRequest>();
            public Task<AIResponse> Send(AIRequest request, CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested(); last = request; calls.Add(request);
                if (request.Method == "GET") return Task.FromResult(new AIResponse(200, "{\"data\":[{\"id\":\"auto-coach\"}]}"));
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
                if (field.name == "AI Azure resource name") field.text = "面试练习";
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
        [UnityTest]
        public IEnumerator FoundryResourceEditorMatchesOptionalFieldsAndDeploymentSelection()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>(); if (app == null) app = new GameObject("Foundry resource flow").AddComponent<HorizonApp>();
            yield return null;
            var life = GameSession.StartMasterLife(2, 15, RunMode.Quick);
            var archive = new ArchiveData { active = life.Snapshot(), nextRareRun = 99, seenSecondLife = true };
            archive.preferences.reducedMotion = true; archive.preferences.sound = false;
            var transport = new TestAITransport(); Set(app, "aiTransport", transport);
            Set(app, "session", life); Set(app, "archive", archive); Call(app, "BuildBoard"); Call(app, "ShowSettings"); yield return null;
            Button(app, "Online AI settings").onClick.Invoke(); yield return null;
            Button(app, "AI provider 2").onClick.Invoke(); yield return null;
            foreach (InputField field in Get<RectTransform>(app, "root").GetComponentsInChildren<InputField>())
            {
                if (field.name == "AI Azure resource name") field.text = "我的 Azure 资源";
                if (field.name == "AI Azure endpoint") field.text = "https://example.services.ai.azure.com/api/projects/interview";
                if (field.name == "AI Azure deployment" || field.name == "AI Azure api version") field.text = "";
                if (field.name == "AI provider key") field.text = "dummy-foundry-session-key";
            }
            Button(app, "Test AI connection").onClick.Invoke(); yield return null;
            Assert.AreEqual(2, transport.calls.Count); Assert.AreEqual("GET", transport.calls[0].Method);
            Assert.That(transport.last.Body, Does.Contain("auto-coach"));
            // All four options are real settings; switching preserves draft fields and session credentials.
            for (int i = 0; i < 4; i++) { Button(app, "AI Azure access mode").onClick.Invoke(); yield return null; }
            Assert.AreEqual(AzureAccessMode.Auto, Get<AISettings>(app, "aiDraft").azureAccessMode);
            foreach (InputField field in Get<RectTransform>(app, "root").GetComponentsInChildren<InputField>())
                if (field.name == "AI Azure deployment") field.text = "coach, review-coach";
            yield return Capture(app, "48-ai-foundry-resource");
            Button(app, "Save AI settings").onClick.Invoke(); yield return null;
            Assert.AreEqual("我的 Azure 资源", archive.ai.azureResourceName); Assert.AreEqual("", archive.ai.azureApiVersion);
            Assert.That(ArchiveStore.Encode(archive), Does.Not.Contain("dummy-foundry-session-key"));
            Button(app, "Close settings").onClick.Invoke(); yield return null;
            Call(app, "ShowFutureSelfDialogue"); yield return null;
            Button(app, "Choose Azure deployment").onClick.Invoke(); yield return null;
            Assert.That(Button(app, "Azure deployment choice 1").GetComponentInChildren<Text>().text, Does.Contain("我的 Azure 资源 / review-coach"));
            yield return Capture(app, "49-ai-azure-deployments");
            Button(app, "Azure deployment choice 1").onClick.Invoke(); yield return null;
            Assert.AreEqual("review-coach", archive.ai.azureSelectedDeployment);
            Button(app, "Generate personal content").onClick.Invoke(); yield return null;
            Assert.AreEqual("https://example.services.ai.azure.com/openai/v1/chat/completions", transport.last.Url);
            Assert.That(transport.last.Body, Does.Contain("\"model\":\"review-coach\""));
            Assert.AreEqual(0, archive.reality.quests.Count);
            yield return new ExitPlayMode();
        }
    }
}
