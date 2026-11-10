using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace AuraKitchen
{
    /// <summary>
    /// Re-skins the UI that is baked into the scenes and prefabs (main menu, pause, options, game over,
    /// order board) with the Aura cartoon kit at runtime, so no Inspector or scene edits are required.
    /// Procedurally built Aura screens already use AuraTheme and are skipped via AuraSkinned / font checks.
    /// </summary>
    public class AuraSceneSkin : MonoBehaviour
    {
        private static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            if (hooked) return;
            hooked = true;
            SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (mode != LoadSceneMode.Single) return;
                Spawn();
            };
        }

        // The very first scene (the main menu) is already up by the time AfterSceneLoad runs; make sure it is skinned too.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SkinFirstScene()
        {
            Spawn();
        }

        private static void Spawn()
        {
            if (FindObjectOfType<AuraSceneSkin>() != null) return;
            new GameObject("Aura_SceneSkin").AddComponent<AuraSceneSkin>();
        }

        private IEnumerator Start()
        {
            yield return null;
            Apply();
            // late-created UI (prefabs instantiated in Start of other scripts)
            yield return new WaitForSecondsRealtime(0.6f);
            Apply();
            yield return new WaitForSecondsRealtime(1.5f);
            Apply();
            Destroy(gameObject);
        }

        // ---------------------------------------------------------------- pass
        public static void Apply()
        {
            Canvas.ForceUpdateCanvases();
            bool mainMenu = SceneManager.GetActiveScene().name == SceneSwitcher.Scene.MainMenu.ToString();

            // one bad element must never stop the rest of the scene from being skinned
            Button[] buttons = FindObjectsOfType<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                try { SkinButton(buttons[i], i); }
                catch (Exception e) { Debug.LogWarning("[AuraSceneSkin] button " + (buttons[i] != null ? buttons[i].name : "?") + ": " + e.Message); }
            }

            Image[] images = FindObjectsOfType<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                try { SkinPanel(images[i]); }
                catch (Exception e) { Debug.LogWarning("[AuraSceneSkin] panel " + (images[i] != null ? images[i].name : "?") + ": " + e.Message); }
            }

            TextMeshProUGUI[] texts = FindObjectsOfType<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                try { SkinText(texts[i]); }
                catch (Exception e) { Debug.LogWarning("[AuraSceneSkin] text " + (texts[i] != null ? texts[i].name : "?") + ": " + e.Message); }
            }

            if (mainMenu)
            {
                try { DecorateMainMenu(); }
                catch (Exception e) { Debug.LogWarning("[AuraSceneSkin] menu decoration: " + e.Message); }
            }
        }

        // ---------------------------------------------------------------- buttons
        private static AuraTheme.ButtonStyle PickStyle(string label)
        {
            string l = (label ?? "").ToLowerInvariant();
            if (l.Contains("multi") || l.Contains("option") || l.Contains("setting"))
                return AuraTheme.ButtonStyle.Gold;
            if (l.Contains("play") || l.Contains("start") || l.Contains("resume") || l.Contains("again") ||
                l.Contains("host") || l.Contains("join") || l.Contains("create"))
                return AuraTheme.ButtonStyle.Danger;
            if (l.Contains("multi") || l.Contains("option") || l.Contains("setting"))
                return AuraTheme.ButtonStyle.Gold;
            if (l.Contains("quit") || l.Contains("menu") || l.Contains("exit") || l.Contains("back") ||
                l.Contains("close") || l.Contains("cancel"))
                return AuraTheme.ButtonStyle.Primary;
            return AuraTheme.ButtonStyle.Ghost;
        }

        private static void SkinButton(Button b, int order)
        {
            if (b == null || b.GetComponent<AuraSkinned>() != null || b.GetComponent<AuraButtonFx>() != null) return;
            if (b.transform.Find("Face") != null) return;

            Image img = b.targetGraphic as Image;
            if (img == null) img = b.GetComponent<Image>();
            TextMeshProUGUI label = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (img == null || label == null) return; // icon-only buttons keep their art

            b.gameObject.AddComponent<AuraSkinned>();

            Color fill, text, accent;
            AuraTheme.ButtonStyle style = PickStyle(label.text);
            AuraTheme.GetButtonColors(style, out fill, out text, out accent);

            img.sprite = AuraTheme.RoundedFace;
            img.type = Image.Type.Sliced;
            img.color = fill;

            // hard "lip" + ink outline under the face (Shadow stacks: last added sits furthest back)
            AddOffsetShadow(img.gameObject, AuraTheme.Lip(fill), new Vector2(0f, -7f));
            AddOffsetShadow(img.gameObject, AuraTheme.Ink, new Vector2(0f, -11f));

            RectTransform rt = img.rectTransform;

            RectTransform gloss = AuraTheme.MakeRect(rt, "Gloss");
            gloss.anchorMin = new Vector2(0f, 0.52f);
            gloss.anchorMax = new Vector2(1f, 1f);
            gloss.offsetMin = new Vector2(8f, 0f);
            gloss.offsetMax = new Vector2(-8f, -5f);
            Image gi = gloss.gameObject.AddComponent<Image>();
            gi.sprite = AuraTheme.RoundedFill;
            gi.type = Image.Type.Sliced;
            gi.color = new Color(1f, 1f, 1f, 0.3f);
            gi.raycastTarget = false;
            gloss.SetSiblingIndex(0);

            AuraTheme.AddRing(rt, "Stroke", AuraTheme.Ink, 0f);

            // tint-style hover states keep working on top of the candy colour
            b.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.selectedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.75f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;

            label.color = text;
            label.fontStyle = FontStyles.Normal;
            label.raycastTarget = false;
            if (AuraTheme.Font != null) label.font = AuraTheme.Font;
            AuraTheme.SetOutline(label, true);
            // keep the label above gloss/ring
            label.transform.SetAsLastSibling();
            if (label.transform.parent == rt)
            {
                Transform stroke = rt.Find("Stroke");
                if (stroke != null) stroke.SetAsLastSibling();
            }

            b.gameObject.AddComponent<AuraButtonFx>();
            AuraPopIn pop = b.gameObject.AddComponent<AuraPopIn>();
            pop.delay = 0.08f * Mathf.Min(order, 8);
        }

        private static void AddOffsetShadow(GameObject go, Color color, Vector2 offset)
        {
            Shadow s = go.AddComponent<Shadow>();
            s.effectColor = color;
            s.effectDistance = offset;
            s.useGraphicAlpha = false;
        }

        // ---------------------------------------------------------------- panels
        private static void SkinPanel(Image img)
        {
            if (img == null || img.GetComponent<AuraSkinned>() != null) return;
            if (img.name != "Background") return;
            if (img.GetComponentInParent<Selectable>() != null) return;
            Canvas canvas = img.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // the tutorial keeps its original art (the instructions are baked into the sprite)
            if (img.GetComponentInParent<TutorialBinding>() != null) return;

            // "DELIVERY" banners floating over the counters (world-space canvas)
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                if (img.transform.parent != null && img.transform.parent.name == "DeliveryBanner")
                {
                    img.gameObject.AddComponent<AuraSkinned>();
                    Color c = img.color;
                    bool good = c.g >= c.r;
                    img.sprite = AuraTheme.RoundedFace;
                    img.type = Image.Type.Sliced;
                    img.color = good ? new Color32(0x4F, 0xD3, 0x5A, 0xFF) : new Color32(0xE8, 0x39, 0x2D, 0xFF);
                    AuraTheme.AddHardShadow(img.gameObject);
                }
                return;
            }
            Canvas root = canvas.rootCanvas;

            img.gameObject.AddComponent<AuraSkinned>();
            Rect r = img.rectTransform.rect;
            Rect cr = ((RectTransform)root.transform).rect;
            bool fullScreen = r.width >= cr.width * 0.9f && r.height >= cr.height * 0.9f;

            if (fullScreen)
            {
                // dim the kitchen behind a menu instead of a flat slab
                img.sprite = null;
                img.color = AuraTheme.Scrim;
                return;
            }

            img.sprite = AuraTheme.RoundedFace;
            img.type = Image.Type.Sliced;
            img.color = AuraTheme.PanelFill;
            AuraTheme.AddHardShadow(img.gameObject, true);
            AuraTheme.AddRing(img.rectTransform, "Stroke", AuraTheme.Ink, 0f);
            AuraTheme.AddRing(img.rectTransform, "Rim", new Color(0.8f, 0.6f, 0.3f, 0.5f), 5f);
            img.gameObject.AddComponent<AuraBoard>();
        }

        // ---------------------------------------------------------------- text
        private static void SkinText(TextMeshProUGUI t)
        {
            if (t == null || t.GetComponent<AuraSkinned>() != null) return;
            if (t.GetComponentInParent<TMP_InputField>() != null) return;
            TMP_FontAsset font = AuraTheme.Font;
            if (font == null || t.font == font) return;

            t.gameObject.AddComponent<AuraSkinned>();
            t.font = font;
            t.fontStyle = FontStyles.Normal;

            Color c = t.color;
            bool light = (c.r + c.g + c.b) > 1.6f;
            bool onCream = t.GetComponentInParent<AuraBoard>() != null && t.GetComponentInParent<Button>() == null;
            if (onCream)
            {
                // cream boards carry chocolate lettering (keys and labels on the tutorial / option boards)
                if (light) t.color = AuraTheme.TextPrimary;
                AuraTheme.SetOutline(t, false);
            }
            else if (light)
            {
                AuraTheme.SetOutline(t, true);
            }

            // The order board heading becomes a candy pill so it reads as a game HUD tag
            if (t.name == "TitleText" && t.text.ToLowerInvariant().Contains("order"))
            {
                Transform parent = t.transform.parent;
                RectTransform pill = AuraTheme.MakeRect(parent, "TitlePill");
                RectTransform tr = t.rectTransform;
                pill.anchorMin = tr.anchorMin;
                pill.anchorMax = tr.anchorMax;
                pill.pivot = tr.pivot;
                pill.anchoredPosition = tr.anchoredPosition + new Vector2(-6f, 0f);
                pill.sizeDelta = tr.sizeDelta + new Vector2(0f, 12f);
                Image pi = pill.gameObject.AddComponent<Image>();
                pi.sprite = AuraTheme.RoundedFace;
                pi.type = Image.Type.Sliced;
                pi.color = new Color32(0xE8, 0x39, 0x2D, 0xFF);
                pi.raycastTarget = false;
                AuraTheme.AddHardShadow(pi.gameObject);
                AuraTheme.AddRing(pill, "Stroke", AuraTheme.Ink, 0f);
                pill.SetSiblingIndex(t.transform.GetSiblingIndex());
                t.alignment = TextAlignmentOptions.MidlineLeft;
                t.margin = new Vector4(18f, 0f, 0f, 0f);
            }
        }

        // ---------------------------------------------------------------- order tickets
        /// <summary>Turns the (inactive) order template into a cream paper ticket with a striped awning.</summary>
        public static void SkinTicket(GameObject ticket)
        {
            if (ticket == null || ticket.GetComponent<AuraSkinned>() != null) return;
            ticket.AddComponent<AuraSkinned>();

            Image bg = null;
            Image[] imgs = ticket.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < imgs.Length; i++)
                if (imgs[i].name == "Background") { bg = imgs[i]; break; }
            if (bg == null) bg = ticket.GetComponent<Image>();

            if (bg != null)
            {
                bg.gameObject.AddComponent<AuraSkinned>();
                bg.sprite = AuraTheme.RoundedFace;
                bg.type = Image.Type.Sliced;
                bg.color = AuraTheme.Paper;
                AuraTheme.AddHardShadow(bg.gameObject, true);
                AuraTheme.AddRing(bg.rectTransform, "Stroke", AuraTheme.Ink, 0f);

                RectTransform awning = AuraTheme.MakeRect(bg.rectTransform, "Awning");
                awning.anchorMin = new Vector2(0f, 1f);
                awning.anchorMax = new Vector2(1f, 1f);
                awning.pivot = new Vector2(0.5f, 1f);
                awning.offsetMin = new Vector2(8f, -16f);
                awning.offsetMax = new Vector2(-8f, -6f);
                Image ai = awning.gameObject.AddComponent<Image>();
                ai.sprite = AuraTheme.RoundedFill;
                ai.type = Image.Type.Sliced;
                ai.color = new Color32(0xE8, 0x39, 0x2D, 0xFF);
                ai.raycastTarget = false;
            }

            TextMeshProUGUI[] texts = ticket.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TextMeshProUGUI t = texts[i];
                if (t.GetComponent<AuraSkinned>() == null) t.gameObject.AddComponent<AuraSkinned>();
                if (AuraTheme.Font != null) t.font = AuraTheme.Font;
                t.fontStyle = FontStyles.Normal;
                t.color = AuraTheme.Ink;
                AuraTheme.SetOutline(t, false);
            }
        }

        // ---------------------------------------------------------------- main menu life
        private static Sprite LoadIcon(string file)
        {
            Texture2D tex = Resources.Load<Texture2D>("AuraUI/" + file);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void DecorateMainMenu()
        {
            if (GameObject.Find("Aura_MenuDeco") != null) return;
            AuraMenuBackdrop.Ensure();
            GameObject logo = GameObject.Find("GameLogo");
            Canvas canvas = logo != null ? logo.GetComponentInParent<Canvas>() : FindObjectOfType<Canvas>();
            if (canvas == null) return;
            canvas = canvas.rootCanvas;

            if (logo != null)
            {
                Image li = logo.GetComponent<Image>();
                if (li != null && logo.GetComponent<Shadow>() == null)
                {
                    Shadow sh = logo.AddComponent<Shadow>();
                    sh.effectColor = new Color(0.14f, 0.05f, 0.02f, 0.4f);
                    sh.effectDistance = new Vector2(0f, -14f);
                    sh.useGraphicAlpha = true;
                }
                // own canvas: the bobbing only re-batches the logo, not the whole menu
                if (logo.GetComponent<Canvas>() == null) logo.AddComponent<Canvas>();
                logo.AddComponent<AuraBob>();
                AuraPopIn lp = logo.AddComponent<AuraPopIn>();
                lp.duration = 0.8f;
            }

            RectTransform deco = AuraTheme.MakeRect(canvas.transform, "Aura_MenuDeco");
            deco.anchorMin = Vector2.zero;
            deco.anchorMax = Vector2.one;
            deco.offsetMin = Vector2.zero;
            deco.offsetMax = Vector2.zero;
            deco.SetAsFirstSibling(); // behind buttons and logo
            deco.gameObject.AddComponent<Canvas>(); // isolates the floating food from the menu canvas
            if (logo != null) deco.gameObject.AddComponent<AuraVisibleWith>().target = logo; // gone while the lobby is open

            string[] files = { "Bread", "Tomato", "CheeseSlice", "Cabbage", "MeatPattyCooked", "Plate" };
            // sky band above the chefs so the floating food never sits on top of a character
            Vector2[] anchors =
            {
                new Vector2(0.50f, 0.90f), new Vector2(0.60f, 0.80f), new Vector2(0.70f, 0.91f),
                new Vector2(0.80f, 0.81f), new Vector2(0.90f, 0.91f), new Vector2(0.97f, 0.78f),
            };
            float[] sizes = { 96f, 88f, 84f, 90f, 92f, 86f };
            for (int i = 0; i < files.Length; i++)
            {
                Sprite sp = LoadIcon(files[i]);
                if (sp == null) continue;
                RectTransform r = AuraTheme.MakeRect(deco, "Food_" + files[i]);
                r.anchorMin = anchors[i];
                r.anchorMax = anchors[i];
                r.sizeDelta = new Vector2(sizes[i], sizes[i]);
                r.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? -10f : 9f));
                Image im = r.gameObject.AddComponent<Image>();
                im.sprite = sp;
                im.preserveAspect = true;
                im.raycastTarget = false;
                Shadow sh = r.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0.14f, 0.05f, 0.02f, 0.45f);
                sh.effectDistance = new Vector2(0f, -8f);
                AuraFloat f = r.gameObject.AddComponent<AuraFloat>();
                f.phase = i * 1.1f;
                f.amplitude = 10f + (i % 3) * 3f;
            }
        }
    }
}
