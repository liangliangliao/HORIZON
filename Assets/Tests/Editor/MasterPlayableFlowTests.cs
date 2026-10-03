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
using UnityEngine.EventSystems;
using System.Collections.Generic;
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
                    b.interactable && (b.name == "Continue result" || b.name == "Continue master event" || b.name == "Next station beat"));
                if (next != null) next.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.15f);
            }
            Assert.Fail("Daily feedback did not return to the next choice.");
        }

        [UnityTest]
        public IEnumerator PhoneCardIsRaycastableAndDragThroughEventSystemPlaysExactlyOnce()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>(); if (app == null) app = new GameObject("Touch hand acceptance").AddComponent<HorizonApp>();
            yield return null;
            var archive = new ArchiveData { seenSecondLife = true };
            var life = GameSession.StartMasterLife(2, 15, RunMode.Quick); archive.active = life.Snapshot();
            Set(app, "archive", archive); Set(app, "session", life); Call(app, "BuildBoard");
            yield return new WaitForSecondsRealtime(0.6f); Canvas.ForceUpdateCanvases();
            HorizonCardDrag card = Card(app, life.Hand[2].Id); var face = card.GetComponent<HorizonCardSurface>();
            Assert.IsNotNull(face); Assert.IsTrue(face.raycastTarget); Assert.Greater(face.Thickness, 0);
            RectTransform rect = (RectTransform)card.transform;
            Vector2 press = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 0, position = press, pressPosition = press, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            GameObject hit = hits.FirstOrDefault(x => x.gameObject.GetComponentInParent<HorizonCardDrag>() == card).gameObject;
            Assert.IsNotNull(hit, "A physical finger must hit the card, not only a direct method call.");
            Assert.AreEqual(hit, hits[0].gameObject, "No decorative overlay may steal the card touch.");
            pointer.pointerPressRaycast = hits[0]; pointer.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(hit);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.beginDragHandler);
            RectTransform target = Get<RectTransform>(app, "destinationBeacon");
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
            ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.dragHandler);
            Assert.Greater(rect.localScale.x, 1, "Lift gives visible feedback.");
            ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.endDragHandler);
            ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.endDragHandler);
            Assert.AreEqual(1, life.Actions.Count); Assert.AreEqual(card.name, life.Actions[0].cardId);
            yield return new ExitPlayMode();
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
            Assert.IsNull(Get<RectTransform>(app, "overlay"), "A new life starts with real choices, without mandatory reading pages.");
            archive.playGuide.completed = false;
            Call(app, "ShowPlayGuidePage", 0, true); yield return null;
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
                if (archive.imagination.phase == ImaginePhase.Preparation) Button(app, "Preparation action 0").onClick.Invoke();
                else if (archive.imagination.phase == ImaginePhase.Recover) Button(app, "Recovery action 1").onClick.Invoke();
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
            Button(app, "Execute next step").onClick.Invoke(); yield return null;
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
            while (archive.imagination.phase != ImaginePhase.Recover)
            { if (archive.imagination.phase == ImaginePhase.Preparation) Button(app, "Preparation action 0").onClick.Invoke();
                else Button(app, "Continue imagination").onClick.Invoke(); yield return null; }
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
            Button(app, "Execute next step").onClick.Invoke(); yield return null;
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
                if (archive.imagination.phase == ImaginePhase.Preparation) Button(app, "Preparation action 0").onClick.Invoke();
                else if (archive.imagination.phase == ImaginePhase.Recover) { yield return Capture(app, "42-imagine-recovery"); Button(app, "Recovery action 1").onClick.Invoke(); }
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
        [UnityTest]
        public IEnumerator LegacyPreparationIsFastAndNeverPromisesNewCostsOrBenefits()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>(); if (app == null) app = new GameObject("Legacy upgrade audit").AddComponent<HorizonApp>();
            yield return null;
            var life = new GameSession(1, 17, 8); string cardId = life.Hand[1].Id; life.LockDecision(cardId);
            var archive = new ArchiveData { active = life.Snapshot(), nextRareRun = 99,
                preferences = new PlayerPreferences { reducedMotion = true, sound = false } }; archive.Repair();
            Set(app, "archive", archive); Set(app, "session", life); Call(app, "ApplyPreferences"); Call(app, "BuildBoard"); Call(app, "ShowExecution"); yield return null;
            Button(app, "Action factors").onClick.Invoke(); yield return null;
            string description = string.Join(" ", Get<RectTransform>(app, "overlay").GetComponentsInChildren<Text>().Select(t => t.text));
            Assert.That(description, Does.Contain("原成本").And.Not.Contain("省1精力"));
            Button(app, "Master back").onClick.Invoke(); yield return null;
            Button(app, "Equip triggers").onClick.Invoke(); yield return null;
            Button(app, "Trigger ticket").onClick.Invoke(); yield return null;
            Assert.AreEqual(5, life.Money);
            description = string.Join(" ", Get<RectTransform>(app, "overlay").GetComponentsInChildren<Text>().Select(t => t.text));
            Assert.That(description, Does.Not.Contain("金钱-1").And.Not.Contain("省1精力"));
            Button(app, "Master back").onClick.Invoke(); yield return null;
            Button(app, "Execute next step").onClick.Invoke(); yield return null;
            Assert.AreEqual(4, life.Master.decision.step); Assert.AreEqual(DecisionStatus.Ready, life.Master.decision.status);
            Assert.AreEqual(-2, life.ImmediateEffect(CardCatalog.FindById(cardId)).energy);
            Assert.AreEqual(4, life.Master.commands.Count(c => c.operation == "step"));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator WideChoicesGroupedEchoesAndPreparationCanCompleteAnActualLife()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>(); if (app == null) app = new GameObject("Playable life audit").AddComponent<HorizonApp>();
            yield return null;
            var life = GameSession.StartMasterLife(1, 17, RunMode.Quick);
            var archive = new ArchiveData { active = life.Snapshot(), nextRareRun = 99,
                preferences = new PlayerPreferences { reducedMotion = true, sound = false } }; archive.Repair(); archive.playGuide.completed = true;
            Set(app, "archive", archive); Set(app, "session", life); Call(app, "ApplyPreferences"); Call(app, "BuildBoard");
            yield return new WaitForSecondsRealtime(0.35f); yield return Capture(app, "58-playable-wide-choices");
            RectTransform root = Get<RectTransform>(app, "root");
            var cardRects = root.GetComponentsInChildren<HorizonCardDrag>().Select(c => (RectTransform)c.transform).ToArray();
            Assert.AreEqual(3, cardRects.Length);
            foreach (RectTransform rect in cardRects)
            { Assert.GreaterOrEqual(rect.rect.height / root.rect.height * 640, 48); Assert.GreaterOrEqual(rect.rect.width / root.rect.width * 360, 48); }
            bool sawBundle = false; int receipts = 0;
            for (int day = 1; day <= 12; day++)
            {
                if (life.HasPredictionReview) { Button(app, "Continue").onClick.Invoke(); yield return null; }
                if (life.CanPredict) { Button(app, "Lock prediction").onClick.Invoke(); yield return null; }
                var playable = life.Hand.Where(life.CanPlay).ToArray(); CardSpec chosen = playable.Last();
                if (day <= 2) chosen = playable.First(c => c.Kind == CardKind.Growth);
                else
                {
                    CardSpec support = playable.FirstOrDefault(c => c.GivesSupport);
                    CardSpec growth = playable.FirstOrDefault(c => c.Kind == CardKind.Growth && c.Id != "imagine");
                    if (support != null && ProductExperience.SupportEvidence(life) < 2) chosen = support;
                    else if (growth != null && life.Energy >= 4 && life.Mood >= 4 && day <= 12 - growth.Delay &&
                        (life.Ability < 6 || ProductExperience.GrowthEvidence(life) < 2)) chosen = growth;
                }
                Call(app, "CardTapped", Card(app, chosen.Id)); yield return null;
                if (day == 4)
                {
                    Button(app, "Lock decision").onClick.Invoke(); yield return null;
                    Button(app, "Equip triggers").onClick.Invoke(); yield return null;
                    Button(app, "Trigger alarm").onClick.Invoke(); yield return null;
                    Button(app, "Master back").onClick.Invoke(); yield return null;
                    Button(app, "Lower friction").onClick.Invoke(); yield return null;
                    Button(app, "Execute next step").onClick.Invoke(); yield return null;
                    Assert.AreEqual(DecisionStatus.Ready, life.Master.decision.status);
                    yield return Capture(app, "59-prepared-execution-cost");
                    Button(app, "Execute next step").onClick.Invoke();
                }
                else Button(app, "Use card").onClick.Invoke();
                float until = Time.realtimeSinceStartup + 20;
                while (Time.realtimeSinceStartup < until)
                {
                    if (day == 12 && !Get<bool>(app, "busy") && Get<RectTransform>(app, "root").GetComponentsInChildren<Button>().Any(b => b.name == "Try another timeline")) break;
                    if (day < 12 && !Get<bool>(app, "busy") && archive.pendingFeedback == null && life.Day > day) break;
                    if (!sawBundle && archive.pendingFeedback?.kind == FeedbackKind.Echoes && archive.pendingFeedback.beats.Count > 1 && !Get<bool>(app, "busy"))
                    { sawBundle = true; yield return Capture(app, "60-grouped-time-echo"); }
                    var next = Get<RectTransform>(app, "root").GetComponentsInChildren<Button>().FirstOrDefault(b => b.interactable &&
                        (b.name == "Continue result" || b.name == "Continue master event" || b.name == "Next station beat"));
                    if (next != null) { next.onClick.Invoke(); receipts++; }
                    yield return new WaitForSecondsRealtime(0.15f);
                }
                Assert.Less(Time.realtimeSinceStartup, until, "UI stopped on D" + day);
                if (day < 12) Assert.AreEqual(day + 1, life.Day);
            }
            Assert.IsTrue(sawBundle); Assert.AreEqual(12, life.Actions.Count); Assert.AreEqual(3, life.CompletedRun.boss.passed);
            Assert.LessOrEqual(receipts, 32, "Repeated feedback must not turn one choice into a long confirmation sequence.");
            Assert.AreEqual(1, archive.runs.Count); yield return Capture(app, "63-playable-life-complete");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PracticePreservesOriginalLifeAndCalibrationSurvivesReturningToIt()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>(); if (app == null) app = new GameObject("Practice isolation audit").AddComponent<HorizonApp>();
            yield return null;
            var life = GameSession.StartMasterLife(2, 17, RunMode.ExperimentRun);
            var archive = new ArchiveData { active = life.Snapshot(), nextRareRun = 99, seenSecondLife = true,
                preferences = new PlayerPreferences { reducedMotion = true, sound = false } }; archive.Repair();
            Set(app, "archive", archive); Set(app, "session", life); Call(app, "ApplyPreferences"); Call(app, "BuildBoard"); yield return null;
            string target = life.Hand[1].Id; Call(app, "PrepareCardImagination", CardCatalog.FindById(target)); yield return null;
            Button(app, "Start imagination").onClick.Invoke(); yield return null;
            while (archive.imagination.phase != ImaginePhase.Complete)
            {
                if (archive.imagination.phase == ImaginePhase.Preparation) Button(app, "Preparation action 1").onClick.Invoke();
                else if (archive.imagination.phase == ImaginePhase.Recover) Button(app, "Recovery action 0").onClick.Invoke();
                else Button(app, "Continue imagination").onClick.Invoke(); yield return null;
            }
            Button(app, "Keep future memory").onClick.Invoke(); yield return null;
            Button(app, "Cancel").onClick.Invoke(); yield return null;
            Call(app, "CardTapped", Card(app, life.Hand[2].Id)); yield return null; Button(app, "Use card").onClick.Invoke();
            yield return FinishDailyFeedback(app, 1);
            Call(app, "ShowImaginationComparison"); yield return null; yield return Capture(app, "61-imagination-actual-divergence");
            Assert.IsTrue(archive.imaginationComparisons[0].actionObserved); Assert.IsFalse(archive.imaginationComparisons[0].actionMatched);
            Assert.AreEqual(1, archive.me.imaginationDifferences); Assert.AreEqual(0, archive.reality.nodes.Count);
            Button(app, "Master back").onClick.Invoke(); yield return null; Call(app, "CloseMasterPage");
            Call(app, "PersistLiveLife"); string originalLife = JsonUtility.ToJson(life.Snapshot());
            string originalWallet = JsonUtility.ToJson(archive.wallet);
            string storePath = Path.Combine(Application.persistentDataPath, "HORIZON.life.json"); byte[] original = File.ReadAllBytes(storePath);
            Call(app, "StartPractice"); yield return null; yield return Capture(app, "62-isolated-practice-life");
            var practice = Get<GameSession>(app, "session"); Assert.AreNotSame(life, practice);
            Call(app, "CardTapped", Card(app, practice.Hand[1].Id)); yield return null; Button(app, "Use card").onClick.Invoke(); yield return FinishDailyFeedback(app, 1);
            Call(app, "OnApplicationPause", true); Call(app, "OnApplicationPause", false); yield return null;
            var store = new ArchiveStore(Path.Combine(Application.persistentDataPath, "HORIZON.life.json"), "HORIZON.PROTOTYPE.V1");
            CollectionAssert.AreEqual(original, File.ReadAllBytes(storePath), "Practice must never overwrite the persistent life.");
            Assert.AreEqual(2, store.Load().active.day);
            Call(app, "ExitPractice"); yield return null;
            Assert.AreSame(archive, Get<ArchiveData>(app, "archive")); Assert.AreEqual(2, Get<GameSession>(app, "session").Day);
            Assert.AreEqual(originalLife, JsonUtility.ToJson(Get<GameSession>(app, "session").Snapshot()));
            Assert.AreEqual(originalWallet, JsonUtility.ToJson(archive.wallet)); Assert.AreEqual(1, archive.me.imaginationDifferences);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FirstLaunchCanStartAStoryAndResumeItsPersistentTarget()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>(); if (app == null) app = new GameObject("First story entry").AddComponent<HorizonApp>();
            yield return null;
            var archive = new ArchiveData { nextRareRun = 99, preferences = new PlayerPreferences { reducedMotion = true, sound = false } }; archive.Repair();
            Set(app, "archive", archive); Set(app, "session", null); Call(app, "ApplyPreferences"); Call(app, "ShowIntro"); yield return null;
            Button(app, "Intro story chapters").onClick.Invoke(); yield return null;
            Button(app, "Begin story chapter").onClick.Invoke(); yield return null;
            Assert.AreEqual("uncertainty", archive.active.master.chapter.id);
            Assert.AreEqual(1, archive.active.day); Assert.AreEqual(0, archive.active.actions.Count);
            Button(app, "Build chapter path").onClick.Invoke(); yield return null;
            var saved = JsonUtility.FromJson<ArchiveData>(JsonUtility.ToJson(archive)); saved.Repair();
            Set(app, "archive", saved); Call(app, "ContinueRun"); yield return null;
            GameSession life = Get<GameSession>(app, "session"); Assert.AreEqual("uncertainty", life.Master.chapter.id);
            Assert.IsNull(Get<RectTransform>(app, "overlay")); Assert.AreEqual(0, life.Actions.Count);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator StoryChallengeConnectsPreparationFailureRecoveryBossAndReadableCause()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>(); if (app == null) app = new GameObject("Story challenge audit").AddComponent<HorizonApp>();
            yield return null;
            var originalLife = GameSession.StartMasterLife(2, 17, RunMode.Quick);
            var original = new ArchiveData { active = originalLife.Snapshot(), nextRareRun = 99, seenSecondLife = true,
                preferences = new PlayerPreferences { reducedMotion = true, sound = false } }; original.Repair();
            Set(app, "archive", original); Set(app, "session", originalLife); Call(app, "ApplyPreferences"); Call(app, "ShowHome"); yield return null;
            yield return Capture(app, "71-story-home");
            Button(app, "Story chapters").onClick.Invoke(); yield return null; yield return Capture(app, "64-story-selection");
            string model = JsonUtility.ToJson(original.me), active = JsonUtility.ToJson(original.active);
            Button(app, "Practice story chapter").onClick.Invoke(); yield return null; yield return Capture(app, "65-story-victory-anchor");
            Button(app, "Build chapter path").onClick.Invoke(); yield return null;
            GameSession life = Get<GameSession>(app, "session"); ArchiveData practice = Get<ArchiveData>(app, "archive");
            bool recovered = false, restarted = false;
            for (int day = 1; day <= 12; day++)
            {
                if (life.HasPredictionReview) { Button(app, "Continue").onClick.Invoke(); yield return null; }
                if (life.CanPredict) { Button(app, "Lock prediction").onClick.Invoke(); yield return null; }
                if (life.ChapterNeedsPreparation)
                { yield return Capture(app, "66-story-preparation"); Button(app, "Chapter route help").onClick.Invoke(); yield return null; }
                if (day == 5)
                { yield return Capture(app, "67-story-setback"); Button(app, "Recover chapter setback").onClick.Invoke(); yield return null; }
                if (life.ChapterNeedsBoss)
                { yield return Capture(app, "68-story-boss"); Assert.IsTrue(life.CanResolveChapter("help")); Button(app, "Chapter route help").onClick.Invoke(); yield return null; }
                CardSpec chosen = life.Hand[2];
                if (day != 5 && life.Energy >= 4 && life.Hand[1].Id != "imagine" && life.CanPlay(life.Hand[1])) chosen = life.Hand[1];
                Call(app, "CardTapped", Card(app, chosen.Id)); yield return null; Button(app, "Use card").onClick.Invoke();
                if (day < 12) yield return FinishDailyFeedback(app, day);
                else
                {
                    float until = Time.realtimeSinceStartup + 20;
                    while (Time.realtimeSinceStartup < until)
                    {
                        if (!Get<bool>(app, "busy") && Get<RectTransform>(app, "root").GetComponentsInChildren<Button>().Any(b => b.name == "Try another timeline")) break;
                        var next = Get<RectTransform>(app, "root").GetComponentsInChildren<Button>().FirstOrDefault(b => b.interactable &&
                            (b.name == "Continue result" || b.name == "Continue master event" || b.name == "Next station beat"));
                        if (next != null) next.onClick.Invoke(); yield return new WaitForSecondsRealtime(0.15f);
                    }
                    Assert.Less(Time.realtimeSinceStartup, until);
                }
                recovered |= !string.IsNullOrEmpty(life.Master.chapter.recoveryNode); restarted |= !string.IsNullOrEmpty(life.Master.chapter.returnNode);
            }
            Assert.IsTrue(recovered); Assert.IsTrue(restarted); Assert.AreEqual(ChapterOutcome.Arrived, life.CompletedRun.master.chapter.outcome);
            Assert.AreEqual(1, practice.runs.Count); yield return Capture(app, "69-story-arrived");
            Button(app, "Inspect timeline").onClick.Invoke(); yield return null; yield return Capture(app, "70-readable-causal-story");
            Assert.IsNotNull(Get<RectTransform>(app, "root").GetComponentsInChildren<ScrollRect>().Single(s => s.name == "Story path window"));
            Assert.AreEqual(model, JsonUtility.ToJson(original.me)); Assert.AreEqual(active, JsonUtility.ToJson(original.active));
            Call(app, "ExitPractice"); yield return null; Assert.AreSame(original, Get<ArchiveData>(app, "archive"));
            Call(app, "BuildBoard"); yield return null; yield return Capture(app, "72-tall-phone-board", 2400);
            Call(app, "ShowSettings"); yield return null; Call(app, "ShowHome"); yield return null; yield return Capture(app, "73-tall-phone-home", 2400);
            Assert.IsFalse(Get<RectTransform>(app, "root").GetComponentsInChildren<Text>().Any(t => t.name == "Settings title"));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PartialViewportBloomAndFrameClearDoNotLeaveStalePixels()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>(); if (app == null) app = new GameObject("Viewport audit").AddComponent<HorizonApp>();
            yield return null;
            HorizonWorld3D world = Get<HorizonWorld3D>(app, "world"); var target = new RenderTexture(200, 400, 24); target.Create();
            var source = new RenderTexture(200, 200, 0); source.Create(); var pixels = new Texture2D(200, 400, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target; GL.Clear(true, true, Color.magenta);
            world.BackgroundCamera.targetTexture = target; world.BackgroundCamera.Render();
            RenderTexture.active = source; GL.Clear(false, true, Color.blue);
            var cameraObject = new GameObject("Retained viewport", typeof(Camera)); Camera view = cameraObject.GetComponent<Camera>();
            view.enabled = false; view.rect = new Rect(0, 0.4f, 1, 0.5f);
            HorizonBloom bloom = cameraObject.AddComponent<HorizonBloom>(); bloom.Initialize(Resources.Load<Shader>("HorizonBloom")); Assert.IsTrue(bloom.IsSupported);
            Material material = (Material)typeof(HorizonBloom).GetField("material", Private).GetValue(bloom); material.SetFloat("_Intensity", 0); material.SetFloat("_Echo", 0);
            Graphics.SetRenderTarget(target); GL.Viewport(new Rect(0, 160, 200, 200));
            typeof(HorizonBloom).GetMethod("DrawInViewport", Private).Invoke(bloom, new object[] { source, target });
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 200, 400), 0, 0); pixels.Apply();
            Assert.Greater(pixels.GetPixel(100, 180).b, 0.5f, "The lower part of the scene must fill its intended viewport.");
            Assert.Greater(pixels.GetPixel(100, 330).b, 0.5f);
            Color outside = pixels.GetPixel(100, 30); Assert.Less(outside.r, 0.1f); Assert.Less(outside.b, 0.2f, "Previous-frame magenta must be cleared outside the world viewport.");
            world.BackgroundCamera.targetTexture = null; RenderTexture.active = previous;
            source.Release(); target.Release(); Object.Destroy(source); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(cameraObject);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ExpeditionChoicesKnowledgeThoughtAndNarrativeArePlayableOnPhone()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>(); if (app == null) app = new GameObject("Expedition UI").AddComponent<HorizonApp>();
            yield return null;
            var life = GameSession.StartMasterLife(2, 15, RunMode.ExperimentRun);
            var archive = new ArchiveData { active = life.Snapshot(), seenSecondLife = true, nextRareRun = 99 };
            archive.preferences.reducedMotion = true; archive.preferences.sound = false; archive.playGuide.completed = true;
            Set(app, "archive", archive); Set(app, "session", life); Call(app, "ApplyPreferences"); Call(app, "BuildBoard");
            yield return new WaitForSecondsRealtime(0.4f); yield return Capture(app, "74-extruded-phone-cards");
            Call(app, "ShowMasterHub"); yield return null; yield return Capture(app, "75-expedition-hub");
            Button(app, "Choose life route").onClick.Invoke(); yield return null;
            Button(app, "Life route growth").onClick.Invoke(); yield return null;
            Assert.AreEqual("growth", life.Master.expedition.route); yield return Capture(app, "76-life-route-tradeoffs");
            Call(app, "ShowExecution"); Button(app, "Adjust action environment").onClick.Invoke(); yield return null;
            int focus = life.Insight; Button(app, "Environment two-minutes").onClick.Invoke(); yield return null;
            Assert.AreEqual(focus - 1, life.Insight); yield return Capture(app, "77-action-environment");
            Button(app, "Understand active thought").onClick.Invoke(); yield return null; yield return Capture(app, "78-thought-monster");
            Button(app, "Thought dialogue").onClick.Invoke(); yield return null;
            Button(app, "Generate studio content").onClick.Invoke(); yield return null; yield return null;
            Assert.That(Get<RectTransform>(app, "root").GetComponentsInChildren<Text>().Single(t => t.name == "Content studio output").text, Does.Contain("步骤"));
            yield return Capture(app, "79-narrative-dialogue");
            Call(app, "ShowExpandedForge"); int index = life.Master.knowledge.FindIndex(k => k.id == "values");
            Set(app, "selectedKnowledge", index); Call(app, "ShowExpandedForge");
            Button(app, "Recognize knowledge").onClick.Invoke(); yield return null;
            Assert.AreEqual(KnowledgeStage.Recognize, life.Master.knowledge[index].stage); yield return Capture(app, "80-knowledge-forge");
            Call(app, "ShowCouncil"); yield return null; yield return Capture(app, "81-inner-council");
            Call(app, "ShowTimeVision"); yield return null; yield return Capture(app, "82-eight-time-horizons");
            Call(app, "ShowOrbit"); Button(app, "Causal reservoir").onClick.Invoke(); yield return null; yield return Capture(app, "87-causal-reservoirs");
            Call(app, "ShowModes"); Button(app, "Online parallel lives").onClick.Invoke(); yield return null; yield return Capture(app, "83-online-invitation");
            Call(app, "CloseMasterPage"); Call(app, "ShowImagineSetup");
            Button(app, "Imagine two failures").onClick.Invoke(); Button(app, "Start imagination").onClick.Invoke(); yield return null;
            Assert.AreEqual(2, archive.imagination.RequiredFailures); Assert.AreEqual(2, archive.imagination.pathVersion);
            yield return Capture(app, "84-goal-specific-imagination");
            Call(app, "PlayMasterSpectacle", new DomainEvent { kind = DomainEventKind.PatternBroken, tier = RewardTier.Mythic,
                title = "PATTERN\nBROKEN", detail = "过去的路停在这里。这次，你继续了。", multiplier = 3 }, (Action)(() => Call(app, "ShowHome")));
            yield return new WaitForSecondsRealtime(1.5f); yield return Capture(app, "85-pattern-broken-timelines");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TenSecondMp4IsExportedByTheShareButtonWithAnAudioTrack()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>(); if (app == null) app = new GameObject("Video export acceptance").AddComponent<HorizonApp>();
            yield return null;
            var life = GameSession.StartMasterLife(1, 15, RunMode.Quick);
            while (life.CompletedRun == null)
            { while (life.HasPredictionReview) life.MarkPredictionReviewed(); if (life.CanPredict) life.SkipPrediction();
                life.Choose(life.Hand.Last(c => life.CanPlay(c)).Id); if (life.NeedsStation) life.VisitStation(); if (life.CompletedRun == null) life.Advance(); }
            var archive = new ArchiveData(); archive.runs.Add(life.CompletedRun); archive.preferences.sound = false;
            Set(app, "archive", archive); Set(app, "session", life);
            Call(app, "ShowShareStory", life.CompletedRun, (Action)(() => Call(app, "ShowHome"))); yield return null;
            Button(app, "Save share video").onClick.Invoke();
            float until = Time.realtimeSinceStartup + 100;
            while (Get<string>(app, "lastSharePath") == null || !Get<string>(app, "lastSharePath").EndsWith(".mp4"))
            { Assert.Less(Time.realtimeSinceStartup, until, "MP4 encoding must finish; GIF fallback does not count as video."); yield return null; }
            string output = Get<string>(app, "lastSharePath"); Assert.Greater(new FileInfo(output).Length, 20000);
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "visuals"); Directory.CreateDirectory(directory);
            File.Copy(output, Path.Combine(directory, "HORIZON-run-001.mp4"), true);
            File.WriteAllBytes(Path.Combine(directory, "HORIZON-run-001.pcm"), TimelineSoundtrack.Pcm(life.CompletedRun));
            yield return Capture(app, "86-mp4-sharing");
            yield return new ExitPlayMode();
        }

        private static IEnumerator Capture(HorizonApp app, string name, int height = 1920)
        {
            var world = Get<HorizonWorld3D>(app, "world"); Canvas canvas = Get<RectTransform>(app, "root").GetComponentInParent<Canvas>();
            var image = new RenderTexture(1080, height, 24); image.Create();
            var cameraObject = new GameObject("Master portrait capture", typeof(Camera)); Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Depth; camera.cullingMask = 1 << 5; camera.nearClipPlane = 0.1f; camera.farClipPlane = 20; camera.targetTexture = image;
            camera.depth = 20;
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 5;
            world.BackgroundCamera.targetTexture = image; world.WorldCamera.targetTexture = image; world.SnapCamera(); yield return null; yield return null;
            Canvas.ForceUpdateCanvases(); View.RefreshText(canvas.transform); world.BackgroundCamera.Render(); world.WorldCamera.Render(); camera.Render();
            RenderTexture previous = RenderTexture.active; RenderTexture.active = image;
            var pixels = new Texture2D(1080, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 1080, height), 0, 0); pixels.Apply();
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "visuals"); Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
            RenderTexture.active = previous; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; world.WorldCamera.targetTexture = null; world.BackgroundCamera.targetTexture = null;
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
