using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AuraKitchen
{
    public class AuraMainMenuUI : MonoBehaviour
    {
        [Header("Menu Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject multiplayerPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject chefCustomizerPanel;

        [Header("Main Buttons")]
        [SerializeField] private Button soloPlayButton;
        [SerializeField] private Button onlineCoopButton;
        [SerializeField] private Button localCoopButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Multiplayer Room Controls")]
        [SerializeField] private Button hostRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private TMP_InputField roomCodeInput;
        [SerializeField] private TextMeshProUGUI roomStatusText;
        [SerializeField] private Button backFromMultiplayerButton;

        [Header("Settings Controls")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle touchControlsToggle;
        [SerializeField] private Button backFromSettingsButton;

        private void Start()
        {
            // Main navigation
            if (soloPlayButton != null)
            {
                soloPlayButton.onClick.AddListener(() =>
                {
                    PlayerPrefs.SetInt("Aura_GameMode", (int)MultiplayerManager.Mode.SoloSwitch);
                    SceneSwitcher.LoadScene(SceneSwitcher.Scene.GameScene);
                });
            }

            if (onlineCoopButton != null)
            {
                onlineCoopButton.onClick.AddListener(() => OpenPanel(multiplayerPanel));
            }

            if (localCoopButton != null)
            {
                localCoopButton.onClick.AddListener(() =>
                {
                    PlayerPrefs.SetInt("Aura_GameMode", (int)MultiplayerManager.Mode.LocalCoop);
                    SceneSwitcher.LoadScene(SceneSwitcher.Scene.GameScene);
                });
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(() => OpenPanel(settingsPanel));
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(() => Application.Quit());
            }

            // Multiplayer Panel
            if (hostRoomButton != null)
            {
                hostRoomButton.onClick.AddListener(OnHostRoomClicked);
            }

            if (joinRoomButton != null)
            {
                joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
            }

            if (backFromMultiplayerButton != null)
            {
                backFromMultiplayerButton.onClick.AddListener(() => OpenPanel(mainPanel));
            }

            // Settings Panel
            if (backFromSettingsButton != null)
            {
                backFromSettingsButton.onClick.AddListener(() => OpenPanel(mainPanel));
            }

            if (musicSlider != null)
            {
                musicSlider.value = PlayerPrefs.GetFloat("Aura_MusicVolume", 0.8f);
                musicSlider.onValueChanged.AddListener(v => PlayerPrefs.SetFloat("Aura_MusicVolume", v));
            }

            if (sfxSlider != null)
            {
                sfxSlider.value = PlayerPrefs.GetFloat("Aura_SFXVolume", 1.0f);
                sfxSlider.onValueChanged.AddListener(v => PlayerPrefs.SetFloat("Aura_SFXVolume", v));
            }

            if (touchControlsToggle != null)
            {
                touchControlsToggle.isOn = PlayerPrefs.GetInt("Aura_TouchControls", 1) == 1;
                touchControlsToggle.onValueChanged.AddListener(v => PlayerPrefs.SetInt("Aura_TouchControls", v ? 1 : 0));
            }

            OpenPanel(mainPanel);
        }

        private void OpenPanel(GameObject targetPanel)
        {
            if (mainPanel != null) mainPanel.SetActive(mainPanel == targetPanel);
            if (multiplayerPanel != null) multiplayerPanel.SetActive(multiplayerPanel == targetPanel);
            if (settingsPanel != null) settingsPanel.SetActive(settingsPanel == targetPanel);
            if (chefCustomizerPanel != null) chefCustomizerPanel.SetActive(chefCustomizerPanel == targetPanel);
        }

        private void OnHostRoomClicked()
        {
            PlayerPrefs.SetInt("Aura_GameMode", (int)MultiplayerManager.Mode.OnlineCoop);
            PlayerPrefs.SetInt("Aura_IsHost", 1);
            SceneSwitcher.LoadScene(SceneSwitcher.Scene.GameScene);
        }

        private void OnJoinRoomClicked()
        {
            string code = roomCodeInput != null ? roomCodeInput.text.Trim().ToUpper() : "AURA";
            if (string.IsNullOrEmpty(code))
            {
                if (roomStatusText != null) roomStatusText.text = "<color=red>Please enter a 4-letter room code!</color>";
                return;
            }

            PlayerPrefs.SetInt("Aura_GameMode", (int)MultiplayerManager.Mode.OnlineCoop);
            PlayerPrefs.SetInt("Aura_IsHost", 0);
            PlayerPrefs.SetString("Aura_RoomCode", code);

            if (roomStatusText != null) roomStatusText.text = $"<color=green>Connecting to room {code}...</color>";
            SceneSwitcher.LoadScene(SceneSwitcher.Scene.GameScene);
        }
    }
}

