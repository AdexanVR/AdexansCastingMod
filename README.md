# Adexan's Casting Mod

A BepInEx spectator and casting overlay for competitive Gorilla Tag leagues. It adds a broadcast camera that follows any player, an Auto Pilot that switches away from tagged players, a match timer, a scoreboard and a fully editable on-screen layout.

The overlay and camera render only on the **desktop window** (the one you capture in OBS). The VR headset view is unchanged.

## Install

1. Build in Release (`dotnet build -c Release`). The build copies `AdexansCastingMod.dll` to
   `Gorilla Tag\BepInEx\plugins\AdexansCastingMod\` automatically.
2. Start the game, then join a **private room** with the players you want to cast.

Required: BepInEx 5. Utilla is not required.

## Fair-play rule

The spectator camera only works in **private rooms** (league/tournament codes). In public rooms it stays off and the panel shows "PUBLIC ROOM". The mod only reads room state that every client already has (`NetworkSystem`, `VRRigCache`, `GorillaGameManager.LocalIsTagged`). It never sends network events or changes gameplay.

## Default shortcuts (all rebindable under Settings → Keyboard Shortcuts)

| Key | Action |
| --- | --- |
| F6 | Settings menu |
| F7 | Layout editor |
| F8 | Show / hide the whole overlay |
| F9 | Show / hide caster controls (clean feed) |
| `.` / `,` | Next / previous player |
| 1 – 0 | Spectate player #1 – #10 in the list |
| P | Toggle Auto Pilot |
| C | Cycle camera mode (3rd person, 1st person, overhead, orbit) |
| V | Spectator camera on / off |
| T / Shift+T | Timer start-pause-resume / reset |
| `[` / Shift+`[` | Team A score +1 / −1 |
| `]` / Shift+`]` | Team B score +1 / −1 |
| N | Next round |

Shortcuts are ignored while you type in a text field.

## Features

- **Player casting**: click a player in the list or use the panel/shortcuts. The camera glides between players, or cuts when they are far apart. It keeps out of walls, and FOV, distance and smoothing are configurable.
- **Auto Pilot**: when the spectated player is tagged it waits a short delay, then picks the next untagged player. Rules: closest to a tagger, nearest to the camera, longest survivor, next in list, or random. It has a minimum hold time and avoids players you just watched. A manual pick pauses it for a set time. If the target leaves, a replacement is picked straight away.
- **Timer**: count down or count up, optional overtime (fixed or open-ended), four formats, a warning flash and sound, final-second beeps and an end buzzer.
- **Scoreboard**: team names, colors and scores, per-player team, score and tag count, round x of y, and match status (manual or following the timer).
- **Broadcast look**: a scorebug in the top right shows [score][TEAM][clock][TEAM][score], with a small box under the clock (spectated player's survival time, time since the last tag, or the round). The bottom-left list shows a number, a slanted color swatch (lava orange when tagged) and the name. Names float above players in their color.
- **Minimap**: a top-down orthographic camera over the spectated player. It sits under ceilings so caves and trees don't block it, has player markers and a heading arrow, and its refresh rate and resolution are adjustable. The shape can be rounded, circle or square.
- **PC only**: the spectator and minimap cameras have URP `allowXRRendering` turned off, so the headset view is never affected.
- **Broadcast effects**: animated banners for round start, round end or final, overtime and "last one standing", plus a wipe or fade effect when the camera cuts to another player.
- **Settings (F6)**: the Home page has the room join, live toggles, camera-style presets, the player list, the clock and team scores in one place. Every setting has a plain-language description. A changed setting shows a dot and a **Reset** button, the search box finds any setting by name, and Camera and Auto Pilot have one-click presets.
- **Layout editor**: drag to move, drag edges or corners to resize, and snap to the grid, guides and other elements. Each element can set its text size, colors, opacity, background style, alignment and corner radius. You can also reset single elements, and save, load or delete named layouts.

- **Fonts**: Utopium (the game's own font), Arial, Roboto, Inter, Poppins, Montserrat, Orbitron, Oswald and Bebas Neue are built in. Your installed system fonts and your own `.ttf`/`.otf` files (put them in `BepInEx/config/AdexansCastingMod/fonts`) are listed too. Each font is shown in its own typeface with a live preview, and the list has search and categories. A font that can't load is marked and the overlay falls back to Segoe UI.
- **Day & Time**: a 24-hour slider, presets (Sunrise, Morning, Noon, Sunset, Night, Midnight) and an exact-time box. It uses the game's own day/night system, only on your PC, and stays applied across maps until you turn it off.
- **Presets**: save your whole setup (optionally with the layout) under a name, then load, overwrite (with confirmation), rename or delete (with confirmation) it. Loading keeps the setup you had before, so you can undo. `settings.json` keeps autosaving on its own.
- **Auto-updater**: on startup the mod checks GitHub Releases. When a newer version exists it downloads it, verifies it (size, SHA-256, assembly name and version) and installs it. If the running DLL is locked, a helper swaps it right after the game closes. The previous DLL is backed up and can be rolled back from Settings → Updates.

## Publishing an update

1. Create a **public** GitHub repository and put its `owner/name` in `UpdateConfig.Repository` (`Core/Updater/UpdateModels.cs`).
2. Bump `PluginInfo.Version` in `Plugin.cs` and add a `## vX.Y.Z` section to `CHANGELOG.md`.
3. Run `powershell -ExecutionPolicy Bypass -File .elease.ps1`. It builds the DLL, checks its version, writes `AdexansCastingMod.dll.sha256` and creates the GitHub release with both files. It needs the GitHub CLI (`gh auth login`) or a `GITHUB_TOKEN`; add `-DryRun` to try it without publishing.

Players running v1.1.0 or newer then update by themselves. Anyone on v1.0.0 needs to install v1.1.0 manually once, because v1.0.0 has no updater.

## Files

Saved under `Gorilla Tag\BepInEx\config\AdexansCastingMod\`:

- `settings.json`: all settings and key bindings
- `match.json`: teams, scores, rounds and player records
- `presets/*.json`: named settings presets
- `updater.json`: update status and history
- `fonts/`: your own font files
- `current_layout.json`: the layout in use (autosaved)
- `layouts\*.json`: named layouts

## Code map

- `Core/PlayerTracker.cs`: room roster and tag state (join/leave events plus polling)
- `Core/CastDirector.cs`: who is spectated and the ordered player list
- `Core/AutoPilot.cs`: automatic switching rules
- `Core/SpectatorCamera.cs`: the desktop broadcast camera
- `Core/MatchTimer.cs`, `Core/MatchManager.cs`, `Core/Beeper.cs`: timer, scoreboard and sounds
- `Core/Shortcuts.cs`: key bindings (Input System)
- `UI/*`: IMGUI drawing, widgets, layout editor and settings menu
- `CastingRuntime.cs`: wires everything together; `Plugin.cs`: BepInEx entry point

## Font licenses

The bundled fonts are licensed under the SIL Open Font License 1.1; the license texts are in `Fonts/OFL-*.txt` and embedded in the DLL.
