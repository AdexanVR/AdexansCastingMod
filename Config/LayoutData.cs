using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdexansCastingMod.Config
{
    public enum BackgroundStyle { None, Solid, Rounded, Gradient, Glass }
    public enum TextAlign { Left, Center, Right }

    public static class WidgetIds
    {
        public const string CastingPanel = "castingPanel";
        public const string PlayerList = "playerList";
        public const string SpectatorInfo = "spectatorInfo";
        public const string AutoPilotStatus = "autoPilotStatus";
        public const string MatchTimer = "matchTimer";
        public const string Scoreboard = "scoreboard";
        public const string RoundInfo = "roundInfo";
        public const string MatchStatus = "matchStatus";
        public const string Minimap = "minimap";

        public static readonly string[] All =
        {
            CastingPanel, PlayerList, SpectatorInfo, AutoPilotStatus, MatchTimer, Scoreboard, RoundInfo, MatchStatus, Minimap
        };

        // Visual variants a widget can switch between in the layout editor (null = only one look)
        public static string[] StyleNames(string id)
        {
            switch (id)
            {
                case Scoreboard: return new[] { "Scorebug bar", "Detailed panel", "Center bar" };
                case PlayerList: return new[] { "Broadcast list", "Panel" };
                case Minimap: return new[] { "Rounded", "Circle", "Square" };
                default: return null;
            }
        }
    }

    // Position and size are fractions of the screen so a layout made at 1080p also fits 1440p/4K
    [Serializable]
    public class WidgetLayout
    {
        public string id;
        public bool visible = true;
        public float x;
        public float y;
        public float width;
        public float height;
        public float fontSize = 16f;
        public Color textColor = Color.white;
        public Color backgroundColor = new Color(0.06f, 0.07f, 0.09f, 0.86f);
        public Color accentColor = new Color(0.27f, 0.6f, 1f, 1f);
        public float opacity = 1f;
        public BackgroundStyle background = BackgroundStyle.Glass;
        public TextAlign alignment = TextAlign.Left;
        public float cornerRadius = 8f;
        public int style;

        public WidgetLayout Clone() => (WidgetLayout)MemberwiseClone();

        public void Sanitize()
        {
            width = Mathf.Clamp(width, 0.02f, 1f);
            height = Mathf.Clamp(height, 0.015f, 1f);
            x = Mathf.Clamp(x, 0f, 1f - width);
            y = Mathf.Clamp(y, 0f, 1f - height);
            fontSize = Mathf.Clamp(fontSize, 8f, 96f);
            opacity = Mathf.Clamp01(opacity);
            cornerRadius = Mathf.Clamp(cornerRadius, 0f, 40f);
            string[] styles = WidgetIds.StyleNames(id);
            style = styles == null ? 0 : Mathf.Clamp(style, 0, styles.Length - 1);
        }
    }

    [Serializable]
    public class LayoutData
    {
        // Bumped when the default look changes enough that old autosaved layouts should be replaced
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;
        public string name = "Default";
        public List<WidgetLayout> widgets = new List<WidgetLayout>();

        public WidgetLayout Get(string id)
        {
            for (int i = 0; i < widgets.Count; i++)
                if (widgets[i].id == id) return widgets[i];
            return null;
        }

        // Fills in any widget missing from an older/hand-edited file so Get() never returns null
        public void Sanitize()
        {
            if (widgets == null) widgets = new List<WidgetLayout>();
            widgets.RemoveAll(w => w == null || Array.IndexOf(WidgetIds.All, w.id) < 0);
            foreach (string id in WidgetIds.All)
            {
                if (Get(id) == null) widgets.Add(DefaultLayout.For(id));
            }
            foreach (WidgetLayout w in widgets) w.Sanitize();
            version = CurrentVersion;
        }

        public LayoutData Clone()
        {
            var copy = new LayoutData { version = version, name = name };
            foreach (WidgetLayout w in widgets) copy.widgets.Add(w.Clone());
            return copy;
        }
    }

    public static class DefaultLayout
    {
        public static LayoutData Create()
        {
            var data = new LayoutData { name = "Default" };
            foreach (string id in WidgetIds.All) data.widgets.Add(For(id));
            return data;
        }

        // Defaults follow a typical Gorilla Tag league broadcast: scorebug with the clock top right,
        // a minimal player list bottom left, minimap top left, caster tools on the left edge
        public static WidgetLayout For(string id)
        {
            var w = new WidgetLayout { id = id };
            switch (id)
            {
                case WidgetIds.Scoreboard:
                    // Top-center league bar with the clock in a trapezoid (style 2)
                    Place(w, 0.311f, 0.008f, 0.378f, 0.14f, 30f);
                    w.style = 2;
                    w.background = BackgroundStyle.Gradient;
                    w.backgroundColor = new Color(0f, 0f, 0f, 0.42f);
                    w.accentColor = new Color(0.24f, 0.24f, 0.26f, 0.94f);
                    w.cornerRadius = 0f;
                    break;
                case WidgetIds.MatchTimer:
                    Place(w, 0.43f, 0.015f, 0.14f, 0.085f, 34f);
                    w.alignment = TextAlign.Center;
                    w.visible = false; // the scorebug already shows the clock
                    break;
                case WidgetIds.RoundInfo:
                    Place(w, 0.795f, 0.13f, 0.105f, 0.032f, 13f);
                    w.alignment = TextAlign.Center;
                    w.background = BackgroundStyle.Rounded;
                    w.visible = false;
                    break;
                case WidgetIds.MatchStatus:
                    Place(w, 0.835f, 0.17f, 0.13f, 0.05f, 14f);
                    w.alignment = TextAlign.Right;
                    w.visible = false;
                    break;
                case WidgetIds.PlayerList:
                    Place(w, 0.008f, 0.82f, 0.15f, 0.17f, 15f);
                    w.background = BackgroundStyle.None;
                    w.accentColor = new Color(1f, 0.85f, 0.3f, 1f); // spectated player's name
                    w.cornerRadius = 4f;
                    break;
                case WidgetIds.Minimap:
                    Place(w, 0.012f, 0.025f, 0.12f, 0.213f, 12f);
                    w.background = BackgroundStyle.None;
                    w.accentColor = Color.white;
                    w.cornerRadius = 12f;
                    break;
                case WidgetIds.CastingPanel:
                    Place(w, 0.012f, 0.3f, 0.16f, 0.33f, 14f);
                    break;
                case WidgetIds.AutoPilotStatus:
                    Place(w, 0.012f, 0.64f, 0.16f, 0.042f, 13f);
                    w.background = BackgroundStyle.Rounded;
                    break;
                case WidgetIds.SpectatorInfo:
                    Place(w, 0.35f, 0.875f, 0.3f, 0.085f, 16f);
                    w.alignment = TextAlign.Center;
                    w.background = BackgroundStyle.Gradient;
                    break;
            }
            return w;
        }

        static void Place(WidgetLayout w, float x, float y, float width, float height, float font)
        {
            w.x = x;
            w.y = y;
            w.width = width;
            w.height = height;
            w.fontSize = font;
        }
    }
}
