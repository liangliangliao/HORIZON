using System.Linq;
using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private Transform echoTrace, echoImprint, echoArrivalMark;
        private LineRenderer echoRoute;
        private Renderer echoFire, echoCore;
        private RewardEvidence echoSource;
        private static readonly Vector3 EchoArrival = new Vector3(0, 1.60f, 2);

        private void PrepareRecordedEcho()
        {
            if (!moviePlan.RecordedEcho) return;
            echoSource = moviePlan.Event.receipt.causes.LastOrDefault(node => node.kind == CausalNodeKind.Action &&
                node.day == moviePlan.Event.receipt.sourceDay);
            if (echoTrace == null)
            {
                echoTrace = Group("Recorded source arriving on the time route", cinematicStage);
                echoImprint = Group("Recorded action imprint", echoTrace);
                Sculpt(echoImprint, "Engraved echo seal", embossedCoin, Vector3.zero, Vector3.one * .37f, showcaseEnamel);
                Ring(echoImprint, "Imprint rim", Vector3.back * .07f, .23f, gold, true);
                echoArrivalMark = Group("Stable echo receipt anchor", echoTrace);
                Ring(echoArrivalMark, "Echo contact surface", Vector3.zero, .48f, portalLight, false);
                echoRoute = CausalLine(echoTrace, "Actual source to receipt", Vector3.zero, Vector3.zero, glass, .032f);
                EnsureCaseEffectMaterial();
                echoFire = CaseQuad(echoTrace, "Echo flame at the receipt anchor");
                echoCore = CaseQuad(echoTrace, "Echo core at the receipt anchor");
            }
            echoTrace.gameObject.SetActive(true);
            echoImprint.name = echoSource == null ? "Recorded action: " + moviePlan.Event.receipt.action : "Recorded action: " + echoSource.id;
        }

        private void SampleRecordedEcho(float time)
        {
            if (!moviePlan.RecordedEcho) return;
            bool still = preferences.reducedMotion;
            float age = time - movieImpactTime;
            bool arrived = age >= 0;
            float flight = still ? (arrived ? 1 : 0) : Smooth(movieImpactTime - 2.65f, movieImpactTime, time);
            int sourceIndex = echoSource == null ? -1 : moviePlan.Event.receipt.causes.IndexOf(echoSource);
            Vector3 origin = sourceIndex >= 0 && sourceIndex < movieNodes.Count ? movieNodes[sourceIndex].localPosition : new Vector3(-.79f, 1.38f, -.9f);
            Vector3 position = origin;
            for (int point = 0; point < echoRoute.positionCount; point++)
            {
                float along = point / (float)(echoRoute.positionCount - 1) * flight;
                position = Vector3.Lerp(origin, EchoArrival, along) + Vector3.up * (.64f * Mathf.Sin(along * Mathf.PI));
                echoRoute.SetPosition(point, position);
            }
            echoImprint.localPosition = position + Vector3.up * .15f;
            echoImprint.localRotation = Quaternion.Euler(-25, still ? -20 : -20 + flight * 215, still ? 0 : flight * 13);
            echoImprint.gameObject.SetActive(time >= movieImpactTime - 2.65f && age < .03f);
            echoRoute.gameObject.SetActive(!still && flight > .005f && age < .72f);
            SetMovieColor(echoRoute.transform, new Color(.48f, .63f, 1) * (1 - Smooth(0, .72f, age)));
            echoArrivalMark.localPosition = new Vector3(EchoArrival.x, .24f, EchoArrival.z);
            echoArrivalMark.gameObject.SetActive(arrived && time < spectacle.SettlementTime);
            EnergyRelease release = energyFrame.Release;
            movieLight.transform.localPosition = arrived ? EchoArrival + new Vector3(.23f, .35f, -.62f) : position + Vector3.up * .45f;
            movieLight.range = 5.5f;
            movieLight.color = Color.Lerp(new Color(.41f, .59f, 1), new Color(1, .67f, .32f), release.Fire + release.Flash);
            movieLight.intensity = .34f + .28f * flight + 2.4f * release.Flash + 1.65f * release.Fire;
            if (!still && bloom != null && !LowCostEffects) bloom.Echo = .23f * release.Flash + .16f * release.Fire;
        }

        private void FrameRecordedEcho()
        {
            if (moviePlan?.RecordedEcho != true || echoTrace == null) return;
            EnergyRelease release = energyFrame.Release;
            float spread = Smooth(0, .93f, release.Age);
            echoFire.transform.localPosition = EchoArrival + new Vector3(-.045f, .05f + spread * .46f, -.19f);
            echoFire.transform.rotation = WorldCamera.transform.rotation * Quaternion.Euler(0, -12, -13);
            echoFire.transform.localScale = new Vector3(1f + spread * .82f, 1.1f + spread * 1.07f, 1);
            CaseEffect(echoFire, 0, new Color(1.21f, 1.03f, 1.82f), release.Fire, spread * 63, release.Age, cinematicEnergy.Seed);
            echoCore.transform.localPosition = EchoArrival + new Vector3(0, .01f, -.37f);
            echoCore.transform.rotation = WorldCamera.transform.rotation;
            echoCore.transform.localScale = new Vector3(.82f, .66f, 1);
            CaseEffect(echoCore, 1, new Color(2.2f, 2.03f, 2.48f), release.Flash, 0, release.Age, cinematicEnergy.Seed);
        }
    }
}
