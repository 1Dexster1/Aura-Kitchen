using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AuraKitchen
{
    public class AuraGameMechanics : MonoBehaviour
    {
        public static AuraGameMechanics Instance { get; private set; }

        public static bool IsAuraRushActive { get; private set; } = false;
        public static float SpeedMultiplier => (IsAuraRushActive ? 1.32f : 1.0f) * AuraShowtime.SpeedBonus * AuraUpgrades.SpeedMult;
        public static int ScoreMultiplier => (IsAuraRushActive ? 2 : 1) * AuraShowtime.ScoreBonusMult;

        public int CurrentScore { get; private set; } = 0;
        public int ComboStreak { get; private set; } = 0;
        public int MaxComboStreak { get; private set; } = 0;
        public float AuraRushTimeRemaining { get; private set; } = 0f;

        private const float ComboTimeout = 20f;
        private const float AuraRushDuration = 15f;
        private float lastDeliveryTime = 0f;

        // UI Overlay for Hype & Aura Energy
        private GameObject mechanicsCanvasObj;
        private TextMeshProUGUI floatingHypeText;
        private CanvasGroup floatingHypeGroup;
        private RectTransform floatingHypeRect;

        private GameObject auraRushBannerObj;
        private TextMeshProUGUI auraRushBannerText;

        private GameObject scoreCardObj;
        private TextMeshProUGUI scoreCardText;

        private Coroutine hypeCoroutine;

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
            if (DeliveryManager.Instance != null)
            {
                DeliveryManager.Instance.OnOrderDelivered += OnOrderDelivered;
                DeliveryManager.Instance.OnFailedOrderDeliver += OnOrderFailed;
            }

            CreateMechanicsUI();
        }

        private void OnDestroy()
        {
            if (DeliveryManager.Instance != null)
            {
                DeliveryManager.Instance.OnOrderDelivered -= OnOrderDelivered;
                DeliveryManager.Instance.OnFailedOrderDeliver -= OnOrderFailed;
            }
            IsAuraRushActive = false;
        }

        private void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

            // Handle Aura Rush Timer
            if (IsAuraRushActive)
            {
                AuraRushTimeRemaining -= Time.deltaTime;
                if (AuraRushTimeRemaining <= 0f)
                {
                    EndAuraRush();
                }
                else if (auraRushBannerText != null)
                {
                    float pulse = 0.85f + Mathf.PingPong(Time.time * 6f, 0.3f);
                    auraRushBannerText.color = new Color(1f, 0.85f * pulse, 0.2f * pulse);
                    auraRushBannerText.text = $"AURA RUSH   2X SPEED & POINTS   ({Mathf.CeilToInt(AuraRushTimeRemaining)}s)";
                }
            }

            // Combo Timeout check
            if (ComboStreak > 0 && Time.time - lastDeliveryTime > ComboTimeout)
            {
                ResetCombo();
            }
        }

        private void OnOrderDelivered(object sender, MenuRecipeSO recipe)
        {
            ComboStreak++;
            if (ComboStreak > MaxComboStreak)
            {
                MaxComboStreak = ComboStreak;
            }
            lastDeliveryTime = Time.time;

            // Base points + Combo bonus
            int basePoints = 100;
            int comboBonus = (ComboStreak - 1) * 75;
            int earned = (basePoints + comboBonus) * ScoreMultiplier;
            CurrentScore += earned;

            // Add Bonus Time: +12s per delivery (+15s if in Aura Rush)
            float bonusSec = IsAuraRushActive ? 15f : 12f;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddBonusTime(bonusSec);
            }

            // Trigger Aura Rush on reaching 4 streak!
            if (ComboStreak >= 4 && !IsAuraRushActive)
            {
                ActivateAuraRush();
            }

            // Hype Banner Announcement
            string hypeMsg;
            Color hypeCol;
            if (IsAuraRushActive)
            {
                hypeMsg = $"FEVER DELIVERY  +{earned} PTS (+{bonusSec:0}s)";
                hypeCol = AuraTheme.Gold;
            }
            else if (ComboStreak >= 3)
            {
                hypeMsg = $"COMBO x{ComboStreak}  +{earned} PTS (+{bonusSec:0}s)";
                hypeCol = AuraTheme.Cyan;
            }
            else if (ComboStreak == 2)
            {
                hypeMsg = $"GOOD STREAK  +{earned} PTS (+{bonusSec:0}s)";
                hypeCol = AuraTheme.Success;
            }
            else
            {
                hypeMsg = $"PERFECT DISH! +{earned} PTS (+{bonusSec:0}s)";
                hypeCol = AuraTheme.Paper;
            }

            ShowHypeText(hypeMsg, hypeCol);
            UpdateScoreDisplay();
        }

        /// <summary>Bonus score from shift events (VIP guests etc.).</summary>
        public void AddBonusScore(int points)
        {
            CurrentScore += points;
            ShowHypeText("+" + points + " BONUS!", AuraTheme.Gold);
            UpdateScoreDisplay();
        }

        private void OnOrderFailed(object sender, EventArgs e)
        {
            ResetCombo();
            ShowHypeText("ORDER FAILED! STREAK LOST", AuraTheme.Danger);
        }

        private void ActivateAuraRush()
        {
            IsAuraRushActive = true;
            AuraRushTimeRemaining = AuraRushDuration;

            if (auraRushBannerObj != null)
            {
                auraRushBannerObj.SetActive(true);
            }

            ShowHypeText("AURA RUSH ACTIVATED!", AuraTheme.Gold);
        }

        private void EndAuraRush()
        {
            IsAuraRushActive = false;
            AuraRushTimeRemaining = 0f;

            if (auraRushBannerObj != null)
            {
                auraRushBannerObj.SetActive(false);
            }
        }

        private void ResetCombo()
        {
            ComboStreak = 0;
            UpdateScoreDisplay();
        }

        private void UpdateScoreDisplay()
        {
            if (scoreCardText != null)
            {
                string comboStr = ComboStreak > 1 ? $" <color={AuraTheme.Hex(AuraTheme.Danger)}>(x{ComboStreak})</color>" : "";
                scoreCardText.text = $"AURA: <b>{CurrentScore:N0}</b>{comboStr}";
            }
        }

        private void ShowHypeText(string msg, Color col)
        {
            if (floatingHypeText == null || floatingHypeGroup == null) return;

            if (hypeCoroutine != null) StopCoroutine(hypeCoroutine);
            hypeCoroutine = StartCoroutine(AnimateHypeText(msg, col));
        }

        private IEnumerator AnimateHypeText(string msg, Color col)
        {
            floatingHypeText.text = msg;
            floatingHypeText.color = col;
            floatingHypeGroup.alpha = 1f;

            if (floatingHypeRect != null)
            {
                floatingHypeRect.anchoredPosition = new Vector2(0, -190);
            }

            float duration = 2.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                if (floatingHypeRect != null)
                {
                    floatingHypeRect.anchoredPosition = new Vector2(0, -190 + progress * 25f);
                }

                if (progress > 0.65f)
                {
                    floatingHypeGroup.alpha = Mathf.Lerp(1f, 0f, (progress - 0.65f) / 0.35f);
                }

                yield return null;
            }

            floatingHypeGroup.alpha = 0f;
        }

        private void CreateMechanicsUI()
        {
            mechanicsCanvasObj = new GameObject("Aura_Mechanics_Canvas");
            Canvas canvas = mechanicsCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;

            CanvasScaler scaler = mechanicsCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            mechanicsCanvasObj.AddComponent<GraphicRaycaster>();

            // 1. Floating Hype Banner (Top-Center under timer)
            GameObject hypeObj = new GameObject("FloatingHypeBanner");
            hypeObj.transform.SetParent(mechanicsCanvasObj.transform, false);
            floatingHypeRect = hypeObj.AddComponent<RectTransform>();
            floatingHypeRect.anchorMin = new Vector2(0.5f, 1f);
            floatingHypeRect.anchorMax = new Vector2(0.5f, 1f);
            floatingHypeRect.pivot = new Vector2(0.5f, 1f);
            floatingHypeRect.anchoredPosition = new Vector2(0, -190);
            floatingHypeRect.sizeDelta = new Vector2(900, 60);

            floatingHypeGroup = hypeObj.AddComponent<CanvasGroup>();
            floatingHypeGroup.alpha = 0f;
            floatingHypeGroup.blocksRaycasts = false;

            floatingHypeText = hypeObj.AddComponent<TextMeshProUGUI>();
            if (AuraTheme.Font != null) floatingHypeText.font = AuraTheme.Font;
            floatingHypeText.fontSize = 40;
            floatingHypeText.fontStyle = FontStyles.Normal;
            floatingHypeText.outlineWidth = 0.3f;
            floatingHypeText.outlineColor = (Color32)AuraTheme.Ink;
            floatingHypeText.enableWordWrapping = false;
            floatingHypeText.alignment = TextAlignmentOptions.Center;
            floatingHypeText.raycastTarget = false;

            // 2. Persistent Aura Rush Top Banner
            auraRushBannerObj = AuraTheme.CreatePanel(mechanicsCanvasObj.transform, "AuraRushBanner",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -96), new Vector2(760, 52),
                new Color32(0xE8, 0x39, 0x2D, 0xFF), AuraTheme.Ink, false);
            auraRushBannerObj.GetComponent<Image>().raycastTarget = false;
            AuraTheme.CreateIcon(auraRushBannerObj.transform, AuraIcons.Get(AuraIcon.Flame), new Vector2(-350, 0), 32f, AuraTheme.Gold);
            AuraTheme.CreateIcon(auraRushBannerObj.transform, AuraIcons.Get(AuraIcon.Flame), new Vector2(350, 0), 32f, AuraTheme.Gold);

            GameObject arTxtObj = new GameObject("Text");
            arTxtObj.transform.SetParent(auraRushBannerObj.transform, false);
            RectTransform artRect = arTxtObj.AddComponent<RectTransform>();
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.sizeDelta = Vector2.zero;

            auraRushBannerText = arTxtObj.AddComponent<TextMeshProUGUI>();
            if (AuraTheme.Font != null) auraRushBannerText.font = AuraTheme.Font;
            auraRushBannerText.fontSize = 28;
            auraRushBannerText.fontStyle = FontStyles.Normal;
            auraRushBannerText.outlineWidth = 0.3f;
            auraRushBannerText.outlineColor = (Color32)AuraTheme.Ink;
            auraRushBannerText.alignment = TextAlignmentOptions.Center;
            auraRushBannerText.color = AuraTheme.Gold;
            auraRushBannerText.raycastTarget = false;
            auraRushBannerObj.SetActive(false);

            // 3. Compact Score Card (Top-Right)
            scoreCardObj = AuraTheme.CreatePanel(mechanicsCanvasObj.transform, "ScoreCard",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-28, -20), new Vector2(260, 48), AuraTheme.PanelFill, AuraTheme.Stroke);
            scoreCardObj.GetComponent<Image>().raycastTarget = false;
            AuraTheme.CreateIcon(scoreCardObj.transform, AuraIcons.Get(AuraIcon.Star), new Vector2(-104, 0), 24f, AuraTheme.Gold);

            GameObject scTxtObj = new GameObject("Text");
            scTxtObj.transform.SetParent(scoreCardObj.transform, false);
            RectTransform sctRect = scTxtObj.AddComponent<RectTransform>();
            sctRect.anchorMin = Vector2.zero;
            sctRect.anchorMax = Vector2.one;
            sctRect.offsetMin = new Vector2(34f, 0f);
            sctRect.offsetMax = new Vector2(-8f, 0f);

            scoreCardText = scTxtObj.AddComponent<TextMeshProUGUI>();
            if (AuraTheme.Font != null) scoreCardText.font = AuraTheme.Font;
            scoreCardText.fontSize = 26;
            scoreCardText.alignment = TextAlignmentOptions.Center;
            scoreCardText.color = AuraTheme.TextPrimary;
            scoreCardText.raycastTarget = false;

            UpdateScoreDisplay();
        }
    }
}
