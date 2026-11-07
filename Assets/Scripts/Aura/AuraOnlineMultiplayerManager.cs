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
                    string[] chefNames = { "Chef Ruby", "Chef Sapphire", "Chef Emerald", "Chef Amber" };
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

            // Scoreboard Container (Top-Right, completely clear of orders on top-left)
            GameObject board = new GameObject("Challenge_Scoreboard");
            board.transform.SetParent(scoreboardCanvasObj.transform, false);
            RectTransform brt = board.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(1f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(-24, -20);
            brt.sizeDelta = new Vector2(360, 110);

            Image bImg = board.AddComponent<Image>();
            bImg.color = new Color(0.12f, 0.12f, 0.12f, 0.88f);
            bImg.raycastTarget = false;

            Outline bOutline = board.AddComponent<Outline>();
            bOutline.effectColor = new Color(0.35f, 0.35f, 0.35f, 0.8f);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(board.transform, false);
            RectTransform trt = textObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(12, 6);
            trt.offsetMax = new Vector2(-12, -6);

            scoreboardText = textObj.AddComponent<TextMeshProUGUI>();
            scoreboardText.fontSize = 17;
            scoreboardText.alignment = TextAlignmentOptions.TopLeft;
            scoreboardText.color = Color.white;
            scoreboardText.raycastTarget = false;

            UpdateScoreboardDisplay();
        }

        private void UpdateScoreboardDisplay()
        {
            if (scoreboardText == null) return;

            string[] colors = { "#ff4444", "#3388ff", "#22cc66", "#ffaa00" };
            string[] shortNames = { "Chef Ruby", "Chef Sapphire", "Chef Emerald", "Chef Amber" };

            int myDishes = GameManager.Instance != null ? GameManager.Instance.PlateServed : 0;
            int myScore = AuraGameMechanics.Instance != null ? AuraGameMechanics.Instance.CurrentScore : 0;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<b>ROOM {ActiveRoomCode} - KITCHEN CLASH:</b>");

            for (int i = 0; i < 4; i++)
            {
                if (i == MySlotIndex)
                {
                    sb.AppendLine($"<color={colors[i]}>▶ {shortNames[i]} (YOU): <b>{myDishes}</b> dishes ({myScore:N0} pts)</color>");
                }
                else if (remoteChefs.TryGetValue(i, out var data))
                {
                    sb.AppendLine($"<color={colors[i]}>  {shortNames[i]}: <b>{data.dishes}</b> dishes ({data.score:N0} pts)</color>");
                }
            }

            scoreboardText.text = sb.ToString();
        }

        public string GetChallengeWinnerSummary()
        {
            int myDishes = GameManager.Instance != null ? GameManager.Instance.PlateServed : 0;
            int myScore = AuraGameMechanics.Instance != null ? AuraGameMechanics.Instance.CurrentScore : 0;

            string myName = PlayerPrefs.GetString("Aura_PlayerName", "");
            if (string.IsNullOrEmpty(myName))
            {
                string[] defaultChefNames = { "Chef Ruby", "Chef Sapphire", "Chef Emerald", "Chef Amber" };
                myName = defaultChefNames[Mathf.Clamp(MySlotIndex, 0, 3)];
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
                return "[TIE MATCH] INCREDIBLE TEAMWORK!";
            }
            return $"[CHALLENGE WINNER] {bestChef} ({maxDishes} Dishes Delivered)!";
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

