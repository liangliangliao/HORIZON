using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private Transform projectionWorld;
        private readonly Transform[] projectionLayers = new Transform[5];
        private LineRenderer[] projectionOutlines;
        private void PrepareProjectionWorld()
        {
            if (projectionWorld != null) { projectionWorld.gameObject.SetActive(true); return; }
            projectionWorld = Group("Imagination projected world", cinematicStage);
            string[] names = { "Street", "Room", "People", "Obstacles", "Future scene" };
            for (int i=0;i<names.Length;i++) projectionLayers[i]=Group("Projection layer "+names[i], projectionWorld);
            Transform street=projectionLayers[0];
            Box(street,"Projected street",new Vector3(0,-.19f,3),new Vector3(5,.1f,10),dark);
            for(int side=-1;side<=1;side+=2)
            {
                Box(street,"Pavement",new Vector3(side*2.5f,-.12f,3),new Vector3(.7f,.2f,10),floor);
                for(int i=0;i<3;i++) Box(street,"Street marking",new Vector3(side*1.6f,-.12f,i*2),new Vector3(.06f,.025f,.7f),ivory);
            }
            Transform room=projectionLayers[1]; room.localPosition=new Vector3(-3,0,4.8f);
            Box(room,"Room floor",Vector3.zero,new Vector3(2.5f,.12f,2.5f),wood);
            Box(room,"Room back",new Vector3(0,1.1f,1.2f),new Vector3(2.5f,2.2f,.1f),floor);
            Box(room,"Room side",new Vector3(-1.2f,1.1f,0),new Vector3(.1f,2.2f,2.5f),floor);
            Box(room,"Window light",new Vector3(.3f,1.4f,1.13f),new Vector3(1.1f,.9f,.04f),glass);
            Box(room,"Small table",new Vector3(-.4f,.7f,.4f),new Vector3(.9f,.09f,.5f),wood);
            Box(room,"Table support",new Vector3(-.4f,.35f,.4f),new Vector3(.08f,.7f,.08f),wood);
            Person("Projected neighbour",projectionLayers[2],new Vector3(-2.5f,0,3.7f),teal);
            Person("Projected helper",projectionLayers[2],new Vector3(2.8f,0,4.7f),ivory);
            Transform obstacle=projectionLayers[3];
            for(int i=0;i<3;i++) Box(obstacle,"Projected difficulty block "+i,new Vector3(1.7f,.2f+i*.17f,2+i*.35f),new Vector3(.7f,.4f,.28f),gold);
            Transform future=projectionLayers[4];
            Box(future,"Destination porch",new Vector3(0,-.1f,6.8f),new Vector3(3,.16f,1.5f),wood);
            for(int i=0;i<3;i++) Box(future,"Future window "+i,new Vector3((i-1)*1.2f,1.8f,8),new Vector3(.7f,1.2f,.05f),glass);
            projectionOutlines=new LineRenderer[8];
            for(int i=0;i<8;i++)
            {
                float x=i<4?-4.2f:-1.8f, z=3.6f+(i%4)*.8f;
                Vector3 a=new Vector3(x,.02f,z),b=new Vector3(i<4?-1.8f:2.7f,.02f,z);
                projectionOutlines[i]=CausalLine(projectionWorld,"Flat projection outline "+i,a,b,portalLight,.015f);
            }
        }
        private void SampleProjectionWorld(float progress, bool materializing)
        {
            if(projectionWorld==null) return;
            float p=materializing?progress:1;
            for(int i=0;i<projectionLayers.Length;i++)
            {
                float reveal=Smooth(.12f+i*.13f,.34f+i*.13f,p);
                projectionLayers[i].gameObject.SetActive(reveal>.001f);
                // Rise out of the flat blueprint, preserving the full footprint.
                projectionLayers[i].localScale=new Vector3(1,Mathf.Max(.001f,reveal),1);
                ProjectionSurface(projectionLayers[i],reveal,p*3,reveal>0 && reveal<1);
            }
            foreach(LineRenderer line in projectionOutlines) line.gameObject.SetActive(p<.9f);
            float future=Smooth(.68f,.98f,p);
            movieAnchor.gameObject.SetActive(future>.001f);
            if(materializing) movieFuture.gameObject.SetActive(future>.001f);
            movieAnchor.localScale=new Vector3(1,Mathf.Max(.001f,future),1);
            movieFuture.localScale=Vector3.one*Mathf.Max(.001f,future);
            ProjectionSurface(movieFuture,future,p*3,future>0 && future<1);
            movieLines[6].gameObject.SetActive(materializing && p<.99f);
            MovieLine(movieLines[6],new Vector3(-1.6f,.75f,1),new Vector3(0,1,5));
        }
    }
}
