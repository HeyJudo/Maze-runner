Option Strict On
Option Explicit On

Imports System

Namespace Rendering
    ' Presentation state only: never changes the engine's input or physics.
    Public NotInheritable Class BoardTilt
        Public Const RestPitchDegrees As Double = 12.0
        Public Const MaxTiltDegrees As Double = 8.0
        Private _x As Double
        Private _y As Double

        Public ReadOnly Property X As Double
            Get
                Return _x
            End Get
        End Property

        Public ReadOnly Property Y As Double
            Get
                Return _y
            End Get
        End Property

        Public Sub Reset()
            _x = 0
            _y = 0
        End Sub

        Public Sub Advance(x As Single, y As Single, elapsedMs As Double)
            ' Exponential easing has the same response at different frame rates.
            Dim blend As Double = 1.0 - Math.Exp(-Math.Max(0, elapsedMs) / 110.0)
            _x += (ClampInput(x) - _x) * blend
            _y += (ClampInput(y) - _y) * blend
        End Sub

        Private Shared Function ClampInput(value As Single) As Double
            If Single.IsNaN(value) OrElse Single.IsInfinity(value) Then Return 0
            Return Math.Clamp(CDbl(value), -1.0, 1.0)
        End Function
    End Class
End Namespace
