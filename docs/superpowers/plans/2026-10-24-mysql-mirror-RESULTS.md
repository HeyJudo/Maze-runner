# MySQL records mirror: results

Branch `feature/mysql-mirror` (from `main` 998211b, after PR #8 merged). Not pushed.

| commit | what |
|---|---|
| 05b9032 | `ScoreManager.Saved` hook (fires only after the XML write succeeds) plus `Friend ToXml()` |
| 0a2af87 | `Data/MySqlMirror.vb`, `Data/gravitymaze.sql`, MySqlConnector 2.6.2, fake-sink checks, and an optional live check |
| 89a48f8 | Form1 wiring with startup backfill, title-screen status, SETUP.md §5, and PRD §9 |
| 06c8ba9 | Review fixes: flush the mirror on close, Windows setup steps, binary key collation, `AllowPublicKeyRetrieval` |

## How it works
After every successful save, `records.xml` (v2, unchanged) hands its root element to the mirror. A background worker replaces the four tables (players, runs, ghosts, settings) in a single transaction. If several saves arrive while it is busy, only the latest one is written. The game also pushes once at startup, so the first successful connection backfills everything and any outage is caught up. If MySQL is down, the game keeps running on XML and the title screen shows `MYSQL OFFLINE · XML ONLY · <reason>`. The config lives in `%AppData%\GravityMaze\mysql.ini`, and the game writes a template the first time it runs.

## Verification
- Full check set green on 06c8ba9: build, Camera 859, Dungeon 1777, Health 98, Marble 1014, Perspective 2387, Transition 525, and VerificationRunner "ALL VERIFICATIONS PASSED SUCCESSFULLY!".
- The live check ran against a throwaway MySQL 8.0.44 server on port 3307. It passed both as root with no password and as a caching_sha2 user whose password contains `;`, `=` and a space. The run covered the schema script, `WriteAll` twice (idempotent) and the row counts.
- The real game was not launched, because that would have touched the real `%AppData%` files.

## Rulings
- `Saved` and `Friend ToXml` are the only additions to ScoreManager. If wrong, two members to remove.
- Each save does a full replace rather than per-row upserts. If wrong, syncs get slow on huge journals (they run in the background).
- Column names are the records.xml attribute names, plus `playerKey` and `runs.id` (the position in the journal). If wrong, columns need renaming.
- No foreign keys, so the mirror never rejects data that the XML accepted. If wrong, Workbench enforces weaker integrity.
- `mysql.ini` sits beside records.xml and its template is written automatically. If wrong, the friend looks in the wrong folder (SETUP says where it is).
- No human review gate between the plan and the build, as requested. If wrong, rework.
- Password values are trimmed at the edges, and the live-check and helper-naming nits are left as they are. If wrong, a password with leading or trailing spaces fails to log in, and the status says so.
- The template is written on every machine, so players without MySQL see a dim OFFLINE line. If wrong, it's one line of text.
- If records.xml is corrupt, the game starts empty and the startup push empties MySQL to match. If wrong, the MySQL copy is lost until the `.bak` is restored.
- The new `utf8mb4_bin` key collation only applies to freshly created tables. If wrong, anyone who ran an older script must drop and recreate the tables.

## To test by hand on the friend's PC
1. Run `GravityMaze\Data\gravitymaze.sql` in Workbench, then refresh Schemas and check that `gravitymaze` has 4 tables.
2. Start the game once. The title screen should show `MYSQL OFF · EDIT mysql.ini`. Close the game.
3. Win+R, `%AppData%\GravityMaze`, open `mysql.ini` and fill in the user and password. Start the game again. Within a few seconds the title should show `MYSQL SYNCED hh:mm`, and the existing records should appear in `SELECT * FROM gravitymaze.runs;`.
4. Play a level, then quit from the menu and close the window. The last run, including the quit run, should be in `runs`.
5. Stop the MySQL80 service (services.msc), then play and finish a level. There should be no freeze or crash, and the title should say OFFLINE. Start the service again, play once more, and check that `runs` has caught up.
6. Put a wrong password in the ini. The title should show OFFLINE with an "Access denied" reason, and the game should keep working.
