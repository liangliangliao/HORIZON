using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private sealed class ScenicEnergy
        {
            public readonly Renderer Rays, Floor;
            public readonly Light Bounce;

            public ScenicEnergy(Renderer rays, Renderer floor, Light bounce)
            { Rays = rays; Floor = floor; Bounce = bounce; }
        }

        private ScenicEnergy CreateScenicEnergy(Transform parent)
        {
            Renderer rays = CaseQuad(parent, "Scenic reveal rays");
            Renderer floor = CaseQuad(parent, "Travelling floor light");
            floor.transform.rotation = Quaternion.Euler(90, 0, 0);
            Light bounce = Group("Scenic bounce light", parent).gameObject.AddComponent<Light>();
            bounce.type = LightType.Point;
            bounce.shadows = LightShadows.None;
            bounce.range = 6.2f;
            bounce.intensity = 0;
            return new ScenicEnergy(rays, floor, bounce);
        }

        private static float RewardFocusPulse(float age) => Smooth(0,.11f,age)*(1-Smooth(.20f,.66f,age));

        public void FocusRewardCharge()
        {
            if(paused || preferences.reducedMotion) return;
            bool echo=Cinematics.IsPlaying && moviePlan?.RecordedEcho==true;
            float time=echo?Cinematics.Elapsed:caseCueTime;
            float last=echo?echoFocusAt:caseFocusAt;
            float hold=echo?RecordedEchoStory.HoldAt:PracticeRewardCinematic.HoldTime;
            if((!echo && caseLight==null) || time<.70f || time>=hold || time-last<.60f) return;
            if(echo) echoFocusAt=time;
            else caseFocusAt=time;
            PrepareRevealSounds();
            SetPracticeAudioTime(time);
            CueSound(revealBolts[0],.27f);
            MasterHaptics.Cue(15,42,preferences);
        }

        private void SampleRewardChargeBeams(LineRenderer[] beams,Vector3 origin,Color tint,float charge,float time,float seed)
        {
            bool low=LowCostEffects;
            float reach=Mathf.Lerp(.14f,1.65f,Smooth(.07f,.83f,charge));
            float speed=Mathf.Lerp(1.35f,3.7f,charge);
            int count=charge>.018f?Mathf.Min(low?6:beams.Length,2+Mathf.FloorToInt(charge*beams.Length)):0;
            for(int i=0;i<beams.Length;i++)
            {
                LineRenderer beam=beams[i];
                if(i>=count)
                {
                    beam.gameObject.SetActive(false);
                    continue;
                }
                float angle=i*2.39996f+seed*5.2f+time*.26f;
                Vector3 direction=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle)*.75f+.27f,(i%5-2)*.22f);
                float travel=1-Mathf.Repeat(time*speed+i*.173f,1);
                float intensity=charge*Smooth(0,.23f,travel)*(1-Smooth(.71f,1,travel));
                beam.widthMultiplier=(i%3==0?.067f:.021f)*(.30f+charge);
                beam.SetPosition(0,origin+direction*(.04f+reach*travel*.73f));
                beam.SetPosition(1,origin+direction*(.11f+reach*travel));
                CaseEffect(beam,3,tint*1.52f,intensity,0,time,seed);
            }
        }

        // This goes through scene transparency on GLES. The direct glow and
        // spill use a fixed draw budget in normal and battery quality.
        private void SampleScenicEnergy(ScenicEnergy track, Vector3 center, float floorHeight, Color tint,
            float charge, EnergyRelease release, float seed)
        {
            Camera camera = WorldCamera;
            Transform rays = track.Rays.transform;
            Vector3 far = center + camera.transform.forward * 1.15f + Vector3.up * .32f;
            rays.SetPositionAndRotation(far, camera.transform.rotation * Quaternion.Euler(0, 0, release.Age * 21f));
            float depth = Mathf.Max(1, Vector3.Dot(far - camera.transform.position, camera.transform.forward));
            float height = 2 * depth * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
            rays.localScale = new Vector3(height * camera.aspect * 1.58f, height * .93f, 1);
            float after = Mathf.Max(release.Fire, release.Wave * .77f);
            float glow = Mathf.Max(charge * .46f, Mathf.Max(after, release.Flash * .84f));
            CaseEffect(track.Rays, 4, Color.Lerp(tint * 1.52f, new Color(1.72f, 1.49f, 1.10f), release.Flash * .45f),
                glow * .96f, 0, release.Age, seed);

            Transform floor = track.Floor.transform;
            floor.position = new Vector3(center.x, floorHeight, center.z);
            float reach = Mathf.Lerp(.40f, 8.1f, EnergyRelease.WaveTravel(release.Age));
            floor.localScale = new Vector3(reach, reach * .81f, 1);
            CaseEffect(track.Floor, 5, tint * 1.7f, release.Wave * .93f, 0, release.Age, seed);
            track.Bounce.transform.position = center + camera.transform.forward * .67f + Vector3.up * .82f;
            track.Bounce.color = tint;
            track.Bounce.intensity = release.Flash * 2.05f + after * 1.8f;
        }
    }
}
