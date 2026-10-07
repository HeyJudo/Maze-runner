Imports System
Imports System.Drawing
Imports GravityMaze.Engine
Imports GravityMaze.Levels

Public Module KeyChecks
    Public Sub Run()
        TestSealsAndGates()
        TestHealthAndReset()
    End Sub

    Private Sub TestSealsAndGates()
        Dim maze = Room(), run As New DungeonRun(maze)
        Check(run.SealCount = 0 AndAlso Not run.GateOpen("E"c), "All dungeon gates must begin locked")
        Dim collected As Integer
        AddHandler run.Cue, Sub(sender, args)
                                If args.Kind = "seal" Then collected += 1
                            End Sub
        run.Collect(5.9F, 1.9F)
        Check(run.SealCount = 0, "A seal must be collected by touching its centre area")
        run.Collect(5.5F, 1.5F) : run.Collect(5.5F, 1.5F)
        Check(run.SealCount = 1 AndAlso collected = 1, "Collecting a seal twice must not repeat its reward")
        Check(Not run.GateOpen("E"c), "First seal leaves the exit gate locked")
        run.Collect(4.5F, 4.5F)
        Check(run.SealCount = 2 AndAlso run.GateOpen("E"c), "Both seals must open the final gate")
        run.Reset()
        Check(run.SealMask = 0 AndAlso run.ElapsedMs = 0 AndAlso run.Darts.Count = 0, "Retry resets seals, clocks and projectiles")
        Dim engine As New GameEngine(maze, 0) With {.HeartsEnabled = False}
        Drive(engine, 4.5F, 2.5F)
        For i As Integer = 1 To 50 : engine.Update(0, 1) : Next
        Check(engine.BallY <= 2.731F AndAlso engine.Dungeon.SealCount = 0, "Locked exit gate must physically block the marble")
        engine.Reset()
        Drive(engine, 6.5F, 2.5F)
        Drive(engine, 6.5F, 5.5F)
        Check(Math.Abs(engine.BallX - 6.5F) < 0.1F AndAlso Math.Abs(engine.BallY - 5.5F) < 0.1F, "Fixture must reach the goal through a route around the gate")
        Check(engine.State = GameState.Playing, "Reaching a bypassed goal without both seals must not finish")
    End Sub

    Private Sub TestHealthAndReset()
        Dim engine As New GameEngine(Room(launcher:=True), 0)
        Dim lost As Integer
        AddHandler engine.HeartLost, Sub(sender, args) lost += 1
        Drive(engine, 4.5F, 2.5F)
        For i As Integer = 1 To 150 : engine.Update(0, 0) : Next
        Check(engine.Hearts = 2 AndAlso lost = 1, "A dart burst must cost one heart with the existing damage protection")
        engine.Reset()
        Check(engine.Hearts = 3 AndAlso engine.Dungeon.ElapsedMs = 0 AndAlso engine.Dungeon.Darts.Count = 0, "Retry restores health and clears hazards")
        ' Reach real seals through physics, then verify collection state survives a pit respawn.
        Dim rows = {"111111111", "1SK00V001", "100000001", "1000E0001", "1000QH001", "100000G01", "111111111"}
        engine = New GameEngine(New MazeDefinition(rows, New DungeonDefinition()), 0)
        Drive(engine, 2.5F, 1.5F)
        Check(engine.Dungeon.SealMask = 1, "Engine must collect seals through real movement")
        Drive(engine, 3.5F, 2.5F)
        Drive(engine, 3.5F, 4.5F)
        Drive(engine, 4.5F, 4.5F)
        Check(engine.Dungeon.SealMask = 3, "Second seal must unlock the exit through engine play")
        Dim fell As Integer
        AddHandler engine.BallFell, Sub(sender, args) fell += 1
        For i As Integer = 1 To 60
            engine.Update(1, 0)
            If fell > 0 Then Exit For
        Next
        Check(fell = 1 AndAlso engine.Dungeon.SealMask = 3 AndAlso engine.Hearts = 2, "Pit respawns cost a heart but retain seals and opened shortcuts")
    End Sub
End Module
