# Hearts, heart pickups and wider mazes: design

Date: 2026-10-02
Status: approved in chat

## Goal

The player has 3 hearts per level. Touching a wall or falling into a pit costs a heart. When the
hearts run out the level is failed. Heart pickups are placed through the maze and refill a heart.
Corridors are currently 1 tile wide around a ball 0.54 tiles across, which is too tight for a
"don't touch the wall" rule. Corridors become 2 tiles wide and every level keeps its layout.

## Decisions (from chat)

| Topic | Decision |
|---|---|
| What costs a heart | Any wall touch, regardless of speed, plus falling into a pit |
| Repeated hits | One heart per new contact, then 1.0 s of invulnerability. Resting or sliding against the same wall costs nothing more |
| Hearts | 3 at level start, max 3 |
| Out of hearts | Level-failed screen, "OUT OF HEARTS", RETRY / MAIN MENU, works like TIME UP |
| Retry | Full hearts, all pickups restored, attempt +1 |
| More room | Corridors 2 tiles wide (walls stay 1 thick), ball size unchanged |
| Pickups | Fixed tiles in the maze files: 1 pickup in L1, 2 in L2, 3 in L3. Placed on dead ends / side branches. Bob and pulse in place; they do not move along paths |
| Pickup at full hearts | Not collected; it stays for later |
| Score / stars | Unchanged |

## 1. Widened mazes

A new one-shot script `tools/widen_maze.py` reads the current 1-wide mazes from
`tools/maze_src/Level{1,2,3}.txt` (today's files, moved there unchanged) and writes
`GravityMaze/Mazes/Level{1,2,3}.txt`.

- **Rule:** every row index `i` with `i` odd and `0 < i < rows-1` is duplicated. Columns use the same
  rule. Every other line keeps a single copy. In the generated mazes, odd lines are cell/corridor lines
  and even lines are wall lines, so corridors become 2 wide and walls stay 1 thick. Level 1 has an even
  size (10x10), so its last line (border, index 9) is excluded by the `< rows-1` bound.
- **S and G:** stay a single tile. In the duplicated copy the tile becomes `0`, or `1` when the copy
  falls on the outer edge, so the edge stays wall-only.
- **I, F, H, M** are duplicated like floor tiles, so ice areas, boost strips and pits scale with their
  corridor. A pit still spans the corridor and can't be bypassed.
- **Resulting sizes:** L1 14x14, L2 31x31, L3 37x37.
- **Pickups:** new tile `L` (life), placed by the script. Candidates are floor tiles (`0`) in dead ends
  (cells with exactly one open neighbour in the source grid) that are off the shortest start-to-goal
  path. Rank candidates by detour length from the path, shortest first, and choose them evenly across
  that ranking (no RNG, deterministic). If there are too few dead ends, fall back to any off-path
  floor tile.
- The script runs once and its output is committed. The game never runs it.

## 2. Engine (`Engine/GameEngine.vb`, `Engine/GameState.vb`, `Levels/MazeDefinition.vb`)

- `MazeDefinition` accepts `L` as a valid symbol (floor with a pickup on it).
- `GameState.OutOfHearts` is added.
- `GameEngine` additions:
  - `Public Const MaxHearts As Integer = 3`, `Public Const InvulnerableMs As Single = 1000`
  - `Public Property HeartsEnabled As Boolean = True`. The attract-mode engine and the
    VerificationRunner keyboard bot set it to `False`; with `False` nothing about hearts changes.
  - `Hearts` (read-only), `IsInvulnerable` (read-only, for the blinking ball)
  - `Event HeartLost(HeartEventArgs)` with the ball position and remaining hearts, and
    `Event HeartGained(HeartEventArgs)` with the pickup tile centre and the new count
  - `IsPickupTaken(row, col)` so the renderer can skip pickups that are already taken
- **Wall hit:** happens wherever a `_inContactX` flag changes False to True (both X and Y branches),
  whatever the speed. The existing `WallImpacted` event keeps its speed threshold. If hearts are enabled
  and the ball is not invulnerable: hearts -1, invulnerable for `InvulnerableMs`, raise `HeartLost`.
- **Pit:** after the existing respawn logic, the same damage applies (it ignores invulnerability; a pit
  always costs 1).
- **0 hearts:** state becomes `OutOfHearts`, velocity 0. `Update` already returns early when the state
  isn't `Playing`.
- **Pickup:** the ball centre lies within 0.45 tiles of an `L` tile centre that isn't taken yet and
  hearts < Max: take it, hearts +1, raise `HeartGained`.
- **Time:** invulnerability counts down by `MsPerTick` each `Update`.
- `Reset()` restores full hearts, clears the taken pickups and clears invulnerability.

## 3. Screens and rendering

- **Sprites:** move the `hearts/` folder to `GravityMaze/Sprites/` and copy it to the build output
  (`Content Include="Sprites\**"`, PreserveNewest), like `Fonts`. Load the images once through a small
  shared loader. A missing file must not crash the game; fall back to drawing a red heart shape.
- **HUD (`GameShell.Draw.vb` `DrawHud`):** 3 hearts (32 px sprite scaled by `s`) under the level name on
  the left. A lost heart is drawn dimmed (about 30% alpha, or the empty frame from the sheet). The heart
  that was just lost briefly plays the spritesheet's draining frames.
- **Pickups (`MazeRenderer`):** draw an `L` tile as floor plus the heart sprite, gently bobbing and
  pulsing. Skip it once taken (ask the engine through `GameCanvas`, the same way as the ball position).
- **Hit feedback:** on `HeartLost` play a red screen flash, a short shake, and sound `wall_hit` loud
  (no new sound file needed). The ball blinks while `IsInvulnerable`.
- **Pickup feedback:** sparkle particles (existing `Particles`) and sound `menu_confirm` (or a new
  synthesized `heart` sound via `tools/gen_sounds.py` if that's easy).
- **New screen `ShellScreen.OutOfHearts`:** copy of TIME UP, with title "OUT OF HEARTS", subtitle
  "NO HEARTS LEFT", a tip line ("Tip: gentle tilts — walls cost hearts."), and menu RETRY / MAIN MENU.
  Keyboard: R / Enter on RETRY = Retry, Esc = main menu. Sound `time_up`.
- **How To Play:** add a legend entry for hearts and pickups.

## 4. Re-tuning

- The VerificationRunner keyboard bot (hearts disabled) re-measures the clear time of each widened
  level. Update par in `Form1.vb` for each level, and set the Level 3 time limit to 1.4x its new par,
  rounded up to whole seconds. This is the rule the existing comment describes.

## 5. Verification (`tools/VerificationRunner`)

- Existing checks updated for 2-wide corridors where they assume 1-wide ones.
- New checks:
  - the widened mazes load, have the expected sizes, and have a passable start-to-goal path
  - pickup counts are 1, 2 and 3
  - one wall contact costs exactly 1 heart; staying pressed against the wall for 0.5 s costs no more
  - after the invulnerability ends, a new contact costs one more heart
  - 3 hits gives `OutOfHearts`
  - a pit costs a heart and respawns the ball
  - a pickup at 2 hearts gives 3, and at full hearts the pickup isn't taken
  - `Reset()` restores hearts and pickups
  - `HeartsEnabled = False` never changes hearts
- UI tour screenshots include the HUD with hearts and the OUT OF HEARTS screen.

## Out of scope

Hearts affecting score or stars, hearts carrying over between levels, moving pickups, difficulty
settings.
