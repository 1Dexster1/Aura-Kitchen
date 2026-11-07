using System;
using UnityEngine;

namespace AuraKitchen
{
    public class SoloChefController : MonoBehaviour
    {
        public static SoloChefController Instance { get; private set; }

        public event EventHandler<int> OnActiveChefChanged;

        [Header("Settings")]
        [SerializeField] private KeyCode switchKey = KeyCode.Tab;
        [SerializeField] private bool enableSousChefAI = true;

        private int activeChefIndex = 0;

        public int ActiveChefIndex => activeChefIndex;
        public bool SousChefAIEnabled
        {
            get => enableSousChefAI;
            set => enableSousChefAI = value;
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private float assistantScanTimer = 0f;

        private void Update()
        {
            if (MultiplayerManager.Instance == null || MultiplayerManager.Instance.CurrentMode != MultiplayerManager.Mode.SoloSwitch)
            {
                return;
            }

            // Keyboard switch trigger
            if (Input.GetKeyDown(switchKey))
            {
                SwitchChef();
            }

            // Optional Sous-Chef Assistant logic throttled to 4Hz to save CPU
            if (enableSousChefAI)
            {
                assistantScanTimer += Time.deltaTime;
                if (assistantScanTimer >= 0.25f)
                {
                    assistantScanTimer = 0f;
                    UpdateSousChefAssistant();
                }
            }
        }

        public void SwitchChef()
        {
            var players = MultiplayerManager.Instance.Players;
            if (players.Count < 2) return;

            activeChefIndex = (activeChefIndex + 1) % players.Count;

            for (int i = 0; i < players.Count; i++)
            {
                bool isActive = (i == activeChefIndex);
                Player p = players[i];
                if (p != null)
                {
                    // Enable/disable player script input or visual aura
                    var visual = p.GetComponent<PlayerAuraVisual>();
                    if (visual != null) visual.SetChefActive(isActive);

                    // Note: Player movement script is enabled only on the active chef
                    p.enabled = isActive;
                }
            }

            OnActiveChefChanged?.Invoke(this, activeChefIndex);
            Debug.Log($"<color=yellow>[Aura Kitchen]</color> Active chef switched to Chef {activeChefIndex + 1}");
        }

        private void UpdateSousChefAssistant()
        {
            // Autonomous helper behavior for inactive chefs
            var players = MultiplayerManager.Instance.Players;
            if (players.Count < 2) return;

            for (int i = 0; i < players.Count; i++)
            {
                if (i == activeChefIndex) continue; // Skip active chef

                Player idleChef = players[i];
                if (idleChef == null) continue;

                // If idle chef is standing near a cutting counter with cuttable item, trigger interact alt
                Collider[] hits = Physics.OverlapSphere(idleChef.transform.position, 1.5f);
                foreach (var hit in hits)
                {
                    if (hit.TryGetComponent<CuttingCounter>(out var cuttingCounter))
                    {
                        if (cuttingCounter.CanCutItem())
                        {
                            cuttingCounter.InteractAlt(idleChef);
                            break;
                        }
                    }
                }
            }
        }
    }
}
