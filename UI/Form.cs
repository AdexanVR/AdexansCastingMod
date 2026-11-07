using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdexansCastingMod.UI
{
    // Vertical form layout for the settings menu and the layout inspector.
    //  - Sections are drawn as cards (their height is measured on one frame and used on the next).
    //  - Rows can carry a one-line description under the label.
    //  - Passing a default value shows a dot + "Reset" button whenever the value differs from it.
    //  - With a search Filter set, only rows whose label/description match are laid out.
    internal sealed class Form
    {
        static readonly Dictionary<string, bool> expanded = new Dictionary<string, bool>();
        static readonly Dictionary<string, string> hexBuffers = new Dictionary<string, string>();
        static readonly Dictionary<string, float> cardHeights = new Dictionary<string, float>();

        const float CardPad = 16f;
        const float CardGap = 12f;

        public float X;
        public float Y;
        public float Width;
        public float RowHeight = 38f;
        public float LabelWidth;
        public int FontSize = 13;
        public bool Changed;
        public bool UseCards = true;

        // Lower-case search text; empty = show everything
        public string Filter = "";
        // Prefix for card titles while searching, e.g. "Camera"
        public string Page = "";

        readonly float baseX;
        readonly float baseWidth;
        readonly float labelRatio;

        string pendingSection;
        string cardKey;
        float cardTop;
        bool cardOpen;
        bool sectionMatches;

        public Form(float x, float y, float width, float labelRatio = 0.5f)
        {
            baseX = x;
            baseWidth = width;
            this.labelRatio = labelRatio;
            X = x;
            Y = y;
            Width = width;
            LabelWidth = Mathf.Round(width * labelRatio);
        }

        public bool Searching => !string.IsNullOrEmpty(Filter);

        bool Matches(string text) => !string.IsNullOrEmpty(text) && text.IndexOf(Filter, StringComparison.OrdinalIgnoreCase) >= 0;

        // ------------------------------------------------------------ Sections / cards

        public void Section(string title)
        {
            EndCard();
            pendingSection = title;
            sectionMatches = !Searching || Matches(title) || Matches(Page);
            if (!Searching) OpenCard();
        }

        // Called by every row before drawing, so a card only appears once it has visible content
        void OpenCard()
        {
            if (cardOpen || pendingSection == null) return;
            cardOpen = true;
            string title = Searching && Page.Length > 0 ? Page + "  ›  " + pendingSection : pendingSection;
            cardKey = Page + "/" + pendingSection;
            cardTop = Y;

            if (UseCards)
            {
                X = baseX + CardPad;
                Width = baseWidth - CardPad * 2f;
                LabelWidth = Mathf.Round(Width * labelRatio);
                if (cardHeights.TryGetValue(cardKey, out float h))
                {
                    Rect card = new Rect(baseX, cardTop, baseWidth, h);
                    Painter.Rect(card, Theme.Card, 10f);
                    Painter.Border(card, Theme.Line, 1f, 10f);
                }
                Y += 14f;
                Painter.Text(new Rect(X, Y, Width, 20f), title.ToUpperInvariant(), FontSize - 1, Theme.Accent, TextAnchor.MiddleLeft, true);
                Y += 26f;
            }
            else
            {
                Y += Y > 20f ? 14f : 2f;
                Painter.Text(new Rect(X, Y, Width, 20f), title.ToUpperInvariant(), FontSize - 2, Theme.Accent, TextAnchor.MiddleLeft, true);
                Y += 22f;
                Painter.Rect(new Rect(X, Y, Width, 1f), Theme.Line);
                Y += 6f;
            }
        }

        void EndCard()
        {
            if (!cardOpen) return;
            cardOpen = false;
            if (!UseCards) return;
            Y += 8f;
            if (Event.current.type == EventType.Repaint || !cardHeights.ContainsKey(cardKey))
                cardHeights[cardKey] = Y - cardTop;
            Y += CardGap;
            X = baseX;
            Width = baseWidth;
            LabelWidth = Mathf.Round(Width * labelRatio);
        }

        // Finish the last card; call once after the last row
        public void End() => EndCard();

        // For hand-drawn content: true if it should be shown (matches the search or its card does)
        public bool Block(string keywords = null)
        {
            bool show = !Searching || sectionMatches || Matches(keywords);
            if (show) OpenCard();
            return show;
        }

        bool Row(string label, string hint)
        {
            bool show = !Searching || sectionMatches || Matches(label) || Matches(hint);
            if (show) OpenCard();
            return show;
        }

        // ------------------------------------------------------------ Row chrome

        float RowH(string hint) => string.IsNullOrEmpty(hint) ? RowHeight : RowHeight + 14f;

        // Label (+ description) on the left; returns the control rect on the right
        Rect DrawLabel(string label, string hint, bool modified)
        {
            float h = RowH(hint);
            Rect row = new Rect(X - 8f, Y, Width + 16f, h);
            if (Controls.Hover(row)) Painter.Rect(row, Theme.RowHover, 6f);

            float lx = X;
            if (modified)
            {
                Painter.Circle(new Vector2(X - 1f, Y + (string.IsNullOrEmpty(hint) ? h * 0.5f : 15f)), 3f, Theme.Accent);
                lx += 8f;
            }

            if (string.IsNullOrEmpty(hint))
            {
                Painter.Text(new Rect(lx, Y, LabelWidth - (lx - X) - 64f, h), Painter.Fit(label, FontSize, LabelWidth - (lx - X) - 64f), FontSize, Theme.Text, TextAnchor.MiddleLeft);
            }
            else
            {
                Painter.Text(new Rect(lx, Y + 5f, LabelWidth - (lx - X) - 64f, 20f), Painter.Fit(label, FontSize, LabelWidth - (lx - X) - 64f), FontSize, Theme.Text, TextAnchor.MiddleLeft);
                Painter.Text(new Rect(X, Y + 24f, Width * 0.98f, 16f), Painter.Fit(hint, FontSize - 2, Width * 0.98f), FontSize - 2, Theme.TextDim, TextAnchor.MiddleLeft);
            }

            float ch = RowHeight - 12f;
            float cy = string.IsNullOrEmpty(hint) ? Y + (h - ch) * 0.5f : Y + 3f;
            return new Rect(X + LabelWidth, cy, Width - LabelWidth, ch);
        }

        // Small "Reset" button just left of the control; true when clicked
        bool ResetButton(Rect control, bool modified)
        {
            if (!modified) return false;
            Rect r = new Rect(control.x - 60f, control.y + 3f, 52f, control.height - 6f);
            return Controls.Button(r, "Reset", FontSize - 3);
        }

        void Advance(string hint) => Y += RowH(hint);

        // ------------------------------------------------------------ Controls

        public bool Toggle(string label, bool value, string hint = null, bool? def = null)
        {
            if (!Row(label, hint)) return value;
            bool modified = def.HasValue && def.Value != value;
            Rect r = DrawLabel(label, hint, modified);
            bool next = Controls.Switch(new Rect(r.xMax - 44f, r.y + 2f, 44f, r.height - 4f), value);
            if (ResetButton(r, modified)) next = def.Value;
            // Clicking the label area toggles too
            else if (GUI.Button(new Rect(X, Y, LabelWidth - 64f, RowH(hint)), GUIContent.none, GUIStyle.none)) next = !value;
            Advance(hint);
            if (next != value) Changed = true;
            return next;
        }

        public float Slider(string label, float value, float min, float max, Func<float, string> format, float step = 0f, string hint = null, float? def = null)
        {
            if (!Row(label, hint)) return value;
            bool modified = def.HasValue && Mathf.Abs(def.Value - value) > Mathf.Max(step * 0.5f, 1e-4f);
            Rect r = DrawLabel(label, hint, modified);
            const float valueW = 70f;
            float next = Controls.Slider(new Rect(r.x + 6f, r.y, r.width - valueW - 16f, r.height), value, min, max);
            if (step > 0f) next = Mathf.Round(next / step) * step;
            next = Mathf.Clamp(next, Mathf.Min(min, max), Mathf.Max(min, max));
            Rect valueRect = new Rect(r.xMax - valueW, r.y + 1f, valueW, r.height - 2f);
            Painter.Rect(valueRect, Theme.Surface, 5f);
            Painter.Text(valueRect, format(next), FontSize - 1, Theme.Text, TextAnchor.MiddleCenter, true);
            if (ResetButton(r, modified)) next = def.Value;
            Advance(hint);
            if (!Mathf.Approximately(next, value)) Changed = true;
            return next;
        }

        public int Choice(string label, int value, string[] options, string hint = null, int? def = null)
        {
            if (!Row(label, hint)) return value;
            bool modified = def.HasValue && def.Value != value;
            Rect r = DrawLabel(label, hint, modified);
            int next = options.Length <= 4 && r.width / options.Length >= 64f
                ? Controls.Segmented(r, value, options, FontSize - 1)
                : Controls.Cycler(r, value, options, FontSize - 1);
            if (ResetButton(r, modified)) next = def.Value;
            Advance(hint);
            if (next != value) Changed = true;
            return next;
        }

        public string Text(string label, string value, int maxLength = 48, string hint = null)
        {
            if (!Row(label, hint)) return value;
            Rect r = DrawLabel(label, hint, false);
            string next = Controls.TextField(r, value, maxLength, FontSize);
            Advance(hint);
            if (next != value) Changed = true;
            return next;
        }

        public int Stepper(string label, int value, int min, int max, string hint = null, int? def = null)
        {
            if (!Row(label, hint)) return value;
            bool modified = def.HasValue && def.Value != value;
            Rect r = DrawLabel(label, hint, modified);
            Rect stepper = new Rect(r.xMax - 140f, r.y, 140f, r.height);
            int next = Controls.Stepper(stepper, value, min, max, FontSize);
            if (modified && Controls.Button(new Rect(stepper.x - 60f, r.y + 3f, 52f, r.height - 6f), "Reset", FontSize - 3)) next = def.Value;
            Advance(hint);
            if (next != value) Changed = true;
            return next;
        }

        // Returns the index of the clicked button, or -1. Hidden while searching unless its card matches.
        public int Buttons(params string[] labels)
        {
            if (!Block(string.Join(" ", labels))) return -1;
            float gap = 8f;
            float w = (Width - gap * (labels.Length - 1)) / labels.Length;
            int clicked = -1;
            for (int i = 0; i < labels.Length; i++)
            {
                if (Controls.Button(new Rect(X + (w + gap) * i, Y + 4f, w, RowHeight - 8f), labels[i], FontSize - 1))
                    clicked = i;
            }
            Y += RowHeight;
            return clicked;
        }

        // Label on the left, one button on the right
        public bool Button(string label, string buttonText, bool danger = false, string hint = null)
        {
            if (!Row(label, hint)) return false;
            Rect r = DrawLabel(label, hint, false);
            bool clicked = Controls.Button(new Rect(r.xMax - 150f, r.y, 150f, r.height), buttonText, FontSize - 1, danger: danger);
            Advance(hint);
            return clicked;
        }

        public void Note(string text)
        {
            if (!Block()) return;
            Painter.Text(new Rect(X, Y, Width, 20f), Painter.Fit(text, FontSize - 2, Width), FontSize - 2, Theme.TextFaint, TextAnchor.MiddleLeft);
            Y += 22f;
        }

        public void Space(float h) => Y += h;

        public Color Color(string label, Color value, string id, bool allowAlpha = true, string hint = null, Color? def = null)
        {
            if (!Row(label, hint)) return value;
            bool modified = def.HasValue && def.Value != value;
            Rect r = DrawLabel(label, hint, modified);
            bool open = expanded.TryGetValue(id, out bool o) && o;

            Rect swatch = new Rect(r.x, r.y, 46f, r.height);
            Controls.Swatch(swatch, value);
            if (GUI.Button(swatch, GUIContent.none, GUIStyle.none))
            {
                open = !open;
                expanded[id] = open;
                hexBuffers.Remove(id);
            }

            // Hex input: keep the typed text in a buffer until it parses so half-typed values aren't overwritten
            string shown = hexBuffers.TryGetValue(id, out string buf) ? buf : ToHex(value, allowAlpha);
            Rect hexRect = new Rect(swatch.xMax + 8f, r.y, Mathf.Min(110f, r.width - 54f - 40f), r.height);
            string typed = Controls.TextField(hexRect, shown, 9, FontSize - 1);
            Color next = value;
            if (typed != shown)
            {
                hexBuffers[id] = typed;
                string h = typed.Trim();
                if (!h.StartsWith("#")) h = "#" + h;
                if ((h.Length == 7 || (allowAlpha && h.Length == 9)) && ColorUtility.TryParseHtmlString(h, out Color parsed))
                {
                    if (h.Length == 7) parsed.a = value.a;
                    next = parsed;
                }
            }

            Rect toggle = new Rect(r.xMax - 30f, r.y, 30f, r.height);
            if (Controls.Button(toggle, open ? "▴" : "▾", FontSize))
            {
                open = !open;
                expanded[id] = open;
            }
            if (modified && Controls.Button(new Rect(r.x - 60f, r.y + 3f, 52f, r.height - 6f), "Reset", FontSize - 3))
            {
                next = def.Value;
                hexBuffers.Remove(id);
            }
            Advance(hint);

            if (open)
            {
                float before = Y;
                next.r = Channel("Red", next.r);
                next.g = Channel("Green", next.g);
                next.b = Channel("Blue", next.b);
                if (allowAlpha) next.a = Channel("Opacity", next.a);

                float sw = 22f;
                float sx = X + LabelWidth;
                for (int i = 0; i < Theme.Swatches.Length; i++)
                {
                    Rect s = new Rect(sx + i * (sw + 6f), Y + 4f, sw, sw);
                    if (s.xMax > X + Width) break;
                    Controls.Swatch(s, Theme.Swatches[i]);
                    if (GUI.Button(s, GUIContent.none, GUIStyle.none))
                    {
                        Color pick = Theme.Swatches[i];
                        pick.a = next.a;
                        next = pick;
                    }
                }
                Y += sw + 10f;
                Painter.Rect(new Rect(X + LabelWidth - 10f, before, 2f, Y - before - 4f), Painter.Alpha(Theme.Accent, 0.5f), 1f);
            }

            if (next != value)
            {
                Changed = true;
                if (typed == shown) hexBuffers.Remove(id);
            }
            return next;
        }

        float Channel(string name, float v)
        {
            float h = 28f;
            Painter.Text(new Rect(X + LabelWidth - 70f, Y, 56f, h), name, FontSize - 2, Theme.TextDim, TextAnchor.MiddleRight);
            Rect r = new Rect(X + LabelWidth + 4f, Y + 4f, Width - LabelWidth - 60f, h - 8f);
            float next = Controls.Slider(r, v, 0f, 1f);
            Painter.Text(new Rect(X + Width - 48f, Y, 48f, h), Mathf.RoundToInt(next * 255f).ToString(), FontSize - 1, Theme.TextDim, TextAnchor.MiddleRight);
            Y += h;
            return next;
        }

        static string ToHex(Color c, bool alpha) => "#" + (alpha ? ColorUtility.ToHtmlStringRGBA(c) : ColorUtility.ToHtmlStringRGB(c));

        public static void ForgetBuffers() => hexBuffers.Clear();
    }
}
