using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private AudioClip walkAir,walkStep,walkBreath;
        private readonly AudioClip[] walkNotes=new AudioClip[3];

        private void PlayWalkRecoverySounds(float previous,float now)
        {
            if(paused || preferences.reducedMotion || cinematicOverviewRequested || tensionFrame.Silent || movieMuted) return;
            if(walkAir==null)
            {
                walkAir=WalkTexture("Air in the park",3.6f,.065f,0);
                walkStep=WalkTexture("Shoe on a stone",.24f,.44f,1);
                walkBreath=WalkTexture("Exhale in the park",1.24f,.18f,2);
                float[] pitches={330f,415.30f,523.25f};
                for(int i=0;i<walkNotes.Length;i++)
                    walkNotes[i]=ShortCueTone("Recovery note "+(i+1),pitches[i],.31f);
            }
            // Drive sound from the same clock as the pose. A seek does not enqueue missed steps.
            float end=lifeAction.ActionEnd,reveal=lifeAction.Reveal;
            WalkCue(previous,now,end*.32f,walkAir,.75f);
            WalkCue(previous,now,end*.32f+3.6f,walkAir,.70f);
            for(int i=0;i<5;i++) WalkCue(previous,now,end*(.33f+.095f*i),walkStep,.52f+i*.065f);
            WalkCue(previous,now,reveal-CinematicOutcome.FootfallLead,walkStep,1);
            WalkCue(previous,now,reveal,walkAir,.8f);
            for(int row=0;row<moviePlan.Outcome.Changes.Count;row++)
            {
                var change=moviePlan.Outcome.Changes[row];
                float start=reveal+row*CinematicOutcome.ChangeSeconds;
                if(change.Axis==1) WalkCue(previous,now,start,walkBreath,.88f);
                for(int i=0;i<CinematicOutcome.RecoverySteps(change);i++)
                    WalkCue(previous,now,start+CinematicOutcome.RecoveryContact(change.Axis,i),
                        walkNotes[change.Axis==0?i:2],change.Axis==0?.65f:.35f);
            }
            WalkCue(previous,now,consequenceTime,walkAir,.66f);
        }

        private void WalkCue(float previous,float now,float at,AudioClip clip,float volume)
        {
            if(previous>=at || now<at || now-at>.18f) return;
#if UNITY_EDITOR
            recordedSoundTime=at;
#endif
            CueSound(clip,volume);
        }

        private AudioClip WalkTexture(string name,float duration,float level,int kind)
        {
            const int rate=22050;
            var data=new float[Mathf.CeilToInt(duration*rate)];
            var noise=new System.Random(421+kind);
            float soft=0,slow=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate,u=t/duration;
                soft+=.12f*((float)(2*noise.NextDouble()-1)-soft);
                slow+=.013f*(soft-slow);
                float signal;
                if(kind==1)
                    signal=(soft*2.3f+Mathf.Sin(2*Mathf.PI*(105*t-70*t*t))*.67f)*Mathf.Exp(-19*t)*Mathf.Clamp01(140*t);
                else if(kind==2)
                    signal=(soft-slow)*2.7f*Mathf.Pow(Mathf.Sin(Mathf.PI*u),1.2f);
                else
                    signal=(slow*5+soft*.38f)*Mathf.Sin(Mathf.PI*u)*(.8f+.2f*Mathf.Sin(5.3f*t));
                data[i]=level*signal*Mathf.Clamp01((duration-t)*60);
            }
            var clip=AudioClip.Create(name,data.Length,1,rate,false);
            clip.SetData(data,0); sounds.Add(clip); return clip;
        }
    }
}
