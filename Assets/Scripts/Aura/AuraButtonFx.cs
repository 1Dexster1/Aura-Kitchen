using UnityEngine;
using UnityEngine.EventSystems;

namespace AuraKitchen
{
    /// <summary>Tiny hover/press scale feedback for procedurally built buttons.</summary>
    public class AuraButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float HoverScale = 1.04f;
        private const float PressScale = 0.95f;
        private const float Speed = 18f;

        private float target = 1f;
        private float current = 1f;
        private bool hovering;

        public void OnPointerEnter(PointerEventData e) { hovering = true; target = HoverScale; }
        public void OnPointerExit(PointerEventData e) { hovering = false; target = 1f; }
        public void OnPointerDown(PointerEventData e) { target = PressScale; }
        public void OnPointerUp(PointerEventData e) { target = hovering ? HoverScale : 1f; }

        private void OnDisable()
        {
            current = 1f;
            target = 1f;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            if (Mathf.Abs(current - target) < 0.001f) return;
            current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(current, current, 1f);
        }
    }
}
