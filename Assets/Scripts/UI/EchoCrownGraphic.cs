using UnityEngine;
using UnityEngine.UI;

namespace Horizon.UI
{
    // A bounded mesh of expanding light rays, rendered behind readable outcomes.
    public sealed class EchoCrownGraphic : MaskableGraphic
    {
        public int Intensity = 2;
        private float age;
        private void Update()
        {
            if (VisualPreferences.Paused) return;
            age += Time.unscaledDeltaTime; SetVerticesDirty();
            if (age >= 1.35f) Destroy(gameObject);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float t = Mathf.Clamp01(age / 1.35f), fade = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.45f));
            Rect r = rectTransform.rect;
            float radius = Mathf.Min(r.width, r.height) * (0.24f + 0.42f * t);
            int count = Mathf.Clamp(Intensity * 9, 18, 45);
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count + t * 0.16f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 across = new Vector2(-direction.y, direction.x);
                float length = (i % 3 == 0 ? 0.7f : 0.32f) * radius;
                Vector2 start = r.center + direction * radius;
                Color inner = color; inner.a *= fade * 0.65f;
                Color outer = inner; outer.a = 0;
                int v = vh.currentVertCount;
                vh.AddVert(start - across * 2.1f, inner, Vector2.zero);
                vh.AddVert(start + across * 2.1f, inner, Vector2.zero);
                vh.AddVert(start + direction * length + across * 0.6f, outer, Vector2.zero);
                vh.AddVert(start + direction * length - across * 0.6f, outer, Vector2.zero);
                vh.AddTriangle(v, v + 1, v + 2); vh.AddTriangle(v, v + 2, v + 3);
            }
        }
    }
}
