using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    public enum CinematicFraming { Wide, Medium, Close, ObjectDetail, ExtremeWide }
    public enum PatternShot { Arrival, FirstHistory, SharedInterruption, PresentRoute, DifferentAction, PatternCrack,
        Silence, CrossInterruption, ShatterHistory, ExtendFuture, FutureSelf, Memento }
    public sealed class CinematicShot
    {
        public readonly float Time;
        public readonly PatternShot Beat;
        public readonly CinematicFraming Framing;
        public CinematicShot(float time, PatternShot beat, CinematicFraming framing)
        { Time=time; Beat=beat; Framing=framing; }
    }
    // Editorial timing is presentation data. It only reads immutable receipts;
    // a shot can never manufacture an action, historical failure or multiplier.
    public static class CinematicStoryboard
    {
        public static IReadOnlyList<CinematicShot> Pattern(RewardPlan plan)
        {
            float stop=plan.Cues.FirstOrDefault(c=>!c.NodeHit && c.Phase==CinematicPhase.HitStop)?.Time ?? plan.Duration*.43f;
            float hit=plan.Cues.First(c=>!c.NodeHit && c.Phase==CinematicPhase.Impact).Time;
            float tail=plan.Duration-hit;
            return new[] {
                new CinematicShot(0,PatternShot.Arrival,CinematicFraming.Wide),
                new CinematicShot(stop*.20f,PatternShot.FirstHistory,CinematicFraming.Medium),
                new CinematicShot(stop*.40f,PatternShot.SharedInterruption,CinematicFraming.Wide),
                new CinematicShot(stop*.60f,PatternShot.PresentRoute,CinematicFraming.Medium),
                new CinematicShot(stop*.73f,PatternShot.DifferentAction,CinematicFraming.Close),
                new CinematicShot(stop*.87f,PatternShot.PatternCrack,CinematicFraming.ObjectDetail),
                new CinematicShot(stop,PatternShot.Silence,CinematicFraming.Close),
                new CinematicShot(hit,PatternShot.CrossInterruption,CinematicFraming.Medium),
                new CinematicShot(hit+tail*.15f,PatternShot.ShatterHistory,CinematicFraming.Wide),
                new CinematicShot(hit+tail*.31f,PatternShot.ExtendFuture,CinematicFraming.ExtremeWide),
                new CinematicShot(hit+tail*.52f,PatternShot.FutureSelf,CinematicFraming.Medium),
                new CinematicShot(hit+tail*.74f,PatternShot.Memento,CinematicFraming.ObjectDetail)
            };
        }
        public static bool AllowsExtremeWide(DomainEventKind kind)
        { return kind==DomainEventKind.PatternBroken || kind==DomainEventKind.RealityConvergence || kind==DomainEventKind.CausalSingularity; }
        public static int DayDuringRewind(DomainEvent e,float seconds)
        {
            if ((e.receipt?.sourceDay ?? 0) <= 0) return e.day;
            int from=Math.Max(e.day,e.receipt?.sourceDay??e.day), to=Math.Max(1,e.receipt?.sourceDay??e.day);
            float p=Math.Max(0,Math.Min(1,(seconds-.35f)/.45f));
            return from-(int)Math.Floor((from-to)*p+.0001f);
        }
    }
}
