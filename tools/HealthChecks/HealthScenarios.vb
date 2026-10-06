Option Strict On
Option Explicit On

Imports System
Imports System.IO
Imports GravityMaze.Engine
Imports GravityMaze.Levels

Namespace Global.GravityMaze.Verification
    Public Module HealthScenarios
        Private _checks As Integer

        Private Sub Check(condition As Boolean, message As String)
            _checks += 1
            If Not condition Then Throw New Exception(message)
        End Sub

        Public Sub Run()
            _checks = 0
            Console.WriteLine("[TEST] Hearts: walls, pits, pickups, out of hearts...")
            Dim room As New MazeDefinition({"11111", "1S001", "10001", "100G1", "11111"})

            ' One contact = one quarter-heart; staying pinned costs nothing more, even after invulnerability ends.
            Dim e As New GameEngine(room, 0)
            Dim lost As Integer = 0
            AddHandler e.HeartLost, Sub(s As Object, a As HeartEventArgs) lost += 1
            Check(e.Hearts = GameEngine.MaxHearts, "starts with full hearts")
            For i As Integer = 1 To 94 : e.Update(-1.0F, 0.0F) : Next   ' ~1.5 s pinned to the left wall
            Check(e.Hearts = 2.75F AndAlso lost = 1, $"pinned for 1.5 s: hearts={e.Hearts}, events={lost}")
            Check(Not e.IsInvulnerable, "invulnerability ends after 1 s")

            ' Leave the wall, come back after invulnerability: one more quarter-heart.
            For i As Integer = 1 To 20 : e.Update(1.0F, 0.0F) : Next
            For i As Integer = 1 To 40 : e.Update(-1.0F, 0.0F) : Next
            Check(e.Hearts = 2.5F, $"second contact: hearts={e.Hearts}")

            ' Contact during invulnerability is free.
            Dim f As New GameEngine(room, 0)
            For i As Integer = 1 To 30 : f.Update(-1.0F, 0.0F) : Next
            For i As Integer = 1 To 8 : f.Update(1.0F, 0.0F) : Next
            For i As Integer = 1 To 10 : f.Update(-1.0F, 0.0F) : Next
            Check(f.Hearts = 2.75F, $"re-hit inside 1 s must be free: hearts={f.Hearts}")

            ' Pits always cost a heart and respawn; 3 pits = out of hearts; Update is then a no-op.
            Dim pit As New MazeDefinition({"1111111", "1S0H0G1", "1111111"})
            Dim p As New GameEngine(pit, 0)
            Dim falls As Integer = 0
            AddHandler p.BallFell, Sub(s As Object, a As BallFellEventArgs) falls += 1
            For i As Integer = 1 To 600
                p.Update(1.0F, 0.0F)
                If p.State <> GameState.Playing Then Exit For
            Next
            Check(falls = 3 AndAlso p.Hearts = 0 AndAlso p.State = GameState.OutOfHearts, $"pits: falls={falls} hearts={p.Hearts} state={p.State}")
            Dim bx As Single = p.BallX
            p.Update(1.0F, 0.0F)
            Check(p.BallX = bx, "no movement after OutOfHearts")

            ' Reset restores hearts and state.
            p.Reset()
            Check(p.Hearts = GameEngine.MaxHearts AndAlso p.State = GameState.Playing AndAlso Not p.IsInvulnerable, "Reset restores hearts")

            ' Pickups: not taken at full hearts; taken at partial health; restored by Reset.
            Dim pk As New MazeDefinition({"1111111", "1S0L001", "10000G1", "1111111"})
            Dim k As New GameEngine(pk, 0)
            For i As Integer = 1 To 36 : k.Update(1.0F, 0.0F) : Next      ' x≈3.9: just rolled over L at full hearts
            Check(Not k.IsPickupTaken(1, 3) AndAlso k.Hearts = 3, "pickup must stay at full hearts")
            Dim k2 As New GameEngine(pk, 0)
            Dim gained As Integer = 0
            AddHandler k2.HeartGained, Sub(s As Object, a As HeartEventArgs)
                gained += 1
                Check(a.PreviousHearts = 2.75F AndAlso a.Hearts = 3, "capped pickup event must report actual healing")
            End Sub
            For i As Integer = 1 To 10 : k2.Update(-1.0F, 0.0F) : Next     ' wall hit -> 2.75 hearts
            For i As Integer = 1 To 40 : k2.Update(1.0F, 0.0F) : Next      ' x≈4.1: past L, short of the right wall
            Check(k2.Hearts = 3 AndAlso gained = 1 AndAlso k2.IsPickupTaken(1, 3), $"pickup: hearts={k2.Hearts} gained={gained}")
            k2.Reset()
            Check(Not k2.IsPickupTaken(1, 3), "Reset restores pickups")

            ' Hearts disabled: nothing ever changes.
            Dim d As New GameEngine(pit, 0) With {.HeartsEnabled = False}
            For i As Integer = 1 To 300 : d.Update(1.0F, 0.0F) : Next
            Check(d.Hearts = GameEngine.MaxHearts AndAlso d.State = GameState.Playing, "HeartsEnabled=False must not cost hearts")
            TestQuarterProgression(room)
            TestCorner(room)
            TestMixedDamage(pit)
            TestUiPulseSequence()
            Console.WriteLine($"PASS: {_checks} health checks (quarter damage, contact suppression, grace period, pits, pickups, reset, disabled mode)")
        End Sub

        Private Sub BumpLeft(engine As GameEngine)
            For i As Integer = 1 To 94 : engine.Update(-1.0F, 0.0F) : Next
        End Sub

        Private Sub TestUiPulseSequence()
            Dim maze As New MazeDefinition(File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Mazes", "Level1.txt")))
            Dim engine As New GameEngine(maze, 0)
            For hit As Integer = 1 To 12
                For tick As Integer = 1 To 94 : engine.Update(0, -1) : Next
                Check(engine.Hearts = 3.0F - hit * 0.25F, $"Level 1 UI pulse {hit}: exact health")
                If hit < 12 Then
                    For tick As Integer = 1 To 12 : engine.Update(0, 1) : Next
                End If
            Next
            Check(engine.State = GameState.OutOfHearts, "UI tour's twelve-hit sequence must end the run")
        End Sub

        Private Sub LeaveLeft(engine As GameEngine)
            For i As Integer = 1 To 20 : engine.Update(1.0F, 0.0F) : Next
        End Sub

        Private Sub TestQuarterProgression(room As MazeDefinition)
            Dim engine As New GameEngine(room, 0)
            Dim events As Integer = 0
            AddHandler engine.HeartLost, Sub(sender, args)
                events += 1
                Check(args.PreviousHearts - args.Hearts = 0.25F, "wall event must carry exact quarter damage")
                Check(args.Hearts = engine.Hearts, "event total must match current health")
            End Sub
            For hit As Integer = 1 To 12
                If hit > 1 Then LeaveLeft(engine)
                BumpLeft(engine)
                Check(engine.Hearts = 3.0F - hit * 0.25F, $"hit {hit}: exact quarter total")
                Check(events = hit, "one event per damaging contact")
                Check(engine.State = If(hit = 12, GameState.OutOfHearts, GameState.Playing), "game over only at zero")
            Next
            Dim x As Single = engine.BallX, seconds As Single = engine.ElapsedSeconds
            engine.Update(1.0F, 1.0F)
            Check(engine.BallX = x AndAlso engine.ElapsedSeconds = seconds, "movement and timer must stop at zero")
            engine.Reset()
            Check(engine.Hearts = 3 AndAlso engine.Attempts = 2, "restart restores exact full health")
        End Sub

        Private Sub TestCorner(room As MazeDefinition)
            Dim engine As New GameEngine(room, 0)
            Dim events As Integer
            AddHandler engine.HeartLost, Sub(sender, args) events += 1
            For i As Integer = 1 To 100 : engine.Update(-1.0F, -1.0F) : Next
            Check(engine.Hearts = 2.75F AndAlso events = 1, "corner contact must not charge both axes")
        End Sub

        Private Sub TestMixedDamage(pit As MazeDefinition)
            Dim engine As New GameEngine(pit, 0)
            For i As Integer = 1 To 10 : engine.Update(-1.0F, 0.0F) : Next
            Check(engine.Hearts = 2.75F AndAlso engine.IsInvulnerable, "wall hit before pit")
            Dim falls As Integer
            AddHandler engine.BallFell, Sub(sender, args)
                If falls = 0 Then Check(engine.IsInvulnerable, "first pit must occur inside the wall grace period")
                falls += 1
            End Sub
            For i As Integer = 1 To 100
                engine.Update(1.0F, 0.0F)
                If falls = 1 Then Exit For
            Next
            Check(falls = 1 AndAlso engine.Hearts = 1.75F, "pit must subtract a full heart during the wall grace period")
            Check(engine.BallX = pit.StartColumn + 0.5F AndAlso engine.BallY = pit.StartRow + 0.5F, "pit must respawn")
            For i As Integer = 1 To 200
                engine.Update(1.0F, 0.0F)
                If engine.State = GameState.OutOfHearts Then Exit For
            Next
            Check(falls = 3 AndAlso engine.Hearts = 0 AndAlso engine.State = GameState.OutOfHearts, "pit with less than one heart must clamp to zero")

            ' One full heart of healing must preserve any fractional remainder below the cap.
            Dim maze As New MazeDefinition({"1111111", "1S0L001", "10000G1", "1111111"})
            Dim healing As New GameEngine(maze, 0)
            For hit As Integer = 1 To 5
                If hit > 1 Then LeaveLeft(healing)
                BumpLeft(healing)
            Next
            Check(healing.Hearts = 1.75F, "five wall hits before pickup")
            Dim gained As Integer
            AddHandler healing.HeartGained, Sub(sender, args)
                gained += 1
                Check(args.Hearts - args.PreviousHearts = 1, "pickup heals one heart below the cap")
            End Sub
            For i As Integer = 1 To 40 : healing.Update(1.0F, 0.0F) : Next
            Check(healing.Hearts = 2.75F AndAlso gained = 1, "pickup must preserve the partial heart")
            For i As Integer = 1 To 20 : healing.Update(-1.0F, 0.0F) : Next
            Check(gained = 1, "collected pickup cannot heal twice")
        End Sub
    End Module
End Namespace
