using System;
using UnityEngine;
using TMPro;

namespace AuraKitchen
{
    public class PlayerAuraVisual : MonoBehaviour
    {
        [Header("Aura Colors")]
        public static readonly Color[] AuraColors = AuraTheme.ChefColors;

        private int playerIndex = 0;
        private string chefDisplayName = "";
        private TextMeshPro overheadText;

        public void SetupPlayerVisual(int index, string displayName = "")
        {
            playerIndex = Mathf.Clamp(index, 0, AuraColors.Length - 1);
            chefDisplayName = displayName;

            ApplyChefColor(AuraColors[playerIndex]);
            EnsureOverheadTag();
        }

        private void ApplyChefColor(Color color)
        {
            // Find all renderers in children belonging to the duck body and head
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                if (r == null || r.sharedMaterial == null) continue;

                if (r.gameObject.name == "Head" || r.gameObject.name == "Body" ||
                    r.sharedMaterial.name.Contains("PlayerBody"))
                {
                    Material instancedMat = new Material(r.sharedMaterial);
                    instancedMat.color = color;
                    if (instancedMat.HasProperty("_BaseColor"))
                    {
                        instancedMat.SetColor("_BaseColor", color);
                    }
                    r.material = instancedMat;
                }
            }
        }

        private void EnsureOverheadTag()
        {
            if (overheadText == null)
            {
                // Look for existing tag
                overheadText = GetComponentInChildren<TextMeshPro>();

                if (overheadText == null)
                {
                    GameObject tagObj = new GameObject("Overhead_Tag");
                    tagObj.transform.SetParent(transform, false);
                    tagObj.transform.localPosition = new Vector3(0, 2.35f, 0);

                    overheadText = tagObj.AddComponent<TextMeshPro>();
                    overheadText.alignment = TextAlignmentOptions.Center;
                    overheadText.fontSize = 5.2f;
                    overheadText.fontStyle = FontStyles.Bold;
                    overheadText.outlineWidth = 0.28f;
                    overheadText.outlineColor = new Color32(0, 0, 0, 255);
                    overheadText.rectTransform.sizeDelta = new Vector2(8f, 1.8f);
                }
            }

            if (overheadText != null)
            {
                string label = string.IsNullOrEmpty(chefDisplayName) ? $"Chef {playerIndex + 1}" : chefDisplayName;
                overheadText.text = label;
                overheadText.color = AuraColors[playerIndex];
            }
        }

        public void UpdateDisplayName(string newName)
        {
            chefDisplayName = newName;
            if (overheadText != null)
            {
                overheadText.text = newName;
            }
        }

        public void SetChefActive(bool active)
        {
            if (overheadText != null)
            {
                overheadText.gameObject.SetActive(active);
            }
        }

        private void LateUpdate()
        {
            // Face camera directly
            if (overheadText != null && Camera.main != null)
            {
                overheadText.transform.rotation = Camera.main.transform.rotation;
            }
        }
    }
}
