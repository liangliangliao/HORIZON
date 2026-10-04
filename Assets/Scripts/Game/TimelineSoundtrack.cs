using System;
using System.Linq;

namespace Horizon.Game
{
    public static class TimelineSoundtrack
    {
        public const int SampleRate = 22050;
        public static byte[] Pcm(RunRecord run)
        {
            if (run == null || run.actions == null || run.actions.Count == 0) throw new ArgumentException("A timeline needs real actions.");
            var audio = new float[SampleRate * 10];
            var graph = GameSession.GraphForRun(run);
            var choices = run.actions.OrderByDescending(a => CausalGraph.Descendants(graph, a.nodeId).Count).Take(3).OrderBy(a => a.day).ToList();
            for (int i = 0; i < choices.Count; i++)
                Mix(audio, choices[i].kind == CardKind.Recovery ? DomainEventKind.FailAndAgain : DomainEventKind.ActionTaken, 0.3 + i * 0.8, 0.7f);
            var echoes = run.actions.Where(a => a.echoed).Take(4).ToList();
            for (int i = 0; i < echoes.Count; i++) Mix(audio, DomainEventKind.TimeEcho, 3.0 + i * 0.85, 0.65f);
            bool broken = graph.Any(n => n.type == CausalNodeKind.Pattern && n.label.Contains("BROKEN"));
            Mix(audio, broken ? DomainEventKind.PatternBroken : DomainEventKind.Victory, 6.8, 0.8f);
            var pcm = new byte[audio.Length * 2];
            for (int i = 0; i < audio.Length; i++)
            {
                short sample = (short)(Math.Max(-0.85, Math.Min(0.85, audio[i])) * short.MaxValue);
                pcm[i * 2] = (byte)(sample & 255); pcm[i * 2 + 1] = (byte)((sample >> 8) & 255);
            }
            return pcm;
        }
        private static void Mix(float[] audio, DomainEventKind kind, double seconds, float gain)
        {
            float[] phrase = SoundLanguage.Render(kind, SampleRate); int offset = (int)(seconds * SampleRate);
            for (int i = 0; i < phrase.Length && i + offset < audio.Length; i++) audio[i + offset] += phrase[i] * gain;
        }
    }
}
