using System.Collections.Generic;
using UnityEngine;

namespace AuraKitchen
{
    public class AuraKitchenEnvironment : MonoBehaviour
    {
        public static AuraKitchenEnvironment Instance { get; private set; }

        [Header("Prefabs for 4-Player Station Duplication")]
        [SerializeField] private GameObject cuttingCounterPrefab;
        [SerializeField] private GameObject stoveCounterPrefab;
        [SerializeField] private GameObject platesCounterPrefab;
        [SerializeField] private GameObject clearCounterPrefab;

        [Header("Atmosphere Settings")]
        [SerializeField] private bool enhanceLightingAndDecor = true;
        [SerializeField] private Color auraWarmLightColor = new Color(1f, 0.92f, 0.78f, 1f);
        [SerializeField] private Color neonAccentColor = new Color(0.1f, 0.9f, 0.85f, 1f); // Neon Mint/Cyan

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
            if (enhanceLightingAndDecor)
            {
                SetupCustomerDiningArea();
                SetupAtmosphericPendantLights();
                SetupRestaurantDecor();
                EnsureFourPlayerWorkstations();
            }
        }

        private void SetupCustomerDiningArea()
        {
            GameObject diningArea = new GameObject("Aura_DiningAndTables");
            diningArea.transform.SetParent(transform);

            // Create 4 customer dining tables positioned along the front
            for (int i = 0; i < 4; i++)
            {
                float posX = -3.5f + (i * 2.3f);
                Vector3 tablePos = new Vector3(posX, 0f, -5.2f);

                // Table
                GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                table.name = $"DiningTable_{i + 1}";
                table.transform.SetParent(diningArea.transform);
                table.transform.position = tablePos + new Vector3(0, 0.45f, 0);
                table.transform.localScale = new Vector3(1.1f, 0.45f, 1.1f);

                // Warm wood material tint
                MeshRenderer mr = table.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.material.color = new Color(0.42f, 0.28f, 0.18f); // Warm Mahogany wood
                }

                // Tablecloth top circle
                GameObject cloth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cloth.name = "TableCloth";
                cloth.transform.SetParent(table.transform);
                cloth.transform.localPosition = new Vector3(0, 0.51f, 0);
                cloth.transform.localScale = new Vector3(0.9f, 0.02f, 0.9f);
                MeshRenderer clothMr = cloth.GetComponent<MeshRenderer>();
                if (clothMr != null)
                {
                    clothMr.material.color = new Color(0.96f, 0.94f, 0.90f); // Warm linen
                }

                // Candle / table lamp
                GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                lamp.name = "TableCandle";
                lamp.transform.SetParent(table.transform);
                lamp.transform.localPosition = new Vector3(0, 0.65f, 0);
                lamp.transform.localScale = new Vector3(0.12f, 0.15f, 0.12f);
                MeshRenderer lampMr = lamp.GetComponent<MeshRenderer>();
                if (lampMr != null) lampMr.material.color = new Color(1f, 0.85f, 0.4f);

                // Point light for cozy dinner candle aura
                GameObject candleLight = new GameObject("CandleLight");
                candleLight.transform.SetParent(lamp.transform);
                candleLight.transform.localPosition = new Vector3(0, 0.2f, 0);
                Light lt = candleLight.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = new Color(1f, 0.75f, 0.35f);
                lt.range = 2.5f;
                lt.intensity = 1.2f;

                // 2 Dining Chairs
                CreateChair(diningArea.transform, tablePos + new Vector3(0, 0, -0.65f), 0f);
                CreateChair(diningArea.transform, tablePos + new Vector3(0, 0, 0.65f), 180f);
            }
        }

        private void CreateChair(Transform parent, Vector3 position, float rotationY)
        {
            GameObject chair = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chair.name = "DiningChair";
            chair.transform.SetParent(parent);
            chair.transform.position = position + new Vector3(0, 0.3f, 0);
            chair.transform.rotation = Quaternion.Euler(0, rotationY, 0);
            chair.transform.localScale = new Vector3(0.45f, 0.3f, 0.45f);

            MeshRenderer mr = chair.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = new Color(0.3f, 0.2f, 0.15f);

            // Chair Backrest
            GameObject backrest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backrest.name = "Backrest";
            backrest.transform.SetParent(chair.transform);
            backrest.transform.localPosition = new Vector3(0, 0.8f, -0.4f);
            backrest.transform.localScale = new Vector3(0.95f, 0.7f, 0.15f);
            MeshRenderer backMr = backrest.GetComponent<MeshRenderer>();
            if (backMr != null) backMr.material.color = new Color(0.3f, 0.2f, 0.15f);
        }

        private void SetupAtmosphericPendantLights()
        {
            GameObject lightsParent = new GameObject("Aura_PendantLights");
            lightsParent.transform.SetParent(transform);

            // Warm hanging lights placed above kitchen islands
            Vector3[] lightPositions = new Vector3[]
            {
                new Vector3(-2.5f, 3.8f, 1.5f), // Above Cutting/Prep
                new Vector3(2.5f, 3.8f, 1.5f),  // Above Stoves
                new Vector3(0f, 3.8f, -0.5f),   // Center Kitchen
                new Vector3(0f, 3.5f, -4.5f)    // Service & Dining
            };

            foreach (var pos in lightPositions)
            {
                // Lamp shade
                GameObject shade = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                shade.name = "PendantShade";
                shade.transform.SetParent(lightsParent.transform);
                shade.transform.position = pos;
                shade.transform.localScale = new Vector3(0.6f, 0.2f, 0.6f);
                MeshRenderer mr = shade.GetComponent<MeshRenderer>();
                if (mr != null) mr.material.color = new Color(0.15f, 0.15f, 0.15f); // Matte Black Metal

                // Light source
                GameObject lightSource = new GameObject("WarmLight");
                lightSource.transform.SetParent(shade.transform);
                lightSource.transform.localPosition = new Vector3(0, -0.2f, 0);
                Light lt = lightSource.AddComponent<Light>();
                lt.type = LightType.Spot;
                lt.spotAngle = 75f;
                lt.color = auraWarmLightColor;
                lt.range = 7f;
                lt.intensity = 2.2f;
            }
        }

        private void SetupRestaurantDecor()
        {
            GameObject decorParent = new GameObject("Aura_RestaurantDecor");
            decorParent.transform.SetParent(transform);

            // 1. Neon Restaurant Sign on the back wall (Z = 5.5)
            GameObject neonSign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            neonSign.name = "NeonAuraSign";
            neonSign.transform.SetParent(decorParent.transform);
            neonSign.transform.position = new Vector3(0, 3.2f, 4.8f);
            neonSign.transform.localScale = new Vector3(3.5f, 0.7f, 0.1f);

            MeshRenderer mr = neonSign.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material.color = new Color(0.05f, 0.05f, 0.08f);
            }

            // Neon glowing accent strip
            GameObject neonStrip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            neonStrip.name = "NeonGlowStrip";
            neonStrip.transform.SetParent(neonSign.transform);
            neonStrip.transform.localPosition = new Vector3(0, -0.45f, -0.05f);
            neonStrip.transform.localScale = new Vector3(0.98f, 0.08f, 0.2f);
            MeshRenderer stripMr = neonStrip.GetComponent<MeshRenderer>();
            if (stripMr != null)
            {
                stripMr.material.color = neonAccentColor;
            }

            // 2. Decorative Indoor Plants in kitchen corners
            CreatePottedPlant(decorParent.transform, new Vector3(-5.5f, 0, 4.5f));
            CreatePottedPlant(decorParent.transform, new Vector3(5.5f, 0, 4.5f));
            CreatePottedPlant(decorParent.transform, new Vector3(-5.5f, 0, -4.5f));
            CreatePottedPlant(decorParent.transform, new Vector3(5.5f, 0, -4.5f));
        }

        private void CreatePottedPlant(Transform parent, Vector3 position)
        {
            GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pot.name = "PlantPot";
            pot.transform.SetParent(parent);
            pot.transform.position = position + new Vector3(0, 0.35f, 0);
            pot.transform.localScale = new Vector3(0.5f, 0.35f, 0.5f);
            MeshRenderer potMr = pot.GetComponent<MeshRenderer>();
            if (potMr != null) potMr.material.color = new Color(0.85f, 0.85f, 0.82f); // Ceramic white

            // Foliage sphere
            GameObject foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            foliage.name = "Foliage";
            foliage.transform.SetParent(pot.transform);
            foliage.transform.localPosition = new Vector3(0, 1.1f, 0);
            foliage.transform.localScale = new Vector3(1.3f, 1.4f, 1.3f);
            MeshRenderer foliageMr = foliage.GetComponent<MeshRenderer>();
            if (foliageMr != null) foliageMr.material.color = new Color(0.18f, 0.55f, 0.25f); // Fresh Monstera Green
        }

        private void EnsureFourPlayerWorkstations()
        {
            // Dynamically check how many cutting counters and stoves exist in the scene.
            // If less than 3, instantiate additional ones so all 4 players have immediate stations!
            var existingCutting = FindObjectsOfType<CuttingCounter>();
            if (existingCutting.Length < 3 && cuttingCounterPrefab != null)
            {
                // Place additional cutting stations
                Instantiate(cuttingCounterPrefab, new Vector3(-3.5f, 0, 2f), Quaternion.Euler(0, 180, 0));
            }

            var existingStoves = FindObjectsOfType<StoveCounter>();
            if (existingStoves.Length < 3 && stoveCounterPrefab != null)
            {
                // Place additional cooking stations
                Instantiate(stoveCounterPrefab, new Vector3(3.5f, 0, 2f), Quaternion.Euler(0, 180, 0));
            }
        }
    }
}

