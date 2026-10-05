using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private string inspectedSystem;
        private int inspectedValue;
        private Transform systemScenery, forgeFurnace, comfortSeat;
        private readonly List<Transform> councilPeople=new List<Transform>();
        private readonly List<TextMesh> systemLabels=new List<TextMesh>();
        private IList<CouncilVoice> inspectedVoices;
        private string councilFocus;
        private KnowledgeSkill inspectedSkill;
        private void InitializeSystemScenery()
        {
            if(systemScenery!=null) return;
            systemScenery=Group("System scene semantics",cinematicStage);
            for(int i=0;i<8;i++)
            {
                councilPeople.Add(Person("Motivation projection "+i,systemScenery,Vector3.zero,i%2==0?teal:ivory));
                var text=new GameObject("System meaning "+i,typeof(TextMesh)).GetComponent<TextMesh>();
                text.transform.SetParent(systemScenery,false); text.anchor=TextAnchor.MiddleCenter; text.characterSize=.045f; text.fontSize=40;
                if(View.Font!=null) { text.font=View.Font; text.GetComponent<Renderer>().sharedMaterial=View.Font.material; }
                systemLabels.Add(text);
            }
            forgeFurnace=Group("Knowledge furnace",systemScenery);
            Shape(forgeFurnace,"Forge chamber",PrimitiveType.Cylinder,Vector3.zero,new Vector3(1,.4f,1),dark);
            Ring(forgeFurnace,"Forge rim",Vector3.up*.4f,.52f,gold,false);
            Shape(forgeFurnace,"Knowledge heat",PrimitiveType.Sphere,Vector3.up*.3f,Vector3.one*.5f,warmLight);
            comfortSeat=Box(systemScenery,"Comfortable resting place",new Vector3(0,.3f,0),new Vector3(2,.6f,1.3f),ivory);
        }
        private void SystemLabel(int index,string text,Vector3 at)
        { TextMesh label=systemLabels[index]; label.gameObject.SetActive(true); label.text=text; label.color=Palette.Text; label.transform.localPosition=at; }

        private void BeginSystemScene(string system, Rect viewport)
        {
            Cinematics.CancelAll(); EndImaginationScene();
            imaginationSavedCamera=cameraPosition; imaginationSavedLook=cameraLook; imaginationSavedRect=WorldCamera.rect; imaginationSavedFov=WorldCamera.fieldOfView;
            imaginationSceneActive=true; imaginedScene=null; imaginationTime=0; inspectedSystem=system;
            cinematicStage.gameObject.SetActive(true); WorldCamera.rect=viewport;
            InitializeSystemScenery(); systemScenery.gameObject.SetActive(true);
            foreach(Transform person in councilPeople) person.gameObject.SetActive(false);
            foreach(TextMesh label in systemLabels) label.gameObject.SetActive(false);
            forgeFurnace.gameObject.SetActive(false); comfortSeat.gameObject.SetActive(false);
            moviePlayer.localScale=Vector3.one; ResetMovieColor(moviePlayer);
            moviePlayer.gameObject.SetActive(true); moviePlayer.localPosition=Vector3.zero; moviePlayer.localRotation=Quaternion.identity;
            moviePlayer.GetComponent<HorizonActor>().SetNeutral(); moviePlayer.GetComponent<HorizonActor>().Walking=false;
            moviePlayer.GetComponent<HorizonActor>().TiredUntil=0; moviePlayer.GetComponent<HorizonActor>().Pointing=false;
            movieAnchor.localScale=Vector3.one; movieAnchor.localPosition=Vector3.zero;
            movieFuture.gameObject.SetActive(false); movieFriend.gameObject.SetActive(false); movieAnchor.gameObject.SetActive(false); movieBarrier.gameObject.SetActive(false);
            foreach(Transform x in movieObjects) x.gameObject.SetActive(false); movieObjects.Clear(); movieObjectReceipts.Clear();
            foreach(Transform x in movieFragments) x.gameObject.SetActive(false);
            foreach(LineRenderer x in pastLines) x.gameObject.SetActive(false);
            foreach(LineRenderer x in movieLines) x.gameObject.SetActive(false);
            foreach(Transform x in movieOrbit) x.gameObject.SetActive(false);
            foreach(Transform x in movieNodes) { x.gameObject.SetActive(false); x.localScale=Vector3.one*.16f; }
            foreach(Transform x in movieTiles) { x.gameObject.SetActive(true); x.localScale=new Vector3(1.25f,.09f,.3f); }
            movieLight.color=Palette.Mint; movieLight.intensity=.55f;
            WorldCamera.transform.position=cinematicStage.TransformPoint(new Vector3(4.4f,3.8f,-5.8f));
            WorldCamera.transform.LookAt(cinematicStage.TransformPoint(new Vector3(0,1.2f,2))); WorldCamera.fieldOfView=39;
        }
        public void ShowOrbitScene(int bits)
        {
            BeginSystemScene("orbit",new Rect(0,.32f,1,.49f)); inspectedValue=bits;
            moviePlayer.gameObject.SetActive(false); movieFuture.gameObject.SetActive(true); movieFuture.localPosition=new Vector3(0,0,4.2f); movieFuture.localScale=Vector3.one*(bits==255?1.18f:1);
            for(int i=0;i<8;i++) { movieOrbit[i].gameObject.SetActive(true); SetMovieColor(movieOrbit[i],(bits&(1<<i))!=0?Palette.Mint:Palette.Muted*.3f); }
            WorldCamera.transform.LookAt(cinematicStage.TransformPoint(new Vector3(0,1.6f,4.2f)));
        }
        public void ShowReservoirScene(IList<InvestmentPool> pools)
        {
            BeginSystemScene("reservoir",new Rect(0,.69f,1,.16f)); inspectedValue=0; moviePlayer.gameObject.SetActive(false);
            Transform core=RewardObject(RewardObjectKind.ReservoirCore,0); core.localPosition=new Vector3(0,1,2); core.localScale=Vector3.one*1.6f; movieObjects.Add(core);
            int count=0; if(pools!=null) foreach(InvestmentPool pool in pools) count+=pool.sources.Count;
            for(int i=0;i<Mathf.Min(24,count);i++) { movieNodes[i].gameObject.SetActive(true); movieNodes[i].localPosition=new Vector3(Mathf.Cos(i*2.4f)*2,.5f,Mathf.Sin(i*2.4f)*2+2); inspectedValue++;
                movieLines[i].gameObject.SetActive(true); MovieLine(movieLines[i],movieNodes[i].localPosition,new Vector3(0,1,2)); }
            SystemLabel(0,"长期积累 · "+count+" 次投入",new Vector3(0,2.3f,2));
            movieLight.intensity=.25f+Mathf.Min(count,24)*.04f;
        }
        public void ShowCouncilScene(IList<CouncilVoice> voices,string selected=null)
        {
            BeginSystemScene("council",new Rect(0,.59f,1,.17f)); inspectedVoices=voices; councilFocus=selected;
            for(int i=0;i<Mathf.Min(8,voices.Count);i++)
            {
                float angle=i*Mathf.PI*2/voices.Count;
                Transform person=councilPeople[i]; person.gameObject.SetActive(true);
                person.localPosition=new Vector3(Mathf.Cos(angle)*2.5f,0,Mathf.Sin(angle)*2.5f+1);
                person.localScale=Vector3.one*(.5f+Mathf.Clamp01(voices[i].weight/65f)*.65f);
                person.LookAt(moviePlayer); SetMovieColor(person,Color.Lerp(Palette.Muted*.4f,Palette.Mint,voices[i].weight/60f));
                SystemLabel(i,voices[i].name+" "+voices[i].weight+"%",person.localPosition+Vector3.up*2);
                if(voices[i].name==selected) CueSound(nodeTone,Mathf.Clamp01(.15f+voices[i].weight*.01f));
            }
            WorldCamera.transform.LookAt(cinematicStage.TransformPoint(Vector3.up));
        }
        public void ShowForgeScene(KnowledgeSkill skill)
        {
            BeginSystemScene("forge",new Rect(0,.61f,1,.16f)); inspectedValue=(int)skill.stage; inspectedSkill=skill.Copy();
            forgeFurnace.gameObject.SetActive(true); forgeFurnace.localPosition=new Vector3(-1.7f,.8f,2);
            moviePlayer.localPosition=new Vector3(.5f,0,.3f);
            SystemLabel(0,skill.principle,new Vector3(-1.7f,2.4f,2));
            SystemLabel(1,new[] { "KNOW", "RECOGNIZE", "SIMULATE", "EXECUTE", "EXPERIENCE" }[inspectedValue],new Vector3(0,2.5f,2));
            Transform tool=RewardObject(RewardObjectKind.ToolKit,0); tool.localPosition=new Vector3(0,1,2); tool.localScale=Vector3.one*1.3f; movieObjects.Add(tool);
            for(int i=0;i<5;i++) { movieNodes[i].gameObject.SetActive(true); movieNodes[i].localPosition=new Vector3((i-2)*1.1f,.8f,2); SetMovieColor(movieNodes[i],i<=inspectedValue?Palette.Mint:Palette.Muted*.25f); }
        }
        public void ShowThoughtScene(string id)
        {
            BeginSystemScene("thought:"+id,new Rect(0,.62f,1,.18f));
            moviePlayer.GetComponent<HorizonActor>().Walking=false;
            if(id=="tomorrow") { movieAnchor.gameObject.SetActive(true); movieAnchor.localPosition=new Vector3(0,0,1); }
            if(id=="better" || id=="possibility" || id=="perfect" || id=="perfection")
                for(int i=0;i<6;i++) { movieLines[i].gameObject.SetActive(true); MovieLine(movieLines[i],new Vector3(0,.1f,0),new Vector3((i-2.5f)*.75f,.3f,5)); }
            if(id=="gaze")
                for(int i=0;i<8;i++) { movieNodes[i].gameObject.SetActive(true); movieNodes[i].localPosition=new Vector3(Mathf.Cos(i*.8f)*2.2f,1.2f,3+Mathf.Sin(i*.8f)); MovieLine(movieLines[i],movieNodes[i].localPosition,Vector3.up); movieLines[i].gameObject.SetActive(true); }
            if(id=="gaze") for(int i=0;i<6;i++) { councilPeople[i].gameObject.SetActive(true); councilPeople[i].localPosition=movieNodes[i].localPosition-Vector3.up; councilPeople[i].localScale=Vector3.one*.7f; councilPeople[i].LookAt(moviePlayer); SetMovieColor(councilPeople[i],Palette.Muted*.5f); }
            if(id=="better" || id=="possibility") { SystemLabel(0,"尚未看清的另一条路 ?",new Vector3(2,1.1f,4)); SetMovieColor(movieLines[5].transform,Palette.Gold); }
            if(id=="perfect" || id=="perfection") SystemLabel(0,"计划仍在分叉，身体停在原地",new Vector3(0,1.5f,3));
            if(id=="comfort") { movieLight.color=new Color(1,.64f,.3f); movieLight.intensity=1.2f; comfortSeat.gameObject.SetActive(true); movieAnchor.gameObject.SetActive(true); }

        }
        public void ShowMementoScene(RewardObjectKind kind)
        {
            BeginSystemScene("memento",new Rect(0,.44f,1,.36f)); moviePlayer.gameObject.SetActive(false);
            Transform item=RewardObject(kind,0); item.localPosition=new Vector3(0,1.3f,2); item.localScale=Vector3.one*1.7f;
            ResetMovieColor(item); movieObjects.Add(item);
        }
        private void SampleSystemScene()
        {
            if(inspectedSystem=="reservoir") for(int i=0;i<inspectedValue;i++)
            { float p=Mathf.Repeat(imaginationTime*.3f+i*.19f,1); movieNodes[i].localPosition=Vector3.Lerp(new Vector3(Mathf.Cos(i*2.4f)*2,.5f,Mathf.Sin(i*2.4f)*2+2),new Vector3(0,1,2),p); }
            if(inspectedSystem=="thought:tomorrow") { movieAnchor.localPosition=new Vector3(0,0,Mathf.Min(4,Mathf.Floor(imaginationTime/1.2f)*.65f)); SystemLabel(0,"目标又远了一天",new Vector3(0,2,3)); }
            if(inspectedSystem=="thought:comfort") movieAnchor.localScale=Vector3.one*Mathf.Lerp(1,.3f,Smooth(0,4,imaginationTime));
            if(inspectedSystem=="orbit")
            {
                int order=0;
                for(int i=0;i<8;i++) if((inspectedValue&(1<<i))!=0) SetMovieColor(movieOrbit[i],imaginationTime>order++*.22f?Palette.Mint:Palette.Muted*.3f);
            }
            if(inspectedSystem=="council" && councilFocus!=null)
            {
                int selected=inspectedVoices.ToList().FindIndex(v=>v.name==councilFocus);
                if(selected>=0 && selected<councilPeople.Count)
                { Vector3 target=councilPeople[selected].localPosition+Vector3.up; WorldCamera.transform.position=cinematicStage.TransformPoint(Vector3.Lerp(new Vector3(4.4f,3.8f,-5.8f),target+new Vector3(.9f,.6f,-2.8f),Smooth(0,1.4f,imaginationTime))); WorldCamera.transform.LookAt(cinematicStage.TransformPoint(target)); }
            }
            if(inspectedSystem=="forge" && movieObjects.Count>0)
            {
                float p=Smooth(0,3.2f,imaginationTime);
                for(int i=0;i<5;i++) if(i<=inspectedValue) movieNodes[i].localPosition=Vector3.Lerp(new Vector3((i-2)*1.1f,.8f,2),new Vector3(-1.7f,1.1f,2),Mathf.Sin(p*Mathf.PI));
                Transform tool=movieObjects[0]; tool.gameObject.SetActive(inspectedValue>=3); tool.localScale=Vector3.one*(.25f+.8f*p); tool.localPosition=Vector3.Lerp(new Vector3(-1.7f,1.1f,2),new Vector3(1,1,2),p);
                if(inspectedValue==0)
                { systemLabels[0].transform.localPosition=Vector3.Lerp(new Vector3(-1.7f,2.4f,2),new Vector3(-1.7f,.8f,2),p); systemLabels[0].color=new Color(1,1,1,1-p*.8f); }
                if(inspectedValue==1)
                { movieBarrier.gameObject.SetActive(true); movieBarrier.localPosition=new Vector3(0,.4f,2); movieBarrier.localScale=new Vector3(1.4f,.8f,.25f); }
                if(inspectedValue==2)
                { movieFuture.gameObject.SetActive(true); movieFuture.localPosition=new Vector3(-.8f,0,Mathf.Lerp(.3f,2.5f,p)); movieFuture.localScale=Vector3.one; SetMovieColor(movieFuture,Palette.Mint*.65f); HorizonActorPerformance.Attach(movieFuture.GetComponent<HorizonActor>()).Play(HorizonBodyAction.Walk,p); }
                if(inspectedValue==3) moviePlayer.localPosition=new Vector3(.5f,0,Mathf.Lerp(.3f,1.4f,p));
                if(inspectedValue==4) { moviePlayer.gameObject.SetActive(false); tool.localPosition=new Vector3(0,1,2); SampleRewardDevice(tool,new MaterialReward(RewardObjectKind.ToolKit,RewardObjectClass.System,inspectedSkill.action,5),p); }
                var actor=HorizonActorPerformance.Attach(moviePlayer.GetComponent<HorizonActor>()); actor.Play(inspectedValue<2?HorizonBodyAction.Reach:HorizonBodyAction.Operate,p);
                if(inspectedValue>=1) SystemLabel(2,"IF · "+inspectedSkill.condition,new Vector3(0,.4f,3.8f));
                if(inspectedValue>=2) SystemLabel(3,"THEN · "+inspectedSkill.action,new Vector3(0,.1f,3.8f));
            }
            foreach(TextMesh label in systemLabels) if(label.gameObject.activeSelf) label.transform.rotation=WorldCamera.transform.rotation;
            if(inspectedSystem=="memento" && movieObjects.Count>0 && !preferences.reducedMotion) movieObjects[0].localRotation=Quaternion.Euler(0,imaginationTime*16,0);
        }
    }
}
