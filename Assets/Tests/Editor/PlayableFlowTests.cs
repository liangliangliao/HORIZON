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
            yield return new WaitForSeconds(0.5f);

            Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType,
                "Visual previews require a graphics device; run Unity under Xvfb without -nographics.");
            yield return Capture(app, "01-board");
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
            yield return new WaitForSeconds(0.3f);
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
            yield return new WaitForSeconds(1);
            ArchiveData archive = Get<ArchiveData>(app, "archive");
            Assert.AreEqual(FeedbackKind.Choice, archive.pendingFeedback.kind);
            Assert.AreEqual(1, session.Day);
            Assert.IsTrue(session.HasChosen);
            int balance = archive.wallet.stardust;
            yield return new WaitForSeconds(1);
            Assert.IsNotNull(archive.pendingFeedback, "Results must wait for the player.");
            Assert.AreEqual(1, session.Day);
            yield return Capture(app, "02-action-result");

            // Reopen from the persisted save while this result is pending.
            archive = JsonUtility.FromJson<ArchiveData>(PlayerPrefs.GetString("HORIZON.PROTOTYPE.V1"));
            archive.Repair();
            Set(app, "archive", archive);
            Call(app, "ContinueRun");
            yield return new WaitForSeconds(0.35f);
            Assert.AreEqual(balance, archive.wallet.stardust);
            Assert.AreEqual(FeedbackKind.Choice, archive.pendingFeedback.kind);
            ButtonNamed(app, "Continue result").onClick.Invoke();
            yield return new WaitForSeconds(0.4f);
            session = Get<GameSession>(app, "session");
            Assert.AreEqual(2, session.Day);

            for (int day = 2; day <= 3; day++)
            {
                FindCard(app, session.Hand[2].Id).OnPointerClick(new PointerEventData(EventSystem.current));
                Assert.IsNotNull(Get<RectTransform>(app, "overlay"));
                ButtonNamed(app, "Use card").onClick.Invoke();
                yield return new WaitForSeconds(1);
                Assert.AreEqual(FeedbackKind.Choice, archive.pendingFeedback.kind);
                ButtonNamed(app, "Continue result").onClick.Invoke();
                yield return new WaitForSeconds(day == 3 ? 5 : 0.4f);
            }
            Assert.AreEqual(4, session.Day);
            Assert.AreEqual(FeedbackKind.Echoes, archive.pendingFeedback.kind);
            Assert.That(archive.pendingFeedback.description, Does.Contain("D1"));
            yield return Capture(app, "03-echo-result");
            ButtonNamed(app, "Continue result").onClick.Invoke();
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(session.CanPredict);
            ButtonNamed(app, "Skip prediction").onClick.Invoke();
            yield return new WaitForSeconds(0.4f);
            Assert.IsFalse(session.CanPredict);
            FindCard(app, session.Hand[2].Id).OnPointerClick(new PointerEventData(EventSystem.current));
            ButtonNamed(app, "Use card").onClick.Invoke();
            yield return new WaitForSeconds(1);
            ButtonNamed(app, "Continue result").onClick.Invoke();
            yield return new WaitForSeconds(0.4f);
            ButtonNamed(app, "Next station beat").onClick.Invoke();
            yield return new WaitForSeconds(0.4f);
            ButtonNamed(app, "Next station beat").onClick.Invoke();
            yield return new WaitForSeconds(0.5f);
            yield return Capture(app, "04-future-station");
            ButtonNamed(app, "Next station beat").onClick.Invoke();
            yield return new WaitForSeconds(5);
            if (archive.pendingFeedback != null) ButtonNamed(app, "Continue result").onClick.Invoke();
            yield return new WaitForSeconds(0.4f);
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
            yield return new WaitForSeconds(0.5f);
            FindCard(app, session.Hand[2].Id).OnPointerClick(new PointerEventData(EventSystem.current));
            ButtonNamed(app, "Use card").onClick.Invoke();
            yield return new WaitForSeconds(3);
            Assert.AreEqual(1, archive.runs.Count);
            Assert.IsNull(archive.active);
            Assert.AreEqual(FeedbackKind.Deadline, archive.pendingFeedback.kind);
            balance = archive.wallet.stardust;
            yield return Capture(app, "05-deadline");
            Call(app, "ContinueRun");
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(balance, archive.wallet.stardust);
            Assert.IsNotNull(archive.pendingFeedback);
            ButtonNamed(app, "Inspect timeline").onClick.Invoke();
            yield return new WaitForSeconds(0.35f);
            if (session.CompletedRun.boss.passed < 3)
            {
                yield return Capture(app, "06-possible-branch");
                ButtonNamed(app, "Close ghost").onClick.Invoke();
            }
            else ButtonNamed(app, "Close map").onClick.Invoke();
            ButtonNamed(app, "Try another timeline").onClick.Invoke();
            yield return new WaitForSeconds(1.5f);
            Assert.IsNull(archive.pendingFeedback);
            Assert.AreEqual(2, Get<GameSession>(app, "session").RunNumber);
            Assert.AreEqual(1, Get<GameSession>(app, "session").Day);
            Assert.AreEqual(balance, archive.wallet.stardust);
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
        private static void Call(HorizonApp app, string method) { typeof(HorizonApp).GetMethod(method, Private).Invoke(app, null); }
    }
}
