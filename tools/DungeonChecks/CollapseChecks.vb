Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports GravityMaze.Engine
Imports GravityMaze.Levels

Public Module CollapseChecks
    Private ReadOnly Grid As String() = {"0000000", "0110110", "0000000"}

    Private Function Make(spec As CollapseSpec) As Collapse
        Return New Collapse(spec, 3, 7, New Point(0, 0), Function(r, c) Grid(r)(c) = "0"c)
    End Function

    Private Function Cave(atReach As Single, ParamArray tiles As Integer()) As CaveIn
        Dim result As New CaveIn With {.AtReach = atReach}
        For i As Integer = 0 To tiles.Length - 1 Step 2
            result.Tiles.Add(New TilePos With {.Row = tiles(i), .Column = tiles(i + 1)})
        Next
        Return result
    End Function

    Public Sub Run()
        Dim nobody As Func(Of Integer, Integer, Boolean) = Function(r, c) False
        Dim front = Make(New CollapseSpec())
        Check(front.Distance(0, 6) = 6 AndAlso front.Distance(2, 0) = 2 AndAlso front.Distance(1, 1) = -1, "Front distances follow corridors, not walls")
        Check(front.Rumbling AndAlso front.Reach < 0 AndAlso Not front.IsCollapsed(0, 0), "Nothing collapses during the rumble")
        front.Advance(5000, nobody)
        Check(Not front.Rumbling AndAlso Math.Abs(front.Reach - 3.0F) < 0.001F, "Reach grows at 3 tiles/s after a 4 s rumble")
        Check(front.IsCollapsed(0, 2) AndAlso Not front.IsCollapsed(0, 3) AndAlso front.IsCracking(0, 3) AndAlso front.IsCracking(0, 4) AndAlso Not front.IsCracking(0, 5), "Collapsed tiles sit behind a two-tile cracking band")

        Dim walled As New CollapseSpec()
        walled.CaveIns.Add(Cave(100, 0, 3, 1, 3, 2, 3))
        Check(Make(walled).Distance(0, 6) = -1, "The front never passes through a cave-in, even before it closes")

        Dim spec As New CollapseSpec()
        spec.CaveIns.Add(Cave(-12, 2, 5))
        Dim caves = Make(spec)
        Dim version = caves.Version
        caves.Advance(16, nobody)
        Check(caves.CaveInState(0) = Collapse.Warning AndAlso caves.IsCaveInWarning(2, 5) AndAlso caves.Version > version, "A cave-in warns once the front reaches its trigger")
        Dim parked As Func(Of Integer, Integer, Boolean) = Function(r, c) r = 2 AndAlso c = 5
        caves.Advance(3000, parked)
        Check(caves.CaveInState(0) = Collapse.Warning AndAlso Not caves.IsRubble(2, 5), "An occupied cave-in waits for the marble to leave")
        caves.Advance(16, nobody)
        Check(caves.CaveInState(0) = Collapse.Closed AndAlso caves.IsRubble(2, 5), "The cave-in closes once the tiles are clear")

        Dim caught = Make(New CollapseSpec())
        caught.Advance(4000 + 3334, nobody)          ' reach ~ 10
        caught.Caught(0, 6)                            ' respawn distance 6
        Check(caught.Reach <= 6 - 8 + 0.001F AndAlso Not caught.IsCollapsed(0, 6), "A catch pushes the front at least 8 tiles behind the respawn")
        Dim before = caught.Reach
        caught.Caught(1, 1)                            ' unreachable respawn
        Check(Math.Abs(caught.Reach - (before - 8)) < 0.001F, "An unreachable respawn still pushes the front back 8 tiles")
    End Sub
End Module
