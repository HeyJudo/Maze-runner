Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Reflection
Imports System.Windows.Forms
Imports GravityMaze.Audio
Imports GravityMaze.Data
Imports GravityMaze.Engine
Imports GravityMaze.Input
Imports GravityMaze.Levels
Imports GravityMaze.UI
Imports GravityMaze.UI.Screens

Namespace VerificationRunner
    ' Authored state captures complement the campaign's actual pilot traversal.
    ' All overlays, menus and camera sequences come from the production shell.
    Public Module DungeonUiTour
        Private NotInheritable Class TourTilt
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

        Public Sub Run(projectRoot As String, outDir As String)
            Directory.CreateDirectory(outDir)
            Dim minimumClient As Size
            Using window As New Form With {.Size = New Size(960, 600)}
                minimumClient = window.ClientSize
            End Using
            For Each size In {minimumClient, New Size(1280, 720), New Size(1920, 1080)}
                Dim scoresPath = Path.Combine(Path.GetTempPath(), $"gm_dungeon_ui_{Guid.NewGuid():N}.xml")
                Dim tilt As New TourTilt()
                Using canvas As New GameCanvas With {.Size = size},
                      sound As New SoundManager(Path.Combine(outDir, "no-sounds"))
                    Dim levels As New List(Of LevelConfig) From {
                        New LevelConfig(1, "Wooden Workshop", Path.Combine(projectRoot, "GravityMaze", "Mazes", "Level1.txt"), 0, 7.3F),
                        New LevelConfig(2, "Frozen Labyrinth", Path.Combine(projectRoot, "GravityMaze", "Mazes", "Level2.txt"), 0, 36.9F),
                        New LevelConfig(3, "Neon Velocity", Path.Combine(projectRoot, "GravityMaze", "Mazes", "Level3.txt"), 57, 40.2F),
                        New LevelConfig(4, "Forgotten Keep", Path.Combine(projectRoot, "GravityMaze", "Mazes", "Level4.txt"), 0, 180)}
                    Dim shell As New GameShell(canvas, New InputManager(tilt), sound, New ScoreManager(scoresPath), levels)
                    Tick(shell, 20)
                    SelectMenu(shell, tilt, 1) : shell.HandleKey(Keys.Enter)
                    SelectMenu(shell, tilt, 3) : shell.HandleKey(Keys.Enter)
                    Expect(shell, ShellScreen.DungeonBriefing)
                    Tick(shell, 40)
                    If canvas.TiltViewEnabled OrElse canvas.CameraZoom <> 1 Then Throw New Exception("Dungeon briefing must show flat overview")
                    Shot(canvas, outDir, size, "01_briefing")
                    SelectMenu(shell, tilt, 2) : shell.HandleKey(Keys.Enter) : Tick(shell, 20)
                    Expect(shell, ShellScreen.DungeonRules)
                    Shot(canvas, outDir, size, "02_rules")
                    shell.HandleKey(Keys.Escape)
                    SelectMenu(shell, tilt, 0)
                    tilt.X = 1 : Tick(shell, 57) : tilt.X = 0
                    Expect(shell, ShellScreen.Intro)
                    Tick(shell, 40)
                    Shot(canvas, outDir, size, "03_countdown")
                    WaitFor(shell, ShellScreen.Playing)
                    If shell.Engine.ElapsedSeconds <> 0 Then Throw New Exception("Ready/countdown must freeze the run clock")
                    Shot(canvas, outDir, size, "04_objective_no_keys")

                    Collect(shell, "K"c) : Tick(shell, 1)
                    Shot(canvas, outDir, size, "05_objective_find_armory")
                    Collect(shell, "Q"c) : Tick(shell, 41)
                    Expect(shell, ShellScreen.DungeonReveal)
                    If canvas.CameraZoom <> 2 Then Throw New Exception("Door reveal capture must be settled at 2x")
                    Shot(canvas, outDir, size, "06_vault_door_reveal")
                    WaitFor(shell, ShellScreen.Playing)
                    Shot(canvas, outDir, size, "07_objective_vault_warning")
                    shell.HandleKey(Keys.Escape) : Tick(shell, 20)
                    Shot(canvas, outDir, size, "08_paused_vault_warning")
                    shell.HandleKey(Keys.Escape)

                    Dim checkpoint = shell.Engine.Dungeon.Definition.Checkpoints.Find(Function(cp) Not cp.Escape)
                    PositionBall(shell.Engine, checkpoint.Column + 0.5F, checkpoint.Row + 0.5F)
                    Tick(shell, 1)
                    Shot(canvas, outDir, size, "09_checkpoint_guidance")
                    Collect(shell, "V"c)
                    Tick(shell, 1)
                    Tick(shell, 124)
                    Shot(canvas, outDir, size, "10_rumble_head_left")
                    shell.HandleKey(Keys.Escape) : Tick(shell, 20)
                    Shot(canvas, outDir, size, "11_paused_escape_guidance")

                    ' Capture the other first-key objective from a fresh run without adding progression state.
                    shell.HandleKey(Keys.Escape) : shell.HandleKey(Keys.R)
                    WaitFor(shell, ShellScreen.Playing)
                    Collect(shell, "Q"c) : Tick(shell, 1)
                    Shot(canvas, outDir, size, "12_objective_find_crypt")
                End Using
                Try
                    File.Delete(scoresPath)
                Catch
                End Try
            Next
            Console.WriteLine($"PASS: 36 Level 4 UI captures at minimum client {minimumClient.Width}x{minimumClient.Height}, 1280x720 and 1920x1080; board Ready, rules, objectives, reveal, warning, checkpoints and escape guidance")
        End Sub

        Private Sub Collect(shell As GameShell, key As Char)
            Dim maze = shell.Engine.Maze
            For row As Integer = 0 To maze.RowCount - 1
                For column As Integer = 0 To maze.ColumnCount - 1
                    If maze.GetTile(row, column) <> key Then Continue For
                    PositionBall(shell.Engine, column + 0.5F, row + 0.5F)
                    shell.Engine.Dungeon.Collect(column + 0.5F, row + 0.5F)
                    Return
                Next
            Next
            Throw New Exception($"Missing dungeon key {key}")
        End Sub

        Private Sub PositionBall(engine As GameEngine, x As Single, y As Single)
            ' Test fixture only: captures authored locations; the main tour proves real movement.
            Dim flags = BindingFlags.Instance Or BindingFlags.NonPublic
            GetType(GameEngine).GetField("_ballX", flags).SetValue(engine, x)
            GetType(GameEngine).GetField("_ballY", flags).SetValue(engine, y)
        End Sub

        Private Sub SelectMenu(shell As GameShell, tilt As TourTilt, index As Integer)
            For guard As Integer = 1 To 12
                tilt.X = 0 : tilt.Y = 0 : Tick(shell, 3)
                If shell.SelectedIndex = index Then Return
                tilt.Y = 1 : Tick(shell, 1)
            Next
            Throw New Exception($"Could not select dungeon UI menu item {index}")
        End Sub

        Private Sub WaitFor(shell As GameShell, screen As ShellScreen)
            For guard As Integer = 1 To 200
                If shell.Screen = screen Then Return
                Tick(shell, 1)
            Next
            Expect(shell, screen)
        End Sub

        Private Sub Tick(shell As GameShell, count As Integer)
            For i As Integer = 1 To count : shell.Tick() : Next
        End Sub

        Private Sub Expect(shell As GameShell, screen As ShellScreen)
            If shell.Screen <> screen Then Throw New Exception($"Dungeon UI expected {screen}, got {shell.Screen}")
        End Sub

        Private Sub Shot(canvas As GameCanvas, outDir As String, size As Size, name As String)
            Using bitmap As New Bitmap(size.Width, size.Height)
                canvas.DrawToBitmap(bitmap, New Rectangle(Point.Empty, size))
                bitmap.Save(Path.Combine(outDir, $"{size.Width}x{size.Height}_{name}.png"), ImageFormat.Png)
            End Using
        End Sub
    End Module
End Namespace
