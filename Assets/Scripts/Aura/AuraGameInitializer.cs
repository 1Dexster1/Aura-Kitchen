using UnityEngine;
using UnityEngine.SceneManagement;

namespace AuraKitchen
{
    public static class AuraGameInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoaded()
        {
            string sceneName = SceneManager.GetActiveScene().name;

            if (sceneName == SceneSwitcher.Scene.GameScene.ToString())
            {
                InitializeGameScene();
            }
        }

        private static void InitializeGameScene()
        {
            GameObject auraRoot = GameObject.Find("Aura_Systems");
            if (auraRoot == null)
            {
                auraRoot = new GameObject("Aura_Systems");
                Object.DontDestroyOnLoad(auraRoot);
            }

            // 1. Environment & Atmosphere
            if (Object.FindObjectOfType<AuraKitchenEnvironment>() == null)
            {
                auraRoot.AddComponent<AuraKitchenEnvironment>();
            }

            // 2. Customer System
            if (Object.FindObjectOfType<CustomerManager>() == null)
            {
                auraRoot.AddComponent<CustomerManager>();
            }

            // 3. Multiplayer & 4-Player Manager
            if (Object.FindObjectOfType<MultiplayerManager>() == null)
            {
                var mp = auraRoot.AddComponent<MultiplayerManager>();
                int mode = PlayerPrefs.GetInt("Aura_GameMode", (int)MultiplayerManager.Mode.SoloSwitch);
                mp.SetGameMode((MultiplayerManager.Mode)mode);
            }

            // 4. Solo Chef Switcher & Assistant
            if (Object.FindObjectOfType<SoloChefController>() == null)
            {
                auraRoot.AddComponent<SoloChefController>();
            }

            // 5. Aura Modern UI Overhaul
            if (Object.FindObjectOfType<AuraUIManager>() == null)
            {
                auraRoot.AddComponent<AuraUIManager>();
            }

            // 6. Mobile & Touch Controls
            if (Object.FindObjectOfType<AuraMobileControls>() == null)
            {
                auraRoot.AddComponent<AuraMobileControls>();
            }

            Debug.Log("<color=#00e5ff>[Aura Kitchen]</color> Systems initialized successfully for " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }
}

