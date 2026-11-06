using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialUI : MonoBehaviour
{
    private TutorialBinding[] bindings;
    private float autoSkipTimer = 4.0f;

    private void Awake()
    {
        bindings = GetComponentsInChildren<TutorialBinding>();

        // Update bottom text to indicate mobile touch support
        var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in allTexts)
        {
            if (t.text.Contains("INTERACT") || t.text.Contains("CONTINUE"))
            {
                t.text = "TAP SCREEN OR PRESS 'INTERACT' TO PLAY";
            }
        }
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += GameManager_OnGameStateChanged;
        }
        if (GameInput.Instance != null)
        {
            GameInput.Instance.OnPlayerInteract += GameInput_OnPlayerInteract;
        }
        Hide();
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;

        // Dismiss immediately on screen touch or click anywhere
        if (Input.touchCount > 0 || Input.GetMouseButtonDown(0))
        {
            DismissTutorial();
            return;
        }

        // Failsafe auto-skip after 3.5s so mobile players are never blocked
        if (Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld)
        {
            autoSkipTimer -= Time.deltaTime;
            if (autoSkipTimer <= 0f)
            {
                DismissTutorial();
            }
        }
    }

    private void GameInput_OnPlayerInteract(object sender, System.EventArgs e)
    {
        if (gameObject.activeSelf)
        {
            DismissTutorial();
        }
    }

    private void DismissTutorial()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ExitTutorial();
        }
        Hide();
    }

    private void GameManager_OnGameStateChanged(object sender, GameManager.GameStateChangedArg e)
    {
        if (e.newState == GameManager.State.GAME_START_WAITING)
        {
            Show();
        }
        else if (e.lastState == GameManager.State.GAME_START_WAITING)
        {
            Hide();
        }
    }

    private void Show()
    {
        gameObject.SetActive(true);
        autoSkipTimer = 4.0f;
        foreach (TutorialBinding binding in bindings)
        {
            binding.UpdateVisual();
        }
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }
}
