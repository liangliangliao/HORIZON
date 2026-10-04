using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private bool imaginationSceneActive;
        private ImagineRun imaginedScene;
        private float imaginationTime;
        private Vector3 imaginationSavedCamera, imaginationSavedLook;
        private Rect imaginationSavedRect;
        private float imaginationSavedFov;
        public void ShowImaginationScene(ImagineRun run)
        {
            if(run==null || run.timeline==null || run.timeline.Count==0) return;
            Cinematics.CancelAll(); EndImaginationScene();
            imaginationSavedCamera=cameraPosition; imaginationSavedLook=cameraLook; imaginationSavedRect=WorldCamera.rect; imaginationSavedFov=WorldCamera.fieldOfView;
            imaginedScene=run.Copy(); imaginationTime=0; imaginationSceneActive=true;
            inspectedSystem=null; movieAnchor.localPosition=Vector3.zero;
            cinematicStage.gameObject.SetActive(true); WorldCamera.rect=new Rect(0,.44f,1,.38f);
            moviePlayer.gameObject.SetActive(true); movieFuture.gameObject.SetActive(true); moviePlayer.localRotation=Quaternion.Euler(0,180,0);
            movieFuture.localScale=Vector3.one; movieFuture.localPosition=new Vector3(0,0,6); movieFuture.GetComponent<HorizonActor>().Pointing=false;
            movieAnchor.gameObject.SetActive(true); movieBarrier.gameObject.SetActive(false); movieFriend.gameObject.SetActive(false);
            foreach(Transform item in movieObjects) item.gameObject.SetActive(false); movieObjects.Clear(); movieObjectReceipts.Clear();
            foreach(Transform x in movieFragments) x.gameObject.SetActive(false);
            foreach(LineRenderer x in pastLines) x.gameObject.SetActive(false);
            foreach(LineRenderer x in movieLines) x.gameObject.SetActive(false);
            foreach(Transform x in movieOrbit) x.gameObject.SetActive(false);
            foreach(Transform x in movieNodes) x.gameObject.SetActive(false);
            foreach(Transform tile in movieTiles) { tile.gameObject.SetActive(true); tile.localScale=new Vector3(1.25f,.09f,.3f); }
            Transform desk=movieAnchor.Find("Future work desk"),task=movieAnchor.Find("Future open task");
            if(desk!=null) desk.gameObject.SetActive(run.goalFamily=="career" || run.goalFamily=="learning");
            if(task!=null) task.gameObject.SetActive(run.goalFamily=="career" || run.goalFamily=="learning");
            movieLight.intensity=.7f; movieLight.color=Palette.Mint;
            moviePlayer.GetComponent<HorizonActor>().SetNeutral();
            moviePlayer.GetComponent<HorizonActor>().TiredUntil=0;
            if(run.phase==ImaginePhase.Effort || run.phase==ImaginePhase.Adjust) moviePlayer.GetComponent<HorizonActor>().SetIntent(CardKind.Growth);
            if(run.phase==ImaginePhase.Recover || run.phase==ImaginePhase.Retry)
            { Transform kit=RewardObject(RewardObjectKind.RepairKit,0); kit.localPosition=new Vector3(.8f,.4f,1.2f); kit.localScale=Vector3.one*.6f; movieObjects.Add(kit); }
            if(run.phase==ImaginePhase.Adjust)
            {
                string actual=run.memories.Count>0?run.memories[run.memories.Count-1].actionKey:"";
                movieFriend.gameObject.SetActive(actual=="AskHelp" || run.preparationKey=="WithSupport");
                if(actual=="ChangeMethod" || run.preparationKey=="FixedTime") { Transform trigger=RewardObject(actual=="ChangeMethod"?RewardObjectKind.ToolKit:RewardObjectKind.TriggerObject,0); trigger.localPosition=new Vector3(.7f,.8f,1.4f); trigger.localScale=Vector3.one*.55f; movieObjects.Add(trigger); }
            }
            if(run.phase==ImaginePhase.Complete) { Transform film=RewardObject(RewardObjectKind.MemoryFilm,0); film.localPosition=new Vector3(0,1.4f,4.7f); film.localScale=Vector3.one*.8f; movieObjects.Add(film); }
            SampleImaginationScene();
        }
        private void SampleImaginationScene()
        {
            if(!imaginationSceneActive || imaginedScene==null) return;
            float p=Smooth(0,2.8f,imaginationTime); ImaginePhase phase=imaginedScene.phase;
            Vector3 position=new Vector3(0,0,-.5f); Vector3 camera=new Vector3(4,3.5f,-6.3f),look=new Vector3(0,1.2f,2);
            moviePlayer.localRotation=Quaternion.Euler(0,180,0); moviePlayer.GetComponent<HorizonActor>().Walking=false;
            if(phase==ImaginePhase.VictoryAnchor) { position=new Vector3(0,0,5); camera=new Vector3(3.4f,2.7f,1.2f); look=new Vector3(0,1.2f,5.6f); }
            if(phase==ImaginePhase.Preparation)
            {
                position=new Vector3(0,0,Mathf.Lerp(5,-.5f,p)); moviePlayer.GetComponent<HorizonActor>().Walking=p<1;
                for(int i=0;i<movieTiles.Count;i++) movieTiles[i].gameObject.SetActive(i<3 || i>21 || p>i/(float)movieTiles.Count);
            }
            if(phase==ImaginePhase.Effort) { position=new Vector3(0,0,Mathf.Lerp(-.5f,1.4f,p)); moviePlayer.GetComponent<HorizonActor>().Walking=p<1; }
            if(phase==ImaginePhase.Failure || phase==ImaginePhase.Recover)
            {
                float fall=phase==ImaginePhase.Failure?p:1; position=new Vector3(0,-fall*.9f,1.6f);
                moviePlayer.localRotation=Quaternion.Euler(fall*22,180,0); moviePlayer.GetComponent<HorizonActor>().Pointing=true;
                movieFuture.gameObject.SetActive(false); SetMovieColor(movieAnchor,Palette.Muted*.28f);
                for(int i=9;i<14;i++) movieTiles[i].gameObject.SetActive(false);
                movieLight.intensity=.2f;
            }
            else { moviePlayer.GetComponent<HorizonActor>().Pointing=false; SetMovieColor(movieAnchor,Palette.Mint); }
            if(phase==ImaginePhase.Retry) { position=new Vector3(0,Mathf.Lerp(-.9f,0,p),Mathf.Lerp(1.6f,2.5f,p)); moviePlayer.GetComponent<HorizonActor>().Walking=p>.4f && p<1; }
            if(phase==ImaginePhase.Adjust) { bool changed=imaginedScene.memories.Count>0 && imaginedScene.memories[imaginedScene.memories.Count-1].actionKey=="ChangeMethod";
                position=new Vector3(changed?Mathf.Sin(p*Mathf.PI)*.7f:0,0,Mathf.Lerp(2,3.6f,p)); moviePlayer.GetComponent<HorizonActor>().Walking=p<1; }
            if(phase==ImaginePhase.Victory || phase==ImaginePhase.Complete)
            {
                position=new Vector3(0,0,Mathf.Lerp(3.6f,5,p)); camera=Vector3.Lerp(camera,new Vector3(3.4f,2.7f,1.2f),p); look=Vector3.Lerp(look,new Vector3(0,1.2f,5.6f),p);
                moviePlayer.GetComponent<HorizonActor>().Walking=p<1; movieLight.intensity=1.1f;
            }
            moviePlayer.localPosition=position;
            if(preferences.reducedMotion) camera=new Vector3(5,4,-6);
            WorldCamera.transform.position=cinematicStage.TransformPoint(camera); WorldCamera.transform.LookAt(cinematicStage.TransformPoint(look)); WorldCamera.fieldOfView=39;
        }
        public void EndImaginationScene()
        {
            if(!imaginationSceneActive) return;
            imaginationSceneActive=false; imaginedScene=null; cinematicStage.gameObject.SetActive(false);
            cameraPosition=imaginationSavedCamera; cameraLook=imaginationSavedLook; WorldCamera.rect=imaginationSavedRect; WorldCamera.fieldOfView=imaginationSavedFov; SnapCamera();
        }
        private void LateUpdate()
        {
            if(imaginationSceneActive && !paused) { imaginationTime+=Time.unscaledDeltaTime; if (imaginedScene!=null) SampleImaginationScene(); else SampleSystemScene(); }
        }
    }
}
