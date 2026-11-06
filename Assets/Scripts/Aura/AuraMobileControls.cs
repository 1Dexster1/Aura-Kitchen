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

        [Header("UI Canvas & Panels")]
        [SerializeField] private GameObject mobileControlsRoot;
        [SerializeField] private RectTransform joystickBackground;
        [SerializeField] private RectTransform joystickHandle;

        [Header("Configuration")]
        [SerializeField] private float joystickRadius = 90f;

        private int activeJoystickFingerId = -1;
        private Vector2 joystickCenterPos = Vector2.zero;
        private static Sprite cachedCircleSprite;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Lock 60 FPS on Android & prevent screen timeout
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Ensure EventSystem exists for UI touches
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("Aura_EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(esObj);
            }

            if (mobileControlsRoot == null)
            {
                CreateProceduralMobileUI();
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            UpdateControlsVisibility(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UpdateControlsVisibility(scene.name);
        }

        public void EnsureControlsVisible(bool visible)
        {
            if (mobileControlsRoot != null)
            {
                mobileControlsRoot.SetActive(visible);
            }
        }

        private void UpdateControlsVisibility(string sceneName)
        {
            bool isGame = sceneName == SceneSwitcher.Scene.GameScene.ToString();
            EnsureControlsVisible(isGame);
        }

        private void CreateProceduralMobileUI()
        {
            mobileControlsRoot = new GameObject("Aura_MobileControls_Canvas");
            DontDestroyOnLoad(mobileControlsRoot);

            Canvas canvas = mobileControlsRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // Always on top of everything

            CanvasScaler scaler = mobileControlsRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            mobileControlsRoot.AddComponent<GraphicRaycaster>();

            Sprite circle = GetCircleSprite();

            // 1. Virtual Joystick (Bottom-Left)
            GameObject joyBgObj = new GameObject("Joystick_Background");
            joyBgObj.transform.SetParent(mobileControlsRoot.transform, false);
            joystickBackground = joyBgObj.AddComponent<RectTransform>();
            joystickBackground.anchorMin = new Vector2(0, 0);
            joystickBackground.anchorMax = new Vector2(0, 0);
            joystickBackground.pivot = new Vector2(0.5f, 0.5f);
            joystickBackground.anchoredPosition = new Vector2(210, 210);
            joystickBackground.sizeDelta = new Vector2(210, 210);

            Image joyBgImg = joyBgObj.AddComponent<Image>();
            joyBgImg.sprite = circle;
            joyBgImg.color = new Color(0.06f, 0.10f, 0.18f, 0.70f);
            joyBgImg.raycastTarget = false;

            Outline joyBgOutline = joyBgObj.AddComponent<Outline>();
            joyBgOutline.effectColor = new Color(0.0f, 0.85f, 1.0f, 0.75f);
            joyBgOutline.effectDistance = new Vector2(3, -3);

            // Joystick Handle Knob
            GameObject joyHandleObj = new GameObject("Joystick_Handle");
            joyHandleObj.transform.SetParent(joyBgObj.transform, false);
            joystickHandle = joyHandleObj.AddComponent<RectTransform>();
            joystickHandle.anchorMin = new Vector2(0.5f, 0.5f);
            joystickHandle.anchorMax = new Vector2(0.5f, 0.5f);
            joystickHandle.pivot = new Vector2(0.5f, 0.5f);
            joystickHandle.anchoredPosition = Vector2.zero;
            joystickHandle.sizeDelta = new Vector2(95, 95);

            Image joyHandleImg = joyHandleObj.AddComponent<Image>();
            joyHandleImg.sprite = circle;
            joyHandleImg.color = new Color(0.0f, 0.92f, 1.0f, 0.90f);
            joyHandleImg.raycastTarget = false;

            // 2. Action Buttons (Bottom-Right)
            // Primary Interact (Pick/Drop) - Emerald
            CreateActionButton("Btn_Interact", "PICK\nDROP", new Vector2(-140, 140), new Vector2(125, 125),
                new Color(0.05f, 0.75f, 0.40f, 0.95f), () =>
                {
                    OnInteractPressed?.Invoke(this, EventArgs.Empty);
                    TriggerActivePlayerInteract();
                });

            // Secondary Action (Chop/Use) - Amber
            CreateActionButton("Btn_ActionAlt", "CHOP\nUSE", new Vector2(-285, 140), new Vector2(115, 115),
                new Color(0.95f, 0.55f, 0.10f, 0.95f), () =>
                {
                    OnInteractAltPressed?.Invoke(this, EventArgs.Empty);
                    TriggerActivePlayerInteractAlt();
                });

            // Switch Chef - Purple
            CreateActionButton("Btn_SwitchChef", "SWITCH\nCHEF", new Vector2(-140, 285), new Vector2(100, 100),
                new Color(0.75f, 0.25f, 0.85f, 0.95f), () =>
                {
                    OnSwitchChefPressed?.Invoke(this, EventArgs.Empty);
                    if (SoloChefController.Instance != null)
                    {
                        SoloChefController.Instance.SwitchChef();
                    }
                });

            // Dash / Sprint - Electric Blue
            CreateActionButton("Btn_Dash", "DASH\nRUN", new Vector2(-285, 275), new Vector2(95, 95),
                new Color(0.15f, 0.60f, 0.95f, 0.95f), () =>
                {
                    IsTouchSprinting = !IsTouchSprinting;
                });
        }

        private GameObject CreateActionButton(string name, string label, Vector2 anchoredPos, Vector2 size, Color color, Action onClick)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(mobileControlsRoot.transform, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            img.sprite = GetCircleSprite();
            img.color = color;

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.4f);
            outline.effectDistance = new Vector2(2, -2);

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = color;
            cb.highlightedColor = Color.white;
            cb.pressedColor = color * 0.75f;
            btn.colors = cb;
            btn.onClick.AddListener(() => onClick?.Invoke());

            // Label Text using official game font
            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset gameFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (gameFont != null)
            {
                tmp.font = gameFont;
                tmp.fontSharedMaterial = gameFont.material;
            }
            tmp.text = label;
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            return btnObj;
        }

        private void Update()
        {
            HandleDirectTouchInput();
        }

        private void HandleDirectTouchInput()
        {
            if (Input.touchCount > 0)
            {
                bool foundJoyTouch = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);

                    // Touch on left half of screen controls movement joystick
                    if (t.position.x < Screen.width * 0.5f)
                    {
                        if (t.phase == TouchPhase.Began)
                        {
                            activeJoystickFingerId = t.fingerId;
                            joystickCenterPos = t.position;
                            UpdateJoystickVisual(t.position);
                            foundJoyTouch = true;
                        }
                        else if (t.fingerId == activeJoystickFingerId && (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary))
                        {
                            UpdateJoystickVisual(t.position);
                            foundJoyTouch = true;
                        }
                    }

                    if (t.fingerId == activeJoystickFingerId && (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled))
                    {
                        ResetJoystick();
                    }
                }

                if (!foundJoyTouch && activeJoystickFingerId != -1)
                {
                    ResetJoystick();
                }
            }
            else if (Input.GetMouseButton(0))
            {
                Vector2 mousePos = Input.mousePosition;
                if (mousePos.x < Screen.width * 0.5f)
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        joystickCenterPos = mousePos;
                    }
                    UpdateJoystickVisual(mousePos);
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                ResetJoystick();
            }
        }

        private void UpdateJoystickVisual(Vector2 currentPos)
        {
            Vector2 offset = currentPos - joystickCenterPos;
            float distance = offset.magnitude;
            Vector2 dir = distance > 0.001f ? offset.normalized : Vector2.zero;

            float clamped = Mathf.Clamp(distance, 0f, joystickRadius);
            TouchMovementVector = dir * (clamped / joystickRadius);

            if (joystickHandle != null)
            {
                joystickHandle.anchoredPosition = dir * clamped;
            }
        }

        private void ResetJoystick()
        {
            activeJoystickFingerId = -1;
            TouchMovementVector = Vector2.zero;
            if (joystickHandle != null)
            {
                joystickHandle.anchoredPosition = Vector2.zero;
            }
        }

        private void TriggerActivePlayerInteract()
        {
            Player p = Player.Instance;
            if (p != null)
            {
                p.InteractionAction(this, EventArgs.Empty);
            }
        }

        private void TriggerActivePlayerInteractAlt()
        {
            Player p = Player.Instance;
            if (p != null)
            {
                p.InteractAltAction(this, EventArgs.Empty);
            }
        }

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
                    float alpha = Mathf.Clamp01((radius - dist) + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return cachedCircleSprite;
        }
    }
}
