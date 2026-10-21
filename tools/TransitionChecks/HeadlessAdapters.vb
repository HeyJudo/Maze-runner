Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Collections.Generic
Imports GravityMaze.Levels
Imports GravityMaze.Engine
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
        G
        F8
        F9
        Tab
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
        Public Property DungeonState As DungeonRun
        Public Property PickupTaken As Func(Of Integer, Integer, Boolean)
        Public Property BallBlink As Boolean
        Public Property BarrierStanding As Func(Of Integer, Integer, Boolean)
        Public Property BarrierCharge As Single
        Public Property GhostPosition As PointF?
        Private ReadOnly Camera As New BoardCamera()
        Public Property CameraEnabled As Boolean
        Public Property CameraOverviewHeld As Boolean
        Public Property PresentationOverview As Boolean
        Private _maze As MazeDefinition
        Private _ball As PointF
        Private _presentation As Boolean
        Public ReadOnly Property CameraPreferredZoom As Single
            Get
                Return Camera.PreferredZoom
            End Get
        End Property
        Public ReadOnly Property CameraZoom As Single
            Get
                Return Camera.Zoom
            End Get
        End Property
        Public ReadOnly Property CameraFocus As PointF
            Get
                Return Camera.Focus
            End Get
        End Property
        Public Sub SetCameraPreferredZoom(value As Single)
            Camera.SetPreferredZoom(value)
        End Sub
        Public Sub SetCameraPresentation(x As Single, y As Single, zoom As Single)
            _presentation = True
            Camera.SetPresentation(x / _maze.ColumnCount, y / _maze.RowCount, zoom)
        End Sub
        Public Sub FreezeBallPosition(x As Single, y As Single)
            _ball = New PointF(x, y)
        End Sub
        Public Sub ReleaseCameraPresentation()
            _presentation = False
        End Sub
        Public ReadOnly Property CameraLabel As String
            Get
                Return Camera.Label
            End Get
        End Property
        Public Sub CycleCameraZoom()
            Camera.CycleZoom()
        End Sub
        Public Sub UpdateCamera(elapsedMs As Single)
            If _maze Is Nothing Then Return
            If PresentationOverview OrElse (_presentation AndAlso CameraOverviewHeld) Then
                Camera.SetPresentation(0.5F, 0.5F, 1)
                Return
            End If
            If _presentation Then Return
            Camera.Advance(_ball.X / _maze.ColumnCount, _ball.Y / _maze.RowCount, elapsedMs, CameraEnabled, CameraOverviewHeld OrElse PresentationOverview)
        End Sub
        Public Property TiltViewEnabled As Boolean = True
        Public Property Transition As GoalDropTransition
        Public Property Incoming As MazeDefinition
        Public Property Invalidations As Integer
        Public Sub Invalidate()
            Invalidations += 1
        End Sub
        Public Sub ShowMaze(maze As MazeDefinition, theme As String)
            ReleaseCameraPresentation()
            _maze = maze
            _ball = New PointF(maze.StartColumn + 0.5F, maze.StartRow + 0.5F)
            ClearEffects()
            Camera.Recenter(_ball.X / maze.ColumnCount, _ball.Y / maze.RowCount)
        End Sub
        Public Sub ClearEffects()
            EndGoalDrop()
            Camera.ResetTracking()
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
            _ball = New PointF(x, y)
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
        Public Sub Shake(durationMs As Single, amplitude As Single)
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
