Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Collections.Generic

Namespace Rendering
    Public NotInheritable Class FxParticle
        Public X, Y, VX, VY As Single    ' tile-space position; velocity in tiles per tick
        Public Age, Life, Size As Single ' ms, ms, fraction of a tile
        Public Spin As Single
        Public IsSpark As Boolean
    End Class

    ' All transient ball effects (spray, boost trail, motion streak, hole fall, spawn pulse).
    ' Pure visual state: fed by GameCanvas from engine values, read by MazeRenderer.
    Public NotInheritable Class BallFxState
        Public Const MaxParticles As Integer = 80
        Public Const TrailLength As Integer = 10
        Public Const FallMs As Single = 450.0F
        Public Const SpawnMs As Single = 300.0F
        Public Const IceSpeed As Single = 0.04F    ' tiles/tick
        Public Const BoostSpeed As Single = 0.07F
        Public Const StreakSpeed As Single = 0.09F

        Private ReadOnly rng As New Random()
        Public ReadOnly Particles As New List(Of FxParticle)()
        Public ReadOnly Trail As New List(Of PointF)()   ' oldest first, newest last
        Public BoostMs As Single      ' > 0 while the boost trail should show
        Public Speed As Single
        Private emitAcc As Single

        ' Hole fall ghost
        Public FallActive As Boolean
        Public FallElapsed As Single
        Public HoleX, HoleY, GhostStartX, GhostStartY As Single
        ' Spawn pulse
        Public SpawnPending As Boolean
        Public SpawnActive As Boolean
        Public SpawnElapsed As Single
        Public SpawnX, SpawnY As Single

        Public Sub Reset()
            Particles.Clear()
            Trail.Clear()
            BoostMs = 0.0F
            Speed = 0.0F
            emitAcc = 0.0F
            FallActive = False
            SpawnPending = False
            SpawnActive = False
        End Sub

        Public Sub AddHoleFall(hx As Single, hy As Single)
            HoleX = hx
            HoleY = hy
            If Trail.Count > 0 Then
                GhostStartX = Trail(Trail.Count - 1).X
                GhostStartY = Trail(Trail.Count - 1).Y
            Else
                GhostStartX = hx + 0.3F
                GhostStartY = hy
            End If
            Trail.Clear()
            Particles.Clear()
            BoostMs = 0.0F
            FallElapsed = 0.0F
            FallActive = True
            SpawnPending = True
            SpawnActive = False
        End Sub

        ' Heart pickup sparkle: a ring of pink/gold sparks fanning out from the pickup (tile-space).
        Public Sub AddBurst(x As Single, y As Single)
            For i As Integer = 1 To 14
                If Particles.Count >= MaxParticles Then Exit For
                Dim a As Double = rng.NextDouble() * Math.PI * 2
                Dim v As Single = CSng(0.012 + rng.NextDouble() * 0.02)
                Dim p As New FxParticle()
                p.X = x
                p.Y = y
                p.VX = CSng(Math.Cos(a)) * v
                p.VY = CSng(Math.Sin(a)) * v
                p.Life = 500.0F
                p.Size = 0.07F
                p.IsSpark = True
                Particles.Add(p)
            Next
        End Sub

        Public Sub UpdateMotion(bx As Single, by As Single, vx As Single, vy As Single, tile As Char)
            If FallActive Then Return
            Speed = CSng(Math.Sqrt(vx * vx + vy * vy))
            Trail.Add(New PointF(bx, by))
            If Trail.Count > TrailLength Then Trail.RemoveAt(0)
            If Speed < 0.0001F Then Return
            Dim dx As Single = vx / Speed
            Dim dy As Single = vy / Speed

            If tile = "I"c AndAlso Speed > IceSpeed Then
                emitAcc += Math.Min(1.0F, Speed / 0.24F) * 3.2F
                While emitAcc >= 1.0F
                    emitAcc -= 1.0F
                    Emit(bx, by, dx, dy, False)
                End While
            End If
            If tile = "F"c AndAlso Speed > BoostSpeed Then BoostMs = 220.0F
            If BoostMs > 0.0F AndAlso Speed > BoostSpeed AndAlso rng.NextDouble() < 0.3 Then
                Emit(bx, by, dx, dy, True)
            End If
        End Sub

        Private Sub Emit(bx As Single, by As Single, dx As Single, dy As Single, spark As Boolean)
            If Particles.Count >= MaxParticles Then Return
            Dim spread As Single = CSng((rng.NextDouble() - 0.5) * 1.1)
            Dim back As Single = Speed * CSng(0.15 + rng.NextDouble() * 0.35)
            Dim p As New FxParticle()
            p.X = bx - dx * 0.16F + CSng(rng.NextDouble() - 0.5) * 0.1F
            p.Y = by - dy * 0.16F + CSng(rng.NextDouble() - 0.5) * 0.1F
            p.VX = -dx * back - dy * spread * Speed
            p.VY = -dy * back + dx * spread * Speed
            p.Life = If(spark, 260.0F, 350.0F)
            p.Size = If(spark, 0.05F, CSng(0.07 + rng.NextDouble() * 0.08))
            p.Spin = CSng(rng.NextDouble() * Math.PI)
            p.IsSpark = spark
            Particles.Add(p)
        End Sub

        ' One 16 ms tick. ballX/ballY is the canvas's latest ball position (spawn point).
        Public Sub Advance(ms As Single, ballX As Single, ballY As Single)
            For i As Integer = Particles.Count - 1 To 0 Step -1
                Dim p As FxParticle = Particles(i)
                p.Age += ms
                p.X += p.VX
                p.Y += p.VY
                p.VX *= 0.95F
                p.VY *= 0.95F
                If p.Age >= p.Life Then Particles.RemoveAt(i)
            Next
            If BoostMs > 0.0F Then BoostMs -= ms
            If FallActive Then
                FallElapsed += ms
                If FallElapsed >= FallMs Then
                    FallActive = False
                    If SpawnPending Then
                        SpawnPending = False
                        SpawnActive = True
                        SpawnElapsed = 0.0F
                        SpawnX = ballX
                        SpawnY = ballY
                    End If
                End If
            ElseIf SpawnActive Then
                SpawnElapsed += ms
                If SpawnElapsed >= SpawnMs Then SpawnActive = False
            End If
        End Sub
    End Class
End Namespace
