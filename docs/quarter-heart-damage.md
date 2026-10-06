# Quarter-heart wall damage

New wall contacts cost 1/4 heart: 3 → 2.75 → 2.5 → 2.25 → 2 hearts.
Twelve damaging wall contacts empty all three hearts if no pickups are collected.
Holding against a wall costs health only on the initial contact. There is one
second of protection after damage; a corner contact cannot charge both axes.

Pits still cost one full heart, bypass wall-damage protection, and respawn the
ball. Damage clamps at zero, including a pit fall with less than one heart left.
Pickups restore one full heart, capped at three. For example, 1.75 becomes 2.75,
and 2.75 becomes 3. They remain on the board at full health and can be used only
once per attempt. A restart restores health and pickups.

The engine stores health in integer quarters. `Hearts` and the heart event
totals expose the exact fractional value; events also report `PreviousHearts`
for the HUD damage animation. The HUD uses the existing quarter-heart sprites
and holds the partial state after damage. If the sheet is missing, a clipped
filled heart over a dim empty heart preserves the partial-health display.

## Checks

Run the shared health scenarios on Linux or Windows from the repository root:

```sh
dotnet run --project tools/HealthChecks/HealthChecks.vbproj -c Release
```

These exercise real engine movement and collisions, exact quarter progression,
wall-contact suppression, the protection window, corner contacts, pit damage,
fractional healing, the health cap, single-use pickups, reset, and disabled health.
The full Windows verification runner uses the same scenarios.

On Windows, check the sprites and produce a visual comparison:

```sh
dotnet run --project tools/VerificationRunner/VerificationRunner.vbproj -c Release -- --hearts-only
```

This runs the health scenarios, checks that all five fill states are visibly
distinct, and saves `quarter-heart-states.png` in
`tools/VerificationRunner/bin/Release/net10.0-windows/heart-checks`.

Play Level 1 using keyboard or Arduino. After a wall hit, check two full hearts
and one 3/4 heart after the short damage animation. Repeated separate contacts
should step that heart through 1/2, 1/4, and empty. Check pits, heart pickups,
pausing, and restarting. The UI tour now checks 2.75 hearts after one wall hit
and game over after twelve separate damaging contacts.
