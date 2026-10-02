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
    Public Module PerspectiveTour
        Public Sub Run(outDir As String)
            Directory.CreateDirectory(outDir)
            Dim maze As New MazeDefinition({"1111111", "1S00001", "1010101", "1000001", "1010101", "10000G1", "1111111"})
            For Each size In {New Size(960, 600), New Size(1600, 900)}
                For Each theme In {"Wooden Workshop", "Frozen Labyrinth", "Neon Velocity"}
                    Using canvas As New GameCanvas With {.Size = size}
                        canvas.ShowMaze(maze, theme)
                        canvas.OverlayPainter = Sub(g, area) g.FillRectangle(Brushes.Magenta, 0, 0, 24, 24)
                        Using neutral = Capture(canvas)
                            canvas.UpdateBoardTilt(1, -1, 600)
                            Using tilted = Capture(canvas)
                                Check(tilted.GetPixel(10, 10).ToArgb() = Color.Magenta.ToArgb(), "HUD must remain unprojected")
                                Dim different As Integer
                                For y As Integer = 40 To size.Height - 40 Step 8
                                    For x As Integer = 40 To size.Width - 40 Step 8
                                        If neutral.GetPixel(x, y) <> tilted.GetPixel(x, y) Then different += 1
                                    Next
                                Next
                                Check(different > 100, "Input must visibly change the perspective")
                                tilted.Save(Path.Combine(outDir, $"{theme}-{size.Width}-tilted.png"), ImageFormat.Png)
                            End Using
                            neutral.Save(Path.Combine(outDir, $"{theme}-{size.Width}-rest.png"), ImageFormat.Png)
                        End Using
                        canvas.TiltViewEnabled = False
                        Using flat = Capture(canvas), expected As New Bitmap(size.Width, size.Height), renderer As New MazeRenderer()
                            Using g = Graphics.FromImage(expected)
                                g.Clear(canvas.BackColor)
                                renderer.Draw(g, New Rectangle(Point.Empty, size), maze,
                                              maze.StartColumn + 0.5F, maze.StartRow + 0.5F, theme)
                                g.FillRectangle(Brushes.Magenta, 0, 0, 24, 24)
                            End Using
                            For y As Integer = 0 To size.Height - 1 Step 7
                                For x As Integer = 0 To size.Width - 1 Step 7
                                    Check(flat.GetPixel(x, y) = expected.GetPixel(x, y), "Disabled tilt must restore the original rendering")
                                Next
                            Next
                            flat.Save(Path.Combine(outDir, $"{theme}-{size.Width}-flat.png"), ImageFormat.Png)
                        End Using
                    End Using
                Next
            Next
            Console.WriteLine("PASS: perspective images, fixed HUD, and original flat rendering across all themes and two window sizes")
        End Sub

        Private Function Capture(canvas As GameCanvas) As Bitmap
            Dim result As New Bitmap(canvas.Width, canvas.Height)
            canvas.DrawToBitmap(result, New Rectangle(0, 0, result.Width, result.Height))
            Return result
        End Function

        Private Sub Check(condition As Boolean, message As String)
            If Not condition Then Throw New Exception(message)
        End Sub
    End Module
End Namespace
