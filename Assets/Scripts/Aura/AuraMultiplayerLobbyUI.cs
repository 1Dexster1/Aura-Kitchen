using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;

namespace AuraKitchen
{
    public class AuraMultiplayerLobbyUI : MonoBehaviour
    {
        public static AuraMultiplayerLobbyUI Instance { get; private set; }

        [Header("Backend")]
        [SerializeField] private string serverBaseUrl = "http://panel.sharebotdex.xyz:30015";

        private GameObject lobbyRootCanvas;
        private GameObject mainModalCard;
        private GameObject topCornerLobbyBtn;

        // Modal Views
        private GameObject hostViewObj;
        private GameObject joinViewObj;
        private GameObject guestWaitingViewObj;
        private GameObject soloViewObj;

        // Tabs
        private Button tabHostBtn;
        private Button tabJoinBtn;
        private Button tabSoloBtn;

        // Host View Elements
        private TextMeshProUGUI hostRoomCodeText;
        private TextMeshProUGUI hostStatusText;
        private TextMeshProUGUI[] hostSlotTexts = new TextMeshProUGUI[4];
        private Button hostStartGameBtn;

        // Join View Elements
        private TMP_InputField joinCodeInput;
        private TextMeshProUGUI joinStatusText;
        private Button joinSubmitBtn;

        // Guest Waiting View Elements
        private TextMeshProUGUI guestRoomCodeText;
        private TextMeshProUGUI guestRoleText;
        private TextMeshProUGUI guestWaitingStatusText;
        private Button guestLeaveBtn;

        // Global status & state
        private TextMeshProUGUI globalStatusText;
        private string activeRoomCode = "AUR4";
        private int mySlotIndex = 0;
        private bool isHost = false;
        private bool isWaitingInRoom = false;
        private Coroutine roomPollCoroutine;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            CreateLobbyProceduralUI();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Only show top-right floating button when inside the GameScene, not MainMenu
            bool isGameScene = scene.name == SceneSwitcher.Scene.GameScene.ToString();
            if (topCornerLobbyBtn != null)
            {
                topCornerLobbyBtn.SetActive(isGameScene);
            }

            if (isGameScene)
            {
                HideLobby();
            }
        }

        private void Start()
        {
            HideLobby();
        }

        public void ShowLobby()
        {
            if (lobbyRootCanvas != null)
            {
                lobbyRootCanvas.SetActive(true);
            }
            SwitchTab("host");
        }

        public void HideLobby()
        {
            if (lobbyRootCanvas != null)
            {
                lobbyRootCanvas.SetActive(false);
            }
        }

        private void CreateLobbyProceduralUI()
        {
            lobbyRootCanvas = new GameObject("Aura_MultiplayerLobby_Canvas");
            DontDestroyOnLoad(lobbyRootCanvas);

            Canvas canvas = lobbyRootCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            CanvasScaler scaler = lobbyRootCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            lobbyRootCanvas.AddComponent<GraphicRaycaster>();

            // 1. Subtle In-Game Corner Badge Button (Only active in GameScene)
            topCornerLobbyBtn = new GameObject("Btn_OpenLobby_Top");
            topCornerLobbyBtn.transform.SetParent(lobbyRootCanvas.transform, false);
            RectTransform topBtnRect = topCornerLobbyBtn.AddComponent<RectTransform>();
            topBtnRect.anchorMin = new Vector2(1, 1);
            topBtnRect.anchorMax = new Vector2(1, 1);
            topBtnRect.pivot = new Vector2(1, 1);
            topBtnRect.anchoredPosition = new Vector2(-20, -18);
            topBtnRect.sizeDelta = new Vector2(180, 42);

            Image topBtnImg = topCornerLobbyBtn.AddComponent<Image>();
            topBtnImg.color = new Color(0.10f, 0.14f, 0.22f, 0.90f);

            Outline topBtnOutline = topCornerLobbyBtn.AddComponent<Outline>();
            topBtnOutline.effectColor = new Color(0.25f, 0.35f, 0.50f, 0.8f);

            Button topBtn = topCornerLobbyBtn.AddComponent<Button>();
            topBtn.onClick.AddListener(ShowLobby);

            TextMeshProUGUI tbTmp = CreateTMPText(topCornerLobbyBtn, "ROOM LOBBY", 14, FontStyles.Bold,
                Vector2.zero, Vector2.zero, new Color(0.85f, 0.92f, 1f));
            tbTmp.rectTransform.anchorMin = Vector2.zero;
            tbTmp.rectTransform.anchorMax = Vector2.one;
            tbTmp.rectTransform.sizeDelta = Vector2.zero;
            topCornerLobbyBtn.SetActive(false);

            // 2. Dim Dark Backdrop
            GameObject backdropObj = new GameObject("Modal_Backdrop");
            backdropObj.transform.SetParent(lobbyRootCanvas.transform, false);
            RectTransform bdRect = backdropObj.AddComponent<RectTransform>();
            bdRect.anchorMin = Vector2.zero;
            bdRect.anchorMax = Vector2.one;
            bdRect.sizeDelta = Vector2.zero;

            Image bdImg = backdropObj.AddComponent<Image>();
            bdImg.color = new Color(0.04f, 0.06f, 0.10f, 0.92f);

            // 3. Modal Card
            mainModalCard = new GameObject("Modal_Card");
            mainModalCard.transform.SetParent(backdropObj.transform, false);
            RectTransform cardRect = mainModalCard.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(780, 560);

            Image cardImg = mainModalCard.AddComponent<Image>();
            cardImg.color = new Color(0.09f, 0.12f, 0.18f, 0.98f);

            Outline cardOutline = mainModalCard.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.20f, 0.28f, 0.42f, 0.9f);
            cardOutline.effectDistance = new Vector2(2, -2);

            // Card Header
            CreateTMPText(mainModalCard, "AURA KITCHEN - MULTIPLAYER", 22, FontStyles.Bold,
                new Vector2(0, 240), new Vector2(680, 36), new Color(0.95f, 0.97f, 1f));

            CreateTMPText(mainModalCard, "Server: panel.sharebotdex.xyz:30015", 12, FontStyles.Normal,
                new Vector2(0, 208), new Vector2(600, 22), new Color(0.45f, 0.75f, 0.65f));

            // Close Button (X)
            CreateStyledButton(mainModalCard, "Btn_Close", "X", new Vector2(350, 240), new Vector2(40, 40),
                new Color(0.25f, 0.12f, 0.15f, 0.95f), new Color(0.85f, 0.35f, 0.4f), HideLobby);

            // Navigation Tabs
            tabHostBtn = CreateStyledButton(mainModalCard, "Tab_Host", "HOST ROOM", new Vector2(-230, 160), new Vector2(210, 46),
                new Color(0.18f, 0.35f, 0.60f), Color.white, () => SwitchTab("host")).GetComponent<Button>();

            tabJoinBtn = CreateStyledButton(mainModalCard, "Tab_Join", "JOIN WITH CODE", new Vector2(0, 160), new Vector2(210, 46),
                new Color(0.12f, 0.16f, 0.24f), new Color(0.7f, 0.8f, 0.95f), () => SwitchTab("join")).GetComponent<Button>();

            tabSoloBtn = CreateStyledButton(mainModalCard, "Tab_Solo", "SOLO OFFLINE", new Vector2(230, 160), new Vector2(210, 46),
                new Color(0.12f, 0.16f, 0.24f), new Color(0.7f, 0.8f, 0.95f), () => SwitchTab("solo")).GetComponent<Button>();

            // --- TAB 1: HOST VIEW ---
            hostViewObj = new GameObject("View_Host");
            hostViewObj.transform.SetParent(mainModalCard.transform, false);
            RectTransform hvr = hostViewObj.AddComponent<RectTransform>();
            hvr.anchoredPosition = Vector2.zero;

            CreateTMPText(hostViewObj, "ROOM CODE:", 13, FontStyles.Bold,
                new Vector2(0, 95), new Vector2(500, 24), new Color(0.65f, 0.75f, 0.90f));

            // Code Display & Copy Button
            GameObject codeCard = CreatePanel(hostViewObj, "Code_Card", new Vector2(-80, 45), new Vector2(220, 56), new Color(0.06f, 0.10f, 0.16f));
            hostRoomCodeText = CreateTMPText(codeCard, "AUR4", 28, FontStyles.Bold, Vector2.zero, new Vector2(220, 56), new Color(0.2f, 0.85f, 0.65f));

            CreateStyledButton(hostViewObj, "Btn_Copy", "COPY CODE", new Vector2(90, 45), new Vector2(130, 50),
                new Color(0.15f, 0.25f, 0.40f), Color.white, CopyRoomCode);

            // Chef Slots List
            CreateTMPText(hostViewObj, "CONNECTED CHEFS (1/4):", 12, FontStyles.Bold,
                new Vector2(0, -5), new Vector2(600, 22), new Color(0.65f, 0.75f, 0.90f));

            string[] defaultRoles = { "Chef Ruby (P1 - Host)", "Chef Sapphire (P2)", "Chef Emerald (P3)", "Chef Amber (P4)" };
            for (int i = 0; i < 4; i++)
            {
                float x = -240 + (i * 160);
                GameObject slotCard = CreatePanel(hostViewObj, $"Slot_{i}", new Vector2(x, -45), new Vector2(150, 40),
                    i == 0 ? new Color(0.35f, 0.10f, 0.15f) : new Color(0.08f, 0.11f, 0.16f));
                hostSlotTexts[i] = CreateTMPText(slotCard, i == 0 ? defaultRoles[0] : "[OPEN]", 11, FontStyles.Bold,
                    Vector2.zero, new Vector2(146, 38), i == 0 ? new Color(1f, 0.7f, 0.75f) : new Color(0.45f, 0.55f, 0.70f));
            }

            hostStartGameBtn = CreateStyledButton(hostViewObj, "Btn_StartHost", "START GAME", new Vector2(0, -125), new Vector2(340, 56),
                new Color(0.08f, 0.55f, 0.35f), Color.white, OnHostStartClicked).GetComponent<Button>();

            hostStatusText = CreateTMPText(hostViewObj, "Waiting for players to join...", 12, FontStyles.Normal,
                new Vector2(0, -180), new Vector2(650, 24), new Color(0.65f, 0.85f, 0.95f));

            // --- TAB 2: JOIN VIEW ---
            joinViewObj = new GameObject("View_Join");
            joinViewObj.transform.SetParent(mainModalCard.transform, false);
            RectTransform jvr = joinViewObj.AddComponent<RectTransform>();
            jvr.anchoredPosition = Vector2.zero;

            CreateTMPText(joinViewObj, "ENTER 4-LETTER ROOM CODE:", 14, FontStyles.Bold,
                new Vector2(0, 80), new Vector2(500, 26), new Color(0.80f, 0.88f, 0.98f));

            GameObject ipBox = CreatePanel(joinViewObj, "Input_Container", new Vector2(0, 20), new Vector2(260, 56), new Color(0.05f, 0.08f, 0.12f));
            joinCodeInput = ipBox.AddComponent<TMP_InputField>();
            joinCodeInput.characterLimit = 4;
            joinCodeInput.contentType = TMP_InputField.ContentType.Alphanumeric;

            TextMeshProUGUI ipTmp = CreateTMPText(ipBox, "", 26, FontStyles.Bold, Vector2.zero, new Vector2(250, 50), Color.white);
            joinCodeInput.textComponent = ipTmp;

            joinSubmitBtn = CreateStyledButton(joinViewObj, "Btn_JoinSubmit", "JOIN ROOM", new Vector2(0, -60), new Vector2(280, 54),
                new Color(0.12f, 0.45f, 0.75f), Color.white, OnJoinSubmitClicked).GetComponent<Button>();

            joinStatusText = CreateTMPText(joinViewObj, "", 12, FontStyles.Normal,
                new Vector2(0, -120), new Vector2(600, 26), new Color(0.9f, 0.4f, 0.4f));

            // --- GUEST WAITING VIEW (Active after joining room) ---
            guestWaitingViewObj = new GameObject("View_GuestWaiting");
            guestWaitingViewObj.transform.SetParent(mainModalCard.transform, false);
            RectTransform gwvr = guestWaitingViewObj.AddComponent<RectTransform>();
            gwvr.anchoredPosition = Vector2.zero;
            guestWaitingViewObj.SetActive(false);

            CreateTMPText(guestWaitingViewObj, "CONNECTED TO ROOM:", 13, FontStyles.Bold,
                new Vector2(0, 95), new Vector2(500, 24), new Color(0.65f, 0.75f, 0.90f));

            guestRoomCodeText = CreateTMPText(guestWaitingViewObj, "AUR4", 28, FontStyles.Bold,
                new Vector2(0, 55), new Vector2(300, 44), new Color(0.2f, 0.85f, 0.65f));

            guestRoleText = CreateTMPText(guestWaitingViewObj, "YOUR ROLE: Chef Sapphire (P2)", 14, FontStyles.Bold,
                new Vector2(0, 10), new Vector2(500, 28), new Color(0.4f, 0.75f, 1f));

            guestWaitingStatusText = CreateTMPText(guestWaitingViewObj, "WAITING FOR HOST TO START GAME...", 15, FontStyles.Bold,
                new Vector2(0, -50), new Vector2(600, 36), new Color(0.98f, 0.75f, 0.15f));

            guestLeaveBtn = CreateStyledButton(guestWaitingViewObj, "Btn_Leave", "LEAVE ROOM", new Vector2(0, -130), new Vector2(240, 48),
                new Color(0.35f, 0.15f, 0.18f), Color.white, OnLeaveRoomClicked).GetComponent<Button>();

            // --- TAB 3: SOLO VIEW ---
            soloViewObj = new GameObject("View_Solo");
            soloViewObj.transform.SetParent(mainModalCard.transform, false);
            RectTransform svr = soloViewObj.AddComponent<RectTransform>();
            svr.anchoredPosition = Vector2.zero;

            CreateTMPText(soloViewObj, "SOLO OFFLINE SHIFT", 18, FontStyles.Bold,
                new Vector2(0, 70), new Vector2(500, 32), new Color(0.95f, 0.97f, 1f));

            CreateTMPText(soloViewObj, "Play offline with full control of 2 chefs,\ninstant Chef Switch, and Sous-Chef assistant!", 14, FontStyles.Normal,
                new Vector2(0, 15), new Vector2(600, 50), new Color(0.75f, 0.85f, 0.95f));

            CreateStyledButton(soloViewObj, "Btn_StartSolo", "START SOLO SHIFT", new Vector2(0, -70), new Vector2(300, 54),
                new Color(0.85f, 0.45f, 0.12f), Color.white, OnStartSoloClicked);

            // Bottom Global Status
            globalStatusText = CreateTMPText(mainModalCard, "", 12, FontStyles.Normal,
                new Vector2(0, -220), new Vector2(700, 24), new Color(0.7f, 0.8f, 0.9f));
        }

        private void SwitchTab(string tab)
        {
            if (isWaitingInRoom && tab != "guest_waiting")
            {
                // If currently waiting in a joined room as guest, show the guest waiting screen
                tab = "guest_waiting";
            }

            hostViewObj.SetActive(tab == "host");
            joinViewObj.SetActive(tab == "join");
            guestWaitingViewObj.SetActive(tab == "guest_waiting");
            soloViewObj.SetActive(tab == "solo");

            tabHostBtn.image.color = tab == "host" ? new Color(0.18f, 0.35f, 0.60f) : new Color(0.12f, 0.16f, 0.24f);
            tabJoinBtn.image.color = (tab == "join" || tab == "guest_waiting") ? new Color(0.18f, 0.35f, 0.60f) : new Color(0.12f, 0.16f, 0.24f);
            tabSoloBtn.image.color = tab == "solo" ? new Color(0.18f, 0.35f, 0.60f) : new Color(0.12f, 0.16f, 0.24f);

            if (tab == "host")
            {
                isHost = true;
                isWaitingInRoom = false;
                InitializeHostRoom();
            }
        }

        private void InitializeHostRoom()
        {
            GenerateNewCode();
            StartCoroutine(RegisterRoomOnServer(activeRoomCode, "Chef Ruby (Host)"));

            if (roomPollCoroutine != null) StopCoroutine(roomPollCoroutine);
            roomPollCoroutine = StartCoroutine(HostPollRoomState());
        }

        private void GenerateNewCode()
        {
            string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            char[] code = new char[4];
            var rnd = new System.Random();
            for (int i = 0; i < 4; i++) code[i] = chars[rnd.Next(chars.Length)];
            activeRoomCode = new string(code);
            if (hostRoomCodeText != null) hostRoomCodeText.text = activeRoomCode;
        }

        private void CopyRoomCode()
        {
            GUIUtility.systemCopyBuffer = activeRoomCode;
            if (hostStatusText != null) hostStatusText.text = $"Room Code '{activeRoomCode}' copied to clipboard.";
        }

        private void OnHostStartClicked()
        {
            if (hostStartGameBtn != null) hostStartGameBtn.interactable = false;
            if (hostStatusText != null) hostStatusText.text = "Starting shift...";

            StartCoroutine(PostRoomStart(activeRoomCode, (success, msg) =>
            {
                PlayerPrefs.SetInt("Aura_GameMode", (int)MultiplayerManager.Mode.OnlineCoop);
                PlayerPrefs.SetString("Aura_RoomCode", activeRoomCode);
                PlayerPrefs.SetInt("Aura_PlayerSlot", 0);

                HideLobby();
                if (SceneManager.GetActiveScene().name != SceneSwitcher.Scene.GameScene.ToString())
                {
                    SceneSwitcher.LoadScene(SceneSwitcher.Scene.GameScene);
                }
            }));
        }

        private void OnJoinSubmitClicked()
        {
            string code = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpper() : "";
            if (string.IsNullOrEmpty(code) || code.Length < 3)
            {
                if (joinStatusText != null) joinStatusText.text = "Please enter a valid 4-letter room code.";
                return;
            }

            if (joinStatusText != null) joinStatusText.text = "Connecting to room...";
            if (joinSubmitBtn != null) joinSubmitBtn.interactable = false;

            StartCoroutine(PostRoomJoin(code, "Chef Sapphire", (success, response) =>
            {
                if (joinSubmitBtn != null) joinSubmitBtn.interactable = true;

                if (!success)
                {
                    // CRITICAL: DO NOT START GAME ON ERROR
                    if (joinStatusText != null)
                    {
                        joinStatusText.text = "Room not found or shift already started. Check code.";
                    }
                    return;
                }

                // Parse slot index from response if present
                activeRoomCode = code;
                isHost = false;
                isWaitingInRoom = true;
                mySlotIndex = 1;
                if (response.Contains("\"slot\":"))
                {
                    int idx = response.IndexOf("\"slot\":") + 7;
                    if (idx < response.Length && char.IsDigit(response[idx]))
                    {
                        mySlotIndex = response[idx] - '0';
                    }
                }

                // Switch to waiting room screen
                SwitchTab("guest_waiting");
                if (guestRoomCodeText != null) guestRoomCodeText.text = activeRoomCode;
                string[] chefNames = { "Chef Ruby (P1)", "Chef Sapphire (P2)", "Chef Emerald (P3)", "Chef Amber (P4)" };
                if (guestRoleText != null) guestRoleText.text = $"YOUR ROLE: {chefNames[Mathf.Clamp(mySlotIndex, 0, 3)]}";

                if (roomPollCoroutine != null) StopCoroutine(roomPollCoroutine);
                roomPollCoroutine = StartCoroutine(GuestPollRoomState());
            }));
        }

        private void OnLeaveRoomClicked()
        {
            isWaitingInRoom = false;
            if (roomPollCoroutine != null) StopCoroutine(roomPollCoroutine);
            StartCoroutine(PostRoomLeave(activeRoomCode, mySlotIndex));
            SwitchTab("join");
            if (joinStatusText != null) joinStatusText.text = "Left the room.";
        }

        private void OnStartSoloClicked()
        {
            PlayerPrefs.SetInt("Aura_GameMode", (int)MultiplayerManager.Mode.SoloSwitch);
            HideLobby();
            if (SceneManager.GetActiveScene().name != SceneSwitcher.Scene.GameScene.ToString())
            {
                SceneSwitcher.LoadScene(SceneSwitcher.Scene.GameScene);
            }
        }

        private IEnumerator HostPollRoomState()
        {
            while (isHost && !isWaitingInRoom)
            {
                yield return new WaitForSeconds(1.2f);
                yield return StartCoroutine(GetRoomState(activeRoomCode, (success, json) =>
                {
                    if (success && !string.IsNullOrEmpty(json))
                    {
                        UpdateHostSlotsDisplay(json);
                    }
                }));
            }
        }

        private void UpdateHostSlotsDisplay(string json)
        {
            // Simple robust JSON checks
            int count = 1;
            if (json.Contains("Chef Sapphire") || json.Contains("\"slot\":1"))
            {
                count = Mathf.Max(count, 2);
                if (hostSlotTexts[1] != null)
                {
                    hostSlotTexts[1].text = "Chef Sapphire (P2)";
                    hostSlotTexts[1].color = new Color(0.4f, 0.75f, 1f);
                }
            }
            if (json.Contains("Chef Emerald") || json.Contains("\"slot\":2"))
            {
                count = Mathf.Max(count, 3);
                if (hostSlotTexts[2] != null)
                {
                    hostSlotTexts[2].text = "Chef Emerald (P3)";
                    hostSlotTexts[2].color = new Color(0.3f, 0.9f, 0.5f);
                }
            }
            if (json.Contains("Chef Amber") || json.Contains("\"slot\":3"))
            {
                count = Mathf.Max(count, 4);
                if (hostSlotTexts[3] != null)
                {
                    hostSlotTexts[3].text = "Chef Amber (P4)";
                    hostSlotTexts[3].color = new Color(1f, 0.8f, 0.3f);
                }
            }

            if (hostStatusText != null)
            {
                hostStatusText.text = $"Connected Chefs: {count}/4. Ready to start when you are!";
            }
        }

        private IEnumerator GuestPollRoomState()
        {
            int dots = 0;
            while (isWaitingInRoom)
            {
                dots = (dots + 1) % 4;
                string dotStr = new string('.', dots);
                if (guestWaitingStatusText != null)
                {
                    guestWaitingStatusText.text = $"WAITING FOR HOST TO START GAME{dotStr}";
                }

                yield return new WaitForSeconds(0.8f);

                yield return StartCoroutine(GetRoomState(activeRoomCode, (success, json) =>
                {
                    if (success && json.Contains("\"isPlaying\":true"))
                    {
                        // HOST STARTED THE GAME!
                        isWaitingInRoom = false;
                        if (guestWaitingStatusText != null)
                        {
                            guestWaitingStatusText.text = "Host started the match! Entering kitchen...";
                        }

                        PlayerPrefs.SetInt("Aura_GameMode", (int)MultiplayerManager.Mode.OnlineCoop);
                        PlayerPrefs.SetString("Aura_RoomCode", activeRoomCode);
                        PlayerPrefs.SetInt("Aura_PlayerSlot", mySlotIndex);

                        StartCoroutine(EnterGameAfterDelay());
                    }
                }));
            }
        }

        private IEnumerator EnterGameAfterDelay()
        {
            yield return new WaitForSeconds(0.4f);
            HideLobby();
            if (SceneManager.GetActiveScene().name != SceneSwitcher.Scene.GameScene.ToString())
            {
                SceneSwitcher.LoadScene(SceneSwitcher.Scene.GameScene);
            }
        }

        // --- HTTP REST HELPERS ---
        private IEnumerator RegisterRoomOnServer(string code, string hostName)
        {
            string url = $"{serverBaseUrl}/api/room/create";
            string json = $"{{\"code\":\"{code}\",\"hostName\":\"{hostName}\"}}";
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] body = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
            }
        }

        private IEnumerator PostRoomJoin(string code, string playerName, Action<bool, string> onComplete)
        {
            string url = $"{serverBaseUrl}/api/room/join";
            string json = $"{{\"code\":\"{code}\",\"playerName\":\"{playerName}\"}}";
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] body = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();

                bool ok = req.result == UnityWebRequest.Result.Success;
                onComplete?.Invoke(ok, ok ? req.downloadHandler.text : req.error);
            }
        }

        private IEnumerator GetRoomState(string code, Action<bool, string> onComplete)
        {
            string url = $"{serverBaseUrl}/api/room/{code}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 5;
                yield return req.SendWebRequest();
                bool ok = req.result == UnityWebRequest.Result.Success;
                onComplete?.Invoke(ok, ok ? req.downloadHandler.text : req.error);
            }
        }

        private IEnumerator PostRoomStart(string code, Action<bool, string> onComplete)
        {
            string url = $"{serverBaseUrl}/api/room/{code}/start";
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] body = Encoding.UTF8.GetBytes("{}");
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
                bool ok = req.result == UnityWebRequest.Result.Success;
                onComplete?.Invoke(ok, ok ? req.downloadHandler.text : req.error);
            }
        }

        private IEnumerator PostRoomLeave(string code, int slot)
        {
            string url = $"{serverBaseUrl}/api/room/{code}/leave";
            string json = $"{{\"slot\":{slot}}}";
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] body = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
            }
        }

        // --- PROCEDURAL UI GENERATION UTILITIES ---
        private GameObject CreatePanel(GameObject parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            GameObject p = new GameObject(name);
            p.transform.SetParent(parent.transform, false);
            RectTransform rt = p.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image img = p.AddComponent<Image>();
            img.color = color;
            return p;
        }

        private GameObject CreateStyledButton(GameObject parent, string name, string label, Vector2 pos, Vector2 size, Color bgColor, Color textColor, Action onClick = null)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = new Color(bgColor.r * 1.4f, bgColor.g * 1.4f, bgColor.b * 1.4f, 0.7f);
            outline.effectDistance = new Vector2(1, -1);

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = bgColor;
            cb.highlightedColor = bgColor * 1.25f;
            cb.pressedColor = bgColor * 0.8f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            TextMeshProUGUI tmp = CreateTMPText(btnObj, label, 15, FontStyles.Bold, Vector2.zero, size, textColor);
            tmp.rectTransform.anchorMin = Vector2.zero;
            tmp.rectTransform.anchorMax = Vector2.one;
            tmp.rectTransform.sizeDelta = Vector2.zero;

            return btnObj;
        }

        private TextMeshProUGUI CreateTMPText(GameObject parent, string text, float size, FontStyles style, Vector2 pos, Vector2 dimensions, Color color)
        {
            GameObject tObj = new GameObject("Text");
            tObj.transform.SetParent(parent.transform, false);
            RectTransform rt = tObj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = dimensions;

            TextMeshProUGUI tmp = tObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
