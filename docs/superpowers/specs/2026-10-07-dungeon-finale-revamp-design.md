# Level 4 Revamp: The Forgotten Keep — Design

Date: 2026-10-07 · Onboarding decisions: 2026-10-08 · Branch: `feature/dungeon-revamp`

## Why

The current Level 4 is mostly solid fill (two thin wings plus a central column), draws
everything procedurally, and ignores the sprite sheets in `ASSETS/`. It feels smaller and
flatter than Level 3. The finale must feel like a dense, professional dungeon maze with a
dramatic ending, and it must be beatable on the Arduino tilt board (the primary controller).

## Decisions (agreed in brainstorming)

- No boss fight. Structure = **explore for 3 keys → final key triggers a collapse → escape**.
- Collapse = **chasing front + scripted cave-ins** that block the inbound route.
- Size **46×46** (31×31 source widened by `tools/widen_maze.py`, same 2-tile corridors as
  Level 3), **3 themed wings**, ~2:00 explore + ~0:45 escape.
- **Exit = entrance** (south). Sealed until key 3 is taken.
- Art = **sprite atlas + wall auto-tiling**. Raou sheets for structure/props, Timberwolf
  sheet for moving parts. Repo is private, so sheets are committed.
- Implementation runs as parallel subagent tracks (see the end of this spec).
- Level 4 teaches its objectives in stages, with a player-paced opening overview.
- Level 4 starts in **flat view**, then uses a **1.5× following camera** for play;
  players can select full-maze view or turn tilt view on themselves.

## 1. Layout and flow

```
┌──────────────────────────────────────────────┐
│             TREASURY VAULT (key V)           │  deepest point
│        gold piles · chests · banners · lava  │
│          portcullis c: needs keys K + Q       │
├──────────────┬───────────────┬───────────────┤
│  THE CRYPT   │  GRAND HALL   │  THE ARMORY   │
│  (key K)     │  (safe hub)   │  (key Q)      │
├──────────────┴───────┬───────┴───────────────┤
│   lower galleries: loops, side rooms, heart   │
│                S  G   entrance = exit (E)     │
└──────────────────────────────────────────────┘
```

1. Spawn at `S` (south). Exit `G` sits beside it behind exit gate `E` (closed).
2. Crypt (west) and Armory (east) in any order. Each is a dense maze wing: many corners,
   dead ends, ≥2 loops, ≥1 optional side room (heart pickup `L` or decor treasure).
3. Grand Hall (center) is trap-free. Three key-slot pedestals light as keys are collected.
4. Vault portcullis `c` (north) opens when K and Q are both held.
5. Taking key `V` starts the collapse; `E` opens; the vault portcullis `c` slams shut; a
   cracked vault wall (a sidecar `breakaway` tile group) turns to floor, opening the escape route.
6. Escape south through wing edges and lower galleries to `G`.

No tile of the 46×46 board is filler: every non-border region is part of a wing,
hub, gallery or escape route.

## 1a. Onboarding, objectives and camera (agreed 2026-10-08)

### Opening briefing

Every fresh entry to Level 4, from level select, next-level selection or the campaign
landing from Level 3, shows a player-paced briefing before the countdown. The campaign's
existing direct-to-play landing must not bypass it. The engine remains frozen: the marble,
elapsed run time, traps, crumble and collapse do not advance while reading the briefing
or during the countdown. Restarts and retries skip the briefing and use the countdown.

The briefing shows the whole keep in flat view. The entrance/exit, western Crypt,
eastern Armory and northern vault are labelled, and the maze geometry is readable behind
the briefing. It gives orientation without drawing a solved route. Its main instruction is:

> Find the keys in the Crypt and Armory, in either order.
> Together, they unlock the northern vault.

The opening does not explain the entire finale upfront. A `RULES` option provides the
hazard and checkpoint reference below. A `READY` option starts the existing countdown;
the briefing never dismisses on a timer. Both options use the existing `MenuList` input:
up/down selects, hold right confirms, and Enter is the keyboard shortcut. The menu is
reset on entry so an already-held tilt cannot accidentally dismiss the briefing.

Include a board-operable `CAMERA` choice on the briefing, defaulting to `1.5×` and
cycling through the existing full-maze / 1.5× / 2× modes. While reading, the board remains
in overview regardless of that choice. On Ready, the camera smoothly settles toward the
selected play zoom, focused on the entrance, during the countdown. Full-maze selection
remains full-maze. Keyboard Tab overview and F9 cycling remain available; the existing
pause-menu camera choice remains operable through tilt navigation. The Arduino supplies
tilt only, so this design adds no during-play gesture or firmware/button requirement.

### Objectives and key identity

Show a persistent objective derived from `DungeonRun.KeyMask` and `Collapse`, alongside
three labelled key icons: `CRYPT`, `ARMORY`, `VAULT`. Collected state uses both icon
appearance and a visible completion indicator. Map characters K/Q/V stay internal; the
player-facing instructions use names. Required objective states are:

| State | Objective |
|---|---|
| Neither Crypt nor Armory key held | Find the Crypt and Armory keys. |
| Crypt key held, Armory key missing | Find the Armory key in the east wing. |
| Armory key held, Crypt key missing | Find the Crypt key in the west wing. |
| Both held, no collapse | Vault unlocked — head north and take the vault key. |
| Collapse started | Escape through the new passage and return to the entrance. |

When both first keys are held, show a persistent warning before V is collected:

> Taking the vault key starts the collapse. Escape through the new passage
> and return to the entrance.

This warning is readable while approaching the key and also appears in paused objective
information. It does not introduce a confirmation or stop after the collapse has begun.

**Vault-door reveal (owner approved after playtest):** when the second of the Crypt and
Armory keys is collected, briefly show the newly unlocked vault door. The shell freezes
gameplay and all engine timers for a 2000 ms camera sequence: pan/zoom toward the door
at 2×, hold it clearly in view, then return to the marble and its prior preferred zoom
before controls resume. Include `VAULT UNLOCKED` feedback. This runs once per run, in
either key order; a fall does not replay it, and restart rearms it. Pausing or focus loss
freezes the reveal and resumes the same point in its sequence. This is a temporary reveal,
not a new player camera preference; retain manual flat/tilt and restore the chosen zoom.

On V collection, highlight the newly opened breakaway passage and announce
`ESCAPE PASSAGE OPENED — HEAD LEFT` for the rumble phase. On the shipped map, the opening
is in the vault's upper-left wall; the inbound door closes, and the player uses this new
opening to reach the Crypt-side escape route south to the entrance/exit. Keep `ESCAPE!`
and the destination visible through the rumble
and escape. Guidance does not draw a route through the maze.

### Checkpoints and rules

On the first checkpoint activation in each phase of a run (exploration and escape), show
`Checkpoint lit — falls return you here; collected keys stay.` for four seconds.
Later activations show `Checkpoint lit` for two seconds. These messages respond to the existing checkpoint
cue; a fall does not erase collected-key feedback or objective progress.

The briefing's Rules option and Level 4's pause screen explain:

- Walls cost a quarter heart; traps and falls cost one heart.
- Amber trap warnings indicate danger is approaching; wait on safe floor.
- Crumbling floor gives way shortly after contact; lava and open pits cause falls.
- Activated checkpoints set the return point, and collected keys survive a fall.
- During escape, the collapse front is the clock. Cracks warn of the approaching front;
  cave-in warnings precede rubble. The vault entrance closes and the new passage opens.
- Escape checkpoints replace the exploration return points during collapse. Before the
  first escape checkpoint, falls return to the vault key position.

The pause screen also shows the current objective and, when applicable, the pre-V warning.
Text must remain readable without overlapping hearts, time, key icons or the board controls.

### Flat view and camera lifetime

Each fresh Level 4 entry defaults tilt view to OFF. F8 and the existing pause-menu option
can turn it on. Preserve a player's manual view and zoom choices during that run, including
pause, falls, collapse, restart and retry. The collapse does not force another zoom change.
When leaving Level 4, restore the view/zoom preferences that were active before entry;
Level 4's defaults must not change Levels 1–3 or the menu attract views.

Keep the existing torchlit darkness during normal play. Check the collapse crack band,
breakaway-opening highlight and cave-in warnings in full-maze, 1.5× and 2× views, including
the optional tilt view. Adjust readability within the existing visual language if needed.
The plan's 6 ms live-layer target and section 4's 16 ms frame limit still apply.

### Responsibilities and acceptance

The shell owns briefing/rules navigation, countdown, transient messages and preference
restoration. A focused Level 4 presentation unit derives objective/warning copy from the
existing engine state; it must not hold a second key progression or alter gate rules.
The canvas/camera owns overview, zoom and flat/tilt presentation. Rendering of the new
passage highlight reads the existing breakaway/collapse state. No engine physics change
is needed for onboarding.

Wave 4 checks must cover fresh level-select and campaign entries, no briefing timeout,
keyboard and board-style confirmation after neutral input, frozen clocks before play,
countdown transition, objective updates in either key order, warning before V, checkpoint
messages, second-key door reveal/frozen timers/return/pause/restart, restart reset, and
view/zoom lifetime. Tours must show the briefing, rules, vault-door reveal,
each objective state, paused guidance, and escape-passage feedback at representative
window sizes. Validate existing Levels 1–3 transitions and view preferences as regressions.

## 2. Hazards

Marble top speed = 0.12 tiles/tick × 62.5 ticks/s ≈ 7.5 tiles/s.

| Wing | Hazard | Tile / data | Behavior |
|---|---|---|---|
| Crypt | Spikes | `T` + sidecar `spikes` | Unchanged 6 s cycle (warn 1.3–2.0 s, active 2.0–3.2 s). Safe waiting tile before each. |
| Crypt | Crumbling floor | `C` | Cracks on contact, becomes a pit 800 ms later, re-forms 6 s after. Only on side branches/loops so it never blocks the only route. |
| Armory | Saw blades | sidecar `blades` | Unchanged rail logic; animated saw sprite. |
| Armory | Darts | sidecar `launchers` | Unchanged warn-then-burst logic; arrow sprite. |
| Treasury | Lava | `W` | Behaves as a pit (`H`), drawn as animated lava. Beside the path, never across it. |

Damage unchanged: trap hit = 1 heart, wall contact = ¼ heart, 1 s protection after a hit.

**Pit respawn rule (all pits, lava, crumble, collapse front):** lose 1 heart, respawn at
the most recently lit checkpoint (`checkpoints` in the sidecar; lit on contact), defaulting
to `S`. Keys and opened gates are kept. One rule everywhere.

## 3. The collapse

Trigger: collecting `V`.

1. **Rumble — 4000 ms.** Camera shake, dust particles, `E` opens (gate cue), breakaway
   tiles become floor. The front does not move yet.
2. **Front.** On trigger, compute a BFS walking distance `d(tile)` from the `V` tile over
   passable tiles (breakaway open, `c` closed, every cave-in tile treated as wall, so the
   front can never shortcut through a corridor the player cannot use). Front reach
   `R = speed × (t − rumble) − pushback`, `speed = 3.0 tiles/s` (sidecar tunable).
   A tile with `d < R` is collapsed (pit). Tiles within `R ≤ d < R + 2` render as cracking.
3. **Cave-ins.** Sidecar list of tile groups, each with `atReach` (front reach that
   triggers it). Warning 1500 ms (cracks + dust), then the tiles become impassable rubble.
   If the marble overlaps a group when the warning ends, it waits until the marble leaves.
4. **Caught.** Marble's tile collapsed → 1 heart lost, respawn at the latest lit escape
   checkpoint (or the `V` tile if none is lit), and the front is pushed back until it is at
   least `pushback` (8) tiles behind the respawn point. 1 s protection applies. Out of hearts → existing
   game-over screen.
5. **Finish.** Reaching `G` → level complete → campaign victory (existing flow).

Rules: no hard escape timer (the front is the clock). Pause / focus loss freezes rumble,
front, cave-ins and crumble timers. Restart resets everything. One heart pickup `L`
roughly halfway along the escape route. Star par ≈ 180 s (tune after board playtest).

**Guarantee (validated):** in the post-collapse map, every tile reachable from `V` (that is
not itself a cave-in tile) can still reach `G` with every cave-in closed. No pocket can
trap the marble. Escape fairness is proven by the board-realistic
pilot runs in section 7, not by a distance formula.

## 4. Art and rendering

**Asset roles**

- `Dungeon.png` (Raou, 128×896): floor slab (sliced into 16 px floor variants), wall runs
  and corners, portcullis + raising-gate strip, wooden door, open arch, chains, wall
  torches, gold piles/coins, iron ball, tables, chairs, weapon racks.
- `free D.png` (Raou, 416×448): brick wall faces/tops, pillared archway, pit, pot, skull,
  banner, chest, bones, keg, pedestal.
- `maze_sprites.png` (Timberwolf, 16 px grid, 320×320): spike frames, saw frames, crumble
  frames, lava/water, key, buttons, arrow, blood decals, rubble. Tinted toward the Raou
  purple at load.

**Placement**

| Area | Assets |
|---|---|
| Entrance/exit | Portcullis, raising-gate frames on open, wall torches |
| Grand Hall | Pillared arch, banners, 3 key pedestals, chains, torches |
| Crypt | Skulls, bones, pots, dark arch, blood decals near spikes |
| Armory | Sword/spear racks, iron balls, kegs, tables/chairs, wooden doors |
| Treasury | Gold piles, coin scatters, chests, banners, key pedestal, lava |
| Collapse | Crumble frames for the front, pit tile, rubble for cave-ins, dust particles |

**Rendering**

- Walls auto-tile from a 4-neighbour mask (+ diagonals for inner corners) to a named piece:
  straight, outer corner, inner corner, end cap, T, cross, isolated.
- Nearest-neighbour scaling (`InterpolationMode.NearestNeighbor`, `PixelOffsetMode.Half`)
  at every camera zoom.
- Static layer (floors, walls, decor) goes into the existing `MazeRenderer` cache. Live layer
  (traps, crumble, lava animation, front, cave-ins, keys, gates, torch flicker) is drawn by
  `DungeonRenderer` each frame. Cave-ins and the front are overlays; the cache is never
  rebuilt mid-run.
- Atmosphere: darkness overlay with a light radius around the marble and torch glows
  (flicker). During collapse: red tint, shake, falling dust. The darkness overlay is built
  once per size and must keep the frame under 16 ms at 2× zoom on the dev laptop;
  if not, it falls back to a cheaper vignette.

## 5. Data formats

**Tiles (`Level4.txt`)** — existing: `0 1 S G H L T`. Dungeon tiles:

| Tile | Meaning |
|---|---|
| `K` | Crypt key |
| `Q` | Armory key |
| `V` | Vault key (triggers collapse) |
| `c` | Vault portcullis, opens with K + Q |
| `E` | Exit gate, opens on collapse |
| `T` | Spike tile (needs sidecar entry) |
| `C` | Crumbling floor |
| `W` | Lava (pit) |

Retired: `a`, `b` (seal shortcut gates). `MazeDefinition`'s dungeon-tile guard becomes
`"KQVcETCW"`.

**Sidecar (`Level4.dungeon.json`)** — keeps `spikes`, `blades`, `launchers`; replaces
`landmarks` with `decor` and `lights`; adds `checkpoints` and `collapse`:

```json
{
  "spikes":    [{ "row": 0, "column": 0, "offsetMs": 0 }],
  "blades":    [{ "x1": 0, "y1": 0, "x2": 0, "y2": 0, "offsetMs": 0, "periodMs": 6000 }],
  "launchers": [{ "x": 0, "y": 0, "dx": 1, "dy": 0, "offsetMs": 0 }],
  "decor":     [{ "sprite": "gold_pile", "row": 0, "column": 0 }],
  "lights":    [{ "row": 0, "column": 0, "radius": 3.5 }],
  "checkpoints": [{ "row": 0, "column": 0, "escape": false }],
  "collapse": {
    "rumbleMs": 4000,
    "speed": 3.0,
    "pushback": 8,
    "breakaway": [{ "row": 0, "column": 0 }],
    "caveIns": [{ "atReach": 12, "tiles": [{ "row": 0, "column": 0 }] }]
  }
}
```

Validation (before play): decor sprites exist in the atlas and sit on floor or wall as the
sprite requires; lights/checkpoints on floor; breakaway tiles are walls; cave-in tiles are
floor and never include `S`, `G`, `E` or a checkpoint; exactly one each of `K Q V`; the
reachability guarantee in section 3.

**Atlas (`Sprites/Dungeon/atlas.json`)** — `{ "name": { "sheet": "dungeon|freed|maze",
"x": 0, "y": 0, "w": 16, "h": 16, "frames": 1 } }`; multi-frame sprites are laid out
horizontally. Sheets copied to `GravityMaze/Sprites/Dungeon/` as `dungeon.png`,
`freed.png`, `maze.png` (already covered by the `Sprites\**` content rule).

## 6. Code units

| Unit | Responsibility |
|---|---|
| `Rendering/DungeonAtlas.vb` (new) | Load sheets + atlas once, tint the Timberwolf sheet, `Draw(g, name, rect, frame)`. Missing file → `Nothing`, falls back to procedural (like `Sprites.vb`). |
| `Rendering/WallAutotile.vb` (new) | Pure: neighbour mask → piece name. |
| `Engine/Collapse.vb` (new) | Pure collapse state: BFS distances, reach, cave-in timers/deferral, catch, pushback, escape checkpoints. Advanced with `Update(ms, marbleRow, marbleCol)`. |
| `Engine/DungeonRun.vb` | 3 keys, `c`/`E` gates, `C` crumble timers, checkpoint lighting, pit-respawn rule, owns a `Collapse`. Spikes/blades/darts unchanged. |
| `Engine/GameEngine.vb` | `W`/`C`-collapsed/front-collapsed treated as pits; cave-in rubble and closed gates as walls. |
| `Levels/DungeonDefinition.vb` | New schema + validation above. |
| `Rendering/DungeonRenderer.vb` | Rewritten for sprites, auto-tiled walls, overlays, lighting. |
| `UI` HUD and Level 4 presentation | Labelled key icons, state-derived objectives, briefing/rules, checkpoint feedback and "ESCAPE!" banner. |
| `Mazes/Level4.txt`, `.dungeon.json`, `tools/maze_src/Level4.txt` | New 46×46 layout. |
| `docs/dungeon-finale.md` | Rewritten for the new level. |

## 7. Testing

Extend `tools/DungeonChecks`:

- Auto-tile: every mask → an atlas name that exists.
- Atlas: every rect inside its sheet; every sidecar sprite exists.
- Collapse: BFS distances on a fixture, reach over time, catch → heart + respawn +
  pushback, cave-in deferral while occupied, pause freezes everything, restart resets.
- Keys/gates: `c` needs K+Q, `E` opens on V, retired `a`/`b` rejected.
- Onboarding and camera: the entry, input, frozen-clock, objective and preference cases
  in section 1a, including the campaign landing that previously skipped the intro.
- Reachability guarantee on the real Level 4 data.
- **Board-realistic proof runs:** `DungeonPilot` clears the whole level (explore + escape)
  with −1/0/+1 inputs fed through an exponential smoother (weight 0.35, the Arduino
  default) at 4 initial trap phases, each finishing with ≥ 1 heart.

Keep all existing check suites green (Transition, Camera, Health, Marble, Perspective).
Refresh `VerificationRunner --dungeon-only` and `--ui-only` screenshots.

Manual (owner): play on the Arduino board; tune front speed, par time, darkness readability.

## 8. Parallel implementation tracks

Section 5 is the shared contract, so the tracks can run concurrently:

1. **Art** — atlas JSON + sheet copies, `DungeonAtlas`, `WallAutotile`, `DungeonRenderer`.
2. **Engine** — schema/validation, `Collapse`, `DungeonRun`, `GameEngine` pit/wall rules, HUD.
3. **Level** — 31×31 source maze, widening, sidecar data (traps, decor, lights, checkpoints,
   collapse), meeting section 1 and 3 rules.

Then: integration, pilot proof runs, screenshots, docs.

## Out of scope

Boss enemies, new sound assets beyond reusing existing cues, changes to Levels 1–3,
Arduino firmware changes.
