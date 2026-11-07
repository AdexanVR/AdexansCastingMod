using System;
using System.Collections.Generic;
using AdexansCastingMod.Config;
using UnityEngine;

namespace AdexansCastingMod.UI
{
    internal sealed class HudRenderer
    {
        readonly CastingRuntime rt;
        readonly HashSet<string> loggedErrors = new HashSet<string>();

        // Draw order: later entries are on top (and win hit-tests in the layout editor)
        public readonly List<HudWidget> Widgets;

        public HudRenderer(CastingRuntime rt)
        {
            this.rt = rt;
            Widgets = new List<HudWidget>
            {
                new MinimapWidget(rt),
                new ScoreboardWidget(rt),
                new TimerWidget(rt),
                new RoundInfoWidget(rt),
                new MatchStatusWidget(rt),
                new PlayerListWidget(rt),
                new SpectatorInfoWidget(rt),
                new AutoPilotStatusWidget(rt),
                new CastingPanelWidget(rt),
            };
        }

        public HudWidget Find(string id) => Widgets.Find(w => w.Id == id);

        public static Rect ToScreen(WidgetLayout l)
        {
            float w = Screen.width, h = Screen.height;
            return new Rect(Mathf.Round(l.x * w), Mathf.Round(l.y * h), Mathf.Round(l.width * w), Mathf.Round(l.height * h));
        }

        bool ShouldShow(HudWidget w, WidgetLayout l)
        {
            CastSettings s = rt.Settings;
            return s.overlayVisible && l.visible && (!w.CasterOnly || s.casterControlsVisible);
        }

        public void Tick(float dt)
        {
            CastSettings s = rt.Settings;
            float speed = s.animations ? 6f * s.animationSpeed : 1000f;
            foreach (HudWidget w in Widgets)
            {
                WidgetLayout l = rt.Layouts.Get(w.Id);
                w.Fade = Mathf.MoveTowards(w.Fade, ShouldShow(w, l) ? 1f : 0f, dt * speed);
            }
        }

        public void Draw()
        {
            bool interactive = rt.Settings.casterControlsVisible && !rt.Menu.IsMouseOver;
            foreach (HudWidget w in Widgets)
            {
                if (w.Fade <= 0.001f) continue;
                WidgetLayout l = rt.Layouts.Get(w.Id);
                DrawWidget(w, l, ToScreen(l), Theme.EaseOut(w.Fade) * l.opacity, interactive);
            }
        }

        public void DrawWidget(HudWidget w, WidgetLayout l, Rect r, float alpha, bool interactive)
        {
            if (r.width < 2f || r.height < 2f) return;
            if (!w.HandlesInput && Event.current.type != EventType.Repaint) return;
            DrawBackground(r, l, alpha);

            GUI.BeginGroup(r);
            try
            {
                w.Draw(new WidgetDraw
                {
                    W = r.width,
                    H = r.height,
                    L = l,
                    Alpha = alpha,
                    Scale = rt.HudScale,
                    Interactive = interactive,
                    Shadow = rt.Settings.textShadows,
                });
            }
            catch (Exception e)
            {
                // Log each widget's failure once instead of every frame
                if (loggedErrors.Add(w.Id)) Plugin.Log.LogError($"Widget '{w.Id}' failed to draw: {e}");
            }
            finally
            {
                GUI.EndGroup();
            }
        }

        static void DrawBackground(Rect r, WidgetLayout l, float alpha)
        {
            Color bg = Painter.Alpha(l.backgroundColor, alpha);
            float radius = l.cornerRadius;
            switch (l.background)
            {
                case BackgroundStyle.Solid:
                    Painter.Rect(r, bg);
                    break;
                case BackgroundStyle.Rounded:
                    Painter.Rect(r, bg, radius);
                    break;
                case BackgroundStyle.Gradient:
                    Painter.Gradient(r, bg, radius);
                    break;
                case BackgroundStyle.Glass:
                    Painter.Rect(r, bg, radius);
                    Painter.Border(r, new Color(1f, 1f, 1f, 0.08f * alpha), 1f, radius);
                    // Accent hairline along the top edge, inset past the rounded corners
                    float inset = Mathf.Max(radius, 4f);
                    Painter.Rect(new Rect(r.x + inset, r.y, r.width - inset * 2f, 2f), Painter.Alpha(l.accentColor, alpha * 0.9f), 1f);
                    break;
            }
        }
    }

    // Short notifications ("Auto Pilot ON", "Layout saved") at the top of the screen
    internal sealed class Toasts
    {
        struct Item
        {
            public string Text;
            public float Time;
            public Color Color;
        }

        const float Lifetime = 2.4f;
        readonly List<Item> items = new List<Item>();

        public void Show(string text, Color? color = null)
        {
            if (items.Count > 0 && items[items.Count - 1].Text == text) items.RemoveAt(items.Count - 1);
            items.Add(new Item { Text = text, Time = UnityEngine.Time.unscaledTime, Color = color ?? Theme.Accent });
            if (items.Count > 4) items.RemoveAt(0);
        }

        public void Draw(float scale, bool animations)
        {
            float now = UnityEngine.Time.unscaledTime;
            items.RemoveAll(i => now - i.Time > Lifetime);
            if (items.Count == 0) return;

            int size = Mathf.RoundToInt(14f * scale);
            float y = Screen.height * 0.17f;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                Item it = items[i];
                float age = now - it.Time;
                float a = animations ? Mathf.Clamp01(age * 6f) * Mathf.Clamp01((Lifetime - age) * 3f) : 1f;
                float w = Painter.Width(it.Text, size, true) + size * 2.6f;
                float h = size * 2.3f;
                Rect r = new Rect((Screen.width - w) * 0.5f, y - (1f - a) * 8f, w, h);
                Painter.Rect(r, new Color(0.06f, 0.07f, 0.09f, 0.92f * a), h * 0.5f);
                Painter.Circle(new Vector2(r.x + size * 1.1f, r.center.y), size * 0.3f, Painter.Alpha(it.Color, a));
                Painter.Text(new Rect(r.x + size * 1.7f, r.y, w - size * 2f, h), it.Text, size, new Color(1f, 1f, 1f, a), TextAnchor.MiddleLeft, true);
                y += h + 6f * scale;
            }
        }
    }

    // Clipped, wheel-scrollable region; content is drawn in local space offset by -scroll
    internal static class ScrollArea
    {
        // A stack so scroll areas can be nested (e.g. the font list inside a settings page)
        static readonly Stack<bool> savedEnabled = new Stack<bool>();

        public static void Begin(Rect view, ref float scroll, float contentHeight)
        {
            Event e = Event.current;
            bool inside = view.Contains(e.mousePosition);
            if (e.type == EventType.ScrollWheel && inside)
            {
                scroll += e.delta.y * 24f;
                e.Use();
            }
            scroll = Mathf.Clamp(scroll, 0f, Mathf.Max(0f, contentHeight - view.height));

            // Content scrolled out of view must not react to clicks outside the visible area
            savedEnabled.Push(GUI.enabled);
            if (!inside && e.isMouse) GUI.enabled = false;
            GUI.BeginGroup(view);
        }

        public static void End(Rect view, float scroll, float contentHeight)
        {
            GUI.EndGroup();
            GUI.enabled = savedEnabled.Count > 0 ? savedEnabled.Pop() : true;

            if (contentHeight <= view.height) return;
            float track = view.height - 8f;
            float thumbH = Mathf.Max(24f, track * view.height / contentHeight);
            float t = scroll / Mathf.Max(1f, contentHeight - view.height);
            Painter.Rect(new Rect(view.xMax - 5f, view.y + 4f + (track - thumbH) * t, 3f, thumbH), new Color(1f, 1f, 1f, 0.18f), 1.5f);
        }
    }
}
