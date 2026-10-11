using Horizon.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private Transform jackpotStage, jackpotChest, jackpotLid, jackpotCore, jackpotPlaque, jackpotNumbers;
        private Transform jackpotHalo, jackpotRays, jackpotFlare;
        private readonly Transform[] jackpotCoins=new Transform[48], jackpotStars=new Transform[16], mysteryOrbs=new Transform[3];
        private readonly MeshFilter[] jackpotGlyphs=new MeshFilter[6];
        private HorizonRewardDigits rewardDigits;
        private Mesh embossedCoin, rewardStar;
        private Material jackpotRed, jackpotViolet, jackpotAura;
        private RewardSpectacle spectacle;
        private RewardSpectacleFrame spectacleFrame;
        private MaterialPropertyBlock auraProperties;
        private int displayedJackpotMultiplier=-1;
        private float jackpotNumberChangedAt;
        private HorizonRewardSparkPool rewardSparkPool;

        private void InitializeJackpotStage()
        {
            jackpotRed=Lit(new Color(.62f,.018f,.052f));
            jackpotRed.SetFloat("_Metallic",.32f); jackpotRed.SetFloat("_Smoothness",.88f); jackpotRed.SetFloat("_Coat",.75f);
            jackpotViolet=Lit(new Color(.48f,.035f,.75f),new Color(.09f,.012f,.18f));
            jackpotViolet.SetFloat("_Metallic",.48f); jackpotViolet.SetFloat("_Smoothness",.9f); jackpotViolet.SetFloat("_Coat",.8f);
            jackpotAura=new Material(Resources.Load<Shader>("HorizonRewardAura")); materials.Add(jackpotAura);
            auraProperties=new MaterialPropertyBlock();
            embossedCoin=Own(HorizonRewardMeshes.Coin()); rewardStar=Own(HorizonRewardMeshes.Star());
            rewardDigits=new HorizonRewardDigits(mesh=>Own(mesh));
            jackpotStage=Group("Pooled 3D reward spectacle",cinematicStage);
            jackpotChest=BuildJackpotChest(jackpotStage);
            jackpotHalo=Shape(jackpotStage,"Soft reward halo",PrimitiveType.Quad,Vector3.zero,Vector3.one,jackpotAura);
            jackpotRays=Shape(jackpotStage,"Radial reward light shafts",PrimitiveType.Quad,Vector3.zero,Vector3.one,jackpotAura);
            jackpotFlare=Shape(jackpotStage,"Impact lens flare",PrimitiveType.Quad,Vector3.zero,Vector3.one,jackpotAura);
            for(int i=0;i<jackpotCoins.Length;i++)
                jackpotCoins[i]=Sculpt(jackpotStage,"Decorative embossed gold coin "+i,embossedCoin,Vector3.zero,Vector3.one,gold);
            for(int i=0;i<jackpotStars.Length;i++)
                jackpotStars[i]=Sculpt(jackpotStage,"Reward star glint "+i,rewardStar,Vector3.zero,Vector3.one,warmLight);
            for(int i=0;i<3;i++)
            {
                Transform orb=Group("Mystery reward orb "+i,jackpotStage); mysteryOrbs[i]=orb;
                Shape(orb,"Polished violet sphere",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.8f,jackpotViolet);
                Sculpt(orb,"Golden orbit",Own(HorizonSculpt.Torus(.42f,.025f)),Vector3.zero,Vector3.one,gold).localRotation=Quaternion.Euler(64,0,0);
                Sculpt(orb,"Raised mystery star",rewardStar,new Vector3(0,0,-.41f),Vector3.one*.25f,gold);
                Shape(orb,"Orb crown jewel",PrimitiveType.Sphere,new Vector3(0,.47f,0),Vector3.one*.15f,gold);
            }
            BuildJackpotPlaque();
            foreach(Renderer renderer in jackpotStage.GetComponentsInChildren<Renderer>())
            { renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false; }
            rewardSparkPool=gameObject.AddComponent<HorizonRewardSparkPool>();
            rewardSparkPool.Initialize(embossedCoin,gold,teal,pink);
            ResetJackpotStage();
        }

        private Transform BuildJackpotChest(Transform parent)
        {
            Transform chest=Group("Gold and crimson reward chest",parent);
            Transform body=Group("Sculpted chest body",chest);
            Box(body,"Crimson chest front",new Vector3(0,-.07f,-.49f),new Vector3(1.72f,.8f,.1f),jackpotRed);
            Box(body,"Crimson chest back",new Vector3(0,-.07f,.49f),new Vector3(1.72f,.8f,.1f),jackpotRed);
            Box(body,"Velvet inner floor",new Vector3(0,-.45f,0),new Vector3(1.72f,.13f,1.04f),dark);
            for(int side=-1;side<=1;side+=2)
            {
                Box(body,"Crimson chest side",new Vector3(side*.83f,-.07f,0),new Vector3(.13f,.8f,1.04f),jackpotRed);
                Box(body,"Gilded corner post",new Vector3(side*.82f,-.08f,-.54f),new Vector3(.13f,.83f,.13f),gold);
                Box(body,"Gilded chest foot",new Vector3(side*.64f,-.55f,0),new Vector3(.32f,.17f,.93f),gold);
                Box(body,"Front golden strap",new Vector3(side*.56f,-.07f,-.558f),new Vector3(.13f,.8f,.045f),gold);
                for(int rivet=0;rivet<3;rivet++)
                    Shape(body,"Raised golden rivet",PrimitiveType.Sphere,new Vector3(side*.56f,-.3f+rivet*.24f,-.59f),Vector3.one*.07f,gold);
                Transform scroll=Sculpt(body,"Gilded filigree",Own(HorizonSculpt.Torus(.2f,.025f)),new Vector3(side*.3f,-.1f,-.56f),new Vector3(1,.75f,1),gold);
                scroll.localRotation=Quaternion.Euler(0,0,side*35);
            }
            Box(body,"Lower gold frame",new Vector3(0,-.45f,-.55f),new Vector3(1.86f,.11f,.1f),gold);
            Box(body,"Upper gold frame",new Vector3(0,.32f,-.55f),new Vector3(1.86f,.1f,.1f),gold);
            Box(body,"Gold lock setting",new Vector3(0,.05f,-.575f),new Vector3(.3f,.42f,.08f),gold);
            Shape(body,"Amethyst lock jewel",PrimitiveType.Cube,new Vector3(0,.055f,-.64f),new Vector3(.19f,.19f,.12f),jackpotViolet).localRotation=Quaternion.Euler(0,0,45);
            jackpotLid=Group("Treasure lid hinge",chest); jackpotLid.localPosition=new Vector3(0,.34f,.52f);
            Mesh lid=Own(HorizonRewardMeshes.ChestLid());
            Sculpt(jackpotLid,"Arched lacquer treasure lid",lid,new Vector3(0,0,-.52f),Vector3.one,jackpotRed);
            for(int side=-1;side<=1;side+=2)
                Sculpt(jackpotLid,"Arched gold lid band",lid,new Vector3(side*.56f,0,-.52f),new Vector3(.105f,1.03f,1.04f),gold);
            Box(jackpotLid,"Lid golden lip",new Vector3(0,0,-1.045f),new Vector3(1.79f,.1f,.1f),gold);
            for(int i=0;i<5;i++)
            {
                float x=(i-2)*.15f, height=.11f+(2-Mathf.Abs(i-2))*.035f;
                Box(jackpotLid,"Crown ray "+i,new Vector3(x,.42f+height*.5f,-.52f),new Vector3(.05f,height,.07f),gold).localRotation=Quaternion.Euler(0,0,(2-i)*10);
                Shape(jackpotLid,"Crown pearl "+i,PrimitiveType.Sphere,new Vector3(x,.42f+height,-.52f),Vector3.one*.08f,gold);
            }
            jackpotCore=Shape(chest,"Treasure inner radiance",PrimitiveType.Sphere,new Vector3(0,.05f,0),Vector3.one*.48f,warmLight);
            GroundContact(chest,new Vector3(0,-.62f,0),new Vector2(2.8f,1.8f));
            StaticBatchingUtility.Combine(body.gameObject);
            return chest;
        }

        private void BuildJackpotPlaque()
        {
            jackpotPlaque=Group("3D presentation multiplier plaque",jackpotStage);
            Box(jackpotPlaque,"Gold bevel plaque",Vector3.zero,new Vector3(3.05f,.78f,.17f),gold);
            Box(jackpotPlaque,"Crimson enamel plaque",new Vector3(0,0,-.1f),new Vector3(2.89f,.62f,.1f),jackpotRed);
            for(int side=-1;side<=1;side+=2)
            {
                Sculpt(jackpotPlaque,"Plaque star ornament",rewardStar,new Vector3(side*1.6f,0,-.08f),Vector3.one*.25f,gold);
                Shape(jackpotPlaque,"Plaque amethyst",PrimitiveType.Cube,new Vector3(side*1.43f,0,-.18f),Vector3.one*.12f,jackpotViolet).localRotation=Quaternion.Euler(0,0,45);
            }
            var label=new GameObject("Presentation multiplier qualifier",typeof(TextMesh)).GetComponent<TextMesh>();
            label.transform.SetParent(jackpotPlaque,false); label.transform.localPosition=new Vector3(0,.55f,-.15f);
            label.anchor=TextAnchor.MiddleCenter; label.alignment=TextAlignment.Center; label.fontSize=32; label.characterSize=.032f;
            label.text="演出倍率"; label.color=Palette.Gold;
            if(View.Font!=null) { label.font=View.Font; label.GetComponent<MeshRenderer>().sharedMaterial=View.Font.material; }
            jackpotNumbers=Group("Extruded multiplier numerals",jackpotPlaque);
            for(int i=0;i<jackpotGlyphs.Length;i++)
            {
                Transform glyph=Group("Solid gold multiplier digit "+i,jackpotNumbers);
                jackpotGlyphs[i]=glyph.gameObject.AddComponent<MeshFilter>(); glyph.gameObject.AddComponent<MeshRenderer>().sharedMaterial=gold;
            }
        }

        private void PrepareJackpotStage(RewardPlan plan)
        {
            ResetJackpotStage(); spectacle=new RewardSpectacle(plan);
            jackpotStage.gameObject.SetActive(true); displayedJackpotMultiplier=-1;
        }

        private void ResetJackpotStage()
        {
            spectacle=null;
            if(jackpotStage==null) return;
            jackpotStage.gameObject.SetActive(false);
            jackpotChest.gameObject.SetActive(false); jackpotLid.localRotation=Quaternion.identity;
            jackpotCore.gameObject.SetActive(false); jackpotPlaque.gameObject.SetActive(false);
            foreach(Transform coin in jackpotCoins) coin.gameObject.SetActive(false);
            foreach(Transform star in jackpotStars) star.gameObject.SetActive(false);
            foreach(Transform orb in mysteryOrbs) orb.gameObject.SetActive(false);
        }

        private void SampleJackpotStage(float time,ref Vector3 camera,ref Vector3 look)
        {
            if(spectacle==null) return;
            spectacleFrame=spectacle.Sample(time,preferences.reducedMotion);
            RewardSpectacleFrame frame=spectacleFrame;
            bool mystery=spectacle.Style==RewardSpectacleStyle.Mystery;
            bool hero=frame.Hero>.001f;
            jackpotChest.gameObject.SetActive(hero && !mystery);
            jackpotChest.localPosition=new Vector3(0,1.1f,2);
            float breath=preferences.reducedMotion?0:Mathf.Sin(time*2)*.022f;
            jackpotChest.localScale=Vector3.one*(.8f+frame.Hero*.35f+breath);
            jackpotChest.localRotation=Quaternion.Euler(0,preferences.reducedMotion?-12:-12+Mathf.Sin(time*.65f)*7,0);
            jackpotLid.localRotation=Quaternion.Euler(-frame.Lid*108,0,0);
            jackpotCore.gameObject.SetActive(hero && frame.Lid>.15f && !mystery);
            jackpotCore.localScale=Vector3.one*(.35f+frame.Lid*.3f);
            for(int i=0;i<3;i++)
            {
                mysteryOrbs[i].gameObject.SetActive(hero && mystery);
                mysteryOrbs[i].localPosition=new Vector3((i-1)*1.12f,1.55f+(preferences.reducedMotion?0:Mathf.Sin(time*1.5f+i)*.12f),2);
                mysteryOrbs[i].localScale=Vector3.one*(.7f+frame.Hero*.5f);
                mysteryOrbs[i].localRotation=Quaternion.Euler(0,preferences.reducedMotion?0:Mathf.Sin(time*.8f+i)*22,0);
            }
            // Preserve each event's narrative shots; move to the treasure only
            // after the recorded event has reached its second reveal.
            if(hero && !preferences.reducedMotion)
            {
                camera=Vector3.Lerp(camera,new Vector3(1.2f,2.7f,-5.8f),frame.Hero);
                look=Vector3.Lerp(look,new Vector3(0,1.45f,2),frame.Hero);
            }
            int budget=spectacle.CoinBudget(preferences.batterySaver,adaptiveBudget.Level,preferences.reducedMotion);
            for(int i=0;i<jackpotCoins.Length;i++) SampleSpectacleCoin(i,time,budget);
            int stars=preferences.reducedMotion?0:moviePlan.Event.tier==RewardTier.Micro?3:LowCostEffects?4:16;
            for(int i=0;i<jackpotStars.Length;i++)
            {
                float age=time-spectacle.ImpactTime-i*.023f, life=Mathf.Min(1.5f,Mathf.Max(.1f,moviePlan.Duration-spectacle.ImpactTime-.04f));
                bool visible=i<stars && age>=0 && age<life && spectacle.Style!=RewardSpectacleStyle.Loss;
                jackpotStars[i].gameObject.SetActive(visible);
                if(!visible) continue;
                float p=age/life, angle=i*2.39996f;
                jackpotStars[i].localPosition=new Vector3(Mathf.Cos(angle)*(.5f+p*2),1.5f+Mathf.Sin(angle)*(.5f+p*1.5f),1.8f+Mathf.Sin(i*1.7f)*.8f);
                jackpotStars[i].localRotation=Quaternion.Euler(0,0,time*45+i*17);
                jackpotStars[i].localScale=Vector3.one*Mathf.Sin(p*Mathf.PI)*(.07f+(i%3)*.025f);
            }
            movieLight.color=spectacle.Style==RewardSpectacleStyle.Loss?new Color(.55f,.15f,.27f):mystery?new Color(.65f,.25f,1):new Color(1,.68f,.26f);
            movieLight.intensity=Mathf.Max(movieLight.intensity,frame.Light*(moviePlan.Event.tier>=RewardTier.Epic?3.5f:1.3f));
        }

        private void SampleSpectacleCoin(int index,float time,int budget)
        {
            Transform coin=jackpotCoins[index];
            float launch=spectacle.ImpactTime;
            if(index>=24 && spectacle.HasHero) launch=Mathf.Max(launch,spectacle.JackpotTime);
            float available=Mathf.Max(.01f,moviePlan.Duration-launch-.02f);
            float stagger=(index%8)*Mathf.Min(.028f,available*.018f);
            float age=time-launch-stagger, life=Mathf.Min(2.15f,available-stagger);
            bool visible=index<budget && age>=0 && age<life && life>.01f;
            coin.gameObject.SetActive(visible); if(!visible) return;
            float p=age/life, angle=index*2.39996f;
            float travel=p*2.1f;
            coin.localPosition=new Vector3(Mathf.Cos(angle)*travel*(.9f+index%4*.2f),
                1.45f+Mathf.Sin(p*Mathf.PI)*(1.25f+index%5*.15f)-p*.55f,1.8f+Mathf.Sin(angle)*travel*.7f);
            coin.localRotation=Quaternion.Euler(time*(130+index%3*30)+index*21,time*90+index*37,time*45);
            coin.localScale=Vector3.one*Mathf.Sin(p*Mathf.PI)*(.38f+(int)moviePlan.Event.tier*.07f);
        }

        private void FrameJackpotStage()
        {
            if(spectacle==null) return;
            RewardSpectacleFrame frame=spectacleFrame;
            Color tint=spectacle.Style==RewardSpectacleStyle.Mystery?new Color(.65f,.16f,1):spectacle.Style==RewardSpectacleStyle.Loss?
                new Color(.4f,.08f,.2f):spectacle.Style==RewardSpectacleStyle.Quiet?Palette.Mint:new Color(1,.66f,.14f);
            Vector3 center=cinematicStage.TransformPoint(new Vector3(0,frame.Hero>0?1.65f:1.3f,2));
            float strength=moviePlan.Event.tier>=RewardTier.Epic?1:moviePlan.Event.tier>=RewardTier.Combo?.55f:.23f;
            Aura(jackpotHalo,center+WorldCamera.transform.forward*.6f,3.8f+frame.Charge,0,tint,(.18f+frame.Charge*.2f+frame.Hero*.4f)*frame.Fade*strength);
            Aura(jackpotRays,center+WorldCamera.transform.forward*.85f,7+frame.Hero*2,1,tint,(frame.Burst+frame.Hero*.65f)*strength);
            Aura(jackpotFlare,center,1.2f+frame.Burst*3,0,new Color(1,.9f,.55f),frame.Burst*strength);
            jackpotPlaque.gameObject.SetActive(frame.ShowMultiplier);
            if(!frame.ShowMultiplier) return;
            if(displayedJackpotMultiplier!=frame.Multiplier)
            {
                displayedJackpotMultiplier=frame.Multiplier; jackpotNumberChangedAt=frame.Time;
                string copy="×"+frame.Multiplier; float width=rewardDigits.Width(copy), pen=-width*.5f;
                for(int i=0;i<jackpotGlyphs.Length;i++)
                {
                    jackpotGlyphs[i].gameObject.SetActive(i<copy.Length);
                    if(i>=copy.Length) continue;
                    jackpotGlyphs[i].sharedMesh=rewardDigits.Mesh(copy[i]);
                    jackpotGlyphs[i].transform.localPosition=new Vector3(pen,-.37f,-.19f); pen+=rewardDigits.Advance(copy[i]);
                }
                jackpotNumbers.localScale=Vector3.one*Mathf.Min(.86f,2.35f/Mathf.Max(.1f,width));
            }
            const float depth=5;
            float viewWidth=2*depth*Mathf.Tan(WorldCamera.fieldOfView*Mathf.Deg2Rad*.5f)*WorldCamera.aspect;
            float punch=preferences.reducedMotion?0:Mathf.Sin(Mathf.Clamp01((frame.Time-jackpotNumberChangedAt)/.3f)*Mathf.PI)*.12f;
            jackpotPlaque.position=WorldCamera.ViewportToWorldPoint(new Vector3(.5f,.17f,depth));
            jackpotPlaque.rotation=WorldCamera.transform.rotation;
            jackpotPlaque.localScale=Vector3.one*Mathf.Min(.78f,viewWidth*.78f/3.5f)*(1+punch)*frame.Fade;
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
