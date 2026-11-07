using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdexansCastingMod.UI
{
    // Custom-drawn IMGUI controls. Each one paints itself on Repaint and handles its own input,
    // so nothing here depends on (or changes) the global GUI.skin other mods might use.
    internal static class Controls
    {
        static readonly int SliderHint = "AdexanCastSlider".GetHashCode();
        static GUIStyle field;

        public static bool Hover(Rect r) => r.Contains(Event.current.mousePosition);

        // Tooltip for whatever the mouse is over; the settings menu draws it last, on top of everything
        public static string HoverTip;
        static Vector2 tipMouse;

        public static void Tip(Rect r, string tip)
        {
            if (Event.current.type == EventType.Repaint && Hover(r))
            {
                HoverTip = tip;
                tipMouse = Event.current.mousePosition;
            }
        }

        public static void DrawTip()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (string.IsNullOrEmpty(HoverTip)) return;
            float w = Mathf.Min(320f, Painter.Width(HoverTip, 12) + 20f);
            float h = Painter.WrappedHeight(HoverTip, 12, w - 20f) + 14f;
            Rect r = new Rect(tipMouse.x + 14f, tipMouse.y + 18f, w, h);
            Painter.Rect(new Rect(r.x + 2f, r.y + 3f, r.width, r.height), new Color(0f, 0f, 0f, 0.35f), 7f);
            Painter.Rect(r, new Color(0.02f, 0.025f, 0.035f, 0.97f), 7f);
            Painter.Border(r, Theme.Line, 1f, 7f);
            Painter.TextWrapped(new Rect(r.x + 10f, r.y + 7f, r.width - 20f, r.height - 14f), HoverTip, 12, Theme.Text);
            HoverTip = null;
        }

        public static bool Button(Rect r, string label, int size = 13, bool active = false, bool enabled = true, Color? accent = null, float alpha = 1f, bool danger = false)
        {
            Color acc = accent ?? Theme.Accent;
            bool hover = enabled && Hover(r);
            bool pressed = hover && GUIUtility.hotControl == 0 && Event.current.type == EventType.Repaint && Input_MouseHeld;

            Color bg = active ? acc : hover ? Theme.SurfaceHover : Theme.Surface;
            if (danger && hover) bg = Painter.Lerp(Theme.Surface, Theme.Danger, 0.55f);
            if (pressed) bg = Painter.Brighten(bg, 0.06f);
            if (!enabled) bg = Painter.Alpha(bg, 0.5f);

            float radius = Mathf.Min(6f, r.height * 0.3f);
            Painter.Rect(r, Painter.Alpha(bg, alpha), radius);
            if (!active) Painter.Border(r, Painter.Alpha(Theme.Line, alpha), 1f, radius);

            Color fg = active ? Painter.OnColor(acc) : enabled ? Theme.Text : Theme.TextFaint;
            Painter.Text(r, Painter.Fit(label, size, r.width - 8f, true), size, Painter.Alpha(fg, alpha), TextAnchor.MiddleCenter, true);

            return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        // Tracks the left button between MouseDown/MouseUp for the pressed look
        static bool Input_MouseHeld;

        public static void TrackMouse()
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0) Input_MouseHeld = true;
            else if (e.type == EventType.MouseUp && e.button == 0) Input_MouseHeld = false;
        }

        // iOS-style switch; returns the new value
        public static bool Switch(Rect r, bool value, bool enabled = true)
        {
            float h = r.height;
            float w = h * 1.8f;
            Rect track = new Rect(r.xMax - w, r.y, w, h);
            bool hover = enabled && Hover(track);
            Painter.Rect(track, value ? Theme.Accent : (hover ? Theme.SurfaceHover : Theme.Surface), h * 0.5f);
            Painter.Border(track, Theme.Line, 1f, h * 0.5f);
            float k = h - 6f;
            float kx = value ? track.xMax - k - 3f : track.x + 3f;
            Painter.Rect(new Rect(kx, track.y + 3f, k, k), Color.white, k * 0.5f);

            if (enabled && GUI.Button(track, GUIContent.none, GUIStyle.none)) return !value;
            return value;
        }

        public static float Slider(Rect r, float value, float min, float max, bool enabled = true)
        {
            int id = GUIUtility.GetControlID(SliderHint, FocusType.Passive, r);
            Event e = Event.current;
            Rect hit = new Rect(r.x - 6f, r.y - 6f, r.width + 12f, r.height + 12f);

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (enabled && GUI.enabled && e.button == 0 && hit.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        value = FromMouse(r, e.mousePosition.x, min, max);
                        GUI.changed = true;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        value = FromMouse(r, e.mousePosition.x, min, max);
                        GUI.changed = true;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
                case EventType.Repaint:
                    float t = Mathf.Approximately(max, min) ? 0f : Mathf.InverseLerp(min, max, value);
                    float th = 4f;
                    Rect track = new Rect(r.x, r.center.y - th * 0.5f, r.width, th);
                    Painter.Rect(track, Theme.Surface, th * 0.5f);
                    Painter.Rect(new Rect(track.x, track.y, track.width * t, th), enabled ? Theme.Accent : Theme.TextFaint, th * 0.5f);
                    bool active = GUIUtility.hotControl == id || (enabled && hit.Contains(e.mousePosition));
                    float k = active ? 14f : 12f;
                    Painter.Circle(new Vector2(track.x + track.width * t, track.center.y), k * 0.5f, Color.white);
                    break;
            }
            return value;
        }

        static float FromMouse(Rect r, float mouseX, float min, float max)
        {
            return Mathf.Lerp(min, max, Mathf.Clamp01((mouseX - r.x) / Mathf.Max(1f, r.width)));
        }

        public static int Segmented(Rect r, int selected, string[] options, int size = 12)
        {
            float w = r.width / options.Length;
            float radius = Mathf.Min(6f, r.height * 0.3f);
            Painter.Rect(r, Theme.Surface, radius);
            Painter.Border(r, Theme.Line, 1f, radius);
            for (int i = 0; i < options.Length; i++)
            {
                Rect cell = new Rect(r.x + w * i, r.y, w, r.height);
                bool sel = i == selected;
                bool hover = Hover(cell);
                if (sel) Painter.Rect(new Rect(cell.x + 2f, cell.y + 2f, cell.width - 4f, cell.height - 4f), Theme.Accent, radius - 1f);
                else if (hover) Painter.Rect(new Rect(cell.x + 2f, cell.y + 2f, cell.width - 4f, cell.height - 4f), Theme.SurfaceHover, radius - 1f);
                Color fg = sel ? Painter.OnColor(Theme.Accent) : hover ? Theme.Text : Theme.TextDim;
                Painter.Text(cell, Painter.Fit(options[i], size, cell.width - 6f, true), size, fg, TextAnchor.MiddleCenter, true);
                if (GUI.Button(cell, GUIContent.none, GUIStyle.none)) selected = i;
            }
            return selected;
        }

        // "<  value  >" picker for long option lists
        public static int Cycler(Rect r, int selected, string[] options, int size = 12)
        {
            float bw = r.height;
            Rect left = new Rect(r.x, r.y, bw, r.height);
            Rect right = new Rect(r.xMax - bw, r.y, bw, r.height);
            Rect mid = new Rect(r.x + bw + 2f, r.y, r.width - bw * 2f - 4f, r.height);
            float radius = Mathf.Min(6f, r.height * 0.3f);
            Painter.Rect(mid, Theme.Surface, radius);
            Painter.Border(mid, Theme.Line, 1f, radius);
            string label = selected >= 0 && selected < options.Length ? options[selected] : "?";
            Painter.Text(mid, Painter.Fit(label, size, mid.width - 8f, true), size, Theme.Text, TextAnchor.MiddleCenter, true);
            if (Button(left, "‹", size + 4)) selected = (selected - 1 + options.Length) % options.Length;
            if (Button(right, "›", size + 4)) selected = (selected + 1) % options.Length;
            if (GUI.Button(mid, GUIContent.none, GUIStyle.none)) selected = (selected + 1) % options.Length;
            return selected;
        }

        public static string TextField(Rect r, string value, int maxLength = 48, int size = 13)
        {
            if (field == null)
            {
                field = new GUIStyle
                {
                    padding = new RectOffset(8, 8, 0, 0),
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                };
            }
            field.font = Painter.Font;
            field.fontSize = size;
            field.normal.textColor = Theme.Text;
            field.focused.textColor = Theme.Text;
            field.hover.textColor = Theme.Text;

            float radius = Mathf.Min(6f, r.height * 0.3f);
            bool hover = Hover(r);
            Painter.Rect(r, new Color(0.03f, 0.035f, 0.045f, 1f), radius);
            Painter.Border(r, hover ? Painter.Alpha(Theme.Accent, 0.6f) : Theme.Line, 1f, radius);
            return GUI.TextField(r, value ?? "", maxLength, field);
        }

        public static int Stepper(Rect r, int value, int min, int max, int size = 13)
        {
            float bw = r.height;
            Rect minus = new Rect(r.x, r.y, bw, r.height);
            Rect plus = new Rect(r.xMax - bw, r.y, bw, r.height);
            Rect mid = new Rect(r.x + bw + 2f, r.y, r.width - bw * 2f - 4f, r.height);
            Painter.Rect(mid, Theme.Surface, 5f);
            Painter.Text(mid, value.ToString(), size + 1, Theme.Text, TextAnchor.MiddleCenter, true);
            if (Button(minus, "−", size + 2) && value > min) value--;
            if (Button(plus, "+", size + 2) && value < max) value++;
            return value;
        }

        public static void Swatch(Rect r, Color c)
        {
            float radius = Mathf.Min(5f, r.height * 0.3f);
            // Checkerboard hint so transparent colors are recognizable
            if (c.a < 0.99f)
            {
                Painter.Rect(r, new Color(0.8f, 0.8f, 0.8f, 1f), radius);
                Painter.Rect(new Rect(r.x, r.y, r.width * 0.5f, r.height * 0.5f), new Color(0.55f, 0.55f, 0.55f, 1f), 0f);
                Painter.Rect(new Rect(r.center.x, r.center.y, r.width * 0.5f, r.height * 0.5f), new Color(0.55f, 0.55f, 0.55f, 1f), 0f);
            }
            Painter.Rect(r, c, radius);
            Painter.Border(r, new Color(1f, 1f, 1f, 0.25f), 1f, radius);
        }
    }
}
