Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports GravityMaze.Engine

Namespace UI.Screens
    Partial Public NotInheritable Class GameShell
        Public Const DungeonRevealDurationMs As Single = 2000
        Private ReadOnly _dungeonMenu As New MenuList("READY", "CAMERA: 1.5x", "RULES")
        Private ReadOnly _dungeonRulesMenu As New MenuList("BACK")
        Private _rulesPauseResumeScreen As ShellScreen
        Private _rulesPauseResumeMs As Single
        Private _rulesReturn As ShellScreen
        Private _dungeonPreferencesCaptured As Boolean
        Private _preDungeonTilt As Boolean
        Private _preDungeonZoom As Single
        Private _dungeonRevealSeen As Boolean
        Private _dungeonRevealMs As Single
        Private _revealFrom As PointF
        Private _revealGate As PointF
        Private _revealZoom As Single
        Private _explorationCheckpointTaught As Boolean
        Private _escapeCheckpointTaught As Boolean
        Private _checkpointMessage As String = ""
        Private _checkpointMessageMs As Single

        Public ReadOnly Property DungeonCheckpointMessage As String
            Get
                Return _checkpointMessage
            End Get
        End Property
        Public ReadOnly Property DungeonCheckpointRemainingMs As Single
            Get
                Return _checkpointMessageMs
            End Get
        End Property
        Public ReadOnly Property DungeonRevealElapsedMs As Single
            Get
                Return _dungeonRevealMs
            End Get
        End Property

        Private Sub CaptureDungeonPreferences()
            _preDungeonTilt = _canvas.TiltViewEnabled
            _preDungeonZoom = _canvas.CameraPreferredZoom
            _dungeonPreferencesCaptured = True
            _canvas.TiltViewEnabled = False
            _canvas.SetCameraPreferredZoom(1.5F)
            If _pauseMenu.Items.Count = 5 Then _pauseMenu.Items.Add("RULES")
            _pauseMenu.Enabled = Function(i) i <> 5 OrElse _pausedScreen <> ShellScreen.DungeonRules
            RefreshCameraLabels()
        End Sub

        Private Sub RestoreDungeonPreferences()
            If Not _dungeonPreferencesCaptured Then Return
            _canvas.ReleaseCameraPresentation()
            _canvas.PresentationOverview = False
            _canvas.CameraOverviewHeld = False
            _canvas.TiltViewEnabled = _preDungeonTilt
            _canvas.SetCameraPreferredZoom(_preDungeonZoom)
            _dungeonPreferencesCaptured = False
            If _pauseMenu.Items.Count > 5 Then _pauseMenu.Items.RemoveAt(5)
            _pauseMenu.Enabled = Function(i) True
            RefreshCameraLabels()
        End Sub

        Private Sub RefreshCameraLabels()
            _pauseMenu.Items(3) = "TILT VIEW: " & If(_canvas.TiltViewEnabled, "ON", "OFF")
            _pauseMenu.Items(4) = "CAMERA: " & _canvas.CameraLabel
            _dungeonMenu.Items(1) = "CAMERA: " & _canvas.CameraLabel
        End Sub

        Private Sub ResetDungeonPresentation()
            _canvas.ReleaseCameraPresentation()
            _canvas.PresentationOverview = False
            _dungeonRevealSeen = False
            _dungeonRevealMs = 0
            _explorationCheckpointTaught = False
            _escapeCheckpointTaught = False
            _checkpointMessage = ""
            _checkpointMessageMs = 0
        End Sub

        Private Function DungeonOverviewActive() As Boolean
            If _engine Is Nothing OrElse _engine.Dungeon Is Nothing Then Return False
            Dim state = If(_screen = ShellScreen.Paused, _pausedScreen, _screen)
            Return state = ShellScreen.DungeonBriefing OrElse state = ShellScreen.DungeonRules
        End Function

        Private Sub DungeonBriefingChoice(index As Integer)
            Select Case index
                Case 0
                    _skipCard = True
                    _canvas.PresentationOverview = False
                    GoTo_(ShellScreen.Intro)
                Case 1 : CycleCameraZoom()
                Case 2 : OpenDungeonRules(ShellScreen.DungeonBriefing)
            End Select
        End Sub

        Private Sub OpenDungeonRules(returnTo As ShellScreen)
            If returnTo = ShellScreen.Paused AndAlso _pausedScreen = ShellScreen.DungeonRules Then Return
            _rulesReturn = returnTo
            If returnTo = ShellScreen.Paused Then
                _rulesPauseResumeScreen = _pausedScreen
                _rulesPauseResumeMs = _pausedScreenMs
            End If
            GoTo_(ShellScreen.DungeonRules)
        End Sub

        Private Sub ReturnFromDungeonRules()
            If _rulesReturn = ShellScreen.Paused Then
                _pausedScreen = _rulesPauseResumeScreen
                _pausedScreenMs = _rulesPauseResumeMs
            End If
            GoTo_(_rulesReturn)
            If _rulesReturn = ShellScreen.Paused AndAlso _pausedScreen = ShellScreen.DungeonReveal Then ApplyDungeonRevealFrame()
        End Sub

        Private Sub ShowDungeonCheckpoint()
            Dim escaping = _engine.Dungeon.Collapse IsNot Nothing
            Dim taught = If(escaping, _escapeCheckpointTaught, _explorationCheckpointTaught)
            _checkpointMessage = If(taught, "Checkpoint lit", "Checkpoint lit — falls return you here; collected keys stay.")
            _checkpointMessageMs = If(taught, 2000, 4000)
            If escaping Then _escapeCheckpointTaught = True Else _explorationCheckpointTaught = True
        End Sub

        Private Sub TickDungeonMessages()
            _checkpointMessageMs = Math.Max(0, _checkpointMessageMs - TickMs)
            If _checkpointMessageMs = 0 Then _checkpointMessage = ""
        End Sub

        Private Function TryBeginDungeonReveal() As Boolean
            If _engine.Dungeon Is Nothing OrElse _dungeonRevealSeen OrElse
               (_engine.Dungeon.KeyMask And 3) <> 3 OrElse _engine.Dungeon.Collapse IsNot Nothing Then Return False
            _dungeonRevealSeen = True
            _dungeonRevealMs = 0
            Dim maze = _engine.Maze
            _revealFrom = New PointF(_canvas.CameraFocus.X * maze.ColumnCount, _canvas.CameraFocus.Y * maze.RowCount)
            _revealZoom = _canvas.CameraZoom
            Dim x As Single = 0, y As Single = 0, count As Integer = 0
            For r As Integer = 0 To maze.RowCount - 1
                For c As Integer = 0 To maze.ColumnCount - 1
                    If maze.GetTile(r, c) <> "c"c Then Continue For
                    x += c + 0.5F : y += r + 0.5F : count += 1
                Next
            Next
            _revealGate = If(count > 0, New PointF(x / count, y / count), New PointF(_engine.BallX, _engine.BallY))
            _canvas.FreezeBallPosition(_engine.BallX, _engine.BallY)
            GoTo_(ShellScreen.DungeonReveal)
            ApplyDungeonRevealFrame()
            Return True
        End Function

        Private Sub TickDungeonReveal()
            _dungeonRevealMs = Math.Min(DungeonRevealDurationMs, _dungeonRevealMs + TickMs)
            ApplyDungeonRevealFrame()
            If _dungeonRevealMs < DungeonRevealDurationMs Then Return
            _canvas.ReleaseCameraPresentation()
            GoTo_(ShellScreen.Playing)
        End Sub

        Private Sub ApplyDungeonRevealFrame()
            ' 500 ms approach, 1000 ms clear hold, 500 ms return. No physics or timers tick.
            Dim target As PointF = _revealGate
            Dim zoom As Single = 2
            If _dungeonRevealMs < 500 Then
                Dim blend = Smooth(_dungeonRevealMs / 500)
                target = Lerp(_revealFrom, _revealGate, blend)
                zoom = _revealZoom + (2 - _revealZoom) * blend
            ElseIf _dungeonRevealMs > 1500 Then
                Dim blend = Smooth((_dungeonRevealMs - 1500) / 500)
                target = Lerp(_revealGate, New PointF(_engine.BallX, _engine.BallY), blend)
                zoom = 2 + (_canvas.CameraPreferredZoom - 2) * blend
            End If
            _canvas.SetCameraPresentation(target.X, target.Y, zoom)
        End Sub

        Private Shared Function Smooth(t As Single) As Single
            Return t * t * (3 - 2 * t)
        End Function
        Private Shared Function Lerp(a As PointF, b As PointF, t As Single) As PointF
            Return New PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t)
        End Function
    End Class
End Namespace
