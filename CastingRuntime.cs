using System;
using AdexansCastingMod.Config;
using System.Collections;
using AdexansCastingMod.Core;
using AdexansCastingMod.Core.Updater;
using AdexansCastingMod.UI;
using AdexansCastingMod.UI.Fonts;
using UnityEngine;

namespace AdexansCastingMod
{
    // Owns every subsystem and drives them from one Update/LateUpdate/OnGUI. Lives on its own
    // DontDestroyOnLoad object so scene changes or the BepInEx manager object can't take it down.
    internal sealed class CastingRuntime : MonoBehaviour
    {
        const float SaveDelay = 1f;

        public CastSettings Settings { get; private set; }
        public PlayerTracker Tracker { get; private set; }
        public CastDirector Director { get; private set; }
        public AutoPilot AutoPilot { get; private set; }
        public SpectatorCamera Camera { get; private set; }
        public MinimapCamera Minimap { get; private set; }
        public RigSmoothing RigSmoothing { get; private set; }
        public RoomJoiner Rooms { get; private set; }
        public Banners Banners { get; private set; }
        public Beeper Beeper { get; private set; }
        public MatchTimer Timer { get; private set; }
        public MatchManager Match { get; private set; }
        public LayoutManager Layouts { get; private set; }
        public HudRenderer Hud { get; private set; }
        public LayoutEditor Editor { get; private set; }
        public SettingsMenu Menu { get; private set; }
        public Shortcuts Shortcuts { get; private set; }
        public Toasts Toasts { get; private set; }
        public FontLibrary Fonts { get; private set; }
        public DayTimeController DayTime { get; private set; }
        public SettingsPresets Presets { get; private set; }
        public AutoUpdater Updater { get; private set; }

        public float TargetChangedAt { get; private set; } = -10f;
        public string TimerText { get; private set; } = "00:00";

        public bool SpectatingAllowed => !Tracker.InRoom || Tracker.IsPrivateRoom;
        public float HudScale => Screen.height / 1080f * Settings.hudScale;
        public float MenuScale => Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f) * Settings.menuScale;

        bool settingsDirty;
        float nextSaveCheck;
        bool cursorForced;
        bool savedCursorVisible;
        CursorLockMode savedCursorLock;
        bool initialized;
        string loadedFontId;
        float nextFontRetry;

        void Awake()
        {
            try
            {
                Settings = Storage.Load(Storage.SettingsPath, () => new CastSettings());
                Settings.Sanitize();
                MatchData matchData = Storage.Load(Storage.MatchPath, () => new MatchData());
                matchData.Sanitize();

                Toasts = new Toasts();
                Beeper = gameObject.AddComponent<Beeper>();
                Beeper.Init(Settings);
                Timer = new MatchTimer(Settings, Beeper);

                Tracker = new PlayerTracker(Settings);
                Match = new MatchManager(Settings, matchData, Timer, Tracker);
                Director = new CastDirector(Settings, Tracker, p => Match.ScoreOf(p));

                Camera = gameObject.AddComponent<SpectatorCamera>();
                Camera.Init(Settings, Director, () => SpectatingAllowed);
                AutoPilot = new AutoPilot(Settings, Tracker, Director, () => Camera.LivePosition);

                Layouts = new LayoutManager();
                Shortcuts = new Shortcuts(this);
                Hud = new HudRenderer(this);
                Editor = new LayoutEditor(this);
                Menu = new SettingsMenu(this);

                // Only render the minimap while it can actually be seen
                RigSmoothing = new RigSmoothing(Settings, () => Tracker.InRoom && Tracker.IsPrivateRoom);
                Rooms = new RoomJoiner(this);

                Banners = new Banners(this);
                Camera.Cut += () => { if (Settings.switchEffect != SwitchEffect.None) Banners.PlaySwitchEffect(); };

                Minimap = gameObject.AddComponent<MinimapCamera>();
                Minimap.Init(Settings, Director, Camera, () =>
                    SpectatingAllowed && (Editor.Active || (Settings.overlayVisible && Layouts.Get(WidgetIds.Minimap).visible)));

                Fonts = new FontLibrary();
                DayTime = new DayTimeController(Settings);
                Presets = new SettingsPresets();
                Updater = new AutoUpdater(this);
                if (Settings.autoUpdateCheck) StartCoroutine(StartupUpdateCheck());

                Director.TargetChanged += OnTargetChanged;
                Tracker.PlayerJoined += p =>
                {
                    if (Tracker.InRoom && !p.IsLocal) Toasts.Show(p.Name + " joined", Theme.Success);
                };
                Tracker.PlayerLeft += p =>
                {
                    if (Tracker.InRoom && !p.IsLocal) Toasts.Show(p.Name + " left", Theme.TextDim);
                };
                Tracker.RoomChanged += () =>
                {
                    if (Tracker.InRoom) Toasts.Show(Tracker.IsPrivateRoom ? "Joined private room " + Tracker.RoomName : "Public room: spectator camera disabled", Tracker.IsPrivateRoom ? Theme.Success : Theme.Warning);
                };

                initialized = true;
                Plugin.Log.LogInfo("Casting mod ready. Settings: " + Storage.Root);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Casting mod failed to start: " + e);
                enabled = false;
            }
        }

        void OnTargetChanged(CastPlayer prev, CastPlayer next, SelectSource source)
        {
            TargetChangedAt = Time.unscaledTime;
            if (next == null || !Settings.showToasts) return;
            if (source == SelectSource.AutoPilot) Toasts.Show("Auto Pilot → " + next.Name);
        }

        void Update()
        {
            if (!initialized) return;
            float dt = Time.unscaledDeltaTime;

            Tracker.Tick();
            RigSmoothing.Tick();
            DayTime.Tick();
            UpdateFont();
            Director.ScoreStamp = Match.ScoreStamp;
            Director.Tick();
            AutoPilot.Tick();
            Timer.Tick(dt);
            TimerText = Timer.DisplayText;
            Shortcuts.Tick();
            Editor.Tick();
            Hud.Tick(dt);
            UpdateCursor();
            SaveTick();
        }

        void OnGUI()
        {
            if (!initialized) return;
            GUI.depth = -1000;

            Event e = Event.current;
            // Clicking anywhere drops text-field focus; a clicked text field takes it back right after
            if (e.type == EventType.MouseDown) GUIUtility.keyboardControl = 0;
            Controls.TrackMouse();

            Painter.Init();
            Painter.MenuMode = false;
            Theme.Accent = Settings.accentColor;

            if (!Editor.Active)
            {
                Banners.DrawSwitchEffect();
                NameTags.Draw(this);
            }
            if (Editor.Active) Editor.Draw();
            else
            {
                Hud.Draw();
                Banners.DrawBanner();
            }
            Painter.MenuMode = true;
            Menu.Draw();
            // Notifications are for the caster only, so they stay off the clean feed
            if (Settings.showToasts && (Settings.casterControlsVisible || Menu.Open || Editor.Active))
                Toasts.Draw(MenuScale, Settings.animations);
            Painter.MenuMode = false;
        }

        // Keeps the overlay font in sync with the setting; a missing font falls back to the system font
        void UpdateFont()
        {
            Painter.MenuUsesBroadcastFont = Settings.menuUsesOverlayFont;
            string id = Settings.fontId;
            if (id == loadedFontId) return;

            // The game font only exists once the game has loaded it: keep retrying quietly for a minute
            if (id == FontLibrary.GameFontId && Time.unscaledTime < nextFontRetry) return;

            FontFace face = Fonts.Load(id);
            if (face == null)
            {
                FontEntry entry = Fonts.Find(id);
                if (id == FontLibrary.GameFontId && Time.unscaledTime < 60f)
                {
                    Fonts.Forget(id);
                    nextFontRetry = Time.unscaledTime + 3f;
                    Painter.SetBroadcastFace(null);
                    return;
                }
                Toasts.Show("Font \"" + (entry != null ? entry.Name : id) + "\" isn't available, using Segoe UI", Theme.Warning);
            }
            Painter.SetBroadcastFace(face);
            loadedFontId = id;
        }

        // Makes the font be (re)loaded on the next frame, e.g. after picking one in the font list
        public void ReloadFont()
        {
            loadedFontId = null;
            nextFontRetry = 0f;
        }

        IEnumerator StartupUpdateCheck()
        {
            // Give the game time to finish loading before going online
            yield return new WaitForSecondsRealtime(8f);
            Updater.CheckNow(false);
        }

        public void Execute(CastAction action)
        {
            switch (action)
            {
                case CastAction.ToggleSettings:
                    Menu.Toggle();
                    break;
                case CastAction.ToggleLayoutEditor:
                    Editor.Toggle();
                    break;
                case CastAction.ToggleOverlay:
                    Settings.overlayVisible = !Settings.overlayVisible;
                    Notify("Overlay " + (Settings.overlayVisible ? "shown" : "hidden"));
                    MarkSettingsDirty();
                    break;
                case CastAction.ToggleCasterControls:
                    Settings.casterControlsVisible = !Settings.casterControlsVisible;
                    Notify(Settings.casterControlsVisible ? "Caster controls shown" : "Clean feed (caster controls hidden)");
                    MarkSettingsDirty();
                    break;
                case CastAction.NextPlayer:
                    Director.Step(1);
                    break;
                case CastAction.PreviousPlayer:
                    Director.Step(-1);
                    break;
                case CastAction.ToggleAutoPilot:
                    AutoPilot.Enabled = !AutoPilot.Enabled;
                    Notify("Auto Pilot " + (AutoPilot.Enabled ? "ON" : "OFF"), AutoPilot.Enabled ? Theme.Success : Theme.TextDim);
                    MarkSettingsDirty();
                    break;
                case CastAction.CycleCameraMode:
                    Settings.cameraMode = (CameraMode)(((int)Settings.cameraMode + 1) % 4);
                    Notify("Camera: " + Settings.cameraMode);
                    MarkSettingsDirty();
                    break;
                case CastAction.ToggleSpectatorCamera:
                    Settings.spectatorCameraEnabled = !Settings.spectatorCameraEnabled;
                    Notify("Spectator camera " + (Settings.spectatorCameraEnabled ? "ON" : "OFF"));
                    MarkSettingsDirty();
                    break;
                case CastAction.TimerStartPause:
                    Timer.StartPauseResume();
                    break;
                case CastAction.TimerReset:
                    Timer.Reset();
                    Notify("Timer reset");
                    break;
                case CastAction.TeamAScoreUp:
                    Match.AddTeamScore(1, 1);
                    break;
                case CastAction.TeamAScoreDown:
                    Match.AddTeamScore(1, -1);
                    break;
                case CastAction.TeamBScoreUp:
                    Match.AddTeamScore(2, 1);
                    break;
                case CastAction.TeamBScoreDown:
                    Match.AddTeamScore(2, -1);
                    break;
                case CastAction.NextRound:
                    Match.NextRound();
                    Notify("Round " + Match.Data.round);
                    break;
                default:
                    if (action >= CastAction.SelectSlot1 && action <= CastAction.SelectSlot10)
                        Director.SelectSlot(action - CastAction.SelectSlot1);
                    break;
            }
        }

        void Notify(string text, Color? color = null)
        {
            if (Settings.showToasts) Toasts.Show(text, color);
        }

        public void MarkSettingsDirty() => settingsDirty = true;

        // Settings are saved shortly after the last change so dragging a slider doesn't write the file every frame
        void SaveTick()
        {
            if (Time.unscaledTime < nextSaveCheck) return;
            nextSaveCheck = Time.unscaledTime + SaveDelay;
            // Wait until a slider drag ends
            if (GUIUtility.hotControl != 0) return;
            SaveDirty();
        }

        void SaveDirty()
        {
            if (settingsDirty)
            {
                settingsDirty = false;
                Settings.Sanitize();
                Storage.Save(Storage.SettingsPath, Settings);
            }
            if (Match.Dirty)
            {
                Match.Dirty = false;
                Storage.Save(Storage.MatchPath, Match.Data);
            }
            if (Layouts.Dirty) Layouts.SaveCurrent();
        }

        public void SaveAll()
        {
            settingsDirty = true;
            Match.Dirty = true;
            Layouts.Dirty = true;
            SaveDirty();
        }

        public void ResetSettings()
        {
            var fresh = new CastSettings();
            fresh.Sanitize();
            // Copy field-by-field so every subsystem holding a reference to Settings sees the new values
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fresh), Settings);
            Settings.keyBindings = KeyBindings.Defaults();
            MarkSettingsDirty();
            Toasts.Show("Settings reset to defaults");
        }

        // The cursor must be usable while any caster UI is on screen; restore the game's state afterwards
        void UpdateCursor()
        {
            bool need = Menu.Open || Editor.Active || (Settings.overlayVisible && Settings.casterControlsVisible);
            if (need)
            {
                if (!cursorForced)
                {
                    savedCursorVisible = Cursor.visible;
                    savedCursorLock = Cursor.lockState;
                    cursorForced = true;
                }
                if (!Cursor.visible) Cursor.visible = true;
                if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            }
            else if (cursorForced)
            {
                Cursor.visible = savedCursorVisible;
                Cursor.lockState = savedCursorLock;
                cursorForced = false;
            }
        }

        void OnApplicationQuit()
        {
            if (!initialized) return;
            SaveAll();
            Updater.OnApplicationQuit();
        }

        void OnDestroy()
        {
            if (!initialized) return;
            SaveAll();
            DayTime.Release();
            Fonts.Dispose();
            Tracker.Dispose();
            RigSmoothing.RestoreAll();
            Painter.Dispose();
            if (cursorForced)
            {
                Cursor.visible = savedCursorVisible;
                Cursor.lockState = savedCursorLock;
            }
        }
    }
}
