using System.Collections.Generic;
using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private CinematicTension cinemaTension;
        private CinematicTensionFrame tensionFrame;
        private Transform attentionIris;
        private Material attentionMaterial;
        private bool tensionStopped;
        private bool tensionPreviewCharged;
        private readonly Dictionary<int,AudioClip> chargeSounds=new Dictionary<int,AudioClip>();
        private void PrepareCinemaTension()
        {
#if UNITY_EDITOR
            recordedCinemaSounds.Clear(); recordedSoundTime=0;
#endif
            cinemaTension=new CinematicTension(moviePlan); tensionStopped=false; tensionPreviewCharged=false;
            if(!cinemaTension.Enabled) return;
            if(cinemaTension.SourceCloseup)
            {
                HideShowcaseOccluder(moviePlayer); HideShowcaseOccluder(movieFuture); HideShowcaseOccluder(movieFriend);
            }
            if(attentionIris==null)
            {
                attentionMaterial=new Material(Resources.Load<Shader>("HorizonCinemaIris")); materials.Add(attentionMaterial);
                attentionIris=Shape(cinematicStage,"Cinematic attention iris",PrimitiveType.Quad,Vector3.zero,Vector3.one,attentionMaterial);
            }
        }
#if UNITY_EDITOR
        private sealed class RecordedCinemaSound { public float Time,Volume; public AudioClip Clip; }
        private readonly List<RecordedCinemaSound> recordedCinemaSounds=new List<RecordedCinemaSound>();
        private float recordedSoundTime;
        // Use the same emitted in-game clips and timestamps in video review.
        // No reference audio, invented soundtrack or extra reward signals.
        public byte[] RecordedCinemaPcm()
        {
            if(moviePlan==null) return new byte[0];
            const int rate=22050; var mixed=new float[Mathf.CeilToInt(moviePlan.Duration*rate)];
            float stopped=moviePlan.Duration,released=moviePlan.Duration;
            foreach(CinematicCue cue in moviePlan.Cues)
            { if(!cue.NodeHit && cue.Phase==CinematicPhase.HitStop) stopped=cue.Time; if(!cue.NodeHit && cue.Phase==CinematicPhase.Impact) released=cue.Time; }
            foreach(RecordedCinemaSound cue in recordedCinemaSounds)
            {
                var samples=new float[cue.Clip.samples*cue.Clip.channels]; cue.Clip.GetData(samples,0);
                // Never place a release cue one sample before its hold ends.
                int start=(int)System.Math.Ceiling((double)cue.Time*rate);
                for(int i=0;i<samples.Length && start+i<mixed.Length;i++)
                {
                    float time=(start+i)/(float)rate;
                    if(time>=stopped && time<released || cinemaTension!=null && cinemaTension.Sample(time,preferences.reducedMotion).Silent) break;
                    mixed[start+i]+=samples[i]*cue.Volume*audioSource.volume;
                }
            }
            var pcm=new byte[mixed.Length*2];
            for(int i=0;i<mixed.Length;i++)
            { short sample=(short)(Mathf.Clamp(mixed[i],-.95f,.95f)*32767); pcm[i*2]=(byte)sample; pcm[i*2+1]=(byte)(sample>>8); }
            return pcm;
        }
#endif
        private void SampleCinemaTension(float time)
        {
            tensionFrame=cinemaTension?.Sample(time,preferences.reducedMotion) ?? default(CinematicTensionFrame);
            bool hold=tensionFrame.Silent || moviePhase==CinematicPhase.HitStop;
            if(cinemaTension==null || !cinemaTension.Enabled) return;
            if(!tensionPreviewCharged && time>=cinemaTension.PreviewStart && cinemaTension.PreviewStart<cinemaTension.Reveal)
            {
                tensionPreviewCharged=true;
                if(!preferences.reducedMotion && !paused && time<cinemaTension.RevealHold)
                {
#if UNITY_EDITOR
                    recordedSoundTime=time;
#endif
                    PlayTensionCharge(cinemaTension.RevealHold-time);
                }
            }
            if(hold!=tensionStopped)
            {
                tensionStopped=hold;
                foreach(HorizonActor actor in cinematicStage.GetComponentsInChildren<HorizonActor>(true)) actor.MotionRate=hold?0:1;
                if(hold) { movieParticles.Pause(); audioSource.Stop(); }
                else if(!paused) movieParticles.Play();
            }
            audioSource.mute=paused || !preferences.sound || hold;
            if(ambience!=null) ambience.mute=paused || !preferences.sound || !preferences.ambience || hold || time<movieImpactTime;
        }
        private void FrameCinemaTension()
        {
            if(attentionIris==null) return;
            float strength=tensionFrame.Focus*.72f;
            attentionIris.gameObject.SetActive(strength>.001f);
            if(strength<=.001f) return;
            const float depth=.6f;
            attentionIris.position=WorldCamera.ViewportToWorldPoint(new Vector3(.5f,.5f,depth));
            attentionIris.rotation=WorldCamera.transform.rotation;
            float height=2*depth*Mathf.Tan(WorldCamera.fieldOfView*Mathf.Deg2Rad*.5f);
            attentionIris.localScale=new Vector3(height*WorldCamera.aspect,height,1);
            attentionMaterial.SetFloat("_Strength",strength);
        }
        private void ResetCinemaTension()
        {
            cinemaTension=null; tensionFrame=default(CinematicTensionFrame); tensionStopped=false; tensionPreviewCharged=false;
            if(attentionIris!=null) attentionIris.gameObject.SetActive(false);
        }
        private void PlayTensionCharge()
        {
            if(cinemaTension==null || !cinemaTension.Enabled || preferences.reducedMotion) return;
            PlayTensionCharge(cinemaTension.FirstHold-cinemaTension.ChargeAt);
        }
        private void PlayTensionCharge(float seconds)
        {
            int key=Mathf.Max(1,Mathf.RoundToInt(seconds*20));
            if(!chargeSounds.TryGetValue(key,out AudioClip clip))
            {
                const int rate=22050; float duration=key/20f; var samples=new float[Mathf.CeilToInt(duration*rate)];
                for(int i=0;i<samples.Length;i++)
                {
                    float t=i/(float)rate,p=t/duration;
                    float phase=2*Mathf.PI*(55*t+180*t*t*t/(3*duration*duration));
                    float envelope=Mathf.Sin(Mathf.Clamp01(p)*Mathf.PI*.5f)*Mathf.Min(1,(duration-t)/.04f);
                    samples[i]=envelope*(Mathf.Sin(phase)*.18f+Mathf.Sin(phase*2.4f)*.06f);
                }
                clip=AudioClip.Create("HORIZON rising charge "+key,samples.Length,1,rate,false); clip.SetData(samples,0);
                sounds.Add(clip); chargeSounds.Add(key,clip);
            }
            CueSound(clip,.7f);
        }
    }
}
