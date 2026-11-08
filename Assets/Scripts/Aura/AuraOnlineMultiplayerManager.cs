using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

namespace AuraKitchen
{
    public class AuraOnlineMultiplayerManager : MonoBehaviour
    {
        public static AuraOnlineMultiplayerManager Instance { get; private set; }

        [Header("Backend")]
        [SerializeField] private string serverBaseUrl = "https://aura.dexster.site";

        public bool IsOnlineActive { get; private set; } = false;
        public string ActiveRoomCode { get; private set; } = "";
        public int MySlotIndex { get; private set; } = 0;

        private class RemoteChefData
        {
            public int slot;
            public string name;
            public Player playerComponent;
            public Vector3 targetPos;
            public Quaternion targetRot;
            public bool isWalking;
            public int dishes;
            public int score;
        }

        private Dictionary<int, RemoteChefData> remoteChefs = new Dictionary<int, RemoteChefData>();
        private Player localPlayer;
        private Coroutine syncCoroutine;
        private bool isSyncing = false;

        // Challenge Scoreboard UI
        private GameObject scoreboardCanvasObj;
        private TextMeshProUGUI scoreboardText;
        private RectTransform scoreboardRect;
        private readonly ScoreRow[] scoreRows = new ScoreRow[4];
        private const float ScoreboardWidth = 400f;
        private const float ScoreboardHeaderH = 52f;
        private const float ScoreboardRowH = 46f;

        private class ScoreRow
        {
            public GameObject root;
            public Image bg;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI statText;
            public GameObject youPill;
        }
        private TextMeshProUGUI announcementText;
        private Coroutine announceCoroutine;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            int mode = PlayerPrefs.GetInt("Aura_GameMode", 0);
            if (mode != (int)MultiplayerManager.Mode.OnlineCoop)
            {
                enabled = false;
                return;
            }

            IsOnlineActive = true;
            ActiveRoomCode = PlayerPrefs.GetString("Aura_RoomCode", "AURA").ToUpper().Trim();
            MySlotIndex = PlayerPrefs.GetInt("Aura_PlayerSlot", 0);

            // Disable SoloChefController in OnlineCoop so local player doesn't switch away
            if (SoloChefController.Instance != null)
            {
                SoloChefController.Instance.enabled = false;
            }

            localPlayer = Player.Instance;
            if (localPlayer != null)
            {
                Vector3[] defaultSpawns = { new Vector3(-2.5f, 0, 0), new Vector3(2.5f, 0, 0), new Vector3(-2.5f, 0, 2f), new Vector3(2.5f, 0, 2f) };
                localPlayer.transform.position = defaultSpawns[Mathf.Clamp(MySlotIndex, 0, 3)];
                localPlayer.transform.rotation = Quaternion.identity;
                localPlayer.SetAsLocalInstance();

                PlayerAuraVisual vis = localPlayer.GetComponent<PlayerAuraVisual>();
                if (vis == null) vis = localPlayer.gameObject.AddComponent<PlayerAuraVisual>();
                string myChefName = PlayerPrefs.GetString("Aura_PlayerName", "");
                if (string.IsNullOrEmpty(myChefName))
                {
                    string[] chefNames = AuraTheme.ChefNames;
                    myChefName = chefNames[Mathf.Clamp(MySlotIndex, 0, 3)];
                }
                vis.SetupPlayerVisual(MySlotIndex, myChefName);
            }

            CreateScoreboardUI();

            // Hook delivery
            if (DeliveryManager.Instance != null)
            {
                DeliveryManager.Instance.OnOrderDelivered += OnLocalOrderDelivered;
            }

            // Start room setup and continuous position sync
            StartCoroutine(SetupRoomAndStartSync());
        }

        private void OnDestroy()
        {
            if (DeliveryManager.Instance != null)
            {
                DeliveryManager.Instance.OnOrderDelivered -= OnLocalOrderDelivered;
            }
            if (syncCoroutine != null) StopCoroutine(syncCoroutine);
            IsOnlineActive = false;
        }

        private void Update()
        {
            if (!IsOnlineActive) return;

            // Smoothly interpolate remote players' positions & rotations
            foreach (var kvp in remoteChefs)
            {
                RemoteChefData data = kvp.Value;
                if (data.playerComponent != null)
                {
                    Transform t = data.playerComponent.transform;
                    t.position = Vector3.Lerp(t.position, data.targetPos, Time.deltaTime * 14f);
                    t.rotation = Quaternion.Slerp(t.rotation, data.targetRot, Time.deltaTime * 14f);
                    data.playerComponent.SetRemoteWalking(data.isWalking);
                }
            }
        }

        private IEnumerator SetupRoomAndStartSync()
        {
            // 1. Query server to know all players in this room
            yield return StartCoroutine(GetRoomState(ActiveRoomCode, (success, json) =>
            {
                if (success && json.Contains("\"players\":"))
                {
                    ParseAndSpawnRemoteChefs(json);
                }
            }));

            // 2. Start high-frequency sync loop
            syncCoroutine = StartCoroutine(NetworkSyncLoop());
        }

        private IEnumerator NetworkSyncLoop()
        {
            WaitForSeconds wait = new WaitForSeconds(0.12f); // 8.3 Hz network sync (smoother, avoids mobile packet pile-up)
            while (IsOnlineActive)
            {
                if (!isSyncing && localPlayer != null)
                {
                    yield return StartCoroutine(SendSyncRequest());
                }
                yield return wait;
            }
        }

        private IEnumerator SendSyncRequest()
        {
            isSyncing = true;
            string url = $"{serverBaseUrl}/api/room/{ActiveRoomCode}/sync";

            Vector3 pos = localPlayer.transform.position;
            float rotY = localPlayer.transform.eulerAngles.y;
            bool isWalking = localPlayer.IsWalking;
            int dishes = GameManager.Instance != null ? GameManager.Instance.PlateServed : 0;
            int score = AuraGameMechanics.Instance != null ? AuraGameMechanics.Instance.CurrentScore : 0;

            string json = $"{{\"slot\":{MySlotIndex},\"x\":{pos.x:F2},\"z\":{pos.z:F2},\"rotY\":{rotY:F1},\"isWalking\":{(isWalking ? "true" : "false")},\"dishes\":{dishes},\"score\":{score}}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                req.timeout = 3;
                byte[] body = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    UpdateRemoteChefsFromJSON(req.downloadHandler.text);
                }
            }
            isSyncing = false;
        }

        private void OnLocalOrderDelivered(object sender, MenuRecipeSO recipe)
        {
            if (!IsOnlineActive) return;
            string dishName = recipe != null ? recipe.recipeName : "Dish";
            int pts = 100 * (AuraGameMechanics.Instance != null ? AuraGameMechanics.ScoreMultiplier : 1);
            StartCoroutine(PostOrderDelivery(dishName, pts));
        }

        private IEnumerator PostOrderDelivery(string dishName, int pts)
        {
            string url = $"{serverBaseUrl}/api/room/{ActiveRoomCode}/deliver";
            string json = $"{{\"slot\":{MySlotIndex},\"dishName\":\"{dishName}\",\"earnedPoints\":{pts}}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                req.timeout = 4;
                byte[] body = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
            }
        }

        private void ParseAndSpawnRemoteChefs(string json)
        {
            string[] chefNames = { "Chef Ruby (P1)", "Chef Sapphire (P2)", "Chef Emerald (P3)", "Chef Amber (P4)" };
            Vector3[] defaultSpawns = { new Vector3(-2.5f, 0, 0), new Vector3(2.5f, 0, 0), new Vector3(-2.5f, 0, 2f), new Vector3(2.5f, 0, 2f) };

            for (int slot = 0; slot < 4; slot++)
            {
                if (slot == MySlotIndex) continue; // Skip local player

                // Check if slot exists in room json
                if (json.Contains($"\"slot\":{slot}"))
                {
                    if (!remoteChefs.ContainsKey(slot))
                    {
                        // Spawn remote chef clone
                        GameObject clone = Instantiate(localPlayer.gameObject, defaultSpawns[slot], Quaternion.identity);
                        clone.name = $"RemotePlayer_P{slot + 1}_{chefNames[slot]}";
                        Player rPlayer = clone.GetComponent<Player>();
                        rPlayer.isRemotePlayer = true;

                        // CRITICAL: Re-assert localPlayer as Player.Instance so touch controls never get stolen
                        if (localPlayer != null)
                        {
                            localPlayer.SetAsLocalInstance();
                        }

                        // Extract custom name if present
                        string slotName = chefNames[slot];
                        int sIdx = json.IndexOf($"\"slot\":{slot}");
                        if (sIdx >= 0)
                        {
                            int nIdx = json.IndexOf("\"name\":\"", sIdx);
                            if (nIdx >= 0 && nIdx < sIdx + 120)
                            {
                                int eIdx = json.IndexOf("\"", nIdx + 8);
                                if (eIdx > nIdx + 8)
                                {
                                    slotName = json.Substring(nIdx + 8, eIdx - (nIdx + 8));
                                }
                            }
                        }

                        // Ensure visual setup
                        PlayerAuraVisual vis = clone.GetComponent<PlayerAuraVisual>();
                        if (vis == null) vis = clone.AddComponent<PlayerAuraVisual>();
                        vis.SetupPlayerVisual(slot, slotName);

                        RemoteChefData data = new RemoteChefData
                        {
                            slot = slot,
                            name = slotName,
                            playerComponent = rPlayer,
                            targetPos = defaultSpawns[slot],
                            targetRot = Quaternion.identity,
                            isWalking = false,
                            dishes = 0,
                            score = 0
                        };
                        remoteChefs[slot] = data;
                        Debug.Log($"<color=cyan>[Multiplayer]</color> Spawned remote chef: {data.name}");
                    }
                }
            }
            UpdateScoreboardDisplay();
        }

        private void UpdateRemoteChefsFromJSON(string json)
        {
            if (string.IsNullOrEmpty(json) || !json.Contains("\"players\":")) return;

            // Check if any new chef joined
            for (int slot = 0; slot < 4; slot++)
            {
                if (slot == MySlotIndex) continue;

                if (json.Contains($"\"slot\":{slot}"))
                {
                    if (!remoteChefs.ContainsKey(slot))
                    {
                        ParseAndSpawnRemoteChefs(json);
                        return;
                    }

                    // Extract coordinates
                    RemoteChefData data = remoteChefs[slot];
                    int slotIdx = json.IndexOf($"\"slot\":{slot}");
                    if (slotIdx >= 0)
                    {
                        data.targetPos.x = ExtractFloat(json, "\"x\":", slotIdx, data.targetPos.x);
                        data.targetPos.z = ExtractFloat(json, "\"z\":", slotIdx, data.targetPos.z);
                        float rotY = ExtractFloat(json, "\"rotY\":", slotIdx, data.targetRot.eulerAngles.y);
                        data.targetRot = Quaternion.Euler(0, rotY, 0);

                        int wIdx = json.IndexOf("\"isWalking\":", slotIdx);
                        if (wIdx >= 0 && wIdx < slotIdx + 150)
                        {
                            data.isWalking = json.Substring(wIdx + 12, 4) == "true";
                        }

                        data.dishes = (int)ExtractFloat(json, "\"dishes\":", slotIdx, data.dishes);
                        data.score = (int)ExtractFloat(json, "\"score\":", slotIdx, data.score);
                    }
                }
            }
            UpdateScoreboardDisplay();
        }

        private float ExtractFloat(string str, string key, int searchStart, float fallback)
        {
            int idx = str.IndexOf(key, searchStart);
            if (idx >= 0 && idx < searchStart + 160)
            {
                int valStart = idx + key.Length;
                int valEnd = valStart;
                while (valEnd < str.Length && (char.IsDigit(str[valEnd]) || str[valEnd] == '.' || str[valEnd] == '-'))
                {
                    valEnd++;
                }
                if (valEnd > valStart && float.TryParse(str.Substring(valStart, valEnd - valStart), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float res))
                {
                    return res;
                }
            }
            return fallback;
        }

        private void CreateScoreboardUI()
        {
            scoreboardCanvasObj = new GameObject("Aura_OnlineChallenge_Canvas");
            Canvas canvas = scoreboardCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 480;

            CanvasScaler scaler = scoreboardCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Glass scoreboard (Top-Right, clear of orders on top-left)
            GameObject board = AuraTheme.CreatePanel(scoreboardCanvasObj.transform, "Challenge_Scoreboard",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -84f),
                new Vector2(ScoreboardWidth, ScoreboardHeaderH + ScoreboardRowH * 4 + 10f),
                AuraTheme.PanelFill, AuraTheme.Stroke);
            board.GetComponent<Image>().raycastTarget = false;
            scoreboardRect = board.GetComponent<RectTransform>();

            // Header: trophy + title + room code
            Image trophy = AuraTheme.CreateIcon(board.transform, AuraIcons.Get(AuraIcon.Trophy), Vector2.zero, 26f, AuraTheme.Gold);
            AuraTheme.PlaceTopLeft(trophy.rectTransform, 16f, 13f, 26f, 26f);

            TextMeshProUGUI title = AuraTheme.CreateText(board.transform, "KITCHEN CLASH", 17f, FontStyles.Bold,
                Vector2.zero, Vector2.zero, AuraTheme.Cyan, TextAlignmentOptions.Left);
            AuraTheme.PlaceTopLeft(title.rectTransform, 50f, 10f, 190f, 32f);

            scoreboardText = AuraTheme.CreateText(board.transform, "", 15f, FontStyles.Bold,
                Vector2.zero, Vector2.zero, AuraTheme.TextSecondary, TextAlignmentOptions.Right);
            AuraTheme.PlaceTopLeft(scoreboardText.rectTransform, ScoreboardWidth - 16f - 130f, 10f, 130f, 32f);

            for (int i = 0; i < 4; i++)
            {
                ScoreRow row = new ScoreRow();
                Color chefColor = AuraTheme.ChefColor(i);

                GameObject rowObj = AuraTheme.CreatePanel(board.transform, "Row_" + i,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                    new Vector2(ScoreboardWidth - 20f, ScoreboardRowH - 6f),
                    AuraTheme.PanelRaised, Color.clear, false);
                rowObj.GetComponent<Image>().raycastTarget = false;
                row.root = rowObj;
                row.bg = rowObj.GetComponent<Image>();

                Image bar = AuraTheme.CreateIcon(rowObj.transform, AuraTheme.RoundedFill, Vector2.zero, 0f, chefColor);
                bar.type = Image.Type.Sliced;
                AuraTheme.PlaceTopLeft(bar.rectTransform, 0f, 0f, 6f, ScoreboardRowH - 6f);

                Image hat = AuraTheme.CreateIcon(rowObj.transform, AuraIcons.Get(AuraIcon.ChefHat), Vector2.zero, 24f, chefColor);
                AuraTheme.PlaceTopLeft(hat.rectTransform, 16f, 8f, 24f, 24f);

                row.nameText = AuraTheme.CreateText(rowObj.transform, "", 16f, FontStyles.Bold,
                    Vector2.zero, Vector2.zero, AuraTheme.TextPrimary, TextAlignmentOptions.Left);
                row.nameText.richText = false;
                row.nameText.overflowMode = TextOverflowModes.Ellipsis;
                AuraTheme.PlaceTopLeft(row.nameText.rectTransform, 48f, 0f, 112f, ScoreboardRowH - 6f);

                GameObject pill = AuraTheme.CreatePanel(rowObj.transform, "YouPill",
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                    new Vector2(38f, 20f), chefColor, Color.clear, false);
                pill.GetComponent<Image>().raycastTarget = false;
                AuraTheme.PlaceTopLeft(pill.GetComponent<RectTransform>(), 164f, 10f, 38f, 20f);
                TextMeshProUGUI pillText = AuraTheme.CreateText(pill.transform, "YOU", 11f, FontStyles.Bold,
                    Vector2.zero, Vector2.zero, new Color(0.04f, 0.06f, 0.1f, 1f));
                pillText.rectTransform.anchorMin = Vector2.zero;
                pillText.rectTransform.anchorMax = Vector2.one;
                pillText.rectTransform.offsetMin = Vector2.zero;
                pillText.rectTransform.offsetMax = Vector2.zero;
                row.youPill = pill;

                row.statText = AuraTheme.CreateText(rowObj.transform, "", 14f, FontStyles.Normal,
                    Vector2.zero, Vector2.zero, AuraTheme.TextPrimary, TextAlignmentOptions.Right);
                AuraTheme.PlaceTopLeft(row.statText.rectTransform, 206f, 0f, ScoreboardWidth - 20f - 206f - 12f, ScoreboardRowH - 6f);

                scoreRows[i] = row;
            }

            UpdateScoreboardDisplay();
        }

        private void UpdateScoreboardDisplay()
        {
            if (scoreboardText == null || scoreRows[0] == null) return;

            int myDishes = GameManager.Instance != null ? GameManager.Instance.PlateServed : 0;
            int myScore = AuraGameMechanics.Instance != null ? AuraGameMechanics.Instance.CurrentScore : 0;

            scoreboardText.text = "ROOM " + ActiveRoomCode;

            int visible = 0;
            for (int i = 0; i < 4; i++)
            {
                ScoreRow row = scoreRows[i];
                bool isMe = i == MySlotIndex;
                RemoteChefData data = null;
                bool has = isMe || remoteChefs.TryGetValue(i, out data);
                row.root.SetActive(has);
                if (!has) continue;

                string chefName;
                int dishes;
                int score;
                if (isMe)
                {
                    chefName = PlayerPrefs.GetString("Aura_PlayerName", "");
                    if (string.IsNullOrEmpty(chefName)) chefName = AuraTheme.ChefName(i);
                    dishes = myDishes;
                    score = myScore;
                }
                else
                {
                    chefName = !string.IsNullOrEmpty(data.name) ? data.name : AuraTheme.ChefName(i);
                    dishes = data.dishes;
                    score = data.score;
                }

                RectTransform rrt = row.root.GetComponent<RectTransform>();
                rrt.anchoredPosition = new Vector2(10f, -(ScoreboardHeaderH + visible * ScoreboardRowH));
                visible++;

                row.nameText.text = chefName;
                row.statText.text = "<b>" + dishes + "</b> dishes  <color=" + AuraTheme.Hex(AuraTheme.TextSecondary) + ">" + score.ToString("N0") + " pts</color>";
                row.youPill.SetActive(isMe);
                row.bg.color = isMe ? AuraTheme.WithAlpha(AuraTheme.ChefColor(i), 0.24f) : AuraTheme.PanelRaised;
            }

            if (scoreboardRect != null)
            {
                scoreboardRect.sizeDelta = new Vector2(ScoreboardWidth, ScoreboardHeaderH + visible * ScoreboardRowH + 10f);
            }
        }

        public string GetChallengeWinnerSummary()
        {
            int myDishes = GameManager.Instance != null ? GameManager.Instance.PlateServed : 0;
            int myScore = AuraGameMechanics.Instance != null ? AuraGameMechanics.Instance.CurrentScore : 0;

            string myName = PlayerPrefs.GetString("Aura_PlayerName", "");
            if (string.IsNullOrEmpty(myName))
            {
                myName = AuraTheme.ChefName(MySlotIndex);
            }

            string bestChef = myName;
            int maxDishes = myDishes;
            int maxScore = myScore;
            bool isTie = false;

            foreach (var kvp in remoteChefs)
            {
                RemoteChefData r = kvp.Value;
                if (r.dishes > maxDishes)
                {
                    maxDishes = r.dishes;
                    maxScore = r.score;
                    bestChef = r.name;
                    isTie = false;
                }
                else if (r.dishes == maxDishes && maxDishes > 0)
                {
                    isTie = true;
                }
            }

            if (isTie)
            {
                return "<color=" + AuraTheme.Hex(AuraTheme.Gold) + ">TIE MATCH</color> - incredible teamwork!";
            }
            return "<color=" + AuraTheme.Hex(AuraTheme.Gold) + ">CHALLENGE WINNER</color> " + bestChef + " - " + maxDishes + " dishes delivered!";
        }

        public void ReturnToLobby()
        {
            StartCoroutine(ResetAndReturnToLobby());
        }

        private IEnumerator ResetAndReturnToLobby()
        {
            string url = $"{serverBaseUrl}/api/room/{ActiveRoomCode}/reset";
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                req.timeout = 6;
                req.downloadHandler = new DownloadHandlerBuffer();
                yield return req.SendWebRequest();
            }

            // Flag so MainMenu automatically re-opens the active room lobby
            PlayerPrefs.SetInt("Aura_ReturnToLobby", 1);
            SceneSwitcher.LoadScene(SceneSwitcher.Scene.MainMenu);
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
    }
}

