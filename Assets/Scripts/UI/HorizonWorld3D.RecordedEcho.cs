using System.Linq;
using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private Transform echoTrace, echoImprint, echoArrivalMark;
        private LineRenderer echoRoute;
        private LineRenderer echoPulse;
        private Renderer echoFire, echoCore;
        private ScenicEnergy echoScenic;
        private Transform echoReceiver;
        private RewardEvidence echoSource;
        private static readonly Vector3 EchoArrival = new Vector3(0, 1.60f, 2);
        internal Vector2 EchoRevealOrigin { get; private set; } = new Vector2(.5f,.566f);

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
                echoReceiver=Group("Echo receiver guide",echoArrivalMark);
                Ring(echoReceiver,"Seal receiver",Vector3.zero,.49f,warmLight,true);
                echoRoute = CausalLine(echoTrace, "Actual source to receipt", Vector3.zero, Vector3.zero, glass, .070f);
                EnsureCaseEffectMaterial();
                echoPulse=CausalLine(echoTrace,"Moving echo front",Vector3.zero,Vector3.zero,caseEffectMaterial,.10f);
                echoScenic = CreateScenicEnergy(echoTrace);
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
            float flight = still ? (arrived ? 1 : 0) : CinematicEnergy.RecordedEchoFlight(age);
            int sourceIndex = echoSource == null ? -1 : moviePlan.Event.receipt.causes.IndexOf(echoSource);
            Vector3 origin = sourceIndex >= 0 && sourceIndex < movieNodes.Count ? movieNodes[sourceIndex].localPosition : new Vector3(-.79f, 1.38f, -.9f);
            Vector3 position = origin;
            for (int point = 0; point < echoRoute.positionCount; point++)
            {
                float along = point / (float)(echoRoute.positionCount - 1) * flight;
                position = Vector3.Lerp(origin, EchoArrival, along) + Vector3.up * (.83f * Mathf.Sin(along * Mathf.PI));
                echoRoute.SetPosition(point, position);
                float front=Mathf.Lerp(Mathf.Max(0,flight-.27f),flight,point/(float)(echoRoute.positionCount-1));
                echoPulse.SetPosition(point,Vector3.Lerp(origin,EchoArrival,front)+Vector3.up*(.83f*Mathf.Sin(front*Mathf.PI)));
            }
            echoImprint.localPosition = position + Vector3.up * .15f;
            echoImprint.localRotation = Quaternion.Euler(-18-22*flight,still?-20:-20+flight*188,-13*flight);
            echoImprint.localScale = Vector3.one * (still ? 1 : Mathf.Lerp(.82f, 1.67f, flight));
            echoImprint.gameObject.SetActive(time >= movieImpactTime - 2.52f && age < .031f);
            echoRoute.gameObject.SetActive(!still && flight > .005f && age < .52f);
            echoRoute.widthMultiplier = Mathf.Lerp(.050f,.122f,flight);
            SetMovieColor(echoRoute.transform, Color.Lerp(new Color(.49f,.58f,1),new Color(1,.68f,.24f),flight)
                * (.58f*(1-Smooth(0,.52f,age))));
            CaseEffect(echoPulse,3,Color.Lerp(new Color(.40f,.60f,1.4f),new Color(1.5f,.92f,.36f),flight),
                still || arrived?0:Smooth(.03f,.35f,flight)*.78f,0,age,cinematicEnergy.Seed);
            echoArrivalMark.localPosition = new Vector3(EchoArrival.x, .24f, EchoArrival.z);
            echoArrivalMark.gameObject.SetActive((arrived || !still && age>-.85f) && time< spectacle.SettlementTime);
            echoReceiver.gameObject.SetActive(!still && age>-.85f && age<.33f);
            echoReceiver.localPosition=Vector3.up*(EchoArrival.y-.24f)+Vector3.forward*.10f;
            echoReceiver.localScale=Vector3.one*(1.34f-.34f*Smooth(-.85f,.13f,age));
            EnergyRelease release = energyFrame.Release;
            movieLight.transform.localPosition = arrived ? EchoArrival + new Vector3(.23f, .35f, -.62f) : position + Vector3.up * .45f;
            movieLight.range = 5.5f;
            movieLight.color = Color.Lerp(new Color(.44f, .43f, 1), new Color(1, .75f, .40f),
                Mathf.Clamp01(release.Fire + release.Flash + release.Wave * .28f));
            movieLight.intensity = .34f + .28f * flight + 3.95f * release.Flash + 2.45f * release.Fire;
            if (!still && bloom != null && !LowCostEffects) bloom.Echo = .23f * release.Flash + .16f * release.Fire;
        }

        private void FrameRecordedEcho()
        {
            if (moviePlan?.RecordedEcho != true || echoTrace == null) return;
            EnergyRelease release = energyFrame.Release;
            float age = energyFrame.Time - movieImpactTime;
            float spread = .64f*Smooth(0,.38f,age)+.36f*Smooth(.33f,1.48f,age);
            float charge = preferences.reducedMotion ? 0 : CinematicEnergy.RecordedEchoCharge(age)*.44f;
            Color spill = Color.Lerp(new Color(.75f,.43f,1),new Color(1,.61f,.22f),
                Mathf.Clamp01(release.Flash + release.Fire * 1.25f));
            SampleScenicEnergy(echoScenic,echoTrace.TransformPoint(EchoArrival),-.033f,spill,charge,release,cinematicEnergy.Seed);
            Vector3 viewport=WorldCamera.WorldToViewportPoint(echoTrace.TransformPoint(EchoArrival));
            EchoRevealOrigin=new Vector2(Mathf.Clamp(viewport.x,.21f,.79f),Mathf.Clamp(viewport.y,.44f,.70f));
            echoFire.transform.localPosition = EchoArrival + new Vector3(-.045f, .14f + spread * .43f, .36f);
            echoFire.transform.rotation = WorldCamera.transform.rotation * Quaternion.Euler(0, -12, -13);
            echoFire.transform.localScale = new Vector3(1.52f + spread * 1.12f, 1.64f + spread * 1.14f, 1);
            CaseEffect(echoFire, 0, Color.Lerp(new Color(1.92f,1.39f,.84f),new Color(1.37f,.89f,1.94f),spread),
                release.Fire, spread * 63, age, cinematicEnergy.Seed);
            echoCore.transform.localPosition = EchoArrival + new Vector3(0, .01f, -.27f);
            echoCore.transform.rotation = WorldCamera.transform.rotation;
            echoCore.transform.localScale = new Vector3(1.30f, .99f, 1);
            CaseEffect(echoCore, 1, new Color(2.2f, 2.03f, 1.62f), release.Flash, 0, age, cinematicEnergy.Seed);
        }
    }
}
