using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private readonly AudioClip[] revealBolts=new AudioClip[3];
        private readonly AudioClip[] revealContacts=new AudioClip[6];
        private AudioClip revealImpact;
        private AudioClip echoImpact;

        private void PrepareRevealSounds()
        {
            if(revealImpact!=null) return;
            for(int i=0;i<revealBolts.Length;i++)
                revealBolts[i]=ShortCueTone("Case bolt "+(i+1),332+163*i,.126f);
            for(int i=0;i<revealContacts.Length;i++)
                revealContacts[i]=ShortCueTone("Receipt contact "+(i+1),523.25f*Mathf.Pow(2,i*2/12f),.27f);
            revealImpact=BuildRevealImpact(false);
            echoImpact=BuildRevealImpact(true);
        }

        private AudioClip BuildRevealImpact(bool echo)
        {
            const int rate=22050;
            const float duration=.42f;
            var samples=new float[(int)(rate*duration)];
            uint seed=0x184e3a7c;
            float lastNoise=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;
                seed=unchecked(1664525u*seed+1013904223u);
                float noise=(seed>>16)/32768f-1;
                float transient=(noise-lastNoise)*Mathf.Exp(-t*(echo?58:87));
                lastNoise=noise;
                float fundamental=Mathf.PI*2*((echo?135:105)*t-(echo?98:79)*t*t);
                float body=Mathf.Sin(fundamental)*Mathf.Exp(-t*12)+.42f*Mathf.Sin(fundamental*2.4f)*Mathf.Exp(-t*22);
                float tail=Mathf.Sin(fundamental*5.6f)*Mathf.Exp(-t*18);
                float envelope=Mathf.Min(1,t/.003f)*Mathf.Min(1,(duration-t)/.04f);
                samples[i]=envelope*(body*.53f+transient*.19f+tail*(echo?.18f:.11f));
            }
            AudioClip clip=AudioClip.Create(echo?"Echo seal contact":"Release spring and case strike",samples.Length,1,rate,false);
            clip.SetData(samples,0); sounds.Add(clip);
            return clip;
        }

        private void SamplePracticeRevealSounds(PracticeRewardCinematic film,float time,bool play)
        {
            if(!play)
            {
                if(caseVisualStarted) audioSource.Stop();
                caseVisualStarted=false; caseSilent=false; UpdateAudio();
                caseCueTime=time;
                return;
            }
            caseVisualStarted=true;
            float previous=caseCueTime;
            caseCueTime=time;
            if(CrossedRevealCue(previous,time,.94f))
            {
                SetPracticeAudioTime(.94f);
                PlayTensionCharge(PracticeRewardCinematic.HoldTime-.94f);
            }
            for(int i=0;i<revealBolts.Length;i++)
            {
                float at=PracticeRewardCinematic.LatchTime(i);
                if(!CrossedRevealCue(previous,time,at)) continue;
                SetPracticeAudioTime(at);
                CueSound(revealBolts[i],.48f+.09f*i);
                MasterHaptics.Cue(13+3*i,33+12*i,preferences);
            }

            bool hold=time>=PracticeRewardCinematic.HoldTime && time<PracticeRewardCinematic.ReleaseTime;
            if(hold!=caseSilent)
            {
                caseSilent=hold;
                if(hold) audioSource.Stop();
                UpdateAudio();
            }
            if(CrossedRevealCue(previous,time,PracticeRewardCinematic.ReleaseTime))
            {
                SetPracticeAudioTime(PracticeRewardCinematic.ReleaseTime);
                PlayRevealStrike(false,film.Difficulty==3?.94f:film.Difficulty==1?.66f:.80f);
            }
            for(int axis=0;axis<revealContacts.Length;axis++)
            {
                if(film.TicksAt(axis)<=0) continue;
                float at=film.ContactTime(axis);
                if(!CrossedRevealCue(previous,time,at)) continue;
                SetPracticeAudioTime(at);
                CueSound(revealContacts[film.GainSlot(axis)],.41f);
                MasterHaptics.Cue(13,36,preferences);
            }
        }

        private static bool CrossedRevealCue(float previous,float time,float cue) =>
            previous<cue && time>=cue && time-cue<=.145f;

        private void SetPracticeAudioTime(float at)
        {
#if UNITY_EDITOR
            recordedSoundTime=at;
#endif
        }

        private void PlayRevealStrike(bool echo,float strength)
        {
            if(preferences.reducedMotion) return;
            PrepareRevealSounds();
            CueSound(echo?echoImpact:revealImpact,strength);
            PlayRevealShimmer(.51f*strength);
            MasterHaptics.Cue(38,Mathf.RoundToInt(145*strength),preferences);
        }
    }
}
