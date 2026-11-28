using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private sealed class EchoAperture
        {
            public Transform Body, Shutter;
            public Renderer Rim, Inlay;
            public LineRenderer Conduit;
        }

        private Transform echoTrace, echoImprint, echoArrivalMark, echoResult;
        private readonly Transform[] echoJaws=new Transform[2];
        private readonly LineRenderer[] echoReaderSeams=new LineRenderer[2];
        private readonly Transform[] echoGuideFlags=new Transform[RecordedEchoStory.GuideCount];
        private readonly Renderer[] echoGuideTips=new Renderer[RecordedEchoStory.GuideCount];
        private readonly AudioClip[] echoGuideNotes=new AudioClip[RecordedEchoStory.GuideCount];
        private readonly Transform[] echoSockets=new Transform[6];
        private readonly Renderer[] echoSocketInsets=new Renderer[6];
        private readonly EchoAperture[] echoApertures=new EchoAperture[6];
        private readonly LineRenderer[] echoChargeBeams=new LineRenderer[12];
        private readonly Dictionary<(RewardObjectKind,RewardObjectForm),Transform> echoObjects=
            new Dictionary<(RewardObjectKind,RewardObjectForm),Transform>();
        private readonly List<Transform> echoActiveObjects=new List<Transform>();
        private readonly List<MaterialReward> echoActiveRewards=new List<MaterialReward>();
        private readonly List<float> echoObjectScales=new List<float>();
        private readonly List<float> echoObjectHeights=new List<float>();
        private readonly List<Vector3> echoObjectCenters=new List<Vector3>();
        private LineRenderer echoRoute, echoPulse;
        private Renderer echoFire, echoCore;
        private Vector3 echoChargeOrigin;
        private float echoFocusAt;
        private ScenicEnergy echoScenic;
        private Light echoLamp, echoWindow;
        private HorizonActorPerformance echoActor;
        private Color echoTint;
        private AudioClip echoPaper, echoSeat, echoEnd;
        private static readonly Color EchoGold=new Color(1.46f,.72f,.20f);
        private static readonly Color EchoCyan=new Color(.12f,.86f,1.22f);
        private static readonly Color EchoViolet=new Color(.93f,.35f,1.48f);
        private static readonly Vector3 EchoStart=new Vector3(-.55f,1.40f,2.13f);
        private static readonly Vector3 EchoArrival=new Vector3(.22f,1.412f,2.22f);
        internal Vector2 EchoRevealOrigin { get; private set; } = new Vector2(.5f,.566f);

        private void PrepareRecordedEcho()
        {
            spectacle=new RewardSpectacle(moviePlan);
            cinematicEnergy=new CinematicEnergy(moviePlan);
            ClearRewardObjects();
            foreach(Transform tile in movieTiles) tile.gameObject.SetActive(false);
            movieFriend.gameObject.SetActive(false);
            if(echoTrace==null) BuildEchoArchive();
            echoTrace.gameObject.SetActive(true);
            var receipt=moviePlan.Event.receipt;
            RewardEvidence source=receipt.causes?.LastOrDefault(node=>node.kind==CausalNodeKind.Action && node.day==receipt.sourceDay);
            echoImprint.name="Recorded action: "+(source?.id??receipt.action);
            LifeActionTheme theme=LifeActionStoryboard.ThemeFor(moviePlan.Event);
            echoTint=theme==LifeActionTheme.Connect?new Color(.23f,.85f,.71f):
                theme==LifeActionTheme.Read || theme==LifeActionTheme.Practice?new Color(.42f,.61f,1):new Color(.59f,.70f,1);
            echoFocusAt=-10;
            PrepareEchoSubjects();
            PrepareCinemaTension();
            tensionOpeningCharged=true;
        }

        private Material ArchiveMaterial(Color color,float metal,float roughness)
        {
            Material material=Lit(color);
            material.SetFloat("_Metallic",metal);
            material.SetFloat("_Smoothness",1-roughness);
            material.SetTexture("_ArchiveFinish",Resources.Load<Texture2D>("EchoArchive/ArchiveFinish"));
            material.EnableKeyword("_ARCHIVE_FINISH");
            return material;
        }

        private void BuildEchoArchive()
        {
            echoTrace=Group("Recorded source arriving on the time route",cinematicStage);
            echoImprint=Group("Recorded action folio",echoTrace);
            Material enamel=ArchiveMaterial(new Color(.064f,.175f,.20f),.37f,.30f);
            Material bronze=ArchiveMaterial(new Color(.72f,.45f,.21f),.67f,.30f);
            Material timber=ArchiveMaterial(new Color(.26f,.133f,.088f),.04f,.47f);
            Material parchment=ArchiveMaterial(new Color(.90f,.79f,.57f),.02f,.56f);
            Material ink=ArchiveMaterial(new Color(.12f,.25f,.32f),.05f,.57f);
            MountEchoModel(echoImprint,"ArchiveFolio",Vector3.zero,enamel,bronze,timber,parchment,ink);
            MountEchoModel(echoTrace,"ArchiveReader",new Vector3(0,1.096f,2.22f),enamel,bronze,timber,parchment,ink);

            Soft(echoTrace,"Walnut reading desk",new Vector3(-.03f,1.029f,2.27f),new Vector3(2.68f,.13f,1.39f),timber);
            Soft(echoTrace,"Bronze desk apron",new Vector3(-.03f,.923f,1.64f),new Vector3(2.48f,.11f,.063f),bronze);
            for(int side=-1;side<=1;side+=2)
                Soft(echoTrace,"Desk support",new Vector3(side*.97f,.40f,2.27f),new Vector3(.19f,1.04f,1.03f),timber);
            GroundContact(echoTrace,new Vector3(0,1.045f,2.19f),new Vector2(1.95f,1.12f));
            Box(echoTrace,"Warm plaster backdrop",new Vector3(0,1.75f,4.15f),new Vector3(6.7f,3.8f,.19f),
                Lit(new Color(.19f,.245f,.277f)));
            Box(echoTrace,"Inset night window",new Vector3(.83f,2.02f,4.01f),new Vector3(1.15f,1.52f,.11f),ink);
            for(int i=0;i<5;i++)
                Box(echoTrace,"Window frame strip",new Vector3(.31f+.26f*i,2.02f,3.93f),
                    new Vector3(.036f,1.54f,.08f),timber);
            Soft(echoTrace,"Desk lamp base",new Vector3(-1.04f,1.15f,2.67f),new Vector3(.48f,.075f,.36f),bronze);
            Box(echoTrace,"Lamp support",new Vector3(-1.05f,1.47f,2.67f),new Vector3(.064f,.59f,.055f),bronze);
            Soft(echoTrace,"Lamp shade",new Vector3(-1.01f,1.82f,2.58f),new Vector3(.47f,.14f,.25f),enamel);
            Box(echoTrace,"Warm paper light",new Vector3(-1.01f,1.738f,2.58f),new Vector3(.37f,.024f,.18f),warmLight);
            echoLamp=EchoPractical("Desk bounce",new Vector3(-.91f,1.82f,2.44f),new Color(1,.71f,.45f),3.8f);
            echoWindow=EchoPractical("Window bounce",new Vector3(.84f,2.03f,3.54f),new Color(.43f,.64f,1),4.0f);

            echoArrivalMark=Group("Stable echo receipt anchor",echoTrace);
            echoArrivalMark.localPosition=EchoArrival;
            Material indicator=Lit(new Color(.19f,.30f,.29f));
            for(int i=0;i<echoGuideFlags.Length;i++)
            {
                Transform guide=Group("Echo guide "+(i+1),echoTrace);
                guide.localPosition=new Vector3(-.37f+i*.25f,1.328f,1.965f);
                Soft(guide,"Guide bearing",Vector3.zero,new Vector3(.19f,.07f,.15f),bronze);
                Transform flag=Group("Echo contact flag "+(i+1),guide);
                flag.localPosition=new Vector3(0,.027f,0);
                Soft(flag,"Guide lever",new Vector3(0,.009f,.078f),new Vector3(.09f,.028f,.165f),enamel);
                echoGuideTips[i]=Soft(flag,"Glass guide tip",new Vector3(0,.024f,.147f),
                    new Vector3(.080f,.025f,.062f),indicator).GetComponent<Renderer>();
                echoGuideFlags[i]=flag;
                echoGuideNotes[i]=ShortCueTone("Archive guide "+(i+1),349.2f*Mathf.Pow(2,i*.25f),.18f+i*.025f);
            }
            Transform echoLeaves=Group("Opening reader leaves",echoArrivalMark);
            for(int i=0;i<echoJaws.Length;i++)
            {
                int side=i==0?-1:1;
                Transform hinge=Group("Archive leaf hinge "+side,echoLeaves);
                hinge.localPosition=new Vector3(side*.27f,-.016f,0);
                Soft(hinge,"Enamel shutter",new Vector3(-side*.121f,.026f,0),new Vector3(.248f,.053f,.437f),enamel);
                Soft(hinge,"Etched bronze edge",new Vector3(-side*.235f,.056f,0),new Vector3(.022f,.019f,.41f),bronze);
                for(int pin=-1;pin<=1;pin+=2)
                    Shape(hinge,"Axle pin",PrimitiveType.Sphere,new Vector3(0,.026f,pin*.19f),Vector3.one*.053f,bronze);
                echoJaws[i]=hinge;
            }
            echoResult=Group("Assembled actual resources",echoTrace);
            for(int i=0;i<echoSockets.Length;i++)
            {
                Transform socket=Group("Echo result mount "+(i+1),echoTrace);
                Soft(socket,"Bronze socket",new Vector3(0,.029f,0),new Vector3(.23f,.058f,.20f),bronze);
                echoSocketInsets[i]=Soft(socket,"Recessed contact glass",new Vector3(0,.056f,0),
                    new Vector3(.18f,.018f,.15f),indicator).GetComponent<Renderer>();
                echoSockets[i]=socket;
            }
            echoActor=HorizonActorPerformance.Attach(moviePlayer.GetComponent<HorizonActor>());
            echoRoute=CausalLine(echoTrace,"Illuminated contact path",Vector3.zero,Vector3.zero,glass,.033f);
            EnsureCaseEffectMaterial();
            for(int i=0;i<echoReaderSeams.Length;i++)
                echoReaderSeams[i]=CaseRibbon(echoTrace,"Reader seam light "+(i+1));
            echoPulse=CausalLine(echoTrace,"Moving copper trace",Vector3.zero,Vector3.zero,caseEffectMaterial,.057f);
            echoScenic=CreateScenicEnergy(echoTrace);
            echoFire=CaseQuad(echoTrace,"Seated echo flame");
            echoCore=CaseQuad(echoTrace,"White light at the seat");
            for(int i=0;i<echoChargeBeams.Length;i++)
                echoChargeBeams[i]=CaseRibbon(echoTrace,"Reader charge beam "+(i+1));
            BuildEchoApertures(enamel,bronze,indicator);
            echoPaper=ShortCueTone("Folio lifted from leather",316,.20f);
            echoSeat=ShortCueTone("Reader guides meet",278,.19f);
            echoEnd=ShortCueTone("Settled archive overtone",418.6f,.64f);
        }

        private void BuildEchoApertures(Material enamel,Material bronze,Material indicator)
        {
            Mesh bezel=Own(HorizonSculpt.Torus(.45f,.064f));
            Mesh filament=Own(HorizonSculpt.Torus(.365f,.022f));
            for(int i=0;i<echoApertures.Length;i++)
            {
                Transform body=Group("Archive aperture "+(i+1),echoTrace);
                Sculpt(body,"Bronze aperture housing",bezel,Vector3.zero,Vector3.one,bronze);
                Renderer rim=Sculpt(body,"Glass aperture rim",filament,new Vector3(0,0,-.052f),Vector3.one,indicator)
                    .GetComponent<Renderer>();
                Transform shutter=Group("Archive shutter",body);
                shutter.localPosition=new Vector3(0,0,-.012f);
                Shape(shutter,"Enamel optical seal",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.77f,.046f,.77f),enamel)
                    .localRotation=Quaternion.Euler(90,0,0);
                Renderer inlay=Sculpt(shutter,"Engraved seal boundary",filament,Vector3.back*.061f,Vector3.one*.83f,indicator)
                    .GetComponent<Renderer>();
                for(int j=0;j<6;j++)
                {
                    float angle=j*60;
                    Vector3 at=Quaternion.Euler(0,0,angle)*new Vector3(0,.168f,-.059f);
                    Soft(shutter,"Seal radial engraving",at,new Vector3(.028f,.245f,.013f),bronze)
                        .localRotation=Quaternion.Euler(0,0,angle+16);
                }
                LineRenderer conduit=CausalLine(echoTrace,"Echo conduit "+(i+1),Vector3.zero,Vector3.zero,caseEffectMaterial,.037f);
                echoApertures[i]=new EchoAperture {Body=body,Shutter=shutter,Rim=rim,Inlay=inlay,Conduit=conduit};
            }
        }

        private void MountEchoModel(Transform parent,string name,Vector3 position,Material enamel,Material bronze,
            Material walnut,Material paper,Material ink)
        {
            Transform assembly=Group(name,parent);
            assembly.localPosition=position;
            foreach(GameObject asset in Resources.LoadAll<GameObject>("EchoArchive/"+name))
            {
                GameObject model=Instantiate(asset,assembly,false);
                model.name=asset.name;
                Material finish=asset.name=="Bronze"?bronze:asset.name=="Walnut"?walnut:
                    asset.name=="Paper"?paper:asset.name=="Etch"?ink:enamel;
                foreach(Renderer mesh in model.GetComponentsInChildren<Renderer>(true)) mesh.sharedMaterial=finish;
            }
        }

        private Light EchoPractical(string name,Vector3 position,Color color,float range)
        {
            Light lamp=Group(name,echoTrace).gameObject.AddComponent<Light>();
            lamp.transform.localPosition=position;
            lamp.type=LightType.Point;
            lamp.range=range;
            lamp.color=color;
            lamp.shadows=LightShadows.None;
            return lamp;
        }

        private void PrepareEchoSubjects()
        {
            foreach(Transform item in echoObjects.Values) item.gameObject.SetActive(false);
            echoResult.gameObject.SetActive(true);
            echoActiveObjects.Clear(); echoActiveRewards.Clear();
            echoObjectScales.Clear(); echoObjectHeights.Clear(); echoObjectCenters.Clear();
            foreach(MaterialReward reward in moviePlan.Objects.Where(item=>item.Class==RewardObjectClass.Resource && item.Amount>0))
            {
                var key=(reward.Kind,reward.Form);
                if(!echoObjects.TryGetValue(key,out Transform item))
                {
                    item=BuildRewardObject(reward.Kind,reward.Form,default);
                    item.SetParent(echoResult,false);
                    echoObjects.Add(key,item);
                }
                RestoreRewardObject(item,reward.Kind);
                item.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
                item.localScale=Vector3.one;
                item.gameObject.SetActive(true);
                Renderer[] renderers=item.GetComponentsInChildren<Renderer>();
                Bounds bounds=renderers[0].bounds;
                for(int i=1;i<renderers.Length;i++) bounds.Encapsulate(renderers[i].bounds);
                echoObjectCenters.Add(item.InverseTransformPoint(bounds.center));
                echoObjectScales.Add(Mathf.Max(.24f,bounds.size.magnitude));
                echoObjectHeights.Add(bounds.extents.y);
                echoActiveObjects.Add(item);
                echoActiveRewards.Add(reward);
            }
            foreach(Transform socket in echoSockets) socket.gameObject.SetActive(false);
            foreach(EchoAperture aperture in echoApertures)
            {
                aperture.Body.gameObject.SetActive(false);
                aperture.Conduit.gameObject.SetActive(false);
            }
            showcaseHero=echoResult;
            echoResult.gameObject.SetActive(false);
        }

        private static Vector3 EchoPath(float progress) =>
            Vector3.Lerp(EchoStart,EchoArrival,progress)+new Vector3(0,.46f,-.09f)*Mathf.Sin(Mathf.PI*progress);

        private void SampleRecordedEchoStage(float time)
        {
            bool still=preferences.reducedMotion;
            int results=echoActiveObjects.Count;
            float settle=RecordedEchoStory.SettleFor(results);
            float joinAge=time-RecordedEchoStory.ConvergenceAt(results);
            var story=RecordedEchoStory.Sample(time,still,results);
            float pose=story.PoseTime;
            float age=pose-RecordedEchoStory.ImpactAt;
            energyFrame=cinematicEnergy.Sample(still?time:pose,still);
            Vector3 folio=EchoPath(story.Progress);
            echoImprint.localPosition=folio;
            float present=Smooth(RecordedEchoStory.PrepareAt,1.42f,pose);
            echoImprint.localRotation=Quaternion.Euler((-12-55*present)*(1-story.Alignment),
                -13+28*story.Progress,9*(1-story.Alignment));
            echoImprint.localScale=Vector3.one*(1-.37f*story.Assembly);
            echoImprint.gameObject.SetActive(true);
            echoRoute.gameObject.SetActive(!still && !story.Held && pose>=RecordedEchoStory.DevelopAt && age<0);
            for(int i=0;i<echoRoute.positionCount;i++)
            {
                float step=i/(float)(echoRoute.positionCount-1);
                echoRoute.SetPosition(i,EchoPath(step*story.Progress));
                float front=Mathf.Lerp(Mathf.Max(0,story.Progress-.31f),story.Progress,step);
                echoPulse.SetPosition(i,EchoPath(front));
            }
            CaseEffect(echoPulse,3,Color.Lerp(echoTint,new Color(1.2f,.83f,.39f),story.Alignment),
                still || story.Held || age>=0?0:Smooth(.18f,.45f,story.Progress)*.63f,0,age,cinematicEnergy.Seed);

            SampleEchoGuides(pose,story,still);

            float touch=age<0 && !still?RewardFocusPulse(pose-echoFocusAt):0;
            float charge=Mathf.Clamp01(energyFrame.Inward+.15f*touch);
            Vector3 readerContact=PoseEchoReader(story,charge,age);
            echoChargeOrigin=readerContact;
            PoseEchoSubjects(story,still);

            float lean=still?0:Smooth(.32f,1.28f,pose)*(1-Smooth(1.66f,2.39f,pose));
            float earnest=still?0:story.ReaderLoad*(1-Smooth(.02f,.44f,age));
            float observe=still?0:Smooth(.78f,1.39f,age)*(1-Smooth(settle,moviePlan.Duration,pose));
            moviePlayer.localPosition=new Vector3(-.60f+.16f*lean+.09f*earnest-.13f*story.Release,.0f,
                2.77f-.24f*lean-.12f*earnest+.13f*story.Release-.11f*observe);
            moviePlayer.localRotation=Quaternion.Euler(9*lean+6*observe-13*story.Release,
                -21-26*earnest-13*observe,-6*story.Release);
            Vector3 handTarget=age>=.33f?EchoArrival+Vector3.up*(.25f+.36f*story.Assembly):
                Vector3.Lerp(folio,readerContact,earnest*.48f);
            float reach=Mathf.Max(story.Grip,.71f*earnest);
            echoActor.Play(story.Release>.06f?HorizonBodyAction.Stumble:reach>.02f?HorizonBodyAction.Reach:HorizonBodyAction.Wait,
                story.Release>.06f?story.Release*.71f:reach*.56f, .9f,echoTrace.TransformPoint(handTarget),reach);

            EnergyRelease release=energyFrame.Release;
            int focus=EchoFocus(pose,results);
            float resultAge=pose-RecordedEchoStory.ResultTurnAt(focus,results);
            float leave=focus+1<results?Smooth(1.92f,2.27f,resultAge):Smooth(settle-.20f,settle+.58f,pose);
            float prize=still?0:Smooth(-.18f,.41f,resultAge)*(1-leave);
            float impact=still?0:Smooth(0,.31f,age)*(1-Smooth(1.23f,2.18f,age));
            float seatBlend=Smooth(RecordedEchoStory.DevelopAt-.18f,RecordedEchoStory.GuideAt(0)+.34f,pose);
            Vector3 device=Vector3.Lerp(folio,readerContact,seatBlend);
            Transform item=echoActiveObjects[focus];
            Vector3 hero=echoTrace.InverseTransformPoint(item.TransformPoint(echoObjectCenters[focus]));
            hero=Vector3.Lerp(EchoGateCenter(focus,results),hero,Smooth(RecordedEchoStory.MaterializeAfter,.48f,resultAge));
            echoLamp.intensity=Mathf.Lerp(1.12f,.37f,Mathf.Max(charge,impact))+.56f*prize+.33f*story.Rest;
            echoWindow.intensity=Mathf.Lerp(.58f,.25f,Mathf.Max(charge,impact))+.21f*story.Rest;
            movieLight.transform.localPosition=Vector3.Lerp(Vector3.Lerp(device,EchoArrival,impact),hero,prize)+
                new Vector3(0,.30f,-.34f);
            movieLight.range=4.4f;
            movieLight.color=Color.Lerp(Color.Lerp(echoTint,EchoGold,Mathf.Max(charge,impact)),
                new Color(1f,.87f,.71f),prize);
            movieLight.intensity=.24f+1.93f*charge+2.6f*release.Flash+2.40f*release.Fire+1.05f*prize;
            if(bloom!=null) bloom.Echo=LowCostEffects?0:.12f*release.Flash+.10f*release.Fire;
            optics.Clear();

            bool wide=cinematicOverviewRequested || still;
            float forward=wide?0:.46f*Smooth(.64f,1.57f,pose)+.54f*Smooth(1.70f,RecordedEchoStory.HoldAt,pose);
            float back=wide?0:Smooth(settle-.16f,moviePlan.Duration-.32f,pose);
            float aspect=Mathf.Max(.30f,Mathf.Min(.58f,WorldCamera.aspect));
            float distance=Mathf.Max(3.0f,1.64f/aspect)+.53f*back-.75f*forward-.16f*touch+.68f*impact+(wide?.78f:0);
            float lens=43f-6*forward+5*impact+4*back;
            Vector3 look=Vector3.Lerp(new Vector3(-.33f,1.51f,2.26f),new Vector3(.16f,1.46f,2.22f),forward);
            look=Vector3.Lerp(look,new Vector3(-.05f,1.47f,2.27f),back);
            if(wide) look=new Vector3(-.08f,1.51f,2.26f);
            Vector3 camera=look+new Vector3(Mathf.Lerp(-.53f,.22f,forward),.87f-.22f*forward+.12f*back,-distance);
            if(!wide && prize>0)
            {
                const float prizeLens=33f;
                float size=EchoGateSize(results);
                // A distinct portrait shot follows the object from the gate to its mount.
                float closeDistance=Mathf.Max(1.34f,size*.74f/(aspect*2*Mathf.Tan(prizeLens*Mathf.Deg2Rad*.5f)*.57f));
                Vector3 detailLook=hero-Vector3.up*(size*.14f);
                Vector3 detailCamera=detailLook+new Vector3(.09f,.12f,-closeDistance);
                camera=Vector3.Lerp(camera,detailCamera,prize);
                look=Vector3.Lerp(look,detailLook,prize);
                lens=Mathf.Lerp(lens,prizeLens,prize);
            }
            CurrentCinematicShot=story.Beat.ToString();
            CurrentCinematicFraming=wide?CinematicFraming.Wide:prize>.62f || forward>.67f?CinematicFraming.Close:CinematicFraming.Medium;
            WorldCamera.transform.position=cinematicStage.TransformPoint(camera);
            ApplyCinematicCamera(look,lens);
            FrameRecordedEcho();
        }

        private Vector3 PoseEchoReader(RecordedEchoStory story,float charge,float age)
        {
            Vector3 center=Vector3.zero;
            Color seamTint=Color.Lerp(echoTint,EchoGold,Smooth(.10f,.56f,charge));
            for(int i=0;i<echoJaws.Length;i++)
            {
                float side=i==0?-1:1;
                float contact=RecordedEchoStory.GuideAt(i+1);
                float unlock=Smooth(contact,contact+.29f,story.PoseTime);
                float tilt=5*unlock+27*story.ReaderLoad+84*story.Assembly+14*story.Release;
                Transform jaw=echoJaws[i];
                jaw.localRotation=Quaternion.Euler(0,0,-side*tilt);
                Vector3 lip=new Vector3(-side*.235f,.064f,0);
                center+=echoTrace.InverseTransformPoint(jaw.TransformPoint(lip));
                LineRenderer seam=echoReaderSeams[i];
                float length=Mathf.Lerp(.023f,.216f,Smooth(.015f,.52f,charge));
                seam.SetPosition(0,echoTrace.InverseTransformPoint(jaw.TransformPoint(lip-Vector3.forward*length)));
                seam.SetPosition(1,echoTrace.InverseTransformPoint(jaw.TransformPoint(lip+Vector3.forward*length)));
                seam.widthMultiplier=Mathf.Lerp(.013f,.047f,charge);
                CaseEffect(seam,3,seamTint*1.5f,charge*(1.10f+story.ReaderLoad*.39f),0,age,cinematicEnergy.Seed);
            }
            return center/echoJaws.Length;
        }

        private void SampleEchoGuides(float pose,RecordedEchoStory story,bool still)
        {
            Color copper=new Color(.98f,.53f,.20f);
            for(int i=0;i<echoGuideFlags.Length;i++)
            {
                float age=pose-RecordedEchoStory.GuideAt(i);
                float step=still?1:Smooth(0,.115f,age);
                float spring=still?0:ImmersiveMotion.Punch(age-.05f,.23f);
                echoGuideFlags[i].localRotation=Quaternion.Euler(-43*step-14*spring,0,0);
                EchoIndicator(echoGuideTips[i],Color.Lerp(echoTint,copper,i*.5f),step*(.39f+.95f*spring)*(1-.66f*story.Assembly));
            }
        }

        private void EchoIndicator(Renderer indicator,Color color,float intensity)
        {
            effectProperties.Clear();
            effectProperties.SetColor("_Emission",color*intensity);
            indicator.SetPropertyBlock(effectProperties);
        }

        private static float EchoGateSize(int count) => count>3?.38f:count==3?.55f:count==2?.78f:.94f;

        private static Vector3 EchoGateCenter(int index,int count)
        {
            float gap=count>3?.285f:count==3?.57f:.91f;
            float side=index-(count-1)*.5f;
            return new Vector3(.03f+side*gap,1.98f+(count>3?(index%2)*.33f:Mathf.Abs(side)*.10f),2.36f);
        }

        private static Color EchoSlotColor(int slot,int count) => Color.Lerp(EchoCyan,EchoViolet,slot/(float)Mathf.Max(1,count-1));

        private static int EchoFocus(float pose,int count)
        {
            int slot=0;
            while(slot+1<count && pose>=RecordedEchoStory.ResultTurnAt(slot+1,count)-.29f) slot++;
            return slot;
        }

        private static float EchoLocalIntensity(float age,int slot,int count)
        {
            float strength=Mathf.Lerp(.62f,.84f,slot/(float)Mathf.Max(1,count-1));
            return Smooth(.06f,.41f,age)*(1-Smooth(1.71f,2.47f,age))*strength;
        }

        private void PoseEchoSubjects(RecordedEchoStory story,bool still)
        {
            int count=echoActiveObjects.Count;
            float pose=story.PoseTime;
            float size=EchoGateSize(count);
            echoResult.gameObject.SetActive(still || pose>=RecordedEchoStory.PrizeAt+RecordedEchoStory.MaterializeAfter);
            for(int i=0;i<count;i++)
            {
                float slot=i-(count-1)*.5f;
                Vector3 gate=EchoGateCenter(i,count);
                float age=pose-RecordedEchoStory.ResultTurnAt(i,count);
                float contactAt=RecordedEchoStory.ResultContactAt(i,count);
                float face=still?1:Smooth(0,.27f,age);
                float land=still?1:Smooth(contactAt-.50f,contactAt,pose);
                float compression=still?0:ImmersiveMotion.Punch(pose-contactAt,.22f);
                float rise=still?0:Smooth(RecordedEchoStory.PrizeAt-.49f+i*.06f,RecordedEchoStory.PrizeAt+.10f+i*.06f,pose);
                float presence=rise*(1-Smooth(RecordedEchoStory.SettleFor(count)-.12f,RecordedEchoStory.SettleFor(count)+.58f,pose));
                EchoAperture aperture=echoApertures[i];
                aperture.Body.gameObject.SetActive(!still && presence>.008f);
                aperture.Body.localPosition=gate+Vector3.down*(.34f*(1-rise));
                aperture.Body.localScale=Vector3.one*size*Mathf.Lerp(.58f,1,presence);
                aperture.Body.localRotation=Quaternion.Euler(-9,-slot*8,0);
                aperture.Shutter.localRotation=Quaternion.Euler(-103*face,14*face,slot*7);
                aperture.Shutter.localPosition=new Vector3(0,.51f*face,-.013f+.14f*face);
                aperture.Shutter.gameObject.SetActive(face<.995f);
                Color color=EchoSlotColor(i,count);
                float flare=still?0:EchoLocalIntensity(age,i,count);
                float linked=Smooth(RecordedEchoStory.ConduitAt(i,count),RecordedEchoStory.ConvergenceAt(count),pose);
                EchoIndicator(aperture.Rim,Color.Lerp(color,EchoGold,linked),.11f+.64f*flare+.12f*face);
                EchoIndicator(aperture.Inlay,color,.07f+.31f*flare);
                FrameEchoConduit(aperture.Conduit,gate,i,count,pose,still);

                Transform socket=echoSockets[i];
                socket.gameObject.SetActive(still || rise>.08f);
                socket.localPosition=new Vector3(gate.x,1.24f+.10f*rise+.10f*(still?1:story.Rest)-.015f*compression,gate.z+.08f);
                EchoIndicator(echoSocketInsets[i],Color.Lerp(color,EchoGold,linked),land*(.24f+.77f*compression));
                Transform part=echoActiveObjects[i];
                float entrance=still?1:Smooth(RecordedEchoStory.MaterializeAfter,.68f,age);
                part.gameObject.SetActive(still || age>=RecordedEchoStory.MaterializeAfter);
                float scale=size*1.15f/echoObjectScales[i]*Mathf.Lerp(.62f,1,entrance);
                Vector3 airborne=gate+new Vector3(.035f*slot,.035f+.15f*entrance,-.30f*entrance);
                Vector3 settled=socket.localPosition+Vector3.up*(.065f+echoObjectHeights[i]*scale);
                Vector3 at=Vector3.Lerp(airborne,settled,land)+Vector3.up*(.048f*compression);
                float tilt=still?0:16f*Mathf.Sin(entrance*Mathf.PI)*(1-land);
                part.localScale=Vector3.one*scale;
                part.localRotation=Quaternion.Euler(tilt,slot*12-36*(1-entrance),tilt*.37f);
                part.localPosition=at-part.localRotation*(echoObjectCenters[i]*scale);
                MaterialReward reward=echoActiveRewards[i];
                float assembled=Mathf.Max(land,entrance*.85f);
                PoseResourceMechanism(part,reward.Kind,assembled);
                if(reward.Kind==RewardObjectKind.ToolKit && part.gameObject.activeInHierarchy)
                    SampleRewardDevice(part,reward,assembled);
            }
        }

        private void FrameEchoConduit(LineRenderer conduit,Vector3 gate,int slot,int count,float time,bool still)
        {
            float age=time-RecordedEchoStory.ConduitAt(slot,count);
            float travel=Smooth(0,RecordedEchoStory.ConvergenceAt(count)-RecordedEchoStory.ConduitAt(slot,count),age);
            float tail=Mathf.Max(0,travel-.44f);
            float fade=1-Smooth(.08f,.38f,time-RecordedEchoStory.ConvergenceAt(count));
            for(int i=0;i<conduit.positionCount;i++)
            {
                float t=Mathf.Lerp(tail,travel,i/(float)(conduit.positionCount-1));
                Vector3 point=Vector3.Lerp(gate+new Vector3(0,-.26f,-.10f),EchoArrival+Vector3.up*.04f,t);
                point.z-=.36f*Mathf.Sin(t*Mathf.PI);
                point.y+=.10f*Mathf.Sin(t*Mathf.PI*2);
                conduit.SetPosition(i,point);
            }
            CaseEffect(conduit,3,Color.Lerp(EchoSlotColor(slot,count),EchoGold,travel),still?0:Smooth(0,.10f,age)*fade,0,age,cinematicEnergy.Seed);
        }

        private void PlayRecordedEchoContacts(float previous,float now)
        {
            if(paused || preferences.reducedMotion) return;
            for(int i=0;i<echoGuideNotes.Length;i++)
            {
                float at=RecordedEchoStory.GuideAt(i);
                if(!CrossedRevealCue(previous,now,at)) continue;
                SetPracticeAudioTime(at);
                CueSound(echoGuideNotes[i],.38f+i*.06f);
            }
            for(int i=0;i<echoActiveObjects.Count;i++)
            {
                float openAt=RecordedEchoStory.ResultTurnAt(i,echoActiveObjects.Count)+.09f;
                if(CrossedRevealCue(previous,now,openAt))
                {
                    SetPracticeAudioTime(openAt);
                    PrepareRevealSounds();
                    CueSound(revealContacts[echoActiveObjects.Count-1-i],i==0?.40f:.58f);
                }
                float at=RecordedEchoStory.ResultContactAt(i,echoActiveObjects.Count);
                if(!CrossedRevealCue(previous,now,at)) continue;
                SetPracticeAudioTime(at);
                PrepareRevealSounds();
                CueSound(revealContacts[i],.43f);
                MasterHaptics.Cue(16,51,preferences);
            }
        }

        private void FrameRecordedEcho()
        {
            float time=energyFrame.Time;
            float age=time-RecordedEchoStory.ImpactAt;
            float touch=age<0 && !preferences.reducedMotion?RewardFocusPulse(time-echoFocusAt):0;
            float charge=Mathf.Clamp01(energyFrame.Inward+.15f*touch);
            EnergyRelease release=energyFrame.Release;
            float spread=Smooth(0,.85f,age);
            Color chargeTint=Color.Lerp(echoTint,EchoGold,Smooth(.19f,.68f,charge));
            SampleRewardChargeBeams(echoChargeBeams,echoChargeOrigin,chargeTint,charge,time,cinematicEnergy.Seed);
            Vector3 worldSeat=echoTrace.TransformPoint(EchoArrival);
            Vector3 lightSource=age<0?echoTrace.TransformPoint(echoChargeOrigin):worldSeat;
            SampleScenicEnergy(echoScenic,lightSource,1.101f,age<0?chargeTint:EchoGold,
                .71f*Smooth(.08f,.78f,charge),release,cinematicEnergy.Seed);
            Vector3 viewport=WorldCamera.WorldToViewportPoint(worldSeat);
            EchoRevealOrigin=new Vector2(Mathf.Clamp(viewport.x,.16f,.84f),Mathf.Clamp(viewport.y,.36f,.71f));
            echoFire.transform.localPosition=EchoArrival+new Vector3(.018f,.23f+spread*.46f,.07f);
            echoFire.transform.rotation=WorldCamera.transform.rotation*Quaternion.Euler(0,-13,-18);
            echoFire.transform.localScale=new Vector3(.89f+spread*.99f,1.09f+spread*.82f,1);
            CaseEffect(echoFire,0,Color.Lerp(new Color(1.89f,1.38f,.83f),new Color(.86f,.93f,1.77f),spread),
                release.Fire,spread*63,age,cinematicEnergy.Seed);
            echoCore.transform.localPosition=EchoArrival+new Vector3(0,.082f,-.082f);
            echoCore.transform.rotation=WorldCamera.transform.rotation;
            echoCore.transform.localScale=new Vector3(1.12f,.83f,1)*(1+.22f*release.Flash);
            CaseEffect(echoCore,1,new Color(2.14f,2.08f,1.88f),release.Flash,0,age,cinematicEnergy.Seed);
        }

        private void ApplyRecordedEchoCue(CinematicCue cue)
        {
            if(cue.Phase==CinematicPhase.Impact)
            {
                movieMuted=false;
                UpdateAudio();
                if(!preferences.reducedMotion) PlayRevealStrike(true,.95f);
                return;
            }
            if(preferences.reducedMotion) return;
            switch(cue.Phase)
            {
                case CinematicPhase.Charge:
                    CueSound(echoPaper,.46f);
                    PlayTensionCharge(RecordedEchoStory.HoldAt-RecordedEchoStory.PrepareAt);
                    break;
                case CinematicPhase.HeroMoment: CueSound(echoSeat,.47f); break;
                case CinematicPhase.HitStop: movieMuted=true; audioSource.Stop(); UpdateAudio(); break;
                case CinematicPhase.Settlement: CueSound(echoEnd,.36f); break;
            }
        }
    }
}
