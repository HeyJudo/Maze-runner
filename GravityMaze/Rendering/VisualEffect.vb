Option Strict On
Option Explicit On

Imports System

Namespace Rendering
    ' Represents a transient visual feedback effect for ball-wall collision.
    ' Lifetime is ~340ms; camera stays fixed, so the flash, wall glow, ring and sparks carry the feedback.
    Public NotInheritable Class ImpactEffect
        Public Property TileX As Single
        Public Property TileY As Single
        Public Property NormalX As Single
        Public Property NormalY As Single
        Public Property Speed As Single
        Public Property LifetimeMs As Single = 340.0F
        Public Property ElapsedMs As Single = 0.0F

        ' Deterministic spark data (angle offset from the normal in radians, distance factor, size factor).
        Public ReadOnly SparkAngles() As Single
        Public ReadOnly SparkDistances() As Single
        Public ReadOnly SparkSizes() As Single

        Public Sub New(tileX As Single, tileY As Single, normalX As Single, normalY As Single, speed As Single)
            Dim rng As New Random(CInt((tileX * 131.0F + tileY * 977.0F + speed * 10000.0F) Mod 100000.0F))
            Dim count As Integer = 8 + CInt(Math.Min(1.0F, speed / 0.24F) * 8.0F)
            ReDim SparkAngles(count - 1)
            ReDim SparkDistances(count - 1)
            ReDim SparkSizes(count - 1)
            For i As Integer = 0 To count - 1
                SparkAngles(i) = CSng((rng.NextDouble() - 0.5) * 2.0 * 1.2)
                SparkDistances(i) = CSng(0.5 + rng.NextDouble() * 0.5)
                SparkSizes(i) = CSng(0.6 + rng.NextDouble() * 0.8)
            Next
            Me.TileX = tileX
            Me.TileY = tileY
            Me.NormalX = normalX
            Me.NormalY = normalY
            Me.Speed = speed
        End Sub

        Public ReadOnly Property IsExpired As Boolean
            Get
                Return ElapsedMs >= LifetimeMs
            End Get
        End Property

        Public Sub Advance(deltaMs As Single)
            ElapsedMs += deltaMs
        End Sub
    End Class

    ' Represents an expanding celebration pulse ring at the goal on level completion.
    Public NotInheritable Class GoalCelebrationEffect
        Public Property CenterX As Single
        Public Property CenterY As Single
        Public Property LifetimeMs As Single = 1200.0F
        Public Property ElapsedMs As Single = 0.0F
        Public Property IsActive As Boolean = False

        Public Sub Trigger(cx As Single, cy As Single)
            CenterX = cx
            CenterY = cy
            ElapsedMs = 0.0F
            IsActive = True
        End Sub

        Public Sub Reset()
            IsActive = False
            ElapsedMs = 0.0F
        End Sub

        Public Sub Advance(deltaMs As Single)
            If IsActive Then
                ElapsedMs += deltaMs
                If ElapsedMs >= LifetimeMs Then
                    ' Keep a gentle subtle idle pulse after the main celebration burst
                    ElapsedMs = LifetimeMs
                End If
            End If
        End Sub
    End Class
End Namespace
