using System.Collections;
using System.Collections.Generic;
using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private Transform timelineGroup, memoryGroup, rareGroup;
        private readonly List<Transform> memoryOrbs = new List<Transform>();
        private AudioSource ambience;
        private AudioLowPassFilter ambientFilter;
        private bool rareActive;
        private int touchGeneration;

        private void InitializeAtmosphere()
        {
            var source = new GameObject("Quiet horizon sound", typeof(AudioSource), typeof(AudioLowPassFilter));
            source.transform.SetParent(transform, false);
            ambience = source.GetComponent<AudioSource>();
            ambience.spatialBlend = 0; ambience.volume = 0.055f; ambience.loop = true;
            ambientFilter = source.GetComponent<AudioLowPassFilter>();
            const int rate = 22050;
            var samples = new float[rate * 16];
            float[] notes = { 130.81f, 164.81f, 196f, 146.83f };
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)rate;
                float note = notes[Mathf.Min(3, (int)(time / 4))];
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Repeat(time, 4) / 4);
                samples[i] = (Mathf.Sin(time * note * Mathf.PI * 2) * 0.16f +
                    Mathf.Sin(time * note * 2.002f * Mathf.PI * 2) * 0.055f) * envelope;
            }
            AudioClip pad = AudioClip.Create("Horizon breathing", samples.Length, 1, rate, false);
            pad.SetData(samples, 0); sounds.Add(pad);
            ambience.clip = pad; ambience.Play();
        }

        public void Focus(bool enabled)
        {
            if (ambientFilter != null) ambientFilter.cutoffFrequency = enabled ? 650 : 18000;
            if (ambience != null) ambience.volume = enabled ? 0.025f : 0.055f;
            if (Avatar != null) Avatar.GetComponent<HorizonActor>().MotionRate = paused ? 0 : enabled ? 0.2f : 1;
        }

        private Vector3 DayPoint(int day)
        {
            float angle = Mathf.Lerp(-155, 155, Mathf.Clamp01((day - 1) / (float)Mathf.Max(1, timelineLength - 1))) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(angle) * 3.8f, 0.09f, 1 - Mathf.Cos(angle) * 3.8f);
        }

        private int timelineLength = 12;
        public void SetTimeline(List<ActionRecord> actions, int length = 12)
        {
            if (timelineGroup != null) Dispose(timelineGroup.gameObject);
            timelineLength = length == 30 || length == 60 ? length : 12;
            timelineGroup = Group("Days left in the world");
            for (int day = 1; day <= timelineLength; day++)
            {
                ActionRecord action = actions?.Find(a => a.day == day);
                Material material = action == null ? dark : action.kind == CardKind.Growth ? teal :
                    action.kind == CardKind.Temptation ? pink : gold;
                Shape(timelineGroup, "Day " + day, PrimitiveType.Sphere, DayPoint(day),
                    Vector3.one * (action == null ? 0.11f : 0.22f), material);
                if (action != null && action.echoed)
                    Ring(timelineGroup, "Returned day " + day, DayPoint(day) + Vector3.up * 0.05f, 0.2f, glass, false);
            }
            BuildTimelineLinks(actions);
        }

        public void BeginEcho(PendingEcho echo)
        {
            Avatar.GetComponent<HorizonActor>().FreezeUntil = Time.unscaledTime + 0.15f;
            if (ambientFilter != null) ambientFilter.cutoffFrequency = 500;
            if (bloom != null && !preferences.reducedMotion) bloom.Echo = 0.55f;
            if (preferences.reducedMotion) return;
            cameraPosition = new Vector3(2.2f, 3.3f, -6.3f);
            cameraLook = Vector3.Lerp(Avatar.position + Vector3.up, DayPoint(echo.sourceDay) + Vector3.up, 0.3f);
        }

        public void ReactToEcho(PendingEcho echo)
        {
            ResourceDelta delta = echo.actualDelta ?? echo.delta;
            if (delta != null && (delta.energy < 0 || delta.mood < 0 || delta.relation < 0 || delta.money < 0))
            {
                Avatar.GetComponent<HorizonActor>().TiredUntil = Time.unscaledTime + 1.3f;
                shake = 0.11f; Play(2);
            }
            else Reward(3);
        }

        public void PowerGate(List<int> days, int gate)
        {
            if (days == null) return;
            StartCoroutine(GateHistory(days, gate));
        }
        private IEnumerator GateHistory(List<int> days, int gate)
        {
            foreach (int day in days)
            {
                StartCoroutine(GateRay(day, gate));
                yield return new WaitForSecondsRealtime(0.025f);
            }
        }
        private IEnumerator GateRay(int day, int gate)
        {
            Transform orb = Shape(transform, "Past day powering a door", PrimitiveType.Sphere,
                DayPoint(day) + Vector3.up * 0.2f, Vector3.one * 0.16f, gold);
            Vector3 start = orb.position, end = new Vector3((gate - 1) * 2.7f, 1.8f, 4);
            for (float age = 0; age < 0.42f; age += Time.unscaledDeltaTime)
            {
                while (paused) yield return null;
                if (orb == null) yield break;
                float t = age / 0.42f;
                orb.position = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI);
                yield return null;
            }
            if (orb != null) Dispose(orb.gameObject);
        }

        public void ShowGhost(int day)
        {
            ShowDeadline();
            futureSelf.gameObject.SetActive(true);
            futureSelf.position = DayPoint(day) + new Vector3(0.5f, -0.09f, 0.3f);
            futureSelf.rotation = Quaternion.Euler(0, 170, 0);
            futureSelf.GetComponent<HorizonActor>().SetNeutral();
            futureSelf.GetComponent<HorizonActor>().Pointing = true;
            WorldCamera.rect = new Rect(0, 0.58f, 1, 0.33f);
            cameraPosition = futureSelf.position + new Vector3(3, 2.8f, -5.3f);
            cameraLook = futureSelf.position + Vector3.up * 1.1f;
        }

        public void ShowOutlook(List<ActionRecord> actions)
        {
            ShowStation(2, true);
            ShowMemories(actions.FindAll(a => a.day == 14 || a.day == 21 || a.day == 28));
            WorldCamera.rect = new Rect(0, 0.43f, 1, 0.49f);
            if (timelineGroup != null) Dispose(timelineGroup.gameObject);
            timelineGroup = Group("Thirty possible days");
            for (int day = 1; day <= 30; day++)
            {
                float a = Mathf.Lerp(-150, 150, (day - 1) / 29f) * Mathf.Deg2Rad;
                ActionRecord action = actions.Find(item => item.day == day);
                Vector3 point = new Vector3(Mathf.Sin(a) * 3.7f, 0.08f, 4.2f - Mathf.Cos(a) * 3.7f);
                Shape(timelineGroup, "Possible day " + day, PrimitiveType.Sphere, point, Vector3.one * 0.12f,
                    action == null ? dark : action.kind == CardKind.Growth ? teal : action.kind == CardKind.Recovery ? gold : pink);
            }
        }

        public void ArriveEcho(PendingEcho echo)
        {
            if (ambientFilter != null) ambientFilter.cutoffFrequency = 18000;
            StartCoroutine(EchoLight(echo));
        }

        private IEnumerator EchoLight(PendingEcho echo)
        {
            Transform orb = IntentionSymbol(echo.kind, DayPoint(echo.sourceDay) + Vector3.up * 0.2f,
                CardCatalog.FindById(echo.cardId)?.GivesSupport ?? false, true);
            Trail(orb, echo.kind == CardKind.Temptation ? warmLight : portalLight);
            Vector3 start = orb.position;
            Vector3 end = Avatar.position + Vector3.up * 1.3f;
            for (float age = 0; age < 0.46f; age += Time.unscaledDeltaTime)
            {
                while (paused) yield return null;
                if (orb == null) yield break;
                float t = age / 0.46f;
                orb.position = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.4f;
                orb.localScale = Vector3.one * (0.2f + Mathf.Sin(t * Mathf.PI) * 0.22f);
                yield return null;
            }
            if (orb != null) Dispose(orb.gameObject);
            Burst(end, echo.kind == CardKind.Temptation ? Palette.Coral : Palette.Mint, 28);
        }

        public void ShowMemories(List<ActionRecord> memories)
        {
            touchGeneration++;
            if (memoryGroup != null) Dispose(memoryGroup.gameObject);
            memoryGroup = Group("The choices you left behind");
            memoryOrbs.Clear();
            for (int i = 0; i < memories.Count; i++)
            {
                ActionRecord action = memories[i];
                Transform branch = Group("Memory of day " + action.day, memoryGroup);
                branch.localPosition = new Vector3((i - (memories.Count - 1) * 0.5f) * 2.4f, 1.25f, 5.1f);
                Material material = action.kind == CardKind.Temptation ? pink : action.kind == CardKind.Growth ? teal : gold;
                Transform orb = Shape(branch, "Original action", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.25f, material);
                memoryOrbs.Add(orb);
                Shape(branch, "Possible future", PrimitiveType.Sphere, new Vector3(0.35f, 0.8f, 0), Vector3.one * 0.19f, material);
                var line = new GameObject("Memory connection", typeof(LineRenderer)).GetComponent<LineRenderer>();
                line.transform.SetParent(branch, false); line.useWorldSpace = false;
                line.sharedMaterial = glass; line.widthMultiplier = 0.032f;
                line.positionCount = 3; line.SetPositions(new[] { Vector3.zero, new Vector3(0.35f, 0.8f, 0), new Vector3(0, 1.35f, 0) });
                Ring(branch, "Memory halo", Vector3.zero, 0.35f, glass, true);
                if (action.kind == CardKind.Growth)
                {
                    Box(branch, "A book left behind", new Vector3(0, -0.38f, 0), new Vector3(0.5f, 0.08f, 0.34f), material);
                    Box(branch, "Pages", new Vector3(0, -0.33f, 0), new Vector3(0.44f, 0.035f, 0.29f), gold);
                }
                else if (action.kind == CardKind.Temptation)
                    Box(branch, "A phone left behind", new Vector3(0, -0.4f, 0), new Vector3(0.23f, 0.4f, 0.06f), material);
                else Shape(branch, "A breath left behind", PrimitiveType.Capsule,
                    new Vector3(0, -0.37f, 0), new Vector3(0.2f, 0.2f, 0.2f), material);
            }
        }

        public void TouchMemory(int index)
        {
            if (index < 0 || index >= memoryOrbs.Count || !station) return;
            StartCoroutine(WalkToMemory(memoryOrbs[index].position, ++touchGeneration));
        }

        private IEnumerator WalkToMemory(Vector3 target, int generation)
        {
            HorizonActor actor = futureSelf.GetComponent<HorizonActor>();
            Vector3 start = futureSelf.position;
            Vector3 end = new Vector3(Mathf.Clamp(target.x, -1.7f, 1.7f), 0, 4.4f);
            if (preferences.reducedMotion)
            {
                futureSelf.position = end; actor.Walking = false; actor.Pointing = true;
                if (!memoryReading) { cameraPosition = futureSelf.position + stationCameraOffset;
                    cameraLook = futureSelf.position + Vector3.up * 1.35f; }
                yield break;
            }
            actor.Walking = true;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / 2.2f)
            {
                while (paused) yield return null;
                if (!station || generation != touchGeneration) yield break;
                futureSelf.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0, 1, t));
                if (!memoryReading) { cameraPosition = futureSelf.position + stationCameraOffset;
                    cameraLook = futureSelf.position + Vector3.up * 1.35f; }
                yield return null;
            }
            actor.Walking = false; actor.Pointing = true;
            Burst(target, Palette.Mint, 12);
            Tick(1);
        }

        public void ShowRare(int type)
        {
            ShowBoard(); rareActive = true;
            WorldCamera.rect = new Rect(0, 0.33f, 1, 0.59f);
            cameraPosition = new Vector3(2.8f, 3.6f, -7.7f);
            if (rareGroup != null) Dispose(rareGroup.gameObject);
            rareGroup = Group("A rare instant");
            if (type == 1)
            {
                futureSelf.gameObject.SetActive(true);
                StartCoroutine(ParallelWalk());
            }
            else
            {
                Ring(rareGroup, "Time fracture", new Vector3(0, 1.8f, 2.8f), 1.2f, glass, true);
                Ring(rareGroup, "Another edge", new Vector3(0.18f, 1.8f, 2.9f), 1.35f, glass, true);
                for (int i = 0; i < 6; i++)
                    Shape(rareGroup, "Leaked future", PrimitiveType.Sphere,
                        new Vector3(Mathf.Sin(i * 2.3f), 1 + i * 0.3f, 2.5f), Vector3.one * 0.13f, type == 3 ? gold : teal);
            }
            Reward(3, true);
        }

        private IEnumerator ParallelWalk()
        {
            var actor = futureSelf.GetComponent<HorizonActor>();
            actor.Walking = true;
            futureSelf.localRotation = Quaternion.Euler(0, -90, 0);
            for (float age = 0; age < 5 && rareActive; age += Time.unscaledDeltaTime)
            {
                while (paused) yield return null;
                futureSelf.localPosition = new Vector3(Mathf.Lerp(-4, 4, age / 5), 0, 1.3f);
                yield return null;
            }
            actor.Walking = false;
            if (futureSelf != null) futureSelf.gameObject.SetActive(false);
        }
    }

    public sealed class HorizonActor : MonoBehaviour
    {
        public Transform Head, LeftArm, RightArm, LeftLeg, RightLeg, LeftEye, RightEye;
        public GameObject Phone, Book;
        public bool Walking, Pointing;
        public float MotionRate = 1, FreezeUntil, TiredUntil;
        private int intent = -1;
        private float phase, cheer;
        public void SetNeutral() { intent = -1; if (Phone != null) Phone.SetActive(false); if (Book != null) Book.SetActive(false); }
        public void SetIntent(CardKind kind)
        { intent = (int)kind; Phone.SetActive(kind == CardKind.Temptation); Book.SetActive(kind == CardKind.Growth); }
        public void Celebrate() { cheer = 1; }
        private void Update()
        {
            if (Head == null || MotionRate <= 0 || VisualPreferences.Paused || Time.unscaledTime < FreezeUntil) return;
            float dt = Time.unscaledDeltaTime * MotionRate;
            phase += dt; cheer = Mathf.MoveTowards(cheer, 0, dt * 0.8f);
            float step = Walking ? Mathf.Sin(phase * 8) * 26 : Mathf.Sin(phase * 1.8f) * 3;
            float arm = intent == 0 ? 52 : intent == 1 ? 28 : 0;
            LeftArm.localRotation = Quaternion.Euler(arm + step + cheer * 55, 0, -5 - cheer * 20);
            RightArm.localRotation = Quaternion.Euler(Pointing ? 85 : arm - step + cheer * 55, 0, 5 + cheer * 20);
            LeftLeg.localRotation = Quaternion.Euler(Walking ? -step : 0, 0, 0);
            RightLeg.localRotation = Quaternion.Euler(Walking ? step : 0, 0, 0);
            Head.localRotation = Quaternion.Euler(Time.unscaledTime < TiredUntil ? 24 :
                intent == 0 ? 12 : Mathf.Sin(phase * 0.8f) * 3, Mathf.Sin(phase * 0.55f) * 5, 0);
            float blink = Mathf.Repeat(phase, 4.7f) > 4.56f ? 0.1f : 1;
            LeftEye.localScale = new Vector3(0.052f, 0.061f * blink, 0.042f);
            RightEye.localScale = LeftEye.localScale;
        }
    }
}
