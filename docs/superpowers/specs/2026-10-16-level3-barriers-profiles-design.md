# Level 3 Overdrive Barriers, Player Profiles & Run Journal — Design

Date: 2026-10-16 · Branch: `feature/dungeon-revamp`

## Goal

1. Replace Level 3's countdown (not allowed by the student) with a speed-based twist.
2. Rebuild XML storage around player profiles and a journal of every attempt, with
   per-profile campaign progress and a ghost of the player's best run.
3. Add quality-of-life features: instant restart, calibration screen, auto-pause on
   controller disconnect.

All Project 16 requirements stay met: the level stopwatch still starts at entry and
stops at the goal; XML gains full "attempts per level" (failed and quit runs count).

## Controls rule

Every new screen is navigable with the Arduino (tilt up/down to move, hold right to
confirm) and keyboard (arrows/WASD, Enter, Esc), via the existing `MenuList` pattern.
Only typing a new player name requires the keyboard. No Arduino firmware change.

## 1. Level 3 — Neon Velocity: Overdrive Barriers

- Remove the time limit: Level 3 `LevelConfig` time limit becomes 0. The stopwatch,
  par time and stars remain. The `TimeUp` path stays in code only if another level
  uses it; otherwise it is deleted.
- New tile `B` = Overdrive Barrier (neon force field).
  - Ball speed at contact **≥ threshold** → barrier shatters (particles + sound),
    tile becomes floor for the rest of the run.
  - Ball speed **< threshold** → normal wall bounce, "TOO SLOW" flash, normal wall
    damage (¼ heart, existing 1 s protection). No extra penalty.
  - Charge glow: barriers brighten as ball speed approaches the threshold so the
    player can read whether they will break through.
- `F` boost lanes are the intended run-ups; some barriers need a long straight instead.
- Broken barriers persist through hole falls/respawn; restart rebuilds them all.
- New `Level3.txt` (25×25) with 4–6 barriers. Threshold and par are measured from
  pilot runs, not guessed.
- Validator check: every barrier on the required route has a reachable run-up long
  enough to exceed the threshold.
- UI: intro card "BREAK THE BARRIERS" replaces "BEAT THE CLOCK"; HUD shows
  "Barriers n/total" instead of the countdown bar.
- Gameplay state lives in the engine; the renderer only reads it (AGENTS.md rules).

## 2. XML v2 — profiles, run journal, progress, ghosts

Same file (`%AppData%\GravityMaze\records.xml`), `version="2"`.

```xml
<GravityMazeRecords version="2">
  <Settings LastPlayer="jude" FullTiltDegrees="..." Deadzone="..." Smoothing="..."
            SwapAxes="false" InvertX="false" InvertY="false"/>
  <Player key="jude" name="Jude" created="..." lastPlayed="..." unlockedLevel="3">
    <Ghost level="1" time="7.10" hz="10">12.5,3.1;12.9,3.4;...</Ghost>
  </Player>
  <Run player="jude" level="3" outcome="cleared" time="52.40" wallHits="6" falls="1"
       heartsLost="2.5" barriersBroken="5" stars="2" score="4120" date="..."/>
  <Run player="jude" level="0" campaignLevels="4" outcome="cleared" time="..." .../>
</GravityMazeRecords>
```

- **Player identity:** `key` = name trimmed, inner whitespace collapsed, lower-cased
  (invariant). `name` keeps the spelling from creation. All lookups use `key`
  (fixes the case-sensitive victory-rank comparison at `GameShell.vb:585`).
- **Run journal:** one `<Run>` per attempt. Outcomes: `cleared`, `out-of-hearts`,
  `restart`, `quit` (exit to menu or app close mid-run). Runs where the ball never
  moved (stopwatch 0) are not saved. Attempts per level = count of runs.
- **Derived stats, not stored:** attempts, clears, clear rate, best time, best stars
  and outcome breakdown are computed from runs.
- **Progress:** clearing level N sets `unlockedLevel = max(unlockedLevel, N+1)`.
  Locked levels show a padlock in Level Select.
- **Campaign rule:** a campaign record (`level="0"`) is written only for an unbroken
  run from Level 1 through the final level. Continue / partial campaigns still write
  every individual level run.

  | Played | Level records | Campaign record |
  |---|---|---|
  | L1 → L2, stop | L1, L2 | no |
  | Continue L3 → L4 | L3, L4 | no |
  | L1 → L4 in one go | all | yes |
  | Campaign ends early | every level reached | no |
- **Ghost:** best clear per player per level, ball tile-space position sampled at
  10 Hz. Replaced when a new personal best is set. Malformed ghost data is skipped.
- **Migration v1 → v2:** each `<Record>` becomes a `cleared` `<Run>`; players created
  from distinct keys (first-seen spelling wins); `unlockedLevel` derived from clears;
  v1 `Settings PlayerName` becomes `LastPlayer`.
- **Safety (kept):** corrupt file → `.bak` copy, start empty; atomic save via `.tmp`
  + `File.Replace`; disk errors never crash the game. Unknown elements are ignored.

## 3. Screens

- **Title:** CONTINUE (Level N) · NEW CAMPAIGN · LEVEL SELECT · RECORDS · PROFILE ·
  CALIBRATE · SWITCH PLAYER.
- **Who's Playing?** (replaces name entry): list of profiles (name, stars, last
  played), last player preselected, plus "+ New player". Typing an existing name in
  any case → "Continue as Jude?" instead of a duplicate.
- **Profile:** per level — attempts, clears, best time, stars, outcome breakdown;
  ghost on/off toggle.
- **Records:** existing leaderboard plus All players / Just me filter.

## 4. Quality of life

- **Instant restart:** keyboard `R` restarts the level immediately (no confirm,
  countdown replays) and logs the abandoned run as `restart`. Arduino users restart
  from the pause menu (no shake gesture, no button — avoids false triggers and
  firmware changes).
- **Calibration screen:** live tilt dot, recenter (hold level + confirm), sensitivity
  (FullTiltDegrees) and dead-zone sliders, Invert X / Invert Y / Swap axes toggles,
  a small test-ball area. Saved per machine in `<Settings>`; current `Form1.vb`
  values are the defaults.
- **Auto-pause on disconnect:** if the Arduino drops while it is the active input
  during play → pause with "CONTROLLER DISCONNECTED — reconnect, or press any key to
  use the keyboard". On reconnect → "Controller reconnected — hold the board level,
  then confirm to resume". Never auto-resumes. Timers/hazards frozen as in pause.
- **Ghost toggle** also in the pause menu.

## 5. Build order

1. XML v2 + profiles + run journal (with migration).
2. Who's Playing? / Profile screens + progress, Continue, campaign rule.
3. Level 3 Overdrive Barriers (tile, engine, renderer, maze, validator, par).
4. Ghost record/playback + toggle.
5. QoL: `R` restart, calibration screen, auto-pause.
6. Docs: PRD (progress saved, Level 3), AGENTS.md (`B` tile), SETUP.md (calibration).

## 6. Testing

Assert-based self-checks in the existing style:
- v1 file migrates; "Jude"/"jude"/" JUDE " resolve to one profile; corrupt file
  backs up; zero-movement runs are not saved.
- Campaign rule table above holds.
- Barrier: fast hit breaks, slow hit bounces with ¼ heart, persists across a fall,
  resets on restart; validator proves run-ups exist.
- Simulated disconnect pauses and does not auto-resume.
- Real-app screenshot pass for every new screen and Level 3, as the project does.

## Plan-time corrections (2026-10-16, from reading the code)

- Level 3 is **37×37 in game**: a 25×25 source in `tools/maze_src/` widened by `tools/widen_maze.py`.
- Break speed (0.16) sits above normal top speed (0.12), so **every barrier needs a boost run-up**.
  The "long straight" variant is dropped. There are **4 barriers**, one at the end of each boost strip on the route.
- The optional countdown code stays **dormant** (no level uses it) rather than being deleted. Removing it would touch
  around 300 test call sites for no player-visible gain.
- `R` instant restart **already existed**; the new work is journaling the abandoned run.
- Title menu keeps **PLAY** (always a new campaign) and adds a separate **CONTINUE · LEVEL N** item, disabled
  when there is nothing to continue.
- Ghost toggle lives on the **Profile screen and the `G` key** during play, not the pause menu (keeps the pause
  menu's indices stable for the dungeon RULES item).
- Name entry already upper-cases what is typed, so new names display in capitals; case-insensitive matching
  matters mainly for names saved by older versions.
- The profile key is **derived from the name**, not stored as its own XML attribute.
- Calibration values **cycle on confirm** instead of using sliders, because left/right are hold-back/hold-confirm
  on the board. The test ball sits behind a **TEST TILT** item that freezes the menu while you tilt.

## Implementation roles

Opus 5.5 orchestrates; Sonnet 5.5 implements code; Haiku 5.5 explores code.
