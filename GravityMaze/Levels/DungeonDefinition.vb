Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.Json
Imports System.Text.Json.Serialization

Namespace Levels
    ' Authored level data; the engine owns clocks/collisions, rendering owns artwork.
    Public NotInheritable Class SpikeTrap
        Public Property Row As Integer
        Public Property Column As Integer
        Public Property OffsetMs As Integer
    End Class

    Public NotInheritable Class BladeTrap
        Public Property X1 As Single
        Public Property Y1 As Single
        Public Property X2 As Single
        Public Property Y2 As Single
        Public Property OffsetMs As Integer
        Public Property PeriodMs As Integer = 6000
    End Class

    Public NotInheritable Class DartLauncher
        Public Property X As Single
        Public Property Y As Single
        Public Property DX As Integer
        Public Property DY As Integer
        Public Property OffsetMs As Integer
    End Class

    Public NotInheritable Class TilePos
        Public Property Row As Integer
        Public Property Column As Integer
    End Class

    Public NotInheritable Class DecorItem
        Public Property Sprite As String = ""
        Public Property Row As Integer
        Public Property Column As Integer
    End Class

    Public NotInheritable Class LightSource
        Public Property Row As Integer
        Public Property Column As Integer
        Public Property Radius As Single = 3.5F
    End Class

    Public NotInheritable Class Checkpoint
        Public Property Row As Integer
        Public Property Column As Integer
        Public Property Escape As Boolean
    End Class

    Public NotInheritable Class CaveIn
        Public Property AtReach As Single
        Public Property Tiles As New List(Of TilePos)()
    End Class

    Public NotInheritable Class CollapseSpec
        Public Property RumbleMs As Integer = 4000
        Public Property Speed As Single = 3.0F
        Public Property Pushback As Single = 8.0F
        Public Property Breakaway As New List(Of TilePos)()
        Public Property CaveIns As New List(Of CaveIn)()
    End Class

    Public NotInheritable Class DungeonDefinition
        Public Property Spikes As New List(Of SpikeTrap)()
        Public Property Blades As New List(Of BladeTrap)()
        Public Property Launchers As New List(Of DartLauncher)()
        Public Property Decor As New List(Of DecorItem)()
        Public Property Lights As New List(Of LightSource)()
        Public Property Checkpoints As New List(Of Checkpoint)()
        Public Property Collapse As New CollapseSpec()

        Public Shared Function Load(path As String) As DungeonDefinition
            Dim options As New JsonSerializerOptions With {
                .PropertyNameCaseInsensitive = True,
                .UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow}
            Dim result = JsonSerializer.Deserialize(Of DungeonDefinition)(File.ReadAllText(path), options)
            If result Is Nothing Then Throw New InvalidDataException("Dungeon definition is empty: " & path)
            Return result
        End Function

        Public Function IsBreakaway(maze As MazeDefinition, row As Integer, column As Integer) As Boolean
            For Each t In Collapse.Breakaway
                If t.Row = row AndAlso t.Column = column Then Return True
            Next
            Return False
        End Function

        ' The map once the collapse starts, before any cave-in: breakaway open, vault gate shut,
        ' pits/lava/crumble never count as floor.
        Public Function EscapeOpen(maze As MazeDefinition, row As Integer, column As Integer) As Boolean
            If row < 0 OrElse column < 0 OrElse row >= maze.RowCount OrElse column >= maze.ColumnCount Then Return False
            Select Case maze.GetTile(row, column)
                Case "1"c : Return IsBreakaway(maze, row, column)
                Case "H"c, "W"c, "C"c, "c"c : Return False
                Case Else : Return True
            End Select
        End Function

        Public Sub Validate(maze As MazeDefinition)
            If Spikes Is Nothing OrElse Blades Is Nothing OrElse Launchers Is Nothing OrElse Decor Is Nothing OrElse Lights Is Nothing OrElse
               Checkpoints Is Nothing OrElse Collapse Is Nothing OrElse Collapse.Breakaway Is Nothing OrElse Collapse.CaveIns Is Nothing OrElse
               Collapse.CaveIns.Any(Function(ci) ci Is Nothing OrElse ci.Tiles Is Nothing) Then
                Throw New ArgumentException("Dungeon lists cannot be null.")
            End If
            Dim keys As Integer, seals As Integer, vaults As Integer, exits As Integer
            Dim vaultAt As Integer
            For r As Integer = 0 To maze.RowCount - 1
                For c As Integer = 0 To maze.ColumnCount - 1
                    Select Case maze.GetTile(r, c)
                        Case "K"c : keys += 1
                        Case "Q"c : seals += 1
                        Case "V"c : vaults += 1 : vaultAt = r * maze.ColumnCount + c
                        Case "E"c : exits += 1
                        Case "a"c, "b"c : Throw New ArgumentException("Retired seal gates a/b are not supported.")
                    End Select
                Next
            Next
            If keys <> 1 OrElse seals <> 1 OrElse vaults <> 1 OrElse exits = 0 Then Throw New ArgumentException("A dungeon needs one each of K, Q, V and an exit gate (E).")
            Dim cells As New HashSet(Of Integer)()
            For Each spike In Spikes
                If spike Is Nothing OrElse Not Floor(maze, spike.Column + 0.5F, spike.Row + 0.5F) Then Throw New ArgumentException("Spike must lie on dungeon floor.")
                If maze.GetTile(spike.Row, spike.Column) <> "T"c OrElse Not cells.Add(spike.Row * maze.ColumnCount + spike.Column) Then
                    Throw New ArgumentException("Each spike needs a unique T tile.")
                End If
            Next
            For r As Integer = 0 To maze.RowCount - 1
                For c As Integer = 0 To maze.ColumnCount - 1
                    If maze.GetTile(r, c) = "T"c AndAlso Not cells.Contains(r * maze.ColumnCount + c) Then Throw New ArgumentException("T tile has no spike definition.")
                Next
            Next
            For Each blade In Blades
                If blade Is Nothing OrElse blade.PeriodMs < 4000 OrElse Not Single.IsFinite(blade.X1) OrElse Not Single.IsFinite(blade.Y1) OrElse
                   Not Single.IsFinite(blade.X2) OrElse Not Single.IsFinite(blade.Y2) OrElse
                   (blade.X1 <> blade.X2 AndAlso blade.Y1 <> blade.Y2) OrElse (blade.X1 = blade.X2 AndAlso blade.Y1 = blade.Y2) Then
                    Throw New ArgumentException("Blade tracks need distinct cardinal endpoints and a period of at least four seconds.")
                End If
                Dim samples = CInt(Math.Ceiling((Math.Abs(blade.X2 - blade.X1) + Math.Abs(blade.Y2 - blade.Y1)) * 8))
                For i As Integer = 0 To samples
                    Dim t = i / CSng(samples)
                    If Not Floor(maze, blade.X1 + (blade.X2 - blade.X1) * t, blade.Y1 + (blade.Y2 - blade.Y1) * t) Then
                        Throw New ArgumentException("Blade track crosses a wall or pit.")
                    End If
                Next
            Next
            For Each launcher In Launchers
                If launcher Is Nothing OrElse Math.Abs(launcher.DX) + Math.Abs(launcher.DY) <> 1 OrElse Not Floor(maze, launcher.X, launcher.Y) Then
                    Throw New ArgumentException("Dart launchers need a floor opening and a cardinal direction.")
                End If
            Next
            For Each d In Decor
                If d Is Nothing OrElse Not InBounds(maze, d.Row, d.Column) Then Throw New ArgumentException("Decor must lie inside the maze.")
            Next
            For Each l In Lights
                If l Is Nothing OrElse Not InBounds(maze, l.Row, l.Column) OrElse maze.GetTile(l.Row, l.Column) = "1"c Then Throw New ArgumentException("Lights must sit on open tiles.")
            Next
            For Each p In Checkpoints
                If p Is Nothing OrElse Not InBounds(maze, p.Row, p.Column) OrElse maze.GetTile(p.Row, p.Column) = "1"c Then Throw New ArgumentException("Checkpoints must sit on open tiles.")
            Next
            If Collapse.RumbleMs < 0 OrElse Not Single.IsFinite(Collapse.Speed) OrElse Collapse.Speed <= 0 OrElse
               Not Single.IsFinite(Collapse.Pushback) OrElse Collapse.Pushback < 0 Then
                Throw New ArgumentException("Collapse needs a non-negative rumble and pushback and a positive front speed.")
            End If
            For Each t In Collapse.Breakaway
                If t Is Nothing OrElse Not InBounds(maze, t.Row, t.Column) OrElse maze.GetTile(t.Row, t.Column) <> "1"c OrElse
                   t.Row = 0 OrElse t.Column = 0 OrElse t.Row = maze.RowCount - 1 OrElse t.Column = maze.ColumnCount - 1 Then
                    Throw New ArgumentException("Breakaway tiles must be interior walls.")
                End If
            Next
            Dim caved As New HashSet(Of Integer)()
            For Each cave In Collapse.CaveIns
                For Each t In cave.Tiles
                    If t Is Nothing OrElse Not EscapeOpen(maze, t.Row, t.Column) OrElse "SGE".Contains(maze.GetTile(t.Row, t.Column)) OrElse
                       Checkpoints.Any(Function(p) p.Row = t.Row AndAlso p.Column = t.Column) OrElse Not caved.Add(t.Row * maze.ColumnCount + t.Column) Then
                        Throw New ArgumentException("Cave-in tiles must be unique escape floor, not start, goal, exit or checkpoint.")
                    End If
                Next
            Next
            ' Guarantee: everything reachable from the vault can still reach the goal once cave-ins fall.
            Dim reach = Flood(maze, vaultAt, Nothing)
            Dim home = Flood(maze, maze.GoalRow * maze.ColumnCount + maze.GoalColumn, caved)
            For Each cell In reach
                If Not caved.Contains(cell) AndAlso Not home.Contains(cell) Then
                    Throw New ArgumentException($"Cave-ins can trap the marble at row {cell \ maze.ColumnCount}, column {cell Mod maze.ColumnCount}.")
                End If
            Next
        End Sub

        Private Function InBounds(maze As MazeDefinition, row As Integer, column As Integer) As Boolean
            Return row >= 0 AndAlso column >= 0 AndAlso row < maze.RowCount AndAlso column < maze.ColumnCount
        End Function

        ' Cells reachable over EscapeOpen from a start cell index, never entering blocked cells.
        Private Function Flood(maze As MazeDefinition, first As Integer, blocked As HashSet(Of Integer)) As HashSet(Of Integer)
            Dim seen As New HashSet(Of Integer) From {first}, queue As New Queue(Of Integer)()
            queue.Enqueue(first)
            While queue.Count > 0
                Dim cur = queue.Dequeue()
                Dim r = cur \ maze.ColumnCount, c = cur Mod maze.ColumnCount
                For Each d In {(-1, 0), (1, 0), (0, -1), (0, 1)}
                    Dim nr = r + d.Item1, nc = c + d.Item2
                    If Not EscapeOpen(maze, nr, nc) Then Continue For
                    Dim n = nr * maze.ColumnCount + nc
                    If (blocked IsNot Nothing AndAlso blocked.Contains(n)) OrElse Not seen.Add(n) Then Continue For
                    queue.Enqueue(n)
                Next
            End While
            Return seen
        End Function

        Private Shared Function Floor(maze As MazeDefinition, x As Single, y As Single) As Boolean
            If Not Single.IsFinite(x) OrElse Not Single.IsFinite(y) OrElse x < 0 OrElse y < 0 OrElse x >= maze.ColumnCount OrElse y >= maze.RowCount Then Return False
            Return "0TM".Contains(maze.GetTile(CInt(Math.Floor(y)), CInt(Math.Floor(x))))
        End Function
    End Class
End Namespace
