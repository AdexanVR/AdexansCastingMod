using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace AdexansCastingMod.Config
{
    // JSON persistence under BepInEx/config/AdexansCastingMod. Writes go to a temp file first so
    // a crash mid-save can't leave a half-written settings file behind.
    internal static class Storage
    {
        public static string Root => Path.Combine(Paths.ConfigPath, "AdexansCastingMod");
        public static string LayoutDir => Path.Combine(Root, "layouts");
        public static string SettingsPath => Path.Combine(Root, "settings.json");
        public static string MatchPath => Path.Combine(Root, "match.json");
        public static string CurrentLayoutPath => Path.Combine(Root, "current_layout.json");

        public static T Load<T>(string path, Func<T> createDefault) where T : class
        {
            T result = createDefault();
            try
            {
                if (!File.Exists(path)) return result;
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return result;
                // Overwrite onto a default instance so fields added in newer versions keep their defaults
                JsonUtility.FromJsonOverwrite(json, result);
                return result;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read {Path.GetFileName(path)}, using defaults: {e.Message}");
                TryBackupCorrupt(path);
                return createDefault();
            }
        }

        public static bool Save<T>(string path, T data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true), Encoding.UTF8);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not save {Path.GetFileName(path)}: {e.Message}");
                return false;
            }
        }

        static void TryBackupCorrupt(string path)
        {
            try
            {
                if (File.Exists(path)) File.Copy(path, path + ".corrupt", true);
            }
            catch
            {
                // Best effort only
            }
        }

        public static string LayoutPath(string name) => Path.Combine(LayoutDir, SafeFileName(name) + ".json");

        public static List<string> ListLayouts()
        {
            var names = new List<string>();
            try
            {
                if (!Directory.Exists(LayoutDir)) return names;
                foreach (string file in Directory.GetFiles(LayoutDir, "*.json"))
                    names.Add(Path.GetFileNameWithoutExtension(file));
                names.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not list layouts: " + e.Message);
            }
            return names;
        }

        public static bool DeleteLayout(string name)
        {
            try
            {
                string path = LayoutPath(name);
                if (!File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not delete layout: " + e.Message);
                return false;
            }
        }

        public static string SafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Layout";
            var sb = new StringBuilder(name.Length);
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in name.Trim())
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            string s = sb.ToString();
            return s.Length > 48 ? s.Substring(0, 48) : s;
        }
    }
}
