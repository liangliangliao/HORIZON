using UnityEngine;
using UnityEngine.UI;
using Horizon.Game;

namespace Horizon.UI
{
    public sealed class StardustGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            vh.AddVert(r.center, color, Vector2.zero);
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5;
                float radius = i % 2 == 0 ? 0.5f : 0.22f;
                vh.AddVert(r.center + new Vector2(Mathf.Cos(angle) * r.width, Mathf.Sin(angle) * r.height) * radius,
                    color, Vector2.zero);
            }
            for (int i = 1; i <= 10; i++) vh.AddTriangle(0, i, i == 10 ? 1 : i + 1);
        }
    }

    public sealed class ActionIconGraphic : MaskableGraphic
    {
        public CardKind Kind;
        public bool Support;
        private void Quad(VertexHelper vh, float x, float y, float w, float h, Color tint)
        {
            Rect r = rectTransform.rect; int first = vh.currentVertCount;
            vh.AddVert(new Vector2(r.xMin + x * r.width, r.yMin + y * r.height), tint, Vector2.zero);
            vh.AddVert(new Vector2(r.xMin + (x + w) * r.width, r.yMin + y * r.height), tint, Vector2.zero);
            vh.AddVert(new Vector2(r.xMin + (x + w) * r.width, r.yMin + (y + h) * r.height), tint, Vector2.zero);
            vh.AddVert(new Vector2(r.xMin + x * r.width, r.yMin + (y + h) * r.height), tint, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Support)
            {
                Quad(vh, 0.12f, 0.19f, 0.3f, 0.35f, color);
                Quad(vh, 0.18f, 0.62f, 0.18f, 0.22f, color);
                Quad(vh, 0.57f, 0.19f, 0.3f, 0.35f, color);
                Quad(vh, 0.63f, 0.62f, 0.18f, 0.22f, color);
                Quad(vh, 0.36f, 0.28f, 0.3f, 0.065f, color);
            }
            else if (Kind == CardKind.Temptation)
            {
                Quad(vh, 0.22f, 0.06f, 0.57f, 0.88f, color);
                Quad(vh, 0.3f, 0.23f, 0.41f, 0.62f, Palette.Ink);
                Quad(vh, 0.36f, 0.33f, 0.29f, 0.06f, color);
                Quad(vh, 0.36f, 0.48f, 0.2f, 0.13f, color);
            }
            else if (Kind == CardKind.Growth)
            {
                Quad(vh, 0.06f, 0.17f, 0.4f, 0.65f, color);
                Quad(vh, 0.53f, 0.17f, 0.4f, 0.65f, color);
                Quad(vh, 0.46f, 0.12f, 0.07f, 0.63f, color);
                Quad(vh, 0.14f, 0.58f, 0.23f, 0.055f, Palette.Ink);
                Quad(vh, 0.62f, 0.42f, 0.22f, 0.055f, Palette.Ink);
            }
            else
            {
                Rect r = rectTransform.rect;
                vh.AddVert(r.center, color, Vector2.zero);
                for (int i = 0; i <= 24; i++)
                {
                    float a = i * Mathf.PI * 2 / 24;
                    vh.AddVert(r.center + new Vector2(Mathf.Cos(a) * r.width * 0.4f,
                        Mathf.Sin(a) * r.height * 0.4f), color, Vector2.zero);
                    if (i > 0) vh.AddTriangle(0, i, i + 1);
                }
                Quad(vh, 0.57f, 0.45f, 0.3f, 0.4f, Palette.Panel);
            }
        }
    }

    public sealed class FeedbackRipple : MonoBehaviour
    {
        private float age;
        private Graphic graphic;
        private Color tint;
        private void Start() { graphic = GetComponent<Graphic>(); tint = graphic.color; }
        private void Update()
        {
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(age / 0.75f);
            transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 2.8f, 1 - (1 - t) * (1 - t));
            graphic.color = new Color(tint.r, tint.g, tint.b, tint.a * (1 - t));
            if (t >= 1) Destroy(gameObject);
        }
    }

    public sealed class ImpactFlash : MonoBehaviour
    {
        private float age;
        private Graphic graphic;
        private Color tint;
        private void Awake() { graphic = GetComponent<Graphic>(); tint = graphic.color;
            if (VisualPreferences.ReducedMotion) { tint.a = 0; graphic.color = tint; } }
        private void Update()
        {
            age += Time.unscaledDeltaTime;
            graphic.color = new Color(tint.r, tint.g, tint.b, tint.a * Mathf.Clamp01(1 - age / 0.32f));
            if (age >= 0.32f) Destroy(gameObject);
        }
    }

    public sealed class ReceiptPop : MonoBehaviour
    {
        public float Delay;
        private float age;
        private CanvasGroup group;
        private void Awake() { group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; }
        private void Update()
        {
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01((age - Delay) / 0.28f);
            group.alpha = t;
            transform.localScale = Vector3.one * (0.86f + t * 0.14f + Mathf.Sin(t * Mathf.PI) * 0.08f);
            if (t >= 1) enabled = false;
        }
    }

    public sealed class RewardCounter : MonoBehaviour
    {
        public int From, To;
        private float age;
        private Text label;
        private void Awake() { label = GetComponent<Text>(); }
        private void Update()
        {
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01((age - 0.5f) / 0.7f);
            label.text = "星尘 " + Mathf.RoundToInt(Mathf.Lerp(From, To, t));
            transform.localScale = Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * 0.065f);
            if (t >= 1) enabled = false;
        }
    }
}
