using System.Linq;
using Horizon.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private Transform cinemaProduction, cinemaHalo, cinemaRotor, cinemaBackdrop;
        private Transform[] cinemaWaves=new Transform[3];
        private readonly LineRenderer[] cinemaRibbons=new LineRenderer[16];
        private readonly Transform[] cinemaGlints=new Transform[24];
        private Material cinemaBackdropMaterial;
        private CameraClearFlags priorCinemaClear;
        private Color priorCinemaBackground,priorCinemaFog;
        private bool productionCameraSaved;

        private void PrepareCinemaProduction()
        {
            if(moviePlan.Event.tier<RewardTier.Major) return;
            if(cinemaProduction==null)
            {
                cinemaProduction=Group("HORIZON optical cinema production",cinematicStage);
                cinemaBackdropMaterial=new Material(Resources.Load<Shader>("HorizonCinemaBackdrop")); materials.Add(cinemaBackdropMaterial);
                cinemaBackdrop=Shape(cinemaProduction,"Deep optical cinematic backdrop",PrimitiveType.Quad,new Vector3(0,5,13),new Vector3(35,22,1),cinemaBackdropMaterial);
                Transform deck=Group("Machined horizon deck",cinemaProduction);
                Sculpt(deck,"Bevelled observatory foundation",Own(HorizonSculpt.Profile("Optical cinema deck",new[] {
                    new Vector2(0,-.3f),new Vector2(4.6f,-.3f),new Vector2(4.8f,-.18f),new Vector2(4.7f,-.08f),new Vector2(0,-.08f) },64)),new Vector3(0,0,2),Vector3.one,showcaseEnamel);
                Mesh tickMesh=softBlock;
                for(int i=0;i<32;i++)
                {
                    float a=i*Mathf.PI/16;
                    Sculpt(deck,"Horizon coordinate inlay "+i,tickMesh,new Vector3(Mathf.Cos(a)*4.3f,-.055f,2+Mathf.Sin(a)*4.3f),new Vector3(.045f,.018f,i%4==0?.36f:.16f),i%4==0?gold:teal).localRotation=Quaternion.Euler(0,90-a*Mathf.Rad2Deg,0);
                }
                Ring(deck,"Observatory inner time rail",new Vector3(0,-.04f,2),3.9f,portalLight,false);
                cinemaHalo=Group("Architectural horizon aperture",cinemaProduction); cinemaHalo.localPosition=new Vector3(0,2.4f,6.4f);
                Sculpt(cinemaHalo,"Brushed aperture rim",Own(HorizonSculpt.Torus(3.25f,.065f)),Vector3.zero,Vector3.one,gold);
                Sculpt(cinemaHalo,"Illuminated horizon rim",Own(HorizonSculpt.Torus(3.08f,.025f)),Vector3.zero,Vector3.one,portalLight);
                cinemaRotor=Group("Rotating time coordinate rim",cinemaHalo);
                for(int i=0;i<24;i++)
                {
                    float a=i*Mathf.PI/12;
                    Soft(cinemaRotor,"Aperture coordinate "+i,new Vector3(Mathf.Cos(a)*3.15f,Mathf.Sin(a)*3.15f,-.03f),new Vector3(.045f,i%3==0?.22f:.12f,.035f),i%3==0?gold:ivory).localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg-90);
                }
                for(int i=0;i<cinemaWaves.Length;i++)
                {
                    cinemaWaves[i]=Group("Layered optical shockwave "+i,cinemaProduction);
                    Sculpt(cinemaWaves[i],"Shockwave luminous edge",Own(HorizonSculpt.Torus(1,.022f+i*.009f)),Vector3.zero,Vector3.one,i==1?warmLight:portalLight);
                }
                for(int i=0;i<cinemaRibbons.Length;i++)
                {
                    LineRenderer line=CausalLine(cinemaProduction,"Cinematic energy ribbon "+i,Vector3.zero,Vector3.one,i%3==0?warmLight:portalLight,.025f);
                    line.positionCount=24; line.numCapVertices=3; line.numCornerVertices=2; cinemaRibbons[i]=line;
                }
                for(int i=0;i<cinemaGlints.Length;i++)
                    cinemaGlints[i]=Sculpt(cinemaProduction,"Cinematic depth star "+i,rewardStar,Vector3.zero,Vector3.one,i%3==0?gold:ivory);
                foreach(Renderer r in cinemaProduction.GetComponentsInChildren<Renderer>())
                { r.shadowCastingMode=ShadowCastingMode.Off; r.receiveShadows=false; }
            }
            cinemaProduction.gameObject.SetActive(true);
            priorCinemaClear=WorldCamera.clearFlags; priorCinemaBackground=WorldCamera.backgroundColor; priorCinemaFog=RenderSettings.fogColor;
            productionCameraSaved=true; WorldCamera.clearFlags=CameraClearFlags.SolidColor;
            WorldCamera.backgroundColor=new Color(.003f,.009f,.025f); RenderSettings.fogColor=WorldCamera.backgroundColor;
            RenderSettings.fogDensity=.012f;
            if(relayPlan!=null && relayPlan.Nodes.Count>0) moviePlayer.localPosition=new Vector3(-2.65f,0,2.8f);
        }

        private void SampleCinemaProduction(float time)
        {
            if(cinemaProduction==null || !cinemaProduction.gameObject.activeSelf || spectacle==null) return;
            Color tint=RewardEffectTint();
            bool golden=spectacle.Style==RewardSpectacleStyle.Golden || spectacle.Style==RewardSpectacleStyle.Legendary;
            Vector3 burstCenter=relayPlan!=null && relayPlan.Nodes.Count>0 && time<spectacle.HeroTime?new Vector3(0,1.3f,2):showcaseCenter;
            bool celebrates=spectacle.Style!=RewardSpectacleStyle.Loss && spectacle.Style!=RewardSpectacleStyle.Quiet;
            cinemaBackdropMaterial.SetColor("_Color",tint); cinemaBackdropMaterial.SetFloat("_Clock",preferences.reducedMotion?0:time);
            cinemaRotor.localRotation=Quaternion.Euler(0,0,preferences.reducedMotion?0:time*7);
            float pulse=0,burstTime=-10;
            foreach(CinematicCue cue in moviePlan.Cues)
            {
                bool burst=!cue.NodeHit && (cue.Phase==CinematicPhase.Impact || cue.Phase==CinematicPhase.SecondReveal || cue.Phase==CinematicPhase.Jackpot);
                if(!celebrates || !burst || cue.Time>time) continue;
                float age=time-cue.Time;
                pulse=Mathf.Max(pulse,Mathf.Exp(-age*3.2f)); burstTime=Mathf.Max(burstTime,cue.Time);
            }
            int ribbonBudget=preferences.reducedMotion?0:LowCostEffects?5:cinemaRibbons.Length;
            for(int i=0;i<cinemaRibbons.Length;i++)
            {
                LineRenderer line=cinemaRibbons[i]; bool visible=i<ribbonBudget && pulse>.025f;
                line.gameObject.SetActive(visible); if(!visible) continue;
                float a=i*2.39996f,timeSince=time-burstTime;
                for(int segment=0;segment<24;segment++)
                {
                    float p=segment/23f; float radius=.12f+p*(1.5f+timeSince*1.6f);
                    float turn=a+p*1.9f+timeSince*.7f;
                    Vector3 center=burstCenter;
                    line.SetPosition(segment,center+new Vector3(Mathf.Cos(turn)*radius,Mathf.Sin(turn)*radius*.78f,Mathf.Sin(a)*.45f+p*.6f));
                }
                line.widthMultiplier=Mathf.Max(.008f,.015f+Mathf.Sin(timeSince*Mathf.PI)*.025f)*pulse;
                SetMovieColor(line.transform,Color.Lerp(tint,golden && i%3==0?Palette.Gold:Color.white,.4f)*pulse);
            }
            for(int i=0;i<cinemaWaves.Length;i++)
            {
                float age=time-burstTime-i*.08f; bool show=celebrates && !preferences.reducedMotion && age>=0 && age<.85f;
                Transform wave=cinemaWaves[i]; wave.gameObject.SetActive(show); if(!show) continue;
                wave.localPosition=burstCenter+new Vector3(0,0,.35f+i*.07f);
                wave.localRotation=Quaternion.Euler(10+i*12,0,0); wave.localScale=Vector3.one*(.12f+age*(3.5f+i*.4f));
                SetMovieColor(wave,Color.Lerp(tint,Color.white,.45f)*(1-age/.85f));
            }
            int starBudget=!celebrates || preferences.reducedMotion?0:LowCostEffects?8:cinemaGlints.Length;
            for(int i=0;i<cinemaGlints.Length;i++)
            {
                Transform star=cinemaGlints[i]; bool show=i<starBudget && time<moviePlan.Duration*.94f;
                star.gameObject.SetActive(show); if(!show) continue;
                float a=i*2.39996f; float drift=Mathf.Sin(time*.7f+i)*.22f;
                star.localPosition=new Vector3(Mathf.Cos(a)*(2.1f+i%3*.5f),.5f+i%6*.53f+drift,2.5f+Mathf.Sin(a)*2.6f);
                star.localRotation=Quaternion.Euler(time*17+i*11,time*23,i*23);
                star.localScale=Vector3.one*(.032f+Mathf.Pow(Mathf.Max(0,Mathf.Sin(time*2+i)),8)*.09f+ pulse*.04f);
                SetMovieColor(star,golden?(i%3==0?Palette.Gold:Color.white):Color.Lerp(tint,Color.white,.3f));
            }
        }

        private void ResetCinemaProduction()
        {
            if(cinemaProduction!=null) cinemaProduction.gameObject.SetActive(false);
            if(!productionCameraSaved) return;
            productionCameraSaved=false; WorldCamera.clearFlags=priorCinemaClear;
            WorldCamera.backgroundColor=priorCinemaBackground; RenderSettings.fogColor=priorCinemaFog;
        }
    }
}
