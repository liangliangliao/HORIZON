using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Horizon.Tests
{
    public sealed partial class PlayableFlowTests
    {
        private static void CausalReady(GameSession life)
        { while (life.HasPredictionReview) life.MarkPredictionReviewed(); if (life.CanPredict) life.SkipPrediction(); }

        [UnityTest]
        public IEnumerator ActualStationNetworkAndMemoryPagesResumeAndNeverAwardResources()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>();
            if (app == null) app = new GameObject("Test actual station chains").AddComponent<HorizonApp>();
            yield return null;
            var life = new GameSession(3, 15);
            while (life.Day < 3) {
                CausalReady(life); life.Choose(life.Day == 1 ? life.Hand[1].Id : life.Hand[2].Id); life.Advance();
            }
            var archive = new ArchiveData { active = life.Snapshot(), nextRareRun = 99, calibrations = 3 };
            archive.wallet.Claim("already earned", 40);
            for (int day = 1; day <= 5; day++) archive.journey.Visit("2026-10-" + day.ToString("00"));
            Set(app, "session", life); Set(app, "archive", archive);
            Call(app, "BuildBoard"); yield return null;
            string beforeFocus = JsonUtility.ToJson(life.Snapshot());
            Call(app, "RenderFocus", false); yield return null;
            Assert.IsNotNull(ButtonNamed(app, "Forecast range"));
            Assert.IsFalse(Get<RectTransform>(app, "root").GetComponentsInChildren<Button>().Any(b => b.name == "Two futures"));
            yield return Capture(app, "34-unified-time-vision");
            Assert.AreEqual(beforeFocus, JsonUtility.ToJson(life.Snapshot()));
            ButtonNamed(app, "Close").onClick.Invoke(); yield return null;
            CausalReady(life); life.Choose(life.Hand[2].Id); life.Advance();
            CausalReady(life); life.Choose(life.Hand[2].Id); archive.active = life.Snapshot();
            string frozen = JsonUtility.ToJson(life.Snapshot());
            Call(app, "ShowInRunStation", 1); yield return null;
            List<MemoryChain> chains = CausalPresentation.Station(life); Assert.IsNotEmpty(chains);
            int nodes = chains.Sum(c => c.Nodes.Count);
            Assert.AreEqual(nodes, Get<HorizonWorld3D>(app, "world").GetComponentsInChildren<Transform>()
                .Count(t => t.name.StartsWith("Memory node ")));
            int edges = chains.Sum(c => c.Nodes.Sum(n => CausalGraph.ObservedParents(n).Count(id => c.Nodes.Any(p => p.id == id))));
            Assert.AreEqual(edges, Get<HorizonWorld3D>(app, "world").GetComponentsInChildren<LineRenderer>()
                .Count(t => t.name.StartsWith("Actual cause ")));
            ButtonNamed(app, "Touch memory 0").onClick.Invoke(); yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsTrue(archive.stationMemoryOpen);
            yield return Capture(app, "35-personal-causal-memory");
            Call(app, "NextStationStage"); Assert.AreEqual(1, archive.stationBeat, "A memory must stop station auto-advance.");
            if (chains[0].Nodes.Count > 1) {
                Button old = ButtonNamed(app, "Next memory node"); old.onClick.Invoke(); old.onClick.Invoke(); yield return null;
                Assert.AreEqual(1, archive.stationMemoryBeat, "Two taps must not skip a memory.");
            }
            int savedBeat = archive.stationMemoryBeat;
            var restored = JsonUtility.FromJson<ArchiveData>(PlayerPrefs.GetString("HORIZON.PROTOTYPE.V1")); restored.Repair();
            Set(app, "archive", restored); Call(app, "ContinueRun"); yield return new WaitForSecondsRealtime(0.4f);
            archive = Get<ArchiveData>(app, "archive"); life = Get<GameSession>(app, "session");
            Assert.IsTrue(archive.stationMemoryOpen); Assert.AreEqual(savedBeat, archive.stationMemoryBeat);
            Assert.AreEqual(40, archive.wallet.stardust); Assert.AreEqual(frozen, JsonUtility.ToJson(life.Snapshot()));
            yield return Capture(app, "36-memory-resumed");
            ButtonNamed(app, "Close memory story").onClick.Invoke(); yield return null;
            Assert.IsFalse(archive.stationMemoryOpen); Assert.AreEqual(4, life.Day);
            Assert.AreEqual(1, archive.stationBeat); Assert.AreEqual(40, archive.wallet.stardust);
            PlayerPrefs.DeleteKey("HORIZON.PROTOTYPE.V1");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator GhostChronologyStopsAtRealChangesResumesAndPreservesTheCompletedLife()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>();
            if (app == null) app = new GameObject("Test unfolding ghost").AddComponent<HorizonApp>();
            yield return null;
            var life = new GameSession(1, 15);
            while (life.CompletedRun == null) {
                CausalReady(life); life.Choose(life.Hand[2].Id); if (life.NeedsStation) life.VisitStation();
                if (life.CompletedRun == null) life.Advance();
            }
            RunRecord original = life.CompletedRun; Assert.IsNotNull(original.boss.ghostTimeline);
            string frozen = JsonUtility.ToJson(original);
            var archive = new ArchiveData { runs = new List<RunRecord> { original }, nextRareRun = 99,
                pendingFeedback = new FeedbackRecord { kind = FeedbackKind.Deadline, runNumber = 1, day = 12, presented = true } };
            archive.wallet.Claim("earned before observation", 40);
            Set(app, "session", life); Set(app, "archive", archive);
            Call(app, "ShowDeadlineResult", original); yield return null;
            ButtonNamed(app, "Inspect timeline").onClick.Invoke();
            yield return CausalWaitForButton(app, "Next ghost beat", "Restart from ghost");
            Assert.IsTrue(archive.ghostOpen); Assert.AreEqual(0, archive.ghostBeat);
            HorizonWorld3D world = Get<HorizonWorld3D>(app, "world");
            HorizonActor future = world.GetComponentsInChildren<HorizonActor>().Single(a => a.name == "Future you");
            Vector3 visible = world.WorldCamera.WorldToViewportPoint(future.Head.position);
            Assert.Greater(visible.z, 0); Assert.That(visible.x, Is.InRange(0.05f, 0.95f));
            Assert.That(visible.y, Is.InRange(0.05f, 0.95f)); Assert.Less(future.transform.position.z, 3.8f);
            GhostStory story = CausalPresentation.Ghost(original); Assert.Greater(story.Beats.Count, 1);
            yield return Capture(app, "37-ghost-original-choice");
            Button old = ButtonNamed(app, "Next ghost beat"); old.onClick.Invoke(); old.onClick.Invoke(); yield return null;
            Assert.AreEqual(1, archive.ghostBeat, "Repeated taps cannot skip the following consequence.");
            var loaded = JsonUtility.FromJson<ArchiveData>(PlayerPrefs.GetString("HORIZON.PROTOTYPE.V1")); loaded.Repair();
            Set(app, "archive", loaded); Call(app, "ContinueRun");
            yield return CausalWaitForButton(app, "Next ghost beat", "Restart from ghost");
            archive = Get<ArchiveData>(app, "archive"); Assert.IsTrue(archive.ghostOpen); Assert.AreEqual(1, archive.ghostBeat);
            yield return Capture(app, "38-ghost-returning-consequence");
            int frames = 0;
            while (archive.ghostBeat < story.Beats.Count - 1) {
                Assert.Less(frames++, 30);
                ButtonNamed(app, "Next ghost beat").onClick.Invoke();
                yield return CausalWaitForButton(app, "Next ghost beat", "Restart from ghost");
            }
            Assert.IsNotNull(ButtonNamed(app, "Restart from ghost"));
            string expected = CausalPresentation.GateChange(story.Original.boss, story.Alternative.boss);
            Assert.That(Get<RectTransform>(app, "root").GetComponentsInChildren<Text>()
                .Single(t => t.name == "Ghost actual change").text, Does.Contain(expected));
            yield return Capture(app, "39-ghost-deadline");
            Assert.AreEqual(40, archive.wallet.stardust); Assert.AreEqual(frozen, JsonUtility.ToJson(archive.runs[0]));
            Call(app, "HandleBack"); yield return null;
            Assert.IsFalse(archive.ghostOpen); Assert.IsNull(Get<RectTransform>(app, "overlay"));
            Assert.IsNull(archive.active); Assert.AreEqual(40, archive.wallet.stardust);
            Assert.IsTrue(archive.pendingFeedback.presented);
            PlayerPrefs.DeleteKey("HORIZON.PROTOTYPE.V1");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AReopenedLongDeadlineRestartsAllThirtyDaysAndPreservesItsArchive()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>();
            if (app == null) app = new GameObject("Test reopened long-life deadline").AddComponent<HorizonApp>();
            yield return null;
            GameSession life = GameSession.StartLongLife(4, 15);
            while (life.CompletedRun == null) { CausalReady(life); life.Choose(life.Hand[2].Id);
                if (life.NeedsStation) life.VisitStation(); if (life.CompletedRun == null) life.Advance(); }
            RunRecord original = life.CompletedRun; string frozen = JsonUtility.ToJson(original);
            var archive = new ArchiveData { runs = new List<RunRecord> { original }, nextRareRun = 99,
                pendingFeedback = new FeedbackRecord { kind = FeedbackKind.Deadline, runNumber = 4, day = 30, presented = true },
                ghostRun = 4, ghostOpen = true };
            for (int d = 1; d <= 7; d++) archive.journey.Visit("2026-10-" + d.ToString("00"));
            archive.wallet.Claim("the old life", 40);
            Set(app, "archive", archive); Set(app, "session", null); Call(app, "ContinueRun");
            Assert.IsTrue(archive.ghostOpen);
            GhostStory story = CausalPresentation.Ghost(original);
            Call(app, "RenderGhostStory", story.Beats.Count - 1);
            yield return CausalWaitForButton(app, "Restart from ghost");
            ButtonNamed(app, "Restart from ghost").onClick.Invoke();
            float start = Time.realtimeSinceStartup;
            while (Get<GameSession>(app, "session") == null) {
                Assert.Less(Time.realtimeSinceStartup - start, 3); yield return null;
            }
            life = Get<GameSession>(app, "session"); Assert.AreEqual(30, life.Deadline); Assert.AreEqual(1, life.Day);
            Assert.AreEqual(5, life.RunNumber); Assert.AreEqual(40, archive.wallet.stardust);
            Assert.AreEqual(frozen, JsonUtility.ToJson(archive.runs[0])); Assert.IsFalse(archive.ghostOpen);
            PlayerPrefs.DeleteKey("HORIZON.PROTOTYPE.V1"); yield return new ExitPlayMode();
        }

        private static IEnumerator CausalWaitForButton(HorizonApp app, params string[] names)
        {
            float beginning = Time.realtimeSinceStartup;
            while (true) {
                Button button = Get<RectTransform>(app, "root").GetComponentsInChildren<Button>()
                    .FirstOrDefault(b => names.Contains(b.name) && b.interactable);
                if (button != null) yield break;
                Assert.Less(Time.realtimeSinceStartup - beginning, 4, "The causal result must settle and remain available.");
                yield return null;
            }
        }
    }
}
