using System.Collections.Generic;
using AdexansCastingMod.UI.Fonts;
using UnityEngine;

namespace AdexansCastingMod.UI
{
    // Low-level IMGUI drawing. Rounded rectangles use GUI.DrawTexture's built-in border radius,
    // so no textures besides one small gradient are created.
    internal static class Painter
    {
        static GUIStyle text;
        static Font uiFont;

        // Font for broadcast graphics (null = the UI system font). Menus keep the UI font unless asked otherwise.
        static FontFace broadcastFace;
        public static bool MenuMode;
        public static bool MenuUsesBroadcastFont;
        static Texture2D gradient;
        static Texture2D slant;
        static Texture2D triangle;
        static Texture2D trapezoid;
        static readonly GUIContent content = new GUIContent();

        // Text measuring is the most expensive part of the overlay, so widths and fitted strings are cached
        static readonly Dictionary<string, float> widthCache = new Dictionary<string, float>();
        static readonly Dictionary<string, string> fitCache = new Dictionary<string, string>();

        // System UI font, also used by text fields (IMGUI input can't use atlas fonts)
        public static Font Font => uiFont;
        static bool Repaint => Event.current.type == EventType.Repaint;

        static FontFace Active => (!MenuMode || MenuUsesBroadcastFont) ? broadcastFace : null;

        public static void SetBroadcastFace(FontFace face)
        {
            if (face == broadcastFace) return;
            broadcastFace = face;
            widthCache.Clear();
            fitCache.Clear();
        }

        public static void Init()
        {
            if (text != null) return;

            try
            {
                uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Segoe UI Symbol", "Arial" }, 16);
            }
            catch
            {
                uiFont = null;
            }

            text = new GUIStyle
            {
                font = uiFont,
                wordWrap = false,
                clipping = TextClipping.Clip,
                richText = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };

            if (gradient == null)
            {
                gradient = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                for (int i = 0; i < 32; i++)
                {
                    float t = i / 31f;
                    gradient.SetPixel(0, i, new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 1f, t)));
                }
                gradient.Apply();
            }

            if (slant == null) slant = MaskTexture(48, 32, (x, y) => x >= y * 0.5f && x <= 32f + y * 0.5f);
            // Full width at the top, ~7% narrower on each side at the bottom (y = 0 is the bottom row)
            if (trapezoid == null) trapezoid = MaskTexture(64, 32, (x, y) => x >= 4.5f * (1f - y / 32f) && x <= 64f - 4.5f * (1f - y / 32f));
            if (triangle == null) triangle = MaskTexture(32, 32, (x, y) => Mathf.Abs(x - 15.5f) <= (y + 0.5f) * 0.5f);
        }

        // Builds an anti-aliased (4x supersampled) white alpha mask; y = 0 is the bottom row
        static Texture2D MaskTexture(int w, int h, System.Func<float, float, bool> inside)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            if (inside(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f)) hits++;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, hits / 16f));
                }
            }
            tex.Apply();
            return tex;
        }

        public static void Dispose()
        {
            if (uiFont != null) Object.Destroy(uiFont);
            uiFont = null;
            broadcastFace = null;
            if (gradient != null) Object.Destroy(gradient);
            if (slant != null) Object.Destroy(slant);
            if (triangle != null) Object.Destroy(triangle);
            if (trapezoid != null) Object.Destroy(trapezoid);
            trapezoid = null;
            gradient = null;
            slant = null;
            triangle = null;
            text = null;
        }

        public static Color Alpha(Color c, float a)
        {
            c.a *= a;
            return c;
        }

        public static void Rect(Rect r, Color c, float radius = 0f)
        {
            if (!Repaint || c.a <= 0.001f) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f));
        }

        public static void Border(Rect r, Color c, float width, float radius = 0f)
        {
            if (!Repaint || c.a <= 0.001f) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, width, Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f));
        }

        // Vertical gradient: full color at the top fading to ~55% at the bottom (texture v=0 is the bottom)
        public static void Gradient(Rect r, Color c, float radius = 0f)
        {
            if (!Repaint || c.a <= 0.001f) return;
            GUI.DrawTexture(r, gradient, ScaleMode.StretchToFill, true, 0f, c, 0f, Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f));
        }

        // Parallelogram leaning right, like the color swatches in broadcast player lists
        public static void Slant(Rect r, Color c)
        {
            if (!Repaint || c.a <= 0.001f || slant == null) return;
            GUI.DrawTexture(r, slant, ScaleMode.StretchToFill, true, 0f, c, 0f, 0f);
        }

        // Triangle pointing up, rotated by the caller via GUIUtility.RotateAroundPivot when needed
        public static void Trapezoid(Rect r, Color c)
        {
            if (!Repaint || c.a <= 0.001f || trapezoid == null) return;
            GUI.DrawTexture(r, trapezoid, ScaleMode.StretchToFill, true, 0f, c, 0f, 0f);
        }

        public static void Triangle(Rect r, Color c)
        {
            if (!Repaint || c.a <= 0.001f || triangle == null) return;
            GUI.DrawTexture(r, triangle, ScaleMode.StretchToFill, true, 0f, c, 0f, 0f);
        }

        public static void Image(Rect r, Texture tex, Color tint, float radius = 0f)
        {
            if (!Repaint || tex == null) return;
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, false, 0f, tint, 0f, Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f));
        }

        // Text with a solid outline, the classic esports scorebug look
        public static void TextOutline(Rect r, string s, int size, Color c, Color outline, float width, TextAnchor anchor, FontStyle style = FontStyle.Bold)
        {
            if (!Repaint || string.IsNullOrEmpty(s) || c.a <= 0.001f) return;
            DrawString(Active, r, s, size, c, anchor, style, width > 0f ? outline : (Color?)null, width, false);
        }

        // Draws with a specific font, e.g. each font's own name in the font list
        public static void TextIn(FontFace face, Rect r, string s, int size, Color c, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            if (!Repaint || string.IsNullOrEmpty(s) || c.a <= 0.001f) return;
            DrawString(face, r, s, size, c, anchor, style, null, 0f, false);
        }

        static AtlasFont AtlasFor(FontFace face, bool bold, string s)
        {
            if (face == null) return null;
            AtlasFont a = bold ? (face.Bold ?? face.Regular) : face.Regular;
            return a != null && a.HasAll(s) ? a : null;
        }

        static void DrawString(FontFace face, Rect r, string s, int size, Color c, TextAnchor anchor, FontStyle style, Color? outline, float outlineWidth, bool shadow)
        {
            bool bold = style == FontStyle.Bold || style == FontStyle.BoldAndItalic;
            bool italic = style == FontStyle.Italic || style == FontStyle.BoldAndItalic;

            AtlasFont atlas = AtlasFor(face, bold, s);
            if (atlas != null)
            {
                if (shadow)
                {
                    float o = Mathf.Max(1f, size / 14f);
                    atlas.Draw(new Rect(r.x + o, r.y + o, r.width, r.height), s, size, new Color(0f, 0f, 0f, c.a * 0.55f), anchor, italic, false);
                }
                if (outline.HasValue && outline.Value.a > 0.001f) atlas.Draw(r, s, size, outline.Value, anchor, italic, true);
                atlas.Draw(r, s, size, c, anchor, italic, false);
                return;
            }

            // System font path (also the fallback for characters an atlas font doesn't have)
            Setup(face, size, anchor, style);
            content.text = s;
            if (shadow)
            {
                float o = Mathf.Max(1f, size / 14f);
                text.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.55f);
                text.Draw(new Rect(r.x + o, r.y + o, r.width, r.height), content, false, false, false, false);
            }
            if (outline.HasValue && outline.Value.a > 0.001f && outlineWidth > 0f)
            {
                text.normal.textColor = outline.Value;
                for (int i = 0; i < 8; i++)
                {
                    float ang = i * Mathf.PI * 0.25f;
                    text.Draw(new Rect(r.x + Mathf.Cos(ang) * outlineWidth, r.y + Mathf.Sin(ang) * outlineWidth, r.width, r.height), content, false, false, false, false);
                }
            }
            text.normal.textColor = c;
            text.Draw(r, content, false, false, false, false);
        }

        public static void Circle(Vector2 center, float radius, Color c)
        {
            Rect(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), c, radius);
        }

        public static void Text(Rect r, string s, int size, Color c, TextAnchor anchor, bool bold = false, bool shadow = false)
        {
            if (!Repaint || string.IsNullOrEmpty(s) || c.a <= 0.001f) return;
            DrawString(Active, r, s, size, c, anchor, bold ? FontStyle.Bold : FontStyle.Normal, null, 0f, shadow);
        }

        public static float Width(string s, int size, bool bold = false) => WidthIn(Active, s, size, bold);

        public static float WidthIn(FontFace face, string s, int size, bool bold = false)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            string key = Key(face, s, size, bold, 0);
            if (widthCache.TryGetValue(key, out float w)) return w;
            AtlasFont atlas = AtlasFor(face, bold, s);
            if (atlas != null)
            {
                w = atlas.Measure(s, size);
            }
            else
            {
                Setup(face, size, TextAnchor.MiddleLeft, bold ? FontStyle.Bold : FontStyle.Normal);
                content.text = s;
                w = text.CalcSize(content).x;
            }
            Remember(widthCache, key, w);
            return w;
        }

        static string Key(FontFace face, string s, int size, bool bold, int maxWidth) =>
            string.Concat(face != null ? face.Id : "ui", "|", s, "|", size.ToString(), bold ? "b" : "n", maxWidth.ToString());

        static void Remember<T>(Dictionary<string, T> cache, string key, T value)
        {
            // Names, timers and scores change over a session; a hard cap keeps the cache from growing forever
            if (cache.Count > 2048) cache.Clear();
            cache[key] = value;
        }

        // Shortens with an ellipsis so long names never overflow their row
        public static string Fit(string s, int size, float maxWidth, bool bold = false)
        {
            if (string.IsNullOrEmpty(s) || Width(s, size, bold) <= maxWidth) return s;
            string key = Key(Active, s, size, bold, Mathf.RoundToInt(maxWidth));
            if (fitCache.TryGetValue(key, out string fitted)) return fitted;
            fitted = Truncate(s, size, maxWidth, bold);
            Remember(fitCache, key, fitted);
            return fitted;
        }

        static string Truncate(string s, int size, float maxWidth, bool bold)
        {
            int lo = 0, hi = s.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (Width(s.Substring(0, mid) + "…", size, bold) <= maxWidth) lo = mid;
                else hi = mid - 1;
            }
            return lo <= 0 ? "…" : s.Substring(0, lo) + "…";
        }

        static void Setup(FontFace face, int size, TextAnchor anchor, FontStyle style)
        {
            if (text == null) Init();
            text.font = face != null && face.OsFont != null ? face.OsFont : uiFont;
            text.fontSize = Mathf.Max(1, size);
            text.alignment = anchor;
            text.fontStyle = style;
        }

        // Multi-line text (release notes, tooltips) in the UI font
        static GUIStyle wrapStyle;

        static GUIStyle Wrap(int size, Color c)
        {
            if (wrapStyle == null)
            {
                if (text == null) Init();
                wrapStyle = new GUIStyle { wordWrap = true, richText = false, clipping = TextClipping.Clip, alignment = TextAnchor.UpperLeft };
            }
            wrapStyle.font = uiFont;
            wrapStyle.fontSize = size;
            wrapStyle.normal.textColor = c;
            return wrapStyle;
        }

        public static float WrappedHeight(string s, int size, float width)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            content.text = s;
            return Wrap(size, Color.white).CalcHeight(content, width);
        }

        public static void TextWrapped(Rect r, string s, int size, Color c)
        {
            if (!Repaint || string.IsNullOrEmpty(s)) return;
            content.text = s;
            Wrap(size, c).Draw(r, content, false, false, false, false);
        }

        // A small rounded label like "TAGGED" or "LIVE"; returns its width so callers can lay out after it
        public static float Pill(float x, float centerY, string label, int size, Color bg, Color fg, bool alignRight = false)
        {
            float w = Width(label, size, true) + size * 1.1f;
            float h = size * 1.55f;
            Rect r = new Rect(alignRight ? x - w : x, centerY - h * 0.5f, w, h);
            Rect(r, bg, h * 0.5f);
            Text(r, label, size, fg, TextAnchor.MiddleCenter, true);
            return w;
        }

        public static Color Lerp(Color a, Color b, float t) => Color.Lerp(a, b, t);

        public static Color Brighten(Color c, float amount)
        {
            return new Color(Mathf.Min(1f, c.r + amount), Mathf.Min(1f, c.g + amount), Mathf.Min(1f, c.b + amount), c.a);
        }

        public static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        // Black or white, whichever reads better on the given background
        public static Color OnColor(Color bg) => Luminance(bg) > 0.6f ? new Color(0.05f, 0.06f, 0.08f, 1f) : Color.white;
    }
}
