# Level 3 Barriers, Profiles & Run Journal: Results

Branch `feature/barriers-profiles` (from `7454f11`), merged into `main` as a fast-forward.
Plan: `docs/superpowers/plans/2026-10-16-level3-barriers-profiles.md` · Spec: `docs/superpowers/specs/2026-10-16-level3-barriers-profiles-design.md`

## ⚠ Read before running the game from the main checkout

The main checkout (`RICK MAZE`) is still on `feature/dungeon-revamp`, which has the **old v1 `ScoreManager`**. Builds of this branch have already rewritten the real `%AppData%\GravityMaze\records.xml` as v2. A v1 build loads a v2 file as **empty**, with no error and no `.bak`, then **overwrites it** on the next save (name entry or any clear). That loses every profile, run and ghost.

Before you run anything from the main checkout, bring it up to `main`. Either check out `main` there, or fast-forward `feature/dungeon-revamp`; commit or stash its screenshot changes first. Don't launch older builds. The old v1 data is still backed up at `%AppData%\GravityMaze\records.v1.bak.xml`.

## What each task did

| Task | Commits | What it did |
|---|---|---|
| 1 | `7454f11..7003700` | XML v2: player profiles (case-insensitive key), one `<Run>` per attempt, ghosts, calibration settings, automatic v1 → v2 upgrade. The old public API was kept. |
| 2 | `7003700..21e3bdc` | Engine: tile `B` Overdrive barrier. It smashes at ≥ 0.16 tiles/tick and otherwise bounces like a wall (¼ heart, once per contact). Broken barriers survive falls, and restart rebuilds them. Adds per-run wall-hit, fall and hearts-lost counters. |
| 3 | `21e3bdc..4561112` | Level 3 maze with four required barriers, each at the end of a boost strip. The countdown is removed (the code stays dormant). The attract pilot boosts through. Par is 37.4 s. |
| 4 | `4561112..6fbb6a7` | Barrier drawing: a charge glow as speed builds, smash particles and sound, a TOO SLOW cue, the HUD "Barriers n/4", the "BREAK THE BARRIERS" intro, and a How To Play legend. |
| 5 | `6fbb6a7..c75ae68` | Run journal (cleared / out-of-hearts / restart / quit), unlocked level per player, CONTINUE · LEVEL N, padlocks in Level Select, the campaign rule (campaign record only for an unbroken L1 → end run), and a nine-item title menu fitted above the footer. |
| 6 | `c75ae68..377ef90` | Who's Playing? (7 most recent profiles + new player), "already exists → continue as" for any letter case, Profile screen (per-level stats and outcome breakdown, ghost toggle), Records filter (All players / Just me). |
| 7 | `377ef90..f867300` | Ghost marble: records the best run at about 10 Hz, races it on later attempts, and the G key toggles it. HUD hints drop the lowest-priority chip when they would meet the status text. |
| 8 | `f867300..c634080` | CALIBRATE screen. RECENTER, SENSITIVITY, DEAD ZONE, INVERT X/Y and SWAP AXES cycle on confirm, with TEST TILT and a live tilt dot; all except RECENTER are saved to records.xml. A board-confirmed RECENTER waits until the board is held level for about 1 s; keyboard Enter recenters at once. |
| 9 | `c634080..0272117` | Unplugging the board mid-run pauses the game with a DISCONNECTED / RECONNECTED banner. It never resumes on its own. |
| 10 | `0272117..d45beeb` | PRD, AGENTS.md and SETUP.md updated. The UI tour captures Profile, Who's Playing? and Calibrate. Screenshots refreshed (`ui_13_time_up` removed). |
| Final fix wave | `d45beeb..16600e0` | SETUP §5 rewritten: CALIBRATE now owns the tuning, and Form1 values are overridden at startup. Also: the charge glow uses single-axis speed like the smash test, a v1 PlayerName with no records is kept, a SaveGhost guard is added, dead code is removed, and comments and labels are tidied. |

Every task passed an Opus review, needed at most one fix round, and ended "review clean". The final whole-branch review found no Critical issues, and its one fix wave was re-reviewed with every item ADDRESSED.

## Test counts (final head `16600e0`)

| Check | Result |
|---|---|
| `dotnet build GravityMaze/GravityMaze.vbproj` | 0 errors |
| CameraChecks | PASS 859 |
| DungeonChecks | PASS 1777 |
| HealthChecks | PASS 98 |
| MarbleChecks | PASS 1014 |
| PerspectiveChecks | PASS 2387 |
| TransitionChecks | PASS 525 (was 500 after Task 5) |
| VerificationRunner | ALL VERIFICATIONS PASSED SUCCESSFULLY! |

## Rulings (decisions made without asking), with cost if wrong

1. **Task 1:** kept the plan's catch-all in Load/Save. It matches v1 behaviour, the file is only written by the game, and a `.bak` is kept. *Cost if wrong:* a corrupt file loses live data except the single `.bak`.
2. **Task 5:** "ball never moved → journal nothing" is implemented as "stopwatch 0 → not journaled", so an idle but timed run is journaled as restart or quit. *Cost if wrong:* a few extra restart/quit rows when a player sits still and then leaves.
3. **Task 6:** Who's Playing? is capped at the 7 most recent profiles, because the spec's no-overlap and tilt-usable rule beats an unbounded list. Older players type their name and the "already exists" flow continues them. *Cost if wrong:* an old player has to type their name instead of picking it.
4. **Task 6:** the Records BACK item touching the hold-left bar at minimum size was folded into the same fix (same no-overlap rule). *Cost if wrong:* one extra line in the fix diff.
5. **Merge target:** `main` via a fast-forward, not `feature/dungeon-revamp` as plan Task 10 Step 5 says, because your instruction overrides the plan. *Cost if wrong:* dungeon-revamp lags main until it is fast-forwarded (see the warning above).
6. **Hand-play skipped:** plan Task 10 Step 3 ("launch the real game and confirm by hand") was not done, because the run was unattended. It is listed below. *Cost if wrong:* a gameplay-only bug reaches main until someone plays it.
7. **Task 8:** the plan's code calibrated RECENTER immediately on a board confirm, while the board was still tipped right, which saved a crooked zero. Now a board confirm waits for about 1 s of level board, and keyboard Enter recenters at once. The spec says "recenter (hold level + confirm)". *Cost if wrong:* a ~1 s delay before recenter on the board.
8. **Task 8:** cheap minors folded into the fix round: header placement, sounds when leaving TEST TILT, and a "RECENTER after changing a setting" hint. *Cost if wrong:* a slightly larger fix diff.
9. **Task 9:** the banner says "PRESS ESC TO PLAY ON THE KEYBOARD" rather than the spec's "press any key", because arrow keys must keep navigating the pause menu. *Cost if wrong:* one word of banner copy.
10. **Task 10:** cheap minors folded into the fix round: measure the player-select date the way it is drawn, and fix the PRD resume wording. *Cost if wrong:* trivial.
11. **Final fix wave:** re-committed the regenerated screenshots, because two fixes change on-screen text and committed screenshots must match the final code. *Cost if wrong:* one more binary-only screenshot diff in history.
12. **v1 downgrade risk:** handled by the warning above rather than by touching the main checkout, which you said not to touch. *Cost if wrong:* if the old build runs before you read this, your v2 records are emptied (the v1 backup remains).

The final reviewer agreed with every ruling.

## Parked findings

None. No task reached the 5-round fix cap.

Deferred minors that the final review triaged as **fine to leave** (all in the ledger) include:
- A transient read error on records.xml is treated like corruption; a later fix would be a no-save flag.
- The Profile "HOW RUNS ENDED" column clips at 960 px wide.
- The ghost is hidden during the Intro countdown.
- The calibration offset is stored after invert/swap, so the screen tells you to RECENTER after changing those.
- The disconnect auto-pause also triggers for a keyboard player whose board drops.
- SETUP §6 still mentions copying the `board = New ArduinoController()` line from Form1 (now only PortName matters).
- Several test-coverage gaps.

## Left for a human to check by hand

- [ ] **Play Level 3:** each of the four barriers smashes when hit straight off a boost strip, and bounces with one TOO SLOW and ¼ heart otherwise. Also check that broken barriers stay broken after a pit fall and come back after R.
- [ ] **Ghost:** clear a level, replay it, and check the translucent ghost races you. Check that G hides and shows it.
- [ ] **Calibration screen with the board:** RECENTER by holding right, then hold the board level, and check that the dot settles in the centre. Then try INVERT X/Y, SWAP AXES, SENSITIVITY, DEAD ZONE and TEST TILT (leave TEST TILT by holding left for 2 s). Restart the game and check that the settings were kept.
- [ ] **Unplug the board mid-run:** the game pauses with CONTROLLER DISCONNECTED. Plug it back in and check the banner says RECONNECTED and the game stays paused until you resume.
- [ ] **Profiles:** Who's Playing? lists your existing players (JUDO, RICK) after the v1 upgrade, and typing an existing name in a different case offers to continue as that player.
- [ ] **Window sizes:** look at the new screens at the smallest window size.
