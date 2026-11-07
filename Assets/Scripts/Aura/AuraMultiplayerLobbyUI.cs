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
        [SerializeField] private string serverBaseUrl = "https://aura.dexster.site";

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
        private string activeRoomCode = "AUR4";
        private int mySlotIndex = 0;
        private bool isHost = false;
        private bool isWaitingInRoom = false;
        private Coroutine roomPollCoroutine;

        private static TMP_FontAsset cachedFont;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (cachedFont == null)
            {
                cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }

            CreateLobbyProceduralUI();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
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

            // 1. In-Game Top-Right Pill (GameScene only)
            topCornerLobbyBtn = new GameObject("Btn_OpenLobby_Top");
            topCornerLobbyBtn.transform.SetParent(lobbyRootCanvas.transform, false);
            RectTransform topBtnRect = topCornerLobbyBtn.AddComponent<RectTransform>();
            topBtnRect.anchorMin = new Vector2(1, 1);
            topBtnRect.anchorMax = new Vector2(1, 1);
            topBtnRect.pivot = new Vector2(1, 1);
            topBtnRect.anchoredPosition = new Vector2(-20, -18);
            topBtnRect.sizeDelta = new Vector2(200, 48);

            Image topBtnImg = topCornerLobbyBtn.AddComponent<Image>();
            topBtnImg.color = new Color(0.20f, 0.20f, 0.20f, 0.90f);

            Outline topBtnOutline = topCornerLobbyBtn.AddComponent<Outline>();
            topBtnOutline.effectColor = new Color(0.35f, 0.35f, 0.35f, 0.8f);

            Button topBtn = topCornerLobbyBtn.AddComponent<Button>();
            topBtn.onClick.AddListener(ShowLobby);

            TextMeshProUGUI tbTmp = CreateTMPText(topCornerLobbyBtn, "ROOM LOBBY", 16, FontStyles.Bold,
                Vector2.zero, Vector2.zero, Color.white);
            tbTmp.rectTransform.anchorMin = Vector2.zero;
            tbTmp.rectTransform.anchorMax = Vector2.one;
            tbTmp.rectTransform.sizeDelta = Vector2.zero;
            topCornerLobbyBtn.SetActive(false);

            // 2. Dim Dark Backdrop (matching pause menu)
            GameObject backdropObj = new GameObject("Modal_Backdrop");
            backdropObj.transform.SetParent(lobbyRootCanvas.transform, false);
            RectTransform bdRect = backdropObj.AddComponent<RectTransform>();
            bdRect.anchorMin = Vector2.zero;
            bdRect.anchorMax = Vector2.one;
            bdRect.sizeDelta = Vector2.zero;

            Image bdImg = backdropObj.AddComponent<Image>();
            bdImg.color = new Color(0.0f, 0.0f, 0.0f, 0.82f);

            // 3. Modal Card (Large, Spacious, Charcoal Kitchen Chaos Theme)
            mainModalCard = new GameObject("Modal_Card");
            mainModalCard.transform.SetParent(backdropObj.transform, false);
            RectTransform cardRect = mainModalCard.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(1180, 800); // Substantially enlarged

            Image cardImg = mainModalCard.AddComponent<Image>();
            cardImg.color = new Color(0.20f, 0.20f, 0.20f, 0.98f);

            Outline cardOutline = mainModalCard.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.38f, 0.38f, 0.38f, 1f);
            cardOutline.effectDistance = new Vector2(3, -3);

            // Card Header
            CreateTMPText(mainModalCard, "AURA KITCHEN - MULTIPLAYER", 32, FontStyles.Bold,
                new Vector2(0, 345), new Vector2(900, 48), Color.white);

            CreateTMPText(mainModalCard, "Server: aura.dexster.site", 16, FontStyles.Normal,
                new Vector2(0, 305), new Vector2(800, 28), new Color(0.75f, 0.75f, 0.75f, 1f));

            // Close Button (X)
            CreateStyledButton(mainModalCard, "Btn_Close", "X", new Vector2(530, 345), new Vector2(60, 60),
                new Color(0.30f, 0.30f, 0.30f, 1f), Color.white, HideLobby, 26);

            // Navigation Tabs (Large charcoal buttons)
            tabHostBtn = CreateStyledButton(mainModalCard, "Tab_Host", "HOST ROOM", new Vector2(-350, 235), new Vector2(320, 64),
                new Color(0.38f, 0.38f, 0.38f), Color.white, () => SwitchTab("host"), 20).GetComponent<Button>();

            tabJoinBtn = CreateStyledButton(mainModalCard, "Tab_Join", "JOIN WITH CODE", new Vector2(0, 235), new Vector2(320, 64),
                new Color(0.26f, 0.26f, 0.26f), new Color(0.85f, 0.85f, 0.85f), () => SwitchTab("join"), 20).GetComponent<Button>();

            tabSoloBtn = CreateStyledButton(mainModalCard, "Tab_Solo", "SOLO OFFLINE", new Vector2(350, 235), new Vector2(320, 64),
                new Color(0.26f, 0.26f, 0.26f), new Color(0.85f, 0.85f, 0.85f), () => SwitchTab("solo"), 20).GetComponent<Button>();

            // --- TAB 1: HOST VIEW ---
            hostViewObj = new GameObject("View_Host");
            hostViewObj.transform.SetParent(mainModalCard.transform, false);
            RectTransform hvr = hostViewObj.AddComponent<RectTransform>();
            hvr.anchoredPosition = Vector2.zero;

            CreateTMPText(hostViewObj, "ROOM CODE:", 19, FontStyles.Bold,
                new Vector2(0, 145), new Vector2(700, 32), new Color(0.90f, 0.90f, 0.90f));

            // Code Display, Copy Button & New Room Button
            GameObject codeCard = CreatePanel(hostViewObj, "Code_Card", new Vector2(-150, 75), new Vector2(280, 84), new Color(0.13f, 0.13f, 0.13f));
            hostRoomCodeText = CreateTMPText(codeCard, "AUR4", 44, FontStyles.Bold, Vector2.zero, new Vector2(280, 84), new Color(1.0f, 0.85f, 0.3f));

            CreateStyledButton(hostViewObj, "Btn_Copy", "COPY CODE", new Vector2(40, 75), new Vector2(150, 84),
                new Color(0.32f, 0.32f, 0.32f), Color.white, CopyRoomCode, 18);

            CreateStyledButton(hostViewObj, "Btn_NewRoom", "NEW CODE", new Vector2(200, 75), new Vector2(150, 84),
                new Color(0.28f, 0.28f, 0.38f), Color.white, () => GenerateNewHostRoom(), 18);

            // Chef Slots List
            CreateTMPText(hostViewObj, "CONNECTED CHEFS (1/4):", 19, FontStyles.Bold,
                new Vector2(0, 5), new Vector2(900, 30), new Color(0.85f, 0.85f, 0.85f));

            string[] defaultRoles = { "Chef Ruby (P1 - Host)", "Chef Sapphire (P2)", "Chef Emerald (P3)", "Chef Amber (P4)" };
            for (int i = 0; i < 4; i++)
            {
                float x = -405 + (i * 270);
                GameObject slotCard = CreatePanel(hostViewObj, $"Slot_{i}", new Vector2(x, -50), new Vector2(250, 58),
                    i == 0 ? new Color(0.34f, 0.22f, 0.22f) : new Color(0.14f, 0.14f, 0.14f));
                hostSlotTexts[i] = CreateTMPText(slotCard, i == 0 ? defaultRoles[0] : "[OPEN]", 15, FontStyles.Bold,
                    Vector2.zero, new Vector2(246, 56), i == 0 ? new Color(1f, 0.85f, 0.85f) : new Color(0.65f, 0.65f, 0.65f));
            }

            hostStartGameBtn = CreateStyledButton(hostViewObj, "Btn_StartHost", "START GAME", new Vector2(0, -155), new Vector2(460, 80),
                new Color(0.18f, 0.58f, 0.28f), Color.white, OnHostStartClicked, 24).GetComponent<Button>();

            hostStatusText = CreateTMPText(hostViewObj, "Waiting for players to join...", 17, FontStyles.Normal,
                new Vector2(0, -235), new Vector2(950, 36), new Color(0.85f, 0.85f, 0.85f));

            // --- TAB 2: JOIN VIEW ---
            joinViewObj = new GameObject("View_Join");
            joinViewObj.transform.SetParent(mainModalCard.transform, false);
            RectTransform jvr = joinViewObj.AddComponent<RectTransform>();
            jvr.anchoredPosition = Vector2.zero;

            CreateTMPText(joinViewObj, "ENTER 4-LETTER ROOM CODE:", 22, FontStyles.Bold,
                new Vector2(0, 130), new Vector2(700, 36), Color.white);

            GameObject ipBox = CreatePanel(joinViewObj, "Input_Container", new Vector2(0, 45), new Vector2(380, 84), new Color(0.13f, 0.13f, 0.13f));
            joinCodeInput = ipBox.AddComponent<TMP_InputField>();
            joinCodeInput.characterLimit = 4;
            joinCodeInput.contentType = TMP_InputField.ContentType.Alphanumeric;

            TextMeshProUGUI ipTmp = CreateTMPText(ipBox, "", 38, FontStyles.Bold, Vector2.zero, new Vector2(370, 78), Color.white);
            joinCodeInput.textComponent = ipTmp;

            joinSubmitBtn = CreateStyledButton(joinViewObj, "Btn_JoinSubmit", "JOIN ROOM", new Vector2(0, -70), new Vector2(380, 76),
                new Color(0.18f, 0.58f, 0.28f), Color.white, OnJoinSubmitClicked, 24).GetComponent<Button>();

            joinStatusText = CreateTMPText(joinViewObj, "", 18, FontStyles.Normal,
                new Vector2(0, -155), new Vector2(900, 36), new Color(1f, 0.5f, 0.5f));

            // --- GUEST WAITING VIEW ---
            guestWaitingViewObj = new GameObject("View_GuestWaiting");
            guestWaitingViewObj.transform.SetParent(mainModalCard.transform, false);
            RectTransform gwvr = guestWaitingViewObj.AddComponent<RectTransform>();
            gwvr.anchoredPosition = Vector2.zero;
            guestWaitingViewObj.SetActive(false);

            CreateTMPText(guestWaitingViewObj, "CONNECTED TO ROOM:", 20, FontStyles.Bold,
                new Vector2(0, 145), new Vector2(700, 32), new Color(0.85f, 0.85f, 0.85f));

            guestRoomCodeText = CreateTMPText(guestWaitingViewObj, "AUR4", 46, FontStyles.Bold,
                new Vector2(0, 85), new Vector2(400, 68), new Color(1.0f, 0.85f, 0.3f));

            guestRoleText = CreateTMPText(guestWaitingViewObj, "YOUR ROLE: Chef Sapphire (P2)", 22, FontStyles.Bold,
                new Vector2(0, 20), new Vector2(700, 40), new Color(0.5f, 0.8f, 1f));

            guestWaitingStatusText = CreateTMPText(guestWaitingViewObj, "WAITING FOR HOST TO START GAME...", 24, FontStyles.Bold,
                new Vector2(0, -60), new Vector2(950, 50), new Color(1.0f, 0.85f, 0.25f));

            guestLeaveBtn = CreateStyledButton(guestWaitingViewObj, "Btn_Leave", "LEAVE ROOM", new Vector2(0, -165), new Vector2(320, 68),
                new Color(0.35f, 0.20f, 0.20f), Color.white, OnLeaveRoomClicked, 20).GetComponent<Button>();

            // --- TAB 3: SOLO VIEW ---
            soloViewObj = new GameObject("View_Solo");
            soloViewObj.transform.SetParent(mainModalCard.transform, false);
            RectTransform svr = soloViewObj.AddComponent<RectTransform>();
            svr.anchoredPosition = Vector2.zero;

            CreateTMPText(soloViewObj, "SOLO OFFLINE SHIFT", 28, FontStyles.Bold,
                new Vector2(0, 110), new Vector2(700, 44), Color.white);

            CreateTMPText(soloViewObj, "Play offline with full control of 2 chefs,\ninstant Chef Switch, and Sous-Chef assistant!", 20, FontStyles.Normal,
                new Vector2(0, 35), new Vector2(850, 70), new Color(0.80f, 0.80f, 0.80f));

            CreateStyledButton(soloViewObj, "Btn_StartSolo", "START SOLO SHIFT", new Vector2(0, -90), new Vector2(420, 78),
                new Color(0.18f, 0.58f, 0.28f), Color.white, OnStartSoloClicked, 24);
        }

        private void SwitchTab(string tabName)
        {
            if (hostViewObj != null) hostViewObj.SetActive(tabName == "host");
            if (joinViewObj != null) joinViewObj.SetActive(tabName == "join");
            if (guestWaitingViewObj != null) guestWaitingViewObj.SetActive(tabName == "guest_waiting");
            if (soloViewObj != null) soloViewObj.SetActive(tabName == "solo");

            Color activeBg = new Color(0.38f, 0.38f, 0.38f);
            Color inactiveBg = new Color(0.24f, 0.24f, 0.24f);

            SetButtonTabColor(tabHostBtn, tabName == "host" ? activeBg : inactiveBg);
            SetButtonTabColor(tabJoinBtn, (tabName == "join" || tabName == "guest_waiting") ? activeBg : inactiveBg);
            SetButtonTabColor(tabSoloBtn, tabName == "solo" ? activeBg : inactiveBg);

            if (tabName == "host")
            {
                if (string.IsNullOrEmpty(activeRoomCode) || !isHost)
                {
                    GenerateNewHostRoom();
                }
                else
                {
                    if (hostRoomCodeText != null) hostRoomCodeText.text = activeRoomCode;
                    if (roomPollCoroutine != null) StopCoroutine(roomPollCoroutine);
                    roomPollCoroutine = StartCoroutine(HostPollRoomState());
                }
            }
        }

        private void SetButtonTabColor(Button btn, Color c)
        {
            if (btn == null) return;
            Image img = btn.GetComponent<Image>();
            if (img != null) img.color = c;
        }

        private void GenerateNewHostRoom()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            System.Random rand = new System.Random();
            char[] codeChars = new char[4];
            for (int i = 0; i < 4; i++)
            {
                codeChars[i] = chars[rand.Next(chars.Length)];
            }
            activeRoomCode = new string(codeChars);

            if (hostRoomCodeText != null) hostRoomCodeText.text = activeRoomCode;
            if (hostStatusText != null) hostStatusText.text = "Registering room on server...";
            if (hostStartGameBtn != null) hostStartGameBtn.interactable = true;

            isHost = true;
            mySlotIndex = 0;

            StartCoroutine(RegisterRoomOnServer(activeRoomCode, "Chef Ruby (Host)"));

            if (roomPollCoroutine != null) StopCoroutine(roomPollCoroutine);
            roomPollCoroutine = StartCoroutine(HostPollRoomState());
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
                    if (joinStatusText != null)
                    {
                        string errMsg = "Could not join room. Check code or server connection.";
                        if (!string.IsNullOrEmpty(response) && response.Contains("\"error\":"))
                        {
                            try
                            {
                                int sIdx = response.IndexOf("\"error\":\"") + 9;
                                int eIdx = response.IndexOf("\"", sIdx);
                                if (sIdx >= 9 && eIdx > sIdx)
                                {
                                    errMsg = response.Substring(sIdx, eIdx - sIdx);
                                }
                            }
                            catch { }
                        }
                        joinStatusText.text = errMsg;
                    }
                    return;
                }

                activeRoomCode = code;
                isHost = false;
                isWaitingInRoom = true;
                mySlotIndex = 1;
                if (response != null && response.Contains("\"slot\":"))
                {
                    int idx = response.IndexOf("\"slot\":") + 7;
                    if (idx < response.Length && char.IsDigit(response[idx]))
                    {
                        mySlotIndex = response[idx] - '0';
                    }
                }

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
                yield return new WaitForSeconds(1.5f);
                yield return StartCoroutine(GetRoomState(activeRoomCode, (success, json) =>
                {
                    if (success && json.Contains("\"players\":"))
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (hostSlotTexts[i] == null) continue;
                            if (json.Contains($"\"slot\":{i}"))
                            {
                                string role = i == 0 ? "Chef Ruby (P1 - Host)" : $"Chef {i + 1} (P{i + 1}) [READY]";
                                hostSlotTexts[i].text = role;
                                hostSlotTexts[i].color = new Color(0.5f, 1f, 0.6f);
                            }
                            else if (i > 0)
                            {
                                hostSlotTexts[i].text = "[OPEN]";
                                hostSlotTexts[i].color = new Color(0.6f, 0.6f, 0.6f);
                            }
                        }
                    }
                }));
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

        // --- HTTP REST HELPERS WITH TIMEOUTS ---
        private IEnumerator RegisterRoomOnServer(string code, string hostName)
        {
            string url = $"{serverBaseUrl}/api/room/create";
            string json = $"{{\"code\":\"{code}\",\"hostName\":\"{hostName}\"}}";
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                req.timeout = 5;
                byte[] body = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();

                if (hostStatusText != null)
                {
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        hostStatusText.text = "Room ready! Waiting for players...";
                    }
                    else
                    {
                        hostStatusText.text = "Offline mode (server unreachable, but local play works).";
                    }
                }
            }
        }

        private IEnumerator PostRoomJoin(string code, string playerName, Action<bool, string> onComplete)
        {
            string url = $"{serverBaseUrl}/api/room/join";
            string json = $"{{\"code\":\"{code}\",\"playerName\":\"{playerName}\"}}";
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                req.timeout = 5;
                byte[] body = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();

                bool ok = req.result == UnityWebRequest.Result.Success;
                string respText = (req.downloadHandler != null && !string.IsNullOrEmpty(req.downloadHandler.text))
                    ? req.downloadHandler.text
                    : req.error;
                onComplete?.Invoke(ok, respText);
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
                string respText = (req.downloadHandler != null && !string.IsNullOrEmpty(req.downloadHandler.text))
                    ? req.downloadHandler.text
                    : req.error;
                onComplete?.Invoke(ok, respText);
            }
        }

        private IEnumerator PostRoomStart(string code, Action<bool, string> onComplete)
        {
            string url = $"{serverBaseUrl}/api/room/{code}/start";
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                req.timeout = 5;
                byte[] body = Encoding.UTF8.GetBytes("{}");
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
                bool ok = req.result == UnityWebRequest.Result.Success;
                string respText = (req.downloadHandler != null && !string.IsNullOrEmpty(req.downloadHandler.text))
                    ? req.downloadHandler.text
                    : req.error;
                onComplete?.Invoke(ok, respText);
            }
        }

        private IEnumerator PostRoomLeave(string code, int slot)
        {
            string url = $"{serverBaseUrl}/api/room/{code}/leave";
            string json = $"{{\"slot\":{slot}}}";
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                req.timeout = 5;
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

        private GameObject CreateStyledButton(GameObject parent, string name, string label, Vector2 pos, Vector2 size, Color bgColor, Color textColor, Action onClick = null, float fontSize = 18f)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.40f, 0.40f, 0.40f, 0.8f);
            outline.effectDistance = new Vector2(1, -1);

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = bgColor;
            cb.highlightedColor = bgColor * 1.25f;
            cb.pressedColor = bgColor * 0.8f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            TextMeshProUGUI tmp = CreateTMPText(btnObj, label, fontSize, FontStyles.Bold, Vector2.zero, size, textColor);
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
            if (cachedFont == null)
            {
                cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }
            if (cachedFont != null)
            {
                tmp.font = cachedFont;
            }
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
