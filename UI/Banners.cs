using System.Collections.Generic;
using AdexansCastingMod.Config;
using AdexansCastingMod.Core;
using UnityEngine;

namespace AdexansCastingMod.UI
{
    // Full-width animated banners for big match moments (round start/end, overtime, last survivor)
    // and the quick "stinger" played when the camera cuts to another player.
    internal sealed class Banners
    {
        const float SwitchDuration = 0.45f;

        readonly CastingRuntime rt;
        string title;
        string subtitle;
        Color color;
        float shownAt = -100f;
        float switchAt = -100f;
        TimerState lastTimerState = TimerState.Stopped;
        bool lastSurvivorShown;

        public Banners(CastingRuntime rt)
        {
            this.rt = rt;
            rt.Timer.StateChanged += OnTimerState;
            rt.Timer.OvertimeStarted += () => RoundEvent("OVERTIME", OvertimeSubtitle(), Theme.Warning);
            rt.Tracker.PlayerTagged += _ => CheckLastSurvivor();
            rt.Tracker.PlayerUntagged += _ => CheckLastSurvivor();
            rt.Tracker.RoomChanged += () => lastSurvivorShown = false;
        }

        public void Show(string bannerTitle, string bannerSubtitle, Color bannerColor)
        {
            if (!rt.Settings.showBanners) return;
            title = bannerTitle;
            subtitle = bannerSubtitle;
            color = bannerColor;
            shownAt = Time.unscaledTime;
        }

        public void PlaySwitchEffect() => switchAt = Time.unscaledTime;

        void RoundEvent(string t, string sub, Color c)
        {
            if (rt.Settings.bannerRoundEvents) Show(t, sub, c);
        }

        void OnTimerState(TimerState state)
        {
            MatchData m = rt.Match.Data;
            if (state == TimerState.Running && (lastTimerState == TimerState.Stopped || lastTimerState == TimerState.Finished))
            {
                lastSurvivorShown = false;
                RoundEvent("ROUND " + m.round, "GO!", Theme.Accent);
            }
            else if (state == TimerState.Finished)
            {
                bool final = m.round >= m.totalRounds;
                RoundEvent(final ? "FINAL" : "ROUND " + m.round + " OVER", ScoreLine(m), final ? Theme.Warning : Theme.Accent);
            }
            lastTimerState = state;
        }

        static string ScoreLine(MatchData m) => m.teamAName + "   " + m.teamAScore + " – " + m.teamBScore + "   " + m.teamBName;

        string OvertimeSubtitle()
        {
            float ot = rt.Settings.overtimeDuration;
            return ot > 0f ? "+" + WidgetUtil.Clock(ot) : "UNTIL THE LAST TAG";
        }

        void CheckLastSurvivor()
        {
            if (!rt.Settings.bannerLastSurvivor) return;
            IReadOnlyList<CastPlayer> list = rt.Director.Ordered;
            int total = 0, untagged = 0;
            CastPlayer survivor = null;
            foreach (CastPlayer p in list)
            {
                if (!p.HasRig) continue;
                total++;
                if (!p.IsTagged)
                {
                    untagged++;
                    survivor = p;
                }
            }

            if (untagged > 1) lastSurvivorShown = false;
            if (untagged == 1 && total >= 3 && !lastSurvivorShown)
            {
                lastSurvivorShown = true;
                Show("LAST ONE STANDING", survivor.Name.ToUpperInvariant(), Theme.Success);
            }
        }

        // Drawn over the game image but under the HUD
        public void DrawSwitchEffect()
        {
            if (Event.current.type != EventType.Repaint || !rt.Camera.IsLive) return;
            float t = Time.unscaledTime - switchAt;
            if (t < 0f || t > SwitchDuration) return;
            float w = Screen.width, h = Screen.height;

            switch (rt.Settings.switchEffect)
            {
                case SwitchEffect.Fade:
                    // Dip from black
                    Painter.Rect(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, 1f - t / SwitchDuration));
                    break;
                case SwitchEffect.Wipe:
                    // Two slanted bands sweep across: accent first, a dark one right behind it
                    float p = Theme.EaseOut(t / SwitchDuration);
                    float band = w * 0.55f;
                    float x = Mathf.Lerp(-band * 1.4f, w + band * 0.2f, p);
                    Painter.Slant(new Rect(x - band * 0.45f, -h * 0.05f, band, h * 1.1f), new Color(0.03f, 0.035f, 0.045f, 0.92f));
                    Painter.Slant(new Rect(x, -h * 0.05f, band * 0.35f, h * 1.1f), Painter.Alpha(rt.Settings.accentColor, 0.95f));
                    break;
            }
        }

        // Drawn on top of the HUD
        public void DrawBanner()
        {
            if (Event.current.type != EventType.Repaint || title == null || !rt.Settings.overlayVisible) return;
            float duration = rt.Settings.bannerDuration;
            float t = Time.unscaledTime - shownAt;
            if (t < 0f || t > duration) return;

            float speed = rt.Settings.animations ? 1f : 1000f;
            float open = Theme.EaseOut(t / 0.35f * speed);
            float close = Theme.EaseOut((duration - t) / 0.35f * speed);
            float k = Mathf.Min(open, close);
            if (k <= 0.001f) return;

            float scale = rt.HudScale;
            float bandH = 128f * scale;
            float cy = Screen.height * 0.36f;
            float bandW = Screen.width * k;
            Rect band = new Rect((Screen.width - bandW) * 0.5f, cy - bandH * 0.5f, bandW, bandH);

            Painter.Rect(band, new Color(0.03f, 0.035f, 0.045f, 0.86f * k));
            Painter.Rect(new Rect(band.x, band.y, band.width, 3f * scale), Painter.Alpha(color, k));
            Painter.Rect(new Rect(band.x, band.yMax - 3f * scale, band.width, 3f * scale), Painter.Alpha(color, k));

            // Colored slanted caps on both ends of the band
            float capW = bandH * 0.9f;
            Painter.Slant(new Rect(band.x - capW * 0.25f, band.y, capW, bandH), Painter.Alpha(color, 0.9f * k));
            Painter.Slant(new Rect(band.xMax - capW * 0.75f, band.y, capW, bandH), Painter.Alpha(color, 0.9f * k));

            // Title slides in from the left, subtitle from the right
            float slide = (1f - open) * 90f * scale;
            int titleSize = Mathf.RoundToInt(64f * scale);
            int subSize = Mathf.RoundToInt(22f * scale);
            Color white = new Color(1f, 1f, 1f, k);
            Painter.TextOutline(new Rect(slide, band.y + bandH * 0.06f, Screen.width, bandH * 0.62f), title, titleSize, white,
                new Color(0f, 0f, 0f, 0.6f * k), Mathf.Max(1f, titleSize * 0.04f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            if (!string.IsNullOrEmpty(subtitle))
                Painter.Text(new Rect(-slide, band.y + bandH * 0.62f, Screen.width, bandH * 0.3f), subtitle, subSize, Painter.Alpha(Painter.Lerp(color, Color.white, 0.55f), k), TextAnchor.MiddleCenter, true, true);
        }
    }
}
