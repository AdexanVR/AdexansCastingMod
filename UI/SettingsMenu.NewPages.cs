using System.Collections.Generic;
using AdexansCastingMod.Config;
using AdexansCastingMod.Core;
using AdexansCastingMod.Core.Updater;
using AdexansCastingMod.UI.Fonts;
using UnityEngine;

namespace AdexansCastingMod.UI
{
    // Fonts, Day & Time, Presets and Updates pages
    internal sealed partial class SettingsMenu
    {
        static readonly string[] FontCategories = { "All", "Gorilla Tag", "Sans-serif", "Display", "System", "Custom" };
        static readonly string[] InstallModes = { "Install automatically", "Ask me first" };
        static readonly int TimeSliderHint = "AdexanCastTimeSlider".GetHashCode();
        const string FontSearchField = "AdexanCastFontSearch";
        const string TimeField = "AdexanCastTimeField";
        const string PresetField = "AdexanCastPresetName";
        const string RenameField = "AdexanCastPresetRename";

        // Fonts page
        string fontFilter = "";
        int fontCategory;
        float fontListScroll;
        float fontListContent;
        readonly List<FontEntry> visibleFonts = new List<FontEntry>();

        // Day & time page
        string timeText;
        string timeError;

        // Presets page
        string presetName = "";
        bool presetIncludeLayout = true;
        string renamingPath;
        string renameText = "";

        void ResetPageState()
        {
            timeText = null;
            timeError = null;
            renamingPath = null;
            rt.Presets.Refresh();
        }

        // ============================================================ Fonts

        void FontPage(Form f)
        {
            CastSettings s = rt.Settings;
            FontEntry current = rt.Fonts.Find(s.fontId);
            FontFace face = rt.Fonts.Load(s.fontId);

            f.Section("Preview");
            if (f.Block("font preview current typeface"))
            {
                Rect card = new Rect(f.X, f.Y, f.Width, 118f);
                Painter.Rect(card, new Color(0.03f, 0.035f, 0.045f, 1f), 10f);
                Painter.Rect(new Rect(card.x, card.y, 5f, card.height), Theme.Accent, 2f);
                if (face != null)
                {
                    Painter.TextIn(face, new Rect(card.x + 22f, card.y + 10f, card.width - 40f, 52f), "ROUND 1   29:49", 40, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                    Painter.TextIn(face, new Rect(card.x + 22f, card.y + 62f, card.width - 40f, 26f), "BTBHAS1S1  ·  Tagged  ·  Season 5", 19, new Color(1f, 0.85f, 0.3f, 1f), TextAnchor.MiddleLeft, FontStyle.Normal);
                    Painter.TextIn(face, new Rect(card.x + 22f, card.y + 88f, card.width - 40f, 22f), "abcdefghijklmnopqrstuvwxyz 0123456789 ÇĞİÖŞÜ", 14, Theme.TextDim, TextAnchor.MiddleLeft, FontStyle.Normal);
                }
                else
                {
                    string why = current != null && current.Error != null ? current.Error : "not loaded";
                    Painter.Text(new Rect(card.x + 22f, card.y, card.width - 40f, card.height), "This font can't be used (" + why + "). The overlay is using Segoe UI.", 13, Theme.Warning, TextAnchor.MiddleLeft, true);
                }
                f.Y += card.height + 10f;
                Painter.Text(new Rect(f.X, f.Y, f.Width, 22f), "In use:  " + (current != null ? current.Name + "  ·  " + current.Category : s.fontId), 13, Theme.Text, TextAnchor.MiddleLeft, true);
                f.Y += 30f;
            }
            bool menuFont = f.Toggle("Use this font in the menus too", s.menuUsesOverlayFont, "Off keeps these settings in the easy-to-read system font", false);
            if (menuFont != s.menuUsesOverlayFont)
            {
                s.menuUsesOverlayFont = menuFont;
                rt.MarkSettingsDirty();
            }
            if (f.Block("font reset folder rescan"))
            {
                int b = ButtonRow(f, 32f, new[] { "Reset to default", "Add your own fonts…", "Rescan fonts" });
                if (b == 0) PickFont(FontLibrary.DefaultId, "Segoe UI");
                else if (b == 1)
                {
                    System.IO.Directory.CreateDirectory(FontLibrary.CustomFolder);
                    Application.OpenURL("file:///" + FontLibrary.CustomFolder.Replace('\\', '/'));
                    rt.Toasts.Show("Put .ttf or .otf files in this folder, then press Rescan");
                }
                else if (b == 2)
                {
                    rt.Fonts.Refresh();
                    rt.Toasts.Show(rt.Fonts.Entries.Count + " fonts found");
                }
            }
            f.Changed = false;

            f.Section("Choose a font");
            if (!f.Block("font list choose typeface utopium arial roboto montserrat poppins inter orbitron oswald bebas")) return;

            // Search + category filter
            Rect search = new Rect(f.X, f.Y, f.Width * 0.4f, 32f);
            GUI.SetNextControlName(FontSearchField);
            fontFilter = Controls.TextField(search, fontFilter, 40, 13);
            if (fontFilter.Length == 0 && GUI.GetNameOfFocusedControl() != FontSearchField)
                Painter.Text(new Rect(search.x + 10f, search.y, search.width - 20f, search.height), "Find a font…", 13, Theme.TextFaint, TextAnchor.MiddleLeft);
            int cat = Controls.Segmented(new Rect(search.xMax + 8f, f.Y, f.Width - search.width - 8f, 32f), fontCategory, FontCategories, 11);
            if (cat != fontCategory)
            {
                fontCategory = cat;
                fontListScroll = 0f;
            }
            f.Y += 42f;

            visibleFonts.Clear();
            string filter = fontFilter.Trim();
            foreach (FontEntry e in rt.Fonts.Entries)
            {
                if (fontCategory > 0 && e.Category != FontCategories[fontCategory]) continue;
                if (filter.Length > 0 && e.Name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                visibleFonts.Add(e);
            }

            const float rowH = 44f;
            float listH = Mathf.Clamp(visibleFonts.Count * rowH, rowH, 352f);
            Rect list = new Rect(f.X, f.Y, f.Width, listH);
            Painter.Rect(list, new Color(0.035f, 0.04f, 0.05f, 1f), 8f);
            fontListContent = visibleFonts.Count * rowH;
            ScrollArea.Begin(list, ref fontListScroll, fontListContent);

            int loadBudget = 2; // rasterizing a font takes a moment; spread it over frames
            for (int i = 0; i < visibleFonts.Count; i++)
            {
                float y = i * rowH - fontListScroll;
                if (y + rowH < 0f || y > listH) continue;
                FontEntry e = visibleFonts[i];
                Rect row = new Rect(4f, y + 2f, list.width - 8f, rowH - 4f);
                bool selected = e.Id == s.fontId;
                if (selected) Painter.Rect(row, Painter.Alpha(Theme.Accent, 0.2f), 7f);
                else if (Controls.Hover(row)) Painter.Rect(row, Theme.Surface, 7f);

                FontFace preview = null;
                bool canLoad = Event.current.type == EventType.Repaint && loadBudget > 0;
                if (e.Error == null && (canLoad || rt.Fonts.HasPreview(e)))
                {
                    if (!rt.Fonts.HasPreview(e)) loadBudget--;
                    preview = rt.Fonts.LoadPreview(e);
                }

                Rect nameRect = new Rect(row.x + 14f, row.y, row.width - 230f, row.height);
                if (preview != null) Painter.TextIn(preview, nameRect, e.Name, 21, e.Error != null ? Theme.TextFaint : Theme.Text, TextAnchor.MiddleLeft);
                else Painter.Text(nameRect, e.Name, 15, e.Error != null ? Theme.TextFaint : Theme.TextDim, TextAnchor.MiddleLeft);

                if (e.Error != null)
                    Painter.Text(new Rect(row.xMax - 330f, row.y, 220f, row.height), Painter.Fit("Unavailable: " + e.Error, 11, 220f), 11, Theme.Danger, TextAnchor.MiddleRight);
                Painter.Text(new Rect(row.xMax - 210f, row.y, 110f, row.height), e.Category, 11, Theme.TextFaint, TextAnchor.MiddleRight);
                if (selected) Painter.Pill(row.xMax - 12f, row.center.y, "IN USE", 10, Theme.Accent, Painter.OnColor(Theme.Accent), alignRight: true);
                Controls.Tip(row, e.Error != null ? "This font couldn't be loaded: " + e.Error : "Click to use " + e.Name + " for the overlay");

                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    if (e.Error != null && e.Source != FontSource.Game) rt.Toasts.Show(e.Name + " can't be used: " + e.Error, Theme.Danger);
                    else PickFont(e.Id, e.Name);
                }
            }
            if (visibleFonts.Count == 0)
                Painter.Text(new Rect(14f, 0f, list.width - 28f, rowH), "No fonts match \"" + filter + "\"", 13, Theme.TextDim, TextAnchor.MiddleLeft);
            ScrollArea.End(list, fontListScroll, fontListContent);
            f.Y += listH + 10f;
            f.Note("Utopium is Gorilla Tag's own font. System fonts are the ones installed on this PC.");
        }

        void PickFont(string id, string name)
        {
            if (id == FontLibrary.GameFontId) rt.Fonts.Forget(id); // retry if it failed before the game loaded it
            rt.Settings.fontId = id;
            rt.MarkSettingsDirty();
            rt.ReloadFont();
            rt.Toasts.Show("Font: " + name, Theme.Success);
        }

        // ============================================================ Day & time

        void DayTimePage(Form f)
        {
            CastSettings s = rt.Settings;
            DayTimeController dt = rt.DayTime;

            f.Section("Time of day");
            bool on = f.Toggle("Set the time myself", s.timeOverride, "Off = the game's normal day cycle. Only changes what you see on this PC", false);
            if (on != s.timeOverride)
            {
                s.timeOverride = on;
                rt.MarkSettingsDirty();
            }

            if (f.Block("clock time hour slider sunrise noon sunset night midnight"))
            {
                float shown = s.timeOverride ? dt.DisplayHour : s.timeOfDayHour;
                Painter.Text(new Rect(f.X, f.Y, 160f, 52f), DayTimeController.Format(shown), 40, s.timeOverride ? Theme.Text : Theme.TextDim, TextAnchor.MiddleLeft, true);
                Color statusColor = !dt.Available ? Theme.Warning : s.timeOverride ? Theme.Success : Theme.TextDim;
                Painter.Text(new Rect(f.X + 160f, f.Y, f.Width - 160f, 52f), Painter.Fit(dt.Status, 12, f.Width - 160f), 12, statusColor, TextAnchor.MiddleRight);
                f.Y += 58f;

                // 24-hour bar colored like the sky at each hour
                Rect bar = new Rect(f.X, f.Y, f.Width, 34f);
                float hour = TimeSlider(bar, s.timeOfDayHour);
                if (!Mathf.Approximately(hour, s.timeOfDayHour)) SetHour(hour);
                f.Y += 40f;
                for (int h = 0; h <= 24; h += 3)
                {
                    float x = bar.x + bar.width * h / 24f;
                    Painter.Text(new Rect(x - 20f, f.Y, 40f, 16f), h.ToString("00") + ":00", 10, Theme.TextFaint, TextAnchor.MiddleCenter);
                }
                f.Y += 26f;

                // Presets
                var names = new string[DayTimeController.Presets.Length];
                int active = -1;
                for (int i = 0; i < names.Length; i++)
                {
                    names[i] = DayTimeController.Presets[i].name;
                    if (s.timeOverride && Mathf.Abs(Mathf.DeltaAngle(s.timeOfDayHour * 15f, DayTimeController.Presets[i].hour * 15f)) < 0.5f) active = i;
                }
                int p = ButtonRow(f, 36f, names, active);
                if (p >= 0) SetHour(DayTimeController.Presets[p].hour);

                // Fine steps
                int step = ButtonRow(f, 30f, new[] { "−1 hour", "−15 min", "+15 min", "+1 hour" });
                if (step >= 0) SetHour(s.timeOfDayHour + new[] { -1f, -0.25f, 0.25f, 1f }[step]);
            }

            f.Section("Exact time");
            if (f.Block("exact time type hour minute"))
            {
                Event e = Event.current;
                bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == TimeField;
                if (timeText == null) timeText = DayTimeController.Format(s.timeOfDayHour);
                Rect field = new Rect(f.X, f.Y, 160f, 34f);
                GUI.SetNextControlName(TimeField);
                timeText = Controls.TextField(field, timeText, 8, 16);
                bool apply = Controls.Button(new Rect(field.xMax + 8f, f.Y, 110f, 34f), "Set time", 12, true);
                if (apply || enter)
                {
                    if (DayTimeController.TryParse(timeText, out float parsed))
                    {
                        SetHour(parsed);
                        timeError = null;
                    }
                    else timeError = "Use a time like 18:30, 0630 or 6:30 pm";
                    if (enter) e.Use();
                }
                Painter.Text(new Rect(field.xMax + 130f, f.Y, f.Width - 300f, 34f), timeError ?? "24-hour clock, e.g. 18:30", 12, timeError != null ? Theme.Danger : Theme.TextFaint, TextAnchor.MiddleLeft);
                f.Y += 44f;
            }

            f.Section("Reset");
            if (f.Block("reset default time"))
            {
                if (ButtonRow(f, 34f, new[] { "Reset to the game's normal cycle" }) == 0)
                {
                    s.timeOverride = false;
                    s.timeOfDayHour = 12f;
                    timeText = null;
                    timeError = null;
                    rt.MarkSettingsDirty();
                    rt.Toasts.Show("Time of day back to normal");
                }
                f.Note("Your chosen time stays applied in every map and room until you turn it off.");
            }
        }

        void SetHour(float hour)
        {
            CastSettings s = rt.Settings;
            s.timeOfDayHour = Mathf.Repeat(Mathf.Round(hour * 12f) / 12f, 24f); // 5-minute steps
            s.timeOverride = true;
            timeText = DayTimeController.Format(s.timeOfDayHour);
            timeError = null;
            rt.MarkSettingsDirty();
        }

        static Color SkyColor(float hour)
        {
            float[] keys = { 0f, 4.5f, 6f, 8f, 12f, 16f, 18f, 19.5f, 21f, 24f };
            Color[] colors =
            {
                new Color(0.04f, 0.06f, 0.16f), new Color(0.1f, 0.1f, 0.26f), new Color(0.98f, 0.58f, 0.38f), new Color(0.55f, 0.76f, 0.96f),
                new Color(0.42f, 0.7f, 1f), new Color(0.5f, 0.72f, 0.96f), new Color(0.98f, 0.5f, 0.28f), new Color(0.45f, 0.28f, 0.48f),
                new Color(0.08f, 0.09f, 0.22f), new Color(0.04f, 0.06f, 0.16f),
            };
            for (int i = 0; i < keys.Length - 1; i++)
                if (hour <= keys[i + 1]) return Color.Lerp(colors[i], colors[i + 1], Mathf.InverseLerp(keys[i], keys[i + 1], hour));
            return colors[0];
        }

        static float TimeSlider(Rect r, float hour)
        {
            int id = GUIUtility.GetControlID(TimeSliderHint, FocusType.Passive, r);
            Event e = Event.current;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && GUI.enabled && r.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        hour = Mathf.Clamp01((e.mousePosition.x - r.x) / r.width) * 24f;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        hour = Mathf.Clamp01((e.mousePosition.x - r.x) / r.width) * 24f;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
                case EventType.Repaint:
                    const int segments = 48;
                    float w = r.width / segments;
                    for (int i = 0; i < segments; i++)
                        Painter.Rect(new Rect(r.x + i * w, r.y, w + 1f, r.height), SkyColor((i + 0.5f) * 24f / segments));
                    Painter.Border(r, new Color(1f, 1f, 1f, 0.15f), 1f, 6f);
                    float x = r.x + r.width * Mathf.Repeat(hour, 24f) / 24f;
                    Painter.Rect(new Rect(x - 3f, r.y - 4f, 6f, r.height + 8f), Color.white, 3f);
                    Painter.Border(new Rect(x - 3f, r.y - 4f, 6f, r.height + 8f), new Color(0f, 0f, 0f, 0.5f), 1f, 3f);
                    break;
            }
            return hour;
        }

        // ============================================================ Presets

        void PresetsPage(Form f)
        {
            SettingsPresets presets = rt.Presets;

            f.Section("Save the current setup");
            if (f.Block("save preset name new"))
            {
                Event e = Event.current;
                bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == PresetField;
                Rect field = new Rect(f.X, f.Y, f.Width - 170f, 36f);
                GUI.SetNextControlName(PresetField);
                presetName = Controls.TextField(field, presetName, 40, 14);
                if (presetName.Length == 0 && GUI.GetNameOfFocusedControl() != PresetField)
                    Painter.Text(new Rect(field.x + 10f, field.y, field.width - 20f, field.height), "Preset name, e.g. Finals stream", 14, Theme.TextFaint, TextAnchor.MiddleLeft);
                bool exists = presets.Exists(presetName);
                bool click = Controls.Button(new Rect(field.xMax + 8f, f.Y, 162f, 36f), exists ? "Overwrite preset" : "Save as preset", 13, true);
                Controls.Tip(new Rect(field.xMax + 8f, f.Y, 162f, 36f), exists ? "A preset with this name exists; it will be replaced" : "Saves every setting under this name");
                if (click || enter)
                {
                    if (enter) e.Use();
                    if (exists && confirm != "overwrite-new:" + presetName)
                    {
                        confirm = "overwrite-new:" + presetName;
                        rt.Toasts.Show("Press again to overwrite \"" + SettingsPresets.CleanName(presetName) + "\"", Theme.Warning);
                    }
                    else if (presets.Save(presetName, rt.Settings, presetIncludeLayout ? rt.Layouts.Current : null, exists, out string error))
                    {
                        rt.Toasts.Show("Preset \"" + SettingsPresets.CleanName(presetName) + "\" saved", Theme.Success);
                        presetName = "";
                        confirm = null;
                    }
                    else rt.Toasts.Show(error, Theme.Danger);
                }
                f.Y += 44f;
            }
            presetIncludeLayout = f.Toggle("Include the on-screen layout", presetIncludeLayout, "Element positions, sizes and colors are saved with the preset");
            f.Changed = false;
            f.Note("Presets don't change scores or teams. Your current settings always keep autosaving on their own.");

            f.Section("Your presets");
            if (!f.Block("presets list load rename delete overwrite")) return;

            IReadOnlyList<PresetInfo> list = presets.List;
            if (list.Count == 0) f.Note("No presets yet. Name your current setup above and save it.");
            if (presets.LastLoaded != null) f.Note("Last loaded: " + presets.LastLoaded);

            foreach (PresetInfo info in list)
            {
                Rect row = new Rect(f.X, f.Y, f.Width, 50f);
                bool last = presets.LastLoaded == info.Name;
                Painter.Rect(row, last ? Painter.Alpha(Theme.Accent, 0.14f) : Theme.Surface, 8f);
                if (last) Painter.Rect(new Rect(row.x, row.y + 10f, 3f, row.height - 20f), Theme.Accent, 1.5f);

                float buttonsW = info.Damaged ? 90f : 4 * 86f;
                if (renamingPath == info.Path)
                {
                    Rect rf = new Rect(row.x + 12f, row.y + 9f, row.width - buttonsW - 30f, 32f);
                    Event e = Event.current;
                    bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == RenameField;
                    bool escape = e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && GUI.GetNameOfFocusedControl() == RenameField;
                    GUI.SetNextControlName(RenameField);
                    renameText = Controls.TextField(rf, renameText, 40, 14);
                    if (escape)
                    {
                        renamingPath = null;
                        e.Use();
                    }
                    float bx = row.xMax - buttonsW - 8f;
                    bool ok = Controls.Button(new Rect(bx, row.y + 10f, 80f, 30f), "Rename", 11, true) || enter;
                    if (Controls.Button(new Rect(bx + 86f, row.y + 10f, 80f, 30f), "Cancel", 11)) renamingPath = null;
                    if (ok && renamingPath != null)
                    {
                        if (enter) e.Use();
                        if (presets.Rename(info, renameText, out string error))
                        {
                            rt.Toasts.Show("Renamed to \"" + SettingsPresets.CleanName(renameText) + "\"", Theme.Success);
                            renamingPath = null;
                            break; // the list was rebuilt
                        }
                        rt.Toasts.Show(error, Theme.Danger);
                    }
                    f.Y += 56f;
                    continue;
                }

                Painter.Text(new Rect(row.x + 16f, row.y + 6f, row.width - buttonsW - 30f, 22f), Painter.Fit(info.Name, 14, row.width - buttonsW - 30f), 14, info.Damaged ? Theme.TextDim : Theme.Text, TextAnchor.MiddleLeft, true);
                string sub = info.Damaged ? "Damaged: " + info.Problem : (string.IsNullOrEmpty(info.SavedAt) ? "" : "Saved " + info.SavedAt) + (info.IncludesLayout ? "  ·  includes layout" : "");
                Painter.Text(new Rect(row.x + 16f, row.y + 26f, row.width - buttonsW - 30f, 18f), Painter.Fit(sub, 11, row.width - buttonsW - 30f), 11, info.Damaged ? Theme.Danger : Theme.TextFaint, TextAnchor.MiddleLeft);

                float x = row.xMax - buttonsW - 8f;
                string delKey = "delete:" + info.Path;
                if (!info.Damaged)
                {
                    Rect loadR = new Rect(x, row.y + 10f, 80f, 30f);
                    if (Controls.Button(loadR, "Load", 11, true))
                    {
                        if (presets.Load(info, rt.Settings, l => rt.Layouts.Replace(l), rt.Layouts.Current, out string error))
                        {
                            rt.MarkSettingsDirty();
                            rt.ReloadFont();
                            Form.ForgetBuffers();
                            rt.Toasts.Show("Loaded \"" + info.Name + "\"", Theme.Success);
                        }
                        else rt.Toasts.Show(error, Theme.Danger);
                    }
                    Controls.Tip(loadR, "Switch to this preset. Your current setup is kept so you can undo.");
                    x += 86f;

                    string overKey = "overwrite:" + info.Path;
                    Rect overR = new Rect(x, row.y + 10f, 80f, 30f);
                    if (Controls.Button(overR, confirm == overKey ? "Sure?" : "Overwrite", 11, confirm == overKey))
                    {
                        if (confirm == overKey)
                        {
                            if (presets.Save(info.Name, rt.Settings, presetIncludeLayout ? rt.Layouts.Current : null, true, out string error))
                                rt.Toasts.Show("\"" + info.Name + "\" updated with the current settings", Theme.Success);
                            else rt.Toasts.Show(error, Theme.Danger);
                            confirm = null;
                        }
                        else confirm = overKey;
                    }
                    Controls.Tip(overR, "Replace this preset with your current settings");
                    x += 86f;

                    Rect renR = new Rect(x, row.y + 10f, 80f, 30f);
                    if (Controls.Button(renR, "Rename", 11))
                    {
                        renamingPath = info.Path;
                        renameText = info.Name;
                    }
                    x += 86f;
                }

                Rect delR = new Rect(x, row.y + 10f, 80f, 30f);
                if (Controls.Button(delR, confirm == delKey ? "Delete!" : "Delete", 11, confirm == delKey, danger: true))
                {
                    if (confirm == delKey)
                    {
                        if (presets.Delete(info, out string error)) rt.Toasts.Show("Deleted \"" + info.Name + "\"");
                        else rt.Toasts.Show(error, Theme.Danger);
                        confirm = null;
                        break; // the list was rebuilt
                    }
                    confirm = delKey;
                }
                Controls.Tip(delR, confirm == delKey ? "Click again to delete permanently" : "Delete this preset (asks to confirm)");
                f.Y += 56f;
            }

            if (presets.HasUndo)
            {
                f.Space(4f);
                if (ButtonRow(f, 34f, new[] { "Undo last load (go back to the setup before it)" }) == 0)
                {
                    if (presets.Undo(rt.Settings, l => rt.Layouts.Replace(l), out string error))
                    {
                        rt.MarkSettingsDirty();
                        rt.ReloadFont();
                        Form.ForgetBuffers();
                        rt.Toasts.Show("Previous setup restored", Theme.Success);
                    }
                    else rt.Toasts.Show(error, Theme.Danger);
                }
            }
        }

        // ============================================================ Updates

        void UpdatesPage(Form f)
        {
            CastSettings s = rt.Settings;
            AutoUpdater up = rt.Updater;

            f.Section("Version");
            if (f.Block("version installed latest check update"))
            {
                Painter.Text(new Rect(f.X, f.Y, 200f, 20f), "INSTALLED", 10, Theme.TextFaint, TextAnchor.MiddleLeft, true);
                Painter.Text(new Rect(f.X + 200f, f.Y, 200f, 20f), "LATEST ON GITHUB", 10, Theme.TextFaint, TextAnchor.MiddleLeft, true);
                Painter.Text(new Rect(f.X + 400f, f.Y, f.Width - 400f, 20f), "LAST CHECK", 10, Theme.TextFaint, TextAnchor.MiddleLeft, true);
                f.Y += 20f;
                Painter.Text(new Rect(f.X, f.Y, 200f, 34f), "v" + PluginInfo.Version, 24, Theme.Text, TextAnchor.MiddleLeft, true);
                string latest = string.IsNullOrEmpty(up.LatestVersion) ? "—" : "v" + up.LatestVersion;
                Painter.Text(new Rect(f.X + 200f, f.Y, 200f, 34f), latest, 24, up.UpdateKnown ? Theme.Success : Theme.Text, TextAnchor.MiddleLeft, true);
                Painter.Text(new Rect(f.X + 400f, f.Y, f.Width - 400f, 34f), string.IsNullOrEmpty(up.State.lastCheck) ? "never" : up.State.lastCheck, 13, Theme.TextDim, TextAnchor.MiddleLeft);
                f.Y += 42f;

                Color statusColor = up.Status == UpdateStatus.Failed ? Theme.Danger
                    : up.Status == UpdateStatus.RestartToFinish || up.Status == UpdateStatus.InstallOnExit || up.Status == UpdateStatus.UpToDate ? Theme.Success
                    : up.Status == UpdateStatus.Available ? Theme.Accent : Theme.TextDim;
                string message = up.Message.Length > 0 ? up.Message : "Not checked yet this session";
                Painter.Circle(new Vector2(f.X + 6f, f.Y + 12f), 5f, statusColor);
                Painter.Text(new Rect(f.X + 20f, f.Y, f.Width - 20f, 24f), Painter.Fit(message, 13, f.Width - 20f), 13, Theme.Text, TextAnchor.MiddleLeft, true);
                f.Y += 30f;

                if (up.Status == UpdateStatus.Downloading || up.Status == UpdateStatus.Verifying || up.Status == UpdateStatus.Installing)
                {
                    Rect track = new Rect(f.X, f.Y, f.Width, 10f);
                    Painter.Rect(track, Theme.Surface, 5f);
                    float p = up.Status == UpdateStatus.Downloading ? up.Progress : 1f;
                    Painter.Rect(new Rect(track.x, track.y, Mathf.Max(10f, track.width * p), track.height), Theme.Accent, 5f);
                    Painter.Text(new Rect(f.X, f.Y + 12f, f.Width, 18f), Mathf.RoundToInt(p * 100f) + "%", 11, Theme.TextDim, TextAnchor.MiddleRight);
                    f.Y += 34f;
                }

                var labels = new List<string> { up.Busy ? "Working…" : "Check for updates" };
                bool canInstall = up.UpdateKnown && !up.Busy && (up.Status == UpdateStatus.Available || up.Status == UpdateStatus.Failed);
                if (canInstall) labels.Add("Install v" + up.LatestVersion);
                string rollback = up.RollbackCandidate;
                string rollbackVersion = rollback != null ? System.IO.Path.GetFileNameWithoutExtension(rollback).Substring("AdexansCastingMod-".Length) : null;
                if (rollback != null) labels.Add(confirm == "rollback" ? "Click again to roll back" : "Roll back to v" + rollbackVersion);
                labels.Add("Open releases page");

                int b = ButtonRow(f, 34f, labels.ToArray());
                string clicked = b >= 0 ? labels[b] : null;
                if (clicked == "Check for updates") up.CheckNow(true);
                else if (clicked != null && clicked.StartsWith("Install")) up.InstallLatest();
                else if (clicked != null && clicked.StartsWith("Roll back")) confirm = "rollback";
                else if (clicked == "Click again to roll back")
                {
                    up.Rollback();
                    confirm = null;
                }
                else if (clicked == "Open releases page") Application.OpenURL(UpdateConfig.ReleasesPage);
            }

            f.Section("Update settings");
            s.autoUpdateCheck = f.Toggle("Check for updates when the game starts", s.autoUpdateCheck, "Looks for a new GitHub release a few seconds after startup", true);
            s.updateInstallMode = (UpdateInstallMode)f.Choice("When an update is found", (int)s.updateInstallMode, InstallModes,
                "Automatic downloads, verifies and installs it; it's used after the next restart", (int)UpdateInstallMode.Automatic);
            SettingsChanged(f);
            if (f.Block("safety checksum backup repository"))
                f.Note("Updates only come from github.com/" + UpdateConfig.Repository + ", are checked with SHA-256 and the old version is backed up. Settings and presets are never touched.");

            string notes = up.ReleaseNotes;
            if (!string.IsNullOrEmpty(notes))
            {
                f.Section("What's new in v" + up.LatestVersion);
                if (f.Block("release notes changelog"))
                {
                    string trimmed = notes.Length > 2500 ? notes.Substring(0, 2500) + "…" : notes;
                    float h = Painter.WrappedHeight(trimmed, 12, f.Width);
                    Painter.TextWrapped(new Rect(f.X, f.Y, f.Width, h), trimmed, 12, Theme.TextDim);
                    f.Y += h + 8f;
                }
            }

            f.Section("Update history");
            if (!f.Block("history installed failed")) return;
            List<UpdateHistoryEntry> history = up.State.history;
            if (history.Count == 0) f.Note("No updates yet.");
            for (int i = history.Count - 1; i >= 0 && i >= history.Count - 10; i--)
            {
                UpdateHistoryEntry h = history[i];
                Color c = h.result == "Failed" ? Theme.Danger : h.result == "Installed" ? Theme.Success : Theme.Accent;
                Painter.Circle(new Vector2(f.X + 5f, f.Y + 13f), 4f, c);
                Painter.Text(new Rect(f.X + 18f, f.Y, 150f, 26f), h.date, 12, Theme.TextDim, TextAnchor.MiddleLeft);
                Painter.Text(new Rect(f.X + 170f, f.Y, 170f, 26f), "v" + h.fromVersion + "  →  v" + h.toVersion, 12, Theme.Text, TextAnchor.MiddleLeft, true);
                Painter.Text(new Rect(f.X + 340f, f.Y, f.Width - 340f, 26f), Painter.Fit(h.result + (string.IsNullOrEmpty(h.detail) ? "" : "  ·  " + h.detail), 12, f.Width - 340f), 12, c, TextAnchor.MiddleLeft);
                f.Y += 28f;
            }
            if (history.Count > 0 && f.Buttons("Clear history") == 0) up.ClearHistory();
        }
    }
}
