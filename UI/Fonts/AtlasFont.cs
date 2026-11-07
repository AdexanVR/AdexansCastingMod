using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace AdexansCastingMod.UI.Fonts
{
    // Draws text with IMGUI from a TextMeshPro font atlas. The TMP atlas is a signed distance field;
    // it is converted once into two white RGBA textures (the glyph fill and a slightly fatter outline),
    // so outlined text costs two quads per letter instead of nine text draws.
    internal sealed class AtlasFont : IDisposable
    {
        struct GlyphInfo
        {
            public float BearingX, BearingY, Width, Height, Advance;
            public Rect Uv, OutlineUv;
            public int Atlas;
        }

        // Outline thickness, in atlas pixels at the sampling size (needs atlas padding >= this)
        const float OutlinePx = 4f;

        readonly Dictionary<uint, GlyphInfo> glyphs = new Dictionary<uint, GlyphInfo>();
        Texture2D[] fill;
        Texture2D[] outline;
        float pointSize;
        float ascent;
        float descent;

        public string Name { get; private set; }
        public bool FauxBold { get; set; }
        public int GlyphCount => glyphs.Count;

        AtlasFont() { }

        // Builds from a font asset. Only reads it: the game's own assets are never modified.
        public static AtlasFont FromAsset(TMP_FontAsset asset, string name, out string error)
        {
            error = null;
            try
            {
                var font = new AtlasFont { Name = name };
                if (!font.Build(asset, out error))
                {
                    font.Dispose();
                    return null;
                }
                return font;
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }

        bool Build(TMP_FontAsset asset, out string error)
        {
            error = null;
            FaceInfo face = asset.faceInfo;
            pointSize = face.pointSize;
            ascent = face.ascentLine;
            descent = face.descentLine;
            if (pointSize <= 0f || ascent <= descent)
            {
                error = "font has no usable metrics";
                return false;
            }

            Texture2D[] pages = asset.atlasTextures;
            if (pages == null || pages.Length == 0 || pages[0] == null)
            {
                error = "font has no atlas";
                return false;
            }

            // Bitmap atlases (render mode has bit 16) store coverage; everything else is a distance field
            bool sdf = ((int)asset.atlasRenderMode & 16) != 16;
            float gradientScale = asset.atlasPadding + 1f;

            fill = new Texture2D[pages.Length];
            outline = new Texture2D[pages.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] == null || pages[i].width <= 1) continue;
                byte[] alpha = ReadAlpha(pages[i]);
                if (alpha == null)
                {
                    error = "could not read the font atlas";
                    return false;
                }
                BakePage(alpha, pages[i].width, pages[i].height, sdf, gradientScale, out fill[i], out outline[i]);
            }

            Dictionary<uint, TMP_Character> table = asset.characterLookupTable;
            if (table == null || table.Count == 0)
            {
                error = "font has no characters";
                return false;
            }

            foreach (KeyValuePair<uint, TMP_Character> kv in table)
            {
                Glyph g = kv.Value?.glyph;
                if (g == null) continue;
                int atlas = g.atlasIndex;
                if (atlas < 0 || atlas >= fill.Length || fill[atlas] == null) continue;

                GlyphRect gr = g.glyphRect;
                float w = fill[atlas].width, h = fill[atlas].height;
                glyphs[kv.Key] = new GlyphInfo
                {
                    BearingX = g.metrics.horizontalBearingX,
                    BearingY = g.metrics.horizontalBearingY,
                    Width = g.metrics.width,
                    Height = g.metrics.height,
                    Advance = g.metrics.horizontalAdvance,
                    Uv = new Rect(gr.x / w, gr.y / h, gr.width / w, gr.height / h),
                    OutlineUv = new Rect((gr.x - OutlinePx) / w, (gr.y - OutlinePx) / h, (gr.width + OutlinePx * 2f) / w, (gr.height + OutlinePx * 2f) / h),
                    Atlas = atlas,
                };
            }

            if (glyphs.Count == 0)
            {
                error = "no glyphs were rendered";
                return false;
            }
            return true;
        }

        // Atlas pixels as bytes (bottom row first). Unreadable textures (the game's) are copied via the GPU.
        static byte[] ReadAlpha(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            Color32[] pixels;
            if (tex.isReadable)
            {
                pixels = tex.GetPixels32();
            }
            else
            {
                RenderTexture prev = RenderTexture.active;
                RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                try
                {
                    Graphics.Blit(tex, rt);
                    RenderTexture.active = rt;
                    var copy = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
                    copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    copy.Apply();
                    pixels = copy.GetPixels32();
                    UnityEngine.Object.Destroy(copy);
                }
                finally
                {
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                }
            }

            if (pixels == null || pixels.Length != w * h) return null;
            var alpha = new byte[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) alpha[i] = pixels[i].a;
            return alpha;
        }

        static void BakePage(byte[] alpha, int w, int h, bool sdf, float gradientScale, out Texture2D fillTex, out Texture2D outlineTex)
        {
            var fillPx = new Color32[alpha.Length];
            var outPx = new Color32[alpha.Length];

            if (sdf)
            {
                // Distance field: 0.5 is the edge, the gradient spans gradientScale pixels each way
                for (int i = 0; i < alpha.Length; i++)
                {
                    float d = (alpha[i] / 255f - 0.5f) * 2f * gradientScale;
                    fillPx[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(d + 0.5f) * 255f));
                    outPx[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(d + OutlinePx + 0.5f) * 255f));
                }
            }
            else
            {
                // Coverage bitmap: the outline is a dilation (separable max filter)
                var tmp = new byte[alpha.Length];
                int r = Mathf.RoundToInt(OutlinePx);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        byte m = 0;
                        for (int k = Mathf.Max(0, x - r); k <= Mathf.Min(w - 1, x + r); k++)
                            if (alpha[y * w + k] > m) m = alpha[y * w + k];
                        tmp[y * w + x] = m;
                    }
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        byte m = 0;
                        for (int k = Mathf.Max(0, y - r); k <= Mathf.Min(h - 1, y + r); k++)
                            if (tmp[k * w + x] > m) m = tmp[k * w + x];
                        fillPx[y * w + x] = new Color32(255, 255, 255, alpha[y * w + x]);
                        outPx[y * w + x] = new Color32(255, 255, 255, m);
                    }
            }

            fillTex = MakeTexture(w, h, fillPx);
            outlineTex = MakeTexture(w, h, outPx);
        }

        static Texture2D MakeTexture(int w, int h, Color32[] px)
        {
            // Mipmaps + trilinear keep small text smooth when the atlas is drawn far below its size
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                anisoLevel = 0,
            };
            tex.SetPixels32(px);
            tex.Apply(true, true); // upload and free the CPU copy
            return tex;
        }

        public bool HasAll(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ' ' || c == ' ') continue;
                if (!glyphs.ContainsKey(c)) return false;
            }
            return true;
        }

        float SpaceAdvance(float scale) => glyphs.TryGetValue(' ', out GlyphInfo sp) ? sp.Advance * scale : pointSize * 0.28f * scale;

        public float Measure(string s, float size)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            float scale = size / pointSize;
            float w = 0f;
            for (int i = 0; i < s.Length; i++)
                w += glyphs.TryGetValue(s[i], out GlyphInfo g) ? g.Advance * scale : SpaceAdvance(scale);
            if (FauxBold) w += size * 0.04f;
            return w;
        }

        // Line box height used for vertical alignment
        public float LineHeight(float size) => (ascent - descent) * size / pointSize;

        public void Draw(Rect r, string s, float size, Color color, TextAnchor anchor, bool italic, bool useOutline)
        {
            if (string.IsNullOrEmpty(s) || color.a <= 0.001f) return;
            float scale = size / pointSize;
            float width = Measure(s, size);
            float lineH = (ascent - descent) * scale;

            float x;
            switch (anchor)
            {
                case TextAnchor.UpperCenter:
                case TextAnchor.MiddleCenter:
                case TextAnchor.LowerCenter:
                    x = r.x + (r.width - width) * 0.5f;
                    break;
                case TextAnchor.UpperRight:
                case TextAnchor.MiddleRight:
                case TextAnchor.LowerRight:
                    x = r.xMax - width;
                    break;
                default:
                    x = r.x;
                    break;
            }

            float top;
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                case TextAnchor.UpperCenter:
                case TextAnchor.UpperRight:
                    top = r.y;
                    break;
                case TextAnchor.LowerLeft:
                case TextAnchor.LowerCenter:
                case TextAnchor.LowerRight:
                    top = r.yMax - lineH;
                    break;
                default:
                    top = r.y + (r.height - lineH) * 0.5f;
                    break;
            }
            float baseline = Mathf.Round(top + ascent * scale);
            x = Mathf.Round(x);

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            if (italic)
            {
                // Shear around the baseline so letters lean right
                Matrix4x4 shear = Matrix4x4.identity;
                shear.m01 = -0.2f;
                GUI.matrix = oldMatrix * Matrix4x4.Translate(new Vector3(0f, baseline, 0f)) * shear * Matrix4x4.Translate(new Vector3(0f, -baseline, 0f));
            }
            GUI.color = color;

            float pen = x;
            float pad = OutlinePx * scale;
            float bold = FauxBold ? Mathf.Max(0.6f, size * 0.035f) : 0f;
            for (int i = 0; i < s.Length; i++)
            {
                if (!glyphs.TryGetValue(s[i], out GlyphInfo g))
                {
                    pen += SpaceAdvance(scale);
                    continue;
                }
                if (g.Width > 0f && g.Height > 0f)
                {
                    float gx = pen + g.BearingX * scale;
                    float gy = baseline - g.BearingY * scale;
                    if (useOutline)
                    {
                        Rect q = new Rect(gx - pad, gy - pad, g.Width * scale + pad * 2f + bold, g.Height * scale + pad * 2f);
                        GUI.DrawTextureWithTexCoords(q, outline[g.Atlas], g.OutlineUv, true);
                    }
                    else
                    {
                        Rect q = new Rect(gx, gy, g.Width * scale, g.Height * scale);
                        GUI.DrawTextureWithTexCoords(q, fill[g.Atlas], g.Uv, true);
                        if (bold > 0f)
                            GUI.DrawTextureWithTexCoords(new Rect(q.x + bold, q.y, q.width, q.height), fill[g.Atlas], g.Uv, true);
                    }
                }
                pen += g.Advance * scale;
            }

            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        public void Dispose()
        {
            DestroyAll(fill);
            DestroyAll(outline);
            fill = outline = null;
            glyphs.Clear();
        }

        static void DestroyAll(Texture2D[] textures)
        {
            if (textures == null) return;
            foreach (Texture2D t in textures)
                if (t != null) UnityEngine.Object.Destroy(t);
        }
    }
}
