using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private HorizonOptics optics;
        private Transform semanticEffects, impactRing, focusTarget;
        private readonly Transform[] energyIndicators=new Transform[3];
        private readonly Transform[] focusNoise=new Transform[6];
        private readonly Transform[] relationshipFriends=new Transform[3];
        private readonly Transform[] energyAmplifiers=new Transform[3];
        private readonly LineRenderer[] energyDistribution=new LineRenderer[4];
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
                for(int i=0;i<energyAmplifiers.Length;i++)
                {
                    energyAmplifiers[i]=Group("Energy presentation amplifier "+i,semanticEffects);
                    BuildEnergyDevice(energyAmplifiers[i],i==0?RewardObjectForm.BatteryPack:i==1?RewardObjectForm.StorageUnit:RewardObjectForm.EnergyCore);
                    energyAmplifiers[i].localPosition=new Vector3(1.8f,.65f,2);
                }
                for(int i=0;i<energyDistribution.Length;i++)
                    energyDistribution[i]=CausalLine(semanticEffects,"Scene power distribution "+i,new Vector3(1.8f,.7f,2),new Vector3((i%2==0?-1:1)*2,.06f,i<2?-1:5),portalLight,.035f);
            }
            ResetCinematicEffects(); semanticEffects.gameObject.SetActive(true);
            PrepareAdditionalVfx();
        }
        private void ResetCinematicEffects()
        {
            optics?.Clear();
            ResetAdditionalVfx();
            if(memoryFreezePlane!=null) memoryFreezePlane.gameObject.SetActive(false);
            if(semanticEffects==null) return;
            semanticEffects.gameObject.SetActive(false);
            foreach(Transform x in focusNoise) x.gameObject.SetActive(false);
            foreach(Transform x in relationshipFriends) x.gameObject.SetActive(false);
            foreach(Transform lamp in energyIndicators) lamp.gameObject.SetActive(false);
            foreach(Transform device in energyAmplifiers) device.gameObject.SetActive(false);
            foreach(LineRenderer line in energyDistribution) line.gameObject.SetActive(false);
            impactRing.gameObject.SetActive(false); focusTarget.gameObject.SetActive(false);
        }
        private void SampleCinematicEffects(float time,float impact)
        {
            if(semanticEffects==null) return;
            SampleAdditionalVfx(time,impact);
            float after=time-movieImpactTime;
            impactRing.gameObject.SetActive(!preferences.reducedMotion && after>=0 && after<.7f && moviePlan.Event.tier>=RewardTier.Major);
            impactRing.localPosition=new Vector3(0,.02f,2); impactRing.localScale=Vector3.one*(.1f+Mathf.Clamp01(after/.7f)*4);
            SetMovieColor(impactRing,Palette.Mint*Mathf.Clamp01(1-after/.7f));
            foreach(MaterialReward reward in moviePlan.Objects)
            {
                if(reward.Kind==RewardObjectKind.FocusLens)
                {
                    float convergence=reward.Amount>0?impact:1-impact;
                    optics.Defocus = preferences.reducedMotion ? 0 : 1-convergence;
                    optics.LowPower = LowCostEffects;
                    optics.FocusDistance = Vector3.Distance(WorldCamera.transform.position, focusTarget.position);
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
                    int strength=moviePlan.PresentationMultiplier;
                    if(strength>=20)
                    {
                        // This device amplifies the performance; the receipt and
                        // absorbed cells keep their original resource amount.
                        Transform device=energyAmplifiers[strength>=100?2:strength>=50?1:0];
                        device.gameObject.SetActive(time>=movieImpactTime*.5f);
                        device.localScale=Vector3.one*(.7f+impact*.6f);
                        SampleRewardDevice(device,reward,impact);
                        if(strength>=100) for(int i=0;i<energyDistribution.Length;i++)
                        {
                            energyDistribution[i].gameObject.SetActive(impact>i*.15f);
                            energyDistribution[i].widthMultiplier=.015f+impact*.04f;
                        }
                        movieLight.intensity=Mathf.Max(movieLight.intensity,impact*(strength>=100?3:1.5f));
                    }
                }
                if(reward.Kind==RewardObjectKind.WarmLamp && moviePlan.Event.kind==DomainEventKind.ActionTaken)
                    moviePerformance.Play(HorizonBodyAction.Rest,reward.Amount>0?impact:1-impact);
            }
        }
    }
}
