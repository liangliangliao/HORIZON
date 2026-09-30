using System.Collections;
using System.IO;
using System.Reflection;
using Horizon.Game;
using Horizon.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Horizon.Tests
{
    public sealed class PlayableFlowTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator ObservationViewsExplainResultsAndKeepTheLiveLifeIntact()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>();
            if (app == null) app = new GameObject("Test observation").AddComponent<HorizonApp>();
            yield return null;
            var session = new GameSession(3, 41);
            while (session.Day < 4) { session.Choose(session.Hand[2].Id); session.Advance(); }
            session.LockPrediction(0, 0, 0);
            while (session.Day < 7) { session.Choose(session.Hand[2].Id);
                if (session.Day == 4) session.VisitStation(); session.Advance(); }
            var archive = new ArchiveData { active = session.Snapshot(), nextRareRun = 99 };
            for (int day = 1; day <= 7; day++) archive.journey.Visit("2026-09-" + day.ToString("00"));
            Set(app, "session", session); Set(app, "archive", archive);
            Call(app, "ShowPredictionReview");
            yield return new WaitForSecondsRealtime(1.1f);
            ButtonNamed(app, "Why").onClick.Invoke();
            yield return null;
            yield return Capture(app, "15-prediction-why");
            string explanations = "";
            foreach (Text text in Get<RectTransform>(app, "overlay").GetComponentsInChildren<Text>()) explanations += text.text;
            Assert.That(explanations, Does.Contain("合计实际变化"));
            ButtonNamed(app, "Close prediction why").onClick.Invoke(); yield return null;
            ButtonNamed(app, "Continue").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.AreEqual(6, Get<RectTransform>(app, "root").GetComponentsInChildren<ResourceOrbitGraphic>().Length);
            int wallet = archive.wallet.stardust;
            Call(app, "ShowFocus"); yield return null;
            yield return Capture(app, "16-probability-focus");
            string frozen = JsonUtility.ToJson(session.Snapshot());
            Assert.AreEqual(1, session.FocusUses);
            ButtonNamed(app, "Close").onClick.Invoke(); yield return null;
            Call(app, "ShowJourney"); yield return null;
            ButtonNamed(app, "Chapter 4").onClick.Invoke(); yield return null;
            ButtonNamed(app, "Exercise choice portfolio").onClick.Invoke(); yield return null;
            yield return Capture(app, "18-chapter-lesson");
            ButtonNamed(app, "Close exercise").onClick.Invoke(); yield return null;
            ButtonNamed(app, "Thirty day view").onClick.Invoke(); yield return null;
            ButtonNamed(app, "Meet thirty day self").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Capture(app, "17-thirty-day-self");
            ButtonNamed(app, "Other thirty day self").onClick.Invoke(); yield return null;
            ButtonNamed(app, "Back to thirty day range").onClick.Invoke(); yield return null;
            ButtonNamed(app, "Close future range").onClick.Invoke(); yield return null;
            Assert.AreEqual(frozen, JsonUtility.ToJson(session.Snapshot()));
            Assert.AreEqual(wallet, archive.wallet.stardust);
            for (int run = 1; run <= 2; run++)
            {
                var past = new GameSession(run, 15);
                while (true) { if (past.CanPredict) past.SkipPrediction(); past.Choose(past.Hand[2].Id);
                    if (past.Day == 4) past.VisitStation(); if (past.Day == 12) break; past.Advance(); }
                archive.runs.Add(past.CompletedRun);
            }
            Call(app, "ShowEchoArchive"); yield return null;
            yield return Capture(app, "19-cross-life-echoes");
            Assert.IsTrue(System.Array.Exists(Get<RectTransform>(app, "overlay").GetComponentsInChildren<Button>(),
                b => b.name.StartsWith("Archived echo 1 ")));
            Assert.IsTrue(System.Array.Exists(Get<RectTransform>(app, "overlay").GetComponentsInChildren<Button>(),
                b => b.name.StartsWith("Archived echo 2 ")));
            app.StartCoroutine((IEnumerator)typeof(HorizonApp).GetMethod("BossSequence", Private)
                .Invoke(app, new object[] { archive.runs[0] }));
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture(app, "20-boss-constellation");
            int stars = 0;
            foreach (RoundedGraphic star in Get<RectTransform>(app, "root").GetComponentsInChildren<RoundedGraphic>())
                if (star.name.StartsWith("Boss day ")) stars++;
            Assert.AreEqual(12, stars);
            yield return new WaitForSecondsRealtime(3);
            Assert.AreEqual(wallet, archive.wallet.stardust);
            Assert.AreEqual(frozen, JsonUtility.ToJson(session.Snapshot()));
            PlayerPrefs.DeleteKey("HORIZON.PROTOTYPE.V1");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator MysteryRemembersItsRealCauseAcrossResumeAndRevealsOnce()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>();
            if (app == null) app = new GameObject("Test memory").AddComponent<HorizonApp>();
            yield return null;
            var session = new GameSession(3, 15);
            while (session.Day < 6) { if (session.CanPredict) session.SkipPrediction();
                session.Choose(session.Day == 1 ? "portfolio" : session.Hand[2].Id);
                if (session.Day == 4) session.VisitStation(); session.Advance(); }
            RareMoment memory = ExperienceContent.Moment(3, 6, new System.Collections.Generic.List<RunRecord>());
            memory.type = 3; memory.title = "尚未找到的因";
            ExperienceContent.AttachMystery(memory, session);
            Assert.IsNotEmpty(memory.causeNodeId); Assert.IsNotEmpty(memory.consequenceNodeId);
            var archive = new ArchiveData { active = session.Snapshot(), pendingMoment = memory, nextRareRun = 99 };
            archive.moments.Add(memory); archive.wallet.stardust = 17;
            Set(app, "archive", archive); Set(app, "session", session); Call(app, "Save"); Call(app, "BuildBoard");
            yield return null; yield return Capture(app, "21-mystery-first");
            archive = JsonUtility.FromJson<ArchiveData>(PlayerPrefs.GetString("HORIZON.PROTOTYPE.V1")); archive.Repair();
            Set(app, "archive", archive); Call(app, "ContinueRun"); yield return null;
            Assert.AreEqual(memory.causeNodeId, archive.pendingMoment.causeNodeId);
            ButtonNamed(app, "Continue rare moment").onClick.Invoke(); yield return null;
            session = Get<GameSession>(app, "session");
            while (session.Day < 9) { session.Choose(session.Hand[2].Id); session.Advance(); }
            Call(app, "BuildBoard"); yield return null;
            Assert.AreEqual(5, archive.pendingMoment.type);
            Assert.AreEqual(memory.consequenceNodeId, archive.pendingMoment.consequenceNodeId);
            yield return Capture(app, "22-mystery-reveal");
            yield return new WaitForSecondsRealtime(1);
            Assert.IsNotNull(archive.pendingMoment);
            ButtonNamed(app, "Continue rare moment").onClick.Invoke(); yield return null;
            Call(app, "BuildBoard"); yield return null;
            Assert.IsNull(archive.pendingMoment); Assert.AreEqual(1, archive.moments.Count);
            Assert.AreEqual(17, archive.wallet.stardust);
            PlayerPrefs.DeleteKey("HORIZON.PROTOTYPE.V1");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FutureStationCanPlayItsFullDefaultSequenceWithoutInput()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>();
            if (app == null) app = new GameObject("Test station").AddComponent<HorizonApp>();
            yield return null;
            var session = new GameSession(1);
            while (session.Day < 4)
            { session.Choose(session.Hand[2].Id); session.Advance(); }
            session.SkipPrediction(); session.Choose(session.Hand[2].Id);
            var archive = new ArchiveData { active = session.Snapshot() };
            Set(app, "archive", archive); Set(app, "session", session);
            float start = Time.realtimeSinceStartup;
            Call(app, "RenderStationBeat", 0);
            while (!session.StationVisited && Time.realtimeSinceStartup - start < 60) yield return null;
            Assert.IsTrue(session.StationVisited, "The default station sequence should finish without a hidden confirmation.");
            Assert.That(Time.realtimeSinceStartup - start, Is.InRange(45f, 60f));
            Assert.AreEqual(5, session.Day);
            Assert.AreEqual(0, archive.wallet.stardust, "Station playback must not add power or currency.");
            PlayerPrefs.DeleteKey("HORIZON.PROTOTYPE.V1");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ThirdStationRevealsTheFutureSelfAndKeepsPlayersConcern()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>();
            if (app == null) app = new GameObject("Test future self").AddComponent<HorizonApp>();
            yield return null;
            var session = new GameSession(3, 15);
            while (session.Day < 4)
            { session.Choose(session.Hand[2].Id); session.Advance(); }
            session.SkipPrediction(); session.Choose(session.Hand[2].Id);
            var archive = new ArchiveData { active = session.Snapshot() };
            Set(app, "archive", archive); Set(app, "session", session);
            Call(app, "RenderStationBeat", 0);
            yield return null;
            ButtonNamed(app, "Next station beat").onClick.Invoke();
            yield return null;
            ButtonNamed(app, "Next station beat").onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.8f);
            yield return Capture(app, "14-future-self-reveal");
            Assert.AreEqual(2, session.HorizonLevel);
            ButtonNamed(app, "Next station beat").onClick.Invoke();
            yield return null;
            ButtonNamed(app, "Protect Growth").onClick.Invoke();
            Assert.AreEqual("Growth", archive.preferredIntent);
            Assert.AreEqual(3, session.HorizonLevel);
            yield return new WaitForSecondsRealtime(4);
            PlayerPrefs.DeleteKey("HORIZON.PROTOTYPE.V1");
            yield return new ExitPlayMode();
        }

        // Runs the actual MonoBehaviours, uGUI buttons, card drag handlers and save
        // path. Screenshots are rendered by Unity, not reconstructed from HTML.
        [UnityTest]
        public IEnumerator PortraitFlowKeepsResultsAndProducesRealRenderPreviews()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>();
            if (app == null) app = new GameObject("Test HORIZON").AddComponent<HorizonApp>();
            yield return null;
            Set(app, "archive", new ArchiveData());
            Call(app, "StartNewRun");
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType,
                "Visual previews require a graphics device; run Unity under Xvfb without -nographics.");
            yield return Capture(app, "01-board");
            yield return new WaitForSecondsRealtime(0.3f);
            GameSession session = Get<GameSession>(app, "session");
            HorizonCardDrag growth = FindCard(app, session.Hand[1].Id);
            var pointer = new PointerEventData(EventSystem.current);
            Vector2 cardOrigin = growth.transform.position;
            pointer.pressPosition = cardOrigin;
            pointer.position = cardOrigin;
            growth.OnPointerDown(pointer);
            growth.OnBeginDrag(pointer);
            pointer.position = new Vector2(0, 0);
            growth.OnDrag(pointer);
            growth.OnEndDrag(pointer);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return null;
            Assert.IsFalse(session.HasChosen, "Releasing outside the ring must not use a card.");
            Assert.Less(Vector2.Distance(cardOrigin, growth.transform.position), 1);
            Assert.AreEqual(6, session.Energy);

            RectTransform target = Get<RectTransform>(app, "destinationBeacon");
            pointer.pressPosition = growth.transform.position;
            pointer.position = pointer.pressPosition;
            growth.OnPointerDown(pointer);
            growth.OnBeginDrag(pointer);
            pointer.position = target.TransformPoint(target.rect.center);
            growth.OnDrag(pointer);
            growth.OnEndDrag(pointer);
            yield return new WaitForSecondsRealtime(1);
            ArchiveData archive = Get<ArchiveData>(app, "archive");
            Assert.AreEqual(FeedbackKind.Choice, archive.pendingFeedback.kind);
            Assert.AreEqual(1, session.Day);
            Assert.IsTrue(session.HasChosen);
            int balance = archive.wallet.stardust;
            yield return new WaitForSecondsRealtime(1);
            Assert.IsNotNull(archive.pendingFeedback, "Results must wait for the player.");
            Assert.AreEqual(1, session.Day);
            yield return Capture(app, "02-action-result");

            // Reopen from the persisted save while this result is pending.
            archive = JsonUtility.FromJson<ArchiveData>(PlayerPrefs.GetString("HORIZON.PROTOTYPE.V1"));
            archive.Repair();
            Set(app, "archive", archive);
            Call(app, "ContinueRun");
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.AreEqual(balance, archive.wallet.stardust);
            Assert.AreEqual(FeedbackKind.Choice, archive.pendingFeedback.kind);
            ButtonNamed(app, "Continue result").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.4f);
            session = Get<GameSession>(app, "session");
            Assert.AreEqual(2, session.Day);

            for (int day = 2; day <= 3; day++)
            {
                FindCard(app, session.Hand[2].Id).OnPointerClick(new PointerEventData(EventSystem.current));
                Assert.IsNotNull(Get<RectTransform>(app, "overlay"));
                ButtonNamed(app, "Use card").onClick.Invoke();
                yield return new WaitForSecondsRealtime(1);
                Assert.AreEqual(FeedbackKind.Choice, archive.pendingFeedback.kind);
                ButtonNamed(app, "Continue result").onClick.Invoke();
                yield return new WaitForSecondsRealtime(day == 3 ? 5 : 0.4f);
            }
            Assert.AreEqual(4, session.Day);
            Assert.AreEqual(FeedbackKind.Echoes, archive.pendingFeedback.kind);
            Assert.That(archive.pendingFeedback.description, Does.Contain("D1"));
            yield return Capture(app, "03-echo-result");
            ButtonNamed(app, "Continue result").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsTrue(session.CanPredict);
            ButtonNamed(app, "Skip prediction").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsFalse(session.CanPredict);
            FindCard(app, session.Hand[2].Id).OnPointerClick(new PointerEventData(EventSystem.current));
            ButtonNamed(app, "Use card").onClick.Invoke();
            yield return new WaitForSecondsRealtime(1);
            ButtonNamed(app, "Continue result").onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.2f);
            ButtonNamed(app, "Next station beat").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.4f);
            archive = JsonUtility.FromJson<ArchiveData>(PlayerPrefs.GetString("HORIZON.PROTOTYPE.V1"));
            archive.Repair();
            Set(app, "archive", archive);
            Call(app, "ContinueRun");
            yield return new WaitForSecondsRealtime(0.4f);
            session = Get<GameSession>(app, "session");
            Assert.AreEqual(1, archive.stationBeat, "Interrupted station playback must resume its saved beat.");
            ButtonNamed(app, "Next station beat").onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.8f);
            yield return Capture(app, "04-future-station");
            ButtonNamed(app, "Next station beat").onClick.Invoke();
            yield return new WaitForSecondsRealtime(5);
            if (archive.pendingFeedback != null) ButtonNamed(app, "Continue result").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(5, session.Day);
            Assert.IsTrue(session.StationVisited);

            // Existing rules tests cover days 5–11. Drive their rules quickly,
            // then use the real final card UI to verify the deadline route.
            while (session.Day < 12)
            {
                if (session.HasPredictionReview) session.MarkPredictionReviewed();
                session.Choose(session.Hand[2].Id);
                session.Advance();
            }
            Call(app, "BuildBoard");
            yield return new WaitForSecondsRealtime(0.5f);
            FindCard(app, session.Hand[2].Id).OnPointerClick(new PointerEventData(EventSystem.current));
            ButtonNamed(app, "Use card").onClick.Invoke();
            yield return new WaitForSecondsRealtime(3);
            Assert.AreEqual(1, archive.runs.Count);
            Assert.IsNull(archive.active);
            Assert.AreEqual(FeedbackKind.Deadline, archive.pendingFeedback.kind);
            balance = archive.wallet.stardust;
            yield return Capture(app, "05-deadline");
            Call(app, "ContinueRun");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(balance, archive.wallet.stardust);
            Assert.IsNotNull(archive.pendingFeedback);
            ButtonNamed(app, "Inspect timeline").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.35f);
            if (session.CompletedRun.boss.passed < 3)
            {
                yield return Capture(app, "06-possible-branch");
                ButtonNamed(app, "Close ghost").onClick.Invoke();
            }
            else ButtonNamed(app, "Close map").onClick.Invoke();

            string savedLife = JsonUtility.ToJson(archive.runs[0]);
            Call(app, "RenderMap", false);
            yield return null;
            ButtonNamed(app, "Inspect day 1").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture(app, "07-archive-detail");
            ButtonNamed(app, "Explore another choice").onClick.Invoke();
            yield return null;
            ButtonNamed(app, "Next branch day").onClick.Invoke();
            yield return null;
            ButtonNamed(app, "Next branch day").onClick.Invoke();
            yield return null;
            ButtonNamed(app, "Branch choice portfolio").onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, Get<System.Collections.Generic.Dictionary<int, string>>(app, "branchChoices").Count);
            yield return Capture(app, "08-branch-planner");
            ButtonNamed(app, "Close branch planner").onClick.Invoke();
            yield return null;
            ButtonNamed(app, "Causal network").onClick.Invoke();
            yield return null;
            yield return Capture(app, "09-causal-network");
            ButtonNamed(app, "Close causal network").onClick.Invoke();
            yield return null;
            ButtonNamed(app, "Browse lives").onClick.Invoke();
            yield return null;
            ButtonNamed(app, "Filter lives 2").onClick.Invoke();
            yield return null;
            ButtonNamed(app, "Open life 1").onClick.Invoke();
            yield return null;
            ButtonNamed(app, "Share life").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.3f);
            ButtonNamed(app, "Save share animation").onClick.Invoke();
            float exportDeadline = Time.realtimeSinceStartup + 45;
            while (string.IsNullOrEmpty(Get<string>(app, "lastSharePath")) && Time.realtimeSinceStartup < exportDeadline)
                yield return null;
            string exported = Get<string>(app, "lastSharePath");
            Assert.IsNotEmpty(exported, "The real canvas recording must produce a playable GIF.");
            Assert.IsTrue(File.Exists(exported));
            Assert.IsFalse(File.Exists(exported + ".tmp"));
            File.Copy(exported, Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "visuals", "HORIZON-run-001.gif"), true);
            yield return Capture(app, "10-share-story");
            ButtonNamed(app, "Save share animation").onClick.Invoke();
            yield return null;
            ButtonNamed(app, "Close share").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsFalse(File.Exists(exported + ".tmp"), "Leaving an export must clear the partial file.");
            Assert.AreEqual(savedLife, JsonUtility.ToJson(archive.runs[0]));
            Assert.AreEqual(balance, archive.wallet.stardust, "Archive exploration and sharing must not mint rewards.");
            ButtonNamed(app, "Close map").onClick.Invoke();
            yield return null;
            ButtonNamed(app, "Try another timeline").onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsNull(archive.pendingFeedback);
            Assert.AreEqual(2, Get<GameSession>(app, "session").RunNumber);
            Assert.AreEqual(1, Get<GameSession>(app, "session").Day);
            Assert.AreEqual(balance, archive.wallet.stardust);
            ButtonNamed(app, "Begin second life").onClick.Invoke();
            yield return null;

            var third = new GameSession(3, 15);
            PendingEcho combined = null;
            while (third.Day < 6)
            {
                if (third.CanPredict) third.SkipPrediction();
                third.Choose(third.Day == 1 ? "portfolio" : third.Hand[2].Id);
                if (third.Day == 4) third.VisitStation();
                DayTransition transition = third.Advance();
                if (transition.Day == 6) combined = transition.Echos.Find(e => e.replacementId == "together");
            }
            Set(app, "session", third);
            archive.active = third.Snapshot();
            Call(app, "BuildBoard");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsNotNull(archive.pendingMoment);
            yield return Capture(app, "11-rare-moment");
            RareMoment moment = archive.pendingMoment;
            Call(app, "ContinueRun");
            yield return null;
            Assert.AreEqual(moment.title, archive.pendingMoment.title);
            ButtonNamed(app, "Continue rare moment").onClick.Invoke();
            yield return null;
            Assert.IsNull(archive.pendingMoment);
            Assert.AreEqual(1, archive.moments.Count);
            Assert.AreEqual(balance, archive.wallet.stardust);
            Call(app, "ShowRangeForecast");
            yield return null;
            string visibleResources = "";
            int rangeRows = 0;
            foreach (Text label in Get<RectTransform>(app, "root").GetComponentsInChildren<Text>())
                if (label.name == "Range value") { rangeRows++; visibleResources += label.text; }
            Assert.AreEqual(6, rangeRows, "The future range must show every resource.");
            foreach (string name in new[] { "精力", "心情", "洞察", "关系", "金钱", "能力" })
                Assert.That(visibleResources, Does.Contain(name));
            yield return Capture(app, "12-range-forecast");
            ButtonNamed(app, "Close future range").onClick.Invoke();
            yield return null;
            third = Get<GameSession>(app, "session");
            Assert.IsNotNull(combined);
            app.StartCoroutine((IEnumerator)typeof(HorizonApp).GetMethod("CascadeSequence", Private).Invoke(app, new object[] { combined }));
            yield return new WaitForSecondsRealtime(1.1f);
            yield return Capture(app, "13-cascade-merge");
            int renderedConnections = 0;
            foreach (TimeThreadGraphic line in Get<RectTransform>(app, "root").GetComponentsInChildren<TimeThreadGraphic>())
                if (line.name == "True chain connection") renderedConnections++;
            Assert.GreaterOrEqual(renderedConnections, 4, "The combined effect must show both actual source histories.");
            yield return new WaitForSecondsRealtime(1.5f);
            PlayerPrefs.DeleteKey("HORIZON.PROTOTYPE.V1");
            yield return new ExitPlayMode();
        }

        private static IEnumerator Capture(HorizonApp app, string name)
        {
            HorizonWorld3D world = Get<HorizonWorld3D>(app, "world");
            Canvas canvas = Get<RectTransform>(app, "root").GetComponentInParent<Canvas>();
            var image = new RenderTexture(1080, 1920, 24);
            image.Create();
            var uiObject = new GameObject("Portrait capture camera", typeof(Camera));
            Camera ui = uiObject.GetComponent<Camera>();
            ui.clearFlags = CameraClearFlags.Depth;
            ui.cullingMask = 1 << 5;
            ui.nearClipPlane = 0.1f;
            ui.farClipPlane = 20;
            ui.targetTexture = image;
            ui.depth = 20;
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = ui;
            canvas.planeDistance = 5;
            world.WorldCamera.targetTexture = image;
            world.SnapCamera();
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            // Moving the canvas to a portrait render target can resize the
            // dynamic font atlas. Rebuild all active text meshes after every
            // character request, before reading the rendered pixels.
            View.RefreshText(canvas.transform);
            world.WorldCamera.Render();
            ui.Render();
            RenderTexture old = RenderTexture.active;
            RenderTexture.active = image;
            var pixels = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
            pixels.Apply();
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "visuals");
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
            RenderTexture.active = old;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            world.WorldCamera.targetTexture = null;
            Object.Destroy(uiObject);
            Object.Destroy(pixels);
            image.Release();
            Object.Destroy(image);
            yield return null;
            yield return null;
        }

        private static HorizonCardDrag FindCard(HorizonApp app, string name)
        {
            foreach (HorizonCardDrag card in Get<RectTransform>(app, "root").GetComponentsInChildren<HorizonCardDrag>())
                if (card.name == name) return card;
            Assert.Fail("Missing card " + name);
            return null;
        }
        private static Button ButtonNamed(HorizonApp app, string name)
        {
            foreach (Button button in Get<RectTransform>(app, "root").GetComponentsInChildren<Button>())
                if (button.name == name && button.interactable) return button;
            Assert.Fail("Missing button " + name);
            return null;
        }
        private static T Get<T>(HorizonApp app, string field) { return (T)typeof(HorizonApp).GetField(field, Private).GetValue(app); }
        private static void Set(HorizonApp app, string field, object value) { typeof(HorizonApp).GetField(field, Private).SetValue(app, value); }
        private static void Call(HorizonApp app, string method, params object[] arguments)
        { typeof(HorizonApp).GetMethod(method, Private).Invoke(app, arguments); }
    }
}
