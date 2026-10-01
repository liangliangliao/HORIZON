using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Horizon.UI
{
    public static class DropTarget
    {
        public static bool Contains(RectTransform target, Vector2 screenPoint)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(target, screenPoint, null, out Vector2 local);
            Rect bounds = target.rect;
            if (bounds.width <= 0 || bounds.height <= 0) return false;
            Vector2 offset = local - bounds.center;
            float x = offset.x / (bounds.width * 0.5f);
            float y = offset.y / (bounds.height * 0.5f);
            return x * x + y * y <= 1f;
        }
    }

    public static class Palette
    {
        public static readonly Color Ink = new Color(0.009f, 0.022f, 0.041f);
        public static readonly Color Deep = new Color(0.035f, 0.075f, 0.11f);
        public static readonly Color Panel = new Color(0.055f, 0.11f, 0.155f, 0.97f);
        public static readonly Color Mint = new Color(0.52f, 0.95f, 0.80f);
        public static readonly Color Gold = new Color(1f, 0.76f, 0.49f);
        public static readonly Color Coral = new Color(1f, 0.43f, 0.39f);
        public static readonly Color Text = new Color(0.94f, 0.96f, 0.94f);
        public static readonly Color Muted = new Color(0.60f, 0.73f, 0.75f);
    }

    public sealed class RoundedGraphic : MaskableGraphic
    {
        public float radius = 22f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float corner = Mathf.Min(radius, r.width * 0.5f, r.height * 0.5f);
            Vector2 center = r.center;
            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = center;
            vh.AddVert(vertex);
            int count = 0;
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                Vector2 origin = quadrant == 0 ? new Vector2(r.xMax - corner, r.yMax - corner) :
                    quadrant == 1 ? new Vector2(r.xMin + corner, r.yMax - corner) :
                    quadrant == 2 ? new Vector2(r.xMin + corner, r.yMin + corner) :
                    new Vector2(r.xMax - corner, r.yMin + corner);
                for (int step = 0; step <= 5; step++)
                {
                    float angle = (quadrant * 90f + step * 18f) * Mathf.Deg2Rad;
                    vertex.position = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * corner;
                    vh.AddVert(vertex);
                    count++;
                }
            }
            for (int i = 1; i < count; i++) vh.AddTriangle(0, i, i + 1);
            vh.AddTriangle(0, count, 1);
        }
    }

    public static class View
    {
        public static Font Font;

        public static RectTransform Rect(Transform parent, string name, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Image Fill(Transform parent, string name, Color color,
            float x0, float y0, float x1, float y1, bool raycast = false)
        {
            Image image = Rect(parent, name, x0, y0, x1, y1).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static RoundedGraphic Panel(Transform parent, string name, Color color,
            float x0, float y0, float x1, float y1, float radius = 20f)
        {
            RoundedGraphic panel = Rect(parent, name, x0, y0, x1, y1).gameObject.AddComponent<RoundedGraphic>();
            panel.color = color;
            panel.radius = radius;
            panel.raycastTarget = false;
            return panel;
        }

        public static Text Label(Transform parent, string name, string value, int size, Color color,
            TextAnchor alignment, float x0, float y0, float x1, float y1)
        {
            Text label = Rect(parent, name, x0, y0, x1, y1).gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = value;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(16, size / 2);
            label.resizeTextMaxSize = size;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(Transform parent, string name, string title, Action action,
            float x0, float y0, float x1, float y1, Color background, Color foreground, int size = 30)
        {
            RectTransform rect = Rect(parent, name, x0, y0, x1, y1);
            RoundedGraphic graphic = rect.gameObject.AddComponent<RoundedGraphic>();
            graphic.color = background;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.onClick.AddListener(() => action());
            Label(rect, "Title", title, size, foreground, TextAnchor.MiddleCenter, 0.02f, 0.05f, 0.98f, 0.95f);
            return button;
        }

        public static void Line(Transform parent, Color color, float x0, float y0, float x1, float y1,
            float thickness = 0.002f)
        {
            Fill(parent, "Line", color, x0, y0, x1, y1 + thickness);
        }

        public static void RefreshText(Transform parent)
        {
            Canvas.ForceUpdateCanvases();
            Text[] labels = parent.GetComponentsInChildren<Text>();
            foreach (Text label in labels)
                if (label.font != null) label.font.RequestCharactersInTexture(label.text, label.fontSize, label.fontStyle);
            foreach (Text label in labels) label.SetAllDirty();
            Canvas.ForceUpdateCanvases();
        }
    }

    public sealed class HorizonCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler,
        IEndDragHandler, IPointerClickHandler, IPointerDownHandler
    {
        public Action<HorizonCardDrag, Vector2> Dragged;
        public Action<HorizonCardDrag> Played;
        public Action<HorizonCardDrag> Tapped;
        public Action<HorizonCardDrag> Rejected;
        public Func<Vector2, bool> IsOverTarget;
        public bool Available = true;
        public System.Func<HorizonCardDrag, bool> CanBegin;
        public System.Action<HorizonCardDrag> Began;
        private bool dragging;
        private int pointerId;
        private RectTransform rect;
        private Vector3 origin;
        private Vector2 down;
        private bool dragged;
        private Coroutine returning;

        private void Awake() { rect = (RectTransform)transform; }

        public void OnPointerDown(PointerEventData eventData) { dragged = false; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Available || dragging || (CanBegin != null && !CanBegin(this))) return;
            if (rect == null) rect = (RectTransform)transform;
            dragging = true;
            pointerId = eventData.pointerId;
            Began?.Invoke(this);
            if (returning != null)
            {
                StopCoroutine(returning);
                returning = null;
                rect.position = origin;
            }
            origin = rect.position;
            down = eventData.pressPosition;
            dragged = true;
            rect.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!Available || !dragging || eventData.pointerId != pointerId) return;
            rect.position = origin + (Vector3)(eventData.position - down);
            rect.localScale = Vector3.one * 0.86f;
            rect.localRotation = Quaternion.Euler(0, 0, -4);
            Dragged?.Invoke(this, eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!Available || !dragging || eventData.pointerId != pointerId) return;
            dragging = false;
            bool reached = IsOverTarget != null && IsOverTarget(eventData.position);
            Dragged?.Invoke(this, Vector2.zero);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            if (reached)
            {
                Available = false;
                Played?.Invoke(this);
            }
            else
            {
                returning = StartCoroutine(ReturnCard());
                Rejected?.Invoke(this);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!dragged) Tapped?.Invoke(this);
        }

        private System.Collections.IEnumerator ReturnCard()
        {
            Vector3 from = rect.position;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / 0.2f)
            {
                rect.position = Vector3.Lerp(from, origin, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            rect.position = origin;
            returning = null;
        }
    }

    public sealed class RewardFlight : MonoBehaviour
    {
        public Vector2 StartPoint;
        public int Index;
        private float age;
        private void Update()
        {
            age += Time.unscaledDeltaTime;
            float delay = Index * 0.025f;
            float t = Mathf.Clamp01((age - delay) / 0.9f);
            float angle = Index * 2.399f;
            Vector2 scatter = new Vector2(Mathf.Cos(angle) * 0.08f, Mathf.Sin(angle) * 0.045f + 0.07f);
            Vector2 point = Vector2.Lerp(StartPoint, new Vector2(0.82f, 0.967f), t * t) + scatter * Mathf.Sin(t * Mathf.PI);
            RectTransform rect = (RectTransform)transform;
            rect.anchorMin = point - new Vector2(0.009f, 0.005f);
            rect.anchorMax = point + new Vector2(0.009f, 0.005f);
            rect.localScale = Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * 0.65f);
            if (t >= 1) Destroy(gameObject);
        }
    }

    public sealed class DropRingGraphic : MaskableGraphic
    {
        public float Thickness = 4;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float innerX = Mathf.Max(0, 1 - Thickness / Mathf.Max(1, r.width * 0.5f));
            float innerY = Mathf.Max(0, 1 - Thickness / Mathf.Max(1, r.height * 0.5f));
            for (int i = 0; i < 80; i++)
            {
                float a = i * Mathf.PI * 2 / 80, b = (i + 1) * Mathf.PI * 2 / 80;
                Vector2 outerA = new Vector2(Mathf.Cos(a) * r.width * 0.5f, Mathf.Sin(a) * r.height * 0.5f);
                Vector2 outerB = new Vector2(Mathf.Cos(b) * r.width * 0.5f, Mathf.Sin(b) * r.height * 0.5f);
                int start = vh.currentVertCount;
                vh.AddVert(outerA + r.center, color, Vector2.zero);
                vh.AddVert(outerB + r.center, color, Vector2.zero);
                vh.AddVert(Vector2.Scale(outerB, new Vector2(innerX, innerY)) + r.center, color, Vector2.zero);
                vh.AddVert(Vector2.Scale(outerA, new Vector2(innerX, innerY)) + r.center, color, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }

    public sealed class GuidePulse : MonoBehaviour
    {
        public bool Active = true;
        public float BaseScale = 1;
        private void Update()
        {
            transform.localScale = Vector3.one * (BaseScale + (Active ? Mathf.Sin(Time.unscaledTime * 3) * 0.025f : 0));
        }
    }

    public sealed class PanelEntrance : MonoBehaviour
    {
        private float age;
        private CanvasGroup group;
        private void Awake()
        {
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            transform.localScale = Vector3.one * 0.91f;
        }
        private void Update()
        {
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(age / 0.28f);
            group.alpha = t;
            transform.localScale = Vector3.one * Mathf.Lerp(0.91f, 1, Mathf.SmoothStep(0, 1, t));
            if (t >= 1) enabled = false;
        }
    }

    public sealed class TutorialHand : MonoBehaviour
    {
        public RectTransform Hand;
        private void Update()
        {
            float t = Mathf.Repeat(Time.unscaledTime * 0.45f, 1);
            float y = Mathf.Lerp(0.26f, 0.7075f, Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 0.7f)));
            Hand.anchorMin = new Vector2(0.49f, y);
            Hand.anchorMax = new Vector2(0.535f, y + 0.028f);
        }
    }

    public sealed class FutureHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action Activated;
        public Action Released;
        private Coroutine pending;
        private bool active;

        public void OnPointerDown(PointerEventData eventData)
        {
            StopPending();
            pending = StartCoroutine(WaitForFocus());
        }

        public void OnPointerUp(PointerEventData eventData) { StopPending(); }
        public void OnPointerExit(PointerEventData eventData) { StopPending(); }

        private System.Collections.IEnumerator WaitForFocus()
        {
            yield return new WaitForSecondsRealtime(0.65f);
            pending = null;
            active = true;
            Activated?.Invoke();
        }

        private void StopPending()
        {
            if (pending != null) StopCoroutine(pending);
            pending = null;
            if (active) { active = false; Released?.Invoke(); }
        }

        private void OnDisable() { StopPending(); }
    }

    public sealed class PredictionAxisDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public Action<int> Changed;
        private RectTransform rect;
        private int value;

        private void Awake() { rect = (RectTransform)transform; }

        public void OnPointerDown(PointerEventData eventData) { OnDrag(eventData); }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position,
                eventData.pressEventCamera, out Vector2 local);
            float position = Mathf.InverseLerp(rect.rect.yMin, rect.rect.yMax, local.y);
            int next = Mathf.RoundToInt(Mathf.Lerp(-3, 3, position));
            if (next == value) return;
            value = next;
            Changed?.Invoke(value);
        }
    }

    public sealed class StationSwipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action Advanced;
        public float ReadyAt;
        private Vector2 down;

        public void OnBeginDrag(PointerEventData eventData) { down = eventData.position; }
        public void OnDrag(PointerEventData eventData) { }
        public void OnEndDrag(PointerEventData eventData)
        {
            if (Time.unscaledTime >= ReadyAt && eventData.position.y - down.y > Screen.height * 0.09f)
                Advanced?.Invoke();
        }
    }
}
