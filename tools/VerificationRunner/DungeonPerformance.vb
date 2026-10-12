Option Strict On
Option Explicit On

Imports System
Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports GravityMaze.Engine
Imports GravityMaze.Levels
Imports GravityMaze.Rendering

Namespace VerificationRunner
    ' Run serially on an idle machine after integration. Timing traces stay useful even above either target.
    Public Module DungeonPerformance
        Private Const WarmupFrames As Integer = 60
        Private Const SampleFrames As Integer = 180

        Public Sub Run()
            Dim maze = DungeonTour.LoadMaze()
            Dim size As New Size(1600, 900)
            Console.WriteLine($"Dungeon rendering timing trace: {WarmupFrames} warmup / {SampleFrames} samples; frozen runtime states, 2x zoom, {size.Width}x{size.Height}; real GameShell HUD and insets.")
            Console.WriteLine("Full-frame target <16 ms; live-layer planning target <6 ms. Live timing alone cannot establish full-frame performance.")
            For Each state In {0, 1, 2, 3, 4}
                For Each tilted In {False, True}
                    Using fixture As New DungeonFrameFixture(state, size, tilted), image As New Bitmap(size.Width, size.Height)
                        Dim canvas = fixture.Canvas
                        If Not tilted Then MeasureLive(maze, canvas.DungeonState, size, DungeonTour.StateNames(state), canvas.BoardInsets)
                        For zoom As Integer = 0 To 1
                            DungeonTour.SettleCamera(canvas)
                            canvas.CycleCameraZoom()
                        Next
                        DungeonTour.SettleCamera(canvas)
                        Dim draw As Action = Sub() canvas.DrawToBitmap(image, New Rectangle(Point.Empty, size))
                        Report("full-frame", DungeonTour.StateNames(state), size, If(tilted, "tilt", "flat"), Measure(draw))
                    End Using
                Next
            Next
        End Sub

        Private Sub MeasureLive(maze As MazeDefinition, run As DungeonRun, size As Size, stateName As String, insets As System.Windows.Forms.Padding)
            Dim area As New Rectangle(insets.Left, insets.Top, size.Width - insets.Horizontal, size.Height - insets.Vertical)
            ' Reproduce GameCanvas's real flat-view camera progression and MazeRenderer layout. The bitmap
            ' is window-sized, with viewport clipping applied BEFORE the actual 2x graphics transform.
            Dim camera As New BoardCamera()
            camera.Recenter((maze.StartColumn + 0.5F) / maze.ColumnCount, (maze.StartRow + 0.5F) / maze.RowCount)
            For zoom As Integer = 0 To 2
                For tick As Integer = 1 To 120
                    camera.Advance(run.MarbleX / maze.ColumnCount, run.MarbleY / maze.RowCount, 16, True, False)
                Next
                If zoom < 2 Then camera.CycleZoom()
            Next
            Dim frame = camera.Frame(area, maze.ColumnCount, maze.RowCount, Nothing)
            If frame.Scale <> 2 Then Throw New Exception("Performance live layer must use settled 2x zoom")
            Dim margin As Single = Math.Min(area.Width, area.Height) * 0.04F
            Dim tile = Math.Min((area.Width - 2 * margin) / maze.ColumnCount, (area.Height - 2 * margin) / maze.RowCount)
            Dim left = area.Left + (area.Width - tile * maze.ColumnCount) / 2
            Dim top = area.Top + (area.Height - tile * maze.RowCount) / 2
            Using image As New Bitmap(size.Width, size.Height), g = Graphics.FromImage(image)
                g.SmoothingMode = SmoothingMode.AntiAlias
                g.SetClip(area, CombineMode.Intersect)
                g.TranslateTransform(frame.Offset.X, frame.Offset.Y)
                g.ScaleTransform(frame.Scale, frame.Scale)
                Dim draw As Action = Sub() DungeonRenderer.Draw(g, left, top, tile, maze, run)
                Report("live-layer", stateName, size, "flat", Measure(draw))
            End Using
        End Sub

        Private Function Measure(draw As Action) As Double()
            For i As Integer = 1 To WarmupFrames : draw() : Next
            Dim samples(SampleFrames - 1) As Double
            Dim watch As New Stopwatch()
            For i As Integer = 0 To samples.Length - 1
                watch.Restart()
                draw()
                watch.Stop()
                samples(i) = watch.Elapsed.TotalMilliseconds
            Next
            Array.Sort(samples)
            Return samples
        End Function

        Private Sub Report(layer As String, state As String, size As Size, view As String, samples As Double())
            Dim median = (samples(samples.Length \ 2 - 1) + samples(samples.Length \ 2)) / 2
            Dim p95 = samples(CInt(Math.Ceiling(samples.Length * 0.95)) - 1)
            Dim maximum = samples(samples.Length - 1)
            Dim target = If(layer = "full-frame", 16.0, 6.0)
            Console.WriteLine($"{layer}, state={state}, size={size.Width}x{size.Height}, view={view}, zoom=2x, samples={samples.Length}, median={median:F3}ms, p95={p95:F3}ms, max={maximum:F3}ms, target=<{target:F0}ms, p95-over-target={p95 >= target}")
        End Sub
    End Module
End Namespace
