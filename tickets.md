# Tickets: Gravity Maze Development Roadmap

A tracer-bullet roadmap for continuing the Gravity Maze project from the
current prototype state. These tickets follow the project's architecture
rules and development workflow. The project must preserve separation
between Input Manager, Game Engine, Renderer, Maze Manager, and Arduino
systems.

Work the frontier: any ticket whose blockers are complete can start
immediately.

------------------------------------------------------------------------

## Complete Maze Rendering Foundation

**What to build:** the end-to-end maze viewing experience where a player
can launch the game and see the first maze level rendered correctly.

**Blocked by:** None --- can start immediately.

-   [ ] The application displays the first maze level successfully.
-   [ ] Maze data is loaded externally rather than hardcoded.
-   [ ] The maze scales correctly with the game window.
-   [ ] The metallic ball, start point, and goal are visually
    represented.
-   [ ] Rendering responsibilities remain separate from gameplay logic.

------------------------------------------------------------------------

## Implement Input Manager and Keyboard Control

**What to build:** the first player interaction flow where keyboard
input controls the marble through the Input Manager.

**Blocked by:** Complete Maze Rendering Foundation.

-   [ ] Keyboard input is converted into movement values.
-   [ ] The Game Engine receives input values without knowing the input
    source.
-   [ ] Input handling is isolated from physics and rendering.
-   [ ] The control system is prepared for future Arduino replacement.

------------------------------------------------------------------------

## Add Ball Physics Movement

**What to build:** a realistic marble movement system where the ball
responds with acceleration, velocity, and friction.

**Blocked by:** Implement Input Manager and Keyboard Control.

-   [ ] Ball movement uses physics-based calculations.
-   [ ] Momentum affects movement behavior.
-   [ ] Friction gradually slows the marble.
-   [ ] Movement values can be tuned for future levels.
-   [ ] Direct position movement is avoided.

------------------------------------------------------------------------

## Add Maze Collision System

**What to build:** a playable maze where the marble interacts correctly
with walls.

**Blocked by:** Add Ball Physics Movement.

-   [ ] The marble cannot pass through walls.
-   [ ] Collision response feels smooth.
-   [ ] Physics and collision logic remain inside gameplay systems.
-   [ ] Renderer only displays the result.

------------------------------------------------------------------------

## Implement Game State and Goal Completion

**What to build:** the complete gameplay loop where players start,
navigate, and finish a maze.

**Blocked by:** Add Maze Collision System.

-   [ ] Goal detection works when the marble reaches the target.
-   [ ] Level completion is tracked.
-   [ ] Timer functionality is supported.
-   [ ] Attempt tracking is supported.
-   [ ] Game state is managed independently.

------------------------------------------------------------------------

## Create Multi-Level Maze System

**What to build:** a level system supporting increasing difficulty and
special terrain mechanics.

**Blocked by:** Implement Game State and Goal Completion.

-   [ ] Level 1 Wooden Workshop is playable.
-   [ ] Level 2 Frozen Labyrinth supports ice zones.
-   [ ] Level 3 Neon Velocity supports fast zones and time pressure.
-   [ ] Maze files remain external.
-   [ ] Level progression is managed separately from rendering.

------------------------------------------------------------------------

## Add Arcade Visual Polish

**What to build:** transform the functional prototype into a visually
engaging arcade experience.

**Blocked by:** Create Multi-Level Maze System.

-   [ ] Each level has its own visual theme.
-   [ ] The same metallic ball is used across all levels.
-   [ ] Ball shadow is displayed.
-   [ ] Goal animation is displayed.
-   [ ] Collision and zone effects are visually represented.

------------------------------------------------------------------------

## Implement Score and Record Storage

**What to build:** a performance tracking system that records player
results.

**Blocked by:** Implement Game State and Goal Completion.

-   [ ] Player records are stored.
-   [ ] Completion time is saved.
-   [ ] Attempts are saved.
-   [ ] Best records can be retrieved.
-   [ ] XML storage is used.

------------------------------------------------------------------------

## Integrate Arduino Tilt Controller

**What to build:** replace keyboard simulation with physical tilt-based
control.

**Blocked by:** Add Arcade Visual Polish, Implement Score and Record
Storage.

-   [ ] Arduino sends processed tilt values.
-   [ ] Serial communication works.
-   [ ] Arduino remains responsible for sensor processing.
-   [ ] VB.NET handles gameplay physics and rendering.
-   [ ] Input Manager supports both keyboard and Arduino sources.

------------------------------------------------------------------------

## Final Testing and Demo Preparation

**What to build:** a complete playable Gravity Maze demonstration.

**Blocked by:** Integrate Arduino Tilt Controller.

-   [ ] All levels are playable.
-   [ ] Keyboard testing works.
-   [ ] Arduino testing works.
-   [ ] Records save correctly.
-   [ ] No major gameplay issues remain.
-   [ ] Project is ready for presentation.

------------------------------------------------------------------------

## Level 2: Interconnected Frozen Labyrinth Layout and Retuned Ice Terrain

**What to build:** An external 15x15 interconnected labyrinth maze definition for Level 2 named "Frozen Labyrinth" with loops and alternative routes: dual start exits (Down and Right) that continue into the maze and reconnect, 3 distinct Split-and-Rejoin sections distributed across the board (Upper, Mid-board, Lower) with zero 2x2 open rooms, sustained 3–5 tile ice sections with recovery and braking floor, 3 dead-end branches, and guaranteed ice traversal (`can_bypass_ice = False`). Retune ice physics (`IceFriction = 0.955F`) to achieve 2.36x coasting distance (2–3x target window) and 2.35x perpendicular turning drift, with instant return to normal friction (`0.90F`) on normal floor.

**Blocked by:** Create Multi-Level Maze System.

-   [x] External file `Mazes\Level2.txt` defines a 15x15 interconnected labyrinth with dual start exits, perimeter shortcut blocked, and exactly one 'S' at (1,1) and 'G' at (13,13).
-   [x] Start at (1,1) offers two immediate forward directions: Down (col 1) and Right (row 1), both continuing onward into the maze and reconnecting at Junction 1 (col 5, row 7).
-   [x] Maze features 3 distinct Split-and-Rejoin sections across the board:
    -   *Split 1 (Upper):* Dual start exits (West col 1 sustained 4-tile ice straight vs Northeast 4-tile ice bend) reconnecting at Junction 1 (col 5, row 7).
    -   *Split 2 (Mid-board):* High-risk 5-tile icy S-bend shortcut vs forgiving 4-tile normal-floor bypass around central wall island reconnecting at Junction 2 (col 7, row 11).
    -   *Split 3 (Lower):* Direct southern 3-tile ice straight approach vs winding lower-right normal-floor route reconnecting at Pre-Goal (col 12, row 13).
-   [x] Corridors are strictly 1-tile wide with zero 2x2 open rooms; all alternative routes separated by walls.
-   [x] Outer-edge perimeter shortcut is blocked by solid walls, requiring navigation through the interior labyrinth.
-   [x] Ice bypass impossibility verified: every start-to-goal path must encounter ice (`can_bypass_ice = False`).
-   [x] Contains 3 plausible dead-end branches: Northeast (depth 3), Northwest (depth 2), Southwest (depth 2).
-   [x] All 73 walkable tiles are 100% reachable from start.
-   [x] Ball collision clearance verified for every corridor and corner (`BallRadius = 0.27F`, clearance margin 0.230F on each side).
-   [x] All 12 simple start-to-goal routes successfully simulated with physics engine (shortest route 24 steps in 299 ticks / ~4.8s; longest route 42 steps in 555 ticks / ~8.9s) with zero wedging or snagging.
-   [x] Ice physics diagnosed and retuned: `IceFriction = 0.955F` produces 2.36x coasting distance (2.537 tiles vs 1.076 tiles on normal floor, 120 ticks vs 53 ticks) and 2.35x perpendicular turning drift; normal friction returns instantaneously on normal tiles.
-   [x] Resizing preserves exact alignment between drawing and collision geometry (sub-pixel error < 0.0001 px across viewports).

------------------------------------------------------------------------

## Level 2: Complete Frozen Theme and Readable Surfaces

**What to build:** A visual theme for Level 2 in `MazeRenderer` and `Form1` inspired by the concept art. Includes frosted blue walls with beveled highlights and shadows, matte pale blue-gray normal flooring with stone texture, glossy cyan ice zones with directional glints and fractures, dark navy outer background, and coordinated HUD. Preserves metallic silver marble and contact shadow. Does not regress Levels 1 or 3.

**Blocked by:** Level 2: Playable Frozen Labyrinth Layout and Functional Ice Terrain.

-   [x] Form background and HUD labels adopt coordinated cool navy and ice-blue styling on Level 2.
-   [x] Walls render as frosted blue blocks with bevel highlights and shadows.
-   [x] Normal floor renders as matte pale blue-gray stone with subtle grip texture.
-   [x] Ice zones render with glossy cyan gradient, bright directional glints, and surface fractures, visually distinct from normal floor in texture and color.
-   [x] Goal is a clearly distinguishable vibrant green/mint glowing portal.
-   [x] Metallic silver marble and contact shadow remain crisp and readable.
-   [x] Levels 1 ("Wooden Workshop") and 3 ("Neon Velocity") retain their proper themes with no regressions.

------------------------------------------------------------------------

## Level 2: Explicit Wall-Impact Feedback System

**What to build:** An event-driven collision feedback system where `GameEngine` detects meaningful wall impacts and exposes impact coordinates, normal, and speed, and `GameCanvas` / `MazeRenderer` renders a brief local white/cyan impact arc or flash.

**Blocked by:** Level 2: Playable Frozen Labyrinth Layout and Functional Ice Terrain.

-   [x] `GameEngine` exposes `WallImpacted` event with impact location, normal, and impact speed.
-   [x] Smooth sliding along walls is preserved; only the velocity component into the wall is resolved.
-   [x] No wall penetration occurs at maximum supported ball speed.
-   [x] A brief white/cyan flash or expanding arc displays at the contact point (120–200 ms duration).
-   [x] Effect scales modestly with impact speed.
-   [x] Repeated impact flashes are suppressed while resting or continuously pressing against a wall.
-   [x] Camera remains fixed; no damage, penalties, or attempt increments for ordinary wall impacts.

------------------------------------------------------------------------

## Level 2: Goal-Completion Feedback and Results Flow

**What to build:** Goal completion feedback and results flow. When the ball enters the goal, `GameEngine` transitions to `Completed` exactly once, stopping the timer and ball physics. An expanding green/mint celebration ring renders at the goal. A readable Level Complete UI panel appears with elapsed time, attempts, and functional Continue and Retry buttons. Continue advances level progression, Retry restarts with attempt tracking, and transient effects are cleared.

**Blocked by:** Level 2: Playable Frozen Labyrinth Layout and Functional Ice Terrain, Level 2: Explicit Wall-Impact Feedback System.

-   [x] Goal entry transitions engine state to `LevelComplete` exactly once.
-   [x] Gameplay timer and ball simulation stop immediately upon goal entry.
-   [x] Expanding mint-green goal ring celebration animation plays smoothly on the canvas.
-   [x] Level Complete panel displays elapsed time, attempt count, Continue button, and Retry button.
-   [x] Continue button advances to the next level in the level progression.
-   [x] Retry button resets the ball to the start tile, increments attempt count, hides the panel, and clears transient effects.
-   [x] Keyboard shortcuts (Enter for Continue, R for Retry) function identically.
-   [x] Completion events and record writes cannot repeat across frames.


------------------------------------------------------------------------

## Phase 1: Level 2 Redesign — Cobblestone Labyrinth and Slippery Ice

**What to build:** Replace the sparse 15x15 layout with a 21x21 maze styled after the cobblestone concept art, and make ice genuinely slippery.

**Blocked by:** None.

-   [x] `Mazes\Level2.txt` is 21x21, generated by `tools\gen_maze.py level2` (seeded, reproducible): 1-tile corridors and walls, entrance on the left edge, exit on the right edge.
-   [x] `MazeDefinition` allows S/G on the outer edge; the engine already treats off-grid as wall.
-   [x] Three 3x3 ice rinks sit on the solution route; each has one extra opening (one loop per rink). Wrong exits lead into dead-end subtrees.
-   [x] Two ice corridors (4+ tiles) end at a grippy corner.
-   [x] Every start-to-goal route must cross ice.
-   [x] Ice physics: `IceGrip = 0.30` (fraction of acceleration), `IceFriction = 0.985`, `IceMaxSpeed = 0.18`. Letting go at cruise speed slides ~5.9 tiles on ice vs ~0.8 on floor; full braking takes ~1.0 tile vs ~0.2.
-   [x] Keyboard-only bot (keys -1/0/+1) clears Level 2 in ~26 s with no softlocks.
-   [x] Frozen theme: frosted cobblestone walls, dark navy floor, bright cyan ice, entrance/exit arrows, no border line over the openings.
-   [x] Static board layer is cached to a bitmap and only redrawn on maze/theme/size change.
-   [ ] Play-test on real keyboard and, later, the Arduino board; retune `IceGrip` if partial tilt feels unplayable.

------------------------------------------------------------------------

## Phase 2: Level 3 Rework — Neon Velocity

**What to build:** A 25x25 generated maze with fast-zone boost corridors and holes.

**Blocked by:** Phase 1.

-   [x] `Mazes\Level3.txt` is 25x25 (`tools\gen_maze.py level3`, seed 4670), entrance left edge, exit right edge, straight-biased "highway" corridors.
-   [x] Four boost strips (`F`, max 6 tiles) end one grippy tile before a corner, with an overshoot pit (`H`) straight past the corner. Chevrons point toward the pit end.
-   [x] Two 5x5 hole plazas with a diagonal slalom of pits (the outer ring always stays open).
-   [x] Holes: ball centre within 0.40 tile of a hole centre respawns at start; timer keeps running; attempts unchanged; `GameEngine.BallFell` event for effects/SFX.
-   [x] Timer 40 s = 1.4x the keyboard bot's clear time (28.5 s, zero falls).
-   [x] No ice on Level 3.
-   [x] Neon rendering: dark wall slabs with glowing edges only on corridor-facing faces, glowing pits, amber chevron boosts, cyan frame with gaps at the openings.
-   [x] Wall-impact feedback made much more visible (all themes): struck block pulses, glowing wall face, double shockwave, theme-tinted sparks, bright flash; scales with impact speed.
-   [ ] Play-test the 40 s limit with real players; raise it if most runs time out.

------------------------------------------------------------------------

## Phase 3: Visual Polish and Sound Effects

**What to build:** Juice for all levels.

**Blocked by:** Phase 2.

-   [ ] Sound via WPF `MediaPlayer` (`UseWPF=true`), overlapping playback with per-sound volume.
-   [ ] Script-generated WAVs in `Sounds\`: wall hit (volume scales with impact speed), ice slide hiss, fast-zone whoosh, hole fall, goal chime, last-5-seconds timer beeps.
-   [ ] Visuals: ice spray particles, fast-zone speed trail, hole-fall shrink animation, level title card.
-   [ ] Level 1 unchanged; no background music.
