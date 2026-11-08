using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AuraKitchen
{
    // ==================== WIRE TYPES (parsed with JsonUtility, never with IndexOf) ====================

    [Serializable]
    public class NetPlayer
    {
        public int slot;
        public string name;
        public string role;
        public bool isHost;
        public bool hasPos;
        public float x;
        public float z;
        public float rotY;
        public bool isWalking;
        public int dishes;
        public int score;
    }

    [Serializable]
    public class NetRoomInfo
    {
        public string code;
        public string host;
        public bool isPlaying;
        public int rev;
        public NetPlayer[] players;
    }

    [Serializable]
    public class NetResponse
    {
        public bool success;
        public string error;
        public string code;
        public int slot;
        public string token;
        public bool changed;
        public bool isPlaying;
        public int rev;
        public NetPlayer[] players;
        public NetRoomInfo room;
    }

    [Serializable] public class NetAuthBody { public int slot; public string token; }
    [Serializable] public class NetCreateBody { public string code; public string hostName; }
    [Serializable] public class NetJoinBody { public string code; public string playerName; }

    [Serializable]
    public class NetSyncBody
    {
        public int slot;
        public string token;
        public float x;
        public float z;
        public float rotY;
        public bool isWalking;
        public int rev;
    }

    [Serializable]
    public class NetDeliverBody
    {
        public int slot;
        public string token;
        public int seq;
        public string dishName;
        public int earnedPoints;
    }

    public class NetResult
    {
        public bool ok;              // HTTP success AND server said success:true
        public long status;          // HTTP status (0 = never reached the server)
        public string text;          // raw body
        public string error;         // human readable reason
        public float rtt;            // round trip in milliseconds
        public NetResponse data;     // parsed body (null if body wasn't JSON)

        /// <summary>True when retrying later makes sense (no connection, timeout, 5xx).</summary>
        public bool Retryable { get { return !ok && (status == 0 || status >= 500); } }
    }

    /// <summary>
    /// Thin REST client for the Aura Kitchen server. Works in WebGL (UnityWebRequest only).
    /// The per-player token issued at create/join is kept in PlayerPrefs so it survives scene loads.
    /// </summary>
    public static class AuraNet
    {
        private const string TokenKey = "Aura_PlayerToken";

        public static string Token
        {
            get { return PlayerPrefs.GetString(TokenKey, ""); }
            set { PlayerPrefs.SetString(TokenKey, value ?? ""); }
        }

        public static NetResponse Parse(string json)
        {
            if (string.IsNullOrEmpty(json) || json[0] != '{') return null;
            try { return JsonUtility.FromJson<NetResponse>(json); }
            catch (Exception) { return null; }
        }

        /// <summary>Players from either response shape (top-level list, or nested under "room").</summary>
        public static NetPlayer[] GetPlayers(NetResponse d)
        {
            if (d == null) return null;
            if (d.players != null && d.players.Length > 0) return d.players;
            if (d.room != null && d.room.players != null) return d.room.players;
            return d.players;
        }

        /// <summary>"?slot=1&amp;token=..." so lobby polls double as heartbeats; empty when not registered.</summary>
        public static string AuthQuery(int slot)
        {
            string t = Token;
            if (string.IsNullOrEmpty(t)) return "";
            return "?slot=" + slot + "&token=" + UnityWebRequest.EscapeURL(t);
        }

        private static string Url(string baseUrl, string path)
        {
            return baseUrl.TrimEnd('/') + path;
        }

        private static string Code(string code)
        {
            return UnityWebRequest.EscapeURL((code ?? "").Trim().ToUpperInvariant());
        }

        public static IEnumerator Request(string method, string url, string json, int timeoutSec, Action<NetResult> done)
        {
            NetResult r = new NetResult();
            float t0 = Time.realtimeSinceStartup;

            using (UnityWebRequest req = new UnityWebRequest(url, method))
            {
                req.timeout = timeoutSec;
                req.downloadHandler = new DownloadHandlerBuffer();
                if (json != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    req.SetRequestHeader("Content-Type", "application/json");
                }

                yield return req.SendWebRequest();

                r.rtt = (Time.realtimeSinceStartup - t0) * 1000f;
                r.status = req.responseCode;
                r.text = req.downloadHandler != null ? req.downloadHandler.text : null;
                r.data = Parse(r.text);
                r.ok = req.result == UnityWebRequest.Result.Success && r.data != null && r.data.success;

                if (r.data != null && !string.IsNullOrEmpty(r.data.error)) r.error = r.data.error;
                else if (!r.ok) r.error = req.error;
            }

            if (done != null) done(r);
        }

        // ---------- Endpoints ----------

        public static IEnumerator Create(string baseUrl, string code, string hostName, Action<NetResult> done)
        {
            string json = JsonUtility.ToJson(new NetCreateBody { code = code, hostName = hostName });
            return Request("POST", Url(baseUrl, "/api/room/create"), json, 10, done);
        }

        public static IEnumerator Join(string baseUrl, string code, string playerName, Action<NetResult> done)
        {
            string json = JsonUtility.ToJson(new NetJoinBody { code = code, playerName = playerName });
            return Request("POST", Url(baseUrl, "/api/room/join"), json, 10, done);
        }

        public static IEnumerator GetRoom(string baseUrl, string code, int slot, Action<NetResult> done)
        {
            return Request("GET", Url(baseUrl, "/api/room/" + Code(code) + AuthQuery(slot)), null, 6, done);
        }

        public static IEnumerator Start(string baseUrl, string code, int slot, Action<NetResult> done)
        {
            string json = JsonUtility.ToJson(new NetAuthBody { slot = slot, token = Token });
            return Request("POST", Url(baseUrl, "/api/room/" + Code(code) + "/start"), json, 10, done);
        }

        public static IEnumerator Sync(string baseUrl, string code, NetSyncBody body, Action<NetResult> done)
        {
            return Request("POST", Url(baseUrl, "/api/room/" + Code(code) + "/sync"), JsonUtility.ToJson(body), 4, done);
        }

        public static IEnumerator Deliver(string baseUrl, string code, NetDeliverBody body, Action<NetResult> done)
        {
            return Request("POST", Url(baseUrl, "/api/room/" + Code(code) + "/deliver"), JsonUtility.ToJson(body), 5, done);
        }

        public static IEnumerator Reset(string baseUrl, string code, int slot, Action<NetResult> done)
        {
            string json = JsonUtility.ToJson(new NetAuthBody { slot = slot, token = Token });
            return Request("POST", Url(baseUrl, "/api/room/" + Code(code) + "/reset"), json, 6, done);
        }

        public static IEnumerator Leave(string baseUrl, string code, int slot, Action<NetResult> done = null)
        {
            string json = JsonUtility.ToJson(new NetAuthBody { slot = slot, token = Token });
            return Request("POST", Url(baseUrl, "/api/room/" + Code(code) + "/leave"), json, 6, done);
        }
    }
}
