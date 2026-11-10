using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AuraKitchen;

public class GameRunningStatUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timeRemainingText;

    private bool timerRunning = false;
    private Image badgeBackground;
    private Image badgeRing;
    private static readonly Color TimerBlue = new Color32(0x3F, 0x95, 0xDB, 0xFF);

    private void Start()
    {
        GameManager.Instance.OnGameStateChanged += GameManager_OnGameStateChanged;
        
        SetupTimerVisuals();

        if (timeRemainingText != null)
        {
            timeRemainingText.gameObject.SetActive(false);
        }
    }

    private void SetupTimerVisuals()
    {
        if (timeRemainingText == null) return;

        // Position neatly at Top-Center
        RectTransform rt = timeRemainingText.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -16);
        rt.sizeDelta = new Vector2(230, 72);

        // Apply official game font asset & material
        TMP_FontAsset gameFont = AuraTheme.Font;
        if (gameFont != null)
        {
            timeRemainingText.font = gameFont;
            timeRemainingText.fontSharedMaterial = gameFont.material;
        }

        // Add clean dark rounded badge behind the timer text if not present
        GameObject parentObj = timeRemainingText.gameObject;
        badgeBackground = parentObj.GetComponent<Image>();
        if (badgeBackground == null)
        {
            badgeBackground = parentObj.AddComponent<Image>();
        }
        badgeBackground.sprite = AuraTheme.RoundedFace;
        badgeBackground.type = Image.Type.Sliced;
        badgeBackground.color = TimerBlue;
        AuraTheme.AddHardShadow(badgeBackground.gameObject);

        Transform existingRing = parentObj.transform.Find("Stroke");
        if (existingRing == null)
        {
            GameObject ringObj = new GameObject("Stroke");
            ringObj.transform.SetParent(parentObj.transform, false);
            RectTransform ringRt = ringObj.AddComponent<RectTransform>();
            ringRt.anchorMin = Vector2.zero;
            ringRt.anchorMax = Vector2.one;
            ringRt.offsetMin = Vector2.zero;
            ringRt.offsetMax = Vector2.zero;
            badgeRing = ringObj.AddComponent<Image>();
            badgeRing.sprite = AuraTheme.RoundedRing;
            badgeRing.type = Image.Type.Sliced;
            badgeRing.raycastTarget = false;
        }
        else
        {
            badgeRing = existingRing.GetComponent<Image>();
        }
        badgeRing.color = AuraTheme.Stroke;

        timeRemainingText.alignment = TextAlignmentOptions.Center;
        timeRemainingText.fontSize = 44;
        timeRemainingText.fontStyle = FontStyles.Normal;
        timeRemainingText.color = AuraTheme.Paper;
        AuraTheme.SetOutline(timeRemainingText, true);
        timeRemainingText.enableWordWrapping = false;
    }

    private void GameManager_OnGameStateChanged(object sender, GameManager.GameStateChangedArg e)
    {
        if (e.newState == GameManager.State.GAME_RUNNING && !timerRunning)
        {
            StartCoroutine(StartTimer());
        }
    }

    private IEnumerator StartTimer()
    {
        timerRunning = true;
        if (timeRemainingText != null)
        {
            timeRemainingText.gameObject.SetActive(true);
            timeRemainingText.text = "";
        }

        yield return null;
        lastShownSeconds = int.MinValue;

        while (GameManager.Instance != null && !GameManager.Instance.GameRunningCountdownEnded)
        {
            int countdown = GameManager.Instance.GameRunningCountdownFloored;
            UpdateTimerDisplay(countdown);
            yield return null;
        }

        timerRunning = false;
        if (timeRemainingText != null)
        {
            timeRemainingText.gameObject.SetActive(false);
        }
    }

    private int lastShownSeconds = int.MinValue;

    private void UpdateTimerDisplay(int seconds)
    {
        if (timeRemainingText == null) return;

        // This runs every frame: only touch the UI when the displayed second changes (or while pulsing in the last 10s)
        bool pulsing = seconds > 0 && seconds <= 10;
        if (seconds != lastShownSeconds)
        {
            lastShownSeconds = seconds;
            if (seconds <= 0)
            {
                timeRemainingText.text = "TIME'S UP";
                timeRemainingText.fontSize = 32;
                if (badgeBackground != null) badgeBackground.color = AuraTheme.Danger;
                timeRemainingText.rectTransform.localScale = Vector3.one;
                return;
            }

            timeRemainingText.fontSize = 44;
            timeRemainingText.text = (seconds / 60) + ":" + (seconds % 60).ToString("00");

            Color face = seconds <= 10 ? AuraTheme.Danger : (seconds <= 30 ? AuraTheme.Amber : TimerBlue);
            if (badgeBackground != null) badgeBackground.color = face;
            if (!pulsing) timeRemainingText.rectTransform.localScale = Vector3.one;
        }

        if (pulsing)
        {
            float scale = 1f + 0.07f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
            timeRemainingText.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
