using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AuraKitchen
{
    /// <summary>
    /// Single source of truth for Aura Kitchen's look: palette, chef identities,
    /// procedural rounded/glass sprites, and shared UI builders (panels, buttons, text).
    /// Every procedural screen should build through this class instead of hardcoding colors.
    /// </summary>
    public static class AuraTheme
    {
        // ==================== PALETTE (cartoon: ink black + paper white) ====================
        public static readonly Color Ink       = new Color32(0x0D, 0x0D, 0x10, 0xFF);
        public static readonly Color Paper     = new Color32(0xF7, 0xF5, 0xF0, 0xFF);

        // "Cyan" is kept as a name so existing call sites keep compiling; it is now paper white.
        public static readonly Color Cyan      = Paper;
        public static readonly Color Amber     = new Color32(0xFF, 0xB8, 0x1C, 0xFF);
        public static readonly Color Gold      = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        public static readonly Color Success   = new Color32(0x4A, 0xDE, 0x80, 0xFF);
        public static readonly Color Danger    = new Color32(0xFF, 0x5C, 0x5C, 0xFF);
        public static readonly Color Ember     = new Color32(0xFF, 0x7A, 0x1A, 0xFF);

        public static readonly Color PanelFill   = new Color32(0x14, 0x14, 0x18, 0xF5);
        public static readonly Color PanelRaised = new Color32(0x22, 0x22, 0x28, 0xFA);
        public static readonly Color PanelDeep   = new Color32(0x0A, 0x0A, 0x0C, 0xFA);
        public static readonly Color Scrim       = new Color32(0x05, 0x05, 0x07, 0xDB);
        public static readonly Color Stroke      = new Color32(0xF7, 0xF5, 0xF0, 0xF2);
        public static readonly Color StrokeSoft  = new Color32(0xF7, 0xF5, 0xF0, 0x55);

        public static readonly Color TextPrimary   = Paper;
        public static readonly Color TextSecondary = new Color32(0xB4, 0xB4, 0xBE, 0xFF);

        // ==================== CHEFS ====================
        public static readonly string[] ChefNames  = { "Chef Ruby", "Chef Sapphire", "Chef Emerald", "Chef Amber" };
        public static readonly Color[]  ChefColors =
        {
            new Color32(0xFF, 0x4D, 0x5E, 0xFF), // Ruby
            new Color32(0x3D, 0x8B, 0xFF, 0xFF), // Sapphire
            new Color32(0x2E, 0xD6, 0x7A, 0xFF), // Emerald
            new Color32(0xFF, 0xAA, 0x00, 0xFF), // Amber
        };

        public static string ChefName(int slot) { return ChefNames[Mathf.Clamp(slot, 0, 3)]; }
        public static Color ChefColor(int slot) { return ChefColors[Mathf.Clamp(slot, 0, 3)]; }
        public static string ChefHex(int slot) { return Hex(ChefColor(slot)); }

        /// <summary>Returns "#RRGGBB" for use inside TMP rich text.</summary>
        public static string Hex(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }

        public static Color WithAlpha(Color c, float a) { return new Color(c.r, c.g, c.b, a); }

        // ==================== FONT ====================
        private static TMP_FontAsset cachedFont;
        public static TMP_FontAsset Font
        {
            get
            {
                // Drop a TMP font asset at Assets/Resources/Fonts/AuraFont.asset to restyle the whole game.
                if (cachedFont == null)
                    cachedFont = Resources.Load<TMP_FontAsset>("Fonts/AuraFont");
                if (cachedFont == null)
                    cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                return cachedFont;
            }
        }

        // ==================== PROCEDURAL SPRITES ====================
        private const int TexSize = 64;
        private const int Radius = 18;
        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

        private static float RoundedBoxSdf(float x, float y, float half, float r)
        {
            float qx = Mathf.Abs(x) - (half - r);
            float qy = Mathf.Abs(y) - (half - r);
            float ox = Mathf.Max(qx, 0f);
            float oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static Sprite BuildSprite(string key, int size, float border, Func<float, float, float> alphaAt)
        {
            Sprite cached;
            if (spriteCache.TryGetValue(key, out cached) && cached != null) return cached;

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color32[] px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(alphaAt(x - c, y - c));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);

            Sprite s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            s.name = key;
            spriteCache[key] = s;
            return s;
        }

        /// <summary>9-sliced filled rounded rectangle (white, tint with Image.color).</summary>
        public static Sprite RoundedFill
        {
            get
            {
                return BuildSprite("aura_rounded_fill", TexSize, Radius + 2, (x, y) =>
                    0.5f - RoundedBoxSdf(x, y, TexSize * 0.5f - 0.5f, Radius));
            }
        }

        /// <summary>9-sliced rounded outline ring (white, tint with Image.color).</summary>
        public static Sprite RoundedRing
        {
            get
            {
                return BuildSprite("aura_rounded_ring", TexSize, Radius + 2, (x, y) =>
                {
                    float d = RoundedBoxSdf(x, y, TexSize * 0.5f - 0.5f, Radius);
                    float outer = 0.5f - d;
                    float inner = 0.5f - (d + 3.4f);
                    return Mathf.Clamp01(outer) - Mathf.Clamp01(inner);
                });
            }
        }

        /// <summary>9-sliced soft drop shadow, meant to be drawn larger than its panel.</summary>
        public static Sprite SoftShadow
        {
            get
            {
                return BuildSprite("aura_soft_shadow", 96, 44, (x, y) =>
                {
                    float d = RoundedBoxSdf(x, y, 30f, 16f);
                    return Mathf.Clamp01(1f - d / 18f) * Mathf.Clamp01(1f - d / 18f) * 0.85f;
                });
            }
        }

        /// <summary>Soft round dot / glow.</summary>
        public static Sprite Dot
        {
            get
            {
                return BuildSprite("aura_dot", TexSize, 0f, (x, y) =>
                {
                    float d = Mathf.Sqrt(x * x + y * y) - (TexSize * 0.5f - 1.5f);
                    return 0.5f - d;
                });
            }
        }

        // ==================== BUILDERS ====================

        public static RectTransform MakeRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        /// <summary>
        /// Glass panel: drop shadow, rounded fill and an accent outline.
        /// Returns the panel root (children should be parented to it).
        /// </summary>
        public static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 pos, Vector2 size, Color fill, Color accent, bool shadow = true)
        {
            RectTransform rt = MakeRect(parent, name);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = RoundedFill;
            img.type = Image.Type.Sliced;
            img.color = fill;

            if (shadow) AddHardShadow(img.gameObject);

            if (accent.a > 0.001f)
            {
                RectTransform ring = MakeRect(rt, "Stroke");
                ring.anchorMin = Vector2.zero;
                ring.anchorMax = Vector2.one;
                ring.offsetMin = Vector2.zero;
                ring.offsetMax = Vector2.zero;
                Image rImg = ring.gameObject.AddComponent<Image>();
                rImg.sprite = RoundedRing;
                rImg.type = Image.Type.Sliced;
                rImg.color = accent;
                rImg.raycastTarget = false;
            }

            return rt.gameObject;
        }

        /// <summary>Cartoon "sticker" shadow: solid, offset down-right, drawn behind the graphic.</summary>
        public static void AddHardShadow(GameObject go)
        {
            Shadow sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
            sh.effectDistance = new Vector2(5f, -6f);
            sh.useGraphicAlpha = false;
        }

        public enum ButtonStyle { Primary, Success, Danger, Ghost, Gold }

        public static void GetButtonColors(ButtonStyle style, out Color fill, out Color text, out Color accent)
        {
            switch (style)
            {
                case ButtonStyle.Success: // the one loud call-to-action color
                    fill = Amber; text = Ink; accent = Ink; break;
                case ButtonStyle.Danger:
                    fill = Ink; text = Danger; accent = Danger; break;
                case ButtonStyle.Gold:
                    fill = Ink; text = Gold; accent = Gold; break;
                case ButtonStyle.Ghost:
                    fill = new Color32(0x14, 0x14, 0x18, 0xF0); text = Paper; accent = new Color32(0xF7, 0xF5, 0xF0, 0xA0); break;
                default: // Primary: white sticker
                    fill = Paper; text = Ink; accent = Ink; break;
            }
        }

        /// <summary>Maps the legacy per-button colors used by the lobby onto the cartoon styles.</summary>
        public static ButtonStyle StyleFromColors(Color bg, Color text)
        {
            if (text.r > 0.9f && text.g > 0.7f && text.b < 0.35f) return ButtonStyle.Gold;
            if (bg.g > 0.38f && bg.g > bg.r * 1.4f && bg.g > bg.b) return ButtonStyle.Success;
            if (bg.r > 0.35f && bg.r > bg.g * 1.8f && bg.r > bg.b * 1.5f) return ButtonStyle.Danger;
            if (bg.b < 0.28f && bg.r < 0.2f) return ButtonStyle.Ghost;
            return ButtonStyle.Primary;
        }

        public static GameObject CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size,
            ButtonStyle style, Action onClick = null, float fontSize = 18f, Sprite icon = null)
        {
            Color fill, text, accent;
            GetButtonColors(style, out fill, out text, out accent);
            return CreateButton(parent, name, label, pos, size, fill, text, accent, onClick, fontSize, icon);
        }

        public static GameObject CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size,
            Color fill, Color textColor, Color accent, Action onClick = null, float fontSize = 18f, Sprite icon = null)
        {
            RectTransform rt = MakeRect(parent, name);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = RoundedFill;
            img.type = Image.Type.Sliced;
            img.color = fill;
            AddHardShadow(img.gameObject);

            RectTransform ring = MakeRect(rt, "Stroke");
            ring.anchorMin = Vector2.zero;
            ring.anchorMax = Vector2.one;
            ring.offsetMin = Vector2.zero;
            ring.offsetMax = Vector2.zero;
            Image rImg = ring.gameObject.AddComponent<Image>();
            rImg.sprite = RoundedRing;
            rImg.type = Image.Type.Sliced;
            rImg.color = accent;
            rImg.raycastTarget = false;

            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            rt.gameObject.AddComponent<AuraButtonFx>();

            float textInset = 0f;
            if (icon != null)
            {
                float iconSize = Mathf.Min(size.y * 0.5f, 34f);
                RectTransform ir = MakeRect(rt, "Icon");
                ir.anchorMin = new Vector2(0f, 0.5f);
                ir.anchorMax = new Vector2(0f, 0.5f);
                ir.pivot = new Vector2(0f, 0.5f);
                ir.anchoredPosition = new Vector2(size.y * 0.3f, 0f);
                ir.sizeDelta = new Vector2(iconSize, iconSize);
                Image iImg = ir.gameObject.AddComponent<Image>();
                iImg.sprite = icon;
                iImg.color = textColor;
                iImg.raycastTarget = false;
                textInset = size.y * 0.3f + iconSize * 0.5f;
            }

            TextMeshProUGUI tmp = CreateText(rt, label, fontSize, FontStyles.Bold, Vector2.zero, size, textColor);
            tmp.rectTransform.anchorMin = Vector2.zero;
            tmp.rectTransform.anchorMax = Vector2.one;
            tmp.rectTransform.offsetMin = new Vector2(textInset, 0f);
            tmp.rectTransform.offsetMax = Vector2.zero;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = Mathf.Max(8f, fontSize * 0.6f);
            tmp.fontSizeMax = fontSize;

            return rt.gameObject;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string text, float size, FontStyles style,
            Vector2 pos, Vector2 dimensions, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            RectTransform rt = MakeRect(parent, "Text");
            rt.anchoredPosition = pos;
            rt.sizeDelta = dimensions;

            TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) tmp.font = Font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>Anchors a rect to its parent's top-left corner at (x, y-from-top) with a fixed size.</summary>
        public static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static Image CreateIcon(Transform parent, Sprite sprite, Vector2 pos, float size, Color tint)
        {
            RectTransform rt = MakeRect(parent, "Icon");
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = tint;
            img.raycastTarget = false;
            return img;
        }
    }
}
