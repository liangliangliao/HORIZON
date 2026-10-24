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
        public readonly float Strength;
        public readonly bool Celebrates;

        public CinematicEnergy(RewardPlan value)
        {
            plan=value ?? throw new ArgumentNullException(nameof(value));
            spectacle=new RewardSpectacle(plan);
            Celebrates=spectacle.Style!=RewardSpectacleStyle.Quiet && spectacle.Style!=RewardSpectacleStyle.Loss;
            Strength=new[] {.10f,.18f,.32f,.56f,.78f,1f}[Math.Max(0,Math.Min(5,(int)plan.Event.tier-1))];
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
                float window=cue.NodeHit?.18f:Math.Min(2.4f,Math.Max(1.15f,plan.Duration*.22f));
                float from=Math.Max(i==0?0:accents[i-1].Time+.06f,cue.Time-window);
                if(time>=from && time<cue.Time)
                    charge=Math.Max(charge,ImmersiveMotion.Smooth((time-from)/Math.Max(.001f,cue.Time-from))*weight);
                float age=time-cue.Time;
                if(age<0) continue;
                last=cue.Time;
                burst=Math.Max(burst,weight*(float)Math.Exp(-age/(cue.NodeHit?.16f:.42f)));
                recoil+=ImmersiveMotion.Punch(age,cue.NodeHit?.22f:.46f)*weight;
            }
            float afterglow=ImmersiveMotion.Smooth((time-spectacle.ImpactTime)/.4f)*(1-settle);
            float focus=charge*(1-settle);
            if(!Celebrates) return new CinematicEnergyFrame(time,0,0,.14f*afterglow,0,settle,last,0);
            if(reducedMotion) return new CinematicEnergyFrame(time,0,0,.18f*afterglow,0,settle,last,0);
            return new CinematicEnergyFrame(time,charge*Strength,burst*Strength*(1-settle),
                afterglow*Strength,focus*Strength,settle,last,Math.Max(-1,Math.Min(1,recoil))*Strength);
        }
    }

    public readonly struct CinematicEnergyFrame
    {
        public readonly float Time,Inward,Burst,Afterglow,Focus,Settlement,AccentTime,Recoil;
        public CinematicEnergyFrame(float time,float inward,float burst,float afterglow,float focus,float settlement,float accentTime,float recoil)
        { Time=time; Inward=inward; Burst=burst; Afterglow=afterglow; Focus=focus; Settlement=settlement; AccentTime=accentTime; Recoil=recoil; }
    }
}
