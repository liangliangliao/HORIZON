using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public enum HorizonEffectKind { EnergyTrail, TimeTrail, CausalBeam, Shockwave, NodeActivation,
        ScreenDistortion, FutureHologram, MemoryFilm, Dissolve, TimeFracture, PatternCrack,
        ConvergenceBeam, OrbitTrail, ReservoirEnergy, OverdriveHorizonGlow, RealityImpactDust }

    public sealed partial class HorizonWorld3D
    {
        private Transform additionalVfx;
        private readonly LineRenderer[] energyTrails=new LineRenderer[3], timeTrails=new LineRenderer[3], orbitTrails=new LineRenderer[8], horizonGlows=new LineRenderer[3];
        private readonly Transform[] nodeHalos=new Transform[24], fracturePanels=new Transform[8], dustCards=new Transform[3];
        private Transform reservoirFlow;
        private Material projectionMaterial;
        private readonly Dictionary<Renderer,Material[]> projectionOriginals=new Dictionary<Renderer,Material[]>();
        private MaterialPropertyBlock effectProperties;
        private void PrepareAdditionalVfx()
        {
            if(additionalVfx!=null) { additionalVfx.gameObject.SetActive(true); return; }
            additionalVfx=Group("Cinematic effect pool",cinematicStage);
            Material dust=FlipbookMaterial("HorizonDustFlipbook"),flow=FlipbookMaterial("HorizonFlowFlipbook");
            for(int i=0;i<3;i++)
            {
                energyTrails[i]=EffectRibbon("Energy trail "+i,portalLight,.028f);
                timeTrails[i]=EffectRibbon("Time trail "+i,glass,.04f);
                horizonGlows[i]=EffectRibbon("Overdrive horizon glow "+i,portalLight,.15f);
                dustCards[i]=Shape(additionalVfx,"Reality impact dust "+i,PrimitiveType.Quad,Vector3.zero,Vector3.one,dust);
            }
            for(int i=0;i<8;i++)
            {
                orbitTrails[i]=EffectRibbon("Orbit trail "+i,portalLight,.014f);
                fracturePanels[i]=Box(additionalVfx,"Time fracture "+i,Vector3.zero,new Vector3(.22f,.55f,.035f),glass);
            }
            for(int i=0;i<24;i++)
            { nodeHalos[i]=Group("Node activation "+i,additionalVfx); Ring(nodeHalos[i],"Activation halo",Vector3.zero,.2f,portalLight,true); }
            reservoirFlow=Shape(additionalVfx,"Baked reservoir energy",PrimitiveType.Quad,new Vector3(0,1.2f,2),Vector3.one*1.3f,flow);
        }
        private LineRenderer EffectRibbon(string name,Material material,float width)
        { var line=CausalLine(additionalVfx,name,Vector3.zero,Vector3.zero,material,width); line.positionCount=20; return line; }
        private Material FlipbookMaterial(string resource)
        { var material=new Material(Resources.Load<Shader>("HorizonFlipbook")); material.mainTexture=Resources.Load<Texture2D>(resource); materials.Add(material); return material; }
        private void Flipbook(Transform card,float frame,Color tint)
        {
            effectProperties.Clear(); effectProperties.SetFloat("_Frame",frame); effectProperties.SetColor("_Color",tint);
            card.GetComponent<Renderer>().SetPropertyBlock(effectProperties);
        }
        private void ProjectionSurface(Transform group,float reveal,float clock,bool active)
        {
            if(!active && projectionOriginals.Count==0) return;
            if(projectionMaterial==null)
            {
                Shader shader=Resources.Load<Shader>("HorizonProjection");
                if(shader==null) return;
                projectionMaterial=new Material(shader) { enableInstancing=true }; materials.Add(projectionMaterial);
            }
            foreach(Renderer renderer in MovieRenderers(group))
            {
                if(renderer is LineRenderer || renderer is ParticleSystemRenderer || renderer.GetComponent<TextMesh>()!=null) continue;
                if(active)
                {
                    if(!projectionOriginals.ContainsKey(renderer)) projectionOriginals.Add(renderer,renderer.sharedMaterials);
                    if(renderer.sharedMaterial!=projectionMaterial) renderer.sharedMaterial=projectionMaterial;
                    effectProperties.Clear(); effectProperties.SetColor("_BaseColor",new Color(.2f,.85f,1,.75f));
                    effectProperties.SetFloat("_Reveal",Mathf.Clamp01(reveal)); effectProperties.SetFloat("_Clock",clock);
                    renderer.SetPropertyBlock(effectProperties);
                }
                else if(projectionOriginals.TryGetValue(renderer,out Material[] original))
                { renderer.sharedMaterials=original; renderer.SetPropertyBlock(null); projectionOriginals.Remove(renderer); }
            }
        }
        private void ResetAdditionalVfx()
        {
            if(additionalVfx!=null) additionalVfx.gameObject.SetActive(false);
            foreach(var pair in projectionOriginals)
                if(pair.Key!=null) { pair.Key.sharedMaterials=pair.Value; pair.Key.SetPropertyBlock(null); }
            projectionOriginals.Clear();
        }
        private void SampleAdditionalVfx(float time,float impact)
        {
            if(additionalVfx==null || moviePlan==null) return;
            DomainEventKind kind=moviePlan.Event.kind; float after=time-movieImpactTime;
            bool motion=!preferences.reducedMotion;
            bool echo=kind==DomainEventKind.TimeEcho || kind==DomainEventKind.Cascade || kind==DomainEventKind.CausalSingularity;
            for(int trail=0;trail<3;trail++)
            {
                bool energy=moviePlan.Objects.Any(o=>o.Kind==RewardObjectKind.EnergyCell && o.Amount>0);
                Vector3 tip=Vector3.zero; int found=0;
                for(int objectIndex=0;objectIndex<movieObjects.Count;objectIndex++)
                    if(movieObjectReceipts[objectIndex].Kind==RewardObjectKind.EnergyCell)
                    { tip=movieObjects[objectIndex].localPosition; if(found++==trail) break; }
                energyTrails[trail].gameObject.SetActive(motion && energy && impact>0 && impact<1 && (!LowCostEffects || trail==0));
                timeTrails[trail].gameObject.SetActive(motion && echo && time<movieImpactTime+.6f && (!LowCostEffects || trail==0));
                for(int i=0;i<20;i++)
                {
                    if(!energyTrails[trail].gameObject.activeSelf && !timeTrails[trail].gameObject.activeSelf) break;
                    float p=i/19f;
                    if(energyTrails[trail].gameObject.activeSelf) energyTrails[trail].SetPosition(i,tip+(tip-new Vector3(0,1.1f,1.1f))*p*.3f+Vector3.up*Mathf.Sin(p*Mathf.PI)*.08f);
                    if(timeTrails[trail].gameObject.activeSelf) timeTrails[trail].SetPosition(i,new Vector3((trail-1)*.22f+Mathf.Sin(p*5+time)*.1f,.28f+Mathf.Sin(p*Mathf.PI)*.2f,Mathf.Lerp(-3,3,p)));
                }
            }
            for(int i=0;i<24;i++)
            {
                float pulse=after-i*.06f;
                bool show=movieNodes[i].gameObject.activeSelf && pulse>=0 && pulse<.5f;
                nodeHalos[i].gameObject.SetActive(show && (motion || pulse<.2f) && (!LowCostEffects || i<8));
                nodeHalos[i].localPosition=movieNodes[i].localPosition;
                nodeHalos[i].localScale=Vector3.one*(1+Mathf.Clamp01(pulse/.5f)*2);
                SetMovieColor(nodeHalos[i],Palette.Mint*Mathf.Clamp01(1-pulse/.5f));
            }
            optics.Distortion=motion && moviePlan.Event.tier>=RewardTier.Epic?Mathf.Clamp01(1-after/.35f)*(after>=0?1:0):0;
            bool hologram=kind==DomainEventKind.TimeEcho || kind==DomainEventKind.Overdrive || kind==DomainEventKind.DejaVu;
            ProjectionSurface(movieFuture,1,time,hologram && movieFuture.gameObject.activeSelf);
            ProjectionSurface(movieBarrier,1-impact,time,kind==DomainEventKind.PatternBroken && impact>0 && impact<1);
            for(int i=0;i<8;i++)
            {
                bool fractured=kind==DomainEventKind.CausalSingularity || kind==DomainEventKind.FailAndAgain;
                fracturePanels[i].gameObject.SetActive(fractured && motion && impact>0 && impact<1 && (!LowCostEffects || i<4));
                fracturePanels[i].localPosition=new Vector3((i%2==0?-1:1)*(.5f+impact*(i+1)*.22f),1+Mathf.Sin(i+impact*3)*.7f,2+(i%3-1)*impact);
                fracturePanels[i].localRotation=Quaternion.Euler(impact*i*35,impact*85,impact*i*11);
                int bits=moviePlan.Event.receipt?.orbitBits??0;
                orbitTrails[i].gameObject.SetActive((kind==DomainEventKind.OrbitActivated || kind==DomainEventKind.AllLinked) && (bits&(1<<i))!=0);
                for(int point=0;point<20 && orbitTrails[i].gameObject.activeSelf;point++)
                {
                    float angle=(i+point/19f)*Mathf.PI/4;
                    orbitTrails[i].SetPosition(point,new Vector3(Mathf.Cos(angle)*2,1.7f+Mathf.Sin(angle)*1.1f,4.2f));
                }
            }
            bool storage=kind==DomainEventKind.Breakthrough;
            reservoirFlow.gameObject.SetActive(storage);
            reservoirFlow.rotation=WorldCamera.transform.rotation;
            if(storage) Flipbook(reservoirFlow,motion?Mathf.Repeat(time*12,16):0,new Color(1,1,1,motion?1:.5f));
            for(int i=0;i<3;i++)
            {
                bool horizon=kind==DomainEventKind.Overdrive || kind==DomainEventKind.HorizonChanged;
                horizonGlows[i].gameObject.SetActive(horizon && (!LowCostEffects || i==0));
                for(int point=0;point<20 && horizonGlows[i].gameObject.activeSelf;point++)
                { float p=point/19f; horizonGlows[i].SetPosition(point,new Vector3((p-.5f)*16,1.2f+Mathf.Sin(p*Mathf.PI)*1.8f+i*.14f,7+impact*2)); }
                SetMovieColor(horizonGlows[i].transform,Palette.Mint*(.08f+impact*.25f));
                bool reality=kind==DomainEventKind.RealityNode || kind==DomainEventKind.RealityConvergence;
                dustCards[i].gameObject.SetActive(motion && reality && after>=0 && after<.85f && (!LowCostEffects || i==0));
                dustCards[i].localPosition=new Vector3((i-1)*.45f,.12f,2+i*.15f);
                dustCards[i].localRotation=Quaternion.Euler(90,0,i*60);
                dustCards[i].localScale=Vector3.one*(1.3f+i*.25f);
                if(dustCards[i].gameObject.activeSelf) Flipbook(dustCards[i],after/.85f*15,Color.white);
            }
        }
    }
}
