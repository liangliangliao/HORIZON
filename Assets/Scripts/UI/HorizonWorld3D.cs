using System.Collections;
using System.Collections.Generic;
using Horizon.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace Horizon.UI
{
    // A real, lit 3D diorama. Geometry and sound are authored here so the Android
    // build has no missing asset downloads, font-dependent symbols or paid packs.
    public sealed partial class HorizonWorld3D : MonoBehaviour
    {
        public Camera WorldCamera { get; private set; }
        public Camera BackgroundCamera { get; private set; }
        public Transform Avatar { get; private set; }
        private Transform futureSelf, bench, gates, environment, companions;
        private readonly Transform[] props = new Transform[3];
        private readonly Transform[] doorLights = new Transform[3];
        private readonly List<Material> materials = new List<Material>();
        private readonly List<AudioClip> sounds = new List<AudioClip>();
        private Material floor, dark, teal, gold, pink, glass, skin, cloth, hair;
        private Light key;
        private AudioSource audioSource;
        private Vector3 cameraPosition, cameraLook, avatarHome, stationCameraOffset;
        private float shake, pulse;
        private bool station;
        private int activeKind = -1;

        public void Initialize()
        {
            if (WorldCamera != null) return;
            var backdrop = new GameObject("HORIZON full-frame clear", typeof(Camera));
            backdrop.transform.SetParent(transform, false);
            BackgroundCamera = backdrop.GetComponent<Camera>();
            BackgroundCamera.depth = -100;
            BackgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            BackgroundCamera.backgroundColor = Palette.Ink;
            BackgroundCamera.cullingMask = 0;
            BackgroundCamera.allowHDR = BackgroundCamera.allowMSAA = false;
            floor = Lit(new Color(0.09f, 0.18f, 0.25f));
            dark = Lit(new Color(0.04f, 0.09f, 0.15f));
            teal = Lit(new Color(0.15f, 0.65f, 0.63f), new Color(0.04f, 0.18f, 0.16f));
            gold = Lit(new Color(1, 0.65f, 0.18f), new Color(0.2f, 0.1f, 0.02f));
            pink = Lit(new Color(0.88f, 0.24f, 0.39f), new Color(0.2f, 0.03f, 0.06f));
            glass = Glow(new Color(0.35f, 1, 0.85f, 0.65f));
            skin = Lit(new Color(0.94f, 0.69f, 0.51f));
            cloth = Lit(new Color(0.15f, 0.37f, 0.53f));
            hair = Lit(new Color(0.06f, 0.04f, 0.07f));
            foreach (AudioListener listener in FindObjectsOfType<AudioListener>()) listener.enabled = false;
            var cameraObject = new GameObject("HORIZON 3D Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            WorldCamera = cameraObject.GetComponent<Camera>();
            WorldCamera.depth = 0;
            WorldCamera.clearFlags = CameraClearFlags.SolidColor;
            WorldCamera.nearClipPlane = 0.1f;
            WorldCamera.farClipPlane = 80;
            WorldCamera.fieldOfView = 43;
            WorldCamera.allowHDR = false;
            WorldCamera.allowMSAA = SceneMsaaEnabled;
            WorldCamera.cullingMask = ~(1 << 5);
            key = new GameObject("Warm key light", typeof(Light)).GetComponent<Light>();
            key.transform.SetParent(transform, false);
            key.type = LightType.Directional;
            key.transform.rotation = Quaternion.Euler(38, -28, 0);
            key.intensity = 1.1f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.6f;
            QualitySettings.shadowDistance = 22;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.antiAliasing = SceneMsaaEnabled ? 2 : 0;
            RenderSettings.ambientLight = new Color(0.23f, 0.29f, 0.39f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.025f;
            InitializeArt();
            environment = Group("Future platform");
            BuildPlatform();
            DressPlatform();
            StaticBatchingUtility.Combine(environment.gameObject);
            Avatar = Person("You", transform, new Vector3(-0.7f,0,0), cloth);
            avatarHome = Avatar.localPosition;
            companions = Group("Friends and collaborators");
            Person("Friend", companions, new Vector3(-1.8f, 0, 0.65f), teal);
            Person("Collaborator", companions, new Vector3(0.5f, 0, 1.2f), pink);
            companions.gameObject.SetActive(false);
            futureSelf = Person("Future you", transform, new Vector3(0, 0, 4.2f), cloth);
            bench = Group("Future station bench");
            Box(bench, "Seat", new Vector3(0, 0.55f, 4.8f), new Vector3(2.8f, 0.15f, 0.55f), floor);
            Box(bench, "Back", new Vector3(0, 0.9f, 5.05f), new Vector3(2.8f, 0.55f, 0.12f), teal);
            for (int i = -1; i <= 1; i += 2)
                Box(bench, "Leg", new Vector3(i, 0.25f, 4.8f), new Vector3(0.13f, 0.5f, 0.4f), dark);
            BuildProps();
            DressProps();
            gates = Group("Deadline gates");
            for (int i = 0; i < 3; i++)
            {
                Transform door = Group("Gate " + i, gates);
                door.localPosition = new Vector3((i - 1) * 2.7f, 0, 4);
                Box(door, "Left column", new Vector3(-0.85f, 1.3f, 0), new Vector3(0.22f, 2.6f, 0.4f), floor);
                Box(door, "Right column", new Vector3(0.85f, 1.3f, 0), new Vector3(0.22f, 2.6f, 0.4f), floor);
                Box(door, "Lintel", new Vector3(0, 2.6f, 0), new Vector3(1.92f, 0.2f, 0.4f), floor);
                doorLights[i] = Box(door, "Living doorway", new Vector3(0, 1.3f, 0),
                    new Vector3(1.5f, 2.45f, 0.035f), Glow(new Color(0.1f, 0.3f, 0.3f, 0.35f)));
            }
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0;
            audioSource.volume = 0.18f;
            sounds.Add(Tone("Accept", new[] { 440f, 660f, 880f }, 0.3f));
            sounds.Add(Tone("Echo reward", new[] { 330f, 440f, 660f, 880f, 1100f }, 0.7f));
            sounds.Add(Tone("Impact", new[] { 160f, 110f, 70f }, 0.25f));
            sounds.Add(Tone("Cascade", new[] { 440f, 554f, 660f, 880f, 1108f, 1320f }, 0.7f));
            InitializeAtmosphere();
            InitializeCinematics();
            SetTheme(0);
            ShowBoard();
            SnapCamera();
        }

        public void SetTheme(int theme)
        {
            Color sky = theme == 1 ? new Color(0.23f, 0.14f, 0.24f) :
                theme == 2 ? new Color(0.13f, 0.065f, 0.24f) : new Color(0.025f, 0.045f, 0.1f);
            WorldCamera.backgroundColor = sky;
            RenderSettings.fogColor = sky;
            if (RenderSettings.skybox != null)
            {
                RenderSettings.skybox.SetColor("_Top", sky);
                RenderSettings.skybox.SetColor("_Horizon", theme == 1 ? new Color(0.48f,0.27f,0.28f) :
                    theme == 2 ? new Color(0.22f,0.24f,0.46f) : new Color(0.13f,0.3f,0.34f));
            }
            key.color = theme == 1 ? new Color(1, 0.7f, 0.48f) :
                theme == 2 ? new Color(0.75f, 0.65f, 1) : new Color(0.8f, 0.9f, 1);
        }

        public void ShowBoard()
        {
            EndCausalPresentation();
            rareActive = false;
            Focus(false);
            if (memoryGroup != null) memoryGroup.gameObject.SetActive(false);
            if (rareGroup != null) rareGroup.gameObject.SetActive(false);
            station = false;
            Aim(false, false, CardKind.Growth);
            WorldCamera.rect = new Rect(0, 0.43f, 1, 0.49f);
            cameraPosition = new Vector3(3.8f, 3.3f, -7.4f);
            cameraLook = new Vector3(0, 2.1f, 1.35f);
            Avatar.gameObject.SetActive(true);
            Avatar.localPosition = avatarHome;
            Avatar.GetComponent<HorizonActor>().Walking = false;
            Avatar.localRotation = Quaternion.Euler(0,-18,0);
            futureSelf.gameObject.SetActive(false);
            bench.gameObject.SetActive(false);
            gates.gameObject.SetActive(false);
            for (int i = 0; i < props.Length; i++) props[i].gameObject.SetActive(activeKind < 0 || i == activeKind);
        }

        public void Preview(CardKind kind, bool support = false)
        {
            activeKind = (int)kind;
            for (int i = 0; i < props.Length; i++) props[i].gameObject.SetActive(i == activeKind);
            companions.gameObject.SetActive(support);
            Avatar.GetComponent<HorizonActor>().SetIntent(kind);
            Avatar.localRotation = Quaternion.Euler(0, kind == CardKind.Growth ? -38 : kind == CardKind.Temptation ? 30 : -8, 0);
        }

        public void ShowStation(int stage, bool reveal)
        {
            EndCausalPresentation();
            station = true;
            Aim(false,false,CardKind.Growth);
            rareActive = false;
            if (rareGroup != null) rareGroup.gameObject.SetActive(false);
            Focus(false);
            WorldCamera.rect = new Rect(0, 0, 1, 1);
            cameraPosition = new Vector3(2.5f - stage * 0.55f, 2.8f, -6 + stage * 1.6f);
            cameraLook = new Vector3(0, 1.35f, 4.2f);
            Avatar.gameObject.SetActive(false);
            companions.gameObject.SetActive(false);
            futureSelf.gameObject.SetActive(true);
            futureSelf.localRotation = Quaternion.Euler(0, reveal ? 0 : 145, 0);
            futureSelf.localPosition = new Vector3(0, 0, 4.2f);
            stationCameraOffset = cameraPosition - futureSelf.position;
            futureSelf.GetComponent<HorizonActor>().Pointing = false;
            futureSelf.GetComponent<HorizonActor>().Walking = false;
            bench.gameObject.SetActive(true);
            gates.gameObject.SetActive(false);
            foreach (Transform prop in props) prop.gameObject.SetActive(false);
        }

        public void ShowDeadline()
        {
            station = false;
            Aim(false,false,CardKind.Growth);
            WorldCamera.rect = new Rect(0, 0.39f, 1, 0.51f);
            cameraPosition = new Vector3(4.5f, 3.8f, -9.5f);
            cameraLook = new Vector3(0, 1.2f, 3);
            gates.gameObject.SetActive(true);
            if (memoryGroup != null) memoryGroup.gameObject.SetActive(false);
            Avatar.gameObject.SetActive(true);
            companions.gameObject.SetActive(false);
            futureSelf.gameObject.SetActive(false);
            bench.gameObject.SetActive(false);
            foreach (Transform prop in props) prop.gameObject.SetActive(false);
        }

        public void OpenGate(int gate, bool passed, bool animate = true)
        {
            Material material = doorLights[gate].GetComponent<Renderer>().sharedMaterial;
            material.SetColor("_Color", passed ? new Color(0.15f, 0.58f, 0.38f, 0.4f) :
                new Color(0.8f, 0.17f, 0.21f, 0.25f));
            if (passed && animate) Burst(new Vector3((gate - 1) * 2.7f, 1.8f, 4), Palette.Gold, 24);
        }

        public void Accept(CardKind kind, bool support = false, int dueDay = 0)
        {
            audioSource.pitch = 1;
            Preview(kind, support);
            pulse = kind == CardKind.Growth ? 0.12f : 1;
            if (kind != CardKind.Growth) Avatar.GetComponent<HorizonActor>().Celebrate();
            shake = kind == CardKind.Growth ? 0 : kind == CardKind.Temptation ? 0.18f : 0.07f;
            if (kind != CardKind.Growth) Play(kind == CardKind.Temptation ? 2 : 0);
            StartCoroutine(SendSymbol(kind, dueDay));
        }

        public void Reward(int amount, bool cascade = false)
        {
            audioSource.pitch = 1;
            Avatar.GetComponent<HorizonActor>().Celebrate();
            Burst(Avatar.position + Vector3.up * 1.4f, Palette.Gold, Mathf.Clamp(amount * 4, 12, 48));
            pulse = 1;
            shake = cascade ? 0.14f : 0.05f;
            Play(cascade ? 3 : 1);
            WorldRipple(Avatar.position + Vector3.up * 0.06f, cascade ? warmLight : portalLight, cascade ? 1.3f : 0.8f);
            if (bloom != null && !preferences.reducedMotion) bloom.Echo = cascade ? 0.65f : 0.25f;
        }

        public void Tick(int step) { audioSource.pitch = 0.8f + step * 0.12f; Play(0); }
        private void Play(int index) { audioSource.PlayOneShot(sounds[index]); }

        public void SnapCamera()
        {
            if (moviePlan != null || imaginationSceneActive) return;
            WorldCamera.transform.position = cameraPosition;
            WorldCamera.transform.LookAt(cameraLook);
        }

        private void Update()
        {
            if (WorldCamera == null || paused) return;
            if (Cinematics != null && Cinematics.IsPlaying || imaginationSceneActive) return;
            UpdateInsight();
            float dt = Time.unscaledDeltaTime;
            shake = Mathf.MoveTowards(shake, 0, dt * 0.32f);
            Vector3 drift = station || preferences.reducedMotion ? Vector3.zero : new Vector3(Mathf.Sin(Time.unscaledTime * 0.28f) * 0.14f, 0, 0);
            WorldCamera.transform.position = Vector3.Lerp(WorldCamera.transform.position,
                cameraPosition + drift, 1 - Mathf.Exp(-dt * 6));
            WorldCamera.transform.position += new Vector3(Mathf.Sin(Time.unscaledTime * 65),
                Mathf.Cos(Time.unscaledTime * 73), 0) * (preferences.reducedMotion ? 0 : shake);
            WorldCamera.transform.LookAt(cameraLook);
            Avatar.localScale = Vector3.one * (1 + Mathf.Sin(Time.unscaledTime * 2) * 0.01f + pulse * 0.04f);
            pulse = Mathf.MoveTowards(pulse, 0, dt * 2);
            for (int i = 0; i < props.Length; i++)
                props[i].localRotation = Quaternion.Euler(0, Mathf.Sin(Time.unscaledTime * 0.7f + i) * 4, 0);
        }

        private IEnumerator SendSymbol(CardKind kind, int dueDay)
        {
            Transform symbol = IntentionSymbol(kind, Avatar.position + Vector3.up * 1.8f);
            Trail(symbol, kind == CardKind.Temptation ? warmLight : portalLight);
            Vector3 from = symbol.position;
            Vector3 to = kind == CardKind.Temptation ? new Vector3(0, 0.3f, -1) :
                dueDay > 0 && dueDay <= timelineLength ? DayPoint(dueDay) + Vector3.up * 0.25f : new Vector3(0, 2.3f, 6);
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / 0.65f)
            {
                symbol.position = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.2f;
                symbol.Rotate(60 * Time.unscaledDeltaTime, 180 * Time.unscaledDeltaTime, 0);
                symbol.localScale = Vector3.one * Mathf.Lerp(0.25f, 0.07f, t);
                yield return null;
            }
            if (kind == CardKind.Growth) WorldRipple(to, portalLight, 0.22f);
            else Burst(to, kind == CardKind.Temptation ? Palette.Coral : Palette.Mint, 20);
            Destroy(symbol.gameObject);
        }

        private void BuildPlatform()
        {

            Ring(environment, "Platform neon", new Vector3(0, 0.015f, 1), 5.1f, glass, false);
            for (int i = 0; i < 6; i++)
            {
                Box(environment, "Path tile", new Vector3(0, 0.005f, i), new Vector3(1.5f, 0.04f, 0.75f), dark);
                Box(environment, "Path light", new Vector3(-0.78f, 0.03f, i), new Vector3(0.035f, 0.04f, 0.5f), teal);
                Box(environment, "Path light", new Vector3(0.78f, 0.03f, i), new Vector3(0.035f, 0.04f, 0.5f), teal);
            }

            for (int i = 0; i < 15; i++)
            {
                float x = (i - 7) * 1.8f;
                float h = 2 + ((i * 7) % 9) * 0.4f;
                Box(environment, "City tower", new Vector3(x, h * 0.5f - 1, 12 + i % 3),
                    new Vector3(1.1f, h, 1.1f), dark);
                for (int w = 0; w < 3; w++)
                    Box(environment, "City window", new Vector3(x, w * 0.7f + 0.2f, 11.4f + i % 3),
                        new Vector3(0.55f, 0.12f, 0.02f), w % 2 == 0 ? teal : gold);
            }
            for (int i = 0; i < 36; i++)
                Shape(environment, "Suspended star", PrimitiveType.Sphere,
                    new Vector3(Mathf.Sin(i * 2.4f) * 17, 5 + i % 7, 14 + i % 4),
                    Vector3.one * (i % 3 == 0 ? 0.065f : 0.035f), glass);
        }

        private void BuildProps()
        {
            props[0] = Group("Temptation phone");
            props[0].localPosition = new Vector3(-2, 0, 0.8f);
            Box(props[0], "Phone frame", new Vector3(0, 1.1f, 0), new Vector3(0.78f, 1.4f, 0.12f), dark);
            Box(props[0], "Living screen", new Vector3(0, 1.1f, -0.07f), new Vector3(0.65f, 1.2f, 0.025f), pink);
            for (int i = 0; i < 3; i++)
                Box(props[0], "Feed strip", new Vector3(0, 0.75f + i * 0.32f, -0.1f), new Vector3(0.48f, 0.08f, 0.02f), gold);
            props[1] = Group("Growth desk and books");
            props[1].localPosition = new Vector3(2.1f, 0, 1.2f);
            Box(props[1], "Desk top", new Vector3(0, 0.85f, 0), new Vector3(1.4f, 0.12f, 0.85f), floor);
            for (int i = -1; i <= 1; i += 2)
                Box(props[1], "Desk leg", new Vector3(i * 0.55f, 0.4f, 0), new Vector3(0.12f, 0.8f, 0.55f), dark);
            for (int i = 0; i < 3; i++)
            {
                Transform book = Box(props[1], "Book cover", new Vector3(-0.25f + i * 0.25f, 1 + i * 0.13f, 0),
                    new Vector3(0.62f, 0.06f, 0.4f), i % 2 == 0 ? teal : gold);
                book.localRotation = Quaternion.Euler(0, i * 18, 0);
                Box(book, "Book pages", new Vector3(0, 0.02f, 0), new Vector3(0.9f, 0.6f, 0.9f), Lit(new Color(0.85f, 0.91f, 0.84f)));
            }
            props[2] = Group("Recovery moon and garden");
            props[2].localPosition = new Vector3(-1.1f, 0, 3.5f);
            Shape(props[2], "Warm moon", PrimitiveType.Sphere, new Vector3(0, 1.7f, 0), Vector3.one * 0.75f, gold);
            Box(props[2], "Quiet seat", new Vector3(0, 0.45f, -0.2f), new Vector3(1.4f, 0.18f, 0.6f), floor);
            for (int i = 0; i < 3; i++)
                Shape(props[2], "Plant", PrimitiveType.Capsule, new Vector3(0.7f + i * 0.23f, 0.45f, 0.4f),
                    new Vector3(0.2f, 0.35f + i * 0.1f, 0.2f), teal);
        }

        private Transform Person(string name, Transform parent, Vector3 position, Material coat)
        {
            Transform person = Group(name, parent);
            person.localPosition = position;
            var actor = person.gameObject.AddComponent<HorizonActor>();
            Sculpt(person, "Tailored coat", coatMesh, new Vector3(0,0.67f,0), new Vector3(1,1,0.78f), coat);
            Box(person, "Jacket seam", new Vector3(0, 1.05f, -0.218f), new Vector3(0.02f, 0.58f, 0.02f), dark);
            for (int button = 0; button < 3; button++)
                Shape(person, "Jacket button", PrimitiveType.Sphere, new Vector3(0.035f, 0.86f + button * 0.16f, -0.226f), Vector3.one * 0.035f, gold);
            Transform head = Group("Head pivot", person);
            head.localPosition = new Vector3(0, 1.66f, 0);
            actor.Head = head;
            Shape(head, "Face", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.55f, 0.56f, 0.5f), skin);
            Shape(head, "Hair crown", PrimitiveType.Sphere, new Vector3(0, 0.15f, 0.025f), new Vector3(0.57f, 0.34f, 0.52f), hair);
            Shape(head, "Hair fringe", PrimitiveType.Sphere, new Vector3(-0.09f, 0.16f, -0.16f), new Vector3(0.27f, 0.17f, 0.24f), hair);
            Shape(head, "Nose", PrimitiveType.Sphere, new Vector3(0, 0.015f, -0.255f), new Vector3(0.067f, 0.06f, 0.085f), skin);
            Box(head, "Smile", new Vector3(0, -0.093f, -0.239f), new Vector3(0.07f, 0.018f, 0.014f), hair);
            for (int i = -1; i <= 1; i += 2)
            {
                Transform leg = Group(i < 0 ? "Left hip" : "Right hip", person);
                leg.localPosition = new Vector3(i * 0.16f, 0.63f, 0);
                Shape(leg, "Trouser", PrimitiveType.Capsule, new Vector3(0, -0.28f, 0), new Vector3(0.19f, 0.29f, 0.21f), dark);
                Shape(leg, "Shoe", PrimitiveType.Sphere, new Vector3(0, -0.54f, -0.08f), new Vector3(0.23f, 0.16f, 0.36f), dark);
                Transform arm = Group(i < 0 ? "Left shoulder" : "Right shoulder", person);
                arm.localPosition = new Vector3(i * 0.33f, 1.3f, 0);
                Shape(arm, "Sleeve", PrimitiveType.Capsule, new Vector3(i * 0.04f, -0.22f, 0), new Vector3(0.19f, 0.24f, 0.2f), coat);
                Transform hand = Shape(arm, "Hand", PrimitiveType.Sphere, new Vector3(i * 0.04f, -0.47f, 0), Vector3.one * 0.17f, skin);
                Transform eye = Shape(head, "Eye", PrimitiveType.Sphere, new Vector3(i * 0.1f, 0.045f, -0.244f), new Vector3(0.052f, 0.061f, 0.042f), hair);
                Shape(eye, "Eye light", PrimitiveType.Sphere, new Vector3(-0.19f, 0.21f, -0.43f), Vector3.one * 0.24f, gold);
                Shape(head, "Ear", PrimitiveType.Sphere, new Vector3(i * 0.27f, 0, 0), new Vector3(0.08f, 0.12f, 0.07f), skin);
                if (i < 0)
                {
                    actor.LeftArm = arm; actor.LeftLeg = leg; actor.LeftEye = eye;
                    actor.Book = Box(hand, "Held book", new Vector3(0, -0.1f, -0.065f), new Vector3(1.15f, 0.22f, 0.8f), teal).gameObject;
                }
                else
                {
                    actor.RightArm = arm; actor.RightLeg = leg; actor.RightEye = eye;
                    actor.Phone = Box(hand, "Held phone", new Vector3(0, -0.1f, -0.05f), new Vector3(0.6f, 1.1f, 0.12f), pink).gameObject;
                }
            }
            Box(person, "Scarf", new Vector3(0, 1.46f, -0.13f), new Vector3(0.44f, 0.1f, 0.32f), gold);
            Box(person, "Scarf tail", new Vector3(0.19f, 1.24f, -0.23f), new Vector3(0.1f, 0.38f, 0.045f), gold);
            Box(person, "Backpack", new Vector3(0, 1.1f, 0.28f), new Vector3(0.44f, 0.55f, 0.2f), teal);
            Soft(person, "Left collar", new Vector3(-0.12f,1.34f,-0.21f), new Vector3(0.17f,0.16f,0.08f), ivory);
            Soft(person, "Right collar", new Vector3(0.12f,1.34f,-0.21f), new Vector3(0.17f,0.16f,0.08f), ivory);
            actor.SetNeutral();
            return person;
        }

        private void Ring(Transform parent, string name, Vector3 center, float radius, Material material, bool vertical)
        {
            var points = new Vector3[65];
            for (int i = 0; i < points.Length; i++)
            {
                float a = i / 64f * Mathf.PI * 2;
                points[i] = center + (vertical ? new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) :
                    new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a))) * radius;
            }
            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(parent, false);
            var line = go.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = material;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.widthMultiplier = 0.045f;
        }

        private void Burst(Vector3 position, Color color, int count)
        {
            if (preferences.reducedMotion) count = Mathf.Min(count, 4);
            else if (preferences.batterySaver) count = Mathf.Min(count, 16);
            for (int i = 0; i < count; i++)
            {
                Transform spark = Shape(transform, "Stardust", PrimitiveType.Cylinder, position,
                    new Vector3(0.12f, 0.018f, 0.12f), color == Palette.Coral ? pink :
                        color == Palette.Mint ? teal : gold);
                var motion = spark.gameObject.AddComponent<WorldSpark>();
                spark.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                float angle = i * 2.399f;
                motion.Velocity = new Vector3(Mathf.Cos(angle), 1.5f + (i % 5) * 0.22f, Mathf.Sin(angle)) * 2;
            }
        }

        private Transform Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent == null ? transform : parent, false);
            return go.transform;
        }

        private Transform Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        { return softBlock == null ? Shape(parent, name, PrimitiveType.Cube, position, scale, material) :
            Soft(parent, name, position, scale, material); }

        private Transform Shape(Transform parent, string name, PrimitiveType type, Vector3 position,
            Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Dispose(go.GetComponent<Collider>());
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = material.shader.name == "HORIZON/LitColor" ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go.transform;
        }

        private Material Lit(Color color, Color emission = default(Color))
        {
            var material = new Material(Resources.Load<Shader>("HorizonLit"));
            material.SetColor("_Color", color);
            material.SetColor("_Emission", emission);
            material.enableInstancing = true;
            materials.Add(material);
            return material;
        }
        private Material Glow(Color color)
        {
            var material = new Material(Resources.Load<Shader>("HorizonGlow"));
            material.SetColor("_Color", color);
            material.enableInstancing = true;
            materials.Add(material);
            return material;
        }

        private AudioClip Tone(string name, float[] notes, float seconds)
        {
            const int rate = 22050;
            var samples = new float[(int)(rate * seconds)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                int note = Mathf.Min(notes.Length - 1, (int)(t / seconds * notes.Length));
                float local = Mathf.Repeat(t / seconds * notes.Length, 1);
                samples[i] = Mathf.Sin(t * notes[note] * Mathf.PI * 2) * Mathf.Sin(local * Mathf.PI) * 0.35f;
            }
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            RestoreRenderQuality();
            foreach (Material material in materials) Dispose(material);
            foreach (AudioClip sound in sounds) Dispose(sound);
            foreach (Mesh mesh in sculptures) Dispose(mesh);
        }

        private static void Dispose(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }

    public sealed class WorldSpark : MonoBehaviour
    {
        public Vector3 Velocity;
        private float age;
        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            age += dt;
            Velocity += Vector3.down * dt * 4;
            transform.position += Velocity * dt;
            transform.Rotate(120 * dt, 150 * dt, 80 * dt);
            if (age > 0.9f) transform.localScale *= Mathf.Max(0, 1 - dt * 5);
            if (age > 1.5f) Destroy(gameObject);
        }
    }
}
