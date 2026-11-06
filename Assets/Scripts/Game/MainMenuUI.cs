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

            // Clean, balanced vertical layout under PLAY
            // Base X: 150, Width: 450
            float baseX = startRect.anchoredPosition.x;
            startRect.anchoredPosition = new Vector2(baseX, 390);
            startRect.sizeDelta = new Vector2(450, 110);

            mpRect.anchoredPosition = new Vector2(baseX, 260);
            mpRect.sizeDelta = new Vector2(450, 110);

            quitRect.anchoredPosition = new Vector2(baseX, 140);
            quitRect.sizeDelta = new Vector2(450, 95);

            // Update text to MULTIPLAYER using identical styling
            TextMeshProUGUI tmp = mpObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = "MULTIPLAYER";
            }

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
}
