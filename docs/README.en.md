# RufMaker

**1.13.1** adds audio spectrograms, independent timeline row heights and object counts, and improves property undo and saving responsiveness. Run `RufMaker.exe`; see the [release notes](releases/v1.13.1.md) and [detailed controls](beta-bullet-control.md).

A native Windows timeline editor for **I Wanna avoidance games**, integrated with GameMaker 8.2.

Place emitters, formations, cameras and effects on a timeline, animate their properties with keyframes, then playtest and export a real game. Native GML, objects and room editing remain available.

## Getting started

You can animate a reusable object, formation, emitter or group, then enable **Template only**. On a source object, use **Object spawning…**; on flying bullets, add **Spawn template** in the bullet behavior editor. Choose a template, trigger age, repeat interval, index/parity filter, offset and optional inherited direction. **Destroy source after spawning** removes the mother while its children continue their own keyframes and motion. Saving a custom preset carries its referenced templates and sprites too.

Generated bullets can read `source.x / source.y`, a snapshot of the source center in world coordinates when generation was triggered. The snapshot includes movement and group transforms and stays fixed afterward. To keep a star while spawning an expanding copy, generate a one-shot template at every member, inherit direction, and use `point_distance(x,y,source.x,source.y)*0.02` in a speed behavior at bullet age 0 with duration 0. See the [star echo example](examples/star-echo.md). Before generation, parameter formula previews use the object's base X/Y as a reference source.

For an editable gallery, run `runtime/node.exe tools/examples/create-pattern-gallery.mjs`. It creates eleven eight-second chapters: periodic star copies, sequential looping member shots, stationary dots left by a moving bullet, counter-rotating spirals, aimed fans, seeded rain, stop/aim/dash, delayed blooms, tangent ring release, gap walls and heart pulses. Existing example folders are preserved. The [step-by-step tutorial](examples/pattern-gallery.md) is currently in Chinese; project data and native controls work in either UI language.

To repeat the expanding copy, set the source's spawn interval to 50 frames, duration to 0 and per-member delay to 0. Keep **Each bullet** and direction inheritance, leave both generation angle settings at 0, and keep **Destroy source after spawning** off. At 50 FPS this creates a new copy each second, using the source's current shape and center, until the source ends.

For sequential shots with equal speeds and increasing directions, make a separate one-shot template: start/end 0, count 1, fixed angle 0 and speed 3. Remove the distance-based speed behavior from the echo example. On the formation's spawn action choose **Each bullet**, set per-member delay to 3, turn direction inheritance off, and set the initial generation angle to 0 and angle increment per member to 10. Members 0, 1, 2… fire at 0, 10, 20… degrees, all at speed 3. A repeat interval of 0 fires each member once; a positive interval repeats the same direction sequence. Keep the source alive through `source start + trigger age + (member count - 1) × per-member delay`.

1. Download **`RufMaker-VERSION-Windows-Portable.zip`** from [Releases](https://github.com/wymcaT/RufMaker/releases/latest). This is the full Windows application. Extract everything, then run `RufMaker.exe`. A starter project is created from the template when needed.
2. Choose **English** from **切换语言 / Language** in the setup window or top bar. If GM8.2 is missing, click **Download and set up** in the engine setup window.
3. Create a project, choose a song and starting terrain, then add emitters or formations.
4. Adjust clips and property keyframes. Press **Space** to preview with music, or **F5** to enter the actual game at the playhead.
5. Use **Export game** and share its entire output folder, including the EXE, Data and required DLLs.

### GameMaker 8.2 setup

The native setup window downloads the **Portable** package through the [GM8.2 project's website](https://gm82.cherry-treehouse.com/), shows download/extraction progress, checks the Core, Sound and DirectX9 extensions, and places the engine in `GameMaker8.2/` next to RufMaker. The engine is downloaded on demand rather than redistributed inside the RufMaker ZIP.

Already have GM8.2? RufMaker detects saved locations, the adjacent `GameMaker8.2/` folder, `%APPDATA%/GameMaker8.2/` and registered installations. You can also **Choose existing folder** or **Import ZIP / 7z**. The selected path is remembered in `user-data/settings.json`. Command-line tools still accept `RUFMAKER_GAMEMAKER`.

Setup checks for the **VC++ x86 and DirectX9 runtimes**. If either is missing, its button downloads the official Microsoft installer and opens it for you. Finish installation, then click **Detect again**. Imported games may require additional extensions, DLLs and assets from their original engines.

You can choose **Skip and continue editing**. F5, game export, native recording generation and opening GameMaker require a configured engine; use **Engine settings** on the top bar when ready. You can retry a failed download or import a local archive. On older Windows versions without 7z extraction support, extract the archive manually and choose its folder.

Replacing a local engine preserves extra extensions and backs up the previous installation in `build/engine-setup/previous-*`. Setup diagnostics are in `build/logs/engine-setup.log`.

Existing RufMaker installations can use **Check for updates**. If current, a dialog says you are up to date. If a newer release exists, it displays the installed and latest version numbers and asks before downloading. Accepting saves your project, verifies the download, installs and restarts RufMaker. Declining continues editing. The `RufMaker-VERSION.zip` asset is the compatible automatic update package. Updates preserve projects, preferences, recordings, presets and your GM8.2 installation.

## Editing

- **Library:** objects, sprites, scripts, motion/effect/camera presets and shared custom presets.
- **Canvas:** Ctrl+wheel to zoom, middle-drag to pan, Ctrl+0 to fit the room.
- **Inspector:** click a number to type or drag vertically to adjust. Ctrl gives fine control; Shift speeds up adjustment.
- **Timeline:** drag clip centers to move them with their keys; drag edges to change boundaries. Different clip types can share a numbered track. Overlapping clips stack within the track so each remains selectable. Drag vertically or use the inspector/context menu to change tracks; right-click empty space to add or delete an empty track. Click its number to select all its clips; right-click to save them together as a custom preset.
- **Clip names and keys:** names appear on the clips. Click a clip’s ▸ to expand only its keyed properties. Other clips keep their own expanded state. Expanded clips hide keys on the main bar so you can still trim its edges.
- **Parameters:** without keyframes a parameter is global; after adding keys, edits affect its value at the playhead.
- **Animation buttons:** click ◇ beside an animatable parameter to create its first key at the playhead. Click ◆ to remove that property’s keys and keep the current value. Animation is off by default.
- **Selection:** drag empty space to select clips or keys; Ctrl/Shift adds to selection. Ctrl+C / Ctrl+V duplicates objects with all their keys.
- **Numeric dragging:** scale/opacity use 0.01 steps, positions/lengths use 1, and speeds use 0.1. Ctrl refines and Shift accelerates; the step stays stable as the value grows. Animated properties stay in their original rows with ◆ / ◇ indicators.
- **Navigation:** click timeline empty space, then tap Left/Right for one frame. Hold to accelerate reverse/forward playback and release to stop. This ignores snapping.
- **Preview speed:** choose 0.5x, 0.75x, 1x or enter a custom rate from 0.05 to 8. Music follows the speed, including reverse; pitch changes too. Game FPS and export timing are unchanged.
- **Snapping:** use the BPM grid or snap to keys on one selected track. Native GM code markers are supported. Hold Alt to bypass snapping.

A project consists of a `.rufmaker` file and its adjacent `GameMaker.gm82/` folder. Keep them together when moving or sharing a project. Music is copied into the project. Project and layout changes are saved automatically.

Dragging keys or moving them to the playhead keeps each key within its own clip. Multiple selected keys retain their spacing and stop together when any key reaches a clip boundary; clip duration stays unchanged.

Ctrl+C / Ctrl+V pastes clips at the current playhead. For multiple clips, the earliest start aligns with the playhead while the other clips, keys and release times retain their offsets. The project duration extends if needed. Spatial positions and parameter values are preserved.

Rainbow formations offer **Original order**, **Distance from center** and **Angle around center**, using the formation X/Y as the center. In distance mode, density sets color frames per pixel; in angle mode, density 1 maps a full turn to one rainbow cycle. Reverse, animation and keyed density/offset/speed remain available. Existing projects keep their previous color order.

Ellipse and other outline height ratios accept -10 to 10. Negative values flip the local height direction, and 0 flattens the outline into a line. Keys and expressions can animate through zero in both preview and exported games.

### Bullet lifetime and destruction effects

Select an emitter or formation and open **Bullet lifetime** in the inspector. It controls fired bullets, bullets fired by templates, and released formation members. Members still held in a formation follow its clip duration.

- **Fixed lifetime:** remove at the configured lifetime. A destruction transition runs during the final frames without extending that lifetime.
- **Destroy outside room:** trigger when the bullet center leaves the room, ignoring fixed lifetime.
- **Destroy outside view:** use the game camera's position, rotation and zoom, including a recorded native view when available. Editor pan/zoom does not change this boundary. Without a game camera it falls back to the room.

Choose **Shrink**, **Fade out**, or **Shrink and fade**, then set the transition length in frames. Boundary exits and RufMaker removal actions start this transition before removing the instance. Pending bullets keep moving, stop colliding and stop generating children. Native GML that directly destroys arbitrary instances does not automatically use these transitions.

Boundary checks use the bullet center and also apply at birth. A positive boundary margin expands the allowed area; a negative margin starts removal inside it. For example, -24 starts the transition 24 pixels before the edge so the effect remains visible. Existing projects retain fixed lifetime with no transition.

### Spectrogram and object counts

Right-click the audio area to choose **Waveform / Spectrogram**. The spectrogram shares the timeline's time axis, uses a logarithmic frequency axis in Hz, and shows stronger energy with greater brightness. The first analysis runs in the background over the entire song and is cached in the project's `cache/audio-spectrum/` folder.

Drag the audio area's bottom edge to enlarge it. Drag a track's bottom edge in the left label area to resize that track or property row independently; double-click to reset. The audio menu also offers common heights. These settings are saved with the workspace layout.

Open **Object counts** in the right sidebar, or click the status bar's `obj` total. It shows counts by GameMaker type and RufMaker source for the rendered preview frame, reusing the existing simulation. Source rows are a breakdown, not extra instances. Trails do not add instances; draw-only decorations are listed separately. Ordinary room instances are estimates because arbitrary native code may create or destroy more objects.

For imported projects, **Regenerate native recording** captures real native instance counts alongside the movie. Existing movies remain usable; without this metadata, the panel explicitly shows only RufMaker counts until you regenerate.

### Random parameters

Enable **Random** to the left of a numeric field, then enter an expression such as `random(360)`, `random_range(100,700)`, `choose(0,90,180,270)` or `400 + random_range(-60,60)`. Integer functions `irandom` and `irandom_range`, nested calls and arithmetic are supported. Keyframed parameters can have a separate expression at each key.

Each expression is sampled once. Scrubbing, playback, saving and reopening keep the same result. **Resample** on the top bar changes the preview seed and redraws all RufMaker random parameters; this is saved with the project and can be undone. Disabling Random keeps the current numeric value.

Under **Project settings → Random**, choose **Fixed (match preview)** for reproducible F5/export results, or **New seed on each play** for fresh samples when entering the avoidance. You can type a fixed seed manually. R restores the current playtest’s samples. Random visuals already baked into a native recording require regenerating that recording to change.

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

## Player expressions and homing bullets

Type a formula directly into a numeric field, or click **ƒx** to choose a template. The Expression toggle enables or disables formulas; the diamond still controls animation separately.

- `player.x` / `player.y`: real player coordinates in the game.
- `point_direction(x,y,player.x,player.y)`: aim from this object at the player, in degrees.
- `point_direction(100,100,400,304)`: direction between two points.
- `point_distance(x,y,player.x,player.y)`: distance to the player.
- `lengthdir_x(length,angle)` / `lengthdir_y(length,angle)`: directional components.
- `sin(time*2*pi)*30`: time-based oscillation; sin/cos use radians.
- `turn_towards(direction,point_direction(x,y,player.x,player.y),3)`: limit each turn to 3 degrees.

Available variables also include frame, age, speed, direction, fps, pi and x1/y1/x2/y2. Functions include min, max, abs, clamp, lerp, sqrt, floor and ceil, plus existing random functions. Self references in coordinate/endpoint fields use their base values; direction and appearance fields use the animated position and endpoints.

Expressions with player, coordinate or time variables run every frame. Random-only expressions keep a single sample. Add `+0*frame` to a random formula for deterministic per-frame samples.

Preview uses a fixed reference player: enable **Edit test player** and drag it, or enter coordinates under **Project settings → Expression preview**. Exported games read the actual player, falling back to the reference only when no player exists. Native film remains a recording; this reference only affects newly added RufMaker content.

For continuous homing, select an emitter and choose **Bullet flight → Flight mode**. Aim every frame, limit the turn rate, add a delay, or enter a per-frame direction formula. In flight formulas, x/y refer to each bullet and direction is its previous direction. Trails follow the curved path. The emitter’s angle formula controls newly spawned bullets; flight controls steer existing bullets.

In the ƒx dialog, enable **Fill a keyframe on every frame in the range** and enter an inclusive range. The same action is available in a clip’s context menu. It replaces only that property’s keys in the range, preserves live formulas and supports one-step undo. Each object supports up to 10,000 keys.

## Editing-style presets

Camera presets now include Speed zoom, Whip pan transition, Rotation rebound and Natural handheld drift. Drop onto a camera to replace its motion, or onto an empty area to create one. Enable Render camera to see it.

Preset motion intensity scales the motion (0 = original framing, 1 = default); it can be animated. Position, angle, zoom and directional blur retain independent keys. Mirror room edges fills exposed borders.

Effect presets include a combined RGB impact (zoom, chromatic split, echo and flash) and Glitch transition (slice displacement, RGB split and image stutter). Their controls remain editable. Stutter affects the rendered image while game logic continues.

The easing menu adds Edit · Smooth in/out, Fast start soft stop, Overshoot and settle, and Elastic settle. Newly created presets use a suitable duration; applying one to an existing clip fits its current span. Room changes and gameplay sections still come from the timeline or GML. All new presets work in preview and exported games and can be saved to the custom library.
