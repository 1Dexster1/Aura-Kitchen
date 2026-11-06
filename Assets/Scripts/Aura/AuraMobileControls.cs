using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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

        [Header("Touch Buttons")]
        [SerializeField] private Button interactButton;
        [SerializeField] private Button cutActionButton;
        [SerializeField] private Button sprintButton;
        [SerializeField] private Button switchChefButton;

        [Header("Configuration")]
        [SerializeField] private bool autoDetectMobile = true;
        [SerializeField] private float joystickRadius = 75f;

        private bool isTouchingJoystick = false;
        private Vector2 joystickCenter = Vector2.zero;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Auto-enable if on mobile or WebGL mobile
            if (autoDetectMobile)
            {
                bool isMobilePlatform = Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;
                if (mobileControlsRoot != null)
                {
                    mobileControlsRoot.SetActive(isMobilePlatform);
                }
            }
        }

        private void Start()
        {
            if (interactButton != null)
            {
                interactButton.onClick.AddListener(() =>
                {
                    OnInteractPressed?.Invoke(this, EventArgs.Empty);
                    TriggerActivePlayerInteract();
                });
            }

            if (cutActionButton != null)
            {
                cutActionButton.onClick.AddListener(() =>
                {
                    OnInteractAltPressed?.Invoke(this, EventArgs.Empty);
                    TriggerActivePlayerInteractAlt();
                });
            }

            if (switchChefButton != null)
            {
                switchChefButton.onClick.AddListener(() =>
                {
                    OnSwitchChefPressed?.Invoke(this, EventArgs.Empty);
                    if (SoloChefController.Instance != null)
                    {
                        SoloChefController.Instance.SwitchChef();
                    }
                });
            }
        }

        public void SetSprintState(bool sprinting)
        {
            IsTouchSprinting = sprinting;
        }

        public void OnJoystickPointerDown(BaseEventData eventData)
        {
            PointerEventData pointer = eventData as PointerEventData;
            if (pointer == null) return;

            isTouchingJoystick = true;
            joystickCenter = pointer.position;
            if (joystickBackground != null)
            {
                joystickBackground.position = joystickCenter;
                joystickBackground.gameObject.SetActive(true);
            }
            UpdateJoystick(pointer.position);
        }

        public void OnJoystickDrag(BaseEventData eventData)
        {
            PointerEventData pointer = eventData as PointerEventData;
            if (pointer == null || !isTouchingJoystick) return;

            UpdateJoystick(pointer.position);
        }

        public void OnJoystickPointerUp(BaseEventData eventData)
        {
            isTouchingJoystick = false;
            TouchMovementVector = Vector2.zero;
            if (joystickHandle != null)
            {
                joystickHandle.anchoredPosition = Vector2.zero;
            }
        }

        private void UpdateJoystick(Vector2 touchPos)
        {
            Vector2 offset = touchPos - joystickCenter;
            float distance = offset.magnitude;
            Vector2 direction = distance > 0.001f ? offset.normalized : Vector2.zero;

            float clampedDistance = Mathf.Clamp(distance, 0f, joystickRadius);
            if (joystickHandle != null)
            {
                joystickHandle.anchoredPosition = direction * clampedDistance;
            }

            // Normalise input to (-1 to 1)
            TouchMovementVector = direction * (clampedDistance / joystickRadius);
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
