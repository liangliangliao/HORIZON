using System.Collections;
using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private AudioClip masterGrowthTone, masterEchoTone, masterMythicTone;
        public void PresentMasterEvent(DomainEvent e)
        {
            if (e == null) return;
            if (!preferences.reducedMotion)
            {
                pulse = e.tier >= RewardTier.Epic ? 1 : 0.4f;
                if (e.tier >= RewardTier.Epic) Burst(Avatar.position + Vector3.up * 1.5f, Palette.Mint, 40);
                if (e.kind == DomainEventKind.DejaVu || e.kind == DomainEventKind.AllLinked) ShowStation(2, true);
            }
            if (preferences.sound) StartCoroutine(MasterEventSound(e));
            MasterHaptics.Play(e, preferences);
        }
        private IEnumerator MasterEventSound(DomainEvent e)
        {
            bool mythic = e.tier == RewardTier.Mythic;
            if (mythic)
            {
                audioSource.Stop(); if (ambience != null) ambience.mute = true;
                yield return new WaitForSecondsRealtime(0.45f);
                if (paused || !preferences.sound) { UpdateAudio(); yield break; }
            }
            if (masterEchoTone == null) masterEchoTone = CreateMasterTone("Past returning", 174, false);
            if (masterGrowthTone == null) masterGrowthTone = CreateMasterTone("Future unfolding", 110, false);
            if (masterMythicTone == null) masterMythicTone = CreateMasterTone("Pattern breakthrough", 65, true);
            audioSource.pitch = 1;
            audioSource.PlayOneShot(mythic ? masterMythicTone : e.kind == DomainEventKind.TimeEcho ? masterEchoTone : masterGrowthTone, 0.4f);
            if (mythic) { yield return new WaitForSecondsRealtime(1.5f); UpdateAudio(); }
        }
        private static AudioClip CreateMasterTone(string name, float frequency, bool mythic)
        {
            const int rate = 22050; float duration = mythic ? 1.5f : 0.8f; int count = (int)(rate * duration);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate, x = t / duration;
                float envelope = Mathf.Sin(Mathf.PI * x) * 0.22f;
                float beat = mythic && t < 0.55f ? Mathf.Exp(-((t % 0.25f) * 24)) : 1;
                samples[i] = (Mathf.Sin(2 * Mathf.PI * frequency * t) + 0.25f * Mathf.Sin(2 * Mathf.PI * frequency * 1.5f * t)) * envelope * beat;
            }
            AudioClip clip = AudioClip.Create(name, count, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
    }
    public static class MasterHaptics
    {
        public static void Play(DomainEvent e, PlayerPreferences preferences)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!preferences.haptics) return;
            long[] timing = e.tier == RewardTier.Mythic ? new long[] { 450, 30, 170, 100 } :
                e.kind == DomainEventKind.TimeEcho ? new long[] { 0, 25, 90, 35 } :
                e.tier >= RewardTier.Epic ? new long[] { 0, 15, 80, 25, 70, 45 } : new long[] { 0, 18 };
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (version.GetStatic<int>("SDK_INT") >= 26)
                        using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = effectClass.CallStatic<AndroidJavaObject>("createWaveform", timing, -1)) vibrator.Call("vibrate", effect);
                    else vibrator.Call("vibrate", timing, -1);
                }
            } catch (System.Exception) { Handheld.Vibrate(); }
#endif
        }
    }
}
