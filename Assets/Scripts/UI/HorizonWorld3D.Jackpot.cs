using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private Transform jackpotStage, showcasePlatform, showcaseHero, jackpotPlaque;
        private Transform jackpotHalo, jackpotRays, jackpotFlare;
        private readonly Transform[] rewardMotes=new Transform[48], jackpotStars=new Transform[16];
        private readonly Dictionary<string,Transform> showcasePool=new Dictionary<string,Transform>();
        private readonly List<Renderer> showcaseOccluders=new List<Renderer>();
        private Mesh embossedCoin, rewardStar;
        private Material showcaseEnamel, rewardCrystal, jackpotAura;
        private RewardSpectacle spectacle;
        private MaterialReward activeHeroReward;
        private MaterialReward[] outcomeResources;
        private RewardSpectacleFrame spectacleFrame;
        private MaterialPropertyBlock auraProperties;
        private float showcaseScale;
        private float outcomeSubjectFocus;
        private float showcaseCameraWeight;
        private Renderer[] showcaseSubjectRenderers;
        private Vector3 showcaseOffset, showcaseCenter;
        private bool showcaseFullscreen, showcaseFramingClean;
        private HorizonRewardSparkPool rewardSparkPool;

        private void InitializeJackpotStage()
        {
            showcaseEnamel=Lit(new Color(.025f,.11f,.17f));
            showcaseEnamel.SetFloat("_Metallic",.3f); showcaseEnamel.SetFloat("_Smoothness",.82f); showcaseEnamel.SetFloat("_Coat",.65f);
            rewardCrystal=Lit(new Color(.35f,.48f,.88f),new Color(.035f,.025f,.09f));
            rewardCrystal.SetFloat("_Metallic",.42f); rewardCrystal.SetFloat("_Smoothness",.9f); rewardCrystal.SetFloat("_Coat",.8f);
            jackpotAura=new Material(Resources.Load<Shader>("HorizonRewardAura")); materials.Add(jackpotAura);
            auraProperties=new MaterialPropertyBlock();
            embossedCoin=Own(HorizonRewardMeshes.Coin()); rewardStar=Own(HorizonRewardMeshes.Star());
            jackpotStage=Group("Pooled 3D reward spectacle",cinematicStage);
            showcasePlatform=BuildRewardShowcasePlatform();
            jackpotHalo=Shape(jackpotStage,"Soft reward halo",PrimitiveType.Quad,Vector3.zero,Vector3.one,jackpotAura);
            jackpotRays=Shape(jackpotStage,"Radial reward light shafts",PrimitiveType.Quad,Vector3.zero,Vector3.one,jackpotAura);
            jackpotFlare=Shape(jackpotStage,"Impact lens flare",PrimitiveType.Quad,Vector3.zero,Vector3.one,jackpotAura);
            for(int i=0;i<rewardMotes.Length;i++)
                rewardMotes[i]=Sculpt(jackpotStage,"Decorative reward star "+i,rewardStar,Vector3.zero,Vector3.one,gold);
            for(int i=0;i<jackpotStars.Length;i++)
                jackpotStars[i]=Sculpt(jackpotStage,"Reward star glint "+i,rewardStar,Vector3.zero,Vector3.one,warmLight);
            BuildJackpotPlaque();
            foreach(Renderer renderer in jackpotStage.GetComponentsInChildren<Renderer>())
            { renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false; }
            rewardSparkPool=gameObject.AddComponent<HorizonRewardSparkPool>();
            rewardSparkPool.Initialize(rewardStar,gold,teal,pink);
            ResetJackpotStage();
        }

        private Transform BuildRewardShowcasePlatform()
        {
            Transform platform=Group("HORIZON reward light platform",jackpotStage);
            Mesh baseMesh=Own(HorizonSculpt.Profile("Bevelled optical platform",new[] {
                new Vector2(0,-.16f),new Vector2(.95f,-.16f),new Vector2(1.08f,-.09f),
                new Vector2(1.08f,.04f),new Vector2(.96f,.13f),new Vector2(0,.13f) },48));
            Sculpt(platform,"Brushed metal showcase base",baseMesh,Vector3.zero,Vector3.one,showcaseEnamel);
            Sculpt(platform,"Golden platform rim",Own(HorizonSculpt.Torus(1.01f,.022f)),
                new Vector3(0,.08f,0),Vector3.one,gold).localRotation=Quaternion.Euler(90,0,0);
            Ring(platform,"Living time ring",new Vector3(0,.14f,0),.78f,portalLight,false);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;
                Box(platform,"Time coordinate inlay "+i,new Vector3(Mathf.Cos(a)*.91f,.13f,Mathf.Sin(a)*.91f),new Vector3(.09f,.02f,.045f),gold)
                    .localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);
            }
            GroundContact(platform,new Vector3(0,-.18f,0),new Vector2(2.8f,2.5f));
            return platform;
        }

        private void BuildJackpotPlaque()
        {
            // An inactive compatibility anchor, NOT a visible reward or plaque.
            // Actual changes are the only large reveal, owned by the caption.
            jackpotPlaque=Group("3D presentation multiplier plaque",jackpotStage);
            jackpotPlaque.gameObject.SetActive(false);
        }

        private void PrepareJackpotStage(RewardPlan plan,bool fullscreen)
        {
            ResetJackpotStage(); spectacle=new RewardSpectacle(plan);
            showcaseFullscreen=fullscreen;
            jackpotStage.gameObject.SetActive(true);
            outcomeResources=plan.Objects.Where(o=>o.Class==RewardObjectClass.Resource).ToArray();
            MaterialReward receipt=spectacle.HasHero?spectacle.HeroReward:outcomeResources.FirstOrDefault();
            if(receipt!=null)
                SelectShowcaseHero(receipt);
            bool money=plan.Objects.Any(o=>o.Kind==RewardObjectKind.MoneyWallet && o.Amount>0);
            foreach(Transform mote in rewardMotes)
            {
                mote.GetComponent<MeshFilter>().sharedMesh=money?embossedCoin:rewardStar;
                mote.name=(money?"Decorative reward coin ":"Decorative reward star ")+mote.GetSiblingIndex();
            }
        }

        private void SelectShowcaseHero(MaterialReward receipt)
        {
                if(showcaseHero!=null) showcaseHero.gameObject.SetActive(false);
                activeHeroReward=receipt;
                TriggerAppearance appearance=RewardDirector.TriggerForm(moviePlan.Event.receipt?.triggerId);
                string key=receipt.Kind+":"+receipt.Form+":"+appearance;
                if(!showcasePool.TryGetValue(key,out showcaseHero))
                {
                    showcaseHero=BuildRewardObject(receipt.Kind,receipt.Form,appearance);
                    showcaseHero.SetParent(jackpotStage,false); showcaseHero.name="Reward showcase "+receipt.Kind;
                    showcasePool.Add(key,showcaseHero);
                }
                RestoreRewardObject(showcaseHero,receipt.Kind);
                showcaseHero.gameObject.SetActive(true);
                showcaseHero.localScale=Vector3.one; showcaseHero.localPosition=Vector3.zero;
                showcaseSubjectRenderers=showcaseHero.GetComponentsInChildren<Renderer>(true);
                // Frame the actual asset at a readable size, rather than inventing
                // a larger reward form or changing its receipt amount.
                Bounds bounds=new Bounds(); bool found=false;
                foreach(Renderer renderer in showcaseHero.GetComponentsInChildren<Renderer>(true))
                {
                    if(renderer is LineRenderer || renderer is ParticleSystemRenderer) continue;
                    if(!found) { bounds=renderer.bounds; found=true; } else bounds.Encapsulate(renderer.bounds);
                }
                showcaseScale=(showcaseFullscreen?1.92f:1.75f)/Mathf.Max(.35f,Mathf.Max(bounds.size.x,bounds.size.y));
                showcaseOffset=found?showcaseHero.InverseTransformPoint(bounds.center):Vector3.zero;
        }

        private void ResetJackpotStage()
        {
            spectacle=null; showcaseHero=null; activeHeroReward=null; outcomeResources=null; outcomeSubjectFocus=0;
            showcaseCameraWeight=0; showcaseSubjectRenderers=null;
            foreach(Renderer renderer in showcaseOccluders) if(renderer!=null) renderer.enabled=true;
            showcaseOccluders.Clear();
            showcaseFramingClean=false;
            if(jackpotStage==null) return;
            jackpotStage.gameObject.SetActive(false); showcasePlatform.gameObject.SetActive(false); jackpotPlaque.gameObject.SetActive(false);
            foreach(Transform item in showcasePool.Values) item.gameObject.SetActive(false);
            foreach(Transform mote in rewardMotes) mote.gameObject.SetActive(false);
            foreach(Transform star in jackpotStars) star.gameObject.SetActive(false);
        }

        private void SampleJackpotStage(float time,ref Vector3 camera,ref Vector3 look)
        {
            if(spectacle==null) return;
            // Seeking can jump from an establishing shot with this parent
            // hidden straight into the resource reveal. Framing needs active
            // renderer bounds now, not after this frame has been rendered.
            jackpotStage.gameObject.SetActive(true);
            spectacleFrame=spectacle.Sample(time,preferences.reducedMotion);
            float outcomeSecond=moviePlan.Cues.First(c=>!c.NodeHit && c.Phase==CinematicPhase.SecondReveal).Time;
            OutcomeFrame outcome=moviePlan.Outcome.Sample(time,spectacle.MultiplierTime,outcomeSecond,spectacle.SettlementTime,preferences.reducedMotion);
            MaterialReward defaultSubject=spectacle.HasHero?spectacle.HeroReward:outcomeResources.FirstOrDefault();
            MaterialReward subject=outcome.Visible && !outcome.Future && outcome.Index>=0 && outcome.Index<outcomeResources.Length?
                outcomeResources[outcome.Index]:defaultSubject;
            if(subject!=null && subject!=activeHeroReward) SelectShowcaseHero(subject);
            RewardSpectacleFrame frame=spectacleFrame;
            // Ordinary actions and losses deserve a readable product shot too.
            // This is attention, not a promotion to a reward tier: their sound,
            // particles and celebration budgets remain unchanged.
            outcomeSubjectFocus=!spectacle.HasHero && outcomeResources.Length>0?
                Smooth(spectacle.MultiplierTime-.8f,spectacle.MultiplierTime,time)*
                (1-Smooth(moviePlan.Duration*.94f,moviePlan.Duration,time)):0;
            float heroFocus=Mathf.Max(frame.Hero,outcomeSubjectFocus);
            float preview=tensionFrame.Preview;
            bool heldReveal=cinemaTension!=null && cinemaTension.PreviewStart<cinemaTension.Reveal && time>=cinemaTension.RevealHold && time<moviePlan.Duration*.94f && !preferences.reducedMotion;
            bool hero=heroFocus>.001f || preview>.001f || heldReveal;
            showcaseCameraWeight=hero?(heldReveal?1:preview>0?preview:heroFocus):0;
            showcaseCenter=showcaseHero!=null?new Vector3(0,1.6f,2):moviePlayer.localPosition+Vector3.up*1.15f;
            if(showcaseHero!=null)
            {
                showcaseHero.gameObject.SetActive(hero);
                float pose=preview>0?tensionFrame.PoseTime:time;
                float breath=preferences.reducedMotion || tensionFrame.Silent?0:Mathf.Sin(pose*2)*.016f;
                float age=time-spectacle.HeroTime;
                float bounce=preferences.reducedMotion?0:age>=0?Mathf.Sin(Mathf.Min(1,age/.45f)*Mathf.PI)*.18f:0;
                float detailTremor=preferences.reducedMotion || tensionFrame.Silent?0:Mathf.Pow(energyFrame.Inward,4)*.035f;
                float scale=showcaseScale*(.72f+heroFocus*.28f+breath+bounce);
                if(preview>0) scale=showcaseScale*(.80f-preview*.055f);
                showcaseHero.localScale=Vector3.one*scale;
                showcaseHero.localRotation=Quaternion.Euler(Mathf.Sin(time*61)*detailTremor*42,
                    preferences.reducedMotion?-15:-15+Mathf.Sin(pose*.65f)*7+Mathf.Sin(time*73)*detailTremor*55,
                    Mathf.Cos(time*67)*detailTremor*28);
                showcaseHero.localPosition=showcaseCenter-showcaseHero.localRotation*(showcaseOffset*scale)+
                    new Vector3(Mathf.Sin(time*79),Mathf.Cos(time*71),0)*detailTremor;
                SampleRewardDevice(showcaseHero,activeHeroReward,frame.Lid);
                SampleRewardDetail(showcaseHero,pose,frame.Lid);
                if(activeHeroReward.Kind==RewardObjectKind.ReservoirCore)
                {
                    Transform lid=showcaseHero.Find("Storage lid");
                    if(lid!=null) lid.localPosition=new Vector3(0,.55f+frame.Lid*.7f,0);
                }
                if(activeHeroReward.Kind==RewardObjectKind.BrokenLink)
                    showcaseHero.Find("Other link").localPosition=new Vector3(.2f+frame.Lid*.16f,.04f+frame.Lid*.1f,0);
                if((heroFocus>.95f || preview>.1f) && !showcaseFramingClean)
                {
                    showcaseFramingClean=true;
                    // The narrative bodies remain in the earlier shots. In the
                    // final product shot keep their state alive while hiding the
                    // renderers that would occlude the actual receipt asset.
                    HideShowcaseOccluder(moviePlayer); HideShowcaseOccluder(movieFuture); HideShowcaseOccluder(movieFriend);
                    foreach(Transform item in movieObjects) HideShowcaseOccluder(item);
                }
            }
            showcasePlatform.gameObject.SetActive(hero);
            showcasePlatform.localPosition=new Vector3(showcaseCenter.x,.08f,showcaseCenter.z);
            showcasePlatform.localScale=Vector3.one*(.8f+heroFocus*.25f);
            // Original narrative shots remain intact until their second reveal.
            // The final close-up highlights the actual resource or memento.
            if(hero)
            {
                float revealed=Smooth(spectacle.HeroTime+.6f,spectacle.HeroTime+2.4f,time);
                // After the light releases, stay with the actual object: a slow
                // product-shot arc, not another flash followed by an idle wide.
                float angle=preferences.reducedMotion?0:Mathf.Lerp(-.12f,.15f,revealed);
                Vector3 closeCamera=preferences.reducedMotion?new Vector3(5,4,-8):showcaseCenter+
                    new Vector3(1.1f+Mathf.Sin(angle)*2,1.05f,Mathf.Lerp(-6.3f,-5.3f,revealed));
                Vector3 eye=closeCamera-showcaseCenter;
                float q=2*Mathf.Tan(CinematicFieldOfView(time)*Mathf.Deg2Rad*.5f)*((showcaseFullscreen?.61f:.64f)-.5f);
                float horizontal=Mathf.Sqrt(eye.x*eye.x+eye.z*eye.z);
                float offset=q*eye.sqrMagnitude/Mathf.Max(.1f,horizontal-q*eye.y);
                Vector3 closeLook=showcaseCenter-Vector3.up*offset;
                // The actual phone HUD covers the lower viewport, including the
                // prediction receipt. Keep the physical asset above those panels.
                float framing=heldReveal?1:preview>0?preview:heroFocus;
                if(!preferences.reducedMotion) camera=Vector3.Lerp(camera,closeCamera,framing);
                look=preferences.reducedMotion?closeLook:Vector3.Lerp(look,closeLook,framing);
            }
            int budget=tensionFrame.Silent?0:spectacle.MoteBudget(preferences.batterySaver,adaptiveBudget.Level,preferences.reducedMotion);
            for(int i=0;i<rewardMotes.Length;i++) SampleRewardMote(i,time,budget);
            int stars=preferences.reducedMotion?0:moviePlan.Event.tier==RewardTier.Micro?3:LowCostEffects?4:16;
            for(int i=0;i<jackpotStars.Length;i++)
            {
                float age=time-spectacle.ImpactTime-i*.023f, life=Mathf.Min(1.5f,Mathf.Max(.1f,moviePlan.Duration-spectacle.ImpactTime-.04f));
                bool visible=!tensionFrame.Silent && i<stars && age>=0 && age<life && spectacle.Style!=RewardSpectacleStyle.Loss &&
                    !(activeHeroReward?.Class==RewardObjectClass.Resource && activeHeroReward.Amount<0);
                jackpotStars[i].gameObject.SetActive(visible);
                if(!visible) continue;
                float p=age/life, angle=i*2.39996f;
                jackpotStars[i].localPosition=showcaseCenter+new Vector3(Mathf.Cos(angle)*(.5f+p*2),Mathf.Sin(angle)*(.5f+p*1.5f),Mathf.Sin(i*1.7f)*.8f);
                jackpotStars[i].localRotation=Quaternion.Euler(0,0,time*45+i*17);
                jackpotStars[i].localScale=Vector3.one*Mathf.Sin(p*Mathf.PI)*(.07f+(i%3)*.025f);
            }
            movieLight.color=RewardEffectTint();
            movieLight.intensity=Mathf.Max(movieLight.intensity,(frame.Light+outcomeSubjectFocus*.28f)*(moviePlan.Event.tier>=RewardTier.Epic?3.5f:1.3f));
        }

        private void HideShowcaseOccluder(Transform item)
        {
            foreach(Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
                if(renderer.enabled) { showcaseOccluders.Add(renderer); renderer.enabled=false; }
        }

        private void FrameShowcaseInReadingAperture(float time,ref Vector3 camera,ref Vector3 look)
        {
            if(showcaseCameraWeight<=0 || showcaseSubjectRenderers==null) return;
            Vector3 fittedCamera=camera,fittedLook=look;
            FitPortraitSubject(showcaseSubjectRenderers,HorizonCinemaLayout.SubjectAperture,
                CinematicFieldOfView(time),ref fittedCamera,ref fittedLook);
            camera=Vector3.Lerp(camera,fittedCamera,showcaseCameraWeight);
            look=Vector3.Lerp(look,fittedLook,showcaseCameraWeight);
        }

        private Color RewardEffectTint()
        {
            if(spectacle.Style==RewardSpectacleStyle.Loss) return new Color(.4f,.08f,.2f);
            if(activeHeroReward!=null && activeHeroReward.Class==RewardObjectClass.Resource && activeHeroReward.Amount<0) return Palette.Coral;
            if(spectacle.Style==RewardSpectacleStyle.Mystery) return new Color(.65f,.16f,1);
            if(spectacle.Style==RewardSpectacleStyle.Legendary) return new Color(1,.66f,.14f);
            RewardObjectKind? kind=activeHeroReward?.Kind;
            if(kind==RewardObjectKind.MoneyWallet || kind==RewardObjectKind.WarmLamp) return new Color(1,.68f,.26f);
            if(kind==RewardObjectKind.FocusLens || kind==RewardObjectKind.HorizonLens || kind==RewardObjectKind.InsightPrism) return new Color(.25f,.6f,1);
            if(kind==RewardObjectKind.ConnectionRing) return new Color(.85f,.28f,.58f);
            return Palette.Mint;
        }

        private void SampleRewardMote(int index,float time,int budget)
        {
            Transform mote=rewardMotes[index]; float launch=spectacle.ImpactTime;
            if(index>=24 && spectacle.HasHero) launch=Mathf.Max(launch,spectacle.JackpotTime);
            float available=Mathf.Max(.01f,moviePlan.Duration-launch-.02f);
            float stagger=(index%8)*Mathf.Min(.028f,available*.018f);
            float age=time-launch-stagger, life=Mathf.Min(2.15f,available-stagger);
            bool visible=index<budget && age>=0 && age<life && life>.01f;
            mote.gameObject.SetActive(visible); if(!visible) return;
            float p=age/life, angle=index*2.39996f;
            float scatter=ImmersiveMotion.Smooth(p/.38f), collect=ImmersiveMotion.Flight((p-.48f)/.52f);
            Vector3 outward=new Vector3(Mathf.Cos(angle)*scatter*(1.5f+index%4*.23f),
                Mathf.Sin(scatter*Mathf.PI*.65f)*(1.25f+index%5*.15f),Mathf.Sin(angle)*scatter*.95f);
            // Decorative motes close their journey on the displayed resource.
            // Actual wallet flights and receipt counters remain the payout proof.
            Vector3 gathered=showcaseCenter+Vector3.up*.12f;
            mote.localPosition=Vector3.Lerp(showcaseCenter+outward,gathered,collect)+Vector3.up*Mathf.Sin(collect*Mathf.PI)*.35f;
            mote.localRotation=Quaternion.Euler(time*(130+index%3*30)+index*21,time*90+index*37,time*45);
            mote.localScale=Vector3.one*Mathf.Sin(p*Mathf.PI)*(.23f+(int)moviePlan.Event.tier*.04f)*(1-collect*.85f);
        }

        private void FrameJackpotStage()
        {
            if(spectacle==null) return;
            RewardSpectacleFrame frame=spectacleFrame; Color tint=RewardEffectTint();
            float heroFocus=Mathf.Max(frame.Hero,outcomeSubjectFocus);
            float burst=tensionFrame.Silent || moviePhase==CinematicPhase.HitStop?0:Mathf.Max(frame.Burst,energyFrame.Burst);
            float afterglow=1-energyFrame.Settlement*.9f;
            Vector3 center=cinematicStage.TransformPoint(showcaseCenter);
            float strength=(moviePlan.Event.tier>=RewardTier.Epic?1:moviePlan.Event.tier>=RewardTier.Combo?.55f:.23f)*(1-tensionFrame.Preview*.94f);
            Aura(jackpotHalo,center+WorldCamera.transform.forward*.6f,3.8f+frame.Charge,0,tint,(.12f+frame.Charge*.12f+heroFocus*.3f)*frame.Fade*strength*afterglow);
            Aura(jackpotRays,center+WorldCamera.transform.forward*.85f,7+heroFocus*2,1,tint,
                (burst+heroFocus*.08f+energyFrame.Afterglow*.16f)*strength*afterglow);
            Aura(jackpotFlare,center,1.2f+burst*3,0,Color.Lerp(tint,Color.white,.65f),burst*strength);
            jackpotPlaque.gameObject.SetActive(false);
            jackpotPlaque.position=WorldCamera.ViewportToWorldPoint(new Vector3(.5f,HorizonCinemaLayout.PlaqueViewportY,2));
        }

        private void Aura(Transform item,Vector3 center,float size,float mode,Color tint,float intensity)
        {
            item.gameObject.SetActive(intensity>.001f); if(intensity<=.001f) return;
            item.position=center; item.rotation=WorldCamera.transform.rotation; item.localScale=Vector3.one*size;
            auraProperties.Clear(); auraProperties.SetColor("_Color",tint); auraProperties.SetFloat("_Intensity",intensity);
            auraProperties.SetFloat("_Mode",mode); auraProperties.SetFloat("_Clock",preferences.reducedMotion?0:spectacleFrame.Time);
            item.GetComponent<Renderer>().SetPropertyBlock(auraProperties);
        }
    }
}
