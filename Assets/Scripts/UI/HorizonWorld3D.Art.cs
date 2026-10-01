using System.Collections;
using System.Collections.Generic;
using Horizon.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private Mesh softBlock, leafMesh, coatMesh;
        private readonly List<Mesh> sculptures = new List<Mesh>();
        private Material ivory, wood, foliage, warmLight, portalLight, contactShadow;
        private Transform receivingHalo;
        private HorizonBloom bloom;
        private Color sceneAccent = Palette.Mint;
        private MaterialPropertyBlock aimProperties;

        private void InitializeArt()
        {
            aimProperties = new MaterialPropertyBlock();
            softBlock = Own(HorizonSculpt.SoftBlock());
            leafMesh = Own(HorizonSculpt.Leaf());
            coatMesh = Own(HorizonSculpt.Profile("Traveller tailored coat", new[] {
                new Vector2(0,0), new Vector2(0.27f,0), new Vector2(0.3f,0.12f),
                new Vector2(0.24f,0.42f), new Vector2(0.31f,0.57f), new Vector2(0.23f,0.67f), new Vector2(0,0.67f) }));
            ivory = Lit(new Color(0.82f, 0.85f, 0.73f));
            wood = Lit(new Color(0.3f, 0.16f, 0.13f));
            foliage = Lit(new Color(0.16f, 0.48f, 0.36f));
            warmLight = Glow(new Color(1.9f, 1.2f, 0.5f, 0.8f));
            portalLight = Glow(new Color(0.55f, 1.9f, 1.3f, 0.68f));
            contactShadow = new Material(Resources.Load<Shader>("HorizonContact"));
            materials.Add(contactShadow);
            var sky = new Material(Resources.Load<Shader>("HorizonAtmosphere"));
            materials.Add(sky); RenderSettings.skybox = sky;
            WorldCamera.clearFlags = CameraClearFlags.Skybox;
            WorldCamera.allowHDR = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
            WorldCamera.fieldOfView = 38;
            bloom = WorldCamera.gameObject.AddComponent<HorizonBloom>();
            bloom.Initialize(Resources.Load<Shader>("HorizonBloom"));
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.25f, 0.34f, 0.45f);
            RenderSettings.ambientEquatorColor = new Color(0.18f, 0.24f, 0.27f);
            RenderSettings.ambientGroundColor = new Color(0.055f, 0.08f, 0.12f);
            QualitySettings.pixelLightCount = 2;
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            key.intensity = 1.35f;
            Directional("Blue fill", new Vector3(25, 125, 0), new Color(0.34f, 0.68f, 0.9f), 0.45f);
            Directional("Amber rim", new Vector3(15, 165, 0), new Color(1, 0.65f, 0.35f), 0.65f);
        }

        private Mesh Own(Mesh mesh) { sculptures.Add(mesh); return mesh; }

        private void Directional(string name, Vector3 rotation, Color color, float intensity)
        {
            Light light = new GameObject(name, typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(transform, false); light.type = LightType.Directional;
            light.color = color; light.intensity = intensity;
            light.transform.rotation = Quaternion.Euler(rotation);
        }

        private Transform Sculpt(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale, Material material)
        {
            Transform item = Group(name, parent); item.localPosition = position; item.localScale = scale;
            item.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = item.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = material == contactShadow ? ShadowCastingMode.Off : ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return item;
        }

        private Transform Soft(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        { return Sculpt(parent, name, softBlock, position, scale, material); }

        private void GroundContact(Transform parent, Vector3 position, Vector2 size)
        {
            Transform shadow = Shape(parent, "Ground contact", PrimitiveType.Quad,
                position + Vector3.up * 0.035f, new Vector3(size.x, size.y, 1), contactShadow);
            shadow.localRotation = Quaternion.Euler(90, 0, 0);
        }

        private void DressPlatform()
        {
            // Sculpted edge, steps, warm inlays and organic forms make scale and depth legible.
            Mesh pedestal = Own(HorizonSculpt.Profile("Terraced island", new[] {
                new Vector2(0,-0.49f), new Vector2(4.7f,-0.49f), new Vector2(5.4f,-0.18f),
                new Vector2(5.25f,-0.035f), new Vector2(0,-0.035f) }, 64));
            Sculpt(environment, "Island bevel", pedestal, new Vector3(0,-0.06f,1), Vector3.one, floor);
            Transform rim = Sculpt(transform, "Solid horizon rim", Own(HorizonSculpt.Torus(2.05f, 0.075f)),
                new Vector3(0,2.25f,6.02f), Vector3.one, teal);
            rim.gameObject.AddComponent<PortalBreath>();
            Transform inner = Sculpt(transform, "Breathing future light", Own(HorizonSculpt.Torus(1.9f, 0.03f)),
                new Vector3(0,2.25f,5.99f), Vector3.one, portalLight);
            inner.gameObject.AddComponent<PortalBreath>().Rate = 0.7f;
            for (int i = 0; i < 3; i++)
                Plant(environment, new Vector3(-3.8f + i * 3.8f,0,4.4f + i % 2), 0.7f + i * 0.1f);
            GroundContact(transform, new Vector3(-0.7f,0,0), new Vector2(1.4f,1));
            receivingHalo = Group("Responsive receiving light");
            Ring(receivingHalo, "Aim ring", new Vector3(0,0.06f,1), 1.15f, portalLight, false);
            receivingHalo.gameObject.SetActive(false);
        }

        private void Plant(Transform parent, Vector3 position, float size)
        {
            Transform plant = Group("Sculpted garden", parent); plant.localPosition = position; plant.localScale = Vector3.one * size;
            Mesh pot = Own(HorizonSculpt.Profile("Ceramic planter", new[] {
                new Vector2(0,0), new Vector2(0.25f,0), new Vector2(0.36f,0.4f),
                new Vector2(0.38f,0.46f), new Vector2(0.3f,0.46f), new Vector2(0.27f,0.37f), new Vector2(0,0.37f) }, 24));
            Sculpt(plant, "Ceramic pot", pot, Vector3.zero, Vector3.one, ivory);
            for (int i = 0; i < 9; i++)
            {
                Transform leaf = Sculpt(plant, "Curved living leaf", leafMesh, new Vector3(0,0.4f,0),
                    new Vector3(0.6f,0.8f + i % 3 * 0.18f,0.6f), i % 2 == 0 ? foliage : teal);
                leaf.localRotation = Quaternion.Euler(22 + i % 3 * 12, i * 137.5f, 0);
            }
        }

        private void DressProps()
        {
            props[0].localPosition = new Vector3(-2.25f,0,1.1f);
            Soft(props[0], "Phone pedestal", new Vector3(0,0.18f,0), new Vector3(1.3f,0.36f,1), floor);
            GroundContact(props[0], Vector3.zero, new Vector2(1.5f,1.2f));
            props[1].localPosition = new Vector3(1.35f,0,0.65f);
            Transform lamp = Group("Reading lamp", props[1]);
            Soft(lamp, "Lamp foot", new Vector3(0.48f,0.99f,0.17f), new Vector3(0.25f,0.08f,0.25f), gold);
            Shape(lamp, "Lamp stem", PrimitiveType.Cylinder, new Vector3(0.48f,1.28f,0.17f), new Vector3(0.045f,0.3f,0.045f), gold);
            Mesh shade = Own(HorizonSculpt.Profile("Reading light shade", new[] {
                new Vector2(0,0), new Vector2(0.24f,0), new Vector2(0.12f,0.28f), new Vector2(0,0.28f) }, 24));
            Sculpt(lamp, "Lamp shade", shade, new Vector3(0.48f,1.51f,0.17f), Vector3.one, gold);
            Shape(lamp, "Lamp glow", PrimitiveType.Sphere, new Vector3(0.48f,1.53f,0.17f), Vector3.one * 0.14f, warmLight);
            Light bulb = new GameObject("Pool of reading light", typeof(Light)).GetComponent<Light>();
            bulb.transform.SetParent(lamp,false); bulb.transform.localPosition = new Vector3(0.48f,1.5f,0.17f);
            bulb.type = LightType.Point; bulb.range = 3.5f; bulb.intensity = 0.8f; bulb.color = new Color(1,0.66f,0.33f);
            Soft(props[1], "Writing page", new Vector3(-0.1f,0.94f,-0.25f), new Vector3(0.55f,0.018f,0.26f), ivory);
            Soft(props[1], "Pencil", new Vector3(-0.22f,0.96f,-0.22f), new Vector3(0.31f,0.014f,0.018f), gold);
            Plant(props[1], new Vector3(0.85f,0,-0.15f), 0.52f);
            GroundContact(props[1], Vector3.zero, new Vector2(2,1.5f));
            props[2].localPosition = new Vector3(1.6f,0,1.2f);
            Plant(props[2], new Vector3(-0.82f,0,0.1f), 0.75f);
            Soft(props[2], "Rest cushion", new Vector3(0,0.64f,-0.2f), new Vector3(1.2f,0.17f,0.55f), teal);
            GroundContact(props[2], Vector3.zero, new Vector2(1.9f,1.4f));
            Soft(bench, "Long warm seat", new Vector3(0,0.66f,4.78f), new Vector3(2.95f,0.15f,0.58f), wood);
            for (int i = 0; i < 3; i++)
                Soft(bench, "Bench back slat", new Vector3(0,0.88f + i * 0.16f,5.05f), new Vector3(2.85f,0.11f,0.12f), wood);
        }

        public void Aim(bool enabled, bool ready, CardKind kind)
        {
            if (receivingHalo == null) return;
            receivingHalo.gameObject.SetActive(enabled);
            receivingHalo.localScale = Vector3.one * (ready ? 1.12f : 1);
            sceneAccent = kind == CardKind.Temptation ? Palette.Coral : kind == CardKind.Growth ? Palette.Mint : Palette.Gold;
            aimProperties.SetColor("_Color",new Color(sceneAccent.r * 1.5f,sceneAccent.g * 1.5f,sceneAccent.b * 1.5f,0.68f));
            receivingHalo.GetComponentInChildren<LineRenderer>(true).SetPropertyBlock(aimProperties);
            if (bloom != null) bloom.Intensity = ready ? 0.75f : 0.42f;
        }

        public void TargetReady()
        {
            audioSource.pitch = 1.3f; audioSource.PlayOneShot(sounds[0], 0.35f);
        }

        private void Trail(Transform token, Material material)
        {
            var trail = token.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material; trail.time = 0.3f;
            trail.startWidth = 0.09f; trail.endWidth = 0;
            trail.minVertexDistance = 0.03f; trail.shadowCastingMode = ShadowCastingMode.Off;
        }

        private Transform IntentionSymbol(CardKind kind, Vector3 position, bool support = false, bool returned = false)
        {
            Transform token = Group("A choice with a recognizable shape");
            token.position = position;
            if (support)
            {
                for (int i = -1; i <= 1; i += 2)
                {
                    Shape(token, "Companion head", PrimitiveType.Sphere, new Vector3(i * 0.3f,0.3f,0),
                        Vector3.one * 0.35f, gold);
                    Shape(token, "Companion coat", PrimitiveType.Capsule, new Vector3(i * 0.3f,-0.15f,0),
                        new Vector3(0.38f,0.28f,0.32f), teal);
                }
                Soft(token,"Connection",Vector3.zero,new Vector3(0.7f,0.06f,0.08f),gold);
            }
            else if (kind == CardKind.Growth && returned)
            {
                for (int i = 0; i < 3; i++)
                {
                    Transform leaf = Sculpt(token,"Effort grown into a sprout",leafMesh,Vector3.zero,
                        new Vector3(1,0.8f,1), i % 2 == 0 ? teal : gold);
                    leaf.localRotation = Quaternion.Euler(20,i * 120,0);
                }
            }
            else if (kind == CardKind.Growth)
            {
                Soft(token,"Book cover",Vector3.zero,new Vector3(0.95f,0.19f,0.63f),teal);
                Soft(token,"Book pages",new Vector3(0,0.02f,-0.03f),new Vector3(0.85f,0.11f,0.6f),ivory);
                token.rotation = Quaternion.Euler(-55,20,0);
            }
            else if (kind == CardKind.Temptation)
            {
                Soft(token,"Phone",Vector3.zero,new Vector3(0.58f,0.98f,0.13f),dark);
                Soft(token,"Phone screen",new Vector3(0,0,-0.08f),new Vector3(0.46f,0.81f,0.03f),pink);
                Soft(token,"Light in the feed",new Vector3(0,0.16f,-0.105f),new Vector3(0.28f,0.05f,0.015f),gold);
            }
            else
            {
                Shape(token,"Moon",PrimitiveType.Sphere,Vector3.zero,Vector3.one * 0.7f,gold);
                Transform leaf = Sculpt(token,"A quiet breath",leafMesh,new Vector3(-0.2f,-0.3f,-0.2f),
                    Vector3.one * 0.55f,teal);
                leaf.localRotation = Quaternion.Euler(0,0,-35);
            }
            token.localScale = Vector3.one * 0.35f;
            return token;
        }

        private void WorldRipple(Vector3 position, Material material, float size = 1)
        {
            Transform ripple = Group("Expanding echo wave"); ripple.position = position;
            Ring(ripple, "Echo shockwave", Vector3.zero, size, material, false);
            ripple.gameObject.AddComponent<WorldRipple>();
        }

        private void BuildTimelineLinks(List<ActionRecord> actions)
        {
            if (actions == null) return;
            foreach (ActionRecord action in actions)
            {
                if (action.echoDay <= 0 || action.echoDay > 12) continue;
                var line = new GameObject("D" + action.day + " leaves a future at D" + action.echoDay, typeof(LineRenderer)).GetComponent<LineRenderer>();
                line.transform.SetParent(timelineGroup, false); line.useWorldSpace = false;
                line.sharedMaterial = action.kind == CardKind.Temptation ? pink : portalLight;
                line.widthMultiplier = action.echoed ? 0.022f : 0.011f; line.positionCount = 24;
                var points = new Vector3[24];
                for (int i = 0; i < points.Length; i++)
                {
                    float t = i / 23f;
                    points[i] = Vector3.Lerp(DayPoint(action.day), DayPoint(action.echoDay), t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.5f;
                }
                line.SetPositions(points);
            }
        }
    }

    public sealed class PortalBreath : MonoBehaviour
    {
        public float Rate = 0.45f;
        private void Update() { transform.localScale = Vector3.one * (1 + Mathf.Sin(Time.unscaledTime * Rate) * 0.014f); }
    }

    public sealed class WorldRipple : MonoBehaviour
    {
        private float age;
        private void Update()
        {
            age += Time.unscaledDeltaTime;
            transform.localScale = Vector3.one * (0.2f + age * 2.8f);
            if (age > 0.65f) Destroy(gameObject);
        }
    }
}
