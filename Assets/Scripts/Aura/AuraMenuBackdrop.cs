using System;
using UnityEngine;
using UnityEngine.UI;

namespace AuraKitchen
{
    /// <summary>
    /// Painted kitchen set behind the main menu and the lobby: blue tiled wall, orange counter, cream tile floor
    /// (the "Kitchen Aura UI Kit" look). It is a far-plane camera canvas, so 3D chefs and the lobby stage render in front.
    /// </summary>
    public class AuraMenuBackdrop : MonoBehaviour
    {
        public static readonly Color Wall = new Color32(0x4D, 0x8F, 0xD6, 255);

        private static AuraMenuBackdrop instance;
        private Canvas canvas;
        private Camera cam;

        public static AuraMenuBackdrop Ensure()
        {
            if (instance != null) return instance;
            Camera main = Camera.main;
            if (main == null) return null;

            GameObject go = new GameObject("Aura_MenuBackdrop");
            instance = go.AddComponent<AuraMenuBackdrop>();
            instance.Build(main);
            return instance;
        }

        public static void SetVisible(bool v)
        {
            if (instance == null) { if (v) Ensure(); return; }
            instance.gameObject.SetActive(v);
        }

        private void Build(Camera main)
        {
            cam = main;
            main.clearFlags = CameraClearFlags.SolidColor;
            main.backgroundColor = Wall;

            // the original tiled floor plane / huge meshes would hide the painted set
            try
            {
                Renderer[] rs = FindObjectsOfType<Renderer>();
                for (int i = 0; i < rs.Length; i++)
                {
                    Renderer r = rs[i];
                    if (r == null || r is ParticleSystemRenderer) continue;
                    if (r.GetComponentInParent<Canvas>() != null) continue;
                    if (r.gameObject.name == "Floor" || r.bounds.size.x > 30f || r.bounds.size.z > 30f)
                        r.enabled = false;
                }
            }
            catch (Exception e) { Debug.LogWarning("[AuraMenuBackdrop] hide floor: " + e.Message); }

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = main;
            float far = main.farClipPlane;
            canvas.planeDistance = Mathf.Clamp(far * 0.85f, main.nearClipPlane + 1f, 90f);
            canvas.sortingOrder = -500;
            CanvasScaler sc = gameObject.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;

            Color ink = AuraTheme.Ink;

            // wall: vertical gradient + faint tile grid
            Image wall = Panel("Wall", 0f, 1f, MakeGradient(new Color32(0x3C, 0x7C, 0xC8, 255), new Color32(0x86, 0xC0, 0xF0, 255)), Color.white);
            wall.type = Image.Type.Simple;
            Image grid = Panel("WallGrid", 0.42f, 1f, MakeTile(96, new Color(1f, 1f, 1f, 0.10f), new Color(1f, 1f, 1f, 0f), 2), Color.white);
            grid.type = Image.Type.Tiled;

            // counter top (orange) + front (darker) with ink borders
            Color32 top = new Color32(0xF5, 0x95, 0x32, 255);
            Color32 front = new Color32(0xDC, 0x6E, 0x1E, 255);
            Panel("CounterTop", 0.535f, 0.585f, Solid(top), Color.white);
            Panel("CounterTopLip", 0.579f, 0.585f, Solid(new Color32(0xFF, 0xC0, 0x6A, 255)), Color.white);
            Panel("CounterFront", 0.426f, 0.535f, Solid(front), Color.white);
            Panel("CounterBorderTop", 0.583f, 0.589f, Solid(ink), Color.white);
            Panel("CounterBorderMid", 0.532f, 0.538f, Solid(ink), Color.white);
            Panel("CounterBorderBot", 0.423f, 0.429f, Solid(ink), Color.white);

            // cabinet doors
            int doors = 6;
            for (int i = 0; i <= doors; i++)
            {
                float x = i / (float)doors;
                RectTransform d = Strip("Divider" + i, x, 0.43f, 0.532f, 6f, ink);
                if (i < doors)
                {
                    float cx = x + 0.5f / doors;
                    Strip("Handle" + i, cx, 0.488f, 0.5f, 54f, new Color32(0xFF, 0xE3, 0xA9, 255));
                }
            }

            // cream tiled floor
            Image floor = Panel("Floor", 0f, 0.423f, MakeTile(128, new Color32(0xE6, 0xC9, 0x8E, 255), new Color32(0xFF, 0xF3, 0xD6, 255), 3), Color.white);
            floor.type = Image.Type.Tiled;
            Panel("FloorShade", 0.34f, 0.423f, Solid(new Color(0.23f, 0.11f, 0.06f, 0.12f)), Color.white);
        }

        private Image Panel(string name, float yMin, float yMax, Sprite sp, Color tint)
        {
            RectTransform r = AuraTheme.MakeRect(transform, name);
            r.anchorMin = new Vector2(0f, yMin);
            r.anchorMax = new Vector2(1f, yMax);
            r.offsetMin = r.offsetMax = Vector2.zero;
            Image im = r.gameObject.AddComponent<Image>();
            im.sprite = sp;
            im.color = tint;
            im.raycastTarget = false;
            return im;
        }

        private RectTransform Strip(string name, float x, float yMin, float yMax, float width, Color c)
        {
            RectTransform r = AuraTheme.MakeRect(transform, name);
            r.anchorMin = new Vector2(x, yMin);
            r.anchorMax = new Vector2(x, yMax);
            r.sizeDelta = new Vector2(width, 0f);
            Image im = r.gameObject.AddComponent<Image>();
            im.sprite = Solid(Color.white);
            im.color = c;
            im.raycastTarget = false;
            return r;
        }

        private static Sprite solid;
        private static Sprite Solid(Color c)
        {
            if (solid == null)
            {
                Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                t.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                t.Apply(false, true);
                solid = Sprite.Create(t, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
            }
            if (c == Color.white) return solid;
            // tinted solids: cache by colour through Image.color would be simpler, so bake a tiny texture
            Texture2D tx = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tx.SetPixels(new[] { c, c, c, c });
            tx.Apply(false, true);
            return Sprite.Create(tx, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeGradient(Color bottom, Color top)
        {
            const int h = 64;
            Texture2D t = new Texture2D(2, h, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(bottom, top, 1f - y / (float)(h - 1));
                t.SetPixel(0, y, c);
                t.SetPixel(1, y, c);
            }
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Square tile: fill with a line along the left and bottom edge (tiles seamlessly).</summary>
        private static Sprite MakeTile(int n, Color line, Color fill, int lineW)
        {
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, true);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Bilinear;
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = (x < lineW || y < lineW) ? (Color32)line : (Color32)fill;
            t.SetPixels32(px);
            t.Apply(true, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
