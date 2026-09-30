using UnityEngine;
using UnityEngine.UI;

namespace Horizon.UI
{
    public sealed class TimeThreadGraphic : MaskableGraphic
    {
        public Vector2 From, To;
        public float Thickness = 3;
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            Vector2 from = new Vector2(rect.xMin + From.x * rect.width, rect.yMin + From.y * rect.height);
            Vector2 to = new Vector2(rect.xMin + To.x * rect.width, rect.yMin + To.y * rect.height);
            Vector2 bend = new Vector2(Mathf.Max(24, Mathf.Abs(to.x - from.x) * 0.55f), 0);
            Vector2 previous = from;
            for (int i = 1; i <= 18; i++)
            {
                float t = i / 18f, u = 1 - t;
                Vector2 next = u * u * u * from + 3 * u * u * t * (from + bend) +
                    3 * u * t * t * (to - bend) + t * t * t * to;
                Vector2 normal = new Vector2(-(next - previous).y, (next - previous).x).normalized * Thickness * 0.5f;
                int first = helper.currentVertCount;
                UIVertex vertex = UIVertex.simpleVert; vertex.color = color;
                vertex.position = previous - normal; helper.AddVert(vertex);
                vertex.position = previous + normal; helper.AddVert(vertex);
                vertex.position = next + normal; helper.AddVert(vertex);
                vertex.position = next - normal; helper.AddVert(vertex);
                helper.AddTriangle(first, first + 1, first + 2); helper.AddTriangle(first, first + 2, first + 3);
                previous = next;
            }
        }
    }
}
