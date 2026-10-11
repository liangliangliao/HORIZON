using System;
using System.Linq;

namespace Horizon.Game
{
    public enum RewardSpectacleStyle { Quiet, Golden, Mystery, Legendary, Loss }

    // Visual choreography reads the same immutable receipt and director clock as
    // the existing movie. Decorative motes and the multiplier never settle rewards.
    public sealed class RewardSpectacle
    {
        public readonly RewardPlan Plan;
        public readonly RewardSpectacleStyle Style;
        public readonly MaterialReward HeroReward;
        public readonly float ImpactTime, HeroTime, SettlementTime, JackpotTime;
        public bool HasHero => Plan.Event.tier >= RewardTier.Major && Style != RewardSpectacleStyle.Quiet && Style != RewardSpectacleStyle.Loss;
        public bool HasMotes => Style == RewardSpectacleStyle.Golden || Style == RewardSpectacleStyle.Legendary;

        public RewardSpectacle(RewardPlan plan)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            ImpactTime = CueTime(CinematicPhase.Impact, plan.Duration * .52f);
            HeroTime = plan.Event.tier >= RewardTier.Epic ? CueTime(CinematicPhase.SecondReveal, ImpactTime) : ImpactTime;
            SettlementTime = CueTime(CinematicPhase.Settlement, plan.Duration * .86f);
            JackpotTime = CueTime(CinematicPhase.Jackpot, HeroTime);
            Style = SelectStyle(plan);
            HeroReward = plan.Objects.FirstOrDefault(o => o.Class == RewardObjectClass.Milestone) ??
                plan.Objects.FirstOrDefault(o => o.Class == RewardObjectClass.System && o.Kind != RewardObjectKind.PredictionPanel) ??
                plan.Objects.FirstOrDefault(o => o.Class == RewardObjectClass.Resource && o.Amount > 0);
        }

        private float CueTime(CinematicPhase phase, float fallback)
        { return Plan.Cues.FirstOrDefault(c => !c.NodeHit && c.Phase == phase)?.Time ?? fallback; }

        private static RewardSpectacleStyle SelectStyle(RewardPlan plan)
        {
            DomainEventKind kind = plan.Event.kind;
            bool positive = plan.Objects.Any(o => o.Class == RewardObjectClass.Resource && o.Amount > 0);
            bool negative = plan.Objects.Any(o => o.Class == RewardObjectClass.Resource && o.Amount < 0);
            if (kind == DomainEventKind.FailAndAgain || negative && !positive) return RewardSpectacleStyle.Loss;
            if (kind == DomainEventKind.PredictionLocked || kind == DomainEventKind.DecisionLocked ||
                kind == DomainEventKind.DecisionReopened || kind == DomainEventKind.VictoryAnchor ||
                kind == DomainEventKind.TriggerEquipped) return RewardSpectacleStyle.Quiet;
            if (kind == DomainEventKind.Surprise || kind == DomainEventKind.DejaVu ||
                kind == DomainEventKind.TimeEcho && plan.Event.receipt != null && plan.Event.receipt.sourceDay == 0)
                return RewardSpectacleStyle.Mystery;
            if (plan.Event.tier == RewardTier.Mythic) return RewardSpectacleStyle.Legendary;
            return RewardSpectacleStyle.Golden;
        }

        public RewardSpectacleFrame Sample(float seconds, bool reducedMotion = false)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            float time = Math.Max(0, Math.Min(Plan.Duration, seconds));
            float fade = 1 - Ease(Plan.Duration * .94f, Plan.Duration, time);
            float after = time - ImpactTime;
            float burst = after < 0 || reducedMotion || Style == RewardSpectacleStyle.Loss || Style == RewardSpectacleStyle.Quiet ? 0 : (float)Math.Exp(-after * 5);
            float span = Math.Min(.48f, Math.Max(.025f, (Plan.Duration - HeroTime) * .2f));
            float hero = HasHero ? Ease(HeroTime, HeroTime + span, time) * fade : 0;
            float charge = Ease(0, ImpactTime, time) * fade;
            return new RewardSpectacleFrame(time, charge, burst * fade, hero,
                Ease(HeroTime, HeroTime + span * 1.6f, time), fade,
                Plan.PresentationMultiplierAt(time),
                Plan.Event.tier >= RewardTier.Combo && Style != RewardSpectacleStyle.Loss && Style != RewardSpectacleStyle.Quiet && time >= ImpactTime && fade > 0,
                (.12f * charge + .65f * burst + .28f * hero) * fade);
        }

        public int MoteBudget(bool batterySaver, int adaptiveLevel, bool reducedMotion)
        {
            if (reducedMotion || !HasMotes || Plan.Event.tier == RewardTier.Micro) return 0;
            int normal = Plan.Event.tier == RewardTier.Local ? 8 : Plan.Event.tier == RewardTier.Combo ? 16 :
                Plan.Event.tier == RewardTier.Major ? 24 : Plan.Event.tier == RewardTier.Epic ? 40 : 48;
            return Math.Min(normal, batterySaver ? 8 : adaptiveLevel >= 2 ? 12 : adaptiveLevel == 1 ? 20 : 48);
        }

        private static float Ease(float from, float to, float time)
        { float p = Math.Max(0, Math.Min(1, (time - from) / Math.Max(.0001f, to - from))); return p * p * (3 - 2 * p); }
    }

    public readonly struct RewardSpectacleFrame
    {
        public readonly float Time, Charge, Burst, Hero, Lid, Fade, Light;
        public readonly int Multiplier;
        public readonly bool ShowMultiplier;
        public RewardSpectacleFrame(float time, float charge, float burst, float hero, float lid, float fade, int multiplier, bool showMultiplier, float light)
        { Time = time; Charge = charge; Burst = burst; Hero = hero; Lid = lid; Fade = fade; Multiplier = multiplier; ShowMultiplier = showMultiplier; Light = light; }
    }
}
