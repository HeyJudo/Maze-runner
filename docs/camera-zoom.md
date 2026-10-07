# Camera zoom

Zoom is shared by every maze, including future levels. It uses grid dimensions and
marble position rather than level numbers or themes. The preference lasts for the
current game session and carries through retries, practice levels, and campaign
progression.

- **Full Maze** preserves the original framing and is the default.
- **1.5x** and **2x** enlarge the board and marble together.
- Press **F9** to cycle settings, or pause and select **CAMERA** using the keyboard
  or Arduino tilt navigation.
- Hold **Tab** for an immediate full-maze overview; release it to ease back to the
  chosen zoom. Losing window focus releases the hold.

Closer views use a small movement allowance around the camera focus before
following, then ease toward the marble. At board edges, panning is clamped. When
an entire board axis still fits, that axis stays centered. Pit respawns and retries
recenter without sweeping through the maze. Menus and goal-drop transitions use
the full view; gameplay restores the selected zoom. HUD and pause menus stay at
their normal size and position. F8 tilt toggling works with each zoom setting.

## Validation

With .NET 10, from the repository root:

```sh
dotnet run --project tools/CameraChecks/CameraChecks.vbproj -c Release
dotnet run --project tools/TransitionChecks/TransitionChecks.vbproj -c Release
dotnet run --project tools/MarbleChecks/MarbleChecks.vbproj -c Release
dotnet run --project tools/HealthChecks/HealthChecks.vbproj -c Release
dotnet run --project tools/PerspectiveChecks/PerspectiveChecks.vbproj -c Release
```

Camera checks cover each shipped grid and arbitrary wide, tall, and square future
grids, multiple window sizes, tilt/flat framing, board corners, camera easing,
overview, preference retention, and retry tracking reset. Shell checks exercise
F9 repeat suppression, key release, Arduino pause-menu confirmation, overview
focus loss, and campaign/retry persistence without advancing paused physics.

On Linux, cross-build the game and the Windows verification runner with
`-p:EnableWindowsTargeting=true`. The actual desktop camera checks require Windows:

```sh
dotnet run --project tools/VerificationRunner/VerificationRunner.vbproj -- --camera-only
```

The tour checks magnification, fixed HUD, viewport clipping, immediate overview,
and preference retention. Snapshots for all four levels, both tilt modes, and
two window sizes are saved under `camera-checks` in the runner output directory.

Review the screenshots and play Level 2 in particular. Check camera comfort,
visibility at corners, resizing/fullscreen, view toggling, wall damage, pit
respawns, and campaign landings. Headless camera checks and a Windows cross-build
do not verify desktop drawing or how the camera feels during play.
