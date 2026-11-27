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
