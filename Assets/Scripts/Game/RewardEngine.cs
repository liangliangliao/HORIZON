using System;

namespace Horizon.Game
{
    // Pure rules. Presentation, sound, haptics and analytics all consume the same immutable receipt.
    public static class RewardEngine
    {
        public static RewardTier Tier(DomainEventKind kind, int chain = 0)
        {
            if (kind == DomainEventKind.PatternBroken || kind == DomainEventKind.RealityConvergence) return RewardTier.Mythic;
            if (kind == DomainEventKind.Cascade || kind == DomainEventKind.CausalSingularity || kind == DomainEventKind.Overdrive ||
                kind == DomainEventKind.Breakthrough || kind == DomainEventKind.Victory || kind == DomainEventKind.AllLinked) return RewardTier.Epic;
            if (kind == DomainEventKind.TimeEcho || kind == DomainEventKind.VictoryAnchor || kind == DomainEventKind.Comeback || kind == DomainEventKind.FutureMemory) return RewardTier.Major;
            return chain >= 3 ? RewardTier.Combo : kind == DomainEventKind.ActionTaken || kind == DomainEventKind.ExecutionStep ? RewardTier.Micro : RewardTier.Local;
        }
        public static DomainEvent Emit(MasterRunState state, int run, int day, DomainEventKind kind, string node,
            string title, string detail = "", int chain = 0)
        {
            RewardTier tier = Tier(kind, chain);
            if (tier == RewardTier.Mythic && state.mythicCount >= MasterSpecification.MythicLimit) tier = RewardTier.Major;
            else if (tier == RewardTier.Mythic) state.mythicCount++;
            var e = new DomainEvent { id = "run:" + run + ":v42:" + state.events.Count, day = day, kind = kind, tier = tier,
                nodeId = node, title = title, detail = detail, chainSize = chain, multiplier = state.resilienceChain };
            state.events.Add(e); return e;
        }
        public static bool Acknowledge(MasterRunState state, string id)
        { var e = state.events.Find(x => x.id == id); if (e == null || e.acknowledged) return false; e.acknowledged = true; return true; }
    }
}
