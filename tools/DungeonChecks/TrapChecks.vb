Imports System
Imports System.Drawing
Imports GravityMaze.Engine
Imports GravityMaze.Levels

Public Module TrapChecks
    Public Sub Run()
        TestTraps()
    End Sub

    Private Sub TestTraps()
        Dim maze = Room(), run As New DungeonRun(maze)
        Dim spike = maze.Dungeon.Spikes(0)
        Check(Not run.SpikeActive(spike, 1999) AndAlso run.SpikeActive(spike, 2000) AndAlso Not run.SpikeActive(spike, 3200), "Spike activation boundaries must match telegraphing")
        For i As Integer = 1 To 90 : run.Advance(1.5F, 1.5F, 1.5F, 1.5F) : Next
        Check(run.SpikeWarning(spike) AndAlso Not run.SpikeActive(spike), "Spikes must visibly warn before becoming dangerous")
        run.Reset()
        Dim hits As Integer
        For i As Integer = 1 To 490
            If run.Advance(3.5F, 1.5F, 3.5F, 1.5F) IsNot Nothing Then hits += 1
        Next
        Check(hits = 1, "Standing on a spike field must not take damage every frame during one activation")
        For i As Integer = 1 To 100
            If run.Advance(3.5F, 1.5F, 3.5F, 1.5F) IsNot Nothing Then hits += 1
        Next
        Check(hits = 2, "A later spike cycle is a new hazard contact")

        Dim blade As New BladeTrap With {.X1 = 2.5F, .Y1 = 2.5F, .X2 = 5.5F, .Y2 = 2.5F}
        Check(DungeonRun.BladePosition(blade, 0) = DungeonRun.BladePosition(blade, 590), "Blade must dwell at the first endpoint")
        Check(DungeonRun.BladePosition(blade, 3000) = DungeonRun.BladePosition(blade, 3590), "Blade must dwell before reversing")
        For at As Integer = 0 To 12000 Step 37
            Dim point = DungeonRun.BladePosition(blade, at)
            Check(point.X >= 2.5F AndAlso point.X <= 5.5F AndAlso point.Y = 2.5F, "Blade must stay on its authored rail")
        Next
        Check(DungeonRun.SweptTouches(0, 0, 2, 0, 1, 0, 1, 0, 0.36F), "Swept collision must catch a fast crossing projectile")
        Check(Not DungeonRun.SweptTouches(0, 0, 2, 0, 1, 1, 1, 1, 0.36F), "A nearby projectile must not cause false damage")
        Check(DungeonRun.SweptTouches(0, 0, 2, 0, 2, 0, 0, 0, 0.36F), "Opposing moving objects must collide between frame endpoints")
        run = New DungeonRun(Room(blade:=True))
        Check(run.Advance(2.5F, 2.5F, 2.5F, 2.5F)?.Kind = "blade", "Blade contact must be a hazard")
        For i As Integer = 1 To 30
            Check(run.Advance(2.5F, 2.5F, 2.5F, 2.5F) Is Nothing, "Sustained blade contact must not repeat each frame")
        Next
        run = New DungeonRun(Room(launcher:=True))
        Dim peak As Integer, warnings As Integer
        AddHandler run.Cue, Sub(sender, args)
                                If args.Kind = "warning" Then warnings += 1
                            End Sub
        For i As Integer = 1 To 160
            run.Advance(4.5F, 4.5F, 4.5F, 4.5F)
            peak = Math.Max(peak, run.Darts.Count)
            For Each dart In run.Darts
                Check(dart.X < 8 AndAlso dart.X >= 1 AndAlso dart.Y = 2.5F, "Darts must follow cardinal lanes and stop at walls")
            Next
        Next
        Check(peak = 3 AndAlso run.Darts.Count = 0, "A launcher must fire three darts, then leave a cooldown")
        Check(warnings >= 1, "Nearby traps must emit warning cues")
    End Sub
End Module
