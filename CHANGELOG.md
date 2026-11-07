# Changelog

Each version's section is used as the GitHub release notes by `release.ps1`.

## v1.1.3

- Smoother spectator camera: other players' network stutter is filtered out ("Remove player stutter").
- The chase camera follows the direction of travel and leads a running player a little.
- The view widens slightly at speed ("Widen the view at speed").
- After a wall pushes the camera in, it eases back out instead of jumping.
- All of these can be adjusted on the Camera page.

## v1.1.2

- Fixed: the auto-updater couldn't see the DLL in GitHub releases ("The release has no AdexansCastingMod.dll file"), and update history wasn't saved.
- If you're on v1.1.0 or v1.1.1, install this version manually once; from here on updates install themselves.

## v1.1.1

- Fixed: with some fonts, italic text (player list names, the name in the lower third) slid sideways off its place, further the lower it was on screen.

## v1.1.0

- **Day & Time:** set the time of day on your PC with a 24-hour slider, presets (Sunrise, Morning, Noon, Sunset, Night, Midnight) or an exact time. It stays applied in every map until you switch it off.
- **Presets:** save your whole setup under a name, then load, overwrite, rename or delete it. Loading keeps your previous setup so you can undo.
- **Fonts:** a new font menu with Utopium (the game's font), Arial, Roboto, Inter, Poppins, Montserrat, Orbitron, Oswald, Bebas Neue, your installed system fonts, and your own .ttf/.otf files.
- **Auto-updater:** the mod now checks GitHub for new versions, verifies them with SHA-256 and installs them by itself.
- Settings: a Save button, tooltips, and smoother page changes.

## v1.0.0

- First release: spectator camera, Auto Pilot, match timer, scoreboard, layout editor.
