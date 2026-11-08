using System;
using System.Collections.Generic;
using UnityEngine;

namespace AuraKitchen
{
    public enum AuraIcon
    {
        Star, Coin, Clock, Plate, Cloche, Flame, ChefHat, Crown,
        Check, Cross, Person, Signal, WifiOff, Trophy
    }

    /// <summary>
    /// Procedural white icon sprites (signed-distance-field drawn at runtime).
    /// Tint them through Image.color so one sprite serves every palette color.
    /// Coordinates inside shape functions are normalized: x,y in [-1, 1], y up.
    /// </summary>
    public static class AuraIcons
    {
        private const int Size = 96;
        private static readonly Dictionary<AuraIcon, Sprite> cache = new Dictionary<AuraIcon, Sprite>();

        public static Sprite Get(AuraIcon icon)
        {
            Sprite s;
            if (cache.TryGetValue(icon, out s) && s != null) return s;

            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color32[] px = new Color32[Size * Size];
            float half = (Size - 1) * 0.5f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float nx = (x - half) / half;
                    float ny = (y - half) / half;
                    float d = Shape(icon, new Vector2(nx, ny));
                    float a = Mathf.Clamp01(0.5f - d * half);
                    px[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);

            s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            s.name = "aura_icon_" + icon;
            cache[icon] = s;
            return s;
        }

        // ---------- SDF primitives ----------
        private static float Circle(Vector2 p, float cx, float cy, float r)
        {
            float dx = p.x - cx, dy = p.y - cy;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        private static float Ring(Vector2 p, float cx, float cy, float r, float t)
        {
            return Mathf.Abs(Circle(p, cx, cy, r)) - t;
        }

        private static float Box(Vector2 p, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(p.x - cx) - (hx - r);
            float qy = Mathf.Abs(p.y - cy) - (hy - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static float Capsule(Vector2 p, float ax, float ay, float bx, float by, float r)
        {
            float pax = p.x - ax, pay = p.y - ay;
            float bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        private static float Poly(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i];
                Vector2 w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        private static float Union(float a, float b) { return Mathf.Min(a, b); }
        private static float Subtract(float a, float b) { return Mathf.Max(a, -b); }
        private static float Intersect(float a, float b) { return Mathf.Max(a, b); }

        private static Vector2[] StarPoints(float outer, float inner)
        {
            Vector2[] pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = (i % 2 == 0) ? outer : inner;
                pts[i] = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r - 0.05f);
            }
            return pts;
        }

        // ---------- Icon shapes ----------
        private static float Shape(AuraIcon icon, Vector2 p)
        {
            switch (icon)
            {
                case AuraIcon.Star:
                    return Poly(p, StarPoints(0.92f, 0.40f));

                case AuraIcon.Coin:
                {
                    float body = Circle(p, 0f, 0f, 0.9f);
                    float groove = Ring(p, 0f, 0f, 0.62f, 0.06f);
                    float mark = Box(p, 0f, 0f, 0.12f, 0.36f, 0.05f);
                    return Union(Subtract(body, groove), mark);
                }

                case AuraIcon.Clock:
                {
                    float rim = Ring(p, 0f, 0f, 0.82f, 0.11f);
                    float h1 = Capsule(p, 0f, 0f, 0f, 0.5f, 0.09f);
                    float h2 = Capsule(p, 0f, 0f, 0.33f, -0.1f, 0.09f);
                    return Union(rim, Union(h1, h2));
                }

                case AuraIcon.Plate:
                    return Union(Ring(p, 0f, 0f, 0.86f, 0.09f), Circle(p, 0f, 0f, 0.48f));

                case AuraIcon.Cloche:
                {
                    float dome = Intersect(Circle(p, 0f, -0.1f, 0.72f), -(p.y + 0.1f));
                    float baseBar = Box(p, 0f, -0.3f, 0.92f, 0.1f, 0.08f);
                    float knob = Circle(p, 0f, 0.72f, 0.12f);
                    return Union(dome, Union(baseBar, knob));
                }

                case AuraIcon.Flame:
                {
                    float body = Circle(p, 0f, -0.25f, 0.58f);
                    Vector2[] tip = { new Vector2(-0.5f, -0.05f), new Vector2(0.5f, -0.05f), new Vector2(0.08f, 0.95f) };
                    float outer = Union(body, Poly(p, tip));
                    float core = Circle(p, 0f, -0.38f, 0.26f);
                    return Subtract(outer, core);
                }

                case AuraIcon.ChefHat:
                {
                    float puff = Union(Circle(p, -0.42f, 0.2f, 0.4f), Union(Circle(p, 0f, 0.36f, 0.46f), Circle(p, 0.42f, 0.2f, 0.4f)));
                    float band = Box(p, 0f, -0.4f, 0.58f, 0.34f, 0.08f);
                    float seam = Box(p, 0f, -0.14f, 0.58f, 0.04f, 0.01f);
                    return Subtract(Union(puff, band), seam);
                }

                case AuraIcon.Crown:
                {
                    Vector2[] pts =
                    {
                        new Vector2(-0.88f, -0.58f), new Vector2(0.88f, -0.58f), new Vector2(0.92f, 0.5f),
                        new Vector2(0.42f, -0.04f), new Vector2(0f, 0.74f), new Vector2(-0.42f, -0.04f), new Vector2(-0.92f, 0.5f)
                    };
                    return Poly(p, pts);
                }

                case AuraIcon.Check:
                    return Union(Capsule(p, -0.62f, 0f, -0.16f, -0.46f, 0.16f), Capsule(p, -0.16f, -0.46f, 0.66f, 0.5f, 0.16f));

                case AuraIcon.Cross:
                    return Union(Capsule(p, -0.55f, -0.55f, 0.55f, 0.55f, 0.15f), Capsule(p, -0.55f, 0.55f, 0.55f, -0.55f, 0.15f));

                case AuraIcon.Person:
                    return Union(Circle(p, 0f, 0.42f, 0.32f), Box(p, 0f, -0.52f, 0.6f, 0.38f, 0.3f));

                case AuraIcon.Signal:
                    return Union(Box(p, -0.6f, -0.45f, 0.14f, 0.22f, 0.05f),
                           Union(Box(p, -0.2f, -0.3f, 0.14f, 0.38f, 0.05f),
                           Union(Box(p, 0.2f, -0.12f, 0.14f, 0.56f, 0.05f),
                                 Box(p, 0.6f, 0.1f, 0.14f, 0.78f, 0.05f))));

                case AuraIcon.WifiOff:
                {
                    float wedgeMask = (Mathf.Abs(p.x) - (p.y + 0.62f)) * 0.7071f;
                    float arcs = Union(Intersect(Ring(p, 0f, -0.62f, 0.4f, 0.075f), wedgeMask),
                                 Union(Intersect(Ring(p, 0f, -0.62f, 0.78f, 0.075f), wedgeMask),
                                       Intersect(Ring(p, 0f, -0.62f, 1.16f, 0.075f), wedgeMask)));
                    float dot = Circle(p, 0f, -0.62f, 0.1f);
                    float slash = Capsule(p, -0.8f, 0.8f, 0.8f, -0.8f, 0.09f);
                    return Union(Union(arcs, dot), slash);
                }

                case AuraIcon.Trophy:
                {
                    float cup = Intersect(Circle(p, 0f, 0.15f, 0.6f), p.y - 0.15f);
                    float top = Box(p, 0f, 0.15f, 0.6f, 0.07f, 0.03f);
                    float stem = Box(p, 0f, -0.42f, 0.08f, 0.22f, 0.02f);
                    float foot = Box(p, 0f, -0.7f, 0.38f, 0.08f, 0.04f);
                    float h1 = Ring(p, -0.62f, 0.1f, 0.2f, 0.055f);
                    float h2 = Ring(p, 0.62f, 0.1f, 0.2f, 0.055f);
                    return Union(Union(cup, top), Union(Union(stem, foot), Union(h1, h2)));
                }
            }
            return 1f;
        }
    }
}
