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
        public int PastFailures = 3, ChainSize = 6, OrbitBits;
        public float Elapsed { get { return elapsed; } }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect r = rectTransform.rect; Vector2 centre = r.center;
            float size = Mathf.Min(r.width, r.height) * 0.40f;
            float time = VisualPreferences.ReducedMotion ? 4 : elapsed;
            Color faint = color; faint.a *= 0.20f;
            if (Kind == DomainEventKind.PatternBroken)
            {
                float advance = Mathf.Clamp01((time - 1.05f) / 1.3f), fracture = Mathf.Clamp01((time - 1.9f) / 0.8f);
                for (int i = 0; i < Mathf.Clamp(PastFailures, 2, 6); i++)
                {
                    float y = centre.y + (i - 2.5f) * size * 0.19f;
                    Vector2 start = new Vector2(centre.x - size, y), stop = new Vector2(centre.x - size * 0.12f, y);
                    Color past = new Color(0.9f, 0.35f, 0.34f, (1 - fracture) * 0.65f);
                    Line(mesh, start, stop, 2, past); Dot(mesh, stop, 5, past);
                    for (int shard = 0; shard < 6; shard++)
                    { Vector2 p = Vector2.Lerp(start, stop, shard / 5f) + new Vector2((shard % 2 == 0 ? -1 : 1) * fracture * size * 0.15f, (i - 2.5f) * fracture * size * 0.38f);
                        Dot(mesh, p, Mathf.Max(1, 4 * (1 - fracture)), past); }
                }
                Vector2 from = centre + new Vector2(-size, -size * 0.62f), crossing = centre + new Vector2(size * (advance * 2 - 1), -size * 0.62f);
                Line(mesh, from, crossing, 5, color); Dot(mesh, crossing, 8 + advance * 4, color);
                return;
            }
            if (Kind == DomainEventKind.RealityConvergence)
            {
                float link = Mathf.Clamp01((time - 0.4f) / 1.8f);
                Color[] colours = { Palette.Gold, Palette.Mint, Palette.Text };
                for (int i = 0; i < 3; i++)
                { float angle = (90 + i * 120) * Mathf.Deg2Rad;
                    Vector2 point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size * (1 - link * 0.50f);
                    Line(mesh, point, Vector2.Lerp(point, centre, link), 3 + link * 3, colours[i]); Dot(mesh, point, 12, colours[i]); }
                Dot(mesh, centre, 4 + link * 20, color); return;
            }
            if (Kind == DomainEventKind.Cascade || Kind == DomainEventKind.CausalSingularity || Kind == DomainEventKind.Breakthrough)
            {
                int count = Mathf.Clamp(ChainSize, 4, 18); float progress = Mathf.Clamp01(time / 2.7f);
                int activated = Mathf.FloorToInt(Mathf.Pow(progress, 0.58f) * count);
                Vector2 previous = centre + Vector2.down * size;
                for (int i = 0; i < count; i++)
                { float x = Mathf.Sin(i * 1.5f) * size * 0.55f, y = Mathf.Lerp(-size, size, i / (float)(count - 1)); Vector2 p = centre + new Vector2(x, y);
                    Line(mesh, previous, p, i <= activated ? 4 : 1, i <= activated ? color : faint);
                    Dot(mesh, p, i == activated ? 13 : i < activated ? 7 : 4, i <= activated ? color : faint); previous = p; }
                if (Kind == DomainEventKind.CausalSingularity && progress > 0.75f)
                    for (int i = 0; i < 48; i++) { float angle = i * Mathf.PI / 24; Vector2 outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        Line(mesh, centre + outward * size * (progress - 0.75f), centre + outward * size * progress * 1.3f, 2, color); }
                return;
            }
            if (Kind == DomainEventKind.OrbitActivated || Kind == DomainEventKind.AllLinked)
            {
                for (int i = 0; i < 8; i++) { float angle = i * Mathf.PI / 4; Vector2 p = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size;
                    bool active = Kind == DomainEventKind.AllLinked || (OrbitBits & (1 << i)) != 0;
                    Line(mesh, centre, p, active ? 3 : 1, active ? color : faint); Dot(mesh, p, active ? 10 : 4, active ? color : faint); }
                Dot(mesh, centre, Kind == DomainEventKind.AllLinked ? 22 : 10, color); return;
            }
            if (Kind == DomainEventKind.DejaVu || Kind == DomainEventKind.FutureMemory)
            { Dot(mesh, centre + Vector2.up * size * 0.38f, size * 0.17f, color);
                Line(mesh, centre + Vector2.up * size * 0.16f, centre + Vector2.down * size * 0.55f, size * 0.27f, faint);
                for (int i = 0; i < 7; i++) Line(mesh, centre + new Vector2(-size, (i - 3) * size * 0.14f), centre + new Vector2(size, (i - 3) * size * 0.14f), 1, faint);
                return;
            }
            if (Kind == DomainEventKind.VictoryAnchor)
            { float opening = Mathf.Clamp01(time / 1.5f); Vector2 bottom = centre - Vector2.up * size, top = centre + Vector2.up * size;
                Line(mesh, bottom + Vector2.left * size * 0.5f, top + Vector2.left * size * 0.5f, 5, color);
                Line(mesh, top + Vector2.left * size * 0.5f, top + Vector2.right * size * 0.5f, 5, color);
                Line(mesh, top + Vector2.right * size * 0.5f, bottom + Vector2.right * size * 0.5f, 5, color);
                Line(mesh, bottom, centre + Vector2.right * size * opening, 3, faint); Dot(mesh, centre, 10 + opening * 14, color); return;
            }
            float wave = Kind == DomainEventKind.TimeEcho ? (time % 1.4f) / 1.4f : Mathf.Clamp01(time / 2);
            for (int layer = 0; layer < 4; layer++)
            { float radius = size * (0.35f + layer * 0.18f + wave * 0.12f);
                for (int i = 0; i < 64; i++) { float a = i * Mathf.PI / 32, b = (i + 1) * Mathf.PI / 32;
                    Line(mesh, centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                        centre + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, layer == 0 ? 3 : 1, layer == 0 ? color : faint); } }
            if (Kind == DomainEventKind.Comeback || Kind == DomainEventKind.FailAndAgain)
                Line(mesh, centre + Vector2.down * size, centre + Vector2.up * size * Mathf.Clamp01(time / 1.4f), 5, color);
            Dot(mesh, centre, 7 + wave * 9, color);
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
