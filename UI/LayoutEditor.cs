using System;
using System.Collections.Generic;
using AdexansCastingMod.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AdexansCastingMod.UI
{
    // Visual editor: drag widgets to move them, drag edges/corners to resize, edit the rest in the side panel
    internal sealed class LayoutEditor
    {
        const int EdgeLeft = 1, EdgeRight = 2, EdgeTop = 4, EdgeBottom = 8;
        const float SnapThreshold = 7f;

        static readonly string[] BackgroundNames = { "None", "Solid", "Rounded", "Gradient", "Glass" };
        static readonly string[] AlignNames = { "Left", "Center", "Right" };

        enum DragMode { None, Move, Resize, Panel }

        readonly CastingRuntime rt;
        string selected = WidgetIds.Scoreboard;

        DragMode drag;
        int edges;
        Vector2 dragStartMouse;
        Rect dragStartRect;
        float guideX = -1f, guideY = -1f;

        Rect panel = new Rect(24f, 24f, 380f, 640f);
        bool panelPlaced;
        float panelScroll;
        float panelContentHeight = 600f;
        string saveName = "";
        string pendingDelete;

        public bool Active { get; private set; }

        public LayoutEditor(CastingRuntime rt)
        {
            this.rt = rt;
        }

        public void Toggle()
        {
            Active = !Active;
            drag = DragMode.None;
            pendingDelete = null;
            if (Active)
            {
                saveName = rt.Layouts.Current.name;
                rt.Layouts.RefreshPresets();
            }
            rt.Toasts.Show(Active ? "Layout editor opened" : "Layout editor closed");
        }

        // Arrow-key nudging (Shift = 10 px)
        public void Tick()
        {
            if (!Active || drag != DragMode.None || GUIUtility.keyboardControl != 0) return;
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            int dx = 0, dy = 0;
            if (kb.leftArrowKey.wasPressedThisFrame) dx = -1;
            if (kb.rightArrowKey.wasPressedThisFrame) dx = 1;
            if (kb.upArrowKey.wasPressedThisFrame) dy = -1;
            if (kb.downArrowKey.wasPressedThisFrame) dy = 1;
            if (dx == 0 && dy == 0) return;

            float step = kb.shiftKey.isPressed ? 10f : 1f;
            WidgetLayout l = rt.Layouts.Get(selected);
            Rect r = HudRenderer.ToScreen(l);
            r.x += dx * step;
            r.y += dy * step;
            Apply(l, r);
        }

        public void Draw()
        {
            if (!Active) return;

            float s = rt.MenuScale;
            if (!panelPlaced)
            {
                panel.x = Screen.width / s - panel.width - 24f;
                panel.y = 70f;
                panelPlaced = true;
            }
            panel.height = Mathf.Min(680f, Screen.height / s - 40f);
            panel.x = Mathf.Clamp(panel.x, 0f, Mathf.Max(0f, Screen.width / s - panel.width));
            panel.y = Mathf.Clamp(panel.y, 0f, Mathf.Max(0f, Screen.height / s - 40f));
            Rect panelScreen = new Rect(panel.x * s, panel.y * s, panel.width * s, panel.height * s);

            Event e = Event.current;
            bool overPanel = panelScreen.Contains(e.mousePosition) || rt.Menu.IsMouseOver;

            Painter.Rect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.3f));
            Color guide = new Color(1f, 1f, 1f, 0.06f);
            Painter.Rect(new Rect(Screen.width * 0.5f, 0f, 1f, Screen.height), guide);
            Painter.Rect(new Rect(0f, Screen.height * 0.5f, Screen.width, 1f), guide);

            DrawWidgets();
            DrawGuides();
            HandleWidgetInput(e, overPanel);
            // The hint bar and side panel are tool UI: they use the menu font, not the broadcast font
            Painter.MenuMode = true;
            DrawHintBar();

            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f)) * old;
            DrawPanel();
            GUI.matrix = old;
            Painter.MenuMode = false;
        }

        void DrawWidgets()
        {
            List<HudWidget> widgets = rt.Hud.Widgets;
            int labelSize = Mathf.RoundToInt(11f * rt.MenuScale);
            foreach (HudWidget w in widgets)
            {
                WidgetLayout l = rt.Layouts.Get(w.Id);
                Rect r = HudRenderer.ToScreen(l);
                rt.Hud.DrawWidget(w, l, r, l.visible ? Mathf.Max(0.25f, l.opacity) : 0.25f, false);

                bool sel = w.Id == selected;
                Painter.Border(r, sel ? Theme.Accent : new Color(1f, 1f, 1f, 0.35f), sel ? 2f : 1f, 2f);

                string tag = w.Title + (l.visible ? "" : "  ·  HIDDEN");
                float tw = Painter.Width(tag, labelSize, true) + labelSize * 1.2f;
                float th = labelSize * 1.7f;
                float ty = r.y - th - 2f < 0f ? r.y + 2f : r.y - th - 2f;
                Rect tagRect = new Rect(r.x, ty, tw, th);
                Painter.Rect(tagRect, sel ? Theme.Accent : new Color(0.1f, 0.11f, 0.14f, 0.9f), 4f);
                Painter.Text(tagRect, tag, labelSize, sel ? Painter.OnColor(Theme.Accent) : Theme.TextDim, TextAnchor.MiddleCenter, true);
            }

            WidgetLayout sl = rt.Layouts.Get(selected);
            if (sl != null)
            {
                Rect r = HudRenderer.ToScreen(sl);
                float hs = 8f;
                foreach (Vector2 p in new[]
                {
                    new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax),
                    new Vector2(r.center.x, r.yMin), new Vector2(r.center.x, r.yMax), new Vector2(r.xMin, r.center.y), new Vector2(r.xMax, r.center.y),
                })
                {
                    Rect h = new Rect(p.x - hs * 0.5f, p.y - hs * 0.5f, hs, hs);
                    Painter.Rect(h, Color.white, 2f);
                    Painter.Border(h, Theme.Accent, 1.5f, 2f);
                }
            }
        }

        void DrawGuides()
        {
            if (drag != DragMode.Move && drag != DragMode.Resize) return;
            Color c = Painter.Alpha(Theme.Accent, 0.8f);
            if (guideX >= 0f) Painter.Rect(new Rect(guideX, 0f, 1f, Screen.height), c);
            if (guideY >= 0f) Painter.Rect(new Rect(0f, guideY, Screen.width, 1f), c);
        }

        void DrawHintBar()
        {
            int size = Mathf.RoundToInt(12f * rt.MenuScale);
            KeyBinding bind = KeyBindings.Find(rt.Settings.keyBindings, CastAction.ToggleLayoutEditor);
            string text = "LAYOUT EDITOR   ·   Drag to move   ·   Drag edges to resize   ·   Arrows nudge (Shift ×10)   ·   " +
                          KeyBindings.Describe(bind) + " to close";
            float w = Painter.Width(text, size, true) + size * 3f;
            float h = size * 2.4f;
            Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 14f, w, h);
            Painter.Rect(r, new Color(0.06f, 0.07f, 0.09f, 0.94f), h * 0.5f);
            Painter.Text(r, text, size, Theme.Text, TextAnchor.MiddleCenter, true);
        }

        void HandleWidgetInput(Event e, bool overPanel)
        {
            Vector2 mouse = e.mousePosition;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button != 0 || overPanel || drag != DragMode.None) return;

                    WidgetLayout sel = rt.Layouts.Get(selected);
                    if (sel != null)
                    {
                        int hit = HitEdges(HudRenderer.ToScreen(sel), mouse);
                        if (hit != 0)
                        {
                            BeginDrag(DragMode.Resize, hit, sel, mouse);
                            e.Use();
                            return;
                        }
                    }

                    List<HudWidget> widgets = rt.Hud.Widgets;
                    for (int i = widgets.Count - 1; i >= 0; i--)
                    {
                        WidgetLayout l = rt.Layouts.Get(widgets[i].Id);
                        if (!HudRenderer.ToScreen(l).Contains(mouse)) continue;
                        selected = widgets[i].Id;
                        BeginDrag(DragMode.Move, 0, l, mouse);
                        e.Use();
                        return;
                    }
                    break;

                case EventType.MouseDrag:
                    if (drag != DragMode.Move && drag != DragMode.Resize) return;
                    WidgetLayout target = rt.Layouts.Get(selected);
                    Vector2 delta = mouse - dragStartMouse;
                    Apply(target, drag == DragMode.Move ? Move(dragStartRect, delta) : Resize(dragStartRect, delta));
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (drag != DragMode.Move && drag != DragMode.Resize) return;
                    drag = DragMode.None;
                    guideX = guideY = -1f;
                    e.Use();
                    break;
            }
        }

        void BeginDrag(DragMode mode, int edgeMask, WidgetLayout l, Vector2 mouse)
        {
            drag = mode;
            edges = edgeMask;
            dragStartMouse = mouse;
            dragStartRect = HudRenderer.ToScreen(l);
            GUIUtility.keyboardControl = 0;
        }

        static int HitEdges(Rect r, Vector2 m)
        {
            const float t = 7f;
            Rect outer = new Rect(r.x - t, r.y - t, r.width + t * 2f, r.height + t * 2f);
            if (!outer.Contains(m)) return 0;
            int mask = 0;
            if (Mathf.Abs(m.x - r.xMin) <= t) mask |= EdgeLeft;
            if (Mathf.Abs(m.x - r.xMax) <= t) mask |= EdgeRight;
            if (Mathf.Abs(m.y - r.yMin) <= t) mask |= EdgeTop;
            if (Mathf.Abs(m.y - r.yMax) <= t) mask |= EdgeBottom;
            return mask;
        }

        Rect Move(Rect start, Vector2 delta)
        {
            Rect r = start;
            r.position += delta;
            guideX = guideY = -1f;

            float sx = SnapAxis(r.x, r.width, true, out float gx);
            float sy = SnapAxis(r.y, r.height, false, out float gy);
            r.x = sx;
            r.y = sy;
            guideX = gx;
            guideY = gy;

            r.x = Mathf.Clamp(r.x, 0f, Screen.width - r.width);
            r.y = Mathf.Clamp(r.y, 0f, Screen.height - r.height);
            return r;
        }

        Rect Resize(Rect start, Vector2 delta)
        {
            float minW = 40f, minH = 18f;
            float xMin = start.xMin, xMax = start.xMax, yMin = start.yMin, yMax = start.yMax;
            guideX = guideY = -1f;

            if ((edges & EdgeLeft) != 0) xMin = Mathf.Min(SnapEdge(start.xMin + delta.x, true, ref guideX), xMax - minW);
            if ((edges & EdgeRight) != 0) xMax = Mathf.Max(SnapEdge(start.xMax + delta.x, true, ref guideX), xMin + minW);
            if ((edges & EdgeTop) != 0) yMin = Mathf.Min(SnapEdge(start.yMin + delta.y, false, ref guideY), yMax - minH);
            if ((edges & EdgeBottom) != 0) yMax = Mathf.Max(SnapEdge(start.yMax + delta.y, false, ref guideY), yMin + minH);

            xMin = Mathf.Max(0f, xMin);
            yMin = Mathf.Max(0f, yMin);
            xMax = Mathf.Min(Screen.width, xMax);
            yMax = Mathf.Min(Screen.height, yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        // Snaps a moving rect's position: guide lines (screen edges/center, other widgets) win over the grid
        float SnapAxis(float pos, float size, bool horizontal, out float guide)
        {
            guide = -1f;
            float screen = horizontal ? Screen.width : Screen.height;

            if (rt.Settings.snapToGuides)
            {
                float best = SnapThreshold;
                float result = pos;
                float line = -1f;
                void Try(float candidate, float guideLine)
                {
                    float d = Mathf.Abs(pos - candidate);
                    if (d < best)
                    {
                        best = d;
                        result = candidate;
                        line = guideLine;
                    }
                }

                Try(0f, 0f);
                Try(screen - size, screen);
                Try(screen * 0.5f - size * 0.5f, screen * 0.5f);
                foreach (HudWidget w in rt.Hud.Widgets)
                {
                    if (w.Id == selected) continue;
                    Rect o = HudRenderer.ToScreen(rt.Layouts.Get(w.Id));
                    float oMin = horizontal ? o.xMin : o.yMin;
                    float oMax = horizontal ? o.xMax : o.yMax;
                    Try(oMin, oMin);
                    Try(oMax - size, oMax);
                    Try(oMax, oMax);
                    Try(oMin - size, oMin);
                }
                if (line >= 0f)
                {
                    guide = line;
                    return result;
                }
            }
            return Grid(pos);
        }

        float SnapEdge(float edge, bool horizontal, ref float guide)
        {
            if (rt.Settings.snapToGuides)
            {
                float screen = horizontal ? Screen.width : Screen.height;
                float best = SnapThreshold;
                float result = edge;
                bool found = false;
                void Try(float c)
                {
                    float d = Mathf.Abs(edge - c);
                    if (d < best)
                    {
                        best = d;
                        result = c;
                        found = true;
                    }
                }

                Try(0f);
                Try(screen);
                Try(screen * 0.5f);
                foreach (HudWidget w in rt.Hud.Widgets)
                {
                    if (w.Id == selected) continue;
                    Rect o = HudRenderer.ToScreen(rt.Layouts.Get(w.Id));
                    Try(horizontal ? o.xMin : o.yMin);
                    Try(horizontal ? o.xMax : o.yMax);
                }
                if (found)
                {
                    guide = result;
                    return result;
                }
            }
            return Grid(edge);
        }

        float Grid(float v)
        {
            float g = rt.Settings.snapGrid * Screen.height / 1080f;
            return g >= 1f ? Mathf.Round(v / g) * g : Mathf.Round(v);
        }

        void Apply(WidgetLayout l, Rect r)
        {
            l.x = r.x / Screen.width;
            l.y = r.y / Screen.height;
            l.width = r.width / Screen.width;
            l.height = r.height / Screen.height;
            l.Sanitize();
            rt.Layouts.Dirty = true;
        }

        // ------------------------------------------------------------ Side panel

        void DrawPanel()
        {
            Event e = Event.current;
            Rect header = new Rect(panel.x, panel.y, panel.width, 44f);

            Painter.Rect(new Rect(panel.x + 2f, panel.y + 4f, panel.width, panel.height), new Color(0f, 0f, 0f, 0.35f), 12f);
            Painter.Rect(panel, Theme.Window, 12f);
            Painter.Border(panel, Theme.Line, 1f, 12f);
            Painter.Text(new Rect(panel.x + 18f, panel.y, panel.width - 80f, 44f), "LAYOUT EDITOR", 14, Theme.Text, TextAnchor.MiddleLeft, true);

            Rect close = new Rect(panel.xMax - 40f, panel.y + 9f, 28f, 26f);
            if (Controls.Button(close, "✕", 13)) Toggle();

            // Drag the panel by its header (manual drag so it works under GUI.matrix scaling)
            if (e.type == EventType.MouseDown && e.button == 0 && header.Contains(e.mousePosition) && !close.Contains(e.mousePosition))
            {
                drag = DragMode.Panel;
                dragStartMouse = e.mousePosition;
                dragStartRect = panel;
                e.Use();
            }
            else if (drag == DragMode.Panel && e.type == EventType.MouseDrag)
            {
                panel.position = dragStartRect.position + (e.mousePosition - dragStartMouse);
                e.Use();
            }
            else if (drag == DragMode.Panel && e.type == EventType.MouseUp)
            {
                drag = DragMode.None;
                e.Use();
            }

            Painter.Rect(new Rect(panel.x, header.yMax, panel.width, 1f), Theme.Line);

            Rect view = new Rect(panel.x, header.yMax + 1f, panel.width, panel.height - header.height - 2f);
            ScrollArea.Begin(view, ref panelScroll, panelContentHeight);
            var form = new Form(16f, 8f - panelScroll, view.width - 32f, 0.4f) { RowHeight = 32f, FontSize = 12, UseCards = false };
            try
            {
                DrawPanelContent(form);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Layout editor panel error: " + ex);
            }
            form.End();
            panelContentHeight = form.Y + panelScroll + 16f;
            ScrollArea.End(view, panelScroll, panelContentHeight);
        }

        void DrawPanelContent(Form f)
        {
            f.Section("Elements");
            foreach (HudWidget w in rt.Hud.Widgets)
            {
                WidgetLayout l = rt.Layouts.Get(w.Id);
                bool sel = w.Id == selected;
                Rect row = new Rect(f.X, f.Y + 2f, f.Width, f.RowHeight - 4f);
                if (sel) Painter.Rect(row, Painter.Alpha(Theme.Accent, 0.22f), 6f);
                else if (Controls.Hover(row)) Painter.Rect(row, Theme.Surface, 6f);

                Painter.Text(new Rect(row.x + 10f, row.y, row.width - 90f, row.height), w.Title + (w.CasterOnly ? "  (caster)" : ""), 12,
                    l.visible ? Theme.Text : Theme.TextFaint, TextAnchor.MiddleLeft, sel);
                Rect vis = new Rect(row.xMax - 70f, row.y + 3f, 66f, row.height - 6f);
                if (Controls.Button(vis, l.visible ? "SHOWN" : "HIDDEN", 10, l.visible))
                {
                    l.visible = !l.visible;
                    rt.Layouts.Dirty = true;
                }
                else if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    selected = w.Id;
                }
                f.Y += f.RowHeight;
            }

            WidgetLayout s = rt.Layouts.Get(selected);
            HudWidget sw = rt.Hud.Find(selected);
            if (s != null && sw != null)
            {
                f.Section(sw.Title);
                f.Changed = false;
                s.visible = f.Toggle("Visible", s.visible);
                string[] styles = WidgetIds.StyleNames(selected);
                if (styles != null) s.style = f.Choice("Style", s.style, styles);
                s.x = f.Slider("X position", s.x, 0f, 1f - s.width, v => (v * 100f).ToString("0.0") + "%");
                s.y = f.Slider("Y position", s.y, 0f, 1f - s.height, v => (v * 100f).ToString("0.0") + "%");
                s.width = f.Slider("Width", s.width, 0.02f, 1f, v => (v * 100f).ToString("0.0") + "%");
                s.height = f.Slider("Height", s.height, 0.015f, 1f, v => (v * 100f).ToString("0.0") + "%");
                s.fontSize = f.Slider("Text size", s.fontSize, 8f, 96f, v => v.ToString("0"), 1f);
                s.alignment = (TextAlign)f.Choice("Alignment", (int)s.alignment, AlignNames);
                s.textColor = f.Color("Text color", s.textColor, selected + ".text");
                s.accentColor = f.Color("Accent color", s.accentColor, selected + ".accent");
                s.background = (BackgroundStyle)f.Choice("Background", (int)s.background, BackgroundNames);
                s.backgroundColor = f.Color("Background color", s.backgroundColor, selected + ".bg");
                s.cornerRadius = f.Slider("Corner radius", s.cornerRadius, 0f, 40f, v => v.ToString("0"), 1f);
                s.opacity = f.Slider("Opacity", s.opacity, 0f, 1f, v => (v * 100f).ToString("0") + "%");
                if (f.Changed)
                {
                    s.Sanitize();
                    rt.Layouts.Dirty = true;
                }

                int b = f.Buttons("Center horizontally", "Reset element");
                if (b == 0)
                {
                    s.x = (1f - s.width) * 0.5f;
                    rt.Layouts.Dirty = true;
                }
                else if (b == 1)
                {
                    rt.Layouts.ResetWidget(selected);
                    Form.ForgetBuffers();
                    rt.Toasts.Show(sw.Title + " reset");
                }
            }

            f.Section("Snapping");
            f.Changed = false;
            rt.Settings.snapToGuides = f.Toggle("Snap to guides", rt.Settings.snapToGuides);
            rt.Settings.snapGrid = f.Slider("Grid size", rt.Settings.snapGrid, 0f, 32f, v => v < 1f ? "Off" : v.ToString("0") + " px", 1f);
            if (f.Changed) rt.MarkSettingsDirty();

            DrawPresets(f);
        }

        void DrawPresets(Form f)
        {
            f.Section("Saved layouts");
            f.Note("Current: " + rt.Layouts.Current.name);
            Rect nameRect = new Rect(f.X, f.Y + 3f, f.Width - 96f, f.RowHeight - 6f);
            saveName = Controls.TextField(nameRect, saveName, 40, 12);
            if (Controls.Button(new Rect(nameRect.xMax + 6f, nameRect.y, 90f, nameRect.height), "SAVE", 11, true))
            {
                string name = string.IsNullOrWhiteSpace(saveName) ? "Layout" : saveName.Trim();
                if (rt.Layouts.SaveAs(name))
                {
                    rt.Settings.lastLayoutName = rt.Layouts.Current.name;
                    rt.MarkSettingsDirty();
                    rt.Toasts.Show("Layout '" + rt.Layouts.Current.name + "' saved", Theme.Success);
                }
                else
                {
                    rt.Toasts.Show("Could not save layout", Theme.Danger);
                }
            }
            f.Y += f.RowHeight + 4f;

            IReadOnlyList<string> presets = rt.Layouts.Presets;
            if (presets.Count == 0) f.Note("No saved layouts yet.");
            for (int i = 0; i < presets.Count; i++)
            {
                string name = presets[i];
                Rect row = new Rect(f.X, f.Y + 2f, f.Width, f.RowHeight - 4f);
                Painter.Rect(row, Theme.Surface, 6f);
                Painter.Text(new Rect(row.x + 10f, row.y, row.width - 150f, row.height), Painter.Fit(name, 12, row.width - 160f), 12, Theme.Text, TextAnchor.MiddleLeft);

                Rect load = new Rect(row.xMax - 140f, row.y + 3f, 64f, row.height - 6f);
                Rect del = new Rect(row.xMax - 70f, row.y + 3f, 66f, row.height - 6f);
                if (Controls.Button(load, "LOAD", 10))
                {
                    if (rt.Layouts.LoadPreset(name))
                    {
                        saveName = name;
                        rt.Settings.lastLayoutName = name;
                        rt.MarkSettingsDirty();
                        Form.ForgetBuffers();
                        rt.Toasts.Show("Layout '" + name + "' loaded", Theme.Success);
                    }
                    else
                    {
                        rt.Toasts.Show("Could not load layout", Theme.Danger);
                    }
                }
                bool confirming = pendingDelete == name;
                if (Controls.Button(del, confirming ? "SURE?" : "DELETE", 10, confirming, danger: true))
                {
                    if (confirming)
                    {
                        rt.Layouts.DeletePreset(name);
                        pendingDelete = null;
                        rt.Toasts.Show("Layout '" + name + "' deleted");
                    }
                    else
                    {
                        pendingDelete = name;
                    }
                }
                f.Y += f.RowHeight;
            }

            f.Space(6f);
            if (f.Buttons("Reset whole layout to default") == 0)
            {
                rt.Layouts.ResetAll();
                Form.ForgetBuffers();
                rt.Toasts.Show("Layout reset to default");
            }
        }

        public bool IsMouseOverPanel(Vector2 screenMouse)
        {
            if (!Active) return false;
            float s = rt.MenuScale;
            return new Rect(panel.x * s, panel.y * s, panel.width * s, panel.height * s).Contains(screenMouse);
        }
    }
}
