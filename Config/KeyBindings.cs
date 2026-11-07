using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace AdexansCastingMod.Config
{
    public enum CastAction
    {
        ToggleSettings,
        ToggleLayoutEditor,
        ToggleOverlay,
        ToggleCasterControls,
        NextPlayer,
        PreviousPlayer,
        ToggleAutoPilot,
        CycleCameraMode,
        ToggleSpectatorCamera,
        TimerStartPause,
        TimerReset,
        TeamAScoreUp,
        TeamAScoreDown,
        TeamBScoreUp,
        TeamBScoreDown,
        NextRound,
        SelectSlot1,
        SelectSlot2,
        SelectSlot3,
        SelectSlot4,
        SelectSlot5,
        SelectSlot6,
        SelectSlot7,
        SelectSlot8,
        SelectSlot9,
        SelectSlot10,
    }

    // Action is stored by name so renaming/reordering the enum never scrambles saved bindings
    [Serializable]
    public class KeyBinding
    {
        public string action;
        public int key;
        public bool ctrl;
        public bool shift;
        public bool alt;

        public KeyBinding() { }

        public KeyBinding(CastAction action, Key key, bool ctrl = false, bool shift = false, bool alt = false)
        {
            this.action = action.ToString();
            this.key = (int)key;
            this.ctrl = ctrl;
            this.shift = shift;
            this.alt = alt;
        }

        public Key Key => (Key)key;
        public bool IsBound => key != (int)Key.None;
    }

    public static class KeyBindings
    {
        public static readonly CastAction[] AllActions = (CastAction[])Enum.GetValues(typeof(CastAction));

        public static List<KeyBinding> Defaults()
        {
            return new List<KeyBinding>
            {
                new KeyBinding(CastAction.ToggleSettings, Key.F6),
                new KeyBinding(CastAction.ToggleLayoutEditor, Key.F7),
                new KeyBinding(CastAction.ToggleOverlay, Key.F8),
                new KeyBinding(CastAction.ToggleCasterControls, Key.F9),
                new KeyBinding(CastAction.NextPlayer, Key.Period),
                new KeyBinding(CastAction.PreviousPlayer, Key.Comma),
                new KeyBinding(CastAction.ToggleAutoPilot, Key.P),
                new KeyBinding(CastAction.CycleCameraMode, Key.C),
                new KeyBinding(CastAction.ToggleSpectatorCamera, Key.V),
                new KeyBinding(CastAction.TimerStartPause, Key.T),
                new KeyBinding(CastAction.TimerReset, Key.T, shift: true),
                new KeyBinding(CastAction.TeamAScoreUp, Key.LeftBracket),
                new KeyBinding(CastAction.TeamAScoreDown, Key.LeftBracket, shift: true),
                new KeyBinding(CastAction.TeamBScoreUp, Key.RightBracket),
                new KeyBinding(CastAction.TeamBScoreDown, Key.RightBracket, shift: true),
                new KeyBinding(CastAction.NextRound, Key.N),
                new KeyBinding(CastAction.SelectSlot1, Key.Digit1),
                new KeyBinding(CastAction.SelectSlot2, Key.Digit2),
                new KeyBinding(CastAction.SelectSlot3, Key.Digit3),
                new KeyBinding(CastAction.SelectSlot4, Key.Digit4),
                new KeyBinding(CastAction.SelectSlot5, Key.Digit5),
                new KeyBinding(CastAction.SelectSlot6, Key.Digit6),
                new KeyBinding(CastAction.SelectSlot7, Key.Digit7),
                new KeyBinding(CastAction.SelectSlot8, Key.Digit8),
                new KeyBinding(CastAction.SelectSlot9, Key.Digit9),
                new KeyBinding(CastAction.SelectSlot10, Key.Digit0),
            };
        }

        // Adds bindings for actions missing from an older settings file and drops unknown/invalid ones
        public static void MergeDefaults(List<KeyBinding> list)
        {
            var seen = new HashSet<string>();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                KeyBinding b = list[i];
                bool known = b != null && Enum.TryParse(b.action, out CastAction _);
                if (!known || !seen.Add(b.action))
                {
                    list.RemoveAt(i);
                    continue;
                }
                if (!Enum.IsDefined(typeof(Key), b.key))
                    b.key = (int)Key.None;
            }

            foreach (KeyBinding def in Defaults())
            {
                if (!seen.Contains(def.action))
                    list.Add(def);
            }
        }

        static Dictionary<string, CastAction> byName;

        // Allocation-free lookup used every frame by the shortcut handler
        public static bool TryGetAction(string name, out CastAction action)
        {
            if (byName == null)
            {
                byName = new Dictionary<string, CastAction>();
                foreach (CastAction a in AllActions) byName[a.ToString()] = a;
            }
            if (name != null && byName.TryGetValue(name, out action)) return true;
            action = default;
            return false;
        }

        public static KeyBinding Find(List<KeyBinding> list, CastAction action)
        {
            string name = action.ToString();
            for (int i = 0; i < list.Count; i++)
                if (list[i].action == name) return list[i];
            return null;
        }

        public static string ActionLabel(CastAction a)
        {
            switch (a)
            {
                case CastAction.ToggleSettings: return "Open / close settings";
                case CastAction.ToggleLayoutEditor: return "Open / close layout editor";
                case CastAction.ToggleOverlay: return "Show / hide entire overlay";
                case CastAction.ToggleCasterControls: return "Show / hide caster controls";
                case CastAction.NextPlayer: return "Spectate next player";
                case CastAction.PreviousPlayer: return "Spectate previous player";
                case CastAction.ToggleAutoPilot: return "Toggle Auto Pilot";
                case CastAction.CycleCameraMode: return "Cycle camera mode";
                case CastAction.ToggleSpectatorCamera: return "Toggle spectator camera";
                case CastAction.TimerStartPause: return "Timer start / pause / resume";
                case CastAction.TimerReset: return "Timer reset";
                case CastAction.TeamAScoreUp: return "Team A score +1";
                case CastAction.TeamAScoreDown: return "Team A score -1";
                case CastAction.TeamBScoreUp: return "Team B score +1";
                case CastAction.TeamBScoreDown: return "Team B score -1";
                case CastAction.NextRound: return "Next round";
                default:
                    if (a >= CastAction.SelectSlot1 && a <= CastAction.SelectSlot10)
                        return "Spectate player #" + (a - CastAction.SelectSlot1 + 1);
                    return a.ToString();
            }
        }

        public static string Describe(KeyBinding b)
        {
            if (b == null || !b.IsBound) return "Unbound";
            string s = "";
            if (b.ctrl) s += "Ctrl + ";
            if (b.shift) s += "Shift + ";
            if (b.alt) s += "Alt + ";
            return s + KeyName(b.Key);
        }

        public static string KeyName(Key k)
        {
            switch (k)
            {
                case Key.Period: return ".";
                case Key.Comma: return ",";
                case Key.Slash: return "/";
                case Key.Backslash: return "\\";
                case Key.Semicolon: return ";";
                case Key.Quote: return "'";
                case Key.LeftBracket: return "[";
                case Key.RightBracket: return "]";
                case Key.Minus: return "-";
                case Key.Equals: return "=";
                case Key.Backquote: return "`";
                case Key.Space: return "Space";
                case Key.PageUp: return "Page Up";
                case Key.PageDown: return "Page Down";
            }
            string n = k.ToString();
            if (n.StartsWith("Digit")) return n.Substring(5);
            if (n.StartsWith("Numpad")) return "Num " + n.Substring(6);
            return n;
        }
    }
}
