using AdexansCastingMod.Config;
using UnityEngine;

namespace AdexansCastingMod.UI
{
    // Everything a widget needs to draw itself inside its own (clipped, local-space) rect
    internal struct WidgetDraw
    {
        public float W;
        public float H;
        public WidgetLayout L;
        public float Alpha;
        public float Scale;
        public bool Interactive;
        public bool Shadow;

        public int Font(float mul = 1f) => Mathf.Max(6, Mathf.RoundToInt(L.fontSize * mul * Scale));
        public float Pad => Mathf.Round(L.fontSize * Scale * 0.65f);
        public Color Text(float a = 1f) => Painter.Alpha(L.textColor, Alpha * a);
        public Color Accent(float a = 1f) => Painter.Alpha(L.accentColor, Alpha * a);
        public Color Tint(Color c, float a = 1f) => Painter.Alpha(c, Alpha * a);

        public TextAnchor Anchor
        {
            get
            {
                switch (L.alignment)
                {
                    case TextAlign.Center: return TextAnchor.MiddleCenter;
                    case TextAlign.Right: return TextAnchor.MiddleRight;
                    default: return TextAnchor.MiddleLeft;
                }
            }
        }

        // Start X for content of the given width, honoring the widget's alignment
        public float AlignX(float contentWidth, float pad)
        {
            switch (L.alignment)
            {
                case TextAlign.Center: return (W - contentWidth) * 0.5f;
                case TextAlign.Right: return W - pad - contentWidth;
                default: return pad;
            }
        }

        public void Label(Rect r, string s, float sizeMul, Color c, bool bold = false, TextAnchor? anchor = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            int size = Font(sizeMul);
            Painter.Text(r, Painter.Fit(s, size, r.width, bold), size, c, anchor ?? Anchor, bold, Shadow);
        }
    }

    internal abstract class HudWidget
    {
        protected readonly CastingRuntime rt;

        public readonly string Id;
        public readonly string Title;
        public float Fade;

        protected HudWidget(CastingRuntime rt, string id, string title)
        {
            this.rt = rt;
            Id = id;
            Title = title;
        }

        // Caster-only widgets disappear in "clean feed" mode (caster controls hidden)
        public virtual bool CasterOnly => false;

        // Widgets without clickable parts only need to run on Repaint
        public virtual bool HandlesInput => false;

        public abstract void Draw(WidgetDraw d);
    }
}
