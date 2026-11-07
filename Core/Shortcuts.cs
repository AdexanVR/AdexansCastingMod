using System;
using System.Collections.Generic;
using AdexansCastingMod.Config;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AdexansCastingMod.Core
{
    // Keyboard shortcuts through the Input System (the game doesn't use the legacy Input manager)
    internal sealed class Shortcuts
    {
        readonly CastingRuntime rt;
        int suppressUntilFrame;
        bool loggedBadKey;

        public CastAction? Capturing { get; private set; }

        public Shortcuts(CastingRuntime rt)
        {
            this.rt = rt;
        }

        public void BeginCapture(CastAction action) => Capturing = action;

        public void CancelCapture() => Capturing = null;

        public void Tick()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (Capturing.HasValue)
            {
                Capture(kb, Capturing.Value);
                return;
            }

            // Don't fire shortcuts while the caster is typing a team name etc.
            if (GUIUtility.keyboardControl != 0 || Time.frameCount <= suppressUntilFrame) return;

            bool ctrl = kb.ctrlKey.isPressed;
            bool shift = kb.shiftKey.isPressed;
            bool alt = kb.altKey.isPressed;

            List<KeyBinding> bindings = rt.Settings.keyBindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                KeyBinding b = bindings[i];
                if (!b.IsBound || b.ctrl != ctrl || b.shift != shift || b.alt != alt) continue;
                if (!WasPressed(kb, b.Key)) continue;
                if (KeyBindings.TryGetAction(b.action, out CastAction action))
                    rt.Execute(action);
            }
        }

        bool WasPressed(Keyboard kb, Key key)
        {
            try
            {
                KeyControl control = kb[key];
                return control != null && control.wasPressedThisFrame;
            }
            catch (ArgumentException)
            {
                // A key that doesn't exist on this keyboard layout; report once
                if (!loggedBadKey)
                {
                    loggedBadKey = true;
                    Plugin.Log.LogWarning("A shortcut uses a key this keyboard doesn't have: " + key);
                }
                return false;
            }
        }

        void Capture(Keyboard kb, CastAction action)
        {
            foreach (KeyControl k in kb.allKeys)
            {
                if (k == null || !k.wasPressedThisFrame) continue;
                Key key = k.keyCode;
                if (IsModifier(key)) continue;

                Capturing = null;
                // The key that finished the capture must not also trigger its new action this frame
                suppressUntilFrame = Time.frameCount + 1;
                if (key == Key.Escape) return;

                KeyBinding b = KeyBindings.Find(rt.Settings.keyBindings, action);
                if (b == null)
                {
                    b = new KeyBinding { action = action.ToString() };
                    rt.Settings.keyBindings.Add(b);
                }

                if (key == Key.Backspace || key == Key.Delete)
                {
                    b.key = (int)Key.None;
                    b.ctrl = b.shift = b.alt = false;
                }
                else
                {
                    b.key = (int)key;
                    b.ctrl = kb.ctrlKey.isPressed;
                    b.shift = kb.shiftKey.isPressed;
                    b.alt = kb.altKey.isPressed;
                }
                rt.MarkSettingsDirty();
                rt.Toasts.Show(KeyBindings.ActionLabel(action) + ": " + KeyBindings.Describe(b));
                return;
            }
        }

        static bool IsModifier(Key k)
        {
            return k == Key.LeftShift || k == Key.RightShift || k == Key.LeftCtrl || k == Key.RightCtrl ||
                   k == Key.LeftAlt || k == Key.RightAlt || k == Key.LeftMeta || k == Key.RightMeta;
        }
    }
}
