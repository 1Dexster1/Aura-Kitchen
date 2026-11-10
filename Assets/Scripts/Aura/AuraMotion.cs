using UnityEngine;

namespace AuraKitchen
{
    /// <summary>Marks UI that has already been restyled so the scene skinner never touches it twice.</summary>
    public class AuraSkinned : MonoBehaviour { }

    /// <summary>Marks a cream paper board so text placed on it is rendered in chocolate ink.</summary>
    public class AuraBoard : MonoBehaviour { }

    /// <summary>Springy entrance: scales from ~0.4 to 1 with an overshoot, every time the object is enabled.</summary>
    public class AuraPopIn : MonoBehaviour
    {
        public float delay = 0f;
        public float duration = 0.5f;
        private float t0;
        private bool running;

        private void OnEnable()
        {
            t0 = Time.unscaledTime + delay;
            running = true;
            transform.localScale = Vector3.one * 0.001f;
        }

        private void OnDisable()
        {
            running = false;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            if (!running) return;
            float k = (Time.unscaledTime - t0) / duration;
            if (k < 0f) return;
            if (k >= 1f)
            {
                transform.localScale = Vector3.one;
                running = false;
                return;
            }
            // easeOutBack
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
            transform.localScale = Vector3.one * Mathf.Max(0.001f, e);
        }
    }

    /// <summary>Gentle idle bob + tilt (logo, banners).</summary>
    public class AuraBob : MonoBehaviour
    {
        public float amplitude = 9f;
        public float tilt = 0.6f;
        public float speed = 1.5f;
        private RectTransform rt;
        private Vector2 basePos;
        private bool captured;

        private void OnEnable() { captured = false; }

        private void Update()
        {
            if (rt == null) rt = transform as RectTransform;
            if (rt == null) return;
            if (!captured) { basePos = rt.anchoredPosition; captured = true; }
            float s = Mathf.Sin(Time.unscaledTime * speed);
            rt.anchoredPosition = basePos + new Vector2(0f, s * amplitude);
            rt.localRotation = Quaternion.Euler(0f, 0f, s * tilt);
        }
    }

    /// <summary>Floating, slowly spinning decoration (food icons behind the menu).</summary>
    public class AuraFloat : MonoBehaviour
    {
        public float amplitude = 14f;
        public float speed = 1.7f;
        public float spin = 5f;
        public float phase;
        private RectTransform rt;
        private Vector2 basePos;
        private float baseRot;
        private bool captured;

        private void Update()
        {
            if (rt == null) rt = transform as RectTransform;
            if (rt == null) return;
            if (!captured) { basePos = rt.anchoredPosition; baseRot = rt.localEulerAngles.z; captured = true; }
            float s = Mathf.Sin(Time.unscaledTime * speed + phase);
            rt.anchoredPosition = basePos + new Vector2(0f, s * amplitude);
            rt.localRotation = Quaternion.Euler(0f, 0f, baseRot + s * spin);
        }
    }
}
