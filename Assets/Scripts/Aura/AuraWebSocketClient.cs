using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AuraKitchen
{
    public class AuraWebSocketClient : MonoBehaviour
    {
        public static AuraWebSocketClient Instance { get; private set; }

        public event EventHandler<string> OnConnected;
        public event EventHandler<string> OnRoomCreated;
        public event EventHandler<string> OnRoomJoined;
        public event EventHandler<RemotePlayerMovedArgs> OnRemotePlayerMoved;
        public event EventHandler<string> OnErrorReceived;

        public class RemotePlayerMovedArgs : EventArgs
        {
            public int Slot;
            public float X;
            public float Z;
            public float RotY;
        }

        [Header("Backend Connection")]
        [SerializeField] private string serverUrl = "ws://panel.sharebotdex.xyz:30015";
        [SerializeField] private bool autoConnect = true;

        private ClientWebSocket webSocket;
        private CancellationTokenSource cancellationTokenSource;
        private bool isConnecting = false;

        public bool IsConnected => webSocket != null && webSocket.State == WebSocketState.Open;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (autoConnect)
            {
                Connect();
            }
        }

        public async void Connect(string customUrl = "")
        {
            if (isConnecting || IsConnected) return;
            isConnecting = true;

            string targetUrl = string.IsNullOrEmpty(customUrl) ? serverUrl : customUrl;
            try
            {
                cancellationTokenSource = new CancellationTokenSource();
                webSocket = new ClientWebSocket();
                Uri serverUri = new Uri(targetUrl);

                await webSocket.ConnectAsync(serverUri, cancellationTokenSource.Token);
                isConnecting = false;

                Debug.Log($"<color=#00e5ff>[Aura Kitchen]</color> Connected to backend at {targetUrl}");
                OnConnected?.Invoke(this, targetUrl);

                _ = ReceiveLoop();
            }
            catch (Exception ex)
            {
                isConnecting = false;
                Debug.LogWarning($"[Aura Kitchen] WebSocket connection failed: {ex.Message}");
                OnErrorReceived?.Invoke(this, ex.Message);
            }
        }

        public void CreateRoom(string playerName = "Chef Ruby")
        {
            SendMessage(new { type = "create_room", playerName });
        }

        public void JoinRoom(string roomCode, string playerName = "")
        {
            SendMessage(new { type = "join_room", roomCode, playerName });
        }

        public void SendPlayerMovement(int slot, float x, float z, float rotY, bool isMoving)
        {
            SendMessage(new { type = "player_move", slot, x, z, rotY, isMoving });
        }

        public void SendCounterInteract(int slot, int counterIdx, string action, string item)
        {
            SendMessage(new { type = "counter_interact", slot, counterIdx, action, item });
        }

        public void SendOrderDelivered(int slot, int tableIdx, int tip, string dishName)
        {
            SendMessage(new { type = "order_delivered", slot, tableIdx, tip, dishName });
        }

        public new async void SendMessage(object payload)
        {
            if (!IsConnected) return;

            try
            {
                string json = JsonUtility.ToJson(payload);
                if (string.IsNullOrEmpty(json) || json == "{}")
                {
                    // Fallback to simple manual JSON serialization
                    json = ConvertObjectToJson(payload);
                }

                byte[] buffer = Encoding.UTF8.GetBytes(json);
                await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Aura Kitchen] Error sending message: {ex.Message}");
            }
        }

        private async Task ReceiveLoop()
        {
            byte[] buffer = new byte[4096];

            while (IsConnected)
            {
                try
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ProcessIncomingMessage(message);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Aura Kitchen] Receive error: {ex.Message}");
                    break;
                }
            }
        }

        private void ProcessIncomingMessage(string json)
        {
            try
            {
                if (json.Contains("\"room_created\""))
                {
                    OnRoomCreated?.Invoke(this, json);
                }
                else if (json.Contains("\"room_joined\""))
                {
                    OnRoomJoined?.Invoke(this, json);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Aura Kitchen] Process message error: {e.Message}");
            }
        }

        private string ConvertObjectToJson(object obj)
        {
            var props = obj.GetType().GetProperties();
            var fields = obj.GetType().GetFields();
            var parts = new List<string>();

            foreach (var p in props)
            {
                object val = p.GetValue(obj);
                string strVal = val is string ? $"\"{val}\"" : (val is bool ? val.ToString().ToLower() : val?.ToString() ?? "null");
                parts.Add($"\"{p.Name}\":{strVal}");
            }

            foreach (var f in fields)
            {
                object val = f.GetValue(obj);
                string strVal = val is string ? $"\"{val}\"" : (val is bool ? val.ToString().ToLower() : val?.ToString() ?? "null");
                parts.Add($"\"{f.Name}\":{strVal}");
            }

            return "{" + string.Join(",", parts) + "}";
        }

        private void OnDestroy()
        {
            cancellationTokenSource?.Cancel();
            webSocket?.Dispose();
        }
    }
}

