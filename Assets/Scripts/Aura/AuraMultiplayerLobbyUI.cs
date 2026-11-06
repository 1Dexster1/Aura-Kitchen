using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AuraKitchen
{
    public class AuraMultiplayerLobbyUI : MonoBehaviour
    {
        public static AuraMultiplayerLobbyUI Instance { get; private set; }

        private GameObject lobbyRootCanvas;
        private GameObject mainModalCard;
        private TextMeshProUGUI roomCodeText;
        private TextMeshProUGUI statusMsgText;
        private TextMeshProUGUI playerCountText;
        private TMP_InputField joinCodeInput;

        private GameObject hostViewObj;
        private GameObject joinViewObj;
        private GameObject soloViewObj;

        private Button tabHostBtn;
        private Button tabJoinBtn;
        private Button tabSoloBtn;

        private string generatedRoomCode = "AUR4";
        private bool isHost = true;

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
        }

        private void Start()
        {
            // Connect to WebSocket client if available
            if (AuraWebSocketClient.Instance != null)
            {
                AuraWebSocketClient.Instance.OnRoomCreated += (s, code) =>
                {
                    generatedRoomCode = code;
                    if (roomCodeText != null) roomCodeText.text = code;
                    UpdateStatus("✅ Room created! Ready for chefs to join.");
                };

                AuraWebSocketClient.Instance.OnRoomJoined += (s, code) =>
                {
                    UpdateStatus($"✅ Joined room {code} successfully!");
                };

                AuraWebSocketClient.Instance.OnErrorReceived += (s, err) =>
                {
                    UpdateStatus($"⚠️ Server: {err}");
                };
            }

            // Hide on initial load
            HideLobby();
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

            // 1. Persistent Top-Right Rooms Button (Always accessible)
            GameObject topBtnObj = new GameObject("Btn_OpenLobby_Top");
            topBtnObj.transform.SetParent(lobbyRootCanvas.transform, false);
            RectTransform topBtnRect = topBtnObj.AddComponent<RectTransform>();
            topBtnRect.anchorMin = new Vector2(1, 1);
            topBtnRect.anchorMax = new Vector2(1, 1);
            topBtnRect.pivot = new Vector2(1, 1);
            topBtnRect.anchoredPosition = new Vector2(-24, -20);
            topBtnRect.sizeDelta = new Vector2(230, 48);

            Image topBtnImg = topBtnObj.AddComponent<Image>();
            topBtnImg.color = new Color(0.12f, 0.16f, 0.28f, 0.9f);

            Button topBtn = topBtnObj.AddComponent<Button>();
            topBtn.onClick.AddListener(ShowLobby);

            GameObject topBtnTextObj = new GameObject("Text");
            topBtnTextObj.transform.SetParent(topBtnObj.transform, false);
            RectTransform tbtr = topBtnTextObj.AddComponent<RectTransform>();
            tbtr.anchorMin = Vector2.zero;
            tbtr.anchorMax = Vector2.one;
            tbtr.sizeDelta = Vector2.zero;

            TextMeshProUGUI tbTmp = topBtnTextObj.AddComponent<TextMeshProUGUI>();
            tbTmp.text = "🌐 MULTIPLAYER ROOMS";
            tbTmp.fontSize = 15;
            tbTmp.fontStyle = FontStyles.Bold;
            tbTmp.alignment = TextAlignmentOptions.Center;
            tbTmp.color = new Color(0f, 0.95f, 1f);

            // 2. Dim Backdrop
            GameObject backdropObj = new GameObject("Modal_Backdrop");
            backdropObj.transform.SetParent(lobbyRootCanvas.transform, false);
            RectTransform bdRect = backdropObj.AddComponent<RectTransform>();
            bdRect.anchorMin = Vector2.zero;
            bdRect.anchorMax = Vector2.one;
            bdRect.sizeDelta = Vector2.zero;

            Image bdImg = backdropObj.AddComponent<Image>();
            bdImg.color = new Color(0.04f, 0.06f, 0.1f, 0.88f);

            // 3. Modal Card
            mainModalCard = new GameObject("Modal_Card");
            mainModalCard.transform.SetParent(backdropObj.transform, false);
            RectTransform cardRect = mainModalCard.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(800, 560);

            Image cardImg = mainModalCard.AddComponent<Image>();
            cardImg.color = new Color(0.08f, 0.11f, 0.18f, 0.98f);

            // Card Title
            CreateTMPText(mainModalCard, "AURA KITCHEN - MULTIPLAYER", 24, FontStyles.Bold,
                new Vector2(0, 240), new Vector2(700, 40), new Color(0f, 0.95f, 1f));

            // Backend Indicator
            CreateTMPText(mainModalCard, "🟢 Server: panel.sharebotdex.xyz:30015", 13, FontStyles.Normal,
                new Vector2(0, 205), new Vector2(600, 24), new Color(0f, 1f, 0.65f));

            // Close button (X)
            CreateButton(mainModalCard, "Btn_Close", "✕", new Vector2(360, 240), new Vector2(40, 40),
                new Color(0.3f, 0.1f, 0.15f, 0.9f), HideLobby);

            // Tabs Row
            tabHostBtn = CreateButton(mainModalCard, "Tab_Host", "👑 Host Room", new Vector2(-240, 160), new Vector2(220, 46),
                new Color(0.15f, 0.45f, 0.85f), () => SwitchTab("host")).GetComponent<Button>();

            tabJoinBtn = CreateButton(mainModalCard, "Tab_Join", "🔑 Join with Code", new Vector2(0, 160), new Vector2(220, 46),
                new Color(0.2f, 0.25f, 0.35f), () => SwitchTab("join")).GetComponent<Button>();

            tabSoloBtn = CreateButton(mainModalCard, "Tab_Solo", "👨‍🍳 Solo Offline", new Vector2(240, 160), new Vector2(220, 46),
                new Color(0.2f, 0.25f, 0.35f), () => SwitchTab("solo")).GetComponent<Button>();

            // --- TAB CONTENT 1: HOST ---
            hostViewObj = new GameObject("View_Host");
            hostViewObj.transform.SetParent(mainModalCard.transform, false);
            RectTransform hvr = hostViewObj.AddComponent<RectTransform>();
            hvr.anchoredPosition = Vector2.zero;

            CreateTMPText(hostViewObj, "SHARE THIS ROOM CODE WITH CHEFS:", 13, FontStyles.Bold,
                new Vector2(0, 95), new Vector2(600, 26), new Color(0.7f, 0.8f, 0.9f));

            // Room code display box
            GameObject codeBox = CreateButton(hostViewObj, "Code_Display", generatedRoomCode, new Vector2(-80, 40), new Vector2(220, 56),
                new Color(0.04f, 0.2f, 0.25f), CopyRoomCode);
            roomCodeText = codeBox.GetComponentInChildren<TextMeshProUGUI>();
            roomCodeText.fontSize = 28;
            roomCodeText.fontStyle = FontStyles.Bold;
            roomCodeText.color = new Color(0f, 1f, 0.7f);

            CreateButton(hostViewObj, "Btn_CopyCode", "📋 COPY CODE", new Vector2(100, 40), new Vector2(140, 50),
                new Color(0.1f, 0.5f, 0.7f), CopyRoomCode);

            // Chef Slots
            CreateTMPText(hostViewObj, "CHEFS IN ROOM (1/4):", 12, FontStyles.Bold,
                new Vector2(0, -10), new Vector2(600, 22), new Color(0.6f, 0.7f, 0.85f));

            CreateButton(hostViewObj, "Slot_P1", "🔴 Ruby (You/Host)", new Vector2(-240, -45), new Vector2(150, 36), new Color(0.6f, 0.1f, 0.15f), null);
            CreateButton(hostViewObj, "Slot_P2", "🔵 Sapphire (Open)", new Vector2(-80, -45), new Vector2(150, 36), new Color(0.1f, 0.25f, 0.45f), null);
            CreateButton(hostViewObj, "Slot_P3", "🟢 Emerald (Open)", new Vector2(80, -45), new Vector2(150, 36), new Color(0.1f, 0.4f, 0.25f), null);
            CreateButton(hostViewObj, "Slot_P4", "🟡 Amber (Open)", new Vector2(240, -45), new Vector2(150, 36), new Color(0.5f, 0.35f, 0.1f), null);

            CreateButton(hostViewObj, "Btn_StartHost", "🚀 START KITCHEN SHIFT", new Vector2(0, -130), new Vector2(340, 56),
                new Color(0.1f, 0.75f, 0.4f), StartHostMatch);

            // --- TAB CONTENT 2: JOIN ---
            joinViewObj = new GameObject("View_Join");
            joinViewObj.transform.SetParent(mainModalCard.transform, false);
            RectTransform jvr = joinViewObj.AddComponent<RectTransform>();
            jvr.anchoredPosition = Vector2.zero;
            joinViewObj.SetActive(false);

            CreateTMPText(joinViewObj, "ENTER THE 4-LETTER ROOM CODE:", 14, FontStyles.Bold,
                new Vector2(0, 80), new Vector2(600, 26), new Color(0.7f, 0.8f, 0.9f));

            // Join input field
            GameObject inputObj = new GameObject("Input_RoomCode");
            inputObj.transform.SetParent(joinViewObj.transform, false);
            RectTransform ipRect = inputObj.AddComponent<RectTransform>();
            ipRect.anchoredPosition = new Vector2(0, 20);
            ipRect.sizeDelta = new Vector2(280, 54);

            Image ipImg = inputObj.AddComponent<Image>();
            ipImg.color = new Color(0.04f, 0.08f, 0.14f);

            joinCodeInput = inputObj.AddComponent<TMP_InputField>();
            joinCodeInput.characterLimit = 6;
            joinCodeInput.contentType = TMP_InputField.ContentType.Alphanumeric;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(inputObj.transform, false);
            RectTransform tr = textObj.AddComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.sizeDelta = Vector2.zero;
            TextMeshProUGUI ipTmp = textObj.AddComponent<TextMeshProUGUI>();
            ipTmp.fontSize = 24;
            ipTmp.alignment = TextAlignmentOptions.Center;
            ipTmp.color = Color.white;
            joinCodeInput.textComponent = ipTmp;

            CreateButton(joinViewObj, "Btn_JoinSubmit", "🤝 JOIN KITCHEN ROOM", new Vector2(0, -60), new Vector2(300, 54),
                new Color(0.15f, 0.65f, 0.95f), JoinRoomSubmit);

            // --- TAB CONTENT 3: SOLO ---
            soloViewObj = new GameObject("View_Solo");
            soloViewObj.transform.SetParent(mainModalCard.transform, false);
            RectTransform svr = soloViewObj.AddComponent<RectTransform>();
            svr.anchoredPosition = Vector2.zero;
            soloViewObj.SetActive(false);

            CreateTMPText(soloViewObj, "SOLO OFFLINE SHIFT", 16, FontStyles.Bold,
                new Vector2(0, 70), new Vector2(600, 30), new Color(1f, 0.85f, 0.2f));

            CreateTMPText(soloViewObj, "Play offline with full control of 2 chefs,\nTab/Touch switching, and Sous-Chef assistant!", 14, FontStyles.Normal,
                new Vector2(0, 15), new Vector2(600, 50), new Color(0.75f, 0.85f, 0.95f));

            CreateButton(soloViewObj, "Btn_StartSolo", "👨‍🍳 START SOLO SHIFT", new Vector2(0, -70), new Vector2(300, 54),
                new Color(0.9f, 0.5f, 0.15f), StartSoloMatch);

            // Status message
            statusMsgText = CreateTMPText(mainModalCard, "", 12, FontStyles.Normal,
                new Vector2(0, -215), new Vector2(700, 24), new Color(0f, 0.95f, 1f));
        }

        private void SwitchTab(string tab)
        {
            hostViewObj.SetActive(tab == "host");
            joinViewObj.SetActive(tab == "join");
            soloViewObj.SetActive(tab == "solo");

            tabHostBtn.image.color = tab == "host" ? new Color(0.15f, 0.45f, 0.85f) : new Color(0.2f, 0.25f, 0.35f);
            tabJoinBtn.image.color = tab == "join" ? new Color(0.15f, 0.45f, 0.85f) : new Color(0.2f, 0.25f, 0.35f);
            tabSoloBtn.image.color = tab == "solo" ? new Color(0.15f, 0.45f, 0.85f) : new Color(0.2f, 0.25f, 0.35f);

            if (tab == "host")
            {
                isHost = true;
                GenerateNewCode();
            }
        }

        private void GenerateNewCode()
        {
            string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            char[] code = new char[4];
            var rnd = new System.Random();
            for (int i = 0; i < 4; i++) code[i] = chars[rnd.Next(chars.Length)];
            generatedRoomCode = new string(code);
            if (roomCodeText != null) roomCodeText.text = generatedRoomCode;

            // Notify server if connected
            if (AuraWebSocketClient.Instance != null && AuraWebSocketClient.Instance.IsConnected)
            {
                AuraWebSocketClient.Instance.CreateRoom("Chef Ruby");
            }
        }

        private void CopyRoomCode()
        {
            GUIUtility.systemCopyBuffer = generatedRoomCode;
            UpdateStatus($"📋 Code '{generatedRoomCode}' copied to clipboard!");
        }

        private void StartHostMatch()
        {
            PlayerPrefs.SetInt("Aura_GameMode", (int)MultiplayerManager.Mode.OnlineCoop);
            PlayerPrefs.SetString("Aura_RoomCode", generatedRoomCode);
            HideLobby();

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != SceneSwitcher.Scene.GameScene.ToString())
            {
                SceneSwitcher.LoadScene(SceneSwitcher.Scene.GameScene);
            }
        }

        private void JoinRoomSubmit()
        {
            string code = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpper() : "";
            if (string.IsNullOrEmpty(code))
            {
                UpdateStatus("⚠️ Please enter a room code!");
                return;
            }

            UpdateStatus($"Connecting to Room {code}... ⏳");

            if (AuraWebSocketClient.Instance != null)
            {
                AuraWebSocketClient.Instance.JoinRoom(code, "Chef Sapphire");
            }

            PlayerPrefs.SetInt("Aura_GameMode", (int)MultiplayerManager.Mode.OnlineCoop);
            PlayerPrefs.SetString("Aura_RoomCode", code);

            StartCoroutine(DelayEnterGame());
        }

        private IEnumerator DelayEnterGame()
        {
            yield return new WaitForSeconds(0.6f);
            HideLobby();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != SceneSwitcher.Scene.GameScene.ToString())
            {
                SceneSwitcher.LoadScene(SceneSwitcher.Scene.GameScene);
            }
        }

        private void StartSoloMatch()
        {
            PlayerPrefs.SetInt("Aura_GameMode", (int)MultiplayerManager.Mode.SoloSwitch);
            HideLobby();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != SceneSwitcher.Scene.GameScene.ToString())
            {
                SceneSwitcher.LoadScene(SceneSwitcher.Scene.GameScene);
            }
        }

        public void ShowLobby()
        {
            if (lobbyRootCanvas != null)
            {
                lobbyRootCanvas.transform.Find("Modal_Backdrop")?.gameObject.SetActive(true);
            }
        }

        public void HideLobby()
        {
            if (lobbyRootCanvas != null)
            {
                lobbyRootCanvas.transform.Find("Modal_Backdrop")?.gameObject.SetActive(false);
            }
        }

        private void UpdateStatus(string msg)
        {
            if (statusMsgText != null)
            {
                statusMsgText.text = msg;
            }
        }

        private TextMeshProUGUI CreateTMPText(GameObject parent, string text, float size, FontStyles style, Vector2 pos, Vector2 sizeDelta, Color color)
        {
            GameObject obj = new GameObject("Text_" + text.Substring(0, Mathf.Min(8, text.Length)));
            obj.transform.SetParent(parent.transform, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = sizeDelta;

            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private GameObject CreateButton(GameObject parent, string name, string label, Vector2 pos, Vector2 size, Color color, Action onClick)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent.transform, false);
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            img.color = color;

            Button btn = btnObj.AddComponent<Button>();
            if (onClick != null)
            {
                btn.onClick.AddListener(() => onClick.Invoke());
            }

            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform tr = textObj.AddComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 15;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            return btnObj;
        }
    }
}

