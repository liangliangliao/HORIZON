using UnityEngine;
using UnityEngine.UI;

namespace Horizon.UI
{
    public sealed class SoftGlowGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = bounds.center; vertex.color = color; mesh.AddVert(vertex);
            Color edge = color; edge.a = 0;
            for (int i = 0; i <= 48; i++)
            {
                float angle = i / 48f * Mathf.PI * 2;
                vertex.position = bounds.center + new Vector2(Mathf.Cos(angle) * bounds.width * 0.5f,
                    Mathf.Sin(angle) * bounds.height * 0.5f);
                vertex.color = edge; mesh.AddVert(vertex);
                if (i > 0) mesh.AddTriangle(0, i, i + 1);
            }
        }
    }
}
