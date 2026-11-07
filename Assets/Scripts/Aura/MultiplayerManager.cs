using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AuraKitchen
{
    public class MultiplayerManager : MonoBehaviour
    {
        public static MultiplayerManager Instance { get; private set; }

        public enum Mode
        {
            SoloSingle,
            SoloSwitch,
            LocalCoop,
            OnlineCoop
        }

        public event EventHandler<Mode> OnModeChanged;
        public event EventHandler<Player> OnPlayerJoined;
        public event EventHandler<string> OnRoomCodeGenerated;

        [Header("Configuration")]
        [SerializeField] private Mode currentMode = Mode.SoloSwitch;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform[] spawnPositions;

        [Header("Online Settings")]
        [SerializeField] private string roomCode = "AURA";
        [SerializeField] private bool isHost = true;
        [SerializeField] private int maxPlayers = 4;

        private List<Player> registeredPlayers = new List<Player>();
        private int localPlayerIndex = 0;

        public Mode CurrentMode => currentMode;
        public string RoomCode => roomCode;
        public bool IsHost => isHost;
        public int LocalPlayerIndex => localPlayerIndex;
        public IReadOnlyList<Player> Players => registeredPlayers;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureDefaultSpawnPositions();
        }

        private void Start()
        {
            InitializeSession();
        }

        public void SetGameMode(Mode mode)
        {
            currentMode = mode;
            OnModeChanged?.Invoke(this, mode);
        }

        public void GenerateNewRoomCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            char[] code = new char[4];
            for (int i = 0; i < 4; i++)
            {
                code[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
            }
            roomCode = new string(code);
            isHost = true;
            OnRoomCodeGenerated?.Invoke(this, roomCode);
            Debug.Log($"<color=cyan>[Aura Kitchen]</color> Room created with code: {roomCode}");
        }

        public void JoinRoomWithCode(string code)
        {
            roomCode = code.ToUpper().Trim();
            isHost = false;
            currentMode = Mode.OnlineCoop;
            Debug.Log($"<color=cyan>[Aura Kitchen]</color> Joining room: {roomCode}");
        }

        private void EnsureDefaultSpawnPositions()
        {
            if (spawnPositions == null || spawnPositions.Length < 4)
            {
                spawnPositions = new Transform[4];
                GameObject spawnParent = new GameObject("Aura_PlayerSpawns");
                spawnParent.transform.SetParent(transform);

                // 4 generous spawn points in kitchen
                Vector3[] defaultSpawns = new Vector3[]
                {
                    new Vector3(-2.5f, 0f, 0f),  // P1 Prep station
                    new Vector3(2.5f, 0f, 0f),   // P2 Cooking station
                    new Vector3(-2.5f, 0f, 2f),  // P3 Assembly station
                    new Vector3(2.5f, 0f, 2f)    // P4 Delivery station
                };

                for (int i = 0; i < 4; i++)
                {
                    GameObject sp = new GameObject($"Spawn_P{i + 1}");
                    sp.transform.SetParent(spawnParent.transform);
                    sp.transform.position = defaultSpawns[i];
                    spawnPositions[i] = sp.transform;
                }
            }
        }

        private void InitializeSession()
        {
            // Find existing player in scene if any
            Player existingPlayer = FindObjectOfType<Player>();
            if (existingPlayer != null && !registeredPlayers.Contains(existingPlayer))
            {
                RegisterPlayer(existingPlayer, 0, "Chef Ruby");
            }

            // If SoloSwitch mode, instantiate second chef
            if (currentMode == Mode.SoloSwitch && registeredPlayers.Count < 2)
            {
                SpawnAdditionalChef(1, "Chef Sapphire");
            }
            // If LocalCoop (offline shared screen), spawn remaining chefs
            else if (currentMode == Mode.LocalCoop)
            {
                int targetCount = Mathf.Min(maxPlayers, spawnPositions.Length);
                for (int i = registeredPlayers.Count; i < targetCount; i++)
                {
                    string chefName = i switch
                    {
                        1 => "Chef Sapphire",
                        2 => "Chef Emerald",
                        3 => "Chef Amber",
                        _ => $"Chef {i + 1}"
                    };
                    SpawnAdditionalChef(i, chefName);
                }
            }
            // OnlineCoop: Handled dynamically by AuraOnlineMultiplayerManager to mirror joined players only
        }

        private void SpawnAdditionalChef(int playerIndex, string chefName)
        {
            if (playerPrefab == null && registeredPlayers.Count > 0)
            {
                // Clone existing player object
                GameObject newChefObj = Instantiate(registeredPlayers[0].gameObject, spawnPositions[playerIndex].position, Quaternion.identity);
                newChefObj.name = $"Player_{playerIndex + 1}_{chefName}";
                Player p = newChefObj.GetComponent<Player>();
                RegisterPlayer(p, playerIndex, chefName);
            }
            else if (playerPrefab != null)
            {
                GameObject newChefObj = Instantiate(playerPrefab, spawnPositions[playerIndex].position, Quaternion.identity);
                newChefObj.name = $"Player_{playerIndex + 1}_{chefName}";
                Player p = newChefObj.GetComponent<Player>();
                RegisterPlayer(p, playerIndex, chefName);
            }
        }

        public void RegisterPlayer(Player player, int index, string name)
        {
            if (!registeredPlayers.Contains(player))
            {
                registeredPlayers.Add(player);

                // Add or configure PlayerAuraVisual
                PlayerAuraVisual visual = player.GetComponent<PlayerAuraVisual>();
                if (visual == null)
                {
                    visual = player.gameObject.AddComponent<PlayerAuraVisual>();
                }
                visual.SetupPlayerVisual(index, name);

                OnPlayerJoined?.Invoke(this, player);
                Debug.Log($"<color=green>[Aura Kitchen]</color> Registered {name} (P{index + 1})");
            }
        }

        public Player GetPlayer(int index)
        {
            if (index >= 0 && index < registeredPlayers.Count)
            {
                return registeredPlayers[index];
            }
            return null;
        }
    }
}

