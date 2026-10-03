using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        public void SetFutureIdentity(int appearance, int orbit)
        {
            if (futureSelf == null) return;
            Color[] colours = { new Color(0.27f, 0.34f, 0.44f), Palette.Gold, new Color(0.46f, 0.39f, 0.63f),
                Palette.Mint, new Color(0.30f, 0.65f, 0.78f), Palette.Coral, new Color(0.75f, 0.77f, 0.73f) };
            Color tint = colours[Mathf.Clamp(appearance, 0, colours.Length - 1)];
            foreach (Renderer renderer in futureSelf.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.name.ToLowerInvariant().Contains("coat") && !renderer.name.ToLowerInvariant().Contains("body")) continue;
                var properties = new MaterialPropertyBlock(); properties.SetColor("_Color", tint); renderer.SetPropertyBlock(properties);
            }
            Transform halo = futureSelf.Find("Identity orbit");
            if (halo != null) Destroy(halo.gameObject);
            var group = new GameObject("Identity orbit"); group.transform.SetParent(futureSelf, false);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                Shape(group.transform, "Future orbit " + i, PrimitiveType.Sphere,
                    new Vector3(Mathf.Cos(angle) * 0.75f, 0.75f + Mathf.Sin(angle) * 0.50f, 0.28f),
                    Vector3.one * ((orbit & (1 << i)) != 0 ? 0.10f : 0.055f), (orbit & (1 << i)) != 0 ? glass : dark);
            }
            futureSelf.localScale = Vector3.one * (orbit == 255 ? 1.18f : 1.0f);
        }
    }
}
