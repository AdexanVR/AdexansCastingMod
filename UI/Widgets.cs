using System.Collections.Generic;
using AdexansCastingMod.Config;
using AdexansCastingMod.Core;
using UnityEngine;

namespace AdexansCastingMod.UI
{
    internal static class WidgetUtil
    {
        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        }

        // Gorilla Tag's tagged/infected lava look
        public static readonly Color LavaColor = new Color(1f, 0.42f, 0.16f, 1f);

        public static float Pulse(float speed = 4f) => 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);

        public static Color StatusColor(MatchStatus s)
        {
            switch (s)
            {
                case MatchStatus.Live: return Theme.Danger;
                case MatchStatus.Paused:
                case MatchStatus.Overtime: return Theme.Warning;
                case MatchStatus.Final:
                case MatchStatus.RoundOver: return Theme.Accent;
                case MatchStatus.Intermission: return new Color(0.66f, 0.45f, 1f, 1f);
                default: return new Color(0.4f, 0.43f, 0.5f, 1f);
            }
        }

        public static string CameraModeShort(CameraMode m)
        {
            switch (m)
            {
                case CameraMode.FirstPerson: return "1ST";
                case CameraMode.Overhead: return "TOP";
                case CameraMode.Orbit: return "ORBIT";
                default: return "3RD";
            }
        }
    }

    // ---------------------------------------------------------------- Casting panel (caster only)

    internal sealed class CastingPanelWidget : HudWidget
    {
        static readonly string[] Modes = { "3RD", "1ST", "TOP", "ORBIT" };

        public CastingPanelWidget(CastingRuntime rt) : base(rt, WidgetIds.CastingPanel, "Player Casting Panel") { }

        public override bool CasterOnly => true;
        public override bool HandlesInput => true;

        public override void Draw(WidgetDraw d)
        {
            CastSettings s = rt.Settings;
            float pad = d.Pad;
            int f = d.Font();
            float y = pad;
            float inner = d.W - pad * 2f;
            float rowH = Mathf.Round(f * 2.1f);
            float gap = Mathf.Round(f * 0.4f);

            // Header + camera state
            d.Label(new Rect(pad, y, inner * 0.6f, f * 1.4f), "CASTER CONTROL", 0.78f, d.Text(0.55f), true, TextAnchor.MiddleLeft);
            string camLabel;
            Color camColor;
            if (rt.Tracker.InRoom && !rt.SpectatingAllowed)
            {
                camLabel = "PUBLIC ROOM";
                camColor = Theme.Warning;
            }
            else if (rt.Camera.IsLive)
            {
                camLabel = "CAM LIVE";
                camColor = Theme.Danger;
            }
            else
            {
                camLabel = s.spectatorCameraEnabled ? "CAM IDLE" : "CAM OFF";
                camColor = new Color(0.4f, 0.43f, 0.5f, 1f);
            }
            Painter.Pill(d.W - pad, y + f * 0.7f, camLabel, d.Font(0.7f), d.Tint(camColor), d.Tint(Color.white), alignRight: true);
            y += f * 1.6f;

            // Current target
            CastPlayer target = rt.Director.Target;
            float nameH = f * 2f;
            if (target != null)
            {
                Painter.Circle(new Vector2(pad + f * 0.4f, y + nameH * 0.5f), f * 0.4f, d.Tint(target.Color));
                Color nameColor = target.IsTagged ? d.Tint(Theme.Tagged) : d.Text();
                d.Label(new Rect(pad + f * 1.2f, y, inner - f * 1.2f, nameH), target.Name, 1.3f, nameColor, true, TextAnchor.MiddleLeft);
            }
            else
            {
                d.Label(new Rect(pad, y, inner, nameH), rt.Tracker.InRoom ? "No player selected" : "Not in a room", 1.05f, d.Text(0.5f), false, TextAnchor.MiddleLeft);
            }
            y += nameH + gap * 0.5f;

            if (rt.Tracker.InRoom && !rt.SpectatingAllowed)
            {
                d.Label(new Rect(pad, y, inner, f * 1.4f), "Spectating works in private rooms only", 0.8f, d.Tint(Theme.Warning), false, TextAnchor.MiddleLeft);
                y += f * 1.5f;
            }

            bool i = d.Interactive;
            float half = (inner - gap) * 0.5f;

            if (Controls.Button(new Rect(pad, y, half, rowH), "‹  PREV", d.Font(0.85f), enabled: i, alpha: d.Alpha))
                rt.Director.Step(-1);
            if (Controls.Button(new Rect(pad + half + gap, y, half, rowH), "NEXT  ›", d.Font(0.85f), enabled: i, alpha: d.Alpha))
                rt.Director.Step(1);
            y += rowH + gap;

            // Camera mode buttons
            float mw = (inner - gap * (Modes.Length - 1)) / Modes.Length;
            for (int m = 0; m < Modes.Length; m++)
            {
                bool active = (int)s.cameraMode == m;
                if (Controls.Button(new Rect(pad + (mw + gap) * m, y, mw, rowH), Modes[m], d.Font(0.78f), active, i, d.L.accentColor, d.Alpha))
                {
                    s.cameraMode = (CameraMode)m;
                    rt.MarkSettingsDirty();
                }
            }
            y += rowH + gap;

            if (Controls.Button(new Rect(pad, y, half, rowH), rt.AutoPilot.Enabled ? "AUTO PILOT ON" : "AUTO PILOT OFF", d.Font(0.78f), rt.AutoPilot.Enabled, i, d.L.accentColor, d.Alpha))
                rt.Execute(CastAction.ToggleAutoPilot);
            if (Controls.Button(new Rect(pad + half + gap, y, half, rowH), s.spectatorCameraEnabled ? "CAMERA ON" : "CAMERA OFF", d.Font(0.78f), s.spectatorCameraEnabled, i, d.L.accentColor, d.Alpha))
                rt.Execute(CastAction.ToggleSpectatorCamera);
            y += rowH + gap;

            string timerLabel;
            switch (rt.Timer.State)
            {
                case TimerState.Running: timerLabel = "PAUSE  " + rt.TimerText; break;
                case TimerState.Paused: timerLabel = "RESUME  " + rt.TimerText; break;
                case TimerState.Finished: timerLabel = "RESTART"; break;
                default: timerLabel = "START TIMER"; break;
            }
            float wide = inner * 0.68f;
            if (Controls.Button(new Rect(pad, y, wide, rowH), timerLabel, d.Font(0.78f), rt.Timer.State == TimerState.Running, i, d.L.accentColor, d.Alpha))
                rt.Execute(CastAction.TimerStartPause);
            if (Controls.Button(new Rect(pad + wide + gap, y, inner - wide - gap, rowH), "RESET", d.Font(0.78f), false, i, null, d.Alpha))
                rt.Execute(CastAction.TimerReset);
            y += rowH + gap;

            if (Controls.Button(new Rect(pad, y, half, rowH), "SETTINGS", d.Font(0.78f), rt.Menu.Open, i, d.L.accentColor, d.Alpha))
                rt.Execute(CastAction.ToggleSettings);
            if (Controls.Button(new Rect(pad + half + gap, y, half, rowH), "LAYOUT", d.Font(0.78f), false, i, d.L.accentColor, d.Alpha))
                rt.Execute(CastAction.ToggleLayoutEditor);
        }
    }

    // ---------------------------------------------------------------- Player list

    internal sealed class PlayerListWidget : HudWidget
    {
        public PlayerListWidget(CastingRuntime rt) : base(rt, WidgetIds.PlayerList, "Player List") { }

        public override bool HandlesInput => true;

        public override void Draw(WidgetDraw d)
        {
            if (d.L.style == 0) DrawBroadcast(d);
            else DrawPanel(d);
        }

        // Minimal broadcast look: "1 [swatch] PLAYERNAME" rows in bold italic with an outline, no panel.
        // The swatch shows the player's color, or lava orange once they are tagged.
        void DrawBroadcast(WidgetDraw d)
        {
            IReadOnlyList<CastPlayer> list = rt.Director.Ordered;
            int f = d.Font();
            float rowH = Mathf.Round(f * 1.3f);
            int maxRows = Mathf.Max(1, Mathf.FloorToInt(d.H / rowH));
            int rows = Mathf.Min(list.Count, maxRows);
            float outline = Mathf.Max(1f, f * 0.08f);
            Color outlineColor = d.Tint(new Color(0f, 0f, 0f, 0.85f));

            if (list.Count == 0)
            {
                float ey = d.L.y > 0.5f ? d.H - rowH : 0f;
                Painter.TextOutline(new Rect(0f, ey, d.W, rowH), rt.Tracker.InRoom ? "WAITING FOR PLAYERS" : "NOT IN A ROOM", f, d.Text(0.7f), outlineColor, outline, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
                return;
            }

            // Anchor to the bottom edge when the list sits in the lower half, so it grows upwards
            float y = d.L.y > 0.5f ? d.H - rows * rowH : 0f;
            CastPlayer target = rt.Director.Target;

            for (int idx = 0; idx < rows; idx++)
            {
                CastPlayer p = list[idx];
                Rect row = new Rect(0f, y, d.W, rowH);
                bool selected = p == target;
                if (d.Interactive && Controls.Hover(row)) Painter.Rect(row, d.Text(0.1f), 3f);

                // Short flash right after a tag
                if (p.IsTagged && p.SinceTagChange < 1.5f)
                {
                    float flash = (1f - p.SinceTagChange / 1.5f) * (0.5f + 0.5f * Mathf.Sin(p.SinceTagChange * 18f));
                    Painter.Rect(row, d.Tint(Theme.Tagged, 0.4f * flash), 3f);
                }

                float x = 2f;
                string number = ((idx + 1) % 10).ToString();
                float numW = f * 0.95f;
                Painter.TextOutline(new Rect(x, y, numW, rowH), number, Mathf.RoundToInt(f * 0.9f), d.Text(), outlineColor, outline, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
                x += numW;

                float swH = rowH * 0.62f;
                float swW = swH * 1.5f;
                Color sw = p.IsTagged ? WidgetUtil.LavaColor : p.Color;
                Painter.Slant(new Rect(x, y + (rowH - swH) * 0.5f, swW, swH), d.Tint(sw));
                x += swW + f * 0.3f;

                Color nameColor = selected ? d.Accent() : d.Text(p.IsTagged ? 0.85f : 1f);
                string name = Painter.Fit(p.Name.ToUpperInvariant(), f, d.W - x - 4f, true);
                Painter.TextOutline(new Rect(x, y, d.W - x, rowH), name, f, nameColor, outlineColor, outline, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);

                if (d.Interactive && GUI.Button(row, GUIContent.none, GUIStyle.none))
                    rt.Director.Select(p, SelectSource.Manual);
                y += rowH;
            }
        }

        void DrawPanel(WidgetDraw d)
        {
            float pad = d.Pad;
            int f = d.Font();
            float inner = d.W - pad * 2f;
            float y = pad;

            IReadOnlyList<CastPlayer> list = rt.Director.Ordered;
            int tagged = 0;
            foreach (CastPlayer p in list)
                if (p.IsTagged) tagged++;

            float headH = f * 1.5f;
            d.Label(new Rect(pad, y, inner * 0.5f, headH), "PLAYERS  " + list.Count, 0.8f, d.Text(0.6f), true, TextAnchor.MiddleLeft);
            if (tagged > 0)
                d.Label(new Rect(pad + inner * 0.4f, y, inner * 0.6f, headH), tagged + " TAGGED", 0.8f, d.Tint(Theme.Tagged), true, TextAnchor.MiddleRight);
            y += headH + f * 0.3f;

            if (list.Count == 0)
            {
                d.Label(new Rect(pad, y, inner, f * 2f), rt.Tracker.InRoom ? "Waiting for players…" : "Not in a room", 0.9f, d.Text(0.45f), false, TextAnchor.MiddleLeft);
                return;
            }

            float rowH = Mathf.Round(f * 1.95f);
            float rowGap = Mathf.Max(1f, Mathf.Round(f * 0.15f));
            CastPlayer target = rt.Director.Target;
            bool showScores = rt.Settings.showIndividualScores;

            for (int idx = 0; idx < list.Count; idx++)
            {
                CastPlayer p = list[idx];
                if (y + rowH > d.H - pad * 0.5f)
                {
                    d.Label(new Rect(pad, y, inner, f * 1.4f), "+" + (list.Count - idx) + " more", 0.75f, d.Text(0.45f), false, TextAnchor.MiddleLeft);
                    break;
                }

                Rect row = new Rect(pad * 0.5f, y, d.W - pad, rowH);
                bool selected = p == target;
                bool hover = d.Interactive && Controls.Hover(row);
                float radius = Mathf.Min(d.L.cornerRadius, rowH * 0.3f);

                if (selected) Painter.Rect(row, d.Accent(0.28f), radius);
                else if (hover) Painter.Rect(row, d.Text(0.07f), radius);

                // Red flash right after a tag so viewers' eyes go to the list
                if (p.IsTagged && p.SinceTagChange < 1.5f)
                {
                    float flash = (1f - p.SinceTagChange / 1.5f) * (0.5f + 0.5f * Mathf.Sin(p.SinceTagChange * 18f));
                    Painter.Rect(row, d.Tint(Theme.Tagged, 0.45f * flash), radius);
                }
                if (selected) Painter.Rect(new Rect(row.x, row.y + rowH * 0.18f, Mathf.Max(2f, f * 0.18f), rowH * 0.64f), d.Accent(), 1f);

                float x = row.x + f * 0.55f;
                float sw = f * 0.75f;
                Painter.Rect(new Rect(x, y + (rowH - sw) * 0.5f, sw, sw), d.Tint(p.Color), sw * 0.3f);
                x += sw + f * 0.5f;

                float right = row.xMax - f * 0.45f;
                if (showScores)
                {
                    string score = rt.Match.ScoreOf(p).ToString();
                    float sw2 = Painter.Width(score, d.Font(0.9f), true) + f * 0.2f;
                    d.Label(new Rect(right - sw2, y, sw2, rowH), score, 0.9f, d.Text(0.85f), true, TextAnchor.MiddleRight);
                    right -= sw2 + f * 0.45f;
                }
                if (p.IsTagged)
                {
                    float pw = Painter.Pill(right, y + rowH * 0.5f, "TAGGED", d.Font(0.62f), d.Tint(Theme.Tagged), d.Tint(Color.white), alignRight: true);
                    right -= pw + f * 0.35f;
                }

                Color nameColor = p.IsTagged ? d.Text(0.5f) : d.Text();
                string name = (d.Interactive && idx < 10 ? ((idx + 1) % 10) + "  " : "") + p.Name;
                d.Label(new Rect(x, y, right - x, rowH), name, 1f, nameColor, selected, TextAnchor.MiddleLeft);

                if (d.Interactive && GUI.Button(row, GUIContent.none, GUIStyle.none))
                    rt.Director.Select(p, SelectSource.Manual);

                y += rowH + rowGap;
            }
        }
    }

    // ---------------------------------------------------------------- Spectated player lower third

    internal sealed class SpectatorInfoWidget : HudWidget
    {
        public SpectatorInfoWidget(CastingRuntime rt) : base(rt, WidgetIds.SpectatorInfo, "Current Spectator Info") { }

        public override void Draw(WidgetDraw d)
        {
            CastPlayer target = rt.Director.Target;
            float pad = d.Pad;
            int f = d.Font();

            // Slide + fade in whenever the spectated player changes
            float t = rt.Settings.animations ? Theme.EaseOut((Time.unscaledTime - rt.TargetChangedAt) * 3f * rt.Settings.animationSpeed) : 1f;
            float a = t;
            float slide = (1f - t) * f * 2f;

            int team = rt.Match.TeamOf(target);
            MatchData m = rt.Match.Data;
            Color blockColor = team == 1 ? m.teamAColor : team == 2 ? m.teamBColor : target != null ? target.Color : d.L.accentColor;
            if (target != null && target.IsTagged) blockColor = WidgetUtil.LavaColor;

            // Rows: small label, big name, a row of info chips
            float labelH = f * 1.15f, nameH = f * 2.1f, chipH = f * 1.5f;
            float total = labelH + nameH + chipH;
            float scaleDown = Mathf.Min(1f, (d.H - pad * 0.6f) / total);
            labelH *= scaleDown; nameH *= scaleDown; chipH *= scaleDown;
            total = labelH + nameH + chipH;
            float y0 = (d.H - total) * 0.5f;

            string label = rt.AutoPilot.Enabled && rt.Director.LastSource == SelectSource.AutoPilot ? "AUTO PILOT  ·  NOW WATCHING" : "NOW WATCHING";
            string name;
            if (target != null) name = target.Name.ToUpperInvariant();
            else if (rt.Tracker.InRoom && !rt.SpectatingAllowed) name = "PRIVATE ROOM REQUIRED";
            else name = rt.Tracker.InRoom ? "WAITING FOR PLAYERS" : "NOT IN A ROOM";

            int labelSize = Mathf.RoundToInt(f * 0.62f * scaleDown);
            int nameSize = Mathf.RoundToInt(f * 1.75f * scaleDown);
            int chipSize = Mathf.RoundToInt(f * 0.66f * scaleDown);

            // Measure so the whole plate can follow the widget's alignment
            float blockW = total * 0.62f;
            float gap = f * 0.7f;
            float maxText = d.W - pad * 2f - blockW - gap;
            string fittedName = Painter.Fit(name, nameSize, maxText, true);
            float nameW = Painter.Width(fittedName, nameSize, true) + nameSize * 0.2f;

            BuildChips(target, team, m);
            float chipsW = 0f;
            for (int i = 0; i < chipCount; i++) chipsW += Painter.Width(chipText[i], chipSize, true) + chipSize * 1.1f + f * 0.35f;
            float columnW = Mathf.Min(maxText, Mathf.Max(nameW, Mathf.Max(chipsW, Painter.Width(label, labelSize, true))));
            float x = d.AlignX(blockW + gap + columnW, pad) + slide;

            // Color block with the player's list number
            Rect block = new Rect(x, y0, blockW, total);
            Painter.Slant(block, d.Tint(blockColor, a));
            int slot = SlotOf(target);
            if (slot > 0)
                Painter.TextOutline(block, slot.ToString(), Mathf.RoundToInt(total * 0.5f), d.Tint(Color.white, a), d.Tint(new Color(0f, 0f, 0f, 0.45f), a), Mathf.Max(1f, f * 0.06f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);

            float cx = x + blockW + gap;
            Painter.Text(new Rect(cx, y0, columnW, labelH), label, labelSize, d.Accent(a), TextAnchor.MiddleLeft, true, d.Shadow);
            Painter.TextOutline(new Rect(cx, y0 + labelH, columnW + nameSize, nameH), fittedName, nameSize, d.Text(a), d.Tint(new Color(0f, 0f, 0f, 0.5f), a),
                d.Shadow ? Mathf.Max(1f, nameSize * 0.04f) : 0f, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);

            float chipX = cx;
            float chipY = y0 + labelH + nameH + chipH * 0.5f;
            for (int i = 0; i < chipCount; i++)
            {
                if (chipX > cx + columnW) break;
                chipX += Painter.Pill(chipX, chipY, chipText[i], chipSize, d.Tint(chipColor[i], a * chipColor[i].a), d.Tint(Painter.OnColor(chipColor[i]), a)) + f * 0.35f;
            }
        }

        readonly string[] chipText = new string[5];
        readonly Color[] chipColor = new Color[5];
        int chipCount;

        void BuildChips(CastPlayer target, int team, MatchData m)
        {
            chipCount = 0;
            if (target == null) return;
            if (target.IsTagged) AddChip("TAGGED " + WidgetUtil.Clock(target.SinceTagChange) + " AGO", Theme.Tagged);
            else AddChip("RUNNING " + WidgetUtil.Clock(target.SurvivalTime), Theme.Success);
            if (team != 0) AddChip((team == 1 ? m.teamAName : m.teamBName).ToUpperInvariant(), team == 1 ? m.teamAColor : m.teamBColor);
            if (rt.Settings.showIndividualScores) AddChip("SCORE " + rt.Match.ScoreOf(target), new Color(1f, 1f, 1f, 0.16f));
            if (rt.Settings.showTagCounts) AddChip(rt.Match.Record(target).timesTagged + "× TAGGED", new Color(1f, 1f, 1f, 0.16f));
        }

        void AddChip(string text, Color color)
        {
            if (chipCount >= chipText.Length) return;
            chipText[chipCount] = text;
            chipColor[chipCount] = color;
            chipCount++;
        }

        int SlotOf(CastPlayer target)
        {
            if (target == null) return 0;
            IReadOnlyList<CastPlayer> list = rt.Director.Ordered;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == target) return (i + 1) % 10 == 0 ? 10 : i + 1;
            return 0;
        }
    }

    // ---------------------------------------------------------------- Auto pilot status

    internal sealed class AutoPilotStatusWidget : HudWidget
    {
        public AutoPilotStatusWidget(CastingRuntime rt) : base(rt, WidgetIds.AutoPilotStatus, "Auto Pilot Status") { }

        public override bool CasterOnly => true;
        public override bool HandlesInput => true;

        public override void Draw(WidgetDraw d)
        {
            AutoPilot ap = rt.AutoPilot;
            float pad = d.Pad;
            int f = d.Font();

            Color dot;
            string state;
            switch (ap.State)
            {
                case AutoPilotState.Off:
                    dot = new Color(0.4f, 0.43f, 0.5f, 1f);
                    state = "OFF";
                    break;
                case AutoPilotState.Override:
                    dot = Theme.Warning;
                    state = "MANUAL · RESUMES " + Mathf.CeilToInt(ap.OverrideRemaining) + "s";
                    break;
                case AutoPilotState.Switching:
                    dot = Painter.Lerp(Theme.Warning, Theme.Success, WidgetUtil.Pulse(10f));
                    state = "SWITCHING " + ap.PendingRemaining.ToString("0.0") + "s";
                    break;
                case AutoPilotState.Waiting:
                    dot = Theme.Success;
                    state = "WAITING FOR PLAYERS";
                    break;
                default:
                    dot = Theme.Success;
                    state = "ON · " + SettingsMenu.RuleLabels[(int)rt.Settings.switchRule].ToUpperInvariant();
                    break;
            }

            const string title = "AUTO PILOT";
            int titleSize = d.Font(0.95f);
            int stateSize = d.Font(0.85f);
            float dotR = f * 0.32f;
            float titleW = Painter.Width(title, titleSize, true);
            float stateW = Painter.Width(state, stateSize);
            float totalW = Mathf.Min(d.W - pad * 2f, dotR * 2f + f * 0.5f + titleW + f * 0.6f + stateW);
            float x = d.AlignX(totalW, pad);
            float cy = d.H * 0.5f;

            Painter.Circle(new Vector2(x + dotR, cy), dotR, d.Tint(dot));
            x += dotR * 2f + f * 0.5f;
            Painter.Text(new Rect(x, 0f, titleW + 2f, d.H), title, titleSize, d.Text(), TextAnchor.MiddleLeft, true, d.Shadow);
            x += titleW + f * 0.6f;
            float remaining = d.W - pad - x;
            Painter.Text(new Rect(x, 0f, remaining, d.H), Painter.Fit(state, stateSize, remaining), stateSize, d.Text(0.65f), TextAnchor.MiddleLeft, false, d.Shadow);

            if (d.Interactive && GUI.Button(new Rect(0f, 0f, d.W, d.H), GUIContent.none, GUIStyle.none))
                rt.Execute(CastAction.ToggleAutoPilot);
        }
    }

    // ---------------------------------------------------------------- Match timer

    internal sealed class TimerWidget : HudWidget
    {
        public TimerWidget(CastingRuntime rt) : base(rt, WidgetIds.MatchTimer, "Match Timer") { }

        public override void Draw(WidgetDraw d)
        {
            MatchTimer timer = rt.Timer;
            CastSettings s = rt.Settings;
            float pad = d.Pad * 0.5f;

            string label = timer.InOvertime ? "OVERTIME" : timer.State == TimerState.Paused ? "PAUSED" : timer.State == TimerState.Finished ? "TIME" : s.timerLabel;
            Color labelColor = timer.InOvertime || timer.State == TimerState.Paused ? d.Tint(Theme.Warning) : d.Text(0.6f);

            float labelH = d.H * 0.27f;
            int labelSize = Mathf.Max(6, Mathf.RoundToInt(Mathf.Min(d.Font(0.36f), labelH * 0.8f)));
            if (!string.IsNullOrEmpty(label))
                Painter.Text(new Rect(pad, pad * 0.5f, d.W - pad * 2f, labelH), Painter.Fit(label.ToUpperInvariant(), labelSize, d.W - pad * 2f, true), labelSize, labelColor, d.Anchor, true, d.Shadow);

            Color digits = d.Text();
            if (timer.IsWarning)
            {
                float pulse = s.warningFlash && s.animations ? WidgetUtil.Pulse(7f) : 1f;
                digits = d.Tint(Painter.Lerp(Theme.Tagged, Color.white, pulse * 0.35f));
            }
            else if (timer.State == TimerState.Finished)
            {
                digits = d.Tint(Theme.Tagged);
            }
            if (timer.State == TimerState.Paused && s.animations) digits.a *= 0.55f + 0.45f * WidgetUtil.Pulse(3f);

            Rect digitsRect = new Rect(pad, labelH, d.W - pad * 2f, d.H - labelH - d.H * 0.1f);
            int size = Mathf.Max(8, Mathf.RoundToInt(Mathf.Min(d.Font(), digitsRect.height * 0.95f)));
            Painter.Text(digitsRect, rt.TimerText, size, digits, d.Anchor, true, d.Shadow);

            // Thin progress bar along the bottom edge
            if (s.matchDuration > 0f && timer.State != TimerState.Stopped)
            {
                float progress = Mathf.Clamp01((float)(timer.Elapsed / s.matchDuration));
                float bh = Mathf.Max(2f, d.H * 0.045f);
                Rect track = new Rect(d.L.cornerRadius, d.H - bh - 2f, d.W - d.L.cornerRadius * 2f, bh);
                Painter.Rect(track, d.Text(0.12f), bh * 0.5f);
                Color fill = timer.IsWarning || timer.InOvertime ? Theme.Warning : d.L.accentColor;
                Painter.Rect(new Rect(track.x, track.y, track.width * progress, bh), d.Tint(fill), bh * 0.5f);
            }
        }
    }

    // ---------------------------------------------------------------- Scoreboard

    internal sealed class ScoreboardWidget : HudWidget
    {
        readonly List<PlayerRecord> teamA = new List<PlayerRecord>();
        readonly List<PlayerRecord> teamB = new List<PlayerRecord>();
        readonly List<CastPlayer> unassigned = new List<CastPlayer>();
        readonly Dictionary<string, CastPlayer> present = new Dictionary<string, CastPlayer>();

        public ScoreboardWidget(CastingRuntime rt) : base(rt, WidgetIds.Scoreboard, "Scoreboard") { }

        public override void Draw(WidgetDraw d)
        {
            switch (d.L.style)
            {
                case 0: DrawScorebug(d); break;
                case 2: DrawCenterBar(d); break;
                default: DrawPanel(d); break;
            }
        }

        // Top-center league bar:
        // |  TEAM A   score  [ 29:49 / Round 1 ]  score   TEAM B  |
        //    0-0                                           0-0
        //            [            Season 5            ]
        // Laid out in a 724 x 150 reference grid, scaled separately on each axis so any widget size works.
        void DrawCenterBar(WidgetDraw d)
        {
            MatchData m = rt.Match.Data;
            MatchTimer timer = rt.Timer;
            float u = d.W / 724f;
            float v = d.H / 150f;
            Color white = d.Text();

            // Team color bars on the outer edges
            float barW = 12f * u;
            Painter.Rect(new Rect(38f * u, 15f * v, barW, 103f * v), d.Tint(m.teamAColor), 2f * u);
            Painter.Rect(new Rect(d.W - 38f * u - barW, 15f * v, barW, 103f * v), d.Tint(m.teamBColor), 2f * u);

            // Team names and series records
            string nameA = m.teamAName ?? "", nameB = m.teamBName ?? "";
            Rect nameARect = new Rect(62f * u, 20f * v, 132f * u, 56f * v);
            Rect nameBRect = new Rect(d.W - 62f * u - 132f * u, 20f * v, 132f * u, 56f * v);
            Painter.Text(nameARect, nameA, FitSize(nameA, 46f * v, nameARect.width), white, TextAnchor.MiddleLeft, true, d.Shadow);
            Painter.Text(nameBRect, nameB, FitSize(nameB, 46f * v, nameBRect.width), white, TextAnchor.MiddleRight, true, d.Shadow);

            Rect recARect = new Rect(66f * u, 86f * v, 120f * u, 28f * v);
            Rect recBRect = new Rect(d.W - 66f * u - 120f * u, 86f * v, 120f * u, 28f * v);
            if (!string.IsNullOrEmpty(m.teamARecord))
                Painter.Text(recARect, m.teamARecord, FitSize(m.teamARecord, 20f * v, recARect.width), white, TextAnchor.MiddleLeft, true, d.Shadow);
            if (!string.IsNullOrEmpty(m.teamBRecord))
                Painter.Text(recBRect, m.teamBRecord, FitSize(m.teamBRecord, 20f * v, recBRect.width), white, TextAnchor.MiddleRight, true, d.Shadow);

            // Big scores either side of the clock
            string scoreA = m.teamAScore.ToString(), scoreB = m.teamBScore.ToString();
            Rect scoreARect = new Rect(187f * u, 14f * v, 92f * u, 108f * v);
            Rect scoreBRect = new Rect(d.W - 187f * u - 92f * u, 14f * v, 92f * u, 108f * v);
            Painter.Text(scoreARect, scoreA, FitSize(scoreA, 96f * v, scoreARect.width), white, TextAnchor.MiddleCenter, true, d.Shadow);
            Painter.Text(scoreBRect, scoreB, FitSize(scoreB, 96f * v, scoreBRect.width), white, TextAnchor.MiddleCenter, true, d.Shadow);

            // Clock box: a trapezoid that narrows towards the bottom
            Rect box = new Rect((d.W - 148f * u) * 0.5f, 12f * v, 148f * u, 108f * v);
            Color boxColor = d.L.accentColor;
            if (timer.State == TimerState.Finished) boxColor = Painter.Lerp(boxColor, Theme.Tagged, 0.6f);
            Painter.Trapezoid(box, d.Tint(boxColor));
            Painter.Rect(new Rect(box.x, box.y, box.width, Mathf.Max(1f, 2f * v)), d.Tint(Color.white, 0.12f));

            Color clockColor = white;
            if (timer.IsWarning)
            {
                float pulse = rt.Settings.warningFlash && rt.Settings.animations ? WidgetUtil.Pulse(7f) : 1f;
                clockColor = d.Tint(Painter.Lerp(Theme.Tagged, Color.white, pulse * 0.35f));
            }
            if (timer.State == TimerState.Paused && rt.Settings.animations) clockColor.a *= 0.55f + 0.45f * WidgetUtil.Pulse(3f);

            Rect clockRect = new Rect(box.x, box.y + 6f * v, box.width, 58f * v);
            Painter.Text(clockRect, rt.TimerText, FitSize(rt.TimerText, 48f * v, box.width * 0.9f), clockColor, TextAnchor.MiddleCenter, true, d.Shadow);

            string roundText = timer.InOvertime ? "Overtime" : "Round " + m.round;
            Rect roundRect = new Rect(box.x, box.y + 62f * v, box.width, 36f * v);
            Color roundColor = timer.InOvertime ? d.Tint(Theme.Warning) : white;
            Painter.Text(roundRect, roundText, FitSize(roundText, 24f * v, box.width * 0.85f), roundColor, TextAnchor.MiddleCenter, true, d.Shadow);

            // Subtitle strip (match title, e.g. "Season 5")
            if (!string.IsNullOrEmpty(m.matchTitle))
            {
                Rect strip = new Rect(145f * u, 127f * v, d.W - 290f * u, 20f * v);
                Painter.Rect(strip, d.Tint(new Color(0.3f, 0.3f, 0.32f, 0.88f)), 2f * v);
                Painter.Text(strip, m.matchTitle, FitSize(m.matchTitle, 15f * v, strip.width * 0.95f), white, TextAnchor.MiddleCenter, true, d.Shadow);
            }
        }

        // [score A][TEAM A][  CLOCK  ][TEAM B][score B] with a smaller box under the clock.
        // Proportions follow the league broadcast reference (455 px wide at 1080p).
        void DrawScorebug(WidgetDraw d)
        {
            MatchData m = rt.Match.Data;
            MatchTimer timer = rt.Timer;
            float u = d.W / 453f;
            float barH = Mathf.Round(d.H * 0.72f);
            float radius = d.L.cornerRadius;

            float scoreW = 55f * u, nameW = 90f * u, clockW = 145f * u, small = 3f * u, big = 6f * u;
            float x = 0f;
            Rect scoreA = new Rect(x, 0f, scoreW, barH); x += scoreW + small;
            Rect nameA = new Rect(x, 0f, nameW, barH); x += nameW + big;
            Rect clock = new Rect(x, 0f, clockW, barH); x += clockW + big;
            Rect nameB = new Rect(x, 0f, nameW, barH); x += nameW + small;
            Rect scoreB = new Rect(x, 0f, scoreW, barH);

            DrawScoreBox(d, scoreA, m.teamAScore, m.teamAColor, radius);
            DrawNameBox(d, nameA, m.teamAName, m.teamAColor, radius, true);
            DrawNameBox(d, nameB, m.teamBName, m.teamBColor, radius, false);
            DrawScoreBox(d, scoreB, m.teamBScore, m.teamBColor, radius);

            // Clock
            Color clockBg = d.L.accentColor;
            if (timer.IsWarning && rt.Settings.warningFlash && rt.Settings.animations)
                clockBg = Painter.Lerp(clockBg, Theme.Tagged, WidgetUtil.Pulse(7f));
            else if (timer.State == TimerState.Finished)
                clockBg = Theme.Tagged;
            Painter.Rect(clock, d.Tint(clockBg), radius);
            Color clockText = d.Text();
            if (timer.State == TimerState.Paused && rt.Settings.animations) clockText.a *= 0.55f + 0.45f * WidgetUtil.Pulse(3f);
            int clockSize = FitSize(rt.TimerText, barH * 0.78f, clock.width * 0.92f);
            Painter.TextOutline(clock, rt.TimerText, clockSize, clockText, d.Tint(Dark(clockBg, 0.45f)), Mathf.Max(1f, clockSize * 0.06f), TextAnchor.MiddleCenter);

            // Sub box under the clock
            string sub = SubText(timer);
            if (sub == null || d.H - barH < 6f) return;
            float subW = clockW * 0.72f;
            Rect subRect = new Rect(clock.center.x - subW * 0.5f, barH, subW, d.H - barH);
            Painter.Rect(subRect, d.Tint(clockBg), radius);
            int subSize = FitSize(sub, subRect.height * 0.8f, subRect.width * 0.9f);
            Painter.TextOutline(subRect, sub, subSize, d.Text(), d.Tint(Dark(clockBg, 0.45f)), Mathf.Max(1f, subSize * 0.06f), TextAnchor.MiddleCenter);
        }

        string SubText(MatchTimer timer)
        {
            if (timer.InOvertime) return "OVERTIME";
            switch (rt.Settings.scorebugSubTimer)
            {
                case SubTimerMode.SpectatedSurvival:
                    CastPlayer t = rt.Director.Target;
                    if (t == null) return "--:--";
                    return t.IsTagged ? "TAGGED" : ClockMMSS(t.SurvivalTime);
                case SubTimerMode.SinceLastTag:
                    return rt.Tracker.LastTagTime < 0f ? "--:--" : ClockMMSS(Time.unscaledTime - rt.Tracker.LastTagTime);
                case SubTimerMode.Round:
                    return "ROUND " + rt.Match.Data.round + "/" + rt.Match.Data.totalRounds;
                default:
                    return null;
            }
        }

        static string ClockMMSS(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }

        static Color Dark(Color c, float amount) => new Color(c.r * (1f - amount), c.g * (1f - amount), c.b * (1f - amount), 1f);

        // Largest font size (capped by height) at which the text still fits the width
        static int FitSize(string text, float maxHeight, float maxWidth)
        {
            int size = Mathf.Max(6, Mathf.RoundToInt(maxHeight));
            float w = Painter.Width(text, size, true);
            if (w > maxWidth && w > 0f) size = Mathf.Max(6, Mathf.FloorToInt(size * maxWidth / w));
            return size;
        }

        void DrawScoreBox(WidgetDraw d, Rect r, int score, Color team, float radius)
        {
            Painter.Rect(r, d.Tint(Painter.Lerp(team, Color.white, 0.45f), d.L.backgroundColor.a), radius);
            string text = score.ToString();
            int size = FitSize(text, r.height * 0.9f, r.width * 0.9f);
            Painter.TextOutline(r, text, size, d.Text(), d.Tint(Dark(team, 0.15f)), Mathf.Max(1.5f, size * 0.07f), TextAnchor.MiddleCenter);
        }

        void DrawNameBox(WidgetDraw d, Rect r, string name, Color team, float radius, bool leftTeam)
        {
            Painter.Rect(r, d.Tint(d.L.backgroundColor), radius);
            // Team-colored slanted strip along the bottom, on the side facing the clock
            float stripH = Mathf.Max(3f, r.height * 0.2f);
            float stripW = r.width * 0.78f;
            Painter.Slant(new Rect(leftTeam ? r.xMax - stripW : r.x, r.yMax - stripH, stripW, stripH), d.Tint(team));

            string text = string.IsNullOrEmpty(name) ? "" : name.ToUpperInvariant();
            Rect textRect = new Rect(r.x, r.y, r.width, r.height - stripH * 0.6f);
            int size = FitSize(text, textRect.height * 0.62f, r.width * 0.88f);
            Painter.TextOutline(textRect, text, size, d.Text(), d.Tint(Dark(team, 0.35f)), Mathf.Max(1f, size * 0.07f), TextAnchor.MiddleCenter);
        }

        void DrawPanel(WidgetDraw d)
        {
            MatchData m = rt.Match.Data;
            CastSettings s = rt.Settings;
            float pad = d.Pad;
            int f = d.Font();
            float inner = d.W - pad * 2f;
            float y = pad * 0.8f;

            if (!string.IsNullOrEmpty(m.matchTitle))
            {
                d.Label(new Rect(pad, y, inner, f * 1.3f), m.matchTitle.ToUpperInvariant(), 0.72f, d.Text(0.55f), true, TextAnchor.MiddleCenter);
                y += f * 1.45f;
            }

            // Team header: [bar NAME  score] [score  NAME bar]
            float headH = f * 2.6f;
            float half = (inner - f * 0.6f) * 0.5f;
            DrawTeamHeader(d, new Rect(pad, y, half, headH), m.teamAName, m.teamAScore, m.teamAColor, false);
            DrawTeamHeader(d, new Rect(pad + half + f * 0.6f, y, half, headH), m.teamBName, m.teamBScore, m.teamBColor, true);
            y += headH + f * 0.4f;

            if (!s.showIndividualScores && !s.showUnassignedPlayers) return;

            CollectRows();
            float rowH = f * 1.55f;

            if (s.showIndividualScores && (teamA.Count > 0 || teamB.Count > 0))
            {
                int rows = Mathf.Min(s.maxScoreboardRows, Mathf.Max(teamA.Count, teamB.Count));
                for (int i = 0; i < rows; i++)
                {
                    if (y + rowH > d.H - pad * 0.4f) break;
                    if (i < teamA.Count) DrawPlayerRow(d, new Rect(pad, y, half, rowH), teamA[i], false);
                    if (i < teamB.Count) DrawPlayerRow(d, new Rect(pad + half + f * 0.6f, y, half, rowH), teamB[i], true);
                    y += rowH;
                }
            }

            if (s.showUnassignedPlayers && unassigned.Count > 0 && y + rowH <= d.H - pad * 0.4f)
            {
                y += f * 0.2f;
                Painter.Rect(new Rect(pad, y, inner, 1f), d.Text(0.1f));
                y += f * 0.3f;
                for (int i = 0; i < unassigned.Count; i++)
                {
                    if (y + rowH > d.H - pad * 0.4f) break;
                    CastPlayer p = unassigned[i];
                    Painter.Rect(new Rect(pad, y + rowH * 0.3f, f * 0.4f, rowH * 0.4f), d.Tint(p.Color), 2f);
                    Color c = p.IsTagged ? d.Tint(Theme.Tagged) : d.Text(0.85f);
                    d.Label(new Rect(pad + f * 0.8f, y, inner * 0.7f, rowH), p.Name, 0.85f, c, false, TextAnchor.MiddleLeft);
                    if (s.showIndividualScores)
                        d.Label(new Rect(pad, y, inner, rowH), rt.Match.ScoreOf(p).ToString(), 0.85f, d.Text(0.85f), true, TextAnchor.MiddleRight);
                    y += rowH;
                }
            }
        }

        void CollectRows()
        {
            teamA.Clear();
            teamB.Clear();
            unassigned.Clear();
            present.Clear();
            IReadOnlyList<CastPlayer> ordered = rt.Director.Ordered;
            foreach (CastPlayer p in ordered) present[p.RecordKey] = p;

            foreach (PlayerRecord r in rt.Match.Data.players)
            {
                if (r.team == 1) teamA.Add(r);
                else if (r.team == 2) teamB.Add(r);
            }
            foreach (CastPlayer p in ordered)
                if (rt.Match.TeamOf(p) == 0) unassigned.Add(p);

            // Present players first, then by score
            System.Comparison<PlayerRecord> cmp = (a, b) =>
            {
                bool pa = present.ContainsKey(a.key), pb = present.ContainsKey(b.key);
                if (pa != pb) return pa ? -1 : 1;
                return b.score.CompareTo(a.score);
            };
            teamA.Sort(cmp);
            teamB.Sort(cmp);
        }

        void DrawTeamHeader(WidgetDraw d, Rect r, string name, int score, Color color, bool mirrored)
        {
            int f = d.Font();
            Painter.Rect(r, d.Tint(color, 0.16f), Mathf.Min(d.L.cornerRadius, r.height * 0.3f));
            float barW = Mathf.Max(3f, f * 0.25f);
            Painter.Rect(mirrored ? new Rect(r.xMax - barW, r.y, barW, r.height) : new Rect(r.x, r.y, barW, r.height), d.Tint(color), Mathf.Min(d.L.cornerRadius, barW));

            string scoreText = score.ToString();
            int scoreSize = d.Font(1.65f);
            float scoreW = Painter.Width(scoreText, scoreSize, true) + f * 0.8f;
            Rect scoreRect = mirrored ? new Rect(r.x + f * 0.2f, r.y, scoreW, r.height) : new Rect(r.xMax - scoreW - f * 0.2f, r.y, scoreW, r.height);
            Painter.Text(scoreRect, scoreText, scoreSize, d.Text(), TextAnchor.MiddleCenter, true, d.Shadow);

            float nameX = mirrored ? scoreRect.xMax : r.x + barW + f * 0.5f;
            float nameW = r.width - scoreW - barW - f * 0.9f;
            d.Label(new Rect(nameX, r.y, nameW, r.height), name.ToUpperInvariant(), 0.95f, d.Text(), true, mirrored ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
        }

        void DrawPlayerRow(WidgetDraw d, Rect r, PlayerRecord rec, bool mirrored)
        {
            int f = d.Font();
            present.TryGetValue(rec.key, out CastPlayer p);
            bool tagged = p != null && p.IsTagged;
            float a = p != null ? 1f : 0.4f;
            bool live = p != null && p == rt.Director.Target;

            string score = rt.Settings.showIndividualScores ? rec.score.ToString() : "";
            float scoreW = Painter.Width(score, d.Font(0.85f), true) + f * 0.4f;
            float dot = f * 0.32f;

            float x = mirrored ? r.xMax - f * 0.3f : r.x + f * 0.3f;
            Color dotColor = tagged ? Theme.Tagged : live ? d.L.accentColor : new Color(1f, 1f, 1f, 0.25f);
            Painter.Circle(new Vector2(mirrored ? x - dot : x + dot, r.center.y), dot, d.Tint(dotColor, a));

            float nameStart = mirrored ? r.x + scoreW : x + dot * 2f + f * 0.4f;
            float nameEnd = mirrored ? x - dot * 2f - f * 0.4f : r.xMax - scoreW;
            string name = rec.name + (p == null ? " (DC)" : "");
            Color nameColor = tagged ? d.Text(0.5f) : d.Text(0.9f * a);
            d.Label(new Rect(nameStart, r.y, nameEnd - nameStart, r.height), name, 0.85f, nameColor, live, mirrored ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
            if (score.Length > 0)
                d.Label(mirrored ? new Rect(r.x, r.y, scoreW, r.height) : new Rect(r.xMax - scoreW, r.y, scoreW, r.height), score, 0.85f, d.Text(0.8f * a), true,
                    mirrored ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
        }
    }

    // ---------------------------------------------------------------- Round info

    internal sealed class RoundInfoWidget : HudWidget
    {
        public RoundInfoWidget(CastingRuntime rt) : base(rt, WidgetIds.RoundInfo, "Round Information") { }

        public override void Draw(WidgetDraw d)
        {
            MatchData m = rt.Match.Data;
            string text = "ROUND " + m.round + " OF " + m.totalRounds;
            string mode = rt.Tracker.GameModeName;
            if (!string.IsNullOrEmpty(mode)) text += "  ·  " + mode.ToUpperInvariant();
            d.Label(new Rect(d.Pad * 0.6f, 0f, d.W - d.Pad * 1.2f, d.H), text, 1f, d.Text(), true);
        }
    }

    // ---------------------------------------------------------------- Match status

    internal sealed class MatchStatusWidget : HudWidget
    {
        public MatchStatusWidget(CastingRuntime rt) : base(rt, WidgetIds.MatchStatus, "Match Status") { }

        public override void Draw(WidgetDraw d)
        {
            MatchStatus status = rt.Match.EffectiveStatus;
            string label = rt.Match.StatusLabel(status);
            Color color = WidgetUtil.StatusColor(status);
            float pad = d.Pad;
            int f = d.Font();
            bool twoLines = d.H >= f * 3f;

            string sub;
            if (!rt.Tracker.InRoom) sub = "NOT IN A ROOM";
            else sub = rt.Director.Ordered.Count + " PLAYERS  ·  " + rt.Tracker.TaggedCount + " TAGGED";

            int pillSize = d.Font(0.85f);
            float dotR = status == MatchStatus.Live ? pillSize * 0.28f : 0f;
            string pillText = label;
            float pillW = Painter.Width(pillText, pillSize, true) + pillSize * 1.1f + (dotR > 0f ? dotR * 2f + pillSize * 0.4f : 0f);
            float pillH = pillSize * 1.6f;

            float subW = twoLines ? 0f : Painter.Width(sub, d.Font(0.75f)) + f * 0.6f;
            float x = d.AlignX(pillW + subW, pad);
            float cy = twoLines ? pad * 0.5f + pillH * 0.5f + 2f : d.H * 0.5f;

            Rect pill = new Rect(x, cy - pillH * 0.5f, pillW, pillH);
            Painter.Rect(pill, d.Tint(color), pillH * 0.5f);
            float tx = pill.x + pillSize * 0.55f;
            if (dotR > 0f)
            {
                float pulse = rt.Settings.animations ? 0.4f + 0.6f * WidgetUtil.Pulse(5f) : 1f;
                Painter.Circle(new Vector2(tx + dotR, cy), dotR, d.Tint(Color.white, pulse));
                tx += dotR * 2f + pillSize * 0.4f;
            }
            Painter.Text(new Rect(tx, pill.y, pill.xMax - tx, pillH), pillText, pillSize, d.Tint(Painter.OnColor(color)), TextAnchor.MiddleLeft, true);

            if (twoLines)
                d.Label(new Rect(pad, pill.yMax + f * 0.2f, d.W - pad * 2f, Mathf.Max(f * 1.3f, d.H - pill.yMax - pad * 0.3f)), sub, 0.75f, d.Text(0.65f));
            else
                Painter.Text(new Rect(pill.xMax + f * 0.6f, 0f, d.W - pill.xMax - pad, d.H), Painter.Fit(sub, d.Font(0.75f), d.W - pill.xMax - pad - f * 0.6f), d.Font(0.75f), d.Text(0.65f), TextAnchor.MiddleLeft, false, d.Shadow);
        }
    }

    // ---------------------------------------------------------------- Minimap

    internal sealed class MinimapWidget : HudWidget
    {
        public MinimapWidget(CastingRuntime rt) : base(rt, WidgetIds.Minimap, "Minimap") { }

        public override void Draw(WidgetDraw d)
        {
            float side = Mathf.Min(d.W, d.H);
            Rect map = new Rect((d.W - side) * 0.5f, (d.H - side) * 0.5f, side, side);
            bool circle = d.L.style == 1;
            float radius = circle ? side * 0.5f : d.L.style == 2 ? 0f : d.L.cornerRadius;
            float border = Mathf.Max(1.5f, side * 0.012f);

            Texture tex = rt.Minimap.Texture;
            Camera cam = rt.Minimap.Cam;
            if (tex == null || cam == null)
            {
                Painter.Rect(map, d.Tint(new Color(0.07f, 0.08f, 0.1f, 0.85f)), radius);
                Painter.Border(map, d.Accent(0.6f), border, radius);
                string msg = rt.Tracker.InRoom && !rt.SpectatingAllowed ? "PRIVATE ROOM ONLY" : "MINIMAP";
                d.Label(map, msg, 0.9f, d.Text(0.5f), true, TextAnchor.MiddleCenter);
                return;
            }

            Painter.Image(map, tex, d.Tint(Color.white), radius);
            Painter.Border(map, d.Accent(0.85f), border, radius);

            // North marker when the map doesn't rotate, and the watched player's name along the bottom
            int labelSize = Mathf.Max(8, Mathf.RoundToInt(side * 0.06f));
            if (!rt.Settings.minimapRotate)
                Painter.TextOutline(new Rect(map.x, map.y + border + 2f, map.width, labelSize * 1.4f), "N", labelSize, d.Text(), d.Tint(new Color(0f, 0f, 0f, 0.8f)), 1f, TextAnchor.MiddleCenter);
            CastPlayer watched = rt.Director.Target;
            if (watched != null)
            {
                string label = Painter.Fit(watched.Name.ToUpperInvariant(), labelSize, side * 0.8f, true);
                float lw = Painter.Width(label, labelSize, true) + labelSize * 1.4f;
                Rect lr = new Rect(map.center.x - lw * 0.5f, map.yMax - labelSize * 2.3f - border, lw, labelSize * 1.65f);
                Painter.Rect(lr, d.Tint(new Color(0.03f, 0.035f, 0.045f, 0.8f)), lr.height * 0.5f);
                Painter.Text(lr, label, labelSize, d.Text(), TextAnchor.MiddleCenter, true);
            }
            if (!rt.Settings.minimapMarkers) return;

            CastPlayer target = rt.Director.Target;
            float dotR = Mathf.Max(3f, side * 0.022f);
            Vector2 center = map.center;
            float limit = side * 0.5f - dotR - border;

            IReadOnlyList<CastPlayer> players = rt.Tracker.All;
            for (int i = 0; i < players.Count; i++)
            {
                CastPlayer p = players[i];
                if (!p.HasRig || p == target || (p.IsLocal && !rt.Settings.includeLocalPlayer)) continue;
                Vector2 pt = Clamp(ToMap(cam, p.Position, map), center, limit, circle);
                Painter.Circle(pt, dotR + 1.5f, d.Tint(new Color(0f, 0f, 0f, 0.7f)));
                Painter.Circle(pt, dotR, d.Tint(p.IsTagged ? WidgetUtil.LavaColor : p.Color));
            }

            if (target != null && target.HasRig)
            {
                Vector2 pt = Clamp(ToMap(cam, target.Position, map), center, limit, circle);
                float heading = 0f;
                Transform head = target.Head;
                if (head != null)
                {
                    Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                    if (fwd.sqrMagnitude > 1e-4f) heading = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg - rt.Minimap.MapYaw;
                }
                float size = dotR * 3.4f;
                Matrix4x4 old = GUI.matrix;
                GUIUtility.RotateAroundPivot(heading, pt);
                Painter.Triangle(new Rect(pt.x - size * 0.5f - 1.5f, pt.y - size * 0.5f - 2f, size + 3f, size + 3f), d.Tint(new Color(0f, 0f, 0f, 0.75f)));
                Painter.Triangle(new Rect(pt.x - size * 0.5f, pt.y - size * 0.5f, size, size), d.Tint(target.IsTagged ? WidgetUtil.LavaColor : Color.white));
                GUI.matrix = old;
            }
        }

        static Vector2 ToMap(Camera cam, Vector3 world, Rect map)
        {
            Vector3 vp = cam.WorldToViewportPoint(world);
            return new Vector2(map.x + vp.x * map.width, map.y + (1f - vp.y) * map.height);
        }

        // Off-map players stick to the edge so the caster still sees which direction they are
        static Vector2 Clamp(Vector2 pt, Vector2 center, float limit, bool circle)
        {
            Vector2 off = pt - center;
            if (circle)
            {
                if (off.magnitude > limit) off = off.normalized * limit;
            }
            else
            {
                off.x = Mathf.Clamp(off.x, -limit, limit);
                off.y = Mathf.Clamp(off.y, -limit, limit);
            }
            return center + off;
        }
    }
}
