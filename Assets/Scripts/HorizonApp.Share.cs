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
    public sealed partial class HorizonApp
    {
        private int shareGeneration;
        private string lastSharePath;
        private readonly List<CanvasGroup> shareNodes = new List<CanvasGroup>();
        private readonly List<TimeThreadGraphic> shareEdges = new List<TimeThreadGraphic>();
        private readonly List<Vector2> shareEdgeEnds = new List<Vector2>();
        private CanvasGroup shareQuote, shareKeys, shareControls;
        private Text shareStatus;
        private RectTransform shareScene;

        private void ShowShareStory(RunRecord run, Action back)
        {
            int generation = ++shareGeneration;
            ArchiveSurface("Ten seconds of your life");
            shareScene = View.Rect(overlay, "Share picture", 0, 0, 1, 1);
            View.Fill(shareScene, "Share night", new Color(0.012f, 0.026f, 0.05f), 0, 0, 1, 1);
            View.Label(shareScene, "Share run", "RUN " + run.number.ToString("000") + " / " + GameSession.RunLength(run) + " DAYS", 27, Palette.Muted,
                TextAnchor.MiddleCenter, 0.05f, 0.897f, 0.95f, 0.946f);
            View.Label(shareScene, "Share name", run.title, 41, Palette.Text,
                TextAnchor.MiddleCenter, 0.055f, 0.8f, 0.945f, 0.893f);
            List<CausalNode> graph = GameSession.GraphForRun(run).Where(n => n.day <= GameSession.RunLength(run)).OrderBy(n => n.day).ToList();
            var positions = new Dictionary<string, Vector2>();
            for (int i = 0; i < graph.Count; i++)
            {
                CausalNode node = graph[i];
                float angle = (node.day - 1) / (float)GameSession.RunLength(run) * Mathf.PI * 2 - Mathf.PI * 0.5f + (i % 3) * 0.14f;
                float radius = 0.24f + (i % 4) * 0.027f;
                positions[node.id] = node.type == CausalNodeKind.Gate ?
                    new Vector2(0.3f + graph.FindAll(n => n.type == CausalNodeKind.Gate).IndexOf(node) * 0.2f, 0.324f) :
                    new Vector2(0.5f + Mathf.Cos(angle) * radius * 1.25f, 0.565f + Mathf.Sin(angle) * radius * 0.78f);
            }
            shareEdges.Clear(); shareEdgeEnds.Clear(); shareNodes.Clear();
            foreach (CausalNode node in graph)
                foreach (string id in CausalGraph.Parents(node))
                {
                    if (!positions.ContainsKey(id)) continue;
                    var edge = View.Rect(shareScene, "A life thread", 0, 0, 1, 1).gameObject.AddComponent<TimeThreadGraphic>();
                    edge.From = positions[id]; edge.To = positions[id]; edge.Thickness = 2.6f;
                    edge.color = new Color(0.5f, 0.93f, 0.83f, 0); edge.raycastTarget = false;
                    shareEdges.Add(edge); shareEdgeEnds.Add(positions[node.id]);
                }
            foreach (CausalNode node in graph)
            {
                Vector2 pos = positions[node.id]; Color color = NodeColor(node);
                RectTransform dot = View.Rect(shareScene, "Share node " + node.id, pos.x - 0.027f, pos.y - 0.015f, pos.x + 0.027f, pos.y + 0.015f);
                CanvasGroup group = dot.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0;
                SoftGlowGraphic glow = View.Rect(dot, "Star glow", -0.7f, -0.7f, 1.7f, 1.7f).gameObject.AddComponent<SoftGlowGraphic>();
                glow.color = new Color(color.r, color.g, color.b, 0.38f); glow.raycastTarget = false;
                View.Panel(dot, "Star halo", new Color(color.r, color.g, color.b, 0.23f), 0, 0, 1, 1, 60);
                View.Panel(dot, "Star core", color, 0.34f, 0.34f, 0.66f, 0.66f, 10);
                shareNodes.Add(group);
            }
            shareKeys = View.Rect(shareScene, "Three turning points", 0, 0, 1, 1).gameObject.AddComponent<CanvasGroup>();
            List<ActionRecord> keys = run.actions.OrderByDescending(a => CausalGraph.Descendants(graph, a.nodeId).Count)
                .Take(3).OrderBy(a => a.day).ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                float y = 0.653f - i * 0.111f;
                View.Panel(shareKeys.transform, "Key choice", Palette.Panel, 0.115f, y, 0.885f, y + 0.086f, 23);
                View.Label(shareKeys.transform, "Key choice text", "DAY " + keys[i].day + "  /  " + keys[i].cardName, 34,
                    Palette.Text, TextAnchor.MiddleCenter, 0.14f, y + 0.012f, 0.86f, y + 0.074f);
            }
            shareQuote = View.Rect(shareScene, "The meaning of this life", 0.055f, 0.122f, 0.945f, 0.29f).gameObject.AddComponent<CanvasGroup>();
            shareQuote.alpha = 0;
            View.Label(shareQuote.transform, "Share causal line", ExperienceContent.ShareLine(run), 36, Palette.Text,
                TextAnchor.MiddleCenter, 0.015f, 0.49f, 0.985f, 1);
            View.Label(shareQuote.transform, "Share brand", "H O R I Z O N", 39, Palette.Mint,
                TextAnchor.MiddleCenter, 0.04f, 0.03f, 0.96f, 0.4f);
            shareControls = View.Rect(overlay, "Share controls", 0, 0, 1, 1).gameObject.AddComponent<CanvasGroup>();
            shareStatus = View.Label(shareControls.transform, "Share status", "三个选择，慢慢连成整段人生。", 23, Palette.Muted,
                TextAnchor.MiddleCenter, 0.06f, 0.946f, 0.94f, 0.987f);
            View.Button(shareControls.transform, "Replay share", "再看一次", () => StartCoroutine(PlayShare(++shareGeneration, run, false)),
                0.04f, 0.045f, 0.26f, 0.107f, Palette.Panel, Palette.Text, 24);
            View.Button(shareControls.transform, "Save share video", "分享视频", () => StartCoroutine(PlayShare(++shareGeneration, run, true, true)),
                0.275f, 0.045f, 0.555f, 0.107f, Palette.Mint, Palette.Ink, 24);
            View.Button(shareControls.transform, "Save share animation", "保存GIF", () => StartCoroutine(PlayShare(++shareGeneration, run, true)),
                0.57f, 0.045f, 0.78f, 0.107f, Palette.Panel, Palette.Text, 23);
            View.Button(shareControls.transform, "Close share", "返回", () => { shareGeneration++; back(); },
                0.795f, 0.045f, 0.96f, 0.107f, Palette.Panel, Palette.Text, 24);
            View.RefreshText(overlay);
            StartCoroutine(PlayShare(generation, run, false));
        }

        private void DrawShare(float seconds)
        {
            shareKeys.alpha = 1 - Mathf.Clamp01((seconds - 2) / 1.6f);
            for (int i = 0; i < shareNodes.Count; i++)
            {
                CanvasGroup group = shareNodes[i];
                float t = Mathf.Clamp01((seconds - 2 - i / (float)Mathf.Max(1, shareNodes.Count) * 4.2f) * 2.6f);
                group.alpha = t;
                group.transform.localScale = Vector3.one * (0.8f + t * 0.2f + Mathf.Sin(seconds * 2.5f + i) * 0.08f);
            }
            for (int i = 0; i < shareEdges.Count; i++)
            {
                float t = Mathf.Clamp01((seconds - 2.4f - i / (float)Mathf.Max(1, shareEdges.Count) * 4.1f) * 2);
                TimeThreadGraphic edge = shareEdges[i];
                edge.To = Vector2.Lerp(edge.From, shareEdgeEnds[i], t);
                edge.color = new Color(0.5f, 0.93f, 0.83f, t * 0.52f);
                edge.SetVerticesDirty();
            }
            shareQuote.alpha = Mathf.Clamp01((seconds - 7.3f) / 1.1f);
        }

        private IEnumerator PlayShare(int generation, RunRecord run, bool export, bool video = false)
        {
            IEnumerator playback = PlayShareCore(generation, run, export, video);
            try
            {
                while (true)
                {
                    bool moved = false, failed = false;
                    object frame = null;
                    try { moved = playback.MoveNext(); if (moved) frame = playback.Current; }
                    catch (Exception exception)
                    {
                        failed = true;
                        Debug.LogWarning("Timeline recording could not finish: " + exception.Message);
                        if (generation == shareGeneration && shareStatus != null)
                            shareStatus.text = "这次没有保存完成。可以返回，或再试一次。";
                    }
                    if (failed || !moved) yield break;
                    yield return frame;
                }
            }
            finally { (playback as IDisposable)?.Dispose(); }
        }

        private IEnumerator PlayShareCore(int generation, RunRecord run, bool export, bool video)
        {
            if (shareScene == null) yield break;
            if (!export)
            {
                float age = 0;
                while (age < 10 && generation == shareGeneration && shareScene != null)
                { DrawShare(age); age += Time.unscaledDeltaTime; yield return null; }
                if (generation == shareGeneration && shareScene != null) DrawShare(10);
                yield break;
            }
            string directory = Path.Combine(Application.persistentDataPath, "Timelines");
            Directory.CreateDirectory(directory);
            string final = Path.Combine(directory, "HORIZON-RUN-" + run.number.ToString("000") + ".gif");
            string temporary = final + ".tmp";
            string frames = video ? Path.Combine(directory, "frames-" + Guid.NewGuid().ToString("N")) : null;
            if (video) Directory.CreateDirectory(frames);
            bool completed = false;
            Canvas canvas = root.GetComponentInParent<Canvas>();
            Camera sceneCamera = world.WorldCamera;
            bool sceneWasEnabled = sceneCamera.enabled;
            float start = Time.unscaledTime;
            lastSharePath = null;
            foreach (Button button in shareControls.GetComponentsInChildren<Button>())
                if (button.name != "Close share") button.interactable = false;
            try
            {
                // The sharing canvas is opaque. Rendering its hidden world again
                // adds shadows and post processing to every recorded frame.
                sceneCamera.enabled = false;
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write))
                using (var gif = new TimelineGifWriter(stream, 540, 960))
                using (var recorder = new SharePortraitRecorder())
                {
                    for (int frame = 0; frame < 60; frame++)
                    {
                        if (generation != shareGeneration || shareScene == null) yield break;
                        DrawShare(frame / 6f);
                        shareStatus.text = "正在保存时间动画 · " + (frame * 100 / 60) + "%";
                        yield return null;
                        if (generation != shareGeneration || shareScene == null) yield break;
                        shareControls.alpha = 0;
                        Color32[] pixels = recorder.Capture(canvas);
                        shareControls.alpha = 1;
                        gif.Frame(pixels, frame % 3 == 0 ? 16 : 17);
                        if (video) recorder.SavePng(Path.Combine(frames, "frame-" + frame.ToString("000") + ".png"));
                        while (Time.unscaledTime < start + (frame + 1) / 6f) yield return null;
                    }
                }
                if (File.Exists(final)) File.Delete(final);
                File.Move(temporary, final);
                completed = true;
                lastSharePath = final;
                sceneCamera.enabled = sceneWasEnabled;
                if (video)
                {
                    shareStatus.text = "正在编码十秒视频与声音…";
                    string pcm = Path.Combine(frames, "story.pcm"); File.WriteAllBytes(pcm, TimelineSoundtrack.Pcm(run));
                    string mp4 = Path.ChangeExtension(final, ".mp4");
                    var encoding = TimelineVideoWriter.EncodeAsync(frames, pcm, mp4);
                    while (!encoding.IsCompleted) yield return null;
                    if (generation != shareGeneration || shareStatus == null) yield break;
                    if (encoding.Result == "") lastSharePath = mp4;
                    else { shareStatus.text = "当前设备的视频编码没有完成，已保留十秒GIF。可以选择保存GIF分享。"; yield break; }
                }
                Debug.Log("Timeline recording finished in " + (Time.unscaledTime - start).ToString("0.0") + " seconds.");
                if (shareStatus != null) shareStatus.text = TimelineSharing.Publish(lastSharePath);
                DrawShare(10);
            }
            finally
            {
                if (sceneCamera != null) sceneCamera.enabled = sceneWasEnabled;
                if (!completed && File.Exists(temporary)) File.Delete(temporary);
                if (frames != null && Directory.Exists(frames)) Directory.Delete(frames, true);
                if (generation == shareGeneration && shareControls != null)
                {
                    shareControls.alpha = 1;
                    foreach (Button button in shareControls.GetComponentsInChildren<Button>()) button.interactable = true;
                }
            }
        }

        private sealed class SharePortraitRecorder : IDisposable
        {
            private readonly RenderTexture image;
            private readonly Camera camera;
            private readonly Texture2D pixels;
            public SharePortraitRecorder()
            {
                image = new RenderTexture(540, 960, 24); image.Create();
                camera = new GameObject("Timeline recording camera", typeof(Camera)).GetComponent<Camera>();
                camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Palette.Ink; camera.cullingMask = 1 << 5;
                camera.nearClipPlane = 0.1f; camera.farClipPlane = 20; camera.targetTexture = image;
                pixels = new Texture2D(540, 960, TextureFormat.RGB24, false);
            }
            public Color32[] Capture(Canvas canvas)
            {
                RenderMode mode = canvas.renderMode; Camera oldCamera = canvas.worldCamera;
                RenderTexture oldActive = RenderTexture.active;
                float distance = canvas.planeDistance;
                try
                {
                    foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 5;
                    View.RefreshText(canvas.transform);
                    camera.Render(); RenderTexture.active = image;
                    pixels.ReadPixels(new Rect(0, 0, 540, 960), 0, 0, false);
                    return pixels.GetPixels32();
                }
                finally
                {
                    canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance;
                    RenderTexture.active = oldActive;
                    View.RefreshText(canvas.transform);
                }
            }
            public void SavePng(string path) { File.WriteAllBytes(path, pixels.EncodeToPNG()); }
            public void Dispose()
            { image.Release(); UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(pixels); UnityEngine.Object.Destroy(camera.gameObject); }
        }
    }
}
