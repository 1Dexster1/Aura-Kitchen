using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
        [SerializeField] private float joystickRadius = 80f;

        private int activeJoystickFingerId = -1;
        private Vector2 joystickCenterPos = Vector2.zero;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Ensure smooth 60 FPS performance on Android & keep screen on
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Ensure EventSystem exists for UI touches
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("Aura_EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }

            // Procedurally create mobile controls if not assigned
            if (mobileControlsRoot == null)
            {
                CreateProceduralMobileUI();
            }
        }

        private void CreateProceduralMobileUI()
        {
            mobileControlsRoot = new GameObject("Aura_MobileControls_Canvas");
            Canvas canvas = mobileControlsRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            CanvasScaler scaler = mobileControlsRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            mobileControlsRoot.AddComponent<GraphicRaycaster>();

            // 1. Virtual Joystick Container (Bottom-Left)
            GameObject joyBgObj = new GameObject("Joystick_Background");
            joyBgObj.transform.SetParent(mobileControlsRoot.transform, false);
            joystickBackground = joyBgObj.AddComponent<RectTransform>();
            joystickBackground.anchorMin = new Vector2(0, 0);
            joystickBackground.anchorMax = new Vector2(0, 0);
            joystickBackground.pivot = new Vector2(0.5f, 0.5f);
            joystickBackground.anchoredPosition = new Vector2(180, 180);
            joystickBackground.sizeDelta = new Vector2(180, 180);

            Image joyBgImg = joyBgObj.AddComponent<Image>();
            joyBgImg.color = new Color(0.08f, 0.12f, 0.2f, 0.65f); // Deep glass navy
            joyBgImg.raycastTarget = false;

            // Joystick knob / handle
            GameObject joyHandleObj = new GameObject("Joystick_Handle");
            joyHandleObj.transform.SetParent(joyBgObj.transform, false);
            joystickHandle = joyHandleObj.AddComponent<RectTransform>();
            joystickHandle.anchorMin = new Vector2(0.5f, 0.5f);
            joystickHandle.anchorMax = new Vector2(0.5f, 0.5f);
            joystickHandle.pivot = new Vector2(0.5f, 0.5f);
            joystickHandle.anchoredPosition = Vector2.zero;
            joystickHandle.sizeDelta = new Vector2(80, 80);

            Image joyHandleImg = joyHandleObj.AddComponent<Image>();
            joyHandleImg.color = new Color(0f, 0.9f, 1f, 0.85f); // Glowing Aura Cyan
            joyHandleImg.raycastTarget = false;

            // 2. Action Buttons Container (Bottom-Right)
            // Button A: PICK / DROP (Large Emerald)
            CreateActionButton("Btn_Interact", "PICK\nDROP", new Vector2(-130, 130), new Vector2(120, 120),
                new Color(0.1f, 0.85f, 0.45f, 0.9f), () =>
                {
                    OnInteractPressed?.Invoke(this, EventArgs.Empty);
                    TriggerActivePlayerInteract();
                });

            // Button B: CHOP / COOK (Large Amber/Orange)
            CreateActionButton("Btn_ActionAlt", "CHOP\nUSE", new Vector2(-270, 130), new Vector2(110, 110),
                new Color(1f, 0.6f, 0.1f, 0.9f), () =>
                {
                    OnInteractAltPressed?.Invoke(this, EventArgs.Empty);
                    TriggerActivePlayerInteractAlt();
                });

            // Button C: SWITCH CHEF (Magenta / Purple)
            CreateActionButton("Btn_SwitchChef", "CHEF\n🔁", new Vector2(-130, 270), new Vector2(95, 95),
                new Color(0.85f, 0.2f, 0.85f, 0.9f), () =>
                {
                    OnSwitchChefPressed?.Invoke(this, EventArgs.Empty);
                    if (SoloChefController.Instance != null)
                    {
                        SoloChefController.Instance.SwitchChef();
                    }
                });

            // Button D: DASH / SPRINT (Electric Blue)
            CreateActionButton("Btn_Dash", "DASH\n⚡", new Vector2(-270, 260), new Vector2(90, 90),
                new Color(0.15f, 0.65f, 1f, 0.9f), () =>
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
            img.color = color;

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = color;
            cb.highlightedColor = Color.white;
            cb.pressedColor = color * 0.7f;
            btn.colors = cb;
            btn.onClick.AddListener(() => onClick?.Invoke());

            // Label Text
            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
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
            // Support both Mobile Touches and Mouse Dragging
            if (Input.touchCount > 0)
            {
                bool foundJoyTouch = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);

                    // If finger touched left side of screen
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
                // Mouse drag fallback for PC testing
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
    }
}
