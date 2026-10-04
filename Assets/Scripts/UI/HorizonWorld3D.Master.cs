using System.Collections;
using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private AudioClip masterGrowthTone, masterEchoTone, masterMythicTone, masterRecoveryTone;
        private float insightTarget, insightAmount;
        public void SetInsightState(int energy, bool active)
        { insightTarget = active ? 1 : Mathf.Clamp01((energy - 55) / 45f) * 0.65f; }
        private void UpdateInsight()
        {
            insightAmount = Mathf.MoveTowards(insightAmount, insightTarget, Time.unscaledDeltaTime * 0.7f);
            if (portalLight != null) portalLight.SetColor("_Color", Color.Lerp(new Color(0.55f, 1.9f, 1.3f, 0.68f), new Color(1.6f, 2.6f, 1.9f, 0.82f), insightAmount));
            if (ambience != null) ambience.pitch = 1 + insightAmount * 0.16f;
            if (bloom != null && !preferences.reducedMotion && !preferences.batterySaver) bloom.Echo = Mathf.Max(bloom.Echo, insightAmount * 0.22f);
        }
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
            if (masterEchoTone == null) masterEchoTone = LanguageClip("Past returning", DomainEventKind.TimeEcho);
            if (masterGrowthTone == null) masterGrowthTone = LanguageClip("Future unfolding", DomainEventKind.FutureMemory);
            if (masterMythicTone == null) masterMythicTone = LanguageClip("Pattern breakthrough", DomainEventKind.PatternBroken);
            if (masterRecoveryTone == null) masterRecoveryTone = LanguageClip("Room to recover", DomainEventKind.FailAndAgain);
            audioSource.pitch = 1;
            audioSource.PlayOneShot(mythic ? masterMythicTone : e.kind == DomainEventKind.TimeEcho ? masterEchoTone : e.kind == DomainEventKind.FailAndAgain ? masterRecoveryTone : masterGrowthTone, 0.4f);
            if (mythic) { yield return new WaitForSecondsRealtime(3.0f); UpdateAudio(); }
        }
        private AudioClip LanguageClip(string name, DomainEventKind kind)
        {
            float[] samples = SoundLanguage.Render(kind); AudioClip clip = AudioClip.Create(name, samples.Length, 1, 22050, false);
            clip.SetData(samples, 0); sounds.Add(clip); return clip;
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
            HapticPhrase phrase = HapticLanguage.For(e.kind, e.tier); if (phrase == null) return;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (version.GetStatic<int>("SDK_INT") >= 26)
                        using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = effectClass.CallStatic<AndroidJavaObject>("createWaveform", phrase.timings, phrase.amplitudes, -1)) vibrator.Call("vibrate", effect);
                    else vibrator.Call("vibrate", phrase.timings, -1);
                }
            } catch (System.Exception) { Handheld.Vibrate(); }
#endif
        }
    }
}
