using UnityEngine;
using UnityEngine.UI;

namespace Horizon.UI
{
    // An extruded, bevelled card mesh. CanvasRenderer keeps its depth when the
    // card rotates; the labels remain on the front plane instead of a bitmap.
    public sealed class HorizonCardSurface : MaskableGraphic
    {
        public Color Accent = Palette.Mint;
        public float Thickness = 14;
        public float Radius = 22;
        public bool Available = true;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect r = rectTransform.rect;
            const int count = 28;
            var front = new Vector3[count]; var inner = new Vector3[count]; var back = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                int corner = i / 7; float angle = (corner * 90 + (i % 7) * 15) * Mathf.Deg2Rad;
                Vector2 centre = new Vector2(corner == 0 || corner == 3 ? r.xMax - Radius : r.xMin + Radius,
                    corner < 2 ? r.yMax - Radius : r.yMin + Radius);
                Vector2 point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius;
                front[i] = new Vector3(point.x, point.y, 0);
                inner[i] = new Vector3(Mathf.Lerp(point.x, r.center.x, 0.026f), Mathf.Lerp(point.y, r.center.y, 0.012f), -2);
                back[i] = front[i] + new Vector3(0, -Thickness * 0.38f, Thickness);
            }
            Color face = Available ? Palette.Panel : Color.Lerp(Palette.Panel, Palette.Ink, 0.55f);
            mesh.AddVert(new Vector3(r.center.x, r.center.y, -2), face, Vector2.zero);
            for (int i = 0; i < count; i++)
            {
                float light = Mathf.InverseLerp(r.yMin, r.yMax, inner[i].y);
                mesh.AddVert(inner[i], Color.Lerp(face, Color.Lerp(face, Accent, 0.18f), light), Vector2.zero);
            }
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                mesh.AddTriangle(0, i + 1, next + 1);
                Quad(mesh, front[i], front[next], inner[next], inner[i], Color.Lerp(Accent, Palette.Text, i < 14 ? 0.28f : 0.02f));
                Quad(mesh, back[i], back[next], front[next], front[i], Color.Lerp(Palette.Deep, Accent, i < 14 ? 0.12f : 0.32f));
            }
        }

        private static void Quad(VertexHelper mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero); mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero); mesh.AddVert(d, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
