Imports System
Imports System.Drawing
Imports GravityMaze.Engine
Imports GravityMaze.Levels

Public Module PilotChecks
    Public Sub Run()
        Dim maze As New MazeDefinition(SchemaChecks.EscapeRows, SchemaChecks.EscapeSpec())
        Dim engine As New GameEngine(maze, 0)
        Dim pilot As New DungeonPilot(maze)
        Dim tilt = PointF.Empty
        For tick As Integer = 1 To 20000
            tilt = DungeonPilot.Smooth(tilt, pilot.NextTilt(engine))
            engine.Update(tilt.X, tilt.Y)
            If engine.State <> GameState.Playing Then Exit For
        Next
        Check(engine.State = GameState.LevelComplete AndAlso engine.Dungeon.KeyCount = 3 AndAlso engine.Hearts > 0, "Pilot explores, takes the vault key and escapes through the breakaway with Arduino-smoothed tilt")
        Dim s = DungeonPilot.Smooth(PointF.Empty, New PointF(1, -1))
        Check(Math.Abs(s.X - 0.35F) < 0.001F AndAlso Math.Abs(s.Y + 0.35F) < 0.001F, "Smoothing matches the board's 0.35 weight")
        Check(DungeonPilot.Smooth(PointF.Empty, New PointF(0.2F, 0)).X = 0, "Readings inside the 0.08 deadzone are ignored")
    End Sub
End Module
