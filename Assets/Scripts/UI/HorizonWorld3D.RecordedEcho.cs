using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private Transform echoTrace, echoImprint, echoArrivalMark, echoResult;
        private readonly Transform[] echoJaws=new Transform[2];
        private readonly Dictionary<RewardObjectKind,Transform> echoObjects=new Dictionary<RewardObjectKind,Transform>();
        private readonly List<Transform> echoActiveObjects=new List<Transform>();
        private readonly List<float> echoObjectScales=new List<float>();
        private readonly List<Vector3> echoObjectCenters=new List<Vector3>();
        private Renderer[] echoSubjects;
        private LineRenderer echoRoute, echoPulse;
        private Renderer echoFire, echoCore;
        private ScenicEnergy echoScenic;
        private Light echoLamp, echoWindow;
        private HorizonActorPerformance echoActor;
        private Color echoTint;
        private AudioClip echoPaper, echoRail, echoSeat, echoEnd;
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
            echoActor=HorizonActorPerformance.Attach(moviePlayer.GetComponent<HorizonActor>());
            echoRoute=CausalLine(echoTrace,"Illuminated contact path",Vector3.zero,Vector3.zero,glass,.033f);
            EnsureCaseEffectMaterial();
            echoPulse=CausalLine(echoTrace,"Moving copper trace",Vector3.zero,Vector3.zero,caseEffectMaterial,.057f);
            echoScenic=CreateScenicEnergy(echoTrace);
            echoFire=CaseQuad(echoTrace,"Seated echo flame");
            echoCore=CaseQuad(echoTrace,"White light at the seat");
            echoPaper=ShortCueTone("Folio lifted from leather",316,.20f);
            echoRail=ShortCueTone("Archive rail resonance",445,.36f);
            echoSeat=ShortCueTone("Reader guides meet",278,.19f);
            echoEnd=ShortCueTone("Settled archive overtone",418.6f,.64f);
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
            echoActiveObjects.Clear(); echoObjectScales.Clear(); echoObjectCenters.Clear();
            foreach(MaterialReward reward in moviePlan.Objects.Where(item=>item.Class==RewardObjectClass.Resource && item.Amount>0))
            {
                if(!echoObjects.TryGetValue(reward.Kind,out Transform item))
                {
                    item=BuildRewardObject(reward.Kind,reward.Form,default);
                    item.SetParent(echoResult,false);
                    echoObjects.Add(reward.Kind,item);
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
                echoActiveObjects.Add(item);
            }
            showcaseHero=echoResult;
            echoSubjects=echoResult.GetComponentsInChildren<Renderer>();
            echoResult.gameObject.SetActive(false);
        }

        private static Vector3 EchoPath(float progress) =>
            Vector3.Lerp(EchoStart,EchoArrival,progress)+new Vector3(0,.46f,-.09f)*Mathf.Sin(Mathf.PI*progress);

        private void SampleRecordedEchoStage(float time)
        {
            bool still=preferences.reducedMotion;
            var story=RecordedEchoStory.Sample(time,still);
            float pose=story.PoseTime;
            float age=pose-RecordedEchoStory.ImpactAt;
            energyFrame=cinematicEnergy.Sample(still?time:pose,still);
            Vector3 folio=EchoPath(story.Progress);
            echoImprint.localPosition=folio;
            echoImprint.localRotation=Quaternion.Euler(49*(1-story.Alignment), -13+28*story.Progress, 9-9*story.Alignment);
            echoImprint.localScale=Vector3.one*(1-.37f*story.Assembly);
            echoImprint.gameObject.SetActive(true);
            echoRoute.gameObject.SetActive(!still && story.Progress>.16f && age<.40f);
            for(int i=0;i<echoRoute.positionCount;i++)
            {
                float step=i/(float)(echoRoute.positionCount-1);
                echoRoute.SetPosition(i,EchoPath(step*story.Progress));
                float front=Mathf.Lerp(Mathf.Max(0,story.Progress-.31f),story.Progress,step);
                echoPulse.SetPosition(i,EchoPath(front));
            }
            CaseEffect(echoPulse,3,Color.Lerp(echoTint,new Color(1.2f,.83f,.39f),story.Alignment),
                still || age>=0?0:Smooth(.15f,.45f,story.Progress)*.74f,0,age,cinematicEnergy.Seed);

            for(int i=0;i<echoJaws.Length;i++)
            {
                float direction=i==0?-1:1;
                float tilt=14*story.Alignment+89*story.Assembly+19*story.Release;
                echoJaws[i].localRotation=Quaternion.Euler(0,0,direction*tilt);
            }
            PoseEchoSubjects(story,age,still);

            float lean=still?0:Smooth(.22f,1.38f,pose)*(1-Smooth(2.59f,3.13f,pose));
            float earnest=still?0:Smooth(1.7f,RecordedEchoStory.HoldAt,pose)*(1-Smooth(2.92f,3.22f,pose));
            moviePlayer.localPosition=new Vector3(-.60f+.16f*lean+.09f*earnest-.13f*story.Release,.0f,
                2.77f-.24f*lean-.12f*earnest+.13f*story.Release);
            moviePlayer.localRotation=Quaternion.Euler(9*lean-13*story.Release,-21-26*earnest,-6*story.Release);
            Vector3 handTarget=age>=.33f?EchoArrival+Vector3.up*(.25f+.36f*story.Assembly):folio;
            float reach=Mathf.Max(story.Grip,.71f*earnest);
            echoActor.Play(story.Release>.06f?HorizonBodyAction.Stumble:reach>.02f?HorizonBodyAction.Reach:HorizonBodyAction.Wait,
                story.Release>.06f?story.Release*.71f:reach*.56f, .9f,echoTrace.TransformPoint(handTarget),reach);

            EnergyRelease release=energyFrame.Release;
            echoLamp.intensity=1.38f-.71f*story.Alignment+.41f*story.Rest;
            echoWindow.intensity=.51f+.32f*story.Rest;
            movieLight.transform.localPosition=(age<0?folio:EchoArrival)+new Vector3(0,.24f,-.31f);
            movieLight.range=4.4f;
            movieLight.color=Color.Lerp(echoTint,new Color(1,.75f,.44f),Mathf.Clamp01(release.Flash+release.Fire+.21f*story.Assembly));
            movieLight.intensity=.49f+.84f*energyFrame.Inward+3.4f*release.Flash+2.25f*release.Fire+.29f*story.Assembly;
            if(bloom!=null) bloom.Echo=LowCostEffects?0:.16f*release.Flash+.11f*release.Fire;
            optics.Clear();

            bool wide=cinematicOverviewRequested || still;
            float forward=wide?0:Smooth(.42f,2.58f,pose);
            float back=wide?0:Smooth(3.84f,5.31f,pose);
            float aspect=Mathf.Max(.30f,Mathf.Min(.58f,WorldCamera.aspect));
            float distance=Mathf.Max(3.0f,1.64f/aspect)+.53f*back-.59f*forward+.12f*story.Release+(wide?.78f:0);
            float lens=43f-6*forward+4*back;
            Vector3 look=Vector3.Lerp(new Vector3(-.34f,1.51f,2.26f),new Vector3(.17f,1.50f,2.23f),forward);
            look=Vector3.Lerp(look,new Vector3(-.05f,1.47f,2.27f),back);
            if(wide) look=new Vector3(-.08f,1.51f,2.26f);
            Vector3 camera=look+new Vector3(Mathf.Lerp(-.62f,.28f,forward),.71f-.26f*forward+.21f*back,-distance);
            if(!wide && echoResult.gameObject.activeSelf && age<1.91f)
            {
                Vector3 framedCamera=camera, framedLook=look;
                FitPortraitSubject(echoSubjects,HorizonCinemaLayout.SubjectAperture,lens,ref framedCamera,ref framedLook);
                float framing=Smooth(.12f,.24f,age)*(1-Smooth(1.12f,1.91f,age));
                camera=Vector3.Lerp(camera,framedCamera,framing);
                look=Vector3.Lerp(look,framedLook,framing);
            }
            CurrentCinematicShot=story.Beat.ToString();
            CurrentCinematicFraming=wide?CinematicFraming.Wide:forward>.67f?CinematicFraming.Close:CinematicFraming.Medium;
            WorldCamera.transform.position=cinematicStage.TransformPoint(camera);
            ApplyCinematicCamera(look,lens);
            FrameRecordedEcho();
        }

        private void PoseEchoSubjects(RecordedEchoStory story,float age,bool still)
        {
            echoResult.gameObject.SetActive(still || age>=.14f);
            int count=echoActiveObjects.Count;
            for(int i=0;i<count;i++)
            {
                float slot=i-(count-1)*.5f;
                float lift=still?1:Smooth(.14f+i*.036f,.68f+i*.047f,age);
                float width=count>3?.26f:count==3?.37f:.56f;
                Vector3 at=EchoArrival+new Vector3(slot*width*lift,.18f+.49f*lift+.10f*Mathf.Sin(Mathf.PI*lift),
                    -.10f+.09f*Mathf.Abs(slot)*lift);
                Transform part=echoActiveObjects[i];
                float size=(count>3?.39f:count==3?.48f:.66f)/echoObjectScales[i];
                float scale=size*Mathf.Lerp(.27f,1,lift);
                part.localScale=Vector3.one*scale;
                float tilt=still?0:16f*Mathf.Sin(lift*Mathf.PI);
                part.localRotation=Quaternion.Euler(tilt,10+slot*13-44*(1-lift),tilt*.31f);
                part.localPosition=at-part.localRotation*(echoObjectCenters[i]*scale);
            }
        }

        private void FrameRecordedEcho()
        {
            EnergyRelease release=energyFrame.Release;
            float age=energyFrame.Time-RecordedEchoStory.ImpactAt;
            float spread=Smooth(0,.72f,age);
            Vector3 worldSeat=echoTrace.TransformPoint(EchoArrival);
            Color spill=Color.Lerp(echoTint,new Color(1,.65f,.32f),Mathf.Clamp01(release.Fire+release.Flash));
            SampleScenicEnergy(echoScenic,worldSeat,1.101f,spill,0,release,cinematicEnergy.Seed);
            Vector3 viewport=WorldCamera.WorldToViewportPoint(worldSeat);
            EchoRevealOrigin=new Vector2(Mathf.Clamp(viewport.x,.16f,.84f),Mathf.Clamp(viewport.y,.36f,.71f));
            echoFire.transform.localPosition=EchoArrival+new Vector3(.018f,.23f+spread*.46f,.22f);
            echoFire.transform.rotation=WorldCamera.transform.rotation*Quaternion.Euler(0,-13,-18);
            echoFire.transform.localScale=new Vector3(.89f+spread*.99f,1.09f+spread*.82f,1);
            CaseEffect(echoFire,0,Color.Lerp(new Color(1.89f,1.38f,.83f),new Color(.86f,.93f,1.77f),spread),
                release.Fire,spread*63,age,cinematicEnergy.Seed);
            echoCore.transform.localPosition=EchoArrival+new Vector3(0,.022f,-.012f);
            echoCore.transform.rotation=WorldCamera.transform.rotation;
            echoCore.transform.localScale=new Vector3(.59f,.48f,1)*(1+.22f*release.Flash);
            CaseEffect(echoCore,1,new Color(2.14f,2.08f,1.88f),release.Flash,0,age,cinematicEnergy.Seed);
        }

        private void ApplyRecordedEchoCue(CinematicCue cue)
        {
            if(cue.Phase==CinematicPhase.Impact)
            {
                movieMuted=false;
                UpdateAudio();
                PlayRevealStrike(true,.88f);
                return;
            }
            if(preferences.reducedMotion) return;
            switch(cue.Phase)
            {
                case CinematicPhase.Charge: CueSound(echoPaper,.46f); break;
                case CinematicPhase.Escalation: CueSound(echoRail,.57f); break;
                case CinematicPhase.HeroMoment: CueSound(echoSeat,.47f); break;
                case CinematicPhase.HitStop: movieMuted=true; audioSource.Stop(); UpdateAudio(); break;
                case CinematicPhase.Settlement: CueSound(echoEnd,.36f); break;
            }
        }
    }
}
