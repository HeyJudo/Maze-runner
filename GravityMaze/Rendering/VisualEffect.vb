Option Strict On
Option Explicit On

Imports System

Namespace Rendering
    ' Represents a transient visual feedback effect for ball-wall collision.
    ' Lifetime is 160ms (within PRD suggested 120-200ms range).
    Public NotInheritable Class ImpactEffect
        Public Property TileX As Single
        Public Property TileY As Single
        Public Property NormalX As Single
        Public Property NormalY As Single
        Public Property Speed As Single
        Public Property LifetimeMs As Single = 160.0F
        Public Property ElapsedMs As Single = 0.0F

        Public Sub New(tileX As Single, tileY As Single, normalX As Single, normalY As Single, speed As Single)
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
