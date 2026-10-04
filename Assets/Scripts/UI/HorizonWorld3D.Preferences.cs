using Horizon.Game;
using UnityEngine;

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
        private bool pausedCameraWasEnabled;

        public void ApplyPreferences(PlayerPreferences value)
        {
            preferences = value ?? new PlayerPreferences();
            if (bloom != null)
            {
                bloom.enabled = !preferences.batterySaver && bloom.IsSupported;
                if (preferences.reducedMotion) bloom.Echo = 0;
            }
            WorldCamera.allowMSAA = !preferences.batterySaver;
            WorldCamera.allowHDR = !preferences.batterySaver && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
            QualitySettings.antiAliasing = preferences.batterySaver ? 0 : 2;
            QualitySettings.shadows = preferences.batterySaver ? ShadowQuality.Disable : ShadowQuality.All;
            UpdateAudio();
        }

        public void SetPaused(bool value)
        {
            if (paused == value) return;
            if (WorldCamera != null)
            {
                if (value) { pausedCameraWasEnabled = WorldCamera.enabled; WorldCamera.enabled = false; }
                else WorldCamera.enabled = pausedCameraWasEnabled;
            }
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
    }
}
