# MySQL Records Mirror Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every player, run, ghost and setting that Gravity Maze saves to `records.xml` also lands in a local MySQL database, without the game ever depending on MySQL.

**Architecture:** `ScoreManager` gains one hook, `Saved As Action(Of XElement)`, fired with the v2 root it just wrote, after the file write succeeds. A new `Data/MySqlMirror.vb` queues that snapshot (latest wins) and writes it on a thread-pool thread. Each write is a full replace in one transaction: delete all four tables, then insert everything. That makes it idempotent, does the backfill on the first successful connection, and catches up after an outage, all for free. `Form1` wires it up from `%AppData%\GravityMaze\mysql.ini` and pushes one snapshot at startup. The title screen shows `MySqlMirror.Status`.

**Tech Stack:** VB.NET, .NET 10 WinForms, MySqlConnector (NuGet, latest stable), MySQL 8.

**Spec:** the task brief, which this session's caller relayed (DECIDED list), restated as Global Constraints below.

## Global Constraints

- `records.xml` v2 stays byte-for-byte the same format and stays the source of truth. No entity, field or XML change.
- MySQL tables mirror players, runs, ghosts and settings one-to-one. Column names are the records.xml attribute names verbatim, plus `playerKey` (= `ScoreManager.KeyOf(name)`) and `runs.id`.
- Schema ships as a committed `GravityMaze/Data/gravitymaze.sql` that creates the database and tables.
- MySQL is off, unreachable or misconfigured → no exception escapes, the UI thread never blocks on MySQL, and the status is shown on the title screen.
- Credentials only in `%AppData%\GravityMaze\mysql.ini`. Never hard-coded, and no real password committed.
- `ScoreManager` public API: only additive (`Saved`). Screens change only to draw the status.
- Check projects link `ScoreManager.vb` alone, so it must not reference `MySqlMirror` or MySqlConnector.
- Only new dependency: `MySqlConnector`, referenced only by `GravityMaze.vbproj`.
- Every VB file: `Option Strict On` / `Option Explicit On`. `Data/` never references UI or input.
- Before every commit: `dotnet build GravityMaze/GravityMaze.vbproj`; `dotnet run --project tools/<X>` for CameraChecks, DungeonChecks, HealthChecks, MarbleChecks, PerspectiveChecks, TransitionChecks; `GM_ROOT="$PWD" dotnet run --project tools/VerificationRunner`, which must end with `ALL VERIFICATIONS PASSED SUCCESSFULLY!`; then `git checkout -- screenshots && git clean -fd screenshots`.
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. No push.
- Never touch the real `%AppData%\GravityMaze\records.xml` or `mysql.ini`. Checks use temp dirs.

## Review Focus

1. **Server down at every save.** Each save retries in the background with a 3 s connect timeout, and the game must not stutter. Pinned by the slow-sink `Push` timing check (Task 2).
2. **Failed XML save.** The mirror must not get data the XML doesn't have. Pinned by the "Saved not fired when write fails" check (Task 1).
3. **Rapid saves while a sync is in flight.** They must coalesce to the latest snapshot and never run two writes at once. Pinned by the coalescing check (Task 2).
4. **Legacy or odd data.** A level-0 run carries `campaignLevels`, other runs carry NULL, and a run with an empty player name must still insert (no foreign keys). Pinned by the `Tables` mapping check (Task 2).
5. **Status leaking into screenshots.** Mirror checks run before UiTour in the same process, so they must reset `MySqlMirror.Status = ""`. Pinned in Task 2's check, and UiTour stays green.

---

### Task 1: `ScoreManager.Saved` hook

**Files:**
- Modify: `GravityMaze/Data/ScoreManager.vb` (`Save()`, around lines 386-440)
- Test: `tools/VerificationRunner/Program.vb` (new `TestSavedHook()`, called next to `TestScoreManager()` in both the `--data-only` branch and the full run)

**Interfaces:**
- Produces: `Public Property Saved As Action(Of XElement)` on `ScoreManager`, invoked with the root `<GravityMazeRecords version="2">` element only after `File.Replace` or `File.Move` succeeds. `Friend Function ToXml() As XElement` builds that root. `Save()` becomes ToXml → write → `Saved`.

- [ ] **Step 1: Write the failing check `TestSavedHook()`**

Assertions (Check style used in Program.vb, temp dir, cleaned up in Finally):
```vb
Dim got As XElement = Nothing
Dim sm As New ScoreManager(xmlPath)
sm.Saved = Sub(root) got = root
sm.PlayerName = "JUDE"
Check(got IsNot Nothing AndAlso got.Name.LocalName = "GravityMazeRecords" AndAlso CStr(got.Attribute("version")) = "2", "Saved fires with v2 root")
Check(got.Element("Settings").Attribute("LastPlayer").Value = "JUDE", "Saved root carries current data")
Check(XDocument.Load(xmlPath).Root.ToString() = got.ToString(), "Saved root equals file on disk")
' failing write: the target path is a directory
Dim badPath As String = Path.Combine(dir, "isadir") : Directory.CreateDirectory(badPath)
Dim bad As New ScoreManager(badPath) : got = Nothing
bad.Saved = Sub(root) got = root
bad.PlayerName = "X"
Check(got Is Nothing, "Saved not fired when write fails")
```
- [ ] **Step 2:** Run `GM_ROOT="$PWD" dotnet run --project tools/VerificationRunner -- --data-only`. Expected: compile error, `Saved` not defined.
- [ ] **Step 3:** Implement. Move the XElement construction from `Save()` into `Friend Function ToXml() As XElement`. In `Save()`, after the replace/move succeeds and still inside the Try, add `If Saved IsNot Nothing Then Saved(root)`. Add a one-line comment saying a hook must not block, because the caller is the UI thread.
- [ ] **Step 4:** Rerun `--data-only`. Expected: the new check passes, plus the existing ScoreManager and profile checks.
- [ ] **Step 5:** Run the full check set (Global Constraints), clean the screenshots, then commit `Add a Saved hook to ScoreManager for mirrors`.

### Task 2: `MySqlMirror`, schema script and checks

**Files:**
- Create: `GravityMaze/Data/MySqlMirror.vb`, `GravityMaze/Data/gravitymaze.sql`
- Modify: `GravityMaze/GravityMaze.vbproj` (`dotnet add GravityMaze/GravityMaze.vbproj package MySqlConnector`, which pins the resolved version)
- Test: `tools/VerificationRunner/Program.vb`, new `TestMySqlMirror()` (fake, always runs) and `TestMySqlLive()` (skips), both called after `TestSavedHook()` in `--data-only` and the full run

**Interfaces:**
- Consumes: `ScoreManager.Saved`, `ScoreManager.KeyOf`, `ScoreManager.DefaultPath()`.
- Produces (namespace `Data`, `Public NotInheritable Class MySqlMirror`):
  - `Public Shared Property Status As String = ""`. Set from worker threads and read by the UI ("" = mirror never started, draw nothing).
  - `Public Sub New(write As Action(Of XElement))`, where the sink is injectable for checks.
  - `Public Sub Push(root As XElement)`. Never blocks, latest wins.
  - `Public Function WaitIdle(timeoutMs As Integer) As Boolean`. Polls with `Thread.Sleep(10)` until nothing is queued or running. Used by checks only.
  - `Public Shared Function DefaultConfigPath() As String`, which is `mysql.ini` beside `ScoreManager.DefaultPath()`.
  - `Public Shared Function ConnectionStringFrom(iniPath As String) As String`. Returns Nothing if the file is missing (and writes the template, see below). Throws `FormatException` on a bad port.
  - `Public Shared Function FromConfig(iniPath As String) As MySqlMirror`. Returns Nothing and sets Status when the config is missing or bad. Otherwise returns `New MySqlMirror(Sub(root) WriteAll(cs, root))`.
  - `Public Shared Function Tables(root As XElement) As Dictionary(Of String, List(Of Dictionary(Of String, Object)))`, keyed by table name (`players`, `runs`, `ghosts`, `settings`) to rows of column → value.
  - `Public Shared Sub WriteAll(connectionString As String, root As XElement)`.

Status strings (exact): `MYSQL SYNCING…`, `MYSQL SYNCED HH:mm`, `MYSQL OFFLINE · XML ONLY · <ex.Message, first 60 chars>`, `MYSQL OFF · EDIT mysql.ini` (template just written), `MYSQL CONFIG ERROR · <msg, first 60>`.

`mysql.ini` (template written when missing; `#` lines and unknown keys ignored, keys case-insensitive, `key=value` split on the first `=`):
```
# Gravity Maze MySQL mirror. records.xml stays the main save; this copies it into MySQL.
# Run GravityMaze\Data\gravitymaze.sql once in MySQL Workbench, then fill in your password.
host=localhost
port=3306
database=gravitymaze
user=root
password=
```
The connection string comes from `MySqlConnectionStringBuilder` with Server, Port, Database, UserID, Password, `ConnectionTimeout = 3` and `DefaultCommandTimeout = 10`.

Schema (`gravitymaze.sql`, utf8mb4, InnoDB, `CREATE DATABASE IF NOT EXISTS gravitymaze; USE gravitymaze;`, `CREATE TABLE IF NOT EXISTS` for each, no foreign keys, so the mirror never rejects what the XML accepted). It includes a commented-out optional `CREATE USER 'gravitymaze'@'localhost' …` / `GRANT ALL ON gravitymaze.*` block. The word `gravitymaze` appears only as the database name, in that commented block and in comments, because the live check rewrites it.

| table | columns (records.xml attribute → type) | key |
|---|---|---|
| players | playerKey VARCHAR(100), name VARCHAR(100), created DATETIME, lastPlayed DATETIME, unlockedLevel INT, ghost BOOLEAN | playerKey |
| runs | id INT (1-based position in the journal), player VARCHAR(100), playerKey VARCHAR(100), level INT, campaignLevels INT NULL (only level 0), outcome VARCHAR(20), time DECIMAL(10,2), attempts INT, stars INT, score INT, wallHits INT, falls INT, heartsLost FLOAT, barriersBroken INT, date DATETIME | id |
| ghosts | playerKey VARCHAR(100), level INT, time DECIMAL(10,2), stepMs FLOAT, points MEDIUMTEXT (Ghost element text, `x,y;x,y`) | (playerKey, level) |
| settings | id TINYINT (always 1), LastPlayer VARCHAR(100), FullTiltDegrees FLOAT, Deadzone FLOAT, Smoothing FLOAT, SwapAxes BOOLEAN, InvertX BOOLEAN, InvertY BOOLEAN | id |

Every non-key column is NOT NULL except `runs.campaignLevels`. Values are parsed with InvariantCulture: dates with `DateTimeStyles.RoundtripKind`, times as Decimal, booleans as `= "true"`, and a missing `campaignLevels` as `DBNull.Value`.

`Push` and the worker (the one algorithm the signatures don't fix):
```vb
Public Sub Push(root As XElement)
    SyncLock gate
        pending = root
        If busy Then Return
        busy = True
    End SyncLock
    Task.Run(AddressOf Drain)
End Sub
' Drain loops: under the lock take pending (Nothing → busy = False, exit); Status = SYNCING;
' Try write(next): Status = SYNCED; Catch ex: Status = OFFLINE…. Never rethrows.
```
`WriteAll`: open, `BeginTransaction`, `DELETE FROM` runs, ghosts, players, settings, then one parameterized `INSERT` per row (backtick-quoted column names from the row's keys, `@p0…`), then commit. Add a `ponytail:` comment saying a full replace per save is fine for a school-sized journal, and that you'd switch to per-row upserts keyed on `runs.id` if it gets slow.

- [ ] **Step 1: Write the failing `TestMySqlMirror()`**

Build the data with a temp `ScoreManager`: player "Jude"; `AddRecord` level 1 cleared with WallHits=3, HeartsLost=0.5, BarriersBroken=2, date 2026-09-30 14:02:11; `AddRecord` level 0 with CampaignLevels=4; a `RunOutcome.Quit` run with TimeSeconds=5; `SaveGhost(1, New GhostRun(12.5F, 96, {New PointF(1,2), New PointF(3,4)}))`; `Calibration.InvertX = True : SaveSettings()`. Capture `root` through `Saved`.
```vb
Dim t = MySqlMirror.Tables(root)
Check(t("players").Count = 1 AndAlso CStr(t("players")(0)("playerKey")) = "jude" AndAlso CStr(t("players")(0)("name")) = "Jude", "players row")
Check(t("runs").Count = 3 AndAlso CInt(t("runs")(0)("id")) = 1 AndAlso CInt(t("runs")(2)("id")) = 3, "runs keep journal order")
Check(CInt(t("runs")(0)("wallHits")) = 3 AndAlso CSng(t("runs")(0)("heartsLost")) = 0.5F AndAlso CInt(t("runs")(0)("barriersBroken")) = 2, "run counters")
Check(CDate(t("runs")(0)("date")) = New DateTime(2026, 9, 30, 14, 2, 11), "run date")
Check(t("runs")(0)("campaignLevels") Is DBNull.Value AndAlso CInt(t("runs")(1)("campaignLevels")) = 4, "campaignLevels only for level 0")
Check(CStr(t("runs")(2)("outcome")) = RunOutcome.Quit, "outcome kept")
Check(t("ghosts").Count = 1 AndAlso CStr(t("ghosts")(0)("points")) = "1.00,2.00;3.00,4.00" AndAlso CDec(t("ghosts")(0)("time")) = 12.5D, "ghost row")
Check(t("settings").Count = 1 AndAlso CBool(t("settings")(0)("InvertX")) AndAlso CStr(t("settings")(0)("LastPlayer")) = "Jude", "settings row")
' a legacy run with no player still maps (no FK)
' (add <Run player="" level="1" outcome="cleared" time="9.00" … /> to a root clone and check Tables doesn't throw and playerKey = "")
' queue: slow sink, non-blocking, coalescing, single writer, never throws
Dim calls As New List(Of XElement)() : Dim active As Integer = 0 : Dim overlap As Boolean = False
Dim m As New MySqlMirror(Sub(r)
    If Interlocked.Increment(active) > 1 Then overlap = True
    Thread.Sleep(300) : SyncLock calls : calls.Add(r) : End SyncLock
    Interlocked.Decrement(active)
End Sub)
Dim sw = Stopwatch.StartNew()
Dim a As New XElement("A"), b As New XElement("B"), c As New XElement("C")
m.Push(a) : m.Push(b) : m.Push(c)
Check(sw.ElapsedMilliseconds < 100, "Push never blocks")
Check(m.WaitIdle(5000) AndAlso Not overlap, "one write at a time")
Check(calls.Count <= 2 AndAlso calls.Last() Is c, "latest snapshot wins")
Check(MySqlMirror.Status.StartsWith("MYSQL SYNCED"), "synced status")
Dim boom As New MySqlMirror(Sub(r) Throw New InvalidOperationException("no server"))
boom.Push(a)
Check(boom.WaitIdle(2000) AndAlso MySqlMirror.Status.StartsWith("MYSQL OFFLINE · XML ONLY · no server"), "failure → offline status, no throw")
' config: missing file → template written, FromConfig Nothing; template parses; bad port → Nothing + CONFIG ERROR
Check(MySqlMirror.FromConfig(ini) Is Nothing AndAlso File.Exists(ini) AndAlso MySqlMirror.Status = "MYSQL OFF · EDIT mysql.ini", "missing config")
Check(MySqlMirror.ConnectionStringFrom(ini).Contains("Server=localhost") , "template parses")
File.WriteAllText(ini, "port=abc")
Check(MySqlMirror.FromConfig(ini) Is Nothing AndAlso MySqlMirror.Status.StartsWith("MYSQL CONFIG ERROR"), "bad port")
MySqlMirror.Status = ""   ' keep UiTour screenshots free of mirror text
```
(The implementer adapts the exact connection-string substring to what `MySqlConnectionStringBuilder` emits.)

`TestMySqlLive()`: if env `GM_MYSQL_INI` is unset, print `  -> MySQL live check SKIPPED (set GM_MYSQL_INI to an ini whose database is a throwaway, not gravitymaze)` and return. Otherwise build the connection string from it. If its database is `gravitymaze` (case-insensitive), fail with "live check needs a throwaway database". Run `gravitymaze.sql` (from `ProjectRoot\GravityMaze\Data`) with `\bgravitymaze\b` replaced by that database name, on a connection without a database set. Call `WriteAll` twice with the root from above, then assert `COUNT(*)` of runs = 3, players = 1, ghosts = 1, settings = 1, and `wallHits` of id 1 = 3. Finally `DROP DATABASE`, and reset Status.

- [ ] **Step 2:** Run `--data-only`. Expected: compile error, `MySqlMirror` undefined.
- [ ] **Step 3:** Add the package, then write `gravitymaze.sql` and `MySqlMirror.vb` per Interfaces.
- [ ] **Step 4:** Rerun `--data-only`. Expected: the mirror checks pass and the live check prints SKIPPED.
- [ ] **Step 5:** Run the full check set (all six check projects must still build without MySqlConnector), clean the screenshots, then commit `Add MySQL mirror for records with schema script`.

### Task 3: Wire it in, show status, document

**Files:**
- Modify: `GravityMaze/Form1.vb:55` (construction), `GravityMaze/UI/Screens/GameShell.Draw.vb` `DrawTitle` (lines 184-191), `SETUP.md` (§2 package line and a new MySQL section before Troubleshooting), `Gravity_Maze_PRD.md` §9

**Interfaces:**
- Consumes: `MySqlMirror.FromConfig`, `DefaultConfigPath`, `Push`, `Status`; `ScoreManager.Saved`, `ToXml` (Friend, same assembly).

- [ ] **Step 1:** In Form1, before `New GameShell`, add:
```vb
Dim scores As New ScoreManager(ScoreManager.DefaultPath())
Dim mirror As MySqlMirror = MySqlMirror.FromConfig(MySqlMirror.DefaultConfigPath())
If mirror IsNot Nothing Then
    scores.Saved = AddressOf mirror.Push
    mirror.Push(scores.ToXml())   ' backfill whatever records.xml already holds
End If
```
Then pass `scores` to the GameShell. Keep it inside the existing Try.
- [ ] **Step 2:** In `DrawTitle`, when `MySqlMirror.Status <> ""`, draw it right-aligned at `b.Right - 48 * s`, `48 * s` from the top. Use `GameFonts.Body(16 * s, GameFonts.FontWeight.Bold)` in `pal.Accent` if it starts with `MYSQL SYNCED`, else `pal.TextDim`, with align 1 and tracking `2 * s`.
- [ ] **Step 3:** `SETUP.md`: change "only package is System.IO.Ports" to name MySqlConnector too. Add a section "MySQL records mirror (optional)" covering: MySQL 8 already installed, or install MySQL Server + Workbench; run `GravityMaze\Data\gravitymaze.sql` in Workbench (File → Open SQL Script → ⚡); start the game once so `%AppData%\GravityMaze\mysql.ini` appears, then fill in user/password and restart; the title top-right shows `MYSQL SYNCED hh:mm`, and if not, the status says why; check the data in Workbench (`SELECT * FROM gravitymaze.runs;`). Note that it's a full copy on every save, so it catches up after downtime, and that XML is still the main save. Also the optional live check: `GM_MYSQL_INI=<ini with database=gravitymaze_check>`.
- [ ] **Step 4:** PRD §9: append that every successful XML save is also mirrored to a local MySQL database (players, runs, ghosts, settings tables, same fields), with XML staying primary and the game working without MySQL.
- [ ] **Step 5:** Run the full check set, clean the screenshots, then commit `Wire MySQL mirror into the game and document setup`.
