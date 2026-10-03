using Horizon.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon.UI
{
    public sealed class MasterSpectacleGraphic : MaskableGraphic
    {
        public DomainEventKind Kind;
        private float elapsed;
        private void Update()
        { if (VisualPreferences.Paused || VisualPreferences.ReducedMotion) return; elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f); SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect r = rectTransform.rect; Vector2 center = r.center;
            float size = Mathf.Min(r.width, r.height) * 0.37f;
            bool broken = Kind == DomainEventKind.PatternBroken;
            float progress = VisualPreferences.ReducedMotion ? 1 : Mathf.Clamp01((elapsed - 0.6f) / 1.6f);
            Color faint = color; faint.a *= 0.2f;
            for (int layer = 0; layer < 4; layer++)
            {
                float radius = size * (0.45f + layer * 0.18f) * (broken ? 1 + progress * layer * 0.3f : 1);
                for (int i = 0; i < 48; i++)
                { float a = i * Mathf.PI * 2 / 48, b = (i + 1) * Mathf.PI * 2 / 48;
                    Vector2 shift = broken ? new Vector2(Mathf.Sin(i * 7 + layer), Mathf.Cos(i * 3 + layer)) * progress * size * 0.35f : Vector2.zero;
                    Line(mesh, center + shift + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                        center + shift + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, layer == 0 ? 3 : 1.5f, faint); }
            }
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2 / 24 + elapsed * (VisualPreferences.ReducedMotion ? 0 : 0.08f);
                float radius = size * (0.5f + (i % 3) * 0.2f) * (broken ? 1 + progress * 0.8f : 1);
                Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                Dot(mesh, p, i % 3 == 0 ? 4 : 2, color);
                if (i % 3 == 0) Line(mesh, center, p, 1.5f, faint);
            }
            if (broken) Line(mesh, center + Vector2.down * size, center + Vector2.up * size * (0.3f + progress), 4, color);
            else Dot(mesh, center, 6 + progress * 5, color);
        }
        internal static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 direction = b - a; if (direction.sqrMagnitude < 0.001f) return;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * width * 0.5f;
            int n = mesh.currentVertCount;
            mesh.AddVert(a - normal, tint, Vector2.zero); mesh.AddVert(a + normal, tint, Vector2.zero);
            mesh.AddVert(b + normal, tint, Vector2.zero); mesh.AddVert(b - normal, tint, Vector2.zero);
            mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n, n + 2, n + 3);
        }
        internal static void Dot(VertexHelper mesh, Vector2 point, float radius, Color tint)
        {
            int n = mesh.currentVertCount; mesh.AddVert(point, tint, Vector2.zero);
            for (int i = 0; i <= 12; i++)
            { float a = i * Mathf.PI * 2 / 12; mesh.AddVert(point + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, tint, Vector2.zero); }
            for (int i = 0; i < 12; i++) mesh.AddTriangle(n, n + i + 1, n + i + 2);
        }
    }
    public sealed class FutureOrbitGraphic : MaskableGraphic
    {
        public int Bits;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect r = rectTransform.rect; Vector2 center = r.center;
            Vector2 scale = new Vector2(r.width * 0.41f, r.height * 0.41f);
            for (int i = 0; i < 8; i++)
            {
                float a = (90 - i * 45) * Mathf.Deg2Rad;
                Vector2 p = center + new Vector2(Mathf.Cos(a) * scale.x, Mathf.Sin(a) * scale.y);
                Color tint = color; bool lit = (Bits & (1 << i)) != 0; tint.a *= lit ? 1 : 0.15f;
                MasterSpectacleGraphic.Line(mesh, center, p, lit ? 2.5f : 1, tint);
                MasterSpectacleGraphic.Dot(mesh, p, lit ? 9 : 5, tint);
            }
            MasterSpectacleGraphic.Dot(mesh, center, 12, color);
        }
    }
}
