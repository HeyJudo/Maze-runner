Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Windows.Forms
Imports GravityMaze.Audio
Imports GravityMaze.Data
Imports GravityMaze.Engine
Imports GravityMaze.Input
Imports GravityMaze.Levels
Imports GravityMaze.UI
Imports GravityMaze.UI.Screens

Namespace VerificationRunner
    ' Plays the real game shell end to end (menus → campaign → victory, pause, time-up) with a
    ' scripted tilt source and the demo pilot, asserting the screen flow and saving a screenshot of each screen.
    Public Module UiTour
        Private NotInheritable Class FakeTilt
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

        Private _tilt As FakeTilt
        Private _shell As GameShell
        Private _canvas As GameCanvas
        Private _outDir As String

        Public Sub Run(projectRoot As String, outDir As String)
            Console.WriteLine("[TEST UI] Touring every screen of the game shell...")
            _outDir = outDir
            Directory.CreateDirectory(outDir)
            Dim gameDir As String = Path.Combine(projectRoot, "GravityMaze")
            Dim levels As New List(Of LevelConfig) From {
                New LevelConfig(1, "Wooden Workshop", Path.Combine(gameDir, "Mazes\Level1.txt"), 0, 5.4F),
                New LevelConfig(2, "Frozen Labyrinth", Path.Combine(gameDir, "Mazes\Level2.txt"), 0, 25.8F),
                New LevelConfig(3, "Neon Velocity", Path.Combine(gameDir, "Mazes\Level3.txt"), 40, 28.5F)}
            Dim scoresPath As String = Path.Combine(Path.GetTempPath(), $"gm_tour_{Guid.NewGuid():N}.xml")

            _tilt = New FakeTilt()
            _canvas = New GameCanvas With {.Size = New Size(1600, 900)}
            _shell = New GameShell(_canvas, New InputManager(_tilt), New SoundManager(Path.Combine(outDir, "no-sounds")),
                                   New ScoreManager(scoresPath), levels)

            Ticks(80)
            Expect(ShellScreen.Title)
            Shot("ui_01_title")

            ' Level select (menu index 1) – preview + cards
            MoveTo(1) : Key(Keys.Enter) : Ticks(40)
            Expect(ShellScreen.LevelSelect)
            Shot("ui_02_level_select")
            Key(Keys.Escape) : Ticks(10)
            Expect(ShellScreen.Title)

            MoveTo(3) : Key(Keys.Enter) : Ticks(30)
            Expect(ShellScreen.HowToPlay)
            Shot("ui_03_how_to_play")
            Key(Keys.Escape) : Ticks(10)

            ' Play with no name yet → name entry first
            MoveTo(0) : Key(Keys.Enter) : Ticks(20)
            Expect(ShellScreen.NameEntry)
            For i As Integer = 1 To 12 : Key(Keys.Back) : Next
            For Each ch As Char In "JUDE" : _shell.HandleChar(ch) : Next
            Ticks(10)
            Shot("ui_04_name_entry")
            Key(Keys.Enter) : Ticks(20)
            Expect(ShellScreen.Intro)
            Shot("ui_05_intro_card")
            Ticks(CInt((1600 + 700) / 16))
            Shot("ui_06_countdown")
            Ticks(CInt(1400 / 16))
            Expect(ShellScreen.Playing)

            ' Campaign: bot plays all three levels.
            For level As Integer = 1 To 3
                Dim pilot As New AttractPilot(_shell.Engine.Maze)
                Dim n As Integer = 0
                While _shell.Screen = ShellScreen.Playing AndAlso n < 6000
                    Dim t As PointF = pilot.NextTilt(_shell.Engine)
                    _tilt.X = t.X : _tilt.Y = t.Y
                    _shell.Tick()
                    n += 1
                    If level = 2 AndAlso n = 400 Then
                        Shot("ui_07_hud_playing")
                        _tilt.X = 0 : _tilt.Y = 0
                        Key(Keys.Escape) : Ticks(15)
                        Expect(ShellScreen.Paused)
                        Shot("ui_08_paused")
                        Key(Keys.Escape) : Ticks(1)
                        Expect(ShellScreen.Playing)
                    End If
                End While
                _tilt.X = 0 : _tilt.Y = 0
                Expect(ShellScreen.LevelComplete)
                Console.WriteLine($"  -> Level {level} cleared by the pilot in {_shell.Engine.ElapsedSeconds:F1}s.")
                Ticks(110)
                If level = 1 Then Shot("ui_09_level_complete")
                Key(Keys.Enter) : Ticks(5)
                If level < 3 Then
                    Expect(ShellScreen.Intro)
                    Ticks(CInt((1600 + 2000) / 16))
                    Expect(ShellScreen.Playing)
                End If
            Next
            Expect(ShellScreen.Victory)
            Ticks(130)
            Shot("ui_10_victory")

            Key(Keys.Enter) : Ticks(40)   ' VIEW RECORDS
            Expect(ShellScreen.Records)
            Shot("ui_11_records")
            Key(Keys.Escape) : Ticks(10)
            Expect(ShellScreen.Title)

            ' Time up on Level 3: idle until the clock runs out.
            MoveTo(1) : Key(Keys.Enter) : Ticks(10)
            MoveTo(2) : Key(Keys.Enter)
            Ticks(CInt((1600 + 2000) / 16) + 10)
            Expect(ShellScreen.Playing)
            Shot("ui_12_hud_level3")
            Ticks(CInt(40000 / 16) + 60)
            Expect(ShellScreen.TimeUp)
            Shot("ui_13_time_up")

            Try : File.Delete(scoresPath) : Catch : End Try
            Console.WriteLine("  -> Menus, intro, countdown, play, pause, results, victory, records, time-up all reached.")
        End Sub

        Private Sub Ticks(n As Integer)
            For i As Integer = 1 To n
                _shell.Tick()
            Next
        End Sub

        Private Sub Key(k As Keys)
            _shell.HandleKey(k)
        End Sub

        ' Menu navigation goes through tilt, exactly like the Arduino: neutral, then a tilt pulse per step.
        Private Sub MoveTo(index As Integer)
            For guard As Integer = 1 To 12
                _tilt.X = 0 : _tilt.Y = 0 : Ticks(3)
                If CurrentMenuSelected() = index Then Return
                _tilt.Y = 1 : Ticks(1)
            Next
            _tilt.Y = 0
            Throw New Exception($"Could not navigate to menu index {index}")
        End Sub

        Private Function CurrentMenuSelected() As Integer
            Return _shell.SelectedIndex
        End Function

        Private Sub Expect(screen As ShellScreen)
            If _shell.Screen <> screen Then Throw New Exception($"UI tour expected {screen} but was {_shell.Screen}")
        End Sub

        Private Sub Shot(name As String)
            Using bmp As New Bitmap(_canvas.Width, _canvas.Height)
                _canvas.DrawToBitmap(bmp, New Rectangle(0, 0, bmp.Width, bmp.Height))
                bmp.Save(Path.Combine(_outDir, name & ".png"), ImageFormat.Png)
            End Using
        End Sub
    End Module
End Namespace
