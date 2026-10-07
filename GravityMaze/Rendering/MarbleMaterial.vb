Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Numerics

Namespace Rendering
    ' A small CPU sphere shader shared by the desktop renderer and cross-platform checks.
    ' Texture belongs to the rotating sphere; lighting belongs to the environment.
    Public NotInheritable Class MarbleMaterial
        Private Sub New()
        End Sub

        Public Shared Function Shade(x As Single, y As Single, orientation As Quaternion,
                                     tiltX As Double, tiltY As Double, theme As String,
                                     Optional darkness As Single = 0) As Color
            Dim squared = x * x + y * y
            If squared > 1 Then Return Color.Transparent
            Dim normal As New Vector3(x, y, CSng(Math.Sqrt(1 - squared)))
            Dim local = Vector3.Transform(normal, Quaternion.Conjugate(orientation))
            ' Brushing plus a broad, very faint machining mark makes the roll legible.
            Dim grain = Math.Sin(local.Y * 68 + local.X * 9) * 2.2
            Dim marking = Math.Sin(Math.Atan2(local.X, local.Z) * 5 + local.Y * 4) * 7
            Dim light = Vector3.Normalize(New Vector3(-0.38F + CSng(tiltX * 0.06),
                                                      -0.48F + CSng(tiltY * 0.06), 0.82F))
            Dim facing = Math.Max(0, Vector3.Dot(normal, light))
            Dim specular = Math.Pow(facing, 42) * 90
            Dim horizon = Math.Exp(-Math.Pow((y - 0.15 - tiltY * 0.10) / 0.16, 2)) * 48
            Dim silver = 72 + 139 * facing - horizon + specular + grain + marking
            Dim tint = If(theme = "Frozen Labyrinth", New Vector3(135, 208, 255),
                          If(theme = "Neon Velocity", New Vector3(150, 128, 255),
                          If(theme = "Forgotten Keep", New Vector3(235, 154, 96), New Vector3(236, 179, 112))))
            Dim reflection = (0.10 + 0.22 * (1 - normal.Z)) * Math.Clamp(0.65 + y * 0.35 + tiltX * x * 0.15, 0, 1)
            Dim dimming = 1 - Math.Clamp(darkness, 0, 1) * 0.90
            Return Color.FromArgb(Channel((silver * (1 - reflection) + tint.X * reflection) * dimming),
                                  Channel((silver * (1 - reflection) + tint.Y * reflection) * dimming),
                                  Channel((silver * (1 - reflection) + tint.Z * reflection) * dimming))
        End Function

        Private Shared Function Channel(value As Double) As Integer
            Return CInt(Math.Clamp(value, 0, 255))
        End Function

        Public Shared Function ShadowSpread(lift As Single) As Single
            Return 1 + Math.Clamp(lift, 0, 3) * 0.30F
        End Function

        Public Shared Function ShadowAlpha(lift As Single) As Integer
            Return CInt(105 / (1 + Math.Clamp(lift, 0, 3) * 0.85F))
        End Function
    End Class
End Namespace
