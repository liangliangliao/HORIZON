using System;

namespace Horizon.Game
{
    public sealed class HapticPhrase
    {
        public readonly long[] timings;
        public readonly int[] amplitudes;
        public HapticPhrase(long[] timings, int[] amplitudes) { this.timings = timings; this.amplitudes = amplitudes; }
    }
    public static class HapticLanguage
    {
        public static HapticPhrase For(DomainEventKind kind, RewardTier tier)
        {
            if (tier == RewardTier.Mythic) return new HapticPhrase(new long[] { 600, 30, 180, 110 }, new[] { 0, 55, 0, 220 });
            if (kind == DomainEventKind.TimeEcho) return new HapticPhrase(new long[] { 0, 25, 90, 35 }, new[] { 0, 90, 0, 120 });
            if (kind == DomainEventKind.Cascade || kind == DomainEventKind.CausalSingularity || kind == DomainEventKind.Breakthrough)
                return new HapticPhrase(new long[] { 0, 15, 85, 25, 70, 45, 55, 65 }, new[] { 0, 55, 0, 90, 0, 145, 0, 210 });
            if (kind == DomainEventKind.ActionTaken) return new HapticPhrase(new long[] { 0, 16 }, new[] { 0, 75 });
            if (kind == DomainEventKind.Comeback || kind == DomainEventKind.Victory) return new HapticPhrase(new long[] { 0, 45 }, new[] { 0, 140 });
            return null;
        }
    }
    public static class SoundLanguage
    {
        public static float[] Render(DomainEventKind kind, int sampleRate = 22050)
        {
            bool mythic = kind == DomainEventKind.PatternBroken || kind == DomainEventKind.RealityConvergence;
            bool echo = kind == DomainEventKind.TimeEcho;
            bool recovery = kind == DomainEventKind.FailAndAgain;
            float duration = mythic ? 3.0f : echo ? 1.1f : recovery ? 1.25f : 0.8f;
            var samples = new float[(int)(duration * sampleRate)];
            for (int i = 0; i < samples.Length; i++)
            {
                double t = i / (double)sampleRate, volume, frequency;
                if (mythic)
                {
                    if (t < 0.60) continue;
                    if (t < 1.25) { double beat = (t - 0.60) % 0.32; volume = Math.Exp(-beat * 26) * 0.30; frequency = 55; }
                    else { volume = Math.Exp(-(t - 1.25) * 2.2) * 0.45; frequency = 65 + (t - 1.25) * 26; }
                }
                else if (echo) { double beat = t < 0.30 ? t : t - 0.45; if (t >= 0.30 && t < 0.45) continue;
                    frequency = 174; volume = Math.Exp(-Math.Max(0, beat) * 7) * 0.25; }
                else { double p = t / duration; frequency = recovery ? 85 : 110 + p * 55; volume = Math.Sin(Math.PI * p) * (recovery ? 0.12 : 0.22); }
                samples[i] = (float)((Math.Sin(2 * Math.PI * frequency * t) + 0.20 * Math.Sin(3 * Math.PI * frequency * t)) * volume);
            }
            return samples;
        }
    }
}
