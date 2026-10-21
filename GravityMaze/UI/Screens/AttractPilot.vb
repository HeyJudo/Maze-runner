Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports GravityMaze.Engine
Imports GravityMaze.Levels

Namespace UI.Screens
    ' Demo "player" for the attract mode behind the menus. Produces tilt values like a keyboard
    ' player would (-1/0/+1) by following the shortest path and steering toward a target speed.
    ' Same controller the VerificationRunner uses to prove levels are beatable.
    Public NotInheritable Class AttractPilot
        Private ReadOnly _maze As MazeDefinition
        Private ReadOnly _path As List(Of Point)
        Private _waypoint As Integer = 1
        Private ReadOnly _dungeon As DungeonPilot

        Public Sub New(maze As MazeDefinition)
            _maze = maze
            _path = ShortestPath(maze)
            If maze.Dungeon IsNot Nothing Then _dungeon = New DungeonPilot(maze)
        End Sub

        Public Sub Restart()
            _waypoint = 1
        End Sub

        Public Function NextTilt(engine As GameEngine) As PointF
            If _dungeon IsNot Nothing Then Return _dungeon.NextTilt(engine)
            If _path.Count < 2 Then Return PointF.Empty
            Dim target As Point = _path(Math.Min(_waypoint, _path.Count - 1))
            Dim dx As Single = target.X + 0.5F - engine.BallX
            Dim dy As Single = target.Y + 0.5F - engine.BallY
            If Math.Abs(dx) < 0.25F AndAlso Math.Abs(dy) < 0.25F AndAlso _waypoint < _path.Count - 1 Then
                _waypoint += 1
                Return NextTilt(engine)
            End If
            Dim r As Integer = Math.Max(0, Math.Min(_maze.RowCount - 1, CInt(Math.Floor(engine.BallY))))
            Dim c As Integer = Math.Max(0, Math.Min(_maze.ColumnCount - 1, CInt(Math.Floor(engine.BallX))))
            Dim tile As Char = _maze.GetTile(r, c)
            Dim cap As Single = If(tile = "I"c, 0.05F, If(tile = "F"c, 0.14F, 0.09F))
            ' Boost strip that ends in a standing barrier: floor it along the strip, steer the other axis as usual.
            Dim boost As Point = If(tile = "F"c, BarrierRunUp(engine), Point.Empty)
            If boost <> Point.Empty Then
                Return New PointF(If(boost.X <> 0, boost.X, Key(dx, engine.VelocityX, cap)),
                                  If(boost.Y <> 0, boost.Y, Key(dy, engine.VelocityY, cap)))
            End If
            Return New PointF(Key(dx, engine.VelocityX, cap), Key(dy, engine.VelocityY, cap))
        End Function

        ' Unit direction of a straight boost strip ahead that ends in a standing barrier, or Point.Empty.
        Private Function BarrierRunUp(engine As GameEngine) As Point
            For j As Integer = Math.Max(1, _waypoint) To Math.Min(_path.Count - 1, _waypoint + 10)
                Dim p As Point = _path(j)
                Dim t As Char = _maze.GetTile(p.Y, p.X)
                If t = "B"c Then
                    If Not engine.IsBarrierStanding(p.Y, p.X) Then Return Point.Empty
                    Return New Point(Math.Sign(p.X - _path(j - 1).X), Math.Sign(p.Y - _path(j - 1).Y))
                End If
                If t <> "F"c Then Return Point.Empty
            Next
            Return Point.Empty
        End Function

        Private Shared Function Key(distance As Single, velocity As Single, cap As Single) As Single
            Dim desired As Single = Math.Max(-cap, Math.Min(cap, distance * 0.2F))
            If velocity < desired - 0.004F Then Return 1.0F
            If velocity > desired + 0.004F Then Return -1.0F
            Return 0.0F
        End Function

        Private Shared Function ShortestPath(maze As MazeDefinition) As List(Of Point)
            Dim start As New Point(maze.StartColumn, maze.StartRow)
            Dim goal As New Point(maze.GoalColumn, maze.GoalRow)
            Dim prev As New Dictionary(Of Point, Point) From {{start, start}}
            Dim q As New Queue(Of Point)()
            q.Enqueue(start)
            While q.Count > 0
                Dim cur As Point = q.Dequeue()
                If cur = goal Then Exit While
                For Each d In {New Point(1, 0), New Point(-1, 0), New Point(0, 1), New Point(0, -1)}
                    Dim n As New Point(cur.X + d.X, cur.Y + d.Y)
                    If n.X < 0 OrElse n.Y < 0 OrElse n.X >= maze.ColumnCount OrElse n.Y >= maze.RowCount Then Continue For
                    Dim t As Char = maze.GetTile(n.Y, n.X)
                    If t = "1"c OrElse t = "H"c OrElse prev.ContainsKey(n) Then Continue For
                    prev(n) = cur
                    q.Enqueue(n)
                Next
            End While
            Dim path As New List(Of Point)()
            If Not prev.ContainsKey(goal) Then Return path
            Dim p As Point = goal
            While p <> start
                path.Add(p)
                p = prev(p)
            End While
            path.Add(start)
            path.Reverse()
            Return path
        End Function
    End Class
End Namespace
