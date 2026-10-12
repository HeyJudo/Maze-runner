Imports System
Imports System.Collections.Generic
Imports GravityMaze.Levels

Public Module SchemaChecks
    ' Escape fixture: vault row 4, breakaway (5,1), escape corridor rows 6-7, exit gate E (6,8), goal G (6,9).
    Public ReadOnly EscapeRows As String() = {
        "11111111111",
        "1S0K000Q001",
        "10000000001",
        "11111c11111",
        "1V000000001",
        "11111111111",
        "10000000EG1",
        "1C0000W0001",
        "11111111111"}

    Public Function EscapeSpec() As DungeonDefinition
        Dim spec As New DungeonDefinition()
        spec.Checkpoints.Add(New Checkpoint With {.Row = 2, .Column = 5})
        spec.Checkpoints.Add(New Checkpoint With {.Row = 6, .Column = 4, .Escape = True})
        spec.Collapse.Breakaway.Add(New TilePos With {.Row = 5, .Column = 1})
        Return spec
    End Function

    Private Function Rejects(rows As String(), spec As DungeonDefinition) As Boolean
        Try
            Dim unused As New MazeDefinition(rows, spec)
            Return False
        Catch ex As ArgumentException
            Return True
        End Try
    End Function

    Public Sub Run()
        Dim maze As New MazeDefinition(EscapeRows, EscapeSpec())
        Check(maze.Dungeon.Collapse.RumbleMs = 4000 AndAlso maze.Dungeon.Collapse.Speed = 3.0F AndAlso maze.Dungeon.Collapse.Pushback = 8.0F, "Collapse defaults match the spec")
        Check(maze.Dungeon.EscapeOpen(maze, 5, 1) AndAlso Not maze.Dungeon.EscapeOpen(maze, 3, 5), "After collapse the breakaway opens and the vault gate shuts")
        Check(Not maze.Dungeon.EscapeOpen(maze, 7, 6) AndAlso Not maze.Dungeon.EscapeOpen(maze, 7, 1) AndAlso maze.Dungeon.EscapeOpen(maze, 6, 8), "Lava and crumble are never escape floor; the exit gate is")
        Check(Rejects({"111111111", "1S0a0K001", "100000V01", "1000E0001", "1000Q0001", "100000G01", "111111111"}, New DungeonDefinition()), "Retired a/b gates are rejected")
        Check(Rejects({"111111111", "1S000K001", "100000001", "1000E0001", "1000Q0001", "100000G01", "111111111"}, New DungeonDefinition()), "A dungeon needs exactly one vault key V")
        Dim notWall = EscapeSpec() : notWall.Collapse.Breakaway(0).Column = 2 : notWall.Collapse.Breakaway(0).Row = 4
        Check(Rejects(EscapeRows, notWall), "Breakaway tiles must be walls")
        Dim trap = EscapeSpec()
        trap.Collapse.CaveIns.Add(New CaveIn With {.AtReach = 0, .Tiles = New List(Of TilePos) From {New TilePos With {.Row = 6, .Column = 3}, New TilePos With {.Row = 7, .Column = 3}}})
        Check(Rejects(EscapeRows, trap), "A cave-in that seals the only escape is rejected")
        Dim onCheckpoint = EscapeSpec()
        onCheckpoint.Collapse.CaveIns.Add(New CaveIn With {.Tiles = New List(Of TilePos) From {New TilePos With {.Row = 6, .Column = 4}}})
        Check(Rejects(EscapeRows, onCheckpoint), "Cave-ins may not bury a checkpoint")
        Dim badLight = EscapeSpec() : badLight.Lights.Add(New LightSource With {.Row = 0, .Column = 0})
        Check(Rejects(EscapeRows, badLight), "Lights sit on open tiles")
        Dim badSpeed = EscapeSpec() : badSpeed.Collapse.Speed = 0
        Check(Rejects(EscapeRows, badSpeed), "Front speed must be positive")
    End Sub
End Module
