Imports System
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports GravityMaze.Engine
Imports GravityMaze.Levels
Imports GravityMaze.Data

Module Program
    Private checks As Integer
    Private Sub Check(condition As Boolean, message As String)
        checks += 1
        If Not condition Then Throw New Exception(message)
    End Sub

    Private Function Room(Optional launcher As Boolean = False, Optional blade As Boolean = False) As MazeDefinition
        Dim spec As New DungeonDefinition()
        spec.Spikes.Add(New SpikeTrap With {.Row = 1, .Column = 3})
        If launcher Then spec.Launchers.Add(New DartLauncher With {.X = 1.5F, .Y = 2.5F, .DX = 1})
        If blade Then spec.Blades.Add(New BladeTrap With {.X1 = 2.5F, .Y1 = 2.5F, .X2 = 5.5F, .Y2 = 2.5F})
        Return New MazeDefinition({"111111111", "1S0T0K001", "100000001", "1a00E00b1", "1000Q0001", "100000G01", "111111111"}, spec)
    End Function

    Sub Main()
        TestSealsAndGates()
        TestTraps()
        TestHealthAndReset()
        TestRecords()
        ProveDungeon()
        Console.WriteLine($"PASS: {checks} dungeon objective, trap, collision, health, record and playable-route checks")
    End Sub

    Private Sub TestSealsAndGates()
        Dim maze = Room(), run As New DungeonRun(maze)
        Check(run.SealCount = 0 AndAlso Not run.GateOpen("a"c) AndAlso Not run.GateOpen("b"c) AndAlso Not run.GateOpen("E"c), "All dungeon gates must begin locked")
        Dim collected As Integer
        AddHandler run.Cue, Sub(sender, args)
                                If args.Kind = "seal" Then collected += 1
                            End Sub
        run.Collect(5.9F, 1.9F)
        Check(run.SealCount = 0, "A seal must be collected by touching its centre area")
        run.Collect(5.5F, 1.5F) : run.Collect(5.5F, 1.5F)
        Check(run.SealCount = 1 AndAlso collected = 1, "Collecting a seal twice must not repeat its reward")
        Check(run.GateOpen("a"c) AndAlso Not run.GateOpen("b"c) AndAlso Not run.GateOpen("E"c), "First seal opens only its shortcut")
        run.Collect(4.5F, 4.5F)
        Check(run.SealCount = 2 AndAlso run.GateOpen("E"c) AndAlso run.GateOpen("b"c), "Both seals must open the final gate")
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
        Dim rows = {"111111111", "1SK000001", "100000001", "1a00E00b1", "1000QH001", "100000G01", "111111111"}
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

    Private Sub TestRecords()
        Dim scoreDir = Path.Combine(Path.GetTempPath(), "dungeon_records_" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(scoreDir)
        Try
            Dim file = Path.Combine(scoreDir, "records.xml")
            Dim scores As New ScoreManager(file)
            scores.AddRecord(New ScoreRecord With {.PlayerName = "TEST", .LevelNumber = 0, .CampaignLevels = 3, .TimeSeconds = 80, .Date = DateTime.UtcNow})
            scores.AddRecord(New ScoreRecord With {.PlayerName = "TEST", .LevelNumber = 0, .CampaignLevels = 4, .TimeSeconds = 140, .Date = DateTime.UtcNow})
            scores = New ScoreManager(file)
            Check(scores.TopRecords(0, 10, 4).Count = 1 AndAlso scores.BestRecordFor("TEST", 0, 4).TimeSeconds = 140, "Four-level campaigns must not compete against old three-level times")
            Check(scores.TopRecords(0, 10).Count = 2, "Old campaign records must remain preserved")
            IO.File.WriteAllText(file, IO.File.ReadAllText(file).Replace(" campaignLevels=""3""", ""))
            scores = New ScoreManager(file)
            Check(scores.TopRecords(0, 10, 3).Count = 1 AndAlso scores.BestRecord(0, 3).TimeSeconds = 80, "Legacy XML without campaign size must reload as a three-level run")
        Finally
            Directory.Delete(scoreDir, True)
        End Try
    End Sub

    Private Sub ProveDungeon()
        Dim maze = MazeManager.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Mazes", "Level4.txt"))
        Check(maze.ColumnCount = 47 AndAlso maze.RowCount = 37, "Shipped dungeon dimensions")
        Check(maze.Dungeon.Spikes.Count >= 4 AndAlso maze.Dungeon.Blades.Count >= 3 AndAlso maze.Dungeon.Launchers.Count >= 3, "Finale must contain all three authored trap types")
        Dim route = DungeonPilot.Route(maze, New Point(maze.StartColumn, maze.StartRow), 0)
        Check(route.Count > 100, "The finale must require a substantial navigable maze route")
        Check(route.Any(Function(p) maze.GetTile(p.Y, p.X) = "K"c) AndAlso route.Any(Function(p) maze.GetTile(p.Y, p.X) = "Q"c), "Route must collect both seals before the exit")
        Check(route.Any(Function(p) maze.GetTile(p.Y, p.X) = "T"c), "Final spike bridge must be part of the exit route")
        For Each delay In {0, 37, 125, 250}
            Dim engine As New GameEngine(maze, 0)
            For i As Integer = 1 To delay : engine.Update(0, 0) : Next
            Dim pilot As New DungeonPilot(maze)
            Dim hits As Integer = 0
            AddHandler engine.HeartLost, Sub(sender, args) hits += 1
            For tick As Integer = 1 To 30000
                Dim tilt = pilot.NextTilt(engine)
                engine.Update(tilt.X, tilt.Y)
                If engine.State <> GameState.Playing Then Exit For
            Next
            Check(engine.State = GameState.LevelComplete AndAlso engine.Hearts > 0 AndAlso engine.Dungeon.SealMask = 3, "Dungeon must be beatable with health and traps enabled at varied start timings")
            Check(pilot.WaitTicks > 0, "Playable route must actually wait for trap windows")
            Console.WriteLine($"CLEAR: delay={delay * 16}ms, {engine.ElapsedSeconds:F2}s, hearts={engine.Hearts:F2}, hits={hits}, waited={pilot.WaitTicks} ticks")
        Next
    End Sub

    Private Sub Drive(engine As GameEngine, x As Single, y As Single)
        For i As Integer = 1 To 1000
            Dim dx = x - engine.BallX, dy = y - engine.BallY
            If Math.Abs(dx) < 0.06F AndAlso Math.Abs(dy) < 0.06F AndAlso Math.Abs(engine.VelocityX) < 0.012F AndAlso Math.Abs(engine.VelocityY) < 0.012F Then Return
            Dim vx = Math.Clamp(dx * 0.2F, -0.07F, 0.07F), vy = Math.Clamp(dy * 0.2F, -0.07F, 0.07F)
            Dim tx As Single = If(engine.VelocityX < vx - 0.003F, 1, If(engine.VelocityX > vx + 0.003F, -1, 0))
            Dim ty As Single = If(engine.VelocityY < vy - 0.003F, 1, If(engine.VelocityY > vy + 0.003F, -1, 0))
            engine.Update(tx, ty)
            If engine.State <> GameState.Playing Then Return
        Next
    End Sub
End Module
