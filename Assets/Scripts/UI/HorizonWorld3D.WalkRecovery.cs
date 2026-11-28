using Horizon.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private Transform walkEffects, walkRipple, walkBadge, walkShoe, walkBench;
        private readonly Transform[] walkMarks=new Transform[3],walkSettings=new Transform[3];
        private readonly LineRenderer[] walkBands=new LineRenderer[3];
        private readonly Transform[] walkSlats=new Transform[4];
        private HorizonActor walkActor;
        private Material rippleInk,trailInk;
        private Light walkLight;
        private readonly Color walkMint=new Color(.14f,.97f,.81f);
        private readonly Color walkAmber=new Color(1f,.58f,.16f);

        private void PrepareWalkRecovery()
        {
            if(walkEffects!=null) return;
            walkActor=moviePlayer.GetComponent<HorizonActor>();
            walkShoe=walkActor.RightLeg.Find("Shoe");
            walkBench=lifePark.Find("Park bench back 1");
            for(int i=0;i<walkSlats.Length;i++) walkSlats[i]=lifePark.Find("Park bench back "+i);
            walkEffects=Group("Walk recovery in the park",cinematicStage);
            Shader shader=Resources.Load<Shader>("HorizonWalkLight");
            rippleInk=new Material(shader); trailInk=new Material(shader);
            materials.Add(rippleInk); materials.Add(trailInk); trailInk.SetFloat("_Ribbon",1);
            walkRipple=Shape(walkEffects,"Footfall across the path",PrimitiveType.Quad,Vector3.zero,Vector3.one*2.45f,rippleInk);
            walkRipple.localRotation=Quaternion.Euler(90,0,0);
            walkRipple.GetComponent<Renderer>().receiveShadows=false;
            walkBadge=Group("Recovery marks on coat",walkEffects);
            for(int i=0;i<walkMarks.Length;i++)
            {
                float x=(i-1)*.14f;
                walkSettings[i]=Box(walkBadge,"Mark setting "+i,new Vector3(x,0,0),new Vector3(.13f,.12f,.052f),dark);
                walkMarks[i]=Box(walkBadge,"Energy mark "+i,new Vector3(x,.004f,-.031f),new Vector3(.093f,.082f,.034f),glass);
                Transform band=Group("Travelling recovery light "+i,walkEffects);
                LineRenderer line=band.gameObject.AddComponent<LineRenderer>();
                line.useWorldSpace=false; line.positionCount=20; line.sharedMaterial=trailInk;
                line.shadowCastingMode=ShadowCastingMode.Off; line.receiveShadows=false;
                line.textureMode=LineTextureMode.Stretch; line.numCapVertices=2;
                walkBands[i]=line;
            }
            walkLight=Group("Reflected walking light",walkEffects).gameObject.AddComponent<Light>();
            walkLight.type=LightType.Point; walkLight.range=2.4f; walkLight.shadows=LightShadows.None;
            walkEffects.gameObject.SetActive(false);
        }

        private void SampleWalkRecovery(float time,bool overview,ref Vector3 camera,ref Vector3 look)
        {
            float age=time-lifeAction.Reveal;
            bool active=age>=-1.2f && !overview;
            walkEffects.gameObject.SetActive(active);
            if(!active) { ClearWalkBench(); return; }

            var changes=moviePlan.Outcome.Changes;
            bool hasEnergy=changes[0].Axis==0;
            bool hasMood=changes[changes.Count-1].Axis==1;
            float moodAge=age-(changes.Count-1)*CinematicOutcome.ChangeSeconds;
            bool mood=hasMood && moodAge>=0;
            bool still=preferences.reducedMotion;
            float plant=Smooth(-1.02f,-CinematicOutcome.FootfallLead,age);
            float release=mood?Smooth(.05f,CinematicOutcome.RecoveryContact(1,0),moodAge):0;
            if(!still)
            {
                float lift=Smooth(-1.18f,-1.02f,age)*(1-plant);
                walkActor.RightLeg.localRotation=Quaternion.Euler(34*lift,0,0);
                float inhale=mood?(1-release)*(1-Smooth(1.5f,2f,moodAge)):0;
                walkActor.LeftArm.localRotation=Quaternion.Euler(12+15*inhale,0,-4-8*inhale);
                walkActor.RightArm.localRotation=Quaternion.Euler(12+15*inhale,0,4+8*inhale);
                walkActor.Head.localRotation=Quaternion.Euler(4+6*inhale-5*release,0,0);
            }

            Vector3 sole=cinematicStage.InverseTransformPoint(walkShoe.position);
            Vector3 chest=cinematicStage.InverseTransformPoint(moviePlayer.TransformPoint(new Vector3(0,1.25f,-.27f)));
            Vector3 cheek=cinematicStage.InverseTransformPoint(walkActor.Head.TransformPoint(new Vector3(.12f,-.055f,-.26f)));
            Vector3 bench=cinematicStage.InverseTransformPoint(walkBench.position);
            Vector3 ground=new Vector3(sole.x,.016f,sole.z);
            walkBadge.localPosition=chest;
            walkBadge.localRotation=moviePlayer.localRotation;
            walkBadge.gameObject.SetActive(hasEnergy && age>=0);
            int marks=hasEnergy?CinematicOutcome.RecoverySteps(changes[0]):0;
            int lit=hasEnergy?(still?marks:CinematicOutcome.LandedSteps(changes[0],age)):0;
            for(int i=0;i<walkMarks.Length;i++)
            {
                walkMarks[i].gameObject.SetActive(i<marks);
                walkSettings[i].gameObject.SetActive(i<marks);
                SetMovieColor(walkMarks[i],i<lit?new Color(.45f,1.7f,1.28f):new Color(.04f,.13f,.14f));
            }

            float rippleAge=(age+CinematicOutcome.FootfallLead)/1.15f;
            walkRipple.gameObject.SetActive(!still && rippleAge>=0 && rippleAge<1);
            walkRipple.localPosition=ground;
            rippleInk.SetFloat("_Travel",Mathf.Clamp01(rippleAge)); rippleInk.SetColor("_Tint",walkMint);
            Color tint=mood?walkAmber:walkMint;
            trailInk.SetColor("_Tint",tint);
            Vector3 start=mood?cheek:ground,end=mood?bench:chest;
            float strongest=0;
            for(int i=0;i<walkBands.Length;i++)
            {
                float travel=mood?(moodAge-.12f-i*.09f)/.78f:
                    (age-CinematicOutcome.RecoveryContact(0,i)+.55f)/.55f;
                bool visible=!still && (mood || i<marks) && travel>0 && travel<1.36f;
                LineRenderer band=walkBands[i]; band.gameObject.SetActive(visible);
                if(!visible) continue;
                float wave=Mathf.Sin(Mathf.PI*Mathf.Clamp01(travel/1.36f));
                band.widthMultiplier=(mood?.15f:.12f)*wave;
                for(int j=0;j<band.positionCount;j++)
                {
                    float u=Mathf.Clamp01(travel-(1-j/(float)(band.positionCount-1))*.39f);
                    band.SetPosition(j,WalkLightPath(start,end,u,i,mood));
                }
                if(wave>strongest)
                {
                    strongest=wave; walkLight.transform.localPosition=WalkLightPath(start,end,Mathf.Min(travel,1),i,mood)+Vector3.up*.16f;
                }
            }
            walkLight.color=tint; walkLight.intensity=still?0:strongest*2.3f;
            if(mood)
            {
                Color timber=wood.GetColor("_Color");
                for(int i=0;i<walkSlats.Length;i++)
                {
                    float sweep=still?1:Smooth(.54f+i*.09f,.82f+i*.09f,moodAge);
                    SetMovieColor(walkSlats[i],Color.Lerp(timber,walkAmber,sweep*.62f));
                }
            }
            else ClearWalkBench();
            if(still)
            {
                if(resourceBeat.HasResource)
                {
                    camera=moviePlayer.localPosition+new Vector3(.8f,1.7f,3.6f);
                    look=moviePlayer.localPosition+Vector3.up;
                    FitPortraitSubject(lifeActorRenderers,HorizonCinemaLayout.SubjectAperture,
                        CinematicFieldOfView(time),ref camera,ref look,false);
                }
                return;
            }
            // Start on the planted foot, follow the pulse up the coat, then include the bench in the exhale.
            float rise=hasEnergy?Smooth(-.20f,.87f,age):Smooth(-.3f,0,age);
            Vector3 target=Vector3.Lerp(ground+Vector3.up*.19f,chest,rise);
            Vector3 eye=Vector3.Lerp(new Vector3(.42f,.58f,2.35f),new Vector3(.48f,.58f,3.35f),rise);
            if(mood)
            {
                float breath=Smooth(0,1.05f,moodAge);
                target=Vector3.Lerp(target,Vector3.Lerp(cheek,bench,.36f),breath);
                eye=Vector3.Lerp(eye,new Vector3(-.87f,.54f,3.65f),breath);
            }
            float close=Smooth(-1.2f,-.76f,age)*(1-Smooth(consequenceTime-.1f,consequenceTime+1.1f,time));
            Vector3 aim=target+Vector3.down*.13f;
            camera=Vector3.Lerp(camera,target+eye,close); look=Vector3.Lerp(look,aim,close);
        }

        private static Vector3 WalkLightPath(Vector3 from,Vector3 to,float u,int strand,bool warm)
        {
            float arch=Mathf.Sin(u*Mathf.PI);
            float arc=u*Mathf.PI*(warm?1.3f:2.6f)+strand*.72f;
            return Vector3.Lerp(from,to,u)+arch*new Vector3(Mathf.Sin(arc)*(warm?.16f:.36f),
                warm?.32f+strand*.08f:.04f, .26f+Mathf.Cos(arc)*.16f);
        }

        private void ClearWalkBench()
        {
            for(int i=0;i<walkSlats.Length;i++)
                if(walkSlats[i]!=null && movieColors.ContainsKey(walkSlats[i])) ResetMovieColor(walkSlats[i]);
        }
    }
}
