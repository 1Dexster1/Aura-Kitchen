using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AuraKitchen
{
    [RequireComponent(typeof(Customer))]
    public class CustomerVisual : MonoBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private MeshRenderer bodyRenderer;
        [SerializeField] private Transform characterModel;
        [SerializeField] private Canvas overheadCanvas;
        [SerializeField] private Image recipeIconImage;
        [SerializeField] private Image patienceBarFill;
        [SerializeField] private TextMeshProUGUI moodEmojiText;
        [SerializeField] private GameObject tipFloatingTextPrefab;

        [Header("Patience Colors")]
        [SerializeField] private Color patienceColorHigh = new Color(0.2f, 0.9f, 0.4f, 1f); // Mint Emerald
        [SerializeField] private Color patienceColorMed = new Color(1f, 0.75f, 0.1f, 1f);  // Amber Gold
        [SerializeField] private Color patienceColorLow = new Color(0.95f, 0.25f, 0.25f, 1f); // Neon Crimson

        [Header("Customer Variety")]
        [SerializeField] private Color[] customerColorPalette = new Color[]
        {
            new Color(0.95f, 0.4f, 0.4f), // Pastel Coral
            new Color(0.4f, 0.7f, 0.95f), // Sky Blue
            new Color(0.95f, 0.8f, 0.3f), // Warm Golden
            new Color(0.6f, 0.85f, 0.5f), // Sage Green
            new Color(0.8f, 0.5f, 0.9f)  // Lilac Purple
        };

        private Customer customer;
        private float bounceTimer = 0f;
        private Vector3 initialScale;

        private void Awake()
        {
            customer = GetComponent<Customer>();
            if (characterModel != null)
            {
                initialScale = characterModel.localScale;
            }
            else
            {
                initialScale = transform.localScale;
            }
        }

        private void Start()
        {
            customer.OnPatienceRatioChanged += Customer_OnPatienceRatioChanged;
            customer.OnCustomerServed += Customer_OnCustomerServed;
            customer.OnCustomerExpired += Customer_OnCustomerExpired;

            ApplyRandomStyling();

            if (customer.RequestedRecipe != null && recipeIconImage != null)
            {
                if (customer.RequestedRecipe.ingredients != null && customer.RequestedRecipe.ingredients.Count > 0)
                {
                    for (int i = customer.RequestedRecipe.ingredients.Count - 1; i >= 0; i--)
                    {
                        if (customer.RequestedRecipe.ingredients[i] != null && customer.RequestedRecipe.ingredients[i].icon != null)
                        {
                            recipeIconImage.sprite = customer.RequestedRecipe.ingredients[i].icon;
                            recipeIconImage.enabled = true;
                            break;
                        }
                    }
                }
            }

            UpdateMoodEmoji(1f);
        }

        private void OnDestroy()
        {
            if (customer != null)
            {
                customer.OnPatienceRatioChanged -= Customer_OnPatienceRatioChanged;
                customer.OnCustomerServed -= Customer_OnCustomerServed;
                customer.OnCustomerExpired -= Customer_OnCustomerExpired;
            }
        }

        private void Update()
        {
            // Face overhead canvas to camera
            if (overheadCanvas != null && Camera.main != null)
            {
                overheadCanvas.transform.rotation = Camera.main.transform.rotation;
            }

            // Procedural animation according to state
            AnimateCharacter();
        }

        private void AnimateCharacter()
        {
            Transform targetTransform = characterModel != null ? characterModel : transform;

            switch (customer.State)
            {
                case Customer.CustomerState.Entering:
                case Customer.CustomerState.LeavingHappy:
                    // Happy waddle
                    bounceTimer += Time.deltaTime * 10f;
                    float waddleY = Mathf.Abs(Mathf.Sin(bounceTimer)) * 0.15f;
                    float tiltZ = Mathf.Sin(bounceTimer * 0.5f) * 6f;
                    targetTransform.localPosition = new Vector3(0, waddleY, 0);
                    targetTransform.localRotation = Quaternion.Euler(0, 0, tiltZ);
                    break;

                case Customer.CustomerState.Eating:
                    // Joyful eating bounce
                    bounceTimer += Time.deltaTime * 14f;
                    float eatScaleY = initialScale.y * (1f + Mathf.Sin(bounceTimer) * 0.12f);
                    targetTransform.localScale = new Vector3(initialScale.x, eatScaleY, initialScale.z);
                    break;

                case Customer.CustomerState.LeavingAngry:
                    // Stomp and smoke
                    bounceTimer += Time.deltaTime * 16f;
                    float stompTilt = Mathf.Sin(bounceTimer) * 8f;
                    targetTransform.localRotation = Quaternion.Euler(0, 0, stompTilt);
                    break;

                case Customer.CustomerState.WaitingAtTable:
                    // Idle breathing
                    bounceTimer += Time.deltaTime * 2.5f;
                    targetTransform.localPosition = new Vector3(0, Mathf.Sin(bounceTimer) * 0.04f, 0);
                    break;
            }
        }

        private void Customer_OnPatienceRatioChanged(object sender, float ratio)
        {
            if (patienceBarFill != null)
            {
                patienceBarFill.fillAmount = ratio;
                if (ratio > 0.6f)
                {
                    patienceBarFill.color = patienceColorHigh;
                }
                else if (ratio > 0.3f)
                {
                    patienceBarFill.color = patienceColorMed;
                }
                else
                {
                    // Pulse low patience red
                    float pulse = 0.7f + Mathf.PingPong(Time.time * 4f, 0.3f);
                    patienceBarFill.color = new Color(patienceColorLow.r * pulse, patienceColorLow.g * pulse, patienceColorLow.b * pulse, 1f);
                }
            }

            UpdateMoodEmoji(ratio);
        }

        private void UpdateMoodEmoji(float patienceRatio)
        {
            if (moodEmojiText == null) return;

            if (customer.State == Customer.CustomerState.Eating)
            {
                moodEmojiText.text = "DELICIOUS";
                moodEmojiText.color = new Color(1f, 0.85f, 0.2f);
            }
            else if (customer.State == Customer.CustomerState.LeavingHappy)
            {
                moodEmojiText.text = "PERFECT +TIP";
                moodEmojiText.color = new Color(0.2f, 1f, 0.4f);
            }
            else if (customer.State == Customer.CustomerState.LeavingAngry)
            {
                moodEmojiText.text = "ANGRY -NO TIP";
                moodEmojiText.color = new Color(1f, 0.3f, 0.3f);
            }
            else if (patienceRatio > 0.65f)
            {
                moodEmojiText.text = "PATIENT";
                moodEmojiText.color = new Color(0.4f, 1f, 0.5f);
            }
            else if (patienceRatio > 0.30f)
            {
                moodEmojiText.text = "WAITING";
                moodEmojiText.color = new Color(1f, 0.75f, 0.2f);
            }
            else
            {
                moodEmojiText.text = "URGENT!";
                moodEmojiText.color = new Color(1f, 0.25f, 0.25f);
            }
        }

        private void Customer_OnCustomerServed(object sender, Customer.CustomerServedEventArgs e)
        {
            UpdateMoodEmoji(1f);
            if (overheadCanvas != null)
            {
                // Hide patience bar, show celebration
                if (patienceBarFill != null && patienceBarFill.transform.parent != null)
                {
                    patienceBarFill.transform.parent.gameObject.SetActive(false);
                }
            }
        }

        private void Customer_OnCustomerExpired(object sender, EventArgs e)
        {
            UpdateMoodEmoji(0f);
        }

        private void ApplyRandomStyling()
        {
            if (bodyRenderer != null && customerColorPalette != null && customerColorPalette.Length > 0)
            {
                Color chosenColor = customerColorPalette[UnityEngine.Random.Range(0, customerColorPalette.Length)];
                // Clone material instance to change tint without altering asset
                bodyRenderer.material.color = chosenColor;
            }
        }
    }
}
