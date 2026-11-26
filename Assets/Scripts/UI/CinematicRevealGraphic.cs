using Horizon.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon.UI
{
    // A single screen layer that works on the Android scene without HDR Bloom.
    // It sits below the receipt and controls and samples the scene's release clock.
    public sealed class CinematicRevealGraphic : MaskableGraphic
    {
        private Material field;

        public static CinematicRevealGraphic Attach(RectTransform parent, int index)
        {
            var rect = View.Rect(parent, "Screen energy release", 0, 0, 1, 1);
            rect.SetSiblingIndex(index);
            var graphic = rect.gameObject.AddComponent<CinematicRevealGraphic>();
            graphic.raycastTarget = false;
            graphic.gameObject.SetActive(false);
            return graphic;
        }

        public void Sample(float age, float charge, EnergyRelease release, float seed, bool echo, bool reduced)
        {
            bool visible = !reduced && (charge > .004f || release.Flash + release.Fire + release.Wave > .004f);
            if (!visible)
            {
                Clear();
                return;
            }
            if (field == null)
            {
                Shader shader = Resources.Load<Shader>("HorizonRevealField");
                if (shader == null || !shader.isSupported) return;
                field = new Material(shader);
                material = field;
            }
            field.SetVector("_Cue", new Vector4(age, charge, seed, rectTransform.rect.width / Mathf.Max(1, rectTransform.rect.height)));
            field.SetVector("_Energy", new Vector4(release.Flash, release.Fire, release.Wave, echo ? 1 : 0));
            field.SetVector("_Origin", new Vector4(.50f, echo ? .566f : .574f, 0, 0));
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Clear()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            mesh.AddVert(new Vector3(bounds.xMin, bounds.yMin), Color.white, Vector2.zero);
            mesh.AddVert(new Vector3(bounds.xMin, bounds.yMax), Color.white, Vector2.up);
            mesh.AddVert(new Vector3(bounds.xMax, bounds.yMax), Color.white, Vector2.one);
            mesh.AddVert(new Vector3(bounds.xMax, bounds.yMin), Color.white, Vector2.right);
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(0, 2, 3);
        }

        protected override void OnDestroy()
        {
            if (field != null) Destroy(field);
            base.OnDestroy();
        }
    }
}
