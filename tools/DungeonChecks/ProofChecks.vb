Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports GravityMaze.Engine
Imports GravityMaze.Levels

Public Module ProofChecks
    Public Sub Run()
        Dim maze = MazeManager.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Mazes", "Level4.txt"))
        CheckRailConnectivity(maze)
        For Each delay In {0, 37, 125, 250}
            Dim engine As New GameEngine(maze, 0)
            For tick As Integer = 1 To delay
                engine.Update(0, 0)
            Next
            Dim pilot As New DungeonPilot(maze)
            Dim tilt = PointF.Empty
            Dim hits = 0, caught = 0
            Dim fallPosition As PointF? = Nothing
            AddHandler engine.HeartLost, Sub(sender, e) hits += 1
            AddHandler engine.BallFell,
                Sub(sender, e)
                    fallPosition = New PointF(engine.BallX, engine.BallY)
                    If engine.Dungeon.Collapse IsNot Nothing Then caught += 1
                End Sub
            Dim escapeStart = -1.0F
            Dim routeClearance = EmptyClearances(maze.Dungeon.Blades.Count)
            Dim travelClearance = EmptyClearances(maze.Dungeon.Blades.Count)
            Dim routeSegments(1) As Integer, travelSegments(1) As Integer
            Dim routeMask = -1, routeAttempts = -1, routeVersion = -2
            Dim lastPosition = PointF.Empty
            For tick As Integer = 1 To 60000
                Dim before As New PointF(engine.BallX, engine.BallY)
                Dim phase = If(engine.Dungeon.Collapse Is Nothing, 0, 1)
                Dim version = If(engine.Dungeon.Collapse Is Nothing, -1, engine.Dungeon.Collapse.Version)
                ' Match the pilot's replanning triggers, and split its complete route at the vault key.
                If routeMask <> engine.Dungeon.KeyMask OrElse routeAttempts <> engine.Attempts OrElse routeVersion <> version OrElse
                   Math.Abs(before.X - lastPosition.X) + Math.Abs(before.Y - lastPosition.Y) > 3 Then
                    Dim route = DungeonPilot.Route(engine.Dungeon, New Point(CInt(Math.Floor(before.X)), CInt(Math.Floor(before.Y))))
                    RecordRoute(maze, route, engine.Dungeon.KeyMask, routeClearance, routeSegments)
                    If route.Count > 1 Then
                        RecordSegment(maze, before, Centre(route(1)), phase, routeClearance, routeSegments)
                    End If
                    routeMask = engine.Dungeon.KeyMask : routeAttempts = engine.Attempts : routeVersion = version
                End If
                lastPosition = before
                fallPosition = Nothing
                tilt = DungeonPilot.Smooth(tilt, pilot.NextTilt(engine))
                engine.Update(tilt.X, tilt.Y)
                Dim after As New PointF(engine.BallX, engine.BallY)
                ' X then Y is the engine's actual movement order; a respawn jump is not travel.
                Dim reached = If(fallPosition.HasValue, fallPosition.GetValueOrDefault(), after)
                Dim corner As New PointF(reached.X, before.Y)
                RecordSegment(maze, before, corner, phase, travelClearance, travelSegments)
                RecordSegment(maze, corner, reached, phase, travelClearance, travelSegments)
                If fallPosition.HasValue Then RecordSegment(maze, after, after, phase, travelClearance, travelSegments)
                If escapeStart < 0 AndAlso engine.Dungeon.Collapse IsNot Nothing Then escapeStart = engine.ElapsedSeconds
                If engine.State <> GameState.Playing Then Exit For
            Next
            Dim escape = If(escapeStart < 0, -1.0F, engine.ElapsedSeconds - escapeStart)
            Dim collapse = engine.Dungeon.Collapse
            Dim collapseInfo = If(collapse Is Nothing, "none", $"elapsed={collapse.ElapsedMs}ms reach={collapse.Reach:F3} version={collapse.Version}")
            Console.WriteLine($"PROOF delay={delay * 16}ms state={engine.State} time={engine.ElapsedSeconds:F3}s ball=({engine.BallX:F3},{engine.BallY:F3}) key={engine.Dungeon.KeyMask} collapse={collapseInfo}")
            Console.WriteLine($"CLEAR delay={delay * 16}ms total={engine.ElapsedSeconds:F3}s escape={escape:F3}s hearts={engine.Hearts:F2} hits={hits} caught={caught} waited={pilot.WaitTicks}")
            ' Signed distances to the full static rails describe the pilot's selected routes
            ' and travel. A negative value is envelope overlap, not a timed blade collision.
            For phase As Integer = 0 To 1
                Dim name = If(phase = 0, "exploration", "escape")
                For blade As Integer = 0 To maze.Dungeon.Blades.Count - 1
                    Console.WriteLine($"BLADE delay={delay * 16}ms phase={name} rail={blade + 1} selectedRouteClearance={routeClearance(phase, blade):F3} travelClearance={travelClearance(phase, blade):F3} tiles routeSegments={routeSegments(phase)} travelSegments={travelSegments(phase)}")
                Next
            Next
            Check(engine.State = GameState.LevelComplete AndAlso engine.Hearts >= 1, $"Keep must be beatable on board-smoothed tilt (delay {delay * 16}ms)")
            Check(escape >= 25 AndAlso escape <= 70, $"Keep escape must take 25-70 seconds (delay {delay * 16}ms, actual {escape:F3}s)")
            Check(maze.Dungeon.Blades.Count = 3, "The shipped Keep proof covers all three blade rails")
            For phase As Integer = 0 To 1
                Dim name = If(phase = 0, "exploration", "escape")
                Check(routeSegments(phase) > 0 AndAlso travelSegments(phase) > 0, $"Selected {name} routes and travel were measured (delay {delay * 16}ms)")
            Next
        Next
    End Sub

    Private Sub CheckRailConnectivity(maze As MazeDefinition)
        Check(maze.Dungeon.Blades.Count = 3, "Rail connectivity covers all three shipped blade rails")
        Dim route = RailSafeRoute(New DungeonRun(maze))
        Check(route.Count > 1, "A tile-centre route must collect K, Q and V and reach the exit while avoiding every full blade collision envelope")
        Dim minima = EmptyClearances(maze.Dungeon.Blades.Count)
        Dim counts(1) As Integer
        RecordRoute(maze, route, 0, minima, counts)
        Console.WriteLine($"CONNECTIVITY static key-and-exit route={route.Count - 1} segments; gates and breakaway follow key mask; all cave-in tiles closed during escape; excludes full blade envelopes")
        For phase As Integer = 0 To 1
            Dim name = If(phase = 0, "exploration", "escape")
            Check(counts(phase) > 0, $"Rail-safe connectivity witness includes {name}")
            For blade As Integer = 0 To maze.Dungeon.Blades.Count - 1
                Console.WriteLine($"CONNECTIVITY phase={name} rail={blade + 1} clearance={minima(phase, blade):F3} tiles segments={counts(phase)}")
                Check(minima(phase, blade) > 0, $"Rail-safe {name} connectivity witness clears rail {blade + 1}, including endpoints and ball/blade radii")
            Next
        Next
    End Sub

    ' Search the same (tile, key mask) graph as DungeonPilot.Route, pruning every
    ' centre-to-centre edge that touches a full rail envelope, and treating every
    ' scripted cave-in as closed after V. This proves rail-free static connectivity;
    ' the live smoothed proof covers the moving collapse front and timed traps.
    Private Function RailSafeRoute(run As DungeonRun) As List(Of Point)
        Dim maze = run.Maze
        Dim caved As New HashSet(Of Integer)()
        For Each cave In maze.Dungeon.Collapse.CaveIns
            For Each tile In cave.Tiles
                caved.Add(tile.Row * maze.ColumnCount + tile.Column)
            Next
        Next
        Dim initial = (X:=maze.StartColumn, Y:=maze.StartRow, Mask:=0)
        Dim previous As New Dictionary(Of (X As Integer, Y As Integer, Mask As Integer), (X As Integer, Y As Integer, Mask As Integer)) From {{initial, initial}}
        Dim queue As New Queue(Of (X As Integer, Y As Integer, Mask As Integer))()
        queue.Enqueue(initial)
        Dim found = initial
        Dim success As Boolean
        While queue.Count > 0
            Dim current = queue.Dequeue()
            If current.X = maze.GoalColumn AndAlso current.Y = maze.GoalRow AndAlso current.Mask = 7 Then
                found = current : success = True : Exit While
            End If
            For Each delta In {New Point(1, 0), New Point(-1, 0), New Point(0, 1), New Point(0, -1)}
                Dim x = current.X + delta.X, y = current.Y + delta.Y
                If DungeonPilot.Blocked(run, x, y, current.Mask) Then Continue For
                If (current.Mask And 4) <> 0 AndAlso caved.Contains(y * maze.ColumnCount + x) Then Continue For
                Dim a = Centre(New Point(current.X, current.Y)), b = Centre(New Point(x, y))
                If maze.Dungeon.Blades.Exists(Function(blade) RailClearance(a, b, blade) <= 0) Then Continue For
                Dim tile = maze.GetTile(y, x)
                Dim nextMask = current.Mask Or If(tile = "K"c, 1, If(tile = "Q"c, 2, If(tile = "V"c, 4, 0)))
                Dim next_ = (X:=x, Y:=y, Mask:=nextMask)
                If previous.ContainsKey(next_) Then Continue For
                previous(next_) = current : queue.Enqueue(next_)
            Next
        End While
        Dim result As New List(Of Point)()
        If Not success Then Return result
        Dim cursor = found
        While Not cursor.Equals(initial)
            result.Add(New Point(cursor.X, cursor.Y)) : cursor = previous(cursor)
        End While
        result.Add(New Point(initial.X, initial.Y)) : result.Reverse()
        Return result
    End Function

    Private Function EmptyClearances(blades As Integer) As Double(,)
        Dim result(1, blades - 1) As Double
        For phase As Integer = 0 To 1
            For blade As Integer = 0 To blades - 1
                result(phase, blade) = Double.PositiveInfinity
            Next
        Next
        Return result
    End Function

    Private Function Centre(cell As Point) As PointF
        Return New PointF(cell.X + 0.5F, cell.Y + 0.5F)
    End Function

    Private Sub RecordRoute(maze As MazeDefinition, route As List(Of Point), mask As Integer, minima As Double(,), counts As Integer())
        For index As Integer = 1 To route.Count - 1
            RecordSegment(maze, Centre(route(index - 1)), Centre(route(index)), If((mask And 4) = 0, 0, 1), minima, counts)
            Select Case maze.GetTile(route(index).Y, route(index).X)
                Case "K"c : mask = mask Or 1
                Case "Q"c : mask = mask Or 2
                Case "V"c : mask = mask Or 4
            End Select
        Next
    End Sub

    Private Sub RecordSegment(maze As MazeDefinition, a As PointF, b As PointF, phase As Integer, minima As Double(,), counts As Integer())
        counts(phase) += 1
        For index As Integer = 0 To maze.Dungeon.Blades.Count - 1
            Dim blade = maze.Dungeon.Blades(index)
            minima(phase, index) = Math.Min(minima(phase, index), RailClearance(a, b, blade))
        Next
    End Sub

    Private Function RailClearance(a As PointF, b As PointF, blade As BladeTrap) As Double
        Return SegmentDistance(a, b, New PointF(blade.X1, blade.Y1), New PointF(blade.X2, blade.Y2)) -
               (GameEngine.BallRadius + DungeonRun.BladeRadius)
    End Function

    ' Finite segments, not infinite rail lines: intersecting segments have zero distance;
    ' otherwise the closest pair includes at least one endpoint (also handles zero length).
    Private Function SegmentDistance(a As PointF, b As PointF, c As PointF, d As PointF) As Double
        Dim ux = CDbl(b.X) - a.X, uy = CDbl(b.Y) - a.Y
        Dim vx = CDbl(d.X) - c.X, vy = CDbl(d.Y) - c.Y
        Dim wx = CDbl(c.X) - a.X, wy = CDbl(c.Y) - a.Y
        Dim cross = ux * vy - uy * vx
        If cross <> 0 Then
            Dim t = (wx * vy - wy * vx) / cross
            Dim s = (wx * uy - wy * ux) / cross
            If t >= 0 AndAlso t <= 1 AndAlso s >= 0 AndAlso s <= 1 Then Return 0
        End If
        Return Math.Min(Math.Min(PointSegmentDistance(a, c, d), PointSegmentDistance(b, c, d)),
                        Math.Min(PointSegmentDistance(c, a, b), PointSegmentDistance(d, a, b)))
    End Function

    Private Function PointSegmentDistance(p As PointF, a As PointF, b As PointF) As Double
        Dim dx = CDbl(b.X) - a.X, dy = CDbl(b.Y) - a.Y
        Dim lengthSquared = dx * dx + dy * dy
        Dim t = If(lengthSquared = 0, 0, Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0, 1))
        Dim x = p.X - (a.X + t * dx), y = p.Y - (a.Y + t * dy)
        Return Math.Sqrt(x * x + y * y)
    End Function
End Module
