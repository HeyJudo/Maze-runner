Imports System
Imports System.Drawing
Imports System.IO
Imports GravityMaze.Rendering
Imports GravityMaze.Levels

Module Program
    Private checks As Integer
    Private Sub Check(condition As Boolean, message As String)
        checks += 1
        If Not condition Then Throw New Exception(message)
    End Sub

    Sub Main()
        Dim camera As New BoardCamera()
        Dim viewport As New Rectangle(24, 100, 1200, 700)
        Check(camera.Zoom = 1 AndAlso camera.Label = "FULL MAZE", "Default must be the full maze")
        Dim frame = camera.Frame(viewport, 31, 15, Nothing)
        Check(frame.Scale = 1 AndAlso frame.Offset = PointF.Empty, "Full-maze rendering must preserve the original coordinates")
        camera.CycleZoom()
        Check(camera.PreferredZoom = 1.5F, "First closer view must be 1.5x")
        camera.Recenter(0.5F, 0.5F)
        camera.Advance(0.52F, 0.48F, 16, True, False)
        Check(camera.Focus = New PointF(0.5F, 0.5F), "Small movements must remain inside a stable camera area")
        Check(camera.Zoom > 1 AndAlso camera.Zoom < 1.5F, "Zoom must ease instead of jump")
        For i As Integer = 1 To 120
            camera.Advance(0.85F, 0.75F, 16, True, False)
        Next
        Check(camera.Zoom = 1.5F, "Zoom must settle at the selected factor")
        Check(camera.Focus.X > 0.7F AndAlso camera.Focus.X < 0.85F, "Camera must follow with a central movement allowance")
        Dim held = camera.Focus
        camera.Advance(0.85F, 0.75F, 16, True, True)
        Check(camera.Zoom = 1 AndAlso camera.PreferredZoom = 1.5F, "Overview must immediately show full maze without losing preference")
        camera.Advance(0.85F, 0.75F, 16, True, False)
        Check(camera.Zoom > 1 AndAlso camera.Zoom < 1.5F, "Releasing overview must smoothly restore zoom")
        camera.ResetTracking()
        camera.Advance(0.04F, 0.08F, 16, True, False)
        Check(camera.Focus = New PointF(0.04F, 0.08F), "Retry must recenter immediately without sweeping across the maze")
        Check(camera.PreferredZoom = 1.5F, "Retry must keep the selected zoom")
        camera.Advance(0.04F, 0.08F, 16, False, False)
        Check(camera.Zoom = 1 AndAlso camera.PreferredZoom = 1.5F, "Menus must use overview while retaining gameplay preference")
        camera.CycleZoom()
        Check(camera.PreferredZoom = 2 AndAlso camera.Label = "2x", "Second closer view must be 2x")
        camera.CycleZoom()
        Check(camera.PreferredZoom = 1, "Cycling must return to full-maze view")

        Dim fast As New BoardCamera(), slow As New BoardCamera()
        fast.CycleZoom() : slow.CycleZoom()
        fast.Recenter(0.5F, 0.5F) : slow.Recenter(0.5F, 0.5F)
        For i As Integer = 1 To 20 : fast.Advance(0.9F, 0.9F, 16, True, False) : Next
        For i As Integer = 1 To 10 : slow.Advance(0.9F, 0.9F, 32, True, False) : Next
        Check(Math.Abs(fast.Focus.X - slow.Focus.X) < 0.00001 AndAlso Math.Abs(fast.Zoom - slow.Zoom) < 0.00001,
              "Tracking and zoom easing must be independent of tick subdivision")
        held = fast.Focus
        fast.Advance(Single.NaN, 0, 16, True, False)
        fast.Recenter(Single.PositiveInfinity, 0)
        Check(fast.Focus = held, "Invalid coordinates must not corrupt camera state")

        ' Use each shipped grid plus arbitrary future grids; no level number or theme enters the camera.
        For Each file In Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Mazes"), "*.txt")
            Dim maze As New MazeDefinition(IO.File.ReadAllLines(file))
            CheckGrid(maze.ColumnCount, maze.RowCount)
        Next
        CheckGrid(80, 11)
        CheckGrid(11, 80)
        CheckGrid(9, 9)
        Console.WriteLine($"PASS: {checks} camera zoom, tracking, overview, reset and all-grid framing checks")
    End Sub

    Private Sub CheckGrid(columns As Integer, rows As Integer)
        For Each size In {New Size(960, 600), New Size(1600, 900), New Size(2560, 1440)}
            Dim view As New Rectangle(24, 118, size.Width - 48, size.Height - 174)
            For Each projected In {False, True}
                Dim projection As BoardProjection = Nothing
                If projected Then projection = New BoardProjection(view.Width, view.Height, 1, -1)
                Dim camera As New BoardCamera()
                camera.CycleZoom() : camera.CycleZoom()
                For Each x In {0.03F, 0.5F, 0.97F}
                    For Each y In {0.03F, 0.5F, 0.97F}
                        camera.Recenter(x, y)
                        For i As Integer = 1 To 120 : camera.Advance(x, y, 16, True, False) : Next
                        Dim frame = camera.Frame(view, columns, rows, projection)
                        Check(frame.Scale = 2 AndAlso Single.IsFinite(frame.Offset.X) AndAlso Single.IsFinite(frame.Offset.Y), "All grids must produce a finite selected zoom")
                        Dim margin = Math.Min(view.Width, view.Height) * 0.04F
                        Dim tileSize = Math.Min((view.Width - margin * 2) / columns, (view.Height - margin * 2) / rows)
                        Dim px = (view.Width - tileSize * columns) / 2 + x * tileSize * columns
                        Dim py = (view.Height - tileSize * rows) / 2 + y * tileSize * rows
                        If projection IsNot Nothing Then
                            Dim point = projection.Project(px * 2.0 / view.Width - 1, py * 2.0 / view.Height - 1)
                            px = (point.X + 1) * view.Width / 2
                            py = (point.Y + 1) * view.Height / 2
                        End If
                        px = (px + view.Left) * frame.Scale + frame.Offset.X
                        py = (py + view.Top) * frame.Scale + frame.Offset.Y
                        Check(px >= view.Left AndAlso px <= view.Right AndAlso py >= view.Top AndAlso py <= view.Bottom,
                              "Tracked marble must remain visible at every board edge and corner")
                    Next
                Next
                camera.Recenter(0.1F, 0.5F)
                Dim left = camera.Frame(view, columns, rows, projection)
                camera.Recenter(0.9F, 0.5F)
                Dim right = camera.Frame(view, columns, rows, projection)
                Check(right.Offset.X <= left.Offset.X, "Following right must pan the board left")
                ' If the entire zoomed board still fits on an axis, movement must not pan that axis.
                If Not projected AndAlso columns < rows / 3 Then
                    Check(Math.Abs(right.Offset.X - left.Offset.X) < 0.001, "Narrow boards must stay horizontally centered")
                End If
                camera.CycleZoom()
                For i As Integer = 1 To 120 : camera.Advance(0.9F, 0.5F, 16, True, False) : Next
                Dim full = camera.Frame(view, columns, rows, projection)
                Check(full.Scale = 1 AndAlso full.Offset = PointF.Empty, "Returning to full must restore original framing")
            Next
        Next
    End Sub
End Module
