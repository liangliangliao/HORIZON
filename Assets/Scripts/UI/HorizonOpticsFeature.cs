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
            private Material bloom;
            private readonly int source = Shader.PropertyToID("_HorizonOpticsSource");
            private readonly int bloomA=Shader.PropertyToID("_HorizonSafeBloomA"), bloomB=Shader.PropertyToID("_HorizonSafeBloomB");
            private bool bloomAllocated;
            public OpticsPass() { ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth); }
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
            {
                if (material == null)
                    material = CoreUtils.CreateEngineMaterial(Resources.Load<Shader>("HorizonOptics"));
                RenderTextureDescriptor descriptor = data.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0; descriptor.msaaSamples = 1;
                descriptor.colorFormat = RenderTextureFormat.ARGB32;
                cmd.GetTemporaryRT(source, descriptor, FilterMode.Bilinear);
                bloomAllocated=Optics!=null && Optics.Glow>.001f;
                if(bloomAllocated)
                {
                    if(bloom==null) bloom=CoreUtils.CreateEngineMaterial(Resources.Load<Shader>("HorizonBloom"));
                    descriptor.width=Mathf.Clamp(descriptor.width/(Optics.LowPower?8:4),16,384);
                    descriptor.height=Mathf.Clamp(descriptor.height/(Optics.LowPower?8:4),16,512);
                    cmd.GetTemporaryRT(bloomA,descriptor,FilterMode.Bilinear); cmd.GetTemporaryRT(bloomB,descriptor,FilterMode.Bilinear);
                }
            }
            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                if (material == null || Target == null || Optics == null) return;
                material.SetVector("_Optics", new Vector4(Optics.Defocus, Optics.FocusDistance,
                    Optics.Distortion, Optics.LowPower ? 2 : 4));
                CommandBuffer cmd = CommandBufferPool.Get("HORIZON depth focus and refraction");
                cmd.Blit(Target.nameID, source);
                bool focus=Optics.Defocus>.001f || Optics.Distortion>.001f;
                if(focus)
                {
                    cmd.Blit(source,Target.nameID,material,0);
                    if(bloomAllocated) cmd.Blit(Target.nameID,source);
                }
                if(bloomAllocated && bloom!=null)
                {
                    // RGBA8 on GLES, never URP's broken packed HDR intermediates.
                    bloom.SetFloat("_Intensity",Optics.Glow); bloom.SetFloat("_Echo",0);
                    cmd.Blit(source,bloomA,bloom,0);
                    cmd.SetGlobalVector("_Direction",new Vector4(1,0,0,0)); cmd.Blit(bloomA,bloomB,bloom,1);
                    cmd.SetGlobalVector("_Direction",new Vector4(0,1,0,0)); cmd.Blit(bloomB,bloomA,bloom,1);
                    cmd.SetGlobalTexture("_BloomTex",bloomA); cmd.Blit(source,Target.nameID,bloom,2);
                }
                context.ExecuteCommandBuffer(cmd); CommandBufferPool.Release(cmd);
            }
            public override void OnCameraCleanup(CommandBuffer cmd)
            { cmd.ReleaseTemporaryRT(source); if(bloomAllocated) {cmd.ReleaseTemporaryRT(bloomA); cmd.ReleaseTemporaryRT(bloomB);} }
            public void Dispose() { CoreUtils.Destroy(material); CoreUtils.Destroy(bloom); }
        }
    }
}
