Option Strict On
Option Explicit On

Imports System
Imports System.Numerics

Namespace Rendering
    ' Presentation only. Integrating displacement (not input or velocity) stops rolling at walls.
    Public NotInheritable Class MarbleMotion
        Public Const Radius As Single = 0.27F
        Private _orientation As Quaternion = Quaternion.Identity
        Private _position As Vector2
        Private _hasPosition As Boolean

        Public ReadOnly Property Orientation As Quaternion
            Get
                Return _orientation
            End Get
        End Property

        Public Sub Reset()
            _orientation = Quaternion.Identity
            _hasPosition = False
        End Sub

        Public Sub Reset(x As Single, y As Single)
            Reset()
            Rebase(x, y)
        End Sub

        ' Respawns/teleports move the reference point without spinning across the whole maze.
        Public Sub Rebase(x As Single, y As Single)
            _hasPosition = Single.IsFinite(x) AndAlso Single.IsFinite(y)
            If _hasPosition Then _position = New Vector2(x, y)
        End Sub

        Public Sub Advance(x As Single, y As Single)
            If Not Single.IsFinite(x) OrElse Not Single.IsFinite(y) Then Return
            If Not _hasPosition Then
                Rebase(x, y)
                Return
            End If
            Dim nextPosition As New Vector2(x, y)
            Dim movement = nextPosition - _position
            _position = nextPosition
            Dim distance = movement.Length()
            If distance < 0.000001F Then Return
            Dim axis As New Vector3(-movement.Y / distance, movement.X / distance, 0)
            Dim turn = Quaternion.CreateFromAxisAngle(axis, distance / Radius)
            _orientation = Quaternion.Normalize(turn * _orientation)
        End Sub
    End Class
End Namespace
