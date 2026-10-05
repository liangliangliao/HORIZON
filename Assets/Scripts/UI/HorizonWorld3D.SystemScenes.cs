using System.Collections.Generic;
using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private string inspectedSystem;
        private int inspectedValue;
        private void BeginSystemScene(string system, Rect viewport)
        {
            Cinematics.CancelAll(); EndImaginationScene();
            imaginationSavedCamera=cameraPosition; imaginationSavedLook=cameraLook; imaginationSavedRect=WorldCamera.rect; imaginationSavedFov=WorldCamera.fieldOfView;
            imaginationSceneActive=true; imaginedScene=null; imaginationTime=0; inspectedSystem=system;
            cinematicStage.gameObject.SetActive(true); WorldCamera.rect=viewport;
            moviePlayer.gameObject.SetActive(true); moviePlayer.localPosition=Vector3.zero; moviePlayer.localRotation=Quaternion.identity;
            moviePlayer.GetComponent<HorizonActor>().SetNeutral(); moviePlayer.GetComponent<HorizonActor>().Walking=false;
            moviePlayer.GetComponent<HorizonActor>().TiredUntil=0; moviePlayer.GetComponent<HorizonActor>().Pointing=false;
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
            for(int i=0;i<Mathf.Min(24,count);i++) { movieNodes[i].gameObject.SetActive(true); movieNodes[i].localPosition=new Vector3(Mathf.Cos(i*2.4f)*2,.5f,Mathf.Sin(i*2.4f)*2+2); inspectedValue++; }
        }
        public void ShowCouncilScene(IList<CouncilVoice> voices)
        {
            BeginSystemScene("council",new Rect(0,.69f,1,.15f));
            for(int i=0;i<Mathf.Min(24,voices.Count);i++)
            {
                float angle=i*Mathf.PI*2/voices.Count; Transform seat=movieNodes[i]; seat.gameObject.SetActive(true);
                seat.localPosition=new Vector3(Mathf.Cos(angle)*2.3f,.9f,Mathf.Sin(angle)*2.3f); seat.localScale=Vector3.one*(.2f+voices[i].weight*.018f);
                SetMovieColor(seat,Color.Lerp(Palette.Muted,Palette.Mint,voices[i].weight/50f)); seat.name=voices[i].name+" "+voices[i].weight;
            }
            WorldCamera.transform.LookAt(cinematicStage.TransformPoint(Vector3.up));
        }
        public void ShowForgeScene(KnowledgeSkill skill)
        {
            BeginSystemScene("forge",new Rect(0,.61f,1,.16f)); inspectedValue=(int)skill.stage; moviePlayer.gameObject.SetActive(false);
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
            if(id=="comfort") { movieLight.color=new Color(1,.64f,.3f); movieLight.intensity=1.2f; }
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
            if(inspectedSystem=="thought:tomorrow") movieAnchor.localPosition=new Vector3(0,0,Smooth(0,2.4f,imaginationTime)*1.5f);
            if(inspectedSystem=="memento" && movieObjects.Count>0 && !preferences.reducedMotion) movieObjects[0].localRotation=Quaternion.Euler(0,imaginationTime*16,0);
        }
    }
}
