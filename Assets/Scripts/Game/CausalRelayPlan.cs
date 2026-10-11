using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    // Presentation data only: one relay for each observed receipt node, with
    // the same cue time used by sound and haptics. No simulation references.
    public sealed class CausalRelayPlan
    {
        public const int MaximumNodes = 24, MaximumEdges = 64;
        public readonly IReadOnlyList<CausalRelayNode> Nodes;
        public readonly IReadOnlyList<CausalRelayEdge> Edges;
        public readonly float ImpactTime, Duration;
        public CausalRelayPlan(RewardPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            Duration = plan.Duration;
            ImpactTime = plan.Cues.First(c => !c.NodeHit && c.Phase == CinematicPhase.Impact).Time;
            bool cascade = plan.Event.kind == DomainEventKind.Cascade || plan.Event.kind == DomainEventKind.CausalSingularity;
            var nodes = new List<CausalRelayNode>(); var edges = new List<CausalRelayEdge>();
            if (cascade)
            {
                List<RewardEvidence> evidence = (plan.Event.receipt?.causes ?? new List<RewardEvidence>())
                    .Take(MaximumNodes).Select(x => x.Copy()).ToList();
                for (int i = 0; i < evidence.Count; i++)
                {
                    RewardEvidence source = evidence[i];
                    var peers = evidence.Where(x => x.day == source.day).ToList();
                    int lane = peers.IndexOf(source);
                    float x = peers.Count == 1 ? (float)Math.Sin(i*2.39996f)*1.2f : (lane / (float)(peers.Count - 1) - .5f) * 2.6f;
                    int first = evidence.Min(n => n.day), last = evidence.Max(n => n.day);
                    float z = first == last ? 2 : -1 + (source.day - first) / (float)(last - first) * 6;
                    // Same-day nodes retain depth separation, even when their
                    // sources are independent. The edges carry causality.
                    float y = .65f + lane * .055f;
                    float at = plan.Cues.FirstOrDefault(c => c.NodeHit && c.NodeIndex == i)?.Time ?? ImpactTime;
                    nodes.Add(new CausalRelayNode(i, source, x, y, z, at));
                }
                var indices = new Dictionary<string, int>();
                foreach (CausalRelayNode node in nodes)
                    if (!string.IsNullOrEmpty(node.Evidence.id) && !indices.ContainsKey(node.Evidence.id)) indices.Add(node.Evidence.id, node.Index);
                foreach (CausalRelayNode node in nodes)
                    foreach (string parent in (node.Evidence.parentIds ?? new List<string>()).Distinct())
                        if (edges.Count < MaximumEdges && parent != node.Evidence.id && indices.TryGetValue(parent, out int from))
                            edges.Add(new CausalRelayEdge(from, node.Index));
            }
            Nodes = nodes.AsReadOnly(); Edges = edges.AsReadOnly();
        }

        public CausalRelayFrame Sample(int index, float seconds, bool reducedMotion)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            CausalRelayNode node = Nodes[index]; float time = Math.Max(0, Math.Min(Duration, seconds));
            float fade = 1 - Ease(Duration * .94f, Duration, time);
            float age = time - node.ActivationTime;
            bool lit = age >= 0;
            float pulse = !lit || reducedMotion ? 0 : (float)Math.Exp(-age * 4);
            float final = time < ImpactTime || reducedMotion ? 0 : (float)Math.Exp(-(time - ImpactTime) * 3.5f);
            float beam = lit && !reducedMotion ? Math.Max(pulse, final) * fade : 0;
            return new CausalRelayFrame(lit, fade, pulse * fade, beam,
                reducedMotion ? 0 : (2.2f + 1.0f * Math.Max(pulse, final)) * fade);
        }
        private static float Ease(float a, float b, float t)
        { float p = Math.Max(0, Math.Min(1, (t - a) / Math.Max(.0001f, b - a))); return p * p * (3 - 2 * p); }
    }
    public sealed class CausalRelayNode
    {
        public readonly int Index;
        public readonly RewardEvidence Evidence;
        public readonly float X, Y, Z, ActivationTime;
        public CausalRelayNode(int index, RewardEvidence evidence, float x, float y, float z, float activationTime)
        { Index = index; Evidence = evidence; X = x; Y = y; Z = z; ActivationTime = activationTime; }
    }
    public sealed class CausalRelayEdge
    {
        public readonly int From, To;
        public CausalRelayEdge(int from, int to) { From = from; To = to; }
    }
    public readonly struct CausalRelayFrame
    {
        public readonly bool Lit;
        public readonly float Fade, Pulse, Beam, Height;
        public CausalRelayFrame(bool lit, float fade, float pulse, float beam, float height)
        { Lit = lit; Fade = fade; Pulse = pulse; Beam = beam; Height = height; }
    }
}
