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
        rt.anchoredPosition = new Vector2(0, -20);
        rt.sizeDelta = new Vector2(240, 52);

        // Apply official game font asset & material
        TMP_FontAsset gameFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
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
        badgeBackground.color = AuraTheme.PanelFill;

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
        timeRemainingText.fontSize = 24;
        timeRemainingText.fontStyle = FontStyles.Bold;
        timeRemainingText.color = AuraTheme.TextPrimary;
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

    private void UpdateTimerDisplay(int seconds)
    {
        if (timeRemainingText == null) return;

        if (seconds <= 0)
        {
            timeRemainingText.text = "<color=" + AuraTheme.Hex(AuraTheme.Danger) + ">TIME'S UP</color>";
            if (badgeRing != null) badgeRing.color = AuraTheme.WithAlpha(AuraTheme.Danger, 0.9f);
            return;
        }

        int minutes = seconds / 60;
        int secondsRemainder = seconds % 60;
        string timeStr = string.Format("{0:00}:{1:00}", minutes, secondsRemainder);

        if (seconds <= 10)
        {
            // Urgency crimson pulse
            float pulse = 0.7f + Mathf.PingPong(Time.time * 5f, 0.3f);
            timeRemainingText.text = "<color=" + AuraTheme.Hex(AuraTheme.Danger) + ">TIME  " + timeStr + "</color>";
            if (badgeRing != null) badgeRing.color = AuraTheme.WithAlpha(AuraTheme.Danger, pulse);
        }
        else if (seconds <= 30)
        {
            timeRemainingText.text = "<color=" + AuraTheme.Hex(AuraTheme.Amber) + ">TIME  " + timeStr + "</color>";
            if (badgeRing != null) badgeRing.color = AuraTheme.WithAlpha(AuraTheme.Amber, 0.85f);
        }
        else
        {
            timeRemainingText.text = "<color=" + AuraTheme.Hex(AuraTheme.Cyan) + ">TIME</color>  " + timeStr;
            if (badgeRing != null) badgeRing.color = AuraTheme.Stroke;
        }
    }
}
