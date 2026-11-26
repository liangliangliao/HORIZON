using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private readonly Renderer[] caseFire = new Renderer[3];
        private readonly LineRenderer[] caseRays = new LineRenderer[12], caseEmbers = new LineRenderer[18];
        private Renderer caseCore, caseWave;
        private Light caseLight;
        private ScenicEnergy caseScenic;
        private Material caseEffectMaterial;
        private Vector3 caseRest;
        private float practiceBloomBefore;
        private bool caseVisualStarted, caseContactPlayed;

        public void BeginPracticeRewardCinematic(PracticeRewardCinematic film)
        {
            int[] receipt = new int[6];
            for (int axis = 0; axis < receipt.Length; axis++) receipt[axis] = film.TicksAt(axis);
            PreparePracticePayout(receipt, film.EventId, film.Difficulty, gainsOnly: true);
            for (int axis = 0; axis < receipt.Length; axis++)
                if (receipt[axis] > 0) EnsurePracticePrize(axis);

            caseRest = new Vector3(.12f, .873f, -.05f);
            chamber.localPosition = caseRest;
            rays.gameObject.SetActive(false);
            sparkRenderer.gameObject.SetActive(false);
            practiceFire.gameObject.SetActive(false);
            foreach (Transform seal in orbitingSeals) seal.gameObject.SetActive(false);
            GroundContact(actionTable, caseRest - Vector3.up * .031f, new Vector2(1.14f, .68f));

            EnsureCaseEffectMaterial();
            Transform effects = Group("Case energy effects", actionTable);
            caseScenic = CreateScenicEnergy(effects);
            caseCore = CaseQuad(effects, "Release core");
            caseWave = CaseQuad(effects, "Tabletop impact wake");
            caseWave.transform.localRotation = Quaternion.Euler(90, 0, 0);
            for (int i = 0; i < caseFire.Length; i++) caseFire[i] = CaseQuad(effects, "Case flame layer " + i);
            for (int i = 0; i < caseRays.Length; i++) caseRays[i] = CaseRibbon(effects, "Inward case beam " + i);
            for (int i = 0; i < caseEmbers.Length; i++) caseEmbers[i] = CaseRibbon(effects, "Release ember " + i);
            caseLight = Group("Case light on desk and cast", effects).gameObject.AddComponent<Light>();
            caseLight.type = LightType.Point;
            caseLight.range = 4.6f;
            caseLight.shadows = LightShadows.None;
            if (resourceChime == null) resourceChime = ShortCueTone("Glass resource contact", 528, .22f);
            if (bloom != null) practiceBloomBefore = bloom.Echo;
            WorldCamera.rect = new Rect(0, 0, 1, 1);
        }

        private void EnsureCaseEffectMaterial()
        {
            if (caseEffectMaterial != null) return;
            caseEffectMaterial = new Material(Resources.Load<Shader>("HorizonCaseEnergy"));
            caseEffectMaterial.mainTexture = Resources.Load<Texture2D>("HorizonCaseFlame");
            materials.Add(caseEffectMaterial);
        }

        private Renderer CaseQuad(Transform parent, string name) =>
            Shape(parent, name, PrimitiveType.Quad, Vector3.zero, Vector3.one, caseEffectMaterial).GetComponent<Renderer>();

        private LineRenderer CaseRibbon(Transform parent, string name)
        {
            LineRenderer ribbon = CausalLine(parent, name, Vector3.zero, Vector3.zero, caseEffectMaterial, 1);
            ribbon.positionCount = 2;
            ribbon.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ribbon.receiveShadows = false;
            return ribbon;
        }

        public void SamplePracticeRewardCinematic(PracticeRewardCinematic film, PracticeRewardCinematicFrame frame,
            bool playCues = true)
        {
            if (caseLight == null) return;
            bool reduced = preferences.reducedMotion, low = LowCostEffects;
            bool released = frame.Age >= 0;
            float dim = Smooth(.48f, 2.65f, frame.Time) * (1 - Smooth(.85f, 2.80f, frame.Age));
            DimPracticeScenery(reduced ? .12f : .12f + .50f * dim);
            chamber.gameObject.SetActive(true);
            heroProp.gameObject.SetActive(false);
            obstacle.gameObject.SetActive(false);
            chamber.localPosition = caseRest + Vector3.up * Mathf.Abs(frame.Tilt) * .0068f;
            chamber.localScale = new Vector3(1 + frame.Compression * .48f, 1 - frame.Compression, 1 + frame.Compression * .18f);
            chamber.localRotation = Quaternion.Euler(-frame.Tilt * .55f, 0, frame.Tilt);
            lidLeft.localRotation = Quaternion.Euler(0, 0, frame.Lid * 108);
            lidRight.localRotation = Quaternion.Euler(0, 0, -frame.Lid * 108);
            for (int i = 0; i < practiceMarks.Length; i++)
                practiceMarks[i].gameObject.SetActive(!reduced && !released && frame.Charge > .25f + i * .18f);

            float lean = reduced ? 0 : Smooth(.35f, 2.27f, frame.Time) * (1 - Smooth(-.13f, .03f, frame.Age));
            float recoil = reduced ? 0 : Smooth(0, .16f, frame.Age) * (1 - Smooth(.27f, .89f, frame.Age));
            float approach = reduced ? 0 : Smooth(.59f, 1.32f, frame.Age);
            performer.localPosition = new Vector3(-.61f + .22f * lean - .17f * recoil + .17f * approach, 0,
                .61f - .15f * lean + .28f * recoil - .06f * approach);
            performer.localRotation = Quaternion.Euler(8 * lean - 10 * recoil, -33 + 15 * recoil, 8 * recoil);
            if (reduced) practiceActor.Play(HorizonBodyAction.Wait, .55f);
            else if (!released) practiceActor.Play(HorizonBodyAction.Reach, lean * .65f, .83f);
            else if (frame.Age < .89f) practiceActor.Play(HorizonBodyAction.Stumble, recoil);
            else practiceActor.Play(HorizonBodyAction.Reach, Smooth(.89f, 2.0f, frame.Age), .65f);
            if (partner.gameObject.activeSelf)
            {
                partner.localPosition = new Vector3(.83f + .17f * recoil, 0, .87f + .12f * recoil);
                partner.localRotation = Quaternion.Euler(-5 * recoil, 38 - 15 * recoil, -6 * recoil);
                practicePartner.Play(HorizonBodyAction.Wait, .21f + .48f * approach);
            }

            Vector3 center = actionTable.TransformPoint(caseRest + new Vector3(.01f, .33f, -.065f));
            float push = reduced ? 0 : Smooth(.25f, 2.51f, frame.Time);
            float pull = reduced ? 0 : Smooth(.03f, .87f, frame.Age);
            float lens = 45 - 6 * push * (1 - pull);
            float aspect = Mathf.Max(.27f, WorldCamera.aspect);
            float distance = Mathf.Max(2.83f, 1.61f / aspect) + .45f * pull - .25f * push * (1 - pull);
            float side = Mathf.Lerp(-1.21f, .43f, push) + .22f * pull;
            if (reduced) side = .22f;
            Vector3 look = center + new Vector3(-.18f * (1 - push) + .11f * pull, -.07f + .10f * pull, .1f);
            WorldCamera.fieldOfView = lens;
            WorldCamera.transform.position = look + new Vector3(side, .94f - .58f * push + .47f * pull, -distance);
            WorldCamera.transform.LookAt(look);

            Vector3 seam = actionTable.InverseTransformPoint(chamber.TransformPoint(new Vector3(0, .31f, -.10f)));
            SamplePracticeCaseLight(frame, film.Seed, seam, reduced, low);
            int gain = 0;
            for (int axis = 0; axis < prizePieces.Length; axis++)
            {
                if (prizePieces[axis] == null) continue;
                SamplePracticeGain(axis, frame.Age - (reduced ? 0 : gain * .065f), reduced);
                gain++;
            }

            if (!playCues || reduced || !film.HasGain)
            {
                if (caseVisualStarted) audioSource.Stop();
                caseVisualStarted = false;
                return;
            }
            if (!caseVisualStarted && !released)
            {
                caseVisualStarted = true;
                if (preferences.sound && frame.Time < 2.51f) PlayTensionCharge(2.66f - frame.Time);
            }
            if (released && !practiceImpactPlayed)
            {
                practiceImpactPlayed = true;
                CueSound(impactTone, .49f);
                PlayRevealShimmer(.24f);
                MasterHaptics.Cue(29, 95, preferences);
            }
            if (frame.Time >= PracticeRewardCinematic.LandingTime && !caseContactPlayed)
            {
                caseContactPlayed = true;
                CueSound(resourceChime, .35f);
            }
        }

        private void SamplePracticeGain(int axis, float age, bool reduced)
        {
            float revealAge = PracticeRewardCinematic.GainTime - PracticeRewardCinematic.ReleaseTime;
            float landingAge = PracticeRewardCinematic.LandingTime - PracticeRewardCinematic.ReleaseTime;
            float flight = reduced ? 1 : Smooth(revealAge, landingAge, age);
            Transform item = prizePieces[axis];
            item.gameObject.SetActive(age >= revealAge);
            item.localPosition = Vector3.Lerp(new Vector3(0, .35f, -.15f), prizeLandings[axis], flight) +
                Vector3.up * (.64f * Mathf.Sin(Mathf.PI * flight));
            item.localScale = Vector3.one * (.50f + .14f * Mathf.Sin(Mathf.PI * flight));
            item.localRotation = Quaternion.Euler(-18 * (1 - flight), Mathf.Lerp(-91, 14, flight), 10 * Mathf.Sin(Mathf.PI * flight));
            prizeSockets[axis].gameObject.SetActive(age >= (reduced ? revealAge : .43f));
            prizeBeams[axis].gameObject.SetActive(!reduced && age >= landingAge && age < landingAge + .23f);
        }

        private void SamplePracticeCaseLight(PracticeRewardCinematicFrame frame, float seed, Vector3 seam, bool reduced, bool low)
        {
            EnergyRelease release = frame.Release;
            float warm = Smooth(.95f, 2.72f, frame.Time);
            Color cool = script.Id == "ask" ? new Color(.27f, .69f, 1) : new Color(.57f, .47f, 1);
            Color amber = new Color(1, .51f, .16f);
            Color light = Color.Lerp(cool, amber, warm);
            caseLight.transform.localPosition = seam + new Vector3(0, .22f * release.Fire + .02f, -.24f);
            caseLight.color = Color.Lerp(light, new Color(1, .89f, .71f), release.Flash);
            caseLight.intensity = reduced ? .32f : .13f + frame.Charge * 1.38f + release.Flash * 3.25f + release.Fire * 2.48f;
            if (bloom != null) bloom.Echo = low || reduced ? 0 : release.Flash * .27f + release.Fire * .21f;
            Vector3 worldSeam = actionTable.TransformPoint(seam);
            SampleScenicEnergy(caseScenic, worldSeam, .012f, light, reduced ? 0 : frame.Charge, release, seed);

            Quaternion view = WorldCamera.transform.rotation;
            caseCore.transform.localPosition = seam + new Vector3(0, .035f, -.095f);
            caseCore.transform.rotation = view;
            caseCore.transform.localScale = new Vector3(.82f, .57f, 1) * (1 + release.Flash * .68f);
            CaseEffect(caseCore, 1, Color.Lerp(light * .83f, new Color(2.4f, 1.99f, 1.55f), release.Flash),
                .23f * frame.Charge + release.Flash, 0, frame.Age, seed);

            for (int i = 0; i < caseFire.Length; i++)
            {
                Renderer plume = caseFire[i];
                float spread = Smooth(0, 1.52f, frame.Age);
                plume.transform.localPosition = seam + new Vector3((i == 0 ? 0 : i == 1 ? -.22f : .20f) * spread,
                    .19f + .33f * spread, i == 0 ? -.14f : i == 1 ? .25f : -.26f);
                plume.transform.rotation = view * Quaternion.Euler(0, (i - 1) * 31, (i - 1) * (18 + 14 * spread));
                float size = Mathf.Lerp(1.26f, 2.08f, spread) * (i == 0 ? 1 : .81f);
                plume.transform.localScale = new Vector3(size, size * 1.19f, 1);
                float opacity = !low || i == 0 ? release.Fire * (i == 0 ? 1.06f : .54f) : 0;
                CaseEffect(plume, 0, new Color(2.05f, 1.41f, .93f), opacity, 63 * spread, frame.Age, seed);
            }

            // The released energy keeps its launch point while the case recoils.
            Vector3 origin = caseRest + new Vector3(.015f, .31f, -.115f);
            caseWave.transform.localPosition = new Vector3(origin.x, caseRest.y + .018f, origin.z);
            float waveSize = Mathf.Lerp(.30f, 2.12f, Smooth(.06f, 1.12f, frame.Age));
            caseWave.transform.localScale = new Vector3(waveSize, waveSize * .84f, 1);
            CaseEffect(caseWave, 2, new Color(1.24f, .47f, .105f), release.Wave * .82f, 0, frame.Age, seed);

            for (int i = 0; i < caseRays.Length; i++)
            {
                LineRenderer ray = caseRays[i];
                if (reduced || frame.Age >= 0 || i >= (low ? 4 : caseRays.Length))
                {
                    ray.gameObject.SetActive(false);
                    continue;
                }
                float angle = i * 2.39996f + seed * 5.2f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), .40f + .15f * (i % 4), Mathf.Sin(angle));
                float pulse = .58f + .42f * Mathf.Sin(frame.Time * (8 + i % 3) + i * 1.31f);
                float intensity = Mathf.Max(0, frame.Charge - .10f - i * .018f) * pulse;
                ray.widthMultiplier = (i % 3 == 0 ? .17f : .035f) * (.5f + frame.Charge);
                ray.SetPosition(0, seam + direction * (.05f + .16f * (1 - pulse)));
                ray.SetPosition(1, seam + direction * (.25f + frame.Charge * (.84f + .27f * pulse)));
                CaseEffect(ray, 3, light * 1.28f, intensity, 0, frame.Age, seed);
            }

            for (int i = 0; i < caseEmbers.Length; i++)
            {
                float age = frame.Age - (i % 6) * .027f;
                LineRenderer spark = caseEmbers[i];
                if (reduced || age <= 0 || age >= 1.38f || i >= (low ? 6 : caseEmbers.Length))
                {
                    spark.gameObject.SetActive(false);
                    continue;
                }
                float opacity = Mathf.Clamp01(age * 29) * (1 - Smooth(.27f, 1.38f, age)) * .92f;
                float angle = i * 2.39996f + seed * 7;
                Vector3 velocity = new Vector3(Mathf.Cos(angle) * (1.0f + .11f * (i % 7)),
                    .83f + .24f * (i % 6), Mathf.Sin(angle) * (.63f + .15f * (i % 4)));
                spark.widthMultiplier = .013f + .006f * (i % 4);
                float tail = Mathf.Max(0, age - .08f);
                spark.SetPosition(0, origin + velocity * age - Vector3.up * (1.21f * age * age));
                spark.SetPosition(1, origin + velocity * tail - Vector3.up * (1.21f * tail * tail));
                CaseEffect(spark, 3, Color.Lerp(new Color(2.4f, 1.26f, .32f), new Color(.72f, .22f, .05f), age),
                    opacity, 0, age, seed);
            }
        }

        private void CaseEffect(Renderer effect, int layer, Color tint, float opacity, float image, float age, float seed)
        {
            bool visible = opacity > .004f;
            effect.gameObject.SetActive(visible);
            if (!visible) return;
            effectProperties.Clear();
            effectProperties.SetFloat("_Layer", layer);
            effectProperties.SetColor("_Color", tint);
            effectProperties.SetVector("_Cue", new Vector4(image, opacity, age, seed));
            if (effect == caseWave)
            {
                Vector3 position = effect.transform.localPosition;
                Vector3 scale = effect.transform.localScale;
                effectProperties.SetVector("_Surface", new Vector4(position.x - .12f, position.z, scale.x * .5f, scale.y * .5f));
            }
            effect.SetPropertyBlock(effectProperties);
        }

        private void EndPracticeRewardCinematic()
        {
            if (caseLight != null && bloom != null) bloom.Echo = practiceBloomBefore;
            if (caseVisualStarted) audioSource.Stop();
            caseLight = null;
            caseScenic = null;
            caseVisualStarted = caseContactPlayed = false;
        }
    }
}
