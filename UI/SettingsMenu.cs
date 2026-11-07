using System;
using System.Collections.Generic;
using AdexansCastingMod.Config;
using AdexansCastingMod.Core;
using AdexansCastingMod.UI.Fonts;
using UnityEngine;

namespace AdexansCastingMod.UI
{
    internal sealed partial class SettingsMenu
    {
        public static readonly string[] RuleLabels = { "Closest to tagger", "Nearest to camera", "Longest survivor", "Next in list", "Random" };

        static readonly string[] Pages =
        {
            "Home", "Camera", "Auto Pilot", "Match & Timer", "Scoreboard", "Overlay & Effects", "Fonts", "Day & Time",
            "Layout", "Presets", "Shortcuts", "Updates", "Advanced",
        };
        static readonly string[] PageHints =
        {
            "Room, live controls and the match at a glance",
            "How the spectator camera follows players",
            "Automatic player switching",
            "Clock, overtime, sounds and rounds",
            "Teams, scores and player teams",
            "Overlay size, colors, name tags, minimap, banners",
            "Pick the typeface used by the broadcast overlay",
            "Change the time of day and lighting on your PC",
            "Move, resize and save the on-screen layout",
            "Save your whole setup under a name and switch between setups",
            "Keyboard shortcuts",
            "Automatic updates from GitHub",
            "Detection, performance and saved data",
        };
        const int FontsPage = 6;

        static readonly string[] CameraModes = { "Third person", "First person", "Overhead", "Orbit" };
        static readonly string[] SortModes = { "Join order", "Name", "Tagged last", "Score" };
        static readonly string[] Transitions = { "Cut", "Glide" };
        static readonly string[] Detection = { "Game mode", "Mode + rig color" };
        static readonly string[] Formats = { "MM:SS", "MM:SS.t", "Seconds", "H:MM:SS" };
        static readonly string[] Statuses = { "Pre-match", "Live", "Paused", "Overtime", "Intermission", "Round over", "Final", "Custom" };
        static readonly string[] Teams = { "—", "A", "B" };
        static readonly string[] AaModes = { "Off", "FXAA", "SMAA" };
        static readonly string[] SubModes = { "Off", "Survival", "Since tag", "Round" };
        static readonly string[] Effects = { "None", "Fade", "Wipe" };
        static readonly string[] ScoreboardStyles = { "Scorebug", "Panel", "Center bar" };
        static readonly int[] MinimapSizes = { 256, 384, 512, 768 };
        static readonly string[] MinimapSizeNames = { "256", "384", "512", "768" };

        static readonly string[] CameraPresetNames = { "Cinematic", "Close follow", "Wide action", "Player POV", "Top-down" };
        static readonly string[] PilotPresetNames = { "Calm", "Balanced", "Fast" };

        // Fresh defaults, used for the "Reset" buttons next to changed settings
        static readonly CastSettings D = new CastSettings();

        const string SearchField = "AdexanCastSearch";
        const string RoomField = "AdexanCastRoomCode";

        readonly CastingRuntime rt;
        readonly float[] contentHeight = new float[Pages.Length + 1];
        readonly float[] scroll = new float[Pages.Length + 1];

        Rect window = new Rect(0f, 0f, 980f, 640f);
        bool placed;
        bool dragging;
        Vector2 dragOffset;
        int page;
        float pageChangedAt = -10f;
        string search = "";
        string confirm;

        public bool Open { get; private set; }

        public SettingsMenu(CastingRuntime rt)
        {
            this.rt = rt;
        }

        public void Toggle()
        {
            Open = !Open;
            dragging = false;
            confirm = null;
            rt.Shortcuts.CancelCapture();
            ResetPageState();
            if (!Open)
            {
                GUIUtility.keyboardControl = 0;
                // Free font previews; keep only the font in use
                rt.Fonts.ReleaseExcept(rt.Settings.fontId);
                rt.ReloadFont();
            }
            else
            {
                pageChangedAt = Time.unscaledTime;
            }
        }

        public void ShowPage(int index)
        {
            if (!Open) Toggle();
            SelectPage(index);
        }

        void SelectPage(int index)
        {
            page = Mathf.Clamp(index, 0, Pages.Length - 1);
            search = "";
            confirm = null;
            pageChangedAt = Time.unscaledTime;
            rt.Shortcuts.CancelCapture();
            GUIUtility.keyboardControl = 0;
        }

        public bool IsMouseOver
        {
            get
            {
                if (!Open) return false;
                float s = rt.MenuScale;
                Vector2 m = Event.current != null ? Event.current.mousePosition : Vector2.zero;
                return new Rect(window.x * s, window.y * s, window.width * s, window.height * s).Contains(m);
            }
        }

        public void Draw()
        {
            if (!Open) return;
            float s = rt.MenuScale;

            window.height = Mathf.Min(640f, Screen.height / s - 20f);
            window.width = Mathf.Min(980f, Screen.width / s - 20f);
            if (!placed)
            {
                window.x = (Screen.width / s - window.width) * 0.5f;
                window.y = (Screen.height / s - window.height) * 0.5f;
                placed = true;
            }
            window.x = Mathf.Clamp(window.x, 0f, Mathf.Max(0f, Screen.width / s - window.width));
            window.y = Mathf.Clamp(window.y, 0f, Mathf.Max(0f, Screen.height / s - 50f));

            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f)) * old;
            try
            {
                DrawWindow();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Settings menu error: " + e);
            }
            finally
            {
                GUI.matrix = old;
            }
        }

        void DrawWindow()
        {
            Event e = Event.current;
            Rect w = window;
            const float sidebarW = 220f;
            const float headerH = 72f;

            Painter.Rect(new Rect(w.x + 4f, w.y + 8f, w.width, w.height), new Color(0f, 0f, 0f, 0.45f), 16f);
            Painter.Rect(w, Theme.Window, 16f);
            Painter.Rect(new Rect(w.x, w.y, sidebarW, w.height), Theme.Sidebar, 16f);
            Painter.Rect(new Rect(w.x + sidebarW - 16f, w.y, 16f, w.height), Theme.Sidebar);
            Painter.Rect(new Rect(w.x + sidebarW, w.y, 1f, w.height), Theme.Line);
            Painter.Border(w, Theme.Line, 1f, 16f);

            // Brand block
            Painter.Slant(new Rect(w.x + 20f, w.y + 22f, 26f, 26f), Theme.Accent);
            Painter.Text(new Rect(w.x + 54f, w.y + 18f, sidebarW - 60f, 18f), "ADEXAN'S", 11, Theme.TextDim, TextAnchor.MiddleLeft, true);
            Painter.Text(new Rect(w.x + 54f, w.y + 33f, sidebarW - 60f, 20f), "CASTING MOD", 15, Theme.Text, TextAnchor.MiddleLeft, true);

            float y = w.y + 76f;
            bool searching = search.Length > 0;
            float step = Mathf.Clamp((w.height - 76f - 44f) / Pages.Length, 26f, 38f);
            for (int i = 0; i < Pages.Length; i++)
            {
                Rect item = new Rect(w.x + 10f, y, sidebarW - 20f, step - 4f);
                bool sel = !searching && i == page;
                bool hover = Controls.Hover(item);
                if (sel) Painter.Rect(item, Painter.Alpha(Theme.Accent, 0.16f), 8f);
                else if (hover) Painter.Rect(item, Theme.Surface, 8f);
                if (sel) Painter.Rect(new Rect(item.x, item.y + item.height * 0.25f, 3f, item.height * 0.5f), Theme.Accent, 1.5f);
                Painter.Text(new Rect(item.x + 16f, item.y, item.width - 22f, item.height), Pages[i], 13, sel ? Theme.Text : new Color(0.78f, 0.81f, 0.87f, 1f), TextAnchor.MiddleLeft, sel);
                Controls.Tip(item, PageHints[i]);
                if (GUI.Button(item, GUIContent.none, GUIStyle.none)) SelectPage(i);
                y += step;
            }

            Painter.Text(new Rect(w.x + 20f, w.yMax - 30f, sidebarW - 30f, 18f), "v" + PluginInfo.Version + "  ·  settings save automatically", 10, Theme.TextFaint, TextAnchor.MiddleLeft);

            // Header: title, search, close (the header is also the drag handle)
            Rect header = new Rect(w.x + sidebarW + 1f, w.y, w.width - sidebarW - 1f, headerH);
            string title = searching ? "Search results" : Pages[page];
            string hint = searching ? "Settings matching \"" + search + "\"" : PageHints[page];
            Painter.Text(new Rect(header.x + 28f, header.y + 14f, header.width - 400f, 26f), title, 20, Theme.Text, TextAnchor.MiddleLeft, true);
            Painter.Text(new Rect(header.x + 28f, header.y + 40f, header.width - 400f, 18f), Painter.Fit(hint, 12, header.width - 400f), 12, Theme.TextDim, TextAnchor.MiddleLeft);

            Rect close = new Rect(w.xMax - 48f, w.y + 20f, 32f, 32f);
            Rect save = new Rect(close.x - 78f, w.y + 20f, 70f, 32f);
            Rect searchRect = new Rect(save.x - 238f, w.y + 20f, 230f, 32f);
            if (Controls.Button(save, "Save", 12, true))
            {
                rt.SaveAll();
                rt.Toasts.Show("Settings saved", Theme.Success);
            }
            Controls.Tip(save, "Saves all settings right now. They also save automatically a second after every change.");
            GUI.SetNextControlName(SearchField);
            string typed = Controls.TextField(searchRect, search, 40, 13);
            if (typed != search)
            {
                search = typed;
                scroll[Pages.Length] = 0f;
            }
            if (search.Length == 0 && GUI.GetNameOfFocusedControl() != SearchField)
                Painter.Text(new Rect(searchRect.x + 10f, searchRect.y, searchRect.width - 20f, searchRect.height), "Search settings…", 13, Theme.TextFaint, TextAnchor.MiddleLeft);
            else if (search.Length > 0 && Controls.Button(new Rect(searchRect.xMax - 28f, searchRect.y + 4f, 24f, 24f), "✕", 11))
            {
                search = "";
                GUIUtility.keyboardControl = 0;
            }
            if (Controls.Button(close, "✕", 14)) Toggle();
            Controls.Tip(close, "Close settings");

            if (e.type == EventType.MouseDown && e.button == 0 && header.Contains(e.mousePosition) && !close.Contains(e.mousePosition) && !searchRect.Contains(e.mousePosition) && !save.Contains(e.mousePosition))
            {
                dragging = true;
                dragOffset = e.mousePosition - window.position;
                e.Use();
            }
            else if (dragging && e.type == EventType.MouseDrag)
            {
                window.position = e.mousePosition - dragOffset;
                e.Use();
            }
            else if (dragging && e.type == EventType.MouseUp)
            {
                dragging = false;
                e.Use();
            }

            Painter.Rect(new Rect(header.x, header.yMax, header.width, 1f), Theme.Line);

            // Content
            int slot = searching ? Pages.Length : page;
            Rect view = new Rect(header.x, header.yMax + 1f, header.width, w.height - headerH - 2f);
            ScrollArea.Begin(view, ref scroll[slot], contentHeight[slot]);
            // Short slide-in when the page changes
            float slide = rt.Settings.animations ? (1f - Theme.EaseOut((Time.unscaledTime - pageChangedAt) * 6f)) * 14f : 0f;
            var form = new Form(24f, 16f + slide - scroll[slot], view.width - 48f) { Filter = search.Trim() };
            if (searching)
            {
                for (int i = 0; i < Pages.Length; i++)
                {
                    form.Page = Pages[i];
                    DrawPage(i, form);
                }
                if (form.Y + scroll[slot] <= 20f)
                    Painter.Text(new Rect(form.X, 20f, form.Width, 24f), "Nothing matches \"" + search + "\". Try another word, like camera, timer or color.", 13, Theme.TextDim, TextAnchor.MiddleLeft);
            }
            else
            {
                form.Page = Pages[page];
                DrawPage(page, form);
            }
            contentHeight[slot] = form.Y - slide + scroll[slot] + 16f;
            ScrollArea.End(view, scroll[slot], contentHeight[slot]);

            // Swallow clicks on the window background so they don't reach widgets underneath
            if (e.type == EventType.MouseDown && w.Contains(e.mousePosition)) e.Use();
            Controls.DrawTip();
        }

        void DrawPage(int index, Form f)
        {
            f.Changed = false;
            switch (index)
            {
                case 0: HomePage(f); break;
                case 1: CameraPage(f); break;
                case 2: AutoPilotPage(f); break;
                case 3: TimerPage(f); break;
                case 4: ScoreboardPage(f); break;
                case 5: OverlayPage(f); break;
                case 6: FontPage(f); break;
                case 7: DayTimePage(f); break;
                case 8: LayoutPage(f); break;
                case 9: PresetsPage(f); break;
                case 10: ShortcutsPage(f); break;
                case 11: UpdatesPage(f); break;
                case 12: AdvancedPage(f); break;
            }
            f.End();
        }

        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";
        static string Sec(float v) => v.ToString("0.0") + " s";
        static string Meters(float v) => v.ToString("0.0") + " m";

        void SettingsChanged(Form f)
        {
            if (f.Changed) rt.MarkSettingsDirty();
            f.Changed = false;
        }

        // A row of equal buttons; returns the clicked index or -1
        static int ButtonRow(Form f, float height, string[] labels, int active = -1, int size = 12)
        {
            float gap = 8f;
            float bw = (f.Width - gap * (labels.Length - 1)) / labels.Length;
            int clicked = -1;
            for (int i = 0; i < labels.Length; i++)
                if (Controls.Button(new Rect(f.X + (bw + gap) * i, f.Y, bw, height), labels[i], size, i == active))
                    clicked = i;
            f.Y += height + 8f;
            return clicked;
        }

        // ============================================================ Home

        void HomePage(Form f)
        {
            CastSettings s = rt.Settings;

            // ---- Room
            f.Section("Room");
            if (f.Block("room join leave code private public"))
            {
                string status;
                Color statusColor;
                if (rt.Rooms.IsBusy)
                {
                    status = "Joining " + rt.Rooms.Pending + "…";
                    statusColor = Theme.Warning;
                }
                else if (rt.Tracker.InRoom)
                {
                    status = rt.Tracker.RoomName + (rt.Tracker.IsPrivateRoom ? "  ·  Private" : "  ·  Public (camera off)") + "  ·  " + rt.Tracker.All.Count + " players";
                    statusColor = rt.Tracker.IsPrivateRoom ? Theme.Success : Theme.Warning;
                }
                else
                {
                    status = "Not in a room";
                    statusColor = Theme.TextDim;
                }
                Painter.Circle(new Vector2(f.X + 6f, f.Y + 14f), 5f, statusColor);
                Painter.Text(new Rect(f.X + 20f, f.Y, f.Width - 160f, 28f), status, 14, Theme.Text, TextAnchor.MiddleLeft, true);
                if (rt.Tracker.InRoom && Controls.Button(new Rect(f.X + f.Width - 140f, f.Y, 140f, 28f), "Leave room", 12, danger: true))
                    rt.Rooms.Leave();
                f.Y += 38f;

                // Enter in the code field joins, like the in-game keyboard
                Event e = Event.current;
                bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) &&
                             GUI.GetNameOfFocusedControl() == RoomField;
                Rect field = new Rect(f.X, f.Y, f.Width - 150f, 36f);
                GUI.SetNextControlName(RoomField);
                string code = RoomJoiner.Sanitize(Controls.TextField(field, s.roomCode, 32, 16));
                if (code != s.roomCode)
                {
                    s.roomCode = code;
                    rt.MarkSettingsDirty();
                }
                if (s.roomCode.Length == 0 && GUI.GetNameOfFocusedControl() != RoomField)
                    Painter.Text(new Rect(field.x + 10f, field.y, field.width - 20f, field.height), "Room code, e.g. LEAGUE1", 14, Theme.TextFaint, TextAnchor.MiddleLeft);
                bool click = Controls.Button(new Rect(field.xMax + 8f, field.y, 142f, field.height), "Join room", 13, true, !rt.Rooms.IsBusy);
                if ((click || enter) && !rt.Rooms.IsBusy)
                {
                    rt.Rooms.Join(s.roomCode);
                    if (enter) e.Use();
                }
                f.Y += 44f;

                if (s.recentRooms.Count > 0)
                {
                    Painter.Text(new Rect(f.X, f.Y, 60f, 26f), "Recent", 11, Theme.TextDim, TextAnchor.MiddleLeft, true);
                    float x = f.X + 60f;
                    foreach (string recent in s.recentRooms)
                    {
                        float bw = Painter.Width(recent, 12, true) + 26f;
                        if (x + bw > f.X + f.Width) break;
                        bool here = rt.Tracker.InRoom && rt.Tracker.RoomName == recent;
                        if (Controls.Button(new Rect(x, f.Y, bw, 26f), recent, 12, here) && !here && !rt.Rooms.IsBusy)
                            rt.Rooms.Join(recent);
                        x += bw + 6f;
                    }
                    f.Y += 32f;
                }
                f.Note("Codes are letters and numbers (max " + RoomJoiner.MaxCodeLength + "). An unused code creates a new private room.");
            }

            // ---- Live controls
            f.Section("Live controls");
            if (f.Block("camera auto pilot clean feed overlay live"))
            {
                string[] tiles =
                {
                    s.spectatorCameraEnabled ? "Camera  ON" : "Camera  OFF",
                    rt.AutoPilot.Enabled ? "Auto Pilot  ON" : "Auto Pilot  OFF",
                    s.casterControlsVisible ? "Clean feed  OFF" : "Clean feed  ON",
                    s.overlayVisible ? "Overlay  ON" : "Overlay  OFF",
                };
                float gap = 8f, tw = (f.Width - gap * 3f) / 4f;
                bool[] on = { s.spectatorCameraEnabled, rt.AutoPilot.Enabled, !s.casterControlsVisible, s.overlayVisible };
                CastAction[] actions = { CastAction.ToggleSpectatorCamera, CastAction.ToggleAutoPilot, CastAction.ToggleCasterControls, CastAction.ToggleOverlay };
                for (int i = 0; i < 4; i++)
                    if (Controls.Button(new Rect(f.X + (tw + gap) * i, f.Y, tw, 44f), tiles[i], 12, on[i]))
                        rt.Execute(actions[i]);
                f.Y += 52f;
                f.Note("Clean feed hides the caster panel, notifications and the mouse-only controls from the stream.");
            }
            s.cameraMode = (CameraMode)f.Choice("Camera mode", (int)s.cameraMode, CameraModes, "How the camera looks at the spectated player");
            SettingsChanged(f);

            // ---- Camera presets
            f.Section("Camera style");
            if (f.Block("camera preset cinematic close wide pov top"))
            {
                int preset = ButtonRow(f, 40f, CameraPresetNames);
                if (preset >= 0) ApplyCameraPreset(preset);
                f.Note("One click sets mode, field of view, distance and smoothness. Fine-tune on the Camera page.");
            }

            // ---- Spectating
            f.Section("Spectating");
            if (f.Block("players spectate next previous"))
            {
                CastPlayer target = rt.Director.Target;
                Painter.Text(new Rect(f.X, f.Y, f.Width - 230f, 30f), target != null ? "Watching  " + target.Name : "Nobody selected", 15, Theme.Text, TextAnchor.MiddleLeft, true);
                if (Controls.Button(new Rect(f.X + f.Width - 222f, f.Y, 107f, 30f), "‹  Previous", 12)) rt.Director.Step(-1);
                if (Controls.Button(new Rect(f.X + f.Width - 107f, f.Y, 107f, 30f), "Next  ›", 12)) rt.Director.Step(1);
                f.Y += 40f;
                PlayerRows(f);
            }

            // ---- Match
            f.Section("Match");
            if (f.Block("timer score teams round start pause reset"))
            {
                MatchTimer t = rt.Timer;
                MatchData m = rt.Match.Data;
                Painter.Text(new Rect(f.X, f.Y, 180f, 48f), rt.TimerText, 38, t.IsWarning ? Theme.Tagged : Theme.Text, TextAnchor.MiddleLeft, true);
                Painter.Text(new Rect(f.X + 175f, f.Y + 6f, 160f, 18f), StateLabel(t), 11, t.State == TimerState.Running ? Theme.Success : Theme.TextDim, TextAnchor.MiddleLeft, true);
                Painter.Text(new Rect(f.X + 175f, f.Y + 24f, 160f, 18f), "Round " + m.round + " of " + m.totalRounds, 11, Theme.TextDim, TextAnchor.MiddleLeft);
                float bx = f.X + f.Width - 4 * 92f;
                string main = t.State == TimerState.Running ? "Pause" : t.State == TimerState.Paused ? "Resume" : "Start";
                if (Controls.Button(new Rect(bx, f.Y + 8f, 86f, 32f), main, 12, t.State != TimerState.Running)) t.StartPauseResume();
                if (Controls.Button(new Rect(bx + 92f, f.Y + 8f, 86f, 32f), "Reset", 12)) t.Reset();
                if (Controls.Button(new Rect(bx + 184f, f.Y + 8f, 86f, 32f), "Next round", 12))
                {
                    rt.Match.NextRound();
                    rt.Toasts.Show("Round " + m.round);
                }
                if (Controls.Button(new Rect(bx + 276f, f.Y + 8f, 86f, 32f), "Swap teams", 12)) rt.Match.SwapTeams();
                f.Y += 58f;

                TeamScoreRow(f, 1, m.teamAName, m.teamAScore, m.teamAColor);
                TeamScoreRow(f, 2, m.teamBName, m.teamBScore, m.teamBColor);
                f.Note("Team names, colors and records are on the Scoreboard page.");
            }

            // ---- Layout
            f.Section("Look");
            if (f.Block("layout editor scoreboard style"))
            {
                WidgetLayout sb = rt.Layouts.Get(WidgetIds.Scoreboard);
                Painter.Text(new Rect(f.X, f.Y, 200f, 34f), "Scoreboard style", 13, Theme.Text, TextAnchor.MiddleLeft);
                int style = Controls.Segmented(new Rect(f.X + 200f, f.Y + 2f, f.Width - 200f, 30f), sb.style, ScoreboardStyles, 12);
                if (style != sb.style)
                {
                    sb.style = style;
                    rt.Layouts.Dirty = true;
                }
                f.Y += 42f;
                if (ButtonRow(f, 36f, new[] { rt.Editor.Active ? "Close layout editor" : "Open layout editor  (move & resize everything)" }) == 0)
                {
                    rt.Editor.Toggle();
                    if (rt.Editor.Active && Open) Toggle();
                }
            }
        }

        static string StateLabel(MatchTimer t)
        {
            if (t.InOvertime) return "OVERTIME";
            switch (t.State)
            {
                case TimerState.Running: return "● RUNNING";
                case TimerState.Paused: return "PAUSED";
                case TimerState.Finished: return "FINISHED";
                default: return "READY";
            }
        }

        void TeamScoreRow(Form f, int team, string name, int score, Color color)
        {
            Rect row = new Rect(f.X, f.Y, f.Width, 40f);
            Painter.Rect(row, Theme.Surface, 8f);
            Painter.Rect(new Rect(row.x, row.y, 5f, row.height), color, 2f);
            Painter.Text(new Rect(row.x + 18f, row.y, row.width - 260f, row.height), Painter.Fit(name, 15, row.width - 260f), 15, Theme.Text, TextAnchor.MiddleLeft, true);
            Painter.Text(new Rect(row.xMax - 180f, row.y, 60f, row.height), score.ToString(), 22, Theme.Text, TextAnchor.MiddleCenter, true);
            if (Controls.Button(new Rect(row.xMax - 236f, row.y + 5f, 48f, 30f), "−1", 13)) rt.Match.AddTeamScore(team, -1);
            if (Controls.Button(new Rect(row.xMax - 112f, row.y + 5f, 48f, 30f), "+1", 13, true)) rt.Match.AddTeamScore(team, 1);
            f.Y += 48f;
        }

        void PlayerRows(Form f)
        {
            IReadOnlyList<CastPlayer> list = rt.Director.Ordered;
            if (!rt.Tracker.InRoom)
            {
                f.Note("Join a private room to see players here.");
                return;
            }
            if (list.Count == 0)
            {
                f.Note("No other players yet.");
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                CastPlayer p = list[i];
                Rect row = new Rect(f.X, f.Y, f.Width, 34f);
                bool live = p == rt.Director.Target;
                Painter.Rect(row, live ? Painter.Alpha(Theme.Accent, 0.18f) : Theme.Surface, 7f);
                Painter.Text(new Rect(row.x + 10f, row.y, 20f, row.height), ((i + 1) % 10).ToString(), 12, Theme.TextFaint, TextAnchor.MiddleLeft, true);
                Painter.Slant(new Rect(row.x + 30f, row.center.y - 7f, 21f, 14f), p.IsTagged ? WidgetUtil.LavaColor : p.Color);
                Painter.Text(new Rect(row.x + 60f, row.y, row.width - 260f, row.height), Painter.Fit(p.Name, 13, row.width - 270f), 13,
                    p.IsTagged ? Theme.TextDim : Theme.Text, TextAnchor.MiddleLeft, live);
                if (p.IsTagged) Painter.Pill(row.xMax - 136f, row.center.y, "TAGGED", 10, Theme.Tagged, Color.white, alignRight: true);
                if (Controls.Button(new Rect(row.xMax - 124f, row.y + 4f, 118f, row.height - 8f), live ? "Watching" : "Spectate", 11, live))
                    rt.Director.Select(p, SelectSource.Manual);
                f.Y += 38f;
            }
        }

        void ApplyCameraPreset(int i)
        {
            CastSettings s = rt.Settings;
            switch (i)
            {
                case 0: // Cinematic: further back, slow and smooth
                    s.cameraMode = CameraMode.ThirdPerson;
                    s.fieldOfView = 78f; s.followDistance = 3.2f; s.followHeight = 0.9f;
                    s.positionResponsiveness = 4f; s.rotationResponsiveness = 5f; s.transition = TransitionStyle.Glide;
                    break;
                case 1: // Close follow: tight over-the-shoulder, snappy
                    s.cameraMode = CameraMode.ThirdPerson;
                    s.fieldOfView = 85f; s.followDistance = 1.8f; s.followHeight = 0.45f;
                    s.positionResponsiveness = 9f; s.rotationResponsiveness = 10f; s.transition = TransitionStyle.Glide;
                    break;
                case 2: // Wide action: high and far, shows chasers around the runner
                    s.cameraMode = CameraMode.ThirdPerson;
                    s.fieldOfView = 95f; s.followDistance = 4.5f; s.followHeight = 1.6f;
                    s.positionResponsiveness = 6f; s.rotationResponsiveness = 7f; s.transition = TransitionStyle.Glide;
                    break;
                case 3: // Player POV
                    s.cameraMode = CameraMode.FirstPerson;
                    s.fieldOfView = 90f; s.positionResponsiveness = 14f; s.rotationResponsiveness = 12f;
                    s.stabilizeHorizon = true; s.transition = TransitionStyle.Cut;
                    break;
                default: // Top-down
                    s.cameraMode = CameraMode.Overhead;
                    s.fieldOfView = 70f; s.overheadHeight = 6f;
                    s.positionResponsiveness = 6f; s.rotationResponsiveness = 6f; s.transition = TransitionStyle.Glide;
                    break;
            }
            rt.MarkSettingsDirty();
            rt.Toasts.Show("Camera style: " + CameraPresetNames[i], Theme.Success);
        }

        // ============================================================ Camera

        void CameraPage(Form f)
        {
            CastSettings s = rt.Settings;

            f.Section("Spectator camera");
            s.spectatorCameraEnabled = f.Toggle("Spectator camera", s.spectatorCameraEnabled, "Shows the followed player's view on your PC screen (never in the headset)", D.spectatorCameraEnabled);
            s.cameraMode = (CameraMode)f.Choice("Camera mode", (int)s.cameraMode, CameraModes, null, (int)D.cameraMode);
            s.fieldOfView = f.Slider("Field of view", s.fieldOfView, 30f, 120f, v => v.ToString("0") + "°", 1f, "How wide the camera sees. Higher = more of the map, smaller players", D.fieldOfView);
            s.followDistance = f.Slider("Distance behind player", s.followDistance, 0.5f, 10f, Meters, 0.1f, "Third person and orbit only", D.followDistance);
            s.followHeight = f.Slider("Height above player", s.followHeight, -1f, 5f, Meters, 0.1f, "Third person and orbit only", D.followHeight);
            s.avoidWalls = f.Toggle("Keep camera out of walls", s.avoidWalls, "Pulls the camera in front of walls and trees between it and the player", D.avoidWalls);
            SettingsChanged(f);

            f.Section("Smoothness");
            s.positionResponsiveness = f.Slider("Follow speed", s.positionResponsiveness, 0f, 30f, v => v < 0.05f ? "Instant" : v.ToString("0.0"), 0.5f, "Lower = smoother, floatier camera. Higher = sticks tightly to the player", D.positionResponsiveness);
            s.rotationResponsiveness = f.Slider("Turn speed", s.rotationResponsiveness, 0f, 30f, v => v < 0.05f ? "Instant" : v.ToString("0.0"), 0.5f, "How fast the camera turns to face where the player looks", D.rotationResponsiveness);
            s.transition = (TransitionStyle)f.Choice("When switching players", (int)s.transition, Transitions, "Cut jumps straight to the new player, Glide flies there", (int)D.transition);
            s.glideMaxDistance = f.Slider("Glide only if closer than", s.glideMaxDistance, 1f, 100f, Meters, 1f, "Further away players are always a cut", D.glideMaxDistance);
            SettingsChanged(f);

            f.Section("Per-mode options");
            s.firstPersonOffset = f.Slider("First person: forward offset", s.firstPersonOffset, 0f, 0.5f, v => v.ToString("0.00") + " m", 0.01f, "Moves the eye forward so the player's own head doesn't block the view", D.firstPersonOffset);
            s.stabilizeHorizon = f.Toggle("First person: keep horizon level", s.stabilizeHorizon, "Removes head tilt, much easier to watch", D.stabilizeHorizon);
            s.overheadHeight = f.Slider("Overhead: height", s.overheadHeight, 1f, 20f, Meters, 0.5f, null, D.overheadHeight);
            s.orbitSpeed = f.Slider("Orbit: speed", s.orbitSpeed, -90f, 90f, v => v.ToString("0") + "°/s", 1f, "Negative orbits the other way", D.orbitSpeed);
            SettingsChanged(f);

            f.Section("Image quality");
            s.antiAliasing = f.Choice("Anti-aliasing", s.antiAliasing, AaModes, "Smooths jagged edges. SMAA looks best, FXAA is cheapest", D.antiAliasing);
            s.postProcessing = f.Toggle("Post-processing", s.postProcessing, "The game's color grading and effects", D.postProcessing);
            s.cameraShadows = f.Toggle("Shadows", s.cameraShadows, "Turn off for more FPS on slower PCs", D.cameraShadows);
            SettingsChanged(f);

            f.Section("Other players' movement (rig lerp)");
            s.rigLerpOverride = f.Toggle("Use my own smoothing", s.rigLerpOverride, "Overrides how the game smooths other players' movement on this PC", D.rigLerpOverride);
            s.rigLerpValue = f.Slider("Smoothing value", s.rigLerpValue, 0.02f, 1f, v => v.ToString("0.00"), 0.01f, "Lower = smooth but delayed, higher = accurate but choppy", D.rigLerpValue);
            if (f.Block("rig lerp default"))
            {
                float? gameDefault = rt.RigSmoothing.GameDefault;
                f.Note("Game default: " + (gameDefault.HasValue ? gameDefault.Value.ToString("0.00") : "shown once another player joins") + ". Private rooms only; nobody else is affected.");
            }
            SettingsChanged(f);
        }

        // ============================================================ Auto pilot

        void AutoPilotPage(Form f)
        {
            CastSettings s = rt.Settings;

            f.Section("Auto Pilot");
            bool enabled = f.Toggle("Auto Pilot", rt.AutoPilot.Enabled, "Automatically moves the camera to another runner when the watched player gets tagged", D.autoPilotEnabled);
            if (enabled != rt.AutoPilot.Enabled) rt.AutoPilot.Enabled = enabled;
            if (f.Block("status override resume"))
            {
                string state;
                switch (rt.AutoPilot.State)
                {
                    case AutoPilotState.Override: state = "Paused by your manual pick, resumes in " + Mathf.CeilToInt(rt.AutoPilot.OverrideRemaining) + " s"; break;
                    case AutoPilotState.Switching: state = "Switching in " + rt.AutoPilot.PendingRemaining.ToString("0.0") + " s"; break;
                    case AutoPilotState.Waiting: state = "Waiting for players"; break;
                    case AutoPilotState.Following: state = "Following " + (rt.Director.Target?.Name ?? "-"); break;
                    default: state = "Off"; break;
                }
                f.Note("Now: " + state + (string.IsNullOrEmpty(rt.AutoPilot.LastReason) ? "" : "   ·   Last switch: " + rt.AutoPilot.LastReason));
                if (rt.AutoPilot.InOverride && f.Buttons("Resume Auto Pilot now") == 0) rt.AutoPilot.CancelOverride();
            }

            f.Section("Quick presets");
            if (f.Block("preset calm balanced fast"))
            {
                int p = ButtonRow(f, 40f, PilotPresetNames);
                if (p >= 0) ApplyPilotPreset(p);
                f.Note("Calm stays on players longer, Fast reacts instantly to every tag.");
            }

            f.Section("Who to switch to");
            s.switchRule = (SwitchRule)f.Choice("Next player", (int)s.switchRule, RuleLabels, "\"Closest to tagger\" goes to the runner a tagger is chasing, usually the best action", (int)D.switchRule);
            SettingsChanged(f);

            f.Section("Timing");
            s.switchDelay = f.Slider("Wait after a tag", s.switchDelay, 0f, 10f, Sec, 0.1f, "So viewers see the tag happen before the camera leaves", D.switchDelay);
            s.minHoldTime = f.Slider("Stay on a player at least", s.minHoldTime, 0f, 30f, Sec, 0.5f, "Stops the camera jumping around when many tags happen at once", D.minHoldTime);
            s.recentTargetCooldown = f.Slider("Don't go back to a player for", s.recentTargetCooldown, 0f, 60f, Sec, 1f, "Avoids bouncing between the same two players", D.recentTargetCooldown);
            s.manualOverrideDuration = f.Slider("After you pick a player, pause for", s.manualOverrideDuration, 0f, 120f, v => v < 0.5f ? "Don't pause" : v.ToString("0") + " s", 1f, "Your manual choice wins for this long", D.manualOverrideDuration);
            SettingsChanged(f);

            f.Section("Rotation");
            s.idleRotation = f.Toggle("Rotate between runners", s.idleRotation, "Switches to another runner now and then, even without tags", D.idleRotation);
            s.idleRotationInterval = f.Slider("Every", s.idleRotationInterval, 5f, 300f, v => v.ToString("0") + " s", 5f, null, D.idleRotationInterval);
            SettingsChanged(f);
        }

        void ApplyPilotPreset(int i)
        {
            CastSettings s = rt.Settings;
            switch (i)
            {
                case 0:
                    s.switchDelay = 2f; s.minHoldTime = 6f; s.recentTargetCooldown = 15f; s.manualOverrideDuration = 25f;
                    s.idleRotation = true; s.idleRotationInterval = 45f;
                    break;
                case 1:
                    s.switchDelay = D.switchDelay; s.minHoldTime = D.minHoldTime; s.recentTargetCooldown = D.recentTargetCooldown;
                    s.manualOverrideDuration = D.manualOverrideDuration; s.idleRotation = D.idleRotation; s.idleRotationInterval = D.idleRotationInterval;
                    break;
                default:
                    s.switchDelay = 0.4f; s.minHoldTime = 1.5f; s.recentTargetCooldown = 4f; s.manualOverrideDuration = 8f;
                    s.idleRotation = false;
                    break;
            }
            rt.MarkSettingsDirty();
            rt.Toasts.Show("Auto Pilot: " + PilotPresetNames[i], Theme.Success);
        }

        // ============================================================ Match & timer

        void TimerPage(Form f)
        {
            CastSettings s = rt.Settings;
            MatchTimer t = rt.Timer;
            MatchData m = rt.Match.Data;

            f.Section("Clock");
            if (f.Block("timer start pause reset clock time"))
            {
                Painter.Text(new Rect(f.X, f.Y, f.Width, 52f), rt.TimerText, 42, t.IsWarning ? Theme.Tagged : Theme.Text, TextAnchor.MiddleLeft, true);
                Painter.Text(new Rect(f.X, f.Y, f.Width, 52f), StateLabel(t), 12, t.State == TimerState.Running ? Theme.Success : Theme.TextDim, TextAnchor.MiddleRight, true);
                f.Y += 58f;
                string main = t.State == TimerState.Running ? "Pause" : t.State == TimerState.Paused ? "Resume" : "Start";
                int b = ButtonRow(f, 34f, new[] { main, "Reset", "−10 s", "+10 s", "+1 min" });
                if (b == 0) t.StartPauseResume();
                else if (b == 1) t.Reset();
                else if (b == 2) t.Adjust(-10f);
                else if (b == 3) t.Adjust(10f);
                else if (b == 4) t.Adjust(60f);
            }

            f.Section("Timer");
            s.matchDuration = f.Slider("Match length", s.matchDuration, 0f, 3600f, v => v < 1f ? "No limit" : WidgetUtil.Clock(v), 15f, "Slide to 0 for a stopwatch with no end", D.matchDuration);
            s.countdown = f.Toggle("Count down", s.countdown, "Off = counts up from 0:00", D.countdown);
            s.timerFormat = (TimerFormat)f.Choice("Format", (int)s.timerFormat, Formats, null, (int)D.timerFormat);
            s.tenthsInFinalTen = f.Toggle("Show tenths in the last 10 seconds", s.tenthsInFinalTen, null, D.tenthsInFinalTen);
            s.timerLabel = f.Text("Label above the clock", s.timerLabel, 32, "Used by the standalone timer element");
            SettingsChanged(f);

            f.Section("Overtime");
            s.overtimeEnabled = f.Toggle("Overtime", s.overtimeEnabled, "When time runs out, keep playing instead of ending", D.overtimeEnabled);
            s.overtimeDuration = f.Slider("Overtime length", s.overtimeDuration, 0f, 600f, v => v < 1f ? "Until you stop it" : WidgetUtil.Clock(v), 5f, null, D.overtimeDuration);
            SettingsChanged(f);

            f.Section("Time running out");
            s.warningThreshold = f.Slider("Warn when this much is left", s.warningThreshold, 0f, 300f, v => v < 1f ? "Never" : v.ToString("0") + " s", 5f, "The clock turns red and pulses", D.warningThreshold);
            s.warningFlash = f.Toggle("Flash the clock", s.warningFlash, null, D.warningFlash);
            s.warningSound = f.Toggle("Warning sound", s.warningSound, null, D.warningSound);
            s.finalCountdownBeeps = f.Toggle("Beep every final second", s.finalCountdownBeeps, null, D.finalCountdownBeeps);
            s.finalCountdownSeconds = f.Stepper("Beep for the last", s.finalCountdownSeconds, 1, 30, "seconds", D.finalCountdownSeconds);
            s.endSound = f.Toggle("Buzzer at the end", s.endSound, null, D.endSound);
            s.soundVolume = f.Slider("Sound volume", s.soundVolume, 0f, 1f, Pct, 0.05f, "Only you hear these, they don't go to the room", D.soundVolume);
            SettingsChanged(f);
            if (f.Block("test sound"))
            {
                int tb = ButtonRow(f, 32f, new[] { "Test tick", "Test warning", "Test buzzer" });
                if (tb == 0) rt.Beeper.Play(Cue.Tick);
                else if (tb == 1) rt.Beeper.Play(Cue.Warning);
                else if (tb == 2) rt.Beeper.Play(Cue.End);
            }

            f.Section("Rounds");
            m.round = f.Stepper("Current round", m.round, 1, 99);
            m.totalRounds = f.Stepper("Rounds in the match", m.totalRounds, 1, 99);
            if (m.round > m.totalRounds) m.totalRounds = m.round;
            if (f.Changed) rt.Match.Dirty = true;
            f.Changed = false;
            s.resetTimerOnNextRound = f.Toggle("Reset the clock on next round", s.resetTimerOnNextRound, null, D.resetTimerOnNextRound);
            s.autoMatchStatus = f.Toggle("Status follows the clock", s.autoMatchStatus, "Shows Live / Paused / Overtime / Round over automatically", D.autoMatchStatus);
            SettingsChanged(f);
            m.status = (MatchStatus)f.Choice("Status when the clock is stopped", (int)m.status, Statuses);
            if (m.status == MatchStatus.Custom) m.customStatus = f.Text("Custom status text", m.customStatus, 32);
            if (f.Changed) rt.Match.Dirty = true;
            f.Changed = false;
        }

        // ============================================================ Scoreboard

        void ScoreboardPage(Form f)
        {
            CastSettings s = rt.Settings;
            MatchManager mm = rt.Match;
            MatchData m = mm.Data;

            f.Section("Team A (left)");
            m.teamAName = f.Text("Name", m.teamAName, 32);
            m.teamAColor = f.Color("Color", m.teamAColor, "team.a", false);
            m.teamAScore = f.Stepper("Score", m.teamAScore, 0, 999);
            m.teamARecord = f.Text("Record", m.teamARecord, 12, "Shown under the name in the center bar, e.g. 2-1");
            if (f.Changed) mm.Dirty = true;
            f.Changed = false;

            f.Section("Team B (right)");
            m.teamBName = f.Text("Name", m.teamBName, 32);
            m.teamBColor = f.Color("Color", m.teamBColor, "team.b", false);
            m.teamBScore = f.Stepper("Score", m.teamBScore, 0, 999);
            m.teamBRecord = f.Text("Record", m.teamBRecord, 12, "Shown under the name in the center bar, e.g. 2-1");
            if (f.Changed) mm.Dirty = true;
            f.Changed = false;

            f.Section("Match");
            m.matchTitle = f.Text("Title", m.matchTitle, 48, "Strip under the center bar, e.g. Season 5. Empty hides it");
            if (f.Changed) mm.Dirty = true;
            f.Changed = false;
            int b = f.Buttons("Swap teams", confirm == "scores" ? "Click again to reset" : "Reset scores", confirm == "match" ? "Click again to reset" : "Reset whole match");
            if (b == 0) mm.SwapTeams();
            else if (b == 1)
            {
                if (confirm == "scores") { mm.ResetScores(); confirm = null; }
                else confirm = "scores";
            }
            else if (b == 2)
            {
                if (confirm == "match") { mm.ResetMatch(); confirm = null; }
                else confirm = "match";
            }

            f.Section("Players & teams");
            if (f.Block("player team score"))
            {
                IReadOnlyList<CastPlayer> list = rt.Director.Ordered;
                if (list.Count == 0) f.Note("Players appear here once you're in a room with them.");
                else
                {
                    Painter.Text(new Rect(f.X + f.Width - 290f, f.Y, 140f, 18f), "TEAM", 10, Theme.TextFaint, TextAnchor.MiddleCenter, true);
                    Painter.Text(new Rect(f.X + f.Width - 136f, f.Y, 132f, 18f), "PLAYER SCORE", 10, Theme.TextFaint, TextAnchor.MiddleCenter, true);
                    f.Y += 22f;
                }
                foreach (CastPlayer p in list)
                {
                    PlayerRecord r = mm.Record(p);
                    Rect row = new Rect(f.X, f.Y, f.Width, 36f);
                    Painter.Rect(row, Theme.Surface, 7f);
                    Painter.Slant(new Rect(row.x + 10f, row.center.y - 7f, 21f, 14f), p.Color);
                    float nameW = row.width - 40f - 300f - 80f;
                    Painter.Text(new Rect(row.x + 40f, row.y, nameW, row.height), Painter.Fit(p.Name, 13, nameW), 13, Theme.Text, TextAnchor.MiddleLeft);
                    if (s.showTagCounts)
                        Painter.Text(new Rect(row.x + 40f + nameW, row.y, 76f, row.height), r.timesTagged + "× tagged", 11, Theme.TextDim, TextAnchor.MiddleRight);

                    int team = Controls.Segmented(new Rect(row.xMax - 290f, row.y + 4f, 140f, row.height - 8f), r.team, Teams, 11);
                    if (team != r.team) mm.SetTeam(r, team);
                    int score = Controls.Stepper(new Rect(row.xMax - 136f, row.y + 4f, 132f, row.height - 8f), r.score, -999, 999, 12);
                    if (score != r.score) mm.AddPlayerScore(r, score - r.score);
                    f.Y += 40f;
                }
                int pb = ButtonRow(f, 32f, new[] { "Reset tag counts", "Forget players who left" });
                if (pb == 0) mm.ResetTagCounts();
                else if (pb == 1) mm.PruneRecords(rt.Tracker);
            }

            f.Section("What the scoreboard shows");
            s.scorebugSubTimer = (SubTimerMode)f.Choice("Small box under the clock", (int)s.scorebugSubTimer, SubModes, "Scorebug style: watched player's survival time, time since last tag, or round", (int)D.scorebugSubTimer);
            s.showIndividualScores = f.Toggle("Player scores", s.showIndividualScores, null, D.showIndividualScores);
            s.showTagCounts = f.Toggle("How often each player was tagged", s.showTagCounts, null, D.showTagCounts);
            s.countTagsAutomatically = f.Toggle("Count tags automatically", s.countTagsAutomatically, null, D.countTagsAutomatically);
            s.showUnassignedPlayers = f.Toggle("Players without a team", s.showUnassignedPlayers, "Panel style only", D.showUnassignedPlayers);
            s.maxScoreboardRows = f.Stepper("Max players per team", s.maxScoreboardRows, 1, 10, "Panel style only", D.maxScoreboardRows);
            SettingsChanged(f);
        }

        // ============================================================ Overlay & effects

        void OverlayPage(Form f)
        {
            CastSettings s = rt.Settings;

            f.Section("Overlay");
            s.overlayVisible = f.Toggle("Show overlay", s.overlayVisible, "Everything drawn over the camera", D.overlayVisible);
            s.casterControlsVisible = f.Toggle("Show caster controls", s.casterControlsVisible, "Off = clean feed: only broadcast graphics are visible", D.casterControlsVisible);
            s.showToasts = f.Toggle("Notifications", s.showToasts, "Small pop-ups like \"Auto Pilot ON\" (never shown in clean feed)", D.showToasts);
            s.hudScale = f.Slider("Overlay size", s.hudScale, 0.5f, 2.5f, Pct, 0.05f, "Text size of everything on screen", D.hudScale);
            s.menuScale = f.Slider("Menu size", s.menuScale, 0.6f, 2f, Pct, 0.05f, "Size of this window and the layout editor", D.menuScale);
            SettingsChanged(f);

            f.Section("Broadcast effects");
            s.showBanners = f.Toggle("Big banners", s.showBanners, "Animated banners for round start, round end, overtime and last survivor", D.showBanners);
            s.bannerRoundEvents = f.Toggle("Round start / end / overtime", s.bannerRoundEvents, null, D.bannerRoundEvents);
            s.bannerLastSurvivor = f.Toggle("Last player standing", s.bannerLastSurvivor, "When only one runner is left", D.bannerLastSurvivor);
            s.bannerDuration = f.Slider("Banner time", s.bannerDuration, 1f, 10f, Sec, 0.5f, null, D.bannerDuration);
            s.switchEffect = (SwitchEffect)f.Choice("Player switch effect", (int)s.switchEffect, Effects, "Plays when the camera cuts to another player", (int)D.switchEffect);
            SettingsChanged(f);
            if (f.Block("test banner preview"))
            {
                int b = ButtonRow(f, 32f, new[] { "Preview banner", "Preview switch effect" });
                if (b == 0) rt.Banners.Show("ROUND " + rt.Match.Data.round, "GET READY", Theme.Accent);
                else if (b == 1) rt.Banners.PlaySwitchEffect();
            }

            f.Section("Colors & text");
            s.accentColor = f.Color("Menu accent color", s.accentColor, "general.accent", false, null, D.accentColor);
            s.textShadows = f.Toggle("Text shadows", s.textShadows, "Easier to read over bright maps", D.textShadows);
            s.animations = f.Toggle("Animations", s.animations, null, D.animations);
            s.animationSpeed = f.Slider("Animation speed", s.animationSpeed, 0.25f, 4f, v => v.ToString("0.00") + "×", 0.05f, null, D.animationSpeed);
            if (f.Block("font typeface"))
            {
                FontEntry current = rt.Fonts.Find(s.fontId);
                Painter.Text(new Rect(f.X, f.Y, f.LabelWidth, 36f), "Font", f.FontSize, Theme.Text, TextAnchor.MiddleLeft);
                Painter.Text(new Rect(f.X + f.LabelWidth, f.Y, f.Width - f.LabelWidth - 160f, 36f), current != null ? current.Name : s.fontId, f.FontSize, Theme.TextDim, TextAnchor.MiddleLeft, true);
                if (Controls.Button(new Rect(f.X + f.Width - 150f, f.Y + 4f, 150f, 28f), "Choose font…", 12, true)) SelectPage(FontsPage);
                f.Y += 38f;
            }
            SettingsChanged(f);

            f.Section("Name tags");
            s.showNameTags = f.Toggle("Names above players", s.showNameTags, "In each player's color, lava orange when tagged", D.showNameTags);
            s.nameTagOnSpectated = f.Toggle("Also above the watched player", s.nameTagOnSpectated, null, D.nameTagOnSpectated);
            s.nameTagSize = f.Slider("Size", s.nameTagSize, 8f, 48f, v => v.ToString("0"), 1f, null, D.nameTagSize);
            s.nameTagMaxDistance = f.Slider("Hide when further than", s.nameTagMaxDistance, 5f, 300f, Meters, 5f, null, D.nameTagMaxDistance);
            SettingsChanged(f);

            f.Section("Minimap");
            s.minimapZoom = f.Slider("Area shown", s.minimapZoom, 3f, 80f, Meters, 1f, "Width of the map in meters. Lower = more zoomed in", D.minimapZoom);
            s.minimapRotate = f.Toggle("Turn with the camera", s.minimapRotate, "Off = north always up", D.minimapRotate);
            s.minimapMarkers = f.Toggle("Player dots", s.minimapMarkers, null, D.minimapMarkers);
            s.minimapMaxHeight = f.Slider("Max camera height", s.minimapMaxHeight, 2f, 100f, Meters, 1f, "It drops below ceilings and trees automatically", D.minimapMaxHeight);
            int sizeIndex = Array.IndexOf(MinimapSizes, s.minimapResolution);
            sizeIndex = f.Choice("Sharpness", sizeIndex < 0 ? 1 : sizeIndex, MinimapSizeNames, "Higher = sharper, costs a little FPS", Array.IndexOf(MinimapSizes, D.minimapResolution));
            s.minimapResolution = MinimapSizes[sizeIndex];
            s.minimapFps = Mathf.RoundToInt(f.Slider("Refresh rate", s.minimapFps, 5f, 120f, v => v.ToString("0") + " fps", 5f, null, D.minimapFps));
            SettingsChanged(f);
        }

        // ============================================================ Layout

        void LayoutPage(Form f)
        {
            f.Section("Layout editor");
            if (f.Block("layout editor move resize"))
            {
                f.Note("Drag elements to move them, drag edges to resize, click one to change its colors and style.");
                if (ButtonRow(f, 40f, new[] { rt.Editor.Active ? "Close layout editor" : "Open layout editor" }) == 0)
                {
                    rt.Editor.Toggle();
                    if (rt.Editor.Active) Toggle();
                }
            }

            f.Section("Show / hide elements");
            foreach (HudWidget w in rt.Hud.Widgets)
            {
                WidgetLayout l = rt.Layouts.Get(w.Id);
                bool next = f.Toggle(w.Title, l.visible, w.CasterOnly ? "Caster only: hidden in clean feed" : null);
                if (next != l.visible)
                {
                    l.visible = next;
                    rt.Layouts.Dirty = true;
                }
            }
            f.Changed = false;

            f.Section("Saved layouts");
            f.Note("Current layout: " + rt.Layouts.Current.name + "   ·   save new ones from the layout editor.");
            IReadOnlyList<string> presets = rt.Layouts.Presets;
            foreach (string name in presets)
            {
                if (f.Button(name, "Load") && rt.Layouts.LoadPreset(name))
                {
                    rt.Settings.lastLayoutName = name;
                    rt.MarkSettingsDirty();
                    Form.ForgetBuffers();
                    rt.Toasts.Show("Layout '" + name + "' loaded", Theme.Success);
                }
            }
            if (f.Buttons(confirm == "layout" ? "Click again to reset the layout" : "Reset layout to default") == 0)
            {
                if (confirm == "layout")
                {
                    rt.Layouts.ResetAll();
                    Form.ForgetBuffers();
                    confirm = null;
                    rt.Toasts.Show("Layout reset to default");
                }
                else confirm = "layout";
            }
        }

        // ============================================================ Shortcuts

        void ShortcutsPage(Form f)
        {
            List<KeyBinding> bindings = rt.Settings.keyBindings;
            f.Section("Keyboard shortcuts");
            f.Note("Click a key to change it, then press the new key. Esc cancels, Backspace removes it.");
            foreach (CastAction a in KeyBindings.AllActions)
            {
                KeyBinding b = KeyBindings.Find(bindings, a);
                string label = KeyBindings.ActionLabel(a);
                if (b == null || !f.Block(label + " shortcut key")) continue;

                Rect row = new Rect(f.X - 8f, f.Y, f.Width + 16f, 36f);
                if (Controls.Hover(row)) Painter.Rect(row, Theme.RowHover, 6f);
                Painter.Text(new Rect(f.X, f.Y, f.Width - 300f, 36f), label, f.FontSize, Theme.Text, TextAnchor.MiddleLeft);

                bool capturing = rt.Shortcuts.Capturing == a;
                string keyText = capturing ? "Press a key…" : KeyBindings.Describe(b);
                Rect btn = new Rect(f.X + f.Width - 236f, f.Y + 4f, 196f, 28f);
                if (Controls.Button(btn, keyText, 12, capturing))
                {
                    if (capturing) rt.Shortcuts.CancelCapture();
                    else rt.Shortcuts.BeginCapture(a);
                }
                if (Controls.Button(new Rect(btn.xMax + 6f, btn.y, 34f, btn.height), "✕", 12) && b.IsBound)
                {
                    b.key = 0;
                    b.ctrl = b.shift = b.alt = false;
                    rt.MarkSettingsDirty();
                }

                string conflict = FindConflict(bindings, b);
                if (conflict != null)
                    Painter.Text(new Rect(f.X + 200f, f.Y, f.Width - 450f, 36f), Painter.Fit("Same key as: " + conflict, 11, f.Width - 450f), 11, Theme.Warning, TextAnchor.MiddleRight);
                f.Y += 38f;
            }

            if (f.Buttons(confirm == "keys" ? "Click again to restore defaults" : "Restore default shortcuts") == 0)
            {
                if (confirm == "keys")
                {
                    rt.Settings.keyBindings = KeyBindings.Defaults();
                    rt.MarkSettingsDirty();
                    confirm = null;
                    rt.Toasts.Show("Default shortcuts restored");
                }
                else confirm = "keys";
            }
        }

        static string FindConflict(List<KeyBinding> bindings, KeyBinding b)
        {
            if (!b.IsBound) return null;
            foreach (KeyBinding o in bindings)
            {
                if (o == b || !o.IsBound) continue;
                if (o.key == b.key && o.ctrl == b.ctrl && o.shift == b.shift && o.alt == b.alt && KeyBindings.TryGetAction(o.action, out CastAction a))
                    return KeyBindings.ActionLabel(a);
            }
            return null;
        }

        // ============================================================ Advanced

        void AdvancedPage(Form f)
        {
            CastSettings s = rt.Settings;

            f.Section("Players");
            s.playerSort = (PlayerSort)f.Choice("Sort the player list by", (int)s.playerSort, SortModes, "\"Tagged last\" keeps runners at the top", (int)D.playerSort);
            s.includeLocalPlayer = f.Toggle("Include yourself", s.includeLocalPlayer, "Lets you spectate your own player (useful for testing)", D.includeLocalPlayer);
            s.reselectOnLeave = f.Toggle("Pick someone else when the watched player leaves", s.reselectOnLeave, null, D.reselectOnLeave);
            s.tagDetection = (TagDetection)f.Choice("Tag detection", (int)s.tagDetection, Detection, "\"Mode + rig color\" also counts the lava/infected look, for custom modes", (int)D.tagDetection);
            SettingsChanged(f);

            f.Section("Performance");
            s.tagPollInterval = f.Slider("Player check interval", s.tagPollInterval, 0.03f, 1f, v => Mathf.RoundToInt(v * 1000f) + " ms", 0.01f, "How often players and tags are checked. Lower = faster reactions", D.tagPollInterval);
            s.disableGameDesktopCamera = f.Toggle("Pause the game's own desktop camera", s.disableGameDesktopCamera, "Saves FPS while the spectator camera is live", D.disableGameDesktopCamera);
            SettingsChanged(f);

            f.Section("Saved data");
            f.Note("Everything saves automatically to BepInEx/config/AdexansCastingMod.");
            int b = f.Buttons("Save everything now", confirm == "settings" ? "Click again to confirm" : "Reset all settings to default");
            if (b == 0)
            {
                rt.SaveAll();
                rt.Toasts.Show("Everything saved", Theme.Success);
            }
            else if (b == 1)
            {
                if (confirm == "settings")
                {
                    rt.ResetSettings();
                    rt.ReloadFont();
                    confirm = null;
                }
                else confirm = "settings";
            }
        }
    }
}
