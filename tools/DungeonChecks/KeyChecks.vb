Imports System
Imports System.Drawing
Imports GravityMaze.Engine
Imports GravityMaze.Levels

Public Module KeyChecks
    Private Function Escape() As MazeDefinition
        Return New MazeDefinition(SchemaChecks.EscapeRows, SchemaChecks.EscapeSpec())
    End Function

    Public Sub Run()
        Dim cues As New Collections.Generic.List(Of String)()
        Dim run As New DungeonRun(Escape())
        AddHandler run.Cue, Sub(s, e) cues.Add(e.Kind)
        Check(run.KeyCount = 0 AndAlso Not run.GateOpen("c"c) AndAlso Not run.GateOpen("E"c), "Gates start shut")
        run.Collect(3.5F, 1.5F)
        Check(run.KeyMask = 1 AndAlso Not run.GateOpen("c"c), "One key is not enough for the vault")
        run.Collect(7.5F, 1.5F) : run.Collect(7.5F, 1.5F)
        Check(run.KeyMask = 3 AndAlso run.GateOpen("c"c) AndAlso Not run.GateOpen("E"c) AndAlso cues.FindAll(Function(k) k = "key").Count = 2, "K+Q open the vault once each")
        Check(run.RespawnPoint = New Point(1, 1), "Before any checkpoint the respawn is the entrance")
        run.Advance(5.5F, 2.5F, 5.5F, 2.5F)
        Check(run.CheckpointLit(0) AndAlso run.RespawnPoint = New Point(5, 2), "Explore checkpoints light on contact")
        run.Advance(4.5F, 6.5F, 4.5F, 6.5F)
        Check(Not run.CheckpointLit(1), "Escape checkpoints stay dark while exploring")
        Check(run.Solid(5, 1) AndAlso run.Collapse Is Nothing, "Breakaway is wall until the collapse")
        run.Collect(1.5F, 4.5F)
        Check(run.Collapse IsNot Nothing AndAlso cues.Contains("collapse") AndAlso run.KeyCount = 3, "The vault key starts the collapse")
        Check(Not run.GateOpen("c"c) AndAlso run.Solid(3, 5) AndAlso run.GateOpen("E"c) AndAlso Not run.Solid(5, 1), "Collapse shuts the vault gate, opens the exit and the breakaway")
        Check(run.RespawnPoint = New Point(1, 4), "During the escape the vault tile is the fallback respawn")
        run.Advance(4.5F, 6.5F, 4.5F, 6.5F)
        Check(run.CheckpointLit(1) AndAlso run.RespawnPoint = New Point(4, 6), "Escape checkpoints light only after the collapse")

        ' Crumble timing
        Dim crumble As New DungeonRun(Escape())
        crumble.Advance(1.5F, 7.5F, 1.5F, 7.5F)
        Check(crumble.CrumbleStage(7, 1) = 1 AndAlso Not crumble.Pit(7, 1), "Crumble cracks on contact")
        For i As Integer = 1 To 50 : crumble.Advance(5.5F, 2.5F, 5.5F, 2.5F) : Next
        Check(crumble.CrumbleStage(7, 1) = 2 AndAlso crumble.Pit(7, 1), "Crumble opens 800 ms later")
        For i As Integer = 1 To 376 : crumble.Advance(5.5F, 2.5F, 5.5F, 2.5F) : Next
        Check(crumble.CrumbleStage(7, 1) = 0, "Crumble re-forms after six seconds")
        Check(crumble.Pit(7, 6), "Lava is always a pit")

        ' Engine: the front catches a parked marble, costs a heart and respawns it ahead of the front.
        Dim engine As New GameEngine(Escape(), 0)
        Drive(engine, 3.5F, 1.5F) : Drive(engine, 7.5F, 1.5F) : Drive(engine, 5.5F, 2.5F) : Drive(engine, 5.5F, 4.5F) : Drive(engine, 1.5F, 4.5F)
        Check(engine.Dungeon.Collapse IsNot Nothing, "Physics reaches the vault key")
        Dim fell = 0
        AddHandler engine.BallFell, Sub(s, e) fell += 1
        For i As Integer = 1 To 600
            engine.Update(0, 0)
            If fell > 0 Then Exit For
        Next
        Check(fell = 1 AndAlso engine.Hearts <= 2.0F AndAlso Math.Abs(engine.BallX - 1.5F) < 0.01F AndAlso Math.Abs(engine.BallY - 4.5F) < 0.01F, "A caught marble loses a heart and respawns at the vault")
        Check(Not engine.Dungeon.Collapse.IsCollapsed(4, 1) AndAlso engine.Dungeon.Collapse.Reach <= -7.99F, "The respawn tile is safe after the pushback")
        engine.Reset()
        Check(engine.Dungeon.Collapse Is Nothing AndAlso engine.Dungeon.KeyMask = 0 AndAlso Not engine.Dungeon.CheckpointLit(0) AndAlso engine.Hearts = 3, "Restart resets keys, collapse and checkpoints")
        TestDartsCostOneHeart()
    End Sub

    Private Sub TestDartsCostOneHeart()
        Dim engine As New GameEngine(Room(launcher:=True), 0)
        Dim lost As Integer
        AddHandler engine.HeartLost, Sub(sender, args) lost += 1
        Drive(engine, 4.5F, 2.5F)
        For i As Integer = 1 To 150 : engine.Update(0, 0) : Next
        Check(engine.Hearts = 2 AndAlso lost = 1, "A dart burst must cost one heart with the existing damage protection")
        engine.Reset()
        Check(engine.Hearts = 3 AndAlso engine.Dungeon.ElapsedMs = 0 AndAlso engine.Dungeon.Darts.Count = 0, "Retry restores health and clears hazards")
    End Sub
End Module
