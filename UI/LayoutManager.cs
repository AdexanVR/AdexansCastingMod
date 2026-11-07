using System.Collections.Generic;
using System.IO;
using AdexansCastingMod.Config;

namespace AdexansCastingMod.UI
{
    // The working layout autosaves to current_layout.json; named presets live in layouts/<name>.json
    internal sealed class LayoutManager
    {
        List<string> presets;

        public LayoutData Current { get; private set; }
        public bool Dirty { get; set; }

        public LayoutManager()
        {
            Current = Storage.Load(Storage.CurrentLayoutPath, DefaultLayout.Create);
            if (Current.version < 2)
            {
                // Autosave from before the broadcast redesign: start from the new defaults
                Current = DefaultLayout.Create();
                Dirty = true;
            }
            else if (Current.version < LayoutData.CurrentVersion)
            {
                // v3 only changed the scoreboard: switch it to the new center bar, keep everything else
                Current.Sanitize();
                ResetWidget(WidgetIds.Scoreboard);
            }
            Current.Sanitize();
        }

        public IReadOnlyList<string> Presets => presets ?? (presets = Storage.ListLayouts());

        public WidgetLayout Get(string id) => Current.Get(id);

        public void ResetWidget(string id)
        {
            WidgetLayout def = DefaultLayout.For(id);
            int index = Current.widgets.FindIndex(w => w.id == id);
            if (index >= 0) Current.widgets[index] = def;
            else Current.widgets.Add(def);
            Dirty = true;
        }

        // Used when a settings preset brings its own layout
        public void Replace(LayoutData data)
        {
            data.Sanitize();
            Current = data;
            Dirty = true;
        }

        public void ResetAll()
        {
            string name = Current.name;
            Current = DefaultLayout.Create();
            Current.name = name;
            Dirty = true;
        }

        public bool SaveAs(string name)
        {
            name = Storage.SafeFileName(name);
            LayoutData copy = Current.Clone();
            copy.name = name;
            if (!Storage.Save(Storage.LayoutPath(name), copy)) return false;
            Current.name = name;
            presets = null;
            Dirty = true;
            return true;
        }

        public bool LoadPreset(string name)
        {
            string path = Storage.LayoutPath(name);
            if (!File.Exists(path)) return false;
            LayoutData data = Storage.Load(path, () => new LayoutData());
            data.name = name;
            data.Sanitize();
            Current = data;
            Dirty = true;
            return true;
        }

        public bool DeletePreset(string name)
        {
            bool ok = Storage.DeleteLayout(name);
            presets = null;
            return ok;
        }

        public void RefreshPresets() => presets = null;

        public void SaveCurrent()
        {
            Storage.Save(Storage.CurrentLayoutPath, Current);
            Dirty = false;
        }
    }
}
