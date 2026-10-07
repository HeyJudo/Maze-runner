Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports GravityMaze.Levels
Imports GravityMaze.UI

Namespace VerificationRunner
    Public Module CameraTour
        Public Sub Run(outDir As String)
            Directory.CreateDirectory(outDir)
            For Each size In {New Size(960, 600), New Size(1600, 900)}
                For level As Integer = 1 To 4
                    Dim maze = MazeManager.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Mazes", $"Level{level}.txt"))
                    Dim theme = {"Wooden Workshop", "Frozen Labyrinth", "Neon Velocity", "Forgotten Keep"}(level - 1)
                    For Each tilted In {False, True}
                        Using canvas As New GameCanvas With {.Size = size, .CameraEnabled = True, .TiltViewEnabled = tilted,
                                                             .BoardInsets = New System.Windows.Forms.Padding(24, 100, 24, 60)}
                            canvas.ShowMaze(maze, theme)
                            If maze.Dungeon IsNot Nothing Then canvas.DungeonState = New GravityMaze.Engine.DungeonRun(maze)
                            canvas.PickupTaken = Function(row, col) True ' Keep pickup bobbing out of camera image comparisons.
                            canvas.UpdateBoardTilt(0.7F, -0.8F, 600)
                            canvas.OverlayPainter = Sub(g, bounds) g.FillRectangle(Brushes.Magenta, 0, 0, 24, 24)
                            Using full = Capture(canvas)
                                Dim label = $"level{level}-{size.Width}-{If(tilted, "tilt", "flat")}"
                                full.Save(Path.Combine(outDir, label & "-full.png"), ImageFormat.Png)
                                For zoom As Integer = 1 To 2
                                    canvas.CycleCameraZoom()
                                    For i As Integer = 1 To 120 : canvas.UpdateCamera(16) : Next
                                    Using close = Capture(canvas)
                                        Check(Differences(full, close) > 500, "Closer view must visibly magnify the maze")
                                        Check(close.GetPixel(10, 10).ToArgb() = Color.Magenta.ToArgb(), "Zoom must leave HUD coordinates fixed")
                                        Check(close.GetPixel(size.Width \ 2, 70) = canvas.BackColor, "Zoomed board must remain inside its viewport")
                                        close.Save(Path.Combine(outDir, label & $"-zoom{zoom}.png"), ImageFormat.Png)
                                    End Using
                                Next
                                canvas.CameraOverviewHeld = True
                                Using overview = Capture(canvas)
                                    Check(Differences(full, overview) = 0, "Holding overview must restore the full maze immediately")
                                End Using
                                canvas.CameraOverviewHeld = False
                                canvas.UpdateBallPosition(maze.GoalColumn + 0.5F, maze.GoalRow + 0.5F)
                                For i As Integer = 1 To 180 : canvas.UpdateCamera(16) : Next
                                Using goal = Capture(canvas)
                                    Check(goal.GetPixel(10, 10).ToArgb() = Color.Magenta.ToArgb(), "Panning must leave HUD coordinates fixed")
                                    goal.Save(Path.Combine(outDir, label & "-goal.png"), ImageFormat.Png)
                                End Using
                                canvas.ShowMaze(maze, theme)
                                Check(canvas.CameraLabel = "2x", "Loading a maze must preserve selected zoom")
                            End Using
                        End Using
                    Next
                Next
            Next
            Console.WriteLine("PASS: camera magnification, fixed HUD, viewport clipping, immediate overview, and preference retention; all-level snapshots saved")
        End Sub

        Private Function Capture(canvas As GameCanvas) As Bitmap
            Dim result As New Bitmap(canvas.Width, canvas.Height)
            canvas.DrawToBitmap(result, New Rectangle(Point.Empty, canvas.Size))
            Return result
        End Function

        Private Function Differences(a As Bitmap, b As Bitmap) As Integer
            Dim count As Integer
            For y As Integer = 0 To a.Height - 1 Step 3
                For x As Integer = 0 To a.Width - 1 Step 3
                    If a.GetPixel(x, y) <> b.GetPixel(x, y) Then count += 1
                Next
            Next
            Return count
        End Function

        Private Sub Check(condition As Boolean, message As String)
            If Not condition Then Throw New Exception(message)
        End Sub
    End Module
End Namespace
