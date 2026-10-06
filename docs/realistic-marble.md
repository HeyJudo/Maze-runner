# Realistic metal marble

The marble has a subtle brushed silver surface that rolls from resolved tile-space
movement, including diagonals and direction reversals. Input held against a wall
cannot spin it. Rolling state updates on game ticks, so repainting or resizing does
not advance it. Levels and retries reset its orientation; pit respawns rebase its
position without rolling across the maze.

The machining marks rotate with the sphere while the main highlight stays in the
lighting frame. Board tilt gently shifts the light and reflection band. Wood, ice,
and neon reflect warm, cool, and violet light onto the same silver material.

In perspective mode, the floor shadow is projected with the board, then the marble
is drawn as a circle with the board camera's position and depth scale. The sphere
and board share the transition's opacity and transforms. Airborne landing shadows
spread and fade, returning to a soft contact shadow at floor level. Pit ghosts use
the same sphere material and darken as they shrink.

This first version covers rolling, reflections, and depth. Physics, health, impact
feedback, and terrain trails retain their existing behavior. Additional impact
shudder and terrain-dependent visual slipping can be tuned separately.

## Validation

From the repository root, with .NET 10:

```sh
dotnet run --project tools/MarbleChecks/MarbleChecks.vbproj -c Release
dotnet run --project tools/HealthChecks/HealthChecks.vbproj -c Release
dotnet run --project tools/TransitionChecks/TransitionChecks.vbproj -c Release
dotnet run --project tools/PerspectiveChecks/PerspectiveChecks.vbproj -c Release
```

The marble checks use production motion, material, camera, and engine code. They
exercise reversal, stopped wall contacts, tick subdivision, teleports, reset,
long-run stability, changing texture with coherent lighting, theme reflections,
projection alignment, and shadow spread/opacity. An optional material-only PPM
sample sheet can be produced without Windows:

```sh
dotnet run --project tools/MarbleChecks/MarbleChecks.vbproj -c Release -- --preview marble-material.ppm
```

On Linux, cross-build with:

```sh
dotnet build GravityMaze/GravityMaze.vbproj -p:EnableWindowsTargeting=true
dotnet build tools/VerificationRunner/VerificationRunner.vbproj -p:EnableWindowsTargeting=true
```

Windows is required for the actual Forms/GDI rendering checks:

```sh
dotnet run --project tools/VerificationRunner/VerificationRunner.vbproj -- --marble-only
dotnet run --project tools/VerificationRunner/VerificationRunner.vbproj -- --perspective-only
dotnet run --project tools/VerificationRunner/VerificationRunner.vbproj -- --transition-only
```

The marble tour checks changing texture, a stable stopped surface, retry reset,
and a round projected silhouette. It saves snapshots to the runner's output
`marble-checks` directory across three themes, flat/tilt views, and two sizes.
Review them and play through ordinary movement, reversals, wall stops, pit falls,
goal drops, landings, F8 view toggling, and resizing. A material-only sample does
not validate the desktop compositing path or in-game frame time.
