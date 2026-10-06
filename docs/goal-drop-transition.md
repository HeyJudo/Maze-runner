# Goal drop and automatic campaign progression

Campaign goals use a 2.4-second transition:

| Stage | Duration | Behavior |
| --- | --- | --- |
| Goal drop | 650 ms | The marble centers on the glowing goal, shrinks, and disappears. |
| Board lift | 650 ms | The completed board rises and fades; the next board appears below it. |
| Landing | 450 ms | The marble falls onto the next start with a small bounce, shadow, and pulse. |
| Ready | 650 ms | The new board holds still with a READY cue before controls resume. |

A brief overlay shows the completed time, earned stars, and new-best status,
then the incoming level name. No NEXT LEVEL confirmation or second countdown
interrupts campaign progression. The final campaign goal uses the drop/lift
stages (1.3 seconds) and opens the victory screen. Level Select uses the same
goal exit but retains its explicit results and retry menu.

Physics, health, and the level timer stop at goal completion. The result is
recorded once; transition time and pauses do not contribute to campaign time.
The next engine starts at the normal start tile with full health and a zero
timer after the animation. Goal drops do not use pit damage or pit respawn.

Esc/P and focus loss pause the animation. Resume continues the same stage and
elapsed time. Restart from the pause menu cancels the pending advancement and
replaces that completion's campaign contribution if the level is played again;
completed attempts remain in score history. MAIN MENU cancels advancement.
Movement and Enter cannot move the marble or skip the transition. F8 and M
still toggle perspective and sound.

The effect layers existing 2D board artwork; it does not simulate a 3D stack of
physical mazes. Incoming and outgoing boards reuse separate render caches and
bounded image surfaces, supporting both flat and perspective views. The normal
engine marble stays hidden while animation visuals replace it. Canvas disposal
releases both sets of rendering resources.

## Validation

From the repository root on Linux or Windows with .NET 10:

```sh
dotnet run --project tools/TransitionChecks/TransitionChecks.vbproj -c Release
```

This links the real shell, engine, menus, input router, pilot, score store, and
transition clock. Only Windows drawing, audio, and hardware presentation use
headless test adapters. It checks stage boundaries, landing heights, automatic
advancement, ignored movement/confirmation input, fresh next-level state,
pause/focus/resume, practice-mode results, final victory, single score recording,
campaign time/stars, restart accounting, cancellation, and paused intros.

On Windows, generate rendering snapshots:

```sh
dotnet run --project tools/VerificationRunner/VerificationRunner.vbproj -c Release -- --transition-only
```

This uses the actual canvas and renderer in flat/perspective modes at two window
sizes. It checks a stationary overlay and marble restoration, and saves stage
snapshots under `tools/VerificationRunner/bin/Release/net10.0-windows/transition-checks`.
The existing full UI tour now advances through the campaign without NEXT LEVEL
confirmation and captures drop, lift, and landing images.

Before merging, play a campaign on Windows. Check both inter-level transitions
and final victory, inspect the goal centering and landing motion, and test Esc/P,
focus loss, F8, and resizing during the effect. Hold an Arduino tilt or keyboard
direction during the transition: movement should start only after READY ends.
Also complete a level through Level Select and confirm its results menu remains.
Windows drawing, motion quality, and end-to-end performance require local review.
