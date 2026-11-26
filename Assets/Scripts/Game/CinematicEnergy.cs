using System;
using System.Linq;

namespace Horizon.Game
{
    // A contrast envelope for EVERY reward event, derived from its existing cues.
    // No additional beats, delay, random reveal, payout or claimed causal edge.
    public sealed class CinematicEnergy
    {
        private readonly RewardPlan plan;
        private readonly RewardSpectacle spectacle;
        private readonly CinematicCue[] accents;
        private readonly bool verifiedRelease;
        public readonly float Seed;
        public readonly float Strength;
        public readonly bool Celebrates;

        public CinematicEnergy(RewardPlan value)
        {
            plan=value ?? throw new ArgumentNullException(nameof(value));
            spectacle=new RewardSpectacle(plan);
            Celebrates=spectacle.Style!=RewardSpectacleStyle.Quiet && spectacle.Style!=RewardSpectacleStyle.Loss;
            bool hasGain = plan.Outcome.Changes.Any(change => change.Amount > 0);
            bool milestone = plan.Event.kind == DomainEventKind.Victory ||
                plan.Event.kind == DomainEventKind.PatternBroken || plan.Event.kind == DomainEventKind.RealityConvergence ||
                plan.Event.kind == DomainEventKind.AllLinked;
            bool cascade = (plan.Event.kind == DomainEventKind.Cascade || plan.Event.kind == DomainEventKind.CausalSingularity) &&
                plan.Event.receipt?.causes?.Count >= 3;
            verifiedRelease = hasGain || milestone || cascade;
            Seed = VisualSeed(plan.Event.id);
            Strength=new[] {.16f,.30f,.48f,.72f,.90f,1f}[Math.Max(0,Math.Min(5,(int)plan.Event.tier-1))];
            accents=plan.Cues.Where(c=>c.NodeHit || c.Phase==CinematicPhase.Impact ||
                c.Phase==CinematicPhase.SecondReveal || c.Phase==CinematicPhase.Jackpot).ToArray();
        }

        public CinematicEnergyFrame Sample(float seconds,bool reducedMotion=false)
        {
            if(float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            float time=Math.Max(0,Math.Min(plan.Duration,seconds));
            if(time>=plan.Duration) return new CinematicEnergyFrame(time,0,0,0,0,1,-1,0);
            float settle=ImmersiveMotion.Smooth((time-spectacle.SettlementTime)/Math.Max(.01f,plan.Duration-spectacle.SettlementTime));
            float charge=0,burst=0,recoil=0,last=-1;
            for(int i=0;i<accents.Length;i++)
            {
                CinematicCue cue=accents[i];
                float weight=cue.NodeHit?.25f+.25f*(cue.NodeIndex+1)/Math.Max(1,plan.Event.receipt?.causes.Count??1):
                    cue.Phase==CinematicPhase.Jackpot?1:cue.Phase==CinematicPhase.SecondReveal?.86f:.72f;
                // A sub-second charge looked like another flash on a phone.
                // Major releases now grow for long enough to be read, while
                // individual causal nodes keep their quicker relay rhythm.
                float window=cue.NodeHit?.95f:Math.Min(5.2f,Math.Max(2.8f,plan.Duration*.28f));
                float from=Math.Max(i==0?0:accents[i-1].Time+.06f,cue.Time-window);
                if(time>=from && time<cue.Time)
                    {
                    float p=Math.Max(0,Math.Min(1,(time-from)/Math.Max(.001f,cue.Time-from)));
                    charge=Math.Max(charge,ChargeAt(p,Seed,cue.NodeHit)*weight);
                }
                float age=time-cue.Time;
                if(age<0) continue;
                last=cue.Time;
                burst=Math.Max(burst,weight*(float)Math.Exp(-age/(cue.NodeHit?.32f:.62f)));
                recoil+=ImmersiveMotion.Punch(age,cue.NodeHit?.22f:.46f)*weight;
            }
            // Keep a visible accumulated charge BETWEEN actual causal hits.
            // Each hit adds to the same field, then the final release opens it.
            int nodes=accents.Count(c=>c.NodeHit),lit=accents.Count(c=>c.NodeHit && c.Time<=time);
            if(nodes>0 && time<spectacle.ImpactTime)
                charge=Math.Max(charge,.14f+.58f*lit/nodes);
            float afterglow=ImmersiveMotion.Smooth((time-spectacle.ImpactTime)/.4f)*(1-settle);
            float focus=charge*(1-settle);
            if(!Celebrates) return new CinematicEnergyFrame(time,0,0,.14f*afterglow,0,settle,last,0);
            if(reducedMotion) return new CinematicEnergyFrame(time,0,0,.18f*afterglow,0,settle,last,0);
            // Only actual positive results receive the new celebration layers.
            // Node hits keep their original relay accents; only the main impact flashes.
            var release=verifiedRelease && (plan.Event.tier>=RewardTier.Combo || plan.RecordedEcho)
                ? plan.RecordedEcho ? RecordedEchoRelease(time-spectacle.ImpactTime) :
                    EnergyRelease.Sample(time-spectacle.ImpactTime,Strength*(1-settle))
                : default(EnergyRelease);
            return new CinematicEnergyFrame(time,charge*Strength,burst*Strength*(1-settle),
                afterglow*Strength,focus*Strength,settle,last,Math.Max(-1,Math.Min(1,recoil))*Strength,release);
        }

        public static EnergyRelease RecordedEchoRelease(float age) => EnergyRelease.SampleScenic(age,.84f);

        // A quiet onset, independent local pulses and a brief hold at the end.
        // Time and seed are immutable presentation inputs.
        public static float ChargeAt(float progress,float seed,bool relay=false)
        {
            float p=Math.Max(0,Math.Min(1,progress));
            float rise=ImmersiveMotion.Smooth(p/.94f);
            if(relay) return rise;
            float pulse=(float)(Math.Sin(p*34+seed*4)*.18+Math.Sin(p*59)*.065)*4*p*(1-p);
            return Math.Max(0,Math.Min(1,rise*(1+pulse)));
        }

        public static float VisualSeed(string id)
        {
            uint hash=2166136261u;
            unchecked { foreach(char c in id??"") hash=(hash^c)*16777619u; }
            return (hash&4095)/4096f;
        }

    }

    // Separate white flash, fire volume and expanding wave, sampled by age.
    // No RNG, wall clock, or business-state access.
    public readonly struct EnergyRelease
    {
        public readonly float Age, Flash, Fire, Wave;
        private EnergyRelease(float age,float flash,float fire,float wave)
        { Age=age; Flash=flash; Fire=fire; Wave=wave; }
        public static EnergyRelease Sample(float age,float strength)
        {
            if(age<0 || age>=1.35f || strength<=0) return default(EnergyRelease);
            strength=Math.Max(0,Math.Min(1,strength));
            float flash=age<.12f?strength*(1-age/.12f)*(1-age/.12f):0;
            float fire=strength*ImmersiveMotion.Smooth((age-.025f)/.14f)*
                (1-ImmersiveMotion.Smooth((age-.28f)/.62f));
            float wave=strength*ImmersiveMotion.Smooth((age-.08f)/.12f)*
                (1-ImmersiveMotion.Smooth((age-.36f)/.99f));
            return new EnergyRelease(age,flash,fire,wave);
        }

        // Close-up room reveals have a single short core, followed by a flame
        // and a moving light front that remain legible at normal playback speed.
        public static EnergyRelease SampleScenic(float age, float strength)
        {
            if (age < 0 || age >= 2.15f || strength <= 0) return new EnergyRelease(age, 0, 0, 0);
            strength = ImmersiveMotion.Unit(strength);
            float flash = strength * (1 - ImmersiveMotion.Smooth(age / .18f));
            float fire = strength * ImmersiveMotion.Smooth((age - .035f) / .19f) *
                (1 - ImmersiveMotion.Smooth((age - .61f) / .91f));
            float wave = strength * ImmersiveMotion.Smooth((age - .035f) / .21f) *
                (1 - ImmersiveMotion.Smooth((age - .80f) / 1.35f));
            return new EnergyRelease(age, flash, fire, wave);
        }
    }

    public readonly struct CinematicEnergyFrame
    {
        public readonly float Time,Inward,Burst,Afterglow,Focus,Settlement,AccentTime,Recoil;
        public readonly EnergyRelease Release;
        public CinematicEnergyFrame(float time,float inward,float burst,float afterglow,float focus,float settlement,float accentTime,float recoil,EnergyRelease release=default(EnergyRelease))
        { Time=time; Inward=inward; Burst=burst; Afterglow=afterglow; Focus=focus; Settlement=settlement; AccentTime=accentTime; Recoil=recoil; Release=release; }
    }
}
