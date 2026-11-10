using UnityEngine;
using UnityEngine.SceneManagement;

namespace AuraKitchen
{
    public static class AuraGameInitializer
    {
        private static bool isHooked = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnAppStart()
        {
            if (!isHooked)
            {
                isHooked = true;
                SceneManager.sceneLoaded += HandleSceneLoaded;
            }
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InitializeGlobalSystems();

            if (scene.name == SceneSwitcher.Scene.GameScene.ToString())
            {
                InitializeGameScene();
            }
            else if (scene.name == SceneSwitcher.Scene.MainMenu.ToString())
            {
                InitializeMainMenuScene();
            }
        }

        private static void InitializeMainMenuScene()
        {
            GameObject lobbyShowcase = GameObject.Find("Aura_3D_Lobby_Showcase_Host");
            if (lobbyShowcase == null)
            {
                lobbyShowcase = new GameObject("Aura_3D_Lobby_Showcase_Host");
                lobbyShowcase.AddComponent<Aura3DLobbyShowcase>();
            }
        }

        private static void InitializeGlobalSystems()
        {
            GameObject auraRoot = GameObject.Find("Aura_Global_Systems");
            if (auraRoot == null)
            {
                auraRoot = new GameObject("Aura_Global_Systems");
                Object.DontDestroyOnLoad(auraRoot);
            }

            // 2. In-Game & Menu Multiplayer Lobby (Create Room, Join, Host)
            if (Object.FindObjectOfType<AuraMultiplayerLobbyUI>() == null)
            {
                auraRoot.AddComponent<AuraMultiplayerLobbyUI>();
            }

            // 3. Mobile Controls (Persistent Global System)
            if (Object.FindObjectOfType<AuraMobileControls>() == null)
            {
                auraRoot.AddComponent<AuraMobileControls>();
            }
        }

        private static void InitializeGameScene()
        {
            GameObject auraRoot = GameObject.Find("Aura_Game_Systems");
            if (auraRoot == null)
            {
                auraRoot = new GameObject("Aura_Game_Systems");
            }

            // 1. Environment & Atmosphere (Expanded stations, dining tables)
            if (Object.FindObjectOfType<AuraKitchenEnvironment>() == null)
            {
                auraRoot.AddComponent<AuraKitchenEnvironment>();
            }

            // 2. Multiplayer & 4-Player Manager
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

            // 6. Aura Game Mechanics (Combos, Bonus Time, Aura Rush)
            if (Object.FindObjectOfType<AuraGameMechanics>() == null)
            {
                auraRoot.AddComponent<AuraGameMechanics>();
            }

            // 6b. Showtime: cinematics, shift events, customer ratings, coins & upgrades
            if (Object.FindObjectOfType<AuraShowtime>() == null)
            {
                auraRoot.AddComponent<AuraShowtime>();
            }

            // 7. Aura Online Multiplayer Manager (Real-time network sync & challenge scoreboard)
            if (Object.FindObjectOfType<AuraOnlineMultiplayerManager>() == null)
            {
                auraRoot.AddComponent<AuraOnlineMultiplayerManager>();
            }

            // Double check Mobile Controls are active and ready in GameScene
            if (AuraMobileControls.Instance != null)
            {
                AuraMobileControls.Instance.EnsureControlsVisible(true);
            }

            Debug.Log("<color=#00e5ff>[Aura Kitchen]</color> Systems initialized successfully for " + SceneManager.GetActiveScene().name);
        }
    }
}
