Imports System
Imports System.IO
Imports System.Linq
Imports System.Collections.Generic
Imports System.Windows.Forms
Imports GravityMaze.Audio
Imports GravityMaze.Data
Imports GravityMaze.Engine
Imports GravityMaze.Input
Imports GravityMaze.Levels
Imports GravityMaze.Rendering
Imports GravityMaze.UI
Imports GravityMaze.UI.Screens

Module Program
    Private _checks As Integer
    Private Sub Check(condition As Boolean, message As String)
        _checks += 1
        If Not condition Then Throw New Exception(message)
    End Sub

    Private Class FakeTilt
        Implements ITiltInput
        Public Property X As Single
        Public Property Y As Single
        Public ReadOnly Property TiltX As Single Implements ITiltInput.TiltX
            Get
                Return X
            End Get
        End Property
        Public ReadOnly Property TiltY As Single Implements ITiltInput.TiltY
            Get
                Return Y
            End Get
        End Property
    End Class

    Private Class Fixture
        Implements IDisposable
        Public ReadOnly Dir As String = Path.Combine(Path.GetTempPath(), "gm_transition_" & Guid.NewGuid().ToString("N"))
        Public ReadOnly Tilt As New FakeTilt()
        Public ReadOnly Canvas As New GameCanvas()
        Public ReadOnly Sound As New SoundManager()
        Public ReadOnly Scores As ScoreManager
        Public ReadOnly Shell As GameShell
        Public Sub New(Optional includeDungeon As Boolean = False, Optional includeExtraLevel As Boolean = False, Optional barrierLevel As Boolean = False)
            Directory.CreateDirectory(Dir)
            Dim levels As New List(Of LevelConfig)()
            For i As Integer = 1 To 3
                Dim file As String = Path.Combine(Dir, $"level{i}.txt")
                IO.File.WriteAllLines(file, If(barrierLevel AndAlso i = 1, {"1111111", "1S00BG1", "1111111"}, {"111111", "1S00G1", "111111"}))
                levels.Add(New LevelConfig(i, {"Wooden Workshop", "Frozen Labyrinth", "Neon Velocity"}(i - 1), file, 0, 5))
            Next
            If includeDungeon Then levels.Add(New LevelConfig(4, "Forgotten Keep", Path.Combine(AppContext.BaseDirectory, "Mazes", "Level4.txt"), 0, 180))
            If includeExtraLevel Then levels.Add(New LevelConfig(5, "Wooden Workshop", Path.Combine(Dir, "level1.txt"), 0, 5))
            Scores = New ScoreManager(Path.Combine(Dir, "records.xml")) With {.PlayerName = "TEST"}
            Shell = New GameShell(Canvas, New InputManager(Tilt), Sound, Scores, levels)
        End Sub
        Public Sub Ticks(count As Integer)
            For i As Integer = 1 To count : Shell.Tick() : Next
        End Sub
        Public Sub WaitFor(screen As ShellScreen, Optional limit As Integer = 500)
            For i As Integer = 1 To limit
                If Shell.Screen = screen Then Return
                Shell.Tick()
            Next
            Check(False, $"Expected {screen}, reached {Shell.Screen}")
        End Sub
        Public Sub Campaign()
            Shell.HandleKey(Keys.Enter)
            WaitFor(ShellScreen.Playing)
        End Sub
        Public Sub Goal(Optional pauseEscape As Boolean = False)
            Dim escapedPause As Boolean = False
            Dim pilot As New AttractPilot(Shell.Engine.Maze)
            For i As Integer = 1 To If(Shell.Engine.Dungeon Is Nothing, 500, 10000)
                If Shell.Screen = ShellScreen.GoalTransition Then
                    Tilt.X = 0 : Tilt.Y = 0
                    Return
                End If
                If pauseEscape AndAlso Not escapedPause AndAlso Shell.Engine.Dungeon IsNot Nothing AndAlso Shell.Engine.Dungeon.Collapse IsNot Nothing Then
                    escapedPause = True
                    Dim collapseClock = Shell.Engine.Dungeon.Collapse.ElapsedMs
                    Dim trapClock = Shell.Engine.Dungeon.ElapsedMs
                    Shell.HandleKey(Keys.Escape) : Ticks(200)
                    Check(Shell.Engine.Dungeon.Collapse.ElapsedMs = collapseClock AndAlso Shell.Engine.Dungeon.ElapsedMs = trapClock, "Mid-escape pause must freeze collapse and trap clocks for 200 ticks")
                    Shell.HandleKey(Keys.Escape)
                End If
                Dim input = pilot.NextTilt(Shell.Engine)
                Tilt.X = input.X : Tilt.Y = input.Y
                Shell.Tick()
            Next
            Check(False, "Pilot did not reach the transition")
        End Sub
        Public Sub Fall()
            For row As Integer = 0 To Shell.Engine.Maze.RowCount - 1
                For col As Integer = 0 To Shell.Engine.Maze.ColumnCount - 1
                    If Shell.Engine.Maze.GetTile(row, col) <> "W"c Then Continue For
                    Dim flags = Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic
                    GetType(GameEngine).GetField("_ballX", flags).SetValue(Shell.Engine, col + 0.5F)
                    GetType(GameEngine).GetField("_ballY", flags).SetValue(Shell.Engine, row + 0.5F)
                    Shell.Tick()
                    Return
                Next
            Next
            Throw New Exception("Fixture has no lava pit")
        End Sub
        Public Sub Menu(index As Integer)
            For guard As Integer = 1 To 12
                Tilt.X = 0 : Tilt.Y = 0 : Ticks(3)
                If Shell.SelectedIndex = index Then Return
                Tilt.Y = 1 : Ticks(1)
            Next
            Check(False, "Menu navigation failed")
        End Sub
        Public Sub Dispose() Implements IDisposable.Dispose
            Directory.Delete(Dir, True)
        End Sub
    End Class

    Sub Main()
        TestDungeonBriefing()
        TestDungeonObjectives()
        TestDungeonCheckpoints()
        TestDungeonReveal()
        TestDungeonRevealFocusLossOverview()
        TestDungeonPreferences()
        TestDungeonOtherLevelExit()
        TestCameraPreferences()
        TestClock()
        TestCampaign()
        TestDungeonCampaign()
        TestPractice()
        TestRetry()
        TestCancel()
        TestIntroPause()
        TestBarrierFeedback()
        Console.WriteLine($"PASS: {_checks} transition and real-shell flow checks (headless presentation adapters)")
    End Sub

    Private Sub EnterDungeon(f As Fixture)
        f.Menu(1) : f.Shell.HandleKey(Keys.Enter)
        f.Menu(3) : f.Shell.HandleKey(Keys.Enter)
        Check(f.Shell.Screen = ShellScreen.DungeonBriefing, "Level select must enter dungeon briefing")
    End Sub

    Private Sub CollectKey(f As Fixture, key As Char)
        For row As Integer = 0 To f.Shell.Engine.Maze.RowCount - 1
            For col As Integer = 0 To f.Shell.Engine.Maze.ColumnCount - 1
                If f.Shell.Engine.Maze.GetTile(row, col) <> key Then Continue For
                f.Shell.Engine.Dungeon.Collect(col + 0.5F, row + 0.5F)
                Return
            Next
        Next
        Throw New Exception("Missing fixture key " & key)
    End Sub

    Private Sub LightCheckpoint(f As Fixture, escape As Boolean, ordinal As Integer)
        Dim cp = f.Shell.Engine.Dungeon.Definition.Checkpoints.Where(Function(p) p.Escape = escape).ElementAt(ordinal)
        f.Shell.Engine.Dungeon.Advance(cp.Column + 0.5F, cp.Row + 0.5F, cp.Column + 0.5F, cp.Row + 0.5F)
    End Sub

    Private Sub TestDungeonBriefing()
        Using f As New Fixture(True)
            EnterDungeon(f)
            f.Tilt.X = 1 : f.Ticks(1000)
            Check(f.Shell.Screen = ShellScreen.DungeonBriefing, "Held tilt on entry must not arm READY or timeout")
            Check(f.Shell.Engine.ElapsedSeconds = 0 AndAlso f.Shell.Engine.Dungeon.ElapsedMs = 0, "Briefing freezes the real engine indefinitely")
            Check(Not f.Canvas.TiltViewEnabled AndAlso f.Canvas.CameraPreferredZoom = 1.5F AndAlso f.Canvas.CameraZoom = 1, "Fresh dungeon defaults to flat overview and a saved 1.5x play preference")
            f.Tilt.X = 0 : f.Ticks(2)
            f.Menu(1) : f.Shell.HandleKey(Keys.Enter)
            Check(f.Canvas.CameraPreferredZoom = 2 AndAlso f.Shell.SelectedIndex = 1, "Briefing CAMERA cycles and retains selection")
            f.Menu(2) : f.Shell.HandleKey(Keys.Space)
            Check(f.Shell.Screen = ShellScreen.DungeonRules, "Briefing opens rules")
            f.Shell.OnDeactivated() : f.Ticks(200) : f.Shell.HandleKey(Keys.P)
            Check(f.Shell.Screen = ShellScreen.DungeonRules, "Focus loss returns to the same rules screen")
            f.Shell.HandleKey(Keys.Escape)
            Check(f.Shell.Screen = ShellScreen.DungeonBriefing AndAlso f.Shell.SelectedIndex = 2 AndAlso f.Canvas.CameraPreferredZoom = 2, "Rules BACK retains briefing selection and camera")
            f.Shell.OnDeactivated() : f.Ticks(200) : f.Shell.HandleKey(Keys.P)
            Check(f.Shell.Screen = ShellScreen.DungeonBriefing AndAlso f.Shell.SelectedIndex = 2, "Focus loss resumes briefing selection")
            f.Menu(0) : f.Tilt.X = 1 : f.Ticks(57)
            Check(f.Shell.Screen = ShellScreen.Intro, "Neutral-armed hold right confirms READY")
            f.Tilt.X = 0 : f.Ticks(10)
            Check(Not f.Canvas.PresentationOverview AndAlso f.Canvas.CameraZoom > 1 AndAlso f.Canvas.CameraZoom < 2, "READY releases overview for a smooth preferred entrance zoom")
            f.Shell.OnDeactivated() : f.Ticks(200) : f.Shell.HandleKey(Keys.P)
            f.Ticks(40)
            Check(f.Shell.Screen = ShellScreen.Intro AndAlso f.Shell.Engine.ElapsedSeconds = 0 AndAlso f.Shell.Engine.Dungeon.ElapsedMs = 0, "Countdown pause resumes at its frozen point")
            f.WaitFor(ShellScreen.Playing)
            Check(f.Shell.Engine.ElapsedSeconds = 0, "READY uses only the countdown and freezes physics through GO")
        End Using
        For Each key In {Keys.Enter, Keys.Space}
            Using f As New Fixture(True)
                EnterDungeon(f) : f.Shell.HandleKey(key)
                Check(f.Shell.Screen = ShellScreen.Intro, "Keyboard READY enters countdown")
                f.Ticks(122)
                Check(f.Shell.Screen = ShellScreen.Playing AndAlso f.Shell.Engine.ElapsedSeconds = 0, "No second 1.6 second card after READY")
            End Using
        Next
        Using f As New Fixture(True)
            f.Menu(1) : f.Shell.HandleKey(Keys.Enter)
            f.Menu(2) : f.Shell.HandleKey(Keys.Enter)
            f.WaitFor(ShellScreen.Playing) : f.Goal() : f.WaitFor(ShellScreen.LevelComplete)
            f.Ticks(90) : f.Shell.HandleKey(Keys.Enter)
            Check(f.Shell.Screen = ShellScreen.DungeonBriefing, "NEXT LEVEL menu freshly enters dungeon briefing")
        End Using
    End Sub

    Private Sub TestDungeonObjectives()
        Check(DungeonPresentation.Objective(3, False).Contains(ChrW(&H2014)), "Exact objective retains its em dash")
        Check(DungeonPresentation.Objective(0, False) = "Find the Crypt and Armory keys.", "Initial key objective")
        Check(DungeonPresentation.Objective(1, False) = "Find the Armory key in the east wing.", "Crypt-first objective")
        Check(DungeonPresentation.Objective(2, False) = "Find the Crypt key in the west wing.", "Armory-first objective")
        Check(DungeonPresentation.Objective(3, False) = "Vault unlocked — head north and take the vault key.", "Both keys unlock north vault objective")
        Check(DungeonPresentation.Objective(7, True) = "Escape through the new passage and return to the entrance.", "Escape objective")
        Check(DungeonPresentation.VaultWarning(3, False) = "Taking the vault key starts the collapse. Escape through the new passage and return to the entrance.", "Pre-vault collapse warning")
        For Each mask In {0, 1, 2, 7}
            Check(DungeonPresentation.VaultWarning(mask, False) = "", "No premature or collected-vault warning")
        Next
        Check(DungeonPresentation.VaultWarning(3, True) = "", "No pre-vault warning during escape")
    End Sub

    Private Sub TestDungeonCheckpoints()
        Using f As New Fixture(True)
            EnterDungeon(f) : f.Shell.HandleKey(Keys.Enter) : f.WaitFor(ShellScreen.Playing)
            LightCheckpoint(f, False, 0)
            Check(f.Shell.DungeonCheckpointMessage = "Checkpoint lit — falls return you here; collected keys stay." AndAlso f.Shell.DungeonCheckpointRemainingMs = 4000, "First exploration checkpoint teaches for 4 seconds")
            f.Shell.HandleKey(Keys.Escape) : f.Ticks(200)
            Check(f.Shell.DungeonCheckpointRemainingMs = 4000, "Checkpoint cue clock freezes while paused")
            f.Menu(5) : f.Shell.HandleKey(Keys.Enter)
            f.Shell.OnDeactivated() : f.Ticks(200) : f.Shell.HandleKey(Keys.P) : f.Shell.HandleKey(Keys.Escape)
            Check(f.Shell.Screen = ShellScreen.Paused, "Rules entered from pause return to pause after focus loss")
            f.Shell.HandleKey(Keys.Escape)
            Check(f.Shell.Screen = ShellScreen.Playing, "Nested rules pause retains original play resume state")
            f.Ticks(249)
            Check(f.Shell.DungeonCheckpointRemainingMs = 16, "First checkpoint has its full 4000 ms duration")
            f.Ticks(1) : Check(f.Shell.DungeonCheckpointMessage = "", "Checkpoint teaching expires exactly at 4000 ms")
            LightCheckpoint(f, False, 1)
            Check(f.Shell.DungeonCheckpointMessage = "Checkpoint lit" AndAlso f.Shell.DungeonCheckpointRemainingMs = 2000, "Later exploration checkpoint uses 2 second cue")
            f.Ticks(124) : Check(f.Shell.DungeonCheckpointRemainingMs = 16, "Later checkpoint cue retains final tick")
            f.Ticks(1) : Check(f.Shell.DungeonCheckpointMessage = "", "Later checkpoint expires at 2000 ms")
            CollectKey(f, "K"c) : CollectKey(f, "Q"c) : f.Shell.Tick() : f.WaitFor(ShellScreen.Playing)
            CollectKey(f, "V"c)
            LightCheckpoint(f, True, 0)
            Check(f.Shell.DungeonCheckpointRemainingMs = 4000 AndAlso f.Shell.DungeonCheckpointMessage.Contains("collected keys stay"), "First escape checkpoint teaches independently of exploration")
            LightCheckpoint(f, True, 1)
            Check(f.Shell.DungeonCheckpointRemainingMs = 2000 AndAlso f.Shell.DungeonCheckpointMessage = "Checkpoint lit", "Later escape checkpoint uses short cue")
            f.Shell.HandleKey(Keys.R)
            Check(f.Shell.DungeonCheckpointMessage = "" AndAlso f.Shell.DungeonCheckpointRemainingMs = 0, "Restart clears checkpoint messages")
            f.WaitFor(ShellScreen.Playing) : LightCheckpoint(f, False, 0)
            Check(f.Shell.DungeonCheckpointRemainingMs = 4000, "Restart rearms exploration teaching")
            CollectKey(f, "K"c) : CollectKey(f, "Q"c) : f.Shell.Tick() : f.WaitFor(ShellScreen.Playing) : CollectKey(f, "V"c) : LightCheckpoint(f, True, 0)
            Check(f.Shell.DungeonCheckpointRemainingMs = 4000, "Restart rearms escape teaching")
        End Using
    End Sub

    Private Sub TestDungeonReveal()
        For Each order In {"KQ", "QK"}
            Using f As New Fixture(True)
                EnterDungeon(f) : f.Shell.HandleKey(Keys.Enter) : f.WaitFor(ShellScreen.Playing) : f.Ticks(50)
                CollectKey(f, order(0)) : f.Shell.Tick()
                Check(f.Shell.Screen = ShellScreen.Playing, "First key does not force camera")
                LightCheckpoint(f, False, 0)
                CollectKey(f, order(1))
                Dim time = f.Shell.Engine.ElapsedSeconds, trapTime = f.Shell.Engine.Dungeon.ElapsedMs
                Dim ball = New System.Drawing.PointF(f.Shell.Engine.BallX, f.Shell.Engine.BallY)
                f.Shell.Tick()
                Check(f.Shell.Screen = ShellScreen.DungeonReveal AndAlso f.Shell.DungeonRevealElapsedMs = 0, "Either second-key order starts vault reveal")
                f.Ticks(40)
                Dim revealTime = f.Shell.DungeonRevealElapsedMs, focus = f.Canvas.CameraFocus, zoom = f.Canvas.CameraZoom
                f.Shell.OnDeactivated() : f.Ticks(200)
                Check(f.Shell.DungeonRevealElapsedMs = revealTime AndAlso f.Canvas.CameraFocus = focus AndAlso f.Canvas.CameraZoom = zoom, "Pause freezes the same reveal frame")
                f.Shell.HandleKey(Keys.Tab) : f.Ticks(1)
                Check(f.Canvas.CameraZoom = 1 AndAlso f.Shell.DungeonRevealElapsedMs = revealTime, "Tab overview remains available during a frozen reveal")
                f.Shell.HandleKeyUp(Keys.Tab)
                Check(f.Canvas.CameraZoom = zoom AndAlso f.Canvas.CameraFocus = focus, "Releasing Tab restores the same paused reveal frame")
                f.Menu(5) : f.Shell.HandleKey(Keys.Enter) : f.Ticks(20)
                Check(f.Shell.Screen = ShellScreen.DungeonRules AndAlso f.Canvas.CameraZoom = 1, "Rules force full overview even from a paused reveal")
                f.Shell.HandleKey(Keys.Escape)
                Check(f.Shell.Screen = ShellScreen.Paused AndAlso f.Canvas.CameraZoom = zoom, "Rules restore the paused reveal's camera frame")
                f.Shell.HandleKey(Keys.P) : f.Ticks(40)
                Check(f.Canvas.CameraZoom = 2 AndAlso f.Canvas.CameraPreferredZoom = 1.5F, "Reveal holds temporary 2x without changing preferred zoom")
                Dim gateX As Single = 0, gateY As Single = 0, count As Integer = 0
                For r As Integer = 0 To f.Shell.Engine.Maze.RowCount - 1
                    For c As Integer = 0 To f.Shell.Engine.Maze.ColumnCount - 1
                        If f.Shell.Engine.Maze.GetTile(r, c) = "c"c Then gateX += c + 0.5F : gateY += r + 0.5F : count += 1
                    Next
                Next
                Check(Math.Abs(f.Canvas.CameraFocus.X - gateX / count / f.Shell.Engine.Maze.ColumnCount) < 0.0001 AndAlso Math.Abs(f.Canvas.CameraFocus.Y - gateY / count / f.Shell.Engine.Maze.RowCount) < 0.0001, "Reveal centers the opened inbound vault gate")
                f.Ticks(44)
                Check(f.Shell.Screen = ShellScreen.DungeonReveal AndAlso f.Shell.DungeonRevealElapsedMs = 1984 AndAlso f.Canvas.CameraZoom < 2, "Return happens before the reveal's final tick")
                f.Ticks(1)
                Check(f.Shell.Screen = ShellScreen.Playing AndAlso f.Shell.DungeonRevealElapsedMs = 2000, "Reveal resumes after exactly 2000 ms")
                Check(f.Shell.Engine.ElapsedSeconds = time AndAlso f.Shell.Engine.Dungeon.ElapsedMs = trapTime AndAlso f.Shell.DungeonCheckpointRemainingMs = 4000, "Reveal freezes engine, traps, and checkpoint cue clocks")
                Check(f.Shell.Engine.BallX = ball.X AndAlso f.Shell.Engine.BallY = ball.Y, "Reveal never teleports the ball")
                Check(f.Canvas.CameraZoom = 1.5F AndAlso Math.Abs(f.Canvas.CameraFocus.X - ball.X / f.Shell.Engine.Maze.ColumnCount) < 0.0001 AndAlso Math.Abs(f.Canvas.CameraFocus.Y - ball.Y / f.Shell.Engine.Maze.RowCount) < 0.0001, "Reveal returns to marble and player's preferred zoom before play resumes")
                Dim attempts = f.Shell.Engine.Attempts
                Dim hearts = f.Shell.Engine.Hearts
                f.Fall() : f.Ticks(1)
                Check(f.Shell.Engine.Hearts = hearts - 1, "Real lava fall costs exactly one heart")
                Check(f.Shell.Engine.Attempts = attempts, "Real lava fall leaves attempts unchanged")
                Check(f.Shell.Engine.Dungeon.KeyMask = 3 AndAlso f.Shell.Screen = ShellScreen.Playing, "Real lava fall retains keys and does not replay second-key reveal")
                CollectKey(f, "V"c) : f.Ticks(1)
                Check(f.Shell.Screen = ShellScreen.Playing, "Third key never forces another camera reveal")
                f.Shell.HandleKey(Keys.R) : f.WaitFor(ShellScreen.Playing)
                CollectKey(f, order(0)) : CollectKey(f, order(1)) : f.Shell.Tick()
                Check(f.Shell.Screen = ShellScreen.DungeonReveal, "Restart rearms vault door reveal")
            End Using
        Next
    End Sub

    Private Sub TestDungeonRevealFocusLossOverview()
        For Each alreadyPaused In {False, True}
            Using f As New Fixture(True)
                EnterDungeon(f) : f.Shell.HandleKey(Keys.Enter) : f.WaitFor(ShellScreen.Playing)
                CollectKey(f, "K"c) : CollectKey(f, "Q"c) : f.Shell.Tick() : f.Ticks(40)
                If alreadyPaused Then f.Shell.HandleKey(Keys.P)
                f.Shell.HandleKey(Keys.Tab) : f.Ticks(1)
                Check(f.Canvas.CameraZoom = 1, "Held Tab shows overview before focus loss during reveal")
                Dim revealTime = f.Shell.DungeonRevealElapsedMs
                Dim engineTime = f.Shell.Engine.ElapsedSeconds, trapTime = f.Shell.Engine.Dungeon.ElapsedMs
                f.Shell.OnDeactivated()
                Dim focus = f.Canvas.CameraFocus
                Check(f.Shell.Screen = ShellScreen.Paused AndAlso Not f.Canvas.CameraOverviewHeld AndAlso f.Canvas.CameraZoom = 2, "Focus loss restores the reveal frame after clearing held Tab, including an already paused reveal")
                f.Ticks(200)
                Check(f.Shell.DungeonRevealElapsedMs = revealTime AndAlso f.Canvas.CameraFocus = focus AndAlso f.Canvas.CameraZoom = 2, "Held-Tab focus loss freezes reveal time and camera frame")
                Check(f.Shell.Engine.ElapsedSeconds = engineTime AndAlso f.Shell.Engine.Dungeon.ElapsedMs = trapTime, "Held-Tab focus loss freezes engine and trap clocks")
                f.Shell.HandleKey(Keys.P) : f.Ticks(1)
                Check(f.Shell.Screen = ShellScreen.DungeonReveal AndAlso f.Shell.DungeonRevealElapsedMs = revealTime + 16, "Focus return resumes the same reveal sequence")
            End Using
        Next
    End Sub

    Private Sub TestDungeonPreferences()
        Using f As New Fixture(True)
            f.Shell.HandleKey(Keys.F8)
            f.Shell.HandleKey(Keys.F9) : f.Shell.HandleKeyUp(Keys.F9)
            f.Shell.HandleKey(Keys.F9) : f.Shell.HandleKeyUp(Keys.F9)
            EnterDungeon(f)
            f.Shell.HandleKey(Keys.F8)
            f.Shell.HandleKey(Keys.F9) : f.Shell.HandleKeyUp(Keys.F9)
            f.Shell.HandleKey(Keys.Enter) : f.WaitFor(ShellScreen.Playing)
            f.Fall() : f.Ticks(40)
            f.Shell.HandleKey(Keys.Escape) : f.Ticks(50) : f.Shell.HandleKey(Keys.Escape)
            Check(f.Canvas.TiltViewEnabled AndAlso f.Canvas.CameraPreferredZoom = 2, "Dungeon manual preferences survive fall effects and pause")
            f.Shell.HandleKey(Keys.R)
            Check(f.Shell.Screen = ShellScreen.Intro AndAlso f.Canvas.TiltViewEnabled AndAlso f.Canvas.CameraPreferredZoom = 2, "Restart skips briefing and preserves chosen view")
            f.WaitFor(ShellScreen.Playing)
            CollectKey(f, "K"c) : CollectKey(f, "Q"c) : f.Shell.Tick() : f.WaitFor(ShellScreen.Playing) : CollectKey(f, "V"c)
            Check(f.Canvas.TiltViewEnabled AndAlso f.Canvas.CameraPreferredZoom = 2, "Collapse preserves manual view and zoom")
            f.Shell.HandleKey(Keys.Escape) : f.Menu(2) : f.Shell.HandleKey(Keys.Enter)
            Check(f.Shell.Screen = ShellScreen.Title AndAlso Not f.Canvas.TiltViewEnabled AndAlso f.Canvas.CameraPreferredZoom = 2, "Menu exit restores captured pre-dungeon preferences")
            EnterDungeon(f)
            Check(Not f.Canvas.TiltViewEnabled AndAlso f.Canvas.CameraPreferredZoom = 1.5F, "Another fresh entry defaults again")
        End Using
    End Sub

    Private Sub TestDungeonOtherLevelExit()
        Using f As New Fixture(True, True)
            f.Shell.HandleKey(Keys.F8)
            EnterDungeon(f)
            f.Shell.HandleKey(Keys.F8)
            f.Shell.HandleKey(Keys.F9) : f.Shell.HandleKeyUp(Keys.F9)
            f.Shell.HandleKey(Keys.Enter) : f.WaitFor(ShellScreen.Playing)
            f.Goal() : f.WaitFor(ShellScreen.LevelComplete)
            f.Ticks(90) : f.Shell.HandleKey(Keys.Enter)
            Check(f.Shell.Screen = ShellScreen.Intro AndAlso f.Shell.Engine.Dungeon Is Nothing AndAlso Not f.Canvas.TiltViewEnabled AndAlso f.Canvas.CameraPreferredZoom = 1, "Leaving the dungeon for another level restores captured preferences")
        End Using
    End Sub

    Private Sub TestCameraPreferences()
        Using f As New Fixture()
            f.Ticks(1)
            Check(Not f.Canvas.CameraEnabled, "Menus must retain full-maze framing")
            f.Campaign()
            Check(f.Canvas.CameraEnabled, "Gameplay must enable the shared camera")
            Check(f.Shell.HandleKey(Keys.F9) AndAlso f.Canvas.CameraLabel = "1.5x", "F9 selects a closer view")
            f.Shell.HandleKey(Keys.F9)
            Check(f.Canvas.CameraLabel = "1.5x", "Keyboard repeat must not cycle zoom repeatedly")
            f.Shell.HandleKeyUp(Keys.F9)
            f.Shell.HandleKey(Keys.F9) : f.Shell.HandleKeyUp(Keys.F9)
            Check(f.Canvas.CameraLabel = "2x", "A new F9 press selects 2x")
            f.Shell.HandleKey(Keys.Escape)
            Dim time = f.Shell.Engine.ElapsedSeconds
            f.Menu(4) : f.Shell.HandleKey(Keys.Enter)
            Check(f.Shell.Screen = ShellScreen.Paused AndAlso f.Canvas.CameraLabel = "FULL MAZE", "Pause-menu camera control cycles without resuming")
            f.Tilt.X = 1 : f.Tilt.Y = 0 : f.Ticks(100)
            Check(f.Canvas.CameraLabel = "1.5x", "Arduino hold-to-confirm must select exactly one zoom step")
            Check(f.Shell.Engine.ElapsedSeconds = time, "Changing the camera while paused must not advance physics")
            f.Tilt.X = 0 : f.Tilt.Y = 0
            f.Shell.HandleKey(Keys.Tab)
            Check(f.Canvas.CameraOverviewHeld, "Tab must enable temporary overview")
            f.Shell.HandleKeyUp(Keys.Tab)
            Check(Not f.Canvas.CameraOverviewHeld AndAlso f.Canvas.CameraLabel = "1.5x", "Releasing Tab must retain selected zoom")
            f.Shell.HandleKey(Keys.Tab) : f.Shell.OnDeactivated()
            Check(Not f.Canvas.CameraOverviewHeld, "Focus loss must release overview")
            f.Shell.HandleKey(Keys.R) : f.WaitFor(ShellScreen.Playing)
            Check(f.Canvas.CameraLabel = "1.5x", "Retry must keep camera preference")
            For level As Integer = 1 To 3
                f.Goal()
                f.WaitFor(If(level = 3, ShellScreen.Victory, ShellScreen.Playing))
                Check(f.Canvas.CameraLabel = "1.5x", "Campaign progression must keep the shared zoom setting")
            Next
        End Using
    End Sub

    Private Sub TestClock()
        Dim clock As New GoalDropTransition(True)
        Check(clock.Phase = GoalDropPhase.Drop AndAlso clock.Progress = 0, "drop begins at zero")
        clock.Advance(GoalDropTransition.DropMs)
        Check(clock.Phase = GoalDropPhase.Lift AndAlso clock.Progress = 0, "drop boundary")
        clock.Advance(GoalDropTransition.LiftMs)
        Check(clock.Phase = GoalDropPhase.Landing AndAlso clock.LandingHeight = 3, "landing begins above board")
        clock.Advance(GoalDropTransition.LandingMs)
        Check(clock.Phase = GoalDropPhase.Ready AndAlso clock.LandingHeight = 0, "ready settles at start")
        clock.Advance(GoalDropTransition.ReadyMs)
        Check(clock.Phase = GoalDropPhase.Finished AndAlso clock.ElapsedMs = 2400, "transition timing")
        clock.Advance(99999)
        Check(clock.ElapsedMs = 2400, "large steps clamp to end")
        Dim exitClock As New GoalDropTransition(False)
        exitClock.Advance(1300)
        Check(exitClock.Phase = GoalDropPhase.Finished, "final/practice exit has no landing")
        Dim sample As New GoalDropTransition(True)
        For i As Integer = 1 To 150
            sample.Advance(16)
            Check(sample.Progress >= 0 AndAlso sample.Progress <= 1, "bounded phase progress")
            Check(sample.LandingHeight >= 0 AndAlso sample.LandingHeight <= 3, "bounded landing arc")
        Next
    End Sub

    Private Sub TestCampaign()
        Using f As New Fixture()
            f.Campaign()
            Dim total As Single = 0
            For level As Integer = 1 To 3
                f.Goal()
                Dim oldEngine = f.Shell.Engine
                Dim time = oldEngine.ElapsedSeconds
                total += time
                Check(oldEngine.State = GameState.LevelComplete AndAlso oldEngine.Hearts = 3, "goal is success without pit damage")
                Check(f.Scores.TopRecords(level, 10).Count = 1, "one level record on entry")
                Check(f.Canvas.Transition.HasNextLevel = (level < 3), "correct transition destination")
                f.Tilt.X = 1 : f.Tilt.Y = -1
                f.Shell.HandleKey(Keys.Enter) : f.Shell.HandleKey(Keys.R)
                f.Ticks(20)
                Check(f.Shell.Screen = ShellScreen.GoalTransition AndAlso oldEngine.ElapsedSeconds = time, "input cannot skip or move during transition")
                If level = 1 Then
                    Dim elapsed = f.Canvas.Transition.ElapsedMs
                    f.Shell.HandleKey(Keys.Escape) : f.Ticks(100)
                    Check(f.Shell.Screen = ShellScreen.Paused AndAlso f.Canvas.Transition.ElapsedMs = elapsed, "pause freezes transition")
                    Check(f.Canvas.Invalidations > 0, "pause menu continues to repaint while animation is frozen")
                    f.Shell.HandleKey(Keys.Escape)
                    Check(f.Shell.Screen = ShellScreen.GoalTransition, "resume returns to transition")
                    f.Shell.OnDeactivated() : f.Ticks(100)
                    Check(f.Canvas.Transition.ElapsedMs = elapsed, "focus loss freezes transition")
                    f.Shell.HandleKey(Keys.Escape)
                End If
                f.Tilt.X = 0 : f.Tilt.Y = 0
                If level < 3 Then
                    f.WaitFor(ShellScreen.Playing)
                    Check(Not Object.ReferenceEquals(oldEngine, f.Shell.Engine), "next engine loaded automatically")
                    Check(f.Shell.Engine.ElapsedSeconds = 0 AndAlso f.Shell.Engine.Hearts = 3, "next level starts fresh after landing")
                    Check(f.Shell.Engine.BallX = 1.5F, "marble begins at next start")
                Else
                    f.WaitFor(ShellScreen.Victory)
                End If
                Check(f.Scores.TopRecords(level, 10).Count = 1, "transition does not repeat score recording")
            Next
            Check(f.Scores.TopRecords(0, 10).Count = 1, "campaign recorded once")
            Check(Enumerable.Count(f.Sound.Played, Function(name) name = "goal") = 3, "one success sound per completion")
            Check(Enumerable.Count(f.Sound.Played, Function(name) name = "star") = 9, "stars are revealed once across pauses")
            Check(Enumerable.Count(f.Sound.Played, Function(name) name = "spawn") = 2, "one landing cue per new floor")
            Check(Math.Abs(f.Scores.BestRecord(0).TimeSeconds - total) < 0.001F, "campaign time excludes transitions and pauses")
            f.Ticks(200)
            Check(f.Scores.TopRecords(0, 10).Count = 1, "victory does not record repeatedly")
        End Using
    End Sub

    Private Sub TestDungeonCampaign()
        Using f As New Fixture(True)
            f.Campaign()
            For level As Integer = 1 To 3
                f.Goal()
                Check(f.Canvas.Transition.HasNextLevel, "Level three must lead to the dungeon in a four-level campaign")
                If level = 3 Then
                    f.WaitFor(ShellScreen.DungeonBriefing)
                    Check(f.Shell.Engine.ElapsedSeconds = 0 AndAlso f.Shell.Engine.Dungeon.ElapsedMs = 0, "Campaign landing enters frozen dungeon briefing")
                    f.Shell.HandleKey(Keys.Enter)
                End If
                f.WaitFor(ShellScreen.Playing)
            Next
            Check(f.Shell.Engine.Maze.Dungeon IsNot Nothing AndAlso Object.ReferenceEquals(f.Canvas.DungeonState, f.Shell.Engine.Dungeon), "Fourth level must attach its own dungeon runtime")
            f.Ticks(75)
            Dim clock = f.Shell.Engine.Dungeon.ElapsedMs
            Dim time = f.Shell.Engine.ElapsedSeconds
            f.Shell.HandleKey(Keys.Escape)
            f.Shell.HandleKey(Keys.F9) : f.Shell.HandleKeyUp(Keys.F9)
            f.Ticks(200)
            Check(f.Shell.Screen = ShellScreen.Paused AndAlso f.Shell.Engine.Dungeon.ElapsedMs = clock AndAlso f.Shell.Engine.ElapsedSeconds = time, "Pause and camera changes must freeze traps and physics together")
            f.Shell.HandleKey(Keys.Escape)
            f.Goal(pauseEscape:=True)
            Check(f.Shell.Engine.Hearts > 0 AndAlso f.Shell.Engine.Dungeon.KeyCount = 3, "The real shell must complete the dungeon with both seals and health enabled")
            Check(Not f.Canvas.Transition.HasNextLevel, "Dungeon is the final floor")
            f.WaitFor(ShellScreen.Victory)
            Check(f.Scores.TopRecords(4, 10).Count = 1, "Dungeon completion records its own level time")
            Check(f.Scores.TopRecords(0, 10, 4).Count = 1 AndAlso f.Scores.TopRecords(0, 10, 3).Count = 0, "Victory records a four-level campaign")
            Check(f.Canvas.TiltViewEnabled AndAlso f.Canvas.CameraPreferredZoom = 1, "Victory restores captured pre-dungeon view and zoom")
            Check(f.Sound.Played.Contains("dungeon_seal"), "Seal pickup must play its cue through the real shell")
        End Using
    End Sub

    Private Sub TestPractice()
        Using f As New Fixture()
            f.Menu(1) : f.Shell.HandleKey(Keys.Enter)
            f.Shell.HandleKey(Keys.Enter) : f.WaitFor(ShellScreen.Playing)
            f.Goal() : f.WaitFor(ShellScreen.LevelComplete)
            Dim engine = f.Shell.Engine
            f.Ticks(200)
            Check(f.Shell.Screen = ShellScreen.LevelComplete AndAlso Object.ReferenceEquals(engine, f.Shell.Engine), "practice waits at results")
            Check(f.Scores.TopRecords(0, 10).Count = 0, "practice is not a campaign")
            f.Shell.HandleKey(Keys.Enter)
            Check(f.Shell.Screen = ShellScreen.Intro, "practice next level remains explicit")
        End Using
    End Sub

    Private Sub TestRetry()
        Using f As New Fixture()
            f.Campaign() : f.Goal()
            f.Shell.HandleKey(Keys.Escape) : f.Shell.HandleKey(Keys.R)
            Check(f.Shell.Screen = ShellScreen.Intro AndAlso f.Canvas.Transition Is Nothing, "retry clears pending transition")
            Check(f.Shell.Engine.Attempts = 2, "retry counts an attempt")
            f.WaitFor(ShellScreen.Playing)
            Dim total As Single = 0
            For level As Integer = 1 To 3
                f.Goal() : total += f.Shell.Engine.ElapsedSeconds
                f.WaitFor(If(level = 3, ShellScreen.Victory, ShellScreen.Playing))
            Next
            Check(f.Scores.TopRecords(1, 10).Count = 2, "both completed attempts remain in history")
            Check(Math.Abs(f.Scores.BestRecord(0).TimeSeconds - total) < 0.001F, "retry replaces campaign contribution")
            Check(f.Scores.BestRecord(0).Stars = 9, "retry does not duplicate campaign stars")
        End Using
    End Sub

    Private Sub TestCancel()
        Using f As New Fixture()
            f.Campaign() : f.Goal()
            f.Shell.HandleKey(Keys.Escape) : f.Menu(2) : f.Shell.HandleKey(Keys.Enter)
            f.Ticks(200)
            Check(f.Shell.Screen = ShellScreen.Title AndAlso f.Canvas.Transition Is Nothing, "main menu cancels pending advancement")
            Check(f.Scores.TopRecords(0, 10).Count = 0, "abandoned campaign is not recorded")
        End Using
    End Sub

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

    Private Sub TestIntroPause()
        Using f As New Fixture()
            f.Shell.HandleKey(Keys.Enter) : f.Ticks(50)
            f.Shell.HandleKey(Keys.Escape) : f.Ticks(200)
            f.Shell.HandleKey(Keys.Escape) : f.Ticks(50)
            Check(f.Shell.Screen = ShellScreen.Intro AndAlso f.Shell.Engine.ElapsedSeconds = 0, "intro pause must resume the countdown")
            f.WaitFor(ShellScreen.Playing)
            Check(f.Shell.Engine.ElapsedSeconds = 0, "countdown ends before physics resumes")
        End Using
    End Sub
End Module
