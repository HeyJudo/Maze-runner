Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Collections.Generic
Imports GravityMaze.Levels
Imports GravityMaze.Rendering

' Test doubles for Windows presentation and serial/audio devices only.
' The production shell, engine, menus, pilot, input routing, and score store are linked unchanged.
Namespace Global.System.Windows.Forms
    Public Enum Keys
        Enter
        Space
        Escape
        P
        R
        M
        F8
        Back
    End Enum
    Public Structure Padding
        Public Left, Top, Right, Bottom As Integer
        Public Sub New(left As Integer, top As Integer, right As Integer, bottom As Integer)
            Me.Left = left : Me.Top = top : Me.Right = right : Me.Bottom = bottom
        End Sub
        Public Shared Operator =(a As Padding, b As Padding) As Boolean
            Return a.Left = b.Left AndAlso a.Top = b.Top AndAlso a.Right = b.Right AndAlso a.Bottom = b.Bottom
        End Operator
        Public Shared Operator <>(a As Padding, b As Padding) As Boolean
            Return Not a = b
        End Operator
    End Structure
End Namespace

Namespace Input
    Public Class ArduinoController
        Public Property TiltX As Single
        Public Property TiltY As Single
        Public Property IsConnected As Boolean
        Public Property IsCalibrating As Boolean
        Public Property ConnectedPort As String = "TEST"
    End Class
End Namespace

Namespace Audio
    Public Class SoundManager
        Public Property Muted As Boolean
        Public ReadOnly Played As New List(Of String)()
        Public Sub Play(name As String, Optional volume As Single = 1)
            Played.Add(name)
        End Sub
        Public Sub StartLoop(name As String, volume As Single)
        End Sub
        Public Sub StopLoop(name As String)
        End Sub
        Public Sub SetLoopVolume(name As String, volume As Single)
        End Sub
    End Class
End Namespace

Namespace UI
    Public Class GameCanvas
        Public Property ClientSize As Size = New Size(1600, 900)
        Public Property BoardInsets As System.Windows.Forms.Padding
        Public Property BackColor As Color
        Public Property OverlayPainter As Action(Of Object, Rectangle)
        Public Property PickupTaken As Func(Of Integer, Integer, Boolean)
        Public Property BallBlink As Boolean
        Public Property TiltViewEnabled As Boolean = True
        Public Property Transition As GoalDropTransition
        Public Property Incoming As MazeDefinition
        Public Property Invalidations As Integer
        Public Sub Invalidate()
            Invalidations += 1
        End Sub
        Public Sub ShowMaze(maze As MazeDefinition, theme As String)
            ClearEffects()
        End Sub
        Public Sub ClearEffects()
            EndGoalDrop()
        End Sub
        Public Sub BeginGoalDrop(clock As GoalDropTransition, maze As MazeDefinition, theme As String)
            Transition = clock : Incoming = maze
        End Sub
        Public Sub EndGoalDrop()
            Transition = Nothing : Incoming = Nothing
        End Sub
        Public Sub UpdateBoardTilt(x As Single, y As Single, ms As Single)
        End Sub
        Public Sub UpdateBallPosition(x As Single, y As Single)
        End Sub
        Public Sub UpdateBallMotion(x As Single, y As Single, tile As Char)
        End Sub
        Public Sub TriggerGoalCelebration(x As Single, y As Single)
        End Sub
        Public Sub AddImpact(x As Single, y As Single, nx As Single, ny As Single, speed As Single)
        End Sub
        Public Sub AddHoleFall(x As Single, y As Single)
        End Sub
        Public Sub AddHeartBurst(x As Single, y As Single)
        End Sub
        Public Sub FlashDamage()
        End Sub
    End Class
End Namespace

Namespace UI.Screens
    Public Class ThemePalette
        Public Property Backdrop As Color = Color.Black
        Public Shared Function ForTheme(theme As String) As ThemePalette
            Return New ThemePalette()
        End Function
    End Class
    Partial Public NotInheritable Class GameShell
        Private Sub Paint(g As Object, bounds As Rectangle)
        End Sub
    End Class
End Namespace
