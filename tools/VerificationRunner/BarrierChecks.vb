Option Strict On
Option Explicit On

Imports System
Imports GravityMaze.Engine
Imports GravityMaze.Levels

Namespace VerificationRunner
    ' Overdrive barriers: smash on a boost, bounce (¼ heart) when too slow, stay open through falls.
    Public Module BarrierChecks
        Private Sub Check(condition As Boolean, message As String)
            If Not condition Then Throw New Exception(message)
        End Sub

        Public Sub Run()
            Console.WriteLine("[TEST] Overdrive barriers...")

            ' Four boost tiles into a barrier: smashed at full tilt, no health lost, ball rolls on.
            Dim boost As New MazeDefinition({"1111111111", "1SFFFFB0G1", "1111111111"})
            Dim e As New GameEngine(boost, 0)
            Dim broken As Integer = 0
            AddHandler e.BarrierBroken, Sub(s As Object, a As BarrierEventArgs) broken += 1
            Check(e.BarriersTotal = 1 AndAlso e.IsBarrierStanding(1, 6) AndAlso Not e.IsBarrierStanding(1, 5), "one standing barrier")
            For i As Integer = 1 To 200
                e.Update(1.0F, 0.0F)
                If broken > 0 Then Exit For
            Next
            Check(broken = 1 AndAlso e.BarriersBroken = 1 AndAlso Not e.IsBarrierStanding(1, 6), "a boosted hit smashes the barrier")
            Check(e.Hearts = GameEngine.MaxHearts AndAlso e.WallHits = 0, "smashing costs no health")
            For i As Integer = 1 To 200
                If e.State <> GameState.Playing Then Exit For
                e.Update(1.0F, 0.0F)
            Next
            Check(e.State = GameState.LevelComplete, "the ball rolls on through the opening")

            ' Normal top speed (0.12) is below break speed: a wall bounce, once, while held against it.
            Dim slow As New MazeDefinition({"1111111111", "1S0000B0G1", "1111111111"})
            Dim f As New GameEngine(slow, 0)
            Dim bounced As Integer = 0
            AddHandler f.BarrierBounced, Sub(s As Object, a As BarrierEventArgs) bounced += 1
            For i As Integer = 1 To 150 : f.Update(1.0F, 0.0F) : Next
            Check(bounced = 1, $"slow hit bounces once while held (got {bounced})")
            Check(f.IsBarrierStanding(1, 6) AndAlso f.BarriersBroken = 0, "a slow hit leaves it standing")
            Check(f.Hearts = 2.75F AndAlso f.WallHits = 1 AndAlso f.HeartsLost = 0.25F, "a slow hit costs a quarter heart like a wall")

            ' Smash, then fall: the barrier stays open; restart rebuilds it and clears the counters.
            Dim pit As New MazeDefinition({"111111111111", "1SFFFFB0H0G1", "111111111111"})
            Dim p As New GameEngine(pit, 0)
            Dim fell As Boolean = False
            AddHandler p.BallFell, Sub(s As Object, a As BallFellEventArgs) fell = True
            For i As Integer = 1 To 300
                p.Update(1.0F, 0.0F)
                If fell Then Exit For
            Next
            Check(fell AndAlso p.Falls = 1 AndAlso p.HeartsLost = 1.0F, "smashed through into the pit")
            Check(p.BarriersBroken = 1 AndAlso Not p.IsBarrierStanding(1, 6), "the barrier stays open after a fall")
            p.Reset()
            Check(p.BarriersBroken = 0 AndAlso p.IsBarrierStanding(1, 6) AndAlso p.Falls = 0 AndAlso p.WallHits = 0 AndAlso p.HeartsLost = 0,
                  "restart rebuilds barriers and clears the run counters")

            ' A 2-tall barrier is one group: touching one tile opens both.
            Dim wide As New MazeDefinition({"11111111111", "1SFFFFB0001", "10FFFFB00G1", "11111111111"})
            Dim w As New GameEngine(wide, 0)
            Check(w.BarriersTotal = 1, "adjacent barrier tiles form one barrier")
            For i As Integer = 1 To 200
                w.Update(1.0F, 0.0F)
                If w.BarriersBroken > 0 Then Exit For
            Next
            Check(Not w.IsBarrierStanding(1, 6) AndAlso Not w.IsBarrierStanding(2, 6), "the whole barrier opens together")
            Console.WriteLine("  -> smash, bounce once with 1/4 heart, open through falls, rebuild on restart, grouped tiles OK")
        End Sub
    End Module
End Namespace
