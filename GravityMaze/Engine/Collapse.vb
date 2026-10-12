Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports GravityMaze.Levels

Namespace Engine
    ' The escape after the vault key: a crumbling front that follows corridors outward from the vault,
    ' plus warned cave-ins. Pure tile logic; DungeonRun drives it and renderers only read it.
    Public NotInheritable Class Collapse
        Public Const CaveWarningMs As Integer = 1500
        Public Const CrackBand As Single = 2.0F
        Public Const Pending As Integer = 0, Warning As Integer = 1, Closed As Integer = 2

        Private ReadOnly spec As CollapseSpec
        Private ReadOnly rows As Integer, columns As Integer
        Private ReadOnly dist As Integer(,)
        Private ReadOnly states As Integer()
        Private ReadOnly warnedAt As Long()
        Private ReadOnly caveOf As New Dictionary(Of Integer, Integer)()   ' tile key -> cave-in index
        Private clock As Long
        Private push As Single
        Private changes As Integer

        ' passable(row, column) describes the map once the collapse starts, before any cave-in closes.
        ' Every cave-in tile counts as wall here, so the front never shortcuts through one.
        Public Sub New(spec As CollapseSpec, rows As Integer, columns As Integer, origin As Point, passable As Func(Of Integer, Integer, Boolean))
            Me.spec = spec : Me.rows = rows : Me.columns = columns
            states = New Integer(spec.CaveIns.Count - 1) {}
            warnedAt = New Long(spec.CaveIns.Count - 1) {}
            For i As Integer = 0 To spec.CaveIns.Count - 1
                For Each t In spec.CaveIns(i).Tiles
                    caveOf(t.Row * columns + t.Column) = i
                Next
            Next
            dist = New Integer(rows - 1, columns - 1) {}
            For r As Integer = 0 To rows - 1
                For c As Integer = 0 To columns - 1
                    dist(r, c) = -1
                Next
            Next
            Dim queue As New Queue(Of Point)()
            dist(origin.Y, origin.X) = 0
            queue.Enqueue(origin)
            While queue.Count > 0
                Dim p = queue.Dequeue()
                For Each d In {New Point(1, 0), New Point(-1, 0), New Point(0, 1), New Point(0, -1)}
                    Dim x = p.X + d.X, y = p.Y + d.Y
                    If x < 0 OrElse y < 0 OrElse x >= columns OrElse y >= rows OrElse dist(y, x) >= 0 Then Continue For
                    If Not passable(y, x) OrElse caveOf.ContainsKey(y * columns + x) Then Continue For
                    dist(y, x) = dist(p.Y, p.X) + 1
                    queue.Enqueue(New Point(x, y))
                Next
            End While
        End Sub

        Public ReadOnly Property ElapsedMs As Long
            Get
                Return clock
            End Get
        End Property

        Public ReadOnly Property Rumbling As Boolean
            Get
                Return clock < spec.RumbleMs
            End Get
        End Property

        ' Walking tiles from the vault that have fallen; negative during the rumble.
        Public ReadOnly Property Reach As Single
            Get
                Return spec.Speed * (clock - spec.RumbleMs) / 1000.0F - push
            End Get
        End Property

        ' Bumps whenever a cave-in changes state, so planners know to re-route.
        Public ReadOnly Property Version As Integer
            Get
                Return changes
            End Get
        End Property

        Public Function Distance(row As Integer, column As Integer) As Integer
            If row < 0 OrElse column < 0 OrElse row >= rows OrElse column >= columns Then Return -1
            Return dist(row, column)
        End Function

        Public Function IsCollapsed(row As Integer, column As Integer) As Boolean
            Dim d = Distance(row, column)
            Return d >= 0 AndAlso d < Reach
        End Function

        Public Function IsCracking(row As Integer, column As Integer) As Boolean
            Dim d = Distance(row, column)
            Return d >= 0 AndAlso d >= Reach AndAlso d < Reach + CrackBand
        End Function

        Public Function CaveInState(index As Integer) As Integer
            Return states(index)
        End Function

        Public Function IsRubble(row As Integer, column As Integer) As Boolean
            Dim index As Integer
            Return caveOf.TryGetValue(row * columns + column, index) AndAlso states(index) = Closed
        End Function

        Public Function IsCaveInWarning(row As Integer, column As Integer) As Boolean
            Dim index As Integer
            Return caveOf.TryGetValue(row * columns + column, index) AndAlso states(index) = Warning
        End Function

        Public Sub Advance(ms As Integer, occupied As Func(Of Integer, Integer, Boolean))
            clock += ms
            For i As Integer = 0 To states.Length - 1
                Dim cave = spec.CaveIns(i)
                If states(i) = Pending AndAlso Reach >= cave.AtReach Then
                    states(i) = Warning : warnedAt(i) = clock : changes += 1
                End If
                If states(i) = Warning AndAlso clock - warnedAt(i) >= CaveWarningMs AndAlso
                   Not cave.Tiles.Any(Function(t) occupied(t.Row, t.Column)) Then
                    states(i) = Closed : changes += 1
                End If
            Next
        End Sub

        ' After a catch or fall the front must sit at least Pushback tiles behind the respawn tile.
        Public Sub Caught(respawnRow As Integer, respawnColumn As Integer)
            Dim d = Distance(respawnRow, respawnColumn)
            Dim target = If(d < 0, Reach - spec.Pushback, Math.Min(Reach - spec.Pushback, d - spec.Pushback))
            push += Reach - target
        End Sub
    End Class
End Namespace
