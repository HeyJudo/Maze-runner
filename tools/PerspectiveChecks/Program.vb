Imports System
Imports System.Diagnostics
Imports PerspectiveChecks.Rendering

Module Program
    Private _checks As Integer

    Private Sub Check(condition As Boolean, message As String)
        _checks += 1
        If Not condition Then Throw New Exception(message)
    End Sub

    Sub Main()
        Dim pose As New BoardTilt()
        pose.Advance(1, -1, 16)
        Check(pose.X > 0 AndAlso pose.X < 1 AndAlso pose.Y < 0 AndAlso pose.Y > -1, "Input must ease, not snap")
        For i As Integer = 1 To 100 : pose.Advance(1, -1, 16) : Next
        Check(Math.Abs(pose.X - 1) < 0.00001 AndAlso Math.Abs(pose.Y + 1) < 0.00001, "Tilt must converge")
        For i As Integer = 1 To 100 : pose.Advance(0, 0, 16) : Next
        Check(Math.Abs(pose.X) < 0.00001 AndAlso Math.Abs(pose.Y) < 0.00001, "Release must return to rest")
        pose.Reset()
        pose.Advance(5, -5, 1000)
        Check(pose.X <= 1 AndAlso pose.Y >= -1, "Input must be bounded")
        pose.Advance(Single.NaN, Single.PositiveInfinity, 1000)
        Check(Double.IsFinite(pose.X) AndAlso Double.IsFinite(pose.Y), "Invalid inputs must not poison the pose")
        Dim fast As New BoardTilt(), slow As New BoardTilt()
        For i As Integer = 1 To 10 : fast.Advance(1, 1, 16) : Next
        For i As Integer = 1 To 5 : slow.Advance(1, 1, 32) : Next
        Check(Math.Abs(fast.X - slow.X) < 0.000001, "Easing must be independent of tick duration")

        Dim rest As New BoardProjection(800, 800, 0, 0)
        Check(Math.Abs(rest.Project(1, -1).X) < Math.Abs(rest.Project(1, 1).X), "Resting board must have a narrower far edge")
        Dim right As New BoardProjection(800, 800, 1, 0)
        Check(Math.Abs(right.Project(1, 1).Y) < Math.Abs(right.Project(-1, 1).Y), "Right tilt must lower the right edge in depth")
        Dim up As New BoardProjection(800, 800, 0, -1)
        Dim down As New BoardProjection(800, 800, 0, 1)
        Check(Math.Abs(up.Project(1, -1).X) < Math.Abs(down.Project(1, -1).X), "Up tilt must increase far-edge perspective")
        For Each size In {(960, 600), (1600, 900), (2560, 1440), (32, 1000), (1000, 32)}
            For Each tx In {-1.0, 0.0, 1.0}
                For Each ty In {-1.0, 0.0, 1.0}
                    Dim camera As New BoardProjection(size.Item1, size.Item2, tx, ty)
                    For Each x In {-1.0, -0.5, 0.0, 0.5, 1.0}
                        For Each y In {-1.0, -0.5, 0.0, 0.5, 1.0}
                            Dim p = camera.Project(x, y)
                            Check(Math.Abs(p.X) <= 1 AndAlso Math.Abs(p.Y) <= 1, "Projection must stay inside the HUD-free viewport")
                            Dim original = camera.Unproject(p.X, p.Y)
                            Check(Math.Abs(original.X - x) < 0.000001 AndAlso Math.Abs(original.Y - y) < 0.000001, "Inverse must preserve ball/wall alignment")
                        Next
                    Next
                Next
            Next
        Next

        Const width As Integer = 128, height As Integer = 96
        Dim source(width * height * 4 - 1) As Byte, output(source.Length - 1) As Byte
        For y As Integer = 0 To height - 1
            For x As Integer = 0 To width - 1
                Dim i = (y * width + x) * 4
                source(i) = CByte(x) : source(i + 1) = CByte(y)
                source(i + 2) = 70 : source(i + 3) = 128
            Next
        Next
        Dim projection As New BoardProjection(width, height, 0.7, -0.8)
        projection.Warp(source, output, width, height)
        Check(output(0) = 0 AndAlso output(3) = 0, "Outside pixels must be transparent")
        For row As Integer = 20 To 70 Step 10
            For col As Integer = 30 To 90 Step 10
                Dim p = projection.Unproject((col + 0.5) * 2 / width - 1, (row + 0.5) * 2 / height - 1)
                Dim i = (row * width + col) * 4
                Check(Math.Abs(output(i) - ((p.X + 1) * width / 2 - 0.5)) < 0.6, "Bilinear X sampling must match the inverse camera")
                Check(Math.Abs(output(i + 1) - ((p.Y + 1) * height / 2 - 0.5)) < 0.6, "Bilinear Y sampling must match the inverse camera")
                Check(output(i + 2) = 70 AndAlso output(i + 3) = 128, "Premultiplied color and alpha must survive projection")
            Next
        Next
        Array.Clear(source)
        projection.Warp(source, output, width, height)
        Check(Array.TrueForAll(output, Function(b) b = 0), "Frames must not retain old pixels")

        Dim large(800 * 800 * 4 - 1) As Byte, result(large.Length - 1) As Byte
        Dim perf As New BoardProjection(800, 800, 0.8, -0.7)
        For i As Integer = 1 To 10 : perf.Warp(large, result, 800, 800) : Next
        Dim timer = Stopwatch.StartNew()
        For i As Integer = 1 To 10 : perf.Warp(large, result, 800, 800) : Next
        Console.WriteLine($"Perspective warp: {timer.Elapsed.TotalMilliseconds / 10:F1} ms/frame at 800x800 (cloud CPU, excludes GDI drawing)")
        Console.WriteLine($"PASS: {_checks} perspective, easing, alignment, and pixel checks")
    End Sub
End Module
