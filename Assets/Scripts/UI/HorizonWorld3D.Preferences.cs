using Horizon.Game;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Horizon.UI
{
    public static class VisualPreferences
    {
        public static bool ReducedMotion;
        public static bool Paused;
    }

    public sealed partial class HorizonWorld3D
    {
        private PlayerPreferences preferences = new PlayerPreferences();
        private bool paused;
        private UniversalRenderPipelineAsset runtimePipeline;
        private RenderPipelineAsset priorPipeline;

        public void ApplyPreferences(PlayerPreferences value)
        {
            preferences = value ?? new PlayerPreferences();
            if (movieParticles != null)
            {
                var budget = movieParticles.main; int cap = preferences.batterySaver ? 12 : 64;
                if (budget.maxParticles != cap) { movieParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); budget.maxParticles = cap; }
                if (preferences.reducedMotion) movieParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            Application.targetFrameRate = preferences.batterySaver ? 30 : 60;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset source)
            {
                if (runtimePipeline == null)
                { priorPipeline = QualitySettings.renderPipeline; runtimePipeline = Instantiate(source); runtimePipeline.name = "HORIZON runtime quality"; QualitySettings.renderPipeline = runtimePipeline; }
                runtimePipeline.msaaSampleCount = preferences.batterySaver ? 1 : 2;
                runtimePipeline.renderScale = preferences.batterySaver ? .85f : 1;
                runtimePipeline.shadowDistance = preferences.batterySaver ? 0 : 22;
                runtimePipeline.supportsHDR = !preferences.batterySaver;
            }
            if (bloom != null)
            {
                bloom.enabled = !preferences.batterySaver && bloom.IsSupported;
                if (preferences.reducedMotion) bloom.Echo = 0;
            }
            WorldCamera.allowMSAA = !preferences.batterySaver;
            WorldCamera.allowHDR = !preferences.batterySaver && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
            QualitySettings.antiAliasing = preferences.batterySaver ? 0 : 2;
            QualitySettings.shadows = preferences.batterySaver ? UnityEngine.ShadowQuality.Disable : UnityEngine.ShadowQuality.All;
            UpdateAudio();
        }

        public void SetPaused(bool value)
        {
            if (paused == value) return;
            // Pause simulation and actors while continuing to draw the last
            // scene. Disabling the base camera leaves URP's clear camera behind
            // and turns the phone's 3D viewport blank during settings/backgrounding.
            paused = value;
            foreach (HorizonActor actor in GetComponentsInChildren<HorizonActor>(true))
                actor.MotionRate = value ? 0 : 1;
            Cinematics?.SetPaused(value);
            UpdateAudio();
        }

        private void UpdateAudio()
        {
            if (audioSource != null) audioSource.mute = paused || !preferences.sound;
            if (ambience != null) ambience.mute = paused || movieMuted || !preferences.sound || !preferences.ambience;
        }
        private void RestoreRenderQuality()
        {
            if (runtimePipeline == null) return;
            if (QualitySettings.renderPipeline == runtimePipeline) QualitySettings.renderPipeline = priorPipeline;
            Dispose(runtimePipeline); runtimePipeline = null;
        }
    }
}
