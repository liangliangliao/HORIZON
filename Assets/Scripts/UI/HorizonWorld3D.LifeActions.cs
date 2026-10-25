using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private LifeActionStoryboard lifeAction;
        private Transform lifeStage,lifePark,lifeDesk,lifeRest,lifeContact,lifeDoor,lifeBook,lifeScreen,lifePhone;
        private Renderer[] lifeOverviewRenderers,lifeActorRenderers;
        private HorizonActorPerformance lifePerformance;
        private float lifePressure;
        private bool imaginationWalkSet;
        public LifeActionShot CurrentLifeActionShot { get; private set; }
        public bool LifeActionPlaying => lifeAction!=null && lifeAction.Enabled;

        private void PrepareImaginationLifeSet(ImagineRun run)
        {
            imaginationWalkSet=LifeActionStoryboard.ThemeForAction(run.goal)==LifeActionTheme.Walk;
            if(!imaginationWalkSet) return;
            if(lifeStage==null) BuildLifeActionStage();
            lifeStage.gameObject.SetActive(true); lifePark.gameObject.SetActive(true);
            lifeDesk.gameObject.SetActive(false); lifeRest.gameObject.SetActive(false); lifeContact.gameObject.SetActive(false);
            lifeDoor.localRotation=Quaternion.Euler(0,-102,0);
        }
        private void SampleImaginationLifeSet(ref Vector3 position)
        {
            if(!imaginationWalkSet) return;
            if(projectionWorld!=null) projectionWorld.gameObject.SetActive(false);
            movieAnchor.gameObject.SetActive(false); movieFuture.gameObject.SetActive(false);
            foreach(Transform tile in movieTiles) tile.gameObject.SetActive(false);
            position.y=Mathf.Max(-.15f,position.y);
            Vector3 home=new Vector3(-1.65f,0,-.85f),park=new Vector3(.65f,0,4.35f);
            float p=CurrentImaginationFrame.Progress;
            switch(CurrentImaginationFrame.Shot)
            {
                case ImaginationShot.VictoryAnchor: case ImaginationShot.ImaginedVictory:position=park;break;
                case ImaginationShot.Rewind:position=Vector3.Lerp(park,home,p);break;
                case ImaginationShot.BuildThePath:position=home;break;
                case ImaginationShot.Action:position=Vector3.Lerp(home,new Vector3(-.7f,0,1.7f),p);break;
                case ImaginationShot.FinalPush:position=Vector3.Lerp(new Vector3(-.7f,0,1.7f),park,p);break;
                default:position.x=-.7f;break;
            }
        }

        private void PrepareLifeAction()
        {
            lifeAction=new LifeActionStoryboard(moviePlan); lifePressure=0;
            if(!lifeAction.Enabled) { if(lifeStage!=null) lifeStage.gameObject.SetActive(false); return; }
            if(lifeStage==null) BuildLifeActionStage();
            lifeStage.gameObject.SetActive(true);
            LifeActionTheme theme=lifeAction.Theme;
            lifePark.gameObject.SetActive(theme==LifeActionTheme.Walk || theme==LifeActionTheme.Explore);
            lifeDesk.gameObject.SetActive(theme!=LifeActionTheme.Walk && theme!=LifeActionTheme.Rest && theme!=LifeActionTheme.Explore);
            lifeRest.gameObject.SetActive(theme==LifeActionTheme.Rest);
            lifeContact.gameObject.SetActive(theme==LifeActionTheme.Connect && !lifeAction.Action.Contains("消息"));
            lifeBook.gameObject.SetActive(theme==LifeActionTheme.Read || theme==LifeActionTheme.Prepare || theme==LifeActionTheme.Practice);
            lifeScreen.gameObject.SetActive(theme==LifeActionTheme.Work || theme==LifeActionTheme.Earn || theme==LifeActionTheme.Explore);
            lifePhone.gameObject.SetActive(theme==LifeActionTheme.Spend || theme==LifeActionTheme.Connect && lifeAction.Action.Contains("消息"));
            lifeActorRenderers=moviePlayer.GetComponentsInChildren<Renderer>(true);
            lifeOverviewRenderers=lifeStage.GetComponentsInChildren<Renderer>(true);
            lifePerformance=HorizonActorPerformance.Attach(moviePlayer.GetComponent<HorizonActor>());
        }

        private void BuildLifeActionStage()
        {
            lifeStage=Group("Everyday action scene",cinematicStage);
            Material leaf=Lit(new Color(.15f,.38f,.24f)),grass=Lit(new Color(.11f,.23f,.18f));
            Material plaster=Lit(new Color(.47f,.55f,.56f)),stone=Lit(new Color(.38f,.40f,.35f));
            Transform room=Group("Home and open doorway",lifeStage);
            Box(room,"Home oak floor",new Vector3(-1.4f,-.14f,-.2f),new Vector3(3.2f,.12f,3),wood);
            Box(room,"Home rear wall",new Vector3(-1.4f,1.1f,-1.7f),new Vector3(3.2f,2.45f,.12f),plaster);
            Box(room,"Home left wall",new Vector3(-3,1.1f,-.2f),new Vector3(.12f,2.45f,3),plaster);
            Box(room,"Window frame",new Vector3(-1.6f,1.5f,-1.6f),new Vector3(1.35f,1,.08f),wood);
            Box(room,"Warm window",new Vector3(-1.6f,1.5f,-1.54f),new Vector3(1.16f,.82f,.06f),glass);
            Box(room,"Window cross",new Vector3(-1.6f,1.5f,-1.48f),new Vector3(.04f,.82f,.04f),ivory);
            for(int i=0;i<2;i++) Box(room,"Door jamb "+i,new Vector3(-2.3f+i*1.3f,1.08f,.85f),new Vector3(.09f,2.2f,.15f),wood);
            Box(room,"Door lintel",new Vector3(-1.65f,2.17f,.85f),new Vector3(1.4f,.12f,.15f),wood);
            lifeDoor=Group("Door hinge",room); lifeDoor.localPosition=new Vector3(-2.3f,0,.85f);
            Box(lifeDoor,"Opening home door",new Vector3(.61f,1.05f,0),new Vector3(1.2f,2.08f,.075f),wood);
            Shape(lifeDoor,"Door handle",PrimitiveType.Sphere,new Vector3(1.06f,1.02f,-.09f),Vector3.one*.08f,gold);
            Plant(room,new Vector3(-2.65f,0,-.65f),.58f);
            lifePark=Group("Park destination",lifeStage);
            Box(lifePark,"Park lawn",new Vector3(.5f,-.17f,3.35f),new Vector3(5.8f,.12f,5.3f),grass);
            for(int i=0;i<14;i++)
            {
                float p=i/13f,x=Mathf.Lerp(-1.65f,.6f,Mathf.SmoothStep(0,1,p));
                Box(lifePark,"Park stepping stone "+i,new Vector3(x,-.055f,.7f+p*4),new Vector3(.83f,.05f,.25f),stone)
                    .localRotation=Quaternion.Euler(0,Mathf.Sin(p*Mathf.PI)*-25,0);
            }
            for(int i=0;i<4;i++)
            {
                float x=i%2==0?-1.8f:2.5f,z=2.2f+i/2*2.6f;
                Shape(lifePark,"Tree trunk "+i,PrimitiveType.Cylinder,new Vector3(x,.6f,z),new Vector3(.18f,.65f,.18f),wood);
                Shape(lifePark,"Tree crown "+i,PrimitiveType.Sphere,new Vector3(x,1.62f,z),new Vector3(1.15f,1.35f,1.1f),leaf);
                Shape(lifePark,"Tree crown lobe "+i,PrimitiveType.Sphere,new Vector3(x+.32f,1.4f,z+.12f),Vector3.one*.82f,leaf);
            }
            for(int i=0;i<4;i++)
            {
                Box(lifePark,"Park bench slat "+i,new Vector3(1.65f,.48f,4.45f+i*.09f),new Vector3(1.4f,.055f,.07f),wood);
                Box(lifePark,"Park bench back "+i,new Vector3(1.65f,.65f+i*.09f,4.8f),new Vector3(1.4f,.07f,.055f),wood);
            }
            for(int i=0;i<2;i++) Box(lifePark,"Bench leg "+i,new Vector3(1.1f+i*1.1f,.2f,4.6f),new Vector3(.09f,.5f,.38f),dark);
            Plant(lifePark,new Vector3(2.65f,0,3.6f),.65f);
            lifeDesk=Group("Chosen action desk",lifeStage);
            Box(lifeDesk,"Everyday desk",new Vector3(.1f,.82f,1.9f),new Vector3(1.65f,.12f,.78f),wood);
            for(int i=0;i<4;i++) Box(lifeDesk,"Desk leg "+i,new Vector3((i%2==0?-.6f:.8f),.38f,1.9f+(i<2?-.3f:.3f)),new Vector3(.07f,.82f,.07f),wood);
            lifeBook=Group("Book and written plan",lifeDesk);
            Box(lifeBook,"Action notebook",new Vector3(0,.91f,1.8f),new Vector3(.6f,.045f,.42f),ivory);
            Box(lifeBook,"Book spine",new Vector3(0,.94f,1.8f),new Vector3(.025f,.025f,.42f),gold);
            for(int i=0;i<4;i++) Box(lifeBook,"Written line "+i,new Vector3(.16f,.945f,1.67f+i*.07f),new Vector3(.17f,.008f,.012f),teal);
            lifeScreen=Group("Work in progress display",lifeDesk);
            Box(lifeScreen,"Work monitor",new Vector3(0,1.25f,2.12f),new Vector3(.82f,.56f,.07f),dark);
            Box(lifeScreen,"Work screen",new Vector3(0,1.25f,2.06f),new Vector3(.71f,.46f,.025f),teal);
            for(int i=0;i<4;i++) Box(lifeScreen,"Work task "+i,new Vector3(-.08f,1.39f-i*.085f,2.04f),new Vector3(.4f,.018f,.01f),ivory);
            Box(lifeScreen,"Keyboard",new Vector3(0,.93f,1.72f),new Vector3(.65f,.035f,.26f),dark);
            lifePhone=Group("Chosen phone action",lifeDesk);
            Box(lifePhone,"Phone body",new Vector3(0,.99f,1.77f),new Vector3(.26f,.06f,.46f),dark);
            Box(lifePhone,"Phone screen",new Vector3(0,1.025f,1.77f),new Vector3(.22f,.012f,.37f),glass);
            for(int i=0;i<3;i++) Box(lifePhone,"Message line "+i,new Vector3(.015f,1.036f,1.65f+i*.08f),new Vector3(.14f,.005f,.022f),teal);
            Shape(lifeDesk,"Reading lamp stem",PrimitiveType.Cylinder,new Vector3(.7f,1.12f,2.05f),new Vector3(.025f,.24f,.025f),gold);
            Shape(lifeDesk,"Reading lamp shade",PrimitiveType.Sphere,new Vector3(.7f,1.36f,2.05f),new Vector3(.32f,.12f,.32f),warmLight);
            lifeRest=Group("Recovery seat",lifeStage);
            Box(lifeRest,"Quiet seat",new Vector3(0,.4f,1.9f),new Vector3(1.25f,.25f,.9f),teal);
            Box(lifeRest,"Quiet seat back",new Vector3(0,.83f,2.3f),new Vector3(1.25f,.85f,.16f),teal);
            for(int i=0;i<2;i++) Box(lifeRest,"Seat arm "+i,new Vector3(i==0?-.67f:.67f,.58f,1.9f),new Vector3(.17f,.42f,.9f),wood);
            lifeContact=Person("Person receiving the contact",lifeStage,new Vector3(.95f,0,1.45f),ivory);
            lifeContact.localRotation=Quaternion.Euler(0,60,0);
        }

        private void SampleLifeAction(float time,ref Vector3 camera,ref Vector3 look)
        {
            lifePressure=0; if(!LifeActionPlaying) return;
            LifeActionFrame frame=lifeAction.Sample(time,preferences.reducedMotion);
            CurrentLifeActionShot=frame.Shot; lifePressure=frame.Pressure;
            bool overview=frame.Shot==LifeActionShot.Overview || cinematicOverviewRequested;
            bool narrative=time<lifeAction.Reveal || overview;
            lifeStage.gameObject.SetActive(time<lifeAction.Reveal || overview);
            if(jackpotStage!=null && (time<lifeAction.ActionEnd || overview)) jackpotStage.gameObject.SetActive(false);
            else if(jackpotStage!=null) jackpotStage.gameObject.SetActive(true);
            if(!narrative) return;
            movieFuture.gameObject.SetActive(false); movieFriend.gameObject.SetActive(false); movieAnchor.gameObject.SetActive(false);
            foreach(Transform tile in movieTiles) tile.gameObject.SetActive(false);
            foreach(Transform node in movieNodes) node.gameObject.SetActive(false);
            foreach(LineRenderer line in movieLines) line.gameObject.SetActive(false);
            foreach(Transform item in movieObjects) item.gameObject.SetActive(false);
            if(overview)
            {
                foreach(Renderer renderer in showcaseOccluders) if(renderer!=null) renderer.enabled=true;
                showcaseOccluders.Clear();
                moviePlayer.gameObject.SetActive(true);
            }
            float p=frame.Progress; bool walking=lifeAction.Theme==LifeActionTheme.Walk || lifeAction.Theme==LifeActionTheme.Explore;
            Vector3 actorPosition;
            HorizonBodyAction body;
            if(walking)
            {
                actorPosition=p<.32f?Vector3.Lerp(new Vector3(-1.65f,0,-.85f),new Vector3(-1.65f,0,1.25f),Smooth(0,.32f,p)):
                    Vector3.Lerp(new Vector3(-1.65f,0,1.25f),new Vector3(.65f,0,4.35f),Smooth(.32f,.96f,p));
                body=p<.12f?HorizonBodyAction.Reach:p<.96f?HorizonBodyAction.Walk:HorizonBodyAction.Rest;
                moviePlayer.localRotation=Quaternion.Euler(0,p<.32f?180:216,0);
                lifeDoor.localRotation=Quaternion.Euler(0,-Mathf.Lerp(0,102,Smooth(0,.25f,p)),0);
            }
            else
            {
                actorPosition=new Vector3(-.1f,lifeAction.Theme==LifeActionTheme.Rest?-.1f:0,lifeAction.Theme==LifeActionTheme.Rest?1.75f:.95f);
                body=lifeAction.Theme==LifeActionTheme.Rest?HorizonBodyAction.Sit:lifeAction.Theme==LifeActionTheme.Connect?HorizonBodyAction.Reach:HorizonBodyAction.Operate;
                moviePlayer.localRotation=Quaternion.Euler(0,lifeAction.Theme==LifeActionTheme.Rest?0:lifeAction.Theme==LifeActionTheme.Connect?245:180,0);
                lifeDoor.localRotation=Quaternion.Euler(0,-100,0);
            }
            if(time>=lifeAction.ActionEnd && lifeAction.Theme!=LifeActionTheme.Rest) body=HorizonBodyAction.Wait;
            moviePlayer.localPosition=actorPosition;
            lifePerformance.Play(body,preferences.reducedMotion?1:time<lifeAction.ActionEnd?Mathf.Repeat(time*.55f,1):Mathf.Repeat(time*.18f,1));
            // Three tension cycles grow toward the reveal. Motion is bounded,
            // sampled, pausable, and absent from reduced-motion playback.
            float tug=preferences.reducedMotion || tensionFrame.Silent || moviePhase==CinematicPhase.HitStop?0:
                Mathf.Sin(time*(9+lifePressure*19))*lifePressure*lifePressure;
            moviePlayer.localPosition+=new Vector3(tug*.012f,0,0);
            movieLight.color=Color.Lerp(new Color(.58f,.78f,.9f),new Color(1,.72f,.28f),lifePressure);
            movieLight.intensity=Mathf.Max(movieLight.intensity,.65f+lifePressure*1.6f);
            immersionFocus=actorPosition+Vector3.up;
            // Wide shots fit the WHOLE relevant set. Detail shots fit the actor.
            // Both use actual renderer bounds, including the screen corners.
            bool wide=overview || frame.Shot==LifeActionShot.Establish || frame.Shot==LifeActionShot.Arrive || preferences.reducedMotion;
            CurrentCinematicFraming=wide?CinematicFraming.ExtremeWide:CinematicFraming.Close;
            camera=wide?new Vector3(6,5.4f,-7):actorPosition+new Vector3(2.2f,1.8f,-3.4f);
            look=wide?new Vector3(0,1,2):actorPosition+Vector3.up;
            Rect aperture=wide?new Rect(.065f,.245f,.87f,.52f):HorizonCinemaLayout.SubjectAperture;
            FitPortraitSubject(wide?lifeOverviewRenderers:lifeActorRenderers,aperture,CinematicFieldOfView(time),ref camera,ref look,false);
        }
        private void EndLifeAction()
        {
            lifePerformance?.Release(); lifeAction=null; lifePressure=0;
            if(lifeStage!=null) lifeStage.gameObject.SetActive(false);
        }
    }
}
