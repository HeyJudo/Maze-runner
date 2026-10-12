# Level 4: Forgotten Keep

Forgotten Keep is the campaign finale: a 46 × 46 dungeon with two-tile corridors,
three themed wings, and an escape back to the southern entrance. Explore the Crypt
and Armory for their keys, unlock the northern Treasury Vault, then take its key
and escape the collapse. There is no enemy boss. The star par is 180 seconds;
exploration has no countdown, and the collapse front provides the escape pressure.

## Starting and playing

Every fresh Level 4 entry, including the campaign landing from Level 3, opens a
player-paced briefing. It shows the keep in flat full-maze view, with the southern
entrance/exit, western Crypt, eastern Armory and northern vault labelled. The
marble, run clock and hazards remain frozen while reading and during the countdown.

Use up/down tilt or keyboard navigation to select **READY**, **CAMERA** or **RULES**;
hold right to confirm, or press Enter. Return the board to neutral before confirming
after entry. The briefing never closes automatically. Rules are also available from
Level 4's pause menu. Ready starts the existing countdown and smoothly settles the
camera toward the entrance at the selected play zoom.

Level 4 defaults to flat view and a 1.5× following camera. Camera cycles full-maze /
1.5× / 2×; the briefing itself stays in overview. During play, hold Tab for overview,
use F9 to cycle zoom, and F8 to toggle tilt view. The pause menu's camera and tilt-view
choices work through board navigation. Manual choices persist through pause, falls,
collapse, restart and retry. Leaving Level 4 restores the view and zoom preferences
used before entry. No Arduino firmware change or additional button is required;
arrow/WASD controls remain usable.

1. Leave the southern entrance and explore the **Crypt** in the west and **Armory**
   in the east, in either order. The Grand Hall is a safe, trap-free hub. Loops,
   dead ends and optional heart detours surround the routes to the keys.
2. Collect both keys to open the northern vault portcullis. A **VAULT UNLOCKED**
   reveal freezes gameplay and engine timers for two seconds: 500 ms toward the
   door at 2×, a 1000 ms hold, then 500 ms back to the marble and preferred zoom.
   Pause or focus loss freezes the reveal at its current point. It happens once
   per run; a fall does not replay it, and restart rearms it.
3. Head north. Before the Vault key is taken, the HUD and paused objective warn:
   **Taking the vault key starts the collapse. Escape through the new passage and
   return to the entrance.** Use this time to orient yourself.
4. Taking the **Vault** key opens the exit gate, shuts the inbound vault door and
   opens the vault's **upper-left wall**. During the rumble, the opening is
   highlighted and the message reads **ESCAPE PASSAGE OPENED — HEAD LEFT**. Leave
   through this opening into the Crypt-side escape route and return south through
   the wing edges and galleries to the entrance/exit. **ESCAPE!** and the destination
   remain visible; no solved route is drawn.

The HUD uses labelled **CRYPT**, **ARMORY** and **VAULT** key icons with visible
completion indicators. Its objective changes with the keys actually held: find
both first keys, find the missing west/east key, head north to the unlocked vault,
then escape to the entrance. Completing Level 4 finishes the four-level campaign.
Campaign records distinguish four-level runs from saved three-level records.

## Hazards and recovery

Amber trap warnings mean danger is approaching: brake and wait on safe floor,
observe a cycle, then cross. The authored spike crossings have safe waiting space.

| Hazard | Behavior | How to respond |
| --- | --- | --- |
| Crypt spikes | Six-second cycle; amber warning at 1.3–2.0 s, active at 2.0–3.2 s, with authored phase offsets | Wait off the spike tiles until they retract |
| Crypt crumbling floor | Cracks on contact; becomes a pit after 800 ms and re-forms after six seconds open | Keep moving or use an alternate loop; crumble is on side branches, not the only route |
| Armory saw blades | Move along visible horizontal/vertical rails; six-second period with 600 ms endpoint dwell | Observe the moving blade and cross behind it |
| Armory wall darts | Amber launcher and dotted lane warn for 800 ms, then three shots fire 250 ms apart | Shelter behind stone and cross after the burst |
| Treasury lava / open pits | Cause a fall; lava sits beside the path | Keep to solid floor |
| Collapse front | Cracks warn two walking tiles ahead; floor behind the front becomes a pit | Continue toward the entrance |
| Cave-ins | Cracks and dust warn for 1500 ms before the tiles become rubble | Leave the warned area and use the remaining escape corridor |

Walls cost a quarter heart; traps and falls cost one heart. Trap/wall damage uses
the existing one-second protection. Falls still cost a heart during protection,
as other pits do. Heart pickups restore health. Running out of hearts uses the
existing game-over screen.

Touch a checkpoint to light it and set the return point for falls. **Collected
keys stay**, so a fall does not erase objective progress. Before collapse, the
latest exploration checkpoint applies, defaulting to the entrance. During escape,
only escape checkpoints apply; before the first one, falls return to the Vault key
position. An escape checkpoint cannot be activated early during exploration.
The first activation in each phase explains this rule for four seconds; later
activations show **Checkpoint lit** for two seconds.

Restart/retry starts a fresh attempt: three hearts, no keys, closed objective
gates, reset pickups, checkpoints, traps, crumble and collapse. It skips the
opening briefing and uses the countdown, retaining the run's camera choices.
Pause and focus loss freeze gameplay, including rumble, cave-in warnings,
crumble and the trap clock.

## Collapse and fairness

The Vault key starts a four-second rumble with shake and dust. The new passage
and exit gate open immediately, and the inbound vault gate closes. The front does
not advance during the rumble; afterwards it travels at **3.0 walking tiles per
second**. There is no separate hard escape timer.

The front follows shortest walking distances from the Vault key, computed on
the escape map with the new passage open, the inbound gate closed, and every
cave-in tile treated as a wall. It cannot take a shortcut through a corridor the
player cannot use. Six scripted cave-ins trigger at authored front reaches. A
cave-in waits if the marble still overlaps its tiles when the warning ends, then
closes after the marble leaves.

A front catch is a fall: lose one heart and return to the current escape return
point. Any fall during escape pushes the front back at least eight tiles and
keeps it at least eight walking tiles behind that point, preventing immediate
repeated catches on respawn. The map validator checks that non-cave-in tiles
reachable from the vault can still reach the goal with all cave-ins closed.
This is a connectivity guarantee, not a promise that a player can stand still
indefinitely. Board-smoothed pilot runs separately check timing and survivability.

## Artwork and licence evidence

The game uses a committed sprite atlas in `GravityMaze/Sprites/Dungeon/`:

| Source sheet supplied for the project | Committed output | Role |
| --- | --- | --- |
| Raou `Dungeon.png`, 128 × 896 | `dungeon.png` | Stone floors/walls, gates/arches, chains, treasure, furniture and weapon racks |
| Raou `free D.png`, 416 × 448 | `freed.png` | Pillared arch, torch, banner, chest, pot, skull, bones, keg and pedestal |
| TimberwolfGames `maze_sprites(1).PNG`, 320 × 320 | `maze.png` | Keys, arrows, crumble, lava and blood animation |
| Compositions from the supplied sheets | `extra.png` | Wall edges/corners, open gate, pit, rubble, reversed spike frames and toothed saw frames |

`tools/build_dungeon_atlas.py` removes the Timberwolf sheet's baked checkerboard
and tints it toward the dungeon palette. `atlas.json` names each rectangle and
horizontal frame strip. Walls auto-tile into the static board cache. Traps, keys,
gates, crumble, cave-ins, the front, opening highlight, lighting and atmosphere
read live engine state; rendering does not drive gameplay. Nearest-neighbour
sampling preserves the sprite pixels at every zoom, with procedural fallback
when the atlas cannot load.

The licence evidence checked on 2026-10-12 is the authors' own itch.io wording,
not a Creative Commons or other named licence:

- **TimberwolfGames:** the [FREE aMazing Maze Sprites page](https://timberwolfgames.itch.io/amazing-maze-sprites)
  says **“No credit needed”** and asks users not to redistribute the pack or claim
  it as their own for greed. The plan's shorthand is **no credit needed, no
  redistribution**. Use the artwork within this game; do not publish the supplied
  sheets or generated atlas as a standalone reusable asset pack.
- **Raou:** the [Mini-Tileset :: Dungeon page](https://raou.itch.io/free-dungeon-mini-tileset)
  says it is free to use as wished; an author reply also permits selling a game
  using it. Raou's [TDRPG Dungeon licence replies](https://raou.itch.io/dungeon-tileset-top-down-rpg/comments)
  permit editing and commercial use in multiple projects, while prohibiting
  resale or redistribution of raw tileset files. These statements are specific
  to those packs. The supplied filenames alone do not establish which download
  each Raou sheet came from; retain that provenance as an open documentation item,
  and do not assign a named licence or assume one pack's terms cover another.

The sheets are committed for this private project. That does not grant permission
to redistribute the raw assets. The generated dungeon WAV cues use no external
samples; they can be rebuilt with `python tools/gen_dungeon_sounds.py`.

## Tile and sidecar reference

`GravityMaze/Mazes/Level4.txt` is the collision grid. `MazeManager` loads the
matching `Level4.dungeon.json` automatically. The generator widens a 31 × 31
source into the committed 46 × 46 grid, duplicating odd interior rows/columns
to create two-tile corridors. The source lives in `tools/maze_src/Level4.txt`.

| Tile | Meaning |
| --- | --- |
| `1` / `0` | Wall / normal floor |
| `S` / `G` | Southern start / goal beside the entrance |
| `H` / `L` | Pit / healing heart |
| `K` / `Q` / `V` | Crypt / Armory / Vault key; `V` triggers collapse |
| `c` | Vault portcullis: opens with `K` + `Q`, closes on collapse |
| `E` | Exit gate: opens on collapse |
| `T` | Spike tile; requires one unique sidecar spike entry |
| `C` / `W` | Crumbling floor / lava pit |

The old `a`/`b` seal shortcut gates are retired and rejected. The map requires
exactly one each of `K`, `Q`, `V` and at least one `E`.

Sidecar tile positions use zero-based `row`/`column`; continuous trap coordinates
use `x` = column and `y` = row, with tile centres at index + 0.5. Times are
milliseconds; radii, rails and front reach are in tile units.

| Field | Entry fields and purpose |
| --- | --- |
| `spikes` | `row`, `column`, `offsetMs`: positions and cycle phases |
| `blades` | `x1`, `y1`, `x2`, `y2`, `offsetMs`, `periodMs`: cardinal rails and motion |
| `launchers` | `x`, `y`, `dx`, `dy`, `offsetMs`: cardinal direction and burst phase |
| `decor` | `sprite`, `row`, `column`: named atlas props |
| `lights` | `row`, `column`, `radius`: torchlight sources |
| `checkpoints` | `row`, `column`, `escape`: exploration/escape return points |
| `collapse` | `rumbleMs`, `speed`, `pushback`, `breakaway`, `caveIns` |
| `collapse.breakaway` | Wall positions that become floor when `V` is taken |
| `collapse.caveIns` | `atReach` and `tiles`: front threshold and warned rubble group |

The shipped sidecar has eight spikes, three blade rails, three dart launchers,
two exploration and three escape checkpoints, six cave-ins, 112 decor entries
and 40 lights. Its breakaway tiles are `(row 1, column 15)` and `(row 2, column 15)`.
Defaults remain rumble 4000 ms, front speed 3.0 and pushback 8.

Validation runs before play: spike entries match `T` uniquely; blade tracks stay
on floor with distinct cardinal endpoints and periods of at least four seconds;
launchers have cardinal directions; breakaways are walls; cave-ins are floor and
cannot bury start, goal, exit gate or checkpoints. The atlas/decor checks verify
sprite names and rectangles, and level checks cover placements and escape
connectivity. Engine classes own keys, gates, clocks, projectiles and collisions;
the shell owns briefing, feedback and temporary camera presentation.

## Regeneration

From the repository root, with Python 3 and Pillow installed:

```sh
python tools/build_dungeon_atlas.py <scratch-dir> <assets-dir>
python tools/gen_level4.py
```

The atlas command needs the three original filenames in the source-sheet table;
`<assets-dir>` defaults to the untracked `ASSETS/` directory. It writes the four
committed sheets and `atlas.json`, plus `<scratch-dir>/atlas_contact.png` for
inspection. Use a scratch directory outside the repository. The level generator
searches from seed 0 by default (an optional first seed can be supplied), then
writes the source, widened map and sidecar after its placement checks pass.
`tools/widen_maze.py` regenerates Levels 1–3; use `gen_level4.py` for this finale.
The game never runs these Python scripts. Inspect generated diffs and the contact
sheet before accepting output; preserve any owner-edited assets/screenshots.

## Validation commands and recorded evidence

With .NET 10, run from the repository root:

```sh
dotnet run --project tools/DungeonChecks/DungeonChecks.vbproj -c Release
dotnet run --project tools/TransitionChecks/TransitionChecks.vbproj -c Release
dotnet run --project tools/CameraChecks/CameraChecks.vbproj -c Release
dotnet run --project tools/HealthChecks/HealthChecks.vbproj -c Release
dotnet run --project tools/MarbleChecks/MarbleChecks.vbproj -c Release
dotnet run --project tools/PerspectiveChecks/PerspectiveChecks.vbproj -c Release
dotnet build GravityMaze/GravityMaze.vbproj -c Release
```

The reviewed proof integrated at `f778cf8` recorded **1777 DungeonChecks**. Four
full live runs at initial trap delays 0, 592, 2000 and 4000 ms completed in
**153.184–153.392 seconds**, with **48.640–48.848 seconds** from Vault key to exit.
Each finished with **one heart**, two recorded hits and zero collapse-phase falls.
They use −1/0/+1 tilt commands smoothed at weight 0.35, with health, real physics
and all hazards enabled; no marble teleports or disabled damage establish these
clears. The front speed remains **3.0 tiles/s**.

A separate static connectivity witness contains **429 segments** from key
collection through escape, avoiding all three full blade collision envelopes,
with gate/breakaway rules and every cave-in closed during escape. The live
pilot's selected route/travel diagnostics overlap rail 3's full static envelope.
That is an envelope-clearance observation, **not evidence of an actual timed
blade collision**. Static connectivity and live survivability are separate checks.

On Windows, generate the presentation tours:

```sh
dotnet run --project tools/VerificationRunner/VerificationRunner.vbproj -c Release -- --dungeon-only
dotnet run --project tools/VerificationRunner/VerificationRunner.vbproj -c Release -- --ui-only
dotnet run --project tools/VerificationRunner/VerificationRunner.vbproj -c Release -- --camera-only
```

The tours must cover briefing/rules, each objective, the paused pre-V warning,
vault-door reveal, checkpoint feedback, opening highlight, rumble, front cracks,
cave-ins and escape, at representative window sizes and camera/view settings.
Inspect the saved frames; do not treat screenshot generation alone as visual
approval. Stage representative `screenshots/level4_*.png` individually after
comparison with existing owner edits.

### Pending final integration validation

The following evidence has not yet been entered here; the earlier proof results
do not imply these checks have passed on the final integrated tree:

- Final suite counts and Release game/runner build results: **pending**.
- Refreshed dungeon/UI tour counts, fixture paths and visual review: **pending**.
- Idle performance at 2× after warmup: **pending**. Record window size, rendered
  states, sampled frames, live-layer and full-frame median/upper percentiles.
  Targets are 6 ms for the live layer and 16 ms for the full frame. Measure
  serially with builds/tests/agents inactive; record any fallback used.
- Original download provenance for the two supplied Raou sheets: **pending**.
- Arduino owner playtest and acceptance: **pending**.

## Owner playtest

1. Connect the Arduino, hold it level for calibration (C re-centres), and enter
   Level 4 from both level select and a Level 3 campaign completion. Confirm the
   briefing waits, its map labels are readable, and held input cannot dismiss it.
   Open Rules, cycle Camera using tilt, choose Ready and check the frozen countdown.
2. Play the first keys in both orders across attempts. Check objectives and
   labelled completion icons, two-second vault reveal, frozen traps/run clock,
   pause/focus-loss during that reveal, restored camera, and the pre-V warning.
3. Light an exploration checkpoint and fall. Check the one-heart loss, return
   location and retained keys. Take V, read **HEAD LEFT**, use the upper-left
   opening and check the inbound door closes and southern exit opens.
4. Follow the escape at the unchanged front speed. Judge warning visibility,
   safe waiting space and turn/braking comfort. Check crumble, lava, spikes,
   saw silhouettes/rails, dart lanes, cave-in deferral, the escape heart and
   checkpoint returns. Before the first escape checkpoint, a fall returns to
   the vault; afterwards it returns to the latest escape checkpoint with a
   pushed-back front. Keys remain collected.
5. Pause or lose focus during rumble/cave-in warnings, resume, restart during
   escape and retry after game over. Check timer freezing, complete gameplay
   reset, skipped repeat briefing and retained manual view/zoom choices.
6. Check full-maze / 1.5× / 2× in flat and optional tilt view, including the
   opening highlight and collapse crack band. Resize/fullscreen and check text
   does not overlap hearts, time, key icons or controls. Leave Level 4 and verify
   earlier levels restore prior preferences; finish to campaign victory.

Record completion time, falls/hits, remaining hearts and any confusing turn or
unreadable warning. Automated proof establishes a workable route; the owner
playtest determines board-control comfort, darkness readability and whether the
180-second par feels appropriate.
