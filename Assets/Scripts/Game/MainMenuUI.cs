using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button gameStartButton;
    [SerializeField] private Button gameQuitButton;
    [SerializeField] private Button gameMultiplayerButton;

    private void Awake()
    {
        if (gameStartButton != null)
        {
            gameStartButton.onClick.AddListener(() =>
            {
                PlayerPrefs.SetInt("Aura_GameMode", 0); // Solo
                SceneSwitcher.LoadScene(SceneSwitcher.Scene.GameScene);
            });
        }

        if (gameQuitButton != null)
        {
            gameQuitButton.onClick.AddListener(() => Application.Quit());
        }

        SetupMultiplayerButton();
    }

    private void SetupMultiplayerButton()
    {
        if (gameMultiplayerButton == null && gameStartButton != null && gameQuitButton != null)
        {
            // Clone the exact same button from GameStartButton
            GameObject mpObj = Instantiate(gameStartButton.gameObject, gameStartButton.transform.parent);
            mpObj.name = "GameMultiplayerButton";
            gameMultiplayerButton = mpObj.GetComponent<Button>();

            RectTransform startRect = gameStartButton.GetComponent<RectTransform>();
            RectTransform quitRect = gameQuitButton.GetComponent<RectTransform>();
            RectTransform mpRect = mpObj.GetComponent<RectTransform>();

            float baseX = startRect.anchoredPosition.x;
            const float btnWidth = 520f;
            const float btnHeight = 95f;

            startRect.anchoredPosition = new Vector2(baseX, 390);
            startRect.sizeDelta = new Vector2(btnWidth, btnHeight);

            mpRect.anchoredPosition = new Vector2(baseX, 265);
            mpRect.sizeDelta = new Vector2(btnWidth, btnHeight);

            quitRect.anchoredPosition = new Vector2(baseX, 140);
            quitRect.sizeDelta = new Vector2(btnWidth, btnHeight);

            // Update text to MULTIPLAYER using identical styling and NO wrapping
            TextMeshProUGUI tmp = mpObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = "MULTIPLAYER";
                ConfigureButtonText(tmp);
            }

            ConfigureButtonText(gameStartButton.GetComponentInChildren<TextMeshProUGUI>());
            ConfigureButtonText(gameQuitButton.GetComponentInChildren<TextMeshProUGUI>());

            gameMultiplayerButton.onClick.RemoveAllListeners();
            gameMultiplayerButton.onClick.AddListener(() =>
            {
                if (AuraKitchen.AuraMultiplayerLobbyUI.Instance != null)
                {
                    AuraKitchen.AuraMultiplayerLobbyUI.Instance.ShowLobby();
                }
            });
        }
    }

    private void ConfigureButtonText(TextMeshProUGUI t)
    {
        if (t == null) return;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.enableAutoSizing = true;
        t.fontSizeMin = 18;
        t.fontSizeMax = 44;
        t.alignment = TextAlignmentOptions.Center;
        RectTransform tr = t.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(10, 0);
        tr.offsetMax = new Vector2(-10, 0);
    }
}
