using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Horizon.UI
{
    // Only the scene camera owns these values. UI, capture and background cameras
    // never inherit a global blur. Sampling is driven by the pauseable director.
    public sealed class HorizonOptics : MonoBehaviour
    {
        [Range(0, 1)] public float Defocus;
        [Range(0, 1)] public float Distortion;
        [Range(0, 1)] public float Glow;
        public float FocusDistance = 8;
        public bool LowPower;
        public bool Active => enabled && (Defocus > .001f || Distortion > .001f || Glow > .001f);
        public void Clear() { Defocus = Distortion = Glow = 0; }
    }

}
