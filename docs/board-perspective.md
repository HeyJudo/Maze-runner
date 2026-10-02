# Board perspective tilt

The board starts at a 12-degree viewing angle. During play, the same resolved
Arduino or keyboard input used by the engine adds up to 8 degrees of tilt per
axis. Keyboard override, board calibration, and input inversion therefore apply
to both movement and visuals. Releasing the keys eases back to the resting view;
an attached board continues supplying input as usual.

The maze, marble, pickups, shadows, and effects share a perspective projection.
HUD text and menus stay in screen coordinates. This projects the existing
artwork onto a plane; it does not add three-dimensional wall geometry. Physics,
collision, scoring, and serial communication are unchanged.

F8 toggles the view anywhere, including during name entry. The pause menu also
has TILT VIEW: ON/OFF, accessible using the board's existing menu controls. Off
restores the original top-down renderer. The setting lasts for the session.
Pausing and results freeze the visual angle; loading or restarting a level resets
it. Input smoothing takes about 110 ms per response time constant.

The renderer reuses two premultiplied-alpha bitmaps and pixel buffers, with a
maximum texture dimension of 1024 pixels. It uses bilinear perspective sampling
and up to four CPU workers for large frames. It preserves the cached static
artwork instead of regenerating it on each tilt. Window resizing recreates the
buffers; closing the canvas disposes its rendering resources.

## Automated checks

From the repository root, on Linux or Windows with .NET 10:

```sh
dotnet run --project tools/PerspectiveChecks/PerspectiveChecks.vbproj -c Release
```

This exercises easing, return to rest, input bounds, projection direction,
clipping across aspect ratios, inverse alignment, bilinear pixel sampling,
transparent borders, and clearing previous frames. It also reports a CPU-only
warp timing, which excludes Windows drawing and is not an end-to-end FPS claim.

On Linux, compile the Windows targets with:

```sh
dotnet build tools/VerificationRunner/VerificationRunner.vbproj -c Release -p:EnableWindowsTargeting=true
```

On Windows, run the targeted rendering checks:

```sh
dotnet run --project tools/VerificationRunner/VerificationRunner.vbproj -c Release -- --perspective-only
```

This checks that input changes the visible board, that an overlay stays fixed,
and that disabling tilt restores the original renderer. It writes rest, tilted,
and flat screenshots for all three themes at two window sizes into
`tools/VerificationRunner/bin/Release/net10.0-windows/perspective-checks`.
The existing full UI tour additionally checks F8 and tilt-driven pause-menu
toggle navigation. The targeted command avoids the full runner's machine-specific
artifact and project paths.

## Windows play check

Run the game using SETUP.md, then check:

1. In each theme, hold each arrow/WASD direction and diagonals. The board should
   ease toward the input, then return to its resting pose on release. The marble
   should stay aligned with paths and walls, including near the edges.
2. Use the Arduino, re-center with C, and briefly override it with keyboard input.
   Visual tilt should follow the same direction and strength as ball steering.
3. Pause, navigate with the board, and toggle TILT VIEW twice. The board's angle
   should hold while navigating; toggling should not resume the game.
4. Toggle F8 during play, menus, and name entry. The view should change without
   altering game state or adding a character to the player name.
5. Restart, complete a level, lose all hearts, and let the timer expire. Confirm
   the appropriate reset/frozen pose, readable HUD, and existing effects.
6. Resize the window, switch fullscreen with F11, and check smoothness on your
   machine. Rendering speed and the appearance of motion need Windows validation.
