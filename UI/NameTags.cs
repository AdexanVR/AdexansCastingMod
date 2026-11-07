using System.Collections.Generic;
using AdexansCastingMod.Config;
using AdexansCastingMod.Core;
using UnityEngine;

namespace AdexansCastingMod.UI
{
    // Player names floating above heads in the broadcast view, in the player's color (lava orange when tagged)
    internal static class NameTags
    {
        static readonly Color Outline = new Color(0f, 0f, 0f, 0.85f);

        public static void Draw(CastingRuntime rt)
        {
            if (Event.current.type != EventType.Repaint) return;
            CastSettings s = rt.Settings;
            if (!s.showNameTags || !s.overlayVisible) return;

            Camera cam = rt.Camera.LiveCamera;
            if (cam == null) return;

            CastPlayer target = rt.Director.Target;
            float scale = rt.HudScale;
            IReadOnlyList<CastPlayer> players = rt.Tracker.All;

            for (int i = 0; i < players.Count; i++)
            {
                CastPlayer p = players[i];
                if (!p.HasRig || (p.IsLocal && !s.includeLocalPlayer)) continue;
                if (p == target && !s.nameTagOnSpectated) continue;
                Transform head = p.Head;
                if (head == null) continue;

                Vector3 sp = cam.WorldToScreenPoint(head.position + Vector3.up * 0.42f);
                if (sp.z < 0.3f || sp.z > s.nameTagMaxDistance) continue;

                // Slightly larger up close, never unreadably small far away
                float distScale = Mathf.Clamp(4f / sp.z, 0.6f, 1.2f);
                int size = Mathf.Max(8, Mathf.RoundToInt(s.nameTagSize * scale * distScale));
                float y = Screen.height - sp.y;
                Rect r = new Rect(sp.x - 300f, y - size, 600f, size * 1.4f);

                Color c = p.IsTagged ? WidgetUtil.LavaColor : Readable(p.Color);
                Painter.TextOutline(r, p.Name, size, c, Outline, Mathf.Max(1f, size * 0.08f), TextAnchor.MiddleCenter);
            }
        }

        // Very dark player colors would vanish against the outline; lift them a bit
        static Color Readable(Color c)
        {
            float lum = Painter.Luminance(c);
            return lum < 0.3f ? Color.Lerp(c, Color.white, 0.45f) : c;
        }
    }
}
