using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using UnityEngine;
using Unity.Cinemachine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        public CinematicDirector Cinematics { get; private set; }
        private Transform cinematicStage, moviePlayer, movieFuture, movieFriend, movieAnchor, movieBarrier;
        private readonly List<Transform> movieNodes = new List<Transform>(), movieTiles = new List<Transform>(), movieFragments = new List<Transform>(), movieOrbit = new List<Transform>();
        private readonly List<LineRenderer> movieLines = new List<LineRenderer>(), pastLines = new List<LineRenderer>();
        private ParticleSystem movieParticles;
        private Light movieLight;
        private RewardPlan moviePlan;
        private CinematicPhase moviePhase;
        private float moviePhaseTime, movieImpactTime;
        private Vector3 savedCameraPosition, savedCameraLook;
        private Rect savedCameraRect;
        private float savedFov, savedFog;
        private AudioClip impactTone, nodeTone, heartbeatTone;
        private MaterialPropertyBlock movieProperties;
        private bool movieMuted;
        private CinemachineBrain cinematicBrain;
        private CinemachineCamera cinematicCamera;

        private void InitializeCinematics()
        {
            Cinematics = gameObject.AddComponent<CinematicDirector>(); Cinematics.Initialize(this);
            cinematicBrain = WorldCamera.gameObject.AddComponent<CinemachineBrain>();
            cinematicBrain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate; cinematicBrain.IgnoreTimeScale = true;
            cinematicBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
            cinematicCamera = new GameObject("HORIZON cinematic shot rig").AddComponent<CinemachineCamera>();
            cinematicCamera.transform.SetParent(transform, false); cinematicCamera.gameObject.SetActive(false);
            cinematicStage = Group("HORIZON cinematic stage"); cinematicStage.localPosition = new Vector3(32,0,0);
            movieProperties = new MaterialPropertyBlock();
            Box(cinematicStage,"Time space floor",new Vector3(0,-.3f,2),new Vector3(15,.25f,19),dark);
            moviePlayer=Person("Cinematic player",cinematicStage,new Vector3(0,0,-.5f),cloth);
            movieFuture=Person("Future self cut in",cinematicStage,new Vector3(0,0,6),ivory);
            movieFriend=Person("Relationship other person",cinematicStage,new Vector3(2.2f,0,2),teal);
            movieAnchor=Group("Victory anchor",cinematicStage);
            Ring(movieAnchor,"Possible future doorway",new Vector3(0,1.5f,6),1.35f,portalLight,true);
            // Everyday scene modules accompany the future, rather than a palace.
            Box(movieAnchor,"Future work desk",new Vector3(1.7f,.9f,6),new Vector3(1.45f,.12f,.7f),wood);
            Box(movieAnchor,"Future open task",new Vector3(1.7f,1.15f,6),new Vector3(.52f,.42f,.07f),teal);
            movieBarrier=Box(cinematicStage,"Historical interruption",new Vector3(0,.1f,2),new Vector3(1.7f,.1f,.3f),gold);
            for(int i=0;i<24;i++)
            {
                movieTiles.Add(Box(cinematicStage,"Path step "+i,new Vector3(0,-.09f,-1+i*.34f),new Vector3(1.25f,.09f,.3f),floor));
                movieNodes.Add(Shape(cinematicStage,"Causal node "+i,PrimitiveType.Sphere,Vector3.zero,Vector3.one*.16f,teal));
                movieLines.Add(CausalLine(cinematicStage,"Cause beam "+i,Vector3.zero,Vector3.forward,portalLight,.028f));
            }
            for(int i=0;i<5;i++) pastLines.Add(CausalLine(cinematicStage,"Recorded interrupted timeline "+i,Vector3.zero,Vector3.forward,glass,.04f));
            for(int i=0;i<32;i++) movieFragments.Add(Box(cinematicStage,"Time fracture fragment "+i,Vector3.zero,new Vector3(.16f,.045f,.3f),teal));
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;
                movieOrbit.Add(Shape(cinematicStage,"Future orbit "+MasterSpecification.OrbitNames[i],PrimitiveType.Sphere,
                    new Vector3(Mathf.Cos(a)*2,1.7f+Mathf.Sin(a)*1.1f,4.2f),Vector3.one*.22f,dark));
            }
            movieLight=new GameObject("Cinematic warm state",typeof(Light)).GetComponent<Light>();
            movieLight.transform.SetParent(cinematicStage,false); movieLight.transform.localPosition=new Vector3(0,3,0);
            movieLight.type=LightType.Point; movieLight.range=10; movieLight.shadows=LightShadows.None;
            var particleObject=new GameObject("Pooled cinematic impact",typeof(ParticleSystem)); particleObject.transform.SetParent(cinematicStage,false);
            movieParticles=particleObject.GetComponent<ParticleSystem>(); var main=movieParticles.main;
            main.playOnAwake=false; main.loop=false; main.startLifetime=.8f; main.startSpeed=3; main.startSize=.08f;
            main.maxParticles=128; main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=movieParticles.emission; emission.enabled=false;
            var shape=movieParticles.shape; shape.shapeType=ParticleSystemShapeType.Sphere; shape.radius=.15f;
            var renderer=movieParticles.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=portalLight; renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            cinematicStage.gameObject.SetActive(false);
            impactTone=ShortCueTone("Synchronous low impact",65,.24f); nodeTone=ShortCueTone("Causal node hit",220,.12f); heartbeatTone=ShortCueTone("Quiet heartbeat",48,.16f);
        }
        private AudioClip ShortCueTone(string name,float frequency,float duration)
        {
            const int rate=22050; var samples=new float[(int)(duration*rate)];
            for(int i=0;i<samples.Length;i++) { float t=i/(float)rate; samples[i]=(Mathf.Sin(t*frequency*2*Mathf.PI)+.18f*Mathf.Sin(t*frequency*3*Mathf.PI))*Mathf.Exp(-t*18)*.4f; }
            AudioClip clip=AudioClip.Create(name,samples.Length,1,rate,false); clip.SetData(samples,0); sounds.Add(clip); return clip;
        }
        public void BeginCinematic(RewardPlan plan,bool fullscreen)
        {
            EndImaginationScene(); moviePlan=plan; moviePhase=CinematicPhase.Anticipation; moviePhaseTime=0;
            movieImpactTime=plan.Cues.First(c=>!c.NodeHit && c.Phase==CinematicPhase.Impact).Time;
            savedCameraPosition=cameraPosition; savedCameraLook=cameraLook; savedCameraRect=WorldCamera.rect;
            savedFov=WorldCamera.fieldOfView; savedFog=RenderSettings.fogDensity;
            cinematicStage.gameObject.SetActive(true); WorldCamera.rect=fullscreen?new Rect(0,.18f,1,.64f):new Rect(0,.543f,1,.245f);
            cinematicCamera.gameObject.SetActive(true);
            movieFuture.localPosition = new Vector3(0,0,6); movieFuture.localScale = Vector3.one;
            movieAnchor.localPosition = Vector3.zero; SetMovieColor(movieAnchor, Palette.Mint);
            movieAnchor.Find("Future work desk").gameObject.SetActive(true); movieAnchor.Find("Future open task").gameObject.SetActive(true);
            moviePlayer.gameObject.SetActive(true); moviePlayer.localPosition=new Vector3(0,0,-.5f); moviePlayer.localRotation=Quaternion.Euler(0,180,0);
            moviePlayer.GetComponent<HorizonActor>().SetNeutral(); moviePlayer.GetComponent<HorizonActor>().Walking=false;
            movieFuture.gameObject.SetActive(false); movieFriend.gameObject.SetActive(plan.Objects.Any(o=>o.Kind==RewardObjectKind.ConnectionRing));
            movieAnchor.gameObject.SetActive(plan.Event.kind==DomainEventKind.VictoryAnchor || plan.Event.kind==DomainEventKind.Victory);
            movieBarrier.gameObject.SetActive(plan.Event.kind==DomainEventKind.PatternBroken);
            foreach(Transform x in movieFragments) x.gameObject.SetActive(false);
            foreach(LineRenderer x in pastLines) x.gameObject.SetActive(false);
            foreach(Transform x in movieOrbit) x.gameObject.SetActive(false);
            foreach(Transform x in movieNodes) x.gameObject.SetActive(false);
            foreach(LineRenderer x in movieLines) x.gameObject.SetActive(false);
            foreach(Transform x in movieTiles) { x.gameObject.SetActive(true); x.localScale=new Vector3(1.25f,.09f,.3f); }
            movieLight.intensity=0; PrepareRewardObjects(plan); PrepareCausalScene();
            movieParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); SampleCinematic(0);
        }
        private void PrepareCausalScene()
        {
            DomainEventKind kind=moviePlan.Event.kind; RewardReceipt receipt=moviePlan.Event.receipt;
            if(kind==DomainEventKind.Cascade || kind==DomainEventKind.CausalSingularity || kind==DomainEventKind.TimeEcho || kind==DomainEventKind.Overdrive)
            {
                int count=Mathf.Min(24,receipt?.causes.Count??0);
                for(int i=0;i<count;i++)
                {
                    Transform node=movieNodes[i]; node.gameObject.SetActive(true); node.name="D"+receipt.causes[i].day+" · "+receipt.causes[i].label;
                    node.localPosition=new Vector3(Mathf.Sin(i*1.6f)*1.5f,.7f+i*.07f,-1+i*.30f); SetMovieColor(node,Palette.Muted*.3f);
                    if(i>0) { movieLines[i].gameObject.SetActive(true); MovieLine(movieLines[i],movieNodes[i-1].localPosition,node.localPosition); }
                }
            }
            if(kind==DomainEventKind.PatternBroken)
            {
                int count=Mathf.Min(5,receipt?.pastFailures.Count??0);
                for(int i=0;i<count;i++) { pastLines[i].name=receipt.pastFailures[i].label; MovieLine(pastLines[i],new Vector3((i-2)*.8f,.06f,-2),new Vector3(0,.08f,2)); }
            }
            if(kind==DomainEventKind.RealityConvergence)
            {
                for(int i=0;i<3;i++) { movieLines[i].name=i==0?"IMAGINATION":i==1?"SIMULATION":"REALITY";
                    movieLines[i].gameObject.SetActive(true); MovieLine(movieLines[i],new Vector3((i-1)*3,.35f,-1),new Vector3((i-1)*1.6f,1.3f,3)); }
            }
            if(kind==DomainEventKind.PredictionLocked || kind==DomainEventKind.Synchronized || kind==DomainEventKind.Surprise || kind==DomainEventKind.DecisionLocked)
            {
                for(int i=0;i<2;i++) { movieLines[i].gameObject.SetActive(true); MovieLine(movieLines[i],new Vector3((i-1)*.55f,.1f,-1),new Vector3((i-1)*1.5f,.3f,5)); }
            }
        }
        public void ApplyCinematicCue(CinematicCue cue)
        {
            if(moviePlan==null) return;
            if(cue.NodeHit)
            {
                if(cue.NodeIndex<movieNodes.Count) { SetMovieColor(movieNodes[cue.NodeIndex],Palette.Mint); MovieBurst(movieNodes[cue.NodeIndex].position,8); }
                CueSound(nodeTone,.65f); MasterHaptics.Cue(28,80,preferences); return;
            }
            moviePhase=cue.Phase; moviePhaseTime=cue.Time;
            if(cue.Phase==CinematicPhase.Charge && moviePlan.Event.tier==RewardTier.Mythic) CueSound(heartbeatTone,.5f);
            if(cue.Phase==CinematicPhase.HitStop)
            {
                foreach (HorizonActor actor in cinematicStage.GetComponentsInChildren<HorizonActor>(true)) actor.MotionRate=0;
                movieParticles.Pause(); movieMuted=true;
                audioSource.Stop(); if(ambience!=null) ambience.mute=true;
            }
            if(cue.Phase==CinematicPhase.HeroMoment && movieMuted) { movieMuted=false; UpdateAudio(); moviePlayer.GetComponent<HorizonActor>().MotionRate=1; }
            if(cue.Phase==CinematicPhase.Impact)
            {
                movieMuted=false; UpdateAudio();
                foreach (HorizonActor actor in cinematicStage.GetComponentsInChildren<HorizonActor>(true)) actor.MotionRate=1;
                movieParticles.Play();
                Vector3 hit=cinematicStage.TransformPoint(new Vector3(0,1.3f,2));
                MovieBurst(hit,moviePlan.Event.tier>=RewardTier.Epic?64:16);
                CueSound(impactTone,moviePlan.Event.tier>=RewardTier.Epic?1:.45f);
                MasterHaptics.Impact(moviePlan.Event,preferences);
                if(!preferences.reducedMotion) { shake=moviePlan.Event.tier>=RewardTier.Epic?.11f:.025f; if(bloom!=null) bloom.Echo=.6f; }
                if(moviePlan.Event.kind!=DomainEventKind.FailAndAgain) moviePlayer.GetComponent<HorizonActor>().Celebrate();
                foreach(Transform node in movieNodes.Where(n=>n.gameObject.activeSelf)) SetMovieColor(node,Palette.Mint);
            }
            if(cue.Phase==CinematicPhase.SecondReveal && moviePlan.Event.tier>=RewardTier.Epic)
            { movieFuture.gameObject.SetActive(true); movieFuture.GetComponent<HorizonActor>().Pointing=true; }
        }
        private void CueSound(AudioClip clip,float volume)
        { if(!paused && preferences.sound && !movieMuted) { audioSource.pitch=1; audioSource.PlayOneShot(clip,volume); } }
        private void MovieBurst(Vector3 position,int requested)
        {
            if(preferences.reducedMotion) return;
            movieParticles.transform.position=position;
            movieParticles.Emit(Mathf.Min(requested,preferences.batterySaver?12:64));
        }
        private void SetMovieColor(Transform item,Color tint)
        {
            foreach(Renderer renderer in item.GetComponentsInChildren<Renderer>())
            { movieProperties.Clear(); movieProperties.SetColor("_Color",tint); movieProperties.SetColor("_BaseColor",tint); movieProperties.SetColor("_Emission",tint*.25f); renderer.SetPropertyBlock(movieProperties); }
        }
        private static void MovieLine(LineRenderer line,Vector3 from,Vector3 to)
        { for(int i=0;i<line.positionCount;i++) { float p=i/(float)(line.positionCount-1); line.SetPosition(i,Vector3.Lerp(from,to,p)+Vector3.up*Mathf.Sin(p*Mathf.PI)*.16f); } }
        private static float Smooth(float a,float b,float t) { return Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t)); }
        public void SampleCinematic(float time)
        {
            if(moviePlan==null) return;
            if (moviePhase == CinematicPhase.HitStop) time = moviePhaseTime;
            float progress=time/moviePlan.Duration, impact=Smooth(movieImpactTime,movieImpactTime+.75f,time);
            bool frozen=moviePhase==CinematicPhase.HitStop;
            DomainEventKind kind=moviePlan.Event.kind; RewardReceipt receipt=moviePlan.Event.receipt;
            Vector3 camera=new Vector3(4.6f,3.6f,-6.8f),look=new Vector3(0,1.1f,2);
            if(moviePlan.Event.tier>=RewardTier.Epic)
            {
                camera=Vector3.Lerp(new Vector3(4.6f,3.6f,-7.8f),new Vector3(.8f,2.3f,-4.4f),Smooth(.1f,.43f,progress));
                camera=Vector3.Lerp(camera,new Vector3(6,5.8f,-12),Smooth(.55f,.85f,progress));
            }
            if(kind==DomainEventKind.TimeEcho)
            {
                Vector3 source=movieNodes.FirstOrDefault(n=>n.gameObject.activeSelf)?.localPosition??new Vector3(0,1,-1);
                look=Vector3.Lerp(new Vector3(0,1,2),source,Smooth(.35f,.8f,time));
                look=Vector3.Lerp(look,new Vector3(0,1,2),Smooth(1.05f,2.15f,time));
                int count=receipt?.causes.Count??0;
                for(int i=0;i<Mathf.Min(count,24);i++) if(time>=1.05f+(i/(float)Mathf.Max(1,count))*.95f) SetMovieColor(movieNodes[i],Palette.Mint);
            }
            if(kind==DomainEventKind.PatternBroken)
            {
                int count=Mathf.Min(5,receipt?.pastFailures.Count??0);
                for(int i=0;i<count;i++) { pastLines[i].gameObject.SetActive(progress>.09f+i*.028f); SetMovieColor(pastLines[i].transform,Color.Lerp(Palette.Muted,Palette.Mint,impact));
                    MovieLine(pastLines[i],new Vector3((i-2)*.8f-impact*(i-2)*.5f,.06f-impact*.2f,-2),new Vector3(impact*(i-2)*.85f,.08f,2-impact*2)); }
                moviePlayer.localPosition=new Vector3(0,0,Mathf.Lerp(-.5f,1.72f,Smooth(0,.31f,progress))+impact*2.6f);
                moviePlayer.GetComponent<HorizonActor>().Walking=!frozen && (progress<.32f || impact>0 && impact<1);
                movieBarrier.localScale=new Vector3(1.7f*(1-impact),.1f,.3f);
                for(int i=0;i<movieFragments.Count;i++)
                {
                    Transform fragment=movieFragments[i]; fragment.gameObject.SetActive(impact>0 && (i<(preferences.batterySaver?8:32)));
                    float side=i%2==0?-1:1;
                    fragment.localPosition=new Vector3(side*(.25f+impact*(1+i%5)*.7f),.2f+Mathf.Sin(impact*Mathf.PI)*(.4f+i%3*.3f),2+impact*(i%7-3)*.35f);
                    fragment.localRotation=Quaternion.Euler(impact*i*11,impact*i*19,impact*90); fragment.localScale=new Vector3(.16f,.045f,.3f)*(1-impact*.8f);
                }
                for(int i=12;i<movieTiles.Count;i++) movieTiles[i].localScale=new Vector3(1.25f,.09f,.3f)*Smooth(.45f+(i-12)*.014f,.6f+(i-12)*.014f,progress);
            }
            if(kind==DomainEventKind.RealityConvergence)
            {
                for(int i=0;i<3;i++) { Vector3 end=Vector3.Lerp(new Vector3((i-1)*1.6f,1.3f,3),new Vector3(0,1.3f,3),impact);
                    MovieLine(movieLines[i],new Vector3((i-1)*3,.35f,-1),end); SetMovieColor(movieLines[i].transform,progress>.09f+i*.1f?i==2?Palette.Gold:Palette.Mint:Palette.Muted); }
                look=new Vector3(0,1.2f,3);
            }
            if(kind==DomainEventKind.OrbitActivated || kind==DomainEventKind.AllLinked)
            {
                movieFuture.gameObject.SetActive(true); moviePlayer.gameObject.SetActive(false); movieFuture.localPosition=new Vector3(0,0,4.2f);
                for(int i=0;i<8;i++) { movieOrbit[i].gameObject.SetActive(true); bool lit=((receipt?.orbitBits??0)&(1<<i))!=0;
                    SetMovieColor(movieOrbit[i],lit && (i!=receipt?.orbitIndex || impact>0)?Palette.Mint:Palette.Muted*.35f); }
                look=new Vector3(0,1.5f,4.2f); movieFuture.localScale=Vector3.one*(kind==DomainEventKind.AllLinked?1+impact*.18f:1);
            }
            if(kind==DomainEventKind.Synchronized || kind==DomainEventKind.Surprise)
            {
                float merge=Smooth(.15f,.55f,progress); float divergence=kind==DomainEventKind.Surprise?1:-1;
                MovieLine(movieLines[1],new Vector3(.5f,.1f,-1),new Vector3(Mathf.Lerp(1.5f,divergence*1.5f,merge),.3f,5));
                SetMovieColor(movieLines[1].transform,kind==DomainEventKind.Surprise?Palette.Gold:Palette.Mint);
            }
            if(kind==DomainEventKind.DecisionLocked) { SetMovieColor(movieLines[1].transform,Palette.Muted*.15f); SetMovieColor(movieLines[0].transform,Palette.Mint); moviePlayer.localPosition=new Vector3(0,0,impact*1.8f); }
            if(kind==DomainEventKind.FailAndAgain) { moviePlayer.localPosition=new Vector3(0,-impact*.75f,impact*.5f); moviePlayer.localRotation=Quaternion.Euler(impact*18,180,0); for(int i=10;i<15;i++) movieTiles[i].gameObject.SetActive(false); }
            if(kind==DomainEventKind.Comeback) { moviePlayer.localPosition=new Vector3(0,Mathf.Lerp(-.75f,0,Smooth(.1f,.5f,progress)),impact*2); moviePlayer.GetComponent<HorizonActor>().Walking=impact>0 && impact<1; }
            if(kind==DomainEventKind.Overdrive || kind==DomainEventKind.HorizonChanged)
            { RenderSettings.fogDensity=Mathf.Lerp(.045f,.004f,impact); foreach(Transform node in movieNodes.Where(n=>n.gameObject.activeSelf)) SetMovieColor(node,Color.Lerp(Palette.Muted*.15f,Palette.Mint,impact)); }
            SampleRewardObjects(time,impact);
            movieLight.color=moviePlan.Objects.Any(o=>o.Kind==RewardObjectKind.WarmLamp)?new Color(1,.64f,.3f):Palette.Mint;
            movieLight.intensity=impact*(moviePlan.Event.tier>=RewardTier.Epic?2:1);
            if(preferences.reducedMotion) camera=new Vector3(5,4,-8);
            WorldCamera.transform.position=cinematicStage.TransformPoint(camera);
            if(!preferences.reducedMotion && !frozen) WorldCamera.transform.position+=new Vector3(Mathf.Sin(time*65),Mathf.Cos(time*73),0)*shake;
            WorldCamera.transform.LookAt(cinematicStage.TransformPoint(look)); WorldCamera.fieldOfView=moviePlan.Event.tier>=RewardTier.Epic?Mathf.Lerp(39,48,Smooth(.55f,.85f,progress)):38;
            cinematicCamera.transform.SetPositionAndRotation(WorldCamera.transform.position, WorldCamera.transform.rotation);
            cinematicCamera.Lens.FieldOfView = WorldCamera.fieldOfView;
            cinematicCamera.Lens.NearClipPlane = .1f; cinematicCamera.Lens.FarClipPlane = 80;
            cinematicBrain.ManualUpdate();
            shake=Mathf.MoveTowards(shake,0,Time.unscaledDeltaTime*.32f);
        }
        private void SampleRewardObjects(float time,float impact)
        {
            float progress=time/moviePlan.Duration;
            for(int i=0;i<movieObjects.Count;i++)
            {
                Transform item=movieObjects[i]; MaterialReward reward=movieObjectReceipts[i];
                float arrival=Smooth(movieImpactTime+i*.035f,movieImpactTime+.65f+i*.035f,time);
                float angle=(i%5)*Mathf.PI*.4f+progress*2;
                Vector3 start=new Vector3(Mathf.Cos(angle)*1.5f,2.3f+Mathf.Sin(angle)*.25f,1+Mathf.Sin(angle)*.7f);
                Vector3 target=new Vector3(0,1.1f,1.1f);
                if(reward.Kind==RewardObjectKind.FocusLens) target=new Vector3(0,1.5f,-.25f+i*.08f);
                if(reward.Kind==RewardObjectKind.WarmLamp) target=new Vector3((i%3-1)*1.2f,.8f,1.2f);
                if(reward.Kind==RewardObjectKind.ConnectionRing) target=new Vector3(1.1f,1.2f,1);
                if(reward.Kind==RewardObjectKind.RealityMilestone) { start=new Vector3(0,4.5f,2); target=new Vector3(0,.55f,2); arrival=Smooth(movieImpactTime-.3f,movieImpactTime,time); }
                if(reward.Kind==RewardObjectKind.ConvergencePrism) target=new Vector3(0,1.3f,3);
                if(reward.Kind==RewardObjectKind.ReservoirCore) target=new Vector3(0,1.2f,2);
                if(reward.Amount<0) { Vector3 swap=start; start=target; target=swap; SetMovieColor(item,Color.Lerp(Palette.Muted,Palette.Muted*.2f,arrival)); }
                item.localPosition=Vector3.Lerp(start,target,arrival);
                float size=Smooth(0,movieImpactTime,time)*(.45f+reward.Scale*.18f);
                if(reward.Kind==RewardObjectKind.EnergyCell && reward.Amount>0) { item.localPosition=Vector3.Lerp(start,moviePlayer.localPosition+Vector3.up*1.1f,arrival); size*=1-arrival*.95f; }
                item.localScale=Vector3.one*Mathf.Max(.001f,size); item.localRotation=Quaternion.Euler(0,Mathf.Lerp(progress*160,0,arrival),0);
                if(reward.Kind==RewardObjectKind.ToolKit && item.childCount>2) { Transform tool=item.GetChild(2); tool.localPosition=new Vector3(-.17f,.3f+(1-arrival)*.7f,0); }
                if(reward.Kind==RewardObjectKind.ConnectionRing)
                { movieLines[3].gameObject.SetActive(true); MovieLine(movieLines[3],moviePlayer.localPosition+Vector3.up,movieFriend.localPosition+Vector3.up); movieLines[3].widthMultiplier=.025f+impact*.04f; }
                if(reward.Kind==RewardObjectKind.ReservoirCore)
                { Transform lid=item.Find("Storage lid"); if(lid!=null) lid.localPosition=new Vector3(0,.55f+impact*.75f,0); }
            }
        }
        public void PauseCinematic(bool value)
        {
            if(moviePlan==null) return;
            foreach(HorizonActor actor in cinematicStage.GetComponentsInChildren<HorizonActor>(true)) actor.MotionRate=value?0:moviePhase==CinematicPhase.HitStop?0:1;
            if(value) movieParticles.Pause(); else movieParticles.Play();
        }
        public void EndCinematic()
        {
            if(moviePlan==null) return;
            moviePlan=null; cinematicStage.gameObject.SetActive(false); movieParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            cinematicCamera.gameObject.SetActive(false);
            cameraPosition=savedCameraPosition; cameraLook=savedCameraLook; WorldCamera.rect=savedCameraRect; WorldCamera.fieldOfView=savedFov;
            RenderSettings.fogDensity=savedFog; movieMuted=false; audioSource.Stop(); UpdateAudio(); shake=0; SnapCamera();
            foreach (HorizonActor actor in cinematicStage.GetComponentsInChildren<HorizonActor>(true)) actor.MotionRate=1;
        }
    }
}
