using UnityEngine;
using TMPro;

namespace AuraKitchen
{
    public class PlayerAuraVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MeshRenderer bodyMeshRenderer;
        [SerializeField] private Material[] playerBodyMaterials;
        [SerializeField] private GameObject auraRingObject;
        [SerializeField] private TextMeshPro overheadText;

        [Header("Aura Colors")]
        public static readonly Color[] AuraColors = new Color[]
        {
            new Color(1.0f, 0.25f, 0.25f, 0.8f), // P1: Ruby Red
            new Color(0.2f, 0.6f, 1.0f, 0.8f),   // P2: Sapphire Blue
            new Color(0.2f, 0.85f, 0.4f, 0.8f),  // P3: Emerald Green
            new Color(1.0f, 0.8f, 0.15f, 0.8f)   // P4: Amber Gold
        };

        private int playerIndex = 0;
        private bool isCurrentActive = true;
        private Material auraMaterialInstance;

        public void SetupPlayerVisual(int index, string displayName = "")
        {
            playerIndex = Mathf.Clamp(index, 0, AuraColors.Length - 1);

            // Apply body material
            if (bodyMeshRenderer != null && playerBodyMaterials != null && playerIndex < playerBodyMaterials.Length)
            {
                if (playerBodyMaterials[playerIndex] != null)
                {
                    bodyMeshRenderer.material = playerBodyMaterials[playerIndex];
                }
            }
            else if (bodyMeshRenderer != null)
            {
                bodyMeshRenderer.material.color = AuraColors[playerIndex];
            }

            // Create or configure Aura Ring
            SetupAuraRing();

            // Setup overhead tag
            if (overheadText != null)
            {
                overheadText.text = string.IsNullOrEmpty(displayName) ? $"Chef {playerIndex + 1}" : displayName;
                overheadText.color = AuraColors[playerIndex];
            }
        }

        public void SetChefActive(bool active)
        {
            isCurrentActive = active;
            if (auraRingObject != null)
            {
                auraRingObject.SetActive(active);
            }
            if (overheadText != null)
            {
                overheadText.gameObject.SetActive(active);
            }
        }

        private void SetupAuraRing()
        {
            if (auraRingObject == null)
            {
                // Procedural glowing aura circle at feet
                auraRingObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                auraRingObject.name = "AuraRing";
                auraRingObject.transform.SetParent(transform);
                auraRingObject.transform.localPosition = new Vector3(0, 0.05f, 0);
                auraRingObject.transform.localScale = new Vector3(1.4f, 0.02f, 1.4f);

                // Remove collider
                Collider col = auraRingObject.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            MeshRenderer ringRenderer = auraRingObject.GetComponent<MeshRenderer>();
            if (ringRenderer != null)
            {
                Color c = AuraColors[playerIndex];
                auraMaterialInstance = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                auraMaterialInstance.color = c;
                if (auraMaterialInstance.HasProperty("_EmissionColor"))
                {
                    auraMaterialInstance.EnableKeyword("_EMISSION");
                    auraMaterialInstance.SetColor("_EmissionColor", c * 1.5f);
                }
                ringRenderer.material = auraMaterialInstance;
            }
        }

        private void Update()
        {
            // Face overhead text towards camera
            if (overheadText != null && Camera.main != null)
            {
                overheadText.transform.rotation = Camera.main.transform.rotation;
            }

            // Subtle pulsing of aura ring
            if (auraRingObject != null && isCurrentActive)
            {
                float pulse = 1.35f + Mathf.Sin(Time.time * 4f) * 0.08f;
                auraRingObject.transform.localScale = new Vector3(pulse, 0.02f, pulse);
            }
        }
    }
}

