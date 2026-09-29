using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Horizon.UI
{
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
            var go = new GameObject(name, typeof(RectTransform));
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
    }

    public sealed class HorizonCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler,
        IEndDragHandler, IPointerClickHandler
    {
        public Action<HorizonCardDrag, Vector2> Dragged;
        public Action<HorizonCardDrag> Played;
        public Action<HorizonCardDrag> Tapped;
        public bool Available = true;
        private RectTransform rect;
        private Vector3 origin;
        private Vector2 down;

        private void Awake() { rect = (RectTransform)transform; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Available) return;
            origin = rect.position;
            down = eventData.position;
            rect.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!Available) return;
            rect.position = origin + (Vector3)(eventData.position - down);
            rect.localScale = Vector3.one * 1.04f;
            Dragged?.Invoke(this, eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!Available) return;
            bool reached = eventData.position.y > Screen.height * 0.46f &&
                eventData.position.y - down.y > Screen.height * 0.11f;
            Dragged?.Invoke(this, Vector2.zero);
            rect.localScale = Vector3.one;
            if (reached) Played?.Invoke(this);
            else rect.position = origin;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Available) Tapped?.Invoke(this);
        }
    }

    public sealed class FutureHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action Activated;
        private Coroutine pending;

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
            Activated?.Invoke();
        }

        private void StopPending()
        {
            if (pending != null) StopCoroutine(pending);
            pending = null;
        }
    }
}
