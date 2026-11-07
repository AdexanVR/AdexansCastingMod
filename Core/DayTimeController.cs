using System;
using System.Reflection;
using AdexansCastingMod.Config;
using UnityEngine;

namespace AdexansCastingMod.Core
{
    // Fixes the time of day on this PC only, through the game's own BetterDayNightManager.
    // The game splits a day into 10 segments and blends lighting between their lightmaps
    // (night, sunrise, sunrise, 10am, noon, 3pm, sunset, sunset, night, night). This maps a normal
    // 24-hour clock onto those segments so "18:30" means the same look on every map.
    // Nothing is sent over the network (SetTimeOfDay is the local, non-RPC call).
    internal sealed class DayTimeController
    {
        public static readonly (string name, float hour)[] Presets =
        {
            ("Sunrise", 6f), ("Morning", 9f), ("Noon", 12f), ("Sunset", 18f), ("Night", 21f), ("Midnight", 0f),
        };

        // Clock hour at which each of the 10 segments starts (its lightmap shows unblended there).
        // Segment 0 starts at 04:00, the last one ends back at 04:00 the next day (28:00).
        static readonly float[] SegmentStart = { 4f, 6f, 7.5f, 10f, 12f, 15f, 18f, 19.5f, 21f, 24f, 28f };

        static FieldInfo currentTimeField;
        static bool reflectionFailed;

        readonly CastSettings settings;
        float displayHour = -1f;   // animated toward settings.timeOfDayHour
        float lastApplied = -1f;
        float nextCheck;
        bool applied;

        public string Status { get; private set; } = "";
        public bool Available => BetterDayNightManager.instance != null;

        public DayTimeController(CastSettings settings)
        {
            this.settings = settings;
        }

        // The hour currently shown (mid-animation this is between the old and new time)
        public float DisplayHour => displayHour < 0f ? settings.timeOfDayHour : displayHour;

        public void Tick()
        {
            BetterDayNightManager m = BetterDayNightManager.instance;
            if (m == null)
            {
                Status = "Waiting for the game to load";
                applied = false;
                return;
            }

            if (!settings.timeOverride)
            {
                if (applied)
                {
                    // Hand control back to the game's normal day cycle
                    m.ClearTimeOfDay(true);
                    applied = false;
                    lastApplied = -1f;
                }
                displayHour = -1f;
                Status = "Following the game's normal cycle";
                return;
            }

            float target = Mathf.Repeat(settings.timeOfDayHour, 24f);
            if (displayHour < 0f || !settings.animations) displayHour = target;
            else
            {
                // Glide along the shorter way around the clock
                float delta = Mathf.DeltaAngle(displayHour * 15f, target * 15f) / 15f;
                float step = Time.unscaledDeltaTime * 8f;
                displayHour = Mathf.Abs(delta) <= step ? target : Mathf.Repeat(displayHour + Mathf.Sign(delta) * step, 24f);
            }

            float now = Time.unscaledTime;
            bool changed = Mathf.Abs(displayHour - lastApplied) > 0.0005f;
            // Re-check once a second in case the game (or another mod) changed the time back
            bool recheck = now >= nextCheck;
            if (!changed && !recheck && applied) return;
            nextCheck = now + 1f;

            if (!changed && applied && IsStillApplied(m)) return;
            Apply(m, displayHour);
        }

        void Apply(BetterDayNightManager m, float hour)
        {
            try
            {
                double[] ranges = m.timeOfDayRange;
                if (ranges == null || ranges.Length == 0)
                {
                    Status = "This map has no day cycle";
                    return;
                }

                ToSegment(hour, ranges.Length, out int index, out float blend);
                double seconds = 0.0;
                for (int i = 0; i < index; i++) seconds += ranges[i] * 3600.0;
                seconds += ranges[index] * 3600.0 * Mathf.Clamp(blend, 0f, 0.999f);

                // SetTimeOfDay switches the game to a fixed time at the segment start...
                m.SetTimeOfDay(index);
                // ...then the exact position inside the segment is set and the lighting refreshed
                if (SetCurrentTime(m, seconds)) m.UpdateTimeOfDay(true);
                else m.SetTimeOfDay(index, true);

                applied = true;
                lastApplied = hour;
                Status = "Fixed at " + Format(hour) + " (" + SafeName(m, index) + ")";
            }
            catch (Exception e)
            {
                Status = "Could not change the time: " + e.Message;
                Plugin.Log.LogWarning("Day/time: " + e.Message);
            }
        }

        bool IsStillApplied(BetterDayNightManager m)
        {
            if (m.currentSetting != TimeSettings.Static) return false;
            if (currentTimeField == null) return true;
            try
            {
                double[] ranges = m.timeOfDayRange;
                ToSegment(lastApplied, ranges.Length, out int index, out float blend);
                double expected = 0.0;
                for (int i = 0; i < index; i++) expected += ranges[i] * 3600.0;
                expected += ranges[index] * 3600.0 * Mathf.Clamp(blend, 0f, 0.999f);
                double actual = (double)currentTimeField.GetValue(m);
                return Math.Abs(actual - expected) < 1.0;
            }
            catch
            {
                return true;
            }
        }

        static bool SetCurrentTime(BetterDayNightManager m, double seconds)
        {
            if (reflectionFailed) return false;
            try
            {
                if (currentTimeField == null)
                    currentTimeField = typeof(BetterDayNightManager).GetField("currentTime", BindingFlags.Instance | BindingFlags.NonPublic);
                if (currentTimeField == null || currentTimeField.FieldType != typeof(double))
                {
                    reflectionFailed = true;
                    currentTimeField = null;
                    return false;
                }
                currentTimeField.SetValue(m, seconds);
                return true;
            }
            catch
            {
                // A game update renamed the field: fall back to whole segments
                reflectionFailed = true;
                return false;
            }
        }

        // 24h clock -> segment index + blend toward the next segment
        static void ToSegment(float hour, int segmentCount, out int index, out float blend)
        {
            if (segmentCount != SegmentStart.Length - 1)
            {
                // Unknown layout (game changed): spread the clock evenly over the cycle
                float t = Mathf.Repeat(hour, 24f) / 24f * segmentCount;
                index = Mathf.Clamp(Mathf.FloorToInt(t), 0, segmentCount - 1);
                blend = t - index;
                return;
            }

            float e = Mathf.Repeat(hour, 24f);
            if (e < SegmentStart[0]) e += 24f;
            index = 0;
            for (int i = 0; i < segmentCount; i++)
            {
                if (e >= SegmentStart[i] && e < SegmentStart[i + 1])
                {
                    index = i;
                    break;
                }
            }
            blend = (e - SegmentStart[index]) / (SegmentStart[index + 1] - SegmentStart[index]);
        }

        static string SafeName(BetterDayNightManager m, int index)
        {
            string[] names = m.dayNightLightmapNames;
            return names != null && index >= 0 && index < names.Length ? names[index] : "segment " + index;
        }

        public static string Format(float hour)
        {
            int minutes = Mathf.RoundToInt(Mathf.Repeat(hour, 24f) * 60f) % (24 * 60);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        // Accepts "18:30", "1830", "18.5" or "6:30 pm"
        public static bool TryParse(string text, out float hour)
        {
            hour = 0f;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim().ToLowerInvariant();
            bool pm = t.EndsWith("pm"), am = t.EndsWith("am");
            if (pm || am) t = t.Substring(0, t.Length - 2).Trim();

            int h, m = 0;
            if (t.Contains(":"))
            {
                string[] parts = t.Split(new[] { ':' });
                if (parts.Length != 2 || !int.TryParse(parts[0], out h) || !int.TryParse(parts[1], out m)) return false;
            }
            else if (t.Length >= 3 && t.Length <= 4 && int.TryParse(t, out int packed))
            {
                h = packed / 100;
                m = packed % 100;
            }
            else if (float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f))
            {
                if (f < 0f || f >= 24f) return false;
                hour = f;
                return true;
            }
            else return false;

            if (pm && h < 12) h += 12;
            if (am && h == 12) h = 0;
            if (h < 0 || h > 23 || m < 0 || m > 59) return false;
            hour = h + m / 60f;
            return true;
        }

        public void Release()
        {
            BetterDayNightManager m = BetterDayNightManager.instance;
            if (applied && m != null) m.ClearTimeOfDay(true);
            applied = false;
        }
    }
}
