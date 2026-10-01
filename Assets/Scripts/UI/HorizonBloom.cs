using UnityEngine;

namespace Horizon.UI
{
    [RequireComponent(typeof(Camera))]
    public sealed class HorizonBloom : MonoBehaviour
    {
        public float Intensity = 0.42f;
        public float Echo;
        private Material material;

        public void Initialize(Shader shader)
        {
            if (shader != null && shader.isSupported && SystemInfo.supportsImageEffects)
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void Update() { Echo = Mathf.MoveTowards(Echo, 0, Time.unscaledDeltaTime * 1.4f); }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null) { Graphics.Blit(source,destination); return; }
            // Two quarter-resolution buffers, capped for predictable mobile cost.
            int width = Mathf.Clamp(source.width / 4, 16, 384);
            int height = Mathf.Clamp(source.height / 4, 16, 512);
            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR)
                ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            RenderTexture a = RenderTexture.GetTemporary(width,height,0,format);
            RenderTexture b = RenderTexture.GetTemporary(width,height,0,format);
            try
            {
                a.filterMode = b.filterMode = FilterMode.Bilinear;
                Graphics.Blit(source,a,material,0);
                material.SetVector("_Direction", new Vector4(1,0,0,0)); Graphics.Blit(a,b,material,1);
                material.SetVector("_Direction", new Vector4(0,1,0,0)); Graphics.Blit(b,a,material,1);
                material.SetTexture("_BloomTex", a); material.SetFloat("_Intensity",Intensity);
                material.SetFloat("_Echo",Echo); Graphics.Blit(source,destination,material,2);
            }
            finally { RenderTexture.ReleaseTemporary(a); RenderTexture.ReleaseTemporary(b); }
        }

        private void OnDestroy()
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        }
    }
}
