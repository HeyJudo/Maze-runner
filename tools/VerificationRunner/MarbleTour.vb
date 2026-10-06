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
    Public Module MarbleTour
        Public Sub Run(outDir As String)
            Directory.CreateDirectory(outDir)
            Dim maze As New MazeDefinition({"1111111", "1S00001", "1000001", "1000001", "1000001", "10000G1", "1111111"})
            For Each size In {New Size(960, 600), New Size(1600, 900)}
                For Each theme In {"Wooden Workshop", "Frozen Labyrinth", "Neon Velocity"}
                    For Each projected In {False, True}
                        Dim label = $"{theme}-{size.Width}-{If(projected, "tilt", "flat")}"
                        Using canvas As New GameCanvas With {.Size = size, .TiltViewEnabled = projected}
                            canvas.ShowMaze(maze, theme)
                            canvas.UpdateBallPosition(3.5F, 3.5F)
                            canvas.UpdateBoardTilt(0.7F, -0.8F, 500)
                            Using initial = Capture(canvas)
                                canvas.UpdateBallPosition(3.7F, 3.5F)
                                ' Return to the same centre without undoing the roll: a square path
                                ' has a different net orientation, unlike reversing a straight path.
                                canvas.UpdateBallPosition(3.7F, 3.7F)
                                canvas.UpdateBallPosition(3.5F, 3.7F)
                                canvas.UpdateBallPosition(3.5F, 3.5F)
                                Using rolled = Capture(canvas)
                                    Check(Differences(initial, rolled) > 10, "Closed rolling path must change visible surface texture")
                                    canvas.UpdateBallPosition(3.5F, 3.5F)
                                    Using stopped = Capture(canvas)
                                        Check(Differences(rolled, stopped) = 0, "Stationary marble must render a stable surface")
                                    End Using
                                    rolled.Save(Path.Combine(outDir, label & "-rolled.png"), ImageFormat.Png)
                                End Using
                                initial.Save(Path.Combine(outDir, label & "-initial.png"), ImageFormat.Png)
                            End Using
                            canvas.ClearEffects()
                            canvas.UpdateBallPosition(maze.StartColumn + 0.5F, maze.StartRow + 0.5F)
                            Using resetImage = Capture(canvas), fresh As New GameCanvas With {.Size = size, .TiltViewEnabled = projected}
                                fresh.ShowMaze(maze, theme)
                                Using expected = Capture(fresh)
                                    Check(Differences(resetImage, expected) = 0, "Retry must reset the texture without rolling across the maze")
                                End Using
                            End Using
                            Dim clock As New GoalDropTransition(True)
                            canvas.BeginGoalDrop(clock, maze, theme)
                            clock.Advance(GoalDropTransition.ExitMs + 100)
                            Using airborne = Capture(canvas)
                                airborne.Save(Path.Combine(outDir, label & "-landing.png"), ImageFormat.Png)
                            End Using
                        End Using
                    Next
                Next
            Next

            ' Measure the actual GDI compositing path, isolating the sphere on transparency.
            Using renderer As New MazeRenderer(), image As New Bitmap(960, 600)
                Dim pose As New BoardTilt()
                pose.Advance(1, -1, 600)
                Using g = Graphics.FromImage(image)
                    Dim area As New Rectangle(0, 0, image.Width, image.Height)
                    Using scratch As New Bitmap(image.Width, image.Height), board = Graphics.FromImage(scratch)
                        renderer.Draw(board, area, maze, 3.5F, 3.5F, deferMarbles:=True)
                    End Using
                    renderer.DrawMarbles(g, area, New BoardProjection(image.Width, image.Height, pose.X, pose.Y))
                End Using
                Dim left = image.Width, top = image.Height, right As Integer = 0, bottom As Integer = 0
                For y As Integer = 0 To image.Height - 1
                    For x As Integer = 0 To image.Width - 1
                        If image.GetPixel(x, y).A < 128 Then Continue For
                        left = Math.Min(left, x) : right = Math.Max(right, x)
                        top = Math.Min(top, y) : bottom = Math.Max(bottom, y)
                    Next
                Next
                Check(right > left AndAlso bottom > top, "Projected sphere must render")
                Check(Math.Abs((right - left) - (bottom - top)) <= 2, "Projected sphere must have a round silhouette")
                image.Save(Path.Combine(outDir, "projected-sphere.png"), ImageFormat.Png)
            End Using
            Console.WriteLine("PASS: marble texture changes, stationary surface, round projected silhouette; theme/tilt/landing snapshots saved")
        End Sub

        Private Function Capture(canvas As GameCanvas) As Bitmap
            Dim image As New Bitmap(canvas.Width, canvas.Height)
            canvas.DrawToBitmap(image, New Rectangle(Point.Empty, canvas.Size))
            Return image
        End Function

        Private Function Differences(a As Bitmap, b As Bitmap) As Integer
            Dim count As Integer
            For y As Integer = 0 To a.Height - 1
                For x As Integer = 0 To a.Width - 1
                    Dim ca = a.GetPixel(x, y), cb = b.GetPixel(x, y)
                    If Math.Abs(CInt(ca.R) - cb.R) + Math.Abs(CInt(ca.G) - cb.G) + Math.Abs(CInt(ca.B) - cb.B) > 3 Then count += 1
                Next
            Next
            Return count
        End Function

        Private Sub Check(condition As Boolean, message As String)
            If Not condition Then Throw New Exception(message)
        End Sub
    End Module
End Namespace
