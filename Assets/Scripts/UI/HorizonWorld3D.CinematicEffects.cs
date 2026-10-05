using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private Transform semanticEffects, impactRing, focusTarget;
        private readonly Transform[] energyIndicators=new Transform[3];
        private readonly Transform[] focusNoise=new Transform[6];
        private readonly Transform[] relationshipFriends=new Transform[3];
        private void PrepareCinematicEffects()
        {
            if(semanticEffects==null)
            {
                semanticEffects=Group("Semantic cinematic effects",cinematicStage);
                impactRing=Group("Impact wave",semanticEffects);
                Ring(impactRing,"Ground shockwave",Vector3.zero,1,portalLight,false);
                focusTarget=Box(semanticEffects,"Focused destination",new Vector3(0,1.5f,3),new Vector3(.25f,.45f,.08f),ivory);
                for(int i=0;i<focusNoise.Length;i++) focusNoise[i]=Box(semanticEffects,"Attention distraction "+i,Vector3.zero,Vector3.one*.15f,teal);
                for(int i=0;i<3;i++) energyIndicators[i]=Box(moviePlayer,"Recovered energy indicator "+i,new Vector3((i-1)*.15f,1.02f+i*.12f,-.22f),new Vector3(.06f,.16f,.025f),portalLight);
                for(int i=0;i<3;i++) relationshipFriends[i]=Person("Relationship network person "+i,semanticEffects,new Vector3(2.6f+i*.8f,0,1.5f+i),i%2==0?teal:ivory);
            }
            ResetCinematicEffects(); semanticEffects.gameObject.SetActive(true);
        }
        private void ResetCinematicEffects()
        {
            if(semanticEffects==null) return;
            semanticEffects.gameObject.SetActive(false);
            foreach(Transform x in focusNoise) x.gameObject.SetActive(false);
            foreach(Transform x in relationshipFriends) x.gameObject.SetActive(false);
            foreach(Transform lamp in energyIndicators) lamp.gameObject.SetActive(false);
            impactRing.gameObject.SetActive(false); focusTarget.gameObject.SetActive(false);
        }
        private void SampleCinematicEffects(float time,float impact)
        {
            if(semanticEffects==null) return;
            float after=time-movieImpactTime;
            impactRing.gameObject.SetActive(!preferences.reducedMotion && after>=0 && after<.7f && moviePlan.Event.tier>=RewardTier.Major);
            impactRing.localPosition=new Vector3(0,.02f,2); impactRing.localScale=Vector3.one*(.1f+Mathf.Clamp01(after/.7f)*4);
            SetMovieColor(impactRing,Palette.Mint*Mathf.Clamp01(1-after/.7f));
            foreach(MaterialReward reward in moviePlan.Objects)
            {
                if(reward.Kind==RewardObjectKind.FocusLens)
                {
                    float convergence=reward.Amount>0?impact:1-impact;
                    focusTarget.gameObject.SetActive(true);
                    for(int i=0;i<focusNoise.Length;i++)
                    {
                        float a=i*Mathf.PI/3;
                        focusNoise[i].gameObject.SetActive(true);
                        focusNoise[i].localPosition=new Vector3(Mathf.Cos(a)*(1.3f-convergence*.9f),1.5f+Mathf.Sin(a)*.6f,2.4f);
                        focusNoise[i].localScale=Vector3.one*(.18f*(1-convergence)+.015f);
                        SetMovieColor(focusNoise[i],Palette.Muted*(1-convergence*.92f));
                    }
                }
                if(reward.Kind==RewardObjectKind.ConnectionRing)
                {
                    causalPulse.gameObject.SetActive(impact>0 && impact<1);
                    causalPulse.localPosition=Vector3.Lerp(moviePlayer.localPosition+Vector3.up,movieFriend.localPosition+Vector3.up,reward.Amount>0?impact:1-impact);
                    int count=Mathf.Clamp(Mathf.Abs(reward.Amount)/3,0,3);
                    for(int i=0;i<count;i++)
                    {
                        relationshipFriends[i].gameObject.SetActive(true);
                        movieLines[16+i].gameObject.SetActive(true);
                        MovieLine(movieLines[16+i],moviePlayer.localPosition+Vector3.up,relationshipFriends[i].localPosition+Vector3.up);
                        SetMovieColor(movieLines[16+i].transform,Color.Lerp(Palette.Muted*.2f,Palette.Mint,Smooth(.15f*i,.5f+.15f*i,impact)));
                    }
                }
                if(reward.Kind==RewardObjectKind.EnergyCell && reward.Amount>0)
                {
                    for(int i=0;i<energyIndicators.Length;i++) energyIndicators[i].gameObject.SetActive(impact>(i+1)*.22f);
                    if(moviePlan.Event.kind==DomainEventKind.ActionTaken || moviePlan.Event.kind==DomainEventKind.TimeEcho)
                    {
                        moviePlayer.localRotation=Quaternion.Euler(Mathf.Lerp(18,0,impact),180,0);
                        moviePerformance.Play(HorizonBodyAction.Stand,impact);
                    }
                    movieLight.intensity=Mathf.Max(movieLight.intensity,impact*(reward.Scale>=3?2:1));
                }
                if(reward.Kind==RewardObjectKind.WarmLamp && moviePlan.Event.kind==DomainEventKind.ActionTaken)
                    moviePerformance.Play(HorizonBodyAction.Rest,reward.Amount>0?impact:1-impact);
            }
        }
    }
}
