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
        // ==================== PALETTE (derived from the kitchen: walnut counters, cream tiles, wall blue) ====================
        public static readonly Color Ink       = new Color32(0x1E, 0x10, 0x0A, 0xFF); // outlines + deep shadow
        public static readonly Color Paper     = new Color32(0xFF, 0xF1, 0xD0, 0xFF); // cream text / paper

        // "Cyan" is kept as a name so existing call sites keep compiling; it is now cream.
        public static readonly Color Cyan      = Paper;
        public static readonly Color Amber     = new Color32(0xFF, 0xB0, 0x20, 0xFF);
        public static readonly Color Gold      = new Color32(0xFF, 0xD5, 0x4A, 0xFF);
        public static readonly Color Success   = new Color32(0x5B, 0xD3, 0x6B, 0xFF);
        public static readonly Color Danger    = new Color32(0xFF, 0x5A, 0x4A, 0xFF);
        public static readonly Color Ember     = new Color32(0xFF, 0x7A, 0x1A, 0xFF);

        public static readonly Color PanelFill   = new Color32(0x3B, 0x24, 0x16, 0xF5); // dark walnut board
        public static readonly Color PanelRaised = new Color32(0x55, 0x36, 0x20, 0xFA); // lighter plank
        public static readonly Color PanelDeep   = new Color32(0x24, 0x14, 0x0B, 0xFA);
        public static readonly Color Scrim       = new Color32(0x14, 0x0A, 0x05, 0xDB);
        public static readonly Color Stroke      = Ink;                                  // chunky dark outline
        public static readonly Color StrokeSoft  = new Color32(0x1E, 0x10, 0x0A, 0xC0);

        public static readonly Color TextPrimary   = Paper;
        public static readonly Color TextSecondary = new Color32(0xE0, 0xC2, 0x96, 0xFF); // tan

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

        private static Sprite BuildSprite(string key, int size, float border, Func<float, float, float> alphaAt, Func<float, float> shadeByHeight = null)
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
                    byte v = 255;
                    if (shadeByHeight != null) v = (byte)Mathf.RoundToInt(Mathf.Clamp01(shadeByHeight(y / (size - 1f))) * 255f);
                    px[y * size + x] = new Color32(v, v, v, (byte)Mathf.RoundToInt(a * 255f));
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

        /// <summary>Filled rounded rectangle with a vertical bevel (lighter top, darker bottom). Tint with Image.color.</summary>
        public static Sprite RoundedFace
        {
            get
            {
                return BuildSprite("aura_rounded_face", TexSize, Radius + 2, (x, y) =>
                    0.5f - RoundedBoxSdf(x, y, TexSize * 0.5f - 0.5f, Radius),
                    t => Mathf.Lerp(0.74f, 1f, Mathf.SmoothStep(0f, 1f, t)));
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

            bool framed = accent.a > 0.001f;

            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = framed ? RoundedFace : RoundedFill;
            img.type = Image.Type.Sliced;
            img.color = fill;

            if (shadow) AddHardShadow(img.gameObject);

            if (framed)
            {
                // chunky outline
                AddRing(rt, "Stroke", accent, 0f);
                // light inner rim = bevel highlight (only on dark outlines)
                if (accent.a > 0.5f && accent.r + accent.g + accent.b < 1.2f)
                {
                    AddRing(rt, "Rim", new Color(1f, 0.86f, 0.62f, 0.22f), 4f);
                }

                // rivets on big boards
                if (size.x >= 300f && size.y >= 110f && shadow)
                {
                    float inset = 13f;
                    Vector2[] corners = { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) };
                    for (int i = 0; i < 4; i++)
                    {
                        RectTransform rv = MakeRect(rt, "Rivet");
                        rv.anchorMin = corners[i];
                        rv.anchorMax = corners[i];
                        rv.pivot = new Vector2(0.5f, 0.5f);
                        rv.anchoredPosition = new Vector2(corners[i].x < 0.5f ? inset : -inset, corners[i].y > 0.5f ? -inset : inset);
                        rv.sizeDelta = new Vector2(8f, 8f);
                        Image rvImg = rv.gameObject.AddComponent<Image>();
                        rvImg.sprite = Dot;
                        rvImg.color = new Color(0.97f, 0.78f, 0.5f, 0.55f);
                        rvImg.raycastTarget = false;
                    }
                }
            }

            return rt.gameObject;
        }

        private static Image AddRing(RectTransform parent, string name, Color color, float inset)
        {
            RectTransform ring = MakeRect(parent, name);
            ring.anchorMin = Vector2.zero;
            ring.anchorMax = Vector2.one;
            ring.offsetMin = new Vector2(inset, inset);
            ring.offsetMax = new Vector2(-inset, -inset);
            Image rImg = ring.gameObject.AddComponent<Image>();
            rImg.sprite = RoundedRing;
            rImg.type = Image.Type.Sliced;
            rImg.color = color;
            rImg.raycastTarget = false;
            return rImg;
        }

        /// <summary>Cartoon "sticker" shadow: solid, offset down-right, drawn behind the graphic.</summary>
        public static void AddHardShadow(GameObject go)
        {
            Shadow sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0.06f, 0.03f, 0.01f, 0.55f);
            sh.effectDistance = new Vector2(0f, -7f);
            sh.useGraphicAlpha = false;
        }

        public enum ButtonStyle { Primary, Success, Danger, Ghost, Gold }

        /// <summary>fill = candy face color; the darker "lip" under it is derived from it.</summary>
        public static void GetButtonColors(ButtonStyle style, out Color fill, out Color text, out Color accent)
        {
            accent = Ink;
            switch (style)
            {
                case ButtonStyle.Success:
                    fill = new Color32(0x4F, 0xC9, 0x5B, 0xFF); text = Paper; break;
                case ButtonStyle.Danger:
                    fill = new Color32(0xE5, 0x53, 0x3C, 0xFF); text = Paper; break;
                case ButtonStyle.Gold:
                    fill = new Color32(0xFF, 0xC5, 0x3A, 0xFF); text = new Color32(0x4A, 0x2A, 0x10, 0xFF); break;
                case ButtonStyle.Ghost:
                    fill = new Color32(0x8B, 0x5A, 0x36, 0xFF); text = Paper; break;
                default: // Primary: wall blue
                    fill = new Color32(0x3F, 0x95, 0xDB, 0xFF); text = Paper; break;
            }
        }

        private static Color Lip(Color face)
        {
            return new Color(face.r * 0.55f, face.g * 0.5f, face.b * 0.5f, 1f);
        }

        /// <summary>Re-skins an existing themed button (used by tabs that toggle between active/inactive).</summary>
        public static void ApplyButtonStyle(GameObject btn, ButtonStyle style)
        {
            if (btn == null) return;
            Color fill, text, accent;
            GetButtonColors(style, out fill, out text, out accent);

            Image root = btn.GetComponent<Image>();
            if (root != null) root.color = Lip(fill);

            Transform face = btn.transform.Find("Face");
            if (face != null)
            {
                Image fi = face.GetComponent<Image>();
                if (fi != null) fi.color = fill;
                TextMeshProUGUI label = face.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.color = text;
                    label.outlineWidth = style == ButtonStyle.Gold ? 0f : 0.22f;
                    label.outlineColor = (Color32)Ink;
                }
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
            float lipH = Mathf.Clamp(size.y * 0.11f, 4f, 8f);

            RectTransform rt = MakeRect(parent, name);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            // root = the dark "lip" you see under the face
            Image root = rt.gameObject.AddComponent<Image>();
            root.sprite = RoundedFill;
            root.type = Image.Type.Sliced;
            root.color = Lip(fill);
            AddHardShadow(root.gameObject);

            // face = the candy top, bevelled, sits above the lip
            RectTransform faceRt = MakeRect(rt, "Face");
            faceRt.anchorMin = Vector2.zero;
            faceRt.anchorMax = Vector2.one;
            faceRt.offsetMin = new Vector2(0f, lipH);
            faceRt.offsetMax = Vector2.zero;
            Image face = faceRt.gameObject.AddComponent<Image>();
            face.sprite = RoundedFace;
            face.type = Image.Type.Sliced;
            face.color = fill;

            // gloss strip
            RectTransform glossRt = MakeRect(faceRt, "Gloss");
            glossRt.anchorMin = new Vector2(0f, 0.52f);
            glossRt.anchorMax = new Vector2(1f, 1f);
            glossRt.offsetMin = new Vector2(8f, 0f);
            glossRt.offsetMax = new Vector2(-8f, -4f);
            Image gloss = glossRt.gameObject.AddComponent<Image>();
            gloss.sprite = RoundedFill;
            gloss.type = Image.Type.Sliced;
            gloss.color = new Color(1f, 1f, 1f, 0.2f);
            gloss.raycastTarget = false;

            // outline around lip + face
            AddRing(rt, "Stroke", accent, 0f);

            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = face;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.75f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            rt.gameObject.AddComponent<AuraButtonFx>();

            float textInset = 0f;
            if (icon != null)
            {
                float iconSize = Mathf.Min(size.y * 0.5f, 34f);
                RectTransform ir = MakeRect(faceRt, "Icon");
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

            TextMeshProUGUI tmp = CreateText(faceRt, label, fontSize, FontStyles.Bold, Vector2.zero, size, textColor);
            tmp.rectTransform.anchorMin = Vector2.zero;
            tmp.rectTransform.anchorMax = Vector2.one;
            tmp.rectTransform.offsetMin = new Vector2(textInset + 4f, 0f);
            tmp.rectTransform.offsetMax = new Vector2(-4f, 0f);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = Mathf.Max(8f, fontSize * 0.6f);
            tmp.fontSizeMax = fontSize;
            // dark text (Gold style) needs no outline; light text gets a dark one
            bool darkText = (textColor.r + textColor.g + textColor.b) < 1.4f;
            tmp.outlineWidth = darkText ? 0f : 0.22f;
            tmp.outlineColor = (Color32)Ink;

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

            // cartoon lettering: dark outline on light text that is big enough to carry it
            bool lightText = (color.r + color.g + color.b) > 1.6f;
            if (lightText && size >= 15f)
            {
                tmp.outlineWidth = size >= 26f ? 0.2f : 0.24f;
                tmp.outlineColor = (Color32)Ink;
            }
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
