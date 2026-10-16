# Level 3 Overdrive Barriers, Player Profiles & Run Journal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Level 3's countdown with Overdrive Barriers, rebuild `records.xml` around player profiles and a journal of every attempt (with saved progress and best-run ghosts), and add the calibration screen, auto-pause on unplug and journaled instant restart.

**Architecture:** `Data/ScoreManager.vb` becomes XML v2 (profiles, runs, ghosts, calibration) while keeping its existing public API, so every current caller still compiles. `GameEngine` owns barrier state and run counters and raises events; `MazeRenderer`/`GameCanvas` only draw what the shell hands them. New shell screens live in a new partial file `GameShell.Players.vb` and reuse the tilt-driven `MenuList`, so the Arduino and the keyboard work on every screen.

**Tech Stack:** VB.NET WinForms on .NET 10 (`net10.0-windows`), GDI+, XML via `System.Xml.Linq`, Python 3 for the maze tool. Checks are console apps under `tools/` that throw on failure (`Check(cond, msg)`).

**Spec:** `docs/superpowers/specs/2026-10-16-level3-barriers-profiles-design.md` (read it first, including its "Plan-time corrections" section)

## Global Constraints

- Every VB file starts with `Option Strict On` / `Option Explicit On`. Match the surrounding style: 4-space indent, `' ──` section comments, short comments, `ponytail:` comments on deliberate simplifications.
- `Engine/` never references UI, rendering or input. `Data/` never references UI or input. Renderer has no gameplay logic (`Gravity_Maze_AGENTS.md`).
- New `Data` types all live in `GravityMaze/Data/ScoreManager.vb` (the check projects link that single file).
- Every new screen works with tilt (up/down = move, hold right = confirm, hold left = back) **and** keyboard (arrows/WASD, Enter/Space, Esc). Only typing a new name needs the keyboard. No Arduino firmware change.
- `R` restart stays keyboard-only. No shake gesture.
- Barrier constants: `BarrierBreakSpeed = 0.16F` tiles/tick (above `BaseMaxSpeed` 0.12, below `FastMaxSpeed` 0.24), `BarrierSmashDamping = 0.5F`.
- Ghost sampling: `GhostRun.DefaultStepMs = 96` (every 6th 16 ms tick ≈ 10 Hz).
- The optional countdown code (`LevelConfig.TimeLimitSecs`, `GameState.TimeUp`, the TimeUp screen) stays in the code but **no level uses it** (all limits `0`).
- **Work in a git worktree** on a new branch `feature/barriers-profiles` created from `feature/dungeon-revamp`. The main checkout has uncommitted screenshot changes that must not be touched.
- `tools/VerificationRunner` writes screenshots under `GM_ROOT` and defaults to the **main checkout path**. Always run it from the worktree root as `GM_ROOT="$PWD" dotnet run --project tools/VerificationRunner ...`.
- Full check set (each must exit 0; VerificationRunner must end with `ALL VERIFICATIONS PASSED SUCCESSFULLY!`):
  ```bash
  dotnet build GravityMaze/GravityMaze.vbproj
  for d in CameraChecks DungeonChecks HealthChecks MarbleChecks PerspectiveChecks TransitionChecks; do dotnet run --project tools/$d || exit 1; done
  GM_ROOT="$PWD" dotnet run --project tools/VerificationRunner
  ```
- Do not commit files under `screenshots/` except in Task 10. Before each commit run `git checkout -- screenshots` and `git clean -fd screenshots` in the worktree.
- Commit after each task. End every commit message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Do not push.
- Agent roles: Opus orchestrates and reviews, Sonnet (`model: "sonnet"`) implements, Haiku (`model: "haiku"`) explores.

## Review Focus

1. **An old `records.xml` with "Jude" and "jude"** must load as one profile with no lost records, keep the first spelling, get the right unlocked level, and be rewritten as v2 on the next save (Task 1 migration test).
2. **Holding the ball against a barrier at normal speed** must cost one quarter-heart and show one "TOO SLOW", not one per tick (Task 2 slow-hit test).
3. **Falling into the pit right after smashing a barrier** must keep the barrier open; only restart rebuilds it (Task 2 fall test).
4. **Running out of hearts and then pressing R** must journal exactly one run (`out-of-hearts`), never a second `restart`. A run where the ball never moved must journal nothing (Task 5 journal test).
5. **Continuing a campaign from Level 2 and finishing** must save every level's run but no campaign record; a Level 1 → end run still posts one (Task 5 continue test).

---

### Task 1: XML v2 data layer (profiles, run journal, ghosts, calibration, migration)

**Files:**
- Modify: `GravityMaze/Data/ScoreManager.vb` (full rewrite below)
- Modify: `tools/VerificationRunner/Program.vb` (add `TestProfilesAndJournal`, `--data-only` flag)

**Interfaces:**
- Produces (all `Namespace Data`):
  - `RunOutcome` constants: `Cleared="cleared"`, `OutOfHearts="out-of-hearts"`, `Restart="restart"`, `Quit="quit"`, `TimeUp="time-up"`.
  - `ScoreRecord` gains `Outcome As String`, `WallHits As Integer`, `Falls As Integer`, `HeartsLost As Single`, `BarriersBroken As Integer`, `ReadOnly Cleared As Boolean`.
  - `GhostRun(timeSeconds As Single, stepMs As Single, points As IEnumerable(Of PointF))`, `.Points`, `.StepMs`, `.TimeSeconds`, `.PositionAt(seconds As Single) As PointF`, `.Encode() As String`, `Shared Decode(time, stepMs, text) As GhostRun` (Nothing if malformed), `Const DefaultStepMs = 96.0F`.
  - `PlayerProfile` (`Key`, `Name`, `Created`, `LastPlayed`, `UnlockedLevel`, `GhostEnabled`, `Ghosts As Dictionary(Of Integer, GhostRun)`).
  - `CalibrationSettings` (`FullTiltDegrees`, `Deadzone`, `Smoothing`, `SwapAxes`, `InvertX`, `InvertY`).
  - `LevelStats` (`Attempts`, `Clears`, `BestTime` (-1 = none), `BestStars`, `Outcomes As Dictionary(Of String, Integer)`).
  - `ScoreManager`: existing `PlayerName`, `AddRecord`, `BestRecord`, `BestRecordFor`, `TopRecords`, `ComputeScore`, `DefaultPath` keep their signatures. New: `Shared KeyOf(name) As String`, `Players As List(Of PlayerProfile)` (most recent first), `FindPlayer(name) As PlayerProfile`, `UnlockedLevel As Integer`, `Unlock(levelNumber)`, `GhostEnabled As Boolean` (read/write), `GhostFor(levelNumber) As GhostRun`, `SaveGhost(levelNumber, ghost)`, `RunsFor(playerName, levelNumber) As List(Of ScoreRecord)`, `StatsFor(playerName, levelNumber) As LevelStats`, `Calibration As CalibrationSettings`, `SaveSettings()`.

- [ ] **Step 1: Create the worktree and branch**

```bash
git worktree add ../RICK-MAZE-barriers -b feature/barriers-profiles feature/dungeon-revamp
cd ../RICK-MAZE-barriers
```

All later commands run from this worktree root.

- [ ] **Step 2: Write the failing test**

In `tools/VerificationRunner/Program.vb`, add this sub after `TestScoreManager`:

```vb
        Private Sub TestProfilesAndJournal()
            Console.WriteLine("[TEST] Profiles, run journal, ghosts, calibration, v1 upgrade...")
            Dim dir As String = Path.Combine(Path.GetTempPath(), "gm_profiles_" & Guid.NewGuid().ToString("N"))
            Dim xmlPath As String = Path.Combine(dir, "records.xml")
            Try
                Check(ScoreManager.KeyOf("  Jude  ") = "jude" AndAlso ScoreManager.KeyOf("JUDE") = "jude" AndAlso
                      ScoreManager.KeyOf("Mary   Ann") = "mary ann", "KeyOf trims, collapses spaces, ignores case")
                Dim sm As New ScoreManager(xmlPath)
                sm.PlayerName = "Jude"
                sm.PlayerName = " jude "
                Check(sm.Players.Count = 1 AndAlso sm.PlayerName = "Jude", "same name in any case is one player, first spelling kept")

                Dim run = Function(lvl As Integer, outcome As String, t As Single) New ScoreRecord With {
                    .PlayerName = "JUDE", .LevelNumber = lvl, .Outcome = outcome, .TimeSeconds = t, .Attempts = 1,
                    .WallHits = 3, .Falls = 1, .HeartsLost = 1.75F, .BarriersBroken = 2, .Date = DateTime.Now}
                Check(Not sm.AddRecord(run(1, RunOutcome.Quit, 12.0F)), "a quit is never a best")
                sm.AddRecord(run(1, RunOutcome.Quit, 0.0F))
                Check(sm.RunsFor("jude", 1).Count = 1, "a run with no time is not journaled")
                Check(sm.TopRecords(1, 10).Count = 0 AndAlso sm.BestRecordFor("Jude", 1) Is Nothing, "unfinished runs are not records")
                Check(sm.UnlockedLevel = 1, "nothing unlocked before a clear")
                Check(sm.AddRecord(run(1, RunOutcome.Cleared, 9.5F)), "first clear is a best")
                Check(sm.UnlockedLevel = 2, "clearing level 1 unlocks level 2")
                sm.AddRecord(run(2, RunOutcome.OutOfHearts, 20.0F))
                Check(sm.UnlockedLevel = 2, "failing level 2 unlocks nothing")
                sm.Unlock(1)
                Check(sm.UnlockedLevel = 2, "Unlock never lowers progress")

                Dim st As LevelStats = sm.StatsFor("JUDE", 1)
                Check(st.Attempts = 2 AndAlso st.Clears = 1 AndAlso Math.Abs(st.BestTime - 9.5F) < 0.001F AndAlso
                      st.Outcomes(RunOutcome.Quit) = 1 AndAlso st.Outcomes(RunOutcome.Cleared) = 1, "stats derive from the journal")
                Check(sm.StatsFor("JUDE", 3).BestTime < 0, "never-cleared level has no best time")

                sm.SaveGhost(1, New GhostRun(9.5F, 96.0F, {New PointF(1.5F, 2.5F), New PointF(2.5F, 2.5F)}))
                sm.GhostEnabled = False
                sm.Calibration.InvertX = True
                sm.Calibration.FullTiltDegrees = 15.0F
                sm.SaveSettings()

                Dim sm2 As New ScoreManager(xmlPath)
                Check(sm2.PlayerName = "Jude" AndAlso sm2.UnlockedLevel = 2, "player and progress reload")
                Check(sm2.RunsFor("Jude", 1).Count = 2 AndAlso sm2.RunsFor("Jude", 1)(0).Outcome = RunOutcome.Quit AndAlso
                      sm2.RunsFor("Jude", 1)(0).WallHits = 3 AndAlso sm2.RunsFor("Jude", 1)(0).HeartsLost = 1.75F, "journal fields reload")
                Dim ghost As GhostRun = sm2.GhostFor(1)
                Check(ghost IsNot Nothing AndAlso ghost.Points.Count = 2, "ghost reloads")
                Dim mid As PointF = ghost.PositionAt(0.048F)
                Check(Math.Abs(mid.X - 2.0F) < 0.01F AndAlso mid.Y = 2.5F, "ghost interpolates between samples")
                Check(ghost.PositionAt(99.0F) = ghost.Points(1), "ghost waits at its last point")
                Check(Not sm2.GhostEnabled AndAlso sm2.Calibration.InvertX AndAlso sm2.Calibration.FullTiltDegrees = 15.0F, "settings reload")
                Check(GhostRun.Decode(1.0F, 96.0F, "1.5,2.5;oops") Is Nothing AndAlso GhostRun.Decode(1.0F, 96.0F, "1.5,2.5") Is Nothing,
                      "malformed or one-point ghosts are rejected")

                ' v1 upgrade: mixed-case names merge, first spelling wins, progress is derived from clears.
                File.WriteAllText(xmlPath,
                    "<GravityMazeRecords version=""1""><Settings PlayerName=""jude""/>" &
                    "<Record player=""Jude"" level=""1"" time=""9.00"" attempts=""1"" stars=""3"" score=""100"" date=""2026-09-30T10:00:00""/>" &
                    "<Record player=""jude"" level=""2"" time=""40.00"" attempts=""2"" stars=""2"" score=""100"" date=""2026-09-30T10:05:00""/>" &
                    "<Record player=""ANA"" level=""1"" time=""8.00"" attempts=""1"" stars=""3"" score=""100"" date=""2026-09-30T11:00:00""/>" &
                    "<Record player=""jude"" level=""0"" time=""90.00"" attempts=""1"" stars=""8"" score=""0"" date=""2026-09-30T11:30:00""/>" &
                    "</GravityMazeRecords>")
                Dim v1 As New ScoreManager(xmlPath)
                Check(v1.Players.Count = 2 AndAlso v1.PlayerName = "Jude", "v1 names merge regardless of case")
                Check(v1.UnlockedLevel = 3, "v1 progress derived from clears")
                Check(v1.RunsFor("JUDE", 2)(0).PlayerName = "Jude" AndAlso v1.RunsFor("jude", 2)(0).Cleared, "v1 records become cleared runs under the profile name")
                Check(v1.BestRecord(0, 3) IsNot Nothing, "v1 campaign stays a three-level record")
                v1.SaveSettings()
                Dim text As String = File.ReadAllText(xmlPath)
                Check(text.Contains("version=""2""") AndAlso text.Contains("<Run ") AndAlso Not text.Contains("<Record "), "file rewritten as v2")
                Console.WriteLine("  -> case-insensitive profiles, journal, stats, progress, ghosts, settings, v1 upgrade OK")
            Finally
                Try
                    Directory.Delete(dir, True)
                Catch
                End Try
            End Try
        End Sub
```

In `Main`, add the flag at the top with the other flags, and call the test after `TestScoreManager()`:

```vb
            If Array.IndexOf(Environment.GetCommandLineArgs(), "--data-only") >= 0 Then
                TestScoreManager()
                TestProfilesAndJournal()
                Return
            End If
```

```vb
            TestScoreManager()
            TestProfilesAndJournal()
```

- [ ] **Step 3: Run the test and confirm it fails**

Run: `GM_ROOT="$PWD" dotnet run --project tools/VerificationRunner -- --data-only`
Expected: build errors, because `RunOutcome`, `GhostRun`, `LevelStats` and `KeyOf` are not defined yet.

- [ ] **Step 4: Rewrite `GravityMaze/Data/ScoreManager.vb`**

```vb
Option Strict On
Option Explicit On

Imports System
Imports System.IO
Imports System.Linq
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.Xml.Linq

Namespace Data
    ' How an attempt ended. Only cleared runs compete for records.
    Public NotInheritable Class RunOutcome
        Public Const Cleared As String = "cleared"
        Public Const OutOfHearts As String = "out-of-hearts"
        Public Const Restart As String = "restart"
        Public Const Quit As String = "quit"
        Public Const TimeUp As String = "time-up"

        Private Sub New()
        End Sub
    End Class

    ' One attempt. LevelNumber 0 = full campaign run.
    Public NotInheritable Class ScoreRecord
        Public Property PlayerName As String = ""
        Public Property LevelNumber As Integer
        Public Property CampaignLevels As Integer = 3 ' Legacy full runs contained three levels.
        Public Property Outcome As String = RunOutcome.Cleared
        Public Property TimeSeconds As Single
        Public Property Attempts As Integer
        Public Property Stars As Integer
        Public Property Score As Integer
        Public Property WallHits As Integer
        Public Property Falls As Integer
        Public Property HeartsLost As Single
        Public Property BarriersBroken As Integer
        Public Property [Date] As DateTime

        Public ReadOnly Property Cleared As Boolean
            Get
                Return Outcome = RunOutcome.Cleared
            End Get
        End Property
    End Class

    ' The best clear's marble path, sampled every StepMs, raced as a translucent ghost.
    Public NotInheritable Class GhostRun
        Public Const DefaultStepMs As Single = 96.0F   ' every 6th 16 ms tick, about 10 Hz

        Public ReadOnly Property TimeSeconds As Single
        Public ReadOnly Property StepMs As Single
        Public ReadOnly Property Points As IReadOnlyList(Of PointF)

        Public Sub New(timeSeconds As Single, stepMs As Single, points As IEnumerable(Of PointF))
            Me.TimeSeconds = timeSeconds
            Me.StepMs = stepMs
            Me.Points = points.ToList()
        End Sub

        Public Function PositionAt(seconds As Single) As PointF
            If Points.Count = 0 Then Return PointF.Empty
            Dim f As Single = Math.Max(0.0F, seconds * 1000.0F / StepMs)
            Dim i As Integer = CInt(Math.Floor(f))
            If i >= Points.Count - 1 Then Return Points(Points.Count - 1)
            Dim t As Single = f - i
            Dim a As PointF = Points(i)
            Dim b As PointF = Points(i + 1)
            Return New PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t)
        End Function

        Public Function Encode() As String
            Return String.Join(";", Points.Select(Function(p) p.X.ToString("0.00", CultureInfo.InvariantCulture) & "," &
                                                              p.Y.ToString("0.00", CultureInfo.InvariantCulture)))
        End Function

        ' Malformed data returns Nothing, so a bad ghost is skipped instead of failing the load.
        Public Shared Function Decode(timeSeconds As Single, stepMs As Single, text As String) As GhostRun
            Dim pts As New List(Of PointF)()
            For Each pair As String In If(text, "").Split(";"c, StringSplitOptions.RemoveEmptyEntries)
                Dim xy As String() = pair.Split(","c)
                Dim x, y As Single
                If xy.Length <> 2 OrElse
                   Not Single.TryParse(xy(0), NumberStyles.Float, CultureInfo.InvariantCulture, x) OrElse
                   Not Single.TryParse(xy(1), NumberStyles.Float, CultureInfo.InvariantCulture, y) Then Return Nothing
                pts.Add(New PointF(x, y))
            Next
            If pts.Count < 2 OrElse stepMs <= 0 Then Return Nothing
            Return New GhostRun(timeSeconds, stepMs, pts)
        End Function
    End Class

    Public NotInheritable Class PlayerProfile
        Public Property Key As String = ""
        Public Property Name As String = ""
        Public Property Created As DateTime
        Public Property LastPlayed As DateTime
        Public Property UnlockedLevel As Integer = 1
        Public Property GhostEnabled As Boolean = True
        Public ReadOnly Property Ghosts As New Dictionary(Of Integer, GhostRun)()
    End Class

    ' Controller tuning belongs to the machine (it describes the hardware), not to a player.
    Public NotInheritable Class CalibrationSettings
        Public Property FullTiltDegrees As Single = 20.0F
        Public Property Deadzone As Single = 0.08F
        Public Property Smoothing As Single = 0.35F
        Public Property SwapAxes As Boolean
        Public Property InvertX As Boolean
        Public Property InvertY As Boolean
    End Class

    ' Worked out from the journal for the Profile screen; never stored.
    Public NotInheritable Class LevelStats
        Public Property Attempts As Integer
        Public Property Clears As Integer
        Public Property BestTime As Single = -1.0F   ' -1 = never cleared
        Public Property BestStars As Integer
        Public ReadOnly Property Outcomes As New Dictionary(Of String, Integer)()
    End Class

    ' XML v2: player profiles (progress, ghosts), a journal of every attempt, and controller settings.
    ' v1 files (cleared <Record>s only) are upgraded on load and rewritten as v2 on the next save.
    Public NotInheritable Class ScoreManager
        Private ReadOnly filePath As String
        Private ReadOnly runs As New List(Of ScoreRecord)()
        Private ReadOnly profiles As New List(Of PlayerProfile)()
        Private current As PlayerProfile
        Private _calibration As New CalibrationSettings()
        Private Shared ReadOnly Inv As CultureInfo = CultureInfo.InvariantCulture

        Public Sub New(filePath As String)
            Me.filePath = filePath
            Load()
        End Sub

        Public Shared Function DefaultPath() As String
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GravityMaze", "records.xml")
        End Function

        ' One player per name regardless of capitals or extra spaces: "  jude " = "Jude" = "JUDE".
        Public Shared Function KeyOf(name As String) As String
            Return CleanName(name).ToLowerInvariant()
        End Function

        Private Shared Function CleanName(name As String) As String
            Return String.Join(" ", If(name, "").Split(CType(Nothing, Char()), StringSplitOptions.RemoveEmptyEntries))
        End Function

        ' ── Players & progress ─────────────────────────────────────────────
        Public ReadOnly Property Players As List(Of PlayerProfile)
            Get
                Return profiles.OrderByDescending(Function(p) p.LastPlayed).ToList()
            End Get
        End Property

        Public Function FindPlayer(name As String) As PlayerProfile
            Dim key As String = KeyOf(name)
            Return profiles.FirstOrDefault(Function(p) p.Key = key)
        End Function

        ' Current player's display name ("" = nobody chosen). Setting it selects, or creates, that profile.
        Public Property PlayerName As String
            Get
                Return If(current Is Nothing, "", current.Name)
            End Get
            Set(value As String)
                current = If(KeyOf(value).Length = 0, Nothing, EnsurePlayer(value))
                If current IsNot Nothing Then current.LastPlayed = DateTime.Now
                Save()
            End Set
        End Property

        Public ReadOnly Property UnlockedLevel As Integer
            Get
                Return If(current Is Nothing, 1, current.UnlockedLevel)
            End Get
        End Property

        Public Sub Unlock(levelNumber As Integer)
            If current Is Nothing OrElse levelNumber <= current.UnlockedLevel Then Return
            current.UnlockedLevel = levelNumber
            Save()
        End Sub

        Public Property GhostEnabled As Boolean
            Get
                Return current IsNot Nothing AndAlso current.GhostEnabled
            End Get
            Set(value As Boolean)
                If current Is Nothing Then Return
                current.GhostEnabled = value
                Save()
            End Set
        End Property

        Public Function GhostFor(levelNumber As Integer) As GhostRun
            Dim ghost As GhostRun = Nothing
            If current IsNot Nothing Then current.Ghosts.TryGetValue(levelNumber, ghost)
            Return ghost
        End Function

        Public Sub SaveGhost(levelNumber As Integer, ghost As GhostRun)
            If current Is Nothing OrElse ghost Is Nothing OrElse ghost.Points.Count < 2 Then Return
            current.Ghosts(levelNumber) = ghost
            Save()
        End Sub

        Public ReadOnly Property Calibration As CalibrationSettings
            Get
                Return _calibration
            End Get
        End Property

        Public Sub SaveSettings()
            Save()
        End Sub

        Private Function EnsurePlayer(name As String) As PlayerProfile
            Dim p As PlayerProfile = FindPlayer(name)
            If p IsNot Nothing Then Return p
            p = New PlayerProfile With {.Key = KeyOf(name), .Name = CleanName(name), .Created = DateTime.Now, .LastPlayed = DateTime.Now}
            profiles.Add(p)
            Return p
        End Function

        ' ── Run journal ────────────────────────────────────────────────────
        ' Journals one attempt and saves. Returns True if it is a new personal-best clear
        ' (a player's first clear counts). Nameless runs and unfinished runs with no time are ignored.
        Public Function AddRecord(record As ScoreRecord) As Boolean
            If KeyOf(record.PlayerName).Length = 0 Then Return False
            If Not record.Cleared AndAlso record.TimeSeconds <= 0 Then Return False
            Dim p As PlayerProfile = EnsurePlayer(record.PlayerName)
            record.PlayerName = p.Name
            If record.[Date] > p.LastPlayed Then p.LastPlayed = record.[Date]
            Dim isBest As Boolean = False
            If record.Cleared Then
                Dim prev As ScoreRecord = BestRecordFor(p.Name, record.LevelNumber, If(record.LevelNumber = 0, record.CampaignLevels, 0))
                isBest = prev Is Nothing OrElse record.TimeSeconds < prev.TimeSeconds
                If record.LevelNumber > 0 Then p.UnlockedLevel = Math.Max(p.UnlockedLevel, record.LevelNumber + 1)
            End If
            runs.Add(record)
            Save()
            Return isBest
        End Function

        Private Function Clears(levelNumber As Integer, campaignLevels As Integer) As IEnumerable(Of ScoreRecord)
            Return runs.Where(Function(r) r.Cleared AndAlso r.LevelNumber = levelNumber AndAlso
                                  (levelNumber <> 0 OrElse campaignLevels <= 0 OrElse r.CampaignLevels = campaignLevels))
        End Function

        Public Function BestRecord(levelNumber As Integer, Optional campaignLevels As Integer = 0) As ScoreRecord
            Return Clears(levelNumber, campaignLevels).OrderBy(Function(r) r.TimeSeconds).FirstOrDefault()
        End Function

        Public Function BestRecordFor(playerName As String, levelNumber As Integer, Optional campaignLevels As Integer = 0) As ScoreRecord
            Dim key As String = KeyOf(playerName)
            Return Clears(levelNumber, campaignLevels).Where(Function(r) KeyOf(r.PlayerName) = key).
                OrderBy(Function(r) r.TimeSeconds).FirstOrDefault()
        End Function

        Public Function TopRecords(levelNumber As Integer, count As Integer, Optional campaignLevels As Integer = 0) As List(Of ScoreRecord)
            Return Clears(levelNumber, campaignLevels).OrderBy(Function(r) r.TimeSeconds).Take(Math.Max(0, count)).ToList()
        End Function

        ' Every attempt (any outcome) by one player on one level, oldest first.
        Public Function RunsFor(playerName As String, levelNumber As Integer) As List(Of ScoreRecord)
            Dim key As String = KeyOf(playerName)
            Return runs.Where(Function(r) r.LevelNumber = levelNumber AndAlso KeyOf(r.PlayerName) = key).ToList()
        End Function

        Public Function StatsFor(playerName As String, levelNumber As Integer) As LevelStats
            Dim stats As New LevelStats()
            For Each r As ScoreRecord In RunsFor(playerName, levelNumber)
                stats.Attempts += 1
                Dim n As Integer = 0
                stats.Outcomes.TryGetValue(r.Outcome, n)
                stats.Outcomes(r.Outcome) = n + 1
                If Not r.Cleared Then Continue For
                stats.Clears += 1
                stats.BestStars = Math.Max(stats.BestStars, r.Stars)
                If stats.BestTime < 0 OrElse r.TimeSeconds < stats.BestTime Then stats.BestTime = r.TimeSeconds
            Next
            Return stats
        End Function

        ' score = stars*1000 + max(0, 3000 - time*25) - (attempts-1)*150, floored at 0
        Public Shared Function ComputeScore(timeSeconds As Single, attempts As Integer, stars As Integer) As Integer
            Dim s As Double = stars * 1000.0 + Math.Max(0.0, 3000.0 - timeSeconds * 25.0) - (Math.Max(1, attempts) - 1) * 150.0
            Return CInt(Math.Max(0.0, Math.Round(s)))
        End Function

        ' ── XML ─────────────────────────────────────────────────────────────
        Private Shared Function Attr(e As XElement, name As String, fallback As String) As String
            Return If(CStr(e.Attribute(name)), fallback)
        End Function

        Private Shared Function DateAttr(e As XElement, name As String) As DateTime
            Dim text As String = CStr(e.Attribute(name))
            Return If(text Is Nothing, DateTime.Now, DateTime.Parse(text, Inv, DateTimeStyles.RoundtripKind))
        End Function

        Private Sub Load()
            If Not File.Exists(filePath) Then Return
            Try
                Dim root As XElement = XDocument.Load(filePath).Root
                If root Is Nothing OrElse root.Name.LocalName <> "GravityMazeRecords" Then Throw New FormatException("bad root")

                Dim loadedProfiles As New List(Of PlayerProfile)()
                For Each e As XElement In root.Elements("Player")
                    Dim name As String = Attr(e, "name", "")
                    Dim p As New PlayerProfile With {
                        .Key = KeyOf(name), .Name = CleanName(name),
                        .Created = DateAttr(e, "created"), .LastPlayed = DateAttr(e, "lastPlayed"),
                        .UnlockedLevel = Integer.Parse(Attr(e, "unlockedLevel", "1"), Inv),
                        .GhostEnabled = Attr(e, "ghost", "true") = "true"}
                    For Each ge As XElement In e.Elements("Ghost")
                        Dim ghost As GhostRun = GhostRun.Decode(Single.Parse(Attr(ge, "time", "0"), Inv),
                                                                Single.Parse(Attr(ge, "stepMs", "0"), Inv), ge.Value)
                        If ghost IsNot Nothing Then p.Ghosts(Integer.Parse(Attr(ge, "level", "0"), Inv)) = ghost
                    Next
                    If p.Key.Length > 0 AndAlso Not loadedProfiles.Any(Function(x) x.Key = p.Key) Then loadedProfiles.Add(p)
                Next

                Dim loadedRuns As New List(Of ScoreRecord)()
                For Each e As XElement In root.Elements().Where(Function(x) x.Name.LocalName = "Run" OrElse x.Name.LocalName = "Record")
                    loadedRuns.Add(New ScoreRecord With {
                        .PlayerName = Attr(e, "player", ""),
                        .LevelNumber = Integer.Parse(Attr(e, "level", "0"), Inv),
                        .CampaignLevels = Integer.Parse(Attr(e, "campaignLevels", "3"), Inv),
                        .Outcome = Attr(e, "outcome", RunOutcome.Cleared),
                        .TimeSeconds = Single.Parse(Attr(e, "time", "0"), Inv),
                        .Attempts = Integer.Parse(Attr(e, "attempts", "1"), Inv),
                        .Stars = Integer.Parse(Attr(e, "stars", "0"), Inv),
                        .Score = Integer.Parse(Attr(e, "score", "0"), Inv),
                        .WallHits = Integer.Parse(Attr(e, "wallHits", "0"), Inv),
                        .Falls = Integer.Parse(Attr(e, "falls", "0"), Inv),
                        .HeartsLost = Single.Parse(Attr(e, "heartsLost", "0"), Inv),
                        .BarriersBroken = Integer.Parse(Attr(e, "barriersBroken", "0"), Inv),
                        .[Date] = DateAttr(e, "date")})
                Next

                Dim loadedCalibration As New CalibrationSettings()
                Dim last As String = ""
                Dim st As XElement = root.Element("Settings")
                If st IsNot Nothing Then
                    last = If(CStr(st.Attribute("LastPlayer")), Attr(st, "PlayerName", ""))   ' v1 used PlayerName
                    loadedCalibration.FullTiltDegrees = Single.Parse(Attr(st, "FullTiltDegrees", "20"), Inv)
                    loadedCalibration.Deadzone = Single.Parse(Attr(st, "Deadzone", "0.08"), Inv)
                    loadedCalibration.Smoothing = Single.Parse(Attr(st, "Smoothing", "0.35"), Inv)
                    loadedCalibration.SwapAxes = Attr(st, "SwapAxes", "false") = "true"
                    loadedCalibration.InvertX = Attr(st, "InvertX", "false") = "true"
                    loadedCalibration.InvertY = Attr(st, "InvertY", "false") = "true"
                End If

                profiles.AddRange(loadedProfiles)
                runs.AddRange(loadedRuns)
                _calibration = loadedCalibration
                ' v1 upgrade (and repair): every named run gets a profile under its first-seen spelling,
                ' and progress covers every level already cleared.
                For Each r As ScoreRecord In runs
                    If KeyOf(r.PlayerName).Length = 0 Then Continue For
                    Dim p As PlayerProfile = EnsurePlayer(r.PlayerName)
                    r.PlayerName = p.Name
                    If r.Cleared AndAlso r.LevelNumber > 0 Then p.UnlockedLevel = Math.Max(p.UnlockedLevel, r.LevelNumber + 1)
                Next
                current = FindPlayer(last)
            Catch
                ' corrupt file: keep a backup, start empty
                runs.Clear()
                profiles.Clear()
                current = Nothing
                _calibration = New CalibrationSettings()
                Try
                    File.Copy(filePath, filePath & ".bak", True)
                Catch
                End Try
            End Try
        End Sub

        Private Shared Function Bool(value As Boolean) As String
            Return If(value, "true", "false")
        End Function

        Private Sub Save()
            Try
                Dim c As CalibrationSettings = _calibration
                Dim root As New XElement("GravityMazeRecords", New XAttribute("version", "2"),
                    New XElement("Settings",
                        New XAttribute("LastPlayer", PlayerName),
                        New XAttribute("FullTiltDegrees", c.FullTiltDegrees.ToString(Inv)),
                        New XAttribute("Deadzone", c.Deadzone.ToString(Inv)),
                        New XAttribute("Smoothing", c.Smoothing.ToString(Inv)),
                        New XAttribute("SwapAxes", Bool(c.SwapAxes)),
                        New XAttribute("InvertX", Bool(c.InvertX)),
                        New XAttribute("InvertY", Bool(c.InvertY))))
                For Each p As PlayerProfile In profiles
                    root.Add(New XElement("Player",
                        New XAttribute("name", p.Name),
                        New XAttribute("created", p.Created.ToString("s", Inv)),
                        New XAttribute("lastPlayed", p.LastPlayed.ToString("s", Inv)),
                        New XAttribute("unlockedLevel", p.UnlockedLevel.ToString(Inv)),
                        New XAttribute("ghost", Bool(p.GhostEnabled)),
                        p.Ghosts.OrderBy(Function(kv) kv.Key).Select(Function(kv) New XElement("Ghost",
                            New XAttribute("level", kv.Key.ToString(Inv)),
                            New XAttribute("time", kv.Value.TimeSeconds.ToString("0.00", Inv)),
                            New XAttribute("stepMs", kv.Value.StepMs.ToString(Inv)),
                            kv.Value.Encode()))))
                Next
                For Each r As ScoreRecord In runs
                    root.Add(New XElement("Run",
                        New XAttribute("player", r.PlayerName),
                        New XAttribute("level", r.LevelNumber.ToString(Inv)),
                        If(r.LevelNumber = 0, New XAttribute("campaignLevels", r.CampaignLevels.ToString(Inv)), Nothing),
                        New XAttribute("outcome", r.Outcome),
                        New XAttribute("time", r.TimeSeconds.ToString("0.00", Inv)),
                        New XAttribute("attempts", r.Attempts.ToString(Inv)),
                        New XAttribute("stars", r.Stars.ToString(Inv)),
                        New XAttribute("score", r.Score.ToString(Inv)),
                        New XAttribute("wallHits", r.WallHits.ToString(Inv)),
                        New XAttribute("falls", r.Falls.ToString(Inv)),
                        New XAttribute("heartsLost", r.HeartsLost.ToString(Inv)),
                        New XAttribute("barriersBroken", r.BarriersBroken.ToString(Inv)),
                        New XAttribute("date", r.[Date].ToString("s", Inv))))
                Next
                Dim dir As String = Path.GetDirectoryName(filePath)
                If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
                Dim tmp As String = filePath & ".tmp"
                Dim doc As New XDocument(root)
                doc.Save(tmp)
                If File.Exists(filePath) Then
                    File.Replace(tmp, filePath, Nothing)
                Else
                    File.Move(tmp, filePath)
                End If
            Catch
                ' disk problems must never crash the game
            End Try
        End Sub
    End Class
End Namespace
```

- [ ] **Step 5: Run the data tests and confirm they pass**

Run: `GM_ROOT="$PWD" dotnet run --project tools/VerificationRunner -- --data-only`
Expected: `-> add/best/top/reload/corrupt recovery OK` and `-> case-insensitive profiles, journal, stats, progress, ghosts, settings, v1 upgrade OK`, exit code 0.

- [ ] **Step 6: Run the projects that link `ScoreManager.vb`**

Run: `dotnet build GravityMaze/GravityMaze.vbproj && dotnet run --project tools/DungeonChecks && dotnet run --project tools/TransitionChecks`
Expected: build succeeds; both checks print `PASS`. The existing callers compile unchanged.

- [ ] **Step 7: Commit**

```bash
git add GravityMaze/Data/ScoreManager.vb tools/VerificationRunner/Program.vb
git commit -m "Store players, every attempt, ghosts and calibration in records.xml v2

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Overdrive barriers and run counters in the engine

**Files:**
- Modify: `GravityMaze/Levels/MazeDefinition.vb:39` (accept `B`)
- Modify: `GravityMaze/Engine/GameEngine.vb` (barriers, counters, events)
- Create: `tools/VerificationRunner/BarrierChecks.vb`
- Modify: `tools/VerificationRunner/Program.vb` (`--barrier-only` flag; call `BarrierChecks.Run()`)

**Interfaces:**
- Produces on `GameEngine`: `BarrierBreakSpeed As Single = 0.16F`, `BarrierSmashDamping As Single = 0.5F`, `Event BarrierBroken As EventHandler(Of BarrierEventArgs)`, `Event BarrierBounced As EventHandler(Of BarrierEventArgs)`, `ReadOnly BarriersTotal As Integer`, `ReadOnly BarriersBroken As Integer`, `IsBarrierStanding(row As Integer, col As Integer) As Boolean`, `ReadOnly WallHits As Integer`, `ReadOnly Falls As Integer`, `ReadOnly HeartsLost As Single`. All reset by `Reset()`.
- Produces `Engine.BarrierEventArgs` with `X As Single`, `Y As Single` (tile space).

- [ ] **Step 1: Write the failing test** — create `tools/VerificationRunner/BarrierChecks.vb`:

```vb
Option Strict On
Option Explicit On

Imports System
Imports GravityMaze.Engine
Imports GravityMaze.Levels

Namespace VerificationRunner
    ' Overdrive barriers: smash on a boost, bounce (¼ heart) when too slow, stay open through falls.
    Public Module BarrierChecks
        Private Sub Check(condition As Boolean, message As String)
            If Not condition Then Throw New Exception(message)
        End Sub

        Public Sub Run()
            Console.WriteLine("[TEST] Overdrive barriers...")

            ' Four boost tiles into a barrier: smashed at full tilt, no health lost, ball rolls on.
            Dim boost As New MazeDefinition({"1111111111", "1SFFFFB0G1", "1111111111"})
            Dim e As New GameEngine(boost, 0)
            Dim broken As Integer = 0
            AddHandler e.BarrierBroken, Sub(s As Object, a As BarrierEventArgs) broken += 1
            Check(e.BarriersTotal = 1 AndAlso e.IsBarrierStanding(1, 6) AndAlso Not e.IsBarrierStanding(1, 5), "one standing barrier")
            For i As Integer = 1 To 200
                e.Update(1.0F, 0.0F)
                If broken > 0 Then Exit For
            Next
            Check(broken = 1 AndAlso e.BarriersBroken = 1 AndAlso Not e.IsBarrierStanding(1, 6), "a boosted hit smashes the barrier")
            Check(e.Hearts = GameEngine.MaxHearts AndAlso e.WallHits = 0, "smashing costs no health")
            For i As Integer = 1 To 200
                If e.State <> GameState.Playing Then Exit For
                e.Update(1.0F, 0.0F)
            Next
            Check(e.State = GameState.LevelComplete, "the ball rolls on through the opening")

            ' Normal top speed (0.12) is below break speed: a wall bounce, once, while held against it.
            Dim slow As New MazeDefinition({"1111111111", "1S0000B0G1", "1111111111"})
            Dim f As New GameEngine(slow, 0)
            Dim bounced As Integer = 0
            AddHandler f.BarrierBounced, Sub(s As Object, a As BarrierEventArgs) bounced += 1
            For i As Integer = 1 To 150 : f.Update(1.0F, 0.0F) : Next
            Check(bounced = 1, $"slow hit bounces once while held (got {bounced})")
            Check(f.IsBarrierStanding(1, 6) AndAlso f.BarriersBroken = 0, "a slow hit leaves it standing")
            Check(f.Hearts = 2.75F AndAlso f.WallHits = 1 AndAlso f.HeartsLost = 0.25F, "a slow hit costs a quarter heart like a wall")

            ' Smash, then fall: the barrier stays open; restart rebuilds it and clears the counters.
            Dim pit As New MazeDefinition({"111111111111", "1SFFFFB0H0G1", "111111111111"})
            Dim p As New GameEngine(pit, 0)
            Dim fell As Boolean = False
            AddHandler p.BallFell, Sub(s As Object, a As BallFellEventArgs) fell = True
            For i As Integer = 1 To 300
                p.Update(1.0F, 0.0F)
                If fell Then Exit For
            Next
            Check(fell AndAlso p.Falls = 1 AndAlso p.HeartsLost = 1.0F, "smashed through into the pit")
            Check(p.BarriersBroken = 1 AndAlso Not p.IsBarrierStanding(1, 6), "the barrier stays open after a fall")
            p.Reset()
            Check(p.BarriersBroken = 0 AndAlso p.IsBarrierStanding(1, 6) AndAlso p.Falls = 0 AndAlso p.WallHits = 0 AndAlso p.HeartsLost = 0,
                  "restart rebuilds barriers and clears the run counters")

            ' A 2-tall barrier is one group: touching one tile opens both.
            Dim wide As New MazeDefinition({"11111111111", "1SFFFFB0001", "10FFFFB00G1", "11111111111"})
            Dim w As New GameEngine(wide, 0)
            Check(w.BarriersTotal = 1, "adjacent barrier tiles form one barrier")
            For i As Integer = 1 To 200
                w.Update(1.0F, 0.0F)
                If w.BarriersBroken > 0 Then Exit For
            Next
            Check(Not w.IsBarrierStanding(1, 6) AndAlso Not w.IsBarrierStanding(2, 6), "the whole barrier opens together")
            Console.WriteLine("  -> smash, bounce once with 1/4 heart, open through falls, rebuild on restart, grouped tiles OK")
        End Sub
    End Module
End Namespace
```

In `tools/VerificationRunner/Program.vb` `Main`, add with the other flags:

```vb
            If Array.IndexOf(Environment.GetCommandLineArgs(), "--barrier-only") >= 0 Then
                BarrierChecks.Run()
                Return
            End If
```

and in the main suite, right after `GravityMaze.Verification.HealthScenarios.Run()`:

```vb
            BarrierChecks.Run()
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `GM_ROOT="$PWD" dotnet run --project tools/VerificationRunner -- --barrier-only`
Expected: build errors for `BarrierEventArgs`, `BarrierBroken`, `BarriersTotal` and similar.

- [ ] **Step 3: Accept the `B` tile**

`GravityMaze/Levels/MazeDefinition.vb:39`:

```vb
                    If "10SGIMFHLKQabETVcCWB".IndexOf(tile) < 0 Then
```

- [ ] **Step 4: Implement barriers and counters in `GravityMaze/Engine/GameEngine.vb`**

Add `Imports System.Linq` under the existing imports.

After the `HeartGained` event declaration, add:

```vb
        ' Overdrive barriers (B): a marble at break speed smashes the whole connected barrier;
        ' slower contact is an ordinary wall hit. Break speed sits above BaseMaxSpeed, so only a boost reaches it.
        Public Property BarrierBreakSpeed As Single = 0.16F
        Public Property BarrierSmashDamping As Single = 0.5F   ' velocity kept after smashing through
        Public Event BarrierBroken As EventHandler(Of BarrierEventArgs)
        Public Event BarrierBounced As EventHandler(Of BarrierEventArgs)
        Private ReadOnly _barrierGroup As Integer()                   ' per tile: barrier id, -1 = none
        Private ReadOnly _barrierCenters As New List(Of PointF)()
        Private ReadOnly _brokenBarriers As New HashSet(Of Integer)()

        ' Run counters for the journal; Reset clears them.
        Private _wallHits As Integer
        Private _falls As Integer
        Private _quartersLost As Integer
```

At the end of `Sub New`, after `_goalCenterY = ...`, add:

```vb
            _barrierGroup = Enumerable.Repeat(-1, maze.RowCount * maze.ColumnCount).ToArray()
            For r As Integer = 0 To maze.RowCount - 1
                For c As Integer = 0 To maze.ColumnCount - 1
                    If maze.GetTile(r, c) <> "B"c OrElse _barrierGroup(r * maze.ColumnCount + c) >= 0 Then Continue For
                    Dim id As Integer = _barrierCenters.Count
                    Dim sumX As Single = 0, sumY As Single = 0
                    Dim n As Integer = 0
                    Dim todo As New Stack(Of Point)()
                    _barrierGroup(r * maze.ColumnCount + c) = id
                    todo.Push(New Point(c, r))
                    While todo.Count > 0
                        Dim p As Point = todo.Pop()
                        sumX += p.X + 0.5F : sumY += p.Y + 0.5F : n += 1
                        For Each d As Point In {New Point(1, 0), New Point(-1, 0), New Point(0, 1), New Point(0, -1)}
                            Dim nx As Integer = p.X + d.X
                            Dim ny As Integer = p.Y + d.Y
                            If nx < 0 OrElse ny < 0 OrElse nx >= maze.ColumnCount OrElse ny >= maze.RowCount Then Continue For
                            If maze.GetTile(ny, nx) <> "B"c OrElse _barrierGroup(ny * maze.ColumnCount + nx) >= 0 Then Continue For
                            _barrierGroup(ny * maze.ColumnCount + nx) = id
                            todo.Push(New Point(nx, ny))
                        Next
                    End While
                    _barrierCenters.Add(New PointF(sumX / n, sumY / n))
                Next
            Next
```

In `Reset()`, before `Dungeon?.Reset()`, add:

```vb
            _brokenBarriers.Clear()
            _wallHits = 0
            _falls = 0
            _quartersLost = 0
```

After the `IsPickupTaken` function, add:

```vb
        Public ReadOnly Property BarriersTotal As Integer
            Get
                Return _barrierCenters.Count
            End Get
        End Property

        Public ReadOnly Property BarriersBroken As Integer
            Get
                Return _brokenBarriers.Count
            End Get
        End Property

        Public Function IsBarrierStanding(row As Integer, col As Integer) As Boolean
            Return BarrierAt(row, col) >= 0
        End Function

        Public ReadOnly Property WallHits As Integer
            Get
                Return _wallHits
            End Get
        End Property

        Public ReadOnly Property Falls As Integer
            Get
                Return _falls
            End Get
        End Property

        Public ReadOnly Property HeartsLost As Single
            Get
                Return CSng(_quartersLost) / QuartersPerHeart
            End Get
        End Property

        ' Standing barrier id at a tile, or -1.
        Private Function BarrierAt(row As Integer, col As Integer) As Integer
            If row < 0 OrElse row >= _maze.RowCount OrElse col < 0 OrElse col >= _maze.ColumnCount Then Return -1
            Dim id As Integer = _barrierGroup(row * _maze.ColumnCount + col)
            Return If(id < 0 OrElse _brokenBarriers.Contains(id), -1, id)
        End Function

        ' Barriers on the ball's leading edge: at break speed every touched barrier opens and the ball keeps
        ' (damped) momentum; slower contact leaves them standing and reports one bounce per contact.
        Private Function SmashBarriers(lead As Integer, fromIndex As Integer, toIndex As Integer, horizontal As Boolean,
                                       speed As Single, touching As Boolean) As Boolean
            Dim hit As New HashSet(Of Integer)()
            Dim hx As Single, hy As Single
            For i As Integer = fromIndex To toIndex
                Dim r As Integer = If(horizontal, i, lead)
                Dim c As Integer = If(horizontal, lead, i)
                Dim id As Integer = BarrierAt(r, c)
                If id >= 0 AndAlso hit.Add(id) Then hx = c + 0.5F : hy = r + 0.5F
            Next
            If hit.Count = 0 Then Return False
            If speed < BarrierBreakSpeed Then
                If Not touching Then RaiseEvent BarrierBounced(Me, New BarrierEventArgs(hx, hy))
                Return False
            End If
            For Each id As Integer In hit
                _brokenBarriers.Add(id)
                RaiseEvent BarrierBroken(Me, New BarrierEventArgs(_barrierCenters(id).X, _barrierCenters(id).Y))
            Next
            _velocityX *= BarrierSmashDamping
            _velocityY *= BarrierSmashDamping
            Return True
        End Function
```

In `TakeHit`, replace

```vb
            _heartQuarters = Math.Max(0, _heartQuarters - damageQuarters)
```

with

```vb
            Dim before As Integer = _heartQuarters
            _heartQuarters = Math.Max(0, _heartQuarters - damageQuarters)
            _quartersLost += before - _heartQuarters
```

In `Update`, step 4 (X movement), replace `If WallBlocksX(newX) Then` with:

```vb
                Dim blockedX As Boolean = WallBlocksX(newX)
                If blockedX Then
                    Dim leadCol As Integer = If(_velocityX > 0.0F, CInt(Math.Floor(newX + BallRadius)), CInt(Math.Floor(newX - BallRadius)))
                    If SmashBarriers(leadCol, Math.Max(0, CInt(Math.Floor(_ballY - BallRadius + Eps))),
                                     Math.Min(_maze.RowCount - 1, CInt(Math.Floor(_ballY + BallRadius - Eps))), True,
                                     Math.Abs(_velocityX), If(_velocityX > 0.0F, _inContactRight, _inContactLeft)) Then blockedX = WallBlocksX(newX)
                End If
                If blockedX Then
```

Step 5 (Y movement), replace `If WallBlocksY(newY) Then` with:

```vb
                Dim blockedY As Boolean = WallBlocksY(newY)
                If blockedY Then
                    Dim leadRow As Integer = If(_velocityY > 0.0F, CInt(Math.Floor(newY + BallRadius)), CInt(Math.Floor(newY - BallRadius)))
                    If SmashBarriers(leadRow, Math.Max(0, CInt(Math.Floor(_ballX - BallRadius + Eps))),
                                     Math.Min(_maze.ColumnCount - 1, CInt(Math.Floor(_ballX + BallRadius - Eps))), False,
                                     Math.Abs(_velocityY), If(_velocityY > 0.0F, _inContactBottom, _inContactTop)) Then blockedY = WallBlocksY(newY)
                End If
                If blockedY Then
```

Count wall hits: change each of the four `If Not _inContactX Then TakeHit(...)` lines to count first, for example:

```vb
                        If Not _inContactRight Then _wallHits += 1 : TakeHit(contactX, _ballY, WallDamageQuarters, False)
```

(the same for `_inContactLeft`, `_inContactBottom`, `_inContactTop`). In the hole branch (step 7), add `_falls += 1` right before `RaiseEvent BallFell(...)`.

Make standing barriers solid. Replace `Solid` with:

```vb
        Private Function Solid(row As Integer, column As Integer) As Boolean
            If BarrierAt(row, column) >= 0 Then Return True
            Return If(Dungeon Is Nothing, _maze.GetTile(row, column) = "1"c, Dungeon.Solid(row, column))
        End Function
```

At the end of the file, before `End Namespace`, add:

```vb
    ' Barrier centre (tile-space) for smash and bounce feedback.
    Public NotInheritable Class BarrierEventArgs
        Inherits EventArgs

        Public ReadOnly Property X As Single
        Public ReadOnly Property Y As Single

        Public Sub New(x As Single, y As Single)
            Me.X = x
            Me.Y = y
        End Sub
    End Class
```

- [ ] **Step 5: Run the barrier test and confirm it passes**

Run: `GM_ROOT="$PWD" dotnet run --project tools/VerificationRunner -- --barrier-only`
Expected: `-> smash, bounce once with 1/4 heart, open through falls, rebuild on restart, grouped tiles OK`.

- [ ] **Step 6: Check nothing else moved**

Run: `dotnet run --project tools/HealthChecks && dotnet run --project tools/DungeonChecks && dotnet run --project tools/MarbleChecks && dotnet run --project tools/TransitionChecks`
Expected: every check prints `PASS`. Mazes without `B` behave exactly as before.

- [ ] **Step 7: Commit**

```bash
git add GravityMaze/Levels/MazeDefinition.vb GravityMaze/Engine/GameEngine.vb tools/VerificationRunner/BarrierChecks.vb tools/VerificationRunner/Program.vb
git commit -m "Add Overdrive barriers and per-run hit/fall counters to the engine

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Level 3 content: barrier maze, boosting pilot, no countdown

**Files:**
- Modify: `tools/maze_src/Level3.txt`, then regenerate `GravityMaze/Mazes/Level3.txt` with `tools/widen_maze.py`
- Modify: `GravityMaze/UI/Screens/AttractPilot.vb` (boost into barriers)
- Modify: `tools/VerificationRunner/Program.vb` (`KeyboardBotRun` uses `AttractPilot`; delete the now-unused `Key` helper; extend `TestLevel3`; `--barrier-only` also runs `TestLevel3`)
- Modify: Level 3 `LevelConfig` in `GravityMaze/Form1.vb:47`, `tools/VerificationRunner/UiTour.vb:51`, `tools/VerificationRunner/DungeonUiTour.vb:53`, `tools/VerificationRunner/DungeonFrameFixture.vb:47`, `tools/TransitionChecks/Program.vb:52`
- Modify: `tools/VerificationRunner/UiTour.vb` (remove the time-up section)
- Modify: `tools/TransitionChecks/Program.vb:462`

**Interfaces:**
- Consumes: `GameEngine.IsBarrierStanding`, `BarriersTotal` (Task 2).
- Produces: a Level 3 with exactly 4 barriers on the only route; the measured par time.

- [ ] **Step 1: Write the failing test** — extend `TestLevel3` in `tools/VerificationRunner/Program.vb`. Insert before the `Dim ticks As Integer = KeyboardBotRun(maze)` line:

```vb
            ' Overdrive barriers: required (no way round), and each has a straight 3+ tile boost run-up.
            Check(New GameEngine(maze, 0).BarriersTotal = 4, "Level 3 must have 4 Overdrive barriers")
            Check(Not Flood(maze, Function(t) t <> "1"c AndAlso t <> "H"c AndAlso t <> "B"c).Contains(New Point(maze.GoalColumn, maze.GoalRow)),
                  "barriers must block every route to the goal")
            Dim dirs As Point() = {New Point(1, 0), New Point(-1, 0), New Point(0, 1), New Point(0, -1)}
            Dim seenB As New HashSet(Of Point)()
            For r As Integer = 0 To maze.RowCount - 1
                For c As Integer = 0 To maze.ColumnCount - 1
                    If maze.GetTile(r, c) <> "B"c OrElse seenB.Contains(New Point(c, r)) Then Continue For
                    Dim group As New List(Of Point) From {New Point(c, r)}
                    seenB.Add(group(0))
                    Dim gi As Integer = 0
                    While gi < group.Count
                        For Each d As Point In dirs
                            Dim n As New Point(group(gi).X + d.X, group(gi).Y + d.Y)
                            If n.X >= 0 AndAlso n.Y >= 0 AndAlso n.X < maze.ColumnCount AndAlso n.Y < maze.RowCount AndAlso
                               maze.GetTile(n.Y, n.X) = "B"c AndAlso seenB.Add(n) Then group.Add(n)
                        Next
                        gi += 1
                    End While
                    Dim guarded As Boolean = group.Any(Function(p) dirs.Any(Function(d) Enumerable.Range(1, 3).All(Function(k)
                        Dim fx As Integer = p.X - k * d.X, fy As Integer = p.Y - k * d.Y
                        Return fx >= 0 AndAlso fy >= 0 AndAlso fx < maze.ColumnCount AndAlso fy < maze.RowCount AndAlso maze.GetTile(fy, fx) = "F"c
                    End Function)))
                    Check(guarded, $"barrier at ({c},{r}) has no straight 3-tile boost run-up")
                Next
            Next
            Console.WriteLine("  -> 4 barriers, all required, each with a boost run-up.")
```

Add `Imports System.Linq` and `Imports GravityMaze.UI.Screens` to `Program.vb` if missing. Make `--barrier-only` also run the Level 3 check:

```vb
            If Array.IndexOf(Environment.GetCommandLineArgs(), "--barrier-only") >= 0 Then
                BarrierChecks.Run()
                TestLevel3(MazeManager.LoadFromFile(Path.Combine(ProjectRoot, "GravityMaze\Mazes\Level3.txt")))
                Return
            End If
```

Replace `KeyboardBotRun` with the shared pilot, and delete the private `Key(distance, velocity, cap)` function below it (nothing else uses it):

```vb
        ' Keyboard-only bot = the attract-mode pilot (-1/0/+1 per axis), so the demo and this proof share one controller.
        Private Function KeyboardBotRun(maze As MazeDefinition) As Integer
            Dim engine As New GameEngine(maze, 0) With {.HeartsEnabled = False}
            Dim pilot As New AttractPilot(maze)
            Dim falls As Integer = 0
            AddHandler engine.BallFell, Sub(s As Object, e As BallFellEventArgs) falls += 1
            Dim ticks As Integer = 0
            While ticks < 20000 AndAlso engine.State = GameState.Playing AndAlso falls = 0
                Dim t As PointF = pilot.NextTilt(engine)
                engine.Update(t.X, t.Y)
                ticks += 1
            End While
            If falls > 0 Then Throw New Exception($"Keyboard bot fell into a hole at ({engine.BallX:F1}, {engine.BallY:F1}) after {ticks} ticks.")
            If engine.State <> GameState.LevelComplete Then
                Throw New Exception($"Keyboard bot stalled at ({engine.BallX:F1}, {engine.BallY:F1}) after {ticks} ticks.")
            End If
            Return ticks
        End Function
```

In `TestLevel3`, change the last `Console.WriteLine` to print the par instead of a time limit:

```vb
            Console.WriteLine($"  -> Keyboard bot cleared Level 3 in {ticks} ticks (~{secs:F1}s) without falling; par = {secs:F1}s.")
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `GM_ROOT="$PWD" dotnet run --project tools/VerificationRunner -- --barrier-only`
Expected: FAIL with `Level 3 must have 4 Overdrive barriers`.

- [ ] **Step 3: Place the barriers in the source maze**

Replace `tools/maze_src/Level3.txt` with the following. Four `0`s became `B`, at the end of each boost strip on the route: rows/cols (4,7), (3,18), (14,9), (23,12).

```
1111111111111111111111111
1010000000000000000000001
1010111H11111111111111101
101010000010FFFFFFB0H0001
1010101B10101111111010101
1010101F10001000000010101
1010101F11111011111010111
1010101F10001010000010001
1010111F1F10111000H011101
S01010001F1000000H0000001
101010101F111010H00011101
101010101F101000000000001
101010101F101011111111101
100000101F101010000000101
111111111B101011111110111
100000000010000000001000G
101111111H111111111111101
1000000010000000000000101
1011111010111000H01110101
100000101010000H000010101
11111010101010H0001010101
1000101010101000001010101
1010101010111010101010101
10100010FFFFB0H0000010001
1111111111111111111111111
```

Regenerate: `python tools/widen_maze.py`
Then: `git diff --stat GravityMaze/Mazes` must list **only** `Level3.txt`. The script is deterministic, and `B` counts as passable for pickup placement, so Levels 1–2 come out byte-identical. If Level 1 or Level 2 changed, stop and report it.

- [ ] **Step 4: Teach the pilot to boost into a standing barrier** — in `GravityMaze/UI/Screens/AttractPilot.vb`, in `NextTilt`, replace the last two lines (`Dim cap ...` / `Return New PointF(...)`) with:

```vb
            Dim cap As Single = If(tile = "I"c, 0.05F, If(tile = "F"c, 0.14F, 0.09F))
            ' Boost strip that ends in a standing barrier: floor it along the strip, steer the other axis as usual.
            Dim boost As Point = If(tile = "F"c, BarrierRunUp(engine), Point.Empty)
            If boost <> Point.Empty Then
                Return New PointF(If(boost.X <> 0, boost.X, Key(dx, engine.VelocityX, cap)),
                                  If(boost.Y <> 0, boost.Y, Key(dy, engine.VelocityY, cap)))
            End If
            Return New PointF(Key(dx, engine.VelocityX, cap), Key(dy, engine.VelocityY, cap))
```

and add below `NextTilt`:

```vb
        ' Unit direction of a straight boost strip ahead that ends in a standing barrier, or Point.Empty.
        Private Function BarrierRunUp(engine As GameEngine) As Point
            For j As Integer = Math.Max(1, _waypoint) To Math.Min(_path.Count - 1, _waypoint + 10)
                Dim p As Point = _path(j)
                Dim t As Char = _maze.GetTile(p.Y, p.X)
                If t = "B"c Then
                    If Not engine.IsBarrierStanding(p.Y, p.X) Then Return Point.Empty
                    Return New Point(Math.Sign(p.X - _path(j - 1).X), Math.Sign(p.Y - _path(j - 1).Y))
                End If
                If t <> "F"c Then Return Point.Empty
            Next
            Return Point.Empty
        End Function
```

- [ ] **Step 5: Run and tune**

Run: `GM_ROOT="$PWD" dotnet run --project tools/VerificationRunner -- --barrier-only`
Expected: `-> 4 barriers, all required, each with a boost run-up.` and `Keyboard bot cleared Level 3 in N ticks (~X.Xs) without falling; par = X.Xs`.
If the bot reports `fell into a hole` just past a barrier, lower the default `BarrierSmashDamping` in `GameEngine.vb` from `0.5F` to `0.35F` and re-run. Then re-run `--barrier-only` to confirm Task 2's checks still pass. Write down the printed par `X.X`.

- [ ] **Step 6: Remove the countdown from every Level 3 config** — in each of the five files listed above, change the Level 3 `LevelConfig` from limit `57` / par `40.2F` to limit `0` / par = the measured `X.X`:

```vb
            New LevelConfig(3, "Neon Velocity", "Mazes\Level3.txt", 0, X.XF),
```

In `GravityMaze/Form1.vb`, also replace the comment `' Level 3 limit = 1.4x its par; the finale has no forced countdown.` with `' No level has a forced countdown; Level 3's twist is its Overdrive barriers.`
In `tools/TransitionChecks/Program.vb:52`, change `If(i = 3, 57, 0)` to `0`, and delete line 462 (`If level = 2 Then Check(... TimeRemainingSeconds = 57 ...)`).

- [ ] **Step 7: Drop the UI tour's time-up step** — in `tools/VerificationRunner/UiTour.vb`, replace the block from the comment `' Time up on Level 3: idle until the clock runs out.` through `Shot("ui_13_time_up")` inclusive (leave the following `' Out of hearts on Level 1` block untouched; its leading `Escape` is harmless on the title) with:

```vb
            ' Level 3 HUD: barriers counter instead of a countdown.
            MoveTo(1) : Key(Keys.Enter) : Ticks(10)
            MoveTo(2) : Key(Keys.Enter)
            Ticks(CInt((1600 + 2000) / 16) + 10)
            Expect(ShellScreen.Playing)
            If _shell.Engine.BarriersTotal <> 4 Then Throw New Exception("Level 3 must show its 4 barriers")
            Shot("ui_12_hud_level3")
            Key(Keys.Escape) : Ticks(10)
            MoveTo(2) : Key(Keys.Enter) : Ticks(10)   ' pause → MAIN MENU
            Expect(ShellScreen.Title)
```

Change the final summary line `..., records, time-up all reached.` to `..., records, level 3 HUD all reached.`, and delete the stale `screenshots/ui/ui_13_time_up.png` reference if the tour lists it anywhere else (`grep -n ui_13 tools`). (Task 5 remaps the title-menu indices; at this point they are still the old ones.)

- [ ] **Step 8: Run the full checks**

Run the full check set from Global Constraints.
Expected: all pass, and the UI tour logs `Level 3 cleared by the pilot` (the pilot smashes all four barriers through the real shell).

- [ ] **Step 9: Commit**

```bash
git checkout -- screenshots && git clean -fd screenshots
git add tools/maze_src/Level3.txt GravityMaze/Mazes/Level3.txt GravityMaze/UI/Screens/AttractPilot.vb GravityMaze/Form1.vb GravityMaze/Engine/GameEngine.vb tools/VerificationRunner tools/TransitionChecks/Program.vb
git commit -m "Replace Level 3 countdown with four required Overdrive barriers

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Barrier presentation (draw, charge glow, smash/too-slow feedback, HUD)

**Files:**
- Modify: `GravityMaze/Rendering/MazeRenderer.vb` (`Draw` gets `barrierStanding`, `barrierCharge`; new `DrawBarriers`)
- Modify: `GravityMaze/UI/GameCanvas.vb` (properties, passed to all three `renderer.Draw` calls)
- Modify: `GravityMaze/UI/Screens/GameShell.vb` (event wiring, charge, too-slow timer)
- Modify: `GravityMaze/UI/Screens/GameShell.Draw.vb` (HUD counter, TOO SLOW banner, intro line)
- Modify: `tools/TransitionChecks/HeadlessAdapters.vb` (fake canvas properties)

**Interfaces:**
- Consumes: Task 2 engine members.
- Produces: `GameCanvas.BarrierStanding As Func(Of Integer, Integer, Boolean)`, `GameCanvas.BarrierCharge As Single` (0..1). `MazeRenderer.Draw(..., Optional barrierStanding As Func(Of Integer, Integer, Boolean) = Nothing, Optional barrierCharge As Single = 0.0F)`. When `barrierStanding` is Nothing, every `B` is drawn (static screenshots).

- [ ] **Step 1: Write the failing check** — add a scenario to `tools/TransitionChecks/Program.vb` and call it from `Main` after `TestIntroPause()`:

```vb
    Private Sub TestBarrierFeedback()
        Using f As New Fixture(barrierLevel:=True)
            f.Campaign()
            Check(f.Canvas.BarrierStanding IsNot Nothing AndAlso f.Canvas.BarrierStanding(1, 4), "canvas draws the engine's standing barriers")
            f.Tilt.X = 1
            For i As Integer = 1 To 120
                If f.Sound.Played.Contains("barrier_slow") Then Exit For
                f.Ticks(1)
            Next
            Check(f.Sound.Played.Contains("barrier_slow"), "a slow hit plays the too-slow cue")
            Check(f.Shell.TooSlowVisible, "a slow hit shows TOO SLOW")
            f.Tilt.X = 0 : f.Ticks(60)
            Check(Not f.Shell.TooSlowVisible, "TOO SLOW fades after 900 ms")
        End Using
    End Sub
```

Give `Fixture.New` an extra parameter `Optional barrierLevel As Boolean = False`. When it is true, write level 1's file as `{"1111111", "1S00BG1", "1111111"}` instead of the default (keep the other levels the same):

```vb
                IO.File.WriteAllLines(file, If(barrierLevel AndAlso i = 1, {"1111111", "1S00BG1", "1111111"}, {"111111", "1S00G1", "111111"}))
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `dotnet run --project tools/TransitionChecks`
Expected: build error: `BarrierStanding` / `TooSlowVisible` not defined.

- [ ] **Step 3: Renderer** — in `GravityMaze/Rendering/MazeRenderer.vb`, add two optional parameters at the end of `Draw`'s parameter list:

```vb
                        Optional dungeon As DungeonRun = Nothing,
                        Optional barrierStanding As Func(Of Integer, Integer, Boolean) = Nothing,
                        Optional barrierCharge As Single = 0.0F)
```

Right after `DrawPickups(graphics, left, top, tileSize, maze, pickupTaken)`, add:

```vb
                ' Barriers flicker and react to the marble's speed, so they are drawn per frame like pickups.
                DrawBarriers(graphics, left, top, tileSize, maze, barrierStanding, barrierCharge)
```

and add this method after `DrawPickups`:

```vb
        ' B tiles are floor in the static board; a standing barrier is a flickering force field that
        ' blazes brighter as the marble approaches break speed (charge 0..1).
        Private Shared Sub DrawBarriers(graphics As Graphics, left As Single, top As Single, tileSize As Single,
                                        maze As MazeDefinition, standing As Func(Of Integer, Integer, Boolean), charge As Single)
            Dim flicker As Single = CSng(0.5 + 0.5 * Math.Sin(Environment.TickCount64 * 2.0 * Math.PI / 260.0))
            Dim fillAlpha As Integer = Math.Min(255, CInt(110 + 60 * flicker + 85 * charge))
            Dim edgeAlpha As Integer = Math.Min(255, CInt(160 + 95 * charge))
            For r As Integer = 0 To maze.RowCount - 1
                For c As Integer = 0 To maze.ColumnCount - 1
                    If maze.GetTile(r, c) <> "B"c Then Continue For
                    If standing IsNot Nothing AndAlso Not standing(r, c) Then Continue For
                    Dim rect As New RectangleF(left + c * tileSize, top + r * tileSize, tileSize, tileSize)
                    Using fill As New SolidBrush(Color.FromArgb(fillAlpha, 255, 60, 200)),
                          edge As New Pen(Color.FromArgb(edgeAlpha, 120, 240, 255), Math.Max(1.0F, tileSize * (0.06F + 0.06F * charge)))
                        graphics.FillRectangle(fill, rect)
                        For k As Integer = 1 To 3
                            Dim y As Single = rect.Y + rect.Height * k / 4.0F
                            graphics.DrawLine(edge, rect.X, y, rect.Right, y)
                        Next
                        graphics.DrawRectangle(edge, rect.X, rect.Y, rect.Width, rect.Height)
                    End Using
                Next
            Next
        End Sub
```

- [ ] **Step 4: Canvas** — in `GravityMaze/UI/GameCanvas.vb`, next to `PickupTaken`, add:

```vb
        ' Overdrive barriers: the engine's standing-barrier lookup (Nothing = draw all) and the 0..1 speed charge.
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property BarrierStanding As Func(Of Integer, Integer, Boolean)
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property BarrierCharge As Single
```

Append `barrierStanding:=BarrierStanding, barrierCharge:=BarrierCharge` to the two `renderer.Draw(...)` calls in `OnPaint`, and `barrierStanding:=BarrierStanding` to the `renderer.Draw(...)` call in `DrawGoalDrop` (the outgoing board).

In `tools/TransitionChecks/HeadlessAdapters.vb`, in the fake `GameCanvas`, add:

```vb
        Public Property BarrierStanding As Func(Of Integer, Integer, Boolean)
        Public Property BarrierCharge As Single
```

- [ ] **Step 5: Shell wiring** — in `GravityMaze/UI/Screens/GameShell.vb`:

Fields (next to `_spawnSoundInMs`):

```vb
        Private _tooSlowMs As Single = -1   ' time since a too-slow barrier hit (-1 = hidden)
```

Public read-only for the checks (next to `Engine`):

```vb
        Public ReadOnly Property TooSlowVisible As Boolean
            Get
                Return _tooSlowMs >= 0
            End Get
        End Property
```

In `StartLevel`, after `AddHandler _engine.HeartGained, ...`:

```vb
            AddHandler _engine.BarrierBroken, AddressOf OnBarrierBroken
            AddHandler _engine.BarrierBounced, AddressOf OnBarrierBounced
```

In `DetachEngine`, add the matching `RemoveHandler` lines. In `BeginRun`, add `_tooSlowMs = -1`.

In `Tick`, in the `If IsGameScreen() Then` branch after `_canvas.PickupTaken = ...`:

```vb
                _canvas.BarrierStanding = AddressOf _engine.IsBarrierStanding
                Dim speed As Single = CSng(Math.Sqrt(_engine.VelocityX * _engine.VelocityX + _engine.VelocityY * _engine.VelocityY))
                _canvas.BarrierCharge = If(_screen = ShellScreen.Playing, Math.Min(1.0F, speed / _engine.BarrierBreakSpeed), 0.0F)
```

and in the `Else` (attract) branch after `_canvas.PickupTaken = Nothing`:

```vb
                _canvas.BarrierStanding = AddressOf _attractEngine.IsBarrierStanding
                _canvas.BarrierCharge = 0.0F
```

In `TickPlaying`, after the `_spawnSoundInMs` block:

```vb
            If _tooSlowMs >= 0 Then
                _tooSlowMs += TickMs
                If _tooSlowMs > 900.0F Then _tooSlowMs = -1
            End If
```

Handlers (next to `OnHeartGained`):

```vb
        Private Sub OnBarrierBroken(sender As Object, e As BarrierEventArgs)
            _canvas.AddImpact(e.X, e.Y, -Math.Sign(_engine.VelocityX), -Math.Sign(_engine.VelocityY), 0.24F)
            _canvas.Shake(220.0F, 4.0F)
            _sound.Play("goal", 0.55F)
        End Sub

        Private Sub OnBarrierBounced(sender As Object, e As BarrierEventArgs)
            _tooSlowMs = 0
            _sound.Play("barrier_slow")
        End Sub
```

Sound: `SoundManager` plays files from `Sounds/`. Copy `GravityMaze/Sounds/menu_back.wav` to `GravityMaze/Sounds/barrier_slow.wav`. (`ponytail:` reuse until a bespoke cue exists.) Check that `GravityMaze.vbproj` copies `Sounds\*.wav` with a wildcard (`grep -n Sounds GravityMaze/GravityMaze.vbproj`); if files are listed one by one, add the new one.

- [ ] **Step 6: HUD and intro** — in `GravityMaze/UI/Screens/GameShell.Draw.vb` `DrawHud`, right after the `If timed Then ... End If` time-bar block:

```vb
            If _engine.BarriersTotal > 0 Then
                UiDraw.Text(g, $"BARRIERS  {_engine.BarriersBroken}/{_engine.BarriersTotal}", GameFonts.Body(18 * s, GameFonts.FontWeight.Bold),
                            If(_engine.BarriersBroken = _engine.BarriersTotal, pal.Accent, pal.TextDim), cx, 122 * s, 0.5F, 4 * s)
            End If
            If _tooSlowMs >= 0 Then
                Dim a As Single = 1.0F - _tooSlowMs / 900.0F
                UiDraw.GlowText(g, "TOO SLOW  ·  HIT IT ON A BOOST", GameFonts.Display(56 * s), UiDraw.WithAlpha(Danger, a),
                                UiDraw.WithAlpha(pal.AccentSoft, a), cx, b.Height * 0.22F, 0.5F, 2 * s, 4 * s)
            End If
```

In `DrawIntro`, after the `If cfg.TimeLimitSecs > 0 Then ... End If` block:

```vb
                If _engine.BarriersTotal > 0 Then
                    UiDraw.Text(g, "BREAK THE BARRIERS  ·  HIT THEM AT FULL BOOST", GameFonts.Body(22 * s, GameFonts.FontWeight.Bold),
                                UiDraw.WithAlpha(pal.Accent, a), cx, y + 300 * s, 0.5F, 5 * s)
                End If
```

In `DrawHowToPlay`, add one line to the rules column, in the same style as its neighbours: `"Neon barriers: hit them on a boost strip to smash through."`

- [ ] **Step 7: Run the checks**

Run: `dotnet run --project tools/TransitionChecks`, then the full check set.
Expected: all pass. Open `screenshots/ui/ui_12_hud_level3.png` in the worktree and confirm the barriers draw as magenta/cyan force fields and the HUD shows `BARRIERS 0/4`.

- [ ] **Step 8: Commit**

```bash
git checkout -- screenshots && git clean -fd screenshots
git add GravityMaze/Rendering/MazeRenderer.vb GravityMaze/UI GravityMaze/Sounds/barrier_slow.wav GravityMaze/GravityMaze.vbproj tools/TransitionChecks
git commit -m "Draw Overdrive barriers with speed charge, smash feedback and TOO SLOW cue

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Run journal, saved progress, Continue and the campaign rule

**Files:**
- Modify: `GravityMaze/UI/Screens/GameShell.vb`
- Modify: `GravityMaze/UI/Screens/GameShell.Draw.vb` (locked level cards, victory note)
- Modify: `GravityMaze/Form1.vb` (`OnFormClosed` calls `shell?.OnClosing()`)
- Modify: `tools/TransitionChecks/Program.vb`, `tools/VerificationRunner/UiTour.vb`, `tools/VerificationRunner/DungeonUiTour.vb` (title-menu index remap, unlocks)

**Interfaces:**
- Consumes: Task 1 (`AddRecord`, `RunOutcome`, `UnlockedLevel`, `Unlock`, `KeyOf`, `RunsFor`), Task 2 counters.
- Produces: title menu order `0 PLAY, 1 CONTINUE, 2 LEVEL SELECT, 3 RECORDS, 4 HOW TO PLAY, 5 PROFILE, 6 CALIBRATE, 7 SWITCH PLAYER, 8 QUIT`. `GameShell.OnClosing()`. Private `WithPlayer(action As Action)`, `_afterPlayerChosen As Action` (Task 6 fills in the player screen; until then `WithPlayer` goes to name entry).

**Title-menu index remap for existing tests** (only for `Menu(n)` / `MoveTo(n)` / `SelectMenu(..., n)` calls made **while on the Title screen**):

| Old | New | Item |
|---|---|---|
| 0 | 0 | PLAY (new campaign from Level 1, unchanged) |
| 1 | 2 | LEVEL SELECT |
| 2 | 3 | RECORDS |
| 3 | 4 | HOW TO PLAY |
| 4 | 7 | CHANGE PLAYER → SWITCH PLAYER |
| 5 | 8 | QUIT |

- [ ] **Step 1: Write the failing checks** — add to `tools/TransitionChecks/Program.vb` and call them from `Main` after `TestIntroPause()`:

```vb
    Private Sub TestRunJournal()
        Using f As New Fixture()
            f.Campaign() : f.Tilt.X = 1 : f.Ticks(5) : f.Tilt.X = 0
            f.Shell.HandleKey(Keys.R)                                   ' restart mid-run
            f.WaitFor(ShellScreen.Playing) : f.Ticks(5)
            f.Shell.HandleKey(Keys.Escape) : f.Menu(2) : f.Shell.HandleKey(Keys.Enter)   ' pause → MAIN MENU
            Dim runs = f.Scores.RunsFor("TEST", 1)
            Check(runs.Count = 2 AndAlso runs(0).Outcome = RunOutcome.Restart AndAlso runs(1).Outcome = RunOutcome.Quit, "restart and quit are journaled")
            Check(f.Scores.TopRecords(1, 10).Count = 0, "unfinished runs are not records")
            f.Shell.HandleKey(Keys.Enter) : f.WaitFor(ShellScreen.Playing)
            f.Shell.HandleKey(Keys.R)                                   ' never moved: nothing journaled
            Check(f.Scores.RunsFor("TEST", 1).Count = 2, "a run with no time is not journaled")

            ' Out of hearts, then R: exactly one more run, and it is the out-of-hearts one.
            f.WaitFor(ShellScreen.Playing)
            For hit As Integer = 1 To 12
                f.Tilt.X = -1 : f.Ticks(94)
                If hit < 12 Then f.Tilt.X = 1 : f.Ticks(12)
            Next
            f.Tilt.X = 0 : f.WaitFor(ShellScreen.OutOfHearts)
            f.Shell.HandleKey(Keys.R)
            runs = f.Scores.RunsFor("TEST", 1)
            Check(runs.Count = 3 AndAlso runs(2).Outcome = RunOutcome.OutOfHearts AndAlso runs(2).WallHits = 12 AndAlso runs(2).HeartsLost = 3,
                  "out of hearts is journaled once with its counters")
        End Using
    End Sub

    Private Sub TestContinueCampaign()
        Using f As New Fixture()
            Check(Not f.Shell.TitleItemEnabled(1), "CONTINUE is disabled before any clear")
            f.Campaign() : f.Goal() : f.WaitFor(ShellScreen.Playing)
            f.Shell.HandleKey(Keys.Escape) : f.Menu(2) : f.Shell.HandleKey(Keys.Enter)
            Check(f.Scores.UnlockedLevel = 2 AndAlso f.Shell.TitleItemEnabled(1), "clearing level 1 unlocks level 2 and CONTINUE")
            f.Menu(1) : f.Shell.HandleKey(Keys.Enter) : f.WaitFor(ShellScreen.Playing)
            For level As Integer = 2 To 3
                f.Goal() : f.WaitFor(If(level = 3, ShellScreen.Victory, ShellScreen.Playing))
            Next
            Check(f.Scores.TopRecords(1, 10).Count = 1 AndAlso f.Scores.TopRecords(2, 10).Count = 1 AndAlso f.Scores.TopRecords(3, 10).Count = 1,
                  "continued run saves each level it played")
            Check(f.Scores.TopRecords(0, 10).Count = 0, "a continued campaign posts no campaign record")
        End Using
    End Sub

    Private Sub TestLevelLocks()
        Using f As New Fixture()
            f.Menu(2) : f.Shell.HandleKey(Keys.Enter)
            Check(f.Shell.Screen = ShellScreen.LevelSelect AndAlso f.Shell.SelectedIndex = 0, "level select opens on level 1")
            f.Tilt.Y = 0 : f.Ticks(3) : f.Tilt.Y = 1 : f.Ticks(1) : f.Tilt.Y = 0
            Check(f.Shell.SelectedIndex = 3, "locked levels 2-3 are skipped straight to BACK")
        End Using
    End Sub
```

Add `Imports GravityMaze.Data` if missing. Apply the remap table to the existing title-menu navigation in `tools/TransitionChecks/Program.vb`, `tools/VerificationRunner/UiTour.vb` and `tools/VerificationRunner/DungeonUiTour.vb`. Read each call in context: pause-menu and dungeon-menu indices do **not** change. Tests that reach Level 4 through Level Select must unlock it first:
- `TransitionChecks` `EnterDungeon(f)`: add `f.Scores.Unlock(4)` as its first line.
- `DungeonUiTour.vb:55`: replace `New ScoreManager(scoresPath)` with a variable created just before the shell: `Dim scores As New ScoreManager(scoresPath) With {.PlayerName = "TEST"} : scores.Unlock(4)`, and pass `scores`.
- `UiTour`: the campaign unlocks levels naturally; no change beyond the remap.

- [ ] **Step 2: Run them and confirm they fail**

Run: `dotnet run --project tools/TransitionChecks`
Expected: build error `TitleItemEnabled` not defined (and, once that is stubbed, `restart and quit are journaled` fails).

- [ ] **Step 3: Implement in `GameShell.vb`**

Title menu:

```vb
        Private ReadOnly _titleMenu As New MenuList("PLAY", "CONTINUE", "LEVEL SELECT", "RECORDS", "HOW TO PLAY",
                                                     "PROFILE", "CALIBRATE", "SWITCH PLAYER", "QUIT")
```

New fields next to `_campaignStars`:

```vb
        Private _campaignFromStart As Boolean   ' only an unbroken Level 1 → end run posts a campaign record
        Private _runLogged As Boolean           ' this attempt is already in the journal
        Private _afterPlayerChosen As Action    ' what to do once a player is picked (Nothing = back to title)
```

Delete `_returnScreen` and its uses. `OpenNameEntry` becomes parameterless and starts empty:

```vb
        Private Sub OpenNameEntry()
            _nameBuffer = ""
            GoTo_(ShellScreen.NameEntry)
        End Sub
```

In `Back()`, the `NameEntry` case also clears `_afterPlayerChosen = Nothing` before going to the Title. `ConfirmName` becomes:

```vb
        Private Sub ConfirmName()
            Dim name As String = _nameBuffer.Trim()
            If name.Length = 0 Then Return
            _scores.PlayerName = name
            ContinueAfterPlayer()
        End Sub

        Private Sub ContinueAfterPlayer()
            Dim nextStep As Action = _afterPlayerChosen
            _afterPlayerChosen = Nothing
            If nextStep Is Nothing Then GoTo_(ShellScreen.Title) Else nextStep()
        End Sub

        ' Runs action now if a player is chosen, otherwise after the player picks or types one.
        Private Sub WithPlayer(action As Action)
            If _scores.PlayerName.Length > 0 Then action() : Return
            _afterPlayerChosen = action
            OpenNameEntry()   ' Task 6 swaps this for the Who's Playing? list
        End Sub
```

Continue and title choices:

```vb
        ' Level index Continue resumes at, or -1 when there is nothing to continue.
        Private Function ContinueIndex() As Integer
            Dim u As Integer = _scores.UnlockedLevel
            Return If(u > 1 AndAlso u <= _levels.Count, u - 1, -1)
        End Function

        Public Function TitleItemEnabled(index As Integer) As Boolean
            Return index <> 1 OrElse ContinueIndex() >= 0
        End Function

        Private Sub TitleChoice(index As Integer)
            Select Case index
                Case 0 : WithPlayer(Sub() StartCampaign(0))
                Case 1 : WithPlayer(Sub() StartCampaign(Math.Max(0, ContinueIndex())))
                Case 2 : WithPlayer(Sub() GoTo_(ShellScreen.LevelSelect))
                Case 3 : GoTo_(ShellScreen.Records)
                Case 4 : GoTo_(ShellScreen.HowToPlay)
                Case 5 : WithPlayer(Sub() GoTo_(ShellScreen.Title))   ' Task 6: profile screen
                Case 6 : GoTo_(ShellScreen.Title)                      ' Task 8: calibration screen
                Case 7 : _afterPlayerChosen = Nothing : OpenNameEntry() ' Task 6: player list
                Case 8 : RaiseEvent QuitRequested(Me, EventArgs.Empty)
            End Select
        End Sub

        Private Sub StartCampaign(fromIndex As Integer)
            _campaignTime = 0
            _campaignStars = 0
            _campaignFromStart = fromIndex = 0
            StartLevel(fromIndex, campaign:=True)
        End Sub
```

In the constructor, after `_levelMenu.Items.Add("BACK")`:

```vb
            _titleMenu.Enabled = AddressOf TitleItemEnabled
            _levelMenu.Enabled = Function(i) i >= _levels.Count OrElse i < _scores.UnlockedLevel
```

In `GoTo_`, change the Title case to refresh the Continue label, and keep Level Select off locked cards:

```vb
                Case ShellScreen.Title
                    Dim ci As Integer = ContinueIndex()
                    _titleMenu.Items(1) = If(ci >= 0, $"CONTINUE · LEVEL {ci + 1}", "CONTINUE")
                    _titleMenu.Reset(If(TitleItemEnabled(_titleMenu.Selected), _titleMenu.Selected, 0))
                Case ShellScreen.LevelSelect
                    _levelMenu.Reset(If(_levelMenu.Enabled(_levelMenu.Selected), _levelMenu.Selected, 0))
```

In `HandleKey`, `ShellScreen.LevelSelect`: only start an unlocked level:

```vb
                        If _levelMenu.Selected >= _levels.Count Then
                            Back()
                        ElseIf _levelMenu.Enabled(_levelMenu.Selected) Then
                            StartLevel(_levelMenu.Selected, campaign:=False)
                        End If
```

In `HandleKey`, `ShellScreen.Title`: `If IsEnter(key) AndAlso TitleItemEnabled(_titleMenu.Selected) Then ...`.

In `StartLevel`, delete the `If campaign AndAlso index = 0 Then ... End If` block (moved into `StartCampaign`).

Journal:

```vb
        ' Every attempt that actually moved is journaled once; cleared runs go through RecordResult.
        Private Sub LogUnfinishedRun(outcome As String)
            If _engine Is Nothing OrElse _completedAtMs >= 0 OrElse _runLogged Then Return
            _runLogged = True
            _scores.AddRecord(New ScoreRecord With {
                .PlayerName = _scores.PlayerName, .LevelNumber = _levels(_levelIndex).LevelNumber, .Outcome = outcome,
                .TimeSeconds = _engine.ElapsedSeconds, .Attempts = _engine.Attempts, .WallHits = _engine.WallHits,
                .Falls = _engine.Falls, .HeartsLost = _engine.HeartsLost, .BarriersBroken = _engine.BarriersBroken,
                .Date = DateTime.Now})
        End Sub

        ' Window closing mid-run counts as a quit.
        Public Sub OnClosing()
            LogUnfinishedRun(RunOutcome.Quit)
        End Sub
```

Call sites:
- `BeginRun`: `_runLogged = False`.
- `Retry`: first line `LogUnfinishedRun(RunOutcome.Restart)`.
- `ToMainMenu`: first line `LogUnfinishedRun(RunOutcome.Quit)`.
- `TickPlaying`: in the `TimeUp` branch `LogUnfinishedRun(RunOutcome.TimeUp)`; in the `OutOfHearts` branch `LogUnfinishedRun(RunOutcome.OutOfHearts)`.
- `RecordResult`: add `.WallHits = _engine.WallHits, .Falls = _engine.Falls, .HeartsLost = _engine.HeartsLost, .BarriersBroken = _engine.BarriersBroken` to the record, and `_runLogged = True`.

Campaign rule and victory rank (fixes the case-sensitive rank match):

```vb
        Private Sub EnterVictory()
            RestoreDungeonPreferences()
            ShowAttractMaze()
            _victoryRank = 0
            If _campaignFromStart Then
                _scores.AddRecord(New ScoreRecord With {
                    .PlayerName = _scores.PlayerName, .LevelNumber = 0, .CampaignLevels = _levels.Count, .TimeSeconds = _campaignTime,
                    .Attempts = 1, .Stars = _campaignStars, .Score = 0, .Date = DateTime.Now})
                Dim mine As String = ScoreManager.KeyOf(_scores.PlayerName)
                _victoryRank = _scores.TopRecords(0, 1000, _levels.Count).FindIndex(
                    Function(r) ScoreManager.KeyOf(r.PlayerName) = mine AndAlso Math.Abs(r.TimeSeconds - _campaignTime) < 0.001F) + 1
            End If
            _sound.Play("victory")
            GoTo_(ShellScreen.Victory)
        End Sub
```

`GravityMaze/Form1.vb` `OnFormClosed`: first line `shell?.OnClosing()`.

- [ ] **Step 4: Drawing** — in `GameShell.Draw.vb` `DrawLevelSelect`, inside the card loop after `Dim sel As Boolean = ...`:

```vb
                Dim locked As Boolean = Not _levelMenu.Enabled(i)
```

When `locked`, draw the card at alpha 120 (`Color.FromArgb(120, lp.Backdrop)` and `UiDraw.WithAlpha(..., 0.45F)` on its texts), skip the stars and best time, and draw on the right instead:

```vb
                If locked Then
                    UiDraw.Text(g, $"LOCKED  ·  CLEAR LEVEL {i}", GameFonts.Body(18 * s, GameFonts.FontWeight.Bold), lp.TextDim,
                                r.Right - 24 * s, r.Y + 48 * s, 1, 3 * s)
                End If
```

In `DrawVictory`, after the stats loop, add the continued-run note:

```vb
            If Not _campaignFromStart Then
                UiDraw.Text(g, "CONTINUED RUN  ·  START FROM LEVEL 1 TO POST A CAMPAIGN TIME", GameFonts.Body(18 * s, GameFonts.FontWeight.Bold),
                            brand.TextDim, cx, y + 460 * s, 0.5F, 4 * s)
            End If
```

- [ ] **Step 5: Run the checks**

Run: `dotnet run --project tools/TransitionChecks`, then the full check set.
Expected: all pass, including `TestRunJournal`, `TestContinueCampaign`, `TestLevelLocks` and the remapped tours.

- [ ] **Step 6: Commit**

```bash
git checkout -- screenshots && git clean -fd screenshots
git add GravityMaze tools
git commit -m "Journal every attempt, save progress per player, add Continue and the campaign rule

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Who's Playing?, name de-duplication, Profile screen, Records filter

**Files:**
- Create: `GravityMaze/UI/Screens/GameShell.Players.vb` (logic)
- Modify: `GravityMaze/UI/Screens/GameShell.vb` (enum values, `CurrentMenu`, `Tick`, `GoTo_`, `HandleKey`, `HandleChar`, title cases 5 and 7, `WithPlayer`)
- Modify: `GravityMaze/UI/Screens/GameShell.Draw.vb` (`DrawPlayerSelect`, `DrawProfile`, name notice, records filter)
- Modify: `tools/TransitionChecks/TransitionChecks.vbproj` (link `GameShell.Players.vb`)
- Modify: `tools/TransitionChecks/Program.vb` (`TestPlayers`)

**Interfaces:**
- Consumes: Task 1 (`Players`, `FindPlayer`, `KeyOf`, `StatsFor`, `GhostEnabled`), Task 5 (`_afterPlayerChosen`, `ContinueAfterPlayer`, `OpenNameEntry`).
- Produces: `ShellScreen.PlayerSelect`, `ShellScreen.Profile`. Public read-only `NameNotice As String` and `RecordsMineOnly As Boolean` (for checks). `OpenPlayerSelect(after As Action)`.

- [ ] **Step 1: Write the failing check** — in `tools/TransitionChecks/Program.vb`, call it from `Main`:

```vb
    Private Sub TestPlayers()
        Using f As New Fixture()   ' current player: TEST
            f.Menu(7) : f.Shell.HandleKey(Keys.Enter)
            Check(f.Shell.Screen = ShellScreen.PlayerSelect AndAlso f.Shell.SelectedIndex = 0, "switch player lists the current player first")
            f.Menu(1) : f.Shell.HandleKey(Keys.Enter)
            Check(f.Shell.Screen = ShellScreen.NameEntry, "+ NEW PLAYER opens name entry")
            For Each ch As Char In "test" : f.Shell.HandleChar(ch) : Next
            f.Shell.HandleKey(Keys.Enter)
            Check(f.Shell.Screen = ShellScreen.NameEntry AndAlso f.Shell.NameNotice.Length > 0, "an existing name asks to continue")
            f.Shell.HandleKey(Keys.Enter)
            Check(f.Shell.Screen = ShellScreen.Title AndAlso f.Scores.Players.Count = 1, "typing test again reuses TEST")
            f.Menu(7) : f.Shell.HandleKey(Keys.Enter) : f.Menu(1) : f.Shell.HandleKey(Keys.Enter)
            For Each ch As Char In "ana" : f.Shell.HandleChar(ch) : Next
            f.Shell.HandleKey(Keys.Enter)
            Check(f.Scores.Players.Count = 2 AndAlso f.Scores.PlayerName = "ANA", "a new name creates a profile")
            f.Menu(5) : f.Shell.HandleKey(Keys.Enter)
            Check(f.Shell.Screen = ShellScreen.Profile, "profile screen opens")
            f.Shell.HandleKey(Keys.Enter)   ' GHOST toggle
            Check(Not f.Scores.GhostEnabled, "profile toggles the ghost")
            f.Shell.HandleKey(Keys.Escape)
            f.Menu(3) : f.Shell.HandleKey(Keys.Enter) : f.Shell.HandleKey(Keys.Enter)
            Check(f.Shell.Screen = ShellScreen.Records AndAlso f.Shell.RecordsMineOnly, "records filter toggles to just me")
        End Using
    End Sub
```

Add to `tools/TransitionChecks/TransitionChecks.vbproj`:

```xml
    <Compile Include="../../GravityMaze/UI/Screens/GameShell.Players.vb" Link="GameShell.Players.vb" />
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `dotnet run --project tools/TransitionChecks`
Expected: build errors (`PlayerSelect`, `NameNotice`, the missing file).

- [ ] **Step 3: Create `GravityMaze/UI/Screens/GameShell.Players.vb`**

```vb
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports GravityMaze.Data

Namespace UI.Screens
    ' Who's Playing?, name de-duplication, the Profile screen and the Records filter.
    ' Every list is a MenuList, so the board and the keyboard drive them the same way.
    Partial Public NotInheritable Class GameShell
        Private ReadOnly _playerMenu As New MenuList()
        Private _playerList As New List(Of PlayerProfile)()
        Private ReadOnly _profileMenu As New MenuList("GHOST: ON", "BACK")
        Private ReadOnly _recordsMenu As New MenuList("SHOW: ALL PLAYERS", "BACK")
        Private _recordsMineOnly As Boolean
        Private _nameNotice As String = ""
        Private _pendingPlayer As PlayerProfile   ' typed name matched this profile; Enter again confirms

        Public ReadOnly Property NameNotice As String
            Get
                Return _nameNotice
            End Get
        End Property

        Public ReadOnly Property RecordsMineOnly As Boolean
            Get
                Return _recordsMineOnly
            End Get
        End Property

        ' No profiles yet: straight to typing. Otherwise list players (current first), then + NEW PLAYER and BACK.
        Private Sub OpenPlayerSelect(after As Action)
            _afterPlayerChosen = after
            _playerList = _scores.Players
            If _playerList.Count = 0 Then OpenNameEntry() : Return
            _playerMenu.Items.Clear()
            For Each p As PlayerProfile In _playerList
                _playerMenu.Items.Add(p.Name)
            Next
            _playerMenu.Items.Add("+ NEW PLAYER")
            _playerMenu.Items.Add("BACK")
            GoTo_(ShellScreen.PlayerSelect)
            Dim key As String = ScoreManager.KeyOf(_scores.PlayerName)
            _playerMenu.Reset(Math.Max(0, _playerList.FindIndex(Function(p) p.Key = key)))
        End Sub

        Private Sub PlayerChoice(index As Integer)
            If index < _playerList.Count Then
                _scores.PlayerName = _playerList(index).Name
                ContinueAfterPlayer()
            ElseIf index = _playerList.Count Then
                OpenNameEntry()
            Else
                _afterPlayerChosen = Nothing
                GoTo_(ShellScreen.Title)
            End If
        End Sub

        ' First Enter on a name that already exists asks; the second Enter continues as that player.
        Private Function ConfirmNewName(name As String) As Boolean
            Dim existing As PlayerProfile = _scores.FindPlayer(name)
            If existing Is Nothing OrElse existing Is _pendingPlayer Then Return True
            _pendingPlayer = existing
            _nameNotice = $"{existing.Name} ALREADY EXISTS  ·  PRESS ENTER TO CONTINUE AS {existing.Name}"
            Return False
        End Function

        Private Sub ClearNameNotice()
            _nameNotice = ""
            _pendingPlayer = Nothing
        End Sub

        Private Sub OpenProfile()
            RefreshProfileLabels()
            GoTo_(ShellScreen.Profile)
        End Sub

        Private Sub RefreshProfileLabels()
            _profileMenu.Items(0) = "GHOST: " & If(_scores.GhostEnabled, "ON", "OFF")
        End Sub

        Private Sub ProfileChoice(index As Integer)
            If index = 0 Then
                _scores.GhostEnabled = Not _scores.GhostEnabled
                RefreshProfileLabels()
            Else
                GoTo_(ShellScreen.Title)
            End If
        End Sub

        Private Sub RecordsChoice(index As Integer)
            If index = 0 Then
                _recordsMineOnly = Not _recordsMineOnly
                _recordsMenu.Items(0) = "SHOW: " & If(_recordsMineOnly, "JUST ME", "ALL PLAYERS")
            Else
                Back()
            End If
        End Sub
    End Class
End Namespace
```

- [ ] **Step 4: Wire it into `GameShell.vb`**

- `ShellScreen` enum: add `PlayerSelect` and `Profile` after `NameEntry`.
- `CurrentMenu`: `Case ShellScreen.PlayerSelect : Return _playerMenu`, `Case ShellScreen.Profile : Return _profileMenu`. Split Records out of the shared case: `Case ShellScreen.Records : Return _recordsMenu` and `Case ShellScreen.HowToPlay : Return _backMenu`.
- `Tick`: split the `Records, HowToPlay` case. Records now does `TickAttract()` and `Select Case Nav(_recordsMenu, tx, ty)` → `Confirm: RecordsChoice(_recordsMenu.Selected)`, `Back: Back()`. Add:

```vb
                Case ShellScreen.PlayerSelect
                    TickAttract()
                    Select Case Nav(_playerMenu, tx, ty)
                        Case MenuAction.Confirm : PlayerChoice(_playerMenu.Selected)
                        Case MenuAction.Back : _afterPlayerChosen = Nothing : GoTo_(ShellScreen.Title)
                    End Select
                Case ShellScreen.Profile
                    TickAttract()
                    Select Case Nav(_profileMenu, tx, ty)
                        Case MenuAction.Confirm : ProfileChoice(_profileMenu.Selected)
                        Case MenuAction.Back : GoTo_(ShellScreen.Title)
                    End Select
```

- `GoTo_`: `Case ShellScreen.Records : _recordsMenu.Reset()`, `Case ShellScreen.Profile : _profileMenu.Reset()`, `Case ShellScreen.NameEntry : _nameMenu.Reset() : ClearNameNotice()`. (PlayerSelect is reset in `OpenPlayerSelect`.)
- `Back()`: send `ShellScreen.Profile` and `ShellScreen.PlayerSelect` to Title as well.
- `WithPlayer`: replace `OpenNameEntry()` with `OpenPlayerSelect(action)`, and drop the `_afterPlayerChosen = action` line above it (OpenPlayerSelect sets it).
- `TitleChoice`: `Case 5 : WithPlayer(AddressOf OpenProfile)` and `Case 7 : OpenPlayerSelect(Nothing)`.
- `ConfirmName`: after the empty check, add `If Not ConfirmNewName(name) Then Return`.
- `HandleChar` and the Backspace branch of `HandleKey` (NameEntry): call `ClearNameNotice()` whenever the buffer changes.
- `HandleKey`: Records → `If IsEnter(key) Then _sound.Play("menu_confirm") : RecordsChoice(_recordsMenu.Selected) : Return True`, `Escape → Back()`. PlayerSelect → `Enter → PlayerChoice(_playerMenu.Selected)`, `Escape → _afterPlayerChosen = Nothing : GoTo_(ShellScreen.Title)`. Profile → `Enter → ProfileChoice(_profileMenu.Selected)`, `Escape → GoTo_(ShellScreen.Title)`.

- [ ] **Step 5: Draw** — in `GameShell.Draw.vb` `Paint`, add `Case ShellScreen.PlayerSelect : DrawPlayerSelect(g, b, s, pal)` and `Case ShellScreen.Profile : DrawProfile(g, b, s, pal)`, then add:

```vb
        ' ── Who's playing? ──────────────────────────────────────────────────
        Private Sub DrawPlayerSelect(g As Graphics, b As Rectangle, s As Single, pal As ThemePalette)
            Scrim(g, b, pal, 0.66F)
            Dim x As Single = b.Width * 0.065F
            UiDraw.Text(g, "PICK YOUR PROFILE", GameFonts.Body(22 * s, GameFonts.FontWeight.Bold), pal.Accent, x, b.Height * 0.1F, 0, 6 * s)
            UiDraw.GlowText(g, "WHO'S PLAYING?", GameFonts.Display(110 * s), pal.Text, pal.AccentSoft, x, b.Height * 0.1F + 34 * s, 0, 3 * s, 4 * s)
            Dim top As Single = b.Height * 0.33F
            DrawMenu(g, _playerMenu, x + 8 * s, top, s, pal, 0, 34, 58)
            For i As Integer = 0 To _playerList.Count - 1
                Dim p As PlayerProfile = _playerList(i)
                Dim stars As Integer = 0
                For Each lv As LevelConfig In _levels
                    stars += _scores.StatsFor(p.Name, lv.LevelNumber).BestStars
                Next
                UiDraw.Text(g, $"{stars} STARS   ·   LEVEL {Math.Min(p.UnlockedLevel, _levels.Count)}   ·   {p.LastPlayed:MMM d}",
                            GameFonts.Body(20 * s), pal.TextDim, x + 420 * s, top + i * 58 * s + 8 * s)
            Next
            DrawFooter(g, b, s, pal, MenuHints, _playerMenu)
        End Sub

        ' ── Profile ─────────────────────────────────────────────────────────
        Private Sub DrawProfile(g As Graphics, b As Rectangle, s As Single, pal As ThemePalette)
            Dim_(g, b, 238)
            Dim x As Single = b.Width * 0.065F
            UiDraw.Text(g, "PLAYER PROFILE", GameFonts.Body(22 * s, GameFonts.FontWeight.Bold), pal.Accent, x, b.Height * 0.08F, 0, 6 * s)
            UiDraw.GlowText(g, _scores.PlayerName, GameFonts.Display(110 * s), pal.Text, pal.AccentSoft, x, b.Height * 0.08F + 34 * s, 0, 3 * s, 4 * s)
            Dim head As Font = GameFonts.Body(18 * s, GameFonts.FontWeight.Bold)
            Dim cell As Font = GameFonts.Body(24 * s, GameFonts.FontWeight.SemiBold)
            Dim cols As Single() = {0, 380, 540, 680, 820, 1000, 1140}
            Dim headers As String() = {"LEVEL", "ATTEMPTS", "CLEARS", "CLEAR %", "BEST", "STARS", "HOW RUNS ENDED"}
            Dim top As Single = b.Height * 0.32F
            For i As Integer = 0 To headers.Length - 1
                UiDraw.Text(g, headers(i), head, pal.TextDim, x + cols(i) * s, top, 0, 3 * s)
            Next
            For li As Integer = 0 To _levels.Count - 1
                Dim lv As LevelConfig = _levels(li)
                Dim st As LevelStats = _scores.StatsFor(_scores.PlayerName, lv.LevelNumber)
                Dim y As Single = top + (44 + li * 52) * s
                Dim ended As String = String.Join("  ·  ", st.Outcomes.Where(Function(kv) kv.Key <> RunOutcome.Cleared).
                    Select(Function(kv) $"{kv.Value} {kv.Key.Replace("-", " ")}"))
                Dim values As String() = {lv.ThemeName.ToUpperInvariant(), st.Attempts.ToString(), st.Clears.ToString(),
                    If(st.Attempts = 0, "--", $"{100 * st.Clears \ st.Attempts}%"),
                    If(st.BestTime < 0, "--:--.--", UiDraw.FormatTime(st.BestTime)), $"{st.BestStars}/3", If(ended = "", "--", ended)}
                For i As Integer = 0 To values.Length - 1
                    UiDraw.Text(g, values(i), If(i = values.Length - 1, GameFonts.Body(18 * s), cell),
                                If(i = 0, pal.Accent, pal.Text), x + cols(i) * s, y, 0, 1 * s)
                Next
            Next
            DrawMenu(g, _profileMenu, x + 8 * s, top + (60 + _levels.Count * 52) * s, s, pal, 0, 30, 52)
            DrawFooter(g, b, s, pal, MenuHints, _profileMenu)
        End Sub
```

Add `Imports System.Linq` to `GameShell.Draw.vb` if missing. In `DrawNameEntry`: change the title from `"WHO'S ROLLING?"` to `"NEW PLAYER"`; when `_nameNotice <> ""`, draw it in `pal.Accent` (`GameFonts.Body(20 * s, GameFonts.FontWeight.Bold)`) in place of the `"Type with the keyboard ..."` hint line. In `DrawRecords`: filter each column's rows when `_recordsMineOnly`:

```vb
                Dim mineKey As String = ScoreManager.KeyOf(_scores.PlayerName)
                Dim rows As List(Of ScoreRecord) = _scores.TopRecords(c.Item2, 1000, If(c.Item2 = 0, _levels.Count, 0)).
                    Where(Function(r) Not _recordsMineOnly OrElse ScoreManager.KeyOf(r.PlayerName) = mineKey).Take(8).ToList()
```

replace the `mine` comparison with `ScoreManager.KeyOf(rec.PlayerName) = mineKey`, replace `BestStars`' `String.Equals(...)` with the same key comparison, draw `DrawMenu(g, _recordsMenu, x + 8 * s, b.Height - 150 * s, s, pal, 0, 28, 46)` above the footer, and pass `_recordsMenu` to `DrawFooter`.

- [ ] **Step 6: Run the checks**

Run: `dotnet run --project tools/TransitionChecks`, then the full check set.
Expected: all pass. The UI tour still reaches name entry directly from PLAY, because a fresh records file has no profiles.

- [ ] **Step 7: Commit**

```bash
git checkout -- screenshots && git clean -fd screenshots
git add GravityMaze/UI/Screens tools/TransitionChecks
git commit -m "Add Who's Playing?, case-insensitive name reuse, Profile stats and records filter

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Ghost marble (record best run, race it, toggle)

**Files:**
- Modify: `GravityMaze/Rendering/MazeRenderer.vb` (`ghost` parameter, drawn under the marble)
- Modify: `GravityMaze/UI/GameCanvas.vb` (`GhostPosition`)
- Modify: `GravityMaze/UI/Screens/GameShell.vb` (record trail, save on best, show, `G` key)
- Modify: `GravityMaze/UI/Screens/GameShell.Draw.vb` (hint `G Ghost`)
- Modify: `tools/TransitionChecks/HeadlessAdapters.vb`, `tools/TransitionChecks/Program.vb`

**Interfaces:**
- Consumes: Task 1 `GhostRun`, `GhostFor`, `SaveGhost`, `GhostEnabled`; Task 6 `RefreshProfileLabels`.
- Produces: `GameCanvas.GhostPosition As PointF?`; `MazeRenderer.Draw(..., Optional ghost As PointF? = Nothing)`.

- [ ] **Step 1: Write the failing check** (call it from `Main`):

```vb
    Private Sub TestGhost()
        Using f As New Fixture()
            f.Campaign() : f.Goal()
            Dim ghost As GhostRun = f.Scores.GhostFor(1)
            Check(ghost IsNot Nothing AndAlso ghost.Points.Count >= 2, "the first clear saves a ghost")
            f.WaitFor(ShellScreen.Playing)
            Check(Not f.Canvas.GhostPosition.HasValue, "no ghost on a level never cleared")
            f.Shell.HandleKey(Keys.Escape) : f.Menu(2) : f.Shell.HandleKey(Keys.Enter)
            f.Shell.HandleKey(Keys.Enter) : f.WaitFor(ShellScreen.Playing) : f.Ticks(2)
            Check(f.Canvas.GhostPosition.HasValue, "the ghost races a cleared level")
            f.Shell.HandleKey(Keys.G) : f.Ticks(1)
            Check(Not f.Canvas.GhostPosition.HasValue AndAlso Not f.Scores.GhostEnabled, "G hides the ghost and remembers it")
        End Using
    End Sub
```

Add `Public Property GhostPosition As PointF?` to the fake `GameCanvas` in `HeadlessAdapters.vb`.

- [ ] **Step 2: Run it and confirm it fails**

Run: `dotnet run --project tools/TransitionChecks`
Expected: FAIL `the first clear saves a ghost`.

- [ ] **Step 3: Implement**

`MazeRenderer.Draw`: add `Optional ghost As PointF? = Nothing` after `barrierCharge`. Before `' 5. Metallic Silver Marble`, add:

```vb
                ' Ghost of the player's best run: a translucent marble the real one races.
                If ghost.HasValue Then
                    Dim gc As New PointF(left + ghost.Value.X * tileSize, top + ghost.Value.Y * tileSize)
                    Dim gr As Single = tileSize * 0.27F
                    Using fill As New SolidBrush(Color.FromArgb(70, 220, 235, 255)),
                          ring As New Pen(Color.FromArgb(150, 220, 235, 255), Math.Max(1.0F, tileSize * 0.04F))
                        graphics.FillEllipse(fill, gc.X - gr, gc.Y - gr, gr * 2, gr * 2)
                        graphics.DrawEllipse(ring, gc.X - gr, gc.Y - gr, gr * 2, gr * 2)
                    End Using
                End If
```

`GameCanvas`: add a `<DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> Public Property GhostPosition As PointF?` next to `BarrierCharge`, and pass `ghost:=GhostPosition` in the two `OnPaint` `renderer.Draw` calls.

`GameShell.vb` fields:

```vb
        Private ReadOnly _ghostTrail As New List(Of PointF)()
        Private _ghost As GhostRun   ' best run being raced (Nothing = none or hidden)
```

`BeginRun`:

```vb
            _ghostTrail.Clear()
            _ghost = If(_scores.GhostEnabled, _scores.GhostFor(_levels(_levelIndex).LevelNumber), Nothing)
```

`TickPlaying`, right after `_engine.Update(tx, ty)`:

```vb
                While _ghostTrail.Count * GhostRun.DefaultStepMs <= _engine.ElapsedSeconds * 1000.0F
                    _ghostTrail.Add(New PointF(_engine.BallX, _engine.BallY))
                End While
```

`RecordResult`, after `_resultNewBest = ...`, add one more sample at the goal and save:

```vb
            _ghostTrail.Add(New PointF(_engine.BallX, _engine.BallY))
            If _resultNewBest Then _scores.SaveGhost(cfg.LevelNumber, New GhostRun(_resultTime, GhostRun.DefaultStepMs, _ghostTrail))
```

`Tick`, inside `If IsGameScreen() Then`:

```vb
                _canvas.GhostPosition = If(_ghost IsNot Nothing AndAlso (_screen = ShellScreen.Playing OrElse _screen = ShellScreen.Paused),
                                           _ghost.PositionAt(_engine.ElapsedSeconds), CType(Nothing, PointF?))
```

and in the attract `Else` branch: `_canvas.GhostPosition = Nothing`.

`HandleKey`, before `Select Case _screen`:

```vb
            If key = Keys.G AndAlso (_screen = ShellScreen.Playing OrElse _screen = ShellScreen.Paused OrElse _screen = ShellScreen.Intro) Then
                _scores.GhostEnabled = Not _scores.GhostEnabled
                _ghost = If(_scores.GhostEnabled, _scores.GhostFor(_levels(_levelIndex).LevelNumber), Nothing)
                RefreshProfileLabels()
                _sound.Play("menu_move", 0.6F)
                Return True
            End If
```

`GameShell.Draw.vb` `DrawHud` bottom hints: add `("G", If(_scores.GhostEnabled, "Ghost on", "Ghost off"))` after `("R", "Restart")`.

- [ ] **Step 4: Run the checks**

Run: `dotnet run --project tools/TransitionChecks`, then the full check set.
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git checkout -- screenshots && git clean -fd screenshots
git add GravityMaze tools/TransitionChecks
git commit -m "Race a ghost of your best run, saved in records.xml

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Calibration screen

**Files:**
- Modify: `GravityMaze/UI/Screens/GameShell.Players.vb` (calibration logic)
- Modify: `GravityMaze/UI/Screens/GameShell.vb` (enum, menu routing, title case 6, apply at startup)
- Modify: `GravityMaze/UI/Screens/GameShell.Draw.vb` (`DrawCalibrate`)
- Modify: `GravityMaze/Form1.vb` (remove the now-duplicated calibration comment; keep `C` recenter)
- Modify: `tools/TransitionChecks/HeadlessAdapters.vb` (fake board properties), `tools/TransitionChecks/Program.vb` (`Fixture` board parameter, `TestCalibration`)

**Interfaces:**
- Consumes: Task 1 `Calibration`, `SaveSettings`; `InputManager.Board` (`ArduinoController`: `FullTiltDegrees`, `Deadzone`, `Smoothing`, `SwapAxes`, `InvertX`, `InvertY`, `Calibrate()`).
- Produces: `ShellScreen.Calibrate`; `ApplyCalibration()` called from the constructor.

- [ ] **Step 1: Write the failing check**

In `HeadlessAdapters.vb`, extend the fake `ArduinoController`:

```vb
        Public Property FullTiltDegrees As Single = 20
        Public Property Deadzone As Single = 0.08F
        Public Property Smoothing As Single = 0.35F
        Public Property SwapAxes As Boolean
        Public Property InvertX As Boolean
        Public Property InvertY As Boolean
        Public Property Calibrations As Integer
        Public Sub Calibrate()
            Calibrations += 1
        End Sub
```

`Fixture.New` gets `Optional board As ArduinoController = Nothing`, and the shell is built with `New InputManager(Tilt, board)`. Then add (and call from `Main`):

```vb
    Private Sub TestCalibration()
        Dim board As New ArduinoController With {.IsConnected = True}
        Using f As New Fixture(board:=board)
            f.Menu(6) : f.Shell.HandleKey(Keys.Enter)
            Check(f.Shell.Screen = ShellScreen.Calibrate, "calibrate opens from the title")
            f.Shell.HandleKey(Keys.Enter)                                            ' RECENTER
            Check(board.Calibrations = 1, "recenter zeroes the board")
            f.Menu(3) : f.Shell.HandleKey(Keys.Enter)                                ' INVERT X
            Check(board.InvertX AndAlso f.Scores.Calibration.InvertX, "invert X applies to the board and is saved")
            f.Menu(1) : f.Shell.HandleKey(Keys.Enter)                                ' SENSITIVITY 20 → 25
            Check(board.FullTiltDegrees = 25 AndAlso New ScoreManager(Path.Combine(f.Dir, "records.xml")).Calibration.FullTiltDegrees = 25,
                  "sensitivity steps and persists")
        End Using
    End Sub
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `dotnet run --project tools/TransitionChecks`
Expected: build error `Calibrate` is not a member of `ShellScreen`.

- [ ] **Step 3: Implement** — add `Imports System.Drawing` and `Imports GravityMaze.Input` to `GameShell.Players.vb`, then append inside the class:

```vb
        ' ── Calibration ─────────────────────────────────────────────────────
        ' Cycling values (confirm = next step) keeps left/right free for hold-to-confirm/back on the board.
        Private ReadOnly _calibrateMenu As New MenuList("RECENTER", "SENSITIVITY", "DEAD ZONE", "INVERT X", "INVERT Y", "SWAP AXES", "TEST TILT", "BACK")
        Private Shared ReadOnly SensitivitySteps As Single() = {10, 15, 20, 25, 30}
        Private Shared ReadOnly DeadzoneSteps As Single() = {0.04F, 0.08F, 0.12F, 0.16F}
        Private _testingTilt As Boolean
        Private _testBall As PointF
        Private _testVelocity As PointF
        Private _testExitMs As Single

        Private Sub OpenCalibration()
            _testingTilt = False
            RefreshCalibrationLabels()
            GoTo_(ShellScreen.Calibrate)
        End Sub

        Private Sub ApplyCalibration()
            Dim board As ArduinoController = _input.Board
            If board Is Nothing Then Return
            Dim c As CalibrationSettings = _scores.Calibration
            board.FullTiltDegrees = c.FullTiltDegrees
            board.Deadzone = c.Deadzone
            board.Smoothing = c.Smoothing
            board.SwapAxes = c.SwapAxes
            board.InvertX = c.InvertX
            board.InvertY = c.InvertY
        End Sub

        Private Shared Function NextStep(steps As Single(), current As Single) As Single
            For Each v As Single In steps
                If v > current + 0.001F Then Return v
            Next
            Return steps(0)
        End Function

        Private Sub RefreshCalibrationLabels()
            Dim c As CalibrationSettings = _scores.Calibration
            _calibrateMenu.Items(1) = $"SENSITIVITY: FULL TILT AT {c.FullTiltDegrees:0}°"
            _calibrateMenu.Items(2) = $"DEAD ZONE: {c.Deadzone:0.00}"
            _calibrateMenu.Items(3) = "INVERT X: " & If(c.InvertX, "ON", "OFF")
            _calibrateMenu.Items(4) = "INVERT Y: " & If(c.InvertY, "ON", "OFF")
            _calibrateMenu.Items(5) = "SWAP AXES: " & If(c.SwapAxes, "ON", "OFF")
        End Sub

        Private Sub CalibrationChoice(index As Integer)
            Dim c As CalibrationSettings = _scores.Calibration
            Select Case index
                Case 0 : _input.Board?.Calibrate() : Return
                Case 1 : c.FullTiltDegrees = NextStep(SensitivitySteps, c.FullTiltDegrees)
                Case 2 : c.Deadzone = NextStep(DeadzoneSteps, c.Deadzone)
                Case 3 : c.InvertX = Not c.InvertX
                Case 4 : c.InvertY = Not c.InvertY
                Case 5 : c.SwapAxes = Not c.SwapAxes
                Case 6
                    _testingTilt = True
                    _testBall = PointF.Empty
                    _testVelocity = PointF.Empty
                    _testExitMs = 0
                    Return
                Case Else : GoTo_(ShellScreen.Title) : Return
            End Select
            ApplyCalibration()
            _scores.SaveSettings()
            RefreshCalibrationLabels()
        End Sub

        ' TEST TILT: the menu freezes and a ball rolls in a circle with the board's tilt.
        ' Leave with Enter/Esc, or by holding the board fully left for 2 seconds.
        Private Sub TickCalibrate(tx As Single, ty As Single)
            If Not _testingTilt Then
                Select Case Nav(_calibrateMenu, tx, ty)
                    Case MenuAction.Confirm : CalibrationChoice(_calibrateMenu.Selected)
                    Case MenuAction.Back : GoTo_(ShellScreen.Title)
                End Select
                Return
            End If
            _testVelocity = New PointF((_testVelocity.X + tx * 0.01F) * 0.9F, (_testVelocity.Y + ty * 0.01F) * 0.9F)
            _testBall = New PointF(_testBall.X + _testVelocity.X, _testBall.Y + _testVelocity.Y)
            Dim d As Single = CSng(Math.Sqrt(_testBall.X * _testBall.X + _testBall.Y * _testBall.Y))
            If d > 1.0F Then
                _testBall = New PointF(_testBall.X / d, _testBall.Y / d)
                _testVelocity = PointF.Empty
            End If
            _testExitMs = If(tx <= -0.9F, _testExitMs + TickMs, 0.0F)
            If _testExitMs >= 2000.0F Then _testingTilt = False : _calibrateMenu.Reset(6)
        End Sub
```

In `GameShell.vb`: add `Calibrate` to `ShellScreen`; `CurrentMenu` → `Case ShellScreen.Calibrate : Return _calibrateMenu`; `Tick` → `Case ShellScreen.Calibrate : TickAttract() : TickCalibrate(tx, ty)`; `TitleChoice` → `Case 6 : OpenCalibration()`; `Back()` → Calibrate goes to Title; `HandleKey`:

```vb
                Case ShellScreen.Calibrate
                    If _testingTilt AndAlso (IsEnter(key) OrElse key = Keys.Escape) Then _testingTilt = False : _calibrateMenu.Reset(6) : Return True
                    If IsEnter(key) Then _sound.Play("menu_confirm") : CalibrationChoice(_calibrateMenu.Selected) : Return True
                    If key = Keys.Escape Then _sound.Play("menu_back") : GoTo_(ShellScreen.Title) : Return True
```

At the end of the constructor, call `ApplyCalibration()`.

`GameShell.Draw.vb`: `Case ShellScreen.Calibrate : DrawCalibrate(g, b, s, pal)` in `Paint`, plus:

```vb
        ' ── Calibrate ───────────────────────────────────────────────────────
        Private Sub DrawCalibrate(g As Graphics, b As Rectangle, s As Single, pal As ThemePalette)
            Dim_(g, b, 238)
            Dim x As Single = b.Width * 0.065F
            UiDraw.Text(g, "CONTROLLER", GameFonts.Body(22 * s, GameFonts.FontWeight.Bold), pal.Accent, x, b.Height * 0.08F, 0, 6 * s)
            UiDraw.GlowText(g, "CALIBRATE", GameFonts.Display(110 * s), pal.Text, pal.AccentSoft, x, b.Height * 0.08F + 34 * s, 0, 3 * s, 4 * s)
            DrawMenu(g, _calibrateMenu, x + 8 * s, b.Height * 0.32F, s, pal, 0, 30, 52)
            UiDraw.Text(g, BoardStatus(), GameFonts.Body(20 * s, GameFonts.FontWeight.Bold), If(BoardConnected(), pal.Accent, pal.TextDim),
                        x, b.Height * 0.32F + 8 * 52 * s + 16 * s, 0, 2 * s)

            ' Live tilt: outer ring = full tilt, dot = current input, ball = TEST TILT physics.
            Dim radius As Single = Math.Min(b.Height * 0.28F, b.Width * 0.2F)
            Dim c As New PointF(b.Width * 0.7F, b.Height * 0.55F)
            Using ring As New Pen(Color.FromArgb(120, pal.Accent), 3 * s), dead As New Pen(Color.FromArgb(60, pal.TextDim), 1.5F * s),
                  dot As New SolidBrush(pal.Accent), ball As New SolidBrush(pal.Text)
                g.DrawEllipse(ring, c.X - radius, c.Y - radius, radius * 2, radius * 2)
                Dim dz As Single = radius * _scores.Calibration.Deadzone
                g.DrawEllipse(dead, c.X - dz, c.Y - dz, dz * 2, dz * 2)
                Dim tx As Single = _input.TiltX, ty As Single = _input.TiltY
                g.FillEllipse(dot, c.X + tx * radius - 8 * s, c.Y + ty * radius - 8 * s, 16 * s, 16 * s)
                If _testingTilt Then g.FillEllipse(ball, c.X + _testBall.X * radius - 18 * s, c.Y + _testBall.Y * radius - 18 * s, 36 * s, 36 * s)
            End Using
            Dim hint As String = If(_testingTilt, "Tilt to roll the ball  ·  ENTER, ESC or hold left 2 s to finish",
                                    "Hold the board level, then RECENTER  ·  the dot shows your tilt")
            UiDraw.Text(g, hint, GameFonts.Body(20 * s), pal.TextDim, c.X, c.Y + radius + 24 * s, 0.5F)
            DrawFooter(g, b, s, pal, MenuHints, _calibrateMenu)
        End Sub
```

`GravityMaze/Form1.vb`: replace the comment `' If the ball rolls the wrong way on the real board, flip InvertX / InvertY / SwapAxes here.` with `' Tuning lives in the in-game CALIBRATE screen (saved in records.xml); C re-centers any time.`

- [ ] **Step 4: Run the checks**

Run: `dotnet run --project tools/TransitionChecks`, then the full check set.
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git checkout -- screenshots && git clean -fd screenshots
git add GravityMaze tools/TransitionChecks
git commit -m "Add in-game controller calibration screen with live tilt and test ball

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Auto-pause when the board is unplugged

**Files:**
- Modify: `GravityMaze/UI/Screens/GameShell.vb` (`WatchBoard`, shared `PausableScreen`, `DisconnectPaused`)
- Modify: `GravityMaze/UI/Screens/GameShell.Draw.vb` (banner on both pause screens)
- Modify: `tools/TransitionChecks/Program.vb` (`TestBoardUnplug`)

**Interfaces:**
- Consumes: `InputManager.Board.IsConnected`, the Task 8 `Fixture(board:=...)` parameter.
- Produces: `GameShell.DisconnectPaused As Boolean` (read-only).

- [ ] **Step 1: Write the failing check** (call it from `Main`):

```vb
    Private Sub TestBoardUnplug()
        Dim board As New ArduinoController With {.IsConnected = True}
        Using f As New Fixture(board:=board)
            board.IsConnected = False : f.Ticks(5) : board.IsConnected = True : f.Ticks(5)
            Check(f.Shell.Screen = ShellScreen.Title, "unplugging on a menu does nothing")
            f.Campaign() : f.Ticks(30)
            board.IsConnected = False : f.Ticks(1)
            Check(f.Shell.Screen = ShellScreen.Paused AndAlso f.Shell.DisconnectPaused, "unplugging mid-run pauses")
            Dim t As Single = f.Shell.Engine.ElapsedSeconds
            board.IsConnected = True : f.Ticks(200)
            Check(f.Shell.Screen = ShellScreen.Paused AndAlso f.Shell.Engine.ElapsedSeconds = t, "reconnecting never resumes by itself")
            f.Shell.HandleKey(Keys.Escape)
            Check(f.Shell.Screen = ShellScreen.Playing AndAlso Not f.Shell.DisconnectPaused, "the player resumes explicitly")
        End Using
    End Sub
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `dotnet run --project tools/TransitionChecks`
Expected: build error `DisconnectPaused` not defined.

- [ ] **Step 3: Implement** in `GameShell.vb`:

```vb
        Private _boardWasConnected As Boolean
        Private _disconnectPaused As Boolean

        Public ReadOnly Property DisconnectPaused As Boolean
            Get
                Return _disconnectPaused
            End Get
        End Property

        ' Screens where the clock or hazards run, so losing focus or the board must pause them.
        Private Function PausableScreen() As Boolean
            Select Case _screen
                Case ShellScreen.Playing, ShellScreen.Intro, ShellScreen.GoalTransition,
                     ShellScreen.DungeonBriefing, ShellScreen.DungeonRules, ShellScreen.DungeonReveal
                    Return True
            End Select
            Return False
        End Function

        ' An unplugged board pauses live play; it never resumes on its own.
        Private Sub WatchBoard()
            Dim connected As Boolean = _input.Board IsNot Nothing AndAlso _input.Board.IsConnected
            If _boardWasConnected AndAlso Not connected AndAlso PausableScreen() Then
                Pause()
                _disconnectPaused = True
                _sound.Play("menu_back")
            End If
            _boardWasConnected = connected
        End Sub
```

- Call `WatchBoard()` as the first line of `Tick()`.
- In `OnDeactivated`, replace the long `If _screen = ... Then Pause()` condition with `If PausableScreen() Then Pause()`.
- Clear the flag: `_disconnectPaused = False` in `ResumeGame`, `Retry` and `ToMainMenu`.

`GameShell.Draw.vb`: add a helper and call it at the end of both `DrawPaused` and `DrawDungeonPaused`:

```vb
        Private Sub DrawDisconnectBanner(g As Graphics, b As Rectangle, s As Single, pal As ThemePalette)
            If Not _disconnectPaused Then Return
            Dim back As Boolean = BoardConnected()
            Dim text As String = If(back, "CONTROLLER RECONNECTED  ·  HOLD THE BOARD LEVEL, THEN RESUME",
                                    "CONTROLLER DISCONNECTED  ·  RECONNECT IT, OR PRESS ESC TO PLAY ON THE KEYBOARD")
            Dim r As New RectangleF(b.Width * 0.15F, b.Height * 0.08F, b.Width * 0.7F, 64 * s)
            UiDraw.Panel(g, r, Color.FromArgb(230, pal.Backdrop), If(back, pal.Accent, Danger), 12 * s)
            UiDraw.Text(g, text, GameFonts.Body(22 * s, GameFonts.FontWeight.Bold), If(back, pal.Accent, Danger),
                        r.X + r.Width / 2, r.Y + 18 * s, 0.5F, 3 * s)
        End Sub
```

- [ ] **Step 4: Run the checks**

Run: `dotnet run --project tools/TransitionChecks`, then the full check set.
Expected: all pass, including the existing focus-loss checks (`OnDeactivated` behaviour is unchanged).

- [ ] **Step 5: Commit**

```bash
git checkout -- screenshots && git clean -fd screenshots
git add GravityMaze/UI/Screens tools/TransitionChecks
git commit -m "Pause play when the Arduino is unplugged and wait for the player to resume

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Docs, new screenshots, final verification

**Files:**
- Modify: `Gravity_Maze_PRD.md` (§4 Gameplay, §5 Level 3, §9 Data Storage, §12 Scope)
- Modify: `Gravity_Maze_AGENTS.md` (Maze System symbols)
- Modify: `SETUP.md` (calibration screen)
- Modify: `tools/VerificationRunner/UiTour.vb` (captures for new screens)
- Commit: regenerated `screenshots/`

- [ ] **Step 1: Capture the new screens in the UI tour** — in `UiTour.Run`, right after the existing lines that follow `Shot("ui_11_records")` (`Key(Keys.Escape) : Ticks(10)` and `Expect(ShellScreen.Title)`), insert:

```vb
            MoveTo(5) : Key(Keys.Enter) : Ticks(30)
            Expect(ShellScreen.Profile)
            Shot("ui_16_profile")
            Key(Keys.Escape) : Ticks(10)
            MoveTo(7) : Key(Keys.Enter) : Ticks(30)
            Expect(ShellScreen.PlayerSelect)
            Shot("ui_17_player_select")
            Key(Keys.Escape) : Ticks(10)
            MoveTo(6) : Key(Keys.Enter) : Ticks(30)
            Expect(ShellScreen.Calibrate)
            Shot("ui_18_calibrate")
            Key(Keys.Escape) : Ticks(10)
            Expect(ShellScreen.Title)
```

- [ ] **Step 2: Update the docs**

`Gravity_Maze_PRD.md`:
- §5 Level 3: replace `Timer (~1.4x measured par time)` with `Overdrive barriers (B): neon force fields across the route that shatter only when hit at boost speed (from F strips); a slower hit is a normal wall bounce (¼ heart)`. Replace the `H = Hole (... timer keeps running)` note with `H = Hole (ball falls and respawns at start; broken barriers stay broken)`, and change `Maze size: - 25x25` to `Maze size: - 37x37 in game (25x25 source widened to 2-tile corridors)`.
- §4: add `Instant restart: R restarts the level at once (keyboard only); the abandoned attempt is still recorded.` and `Unplugging the Arduino mid-run pauses the game; play resumes only when the player chooses RESUME.`
- §9 Data Storage: replace `The project saves performance history only, not game progress.` with a short description of v2: player profiles (case-insensitive names, unlocked level, ghost setting), one `<Run>` per attempt with outcome (`cleared`/`out-of-hearts`/`restart`/`quit`), time, wall hits, falls, hearts lost, barriers broken, stars and score; the best-run ghost per level; controller calibration; automatic v1 upgrade; campaign records only for unbroken Level 1 → end runs.
- §12 Scope: change `Timer` to `Stopwatch timer (no countdown)`; under Bonus add `Player profiles, ghost replays, calibration screen`.

`Gravity_Maze_AGENTS.md` Maze System: add `B = Overdrive barrier (solid until hit at boost speed, then open for the rest of the attempt)` to the symbol list.

`SETUP.md`: in the Arduino/controller section, add that tuning is now done in-game via **CALIBRATE** on the title menu (recenter, sensitivity, dead zone, invert X/Y, swap axes, test tilt), saved to `%AppData%\GravityMaze\records.xml`; `C` still re-centers at any time; the `Form1.vb` values are only the defaults.

- [ ] **Step 3: Full verification**

Run the full check set from Global Constraints.
Expected: every project passes; VerificationRunner ends with `ALL VERIFICATIONS PASSED SUCCESSFULLY!`. Inspect `screenshots/ui/ui_12_hud_level3.png`, `ui_16_profile.png`, `ui_17_player_select.png` and `ui_18_calibrate.png` in the worktree for clipped or overlapping text at 1600×900; fix any layout issue before committing.

Then launch the real game once (`dotnet run --project GravityMaze`) and confirm by hand: Level 3 barriers smash on a boost and bounce with TOO SLOW otherwise; the ghost appears on a second attempt; unplugging the Arduino (if available) pauses.

- [ ] **Step 4: Commit docs and screenshots**

```bash
git rm -q screenshots/ui/ui_13_time_up.png
git add Gravity_Maze_PRD.md Gravity_Maze_AGENTS.md SETUP.md tools/VerificationRunner/UiTour.vb screenshots
git commit -m "Document barriers, profiles and calibration; refresh screenshots

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 5: Hand off** — use superpowers:finishing-a-development-branch to decide how to merge `feature/barriers-profiles` back into `feature/dungeon-revamp`.
