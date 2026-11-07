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
        private GameObject namePromptModalObj;
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

        // Header Identity
        private TextMeshProUGUI headerChefNameText;

        // Name Prompt Elements
        private TMP_InputField namePromptInput;
        private TextMeshProUGUI namePromptStatusText;

        // Host View Elements
        private TextMeshProUGUI hostRoomCodeText;
        private TextMeshProUGUI hostStatusText;
        private Button hostStartGameBtn;
        private ChefCardUI[] hostCards = new ChefCardUI[4];

        // Join View Elements
        private TMP_InputField joinCodeInput;
        private TextMeshProUGUI joinStatusText;
        private Button joinSubmitBtn;

        // Guest Waiting View Elements
        private TextMeshProUGUI guestRoomCodeText;
        private TextMeshProUGUI guestRoleText;
        private TextMeshProUGUI guestWaitingStatusText;
        private Button guestLeaveBtn;
        private ChefCardUI[] guestCards = new ChefCardUI[4];

        // Global status & state
        private string activeRoomCode = "AUR4";
        private int mySlotIndex = 0;
        private bool isHost = false;
        private bool isWaitingInRoom = false;
        private Coroutine roomPollCoroutine;

        private static TMP_FontAsset cachedFont;

        private class ChefCardUI
        {
            public GameObject root;
            public Image accentBar;
            public TextMeshProUGUI roleText;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI statusText;
            public Image bgImage;
        }

        private static readonly Color[] ChefSlotColors = new Color[]
        {
            new Color(0.95f, 0.25f, 0.25f, 1f), // P1: Ruby Red
            new Color(0.20f, 0.55f, 0.95f, 1f), // P2: Sapphire Blue
            new Color(0.18f, 0.80f, 0.35f, 1f), // P3: Emerald Green
            new Color(0.95f, 0.75f, 0.15f, 1f)  // P4: Amber Gold
        };

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
            CreateNamePromptModal();
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
                HideNamePromptModal();
            }
            else
            {
                // Check if returning from match to active room
                if (PlayerPrefs.GetInt("Aura_ReturnToLobby", 0) == 1)
                {
                    PlayerPrefs.SetInt("Aura_ReturnToLobby", 0);
                    string code = PlayerPrefs.GetString("Aura_RoomCode", "AUR4");
                    int slot = PlayerPrefs.GetInt("Aura_PlayerSlot", 0);
                    ReturnToActiveRoom(code, slot == 0, slot);
                }
            }
        }

        private void Start()
        {
            HideLobby();
            HideNamePromptModal();
        }

        public void OpenMultiplayer()
        {
            string savedName = PlayerPrefs.GetString("Aura_PlayerName", "").Trim();
            if (string.IsNullOrEmpty(savedName))
            {
                ShowNamePromptModal();
            }
            else
            {
                UpdateHeaderName();
                ShowLobby();
            }
        }

        public void ShowLobby()
        {
            if (lobbyRootCanvas != null)
            {
                lobbyRootCanvas.SetActive(true);
            }
            if (mainModalCard != null)
            {
                mainModalCard.SetActive(true);
            }
            UpdateHeaderName();
            SwitchTab("host");
        }

        public void HideLobby()
        {
            if (mainModalCard != null)
            {
                mainModalCard.SetActive(false);
            }
            if (namePromptModalObj != null && !namePromptModalObj.activeSelf && (topCornerLobbyBtn == null || !topCornerLobbyBtn.activeSelf))
            {
                if (lobbyRootCanvas != null) lobbyRootCanvas.SetActive(false);
            }
        }

        public void ReturnToActiveRoom(string code, bool asHost, int slot)
        {
            activeRoomCode = code.ToUpper().Trim();
            isHost = asHost;
            mySlotIndex = slot;

            if (lobbyRootCanvas != null) lobbyRootCanvas.SetActive(true);
            if (mainModalCard != null) mainModalCard.SetActive(true);
            UpdateHeaderName();

            if (isHost)
            {
                SwitchTab("host");
                if (hostRoomCodeText != null) hostRoomCodeText.text = activeRoomCode;
                if (hostStatusText != null) hostStatusText.text = "Returned to room lobby. Waiting for chefs...";
                if (hostStartGameBtn != null) hostStartGameBtn.interactable = true;

                if (roomPollCoroutine != null) StopCoroutine(roomPollCoroutine);
                roomPollCoroutine = StartCoroutine(HostPollRoomState());
            }
            else
            {
                isWaitingInRoom = true;
                SwitchTab("guest_waiting");
                if (guestRoomCodeText != null) guestRoomCodeText.text = activeRoomCode;
                string[] chefNames = { "Chef Ruby (P1)", "Chef Sapphire (P2)", "Chef Emerald (P3)", "Chef Amber (P4)" };
                if (guestRoleText != null) guestRoleText.text = $"YOUR ROLE: {chefNames[Mathf.Clamp(mySlotIndex, 0, 3)]}";

                if (roomPollCoroutine != null) StopCoroutine(roomPollCoroutine);
                roomPollCoroutine = StartCoroutine(GuestPollRoomState());
            }
        }

        private void UpdateHeaderName()
        {
            if (headerChefNameText != null)
            {
                string name = PlayerPrefs.GetString("Aura_PlayerName", "Chef Ruby");
                headerChefNameText.text = $"CHEF: <color=#ffd700>{name}</color>";
            }
        }

        // ==================== PROCEDURAL UI CREATION ====================

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

            // 2. Dim Dark Backdrop
            GameObject backdropObj = new GameObject("Modal_Backdrop");
            backdropObj.transform.SetParent(lobbyRootCanvas.transform, false);
            RectTransform bdRect = backdropObj.AddComponent<RectTransform>();
            bdRect.anchorMin = Vector2.zero;
            bdRect.anchorMax = Vector2.one;
            bdRect.sizeDelta = Vector2.zero;

            Image bdImg = backdropObj.AddComponent<Image>();
            bdImg.color = new Color(0.0f, 0.0f, 0.0f, 0.85f);

            // 3. Modal Card (Large, Charcoal Kitchen Chaos Theme)
            mainModalCard = new GameObject("Modal_Card");
            mainModalCard.transform.SetParent(backdropObj.transform, false);
            RectTransform cardRect = mainModalCard.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(1200, 820);

            Image cardImg = mainModalCard.AddComponent<Image>();
            cardImg.color = new Color(0.18f, 0.18f, 0.18f, 0.98f);

            Outline cardOutline = mainModalCard.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.38f, 0.38f, 0.38f, 1f);
            cardOutline.effectDistance = new Vector2(3, -3);

            // Card Header
            CreateTMPText(mainModalCard, "AURA KITCHEN - MULTIPLAYER", 32, FontStyles.Bold,
                new Vector2(0, 360), new Vector2(900, 48), Color.white);

            // Header Identity & Edit button
            headerChefNameText = CreateTMPText(mainModalCard, "CHEF: Chef Ruby", 18, FontStyles.Bold,
                new Vector2(-100, 315), new Vector2(400, 30), new Color(0.85f, 0.85f, 0.85f));
            headerChefNameText.alignment = TextAlignmentOptions.Right;

            CreateStyledButton(mainModalCard, "Btn_EditChefName", "CHANGE NAME", new Vector2(150, 315), new Vector2(160, 34),
                new Color(0.32f, 0.32f, 0.40f), Color.white, ShowNamePromptModal, 14);

            // Close Button (X)
            CreateStyledButton(mainModalCard, "Btn_Close", "X", new Vector2(550, 360), new Vector2(60, 60),
                new Color(0.30f, 0.30f, 0.30f, 1f), Color.white, HideLobby, 26);

            // Navigation Tabs
            tabHostBtn = CreateStyledButton(mainModalCard, "Tab_Host", "HOST ROOM", new Vector2(-360, 245), new Vector2(340, 64),
                new Color(0.38f, 0.38f, 0.38f), Color.white, () => SwitchTab("host"), 20).GetComponent<Button>();

            tabJoinBtn = CreateStyledButton(mainModalCard, "Tab_Join", "JOIN WITH CODE", new Vector2(0, 245), new Vector2(340, 64),
                new Color(0.26f, 0.26f, 0.26f), new Color(0.85f, 0.85f, 0.85f), () => SwitchTab("join"), 20).GetComponent<Button>();

            tabSoloBtn = CreateStyledButton(mainModalCard, "Tab_Solo", "SOLO OFFLINE", new Vector2(360, 245), new Vector2(340, 64),
                new Color(0.26f, 0.26f, 0.26f), new Color(0.85f, 0.85f, 0.85f), () => SwitchTab("solo"), 20).GetComponent<Button>();

            // --- TAB 1: HOST VIEW ---
            hostViewObj = new GameObject("View_Host");
            hostViewObj.transform.SetParent(mainModalCard.transform, false);
            RectTransform hvr = hostViewObj.AddComponent<RectTransform>();
            hvr.anchoredPosition = Vector2.zero;

            CreateTMPText(hostViewObj, "ROOM CODE:", 18, FontStyles.Bold,
                new Vector2(0, 165), new Vector2(700, 30), new Color(0.85f, 0.85f, 0.85f));

            // Code Display, Copy Button & New Room Button
            GameObject codeCard = CreatePanel(hostViewObj, "Code_Card", new Vector2(-150, 105), new Vector2(280, 80), new Color(0.12f, 0.12f, 0.12f));
            hostRoomCodeText = CreateTMPText(codeCard, "AUR4", 44, FontStyles.Bold, Vector2.zero, new Vector2(280, 80), new Color(1.0f, 0.85f, 0.3f));

            CreateStyledButton(hostViewObj, "Btn_Copy", "COPY CODE", new Vector2(40, 105), new Vector2(150, 80),
                new Color(0.32f, 0.32f, 0.32f), Color.white, CopyRoomCode, 18);

            CreateStyledButton(hostViewObj, "Btn_NewRoom", "NEW CODE", new Vector2(200, 105), new Vector2(150, 80),
                new Color(0.28f, 0.28f, 0.38f), Color.white, () => GenerateNewHostRoom(), 18);

            // 4 Distinct Character Cards for Connected Chefs
            CreateTMPText(hostViewObj, "CONNECTED CHEFS (1/4):", 18, FontStyles.Bold,
                new Vector2(0, 35), new Vector2(900, 28), new Color(0.85f, 0.85f, 0.85f));

            for (int i = 0; i < 4; i++)
            {
                float x = -420 + (i * 280);
                hostCards[i] = BuildCharacterCard(hostViewObj, $"HostCard_{i}", new Vector2(x, -60), i);
            }

            hostStartGameBtn = CreateStyledButton(hostViewObj, "Btn_StartHost", "START GAME", new Vector2(0, -190), new Vector2(460, 78),
                new Color(0.18f, 0.58f, 0.28f), Color.white, OnHostStartClicked, 24).GetComponent<Button>();

            hostStatusText = CreateTMPText(hostViewObj, "Waiting for players to join...", 16, FontStyles.Normal,
                new Vector2(0, -255), new Vector2(950, 32), new Color(0.85f, 0.85f, 0.85f));

            // --- TAB 2: JOIN VIEW ---
            joinViewObj = new GameObject("View_Join");
            joinViewObj.transform.SetParent(mainModalCard.transform, false);
            RectTransform jvr = joinViewObj.AddComponent<RectTransform>();
            jvr.anchoredPosition = Vector2.zero;

            CreateTMPText(joinViewObj, "ENTER 4-LETTER ROOM CODE:", 22, FontStyles.Bold,
                new Vector2(0, 140), new Vector2(700, 36), Color.white);

            GameObject ipBox = CreatePanel(joinViewObj, "Input_Container", new Vector2(0, 50), new Vector2(380, 86), new Color(0.12f, 0.12f, 0.12f));
            Outline ipOutline = ipBox.AddComponent<Outline>();
            ipOutline.effectColor = new Color(0.40f, 0.40f, 0.40f, 1f);

            joinCodeInput = ipBox.AddComponent<TMP_InputField>();
            joinCodeInput.characterLimit = 4;
            joinCodeInput.contentType = TMP_InputField.ContentType.Alphanumeric;

            TextMeshProUGUI ipTmp = CreateTMPText(ipBox, "", 40, FontStyles.Bold, Vector2.zero, new Vector2(360, 80), Color.white);
            joinCodeInput.textComponent = ipTmp;

            joinSubmitBtn = CreateStyledButton(joinViewObj, "Btn_JoinSubmit", "JOIN ROOM", new Vector2(0, -65), new Vector2(380, 78),
                new Color(0.18f, 0.58f, 0.28f), Color.white, OnJoinSubmitClicked, 24).GetComponent<Button>();

            joinStatusText = CreateTMPText(joinViewObj, "", 18, FontStyles.Normal,
                new Vector2(0, -155), new Vector2(900, 48), new Color(1f, 0.4f, 0.4f));

            // --- GUEST WAITING VIEW ---
            guestWaitingViewObj = new GameObject("View_GuestWaiting");
            guestWaitingViewObj.transform.SetParent(mainModalCard.transform, false);
            RectTransform gwvr = guestWaitingViewObj.AddComponent<RectTransform>();
            gwvr.anchoredPosition = Vector2.zero;
            guestWaitingViewObj.SetActive(false);

            CreateTMPText(guestWaitingViewObj, "CONNECTED TO ROOM:", 18, FontStyles.Bold,
                new Vector2(0, 165), new Vector2(700, 28), new Color(0.85f, 0.85f, 0.85f));

            guestRoomCodeText = CreateTMPText(guestWaitingViewObj, "AUR4", 46, FontStyles.Bold,
                new Vector2(0, 110), new Vector2(400, 64), new Color(1.0f, 0.85f, 0.3f));

            guestRoleText = CreateTMPText(guestWaitingViewObj, "YOUR ROLE: Chef Sapphire (P2)", 20, FontStyles.Bold,
                new Vector2(0, 50), new Vector2(700, 36), new Color(0.4f, 0.8f, 1f));

            // 4 Distinct Character Cards for Guest Waiting
            for (int i = 0; i < 4; i++)
            {
                float x = -420 + (i * 280);
                guestCards[i] = BuildCharacterCard(guestWaitingViewObj, $"GuestCard_{i}", new Vector2(x, -50), i);
            }

            guestWaitingStatusText = CreateTMPText(guestWaitingViewObj, "WAITING FOR HOST TO START GAME...", 22, FontStyles.Bold,
                new Vector2(0, -170), new Vector2(950, 44), new Color(1.0f, 0.85f, 0.25f));

            guestLeaveBtn = CreateStyledButton(guestWaitingViewObj, "Btn_Leave", "LEAVE ROOM", new Vector2(0, -245), new Vector2(320, 66),
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

        private ChefCardUI BuildCharacterCard(GameObject parent, string name, Vector2 pos, int slotIndex)
        {
            ChefCardUI card = new ChefCardUI();

            card.root = new GameObject(name);
            card.root.transform.SetParent(parent.transform, false);
            RectTransform rt = card.root.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(260, 140);

            card.bgImage = card.root.AddComponent<Image>();
            card.bgImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            Outline outline = card.root.AddComponent<Outline>();
            outline.effectColor = new Color(0.28f, 0.28f, 0.28f, 0.8f);

            // Colored Top Accent Bar
            GameObject barObj = new GameObject("Accent_Bar");
            barObj.transform.SetParent(card.root.transform, false);
            RectTransform brt = barObj.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 1);
            brt.anchorMax = new Vector2(1, 1);
            brt.pivot = new Vector2(0.5f, 1);
            brt.sizeDelta = new Vector2(0, 8);
            card.accentBar = barObj.AddComponent<Image>();
            card.accentBar.color = ChefSlotColors[slotIndex];

            // Role Tag
            string[] slotTags = { "[HOST - P1]", "[P2 CHEF]", "[P3 CHEF]", "[P4 CHEF]" };
            card.roleText = CreateTMPText(card.root, slotTags[slotIndex], 14, FontStyles.Bold,
                new Vector2(0, 38), new Vector2(250, 24), ChefSlotColors[slotIndex]);

            // Name
            string defaultName = slotIndex == 0 ? "Chef Ruby" : "[OPEN SLOT]";
            card.nameText = CreateTMPText(card.root, defaultName, 17, FontStyles.Bold,
                new Vector2(0, 6), new Vector2(250, 30), slotIndex == 0 ? Color.white : new Color(0.55f, 0.55f, 0.55f));

            // Status Pill
            string statusStr = slotIndex == 0 ? "[READY]" : "[WAITING]";
            Color statusColor = slotIndex == 0 ? new Color(0.3f, 1f, 0.4f) : new Color(0.5f, 0.5f, 0.5f);
            card.statusText = CreateTMPText(card.root, statusStr, 13, FontStyles.Bold,
                new Vector2(0, -32), new Vector2(250, 22), statusColor);

            return card;
        }

        // ==================== CHEF REGISTRATION MODAL ====================

        private void CreateNamePromptModal()
        {
            namePromptModalObj = new GameObject("Aura_NamePrompt_Modal");
            namePromptModalObj.transform.SetParent(lobbyRootCanvas.transform, false);

            RectTransform rootRt = namePromptModalObj.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;

            Image bg = namePromptModalObj.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.88f);

            // Center Card
            GameObject card = new GameObject("Card");
            card.transform.SetParent(namePromptModalObj.transform, false);
            RectTransform crt = card.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(820, 560);

            Image cImg = card.AddComponent<Image>();
            cImg.color = new Color(0.18f, 0.18f, 0.18f, 0.98f);

            Outline cOutline = card.AddComponent<Outline>();
            cOutline.effectColor = new Color(0.95f, 0.75f, 0.15f, 1f); // Gold highlight
            cOutline.effectDistance = new Vector2(2, -2);

            CreateTMPText(card, "CHEF REGISTRATION", 32, FontStyles.Bold,
                new Vector2(0, 205), new Vector2(750, 48), new Color(1.0f, 0.85f, 0.25f));

            CreateTMPText(card, "Choose your unique Chef Nickname for Multiplayer Clash.\nThis name will be saved and displayed above your character!", 18, FontStyles.Normal,
                new Vector2(0, 130), new Vector2(720, 60), new Color(0.85f, 0.85f, 0.85f));

            // Input Box
            GameObject ipBox = CreatePanel(card, "Name_Input_Container", new Vector2(0, 30), new Vector2(500, 76), new Color(0.12f, 0.12f, 0.12f));
            Outline ipOutline = ipBox.AddComponent<Outline>();
            ipOutline.effectColor = new Color(0.40f, 0.40f, 0.40f, 1f);

            namePromptInput = ipBox.AddComponent<TMP_InputField>();
            namePromptInput.characterLimit = 16;
            namePromptInput.contentType = TMP_InputField.ContentType.Alphanumeric;

            TextMeshProUGUI ipTmp = CreateTMPText(ipBox, "", 32, FontStyles.Bold, Vector2.zero, new Vector2(480, 70), Color.white);
            namePromptInput.textComponent = ipTmp;

            // Suggestion Tags
            string[] suggestions = { "Dexter", "Omar", "Master Chef", "Speedy Slicer" };
            for (int i = 0; i < suggestions.Length; i++)
            {
                string tag = suggestions[i];
                float x = -210 + (i * 140);
                CreateStyledButton(card, $"Btn_Tag_{i}", tag, new Vector2(x, -50), new Vector2(130, 40),
                    new Color(0.28f, 0.28f, 0.35f), Color.white, () =>
                    {
                        if (namePromptInput != null) namePromptInput.text = tag;
                    }, 14);
            }

            // Confirm Button
            CreateStyledButton(card, "Btn_ConfirmName", "SAVE & ENTER LOBBY", new Vector2(0, -140), new Vector2(440, 74),
                new Color(0.18f, 0.58f, 0.28f), Color.white, OnSaveNameClicked, 22);

            namePromptStatusText = CreateTMPText(card, "", 16, FontStyles.Normal,
                new Vector2(0, -210), new Vector2(700, 36), new Color(1f, 0.4f, 0.4f));

            namePromptModalObj.SetActive(false);
        }

        private void ShowNamePromptModal()
        {
            if (lobbyRootCanvas != null) lobbyRootCanvas.SetActive(true);
            if (namePromptModalObj != null)
            {
                namePromptModalObj.SetActive(true);
                string existing = PlayerPrefs.GetString("Aura_PlayerName", "");
                if (namePromptInput != null)
                {
                    namePromptInput.text = existing;
                    namePromptInput.ActivateInputField();
                }
                if (namePromptStatusText != null) namePromptStatusText.text = "";
            }
        }

        private void HideNamePromptModal()
        {
            if (namePromptModalObj != null)
            {
                namePromptModalObj.SetActive(false);
            }
        }

        private void OnSaveNameClicked()
        {
            string chosenName = namePromptInput != null ? namePromptInput.text.Trim() : "";
            if (string.IsNullOrEmpty(chosenName) || chosenName.Length < 2)
            {
                if (namePromptStatusText != null)
                {
                    namePromptStatusText.text = "Please enter at least 2 characters.";
                }
                return;
            }

            PlayerPrefs.SetString("Aura_PlayerName", chosenName);
            PlayerPrefs.Save();

            HideNamePromptModal();
            UpdateHeaderName();
            ShowLobby();
        }

        // ==================== TAB NAVIGATION & ROOM FLOW ====================

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

            string myChefName = PlayerPrefs.GetString("Aura_PlayerName", "Chef Ruby");
            StartCoroutine(RegisterRoomOnServer(activeRoomCode, myChefName));

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
            string code = joinCodeInput != null ? joinCodeInput.text.Replace(" ", "").Trim().ToUpper() : "";
            if (string.IsNullOrEmpty(code) || code.Length < 3)
            {
                if (joinStatusText != null) joinStatusText.text = "Please enter a valid 4-letter room code.";
                return;
            }

            if (joinStatusText != null) joinStatusText.text = "Connecting to room...";
            if (joinSubmitBtn != null) joinSubmitBtn.interactable = false;

            string myChefName = PlayerPrefs.GetString("Aura_PlayerName", "Chef Sapphire");
            StartCoroutine(PostRoomJoin(code, myChefName, (success, response) =>
            {
                if (joinSubmitBtn != null) joinSubmitBtn.interactable = true;

                if (!success)
                {
                    if (joinStatusText != null)
                    {
                        string errMsg = $"Could not join room '{code}'. Check code or server connection.";
                        if (!string.IsNullOrEmpty(response))
                        {
                            if (response.Contains("Room not found"))
                            {
                                errMsg = $"Room '{code}' not found! Ask host for their 4-letter code.";
                            }
                            else if (response.Contains("Match already started"))
                            {
                                errMsg = $"Match in room '{code}' has already started!";
                            }
                            else if (response.Contains("Room is full"))
                            {
                                errMsg = $"Room '{code}' is full (max 4 chefs).";
                            }
                            else if (response.Contains("\"error\":"))
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

                UpdateGuestWaitingCards(response);

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
                        UpdateCharacterCardsList(hostCards, json);
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

                yield return new WaitForSeconds(1.0f);

                yield return StartCoroutine(GetRoomState(activeRoomCode, (success, json) =>
                {
                    if (success)
                    {
                        UpdateCharacterCardsList(guestCards, json);

                        if (json.Contains("\"isPlaying\":true"))
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
                    }
                }));
            }
        }

        private void UpdateGuestWaitingCards(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            UpdateCharacterCardsList(guestCards, json);
        }

        private void UpdateCharacterCardsList(ChefCardUI[] cardArray, string json)
        {
            if (cardArray == null) return;

            string[] defaultChefNames = { "Chef Ruby", "Chef Sapphire", "Chef Emerald", "Chef Amber" };

            for (int i = 0; i < 4; i++)
            {
                ChefCardUI card = cardArray[i];
                if (card == null || card.root == null) continue;

                int sIdx = json.IndexOf($"\"slot\":{i}");
                if (sIdx >= 0)
                {
                    string pName = defaultChefNames[i];
                    int nIdx = json.IndexOf("\"name\":\"", sIdx);
                    if (nIdx >= 0 && nIdx < sIdx + 120)
                    {
                        int eIdx = json.IndexOf("\"", nIdx + 8);
                        if (eIdx > nIdx + 8)
                        {
                            pName = json.Substring(nIdx + 8, eIdx - (nIdx + 8));
                        }
                    }

                    card.nameText.text = pName;
                    card.nameText.color = Color.white;
                    card.statusText.text = i == 0 ? "[READY (HOST)]" : "[CONNECTED]";
                    card.statusText.color = new Color(0.3f, 1.0f, 0.4f);
                    card.bgImage.color = new Color(0.18f, 0.22f, 0.20f, 0.95f);
                }
                else
                {
                    card.nameText.text = "[OPEN SLOT]";
                    card.nameText.color = new Color(0.55f, 0.55f, 0.55f);
                    card.statusText.text = "[WAITING]";
                    card.statusText.color = new Color(0.5f, 0.5f, 0.5f);
                    card.bgImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
                }
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

        // ==================== HTTP REST HELPERS WITH TIMEOUTS ====================

        private IEnumerator RegisterRoomOnServer(string code, string hostName)
        {
            string url = $"{serverBaseUrl}/api/room/create";
            string json = $"{{\"code\":\"{code}\",\"hostName\":\"{hostName}\"}}";
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                req.timeout = 10;
                byte[] body = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();

                if (hostStatusText != null)
                {
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        hostStatusText.text = "Room ready on server! Share the code with friends.";
                        if (json.Contains(hostName) && hostCards[0] != null)
                        {
                            hostCards[0].nameText.text = hostName;
                            hostCards[0].statusText.text = "[READY (HOST)]";
                            hostCards[0].statusText.color = new Color(0.3f, 1f, 0.4f);
                        }
                    }
                    else
                    {
                        hostStatusText.text = "Offline fallback: server unreachable, but room ready locally.";
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
                req.timeout = 10;
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
                req.timeout = 10;
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
                req.timeout = 10;
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
                req.timeout = 10;
                byte[] body = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
            }
        }

        // ==================== PROCEDURAL UI GENERATION UTILITIES ====================

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
