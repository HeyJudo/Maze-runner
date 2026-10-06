Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports GravityMaze.Engine
Imports GravityMaze.Levels
Imports GravityMaze.UI

Namespace VerificationRunner
    ' Presentation fixtures only. DungeonChecks proves clears using real tilt physics.
    Public Module DungeonTour
        Public Sub Run(outDir As String)
            Directory.CreateDirectory(outDir)
            Dim maze = MazeManager.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Mazes", "Level4.txt"))
            For Each size In {New Size(960, 600), New Size(1600, 900)}
                For Each tilted In {False, True}
                    For state As Integer = 0 To 3
                        Dim run As New DungeonRun(maze)
                        Dim at = {0, 816, 1600, 2400}(state)
                        For tick As Integer = 1 To at \ 16
                            run.Advance(maze.StartColumn + 0.5F, maze.StartRow + 0.5F, maze.StartColumn + 0.5F, maze.StartRow + 0.5F)
                        Next
                        If state > 0 Then CollectSeal(maze, run, "K"c)
                        If state > 1 Then CollectSeal(maze, run, "Q"c)
                        Using canvas As New GameCanvas With {.Size = size, .CameraEnabled = True, .TiltViewEnabled = tilted,
                                                            .BoardInsets = New System.Windows.Forms.Padding(24, 120, 24, 60)}
                            canvas.ShowMaze(maze, "Forgotten Keep")
                            canvas.DungeonState = run
                            canvas.UpdateBoardTilt(0.7F, -0.8F, 600)
                            canvas.OverlayPainter = Sub(g, bounds) g.FillRectangle(Brushes.Magenta, 0, 0, 24, 24)
                            ' Focus the final gauntlet so warning/spike/dart states remain legible at zoom.
                            canvas.UpdateBallPosition(23.5F, 8.5F)
                            For zoom As Integer = 0 To 2
                                For tick As Integer = 1 To 120 : canvas.UpdateCamera(16) : Next
                                Using image As New Bitmap(size.Width, size.Height)
                                    canvas.DrawToBitmap(image, New Rectangle(Point.Empty, size))
                                    If image.GetPixel(10, 10).ToArgb() <> Color.Magenta.ToArgb() Then Throw New Exception("Dungeon camera must leave HUD fixed")
                                    image.Save(Path.Combine(outDir, $"{size.Width}-{If(tilted, "tilt", "flat")}-state{state}-zoom{zoom}.png"), ImageFormat.Png)
                                End Using
                                canvas.CycleCameraZoom()
                            Next
                        End Using
                    Next
                Next
            Next
            Console.WriteLine("PASS: dungeon rendering at four trap/seal states, three zooms, two sizes, and flat/tilt views; 48 snapshots saved")
        End Sub

        Private Sub CollectSeal(maze As MazeDefinition, run As DungeonRun, symbol As Char)
            For row As Integer = 0 To maze.RowCount - 1
                For column As Integer = 0 To maze.ColumnCount - 1
                    If maze.GetTile(row, column) = symbol Then run.Collect(column + 0.5F, row + 0.5F)
                Next
            Next
        End Sub
    End Module
End Namespace
