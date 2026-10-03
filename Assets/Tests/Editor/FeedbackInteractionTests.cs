using System.Collections;
using System.Reflection;
using Horizon.Game;
using Horizon.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Horizon.Tests
{
    public sealed class FeedbackInteractionTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void UnstartedBlockedAndForeignPointerReleasesCannotPlay()
        {
            var go = new GameObject("Gesture", typeof(RectTransform), typeof(HorizonCardDrag));
            try
            {
                var drag = go.GetComponent<HorizonCardDrag>();
                int plays = 0;
                drag.Played = _ => plays++;
                drag.IsOverTarget = _ => true;
                var first = new PointerEventData(null) { pointerId = 1 };
                drag.OnEndDrag(first);
                Assert.AreEqual(0, plays, "A release alone is not an action.");
                drag.CanBegin = _ => false;
                drag.OnBeginDrag(first); drag.OnEndDrag(first);
                Assert.AreEqual(0, plays, "An overlay or another finger can own input.");
                drag.CanBegin = _ => true;
                drag.OnBeginDrag(first);
                drag.OnEndDrag(new PointerEventData(null) { pointerId = 2 });
                Assert.AreEqual(0, plays);
                drag.OnEndDrag(first); drag.OnEndDrag(first);
                Assert.AreEqual(1, plays, "A completed gesture is consumed once.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PersistedReceiptsKeepActualChangesAndReadingPosition()
        {
            var receipt = new FeedbackRecord { kind = FeedbackKind.Echoes, runNumber = 2, day = 7,
                page = 1, presented = true, stardust = 6 };
            receipt.beats.Add(new FeedbackBeat { source = "刻意练习", sourceDay = 4, delta = new ResourceDelta(0,0,0,0,0,3) });
            receipt.beats.Add(new FeedbackBeat { source = "早点休息", sourceDay = 5, delta = new ResourceDelta(1) });
            FeedbackRecord restored = JsonUtility.FromJson<FeedbackRecord>(JsonUtility.ToJson(receipt));
            Assert.AreEqual(1, restored.page); Assert.IsTrue(restored.presented);
            Assert.AreEqual(3, restored.beats[0].delta.ability);
            Assert.AreEqual(1, restored.beats[1].delta.energy);
            Assert.AreEqual(6, restored.stardust);
            Assert.AreEqual("今天 · 立即恢复", PlayExperience.DestinationLabel(CardCatalog.FindById("rest"), 1));
            Assert.That(PlayExperience.DestinationLabel(CardCatalog.FindById("practice"), 11), Does.Contain("截止日之后"));
        }

        [Test]
        public void SculptedMeshesHaveFiniteGeometryAndOutwardPortalNormals()
        {
            Mesh[] meshes = { HorizonSculpt.SoftBlock(), HorizonSculpt.Leaf(), HorizonSculpt.Torus(2,0.08f) };
            try
            {
                foreach (Mesh mesh in meshes)
                {
                    Assert.Greater(mesh.vertexCount, 8);
                    foreach (Vector3 v in mesh.vertices)
                        Assert.IsFalse(float.IsNaN(v.x) || float.IsInfinity(v.y) || float.IsNaN(v.z));
                    foreach (int index in mesh.triangles) Assert.That(index, Is.InRange(0,mesh.vertexCount - 1));
                }
                Assert.Greater(meshes[2].normals[0].x, 0.5f, "The portal's outer surface faces outwards.");
            }
            finally { foreach (Mesh mesh in meshes) Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ArtShadersImportWithoutCompilationErrors()
        {
            foreach (string name in new[] { "HorizonLit", "HorizonGlow", "HorizonAtmosphere", "HorizonContact", "HorizonBloom" })
            {
                Shader shader = Resources.Load<Shader>(name);
                Assert.IsNotNull(shader, name);
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    Assert.AreNotEqual(UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error, message.severity,
                        name + ": " + message.message);
            }
        }

        [UnityTest]
        public IEnumerator MultipleEchoReceiptsWaitAndResumeAtTheUnreadBeat()
        {
            yield return new EnterPlayMode();
            HorizonApp app = Object.FindObjectOfType<HorizonApp>();
            if (app == null) app = new GameObject("Receipt test").AddComponent<HorizonApp>();
            yield return null;
            var session = new GameSession(2,15, 4);
            var archive = new ArchiveData { active = session.Snapshot(), nextRareRun = 99 };
            archive.wallet.Claim("test echoes",6);
            archive.pendingFeedback = new FeedbackRecord { kind = FeedbackKind.Echoes, runNumber = 2, day = 1,
                title = "两条回声", description = "实际变化", stardust = 6 };
            archive.pendingFeedback.beats.Add(new FeedbackBeat { title = "努力回来了", source = "练习", sourceDay = 1,
                destinationDay = 1, delta = new ResourceDelta(0,0,0,0,0,3), meaning = "第一条" });
            archive.pendingFeedback.beats.Add(new FeedbackBeat { title = "休息回来了", source = "休息", sourceDay = 1,
                destinationDay = 1, delta = new ResourceDelta(1), meaning = "第二条" });
            Set(app,"session",session); Set(app,"archive",archive); Call(app,"ShowFeedback");
            yield return new WaitForSecondsRealtime(0.35f);
            ButtonNamed(app,"Review echo details").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.35f);
            Button next = ButtonNamed(app,"Continue result");
            Assert.That(next.GetComponentInChildren<Text>().text, Does.Contain("下一条回声"));
            next.onClick.Invoke(); next.onClick.Invoke(); yield return null;
            Assert.AreEqual(1,archive.pendingFeedback.page);
            Assert.IsNotNull(archive.pendingFeedback,"A double tap cannot skip the next receipt.");
            Assert.AreEqual(1,session.Day);
            Assert.AreEqual(6,archive.wallet.stardust);
            var restored = JsonUtility.FromJson<ArchiveData>(PlayerPrefs.GetString("HORIZON.PROTOTYPE.V1"));
            restored.Repair(); Set(app,"archive",restored); Call(app,"ContinueRun"); yield return null;
            Assert.AreEqual(1,restored.pendingFeedback.page);
            Assert.AreEqual(6,restored.wallet.stardust);
            Assert.IsFalse(Get<RectTransform>(app,"root").GetComponentsInChildren<RewardCounter>().Length > 0,
                "Resuming an already presented result does not replay collection.");
            string text = string.Join(" ",System.Array.ConvertAll(Get<RectTransform>(app,"root").GetComponentsInChildren<Text>(), t => t.text));
            Assert.That(text, Does.Contain("第二条"));
            yield return new WaitForSecondsRealtime(0.3f);
            ButtonNamed(app,"Continue result").onClick.Invoke(); yield return null;
            Assert.IsNull(restored.pendingFeedback);
            Assert.AreEqual(1,Get<GameSession>(app,"session").Day);
            PlayerPrefs.DeleteKey("HORIZON.PROTOTYPE.V1");
            yield return new ExitPlayMode();
        }

        private static Button ButtonNamed(HorizonApp app,string name)
        { return System.Array.Find(Get<RectTransform>(app,"root").GetComponentsInChildren<Button>(), b => b.name == name); }
        private static T Get<T>(HorizonApp app,string name) { return (T)typeof(HorizonApp).GetField(name,Private).GetValue(app); }
        private static void Set(HorizonApp app,string name,object value) { typeof(HorizonApp).GetField(name,Private).SetValue(app,value); }
        private static void Call(HorizonApp app,string name) { typeof(HorizonApp).GetMethod(name,Private).Invoke(app,null); }
    }
}
