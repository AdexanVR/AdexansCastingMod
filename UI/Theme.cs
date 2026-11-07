using UnityEngine;

namespace AdexansCastingMod.UI
{
    // Fixed palette for the caster-facing menus (the broadcast widgets use per-widget colors instead)
    internal static class Theme
    {
        public static Color Accent = new Color(0.27f, 0.6f, 1f, 1f);

        public static readonly Color Window = new Color(0.055f, 0.062f, 0.078f, 0.98f);
        public static readonly Color Sidebar = new Color(0.04f, 0.045f, 0.058f, 1f);
        public static readonly Color Card = new Color(0.085f, 0.095f, 0.118f, 1f);
        public static readonly Color RowHover = new Color(1f, 1f, 1f, 0.025f);
        public static readonly Color Surface = new Color(0.13f, 0.142f, 0.172f, 1f);
        public static readonly Color SurfaceHover = new Color(0.18f, 0.192f, 0.232f, 1f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.07f);
        public static readonly Color Text = new Color(0.93f, 0.95f, 0.98f, 1f);
        public static readonly Color TextDim = new Color(0.6f, 0.64f, 0.71f, 1f);
        public static readonly Color TextFaint = new Color(0.42f, 0.45f, 0.52f, 1f);
        public static readonly Color Danger = new Color(0.94f, 0.3f, 0.3f, 1f);
        public static readonly Color Warning = new Color(1f, 0.72f, 0.2f, 1f);
        public static readonly Color Success = new Color(0.25f, 0.85f, 0.5f, 1f);
        public static readonly Color Tagged = new Color(0.95f, 0.27f, 0.27f, 1f);

        public static readonly Color[] Swatches =
        {
            new Color(1f, 1f, 1f, 1f),
            new Color(0.06f, 0.07f, 0.09f, 1f),
            new Color(0.27f, 0.6f, 1f, 1f),
            new Color(1f, 0.36f, 0.33f, 1f),
            new Color(0.25f, 0.85f, 0.5f, 1f),
            new Color(1f, 0.72f, 0.2f, 1f),
            new Color(0.66f, 0.45f, 1f, 1f),
            new Color(1f, 0.45f, 0.75f, 1f),
            new Color(0.2f, 0.85f, 0.9f, 1f),
            new Color(0.5f, 0.53f, 0.6f, 1f),
        };

        // Ease-out cubic, used by slide/fade animations
        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }
    }
}
