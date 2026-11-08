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
            public Vector3 vel;
            public bool snapped;
        }

        private Dictionary<int, RemoteChefData> remoteChefs = new Dictionary<int, RemoteChefData>();
        private Player localPlayer;
        private Coroutine syncCoroutine;

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
            CreateNetUI();

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

            // Critically damped follow: smooth at any network rate, snaps only on teleports
            foreach (var kvp in remoteChefs)
            {
                RemoteChefData data = kvp.Value;
                if (data.playerComponent == null) continue;

                Transform t = data.playerComponent.transform;
                if ((t.position - data.targetPos).sqrMagnitude > 36f)
                {
                    t.position = data.targetPos;
                    data.vel = Vector3.zero;
                }
                else
                {
                    t.position = Vector3.SmoothDamp(t.position, data.targetPos, ref data.vel, 0.1f);
                }
                t.rotation = Quaternion.Slerp(t.rotation, data.targetRot, 1f - Mathf.Exp(-14f * Time.deltaTime));
                data.playerComponent.SetRemoteWalking(data.isWalking);
            }
        }

        // ==================== NETWORK CORE ====================

        private enum NetState { Connected, Reconnecting, Lost }

        private const float SyncInterval = 0.1f;        // 10 Hz; unchanged replies are tiny
        private const float GiveUpAfterSeconds = 25f;   // server drops silent players after 30s

        private NetState netState = NetState.Connected;
        private string myToken = "";
        private bool offlineMode = false;
        private int knownRev = 0;
        private int consecutiveFailures = 0;
        private float lastSuccessTime = 0f;
        private float pingMs = 0f;
        private int deliverSeq = 0;

        private Image pingIcon;
        private TextMeshProUGUI pingLabel;
        private GameObject pingPill;
        private GameObject reconnectBanner;
        private GameObject lostOverlay;
        private TextMeshProUGUI lostReasonText;

        private IEnumerator SetupRoomAndStartSync()
        {
            myToken = AuraNet.Token;
            if (string.IsNullOrEmpty(myToken))
            {
                // Room was never registered on the server (lobby offline fallback): play locally.
                offlineMode = true;
                if (pingPill != null) pingPill.SetActive(false);
                yield break;
            }

            lastSuccessTime = Time.realtimeSinceStartup;

            NetResult first = null;
            yield return AuraNet.GetRoom(serverBaseUrl, ActiveRoomCode, MySlotIndex, r => first = r);
            if (first != null && first.ok) ApplyPlayers(AuraNet.GetPlayers(first.data));

            syncCoroutine = StartCoroutine(NetworkSyncLoop());
        }

        private IEnumerator NetworkSyncLoop()
        {
            while (IsOnlineActive && !offlineMode && netState != NetState.Lost)
            {
                float start = Time.realtimeSinceStartup;
                if (localPlayer != null)
                {
                    yield return SendSyncRequest();
                }
                float spent = Time.realtimeSinceStartup - start;

                // Exponential back-off while the connection is struggling (max ~2s)
                float backoff = consecutiveFailures == 0 ? 0f : Mathf.Min(2f, 0.15f * (1 << Mathf.Min(consecutiveFailures, 4)));
                yield return new WaitForSecondsRealtime(Mathf.Max(0.02f, SyncInterval - spent) + backoff);
            }
        }

        private IEnumerator SendSyncRequest()
        {
            Vector3 pos = localPlayer.transform.position;
            NetSyncBody body = new NetSyncBody
            {
                slot = MySlotIndex,
                token = myToken,
                x = pos.x,
                z = pos.z,
                rotY = localPlayer.transform.eulerAngles.y,
                isWalking = localPlayer.IsWalking,
                rev = knownRev
            };

            NetResult res = null;
            yield return AuraNet.Sync(serverBaseUrl, ActiveRoomCode, body, r => res = r);
            HandleSyncResult(res);
        }

        private void HandleSyncResult(NetResult res)
        {
            if (res == null) return;

            if (res.ok)
            {
                consecutiveFailures = 0;
                lastSuccessTime = Time.realtimeSinceStartup;
                pingMs = pingMs <= 0f ? res.rtt : Mathf.Lerp(pingMs, res.rtt, 0.25f);
                SetNetState(NetState.Connected);

                if (res.data.rev > 0) knownRev = res.data.rev;
                if (res.data.changed && res.data.players != null) ApplyPlayers(res.data.players);
                RefreshPingUI();
                return;
            }

            if (res.status == 403)
            {
                SetLost("You were removed from the room.");
                return;
            }
            if (res.status == 404)
            {
                SetLost("The room was closed.");
                return;
            }

            consecutiveFailures++;
            if (Time.realtimeSinceStartup - lastSuccessTime > GiveUpAfterSeconds)
            {
                SetLost("Connection to the server was lost.");
            }
            else if (consecutiveFailures >= 2)
            {
                SetNetState(NetState.Reconnecting);
            }
        }

        private void SetNetState(NetState state)
        {
            if (netState == NetState.Lost || netState == state) return;
            netState = state;
            if (reconnectBanner != null) reconnectBanner.SetActive(state == NetState.Reconnecting);
        }

        private void SetLost(string reason)
        {
            netState = NetState.Lost;
            if (reconnectBanner != null) reconnectBanner.SetActive(false);
            if (lostReasonText != null) lostReasonText.text = reason;
            if (lostOverlay != null) lostOverlay.SetActive(true);
        }

        private void ContinueOffline()
        {
            offlineMode = true;
            if (lostOverlay != null) lostOverlay.SetActive(false);
            if (pingPill != null) pingPill.SetActive(false);
        }

        private void OnLocalOrderDelivered(object sender, MenuRecipeSO recipe)
        {
            if (!IsOnlineActive || offlineMode || netState == NetState.Lost) return;
            string dishName = recipe != null ? recipe.recipeName : "Dish";
            int pts = 100 * (AuraGameMechanics.Instance != null ? AuraGameMechanics.ScoreMultiplier : 1);
            StartCoroutine(PostOrderDelivery(dishName, pts, ++deliverSeq));
        }

        private IEnumerator PostOrderDelivery(string dishName, int pts, int seq)
        {
            NetDeliverBody body = new NetDeliverBody { slot = MySlotIndex, token = myToken, seq = seq, dishName = dishName, earnedPoints = pts };

            // The server de-duplicates by seq, so retrying after a timeout can never double count.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                NetResult res = null;
                yield return AuraNet.Deliver(serverBaseUrl, ActiveRoomCode, body, r => res = r);
                if (res != null && !res.Retryable) yield break;
                yield return new WaitForSecondsRealtime(0.5f * (attempt + 1));
            }
        }

        // ==================== REMOTE CHEFS ====================

        private void ApplyPlayers(NetPlayer[] players)
        {
            if (players == null) return;

            bool[] present = new bool[4];
            foreach (NetPlayer np in players)
            {
                if (np == null || np.slot < 0 || np.slot > 3) continue;
                present[np.slot] = true;
                if (np.slot == MySlotIndex) continue;

                RemoteChefData data;
                if (!remoteChefs.TryGetValue(np.slot, out data))
                {
                    data = SpawnRemoteChef(np);
                    if (data == null) continue;
                }

                if (!string.IsNullOrEmpty(np.name)) data.name = np.name;

                if (np.hasPos)
                {
                    data.targetPos = new Vector3(np.x, data.targetPos.y, np.z);
                    data.targetRot = Quaternion.Euler(0f, np.rotY, 0f);
                    if (!data.snapped && data.playerComponent != null)
                    {
                        data.playerComponent.transform.position = data.targetPos;
                        data.playerComponent.transform.rotation = data.targetRot;
                        data.snapped = true;
                    }
                }
                data.isWalking = np.isWalking;
                data.dishes = np.dishes;
                data.score = np.score;
            }

            // Chefs that left the room disappear from the kitchen
            List<int> gone = null;
            foreach (var kvp in remoteChefs)
            {
                if (!present[kvp.Key])
                {
                    if (gone == null) gone = new List<int>();
                    gone.Add(kvp.Key);
                }
            }
            if (gone != null)
            {
                foreach (int slot in gone)
                {
                    RemoteChefData data = remoteChefs[slot];
                    if (data.playerComponent != null) Destroy(data.playerComponent.gameObject);
                    remoteChefs.Remove(slot);
                }
            }

            UpdateScoreboardDisplay();
        }

        private RemoteChefData SpawnRemoteChef(NetPlayer np)
        {
            if (localPlayer == null) return null;

            Vector3[] defaultSpawns = { new Vector3(-2.5f, 0, 0), new Vector3(2.5f, 0, 0), new Vector3(-2.5f, 0, 2f), new Vector3(2.5f, 0, 2f) };
            int slot = np.slot;
            string slotName = !string.IsNullOrEmpty(np.name) ? np.name : AuraTheme.ChefName(slot);

            GameObject clone = Instantiate(localPlayer.gameObject, defaultSpawns[slot], Quaternion.identity);
            clone.name = "RemotePlayer_P" + (slot + 1) + "_" + AuraTheme.ChefName(slot);
            Player rPlayer = clone.GetComponent<Player>();
            rPlayer.isRemotePlayer = true;

            // CRITICAL: Re-assert localPlayer as Player.Instance so touch controls never get stolen
            localPlayer.SetAsLocalInstance();

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
            Debug.Log("<color=cyan>[Multiplayer]</color> Spawned remote chef: " + data.name);
            return data;
        }

        // ==================== CONNECTION UI ====================

        private void CreateNetUI()
        {
            if (scoreboardCanvasObj == null) return;
            if (scoreboardCanvasObj.GetComponent<GraphicRaycaster>() == null)
            {
                scoreboardCanvasObj.AddComponent<GraphicRaycaster>();
            }
            Transform root = scoreboardCanvasObj.transform;

            // Ping pill (bottom-left)
            pingPill = AuraTheme.CreatePanel(root, "PingPill",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 20f),
                new Vector2(128f, 36f), AuraTheme.PanelFill, AuraTheme.StrokeSoft, false);
            pingPill.GetComponent<Image>().raycastTarget = false;
            pingIcon = AuraTheme.CreateIcon(pingPill.transform, AuraIcons.Get(AuraIcon.Signal), new Vector2(-44f, 0f), 20f, AuraTheme.TextSecondary);
            pingLabel = AuraTheme.CreateText(pingPill.transform, "-- ms", 15f, FontStyles.Bold, new Vector2(14f, 0f), new Vector2(80f, 30f), AuraTheme.TextSecondary);

            // Reconnecting banner (top-center, under the timer and hype text)
            reconnectBanner = AuraTheme.CreatePanel(root, "ReconnectBanner",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f),
                new Vector2(420f, 46f), new Color32(0x2A, 0x1E, 0x08, 0xEE), AuraTheme.Amber, false);
            reconnectBanner.GetComponent<Image>().raycastTarget = false;
            AuraTheme.CreateIcon(reconnectBanner.transform, AuraIcons.Get(AuraIcon.WifiOff), new Vector2(-170f, 0f), 24f, AuraTheme.Amber);
            AuraTheme.CreateText(reconnectBanner.transform, "RECONNECTING...", 18f, FontStyles.Bold, new Vector2(14f, 0f), new Vector2(320f, 36f), AuraTheme.Amber);
            reconnectBanner.SetActive(false);

            // Connection lost overlay
            RectTransform overlayRt = AuraTheme.MakeRect(root, "ConnectionLostOverlay");
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;
            Image scrim = overlayRt.gameObject.AddComponent<Image>();
            scrim.color = AuraTheme.Scrim;
            lostOverlay = overlayRt.gameObject;

            GameObject card = AuraTheme.CreatePanel(overlayRt, "Card",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(620f, 330f), AuraTheme.PanelFill, AuraTheme.WithAlpha(AuraTheme.Danger, 0.8f));
            AuraTheme.CreateIcon(card.transform, AuraIcons.Get(AuraIcon.WifiOff), new Vector2(0f, 105f), 56f, AuraTheme.Danger);
            AuraTheme.CreateText(card.transform, "CONNECTION LOST", 30f, FontStyles.Bold, new Vector2(0f, 48f), new Vector2(560f, 44f), AuraTheme.TextPrimary);
            lostReasonText = AuraTheme.CreateText(card.transform, "", 18f, FontStyles.Normal, new Vector2(0f, 0f), new Vector2(540f, 56f), AuraTheme.TextSecondary);

            AuraTheme.CreateButton(card.transform, "Btn_BackToMenu", "BACK TO MENU", new Vector2(-150f, -95f), new Vector2(260f, 64f),
                AuraTheme.ButtonStyle.Primary, () => SceneSwitcher.LoadScene(SceneSwitcher.Scene.MainMenu), 20f);
            AuraTheme.CreateButton(card.transform, "Btn_PlayOffline", "PLAY OFFLINE", new Vector2(150f, -95f), new Vector2(260f, 64f),
                AuraTheme.ButtonStyle.Ghost, ContinueOffline, 20f);
            lostOverlay.SetActive(false);
        }

        private void RefreshPingUI()
        {
            if (pingLabel == null || pingIcon == null) return;
            Color c = pingMs < 90f ? AuraTheme.Success : (pingMs < 200f ? AuraTheme.Amber : AuraTheme.Danger);
            pingLabel.text = Mathf.RoundToInt(pingMs) + " ms";
            pingLabel.color = c;
            pingIcon.color = c;
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
            if (!offlineMode && !string.IsNullOrEmpty(myToken))
            {
                // Only the host's reset is accepted; guests just follow back to the lobby.
                yield return AuraNet.Reset(serverBaseUrl, ActiveRoomCode, MySlotIndex, null);
            }

            // Flag so MainMenu automatically re-opens the active room lobby
            PlayerPrefs.SetInt("Aura_ReturnToLobby", 1);
            SceneSwitcher.LoadScene(SceneSwitcher.Scene.MainMenu);
        }
    }
}
