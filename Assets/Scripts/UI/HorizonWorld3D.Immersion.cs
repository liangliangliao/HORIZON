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
        private Renderer[] cinematicOverviewRenderers;
        private bool cinematicOverviewRequested;
        private Renderer[] narrativeSubjects,cinematicSolids;
        private readonly List<Renderer> shotOccluders=new List<Renderer>();
        private readonly HashSet<Renderer> narrativeSubjectSet=new HashSet<Renderer>();
        public bool CinematicOverview => cinematicOverviewRequested;
        public void ToggleCinematicOverview()
        {
            if(moviePlan==null) return;
            cinematicOverviewRequested=!cinematicOverviewRequested;
            if(!cinematicOverviewRequested) showcaseFramingClean=false;
            SampleCinematic(Cinematics.Elapsed);
        }

        private void PrepareCinematicOverview()
        {
            var renderers=new List<Renderer>();
            foreach(Transform item in new[] {moviePlayer,movieFuture,movieFriend,movieAnchor,movieBarrier,cinemaNarrative})
                renderers.AddRange(item.GetComponentsInChildren<Renderer>(true));
            foreach(Transform item in movieNodes) renderers.AddRange(item.GetComponentsInChildren<Renderer>(true));
            foreach(Transform item in movieTiles) renderers.AddRange(item.GetComponentsInChildren<Renderer>(true));
            foreach(Transform item in movieOrbit) renderers.AddRange(item.GetComponentsInChildren<Renderer>(true));
            foreach(Transform item in movieObjects) renderers.AddRange(item.GetComponentsInChildren<Renderer>(true));
            if(relayStage!=null) renderers.AddRange(relayStage.GetComponentsInChildren<Renderer>(true));
            cinematicOverviewRenderers=renderers.ToArray();
            var subjects=new List<Renderer>();
            foreach(Transform item in new[] {moviePlayer,movieFuture,movieFriend}) subjects.AddRange(item.GetComponentsInChildren<Renderer>(true));
            foreach(Transform item in movieObjects) subjects.AddRange(item.GetComponentsInChildren<Renderer>(true));
            narrativeSubjects=subjects.ToArray(); narrativeSubjectSet.Clear();
            foreach(Renderer subject in narrativeSubjects) narrativeSubjectSet.Add(subject);
            cinematicSolids=cinematicStage.GetComponentsInChildren<Renderer>(true);
        }
        private void RestoreShotOccluders()
        {
            foreach(Renderer renderer in shotOccluders) if(renderer!=null) renderer.enabled=true;
            shotOccluders.Clear();
        }
        private void FrameCinematicSafety(float time,ref Vector3 camera,ref Vector3 look)
        {
            bool life=LifeActionPlaying && lifeStage.gameObject.activeSelf;
            bool wide=cinematicOverviewRequested || CurrentCinematicFraming==CinematicFraming.ExtremeWide;
            // Fit complete bodies/objects instead of trusting an authored
            // camera coordinate which can land inside a head or behind a desk.
            if(!life && (relayPlan==null || relayPlan.Nodes.Count==0) && showcaseCameraWeight<.01f)
            {
                // Optical travel is suggested by the lens, not by pushing the
                // physical camera through the actor's head/torso.
                if(!wide && (moviePlan.Event.kind==DomainEventKind.HorizonChanged || moviePlan.Event.kind==DomainEventKind.Overdrive))
                {
                    camera=new Vector3(4.8f,3.4f,-6.5f); look=new Vector3(0,1.4f,2.5f);
                    if(moviePlayer.gameObject.activeSelf) moviePlayer.localRotation=Quaternion.identity;
                }
                FitPortraitSubject(narrativeSubjects,HorizonCinemaLayout.SubjectAperture,
                    CinematicFieldOfView(time),ref camera,ref look);
            }
            if(wide) return;
            Vector3 eye=cinematicStage.TransformPoint(camera),target=cinematicStage.TransformPoint(look);
            if(life) target=moviePlayer.position+Vector3.up;
            Vector3 delta=target-eye; float distance=delta.magnitude;
            if(distance<.01f || cinematicSolids==null) return;
            Ray ray=new Ray(eye,delta/distance);
            foreach(Renderer renderer in cinematicSolids)
            {
                if(renderer==null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer is LineRenderer || renderer is ParticleSystemRenderer || renderer.GetComponent<TextMesh>()!=null ||
                    narrativeSubjectSet.Contains(renderer) || renderer.transform.IsChildOf(jackpotStage) ||
                    renderer.transform.IsChildOf(immersionStage) || renderer.transform.IsChildOf(cinemaProduction)) continue;
                Bounds bounds=renderer.bounds;
                if(bounds.size.magnitude<.45f || !bounds.IntersectRay(ray,out float hit) || hit>=distance-.5f) continue;
                shotOccluders.Add(renderer); renderer.enabled=false;
            }
        }
        private void FrameCinematicOverview(float time,ref Vector3 camera,ref Vector3 look)
        {
            if(moviePlan==null || LifeActionPlaying) return;
            float start=spectacle.SettlementTime;
            float opening=cinemaTension!=null && cinemaTension.SourceCloseup?0:1-Smooth(1.05f,2.15f,time);
            float weight=cinematicOverviewRequested?1:Mathf.Max(opening,Smooth(start,start+.85f,time));
            if(weight<=0) return;
            foreach(Renderer renderer in showcaseOccluders) if(renderer!=null) renderer.enabled=true;
            showcaseOccluders.Clear();
            if(showcaseHero!=null) showcaseHero.gameObject.SetActive(false);
            if(showcasePlatform!=null) showcasePlatform.gameObject.SetActive(false);
            Vector3 wide=new Vector3(5.8f,5.2f,-8.5f),wideLook=new Vector3(0,1.1f,2.6f);
            FitPortraitSubject(cinematicOverviewRenderers,HorizonCinemaLayout.SubjectAperture,
                CinematicFieldOfView(time),ref wide,ref wideLook,false);
            camera=Vector3.Lerp(camera,wide,weight); look=Vector3.Lerp(look,wideLook,weight);
            CurrentCinematicFraming=CinematicFraming.ExtremeWide;
        }

        private void FitPortraitSubject(Renderer[] renderers,Rect aperture,float lens,ref Vector3 camera,ref Vector3 look,bool preserveDistance=true)
        {
            bool found=false; Bounds bounds=default(Bounds);
            foreach(Renderer renderer in renderers)
            {
                if(renderer==null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer is LineRenderer || renderer is ParticleSystemRenderer || renderer.GetComponent<TextMesh>()!=null) continue;
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
            float distance=preserveDistance?Vector3.Distance(eye,target):.5f;
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
            float lifeBurst=LifeActionPlaying && energyFrame.Time>=lifeAction.Impact && energyFrame.Time<lifeAction.Reveal?
                Mathf.Exp(-(energyFrame.Time-lifeAction.Impact)/.48f)*.78f:0;
            bool visible=(cinematicEnergy.Celebrates || lifePressure>0 || lifeBurst>.003f) && !preferences.reducedMotion;
            immersionStage.gameObject.SetActive(visible);
            if(!visible) return;
            int tierBudget=moviePlan.Event.tier<=RewardTier.Local?1:moviePlan.Event.tier==RewardTier.Combo?2:3;
            int budget=LowCostEffects?1:tierBudget;
            // Align with the visible authored subject, including the current
            // causal close-up. Keep every membrane behind it in camera space.
            Vector3 localCenter=showcaseCenter;
            if(LifeActionPlaying && energyFrame.Time<lifeAction.Reveal) localCenter=moviePlayer.localPosition+Vector3.up;
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
                float layerDepth=Mathf.Max(1,WorldCamera.WorldToViewportPoint(layer.position).z);
                float fullHeight=2*layerDepth*Mathf.Tan(WorldCamera.fieldOfView*Mathf.Deg2Rad*.5f);
                layer.localScale=Vector3.one*Mathf.Max(4.9f+i*1.9f,fullHeight*(1.02f+i*.12f)+energyFrame.Burst*.72f);
                Material material=immersionMaterials[i];
                Color charged=Color.Lerp(RewardEffectTint(),new Color(1,.62f,.13f,1),
                    Mathf.Clamp01(Mathf.Max(energyFrame.Inward,lifePressure)*.72f+Mathf.Max(energyFrame.Burst,lifeBurst)*.48f));
                material.SetColor("_Color",charged);
                material.SetFloat("_Clock",energyFrame.Time);
                material.SetFloat("_Charge",Mathf.Max(energyFrame.Inward,lifePressure*.92f)*(1-energyFrame.Settlement));
                material.SetFloat("_Burst",tensionFrame.Silent || moviePhase==CinematicPhase.HitStop?0:Mathf.Max(energyFrame.Burst,lifeBurst));
                material.SetFloat("_Afterglow",energyFrame.Afterglow);
            }
        }

        private void ResetImmersion()
        {
            RestoreShotOccluders();
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
