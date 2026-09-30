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
        public string preferredIntent;
        public RewardWallet wallet = new RewardWallet();
        public FeedbackRecord pendingFeedback;
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
        private DropRingGraphic dropRing;
        private Text dropTitle;
        private Text boardHint;
        private RectTransform handGuide;
        private HorizonWorld3D world;
        private readonly Dictionary<HorizonCardDrag, CardSpec> cards = new Dictionary<HorizonCardDrag, CardSpec>();
        private bool busy;
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
            world = new GameObject("HORIZON 3D diorama").AddComponent<HorizonWorld3D>();
            world.Initialize();
            world.SetTheme(archive.wallet.theme);
            DontDestroyOnLoad(world.gameObject);

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
            if (archive.pendingFeedback != null) { ContinueRun(); return; }
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
            Font bundled = Resources.Load<Font>("HorizonSansSC");
            if (bundled != null) return bundled;
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
                    if (data.wallet == null) data.wallet = new RewardWallet();
                    data.wallet.Repair();
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
            dropRing = null;
            dropTitle = null;
            boardHint = null;
            handGuide = null;
            busy = false;
            Backdrop();
        }

        private void Backdrop()
        {
            View.Fill(root, "Readable header", Palette.Ink, 0, 0.91f, 1, 1);
            View.Fill(root, "Readable actions", new Color(0.014f, 0.035f, 0.06f, 0.98f), 0, 0, 1, 0.445f);
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
            archive.pendingFeedback = null;
            session = new GameSession(archive.runs.Count + 1);
            archive.active = session.Snapshot();
            Save();
            BuildBoard();
        }

        private void ContinueRun()
        {
            if (archive.pendingFeedback != null && archive.pendingFeedback.kind == FeedbackKind.Deadline)
            {
                ShowDeadlineResult(archive.runs[archive.runs.Count - 1]);
                return;
            }
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
            if (archive.pendingFeedback != null) { ShowFeedback(); return; }
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
            world.ShowBoard();
            WalletButton(root);
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
            if (archive.pendingFeedback != null) { ShowFeedback(); return; }
            if (session != null && session.CanPredict) { ShowPrediction(); return; }
            Clear();
            if (session == null) return;
            world.ShowBoard();
            FutureRegion();
            PresentRegion();
            ResourceRegion();
            TimelineRegion();
            HandRegion();
        }

        private void WalletButton(Transform parent)
        {
            View.Button(parent, "Stardust wallet", "星尘 " + archive.wallet.stardust, ShowThemes,
                0.69f, 0.95f, 0.945f, 0.988f, Palette.Panel, Palette.Gold, 25);
        }

        private void ShowThemes()
        {
            if (busy || overlay != null) return;
            overlay = View.Rect(root, "Scenery wardrobe", 0, 0, 1, 1);
            View.Fill(overlay, "Wardrobe shade", new Color(0.008f, 0.022f, 0.035f, 0.95f), 0, 0, 1, 1, true);
            View.Label(overlay, "Wardrobe title", "给你的未来换一种颜色", 43, Palette.Text,
                TextAnchor.MiddleCenter, 0.06f, 0.8f, 0.94f, 0.9f);
            Text balance = View.Label(overlay, "Balance", "星尘 " + archive.wallet.stardust, 35, Palette.Gold,
                TextAnchor.MiddleCenter, 0.1f, 0.71f, 0.9f, 0.78f);
            string[] names = { "夜航 · 蓝色城市", "黎明 · 琥珀天空", "极光 · 紫色梦境" };
            for (int i = 0; i < 3; i++)
            {
                int theme = i;
                float y = 0.58f - i * 0.135f;
                bool owned = archive.wallet.ownedThemes.Contains(i);
                string suffix = archive.wallet.theme == i ? "已使用" : owned ? "已拥有" : RewardWallet.ThemeCost(i) + " 星尘";
                View.Button(overlay, "Theme " + i, names[i] + "\n" + suffix, () =>
                {
                    if (!archive.wallet.SelectTheme(theme))
                    {
                        balance.text = "还需 " + (RewardWallet.ThemeCost(theme) - archive.wallet.stardust) + " 星尘";
                        return;
                    }
                    world.SetTheme(theme);
                    Save();
                    Destroy(overlay.gameObject);
                    overlay = null;
                    if (session != null && !session.HasChosen) BuildBoard(); else ShowHome();
                    ShowThemes();
                }, 0.1f, y, 0.9f, y + 0.105f, Palette.Panel, i == archive.wallet.theme ? Palette.Mint : Palette.Text, 31);
            }
            View.Label(overlay, "Cosmetic only", "行动、回声、因果链都会获得星尘。\n星尘用于场景外观，三种颜色遵循相同规则。", 27,
                Palette.Muted, TextAnchor.MiddleCenter, 0.09f, 0.155f, 0.91f, 0.265f);
            View.Button(overlay, "Close wardrobe", "返回", () => { Destroy(overlay.gameObject); overlay = null; },
                0.2f, 0.065f, 0.8f, 0.13f, Palette.Mint, Palette.Ink);
        }

        private void ShowGoal()
        {
            if (busy || overlay != null || session == null) return;
            overlay = View.Rect(root, "How to play", 0, 0, 1, 1);
            View.Fill(overlay, "Goal shade", new Color(0.01f, 0.028f, 0.048f, 0.97f), 0, 0, 1, 1, true);
            View.Label(overlay, "Goal title", "在第 12 天点亮三道门", 43, Palette.Text,
                TextAnchor.MiddleCenter, 0.06f, 0.84f, 0.94f, 0.94f);
            View.Label(overlay, "Core rule", "每天选一张牌 → 拖进光圈松手\n今天的变化立即发生，回声会在标记的日期回来。", 30,
                Palette.Muted, TextAnchor.MiddleCenter, 0.07f, 0.715f, 0.93f, 0.825f);
            string[] titles = { "能力门 · 留下成长", "状态门 · 照顾自己", "支援门 · 联系他人" };
            for (int i = 0; i < 3; i++)
            {
                float y = 0.545f - i * 0.15f;
                View.Panel(overlay, "Gate progress", Palette.Panel, 0.075f, y, 0.925f, y + 0.135f, 25);
                View.Label(overlay, "Gate name", titles[i], 32, Palette.Mint,
                    TextAnchor.MiddleLeft, 0.115f, y + 0.074f, 0.885f, y + 0.126f);
                View.Label(overlay, "Gate progress", PlayExperience.GateReason(session, i), 26, Palette.Text,
                    TextAnchor.MiddleLeft, 0.115f, y + 0.01f, 0.885f, y + 0.075f);
            }
            View.Label(overlay, "All states", "精力 " + session.Energy + "   心情 " + session.Mood + "   洞察 " + session.Insight +
                "\n关系 " + session.Relation + "   金钱 " + session.Money + "   能力 " + session.Ability + "    / 上限 10", 28,
                Palette.Gold, TextAnchor.MiddleCenter, 0.07f, 0.145f, 0.93f, 0.235f);
            View.Button(overlay, "Got it", "明白了，回到今天", () => { Destroy(overlay.gameObject); overlay = null; },
                0.13f, 0.055f, 0.87f, 0.125f, Palette.Mint, Palette.Ink, 30);
        }

        private void ShowFeedback()
        {
            FeedbackRecord feedback = archive.pendingFeedback;
            if (feedback == null) { BuildBoard(); return; }
            if (feedback.kind == FeedbackKind.Deadline)
            { ShowDeadlineResult(archive.runs[archive.runs.Count - 1]); return; }
            Clear();
            world.ShowBoard();
            View.Label(root, "Result day", "DAY " + feedback.day.ToString("00") + " / 12", 32,
                Palette.Text, TextAnchor.MiddleLeft, 0.06f, 0.95f, 0.55f, 0.985f);
            View.Label(root, "Wallet", "星尘 " + archive.wallet.stardust, 28, Palette.Gold,
                TextAnchor.MiddleRight, 0.61f, 0.95f, 0.94f, 0.985f);
            RectTransform panel = View.Panel(root, "Readable result", Palette.Panel,
                0.05f, 0.055f, 0.95f, 0.57f, 40).rectTransform;
            panel.gameObject.AddComponent<PanelEntrance>();
            View.Label(panel, "Result title", feedback.title, 40, Palette.Text,
                TextAnchor.MiddleLeft, 0.065f, 0.79f, 0.935f, 0.955f);
            View.Label(panel, "Result explanation", feedback.description, 30, Palette.Mint,
                TextAnchor.UpperLeft, 0.065f, 0.32f, 0.935f, 0.78f);
            View.Label(panel, "Reward", "+" + feedback.stardust + " 星尘 · 已收集", 34, Palette.Gold,
                TextAnchor.MiddleLeft, 0.065f, 0.225f, 0.935f, 0.315f);
            View.Label(panel, "Hold result", "看完再继续。你可以按自己的节奏玩。", 23, Palette.Muted,
                TextAnchor.MiddleCenter, 0.065f, 0.158f, 0.935f, 0.217f);
            string next = feedback.kind == FeedbackKind.Choice ?
                feedback.day == 4 && !session.StationVisited ? "走进未来站" : "前往第 " + (feedback.day + 1) + " 天" :
                session.CanPredict ? "试着预测三天后" : "回到今天，选一张牌";
            View.Button(panel, "Continue result", next, ContinueFeedback,
                0.065f, 0.035f, 0.935f, 0.153f, Palette.Mint, Palette.Ink, 31);
            if (feedback.stardust > 0)
            {
                world.Reward(feedback.stardust, feedback.kind == FeedbackKind.Echoes);
                StarBurst(feedback.stardust, new Vector2(0.5f, 0.21f));
            }
        }

        private void ContinueFeedback()
        {
            if (busy || archive.pendingFeedback == null) return;
            FeedbackKind kind = archive.pendingFeedback.kind;
            archive.pendingFeedback = null;
            Save();
            if (kind == FeedbackKind.Choice)
            {
                if (session.Day == 4 && !session.StationVisited) ShowInRunStation(0);
                else StartCoroutine(AdvanceDay());
            }
            else if (session.HasPredictionReview) ShowPredictionReview();
            else BuildBoard();
        }

        private void StarBurst(int amount, Vector2 start)
        {
            for (int i = 0; i < Mathf.Clamp(amount * 3, 6, 20); i++)
            {
                RectTransform coin = View.Panel(root, "Collected stardust", Palette.Gold,
                    start.x - 0.009f, start.y - 0.005f, start.x + 0.009f, start.y + 0.005f, 15).rectTransform;
                var flight = coin.gameObject.AddComponent<RewardFlight>();
                flight.StartPoint = start;
                flight.Index = i;
            }
        }

        private void PrepareDeadlineFeedback(RunRecord run, int actionStars)
        {
            int stars = actionStars + archive.wallet.Claim("run:" + run.number + ":deadline", 5 + run.boss.passed * 8);
            archive.pendingFeedback = new FeedbackRecord
            {
                kind = FeedbackKind.Deadline, runNumber = run.number, day = 12,
                title = "你点亮了 " + run.boss.passed + " / 3 道门", stardust = stars
            };
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
            View.Label(root, "Prediction note", "相比今天，三天后会上升还是下降？拖动三个滑块。", 28,
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
            }, 0.15f, 0.105f, 0.85f, 0.175f, Palette.Mint, Palette.Ink, 31);
            View.Button(root, "Skip prediction", "先玩下去，暂不预测", () =>
            {
                session.SkipPrediction();
                archive.active = session.Snapshot();
                Save();
                BuildBoard();
            }, 0.18f, 0.025f, 0.82f, 0.083f, Palette.Panel, Palette.Muted, 27);
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
                View.Label(root, "Understanding", ForecastKnowledge.UnlockAt(archive.calibrations + 1),
                    29, Palette.Mint,
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
            Image hit = View.Fill(root, "Hold future", new Color(0, 0, 0, 0), 0.01f, 0.81f, 0.99f, 0.925f, true);
            hit.gameObject.AddComponent<FutureHold>().Activated = ShowFocus;
            View.Label(root, "Day", string.Format("DAY {0:00} / 12", session.Day), 37, Palette.Text,
                TextAnchor.MiddleLeft, 0.055f, 0.949f, 0.5f, 0.988f);
            View.Label(root, "Vision", "HORIZON " + Roman(session.HorizonLevel), 23, Palette.Mint,
                TextAnchor.MiddleLeft, 0.055f, 0.92f, 0.46f, 0.949f);
            WalletButton(root);
            View.Button(root, "Goal", "目标 · 点亮三道门", ShowGoal,
                0.50f, 0.914f, 0.945f, 0.949f, Palette.Panel, Palette.Gold, 23);
            View.Label(root, "Future caption", archive.calibrations >= 10 ?
                "未来 · 看见二阶影响" : archive.calibrations >= 3 ?
                "未来 · 影响强度可见" : archive.calibrations > 0 ?
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
            View.Label(root, "Focus hint", session.RunNumber == 1 && session.Day < 3 ?
                "每天选一张牌，第 12 天迎接截止日" : session.FocusUses == 0 ? FingerHint : "今天已经凝视过未来",
                21, Palette.Muted, TextAnchor.MiddleCenter, 0.11f, 0.785f, 0.89f, 0.814f);
            View.Button(root, "Map", "时间地图", () => ShowMap(true), 0.745f, 0.877f,
                0.95f, 0.911f, new Color(0.09f, 0.20f, 0.23f, 0.8f), Palette.Text, 20);
        }

        private static string Roman(int value) { return value == 1 ? "I" : value == 2 ? "II" : "III"; }

        private void PresentRegion()
        {
            View.Label(root, "Scene line", session.SocialUnavailableToday ?
                "疲惫让邀约改变了，今天还有其他选择。" : "今天，你想做什么？", 30,
                Palette.Text, TextAnchor.MiddleCenter, 0.07f, 0.727f, 0.93f, 0.778f);
            destinationBeacon = View.Rect(root, "Play destination", 0.16f, 0.472f, 0.84f, 0.605f);
            View.Panel(destinationBeacon, "Drop interior", new Color(0.012f, 0.08f, 0.11f, 0.76f),
                0.02f, 0.07f, 0.98f, 0.93f, 75);
            dropRing = destinationBeacon.gameObject.AddComponent<DropRingGraphic>();
            dropRing.color = Palette.Mint;
            dropRing.raycastTarget = false;
            dropTitle = View.Label(destinationBeacon, "Drop title", "把卡牌拖进这个光圈", 31,
                Palette.Text, TextAnchor.MiddleCenter, 0.08f, 0.49f, 0.92f, 0.82f);
            dragHint = View.Label(destinationBeacon, "Destination", "在圈内松手即可使用", 24,
                Palette.Mint, TextAnchor.MiddleCenter, 0.06f, 0.13f, 0.94f, 0.48f);
            destinationBeacon.gameObject.AddComponent<GuidePulse>();
        }

        private void ResourceRegion()
        {
            int[] values = { session.Energy, session.Mood, session.Ability };
            string[] names = { "精力", "心情", "能力" };
            View.Label(root, "State heading", "此刻的你", 22, Palette.Muted, TextAnchor.MiddleLeft,
                0.06f, 0.416f, 0.5f, 0.443f);
            View.Button(root, "All resources", "全部状态", ShowGoal, 0.73f, 0.417f, 0.94f, 0.442f,
                Palette.Panel, Palette.Muted, 20);
            for (int row = 0; row < values.Length; row++)
            {
                float x0 = 0.065f + (row % 3) * 0.295f;
                float x1 = x0 + 0.28f;
                float y = 0.359f;
                View.Panel(root, "State glass", new Color(0.025f, 0.07f, 0.105f, 0.81f),
                    x0, y, x1, y + 0.052f, 15);
                View.Label(root, "Resource", names[row], 22, values[row] <= 2 ? Palette.Coral : Palette.Text,
                    TextAnchor.MiddleLeft, x0 + 0.025f, y + 0.021f, x1 - 0.02f, y + 0.048f);
                for (int pip = 0; pip < 10; pip++)
                {
                    float x = x0 + 0.025f + pip * 0.022f;
                    View.Panel(root, "State trace", pip < values[row] ?
                        (values[row] <= 2 ? Palette.Coral : Palette.Mint) : new Color(0.36f, 0.52f, 0.55f, 0.42f),
                        x, y + 0.01f, x + 0.015f, y + 0.016f, 4);
                }
                if (values[row] <= 2)
                    View.Label(root, "Value", values[row].ToString(), 20,
                        values[row] <= 2 ? Palette.Coral : Palette.Text, TextAnchor.MiddleCenter,
                        x1 - 0.065f, y + 0.021f, x1 - 0.02f, y + 0.048f);
            }
        }

        private void TimelineRegion()
        {
            View.Label(root, "Past", "12 天旅程", 19, Palette.Muted, TextAnchor.MiddleLeft,
                0.065f, 0.32f, 0.3f, 0.348f);
            View.Fill(root, "Timeline", new Color(0.48f, 0.76f, 0.74f, 0.35f),
                0.32f, 0.333f, 0.90f, 0.335f);
            for (int day = 1; day <= GameSession.LastDay; day++)
            {
                float x = 0.32f + (day - 1) * 0.052f;
                ActionRecord action = session.Actions.Find(a => a.day == day);
                bool due = session.Pending.Exists(e => e.dueDay == day);
                Color color = due ? Palette.Mint : action == null ? new Color(0.45f, 0.60f, 0.62f, 0.35f) :
                    action.kind == CardKind.Temptation ? Palette.Coral :
                    action.kind == CardKind.Growth ? Palette.Mint : Palette.Gold;
                if (due) View.Panel(root, "Echo glow", new Color(color.r, color.g, color.b, 0.18f),
                    x - 0.008f, 0.323f, x + 0.032f, 0.345f, 18);
                View.Panel(root, "Past node", day == session.Day ? Palette.Text : color,
                    x, 0.328f, x + 0.018f, 0.339f, 12);
            }
        }

        private void HandRegion()
        {
            boardHint = View.Label(root, "Drag hint", "拖进光圈松手 · 或点卡牌查看并使用", 23, Palette.Muted,
                TextAnchor.MiddleCenter, 0.05f, 0.025f, 0.95f, 0.063f);
            trail = View.Fill(root, "Causal light", new Color(0.53f, 1f, 0.83f, 0.76f),
                0.5f, 0.5f, 0.5f, 0.5f).rectTransform;
            trail.gameObject.SetActive(false);
            CardSpec[] hand = session.Hand;
            for (int i = 0; i < hand.Length; i++)
            {
                CardSpec card = hand[i];
                float x = 0.043f + i * 0.308f;
                RectTransform rect = View.Rect(root, card.Id, x, 0.08f, x + 0.298f, 0.305f);
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
                View.Label(rect, "Now", available ? PlayExperience.NowLabel(card.Now) : "资源不足 · 点开查看",
                    25, available ? Palette.Text : Palette.Coral,
                    TextAnchor.MiddleLeft, 0.105f, 0.278f, 0.91f, 0.43f);
                string futureLabel = card.Delay > 0 && session.Day + card.Delay > GameSession.LastDay ?
                    "D" + (session.Day + card.Delay) + " · 超过截止日" :
                    card.Delay == 0 ? "立即恢复" : "D" + (session.Day + card.Delay) + " · " + PlayExperience.FutureMeaning(card);
                View.Label(rect, "Future", futureLabel, 22, Palette.Muted,
                    TextAnchor.MiddleLeft, 0.105f, 0.075f, 0.91f, 0.23f);
                var drag = rect.gameObject.AddComponent<HorizonCardDrag>();
                drag.Available = available;
                drag.Dragged = CardDragged;
                drag.Played = CardPlayed;
                drag.Tapped = CardTapped;
                drag.IsOverTarget = IsInsideDropZone;
                drag.Rejected = item => { if (boardHint != null) boardHint.text = "没有用掉卡牌，再拖进发光圈内松手。"; };
                cards.Add(drag, card);
                CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
                StartCoroutine(RiseCard(rect, group, i * 0.08f));
            }
            if (session.RunNumber == 1 && session.Day == 1)
            {
                handGuide = View.Rect(root, "First action guide", 0, 0, 1, 1);
                RectTransform handMark = View.Panel(handGuide, "Guiding finger", new Color(1, 0.84f, 0.61f, 0.65f),
                    0.49f, 0.29f, 0.535f, 0.318f, 24).rectTransform;
                handGuide.gameObject.AddComponent<TutorialHand>().Hand = handMark;
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
            trail.gameObject.SetActive(visible);
            if (handGuide != null) handGuide.gameObject.SetActive(!visible);
            bool ready = visible && IsInsideDropZone(pointer);
            dropRing.color = ready ? Palette.Gold : Palette.Mint;
            dropTitle.text = ready ? "松手确认 · 使用这张牌" : "把卡牌拖进这个光圈";
            destinationBeacon.GetComponent<GuidePulse>().Active = !visible;
            if (!visible) { dragHint.text = "在圈内松手即可使用"; return; }
            CardSpec card = cards[drag];
            world.Preview(card.Kind, card.GivesSupport);
            Vector2 from = new Vector2(pointer.x, Screen.height * 0.31f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, from, null, out Vector2 localFrom);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointer, null, out Vector2 localTo);
            Vector2 vector = localTo - localFrom;
            trail.pivot = new Vector2(0, 0.5f);
            trail.anchoredPosition = localFrom;
            trail.sizeDelta = new Vector2(vector.magnitude, 5);
            trail.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg);
            dragHint.text = card.Delay == 0 ? "今天立即恢复" :
                "今天行动 → 第 " + (session.Day + card.Delay) + " 天收到回声";
        }

        private bool IsInsideDropZone(Vector2 pointer)
        {
            return !busy && overlay == null && DropTarget.Contains(destinationBeacon, pointer);
        }

        private void CardTapped(HorizonCardDrag drag)
        {
            if (busy || overlay != null || !cards.TryGetValue(drag, out CardSpec card)) return;
            world.Preview(card.Kind, card.GivesSupport);
            overlay = View.Rect(root, "Card explanation", 0, 0, 1, 1);
            View.Fill(overlay, "Block touches", new Color(0.008f, 0.024f, 0.04f, 0.64f), 0, 0, 1, 1, true);
            RectTransform panel = View.Panel(overlay, "Card details", Palette.Panel,
                0.06f, 0.16f, 0.94f, 0.69f, 38).rectTransform;
            panel.gameObject.AddComponent<PanelEntrance>();
            View.Label(panel, "Name", card.Name, 48, Palette.Text, TextAnchor.MiddleLeft,
                0.075f, 0.8f, 0.925f, 0.95f);
            View.Label(panel, "Today", "今天\n" + PlayExperience.NowLabel(card.Now), 32, Palette.Mint,
                TextAnchor.MiddleLeft, 0.075f, 0.58f, 0.925f, 0.8f);
            View.Label(panel, "Later", card.Delay == 0 ? "现在就能得到恢复。" :
                "第 " + (session.Day + card.Delay) + " 天\n" + PlayExperience.FutureMeaning(card) +
                (session.Day + card.Delay > GameSession.LastDay ? "（超过本局截止日）" : ""),
                30, Palette.Gold, TextAnchor.MiddleLeft, 0.075f, 0.35f, 0.925f, 0.59f);
            bool available = session.CanPlay(card);
            View.Label(panel, "Rule", available ? "每天只用一张。确认后，今天的选择就留下了。" :
                PlayExperience.BlockReason(session, card), 25, available ? Palette.Muted : Palette.Coral,
                TextAnchor.MiddleLeft, 0.075f, 0.23f, 0.925f, 0.35f);
            View.Button(panel, "Cancel", "再想想", () => { Destroy(overlay.gameObject); overlay = null; },
                0.075f, 0.055f, 0.43f, 0.19f, Palette.Deep, Palette.Text, 29);
            Button confirm = View.Button(panel, "Use card", available ? "使用这张牌" : "暂时无法使用", () =>
            {
                Destroy(overlay.gameObject);
                overlay = null;
                CardPlayed(drag);
            }, 0.46f, 0.055f, 0.925f, 0.19f, Palette.Mint, Palette.Ink, 29);
            confirm.interactable = available;
        }

        private void CardPlayed(HorizonCardDrag drag)
        {
            if (busy || !cards.TryGetValue(drag, out CardSpec card) || !session.CanPlay(card)) return;
            busy = true;
            foreach (HorizonCardDrag item in cards.Keys) item.Available = false;
            RunSnapshot before = session.Snapshot();
            ActionRecord action = session.Choose(card.Id);
            int stars = archive.wallet.Claim("run:" + session.RunNumber + ":action:" + session.Day, 1);
            ResourceDelta change = new ResourceDelta(session.Energy - before.energy, session.Mood - before.mood,
                session.Insight - before.insight, session.Relation - before.relation,
                session.Money - before.money, session.Ability - before.ability);
            archive.pendingFeedback = new FeedbackRecord
            {
                kind = FeedbackKind.Choice, runNumber = session.RunNumber, day = session.Day,
                title = "今天选择了「" + card.Name + "」",
                description = "今天实际变化\n" + PlayExperience.NowLabel(change) + "\n\n" +
                    PlayExperience.FutureLabel(card, session.Day), stardust = stars
            };
            if (session.CompletedRun == null) archive.active = session.Snapshot();
            else
            {
                archive.runs.Add(session.CompletedRun);
                archive.active = null;
                PrepareDeadlineFeedback(session.CompletedRun, stars);
            }
            Save();
            StartCoroutine(ResolveChoice(drag, card, action));
        }

        private IEnumerator ResolveChoice(HorizonCardDrag drag, CardSpec card, ActionRecord action)
        {
            trail.gameObject.SetActive(false);
            if (handGuide != null) handGuide.gameObject.SetActive(false);
            dropTitle.text = "投放成功";
            dropRing.color = Palette.Gold;
            world.Accept(card.Kind, card.GivesSupport);
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
            if (card.Kind == CardKind.Temptation)
                View.Fill(root, "Brief impact", new Color(1f, 0.32f, 0.28f, 0.12f), 0, 0, 1, 1);
            else if (card.Delay > 0)
            {
                View.Panel(root, "Seed halo", new Color(0.36f, 0.96f, 0.77f, 0.22f),
                    targetX - 0.04f, 0.83f, targetX + 0.04f, 0.87f, 30);
                View.Panel(root, "Seed core", Palette.Mint,
                    targetX - 0.009f, 0.845f, targetX + 0.009f, 0.854f, 9);
            }
            if (card.Kind == CardKind.Temptation)
            {
                Handheld.Vibrate();
            }
            yield return new WaitForSeconds(0.5f);
            if (session.CompletedRun != null)
            {
                yield return BossSequence(session.CompletedRun);
                yield break;
            }
            ShowFeedback();
        }

        private IEnumerator AdvanceDay()
        {
            busy = true;
            DayTransition transition = session.Advance();
            archive.active = session.Snapshot();
            if (transition.Echos.Count > 0)
            {
                string description = "";
                int stars = 0;
                foreach (PendingEcho echo in transition.Echos)
                {
                    int amount = echo.depth >= 2 ? session.CausalPath(echo.nodeId).Count : 3;
                    stars += archive.wallet.Claim("run:" + session.RunNumber + ":echo:" + echo.nodeId, amount);
                    description += (description.Length == 0 ? "" : "\n\n") +
                        "D" + echo.sourceDay + "「" + echo.cardName + "」 → 今天\n" +
                        echo.echoName + " · " + PlayExperience.NowLabel(echo.delta);
                }
                archive.pendingFeedback = new FeedbackRecord
                {
                    kind = FeedbackKind.Echoes, runNumber = session.RunNumber, day = session.Day,
                    title = transition.Echos.Count > 1 ? "过去的选择，一起回来了" : "你的选择回来了",
                    description = description, stardust = stars
                };
            }
            Save();
            foreach (PendingEcho echo in transition.Echos)
            {
                if (echo.depth >= 2) yield return CascadeSequence(echo);
                else yield return EchoSequence(echo);
            }
            if (archive.pendingFeedback != null) ShowFeedback();
            else if (session.HasPredictionReview) ShowPredictionReview();
            else BuildBoard();
        }

        private void ShowInRunStation(int stage)
        {
            stationStage = stage;
            Clear();
            bool reveal = session.RunNumber == 3 && stage >= 2;
            world.ShowStation(stage, reveal);
            View.Fill(root, "Station veil", new Color(0.005f, 0.02f, 0.035f, 0.45f), 0, 0, 1, 1);
            bool question = session.RunNumber == 3 && stage == 3;
            if (!question)
            {
                Image touch = View.Fill(root, "Walk forward", new Color(0, 0, 0, 0), 0, 0, 1, 1, true);
                StationSwipe swipe = touch.gameObject.AddComponent<StationSwipe>();
                swipe.ReadyAt = Time.unscaledTime + 1.1f;
                swipe.Advanced = () =>
                {
                    if (stationStage == stage) NextStationStage();
                };
            }
            View.Label(root, "Station title", "F U T U R E   S T A T I O N", 35, Palette.Mint,
                TextAnchor.MiddleCenter, 0.05f, 0.87f, 0.95f, 0.95f);
            View.Label(root, "Station day", "DAY 04  /  在地平线的另一边", 26, Palette.Muted,
                TextAnchor.MiddleCenter, 0.08f, 0.78f, 0.92f, 0.85f);
            string voice = question ? "「你还想继续这样走吗？」" : stage == 0 ? "「你终于来了。」" :
                stage == 1 ? "「你最近留下了很多东西。」" :
                reveal ? "「现在你终于看见我了。」" : session.RunNumber == 2 ?
                "「你已经知道，一些东西会回来。」" : "「它们还会继续生长。」";
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
            else if (question)
            {
                View.Label(root, "Choose concern", "现在，我更想保护这个。", 31, Palette.Muted,
                    TextAnchor.MiddleCenter, 0.1f, 0.315f, 0.9f, 0.38f);
                CardKind[] intentions = { CardKind.Temptation, CardKind.Growth, CardKind.Recovery };
                for (int i = 0; i < intentions.Length; i++)
                {
                    CardKind intent = intentions[i];
                    float y = 0.245f - i * 0.08f;
                    string name = RecentBehavior(intent);
                    View.Button(root, "Protect " + intent, "保护 · " + name, () =>
                    {
                        archive.preferredIntent = intent.ToString();
                        FinishStation();
                    }, 0.15f, y, 0.85f, y + 0.065f,
                    intent == CardKind.Growth ? Palette.Mint : Palette.Panel,
                    intent == CardKind.Growth ? Palette.Ink : Palette.Text, 27);
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
            if (!question)
                View.Button(root, "Next station beat", stage == 2 && session.RunNumber != 3 ?
                    "回到第 5 天" : "继续靠近", () => { if (stationStage == stage) NextStationStage(); },
                    0.17f, 0.065f, 0.83f, 0.135f, Palette.Mint, Palette.Ink, 30);
            View.Label(root, "Walk hint", question ? "这会改变你关注的未来，不会改变资源。" :
                "点击继续，或向前滑动。", 23, Palette.Muted,
                TextAnchor.MiddleCenter, 0.1f, 0.022f, 0.9f, 0.059f);
        }

        private void NextStationStage()
        {
            if (busy) return;
            busy = true;
            Handheld.Vibrate();
            if (stationStage < 2) ShowInRunStation(stationStage + 1);
            else if (session.RunNumber == 3) ShowInRunStation(3);
            else FinishStation();
        }

        private string RecentBehavior(CardKind kind)
        {
            int first = Mathf.Max(0, session.Actions.Count - 4);
            for (int i = session.Actions.Count - 1; i >= first; i--)
                if (session.Actions[i].kind == kind) return session.Actions[i].cardName;
            return kind == CardKind.Temptation ? "给自己一点快乐" :
                kind == CardKind.Growth ? "继续练习" : "照顾自己";
        }

        private void FinishStation()
        {
            if (session.StationVisited) return;
            busy = true;
            session.VisitStation();
            archive.active = session.Snapshot();
            Save();
            StartCoroutine(AdvanceDay());
        }

        private IEnumerator EchoSequence(PendingEcho echo)
        {
            Clear();
            busy = true;
            world.ShowBoard();
            float total = archive.seenFirstEcho ? 1.4f : 2.9f;
            View.Fill(root, "Echo shade", new Color(0.014f, 0.045f, 0.068f, 0.35f), 0, 0, 1, 1);
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
            View.Label(root, "Consequence", echo.echoName + "   " + PlayExperience.NowLabel(echo.delta), 42,
                echo.kind == CardKind.Temptation ? Palette.Coral : Palette.Mint,
                TextAnchor.MiddleCenter, 0.06f, 0.18f, 0.94f, 0.31f);
            Handheld.Vibrate();
            world.Reward(3);
            StarBurst(3, new Vector2(0.5f, 0.48f));
            yield return new WaitForSeconds(total * 0.34f);
            archive.seenFirstEcho = true;
            Save();
        }

        private IEnumerator CascadeSequence(PendingEcho echo)
        {
            List<CausalNode> path = session.CausalPath(echo.nodeId);
            if (path.Count < 3) yield break;
            Clear();
            busy = true;
            world.ShowBoard();
            View.Fill(root, "Chain darkness", new Color(0.009f, 0.026f, 0.045f, 0.7f),
                0, 0, 1, 1);
            View.Label(root, "Chain title", "C H A I N   F O U N D", 45, Palette.Gold,
                TextAnchor.MiddleCenter, 0.05f, 0.82f, 0.95f, 0.91f);
            bool network = path.Count >= 7;
            View.Label(root, "Chain clue", network ? "这一刻，只剩下你走出的因果网络。" :
                "一个选择，正在改变后来的选择。", 30, Palette.Muted,
                TextAnchor.MiddleCenter, 0.08f, 0.74f, 0.92f, 0.81f);
            for (int i = 0; i < path.Count; i++)
            {
                CausalNode node = path[i];
                world.Tick(i);
                Color color = node.type == CausalNodeKind.Choice ? Palette.Gold :
                    node.type == CausalNodeKind.Action ? Palette.Coral : Palette.Mint;
                if (network)
                {
                    float y = 0.69f - i * (0.49f / Mathf.Max(1, path.Count - 1));
                    if (i > 0) View.Fill(root, "Causal link", new Color(0.8f, 0.76f, 0.61f, 0.65f),
                        0.202f, y + 0.025f, 0.206f,
                        0.69f - (i - 1) * (0.49f / (path.Count - 1)) + 0.017f);
                    View.Panel(root, "Causal node", color, 0.186f, y, 0.22f, y + 0.032f, 18);
                    View.Label(root, "Causal day", "D" + node.day, 24, color,
                        TextAnchor.MiddleLeft, 0.27f, y - 0.005f, 0.39f, y + 0.038f);
                    View.Label(root, "Causal event", node.label, 26, Palette.Text,
                        TextAnchor.MiddleLeft, 0.4f, y - 0.005f, 0.89f, y + 0.038f);
                }
                else
                {
                    float x = 0.15f + i * (0.7f / (path.Count - 1));
                    if (i > 0) View.Fill(root, "Causal link", color,
                        0.15f + (i - 1) * (0.7f / (path.Count - 1)) + 0.018f,
                        0.519f, x, 0.526f);
                    View.Panel(root, "Causal node", color, x - 0.018f, 0.502f, x + 0.018f, 0.542f, 22);
                    View.Label(root, "Causal day", "DAY " + node.day, 25, color,
                        TextAnchor.MiddleCenter, x - 0.11f, 0.555f, x + 0.11f, 0.61f);
                    View.Label(root, "Causal event", node.label, 26, Palette.Text,
                        TextAnchor.MiddleCenter, x - 0.11f, 0.42f, x + 0.11f, 0.495f);
                }
                yield return new WaitForSeconds(i == 0 ? 0.25f : 0.25f / i);
            }
            View.Fill(root, "Chain flash", new Color(1f, 0.69f, 0.4f, 0.1f), 0, 0, 1, 1);
            View.Label(root, "Cascade", "C A S C A D E  × " + path.Count, 51, Palette.Gold,
                TextAnchor.MiddleCenter, 0.06f, network ? 0.08f : 0.28f,
                0.94f, network ? 0.16f : 0.37f);
            if (!network)
            {
                CardSpec replacement = CardCatalog.FindById(echo.replacementId);
                View.Label(root, "Changed hand", echo.echoName + (replacement == null ? "" :
                    "。今天，你可以选择「" + replacement.Name + "」。"), 27,
                    Palette.Text, TextAnchor.MiddleCenter, 0.07f, 0.2f, 0.93f, 0.28f);
            }
            Handheld.Vibrate();
            world.Reward(path.Count, true);
            StarBurst(path.Count, new Vector2(0.5f, 0.48f));
            yield return new WaitForSeconds(network ? 0.95f : 0.66f);
        }

        private static float EchoX(int day, int source, int due)
        {
            return Mathf.Lerp(0.12f, 0.88f, (day - source) / (float)Mathf.Max(1, due - source));
        }

        private void ShowFocus()
        {
            if (busy || overlay != null || session == null || !session.TryFocus()) return;
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
            View.Label(overlay, "Subtitle", compare ? "同一个今天，可以走向不同方向。" :
                string.IsNullOrEmpty(archive.preferredIntent) ? "你在未来留下的微光" :
                "你更想保护：" + IntentName(archive.preferredIntent),
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
                echoes.Sort((a, b) => a.dueDay != b.dueDay ? a.dueDay.CompareTo(b.dueDay) :
                    (a.kind.ToString() == archive.preferredIntent ? 0 : 1).CompareTo(
                        b.kind.ToString() == archive.preferredIntent ? 0 : 1));
                int limit = session.HorizonLevel >= 3 ? 3 : 5;
                for (int i = 0; i < Mathf.Min(limit, echoes.Count); i++)
                {
                    PendingEcho echo = echoes[i];
                    float y = 0.61f - i * 0.102f;
                    View.Panel(overlay, "Future event", Palette.Panel, 0.11f, y, 0.89f, y + 0.082f);
                    string detail = ForecastKnowledge.Clue(echo, session.HorizonLevel, archive.calibrations);
                    if (echo.kind.ToString() == archive.preferredIntent) detail = "关注 · " + detail;
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

        private static string IntentName(string intent)
        {
            return intent == CardKind.Temptation.ToString() ? "留给自己的快乐" :
                intent == CardKind.Growth.ToString() ? "长期成长" : "休息与关系";
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
                "  洞察" + Tendency(future.Insight - session.Insight) + "\n" +
                "关系" + Tendency(future.Relation - session.Relation) +
                "  金钱" + Tendency(future.Money - session.Money) +
                "  能力" + Tendency(future.Ability - session.Ability) : "此刻资源不足，这条路暂时走不通。",
                25, Palette.Text, TextAnchor.MiddleLeft, 0.07f, 0.29f, 0.94f, 0.7f);
            View.Label(panel, "Possible echo", future.Available && future.EchoDay > GameSession.LastDay ?
                "截止日之后 · 本局不会兑现" : future.Available && future.EchoDay > 0 ?
                "D" + future.EchoDay + " · " + card.FutureHint : "未来仍有未写下的部分",
                23, Palette.Muted, TextAnchor.MiddleLeft, 0.07f, 0.08f, 0.94f, 0.32f);
        }

        private string Tendency(int delta)
        {
            if (delta == 0) return "→";
            string direction = delta > 0 ? "↑" : "↓";
            return archive.calibrations >= 3 && Mathf.Abs(delta) >= 2 ? direction + direction : direction;
        }

        private IEnumerator BossSequence(RunRecord run)
        {
            Clear();
            busy = true;
            world.ShowDeadline();
            View.Label(root, "Deadline", "截止日到了", 53, Palette.Text,
                TextAnchor.MiddleCenter, 0.08f, 0.86f, 0.92f, 0.94f);
            View.Label(root, "Whole timeline", "你过去的 12 天，正一起回答。", 30, Palette.Muted,
                TextAnchor.MiddleCenter, 0.08f, 0.78f, 0.92f, 0.85f);
            bool[] gates = { run.boss.ability, run.boss.state, run.boss.support };
            string[] names = { "能力", "状态", "支援" };
            for (int i = 0; i < 3; i++)
            {
                world.OpenGate(i, gates[i]);
                world.Tick(i);
                View.Label(root, "Gate opening", names[i] + (gates[i] ? "门 · 点亮" : "门 · 还差一点"),
                    38, gates[i] ? Palette.Mint : Palette.Coral, TextAnchor.MiddleCenter,
                    0.08f, 0.28f - i * 0.08f, 0.92f, 0.35f - i * 0.08f);
                if (gates[i]) Handheld.Vibrate();
                yield return new WaitForSeconds(0.5f);
            }
            ShowDeadlineResult(run);
        }

        private void ShowDeadlineResult(RunRecord run)
        {
            Clear();
            world.ShowDeadline();
            bool[] gates = { run.boss.ability, run.boss.state, run.boss.support };
            for (int i = 0; i < 3; i++) world.OpenGate(i, gates[i]);
            View.Label(root, "Deadline verdict", "点亮 " + run.boss.passed + " / 3 道门", 48, Palette.Text,
                TextAnchor.MiddleCenter, 0.05f, 0.86f, 0.95f, 0.94f);
            View.Label(root, "Wallet", "星尘 " + archive.wallet.stardust, 28, Palette.Gold,
                TextAnchor.MiddleRight, 0.61f, 0.95f, 0.94f, 0.985f);
            RectTransform panel = View.Panel(root, "Deadline summary", Palette.Panel,
                0.045f, 0.045f, 0.955f, 0.57f, 38).rectTransform;
            panel.gameObject.AddComponent<PanelEntrance>();
            string[] names = { "能力", "状态", "支援" };
            string[] notes = {
                "能力 " + run.finalAbility + "/6",
                "精力 " + run.finalEnergy + "、心情 " + run.finalMood + " · 各需 4",
                "关系 " + run.finalRelation + "/6 · 金钱 " + run.finalMoney + "/2 · 支援行动需 2 次"
            };
            List<int>[] evidence = { run.boss.abilityDays, run.boss.stateDays, run.boss.supportDays };
            for (int i = 0; i < 3; i++)
            {
                float y = 0.72f - i * 0.155f;
                View.Label(panel, "Gate outcome", (gates[i] ? "已点亮 · " : "未点亮 · ") + names[i], 32,
                    gates[i] ? Palette.Mint : Palette.Coral, TextAnchor.MiddleLeft,
                    0.065f, y + 0.045f, 0.935f, y + 0.13f);
                View.Label(panel, "Gate evidence", notes[i] + "\n" + EvidenceSummary(evidence[i]), 24, Palette.Text,
                    TextAnchor.MiddleLeft, 0.065f, y - 0.005f, 0.935f, y + 0.068f);
            }
            int stars = archive.pendingFeedback == null ? 0 : archive.pendingFeedback.stardust;
            View.Label(panel, "Deadline reward", "+" + stars + " 星尘 · 时间线已收藏", 31, Palette.Gold,
                TextAnchor.MiddleLeft, 0.065f, 0.27f, 0.935f, 0.36f);
            View.Button(panel, "Inspect timeline", run.boss.passed < 3 ? "看看另一种可能" : "查看我的时间地图",
                () => { if (run.boss.passed < 3) ShowGhostResult(run); else ShowMap(false); },
                0.065f, 0.163f, 0.935f, 0.253f, Palette.Deep, Palette.Text, 28);
            View.Button(panel, "Try another timeline", "再试一条时间线", () =>
            {
                if (!busy) StartCoroutine(RestartSequence());
            }, 0.065f, 0.035f, 0.935f, 0.148f, Palette.Mint, Palette.Ink, 31);
            if (stars > 0) { world.Reward(stars, run.boss.passed == 3); StarBurst(stars, new Vector2(0.5f, 0.21f)); }
        }

        private void ShowGhostResult(RunRecord run)
        {
            if (overlay != null) return;
            overlay = View.Rect(root, "Possible branch", 0, 0, 1, 1);
            View.Fill(overlay, "Ghost veil", new Color(0.022f, 0.035f, 0.065f, 0.97f), 0, 0, 1, 1, true);
            View.Label(overlay, "Ghost title", "如果这里不同，会发生什么？", 42, Palette.Text,
                TextAnchor.MiddleCenter, 0.06f, 0.79f, 0.94f, 0.9f);
            GhostTimeline ghost = run.boss.ghostTimeline;
            string explanation = ghost == null ? run.boss.ghost :
                "第 " + ghost.sourceDay + " 天\n「" + ghost.originalName + "」改为「" + ghost.alternativeName + "」\n\n" +
                (ghost.echoDay > 0 ? "第 " + ghost.echoDay + " 天\n" + ghost.echoName + "\n\n" : "") +
                "第 12 天\n" + ghost.gateName + (ghost.gateOpens ? "门打开了" : "门仍未打开") +
                "\n\n" + ghost.beforePassed + " 道门 → " + ghost.afterPassed + " 道门";
            View.Label(overlay, "Possible chain", explanation, 35, Palette.Mint,
                TextAnchor.MiddleCenter, 0.1f, 0.24f, 0.9f, 0.77f);
            View.Label(overlay, "Conditional branch", "这是一条按现有规则重演的可能时间线。", 26, Palette.Muted,
                TextAnchor.MiddleCenter, 0.08f, 0.15f, 0.92f, 0.22f);
            View.Button(overlay, "Close ghost", "回到这次人生", () => { Destroy(overlay.gameObject); overlay = null; },
                0.13f, 0.065f, 0.87f, 0.135f, Palette.Mint, Palette.Ink);
        }

        private IEnumerator RestartSequence()
        {
            busy = true;
            archive.pendingFeedback = null;
            Save();
            Clear();
            busy = true;
            world.ShowBoard();
            View.Label(root, "Another horizon", "另一条时间线，正在形成", 38, Palette.Mint,
                TextAnchor.MiddleCenter, 0.07f, 0.49f, 0.93f, 0.57f);
            RectTransform seed = View.Panel(root, "New life seed", Palette.Gold, 0.47f, 0.42f, 0.53f, 0.46f, 30).rectTransform;
            for (float t = 0; t < 1.1f; t += Time.unscaledDeltaTime)
            {
                seed.localScale = Vector3.one * Mathf.Lerp(2.2f, 0.2f, t / 1.1f);
                seed.Rotate(0, 0, Time.unscaledDeltaTime * 130);
                yield return null;
            }
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
                TextAnchor.MiddleCenter, 0.07f, 0.835f,
                duringRun || run == null ? 0.93f : 0.79f, 0.895f);
            if (!duringRun && run != null)
                View.Button(overlay, "Rename", "改标题", () => ShowRenameRun(run),
                    0.8f, 0.846f, 0.94f, 0.892f, Palette.Panel, Palette.Mint, 22);
            View.Fill(overlay, "Map spine", new Color(0.46f, 0.76f, 0.72f, 0.36f),
                0.19f, 0.1f, 0.192f, 0.814f);
            List<CausalNode> graph = duringRun && session != null ? session.CausalNodes :
                GameSession.GraphForRun(run);
            for (int i = 0; i < graph.Count; i++)
            {
                CausalNode node = graph[i];
                CausalNode parent = graph.Find(n => n.id == node.parentId);
                if (parent == null || node.day > GameSession.LastDay) continue;
                float from = 0.795f - (parent.day - 1) * 0.055f;
                float to = 0.795f - (node.day - 1) * 0.055f;
                float x = 0.77f + (i % 5) * 0.027f;
                Color baseColor = node.type == CausalNodeKind.Choice ? Palette.Gold : Palette.Mint;
                Color thread = new Color(baseColor.r, baseColor.g, baseColor.b,
                    node.resolved ? 0.75f : 0.32f);
                if (parent.day < node.day)
                {
                    View.Fill(overlay, "Causal thread", thread, x, to, x + 0.003f, from);
                    View.Fill(overlay, "Causal start", thread, 0.73f, from, x, from + 0.003f);
                    View.Panel(overlay, "Causal arrival", thread,
                        x - 0.007f, to - 0.005f, x + 0.011f, to + 0.006f, 9);
                }
                else if (node.type == CausalNodeKind.Action)
                    View.Fill(overlay, "Choice became action", thread, 0.73f, to, x, to + 0.003f);
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
                    string immediate = action.now == null ? "" : "当下 " + PlayExperience.NowLabel(action.now);
                    string future = action.echoDay > 0 ? "D" + action.echoDay + " " +
                        (action.later == null ? action.echoName : PlayExperience.NowLabel(action.later)) : "";
                    CausalNode choice = graph.Find(n => n.day == day && n.type == CausalNodeKind.Choice);
                    if (choice != null) future += "  ·  " + choice.label;
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

        private void ShowRenameRun(RunRecord run)
        {
            RectTransform prompt = View.Rect(overlay, "Rename life", 0, 0, 1, 1);
            View.Fill(prompt, "Dim map", new Color(0.005f, 0.02f, 0.035f, 0.96f),
                0, 0, 1, 1, true);
            View.Label(prompt, "Question", "这段人生，你想怎样命名？", 37, Palette.Text,
                TextAnchor.MiddleCenter, 0.08f, 0.59f, 0.92f, 0.68f);
            RectTransform field = View.Rect(prompt, "Title field", 0.12f, 0.45f, 0.88f, 0.54f);
            Image background = field.gameObject.AddComponent<Image>();
            background.color = Palette.Panel;
            var input = field.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            input.textComponent = View.Label(field, "Edited title", "", 32, Palette.Text,
                TextAnchor.MiddleLeft, 0.05f, 0.06f, 0.95f, 0.94f);
            input.characterLimit = 28;
            input.lineType = InputField.LineType.SingleLine;
            input.text = run.title;
            View.Label(prompt, "Hint", "只改变档案里的名字，不改写已经发生的事。", 25,
                Palette.Muted, TextAnchor.MiddleCenter, 0.11f, 0.37f, 0.89f, 0.43f);
            View.Button(prompt, "Cancel", "取消", () => Destroy(prompt.gameObject),
                0.12f, 0.25f, 0.47f, 0.32f, Palette.Panel, Palette.Text);
            View.Button(prompt, "Save title", "保存", () =>
            {
                string title = input.text.Trim();
                if (title.Length == 0) return;
                run.title = title;
                Save();
                RenderMap(false);
            }, 0.53f, 0.25f, 0.88f, 0.32f, Palette.Mint, Palette.Ink);
            input.ActivateInputField();
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
            world.ShowStation(2, archive.runs.Count >= 3);
            View.Label(root, "Station title", "F U T U R E   S T A T I O N", 35, Palette.Mint,
                TextAnchor.MiddleCenter, 0.06f, 0.83f, 0.94f, 0.91f);
            string voice = archive.runs.Count > 0 && archive.runs[archive.runs.Count - 1].boss.passed == 3 ?
                "「你最近留下了很多光。」" : "「你终于来了。」";
            View.Label(root, "Voice", voice, 42, Palette.Text, TextAnchor.MiddleCenter,
                0.1f, 0.22f, 0.9f, 0.32f);
            if (!string.IsNullOrEmpty(archive.preferredIntent))
                View.Label(root, "Concern", "你说过，更想保护：" + IntentName(archive.preferredIntent),
                    29, Palette.Mint, TextAnchor.MiddleCenter, 0.12f, 0.15f, 0.88f, 0.21f);
            View.Button(root, "Home", "回到地平线", ShowHome, 0.19f, 0.065f, 0.81f, 0.13f,
                Palette.Mint, Palette.Ink);
        }
    }
}
