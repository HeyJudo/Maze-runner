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
        Public Sub New()
            Directory.CreateDirectory(Dir)
            Dim levels As New List(Of LevelConfig)()
            For i As Integer = 1 To 3
                Dim file As String = Path.Combine(Dir, $"level{i}.txt")
                IO.File.WriteAllLines(file, {"111111", "1S00G1", "111111"})
                levels.Add(New LevelConfig(i, {"Wooden Workshop", "Frozen Labyrinth", "Neon Velocity"}(i - 1), file, If(i = 3, 57, 0), 5))
            Next
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
        Public Sub Goal()
            Dim pilot As New AttractPilot(Shell.Engine.Maze)
            For i As Integer = 1 To 500
                If Shell.Screen = ShellScreen.GoalTransition Then
                    Tilt.X = 0 : Tilt.Y = 0
                    Return
                End If
                Dim input = pilot.NextTilt(Shell.Engine)
                Tilt.X = input.X : Tilt.Y = input.Y
                Shell.Tick()
            Next
            Check(False, "Pilot did not reach the transition")
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
        TestCameraPreferences()
        TestClock()
        TestCampaign()
        TestPractice()
        TestRetry()
        TestCancel()
        TestIntroPause()
        Console.WriteLine($"PASS: {_checks} transition and real-shell flow checks (headless presentation adapters)")
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
                    If level = 2 Then Check(f.Shell.Engine.TimeRemainingSeconds = 57, "timed level keeps its full countdown during landing")
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
