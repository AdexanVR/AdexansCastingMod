using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace AdexansCastingMod.UI.Fonts
{
    internal enum FontSource { Game, Bundled, System, Custom }

    // One selectable font in the list
    internal sealed class FontEntry
    {
        public string Id;
        public string Name;
        public string Category;
        public FontSource Source;
        public string RegularFile; // bundled resource name or custom file path
        public string BoldFile;
        public string OsName;
        public string Error;       // set when loading failed; the entry stays listed but greyed out
    }

    // A loaded font ready for drawing: either an OS font (IMGUI GUIStyle) or atlas fonts
    internal sealed class FontFace : IDisposable
    {
        public string Id;
        public Font OsFont;
        public AtlasFont Regular;
        public AtlasFont Bold;
        bool ownsOsFont;

        public static FontFace FromOs(string id, Font font, bool owns) => new FontFace { Id = id, OsFont = font, ownsOsFont = owns };

        public void Dispose()
        {
            Regular?.Dispose();
            Bold?.Dispose();
            if (ownsOsFont && OsFont != null) UnityEngine.Object.Destroy(OsFont);
            Regular = Bold = null;
            OsFont = null;
        }
    }

    // Catalog of fonts and a cache of loaded faces. Bundled fonts ship inside the DLL (so an update
    // only replaces one file) and are extracted to BepInEx/cache before TextMeshPro rasterizes them.
    internal sealed class FontLibrary : IDisposable
    {
        public const string DefaultId = "os:Segoe UI";
        public const string GameFontId = "game:Utopium";

        // Every character the overlay may draw; anything else falls back to the system font
        const string Charset =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "ÇçĞğİıÖöŞşÜüÄäËëÏïÂâÊêÎîÔôÛûÀàÈèÉéÌìÒòÓóÙùÚúÑñßÆæØøÅåÝýÿ" +
            "•·…–—‘’“”‹›«»×°©®™€£¥¿¡";

        static readonly (string name, string category, string regular, string bold)[] Bundled =
        {
            ("Roboto", "Sans-serif", "Roboto-Regular.ttf", "Roboto-Bold.ttf"),
            ("Inter", "Sans-serif", "Inter-Regular.ttf", "Inter-Bold.ttf"),
            ("Poppins", "Sans-serif", "Poppins-Regular.ttf", "Poppins-Bold.ttf"),
            ("Montserrat", "Sans-serif", "Montserrat-Regular.ttf", "Montserrat-Bold.ttf"),
            ("Orbitron", "Display", "Orbitron-Regular.ttf", "Orbitron-Bold.ttf"),
            ("Oswald", "Display", "Oswald-Regular.ttf", "Oswald-Bold.ttf"),
            ("Bebas Neue", "Display", "BebasNeue-Regular.ttf", null),
        };

        static readonly string[] PreferredSystem = { "Segoe UI", "Arial", "Bahnschrift", "Consolas", "Impact", "Verdana", "Tahoma", "Trebuchet MS", "Calibri" };

        readonly List<FontEntry> entries = new List<FontEntry>();
        readonly Dictionary<string, FontFace> faces = new Dictionary<string, FontFace>();
        readonly Dictionary<string, FontFace> previews = new Dictionary<string, FontFace>();
        readonly List<string> previewOrder = new List<string>();

        public IReadOnlyList<FontEntry> Entries => entries;
        public static string CustomFolder => Path.Combine(Paths.ConfigPath, "AdexansCastingMod", "fonts");
        static string ExtractFolder => Path.Combine(Paths.CachePath, "AdexansCastingMod", "fonts");

        public FontLibrary()
        {
            Refresh();
        }

        // Rebuilds the list (picks up fonts dropped into the custom folder)
        public void Refresh()
        {
            entries.Clear();
            entries.Add(new FontEntry { Id = GameFontId, Name = "Utopium", Category = "Gorilla Tag", Source = FontSource.Game });
            entries.Add(new FontEntry { Id = "os:Arial", Name = "Arial", Category = "Sans-serif", Source = FontSource.System, OsName = "Arial" });
            foreach (var b in Bundled)
                entries.Add(new FontEntry { Id = "file:" + b.name, Name = b.name, Category = b.category, Source = FontSource.Bundled, RegularFile = b.regular, BoldFile = b.bold });

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Arial" };
            string[] installed = Array.Empty<string>();
            try
            {
                installed = Font.GetOSInstalledFontNames() ?? Array.Empty<string>();
            }
            catch
            {
                // Some systems can't enumerate fonts; the preferred list below still works
            }
            var installedSet = new HashSet<string>(installed, StringComparer.OrdinalIgnoreCase);
            foreach (string name in PreferredSystem)
                if ((installedSet.Count == 0 || installedSet.Contains(name)) && seen.Add(name))
                    entries.Add(new FontEntry { Id = "os:" + name, Name = name, Category = "System", Source = FontSource.System, OsName = name });
            Array.Sort(installed, StringComparer.OrdinalIgnoreCase);
            foreach (string name in installed)
                if (!string.IsNullOrEmpty(name) && seen.Add(name))
                    entries.Add(new FontEntry { Id = "os:" + name, Name = name, Category = "System", Source = FontSource.System, OsName = name });

            try
            {
                if (Directory.Exists(CustomFolder))
                {
                    foreach (string file in Directory.GetFiles(CustomFolder))
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext != ".ttf" && ext != ".otf") continue;
                        string name = Path.GetFileNameWithoutExtension(file);
                        entries.Add(new FontEntry { Id = "custom:" + Path.GetFileName(file), Name = name, Category = "Custom", Source = FontSource.Custom, RegularFile = file });
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not list custom fonts: " + e.Message);
            }
        }

        public FontEntry Find(string id)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Id == id) return entries[i];
            return null;
        }

        // Full-quality face for drawing the overlay. Returns null (and records Error) if it can't be loaded.
        public FontFace Load(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (faces.TryGetValue(id, out FontFace cached)) return cached;

            FontEntry entry = Find(id);
            if (entry == null)
            {
                // e.g. a preset made on another PC with a font that isn't installed here
                if (id.StartsWith("os:")) entry = new FontEntry { Id = id, Name = id.Substring(3), Source = FontSource.System, OsName = id.Substring(3) };
                else return null;
            }

            FontFace face = Build(entry, 72, 1024, Charset);
            faces[id] = face; // cache failures too (null) so we don't retry every frame
            return face;
        }

        public bool HasPreview(FontEntry entry) => (faces.TryGetValue(entry.Id, out FontFace f) && f != null) || previews.ContainsKey(entry.Id);

        // Small face used only to draw a font's own name in the list
        public FontFace LoadPreview(FontEntry entry)
        {
            if (faces.TryGetValue(entry.Id, out FontFace full) && full != null) return full;
            if (previews.TryGetValue(entry.Id, out FontFace p)) return p;

            FontFace face = entry.Source == FontSource.System || entry.Source == FontSource.Game
                ? Build(entry, 72, 1024, Charset) // OS fonts are cheap; the game font is already in memory
                : Build(entry, 40, 512, entry.Name + entry.Name.ToUpperInvariant() + " 0123456789AaBbCcGgQqRr:");
            previews[entry.Id] = face;
            previewOrder.Add(entry.Id);

            // Keep memory bounded while scrolling through hundreds of system fonts
            while (previewOrder.Count > 48)
            {
                string old = previewOrder[0];
                previewOrder.RemoveAt(0);
                if (previews.TryGetValue(old, out FontFace f)) f?.Dispose();
                previews.Remove(old);
            }
            return face;
        }

        FontFace Build(FontEntry entry, int samplingSize, int atlasSize, string charset)
        {
            entry.Error = null;
            try
            {
                switch (entry.Source)
                {
                    case FontSource.System:
                        Font os = Font.CreateDynamicFontFromOSFont(entry.OsName, 16);
                        if (os == null) throw new Exception("not installed");
                        return FontFace.FromOs(entry.Id, os, true);

                    case FontSource.Game:
                        return BuildGameFont(entry);

                    default:
                        string regularPath = entry.Source == FontSource.Bundled ? Extract(entry.RegularFile) : entry.RegularFile;
                        if (regularPath == null || !File.Exists(regularPath)) throw new Exception("font file is missing");
                        AtlasFont regular = Rasterize(regularPath, entry.Name, samplingSize, atlasSize, charset);
                        AtlasFont bold = null;
                        if (!string.IsNullOrEmpty(entry.BoldFile))
                        {
                            string boldPath = Extract(entry.BoldFile);
                            if (boldPath != null) bold = TryRasterize(boldPath, entry.Name + " Bold", samplingSize, atlasSize, charset);
                        }
                        if (bold == null)
                        {
                            // No bold file: thicken the regular one when bold is asked for
                            bold = TryRasterize(regularPath, entry.Name, samplingSize, atlasSize, charset);
                            if (bold != null) bold.FauxBold = true;
                        }
                        return new FontFace { Id = entry.Id, Regular = regular, Bold = bold };
                }
            }
            catch (Exception e)
            {
                entry.Error = e.Message;
                Plugin.Log.LogWarning($"Font '{entry.Name}' unavailable: {e.Message}");
                return null;
            }
        }

        static FontFace BuildGameFont(FontEntry entry)
        {
            TMP_FontAsset asset = null;
            foreach (TMP_FontAsset fa in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (fa != null && fa.name.IndexOf("utopium", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    asset = fa;
                    break;
                }
            }
            if (asset == null) throw new Exception("not found in the game yet (try again once you're in the game)");

            AtlasFont atlas = AtlasFont.FromAsset(asset, "Utopium", out string error);
            if (atlas == null) throw new Exception(error ?? "could not read the game font");
            AtlasFont bold = AtlasFont.FromAsset(asset, "Utopium", out _);
            if (bold != null) bold.FauxBold = true;
            return new FontFace { Id = entry.Id, Regular = atlas, Bold = bold };
        }

        static AtlasFont TryRasterize(string path, string name, int samplingSize, int atlasSize, string charset)
        {
            try
            {
                return Rasterize(path, name, samplingSize, atlasSize, charset);
            }
            catch
            {
                return null;
            }
        }

        // TextMeshPro rasterizes the file into a distance-field atlas; AtlasFont copies it and the
        // temporary TMP asset is destroyed again right away
        static AtlasFont Rasterize(string path, string name, int samplingSize, int atlasSize, string charset)
        {
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(path, 0, samplingSize, 9, GlyphRenderMode.SDFAA, atlasSize, atlasSize);
            if (asset == null) throw new Exception("TextMeshPro could not open the font file");
            try
            {
                asset.TryAddCharacters(charset);
                AtlasFont font = AtlasFont.FromAsset(asset, name, out string error);
                if (font == null) throw new Exception(error ?? "rendering failed");
                return font;
            }
            finally
            {
                DestroyTemporaryAsset(asset);
            }
        }

        static FieldInfo applyQueueField;
        static FieldInfo applyLookupField;
        static bool applyQueueChecked;

        // TMP queues new atlas textures for a deferred Apply(); destroying one that is still queued makes
        // TMP throw every frame. So our textures are taken out of that queue first. If TMP's internals
        // ever change and that isn't possible, the (small) textures are kept rather than risk the errors.
        static void DestroyTemporaryAsset(TMP_FontAsset asset)
        {
            bool canDestroyTextures = RemoveFromApplyQueue(asset.atlasTextures);
            if (canDestroyTextures && asset.atlasTextures != null)
                foreach (Texture2D t in asset.atlasTextures)
                    if (t != null) UnityEngine.Object.Destroy(t);
            if (asset.material != null) UnityEngine.Object.Destroy(asset.material);
            if (canDestroyTextures) UnityEngine.Object.Destroy(asset);
        }

        static bool RemoveFromApplyQueue(Texture2D[] textures)
        {
            if (textures == null) return true;
            try
            {
                if (!applyQueueChecked)
                {
                    applyQueueChecked = true;
                    const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
                    applyQueueField = typeof(TMP_FontAsset).GetField("k_FontAssets_AtlasTexturesUpdateQueue", flags);
                    applyLookupField = typeof(TMP_FontAsset).GetField("k_FontAssets_AtlasTexturesUpdateQueueLookup", flags);
                }
                if (!(applyQueueField?.GetValue(null) is List<Texture2D> queue) || !(applyLookupField?.GetValue(null) is HashSet<int> lookup))
                    return false;

                foreach (Texture2D t in textures)
                {
                    if (t == null) continue;
                    queue.Remove(t);
                    lookup.Remove(t.GetInstanceID());
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Writes a bundled font from the DLL's resources to the cache folder (only when missing or changed)
        static string Extract(string fileName)
        {
            Assembly asm = typeof(FontLibrary).Assembly;
            string resource = "AdexansCastingMod.Fonts." + fileName;
            using (Stream s = asm.GetManifestResourceStream(resource))
            {
                if (s == null) return null;
                Directory.CreateDirectory(ExtractFolder);
                string path = Path.Combine(ExtractFolder, fileName);
                if (File.Exists(path) && new FileInfo(path).Length == s.Length) return path;
                string tmp = path + ".tmp";
                using (FileStream f = File.Create(tmp)) s.CopyTo(f);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return path;
            }
        }

        // Frees fonts that aren't in use (called when the font menu closes)
        public void ReleaseExcept(params string[] keep)
        {
            var keepSet = new HashSet<string>(keep);
            foreach (KeyValuePair<string, FontFace> kv in previews) kv.Value?.Dispose();
            previews.Clear();
            previewOrder.Clear();

            var drop = new List<string>();
            foreach (KeyValuePair<string, FontFace> kv in faces)
                if (!keepSet.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (string id in drop)
            {
                faces[id]?.Dispose();
                faces.Remove(id);
            }
        }

        // Lets a failed font (e.g. the game font before the game finished loading) be tried again
        public void Forget(string id)
        {
            if (faces.TryGetValue(id, out FontFace f)) f?.Dispose();
            faces.Remove(id);
            if (previews.TryGetValue(id, out FontFace p)) p?.Dispose();
            previews.Remove(id);
            previewOrder.Remove(id);
        }

        public void Dispose()
        {
            foreach (FontFace f in faces.Values) f?.Dispose();
            foreach (FontFace f in previews.Values) f?.Dispose();
            faces.Clear();
            previews.Clear();
        }
    }
}
