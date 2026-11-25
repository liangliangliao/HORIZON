using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public enum PracticeShot { Establish, Setback, Route, Success, Charge, Open, Receive }

    public sealed partial class HorizonWorld3D
    {
        private Transform practiceStage, performer, partner, heroProp, actionTable, obstacle, chamber, lidLeft, lidRight, rays, prize;
        private HorizonActorPerformance practiceActor, practicePartner;
        private PracticeScript script;
        private Transform[] practiceMarks, orbitingSeals;
        private Vector3 routeEnd;
        private PracticeShot lastPracticeShot;
        private bool firstPracticeShot;
        private Vector3 originalCamera, propOrigin;
        private Quaternion originalRotation;
        private Rect originalRect;
        private float originalFov;
        private Color originalSky;
        private Material rayMaterial;
        private MaterialPropertyBlock practiceLight;
        private Renderer rayRenderer;
        private readonly Transform[] prizePieces=new Transform[6], prizeSockets=new Transform[6], prizeBeams=new Transform[6];
        private readonly Vector3[] prizeLandings=new Vector3[6];
        private readonly bool[] prizeLanded=new bool[6];
        private int prizeAxis;
        private bool prizeNegative;

        public void BeginPracticeSet(PracticeScript film)
        {
            EndPracticeSet();
            script = film;
            originalCamera=WorldCamera.transform.position; originalRotation=WorldCamera.transform.rotation;
            originalRect=WorldCamera.rect; originalFov=WorldCamera.fieldOfView; originalSky=WorldCamera.backgroundColor;
            practiceStage=Group("Practice location · "+film.Set);
            practiceStage.localPosition=new Vector3(180,0,0);
            WorldCamera.rect=new Rect(0,0,1,1);
            bool exterior=film.Set==PracticeSet.Street || film.Set==PracticeSet.Track;
            WorldCamera.backgroundColor=exterior ? new Color(.31f,.47f,.56f) : new Color(.09f,.14f,.19f);
            Box(practiceStage,"Floor",new Vector3(0,-.12f,1),new Vector3(15,.22f,13),exterior ? floor : wood);
            if (!exterior)
            {
                Box(practiceStage,"Back wall",new Vector3(0,2.1f,3.25f),new Vector3(11,4.3f,.14f),floor);
                for(int w=0;w<3;w++)
                    Box(practiceStage,"Window pane",new Vector3(-2.75f+w*.67f,2.06f,3.15f),new Vector3(.59f,1.92f,.06f),glass);
                Box(practiceStage,"Window sill",new Vector3(-2.08f,1.07f,3.06f),new Vector3(2.15f,.09f,.31f),ivory);
            }
            actionTable=Group("Action surface",practiceStage);
            actionTable.localPosition=new Vector3(.12f,0,0);
            DressPracticeSet(film.Set);
            heroProp=PracticeObject(actionTable,film.Prop);
            propOrigin=new Vector3(.1f,exterior ? .95f : .92f,-.34f);
            heroProp.localPosition=propOrigin;
            obstacle=Group("Unfinished obstacle",actionTable);
            if (exterior)
            {
                Box(obstacle,"Closed crossing",Vector3.up*.72f,new Vector3(1.2f,.20f,.1f),wood);
                foreach(float x in new[]{-.48f,.48f})
                    Box(obstacle,"Trestle",new Vector3(x,.38f,0),new Vector3(.09f,.76f,.25f),dark);
                obstacle.localPosition=new Vector3(.6f,0,.0f);
            }
            else
            {
                Transform loose=PracticeObject(obstacle,film.Prop);
                loose.localScale=Vector3.one*.65f;
                loose.localEulerAngles=new Vector3(12,30,22);
                obstacle.localPosition=new Vector3(-.39f,.91f,.05f);
            }
            performer=Person("Practitioner",practiceStage,new Vector3(-.8f,0,.45f),cloth);
            practiceActor=HorizonActorPerformance.Attach(performer.GetComponent<HorizonActor>());
            partner=Person("Partner",practiceStage,new Vector3(1.35f,0,.47f),teal);
            partner.localEulerAngles=new Vector3(0,36,0);
            practicePartner=HorizonActorPerformance.Attach(partner.GetComponent<HorizonActor>());
            partner.gameObject.SetActive(film.Set==PracticeSet.Cafe || film.Set==PracticeSet.Kitchen ||
                film.MoveA==PracticeMove.Talk || film.MoveB==PracticeMove.Talk || film.MoveB==PracticeMove.Listen && film.Set!=PracticeSet.Stage);
            BuildPracticeChamber();
            routeEnd=performer.localPosition; firstPracticeShot=true;
            SamplePractice(PracticeShot.Establish,0,film.MoveA,0);
        }

        private void Table(Transform parent,Vector3 at,float width,float height,Material top)
        {
            Box(parent,"Surface",at+Vector3.up*height,new Vector3(width,.10f,.83f),top);
            foreach(float x in new[]{-.39f,.39f})
                Box(parent,"Support",at+new Vector3(width*x,height*.5f,0),new Vector3(.13f,height,.6f),dark);
        }
        private void DressPracticeSet(PracticeSet set)
        {
            switch(set)
            {
                case PracticeSet.Street: case PracticeSet.Track:
                    Table(practiceStage,new Vector3(1.5f,0,1.15f),1.5f,.48f,wood);
                    Box(practiceStage,"Bench back",new Vector3(1.5f,.89f,1.45f),new Vector3(1.55f,.36f,.08f),wood);
                    for(int i=-3;i<=3;i++)
                    {
                        float x=i*1.42f;
                        if(set==PracticeSet.Street)
                        {
                            Box(practiceStage,"River rail",new Vector3(x,.43f,3),new Vector3(.08f,.86f,.08f),dark);
                            Box(practiceStage,"Distant building",new Vector3(x,1.0f,6+Mathf.Abs(i)%2),new Vector3(1.18f,2.4f+Mathf.Abs(i)%3,1.3f),floor);
                        }
                        else Box(practiceStage,"Painted lane",new Vector3(x,.006f,0),new Vector3(.045f,.015f,9),ivory);
                    }
                    if(set==PracticeSet.Street)
                    {
                        Box(practiceStage,"River",new Vector3(0,-.01f,5),new Vector3(15,.08f,3.4f),teal);
                        Box(practiceStage,"Handrail",new Vector3(0,.9f,3),new Vector3(10,.08f,.08f),wood);
                    }
                    break;
                case PracticeSet.Bedroom:
                    Table(practiceStage,new Vector3(1.67f,0,1.25f),2.2f,.42f,wood);
                    Box(practiceStage,"Quilt",new Vector3(1.59f,.59f,1.25f),new Vector3(1.85f,.28f,.95f),cloth);
                    Box(practiceStage,"Pillow",new Vector3(2.48f,.71f,1.24f),new Vector3(.55f,.23f,.78f),ivory);
                    break;
                case PracticeSet.Library: case PracticeSet.Shop: case PracticeSet.Workshop:
                    for(int s=0;s<3;s++)
                    {
                        Table(practiceStage,new Vector3(1.64f,s*.63f,2.68f),2.65f,.49f,wood);
                        for(int j=0;j<7;j++)
                            Box(practiceStage,"Stock",new Vector3(.67f+j*.31f,.72f+s*.63f,2.61f),
                                set==PracticeSet.Library ? new Vector3(.17f,.4f,.28f) : new Vector3(.25f,.26f,.34f),j%3==0?ivory:j%3==1?teal:wood);
                    }
                    break;
                case PracticeSet.Garden:
                    for(int j=0;j<4;j++)
                    { var plant=PracticeObject(practiceStage,PracticeProp.Flower); plant.localPosition=new Vector3(-1.65f+j,1.14f,2.78f); }
                    Table(practiceStage,new Vector3(0,0,2.7f),4.6f,1.08f,wood); break;
                case PracticeSet.Stage:
                    for(int j=-1;j<=1;j+=2)
                    {
                        Box(practiceStage,"Speaker cabinet",new Vector3(j*2,.80f,2),new Vector3(.76f,1.6f,.63f),dark);
                        Disc(practiceStage,"Driver",new Vector3(j*2,.9f,1.65f),.46f,glass,true);
                    }
                    break;
                case PracticeSet.Kitchen: case PracticeSet.Cafe:
                    for(int k=0;k<3;k++)
                    {
                        Table(practiceStage,new Vector3(-3+k*2.4f,0,2.3f),1.3f,.72f,wood);
                        var cup=PracticeObject(practiceStage,PracticeProp.Cup); cup.localPosition=new Vector3(-3+k*2.4f,.79f,2.34f);
                    }
                    break;
                default:
                    Box(practiceStage,"Pinboard",new Vector3(1.6f,2f,3.02f),new Vector3(2.1f,1.35f,.09f),wood);
                    for(int k=0;k<4;k++) Box(practiceStage,"Pinned sheet",new Vector3(.95f+k*.41f,2.03f,2.96f),new Vector3(.32f,.57f,.02f),ivory);
                    break;
            }
            if(set!=PracticeSet.Street && set!=PracticeSet.Track)
                Table(actionTable,new Vector3(.12f,0,0),1.65f,.82f,wood);
        }

        private Transform Disc(Transform parent,string name,Vector3 pos,float diameter,Material material,bool upright=false)
        {
            Transform d=Shape(parent,name,PrimitiveType.Cylinder,pos,new Vector3(diameter,.035f,diameter),material);
            if(upright) d.localRotation=Quaternion.Euler(90,0,0);
            return d;
        }
        private Transform PracticeObject(Transform parent, PracticeProp prop)
        {
            Transform obj=Group(prop.ToString(),parent);
            switch(prop)
            {
                case PracticeProp.Map: case PracticeProp.Book: case PracticeProp.Folder: case PracticeProp.Note: case PracticeProp.Ticket:
                    Box(obj,"Binding",Vector3.zero,new Vector3(.58f,.046f,.39f),prop==PracticeProp.Book?teal:wood);
                    for(int i=0;i<2;i++)
                    {
                        Transform page=Box(obj,"Paper",new Vector3((i-.5f)*.27f,.036f,0),new Vector3(.266f,.023f,.37f),ivory);
                        for(int l=0;l<3;l++)
                            Box(page,"Printed line",new Vector3(0,.52f,-.29f+l*.23f),new Vector3(.77f,.09f,.038f),prop==PracticeProp.Map?teal:dark);
                    }
                    break;
                case PracticeProp.Phone:
                    Box(obj,"Handset",Vector3.zero,new Vector3(.28f,.045f,.5f),dark);
                    Box(obj,"Screen",Vector3.up*.032f,new Vector3(.245f,.012f,.43f),teal); break;
                case PracticeProp.Clock:
                    Disc(obj,"Clock face",new Vector3(0,.24f,0),.47f,ivory,true);
                    Box(obj,"Minute hand",new Vector3(.014f,.31f,-.047f),new Vector3(.019f,.15f,.012f),dark);
                    Box(obj,"Hour hand",new Vector3(.06f,.24f,-.05f),new Vector3(.13f,.023f,.012f),dark); break;
                case PracticeProp.Flower:
                    Shape(obj,"Clay pot",PrimitiveType.Cylinder,new Vector3(0,.13f,0),new Vector3(.31f,.14f,.31f),wood);
                    Box(obj,"Stem",new Vector3(0,.47f,0),new Vector3(.03f,.45f,.03f),foliage);
                    for(int j=0;j<5;j++) Shape(obj,"Petal",PrimitiveType.Sphere,new Vector3(Mathf.Sin(j*1.257f)*.1f,.7f+Mathf.Cos(j*1.257f)*.085f,0),new Vector3(.14f,.14f,.08f),pink);
                    break;
                case PracticeProp.Cup:
                    Shape(obj,"Cup",PrimitiveType.Cylinder,new Vector3(0,.15f,0),new Vector3(.26f,.14f,.26f),ivory);
                    Disc(obj,"Tea",Vector3.up*.292f,.22f,wood);
                    Box(obj,"Handle",new Vector3(.15f,.15f,0),new Vector3(.12f,.12f,.07f),ivory);break;
                case PracticeProp.Record: case PracticeProp.Coin:
                    Disc(obj,"Disc",Vector3.zero,prop==PracticeProp.Coin?.32f:.60f,prop==PracticeProp.Coin?gold:dark);
                    Disc(obj,"Stamped centre",Vector3.up*.045f,.16f,prop==PracticeProp.Coin?wood:pink);break;
                case PracticeProp.Model:
                    Box(obj,"Base",Vector3.zero,new Vector3(.54f,.1f,.38f),wood);
                    Box(obj,"Frame",new Vector3(-.13f,.18f,0),new Vector3(.09f,.4f,.19f),teal);
                    Disc(obj,"Flywheel",new Vector3(.03f,.29f,-.04f),.35f,gold,true);break;
                case PracticeProp.Shoe:
                    Shape(obj,"Running shoe",PrimitiveType.Sphere,new Vector3(0,.1f,0),new Vector3(.22f,.21f,.44f),teal);
                    Box(obj,"Sole",Vector3.zero,new Vector3(.23f,.046f,.44f),ivory);break;
                default:
                    Box(obj,"Container",new Vector3(0,.135f,0),new Vector3(.51f,.27f,.38f),wood);
                    Box(obj,"Banded rim",new Vector3(0,.28f,0),new Vector3(.53f,.045f,.40f),prop==PracticeProp.Basket?teal:ivory);break;
            }
            return obj;
        }

        private void BuildPracticeChamber()
        {
            if(practiceLight==null) practiceLight=new MaterialPropertyBlock();
            chamber=Group("Reward case at finished work",actionTable);
            chamber.localPosition=new Vector3(.1f,propOrigin.y,-.36f);
            Box(chamber,"Enamel case",new Vector3(0,.12f,0),new Vector3(.82f,.23f,.52f),teal);
            lidLeft=Group("Hinged left leaf",chamber); lidRight=Group("Hinged right leaf",chamber);
            for(int i=0;i<2;i++)
            {
                int sign=i==0?-1:1; var hinge=i==0?lidLeft:lidRight;
                hinge.localPosition=new Vector3(sign*.41f,.23f,0);
                Box(hinge,"Brass leaf",new Vector3(-sign*.2f,.04f,0),new Vector3(.40f,.08f,.53f),gold);
            }
            practiceMarks=new Transform[3];
            for(int i=0;i<3;i++)
                practiceMarks[i]=Shape(chamber,"Lock facet",PrimitiveType.Sphere,new Vector3((i-1)*.20f,.14f,-.273f),Vector3.one*.09f,warmLight);
            orbitingSeals=new Transform[LowCostEffects?6:12];
            for(int i=0;i<orbitingSeals.Length;i++)
            {
                Transform seal=Group("Orbiting seal",chamber);
                Transform token=Disc(seal,"Stamped brass",Vector3.zero,.13f,i%3==0?teal:gold,true);
                Box(token,"Engraved glint",Vector3.up*.94f,new Vector3(.46f,.19f,.10f),warmLight);
                orbitingSeals[i]=seal;
            }
            rays=Group("Radial light",chamber);
            if(rayMaterial==null) { rayMaterial=new Material(Resources.Load<Shader>("HorizonPracticeRays")); materials.Add(rayMaterial); }
            Transform plane=Shape(rays,"Light fan",PrimitiveType.Quad,Vector3.zero,Vector3.one*4.5f,rayMaterial);
            rayRenderer=plane.GetComponent<Renderer>(); rays.localPosition=new Vector3(0,.30f,.28f);
            chamber.gameObject.SetActive(false);
        }

        public void PreparePracticePayout(int[] ticks)
        {
            int count=System.Array.FindAll(ticks,v=>v!=0).Length;
            int slot=0;
            for (int axis=0;axis<6;axis++)
            {
                if(ticks[axis]==0) continue;
                float angle=count==1 ? 0 : Mathf.Lerp(-1.10f,1.10f,slot++/(float)(count-1));
                prizeLandings[axis]=new Vector3(Mathf.Sin(angle)*.61f,.16f,-.50f*Mathf.Cos(angle)-.16f);
                var socket=Group(PracticeReward.Names[axis]+" landing",chamber);
                socket.localPosition=prizeLandings[axis]-Vector3.up*.055f;
                Disc(socket,"Foot",Vector3.zero,.32f,dark);
                Disc(socket,"Gold lip",Vector3.up*.024f,.28f,gold);
                var beam=Shape(socket,"Column of light",PrimitiveType.Quad,new Vector3(0,.35f,.08f),new Vector3(.31f,.68f,1),rayMaterial);
                Color[] colors={new Color(.21f,1,.76f),new Color(1,.42f,.26f),new Color(.43f,.66f,1),new Color(1,.70f,.13f),new Color(.98f,.52f,.70f),new Color(.74f,.52f,1)};
                practiceLight.Clear();
                practiceLight.SetColor("_Column",colors[axis]);
                beam.GetComponent<Renderer>().SetPropertyBlock(practiceLight);
                practiceLight.Clear();
                prizeBeams[axis]=beam; prizeSockets[axis]=socket;
                socket.gameObject.SetActive(false);
            }
        }
        public void SetPracticePrize(int axis,bool negative)
        {
            if(prize!=null && axis!=prizeAxis && prizeLanded[prizeAxis])
            {
                prize.localPosition=prizeLandings[prizeAxis]; prize.localScale=Vector3.one*.50f;
                prize.localRotation=Quaternion.Euler(0,24,0);
            }
            prizeAxis=axis; prizeNegative=negative;
            if(prizePieces[axis]==null)
            {
                PracticeProp[] symbols={PracticeProp.Shoe,PracticeProp.Flower,PracticeProp.Book,PracticeProp.Coin,PracticeProp.Cup,PracticeProp.Model};
                prizePieces[axis]=PracticeObject(chamber,symbols[axis]);
                prizePieces[axis].gameObject.SetActive(false);
            }
            prize=prizePieces[axis];
        }

        public void SamplePractice(PracticeShot shot,float progress,PracticeMove move,int beat)
        {
            if(practiceStage==null) return;
            float p=Mathf.Clamp01(progress), t=Mathf.SmoothStep(0,1,p);
            bool reward=shot>=PracticeShot.Charge, reduced=preferences.reducedMotion;
            if (firstPracticeShot || shot!=lastPracticeShot)
            {
                if(shot==PracticeShot.Charge && !reduced && preferences.sound) PlayTensionCharge(2.8f);
                if(shot==PracticeShot.Open && preferences.sound) CueSound(sounds[1],.75f);
                firstPracticeShot=false; lastPracticeShot=shot;
            }
            chamber.gameObject.SetActive(reward);
            obstacle.gameObject.SetActive(!reward && shot!=PracticeShot.Success);
            heroProp.gameObject.SetActive(!reward);
            Vector3 rest=new Vector3(-.50f,0,.48f), actorPos=rest;
            bool outdoors=script.Set==PracticeSet.Street || script.Set==PracticeSet.Track;
            Vector3 look=new Vector3(0,1.03f,0), offset=new Vector3(-1.35f,.59f,-4.45f);
            float fov=43;
            HorizonBodyAction pose=HorizonBodyAction.Wait;
            heroProp.localPosition=propOrigin; heroProp.localRotation=Quaternion.identity;
            performer.localEulerAngles=new Vector3(0,-23,0);
            switch(shot)
            {
                case PracticeShot.Establish:
                    actorPos=Vector3.Lerp(new Vector3(-1.82f,0,1.35f),rest,t);
                    pose=HorizonBodyAction.Walk; offset*=reduced?1.15f:1.29f-.24f*t; break;
                case PracticeShot.Setback:
                    pose=outdoors?HorizonBodyAction.Stumble:HorizonBodyAction.Reach;
                    heroProp.localRotation=Quaternion.Euler(0,0,Mathf.Sin(t*Mathf.PI*.5f)*(outdoors?18:49));
                    actorPos.x-=Mathf.Sin(t*Mathf.PI)*.23f;
                    look=Vector3.Lerp(new Vector3(-.31f,1.52f,.42f),propOrigin,t*.59f);
                    offset=new Vector3(.74f,.38f,-2.63f); break;
                case PracticeShot.Route:
                    pose=PracticePose(move);
                    float arc=Mathf.Sin(p*Mathf.PI);
                    if(move==PracticeMove.Walk || move==PracticeMove.Carry)
                    {
                        actorPos=Vector3.Lerp(rest,new Vector3(1.12f,0,.18f),t)+new Vector3(0,0,-arc*.72f);
                        performer.localEulerAngles=new Vector3(0,-72,0);
                        if(move==PracticeMove.Carry) heroProp.localPosition=actorPos+new Vector3(.29f,.99f,-.24f);
                        look=actorPos+Vector3.up*1.05f; offset=new Vector3(-.20f,.54f,-3.22f);
                    }
                    else
                    {
                        if(move==PracticeMove.Talk || move==PracticeMove.Listen) { performer.localEulerAngles=new Vector3(0,-60,0); look=new Vector3(.35f,1.48f,.4f); }
                        if(move==PracticeMove.Sort || move==PracticeMove.Assemble) heroProp.localPosition=propOrigin+new Vector3(.3f*t,arc*.24f,0);
                        if(move==PracticeMove.Write || move==PracticeMove.Examine) { heroProp.localRotation=Quaternion.Euler(-27*t,12*t,0); look=new Vector3(-.09f,1.16f,0); offset=new Vector3(1.02f,.80f,-2.47f); }
                        if(move==PracticeMove.Rest) actorPos.y=-.25f*t;
                    }
                    routeEnd=actorPos;
                    break;
                case PracticeShot.Success:
                    actorPos=Vector3.Lerp(routeEnd,rest,t);
                    pose=outdoors?HorizonBodyAction.Rest:HorizonBodyAction.Stand;
                    if (script.Id=="walk" || script.Id=="nightwalk")
                    {
                        Vector3 seat=new Vector3(1.45f,.02f,.98f);
                        actorPos=Vector3.Lerp(routeEnd,seat,Mathf.SmoothStep(0,1,Mathf.Min(1,p*1.6f)));
                        pose=p<.64f?HorizonBodyAction.Walk:HorizonBodyAction.Sit;
                        look=actorPos+Vector3.up*1.03f; offset=new Vector3(.54f,.44f,-3.3f);
                        chamber.localPosition=seat+new Vector3(.56f,.57f,-.22f);
                        break;
                    }
                    if (script.Id=="garden") heroProp.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(19,0,t));
                    if (script.Id=="music") heroProp.localEulerAngles=new Vector3(0,125*t,0);
                    look=Vector3.Lerp(propOrigin,new Vector3(-.32f,1.56f,.43f),t*.75f); offset=new Vector3(-.47f,.4f,-2.91f);
                    break;
                default:
                    float open=shot==PracticeShot.Charge?0:shot==PracticeShot.Open?t:1;
                    lidLeft.localRotation=Quaternion.Euler(0,0,open*108); lidRight.localRotation=Quaternion.Euler(0,0,-open*108);
                    float charge=shot==PracticeShot.Charge?t:1;
                    for(int i=0;i<orbitingSeals.Length;i++)
                    {
                        Transform seal=orbitingSeals[i];
                        float angle=(float)i/orbitingSeals.Length*Mathf.PI*2+(reduced?0:shot==PracticeShot.Charge?t*2.45f:2.45f+open*.7f);
                        float radius=shot==PracticeShot.Charge ? Mathf.Lerp(.98f,.17f,t) : Mathf.Lerp(.17f,1.02f,open);
                        seal.localPosition=new Vector3(Mathf.Cos(angle)*radius,.35f+Mathf.Sin(angle)*radius*.63f,Mathf.Sin(angle+.65f)*.16f);
                        seal.localRotation=Quaternion.Euler(12,Mathf.Sin(angle)*53,angle*12);
                        seal.localScale=Vector3.one*(shot==PracticeShot.Charge ? .4f+.62f*t:1-open*.36f);
                    }
                    float scale=shot==PracticeShot.Charge ? .52f+.48f*t : 1f;
                    chamber.localScale=Vector3.one*(scale+(reduced?0:shot==PracticeShot.Open?.12f*Mathf.Sin(p*Mathf.PI):.018f*Mathf.Sin(p*24)*charge));
                    for(int i=0;i<3;i++) practiceMarks[i].gameObject.SetActive(i<=beat && charge>i*.22f);
                    float light=shot==PracticeShot.Charge ? .08f+.67f*t : shot==PracticeShot.Open ? 1f-.5f*t : .29f;
                    practiceLight.SetVector("_Beat",new Vector4(reduced?0:p,light*(reduced?.48f:1),shot==PracticeShot.Open?1:0,charge));
                    rayRenderer.SetPropertyBlock(practiceLight);
                    for(int index=0;index<6;index++)
                    {
                        if(prizeSockets[index]==null) continue;
                        prizeSockets[index].gameObject.SetActive(shot!=PracticeShot.Charge);
                        bool arriving=shot==PracticeShot.Receive && index==prizeAxis;
                        float column=arriving?.35f+.65f*Mathf.Sin(p*Mathf.PI*.5f):prizeLanded[index]?.55f:0;
                        prizeBeams[index].gameObject.SetActive(column>0);
                        prizeBeams[index].localScale=new Vector3(.28f,column*.78f,1);
                        prizeBeams[index].localPosition=new Vector3(0,column*.39f,.085f);
                    }
                    if(prize!=null)
                    {
                        prize.gameObject.SetActive(shot!=PracticeShot.Charge);
                        Vector3 lifted=new Vector3(0,.76f,-.03f);
                        prize.localPosition=shot==PracticeShot.Receive ?
                            Vector3.Lerp(lifted,prizeLandings[prizeAxis],t)+Vector3.up*(reduced?0:.19f*Mathf.Sin(t*Mathf.PI)) : Vector3.Lerp(Vector3.up*.13f,lifted,open);
                        prize.localScale=Vector3.one*(shot==PracticeShot.Receive?Mathf.Lerp(1.06f,.50f,t):.09f+open*.97f);
                        if(shot==PracticeShot.Receive && p>=1) prizeLanded[prizeAxis]=true;
                        prize.localEulerAngles=new Vector3(0,open*29+(reduced?0:p*24),prizeNegative?-12*t:0);
                    }
                    look=chamber.localPosition+new Vector3(-.12f,.43f,.14f);
                    actorPos=chamber.localPosition+new Vector3(-.59f,-chamber.localPosition.y,.67f);
                    offset=shot==PracticeShot.Receive ? new Vector3(.28f,.43f,-3.15f) : new Vector3(.12f,.38f,-2.84f+(reduced?0:charge*.43f));
                    pose=shot==PracticeShot.Receive ? prizeNegative?HorizonBodyAction.Rest:
                        prizeAxis==5?HorizonBodyAction.Operate:prizeAxis==0?HorizonBodyAction.Stand:prizeAxis==1?HorizonBodyAction.Rest:HorizonBodyAction.Reach : HorizonBodyAction.Wait;
                    break;
            }
            performer.localPosition=actorPos;
            practiceActor.Play(pose,p);
            if(partner.gameObject.activeSelf) practicePartner.Play(shot==PracticeShot.Route && move==PracticeMove.Talk?HorizonBodyAction.Point:HorizonBodyAction.Wait,p);
            look=practiceStage.TransformPoint(look);
            WorldCamera.transform.position=look+offset; WorldCamera.transform.LookAt(look);
            WorldCamera.fieldOfView=fov;
        }
        private static HorizonBodyAction PracticePose(PracticeMove move)
        {
            switch(move)
            {
                case PracticeMove.Walk: case PracticeMove.Carry: return HorizonBodyAction.Walk;
                case PracticeMove.Talk: return HorizonBodyAction.Point;
                case PracticeMove.Rest: case PracticeMove.Listen: return HorizonBodyAction.Rest;
                case PracticeMove.Examine: return HorizonBodyAction.Reach;
                default: return HorizonBodyAction.Operate;
            }
        }
        public void EndPracticeSet()
        {
            if(practiceStage==null) return;
            practiceStage.gameObject.SetActive(false); Dispose(practiceStage.gameObject); practiceStage=null; prize=null;
            for(int i=0;i<6;i++)
            { prizePieces[i]=null; prizeSockets[i]=null; prizeBeams[i]=null; prizeLanded[i]=false; }
            WorldCamera.rect=originalRect; WorldCamera.fieldOfView=originalFov; WorldCamera.backgroundColor=originalSky;
            WorldCamera.transform.SetPositionAndRotation(originalCamera,originalRotation);
        }
    }
}
