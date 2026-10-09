using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    public enum RewardObjectKind { EnergyCell, FocusLens, WarmLamp, MoneyWallet, ConnectionRing, ToolKit,
        InsightPrism, HorizonLens, PredictionPanel, Projector, RouteLock, TriggerObject, RepairKit,
        MemoryFilm, BrokenLink, RealityMilestone, ConvergencePrism, CausalChain, OrbitNode, ReservoirCore }
    public enum RewardObjectClass { Resource, System, Milestone }
    public enum RewardObjectForm { Default, Cell, ParallelCells, CellPack, BatteryPack, StorageUnit, EnergyCore,
        SingleLens, DoubleLens, OpticalDevice, Coin, Wallet, CashBundle, Vault, Tool, ToolBox }
    public enum TriggerAppearance { Alarm, Key, Appointment, Ticket, Countdown, Reminder, Commitment, Deposit }
    public enum CinematicPhase { Anticipation, Charge, Escalation, HeroMoment, HitStop, Impact,
        Multiplication, SecondReveal, Jackpot, Settlement, ReturnToGame }

    // Persisted with the domain event, before observers run. Never read mutable game
    // state during a movie, and never manufacture historical failures or resource gains.
    [Serializable]
    public sealed class RewardEvidence
    {
        public string id, label;
        public int day;
        public CausalNodeKind kind;
        public RewardEvidence Copy() { return (RewardEvidence)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class RewardReceipt
    {
        public bool resourcesRecorded;
        public bool predictionRecorded, predictionResolved;
        public ResourceDelta predicted = new ResourceDelta(), actual = new ResourceDelta();
        public ResourceDelta resources = new ResourceDelta();
        public string action, triggerId;
        public int sourceDay, orbitBits, orbitIndex = -1, horizonLevel;
        public List<RewardEvidence> causes = new List<RewardEvidence>();
        public List<RewardEvidence> pastFailures = new List<RewardEvidence>();
        public List<MemoryFrame> frames = new List<MemoryFrame>();
        public RewardReceipt Copy()
        {
            var copy = (RewardReceipt)MemberwiseClone(); copy.resources = ResourceMath.Copy(resources);
            copy.predicted = ResourceMath.Copy(predicted); copy.actual = ResourceMath.Copy(actual);
            copy.causes = (causes ?? new List<RewardEvidence>()).Select(x => x.Copy()).ToList();
            copy.frames = MemoryFrame.CopyFrames(frames);
            copy.pastFailures = (pastFailures ?? new List<RewardEvidence>()).Select(x => x.Copy()).ToList(); return copy;
        }
        public static RewardReceipt Capture(DomainEventKind kind, CausalNode node, IList<CausalNode> graph,
            int orbitBits, int horizon, string trigger)
        {
            var receipt = new RewardReceipt { orbitBits = orbitBits, horizonLevel = horizon, triggerId = trigger };
            if (node == null) return receipt;
            if (node.effectRecorded && (kind == DomainEventKind.ActionTaken || kind == DomainEventKind.TimeEcho || kind == DomainEventKind.Breakthrough))
            { receipt.resourcesRecorded = true; receipt.resources = ResourceMath.Copy(node.effect); }
            var observed = CausalGraph.ObservedGraph(graph.ToList());
            var causes = CausalGraph.Ancestors(observed, node.id).OrderBy(n => n.day).ThenBy(n => n.depth).ToList();
            foreach (CausalNode n in causes)
            {
                var evidence = new RewardEvidence { id = n.id, day = n.day, kind = n.type, label = n.label };
                if (n.type == CausalNodeKind.Memory && kind == DomainEventKind.PatternBroken) receipt.pastFailures.Add(evidence);
                else receipt.causes.Add(evidence);
            }
            CausalNode action = node.type == CausalNodeKind.Action ? node : causes.LastOrDefault(n => n.type == CausalNodeKind.Action);
            receipt.action = action?.label ?? node.label; receipt.sourceDay = action?.day ?? node.day;
            if (node.originHidden) { receipt.sourceDay = 0; receipt.action = "暂未显现的来路"; }
            return receipt;
        }
    }

    public sealed class MaterialReward
    {
        public readonly RewardObjectKind Kind;
        public readonly RewardObjectClass Class;
        public readonly int Amount, VisualCount, Scale;
        public readonly RewardObjectForm Form;
        public readonly string Copy;
        public MaterialReward(RewardObjectKind kind, RewardObjectClass category, string copy, int amount = 0)
        {
            Kind = kind; Class = category; Copy = copy; Amount = amount;
            long magnitude = Math.Abs((long)amount);
            Form = FormFor(kind, magnitude);
            // Large receipts become a device, rather than hundreds of meshes.
            VisualCount = category == RewardObjectClass.Resource ? magnitude <= 5 ? (int)magnitude : magnitude < 20 ? 3 : 1 : 1;
            Scale = magnitude >= 100 ? 4 : magnitude >= 50 ? 3 : magnitude >= 20 ? 2 : magnitude >= 5 ? 1 : 0;
            if (kind == RewardObjectKind.EnergyCell && magnitude >= 10 ||
                kind == RewardObjectKind.FocusLens && magnitude >= 5 || kind == RewardObjectKind.MoneyWallet ||
                kind == RewardObjectKind.ToolKit) VisualCount = magnitude == 0 && category == RewardObjectClass.Resource ? 0 : 1;
            if (kind == RewardObjectKind.WarmLamp && magnitude >= 5) VisualCount = 5;
        }
        private static RewardObjectForm FormFor(RewardObjectKind kind, long magnitude)
        {
            if (kind == RewardObjectKind.EnergyCell) return magnitude >= 100 ? RewardObjectForm.EnergyCore : magnitude >= 20 ? RewardObjectForm.StorageUnit :
                magnitude >= 10 ? RewardObjectForm.BatteryPack : magnitude >= 5 ? RewardObjectForm.CellPack : magnitude >= 2 ? RewardObjectForm.ParallelCells : RewardObjectForm.Cell;
            if (kind == RewardObjectKind.FocusLens) return magnitude >= 5 ? RewardObjectForm.OpticalDevice : magnitude >= 2 ? RewardObjectForm.DoubleLens : RewardObjectForm.SingleLens;
            if (kind == RewardObjectKind.MoneyWallet) return magnitude >= 20 ? RewardObjectForm.Vault : magnitude >= 5 ? RewardObjectForm.CashBundle : magnitude >= 2 ? RewardObjectForm.Wallet : RewardObjectForm.Coin;
            if (kind == RewardObjectKind.ToolKit) return magnitude >= 5 ? RewardObjectForm.ToolBox : RewardObjectForm.Tool;
            return RewardObjectForm.Default;
        }
    }
    public sealed class PresentationBeat
    {
        public readonly float Time;
        public readonly int Multiplier;
        public string Copy { get { return "演出 ×" + Multiplier; } }
        public PresentationBeat(float time, int multiplier) { Time = time; Multiplier = multiplier; }
    }
    public sealed class CinematicCue
    {
        public readonly float Time;
        public readonly CinematicPhase Phase;
        public readonly string Copy;
        public readonly int NodeIndex;
        public readonly bool NodeHit;
        public CinematicCue(float time, CinematicPhase phase, string copy = "", int nodeIndex = -1)
        { Time = time; Phase = phase; Copy = copy; NodeIndex = nodeIndex; NodeHit = nodeIndex >= 0; }
    }
    public sealed class RewardPlan
    {
        public readonly DomainEvent Event;
        public readonly IReadOnlyList<MaterialReward> Objects;
        public readonly IReadOnlyList<CinematicCue> Cues;
        public readonly float Duration;
        public readonly int PresentationMultiplier;
        public readonly IReadOnlyList<PresentationBeat> PresentationBeats;
        public RewardPlan(DomainEvent e, List<MaterialReward> objects, List<CinematicCue> cues, float duration, int multiplier, List<PresentationBeat> beats = null)
        { Event = e.Copy(); Objects = objects.AsReadOnly(); Cues = cues.OrderBy(c => c.Time).ToList().AsReadOnly(); Duration = duration; PresentationMultiplier = multiplier;
            PresentationBeats = (beats ?? new List<PresentationBeat> { new PresentationBeat(0, multiplier) }).AsReadOnly(); }
        public int PresentationMultiplierAt(float time)
        { int value = 1; foreach (PresentationBeat beat in PresentationBeats) { if (beat.Time > time) break; value = beat.Multiplier; } return value; }
        public string ResourceCopy { get { return string.Join(" · ", Objects.Where(o => o.Class == RewardObjectClass.Resource).Select(o => o.Copy)); } }
    }

    // Presentation only: no wallet, session, RNG, probability or ApplyEffect access.
    public static class RewardDirector
    {
        public const string Version = "1.0";
        public static RewardPlan Direct(DomainEvent source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            DomainEvent e = source.Copy(); RewardReceipt r = e.receipt ?? new RewardReceipt();
            var objects = new List<MaterialReward>();
            if (r.resourcesRecorded)
            {
                int[] axes = ResourceMath.Axes(r.resources);
                string[] names = { "精力", "心情", "专注", "金钱", "关系", "能力" };
                RewardObjectKind[] kinds = { RewardObjectKind.EnergyCell, RewardObjectKind.WarmLamp, RewardObjectKind.FocusLens,
                    RewardObjectKind.MoneyWallet, RewardObjectKind.ConnectionRing, RewardObjectKind.ToolKit };
                for (int i = 0; i < axes.Length; i++) if (axes[i] != 0)
                    objects.Add(new MaterialReward(kinds[i], RewardObjectClass.Resource, names[i] + " " + (axes[i] > 0 ? "+" : "") + axes[i], axes[i]));
            }
            RewardObjectKind? system = ObjectFor(e.kind);
            if (system.HasValue) objects.Add(new MaterialReward(system.Value, IsMilestone(e.kind) ? RewardObjectClass.Milestone : RewardObjectClass.System, e.title));
            if (r.predictionRecorded && (e.kind == DomainEventKind.Synchronized || e.kind == DomainEventKind.Surprise))
                objects.Add(new MaterialReward(RewardObjectKind.PredictionPanel, RewardObjectClass.System, e.detail));
            float duration = Duration(e.tier);
            int multiplier = PresentationStrength(e);
            var cues = new List<CinematicCue>();
            if (e.tier >= RewardTier.Epic)
            {
                string[] copy = Story(e, r);
                float[] fractions = { 0, .09f, .20f, .31f, .43f, .45f, .54f, .63f, .75f, .86f, 1 };
                for (int i = 0; i < fractions.Length; i++) cues.Add(new CinematicCue(fractions[i] * duration, (CinematicPhase)i, copy[i]));
                if (e.kind == DomainEventKind.RealityConvergence)
                { cues.RemoveAll(c => c.Phase == CinematicPhase.HitStop); cues.Add(new CinematicCue(duration * .45f - .2f, CinematicPhase.HitStop, copy[4])); }
                // One cue fires all senses on the same frame. Hit-stop ends at Impact.
                if (e.kind == DomainEventKind.Cascade || e.kind == DomainEventKind.CausalSingularity)
                {
                    int count = Math.Min(16, r.causes.Count);
                    float time = duration * .20f;
                    for (int i = 0; i < count; i++)
                    { cues.Add(new CinematicCue(time, CinematicPhase.Escalation, r.causes[i].label, i)); time += i == 0 ? .35f : i == 1 ? .28f : i == 2 ? .21f : .15f; }
                    // Final network impact follows the final node and a deliberate 150ms silence.
                    cues.RemoveAll(c => !c.NodeHit && c.Phase >= CinematicPhase.HeroMoment);
                    float hit = count == 0 ? duration * .45f : cues.Where(c => c.NodeHit).Max(c => c.Time) + .001f;
                    cues.Add(new CinematicCue(Math.Max(0, hit - .0005f), CinematicPhase.HeroMoment, e.title));
                    cues.Add(new CinematicCue(hit, CinematicPhase.HitStop, "每一条光，都有真实的来路。"));
                    cues.Add(new CinematicCue(hit + .15f, CinematicPhase.Impact, e.detail));
                    float remaining = duration - hit - .15f;
                    cues.Add(new CinematicCue(hit + .15f + remaining * .15f, CinematicPhase.Multiplication, "真实因果深度 ×" + Math.Max(1, e.chainSize)));
                    cues.Add(new CinematicCue(hit + .15f + remaining * .36f, CinematicPhase.SecondReveal, "整个时间网络被点亮。"));
                    cues.Add(new CinematicCue(hit + .15f + remaining * .58f, CinematicPhase.Jackpot, e.title));
                    cues.Add(new CinematicCue(hit + .15f + remaining * .8f, CinematicPhase.Settlement, e.detail));
                    cues.Add(new CinematicCue(duration, CinematicPhase.ReturnToGame));
                }
            }
            else if (e.kind == DomainEventKind.TimeEcho)
            {
                cues.Add(new CinematicCue(0, CinematicPhase.Anticipation, "TIME ECHO"));
                cues.Add(new CinematicCue(.12f, CinematicPhase.Charge, "沿时间线，回到原来的行动。"));
                cues.Add(new CinematicCue(.35f, CinematicPhase.Escalation, r.sourceDay > 0 ? "D" + r.sourceDay + " · " + r.action : e.detail));
                cues.Add(new CinematicCue(.8f, CinematicPhase.HitStop, r.sourceDay > 0 ? "这个结果来自 D" + r.sourceDay + " 的行动。" : e.detail));
                cues.Add(new CinematicCue(1.05f, CinematicPhase.HeroMoment, e.detail));
                cues.Add(new CinematicCue(2.15f, CinematicPhase.Impact, string.Join(" · ", objects.Select(o => o.Copy))));
                cues.Add(new CinematicCue(2.5f, CinematicPhase.Settlement, r.sourceDay > 0 ? "这个结果来自 Day " + r.sourceDay + " 的行动。\n" + e.detail : e.detail));
                cues.Add(new CinematicCue(duration, CinematicPhase.ReturnToGame));
            }
            else
            {
                cues.Add(new CinematicCue(0, CinematicPhase.Anticipation, e.detail));
                cues.Add(new CinematicCue(duration * .32f, CinematicPhase.HeroMoment, e.title));
                cues.Add(new CinematicCue(duration * .52f, CinematicPhase.Impact, e.detail));
                cues.Add(new CinematicCue(duration * .78f, CinematicPhase.Settlement, e.detail));
                cues.Add(new CinematicCue(duration, CinematicPhase.ReturnToGame));
            }
            if(e.kind==DomainEventKind.Synchronized && e.tier<RewardTier.Epic)
                cues.Add(new CinematicCue(duration*.52f-.1f,CinematicPhase.HitStop,"预测与现实，正在重合。"));
            if(e.tier<RewardTier.Epic && (e.kind==DomainEventKind.RealityConvergence || e.kind==DomainEventKind.PatternBroken))
                cues.Add(new CinematicCue(Math.Max(0,duration*.52f-(e.kind==DomainEventKind.RealityConvergence?.2f:.12f)),
                    CinematicPhase.HitStop,e.kind==DomainEventKind.RealityConvergence?"三条路径锁定同一个事件。":"这一刻，时间停住。"));
            var beats = new List<PresentationBeat>();
            if (e.kind == DomainEventKind.PatternBroken && e.tier == RewardTier.Mythic)
            {
                beats.Add(new PresentationBeat(duration * .09f, 2)); beats.Add(new PresentationBeat(duration * .20f, 5));
                beats.Add(new PresentationBeat(duration * .40f, 20)); beats.Add(new PresentationBeat(duration * .54f, 100));
                if (multiplier > 100) beats.Add(new PresentationBeat(duration * .75f, multiplier));
            }
            else { beats.Add(new PresentationBeat(0, 1)); beats.Add(new PresentationBeat(cues.First(c => c.Phase == CinematicPhase.Impact).Time, multiplier)); }
            return new RewardPlan(e, objects, cues, duration, multiplier, beats);
        }
        // These values control sensory intensity only. Never reuse them as causal
        // depth, resilience, resource amounts, or an input to the rule engine.
        public static int PresentationStrength(DomainEvent e)
        {
            if (e.tier == RewardTier.Mythic)
            {
                if (e.kind == DomainEventKind.CausalSingularity && e.chainSize >= 9) return e.chainSize >= 16 ? 500 : 300;
                if (e.kind == DomainEventKind.PatternBroken && e.chainSize >= 9 && (e.receipt?.pastFailures?.Count ?? 0) >= 5)
                    return e.chainSize >= 16 && e.receipt.pastFailures.Count >= 8 ? 500 : 300;
                if (e.kind == DomainEventKind.RealityConvergence && (e.receipt?.causes?.Select(x => x.kind).Distinct().Count() ?? 0) >= 3) return 300;
                return 100;
            }
            return e.tier == RewardTier.Epic ? 50 : e.tier == RewardTier.Major ? 20 : e.tier == RewardTier.Combo ? 5 : e.tier == RewardTier.Local ? 2 : 1;
        }
        public static TriggerAppearance TriggerForm(string id)
        {
            switch (id)
            {
                case "appointment": return TriggerAppearance.Appointment;
                case "ticket": return TriggerAppearance.Ticket;
                case "route": case "place": case "environment": return TriggerAppearance.Key;
                case "deadline": return TriggerAppearance.Countdown;
                case "friend": return TriggerAppearance.Reminder;
                case "promise": return TriggerAppearance.Commitment;
                case "deposit": return TriggerAppearance.Deposit;
                default: return TriggerAppearance.Alarm;
            }
        }
        public static float Duration(RewardTier tier)
        { switch (tier) { case RewardTier.Micro: return .24f; case RewardTier.Local: return .75f; case RewardTier.Combo: return 1.6f; case RewardTier.Major: return 3.6f; case RewardTier.Epic: return 6.8f; default: return 11.8f; } }
        public static RewardObjectKind? ObjectFor(DomainEventKind kind)
        {
            switch (kind)
            {
                case DomainEventKind.PredictionLocked: return RewardObjectKind.PredictionPanel;
                case DomainEventKind.Synchronized: case DomainEventKind.Surprise: return RewardObjectKind.InsightPrism;
                case DomainEventKind.DecisionLocked: case DomainEventKind.DecisionReopened: return RewardObjectKind.RouteLock;
                case DomainEventKind.TriggerEquipped: return RewardObjectKind.TriggerObject;
                case DomainEventKind.FailAndAgain: case DomainEventKind.Comeback: return RewardObjectKind.RepairKit;
                case DomainEventKind.VictoryAnchor: return RewardObjectKind.Projector;
                case DomainEventKind.FutureMemory: case DomainEventKind.DejaVu: return RewardObjectKind.MemoryFilm;
                case DomainEventKind.PatternBroken: return RewardObjectKind.BrokenLink;
                case DomainEventKind.RealityNode: return RewardObjectKind.RealityMilestone;
                case DomainEventKind.RealityConvergence: return RewardObjectKind.ConvergencePrism;
                case DomainEventKind.Overdrive: case DomainEventKind.HorizonChanged: return RewardObjectKind.HorizonLens;
                case DomainEventKind.Cascade: case DomainEventKind.CausalSingularity: return RewardObjectKind.CausalChain;
                case DomainEventKind.OrbitActivated: case DomainEventKind.AllLinked: return RewardObjectKind.OrbitNode;
                case DomainEventKind.Breakthrough: return RewardObjectKind.ReservoirCore;
                default: return null;
            }
        }
        public static bool IsMilestone(DomainEventKind kind)
        { return kind == DomainEventKind.PatternBroken || kind == DomainEventKind.RealityConvergence || kind == DomainEventKind.RealityNode ||
            kind == DomainEventKind.FutureMemory || kind == DomainEventKind.HorizonChanged || kind == DomainEventKind.AllLinked ||
            kind == DomainEventKind.OrbitActivated || kind == DomainEventKind.Cascade || kind == DomainEventKind.CausalSingularity; }
        private static string[] Story(DomainEvent e, RewardReceipt r)
        {
            if (e.kind == DomainEventKind.PatternBroken) return new[] {
                "你又一次来到了这里。", "过去的时间线，从身后浮现。", "过去，你多次在这里中断。", r.action ?? e.detail,
                "这一刻，时间停住。", "这一次，你穿过了旧中断点。", "韧性链 ×" + Math.Max(1, e.multiplier),
                "旧路线碎裂，当前路线继续生成。", "过去多次在这里停下。这一次，你没有。", "模式突破纪念物已留下。", "" };
            if (e.kind == DomainEventKind.RealityConvergence) return new[] {
                "IMAGINATION · SIMULATION · REALITY", "你曾经想象。", "你曾经预演。", "现在，它真实发生了。", "三条路径锁定同一个事件。",
                "想象、模拟与现实，真正重合。", e.detail, "三条已有路径，汇聚成一个纪念体。", "REALITY CONVERGENCE", "现实节点将永久留在你的星座。", "" };
            if (e.kind == DomainEventKind.Overdrive) return new[] {
                "远处的未来，开始清晰。", "噪点减少，因果线浮现。", "你能看见更多隐藏的来路。", "短时间，看得极清楚。", "视野展开。", e.title, e.detail,
                "过去与未来，第一次同时清晰。", e.title, e.detail, "" };
            return new[] { e.title, "积累的因果，开始回应。", e.detail, e.title, "每一次改变，都有来路。", e.detail,
                e.chainSize > 0 ? "真实因果深度 ×" + e.chainSize : e.detail, "整个时间网络被点亮。", e.title, e.detail, "" };
        }
    }

    // Pure, pauseable clock. Drains crossed cues once even after a slow frame.
    // Skipping discards sensory cues; it does not replay impact or grant a reward.
    public sealed class CinematicClock
    {
        public readonly RewardPlan Plan;
        public float Elapsed { get; private set; }
        public bool Paused { get; private set; }
        public bool Finished { get; private set; }
        private int next;
        public CinematicClock(RewardPlan plan) { Plan = plan ?? throw new ArgumentNullException(nameof(plan)); }
        public void Pause(bool value) { Paused = value; }
        public void Advance(float delta, Action<CinematicCue> deliver)
        {
            if (Paused || Finished) return;
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            Elapsed = Math.Min(Plan.Duration, Elapsed + delta);
            while (next < Plan.Cues.Count && Plan.Cues[next].Time <= Elapsed) deliver(Plan.Cues[next++]);
            Finished = Elapsed >= Plan.Duration;
        }
        public void Skip() { next = Plan.Cues.Count; Elapsed = Plan.Duration; Finished = true; }
    }
}
