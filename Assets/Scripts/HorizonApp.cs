using System;
using System.Collections;
using System.Collections.Generic;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Horizon
{
    [Serializable]
    public sealed class ArchiveData
    {
        public List<RunRecord> runs = new List<RunRecord>();
        public RunSnapshot active;
        public bool seenFirstEcho;
        public int calibrations;
    }

    public sealed class HorizonApp : MonoBehaviour
    {
        private const string SaveKey = "HORIZON.PROTOTYPE.V1";
        private const string FingerHint = "按住上方，凝视未来";
        private ArchiveData archive;
        private GameSession session;
        private RectTransform root;
        private RectTransform overlay;
        private RectTransform trail;
        private Text dragHint;
        private RectTransform destinationBeacon;
        private readonly Dictionary<HorizonCardDrag, CardSpec> cards = new Dictionary<HorizonCardDrag, CardSpec>();
        private bool busy;
        private bool detailVisible;
        private int mapIndex;
        private int stationStage;
        private readonly int[] forecastOffsets = new int[3];
        private Rect lastSafeArea;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindObjectOfType<HorizonApp>() == null)
                new GameObject("HORIZON Runtime").AddComponent<HorizonApp>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            View.Font = ChooseFont();
            archive = LoadArchive();

            var canvasObject = new GameObject("HORIZON Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            root = View.Rect(canvasObject.transform, "Safe area", 0, 0, 1, 1);
            ApplySafeArea();
            if (FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(events);
            }
            DontDestroyOnLoad(gameObject);
            DontDestroyOnLoad(canvasObject);
        }

        private void Start()
        {
            if (archive.runs.Count == 0)
            {
                if (archive.active == null) ShowIntro();
                else ContinueRun();
            }
            else ShowHome();
        }

        private void Update()
        {
            if (!Screen.safeArea.Equals(lastSafeArea)) ApplySafeArea();
        }

        private static Font ChooseFont()
        {
            string[] preferred = { "Noto Sans CJK SC", "Noto Sans SC", "PingFang SC",
                "Microsoft YaHei", "Droid Sans Fallback", "Source Han Sans" };
            string[] installed = Font.GetOSInstalledFontNames();
            foreach (string name in preferred)
                foreach (string installedName in installed)
                    if (installedName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                        return Font.CreateDynamicFontFromOSFont(installedName, 44);
            return Font.CreateDynamicFontFromOSFont(new[] { "Arial Unicode MS", "Arial" }, 44);
        }

        private static ArchiveData LoadArchive()
        {
            try
            {
                string json = PlayerPrefs.GetString(SaveKey, "");
                ArchiveData data = string.IsNullOrEmpty(json) ? new ArchiveData() : JsonUtility.FromJson<ArchiveData>(json);
                if (data != null)
                {
                    if (data.runs == null) data.runs = new List<RunRecord>();
                    return data;
                }
            }
            catch (Exception exception) { Debug.LogWarning("Could not read HORIZON save: " + exception.Message); }
            return new ArchiveData();
        }

        private void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(archive));
            PlayerPrefs.Save();
        }

        private void ApplySafeArea()
        {
            Rect safe = Screen.safeArea;
            lastSafeArea = safe;
            root.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            root.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        }

        private void Clear()
        {
            for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
            cards.Clear();
            overlay = null;
            trail = null;
            dragHint = null;
            destinationBeacon = null;
            busy = false;
            Backdrop();
        }

        private void Backdrop()
        {
            View.Fill(root, "Night", Palette.Ink, 0, 0, 1, 1);
            Texture2D artwork = Resources.Load<Texture2D>("HorizonSky");
            if (artwork != null)
            {
                RawImage image = View.Rect(root, "Painted horizon", 0, 0, 1, 1).gameObject.AddComponent<RawImage>();
                image.texture = artwork;
                image.raycastTarget = false;
            }
            View.Fill(root, "Readable future", new Color(0.006f, 0.018f, 0.033f, 0.28f), 0, 0.77f, 1, 1);
            View.Fill(root, "Readable actions", new Color(0.006f, 0.016f, 0.031f, 0.46f), 0, 0, 1, 0.51f);
            View.Fill(root, "Horizon glint", new Color(0.55f, 0.97f, 0.82f, 0.22f), 0.05f, 0.776f, 0.95f, 0.777f);
        }

        private void ShowIntro()
        {
            Clear();
            View.Fill(root, "Black", new Color(0.012f, 0.025f, 0.043f, 0.92f), 0, 0, 1, 1);
            View.Label(root, "First question", "你想看看未来的自己吗？", 51, Palette.Text,
                TextAnchor.MiddleCenter, 0.08f, 0.48f, 0.92f, 0.59f);
            View.Button(root, "Look", "看看", () => StartCoroutine(IntroSequence()),
                0.37f, 0.36f, 0.63f, 0.415f, Palette.Panel, Palette.Mint, 30);
        }

        private IEnumerator IntroSequence()
        {
            Clear();
            View.Fill(root, "Black", new Color(0.012f, 0.025f, 0.043f, 0.63f), 0, 0, 1, 1);
            View.Fill(root, "Horizon", Palette.Mint, 0.1f, 0.47f, 0.9f, 0.471f);
            yield return new WaitForSeconds(0.8f);
            View.Label(root, "Promise", "但你只能看到自己创造出来的未来。", 36,
                Palette.Text, TextAnchor.MiddleCenter, 0.08f, 0.29f, 0.92f, 0.39f);
            yield return new WaitForSeconds(1.35f);
            StartNewRun();
        }

        private void StartNewRun()
        {
            detailVisible = false;
            session = new GameSession(archive.runs.Count + 1);
            archive.active = session.Snapshot();
            Save();
            BuildBoard();
        }

        private void ContinueRun()
        {
            if (archive.active == null) { StartNewRun(); return; }
            try
            {
                session = GameSession.Restore(archive.active);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not resume HORIZON run: " + exception.Message);
                StartNewRun();
                return;
            }
            detailVisible = false;
            if (session.HasChosen && session.Day == 4 && !session.StationVisited)
            {
                ShowInRunStation(0);
                return;
            }
            if (session.HasChosen && session.Day < GameSession.LastDay)
            {
                StartCoroutine(AdvanceDay());
                return;
            }
            if (session.HasPredictionReview)
            {
                ShowPredictionReview();
                return;
            }
            BuildBoard();
        }

        private void ShowHome()
        {
            Clear();
            View.Label(root, "Logo", "H O R I Z O N", 57, Palette.Text,
                TextAnchor.MiddleCenter, 0.08f, 0.82f, 0.92f, 0.91f);
            View.Label(root, "Home line", "你走过的路，还在发光。", 35, Palette.Muted,
                TextAnchor.MiddleCenter, 0.12f, 0.39f, 0.88f, 0.47f);
            View.Button(root, "Continue", "继续一生", ContinueRun, 0.12f, 0.265f, 0.88f, 0.335f,
                Palette.Mint, Palette.Ink, 37);
            View.Button(root, "Map", "时间地图", () => ShowMap(false), 0.12f, 0.178f, 0.49f, 0.247f,
                Palette.Panel, Palette.Text);
            View.Button(root, "Station", "未来站", ShowStation, 0.51f, 0.178f, 0.88f, 0.247f,
                Palette.Panel, Palette.Text);
            View.Button(root, "Echoes", "回声档案", ShowEchoArchive, 0.12f, 0.09f, 0.88f, 0.159f,
                Palette.Panel, Palette.Text);
        }

        private void BuildBoard()
        {
            if (session != null && session.CanPredict) { ShowPrediction(); return; }
            Clear();
            if (session == null) return;
            FutureRegion();
            PresentRegion();
            ResourceRegion();
            TimelineRegion();
            HandRegion();
        }

        private void ShowPrediction()
        {
            Clear();
            Array.Clear(forecastOffsets, 0, forecastOffsets.Length);
            View.Fill(root, "Prediction hush", new Color(0.01f, 0.03f, 0.05f, 0.75f), 0, 0, 1, 1);
            View.Label(root, "Day", "DAY 04  /  12", 31, Palette.Muted,
                TextAnchor.MiddleCenter, 0.1f, 0.88f, 0.9f, 0.94f);
            View.Label(root, "Prediction title", "画下三天后的自己", 47, Palette.Text,
                TextAnchor.MiddleCenter, 0.06f, 0.77f, 0.94f, 0.85f);
            View.Label(root, "Prediction note", "沿着轨道拖动，猜猜 Day 7 会如何变化。", 28,
                Palette.Muted, TextAnchor.MiddleCenter, 0.07f, 0.7f, 0.93f, 0.77f);
            string[] names = { "精力", "心情", "洞察" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                float x = 0.14f + i * 0.29f;
                View.Label(root, "Axis name", names[i], 31, Palette.Text,
                    TextAnchor.MiddleCenter, x - 0.035f, 0.625f, x + 0.18f, 0.68f);
                Image track = View.Fill(root, "Draw future", new Color(0.04f, 0.11f, 0.14f, 0.88f),
                    x, 0.33f, x + 0.15f, 0.625f, true);
                View.Fill(track.transform, "Zero", new Color(0.84f, 0.88f, 0.85f, 0.4f),
                    0.12f, 0.498f, 0.88f, 0.502f);
                View.Label(track.transform, "Up", "+", 25, Palette.Muted,
                    TextAnchor.MiddleCenter, 0.34f, 0.82f, 0.66f, 0.96f);
                View.Label(track.transform, "Down", "-", 25, Palette.Muted,
                    TextAnchor.MiddleCenter, 0.34f, 0.04f, 0.66f, 0.18f);
                RectTransform marker = View.Panel(track.transform, "Forecast mark", Palette.Mint,
                    0.23f, 0.47f, 0.77f, 0.53f, 23).rectTransform;
                Text value = View.Label(root, "Forecast value", "不变", 27, Palette.Mint,
                    TextAnchor.MiddleCenter, x - 0.05f, 0.26f, x + 0.2f, 0.32f);
                track.gameObject.AddComponent<PredictionAxisDrag>().Changed = next =>
                {
                    forecastOffsets[index] = next;
                    float center = 0.1f + (next + 3) / 6f * 0.8f;
                    marker.anchorMin = new Vector2(0.23f, center - 0.03f);
                    marker.anchorMax = new Vector2(0.77f, center + 0.03f);
                    value.text = Direction(next);
                };
            }
            View.Label(root, "Seal line", "预测不是答案，是你此刻理解世界的方式。", 29,
                Palette.Text, TextAnchor.MiddleCenter, 0.08f, 0.19f, 0.92f, 0.25f);
            View.Button(root, "Lock prediction", "封存预测  /  LOCK", () =>
            {
                session.LockPrediction(forecastOffsets[0], forecastOffsets[1], forecastOffsets[2]);
                archive.active = session.Snapshot();
                Save();
                BuildBoard();
            }, 0.15f, 0.075f, 0.85f, 0.15f, Palette.Mint, Palette.Ink, 31);
        }

        private static string Direction(int value)
        {
            return value == 0 ? "不变" : value > 0 ? "上升 " + value : "下降 " + -value;
        }

        private void ShowPredictionReview()
        {
            Clear();
            PredictionRecord prediction = session.Prediction;
            bool close = prediction.accurate;
            View.Fill(root, "Comparison hush", new Color(0.01f, 0.03f, 0.05f, 0.82f), 0, 0, 1, 1);
            View.Label(root, "Day", "DAY 07  /  DAY 04", 29, Palette.Muted,
                TextAnchor.MiddleCenter, 0.08f, 0.89f, 0.92f, 0.95f);
            View.Label(root, "Verdict", close ? "SYNCHRONIZED" : "SURPRISE", 53,
                close ? Palette.Mint : Palette.Gold, TextAnchor.MiddleCenter,
                0.04f, 0.75f, 0.96f, 0.85f);
            View.Label(root, "Verdict line", close ? "你看见了一部分即将到来的自己。" :
                "这里，与你想的不一样。", 33, Palette.Text,
                TextAnchor.MiddleCenter, 0.06f, 0.69f, 0.94f, 0.76f);
            string[] names = { "精力", "心情", "洞察" };
            int[] expected = { prediction.energy, prediction.mood, prediction.insight };
            int[] actual = { prediction.actualEnergy, prediction.actualMood, prediction.actualInsight };
            View.Label(root, "Legend", "琥珀 · 你的预测       薄荷 · 真实结果", 24, Palette.Muted,
                TextAnchor.MiddleCenter, 0.08f, 0.62f, 0.92f, 0.68f);
            for (int i = 0; i < 3; i++)
            {
                float y = 0.55f - i * 0.115f;
                View.Label(root, "Axis name", names[i], 29, Palette.Text,
                    TextAnchor.MiddleLeft, 0.11f, y + 0.015f, 0.27f, y + 0.068f);
                View.Fill(root, "Comparison rail", new Color(0.44f, 0.65f, 0.64f, 0.52f),
                    0.31f, y + 0.032f, 0.81f, y + 0.034f);
                float forecastX = 0.31f + (expected[i] + 3) / 6f * 0.5f;
                float actualX = 0.31f + Mathf.Clamp01((actual[i] + 3) / 6f) * 0.5f;
                View.Panel(root, "Forecast point", Palette.Gold,
                    forecastX - 0.012f, y + 0.035f, forecastX + 0.012f, y + 0.048f, 13);
                View.Panel(root, "Actual point", Palette.Mint,
                    actualX - 0.012f, y + 0.018f, actualX + 0.012f, y + 0.031f, 13);
                View.Label(root, "Comparison text", Direction(expected[i]) + "  /  " + Direction(actual[i]),
                    22, Palette.Muted, TextAnchor.MiddleLeft, 0.31f, y - 0.015f, 0.89f, y + 0.016f);
            }
            if (close)
                View.Label(root, "Understanding", archive.calibrations == 0 ?
                    "解锁 · 未来方向" : "未来方向更加清晰", 29, Palette.Mint,
                    TextAnchor.MiddleCenter, 0.08f, 0.165f, 0.92f, 0.245f);
            else
                View.Button(root, "Why", "为什么？  查看因果线", () => ShowMap(true),
                    0.18f, 0.16f, 0.82f, 0.225f, Palette.Panel, Palette.Text, 26);
            View.Button(root, "Continue", "继续前行", () =>
            {
                if (prediction.accurate) archive.calibrations++;
                session.MarkPredictionReviewed();
                archive.active = session.Snapshot();
                Save();
                BuildBoard();
            }, 0.17f, 0.06f, 0.83f, 0.13f, Palette.Mint, Palette.Ink);
        }

        private void FutureRegion()
        {
            Image hit = View.Fill(root, "Hold future", new Color(0, 0, 0, 0), 0.01f, 0.79f, 0.99f, 0.99f, true);
            hit.gameObject.AddComponent<FutureHold>().Activated = ShowFocus;
            View.Label(root, "Day", string.Format("DAY {0:00} / 12", session.Day), 37, Palette.Text,
                TextAnchor.MiddleLeft, 0.07f, 0.935f, 0.55f, 0.979f);
            View.Label(root, "Vision", "HORIZON " + Roman(session.HorizonLevel), 23, Palette.Mint,
                TextAnchor.MiddleRight, 0.53f, 0.937f, 0.92f, 0.978f);
            View.Label(root, "Future caption", archive.calibrations > 0 ?
                "未来 · 方向正在显形" : "未来 · 尚未发生", 24, Palette.Muted,
                TextAnchor.MiddleLeft, 0.075f, 0.875f, 0.49f, 0.91f);
            View.Fill(root, "Future rail", new Color(0.55f, 0.90f, 0.80f, 0.40f),
                0.12f, 0.843f, 0.88f, 0.844f);
            for (int i = 0; i < 4; i++)
            {
                int target = i == 0 ? session.Day : Math.Min(GameSession.LastDay, session.Day + i * 2);
                PendingEcho echo = session.Pending.Find(e => e.dueDay == target);
                string glyph = i == 0 ? "" : "?";
                Color color = i == 0 ? Palette.Mint : Palette.Muted;
                if (echo != null)
                {
                    if (session.HorizonLevel >= 2 || echo.kind == CardKind.Temptation) glyph = "";
                    color = echo.kind == CardKind.Temptation ? Palette.Coral : Palette.Mint;
                }
                float x = 0.105f + i * 0.245f;
                View.Panel(root, "Future halo", new Color(color.r, color.g, color.b, i == 0 ? 0.25f : 0.12f),
                    x, 0.831f, x + 0.047f, 0.857f, 30);
                View.Panel(root, "Future core", glyph == "?" ? Palette.Deep : color,
                    x + 0.012f, 0.838f, x + 0.035f, 0.851f, 15);
                if (glyph == "?") View.Label(root, "Unknown", "?", 23, Palette.Muted,
                    TextAnchor.MiddleCenter, x - 0.012f, 0.846f, x + 0.06f, 0.885f);
                View.Label(root, "Day mark", i == 0 ? "今天" : "D" + target, 21, Palette.Muted,
                    TextAnchor.MiddleCenter, x - 0.028f, 0.802f, x + 0.075f, 0.832f);
            }
            View.Label(root, "Focus hint", session.FocusUses == 0 ? FingerHint : "今天已经凝视过未来",
                21, Palette.Muted, TextAnchor.MiddleCenter, 0.11f, 0.765f, 0.89f, 0.794f);
            View.Button(root, "Map", "时间地图", () => ShowMap(true), 0.745f, 0.877f,
                0.95f, 0.911f, new Color(0.09f, 0.20f, 0.23f, 0.8f), Palette.Text, 20);
        }

        private static string Roman(int value) { return value == 1 ? "I" : value == 2 ? "II" : "III"; }

        private void PresentRegion()
        {
            View.Label(root, "Now", "现在 / 夜", 22, Palette.Mint, TextAnchor.MiddleLeft,
                0.08f, 0.682f, 0.48f, 0.717f);
            View.Label(root, "Scene line", session.SocialUnavailableToday ?
                "那次疲惫，今天让你错过了邀约。" : session.Day == 4 && session.Prediction != null ?
                "你已画下未来，今天会走向哪里？" : session.Day == 1 ? "今天的你，会留给未来什么？" :
                session.Pending.Count > 0 ? "你留下的选择，正在路上。" : "今晚，你想把什么送向明天？", 37,
                Palette.Text, TextAnchor.MiddleLeft, 0.08f, 0.565f, 0.83f, 0.639f);
            View.Fill(root, "Present accent", new Color(0.59f, 0.98f, 0.82f, 0.86f),
                0.08f, 0.553f, 0.21f, 0.555f);
        }

        private void ResourceRegion()
        {
            int[] values = { session.Energy, session.Mood, session.Insight };
            string[] names = { "精力", "心情", "洞察" };
            View.Label(root, "State heading", "此刻的状态", 22, Palette.Muted, TextAnchor.MiddleLeft,
                0.07f, 0.496f, 0.54f, 0.532f);
            for (int row = 0; row < 3; row++)
            {
                float x0 = 0.065f + row * 0.295f;
                float x1 = x0 + 0.28f;
                View.Panel(root, "State glass", new Color(0.025f, 0.07f, 0.105f, 0.81f),
                    x0, 0.407f, x1, 0.491f, 18);
                View.Label(root, "Resource", names[row], 25, values[row] <= 2 ? Palette.Coral : Palette.Text,
                    TextAnchor.MiddleLeft, x0 + 0.025f, 0.449f, x1 - 0.02f, 0.481f);
                for (int pip = 0; pip < 5; pip++)
                {
                    float x = x0 + 0.025f + pip * 0.047f;
                    View.Panel(root, "State trace", pip * 2 < values[row] ?
                        (values[row] <= 2 ? Palette.Coral : Palette.Mint) : new Color(0.36f, 0.52f, 0.55f, 0.42f),
                        x, 0.427f, x + 0.035f, 0.431f, 4);
                }
                if (detailVisible || values[row] <= 2)
                    View.Label(root, "Value", values[row].ToString(), 24,
                        values[row] <= 2 ? Palette.Coral : Palette.Text, TextAnchor.MiddleCenter,
                        x1 - 0.065f, 0.45f, x1 - 0.02f, 0.48f);
            }
        }

        private void TimelineRegion()
        {
            View.Panel(root, "Memory glass", new Color(0.02f, 0.055f, 0.08f, 0.69f),
                0.06f, 0.335f, 0.94f, 0.397f, 17);
            View.Label(root, "Past", "已走过", 21, Palette.Muted, TextAnchor.MiddleLeft,
                0.085f, 0.365f, 0.32f, 0.393f);
            View.Label(root, "Deadline", "截止日", 21, Palette.Muted, TextAnchor.MiddleRight,
                0.68f, 0.365f, 0.914f, 0.393f);
            View.Fill(root, "Timeline", new Color(0.48f, 0.76f, 0.74f, 0.35f),
                0.105f, 0.351f, 0.885f, 0.352f);
            for (int day = 1; day <= GameSession.LastDay; day++)
            {
                float x = 0.102f + (day - 1) * 0.0697f;
                ActionRecord action = session.Actions.Find(a => a.day == day);
                bool due = session.Pending.Exists(e => e.dueDay == day);
                Color color = due ? Palette.Mint : action == null ? new Color(0.45f, 0.60f, 0.62f, 0.35f) :
                    action.kind == CardKind.Temptation ? Palette.Coral :
                    action.kind == CardKind.Growth ? Palette.Mint : Palette.Gold;
                if (due) View.Panel(root, "Echo glow", new Color(color.r, color.g, color.b, 0.18f),
                    x - 0.008f, 0.339f, x + 0.032f, 0.363f, 18);
                View.Panel(root, "Past node", color, x, 0.346f, x + 0.022f, 0.357f, 12);
            }
        }

        private void HandRegion()
        {
            View.Label(root, "Invitation", "把今天，送向未来", 31, Palette.Text,
                TextAnchor.MiddleCenter, 0.1f, 0.284f, 0.9f, 0.326f);
            View.Label(root, "Drag hint", "按住卡牌，向地平线推", 23, Palette.Muted,
                TextAnchor.MiddleCenter, 0.2f, 0.039f, 0.8f, 0.073f);
            trail = View.Fill(root, "Causal light", new Color(0.53f, 1f, 0.83f, 0.76f),
                0.5f, 0.5f, 0.5f, 0.5f).rectTransform;
            trail.gameObject.SetActive(false);
            destinationBeacon = View.Rect(root, "Destination beacon", 0.225f, 0.715f, 0.775f, 0.76f);
            View.Panel(destinationBeacon, "Beacon glow", new Color(0.39f, 0.94f, 0.78f, 0.22f),
                0, 0, 1, 1, 19);
            dragHint = View.Label(destinationBeacon, "Destination", "", 25, Palette.Mint,
                TextAnchor.MiddleCenter, 0.02f, 0.07f, 0.98f, 0.93f);
            destinationBeacon.gameObject.SetActive(false);
            CardSpec[] hand = session.Hand;
            for (int i = 0; i < hand.Length; i++)
            {
                CardSpec card = hand[i];
                float x = 0.043f + i * 0.308f;
                RectTransform rect = View.Rect(root, card.Id, x, 0.083f, x + 0.298f, 0.279f);
                bool available = session.CanPlay(card);
                var panel = rect.gameObject.AddComponent<RoundedGraphic>();
                panel.color = available ? new Color(0.037f, 0.09f, 0.127f, 0.95f) :
                    new Color(0.045f, 0.067f, 0.08f, 0.84f);
                panel.radius = 31f;
                panel.raycastTarget = true;
                Color accent = card.Kind == CardKind.Growth ? Palette.Mint :
                    card.Kind == CardKind.Temptation ? Palette.Coral : Palette.Gold;
                View.Fill(rect, "Card accent", accent, 0.095f, 0.92f, 0.55f, 0.926f);
                View.Panel(rect, "Quiet symbol", new Color(accent.r, accent.g, accent.b, 0.14f),
                    0.73f, 0.78f, 0.91f, 0.88f, 26);
                View.Panel(rect, "Symbol core", accent, 0.795f, 0.815f, 0.845f, 0.844f, 13);
                View.Label(rect, "Type", card.Kind == CardKind.Growth ? "长 线" :
                    card.Kind == CardKind.Temptation ? "即 时" : "恢 复", 23, accent,
                    TextAnchor.MiddleLeft, 0.105f, 0.75f, 0.7f, 0.89f);
                View.Label(rect, "Name", card.Name, 33, Palette.Text,
                    TextAnchor.MiddleLeft, 0.105f, 0.48f, 0.91f, 0.71f);
                View.Fill(rect, "Now divider", new Color(accent.r, accent.g, accent.b, 0.3f),
                    0.105f, 0.445f, 0.895f, 0.448f);
                View.Label(rect, "Now", available ? card.Now.ShortLabel() : "精力不足",
                    25, available ? Palette.Text : Palette.Coral,
                    TextAnchor.MiddleLeft, 0.105f, 0.278f, 0.91f, 0.43f);
                string futureLabel = card.Delay > 0 && session.Day + card.Delay > GameSession.LastDay ?
                    "截止日之后 · 未兑现" : card.FutureHint;
                View.Label(rect, "Future", futureLabel, 22, Palette.Muted,
                    TextAnchor.MiddleLeft, 0.105f, 0.075f, 0.91f, 0.23f);
                var drag = rect.gameObject.AddComponent<HorizonCardDrag>();
                drag.Available = available;
                drag.Dragged = CardDragged;
                drag.Played = CardPlayed;
                drag.Tapped = CardTapped;
                cards.Add(drag, card);
                CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
                StartCoroutine(RiseCard(rect, group, i * 0.08f));
            }
        }

        private IEnumerator RiseCard(RectTransform rect, CanvasGroup group, float delay)
        {
            group.alpha = 0;
            rect.localScale = Vector3.one * 0.94f;
            yield return new WaitForSeconds(delay);
            if (rect == null || group == null) yield break;
            float elapsed = 0;
            while (elapsed < 0.22f)
            {
                if (rect == null || group == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.22f);
                group.alpha = t;
                rect.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, t);
                yield return null;
            }
            group.alpha = 1;
            rect.localScale = Vector3.one;
        }

        private void CardDragged(HorizonCardDrag drag, Vector2 pointer)
        {
            if (busy || trail == null) return;
            bool visible = pointer != Vector2.zero;
            visible = visible && pointer.y > Screen.height * 0.29f;
            trail.gameObject.SetActive(visible);
            destinationBeacon.gameObject.SetActive(visible);
            if (!visible) { dragHint.text = ""; return; }
            CardSpec card = cards[drag];
            Vector2 from = new Vector2(pointer.x, Screen.height * 0.31f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, from, null, out Vector2 localFrom);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointer, null, out Vector2 localTo);
            Vector2 vector = localTo - localFrom;
            trail.pivot = new Vector2(0, 0.5f);
            trail.anchoredPosition = localFrom;
            trail.sizeDelta = new Vector2(vector.magnitude, 5);
            trail.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg);
            if (card.Delay == 0) dragHint.text = "此刻 · 即时回响";
            else
            {
                int offset = Mathf.Clamp(Mathf.CeilToInt(Mathf.InverseLerp(0.33f, 0.78f,
                    pointer.y / Screen.height) * card.Delay), 1, card.Delay);
                int destination = session.Day + offset;
                dragHint.text = "DAY " + destination + "  /  " +
                    (destination > GameSession.LastDay ? "截止日之后" :
                        offset == card.Delay ? card.FutureHint : "仍然未知");
            }
        }

        private void CardTapped(HorizonCardDrag drag)
        {
            detailVisible = !detailVisible;
            BuildBoard();
        }

        private void CardPlayed(HorizonCardDrag drag)
        {
            if (busy || !cards.TryGetValue(drag, out CardSpec card)) return;
            busy = true;
            foreach (HorizonCardDrag item in cards.Keys) item.Available = false;
            ActionRecord action = session.Choose(card.Id);
            if (session.CompletedRun == null) archive.active = session.Snapshot();
            else
            {
                archive.runs.Add(session.CompletedRun);
                archive.active = null;
            }
            Save();
            StartCoroutine(ResolveChoice(drag, card, action));
        }

        private IEnumerator ResolveChoice(HorizonCardDrag drag, CardSpec card, ActionRecord action)
        {
            trail.gameObject.SetActive(false);
            destinationBeacon.gameObject.SetActive(false);
            RectTransform selected = (RectTransform)drag.transform;
            Vector3 start = selected.position;
            float targetX = card.Kind == CardKind.Temptation ? 0.5f :
                0.105f + Mathf.Clamp01(card.Delay / 6f) * 0.735f;
            float targetY = card.Kind == CardKind.Temptation ? 0.24f :
                card.Delay == 0 ? 0.54f : 0.85f;
            Vector3 destination = new Vector3(Screen.width * targetX, Screen.height * targetY, start.z);
            float travel = 0;
            while (travel < 0.32f)
            {
                travel += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(travel / 0.32f));
                selected.position = Vector3.Lerp(start, destination, t);
                selected.localScale = Vector3.one * (card.Kind == CardKind.Temptation ?
                    Mathf.Lerp(1f, 1.16f, t) : Mathf.Lerp(1f, 0.18f, t));
                yield return null;
            }
            selected.gameObject.SetActive(false);
            Color color = card.Kind == CardKind.Temptation ? Palette.Coral : Palette.Mint;
            if (card.Kind == CardKind.Temptation)
                View.Fill(root, "Brief impact", new Color(1f, 0.32f, 0.28f, 0.12f), 0, 0, 1, 1);
            else if (card.Delay > 0)
            {
                View.Panel(root, "Seed halo", new Color(0.36f, 0.96f, 0.77f, 0.22f),
                    targetX - 0.04f, 0.83f, targetX + 0.04f, 0.87f, 30);
                View.Panel(root, "Seed core", Palette.Mint,
                    targetX - 0.009f, 0.845f, targetX + 0.009f, 0.854f, 9);
            }
            overlay = View.Rect(root, "Action pulse", 0, 0.38f, 1, 0.72f);
            View.Label(overlay, "Action name", card.Name, 44, Palette.Text,
                TextAnchor.MiddleCenter, 0.08f, 0.48f, 0.92f, 0.76f);
            View.Label(overlay, "Immediate change", card.Now.ShortLabel(),
                card.Kind == CardKind.Temptation ? 53 : 36, color,
                TextAnchor.MiddleCenter, 0.13f, 0.28f, 0.87f, 0.52f);
            if (card.Kind == CardKind.Temptation)
            {
                View.Fill(overlay, "Impact", new Color(1f, 0.38f, 0.29f, 0.18f),
                    0.04f, 0.04f, 0.96f, 0.15f);
                Handheld.Vibrate();
            }
            else if (card.Delay > 0)
                View.Label(overlay, "Seed", "DAY " + action.echoDay + " · 已埋下", 28, Palette.Mint,
                    TextAnchor.MiddleCenter, 0.2f, 0.08f, 0.8f, 0.27f);
            yield return new WaitForSeconds(card.Kind == CardKind.Temptation ? 0.62f : 0.5f);
            Destroy(overlay.gameObject);
            overlay = null;
            if (session.CompletedRun != null)
            {
                yield return BossSequence(session.CompletedRun);
                yield break;
            }
            if (session.Day == 4 && !session.StationVisited)
            {
                ShowInRunStation(0);
                yield break;
            }
            yield return AdvanceDay();
        }

        private IEnumerator AdvanceDay()
        {
            DayTransition transition = session.Advance();
            detailVisible = false;
            archive.active = session.Snapshot();
            Save();
            foreach (PendingEcho echo in transition.Echos)
            {
                if (echo.depth >= 2) yield return CascadeSequence(echo);
                else yield return EchoSequence(echo);
            }
            if (session.HasPredictionReview) ShowPredictionReview();
            else BuildBoard();
        }

        private void ShowInRunStation(int stage)
        {
            stationStage = stage;
            Clear();
            View.Fill(root, "Station veil", new Color(0.005f, 0.02f, 0.035f, 0.45f), 0, 0, 1, 1);
            Image touch = View.Fill(root, "Walk forward", new Color(0, 0, 0, 0), 0, 0, 1, 1, true);
            StationSwipe swipe = touch.gameObject.AddComponent<StationSwipe>();
            swipe.ReadyAt = Time.unscaledTime + 1.1f;
            swipe.Advanced = () =>
            {
                Handheld.Vibrate();
                if (stationStage < 2) ShowInRunStation(stationStage + 1);
                else FinishStation();
            };
            View.Label(root, "Station title", "F U T U R E   S T A T I O N", 35, Palette.Mint,
                TextAnchor.MiddleCenter, 0.05f, 0.87f, 0.95f, 0.95f);
            View.Label(root, "Station day", "DAY 04  /  在地平线的另一边", 26, Palette.Muted,
                TextAnchor.MiddleCenter, 0.08f, 0.78f, 0.92f, 0.85f);
            bool reveal = session.RunNumber == 3 && stage == 2;
            View.Panel(root, "Distant glow", new Color(0.43f, 0.84f, 0.79f, reveal ? 0.24f : 0.09f),
                0.37f, 0.51f, 0.63f, 0.72f, 90);
            View.Panel(root, "Future silhouette", new Color(0.04f, 0.11f, 0.16f, 0.96f),
                0.42f, 0.52f, 0.58f, 0.65f, 50);
            View.Panel(root, "Future face", reveal ? Palette.Mint : new Color(0.11f, 0.24f, 0.26f, 0.92f),
                0.466f, 0.62f, 0.534f, 0.674f, 35);
            View.Panel(root, "Station bench", new Color(0.24f, 0.43f, 0.45f, 0.55f),
                0.27f, 0.505f, 0.73f, 0.516f, 8);
            string voice = stage == 0 ? "「你终于来了。」" :
                stage == 1 ? "「你最近留下了很多东西。」" :
                reveal ? "「现在你终于看见我了。」" : "「它们还会继续生长。」";
            View.Label(root, "Future voice", voice, 42, Palette.Text,
                TextAnchor.MiddleCenter, 0.07f, 0.39f, 0.93f, 0.49f);
            if (stage == 1)
            {
                int shown = 0;
                for (int i = session.Actions.Count - 1; i >= 0 && shown < 3; i--)
                {
                    ActionRecord action = session.Actions[i];
                    float y = 0.3f - shown * 0.07f;
                    View.Panel(root, "Memory", new Color(0.08f, 0.18f, 0.2f, 0.65f),
                        0.15f, y, 0.85f, y + 0.057f, 18);
                    View.Label(root, "Memory line", "D" + action.day + "  /  " + action.cardName,
                        27, action.kind == CardKind.Temptation ? Palette.Coral : Palette.Mint,
                        TextAnchor.MiddleCenter, 0.18f, y + 0.004f, 0.82f, y + 0.053f);
                    shown++;
                }
            }
            else if (stage == 2)
            {
                if (reveal)
                {
                    View.Label(root, "Revelation", "那个人，就是未来的你。", 32, Palette.Text,
                        TextAnchor.MiddleCenter, 0.08f, 0.27f, 0.92f, 0.34f);
                    View.Label(root, "Horizon unlock", "HORIZON III  /  两条可能未来", 32, Palette.Mint,
                        TextAnchor.MiddleCenter, 0.08f, 0.17f, 0.92f, 0.24f);
                }
                else
                {
                    ActionRecord cause = session.Actions.FindLast(a => a.echoDay > a.day);
                    if (cause != null)
                    {
                        View.Label(root, "Cause", "DAY " + cause.day + "  /  " + cause.cardName, 31,
                            Palette.Mint, TextAnchor.MiddleCenter, 0.08f, 0.29f, 0.92f, 0.35f);
                        View.Fill(root, "Cause line", new Color(0.54f, 0.97f, 0.79f, 0.72f),
                            0.49f, 0.23f, 0.51f, 0.285f);
                        View.Label(root, "Possible echo", "DAY " + cause.echoDay + "  /  " + cause.echoName,
                            31, Palette.Gold, TextAnchor.MiddleCenter, 0.08f, 0.165f, 0.92f, 0.23f);
                    }
                }
            }
            View.Label(root, "Walk hint", stage == 2 ? "向前滑动，回到现在" : "向前滑动，靠近未来的自己",
                26, Palette.Muted, TextAnchor.MiddleCenter, 0.1f, 0.045f, 0.9f, 0.11f);
        }

        private void FinishStation()
        {
            session.VisitStation();
            archive.active = session.Snapshot();
            Save();
            StartCoroutine(AdvanceDay());
        }

        private IEnumerator EchoSequence(PendingEcho echo)
        {
            Clear();
            float total = archive.seenFirstEcho ? 1.4f : 2.9f;
            View.Fill(root, "Echo shade", new Color(0.014f, 0.045f, 0.068f, 0.9f), 0, 0, 1, 1);
            View.Fill(root, "Left aberration", new Color(0.95f, 0.36f, 0.38f, 0.13f),
                0, 0.16f, 0.012f, 0.84f);
            View.Fill(root, "Right aberration", new Color(0.24f, 0.89f, 0.93f, 0.17f),
                0.988f, 0.16f, 1, 0.84f);
            View.Label(root, "Echo title", "T I M E   E C H O", 50, Palette.Mint,
                TextAnchor.MiddleCenter, 0.06f, 0.71f, 0.94f, 0.78f);
            View.Label(root, "Echo subtitle", "有些选择，现在才抵达。", 29, Palette.Muted,
                TextAnchor.MiddleCenter, 0.1f, 0.65f, 0.9f, 0.7f);
            View.Fill(root, "Causal line", new Color(0.58f, 0.93f, 0.81f, 0.44f),
                0.12f, 0.541f, 0.88f, 0.543f);
            for (int day = echo.sourceDay; day <= echo.dueDay; day++)
            {
                float x = EchoX(day, echo.sourceDay, echo.dueDay);
                View.Panel(root, "Echo track", new Color(0.49f, 0.81f, 0.76f, 0.48f),
                    x - 0.008f, 0.537f, x + 0.008f, 0.547f, 10);
            }
            RectTransform tracer = View.Panel(root, "Rewinding light", Palette.Mint,
                0.857f, 0.529f, 0.903f, 0.555f, 24).rectTransform;
            Text dayLabel = View.Label(root, "Rewinding day", "DAY " + echo.dueDay, 34, Palette.Text,
                TextAnchor.MiddleCenter, 0.16f, 0.57f, 0.84f, 0.635f);
            yield return new WaitForSeconds(total * 0.12f);
            for (int day = echo.dueDay; day >= echo.sourceDay; day--)
            {
                float x = EchoX(day, echo.sourceDay, echo.dueDay);
                tracer.anchorMin = new Vector2(x - 0.023f, 0.529f);
                tracer.anchorMax = new Vector2(x + 0.023f, 0.555f);
                dayLabel.text = "DAY " + day;
                yield return new WaitForSeconds(total * 0.29f /
                    (echo.dueDay - echo.sourceDay + 1));
            }
            View.Panel(root, "Origin halo", new Color(0.57f, 0.96f, 0.78f, 0.18f),
                0.07f, 0.507f, 0.17f, 0.569f, 35);
            View.Label(root, "Past action", "「" + echo.cardName + "」", 43, Palette.Text,
                TextAnchor.MiddleCenter, 0.08f, 0.38f, 0.92f, 0.49f);
            View.Label(root, "How long ago", (echo.dueDay - echo.sourceDay) + "天前，种下的因。", 27,
                Palette.Muted, TextAnchor.MiddleCenter, 0.1f, 0.33f, 0.9f, 0.395f);
            yield return new WaitForSeconds(total * 0.25f);
            View.Fill(root, "Consequence flash", new Color(0.39f, 0.99f, 0.83f, 0.11f), 0, 0, 1, 1);
            View.Label(root, "Consequence", echo.echoName + "   " + echo.delta.ShortLabel(), 42,
                echo.kind == CardKind.Temptation ? Palette.Coral : Palette.Mint,
                TextAnchor.MiddleCenter, 0.06f, 0.18f, 0.94f, 0.31f);
            Handheld.Vibrate();
            yield return new WaitForSeconds(total * 0.34f);
            archive.seenFirstEcho = true;
            Save();
        }

        private IEnumerator CascadeSequence(PendingEcho echo)
        {
            Clear();
            View.Fill(root, "Chain darkness", new Color(0.009f, 0.026f, 0.045f, 0.96f),
                0, 0, 1, 1);
            View.Label(root, "Chain title", "C H A I N   F O U N D", 45, Palette.Gold,
                TextAnchor.MiddleCenter, 0.05f, 0.76f, 0.95f, 0.85f);
            View.Label(root, "Chain clue", "后果，又改变了一个选择。", 30, Palette.Muted,
                TextAnchor.MiddleCenter, 0.08f, 0.68f, 0.92f, 0.75f);
            View.Fill(root, "Causal rail", new Color(0.91f, 0.63f, 0.42f, 0.25f),
                0.17f, 0.521f, 0.83f, 0.523f);
            yield return new WaitForSeconds(0.18f);
            View.Panel(root, "Origin pulse", Palette.Coral, 0.145f, 0.502f, 0.195f, 0.542f, 29);
            View.Label(root, "Origin day", "DAY " + echo.sourceDay, 26, Palette.Coral,
                TextAnchor.MiddleCenter, 0.045f, 0.565f, 0.295f, 0.615f);
            View.Label(root, "Origin action", echo.cardName, 27, Palette.Text,
                TextAnchor.MiddleCenter, 0.035f, 0.44f, 0.305f, 0.5f);
            yield return new WaitForSeconds(0.25f);
            View.Fill(root, "First link", Palette.Coral, 0.19f, 0.518f, 0.5f, 0.526f);
            View.Panel(root, "First echo pulse", Palette.Coral, 0.475f, 0.502f, 0.525f, 0.542f, 29);
            View.Label(root, "First echo day", "DAY " + echo.parentDay, 26, Palette.Coral,
                TextAnchor.MiddleCenter, 0.37f, 0.565f, 0.63f, 0.615f);
            View.Label(root, "First echo", "精力见底", 27, Palette.Text,
                TextAnchor.MiddleCenter, 0.35f, 0.44f, 0.65f, 0.5f);
            yield return new WaitForSeconds(0.18f);
            View.Fill(root, "Second link", Palette.Gold, 0.5f, 0.518f, 0.81f, 0.526f);
            View.Panel(root, "Second echo pulse", Palette.Gold, 0.805f, 0.502f, 0.855f, 0.542f, 29);
            View.Label(root, "Second echo day", "DAY " + echo.dueDay, 26, Palette.Gold,
                TextAnchor.MiddleCenter, 0.695f, 0.565f, 0.955f, 0.615f);
            View.Label(root, "Second echo", "错过邀约", 27, Palette.Text,
                TextAnchor.MiddleCenter, 0.685f, 0.44f, 0.965f, 0.5f);
            yield return new WaitForSeconds(0.13f);
            View.Fill(root, "Chain flash", new Color(1f, 0.69f, 0.4f, 0.1f), 0, 0, 1, 1);
            View.Label(root, "Cascade", "C A S C A D E  × 3", 51, Palette.Gold,
                TextAnchor.MiddleCenter, 0.06f, 0.28f, 0.94f, 0.37f);
            View.Label(root, "Changed hand", "邀约没有到来。今天你仍可以选择独处休息。", 27,
                Palette.Text, TextAnchor.MiddleCenter, 0.07f, 0.2f, 0.93f, 0.28f);
            Handheld.Vibrate();
            yield return new WaitForSeconds(0.66f);
        }

        private static float EchoX(int day, int source, int due)
        {
            return Mathf.Lerp(0.12f, 0.88f, (day - source) / (float)Mathf.Max(1, due - source));
        }

        private void ShowFocus()
        {
            if (busy || session == null || !session.TryFocus()) return;
            archive.active = session.Snapshot();
            Save();
            RenderFocus(false);
        }

        private void RenderFocus(bool compare)
        {
            if (overlay != null) Destroy(overlay.gameObject);
            overlay = View.Rect(root, "FOCUS MODE", 0, 0, 1, 1);
            View.Fill(overlay, "Veil", new Color(0.015f, 0.05f, 0.08f, 0.97f), 0, 0, 1, 1, true);
            View.Label(overlay, "Title", compare ? "T W O   F U T U R E S" : "F O C U S   M O D E",
                44, Palette.Mint,
                TextAnchor.MiddleCenter, 0.05f, 0.78f, 0.95f, 0.89f);
            View.Label(overlay, "Subtitle", compare ? "同一个今天，可以走向不同方向。" : "你在未来留下的微光",
                32, Palette.Text,
                TextAnchor.MiddleCenter, 0.1f, 0.69f, 0.9f, 0.77f);
            if (compare)
            {
                CardSpec[] hand = session.Hand;
                CardSpec first = session.CanPlay(hand[0]) ? hand[0] : hand[2];
                CardSpec second = session.CanPlay(hand[1]) ? hand[1] :
                    first.Id == hand[2].Id ? hand[0] : hand[2];
                DrawFutureBranch(first, 0.5f, "可能 A");
                DrawFutureBranch(second, 0.29f, "可能 B");
                View.Label(overlay, "Conditional", "只计算已埋下的回声；之后的行动和连锁仍会改写未来。",
                    24, Palette.Muted, TextAnchor.MiddleCenter, 0.07f, 0.205f, 0.93f, 0.26f);
                View.Button(overlay, "Return to echoes", "查看已埋下的回声", () => RenderFocus(false),
                    0.16f, 0.125f, 0.84f, 0.19f, Palette.Panel, Palette.Text, 25);
            }
            else
            {
                List<PendingEcho> echoes = new List<PendingEcho>(session.Pending);
                echoes.Sort((a, b) => a.dueDay.CompareTo(b.dueDay));
                int limit = session.HorizonLevel >= 3 ? 3 : 5;
                for (int i = 0; i < Mathf.Min(limit, echoes.Count); i++)
                {
                    PendingEcho echo = echoes[i];
                    float y = 0.61f - i * 0.102f;
                    View.Panel(overlay, "Future event", Palette.Panel, 0.11f, y, 0.89f, y + 0.082f);
                    string detail = session.HorizonLevel >= 3 ? echo.echoName :
                        session.HorizonLevel >= 2 ? (echo.kind == CardKind.Temptation ? "火种" :
                            echo.kind == CardKind.Growth ? "芽" : "回应") :
                        archive.calibrations > 0 ?
                            (echo.kind == CardKind.Temptation ? "状态可能下降" :
                                echo.kind == CardKind.Growth ? "洞察可能上升" : "有人可能回应") :
                        echo.kind == CardKind.Temptation ? "一处微弱的火种" : "一颗尚未发芽的种子";
                    View.Label(overlay, "Forecast", "DAY " + echo.dueDay + "     " +
                        (echo.depth >= 2 ? "连锁 · " : "") + detail, 29,
                        echo.kind == CardKind.Temptation ? Palette.Coral : Palette.Mint,
                        TextAnchor.MiddleLeft, 0.16f, y + 0.01f, 0.84f, y + 0.072f);
                }
                if (echoes.Count == 0)
                    View.Label(overlay, "Empty", "这里还没有被埋下的回声。", 31, Palette.Muted,
                        TextAnchor.MiddleCenter, 0.12f, 0.49f, 0.88f, 0.6f);
                if (session.HorizonLevel >= 3)
                    View.Button(overlay, "Two futures", "查看两条可能未来", () => RenderFocus(true),
                        0.16f, 0.14f, 0.84f, 0.205f, Palette.Panel, Palette.Mint, 27);
                else
                    View.Label(overlay, "Boss", "距截止日还有 " + (GameSession.LastDay - session.Day) + " 天",
                        30, Palette.Muted, TextAnchor.MiddleCenter, 0.13f, 0.14f, 0.87f, 0.21f);
            }
            View.Button(overlay, "Close", "回到现在", () => BuildBoard(),
                0.19f, 0.055f, 0.81f, 0.12f, Palette.Mint, Palette.Ink);
        }

        private void DrawFutureBranch(CardSpec card, float y, string title)
        {
            FutureProjection future = session.ProjectFuture(card.Id);
            RectTransform panel = View.Rect(overlay, title, 0.11f, y, 0.89f, y + 0.17f);
            View.Panel(panel, "Path", Palette.Panel, 0, 0, 1, 1, 22);
            View.Label(panel, "Action", title + "  /  " + card.Name, 31,
                future.Available ? Palette.Mint : Palette.Muted,
                TextAnchor.MiddleLeft, 0.07f, 0.66f, 0.94f, 0.94f);
            View.Label(panel, "Path detail", future.Available ?
                "DAY " + future.TargetDay + "  ·  精力" + Tendency(future.Energy - session.Energy) +
                "  心情" + Tendency(future.Mood - session.Mood) +
                "  洞察" + Tendency(future.Insight - session.Insight) : "此刻资源不足，这条路暂时走不通。",
                27, Palette.Text, TextAnchor.MiddleLeft, 0.07f, 0.32f, 0.94f, 0.67f);
            View.Label(panel, "Possible echo", future.Available && future.EchoDay > GameSession.LastDay ?
                "截止日之后 · 本局不会兑现" : future.Available && future.EchoDay > 0 ?
                "D" + future.EchoDay + " · " + card.FutureHint : "未来仍有未写下的部分",
                23, Palette.Muted, TextAnchor.MiddleLeft, 0.07f, 0.08f, 0.94f, 0.32f);
        }

        private static string Tendency(int delta) { return delta > 0 ? "↑" : delta < 0 ? "↓" : "→"; }

        private IEnumerator BossSequence(RunRecord run)
        {
            Clear();
            View.Label(root, "Deadline", "截止日到了。", 53, Palette.Text,
                TextAnchor.MiddleCenter, 0.08f, 0.86f, 0.92f, 0.93f);
            View.Label(root, "Whole timeline", "你过去的 12 天，正一起回答。", 29, Palette.Muted,
                TextAnchor.MiddleCenter, 0.1f, 0.79f, 0.9f, 0.85f);
            View.Fill(root, "Star map", new Color(0.43f, 0.79f, 0.74f, 0.45f),
                0.12f, 0.73f, 0.88f, 0.731f);
            for (int i = 0; i < run.actions.Count; i++)
            {
                float x = 0.12f + i * 0.068f;
                View.Panel(root, "Action star", new Color(0.44f, 0.62f, 0.64f, 0.52f),
                    x, 0.721f, x + 0.018f, 0.739f, 10);
            }
            bool[] gates = { run.boss.ability, run.boss.state, run.boss.support };
            string[] names = { "能力", "状态", "支援" };
            string[] notes = { "成长留下的能力", "休息、透支与恢复的轨迹", "主动建立的连接" };
            List<int>[] evidence = { run.boss.abilityDays, run.boss.stateDays, run.boss.supportDays };
            for (int i = 0; i < 3; i++)
            {
                List<int> days = evidence[i] ?? new List<int>();
                Color beam = i == 0 ? Palette.Mint : i == 1 ? Palette.Gold : Palette.Text;
                foreach (int day in days)
                {
                    if (day < 1 || day > GameSession.LastDay) continue;
                    float x = 0.12f + (day - 1) * 0.068f;
                    View.Panel(root, "Gate evidence", beam,
                        x - 0.006f, 0.715f, x + 0.024f, 0.745f, 18);
                    yield return new WaitForSeconds(0.045f);
                }
                float y = 0.58f - i * 0.145f;
                View.Panel(root, "Gate", Palette.Panel, 0.1f, y, 0.9f, y + 0.118f);
                View.Label(root, "Gate sign", gates[i] ? "已开" : "未开", 30,
                    gates[i] ? Palette.Mint : Palette.Muted, TextAnchor.MiddleCenter,
                    0.14f, y + 0.02f, 0.25f, y + 0.098f);
                View.Label(root, "Gate name", names[i], 36, Palette.Text,
                    TextAnchor.MiddleLeft, 0.29f, y + 0.047f, 0.52f, y + 0.105f);
                View.Label(root, "Gate note", notes[i] + "  ·  " + EvidenceSummary(days), 22, Palette.Muted,
                    TextAnchor.MiddleLeft, 0.29f, y + 0.011f, 0.85f, y + 0.055f);
                yield return new WaitForSeconds(0.35f);
            }
            View.Label(root, "Future voice", run.boss.passed == 3 ? "「这条路，是你亲手照亮的。」" :
                "「如果这里不同，会发生什么？」", 34, Palette.Mint,
                TextAnchor.MiddleCenter, 0.08f, 0.13f, 0.92f, 0.21f);
            yield return new WaitForSeconds(0.8f);
            if (run.boss.passed < 3)
                yield return GhostSequence(run.boss);
            else
                yield return new WaitForSeconds(0.9f);
            View.Fill(root, "Collapse", Palette.Ink, 0, 0, 1, 1);
            View.Label(root, "New horizon", "另一条时间线，正在形成。", 35, Palette.Mint,
                TextAnchor.MiddleCenter, 0.08f, 0.43f, 0.92f, 0.57f);
            yield return new WaitForSeconds(1.7f);
            StartNewRun();
        }

        private static string EvidenceSummary(List<int> days)
        {
            if (days == null || days.Count == 0) return "没有对应的行动";
            string label = "";
            for (int i = 0; i < Mathf.Min(3, days.Count); i++)
                label += (i == 0 ? "" : " · ") + "D" + days[i];
            return days.Count > 3 ? label + " 等" : label;
        }

        private IEnumerator GhostSequence(BossResult boss)
        {
            GhostTimeline ghost = boss.ghostTimeline;
            View.Fill(root, "Ghost sky", new Color(0.025f, 0.048f, 0.078f, 0.98f),
                0, 0, 1, 1);
            View.Label(root, "Ghost title", "G H O S T   T I M E L I N E", 43,
                Palette.Muted, TextAnchor.MiddleCenter, 0.04f, 0.82f, 0.96f, 0.91f);
            if (ghost == null)
            {
                View.Label(root, "No single switch", boss.ghost, 34, Palette.Text,
                    TextAnchor.MiddleCenter, 0.09f, 0.39f, 0.91f, 0.59f);
                yield return new WaitForSeconds(1.8f);
                yield break;
            }
            View.Label(root, "Pointed node", "「如果这里不同，会发生什么？」", 32,
                Palette.Text, TextAnchor.MiddleCenter, 0.08f, 0.69f, 0.92f, 0.77f);
            View.Fill(root, "Phantom rail", new Color(0.87f, 0.78f, 0.97f, 0.35f),
                0.17f, 0.52f, 0.83f, 0.523f);
            View.Panel(root, "Changed origin", Palette.Gold, 0.143f, 0.499f, 0.193f, 0.543f, 25);
            View.Label(root, "Original", "DAY " + ghost.sourceDay + "  /  原来是「" + ghost.originalName + "」",
                26, Palette.Muted, TextAnchor.MiddleCenter, 0.05f, 0.57f, 0.95f, 0.63f);
            View.Label(root, "Alternative", "如果改为「" + ghost.alternativeName + "」",
                31, Palette.Gold, TextAnchor.MiddleCenter, 0.06f, 0.395f, 0.42f, 0.5f);
            yield return new WaitForSeconds(0.32f);
            View.Fill(root, "Ghost first link", Palette.Gold, 0.19f, 0.518f, 0.5f, 0.527f);
            View.Panel(root, "Changed echo", Palette.Mint, 0.475f, 0.499f, 0.525f, 0.543f, 25);
            string middle = ghost.echoDay > 0 ?
                "D" + ghost.echoDay + "  " + ghost.echoName : ghost.changedChoiceDay > 0 ?
                "D" + ghost.changedChoiceDay + "  " + ghost.changedChoiceName : "后续的资源轨迹改变";
            View.Label(root, "Consequential node", middle, 27, Palette.Text,
                TextAnchor.MiddleCenter, 0.35f, 0.395f, 0.65f, 0.5f);
            yield return new WaitForSeconds(0.25f);
            View.Fill(root, "Ghost second link", Palette.Mint, 0.5f, 0.518f, 0.81f, 0.527f);
            View.Panel(root, "Ghost gate", ghost.gateOpens ? Palette.Mint : Palette.Coral,
                0.805f, 0.499f, 0.855f, 0.543f, 25);
            View.Label(root, "Gate consequence", "D12  " + ghost.gateName +
                (ghost.gateOpens ? "门打开" : "门仍未开"), 27,
                ghost.gateOpens ? Palette.Mint : Palette.Coral,
                TextAnchor.MiddleCenter, 0.69f, 0.395f, 0.97f, 0.5f);
            View.Label(root, "Honest branch", boss.ghost + "  （" + ghost.beforePassed +
                " → " + ghost.afterPassed + " 门）", 30, Palette.Text,
                TextAnchor.MiddleCenter, 0.07f, 0.23f, 0.93f, 0.33f);
            Handheld.Vibrate();
            yield return new WaitForSeconds(1.7f);
        }

        private void ShowMap(bool duringRun)
        {
            if (busy) return;
            mapIndex = Mathf.Max(0, archive.runs.Count - 1);
            RenderMap(duringRun);
        }

        private void RenderMap(bool duringRun)
        {
            if (overlay != null) Destroy(overlay.gameObject);
            overlay = View.Rect(root, "Time map", 0, 0, 1, 1);
            View.Fill(overlay, "Map background", new Color(0.025f, 0.055f, 0.085f, 0.99f), 0, 0, 1, 1, true);
            View.Label(overlay, "Map title", "时 间 地 图", 48, Palette.Text,
                TextAnchor.MiddleCenter, 0.08f, 0.9f, 0.92f, 0.97f);
            RunRecord run = archive.runs.Count > 0 ? archive.runs[mapIndex] : null;
            List<ActionRecord> actions = duringRun && session != null ? session.Actions :
                run != null ? run.actions : new List<ActionRecord>();
            string title = duringRun && session != null ? "RUN " + session.RunNumber.ToString("000") + "  ·  正在发生" :
                run != null ? "RUN " + run.number.ToString("000") + "  ·  " + run.title : "还没有走过的时间线";
            View.Label(overlay, "Run title", title, 31, Palette.Mint,
                TextAnchor.MiddleCenter, 0.07f, 0.835f, 0.93f, 0.895f);
            View.Fill(overlay, "Map spine", new Color(0.46f, 0.76f, 0.72f, 0.36f),
                0.19f, 0.1f, 0.192f, 0.814f);
            for (int i = 0; i < actions.Count; i++)
            {
                ActionRecord action = actions[i];
                if (action.echoDay <= action.day || action.echoDay > GameSession.LastDay) continue;
                float from = 0.788f - (action.day - 1) * 0.055f + 0.007f;
                float to = 0.788f - (action.echoDay - 1) * 0.055f + 0.007f;
                float x = 0.79f + (i % 4) * 0.029f;
                Color baseColor = action.kind == CardKind.Temptation ? Palette.Coral : Palette.Mint;
                Color thread = new Color(baseColor.r, baseColor.g, baseColor.b,
                    action.echoed ? 0.66f : 0.32f);
                View.Fill(overlay, "Causal thread", thread, x, to, x + 0.002f, from);
                View.Fill(overlay, "Causal start", thread, 0.73f, from, x, from + 0.002f);
                View.Panel(overlay, "Causal arrival", thread,
                    x - 0.007f, to - 0.005f, x + 0.011f, to + 0.006f, 9);
                if (action.secondaryDay > action.echoDay)
                {
                    float second = 0.788f - (action.secondaryDay - 1) * 0.055f + 0.007f;
                    Color next = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b,
                        action.secondaryResolved ? 0.85f : 0.38f);
                    View.Fill(overlay, "Second-order thread", next,
                        x + 0.013f, second, x + 0.016f, to);
                    View.Fill(overlay, "Chain turn", next,
                        x, to, x + 0.016f, to + 0.003f);
                    View.Panel(overlay, "Lost possibility", next,
                        x + 0.006f, second - 0.005f, x + 0.023f, second + 0.007f, 9);
                }
            }
            for (int day = 1; day <= GameSession.LastDay; day++)
            {
                float y = 0.788f - (day - 1) * 0.055f;
                ActionRecord action = actions.Find(a => a.day == day);
                Color color = action == null ? Palette.Muted :
                    action.kind == CardKind.Growth ? Palette.Mint :
                    action.kind == CardKind.Temptation ? Palette.Coral : Palette.Gold;
                View.Panel(overlay, "Node", color, 0.178f, y, 0.205f, y + 0.015f, 14);
                View.Label(overlay, "Day", day.ToString("00"), 23, Palette.Muted,
                    TextAnchor.MiddleRight, 0.07f, y - 0.01f, 0.16f, y + 0.029f);
                string label = action == null ? "尚未到来" : action.cardName;
                View.Label(overlay, "Action", label, 27, action == null ? Palette.Muted : Palette.Text,
                    TextAnchor.MiddleLeft, 0.25f, y + 0.002f, 0.73f, y + 0.039f);
                if (action != null)
                {
                    string immediate = action.now == null ? "" : "当下 " + action.now.ShortLabel();
                    string future = action.echoDay > 0 ? "D" + action.echoDay + " " +
                        (action.later == null ? action.echoName : action.later.ShortLabel()) : "";
                    if (action.secondaryDay > 0)
                        future += "  →  D" + action.secondaryDay + " 邀约缺席";
                    View.Label(overlay, "Why", immediate + (future.Length > 0 ? "   → " + future : ""),
                        19, action.echoed ? Palette.Mint : Palette.Muted,
                        TextAnchor.MiddleLeft, 0.25f, y - 0.017f, 0.73f, y + 0.009f);
                }
            }
            PredictionRecord prediction = duringRun && session != null ? session.Prediction :
                run != null ? run.prediction : null;
            if (prediction != null)
                View.Label(overlay, "Prediction mark", prediction.evaluated ?
                    "D4 的预测  →  D7 的自己  /  " + (prediction.accurate ? "接近" : "出乎意料") :
                    "D4 的预测，等待 D7 回答", 23, Palette.Gold,
                    TextAnchor.MiddleCenter, 0.1f, 0.123f, 0.9f, 0.167f);
            if (!duringRun && archive.runs.Count > 1)
            {
                View.Button(overlay, "Previous", "‹", () => { mapIndex = (mapIndex + archive.runs.Count - 1) % archive.runs.Count; RenderMap(false); },
                    0.06f, 0.08f, 0.17f, 0.13f, Palette.Panel, Palette.Text);
                View.Button(overlay, "Next", "›", () => { mapIndex = (mapIndex + 1) % archive.runs.Count; RenderMap(false); },
                    0.83f, 0.08f, 0.94f, 0.13f, Palette.Panel, Palette.Text);
            }
            View.Button(overlay, "Close map", "返回", () => { Destroy(overlay.gameObject); overlay = null; },
                0.32f, 0.044f, 0.68f, 0.105f, Palette.Mint, Palette.Ink);
        }

        private void ShowEchoArchive()
        {
            if (archive.runs.Count == 0) { ShowMap(false); return; }
            Clear();
            View.Label(root, "Archive", "回 声 档 案", 49, Palette.Text,
                TextAnchor.MiddleCenter, 0.08f, 0.86f, 0.92f, 0.94f);
            RunRecord run = archive.runs[archive.runs.Count - 1];
            int shown = 0;
            for (int i = run.actions.Count - 1; i >= 0 && shown < 7; i--)
            {
                ActionRecord action = run.actions[i];
                if (action.echoDay == 0 || !action.echoed) continue;
                float y = 0.74f - shown * 0.094f;
                View.Panel(root, "Echo", Palette.Panel, 0.1f, y, 0.9f, y + 0.076f);
                View.Label(root, "Echo title", "D" + action.day + "  " + action.cardName +
                    "   →   D" + action.echoDay + "  " + action.echoName,
                    27, Palette.Mint, TextAnchor.MiddleLeft, 0.14f, y + 0.012f, 0.86f, y + 0.067f);
                shown++;
            }
            if (shown == 0) View.Label(root, "Empty", "还没有兑现的回声。", 35, Palette.Muted,
                TextAnchor.MiddleCenter, 0.1f, 0.44f, 0.9f, 0.56f);
            View.Button(root, "Home", "返回", ShowHome, 0.25f, 0.065f, 0.75f, 0.13f,
                Palette.Mint, Palette.Ink);
        }

        private void ShowStation()
        {
            Clear();
            View.Label(root, "Station title", "F U T U R E   S T A T I O N", 35, Palette.Mint,
                TextAnchor.MiddleCenter, 0.06f, 0.83f, 0.94f, 0.91f);
            string voice = archive.runs.Count > 0 && archive.runs[archive.runs.Count - 1].boss.passed == 3 ?
                "「你最近留下了很多光。」" : "「你终于来了。」";
            View.Label(root, "Voice", voice, 42, Palette.Text, TextAnchor.MiddleCenter,
                0.1f, 0.22f, 0.9f, 0.32f);
            View.Button(root, "Home", "回到地平线", ShowHome, 0.19f, 0.065f, 0.81f, 0.13f,
                Palette.Mint, Palette.Ink);
        }
    }
}
