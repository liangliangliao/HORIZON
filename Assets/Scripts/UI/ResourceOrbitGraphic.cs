using UnityEngine;
using UnityEngine.UI;

namespace Horizon.UI
{
    public sealed class ResourceOrbitGraphic : MaskableGraphic
    {
        public int Value;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float outer = Mathf.Min(r.width, r.height) * 0.48f;
            float inner = outer * 0.70f;
            for (int segment = 0; segment < 10; segment++)
            {
                Color shade = segment < Value ? color : new Color(color.r, color.g, color.b, 0.20f);
                for (int step = 0; step < 4; step++)
                {
                    float a = (90 - segment * 36 - 3 - step * 7.5f) * Mathf.Deg2Rad;
                    float b = a - 7.5f * Mathf.Deg2Rad;
                    int first = vh.currentVertCount;
                    vh.AddVert(r.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * outer, shade, Vector2.zero);
                    vh.AddVert(r.center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * outer, shade, Vector2.zero);
                    vh.AddVert(r.center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * inner, shade, Vector2.zero);
                    vh.AddVert(r.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * inner, shade, Vector2.zero);
                    vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
                }
            }
        }
    }
}
