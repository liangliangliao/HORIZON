using System.Collections.Generic;
using Horizon.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private CinematicEnergy cinematicEnergy;
        private CinematicEnergyFrame energyFrame;
        private Transform immersionStage;
        private readonly Transform[] immersionLayers=new Transform[3];
        private readonly Material[] immersionMaterials=new Material[3];
        private readonly Dictionary<int,AudioClip> relayVoices=new Dictionary<int,AudioClip>();
        private AudioClip revealShimmer;
        private Vector3 immersionFocus;

        private void FitPortraitSubject(Renderer[] renderers,Rect aperture,float lens,ref Vector3 camera,ref Vector3 look)
        {
            bool found=false; Bounds bounds=default(Bounds);
            foreach(Renderer renderer in renderers)
            {
                if(renderer==null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer is LineRenderer || renderer is ParticleSystemRenderer) continue;
                if(!found) {bounds=renderer.bounds; found=true;} else bounds.Encapsulate(renderer.bounds);
            }
            if(!found) return;
            Vector3 eye=cinematicStage.TransformPoint(camera),target=cinematicStage.TransformPoint(look);
            Quaternion rotation=Quaternion.LookRotation(target-eye,Vector3.up),inverse=Quaternion.Inverse(rotation);
            float tangent=Mathf.Tan(lens*Mathf.Deg2Rad*.5f);
            // Captures may replace an editor target after sampling. Frame for
            // 20:9 as well as 9:16, never for the editor's landscape Game view.
            float aspect=Mathf.Max(.1f,Mathf.Min(WorldCamera.aspect,9f/20));
            float horizontal=tangent*aspect,cx=aperture.center.x,cy=aperture.center.y;
            float distance=Vector3.Distance(eye,target);
            for(int corner=0;corner<8;corner++)
            {
                Vector3 point=bounds.center+Vector3.Scale(bounds.extents,new Vector3(
                    (corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                Vector3 p=inverse*(point-bounds.center);
                distance=Mathf.Max(distance,.3f-p.z);
                distance=Mathf.Max(distance,(p.x-2*horizontal*(aperture.xMax-.5f)*p.z)/(2*horizontal*(aperture.xMax-cx)));
                distance=Mathf.Max(distance,(2*horizontal*(aperture.xMin-.5f)*p.z-p.x)/(2*horizontal*(cx-aperture.xMin)));
                distance=Mathf.Max(distance,(p.y-2*tangent*(aperture.yMax-.5f)*p.z)/(2*tangent*(aperture.yMax-cy)));
                distance=Mathf.Max(distance,(2*tangent*(aperture.yMin-.5f)*p.z-p.y)/(2*tangent*(cy-aperture.yMin)));
            }
            distance*=1.06f; // Leave room for the small authored lens recoil.
            Vector3 forward=rotation*Vector3.forward;
            eye=bounds.center-forward*distance-rotation*new Vector3(
                2*(cx-.5f)*horizontal*distance,2*(cy-.5f)*tangent*distance,0);
            camera=cinematicStage.InverseTransformPoint(eye);
            look=cinematicStage.InverseTransformPoint(eye+forward*distance);
        }

        private void PrepareImmersion()
        {
            cinematicEnergy=new CinematicEnergy(moviePlan);
            if(immersionStage==null)
            {
                immersionStage=Group("HORIZON inward release depth field",cinematicStage);
                for(int i=0;i<immersionLayers.Length;i++)
                {
                    // Separate shared materials: no per-frame allocation or MPB
                    // claim of SRP batching. At most three extra transparent draws.
                    var material=new Material(Resources.Load<Shader>("HorizonImmersion"));
                    material.SetFloat("_Layer",i); materials.Add(material); immersionMaterials[i]=material;
                    immersionLayers[i]=Shape(immersionStage,"Optical depth membrane "+i,PrimitiveType.Quad,
                        Vector3.zero,Vector3.one,material);
                    var renderer=immersionLayers[i].GetComponent<Renderer>();
                    renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
                }
            }
            immersionStage.gameObject.SetActive(true);
        }

        private void SampleImmersion(float time,ref Vector3 camera,ref Vector3 look)
        {
            if(cinematicEnergy==null) return;
            immersionFocus=look;
            if(preferences.reducedMotion || tensionFrame.Silent || moviePhase==CinematicPhase.HitStop) return;
            // Lens-space push and recoil preserve the authored target and framing.
            // Existing source close-ups already own their camera position.
            if(cinemaTension==null || !cinemaTension.SourceCloseup)
            {
                Vector3 direction=(look-camera).normalized;
                camera+=direction*(energyFrame.Inward*.48f-energyFrame.Recoil*.24f);
            }
            movieLight.intensity+=energyFrame.Inward*.42f+energyFrame.Burst*1.25f;
        }

        private void FrameImmersion()
        {
            if(immersionStage==null || cinematicEnergy==null) return;
            bool visible=cinematicEnergy.Celebrates && !preferences.reducedMotion;
            immersionStage.gameObject.SetActive(visible);
            if(!visible) return;
            int tierBudget=moviePlan.Event.tier<=RewardTier.Local?1:moviePlan.Event.tier==RewardTier.Combo?2:3;
            int budget=LowCostEffects?1:tierBudget;
            // Align with the visible authored subject, including the current
            // causal close-up. Keep every membrane behind it in camera space.
            Vector3 localCenter=showcaseCenter;
            if(relayPlan!=null && relayPlan.Nodes.Count>0 && energyFrame.Time<spectacle.HeroTime)
                localCenter=immersionFocus;
            Vector3 center=cinematicStage.TransformPoint(localCenter);
            float depth=WorldCamera.WorldToViewportPoint(center).z;
            if(depth<1) center=WorldCamera.transform.position+WorldCamera.transform.forward*6;
            for(int i=0;i<3;i++)
            {
                Transform layer=immersionLayers[i]; layer.gameObject.SetActive(i<budget);
                if(i>=budget) continue;
                layer.position=center+WorldCamera.transform.forward*(.7f+i*1.35f);
                layer.rotation=WorldCamera.transform.rotation*Quaternion.Euler(0,0,i*31);
                layer.localScale=Vector3.one*(4.9f+i*1.9f+energyFrame.Burst*.72f);
                Material material=immersionMaterials[i];
                Color charged=Color.Lerp(RewardEffectTint(),new Color(1,.62f,.13f,1),
                    Mathf.Clamp01(energyFrame.Inward*.72f+energyFrame.Burst*.48f));
                material.SetColor("_Color",charged);
                material.SetFloat("_Clock",energyFrame.Time);
                material.SetFloat("_Charge",energyFrame.Inward*(1-energyFrame.Settlement));
                material.SetFloat("_Burst",tensionFrame.Silent || moviePhase==CinematicPhase.HitStop?0:energyFrame.Burst);
                material.SetFloat("_Afterglow",energyFrame.Afterglow);
            }
        }

        private void ResetImmersion()
        {
            cinematicEnergy=null; energyFrame=default(CinematicEnergyFrame);
            if(immersionStage!=null) immersionStage.gameObject.SetActive(false);
        }

        private AudioClip RelayVoice(int index)
        {
            int key=Mathf.Clamp(index,0,15);
            if(!relayVoices.TryGetValue(key,out AudioClip clip))
            {
                // Register climbs with genuine causal nodes; it never represents
                // invented wins. Cache only sixteen short voices.
                clip=ShortCueTone("Causal relay pitch "+key,174*Mathf.Pow(2,key/15f),.16f);
                relayVoices.Add(key,clip);
            }
            return clip;
        }

        private void PlayRevealShimmer(float strength)
        {
            if(preferences.reducedMotion) return;
            if(revealShimmer==null)
            {
                const int rate=22050; const float duration=.65f;
                var data=new float[(int)(rate*duration)];
                for(int i=0;i<data.Length;i++)
                {
                    float t=i/(float)rate;
                    float envelope=Mathf.Min(1,t/.025f)*Mathf.Exp(-t*7)*Mathf.Min(1,(duration-t)/.05f);
                    data[i]=(Mathf.Sin(2*Mathf.PI*523.25f*t)*.13f+Mathf.Sin(2*Mathf.PI*784*t)*.08f+
                        Mathf.Sin(2*Mathf.PI*1046.5f*t)*.04f)*envelope;
                }
                revealShimmer=AudioClip.Create("HORIZON release overtone",data.Length,1,rate,false);
                revealShimmer.SetData(data,0); sounds.Add(revealShimmer);
            }
            CueSound(revealShimmer,strength);
        }
    }
}
