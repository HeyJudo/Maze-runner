Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports GravityMaze.Levels
Imports GravityMaze.Rendering
Imports GravityMaze.UI

Namespace VerificationRunner
    Public Module GoalTransitionTour
        Public Sub Run(outDir As String)
            Directory.CreateDirectory(outDir)
            Dim first = MazeManager.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Mazes", "Level1.txt"))
            Dim second = MazeManager.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Mazes", "Level2.txt"))
            For Each size In {New Size(960, 600), New Size(1600, 900)}
                For Each projected In {False, True}
                    Using canvas As New GameCanvas With {.Size = size, .TiltViewEnabled = projected,
                                                        .BoardInsets = New System.Windows.Forms.Padding(24, 100, 24, 60)}
                        canvas.ShowMaze(first)
                        canvas.UpdateBoardTilt(0.8F, -0.7F, 600)
                        canvas.UpdateBallPosition(first.GoalColumn + 0.3F, first.GoalRow + 0.5F)
                        canvas.TriggerGoalCelebration(first.GoalColumn + 0.5F, first.GoalRow + 0.5F)
                        canvas.OverlayPainter = Sub(g, area) g.FillRectangle(Brushes.Magenta, 0, 0, 24, 24)
                        Dim clock As New GoalDropTransition(True)
                        canvas.BeginGoalDrop(clock, second, "Frozen Labyrinth")
                        If Not canvas.IsBallHidden Then Throw New Exception("Goal animation must replace the engine marble")
                        For Each at As Single In {0, 320, 960, 1440, 1600, 2000}
                            While clock.ElapsedMs < at
                                clock.Advance(Math.Min(16, at - clock.ElapsedMs))
                                canvas.UpdateBallPosition(first.GoalColumn + 0.3F, first.GoalRow + 0.5F)
                            End While
                            Using image As New Bitmap(size.Width, size.Height)
                                canvas.DrawToBitmap(image, New Rectangle(Point.Empty, size))
                                If image.GetPixel(10, 10).ToArgb() <> Color.Magenta.ToArgb() Then Throw New Exception("Transition transforms must not move the HUD")
                                image.Save(Path.Combine(outDir, $"{size.Width}-{If(projected, "tilt", "flat")}-{clock.Phase}-{at:0}.png"), ImageFormat.Png)
                            End Using
                        Next
                        canvas.ShowMaze(second, "Frozen Labyrinth")
                        If canvas.IsBallHidden Then Throw New Exception("Next level must restore the normal marble")
                    End Using
                Next
            Next
            Console.WriteLine("PASS: transition snapshots in flat/tilt views and two window sizes; fixed overlay and next-level marble verified")
        End Sub
    End Module
End Namespace
