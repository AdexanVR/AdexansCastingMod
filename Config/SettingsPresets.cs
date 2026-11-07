using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace AdexansCastingMod.Config
{
    // A named snapshot of every setting (and optionally the on-screen layout).
    // Settings and layout are stored as JSON strings and loaded through the same
    // "defaults + overwrite + sanitize" path as settings.json, so old, partial or hand-edited
    // presets still load safely and never produce out-of-range values.
    [Serializable]
    public class SettingsPreset
    {
        public int version = 1;
        public string name = "";
        public string savedAt = "";
        public string modVersion = "";
        public string settingsJson = "";
        public bool includesLayout;
        public string layoutJson = "";
    }

    internal sealed class PresetInfo
    {
        public string Name;
        public string Path;
        public string SavedAt;
        public bool IncludesLayout;
        public bool Damaged;
        public string Problem;
    }

    // Presets live in BepInEx/config/AdexansCastingMod/presets and are independent of settings.json:
    // saving or loading a preset never touches the autosaved settings except to apply the loaded values.
    internal sealed class SettingsPresets
    {
        public const string UndoName = "Before last load";
        const int MaxNameLength = 40;

        List<PresetInfo> cache;

        public static string Folder => System.IO.Path.Combine(Storage.Root, "presets");
        static string UndoPath => System.IO.Path.Combine(Storage.Root, "preset_undo.json");

        public IReadOnlyList<PresetInfo> List => cache ?? (cache = Scan());
        public bool HasUndo => File.Exists(UndoPath);
        public string LastLoaded { get; private set; }

        public void Refresh() => cache = null;

        public static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var sb = new StringBuilder();
            foreach (char c in name.Trim())
                if (!char.IsControl(c)) sb.Append(c);
            string s = sb.ToString().Trim();
            return s.Length > MaxNameLength ? s.Substring(0, MaxNameLength).Trim() : s;
        }

        static string PathFor(string name) => System.IO.Path.Combine(Folder, Storage.SafeFileName(name) + ".json");

        public bool Exists(string name) => File.Exists(PathFor(CleanName(name)));

        List<PresetInfo> Scan()
        {
            var list = new List<PresetInfo>();
            try
            {
                if (!Directory.Exists(Folder)) return list;
                foreach (string file in Directory.GetFiles(Folder, "*.json"))
                {
                    var info = new PresetInfo { Path = file, Name = System.IO.Path.GetFileNameWithoutExtension(file) };
                    try
                    {
                        SettingsPreset p = Read(file);
                        if (!string.IsNullOrEmpty(p.name)) info.Name = p.name;
                        info.SavedAt = p.savedAt;
                        info.IncludesLayout = p.includesLayout;
                        if (string.IsNullOrEmpty(p.settingsJson))
                        {
                            info.Damaged = true;
                            info.Problem = "no settings inside";
                        }
                    }
                    catch (Exception e)
                    {
                        info.Damaged = true;
                        info.Problem = e.Message;
                    }
                    list.Add(info);
                }
                list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not list presets: " + e.Message);
            }
            return list;
        }

        static SettingsPreset Read(string path)
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json)) throw new Exception("empty file");
            var p = new SettingsPreset();
            JsonUtility.FromJsonOverwrite(json, p);
            return p;
        }

        static SettingsPreset Capture(string name, CastSettings settings, LayoutData layout)
        {
            var copy = new CastSettings();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(settings), copy);
            // Room codes are session data, not a "look": keep them out of presets
            copy.roomCode = "";
            copy.recentRooms = new List<string>();

            return new SettingsPreset
            {
                name = name,
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                modVersion = PluginInfo.Version,
                settingsJson = JsonUtility.ToJson(copy),
                includesLayout = layout != null,
                layoutJson = layout != null ? JsonUtility.ToJson(layout) : "",
            };
        }

        // Creates a new preset, or overwrites the one with the same name when overwrite is true
        public bool Save(string rawName, CastSettings settings, LayoutData layout, bool overwrite, out string error)
        {
            error = null;
            string name = CleanName(rawName);
            if (name.Length == 0)
            {
                error = "Give the preset a name first";
                return false;
            }
            if (!overwrite && Exists(name))
            {
                error = "A preset called \"" + name + "\" already exists";
                return false;
            }
            bool ok = Storage.Save(PathFor(name), Capture(name, settings, layout));
            if (!ok) error = "Could not write the preset file";
            Refresh();
            return ok;
        }

        // Loads a preset into the live settings. The current setup is kept as an undo snapshot first.
        public bool Load(PresetInfo info, CastSettings settings, Action<LayoutData> applyLayout, LayoutData currentLayout, out string error)
        {
            error = null;
            SettingsPreset preset;
            try
            {
                preset = Read(info.Path);
                if (string.IsNullOrEmpty(preset.settingsJson)) throw new Exception("no settings inside");
            }
            catch (Exception e)
            {
                error = "This preset is damaged (" + e.Message + ")";
                return false;
            }

            // Validate fully before touching anything live
            CastSettings incoming;
            LayoutData layout = null;
            try
            {
                incoming = new CastSettings();
                JsonUtility.FromJsonOverwrite(preset.settingsJson, incoming);
                incoming.Sanitize();
                if (preset.includesLayout && !string.IsNullOrEmpty(preset.layoutJson))
                {
                    layout = new LayoutData();
                    JsonUtility.FromJsonOverwrite(preset.layoutJson, layout);
                    layout.name = string.IsNullOrEmpty(layout.name) ? preset.name : layout.name;
                    layout.Sanitize();
                }
            }
            catch (Exception e)
            {
                error = "This preset has invalid values (" + e.Message + ")";
                return false;
            }

            Storage.Save(UndoPath, Capture(UndoName, settings, currentLayout));
            Apply(incoming, settings);
            if (layout != null) applyLayout(layout);
            LastLoaded = preset.name;
            return true;
        }

        public bool Undo(CastSettings settings, Action<LayoutData> applyLayout, out string error)
        {
            error = null;
            if (!HasUndo)
            {
                error = "Nothing to undo";
                return false;
            }
            SettingsPreset preset;
            try
            {
                preset = Read(UndoPath);
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
            var incoming = new CastSettings();
            JsonUtility.FromJsonOverwrite(preset.settingsJson, incoming);
            incoming.Sanitize();
            Apply(incoming, settings);
            if (preset.includesLayout && !string.IsNullOrEmpty(preset.layoutJson))
            {
                var layout = new LayoutData();
                JsonUtility.FromJsonOverwrite(preset.layoutJson, layout);
                layout.Sanitize();
                applyLayout(layout);
            }
            try
            {
                File.Delete(UndoPath);
            }
            catch
            {
                // harmless
            }
            LastLoaded = null;
            return true;
        }

        // Copies values into the existing settings object, keeping session data (rooms) as they are
        static void Apply(CastSettings incoming, CastSettings live)
        {
            string room = live.roomCode;
            List<string> recent = live.recentRooms;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(incoming), live);
            live.roomCode = room;
            live.recentRooms = recent;
            live.Sanitize();
        }

        public bool Rename(PresetInfo info, string rawName, out string error)
        {
            error = null;
            string name = CleanName(rawName);
            if (name.Length == 0)
            {
                error = "The new name can't be empty";
                return false;
            }
            string target = PathFor(name);
            bool sameFile = string.Equals(System.IO.Path.GetFullPath(target), System.IO.Path.GetFullPath(info.Path), StringComparison.OrdinalIgnoreCase);
            if (!sameFile && File.Exists(target))
            {
                error = "A preset called \"" + name + "\" already exists";
                return false;
            }
            try
            {
                SettingsPreset p = Read(info.Path);
                p.name = name;
                if (!Storage.Save(target, p)) throw new Exception("could not write the file");
                if (!sameFile) File.Delete(info.Path);
                if (LastLoaded == info.Name) LastLoaded = name;
                Refresh();
                return true;
            }
            catch (Exception e)
            {
                error = "Rename failed: " + e.Message;
                return false;
            }
        }

        public bool Delete(PresetInfo info, out string error)
        {
            error = null;
            try
            {
                if (File.Exists(info.Path)) File.Delete(info.Path);
                if (LastLoaded == info.Name) LastLoaded = null;
                Refresh();
                return true;
            }
            catch (Exception e)
            {
                error = "Delete failed: " + e.Message;
                return false;
            }
        }
    }
}
