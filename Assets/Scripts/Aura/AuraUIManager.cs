using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AuraKitchen
{
    public class AuraUIManager : MonoBehaviour
    {
        public static AuraUIManager Instance { get; private set; }

        [Header("Header & Branding")]
        [SerializeField] private TextMeshProUGUI gameTitleText;
        [SerializeField] private TextMeshProUGUI shiftTimerText;
        [SerializeField] private Image shiftTimerFill;

        [Header("Economy & Stats")]
        [SerializeField] private TextMeshProUGUI tipsEarnedText;
        [SerializeField] private TextMeshProUGUI comboMultiplierText;
        [SerializeField] private TextMeshProUGUI customerRatingText;

        [Header("Chef Status")]
        [SerializeField] private GameObject chefSwitchButtonObj;
        [SerializeField] private TextMeshProUGUI activeChefLabel;

        [Header("Shift Summary Modal")]
        [SerializeField] private GameObject shiftSummaryPanel;
        [SerializeField] private TextMeshProUGUI dishesServedText;
        [SerializeField] private TextMeshProUGUI happyCustomersText;
        [SerializeField] private TextMeshProUGUI totalTipsText;
        [SerializeField] private TextMeshProUGUI finalRatingText;
        [SerializeField] private TextMeshProUGUI rankTitleText;
        [SerializeField] private Button restartShiftButton;
        [SerializeField] private Button returnToMenuButton;

        private int currentTips = 0;
        private int displayedTips = 0;
        private int comboStreak = 0;
        private float lastDeliveryTime = 0f;
        private const float ComboTimeout = 15f;

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
            if (gameTitleText != null)
            {
                gameTitleText.text = "<color=#00e5ff>AURA</color> <color=#ffaa00>KITCHEN</color>";
            }

            if (shiftSummaryPanel != null)
            {
                shiftSummaryPanel.SetActive(false);
            }

            // Hook into CustomerManager
            if (CustomerManager.Instance != null)
            {
                CustomerManager.Instance.OnTipsUpdated += CustomerManager_OnTipsUpdated;
                CustomerManager.Instance.OnCustomerStatsUpdated += CustomerManager_OnCustomerStatsUpdated;
            }

            // Hook into DeliveryManager
            if (DeliveryManager.Instance != null)
            {
                DeliveryManager.Instance.OnOrderDelivered += DeliveryManager_OnOrderDelivered;
                DeliveryManager.Instance.OnFailedOrderDeliver += DeliveryManager_OnFailedOrderDeliver;
            }

            // Hook into GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += GameManager_OnGameStateChanged;
            }

            // Hook into SoloChefController
            if (SoloChefController.Instance != null)
            {
                SoloChefController.Instance.OnActiveChefChanged += SoloChefController_OnActiveChefChanged;
                UpdateChefLabel(SoloChefController.Instance.ActiveChefIndex);
            }

            if (restartShiftButton != null)
            {
                restartShiftButton.onClick.AddListener(() => SceneSwitcher.LoadScene(SceneSwitcher.Scene.GameScene));
            }

            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.AddListener(() => SceneSwitcher.LoadScene(SceneSwitcher.Scene.MainMenu));
            }
        }

        private void Update()
        {
            // Smoothly animate tip counter
            if (displayedTips < currentTips)
            {
                displayedTips = (int)Mathf.MoveTowards(displayedTips, currentTips, Time.deltaTime * 60f + 1f);
                if (tipsEarnedText != null)
                {
                    tipsEarnedText.text = $"${displayedTips}";
                }
            }

            // Update shift timer
            if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
            {
                int remainingSeconds = GameManager.Instance.GameRunningCountdownFloored;
                if (shiftTimerText != null)
                {
                    int mins = remainingSeconds / 60;
                    int secs = remainingSeconds % 60;
                    shiftTimerText.text = $"{mins:00}:{secs:00}";
                    if (remainingSeconds <= 10)
                    {
                        // Urgency pulse red
                        float pulse = 0.8f + Mathf.PingPong(Time.time * 6f, 0.4f);
                        shiftTimerText.color = new Color(1f, 0.2f * pulse, 0.2f * pulse);
                    }
                }

                // Combo timeout check
                if (comboStreak > 1 && Time.time - lastDeliveryTime > ComboTimeout)
                {
                    ResetCombo();
                }
            }
        }

        private void CustomerManager_OnTipsUpdated(object sender, int totalTips)
        {
            currentTips = totalTips;
        }

        private void CustomerManager_OnCustomerStatsUpdated(object sender, CustomerManager.CustomerServedStatsEventArgs e)
        {
            if (customerRatingText != null)
            {
                customerRatingText.text = $"⭐ {e.AverageRating:0.0}";
            }
        }

        private void DeliveryManager_OnOrderDelivered(object sender, MenuRecipeSO e)
        {
            comboStreak++;
            lastDeliveryTime = Time.time;
            UpdateComboDisplay();
        }

        private void DeliveryManager_OnFailedOrderDeliver(object sender, EventArgs e)
        {
            ResetCombo();
        }

        private void UpdateComboDisplay()
        {
            if (comboMultiplierText == null) return;

            if (comboStreak >= 4)
            {
                comboMultiplierText.text = $"<color=#ff3d00>x4 MAX AURA 🔥</color>";
            }
            else if (comboStreak > 1)
            {
                comboMultiplierText.text = $"<color=#ffaa00>x{comboStreak} STREAK</color>";
            }
            else
            {
                comboMultiplierText.text = "";
            }
        }

        private void ResetCombo()
        {
            comboStreak = 0;
            if (comboMultiplierText != null)
            {
                comboMultiplierText.text = "";
            }
        }

        private void SoloChefController_OnActiveChefChanged(object sender, int activeIndex)
        {
            UpdateChefLabel(activeIndex);
        }

        private void UpdateChefLabel(int activeIndex)
        {
            if (activeChefLabel != null)
            {
                string chefName = activeIndex == 0 ? "Chef Ruby (P1)" : "Chef Sapphire (P2)";
                activeChefLabel.text = $"Controlling: {chefName}";
            }
        }

        private void GameManager_OnGameStateChanged(object sender, GameManager.GameStateChangedArg e)
        {
            if (e.newState == GameManager.State.GAME_END)
            {
                ShowShiftSummary();
            }
        }

        private void ShowShiftSummary()
        {
            if (shiftSummaryPanel == null) return;

            shiftSummaryPanel.SetActive(true);

            int plates = GameManager.Instance != null ? GameManager.Instance.PlateServed : 0;
            int tips = CustomerManager.Instance != null ? CustomerManager.Instance.TotalTips : currentTips;
            int happy = CustomerManager.Instance != null ? CustomerManager.Instance.HappyCount : plates;
            float rating = CustomerManager.Instance != null ? CustomerManager.Instance.AverageStarRating : 5.0f;

            if (dishesServedText != null) dishesServedText.text = $"Dishes Served: {plates}";
            if (happyCustomersText != null) happyCustomersText.text = $"Happy Customers: {happy}";
            if (totalTipsText != null) totalTipsText.text = $"Tips & Revenue: ${tips}";
            if (finalRatingText != null) finalRatingText.text = $"Restaurant Aura: ⭐ {rating:0.0} / 5.0";

            if (rankTitleText != null)
            {
                if (rating >= 4.5f && plates >= 8)
                {
                    rankTitleText.text = "<color=#ffd700>👑 3-STAR MICHELIN AURA MASTER</color>";
                }
                else if (rating >= 3.5f && plates >= 4)
                {
                    rankTitleText.text = "<color=#00e5ff>⭐ EXECUTIVE SOUS-CHEF</color>";
                }
                else
                {
                    rankTitleText.text = "<color=#ffaa00>🍳 TALENTED LINE COOK</color>";
                }
            }
        }
    }
}

