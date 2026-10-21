# RufMaker

A native Windows timeline editor for **I Wanna avoidance games**, integrated with GameMaker 8.2.

Place emitters, formations, cameras and effects on a timeline, animate their properties with keyframes, then playtest and export a real game. Native GML, objects and room editing remain available.

## Getting started

1. For a first installation, download the [complete repository ZIP](https://github.com/wymcaT/RufMaker/archive/refs/heads/main.zip), extract everything, then run `RufMaker.exe`.
2. Choose **English** from **切换语言 / Language** on the top bar. The setting applies immediately and is remembered.
3. Create a project, choose a song and starting terrain, then add emitters or formations.
4. Adjust clips and property keyframes. Press **Space** to preview with music, or **F5** to enter the actual game at the playhead.
5. Use **Export game** and share its entire output folder, including the EXE, Data and required DLLs.

Compiling, playtesting and exporting require GameMaker 8.2 with the extensions used by the game's engine. RufMaker checks `GameMaker8.2/GameMaker.exe`, then `%APPDATA%/GameMaker8.2/GameMaker.exe`. Set `RUFMAKER_GAMEMAKER` to the full EXE path for another installation.

Existing installations can use **Check for updates** or download the [latest update](https://github.com/wymcaT/RufMaker/releases/latest). Release ZIPs update an existing installation and do not include the demo project.

## Editing

- **Library:** objects, sprites, scripts, motion/effect/camera presets and shared custom presets.
- **Canvas:** Ctrl+wheel to zoom, middle-drag to pan, Ctrl+0 to fit the room.
- **Inspector:** click a number to type or drag vertically to adjust. Ctrl gives fine control; Shift speeds up adjustment.
- **Timeline:** drag clip centers to move them with their keys; drag edges to change boundaries. Expand a track to edit each keyed property separately.
- **Parameters:** without keyframes a parameter is global; after adding keys, edits affect its value at the playhead.
- **Selection:** drag empty space to select clips or keys; Ctrl/Shift adds to selection. Ctrl+C / Ctrl+V duplicates objects with all their keys.
- **Navigation:** click timeline empty space, then tap Left/Right for one frame. Hold to accelerate reverse/forward playback and release to stop. This ignores snapping.
- **Preview speed:** choose 0.5x, 0.75x, 1x or enter a custom rate from 0.05 to 8. Music follows the speed, including reverse; pitch changes too. Game FPS and export timing are unchanged.
- **Snapping:** use the BPM grid or snap to keys on one selected track. Native GM code markers are supported. Hold Alt to bypass snapping.

A project consists of a `.rufmaker` file and its adjacent `GameMaker.gm82/` folder. Keep them together when moving or sharing a project. Music is copied into the project. Project and layout changes are saved automatically.

## Importing a non-RufMaker game

Choose the **complete GameMaker 8.2 text project folder**, including its `.gm82` entry, resource folders, Data, music and DLLs. Convert older binary GMK/GM81 projects to GM8.2 text format first. Confirm that the original project runs in its own engine with all extensions installed.

Use **Import non-RufMaker project**, then check the suggested avoidance room, controller and song. Import creates a copy; the source is unchanged. Imported rooms use **50 FPS**, so check timing if the source used another rate.

On first opening, RufMaker runs the native avoidance from the beginning and records a seekable background. Initialization and historical variable assignments execute in order. During recording, the player is fixed at **(400,304)**, movement and keyboard input are disabled, and the standard killPlayer path is bypassed.

The native recording is stored in `cache/native-film/base.rufvideo` and reused on later opens. RufMaker additions render live over it. Aimed patterns in the recording target the fixed player, so actual play can differ.

**After changing original GML, click Regenerate native recording.** Failed or cancelled generation keeps the previous complete recording. Older recordings may need one regeneration to include native camera metadata.

The **GM project · Native** track shows detected code events. Double-click a marker to edit that event's block; right-click to open the entire source. Detection does not convert arbitrary GML into editable UI keyframes. Review import warnings for clocks or expressions that could not be resolved.

F5 sequentially replays to the requested time before enabling player control. Enable **Edit test player** to set the starting position. In the playtest, **R** restores the test snapshot in the same process. Exported games retain the original engine's R/save behavior.

If recording fails despite detected events, check that the original game runs, verify DLLs/extensions, then read the logs in the directory shown by the error. Typical files include `compiler.log` and `game_errors.log`. The import report is `native-import-report.json` beside the new project.

## Folders

| Folder | Purpose |
| --- | --- |
| `desktop/` | Native interface, preview, localization and build scripts |
| `studio/` | Project synchronization, code generation and native compatibility |
| `assets/` | Icons, localization, terrain, source sprites and sprite packs |
| `templates/` | GameMaker engine templates |
| `runtime/` | Bundled runtime libraries |
| `projects/` | Editable projects |
| `docs/` | Guides and development notes |
| `user-presets/` | Shared personal presets; preserved during updates |
| `user-data/` | Local preferences |
| `build/` | Build results, logs and temporary recordings |
| `references/` | Local reference material |

The rainbow Cherry source is in `assets/sprites/sprrainbowcherry.gmspr`. The app icon is in `assets/icons/`. Older installations have their shipped assets arranged automatically on startup after updating.

Code is under the [MIT license](../LICENSE). Third-party engines, assets and runtime libraries retain their authors' rights and licenses.
