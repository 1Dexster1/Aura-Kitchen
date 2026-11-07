using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace AuraKitchen
{
    public class AuraMobileControls : MonoBehaviour
    {
        public static AuraMobileControls Instance { get; private set; }

        public static Vector2 TouchMovementVector { get; private set; } = Vector2.zero;
        public static bool IsTouchSprinting { get; private set; } = false;

        public event EventHandler OnInteractPressed;
        public event EventHandler OnInteractAltPressed;
        public event EventHandler OnSwitchChefPressed;

        [Header("State")]
        private Vector3 targetMovePosition = Vector3.zero;
        private KitchenCounter targetCounter = null;
        private bool isMovingToTarget = false;
        private float dashTimer = 0f;
        private const float DashDuration = 0.45f;

        [Header("UI Canvas")]
        private GameObject mobileCanvasObj;
        private GameObject tapMarkerObj;
        private Image tapMarkerImage;
        private RectTransform tapMarkerRect;
        private float tapMarkerAlpha = 0f;

        private GameObject switchChefBtnObj;
        private TextMeshProUGUI switchChefBtnText;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("Aura_EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(esObj);
            }

            CreateMobileCanvas();

            SceneManager.sceneLoaded += OnSceneLoaded;
            UpdateVisibility(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UpdateVisibility(scene.name);
            ResetMovement();
        }

        public void EnsureControlsVisible(bool visible)
        {
            if (mobileCanvasObj != null)
            {
                mobileCanvasObj.SetActive(visible);
            }
        }

        private void UpdateVisibility(string sceneName)
        {
            bool isGame = sceneName == SceneSwitcher.Scene.GameScene.ToString();
            EnsureControlsVisible(isGame);
        }

        private void CreateMobileCanvas()
        {
            mobileCanvasObj = new GameObject("Aura_TapToMove_Canvas");
            DontDestroyOnLoad(mobileCanvasObj);

            Canvas canvas = mobileCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = mobileCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            mobileCanvasObj.AddComponent<GraphicRaycaster>();

            // 1. Subtle Tap Ripple Marker
            tapMarkerObj = new GameObject("TapMarker");
            tapMarkerObj.transform.SetParent(mobileCanvasObj.transform, false);
            tapMarkerRect = tapMarkerObj.AddComponent<RectTransform>();
            tapMarkerRect.sizeDelta = new Vector2(90, 90);
            tapMarkerRect.pivot = new Vector2(0.5f, 0.5f);

            tapMarkerImage = tapMarkerObj.AddComponent<Image>();
            tapMarkerImage.sprite = AuraMobileControls.GetCircleSprite();
            tapMarkerImage.color = new Color(0.2f, 0.85f, 1f, 0f);
            tapMarkerImage.raycastTarget = false;

            // 2. Minimal Chef Switch Button (bottom-center)
            switchChefBtnObj = new GameObject("Btn_SwitchChef_Bottom");
            switchChefBtnObj.transform.SetParent(mobileCanvasObj.transform, false);
            RectTransform sRect = switchChefBtnObj.AddComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.5f, 0);
            sRect.anchorMax = new Vector2(0.5f, 0);
            sRect.pivot = new Vector2(0.5f, 0);
            sRect.anchoredPosition = new Vector2(0, 24);
            sRect.sizeDelta = new Vector2(240, 52);

            Image sImg = switchChefBtnObj.AddComponent<Image>();
            sImg.color = new Color(0.18f, 0.18f, 0.18f, 0.88f);

            Outline sOutline = switchChefBtnObj.AddComponent<Outline>();
            sOutline.effectColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            sOutline.effectDistance = new Vector2(1, -1);

            Button sBtn = switchChefBtnObj.AddComponent<Button>();
            sBtn.onClick.AddListener(() =>
            {
                if (SoloChefController.Instance != null)
                {
                    SoloChefController.Instance.SwitchChef();
                    UpdateSwitchChefLabel();
                }
            });

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(switchChefBtnObj.transform, false);
            RectTransform tRect = txtObj.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.sizeDelta = Vector2.zero;

            switchChefBtnText = txtObj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
            {
                switchChefBtnText.font = font;
            }
            switchChefBtnText.text = "SWITCH CHEF (P1/P2)";
            switchChefBtnText.fontSize = 16;
            switchChefBtnText.fontStyle = FontStyles.Bold;
            switchChefBtnText.alignment = TextAlignmentOptions.Center;
            switchChefBtnText.color = Color.white;
            switchChefBtnText.raycastTarget = false;
        }

        private void UpdateSwitchChefLabel()
        {
            if (switchChefBtnText != null && SoloChefController.Instance != null)
            {
                int current = SoloChefController.Instance.ActiveChefIndex + 1;
                switchChefBtnText.text = $"ACTIVE: CHEF {current} (TAP TO SWITCH)";
            }
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name != SceneSwitcher.Scene.GameScene.ToString())
            {
                return;
            }

            HandleTapInput();
            ProcessMovement();
            UpdateDash();
            UpdateTapMarkerAnimation();

            // Only show switch chef button in SoloSwitch mode
            if (switchChefBtnObj != null)
            {
                bool isSolo = MultiplayerManager.Instance != null &&
                              MultiplayerManager.Instance.CurrentMode == MultiplayerManager.Mode.SoloSwitch;
                if (switchChefBtnObj.activeSelf != isSolo)
                {
                    switchChefBtnObj.SetActive(isSolo);
                }
            }
        }

        private void HandleTapInput()
        {
            bool tapBegan = false;
            Vector2 tapPos = Vector2.zero;

            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId))
                    {
                        continue;
                    }

                    if (t.phase == TouchPhase.Began)
                    {
                        tapBegan = true;
                        tapPos = t.position;
                        break;
                    }
                }
            }
            else if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                {
                    tapBegan = true;
                    tapPos = Input.mousePosition;
                }
            }

            if (!tapBegan) return;

            // Trigger Dash if duck is already moving towards a target!
            if (isMovingToTarget)
            {
                TriggerDash();
            }

            ShowTapMarker(tapPos);

            if (Camera.main == null) return;
            Ray ray = Camera.main.ScreenPointToRay(tapPos);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                Player player = Player.Instance;
                if (player == null) return;

                // 1. Tapped another chef in Solo mode?
                Player clickedPlayer = hit.transform.GetComponentInParent<Player>();
                if (clickedPlayer != null && clickedPlayer != player)
                {
                    if (SoloChefController.Instance != null)
                    {
                        SoloChefController.Instance.SwitchChef();
                        UpdateSwitchChefLabel();
                        ResetMovement();
                        return;
                    }
                }

                // 2. Tapped a Kitchen Counter?
                KitchenCounter counter = hit.transform.GetComponentInParent<KitchenCounter>();
                if (counter != null)
                {
                    HandleCounterTap(player, counter);
                    return;
                }

                // 3. Tapped Floor / Destination?
                Vector3 dest = hit.point;
                dest.y = player.transform.position.y;
                targetMovePosition = dest;
                targetCounter = null;
                isMovingToTarget = true;
            }
        }

        private void HandleCounterTap(Player player, KitchenCounter counter)
        {
            Vector3 counterPos = counter.transform.position;
            Vector3 playerPos = player.transform.position;

            // Compute ideal interaction position in front of counter
            Vector3 interactPos = counterPos + counter.transform.forward * 1.15f;
            interactPos.y = playerPos.y;

            float distToCounter = Vector3.Distance(new Vector3(playerPos.x, 0, playerPos.z),
                                                   new Vector3(counterPos.x, 0, counterPos.z));

            // If already standing right next to this counter, interact immediately!
            if (distToCounter <= 1.65f)
            {
                Vector3 lookDir = (counterPos - playerPos);
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    player.transform.forward = lookDir.normalized;
                }

                // If it's a cutting counter with something to chop, trigger Alt, else Normal
                if (counter is CuttingCounter cuttingCounter && cuttingCounter.HoldingObject)
                {
                    player.InteractAltAction(this, EventArgs.Empty);
                }
                else
                {
                    player.InteractionAction(this, EventArgs.Empty);
                }

                ResetMovement();
            }
            else
            {
                // Walk to the counter then interact
                targetMovePosition = interactPos;
                targetCounter = counter;
                isMovingToTarget = true;
            }
        }

        private void ProcessMovement()
        {
            if (!isMovingToTarget) return;

            Player player = Player.Instance;
            if (player == null)
            {
                ResetMovement();
                return;
            }

            Vector3 currentPos = player.transform.position;
            Vector3 offset = targetMovePosition - currentPos;
            offset.y = 0f;

            float dist = offset.magnitude;
            if (dist > 0.28f)
            {
                Vector3 dir = offset.normalized;
                TouchMovementVector = new Vector2(dir.x, dir.z);
            }
            else
            {
                // Reached destination!
                ResetMovement();

                if (targetCounter != null)
                {
                    Vector3 lookDir = (targetCounter.transform.position - player.transform.position);
                    lookDir.y = 0;
                    if (lookDir != Vector3.zero)
                    {
                        player.transform.forward = lookDir.normalized;
                    }

                    player.InteractionAction(this, EventArgs.Empty);
                    targetCounter = null;
                }
            }
        }

        public void TriggerDash()
        {
            dashTimer = DashDuration;
            IsTouchSprinting = true;
        }

        private void UpdateDash()
        {
            if (dashTimer > 0f)
            {
                dashTimer -= Time.deltaTime;
                if (dashTimer <= 0f)
                {
                    IsTouchSprinting = false;
                }
            }
        }

        private void ResetMovement()
        {
            isMovingToTarget = false;
            TouchMovementVector = Vector2.zero;
        }

        private void ShowTapMarker(Vector2 screenPos)
        {
            if (tapMarkerRect != null && tapMarkerImage != null)
            {
                tapMarkerRect.position = screenPos;
                tapMarkerRect.sizeDelta = new Vector2(40, 40);
                tapMarkerAlpha = 0.85f;
                tapMarkerImage.color = new Color(0.2f, 0.9f, 1f, tapMarkerAlpha);
                tapMarkerObj.SetActive(true);
            }
        }

        private void UpdateTapMarkerAnimation()
        {
            if (tapMarkerAlpha > 0f && tapMarkerRect != null && tapMarkerImage != null)
            {
                tapMarkerAlpha -= Time.deltaTime * 3.5f;
                float currentSize = tapMarkerRect.sizeDelta.x + (Time.deltaTime * 180f);
                tapMarkerRect.sizeDelta = new Vector2(currentSize, currentSize);

                if (tapMarkerAlpha <= 0f)
                {
                    tapMarkerAlpha = 0f;
                    tapMarkerObj.SetActive(false);
                }
                else
                {
                    tapMarkerImage.color = new Color(0.2f, 0.9f, 1f, tapMarkerAlpha);
                }
            }
        }

        private static Sprite cachedCircleSprite;
        public static Sprite GetCircleSprite()
        {
            if (cachedCircleSprite != null) return cachedCircleSprite;
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float radius = size / 2f - 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float ringDist = Mathf.Abs(dist - (radius * 0.75f));
                    float alpha = Mathf.Clamp01(1f - (ringDist / 4f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return cachedCircleSprite;
        }
    }
}
