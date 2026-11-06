using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameRunningStatUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timeRemainingText;

    private bool timerRunning = false;
    private Image badgeBackground;
    private Outline badgeOutline;

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

        // Add clean dark rounded badge behind the timer text if not present
        GameObject parentObj = timeRemainingText.gameObject;
        badgeBackground = parentObj.GetComponent<Image>();
        if (badgeBackground == null)
        {
            badgeBackground = parentObj.AddComponent<Image>();
        }
        badgeBackground.color = new Color(0.08f, 0.11f, 0.18f, 0.90f);

        badgeOutline = parentObj.GetComponent<Outline>();
        if (badgeOutline == null)
        {
            badgeOutline = parentObj.AddComponent<Outline>();
        }
        badgeOutline.effectColor = new Color(0.18f, 0.28f, 0.45f, 0.8f);
        badgeOutline.effectDistance = new Vector2(2, -2);

        timeRemainingText.alignment = TextAlignmentOptions.Center;
        timeRemainingText.fontSize = 24;
        timeRemainingText.fontStyle = FontStyles.Bold;
        timeRemainingText.color = Color.white;
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
            timeRemainingText.text = "<color=#ef4444>TIME'S UP</color>";
            if (badgeOutline != null) badgeOutline.effectColor = new Color(0.9f, 0.2f, 0.2f, 0.9f);
            return;
        }

        int minutes = seconds / 60;
        int secondsRemainder = seconds % 60;
        string timeStr = string.Format("{0:00}:{1:00}", minutes, secondsRemainder);

        if (seconds <= 10)
        {
            // Urgency crimson pulse
            float pulse = 0.7f + Mathf.PingPong(Time.time * 5f, 0.3f);
            timeRemainingText.text = $"<color=#ef4444>TIME  {timeStr}</color>";
            if (badgeOutline != null) badgeOutline.effectColor = new Color(0.9f, 0.2f, 0.2f, pulse);
        }
        else if (seconds <= 30)
        {
            timeRemainingText.text = $"<color=#fbbf24>TIME  {timeStr}</color>";
            if (badgeOutline != null) badgeOutline.effectColor = new Color(0.98f, 0.75f, 0.15f, 0.8f);
        }
        else
        {
            timeRemainingText.text = $"<color=#38bdf8>TIME</color>  {timeStr}";
            if (badgeOutline != null) badgeOutline.effectColor = new Color(0.18f, 0.28f, 0.45f, 0.8f);
        }
    }
}
