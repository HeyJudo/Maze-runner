Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Collections.Generic
Imports GravityMaze.Levels

Namespace Engine
    ' Demo/verification driver using the same -1/0/+1 tilt inputs as the keyboard.
    ' Plans through seals and gates, brakes in safe cells, and waits for real trap windows.
    Public NotInheritable Class DungeonPilot
        Private ReadOnly maze As MazeDefinition
        Private path As List(Of Point)
        Private waypoint As Integer
        Private mask As Integer = -1
        Private attempts As Integer = -1
        Private version As Integer = -1
        Private lastPosition As PointF
        Public Property WaitTicks As Integer
            Get
                Return waits
            End Get
            Private Set(value As Integer)
                waits = value
            End Set
        End Property
        Private waits As Integer
        Private Const Cap As Single = 0.07F

        ' DungeonRun exposes no tile lookup (and engine files are off-limits), so the static planner
        ' reads tiles from the maze of the most recently constructed pilot.
        Private Shared Bound As MazeDefinition

        Public Sub New(maze As MazeDefinition)
            Me.maze = maze
            Bound = maze
        End Sub

        Public Function NextTilt(engine As GameEngine) As PointF
            If engine.Dungeon Is Nothing OrElse engine.State <> GameState.Playing Then Return PointF.Empty
            Dim position As New PointF(engine.BallX, engine.BallY)
            If path Is Nothing OrElse mask <> engine.Dungeon.KeyMask OrElse attempts <> engine.Attempts OrElse version <> CollapseVersion(engine.Dungeon) OrElse
               Math.Abs(position.X - lastPosition.X) + Math.Abs(position.Y - lastPosition.Y) > 3 Then
                mask = engine.Dungeon.KeyMask : attempts = engine.Attempts : version = CollapseVersion(engine.Dungeon)
                path = Route(engine.Dungeon, New Point(CInt(Math.Floor(position.X)), CInt(Math.Floor(position.Y))))
                waypoint = Math.Min(1, path.Count - 1)
            End If
            lastPosition = position
            If path.Count = 0 Then Return PointF.Empty
            Dim target = path(waypoint)
            Dim dx = target.X + 0.5F - position.X, dy = target.Y + 0.5F - position.Y
            While Math.Abs(dx) < 0.10F AndAlso Math.Abs(dy) < 0.10F AndAlso waypoint < path.Count - 1
                waypoint += 1 : target = path(waypoint)
                dx = target.X + 0.5F - position.X : dy = target.Y + 0.5F - position.Y
            End While
            If engine.Dungeon.Collapse Is Nothing AndAlso Not SafeTravel(engine, position) AndAlso Not Danger(engine.Dungeon, position.X, position.Y, engine.Dungeon.ElapsedMs) Then
                WaitTicks += 1
                Return New PointF(Brake(engine.VelocityX), Brake(engine.VelocityY))
            End If
            Return New PointF(Steer(dx, engine.VelocityX), Steer(dy, engine.VelocityY))
        End Function

        Private Shared Function CollapseVersion(run As DungeonRun) As Integer
            Return If(run.Collapse Is Nothing, -1, run.Collapse.Version)
        End Function

        Private Function SafeTravel(engine As GameEngine, start As PointF) As Boolean
            Dim previous = start
            Dim at = engine.Dungeon.ElapsedMs + 80
            For index As Integer = waypoint To Math.Min(path.Count - 1, waypoint + 2)
                Dim target As New PointF(path(index).X + 0.5F, path(index).Y + 0.5F)
                Dim dx = target.X - previous.X, dy = target.Y - previous.Y
                Dim distance = Math.Sqrt(dx * dx + dy * dy)
                Dim steps = Math.Max(1, CInt(Math.Ceiling(distance / Cap)))
                For i As Integer = 1 To steps
                    Dim t = i / CSng(steps)
                    If Danger(engine.Dungeon, previous.X + dx * t, previous.Y + dy * t, at + i * 16) Then Return False
                Next
                at += steps * 16 : previous = target
            Next
            Return True
        End Function

        Public Shared Function Danger(run As DungeonRun, x As Single, y As Single, at As Long) As Boolean
            Const margin As Single = 0.06F
            For Each spike In run.Definition.Spikes
                If run.SpikeActive(spike, at) AndAlso DungeonRun.TouchesTile(x, y, spike.Column, spike.Row, GameEngine.BallRadius + margin) Then Return True
            Next
            For Each blade In run.Definition.Blades
                Dim p = DungeonRun.BladePosition(blade, at)
                If (p.X - x) ^ 2 + (p.Y - y) ^ 2 <= (GameEngine.BallRadius + DungeonRun.BladeRadius + margin) ^ 2 Then Return True
            Next
            For Each launcher In run.Definition.Launchers
                Dim phase = DungeonRun.Phase(at, launcher.OffsetMs)
                For Each shot In {DungeonRun.WarningMs, DungeonRun.WarningMs + 250, DungeonRun.WarningMs + 500}
                    If phase < shot Then Continue For
                    Dim distance = (phase - shot) / 16.0F * DungeonRun.DartStep + DungeonRun.DartStep
                    Dim px = launcher.X + launcher.DX * distance, py = launcher.Y + launcher.DY * distance
                    If (px - x) ^ 2 + (py - y) ^ 2 > (GameEngine.BallRadius + DungeonRun.DartRadius + margin) ^ 2 Then Continue For
                    Dim blocked As Boolean = False
                    For step_ As Integer = 1 To CInt(Math.Ceiling(distance / DungeonRun.DartStep))
                        Dim d = Math.Min(distance, step_ * DungeonRun.DartStep)
                        If run.Solid(CInt(Math.Floor(launcher.Y + launcher.DY * d)), CInt(Math.Floor(launcher.X + launcher.DX * d))) Then
                            blocked = True : Exit For
                        End If
                    Next
                    If Not blocked Then Return True
                Next
            Next
            Return False
        End Function

        Private Shared Function Brake(velocity As Single) As Single
            If Math.Abs(velocity) < 0.004F Then Return 0
            Return -Math.Sign(velocity)
        End Function

        Private Shared Function Steer(distance As Single, velocity As Single) As Single
            Dim desired = Math.Clamp(distance * 0.2F, -Cap, Cap)
            If velocity < desired - 0.004F Then Return 1
            If velocity > desired + 0.004F Then Return -1
            Return 0
        End Function

        ' Board model for proofs: ArduinoController's 0.35 smoothing, then its 0.08 deadzone.
        Public Shared Function Smooth(previous As PointF, raw As PointF) As PointF
            Dim x = previous.X + 0.35F * (raw.X - previous.X)
            Dim y = previous.Y + 0.35F * (raw.Y - previous.Y)
            Return New PointF(If(Math.Abs(x) < 0.08F, 0, x), If(Math.Abs(y) < 0.08F, 0, y))
        End Function

        Public Shared Function Blocked(run As DungeonRun, column As Integer, row As Integer, mask As Integer) As Boolean
            Dim maze = Bound
            If column < 0 OrElse row < 0 OrElse column >= maze.ColumnCount OrElse row >= maze.RowCount Then Return True
            Select Case maze.GetTile(row, column)
                Case "1"c : If Not (run.IsBreakaway(row, column) AndAlso (mask And 4) <> 0) Then Return True
                Case "H"c, "W"c, "C"c : Return True
                Case "c"c : If Not ((mask And 3) = 3 AndAlso (mask And 4) = 0) Then Return True
                Case "E"c : If (mask And 4) = 0 Then Return True
            End Select
            Dim c = run.Collapse
            Return c IsNot Nothing AndAlso (c.IsRubble(row, column) OrElse c.IsCaveInWarning(row, column) OrElse c.IsCollapsed(row, column) OrElse c.IsCracking(row, column))
        End Function

        Public Shared Function Route(run As DungeonRun, start As Point) As List(Of Point)
            Dim maze = Bound
            Dim initial = (X:=start.X, Y:=start.Y, Mask:=run.KeyMask)
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
                    If Blocked(run, x, y, current.Mask) Then Continue For
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
            result.Add(start) : result.Reverse()
            Return result
        End Function
    End Class
End Namespace
