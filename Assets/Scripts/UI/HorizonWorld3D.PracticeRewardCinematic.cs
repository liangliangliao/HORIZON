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
        private readonly Transform[] caseLocks = new Transform[3];
        private Transform caseHandle;
        public Vector2 CaseRevealOrigin { get; private set; }
        private Material caseEffectMaterial;
        private Vector3 caseRest;
        private float practiceBloomBefore;
        private bool caseVisualStarted, caseSilent;
        private float caseCueTime;

        public void BeginPracticeRewardCinematic(PracticeRewardCinematic film)
        {
            int[] receipt = new int[6];
            for (int axis = 0; axis < receipt.Length; axis++) receipt[axis] = film.TicksAt(axis);
            PreparePracticePayout(receipt, film.EventId, film.Difficulty, gainsOnly: true);
            for (int axis = 0; axis < receipt.Length; axis++)
                if (receipt[axis] > 0) EnsurePracticePrize(axis);

            caseRest = new Vector3(.12f, .873f, -.05f);
            chamber.localPosition = caseRest;
            caseHandle=Group("Manual release lever",chamber);
            caseHandle.localPosition=new Vector3(-.34f,.277f,.15f);
            Box(caseHandle,"Handle arm",new Vector3(-.051f,.018f,.01f),new Vector3(.15f,.036f,.054f),prizeBrass);
            Shape(caseHandle,"Grip",PrimitiveType.Sphere,new Vector3(-.12f,.026f,.01f),new Vector3(.074f,.075f,.074f),teal);
            for(int i=0;i<caseLocks.Length;i++)
            {
                Transform bolt=Group("Sliding case bolt "+(i+1),chamber);
                bolt.localPosition=new Vector3((i-1)*.23f,.242f,-.277f);
                Box(bolt,"Machined bolt",Vector3.up*.042f,new Vector3(.105f,.153f,.050f),prizeBrass);
                Box(bolt,"Blue face inset",new Vector3(0,.073f,-.027f),new Vector3(.060f,.075f,.009f),prizeEnamel);
                caseLocks[i]=bolt;
            }
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
            caseCueTime = -1;
            PrepareRevealSounds();
#if UNITY_EDITOR
            recordedCinemaSounds.Clear();
#endif
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
            float pose = released ? frame.Time : Mathf.Min(frame.Time,PracticeRewardCinematic.HoldTime);
            float dim = Smooth(.79f, PracticeRewardCinematic.HoldTime-.31f, pose) *
                (1 - Smooth(.39f, 1.66f, frame.Age));
            DimPracticeScenery(reduced ? .12f : .06f + .59f * dim);
            chamber.gameObject.SetActive(true);
            heroProp.gameObject.SetActive(false);
            obstacle.gameObject.SetActive(false);
            chamber.localPosition = caseRest + Vector3.up * Mathf.Abs(frame.Tilt) * .0068f;
            chamber.localScale = new Vector3(1 + frame.Compression * .48f, 1 - frame.Compression, 1 + frame.Compression * .18f);
            chamber.localRotation = Quaternion.Euler(-frame.Tilt * .55f, 0, frame.Tilt);
            float lidAngle = (frame.Lid + frame.LidStrain) * 108;
            lidLeft.localRotation = Quaternion.Euler(0, 0, lidAngle);
            lidRight.localRotation = Quaternion.Euler(0, 0, -lidAngle);
            for (int i = 0; i < practiceMarks.Length; i++)
            {
                float age=pose-PracticeRewardCinematic.LatchTime(i);
                float slide=reduced?1:Smooth(0,.14f,age);
                Transform bolt=caseLocks[i];
                float direction=i==1?0:i==0?-1:1;
                bolt.localPosition=new Vector3((i-1)*.23f+direction*.057f*slide,.242f-.12f*slide,-.277f-.025f*slide);
                bolt.localRotation=Quaternion.Euler(-53*slide,0,-direction*17*slide+direction*frame.LidStrain*24*(1-slide));
                practiceMarks[i].gameObject.SetActive(!reduced && age>=0 && age<.12f && !released);
            }

            float lean = reduced ? 0 : Smooth(.29f, 1.24f, pose) * (1 - Smooth(0,.11f,frame.Age));
            float grip=reduced?0:Smooth(.66f,1.18f,pose)*(1-Smooth(0,.09f,frame.Age));
            float recoil = reduced ? 0 : Smooth(.021f,.15f,frame.Age) * (1 - Smooth(.24f,.61f,frame.Age));
            float approach = reduced ? 0 : Smooth(.66f, 1.47f, frame.Age);
            caseHandle.localRotation=Quaternion.Euler(0,0,-32*Smooth(1.13f,1.31f,pose)*(1-Smooth(0,.12f,frame.Age))
                -28*frame.LidStrain);
            Vector3 handle=caseHandle.TransformPoint(new Vector3(-.12f,.026f,.01f));
            performer.localPosition = new Vector3(-.58f + .21f * lean - .21f * recoil + .13f * approach, 0,
                .49f - .30f * lean + .31f * recoil - .05f * approach);
            performer.localRotation = Quaternion.Euler(11 * lean - 15 * recoil, -26 + 16 * recoil, 11 * recoil);
            if (reduced) practiceActor.Play(HorizonBodyAction.Wait, .55f);
            else if (!released || frame.Age<.095f)
                practiceActor.Play(HorizonBodyAction.Reach,lean*.57f,.80f,handle,grip);
            else if (frame.Age < .64f) practiceActor.Play(HorizonBodyAction.Stumble, recoil);
            else practiceActor.Play(HorizonBodyAction.Reach, Smooth(.73f, 1.96f, frame.Age), .65f);
            if (partner.gameObject.activeSelf)
            {
                partner.localPosition = new Vector3(.83f + .17f * recoil, 0, .87f + .12f * recoil);
                partner.localRotation = Quaternion.Euler(-5 * recoil, 38 - 15 * recoil, -6 * recoil);
                practicePartner.Play(recoil>.01f?HorizonBodyAction.Stumble:HorizonBodyAction.Wait,
                    recoil>.01f?recoil*.63f:.21f+.48f*approach,focus:reduced?(Vector3?)null:handle);
            }

            Vector3 center = actionTable.TransformPoint(caseRest + new Vector3(.01f, .33f, -.065f));
            float push = reduced ? 0 : .65f*Smooth(.32f,1.45f,pose)+.35f*
                Smooth(PracticeRewardCinematic.LatchTime(1),PracticeRewardCinematic.HoldTime-.12f,pose);
            float pull = reduced ? 0 : .84f*Smooth(.04f,.31f,frame.Age)+.16f*Smooth(.59f,1.26f,frame.Age);
            float kick = reduced ? 0 : Smooth(0,.065f,frame.Age)*(1-Smooth(.072f,.28f,frame.Age));
            float lens = 45 - 7 * push * (1 - pull) + 2.8f*kick;
            float aspect = Mathf.Max(.27f, WorldCamera.aspect);
            float distance = Mathf.Max(2.85f, 1.63f / aspect) + .32f * pull - .27f * push * (1 - pull) - .10f*kick;
            float side = Mathf.Lerp(-1.26f,-.58f,push) + .96f * pull;
            if (reduced) side = .22f;
            Vector3 look = center + new Vector3(-.15f-.105f*push+.23f*pull,-.12f+.12f*pull+.04f*kick,.06f);
            WorldCamera.fieldOfView = lens;
            WorldCamera.transform.position = look + new Vector3(side, .94f - .58f * push + .47f * pull, -distance);
            WorldCamera.transform.LookAt(look);

            Vector3 seam = actionTable.InverseTransformPoint(chamber.TransformPoint(new Vector3(0, .31f, -.10f)));
            CaseRevealOrigin=WorldCamera.WorldToViewportPoint(actionTable.TransformPoint(seam));
            SamplePracticeCaseLight(film,frame,pose,seam,reduced,low);
            for (int axis = 0; axis < prizePieces.Length; axis++)
            {
                if (prizePieces[axis] == null) continue;
                SamplePracticeGain(axis,frame.Age-(reduced?0:film.GainSlot(axis)*PracticeRewardCinematic.ObjectSpacing),reduced);
            }

            SamplePracticeRevealSounds(film,frame.Time,playCues && !reduced && film.HasGain);
        }

        private void SamplePracticeGain(int axis, float age, bool reduced)
        {
            float revealAge = PracticeRewardCinematic.GainTime - PracticeRewardCinematic.ReleaseTime;
            float landingAge = PracticeRewardCinematic.LandingTime - PracticeRewardCinematic.ReleaseTime;
            float takeoff=revealAge-.095f;
            float lift=reduced?1:Smooth(takeoff,.37f,age);
            float drift=reduced?1:.57f*Smooth(.23f,.74f,age)+.43f*Smooth(.79f,landingAge,age);
            float drop=reduced?1:Smooth(.83f,landingAge,age);
            float bounce=reduced?0:.11f*ImmersiveMotion.Punch(age-landingAge,.21f);
            Transform item = prizePieces[axis];
            item.gameObject.SetActive(age >= (reduced?revealAge:takeoff));
            item.localPosition = Vector3.Lerp(new Vector3(0, .26f, -.14f),prizeLandings[axis],drift)+
                Vector3.up*(lift*.63f*(1-drop)+bounce);
            item.localScale = Vector3.one * (.50f + .17f * lift*(1-drop));
            item.localRotation = Quaternion.Euler(-20*(1-drift),Mathf.Lerp(-115,14,lift)-21*drop,12*Mathf.Sin(Mathf.PI*drift));
            prizeSockets[axis].gameObject.SetActive(age >= (reduced ? revealAge : .67f));
            prizeBeams[axis].gameObject.SetActive(!reduced && age >= landingAge && age < landingAge + .18f);
        }

        private void SamplePracticeCaseLight(PracticeRewardCinematic film,PracticeRewardCinematicFrame frame,
            float pose,Vector3 seam,bool reduced,bool low)
        {
            float seed=film.Seed;
            EnergyRelease release = frame.Release;
            bool held=frame.Time>=PracticeRewardCinematic.HoldTime && frame.Age<0;
            float warm = Smooth(PracticeRewardCinematic.LatchTime(1)-.30f, PracticeRewardCinematic.HoldTime-.15f, pose);
            Color cool = script.Id == "ask" ? new Color(.27f, .69f, 1) : new Color(.57f, .47f, 1);
            Color amber = new Color(1, .51f, .16f);
            Color light = Color.Lerp(cool, amber, warm);
            float sparkAtLatch=0,contact=0;
            for(int i=0;i<caseLocks.Length;i++)
                sparkAtLatch=Mathf.Max(sparkAtLatch,ImmersiveMotion.Punch(pose-PracticeRewardCinematic.LatchTime(i),.16f));
            for(int axis=0;axis<prizePieces.Length;axis++)
                if(film.TicksAt(axis)>0) contact=Mathf.Max(contact,ImmersiveMotion.Punch(frame.Time-film.ContactTime(axis),.23f));
            caseLight.transform.localPosition = seam + new Vector3(0, .22f * release.Fire + .02f, -.24f);
            caseLight.color = Color.Lerp(light, new Color(1, .89f, .71f), release.Flash);
            caseLight.intensity = reduced ? .32f : .11f + frame.Charge*(held?.28f:.75f) +
                frame.LidStrain*1.6f + sparkAtLatch*.91f +
                release.Flash*4.0f + release.Fire*2.32f + .78f*contact;
            if (bloom != null) bloom.Echo = low || reduced ? 0 : release.Flash * .27f + release.Fire * .21f;
            Vector3 worldSeam = actionTable.TransformPoint(seam);
            SampleScenicEnergy(caseScenic, worldSeam, .012f, light,reduced || held?0:frame.Charge*.34f,release,seed);

            Quaternion view = WorldCamera.transform.rotation;
            caseCore.transform.localPosition = seam + new Vector3(0, .035f, -.095f);
            caseCore.transform.rotation = view;
            caseCore.transform.localScale = Vector3.Lerp(new Vector3(.27f,.11f,1),new Vector3(.82f,.57f,1),
                release.Flash)*(1 + release.Flash*.68f);
            CaseEffect(caseCore, 1, Color.Lerp(light * .83f, new Color(2.4f, 1.99f, 1.55f), release.Flash),
                .10f*frame.Charge+.70f*frame.LidStrain+release.Flash, 0, frame.Age, seed);

            for (int i = 0; i < caseFire.Length; i++)
            {
                Renderer plume = caseFire[i];
                float spread = .64f*Smooth(0,.38f,frame.Age)+.36f*Smooth(.33f,1.48f,frame.Age);
                plume.transform.localPosition = seam + new Vector3((i == 0 ? 0 : i == 1 ? -.22f : .20f) * spread,
                    .19f + .33f * spread, i == 0 ? -.14f : i == 1 ? .25f : -.26f);
                plume.transform.rotation = view * Quaternion.Euler(0, (i - 1) * 31, (i - 1) * (18 + 14 * spread));
                float size = Mathf.Lerp(1.26f, 2.08f, spread) * (i == 0 ? 1 : .81f);
                plume.transform.localScale = new Vector3(size, size * 1.19f, 1);
                float opacity = i < (low?1:film.Difficulty)?release.Fire*(i==0?1.06f:.54f):0;
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
                if (reduced || held || pose < .99f || frame.Age >= 0 || i >= (low ? 4 : caseRays.Length))
                {
                    ray.gameObject.SetActive(false);
                    continue;
                }
                float angle = i * 2.39996f + seed * 5.2f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), .40f + .15f * (i % 4), Mathf.Sin(angle));
                float period=Mathf.Lerp(.49f,.19f,Smooth(PracticeRewardCinematic.LatchTime(0),PracticeRewardCinematic.HoldTime,pose));
                float inward=1-Mathf.Repeat((pose-.99f)/period+i*.173f,1);
                float intensity=frame.Charge*Smooth(0,.25f,inward)*(1-Smooth(.81f,1,inward));
                ray.widthMultiplier=(i%3==0?.072f:.022f)*(.5f+frame.Charge);
                ray.SetPosition(0,seam+direction*(.04f+inward*.94f));
                ray.SetPosition(1,seam+direction*(.14f+inward*1.34f));
                CaseEffect(ray,3,light*1.28f,intensity,0,frame.Age,seed);
            }

            for (int i = 0; i < caseEmbers.Length; i++)
            {
                float age = frame.Age - (i % 6) * .027f;
                LineRenderer spark = caseEmbers[i];
                if (reduced || age <= 0 || age >= 1.38f || i >= (low?6:9+3*film.Difficulty))
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
            caseSilent=false;
            UpdateAudio();
            caseLight = null;
            caseScenic = null;
            caseVisualStarted = false;
        }
    }
}
