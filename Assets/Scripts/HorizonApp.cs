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
        private readonly Dictionary<HorizonCardDrag, CardSpec> cards = new Dictionary<HorizonCardDrag, CardSpec>();
        private bool busy;
        private bool detailVisible;
        private int mapIndex;
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
            busy = false;
            Backdrop();
        }

        private void Backdrop()
        {
            View.Fill(root, "Night", Palette.Ink, 0, 0, 1, 1);
            View.Fill(root, "Lower air", new Color(0.045f, 0.10f, 0.15f, 0.72f), 0, 0, 1, 0.65f);
            View.Fill(root, "Far sky", new Color(0.07f, 0.13f, 0.17f, 0.45f), 0, 0.70f, 1, 0.93f);
            for (int i = 0; i < 26; i++)
            {
                float x = (i * 0.618034f + 0.13f) % 1f;
                float y = 0.42f + (i * 0.381966f % 1f) * 0.54f;
                float size = i % 7 == 0 ? 0.004f : 0.002f;
                View.Fill(root, "Distant light", new Color(0.54f, 0.86f, 0.84f, i % 4 == 0 ? 0.25f : 0.1f),
                    x, y, x + size, y + size);
            }
            View.Fill(root, "Horizon glow", new Color(0.31f, 0.75f, 0.72f, 0.06f), 0, 0.736f, 1, 0.757f);
            View.Fill(root, "Horizon", new Color(0.57f, 0.89f, 0.83f, 0.58f), 0.04f, 0.744f, 0.96f, 0.745f);
        }

        private void ShowIntro()
        {
            Clear();
            View.Fill(root, "Black", new Color(0.012f, 0.025f, 0.043f), 0, 0, 1, 1);
            View.Label(root, "First question", "你想看看未来的自己吗？", 51, Palette.Text,
                TextAnchor.MiddleCenter, 0.08f, 0.48f, 0.92f, 0.59f);
            View.Button(root, "Look", "看看", () => StartCoroutine(IntroSequence()),
                0.37f, 0.36f, 0.63f, 0.415f, Palette.Panel, Palette.Mint, 30);
        }

        private IEnumerator IntroSequence()
        {
            Clear();
            View.Fill(root, "Black", new Color(0.012f, 0.025f, 0.043f), 0, 0, 1, 1);
            View.Panel(root, "Far figure", new Color(0.35f, 0.65f, 0.65f, 0.12f),
                0.465f, 0.48f, 0.535f, 0.64f, 42);
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
                if (session.HasChosen && session.Day < GameSession.LastDay) session.Advance();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not resume HORIZON run: " + exception.Message);
                StartNewRun();
                return;
            }
            archive.active = session.Snapshot();
            Save();
            detailVisible = false;
            BuildBoard();
        }

        private void ShowHome()
        {
            Clear();
            View.Label(root, "Logo", "H O R I Z O N", 57, Palette.Text,
                TextAnchor.MiddleCenter, 0.08f, 0.82f, 0.92f, 0.91f);
            View.Panel(root, "Future self", new Color(0.46f, 0.77f, 0.74f, 0.2f),
                0.44f, 0.48f, 0.56f, 0.75f, 56);
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
            Clear();
            if (session == null) return;
            FutureRegion();
            PresentRegion();
            ResourceRegion();
            TimelineRegion();
            HandRegion();
        }

        private void FutureRegion()
        {
            Image hit = View.Fill(root, "Hold future", new Color(0, 0, 0, 0), 0.01f, 0.76f, 0.99f, 0.99f, true);
            hit.gameObject.AddComponent<FutureHold>().Activated = ShowFocus;
            View.Label(root, "Day", string.Format("DAY {0:00}  /  12", session.Day), 39, Palette.Text,
                TextAnchor.MiddleLeft, 0.07f, 0.931f, 0.55f, 0.979f);
            View.Label(root, "Vision", "HORIZON  " + Roman(session.HorizonLevel), 23, Palette.Mint,
                TextAnchor.MiddleRight, 0.53f, 0.934f, 0.93f, 0.976f);
            View.Fill(root, "Future rail", new Color(0.47f, 0.73f, 0.72f, 0.26f),
                0.14f, 0.849f, 0.88f, 0.850f);
            for (int i = 0; i < 4; i++)
            {
                int target = i == 0 ? session.Day : Math.Min(GameSession.LastDay, session.Day + i * 2);
                PendingEcho echo = session.Pending.Find(e => e.dueDay == target);
                string glyph = i == 0 ? "◉" : "?";
                Color color = i == 0 ? Palette.Mint : Palette.Muted;
                if (echo != null)
                {
                    if (session.HorizonLevel >= 2)
                        glyph = echo.kind == CardKind.Growth ? "✦" : echo.kind == CardKind.Temptation ? "✶" : "○";
                    else if (echo.kind == CardKind.Temptation) glyph = "✶";
                    color = echo.kind == CardKind.Temptation ? Palette.Coral : Palette.Mint;
                }
                float x = 0.11f + i * 0.245f;
                View.Label(root, "Future node", glyph, 43, color, TextAnchor.MiddleCenter,
                    x, 0.823f, x + 0.09f, 0.875f);
                View.Label(root, "Day mark", i == 0 ? "今天" : "D" + target, 21, Palette.Muted,
                    TextAnchor.MiddleCenter, x - 0.025f, 0.787f, x + 0.115f, 0.821f);
            }
            View.Label(root, "Focus hint", session.FocusUses == 0 ? FingerHint : "今天已经凝视过未来",
                22, Palette.Muted, TextAnchor.MiddleCenter, 0.11f, 0.754f, 0.89f, 0.783f);
            View.Button(root, "Map", "时间地图", () => ShowMap(true), 0.765f, 0.886f,
                0.96f, 0.923f, new Color(0.18f, 0.32f, 0.34f, 0.7f), Palette.Text, 21);
        }

        private static string Roman(int value) { return value == 1 ? "I" : value == 2 ? "II" : "III"; }

        private void PresentRegion()
        {
            View.Label(root, "Now", "现 在", 24, Palette.Muted, TextAnchor.MiddleCenter,
                0.27f, 0.69f, 0.73f, 0.725f);
            View.Panel(root, "Distant window", new Color(0.41f, 0.73f, 0.72f, 0.055f),
                0.15f, 0.525f, 0.85f, 0.675f, 70);
            View.Fill(root, "Window edge", new Color(0.49f, 0.81f, 0.77f, 0.12f),
                0.16f, 0.527f, 0.84f, 0.529f);
            View.Panel(root, "Person", new Color(0.45f, 0.78f, 0.74f, 0.19f),
                0.47f, 0.56f, 0.53f, 0.642f, 35);
            View.Label(root, "Scene time", string.Format("DAY {0:00}  ·  夜", session.Day), 45,
                Palette.Text, TextAnchor.MiddleCenter, 0.1f, 0.58f, 0.9f, 0.642f);
            View.Label(root, "Scene line", session.Day == 1 ? "从今天开始，未来会记住你。" :
                "有些选择，会在后来与你重逢。", 29, Palette.Muted,
                TextAnchor.MiddleCenter, 0.11f, 0.515f, 0.89f, 0.569f);
        }

        private void ResourceRegion()
        {
            int[] values = { session.Energy, session.Mood, session.Insight };
            string[] icons = { "⚡", "☀", "▲" };
            for (int row = 0; row < 3; row++)
            {
                float y = 0.479f - row * 0.037f;
                View.Label(root, "Resource", icons[row], 28, Palette.Mint, TextAnchor.MiddleCenter,
                    0.11f, y, 0.18f, y + 0.033f);
                for (int pip = 0; pip < GameSession.ResourceCap; pip++)
                {
                    float x = 0.215f + pip * 0.060f;
                    View.Panel(root, "State pip", pip < values[row] ?
                        new Color(0.53f, 0.86f, 0.76f, 0.85f) : new Color(0.38f, 0.55f, 0.56f, 0.25f),
                        x, y + 0.01f, x + 0.027f, y + 0.023f, 12);
                }
                if (detailVisible || values[row] <= 2)
                    View.Label(root, "Value", values[row].ToString(), 24,
                        values[row] <= 2 ? Palette.Coral : Palette.Text, TextAnchor.MiddleCenter,
                        0.84f, y, 0.9f, y + 0.033f);
            }
        }

        private void TimelineRegion()
        {
            View.Label(root, "Past", "过 去", 22, Palette.Muted, TextAnchor.MiddleLeft,
                0.08f, 0.342f, 0.27f, 0.382f);
            View.Fill(root, "Timeline", new Color(0.43f, 0.64f, 0.65f, 0.37f),
                0.13f, 0.337f, 0.9f, 0.339f);
            for (int day = 1; day <= GameSession.LastDay; day++)
            {
                float x = 0.125f + (day - 1) * 0.0685f;
                ActionRecord action = session.Actions.Find(a => a.day == day);
                Color color = action == null ? new Color(0.45f, 0.60f, 0.62f, 0.25f) :
                    action.kind == CardKind.Temptation ? Palette.Coral :
                    action.kind == CardKind.Growth ? Palette.Mint : Palette.Gold;
                View.Panel(root, "Past node", color, x, 0.331f, x + 0.012f, 0.345f, 8);
            }
            View.Label(root, "Timeline end", "12", 21, Palette.Muted, TextAnchor.MiddleRight,
                0.88f, 0.35f, 0.94f, 0.379f);
        }

        private void HandRegion()
        {
            View.Label(root, "Invitation", "把一个行动，送进未来", 30, Palette.Text,
                TextAnchor.MiddleCenter, 0.1f, 0.289f, 0.9f, 0.329f);
            View.Label(root, "Drag hint", "向上推一张牌", 23, Palette.Muted,
                TextAnchor.MiddleCenter, 0.2f, 0.045f, 0.8f, 0.08f);
            trail = View.Fill(root, "Causal light", Palette.Mint, 0.5f, 0.5f, 0.5f, 0.5f).rectTransform;
            trail.gameObject.SetActive(false);
            dragHint = View.Label(root, "Destination", "", 25, Palette.Mint,
                TextAnchor.MiddleCenter, 0.1f, 0.298f, 0.9f, 0.332f);
            CardSpec[] hand = session.Hand;
            for (int i = 0; i < hand.Length; i++)
            {
                CardSpec card = hand[i];
                float x = 0.045f + i * 0.307f;
                RectTransform rect = View.Rect(root, card.Id, x, 0.085f, x + 0.295f, 0.283f);
                bool available = session.CanPlay(card);
                var panel = rect.gameObject.AddComponent<RoundedGraphic>();
                panel.color = available ? Palette.Panel : new Color(0.065f, 0.09f, 0.11f, 0.82f);
                panel.radius = 24f;
                panel.raycastTarget = true;
                Color accent = card.Kind == CardKind.Growth ? Palette.Mint :
                    card.Kind == CardKind.Temptation ? Palette.Coral : Palette.Gold;
                View.Fill(rect, "Card accent", accent, 0.08f, 0.89f, 0.92f, 0.897f);
                View.Label(rect, "Type", card.Kind == CardKind.Growth ? "长 线" :
                    card.Kind == CardKind.Temptation ? "即 时" : "恢 复", 23, accent,
                    TextAnchor.MiddleLeft, 0.1f, 0.73f, 0.9f, 0.86f);
                View.Label(rect, "Name", card.Name, 32, Palette.Text,
                    TextAnchor.MiddleLeft, 0.1f, 0.49f, 0.9f, 0.72f);
                View.Label(rect, "Now", available ? card.Now.ShortLabel() : "⚡不足 · 先恢复",
                    25, available ? Palette.Text : Palette.Coral,
                    TextAnchor.MiddleLeft, 0.1f, 0.29f, 0.9f, 0.46f);
                string futureLabel = card.Delay > 0 && session.Day + card.Delay > GameSession.LastDay ?
                    "截止日之后 · 未兑现" : card.FutureHint;
                View.Label(rect, "Future", futureLabel, 22, Palette.Muted,
                    TextAnchor.MiddleLeft, 0.1f, 0.07f, 0.9f, 0.24f);
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
            trail.gameObject.SetActive(visible);
            if (!visible) { dragHint.text = ""; return; }
            CardSpec card = cards[drag];
            Vector2 from = new Vector2(Screen.width * 0.5f, Screen.height * 0.35f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, from, null, out Vector2 localFrom);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointer, null, out Vector2 localTo);
            Vector2 vector = localTo - localFrom;
            trail.pivot = new Vector2(0, 0.5f);
            trail.anchoredPosition = localFrom;
            trail.sizeDelta = new Vector2(vector.magnitude, 4);
            trail.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg);
            if (card.Delay == 0) dragHint.text = "现在 · 让明天轻一点";
            else
            {
                int offset = Mathf.Clamp(Mathf.CeilToInt(Mathf.InverseLerp(0.33f, 0.78f,
                    pointer.y / Screen.height) * card.Delay), 1, card.Delay);
                int destination = session.Day + offset;
                dragHint.text = "DAY " + destination + "  ·  " +
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
            RectTransform selected = (RectTransform)drag.transform;
            Vector3 start = selected.position;
            float targetX = card.Kind == CardKind.Temptation ? 0.5f :
                Mathf.Clamp01(0.13f + Mathf.Min(3, card.Delay) * 0.245f);
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
                View.Label(overlay, "Seed", "✦  DAY " + action.echoDay, 28, Palette.Mint,
                    TextAnchor.MiddleCenter, 0.2f, 0.08f, 0.8f, 0.27f);
            yield return new WaitForSeconds(card.Kind == CardKind.Temptation ? 0.62f : 0.5f);
            Destroy(overlay.gameObject);
            overlay = null;
            if (session.CompletedRun != null)
            {
                yield return BossSequence(session.CompletedRun);
                yield break;
            }
            DayTransition transition = session.Advance();
            detailVisible = false;
            archive.active = session.Snapshot();
            Save();
            foreach (PendingEcho echo in transition.Echos)
            {
                yield return EchoSequence(echo);
            }
            BuildBoard();
        }

        private IEnumerator EchoSequence(PendingEcho echo)
        {
            Clear();
            float total = archive.seenFirstEcho ? 1.4f : 2.8f;
            View.Fill(root, "Echo shade", new Color(0.02f, 0.07f, 0.1f, 0.92f), 0, 0, 1, 1);
            View.Label(root, "Echo title", "T I M E   E C H O", 48, Palette.Mint,
                TextAnchor.MiddleCenter, 0.06f, 0.69f, 0.94f, 0.77f);
            View.Fill(root, "Causal line", Palette.Mint, 0.12f, 0.53f, 0.88f, 0.532f);
            View.Label(root, "Backtrack", "DAY " + echo.dueDay + "     ←     DAY " + echo.sourceDay,
                32, Palette.Muted, TextAnchor.MiddleCenter, 0.1f, 0.55f, 0.9f, 0.63f);
            yield return new WaitForSeconds(total * 0.38f);
            View.Label(root, "Past action", "「" + echo.cardName + "」", 43, Palette.Text,
                TextAnchor.MiddleCenter, 0.08f, 0.42f, 0.92f, 0.52f);
            View.Label(root, "How long ago", (echo.dueDay - echo.sourceDay) + "天前。", 27, Palette.Muted,
                TextAnchor.MiddleCenter, 0.1f, 0.34f, 0.9f, 0.42f);
            yield return new WaitForSeconds(total * 0.32f);
            View.Label(root, "Consequence", echo.echoName + "   " + echo.delta.ShortLabel(), 42,
                echo.kind == CardKind.Temptation ? Palette.Coral : Palette.Mint,
                TextAnchor.MiddleCenter, 0.06f, 0.22f, 0.94f, 0.34f);
            if (echo.kind == CardKind.Temptation) Handheld.Vibrate();
            yield return new WaitForSeconds(total * 0.3f);
            archive.seenFirstEcho = true;
            Save();
        }

        private void ShowFocus()
        {
            if (busy || session == null || !session.TryFocus()) return;
            archive.active = session.Snapshot();
            Save();
            overlay = View.Rect(root, "FOCUS MODE", 0, 0, 1, 1);
            View.Fill(overlay, "Veil", new Color(0.015f, 0.05f, 0.08f, 0.97f), 0, 0, 1, 1, true);
            View.Label(overlay, "Title", "F O C U S   M O D E", 44, Palette.Mint,
                TextAnchor.MiddleCenter, 0.05f, 0.78f, 0.95f, 0.89f);
            View.Label(overlay, "Subtitle", "你在未来留下的微光", 32, Palette.Text,
                TextAnchor.MiddleCenter, 0.1f, 0.69f, 0.9f, 0.77f);
            List<PendingEcho> echoes = new List<PendingEcho>(session.Pending);
            echoes.Sort((a, b) => a.dueDay.CompareTo(b.dueDay));
            for (int i = 0; i < Mathf.Min(5, echoes.Count); i++)
            {
                PendingEcho echo = echoes[i];
                float y = 0.61f - i * 0.102f;
                View.Panel(overlay, "Future event", Palette.Panel, 0.11f, y, 0.89f, y + 0.082f);
                string detail = session.HorizonLevel >= 2 ? echo.echoName :
                    echo.kind == CardKind.Temptation ? "一处微弱的火种" : "一颗尚未发芽的种子";
                View.Label(overlay, "Forecast", "DAY " + echo.dueDay + "     " + detail, 29,
                    echo.kind == CardKind.Temptation ? Palette.Coral : Palette.Mint,
                    TextAnchor.MiddleLeft, 0.16f, y + 0.01f, 0.84f, y + 0.072f);
            }
            if (echoes.Count == 0)
                View.Label(overlay, "Empty", "这里还没有被埋下的回声。", 31, Palette.Muted,
                    TextAnchor.MiddleCenter, 0.12f, 0.49f, 0.88f, 0.6f);
            View.Label(overlay, "Boss", "距截止日还有 " + (GameSession.LastDay - session.Day) + " 天",
                30, Palette.Muted, TextAnchor.MiddleCenter, 0.13f, 0.14f, 0.87f, 0.21f);
            View.Button(overlay, "Close", "回到现在", () => BuildBoard(),
                0.19f, 0.055f, 0.81f, 0.12f, Palette.Mint, Palette.Ink);
        }

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
                Color color = run.actions[i].kind == CardKind.Growth ? Palette.Mint :
                    run.actions[i].kind == CardKind.Temptation ? Palette.Coral : Palette.Gold;
                View.Panel(root, "Action star", color, x, 0.721f, x + 0.018f, 0.739f, 10);
            }
            bool[] gates = { run.boss.ability, run.boss.state, run.boss.support };
            string[] names = { "能力", "状态", "支援" };
            string[] notes = { "洞察达到 7", "精力与心情达到 4", "向朋友伸出手至少 2 次" };
            for (int i = 0; i < 3; i++)
            {
                float y = 0.58f - i * 0.145f;
                View.Panel(root, "Gate", Palette.Panel, 0.1f, y, 0.9f, y + 0.118f);
                View.Label(root, "Gate sign", gates[i] ? "✦" : "○", 45,
                    gates[i] ? Palette.Mint : Palette.Muted, TextAnchor.MiddleCenter,
                    0.14f, y + 0.02f, 0.25f, y + 0.098f);
                View.Label(root, "Gate name", names[i], 36, Palette.Text,
                    TextAnchor.MiddleLeft, 0.29f, y + 0.047f, 0.52f, y + 0.105f);
                View.Label(root, "Gate note", notes[i], 22, Palette.Muted,
                    TextAnchor.MiddleLeft, 0.29f, y + 0.011f, 0.85f, y + 0.055f);
            }
            yield return new WaitForSeconds(2.1f);
            View.Label(root, "Future voice", run.boss.passed == 3 ? "「这条路，是你亲手照亮的。」" :
                "「如果这里不同，会发生什么？」", 34, Palette.Mint,
                TextAnchor.MiddleCenter, 0.08f, 0.13f, 0.92f, 0.21f);
            View.Label(root, "Ghost", run.boss.ghost, 28, Palette.Text,
                TextAnchor.MiddleCenter, 0.11f, 0.05f, 0.89f, 0.13f);
            yield return new WaitForSeconds(4.0f);
            View.Fill(root, "Collapse", Palette.Ink, 0, 0, 1, 1);
            View.Label(root, "New horizon", "另一条时间线，正在形成。", 35, Palette.Mint,
                TextAnchor.MiddleCenter, 0.08f, 0.43f, 0.92f, 0.57f);
            yield return new WaitForSeconds(1.7f);
            StartNewRun();
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
                string label = action == null ? "尚未到来" : action.cardName +
                    (action.echoDay > 0 ? "   →   D" + action.echoDay + (action.echoed ? "  ✦" : "  ·") : "");
                View.Label(overlay, "Action", label, 28, action == null ? Palette.Muted : Palette.Text,
                    TextAnchor.MiddleLeft, 0.25f, y - 0.016f, 0.92f, y + 0.036f);
            }
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
            View.Panel(root, "Bench", new Color(0.6f, 0.82f, 0.79f, 0.25f),
                0.26f, 0.36f, 0.74f, 0.385f, 10);
            View.Panel(root, "Future figure", new Color(0.49f, 0.81f, 0.77f, 0.26f),
                0.47f, 0.4f, 0.53f, 0.59f, 35);
            string voice = archive.runs.Count > 0 && archive.runs[archive.runs.Count - 1].boss.passed == 3 ?
                "「你最近留下了很多光。」" : "「你终于来了。」";
            View.Label(root, "Voice", voice, 42, Palette.Text, TextAnchor.MiddleCenter,
                0.1f, 0.22f, 0.9f, 0.32f);
            View.Button(root, "Home", "回到地平线", ShowHome, 0.19f, 0.065f, 0.81f, 0.13f,
                Palette.Mint, Palette.Ink);
        }
    }
}
