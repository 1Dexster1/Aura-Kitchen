using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AuraKitchen
{
    /// <summary>
    /// Atmosphere layer on top of the modeled Kitchen Chaos scene. It never places solid props:
    /// everything is derived from the scene's own bounds, so it can't clip into the existing geometry.
    ///  - warm accent lights with a gentle flicker
    ///  - floating dust motes in the light
    ///  - steam wisps rising over the cooking area
    ///  - soft screen vignette
    /// </summary>
    public class AuraKitchenEnvironment : MonoBehaviour
    {
        public static AuraKitchenEnvironment Instance { get; private set; }

        private readonly List<Light> flickerLights = new List<Light>();
        private readonly List<float> flickerBase = new List<float>();
        private readonly List<float> flickerSeed = new List<float>();
        private Bounds sceneBounds;
        private GameObject root;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            root = new GameObject("Aura_Atmosphere");
            sceneBounds = MeasureScene();

            BuildLights();
            BuildDust();
            BuildSteam();
            BuildVignette();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (root != null) Destroy(root);
        }

        private void Update()
        {
            for (int i = 0; i < flickerLights.Count; i++)
            {
                if (flickerLights[i] == null) continue;
                float n = Mathf.PerlinNoise(Time.time * 0.8f, flickerSeed[i]);
                flickerLights[i].intensity = flickerBase[i] * (0.86f + n * 0.28f);
            }
        }

        private Bounds MeasureScene()
        {
            bool has = false;
            Bounds b = new Bounds(Vector3.zero, new Vector3(16f, 4f, 12f));
            Renderer[] rs = FindObjectsOfType<Renderer>();
            for (int i = 0; i < rs.Length; i++)
            {
                Renderer r = rs[i];
                if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
                if (r.GetComponentInParent<Canvas>() != null) continue;
                Vector3 s = r.bounds.size;
                if (s.x > 60f || s.z > 60f) continue; // skip skyboxes / huge floors that would distort the area
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private Vector3 RandomPointInFloorplan(float y)
        {
            return new Vector3(
                Random.Range(sceneBounds.min.x * 0.8f + sceneBounds.center.x * 0.2f, sceneBounds.max.x * 0.8f + sceneBounds.center.x * 0.2f),
                y,
                Random.Range(sceneBounds.min.z * 0.8f + sceneBounds.center.z * 0.2f, sceneBounds.max.z * 0.8f + sceneBounds.center.z * 0.2f));
        }

        // ---------- Lights ----------
        private void BuildLights()
        {
            float y = sceneBounds.max.y - 0.4f;
            Color warm = new Color(1f, 0.78f, 0.52f);
            Color cool = AuraTheme.Cyan;

            Vector3 c = sceneBounds.center;
            Vector3 e = sceneBounds.extents;
            Vector3[] spots =
            {
                new Vector3(c.x - e.x * 0.5f, y, c.z - e.z * 0.4f),
                new Vector3(c.x + e.x * 0.5f, y, c.z - e.z * 0.4f),
                new Vector3(c.x - e.x * 0.5f, y, c.z + e.z * 0.4f),
                new Vector3(c.x + e.x * 0.5f, y, c.z + e.z * 0.4f),
            };

            for (int i = 0; i < spots.Length; i++)
            {
                GameObject go = new GameObject("AccentLight_" + i);
                go.transform.SetParent(root.transform, false);
                go.transform.position = spots[i];
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = (i % 2 == 0) ? warm : Color.Lerp(warm, cool, 0.35f);
                l.range = Mathf.Max(5f, e.x * 0.9f);
                l.intensity = 0.55f;
                l.shadows = LightShadows.None;
                flickerLights.Add(l);
                flickerBase.Add(l.intensity);
                flickerSeed.Add(Random.value * 100f);
            }
        }

        // ---------- Particles ----------
        private Material particleMaterial;

        private Material ParticleMaterial()
        {
            if (particleMaterial != null) return particleMaterial;
            Shader sh = Shader.Find("Sprites/Default");
            particleMaterial = new Material(sh);
            particleMaterial.mainTexture = AuraTheme.Dot.texture;
            return particleMaterial;
        }

        private ParticleSystem MakeSystem(string name, Vector3 pos, Vector3 boxSize)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = pos;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = boxSize;

            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = ParticleMaterial();
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            return ps;
        }

        private void BuildDust()
        {
            Vector3 c = sceneBounds.center;
            Vector3 size = new Vector3(sceneBounds.size.x * 0.9f, Mathf.Max(1.5f, sceneBounds.size.y * 0.6f), sceneBounds.size.z * 0.9f);
            ParticleSystem ps = MakeSystem("DustMotes", new Vector3(c.x, sceneBounds.min.y + size.y * 0.6f, c.z), size);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new Color(1f, 0.93f, 0.8f, 0.45f);
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 14f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            vel.y = new ParticleSystem.MinMaxCurve(0.02f, 0.1f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.3f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;

            ps.Play();
        }

        private void BuildSteam()
        {
            // Three soft wisps spread over the floorplan, waist-high, drifting up and fading out
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = RandomPointInFloorplan(sceneBounds.min.y + 1.1f);
                ParticleSystem ps = MakeSystem("Steam_" + i, p, new Vector3(0.5f, 0.05f, 0.5f));

                var main = ps.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
                main.startColor = new Color(1f, 1f, 1f, 0.12f);
                main.maxParticles = 40;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = -0.02f;

                var emission = ps.emission;
                emission.rateOverTime = 7f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 8f;
                shape.radius = 0.15f;
                shape.rotation = new Vector3(-90f, 0f, 0f);

                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.5f), new Keyframe(1f, 1.6f)));

                var fade = ps.colorOverLifetime;
                fade.enabled = true;
                Gradient g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
                fade.color = g;

                ps.Play();
            }
        }

        // ---------- Vignette ----------
        private void BuildVignette()
        {
            GameObject canvasObj = new GameObject("Aura_Vignette_Canvas");
            canvasObj.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -50; // behind every HUD canvas

            RectTransform rt = AuraTheme.MakeRect(canvasObj.transform, "Vignette");
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            RawImage img = rt.gameObject.AddComponent<RawImage>();
            img.texture = MakeVignetteTexture();
            img.color = new Color(0f, 0f, 0f, 0.55f);
            img.raycastTarget = false;
        }

        private static Texture2D MakeVignetteTexture()
        {
            const int n = 128;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x / (n - 1f)) * 2f - 1f;
                    float dy = (y / (n - 1f)) * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
