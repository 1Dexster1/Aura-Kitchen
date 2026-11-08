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
        private GameObject hudRootObj;
        private GameObject namePromptModalObj;
        private GameObject topCornerLobbyBtn;

        // HUD Edge Containers
        private GameObject hudTopLeftBar;
        private GameObject hudTopTabsBar;
        private GameObject hudTopRightRoomBar;
        private GameObject hudBottomRightArea;
        private GameObject hudBottomLeftArea;

        // View Panels
        private GameObject hostViewControls;
        private GameObject joinModalDialog;
        private GameObject guestWaitingControls;

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

        // Join View Elements
        private TMP_InputField joinCodeInput;
        private TextMeshProUGUI joinStatusText;
        private Button joinSubmitBtn;

        // Guest Waiting View Elements
        private TextMeshProUGUI guestRoomCodeText;
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

            CreateTransparentBattleRoyaleHUD();
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
            // Hide standard 2D menu buttons so 3D squad stage is completely clear
            HideMainMenu2DButtons(true);

            if (lobbyRootCanvas != null) lobbyRootCanvas.SetActive(true);
            if (hudRootObj != null) hudRootObj.SetActive(true);

            if (Aura3DLobbyShowcase.Instance != null)
            {
                Aura3DLobbyShowcase.Instance.SetStageVisible(true);
            }

            UpdateHeaderName();
            SwitchTab("host");
        }

        public void HideLobby()
        {
            if (hudRootObj != null) hudRootObj.SetActive(false);

            if (namePromptModalObj != null && !namePromptModalObj.activeSelf && (topCornerLobbyBtn == null || !topCornerLobbyBtn.activeSelf))
            {
                if (lobbyRootCanvas != null) lobbyRootCanvas.SetActive(false);
            }

            if (Aura3DLobbyShowcase.Instance != null)
            {
                Aura3DLobbyShowcase.Instance.SetStageVisible(false);
            }

            HideMainMenu2DButtons(false);
        }

        public void CloseMultiplayerToMenu()
        {
            if (isHost && !string.IsNullOrEmpty(activeRoomCode))
            {
                StartCoroutine(PostRoomLeave(activeRoomCode, 0));
            }
            isWaitingInRoom = false;
            if (roomPollCoroutine != null) StopCoroutine(roomPollCoroutine);

            HideLobby();
        }

        private void HideMainMenu2DButtons(bool hide)
        {
            if (MainMenuUI.Instance != null)
            {
                MainMenuUI.Instance.gameObject.SetActive(!hide);
            }
            else
            {
                MainMenuUI mmUI = FindObjectOfType<MainMenuUI>(true);
                if (mmUI != null)
                {
                    mmUI.gameObject.SetActive(!hide);
                }
                else
                {
                    GameObject mmCanvas = GameObject.Find("Canvas");
                    if (mmCanvas != null && SceneManager.GetActiveScene().name == SceneSwitcher.Scene.MainMenu.ToString())
                    {
                        Transform mmObj = mmCanvas.transform.Find("MainMenuUI");
                        if (mmObj != null) mmObj.gameObject.SetActive(!hide);
                    }
                }
            }
        }

        public void ReturnToActiveRoom(string code, bool asHost, int slot)
        {
            activeRoomCode = code.ToUpper().Trim();
            isHost = asHost;
            mySlotIndex = slot;

            HideMainMenu2DButtons(true);
            if (lobbyRootCanvas != null) lobbyRootCanvas.SetActive(true);
            if (hudRootObj != null) hudRootObj.SetActive(true);

            if (Aura3DLobbyShowcase.Instance != null)
            {
                Aura3DLobbyShowcase.Instance.SetStageVisible(true);
            }

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

                if (roomPollCoroutine != null) StopCoroutine(roomPollCoroutine);
                roomPollCoroutine = StartCoroutine(GuestPollRoomState());
            }
        }

        private void UpdateHeaderName()
        {
            if (headerChefNameText != null)
            {
                string name = PlayerPrefs.GetString("Aura_PlayerName", AuraTheme.ChefNames[0]);
                headerChefNameText.text = $"CHEF: <color=#ffd700>{name}</color>";
            }
        }

        // ==================== BATTLE-ROYALE TRANSPARENT EDGE HUD ====================

        private void CreateTransparentBattleRoyaleHUD()
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

            // Root HUD Container (Transparent - No grey backdrop blocking the 3D scene!)
            hudRootObj = new GameObject("HUD_EdgeOverlay");
            hudRootObj.transform.SetParent(lobbyRootCanvas.transform, false);
            RectTransform hudRt = hudRootObj.AddComponent<RectTransform>();
            hudRt.anchorMin = Vector2.zero;
            hudRt.anchorMax = Vector2.one;
            hudRt.sizeDelta = Vector2.zero;

            // 1. TOP-LEFT: Chef Profile & Connection Status (Glass Pill)
            hudTopLeftBar = CreateGlassPanel(hudRootObj, "HUD_TopLeft", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(28, -24), new Vector2(380, 68), AuraTheme.PanelFill);

            headerChefNameText = CreateTMPText(hudTopLeftBar, "CHEF: <color=#ffd700>Dexster</color>", 18, FontStyles.Bold,
                new Vector2(-60, 12), new Vector2(240, 26), Color.white);
            headerChefNameText.alignment = TextAlignmentOptions.Left;

            CreateTMPText(hudTopLeftBar, "<color=#2ecc71>ONLINE</color> | aura.dexster.site", 13, FontStyles.Normal,
                new Vector2(-60, -14), new Vector2(240, 20), AuraTheme.TextSecondary).alignment = TextAlignmentOptions.Left;

            CreateStyledButton(hudTopLeftBar, "Btn_ChangeName", "EDIT", new Vector2(135, 0), new Vector2(76, 44),
                new Color32(0x1B, 0x3A, 0x5E, 0xF2), Color.white, ShowNamePromptModal, 14);

            // 2. TOP-CENTER: Squad Tabs (Floating Sleek Pills)
            hudTopTabsBar = new GameObject("HUD_TopTabs");
            hudTopTabsBar.transform.SetParent(hudRootObj.transform, false);
            RectTransform tabRt = hudTopTabsBar.AddComponent<RectTransform>();
            tabRt.anchorMin = new Vector2(0.5f, 1f);
            tabRt.anchorMax = new Vector2(0.5f, 1f);
            tabRt.pivot = new Vector2(0.5f, 1f);
            tabRt.anchoredPosition = new Vector2(0, -24);
            tabRt.sizeDelta = new Vector2(660, 56);

            tabHostBtn = CreateStyledButton(hudTopTabsBar, "Tab_Host", "HOST SQUAD", new Vector2(-220, 0), new Vector2(200, 52),
                new Color32(0x12, 0x3A, 0x5C, 0xF2), Color.white, () => SwitchTab("host"), 17).GetComponent<Button>();

            tabJoinBtn = CreateStyledButton(hudTopTabsBar, "Tab_Join", "JOIN SQUAD", new Vector2(0, 0), new Vector2(200, 52),
                new Color32(0x14, 0x1D, 0x30, 0xD9), AuraTheme.TextSecondary, () => SwitchTab("join"), 17).GetComponent<Button>();

            tabSoloBtn = CreateStyledButton(hudTopTabsBar, "Tab_Solo", "SOLO SHIFT", new Vector2(220, 0), new Vector2(200, 52),
                new Color32(0x14, 0x1D, 0x30, 0xD9), AuraTheme.TextSecondary, () => SwitchTab("solo"), 17).GetComponent<Button>();

            // 3. TOP-RIGHT: Room Code Badge with Copy & New (Glowing Gold Edge Card)
            hudTopRightRoomBar = CreateGlassPanel(hudRootObj, "HUD_TopRight", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-28, -24), new Vector2(400, 68), AuraTheme.PanelFill);


            CreateTMPText(hudTopRightRoomBar, "ROOM CODE:", 13, FontStyles.Bold,
                new Vector2(-125, 14), new Vector2(120, 20), AuraTheme.TextSecondary);

            hostRoomCodeText = CreateTMPText(hudTopRightRoomBar, "AUR4", 28, FontStyles.Bold,
                new Vector2(-125, -12), new Vector2(120, 32), AuraTheme.Gold);

            CreateStyledButton(hudTopRightRoomBar, "Btn_CopyCode", "COPY", new Vector2(45, 0), new Vector2(90, 44),
                new Color32(0x1B, 0x3A, 0x5E, 0xF2), Color.white, CopyRoomCode, 15);

            CreateStyledButton(hudTopRightRoomBar, "Btn_NewCode", "NEW", new Vector2(145, 0), new Vector2(80, 44),
                new Color32(0x2D, 0x24, 0x52, 0xF2), Color.white, GenerateNewHostRoom, 15);

            // 4. BOTTOM-RIGHT: Massive Glowing Action Button (START GAME / WAITING...)
            hudBottomRightArea = new GameObject("HUD_BottomRight");
            hudBottomRightArea.transform.SetParent(hudRootObj.transform, false);
            RectTransform brRt = hudBottomRightArea.AddComponent<RectTransform>();
            brRt.anchorMin = new Vector2(1, 0);
            brRt.anchorMax = new Vector2(1, 0);
            brRt.pivot = new Vector2(1, 0);
            brRt.anchoredPosition = new Vector2(-36, 36);
            brRt.sizeDelta = new Vector2(440, 110);

            // Host Controls
            hostViewControls = new GameObject("Host_Controls");
            hostViewControls.transform.SetParent(hudBottomRightArea.transform, false);
            RectTransform hcr = hostViewControls.AddComponent<RectTransform>();
            hcr.anchorMin = Vector2.zero;
            hcr.anchorMax = Vector2.one;
            hcr.sizeDelta = Vector2.zero;

            hostStartGameBtn = CreateStyledButton(hostViewControls, "Btn_StartGame", "START SHIFT", Vector2.zero, new Vector2(400, 84),
                new Color32(0x12, 0x8A, 0x4E, 0xF5), Color.white, OnHostStartClicked, 26).GetComponent<Button>();


            hostStatusText = CreateTMPText(hostViewControls, "Waiting for squad chefs to connect...", 14, FontStyles.Normal,
                new Vector2(0, -56), new Vector2(420, 24), AuraTheme.TextSecondary);

            // Guest Waiting Controls
            guestWaitingControls = new GameObject("Guest_Waiting_Controls");
            guestWaitingControls.transform.SetParent(hudBottomRightArea.transform, false);
            RectTransform gwcr = guestWaitingControls.AddComponent<RectTransform>();
            gwcr.anchorMin = Vector2.zero;
            gwcr.anchorMax = Vector2.one;
            gwcr.sizeDelta = Vector2.zero;
            guestWaitingControls.SetActive(false);

            GameObject pulsePill = CreateGlassPanel(guestWaitingControls, "Pill_Waiting", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 84), AuraTheme.PanelFill);


            guestWaitingStatusText = CreateTMPText(pulsePill, "WAITING FOR HOST TO START...", 19, FontStyles.Bold,
                Vector2.zero, new Vector2(400, 50), AuraTheme.Gold);

            // 5. BOTTOM-LEFT: Leave / Back Button
            hudBottomLeftArea = new GameObject("HUD_BottomLeft");
            hudBottomLeftArea.transform.SetParent(hudRootObj.transform, false);
            RectTransform blRt = hudBottomLeftArea.AddComponent<RectTransform>();
            blRt.anchorMin = new Vector2(0, 0);
            blRt.anchorMax = new Vector2(0, 0);
            blRt.pivot = new Vector2(0, 0);
            blRt.anchoredPosition = new Vector2(36, 36);
            blRt.sizeDelta = new Vector2(260, 80);

            CreateStyledButton(hudBottomLeftArea, "Btn_BackMenu", "BACK TO MENU", Vector2.zero, new Vector2(240, 64),
                new Color32(0x7A, 0x1F, 0x2B, 0xEE), Color.white, CloseMultiplayerToMenu, 17);

            // 6. FLOATING JOIN DIALOG (Only shown when Tab == "join")
            joinModalDialog = CreateGlassPanel(hudRootObj, "Dialog_JoinSquad", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(560, 320), AuraTheme.PanelFill);


            CreateTMPText(joinModalDialog, "JOIN SQUAD KITCHEN", 26, FontStyles.Bold,
                new Vector2(0, 105), new Vector2(500, 36), Color.white);

            CreateTMPText(joinModalDialog, "Enter the 4-letter room code shared by your friend:", 16, FontStyles.Normal,
                new Vector2(0, 68), new Vector2(500, 28), AuraTheme.TextSecondary);

            GameObject jBox = CreateGlassPanel(joinModalDialog, "Input_Container", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, 5), new Vector2(340, 72), AuraTheme.PanelDeep);

            joinCodeInput = jBox.AddComponent<TMP_InputField>();
            joinCodeInput.characterLimit = 4;
            joinCodeInput.contentType = TMP_InputField.ContentType.Alphanumeric;

            TextMeshProUGUI ipTmp = CreateTMPText(jBox, "", 36, FontStyles.Bold, Vector2.zero, new Vector2(320, 66), AuraTheme.Gold);
            joinCodeInput.textComponent = ipTmp;

            joinSubmitBtn = CreateStyledButton(joinModalDialog, "Btn_JoinSubmit", "JOIN SQUAD", new Vector2(0, -75), new Vector2(340, 64),
                new Color32(0x12, 0x8A, 0x4E, 0xF5), Color.white, OnJoinSubmitClicked, 22).GetComponent<Button>();

            joinStatusText = CreateTMPText(joinModalDialog, "", 15, FontStyles.Normal,
                new Vector2(0, -125), new Vector2(520, 30), AuraTheme.Danger);

            joinModalDialog.SetActive(false);
        }

        private GameObject CreateGlassPanel(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            GameObject p = new GameObject(name);
            p.transform.SetParent(parent.transform, false);
            RectTransform rt = p.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = p.AddComponent<Image>();
            img.sprite = AuraTheme.RoundedFill;
            img.type = Image.Type.Sliced;
            img.color = color;

            GameObject ringObj = new GameObject("Stroke");
            ringObj.transform.SetParent(p.transform, false);
            RectTransform ringRt = ringObj.AddComponent<RectTransform>();
            ringRt.anchorMin = Vector2.zero;
            ringRt.anchorMax = Vector2.one;
            ringRt.offsetMin = Vector2.zero;
            ringRt.offsetMax = Vector2.zero;
            Image ringImg = ringObj.AddComponent<Image>();
            ringImg.sprite = AuraTheme.RoundedRing;
            ringImg.type = Image.Type.Sliced;
            ringImg.color = AuraTheme.Stroke;
            ringImg.raycastTarget = false;

            AuraTheme.AddHardShadow(p);
            return p;
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
            bg.color = AuraTheme.Scrim;

            // Center Card
            GameObject card = new GameObject("Card");
            card.transform.SetParent(namePromptModalObj.transform, false);
            RectTransform crt = card.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(800, 540);

            Image cImg = card.AddComponent<Image>();
            cImg.color = AuraTheme.PanelFill;


            CreateTMPText(card, "CHEF REGISTRATION", 32, FontStyles.Bold,
                new Vector2(0, 195), new Vector2(750, 48), AuraTheme.Gold);

            CreateTMPText(card, "Choose your unique Chef Nickname for Multiplayer Squad.\nThis name will be saved and displayed above your 3D character!", 17, FontStyles.Normal,
                new Vector2(0, 125), new Vector2(720, 56), AuraTheme.TextSecondary);

            // Input Box
            GameObject ipBox = CreateGlassPanel(card, "Name_Input_Container", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(480, 72), AuraTheme.PanelDeep);

            namePromptInput = ipBox.AddComponent<TMP_InputField>();
            namePromptInput.characterLimit = 16;
            namePromptInput.contentType = TMP_InputField.ContentType.Alphanumeric;

            TextMeshProUGUI ipTmp = CreateTMPText(ipBox, "", 30, FontStyles.Bold, Vector2.zero, new Vector2(460, 66), Color.white);
            namePromptInput.textComponent = ipTmp;

            // Suggestion Tags
            string[] suggestions = { "Dexter", "Omar", "Master Chef", "Speedy Slicer" };
            for (int i = 0; i < suggestions.Length; i++)
            {
                string tag = suggestions[i];
                float x = -210 + (i * 140);
                CreateStyledButton(card, $"Btn_Tag_{i}", tag, new Vector2(x, -45), new Vector2(130, 40),
                    new Color32(0x1B, 0x3A, 0x5E, 0xFF), Color.white, () =>
                    {
                        if (namePromptInput != null) namePromptInput.text = tag;
                    }, 14);
            }

            // Confirm Button
            CreateStyledButton(card, "Btn_ConfirmName", "SAVE & ENTER SQUAD LOBBY", new Vector2(0, -130), new Vector2(420, 72),
                new Color32(0x12, 0x8A, 0x4E, 0xFF), Color.white, OnSaveNameClicked, 20);

            namePromptStatusText = CreateTMPText(card, "", 15, FontStyles.Normal,
                new Vector2(0, -195), new Vector2(700, 32), AuraTheme.Danger);

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

            if (Aura3DLobbyShowcase.Instance != null)
            {
                Aura3DLobbyShowcase.Instance.UpdateSlot(mySlotIndex, true, chosenName, true);
            }

            ShowLobby();
        }

        // ==================== TAB NAVIGATION & ROOM FLOW ====================

        private void SwitchTab(string tabName)
        {
            if (hostViewControls != null) hostViewControls.SetActive(tabName == "host");
            if (joinModalDialog != null) joinModalDialog.SetActive(tabName == "join");
            if (guestWaitingControls != null) guestWaitingControls.SetActive(tabName == "guest_waiting");
            if (hudTopRightRoomBar != null) hudTopRightRoomBar.SetActive(tabName == "host" || tabName == "guest_waiting");

            SetTabActive(tabHostBtn, tabName == "host");
            SetTabActive(tabJoinBtn, tabName == "join" || tabName == "guest_waiting");
            SetTabActive(tabSoloBtn, tabName == "solo");

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
            else if (tabName == "solo")
            {
                OnStartSoloClicked();
            }
        }

        private void SetTabActive(Button btn, bool active)
        {
            if (btn == null) return;
            AuraTheme.ButtonStyle style = active ? AuraTheme.ButtonStyle.Primary : AuraTheme.ButtonStyle.Ghost;
            Color fill, text, accent;
            AuraTheme.GetButtonColors(style, out fill, out text, out accent);

            Image img = btn.GetComponent<Image>();
            if (img != null) img.color = fill;

            Transform ring = btn.transform.Find("Stroke");
            if (ring != null)
            {
                Image ringImg = ring.GetComponent<Image>();
                if (ringImg != null) ringImg.color = accent;
            }

            TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = text;
        }

        private static string RandomRoomCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            System.Random rand = new System.Random();
            char[] codeChars = new char[4];
            for (int i = 0; i < 4; i++)
            {
                codeChars[i] = chars[rand.Next(chars.Length)];
            }
            return new string(codeChars);
        }

        private void GenerateNewHostRoom()
        {
            // Abandon the previous room we were hosting so it doesn't linger on the server
            if (isHost && !string.IsNullOrEmpty(AuraNet.Token) && !string.IsNullOrEmpty(activeRoomCode))
            {
                StartCoroutine(AuraNet.Leave(serverBaseUrl, activeRoomCode, 0));
            }
            activeRoomCode = RandomRoomCode();

            if (hostRoomCodeText != null) hostRoomCodeText.text = activeRoomCode;
            if (hostStatusText != null) hostStatusText.text = "Registering room on server...";
            if (hostStartGameBtn != null) hostStartGameBtn.interactable = true;

            isHost = true;
            mySlotIndex = 0;

            string myChefName = PlayerPrefs.GetString("Aura_PlayerName", AuraTheme.ChefNames[0]);
            if (Aura3DLobbyShowcase.Instance != null)
            {
                Aura3DLobbyShowcase.Instance.UpdateSlot(0, true, myChefName, true);
                Aura3DLobbyShowcase.Instance.UpdateSlot(1, false, "[OPEN]", false);
                Aura3DLobbyShowcase.Instance.UpdateSlot(2, false, "[OPEN]", false);
                Aura3DLobbyShowcase.Instance.UpdateSlot(3, false, "[OPEN]", false);
            }

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
                bool offline = string.IsNullOrEmpty(AuraNet.Token); // room never registered: local fallback
                if (!success && !offline)
                {
                    NetResponse failed = AuraNet.Parse(msg);
                    string reason = failed != null && !string.IsNullOrEmpty(failed.error) ? failed.error : "server unreachable, please try again.";
                    if (hostStartGameBtn != null) hostStartGameBtn.interactable = true;
                    if (hostStatusText != null) hostStatusText.text = "Couldn't start the shift: " + reason;
                    return;
                }

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

            if (joinStatusText != null) joinStatusText.text = "Connecting to squad room...";
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
                NetResponse joined = AuraNet.Parse(response);
                mySlotIndex = joined != null ? Mathf.Clamp(joined.slot, 0, 3) : 1;

                SwitchTab("guest_waiting");
                if (hostRoomCodeText != null) hostRoomCodeText.text = activeRoomCode;

                Update3DShowcaseFromJSON(response);

                if (roomPollCoroutine != null) StopCoroutine(roomPollCoroutine);
                roomPollCoroutine = StartCoroutine(GuestPollRoomState());
            }));
        }

        private void OnLeaveRoomClicked()
        {
            isWaitingInRoom = false;
            if (roomPollCoroutine != null) StopCoroutine(roomPollCoroutine);
            StartCoroutine(PostRoomLeave(activeRoomCode, mySlotIndex));
            SwitchTab("host");
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
                        Update3DShowcaseFromJSON(json);
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
                    guestWaitingStatusText.text = $"WAITING FOR HOST TO START SHIFT{dotStr}";
                }

                yield return new WaitForSeconds(1.0f);

                yield return StartCoroutine(GetRoomState(activeRoomCode, (success, json) =>
                {
                    if (!success)
                    {
                        if (!string.IsNullOrEmpty(json) && json.Contains("Room not found"))
                        {
                            LeaveRoomAfterKick("The room was closed by the host.");
                        }
                        return;
                    }

                    NetResponse state = AuraNet.Parse(json);
                    NetPlayer[] roster = AuraNet.GetPlayers(state);
                    if (roster != null && !RosterHasSlot(roster, mySlotIndex))
                    {
                        LeaveRoomAfterKick("You were removed from the room.");
                        return;
                    }

                    Update3DShowcaseFromJSON(json);

                    if (state != null && state.isPlaying)
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

        private static bool RosterHasSlot(NetPlayer[] roster, int slot)
        {
            for (int i = 0; i < roster.Length; i++)
            {
                if (roster[i] != null && roster[i].slot == slot) return true;
            }
            return false;
        }

        private void LeaveRoomAfterKick(string message)
        {
            isWaitingInRoom = false;
            SwitchTab("join");
            if (joinStatusText != null) joinStatusText.text = message;
        }

        private void Update3DShowcaseFromJSON(string json)
        {
            if (string.IsNullOrEmpty(json) || Aura3DLobbyShowcase.Instance == null) return;

            NetPlayer[] roster = AuraNet.GetPlayers(AuraNet.Parse(json));
            if (roster == null) return;

            bool[] present = new bool[4];
            for (int i = 0; i < roster.Length; i++)
            {
                NetPlayer np = roster[i];
                if (np == null || np.slot < 0 || np.slot > 3) continue;
                present[np.slot] = true;
                string pName = string.IsNullOrEmpty(np.name) ? AuraTheme.ChefName(np.slot) : np.name;
                Aura3DLobbyShowcase.Instance.UpdateSlot(np.slot, true, pName, true);
            }
            for (int slot = 0; slot < 4; slot++)
            {
                if (!present[slot]) Aura3DLobbyShowcase.Instance.UpdateSlot(slot, false, "[OPEN]", false);
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
            AuraNet.Token = "";

            for (int attempt = 0; attempt < 4; attempt++)
            {
                NetResult res = null;
                yield return AuraNet.Create(serverBaseUrl, code, hostName, r => res = r);

                if (res != null && res.ok)
                {
                    AuraNet.Token = res.data.token;
                    if (!string.IsNullOrEmpty(res.data.code)) activeRoomCode = res.data.code;
                    if (hostRoomCodeText != null) hostRoomCodeText.text = activeRoomCode;
                    if (hostStatusText != null) hostStatusText.text = "Room ready on server! Share the 4-letter code with your squad.";
                    yield break;
                }

                if (res != null && res.status == 409)
                {
                    // Someone else is using this code right now: pick another one and retry
                    code = RandomRoomCode();
                    activeRoomCode = code;
                    if (hostRoomCodeText != null) hostRoomCodeText.text = code;
                    continue;
                }
                break;
            }

            if (hostStatusText != null)
            {
                hostStatusText.text = "Offline fallback: server unreachable, but room ready locally.";
            }
        }

        private IEnumerator PostRoomJoin(string code, string playerName, Action<bool, string> onComplete)
        {
            NetResult res = null;
            yield return AuraNet.Join(serverBaseUrl, code, playerName, r => res = r);

            bool ok = res != null && res.ok;
            if (ok) AuraNet.Token = res.data.token;
            string text = res == null ? "no response" : (!string.IsNullOrEmpty(res.text) ? res.text : res.error);
            if (onComplete != null) onComplete(ok, text);
        }

        private IEnumerator GetRoomState(string code, Action<bool, string> onComplete)
        {
            NetResult res = null;
            yield return AuraNet.GetRoom(serverBaseUrl, code, mySlotIndex, r => res = r);

            bool ok = res != null && res.ok;
            string text = res == null ? "no response" : (!string.IsNullOrEmpty(res.text) ? res.text : res.error);
            if (onComplete != null) onComplete(ok, text);
        }

        private IEnumerator PostRoomStart(string code, Action<bool, string> onComplete)
        {
            NetResult res = null;
            yield return AuraNet.Start(serverBaseUrl, code, 0, r => res = r);

            bool ok = res != null && res.ok;
            string text = res == null ? "no response" : (!string.IsNullOrEmpty(res.text) ? res.text : res.error);
            if (onComplete != null) onComplete(ok, text);
        }

        private IEnumerator PostRoomLeave(string code, int slot)
        {
            yield return AuraNet.Leave(serverBaseUrl, code, slot);
        }

        // ==================== PROCEDURAL UI GENERATION UTILITIES ====================

        private GameObject CreateStyledButton(GameObject parent, string name, string label, Vector2 pos, Vector2 size, Color bgColor, Color textColor, Action onClick = null, float fontSize = 18f)
        {
            AuraTheme.ButtonStyle style = AuraTheme.StyleFromColors(bgColor, textColor);
            return AuraTheme.CreateButton(parent.transform, name, label, pos, size, style, onClick, fontSize);
        }

        private TextMeshProUGUI CreateTMPText(GameObject parent, string text, float size, FontStyles style, Vector2 pos, Vector2 dimensions, Color color)
        {
            return AuraTheme.CreateText(parent.transform, text, size, style, pos, dimensions, color);
        }
    }
}
