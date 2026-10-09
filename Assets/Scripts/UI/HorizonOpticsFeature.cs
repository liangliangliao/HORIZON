using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Horizon.UI
{
    // URP compatibility pass deliberately uses ordinary RGBA targets and its own
    // composite, including on GLES3. It does not enable URP's HDR post-process stack.
    public sealed class HorizonOpticsFeature : ScriptableRendererFeature
    {
        private OpticsPass pass;
        public override void Create()
        {
            pass?.Dispose();
            pass = new OpticsPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (data.cameraData.camera.TryGetComponent(out HorizonOptics optics) && optics.Active)
            { pass.Optics = optics; renderer.EnqueuePass(pass); }
        }
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData data)
        {
            if (data.cameraData.camera.TryGetComponent(out HorizonOptics optics) && optics.Active)
                pass.Target = renderer.cameraColorTargetHandle;
        }
        protected override void Dispose(bool disposing) { pass?.Dispose(); }

        private sealed class OpticsPass : ScriptableRenderPass
        {
            public HorizonOptics Optics;
            public RTHandle Target;
            private Material material;
            private readonly int source = Shader.PropertyToID("_HorizonOpticsSource");
            public OpticsPass() { ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth); }
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
            {
                if (material == null)
                    material = CoreUtils.CreateEngineMaterial(Resources.Load<Shader>("HorizonOptics"));
                RenderTextureDescriptor descriptor = data.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0; descriptor.msaaSamples = 1;
                descriptor.colorFormat = RenderTextureFormat.ARGB32;
                cmd.GetTemporaryRT(source, descriptor, FilterMode.Bilinear);
            }
            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                if (material == null || Target == null || Optics == null) return;
                material.SetVector("_Optics", new Vector4(Optics.Defocus, Optics.FocusDistance,
                    Optics.Distortion, Optics.LowPower ? 2 : 4));
                CommandBuffer cmd = CommandBufferPool.Get("HORIZON depth focus and refraction");
                cmd.Blit(Target.nameID, source);
                cmd.Blit(source, Target.nameID, material, 0);
                context.ExecuteCommandBuffer(cmd); CommandBufferPool.Release(cmd);
            }
            public override void OnCameraCleanup(CommandBuffer cmd) { cmd.ReleaseTemporaryRT(source); }
            public void Dispose() { CoreUtils.Destroy(material); }
        }
    }
}
