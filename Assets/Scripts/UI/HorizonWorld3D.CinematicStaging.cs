using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private Transform cinemaNarrative;
        private readonly List<TextMesh> movieLabels=new List<TextMesh>();
        private readonly List<Transform> convergenceNodes=new List<Transform>();
        private readonly List<LineRenderer> patternCracks=new List<LineRenderer>();
        private Transform horizonPlatform, causalPulse;
        private IReadOnlyList<CinematicShot> patternStoryboard;
        private HorizonActorPerformance moviePerformance;
        public string CurrentCinematicShot { get; private set; } = "";
        public CinematicFraming CurrentCinematicFraming { get; private set; } = CinematicFraming.Wide;

        private void InitializeCinematicStaging()
        {
            cinemaNarrative=Group("Cinematic semantic staging",cinematicStage);
            for(int i=0;i<26;i++)
            {
                var label=new GameObject("Cinematic evidence label "+i,typeof(TextMesh)).GetComponent<TextMesh>();
                label.transform.SetParent(cinemaNarrative,false); label.anchor=TextAnchor.MiddleCenter;
                label.alignment=TextAlignment.Center; label.fontSize=42; label.characterSize=.052f;
                if(View.Font!=null) { label.font=View.Font; label.GetComponent<MeshRenderer>().sharedMaterial=View.Font.material; }
                label.color=Palette.Text; movieLabels.Add(label);
            }
            for(int i=0;i<3;i++)
            {
                Transform node=Group(i==0?"IMAGINATION node":i==1?"SIMULATION node":"REALITY node",cinemaNarrative);
                Box(node,"Event foundation",Vector3.zero,new Vector3(.65f,.18f,.55f),i==2?wood:teal);
                Ring(node,"Locked evidence ring",new Vector3(0,.16f,0),.29f,i==2?warmLight:portalLight,false);
                convergenceNodes.Add(node);
            }
            for(int i=0;i<5;i++)
            {
                var crack=CausalLine(cinemaNarrative,"Pattern crack "+i,new Vector3(-.8f+i*.3f,.18f,2),new Vector3(-.5f+i*.3f,.18f,2.2f),warmLight,.028f);
                crack.positionCount=5; patternCracks.Add(crack);
            }
            horizonPlatform=Box(cinemaNarrative,"Horizon lookout platform",new Vector3(0,.25f,-.5f),new Vector3(2,.55f,1.8f),wood);
            causalPulse=Shape(cinemaNarrative,"Cause travelling from past to present",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.12f,portalLight);
            ResetCinematicStaging();
        }
        private void ResetCinematicStaging()
        {
            if(cinemaNarrative==null) return;
            cinemaNarrative.gameObject.SetActive(false); CurrentCinematicShot="";
            foreach(TextMesh label in movieLabels) { label.gameObject.SetActive(false); label.color=Palette.Text; }
            foreach(Transform node in convergenceNodes) node.gameObject.SetActive(false);
            foreach(LineRenderer crack in patternCracks) crack.gameObject.SetActive(false);
            horizonPlatform.gameObject.SetActive(false); causalPulse.gameObject.SetActive(false);
            moviePerformance?.Release(); ResetCinematicEffects();
        }
        private void PrepareCinematicStaging()
        {
            ResetCinematicStaging(); cinemaNarrative.gameObject.SetActive(true);
            moviePerformance=HorizonActorPerformance.Attach(moviePlayer.GetComponent<HorizonActor>());
            DomainEvent e=moviePlan.Event; RewardReceipt receipt=e.receipt;
            patternStoryboard=e.kind==DomainEventKind.PatternBroken?CinematicStoryboard.Pattern(moviePlan):null;
            if(e.kind==DomainEventKind.TimeEcho)
            {
                int count=Mathf.Min(24,receipt?.causes.Count??0);
                for(int i=0;i<count;i++)
                {
                    movieNodes[i].localPosition=new Vector3(0,.65f,Mathf.Lerp(-3,3,count==1?0:i/(float)(count-1)));
                    EvidenceLabel(i,"Day "+receipt.causes[i].day,movieNodes[i].localPosition+new Vector3(.6f,.25f,0));
                    if(i>0) MovieLine(movieLines[i],movieNodes[i-1].localPosition,movieNodes[i].localPosition);
                }
                EvidenceLabel(24,"Day "+e.day,new Vector3(0,2.45f,0));
            }
            if(e.kind==DomainEventKind.RealityConvergence)
                for(int i=0;i<3;i++)
                {
                    convergenceNodes[i].gameObject.SetActive(true);
                    convergenceNodes[i].localPosition=new Vector3((i-1)*2.5f,.25f,-.3f);
                    EvidenceLabel(i,i==0?"IMAGINATION":i==1?"SIMULATION":"REALITY",new Vector3((i-1)*2.5f,1.7f,-.3f));
                }
            if(e.kind==DomainEventKind.HorizonChanged || e.kind==DomainEventKind.Overdrive)
            {
                horizonPlatform.gameObject.SetActive(true);
                for(int i=0;i<2;i++)
                {
                    movieNodes[i].gameObject.SetActive(true); movieNodes[i].localPosition=new Vector3(i==0?-1.1f:1.1f,1.8f+i*.5f,4+i*2);
                    int level=receipt?.horizonLevel??1;
                    EvidenceLabel(i,level>=3?(i==0?"Day +3":level>=4?"Day +7 · 概率节点":"未显现的远方"):(i==0?"Day +1":"未显现的远方"),movieNodes[i].localPosition+Vector3.up*.4f);
                }
            }
            if(e.kind==DomainEventKind.PredictionLocked || e.kind==DomainEventKind.Synchronized || e.kind==DomainEventKind.Surprise)
            {
                EvidenceLabel(0,"PREDICTION",new Vector3(-1.5f,.8f,4.8f));
                EvidenceLabel(1,"REALITY",new Vector3(1.5f,.8f,4.8f));
            }
            if(e.kind==DomainEventKind.RealityNode) EvidenceLabel(0,"Day "+e.day+" · REALITY NODE",new Vector3(0,1.8f,2));
            PrepareCinematicEffects();
        }
        private void EvidenceLabel(int index,string text,Vector3 position)
        { TextMesh label=movieLabels[index]; label.gameObject.SetActive(true); label.text=text; label.transform.localPosition=position; }
        private void SampleCinematicStaging(float time,float impact,ref Vector3 camera,ref Vector3 look)
        {
            DomainEvent e=moviePlan.Event; RewardReceipt receipt=e.receipt; float p=time/moviePlan.Duration;
            CurrentCinematicFraming=e.tier>=RewardTier.Epic?CinematicFraming.Wide:CinematicFraming.Medium;
            CurrentCinematicShot=moviePhase.ToString();
            if(e.kind==DomainEventKind.PatternBroken) SamplePatternStoryboard(time,ref camera,ref look);
            else if(e.kind==DomainEventKind.TimeEcho)
            {
                CurrentCinematicShot=time<.12f?"Slow present":time<.35f?"Desaturate":time<.8f?"Rewind actual days":time<1.05f?"Original action stop":time<2.15f?"Cause returns":"Attribution";
                int count=Mathf.Min(24,receipt?.causes.Count??0); Vector3 source=count>0?movieNodes[0].localPosition:new Vector3(0,.65f,-3);
                float rewind=(receipt?.sourceDay??0)>0?Smooth(.35f,.8f,time):0, forward=Smooth(1.05f,2.15f,time);
                look=Vector3.Lerp(new Vector3(0,1,2),source,rewind*(1-forward));
                camera=Vector3.Lerp(new Vector3(3.7f,2.8f,-4.8f),source+new Vector3(1.8f,1.5f,-2.5f),rewind);
                camera=Vector3.Lerp(camera,new Vector3(3.7f,2.8f,-4.8f),forward);
                movieLabels[24].text=(receipt?.sourceDay??0)>0?"Day "+CinematicStoryboard.DayDuringRewind(e,time):"来路暂未显现";
                movieLabels[24].transform.localPosition=look+Vector3.up*1.1f;
                float saturation=time<1.05f?1-Smooth(.12f,.35f,time)*.85f:Smooth(1.05f,2.15f,time);
                foreach(Transform tile in movieTiles) SetMovieColor(tile,Color.Lerp(Palette.Muted*.3f,Palette.Mint*.4f,saturation));
                SetMovieColor(moviePlayer,Color.Lerp(Palette.Muted*.5f,Color.white,saturation));
                moviePlayer.GetComponent<HorizonActor>().MotionRate=time<.12f?Mathf.Lerp(1,.18f,time/.12f):time<1.05f?0:1;
                causalPulse.gameObject.SetActive(time>=1.05f && time<2.15f);
                float nodeProgress=forward*Mathf.Max(1,count-1); int node=Mathf.Min(Mathf.FloorToInt(nodeProgress),Mathf.Max(0,count-2));
                causalPulse.localPosition=count>1?Vector3.Lerp(movieNodes[node].localPosition,movieNodes[node+1].localPosition,nodeProgress-node):Vector3.Lerp(source,new Vector3(0,1,2),forward);
                for(int i=0;i<count;i++) if(i<=nodeProgress) { SetMovieColor(movieNodes[i],Palette.Mint); if(i>0) SetMovieColor(movieLines[i].transform,Palette.Mint); }
            }
            else if(e.kind==DomainEventKind.RealityConvergence)
            {
                float hitStop=moviePlan.Cues.First(c=>c.Phase==CinematicPhase.HitStop).Time;
                CurrentCinematicShot=time<hitStop*.4f?"Lock imagination":time<hitStop*.8f?"Lock simulation":time<movieImpactTime?"Reality arrives":"Three paths converge";
                for(int i=0;i<3;i++)
                {
                    float lockTime=i==0?hitStop*.2f:i==1?hitStop*.5f:hitStop;
                    float arrived=i<2?Smooth(lockTime-.35f,lockTime,time):Smooth(hitStop-.8f,hitStop,time);
                    Vector3 from=new Vector3((i-1)*2.5f,.25f,-.3f);
                    convergenceNodes[i].localPosition=from+Vector3.up*(i==2?(1-arrived)*3:0);
                    convergenceNodes[i].localScale=Vector3.one*(.7f+arrived*.3f);
                    SetMovieColor(convergenceNodes[i],Color.Lerp(Palette.Muted*.3f,i==2?Palette.Gold:Palette.Mint,arrived));
                    Vector3 end=Vector3.Lerp(new Vector3((i-1)*1.6f,1.3f,3),new Vector3(0,1.3f,3),impact);
                    MovieLine(movieLines[i],from+Vector3.up*.3f,end); movieLines[i].widthMultiplier=.025f+impact*.06f;
                }
                camera=Vector3.Lerp(new Vector3(1,3,-7),new Vector3(5.5f,6,-12),Smooth(movieImpactTime,moviePlan.Duration*.85f,time));
                look=new Vector3(0,1.2f,2); CurrentCinematicFraming=impact>.7f?CinematicFraming.ExtremeWide:CinematicFraming.Wide;
            }
            else if(e.kind==DomainEventKind.HorizonChanged || e.kind==DomainEventKind.Overdrive)
            {
                moviePlayer.localPosition=new Vector3(0,.52f,-.5f); moviePlayer.GetComponent<HorizonActor>().Pointing=true;
                float reveal=Smooth(.18f,.7f,p);
                foreach(TextMesh label in movieLabels.Where(x=>x.gameObject.activeSelf)) label.color=Color.Lerp(new Color(.4f,.45f,.5f,.15f),Palette.Text,reveal);
                foreach(Transform node in movieNodes.Where(x=>x.gameObject.activeSelf)) SetMovieColor(node,Color.Lerp(Palette.Muted*.12f,Palette.Mint,reveal));
                if(e.kind==DomainEventKind.HorizonChanged)
                {
                    camera=Vector3.Lerp(new Vector3(1.6f,2,-1.5f),new Vector3(.1f,2.05f,2.5f),Smooth(.2f,.7f,p));
                    look=new Vector3(0,2.1f,6); CurrentCinematicFraming=p<.45f?CinematicFraming.ObjectDetail:CinematicFraming.Wide;
                }
                else { camera=Vector3.Lerp(new Vector3(.2f,2,-2.4f),new Vector3(3.8f,4.8f,-8),reveal); look=new Vector3(0,1.8f,4); }
            }
            else if(e.kind==DomainEventKind.DecisionLocked)
            {
                camera=new Vector3(-.55f,2.1f,Mathf.Lerp(-4,0,Smooth(.25f,.85f,p)));
                look=new Vector3(-1.1f,1,4); CurrentCinematicShot="Follow the locked route";
            }
            else if(e.kind==DomainEventKind.Synchronized || e.kind==DomainEventKind.Surprise)
            {
                camera=Vector3.Lerp(new Vector3(2.8f,2.6f,-4.8f),new Vector3(1.3f,1.8f,1.6f),Smooth(.08f,.5f,p));
                look=new Vector3(0,.6f,3); CurrentCinematicShot=e.kind==DomainEventKind.Synchronized?"Calibration overlap":"Explain the divergence";
                int sources=Mathf.Min(6,receipt?.causes.Count??0);
                if(impact>0) for(int i=0;i<sources;i++)
                { movieLines[i+2].gameObject.SetActive(true); Vector3 end=new Vector3((i-(sources-1)*.5f)*.65f,1.1f,4);
                    MovieLine(movieLines[i+2],new Vector3(0,1.2f,1),end); SetMovieColor(movieLines[i+2].transform,Palette.Mint);
                    EvidenceLabel(i+2,"D"+receipt.causes[i].day+" · "+receipt.causes[i].label,end+Vector3.up*.35f); }
            }
            else if(e.kind==DomainEventKind.OrbitActivated || e.kind==DomainEventKind.AllLinked)
            {
                int lit=0; for(int i=0;i<8;i++) if(((receipt?.orbitBits??0)&(1<<i))!=0)
                { float at=.12f+lit++*.055f; SetMovieColor(movieOrbit[i],p>=at?Palette.Mint:Palette.Muted*.2f); }
                camera=Vector3.Lerp(new Vector3(2.5f,2.8f,.5f),new Vector3(4.5f,3.6f,-5),e.kind==DomainEventKind.AllLinked?Smooth(.4f,.8f,p):0);
                CurrentCinematicShot=e.kind==DomainEventKind.AllLinked?"Reveal the completed orbit":"Light an actual time node";
            }
            else if(e.kind==DomainEventKind.Comeback)
            {
                int repetition=Mathf.Clamp(e.multiplier,1,3); float hesitation=repetition==1?.35f:repetition==2?.16f:.04f;
                float walk=Smooth(hesitation,.85f,p); moviePlayer.localPosition=new Vector3(0,Mathf.Lerp(-.65f,0,Smooth(0,hesitation+.2f,p)),walk*2.3f);
                moviePerformance.Play(p<hesitation?HorizonBodyAction.Rest:p<hesitation+.2f?HorizonBodyAction.Stand:HorizonBodyAction.Walk,walk);
                for(int i=9;i<15;i++) { float fix=Smooth(.12f+(i-9)*.055f,.28f+(i-9)*.055f,p); movieTiles[i].localScale=new Vector3(1.25f,.09f,.3f)*fix; }
                movieBarrier.gameObject.SetActive(repetition>=3); movieBarrier.localScale=new Vector3(.45f,.1f,.3f); movieBarrier.localPosition=new Vector3(.55f,.1f,1.3f);
            }
            else if(e.kind==DomainEventKind.FutureMemory || e.kind==DomainEventKind.DejaVu)
            {
                moviePerformance.Play(HorizonBodyAction.Operate,Mathf.Min(.65f,p*2));
                for(int i=0;i<movieObjects.Count;i++) if(movieObjectReceipts[i].Kind==RewardObjectKind.MemoryFilm)
                { movieObjects[i].localPosition=new Vector3(0,1.3f,2); movieObjects[i].localScale=Vector3.one*(.45f+Smooth(.3f,.65f,p)*.35f); }
                moviePlayer.localScale=Vector3.one*(1-Smooth(.3f,.65f,p)*.85f); moviePlayer.localPosition=new Vector3(0,Smooth(.3f,.65f,p),Smooth(.3f,.65f,p)*2);
            }
            else if(e.kind==DomainEventKind.CausalSingularity && p>.65f)
            { camera=Vector3.Lerp(camera,new Vector3(7,7,-14),Smooth(.65f,.86f,p)); CurrentCinematicFraming=CinematicFraming.ExtremeWide; }
            SampleCinematicEffects(time,impact);
            foreach(TextMesh label in movieLabels) if(label.gameObject.activeSelf) label.transform.rotation=WorldCamera.transform.rotation;
        }
        private void SamplePatternStoryboard(float time,ref Vector3 camera,ref Vector3 look)
        {
            CinematicShot shot=patternStoryboard.Last(s=>s.Time<=time); int index=(int)shot.Beat;
            float next=index+1<patternStoryboard.Count?patternStoryboard[index+1].Time:moviePlan.Duration;
            float p=Smooth(shot.Time,next,time); CurrentCinematicShot=shot.Beat.ToString(); CurrentCinematicFraming=shot.Framing;
            float crossing=Smooth(patternStoryboard[7].Time,patternStoryboard[8].Time,time);
            float shatter=Smooth(patternStoryboard[8].Time,patternStoryboard[9].Time,time);
            float extension=Smooth(patternStoryboard[9].Time,patternStoryboard[10].Time,time);
            moviePlayer.localPosition=new Vector3(0,0,Mathf.Lerp(-.5f,1.65f,Smooth(0,patternStoryboard[3].Time,time))+crossing*2.6f);
            movieBarrier.localPosition=new Vector3(0,.1f,2); movieBarrier.localScale=new Vector3(1.7f*(1-shatter),.1f,.3f);
            int histories=Mathf.Min(5,moviePlan.Event.receipt?.pastFailures.Count??0);
            for(int i=0;i<histories;i++)
            {
                pastLines[i].gameObject.SetActive(index>=2 || index==1 && i==0);
                Vector3 from=new Vector3((i-2)*.8f, .06f,-2),end=new Vector3(0,.08f,2);
                MovieLine(pastLines[i],from+new Vector3((i-2)*shatter,-shatter*.4f,0),end+new Vector3((i-2)*shatter,0,-shatter));
                SetMovieColor(pastLines[i].transform,Palette.Muted*(1-shatter*.9f));
            }
            foreach(LineRenderer crack in patternCracks)
            {
                crack.gameObject.SetActive(index>=5 && index<=8); int n=patternCracks.IndexOf(crack);
                for(int j=0;j<5;j++) crack.SetPosition(j,new Vector3(-.8f+n*.32f+j*.09f,.18f,1.9f+(j%2)*.12f));
                crack.widthMultiplier=.016f+.015f*(index>=6?1:p);
            }
            for(int i=0;i<movieFragments.Count;i++)
            {
                Transform fragment=movieFragments[i]; fragment.gameObject.SetActive(index>=8 && i<(preferences.batterySaver?8:32));
                fragment.localPosition=new Vector3((i%2==0?-1:1)*(.25f+shatter*(1+i%5)*.7f),.2f+Mathf.Sin(shatter*Mathf.PI)*(.4f+i%3*.3f),2+shatter*(i%7-3)*.35f);
                fragment.localRotation=Quaternion.Euler(shatter*i*11,shatter*i*19,shatter*90); fragment.localScale=new Vector3(.16f,.045f,.3f)*(1-shatter*.8f);
            }
            for(int i=12;i<movieTiles.Count;i++) movieTiles[i].localScale=new Vector3(1.25f,.09f,.3f)*Mathf.Clamp01(extension*2-(i-12)/12f);
            movieFuture.gameObject.SetActive(index>=10); movieFuture.GetComponent<HorizonActor>().Pointing=index>=10;
            string action=moviePlan.Event.receipt?.action??"";
            HorizonBodyAction motion=index<=2?HorizonBodyAction.Walk:index==3?HorizonBodyAction.Wait:index==4?
                (action.Contains("帮助")?HorizonBodyAction.Reach:action.Contains("改变") || action.Contains("学习") || action.Contains("计划")?HorizonBodyAction.Operate:HorizonBodyAction.Walk):
                index==5 || index==6?HorizonBodyAction.Reach:index==7?HorizonBodyAction.Walk:index>=10?HorizonBodyAction.Point:HorizonBodyAction.Stand;
            moviePerformance.Play(motion,index==6?1:p);
            look=moviePlayer.localPosition+Vector3.up*1.2f;
            switch(shot.Framing)
            {
                case CinematicFraming.Close: camera=look+new Vector3(1.4f,.35f,-2.2f); break;
                case CinematicFraming.ObjectDetail: look=index==5?new Vector3(0,.2f,2):new Vector3(0,1.2f,1.1f); camera=look+new Vector3(1,.6f,-2.8f); break;
                case CinematicFraming.Medium: camera=look+new Vector3(index==3?0:2.4f,1,index==3?4.4f:-4.4f); break;
                case CinematicFraming.ExtremeWide: look=new Vector3(0,1.3f,3); camera=new Vector3(7,6,-12); break;
                default: look=new Vector3(0,1,2); camera=new Vector3(4.7f,3.8f,-6.8f); break;
            }
        }
    }
}
