Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports GravityMaze.Data
Imports GravityMaze.Input

Namespace UI.Screens
    ' Who's Playing?, name de-duplication, the Profile screen and the Records filter.
    ' Every list is a MenuList, so the board and the keyboard drive them the same way.
    Partial Public NotInheritable Class GameShell
        Private ReadOnly _playerMenu As New MenuList()
        Private _playerList As New List(Of PlayerProfile)()
        Private ReadOnly _profileMenu As New MenuList("GHOST: ON", "BACK")
        Private ReadOnly _recordsMenu As New MenuList("SHOW: ALL PLAYERS", "BACK")
        Private _recordsMineOnly As Boolean
        Private _nameNotice As String = ""
        Private _pendingPlayer As PlayerProfile   ' typed name matched this profile; Enter again confirms

        Public ReadOnly Property NameNotice As String
            Get
                Return _nameNotice
            End Get
        End Property

        Public ReadOnly Property RecordsMineOnly As Boolean
            Get
                Return _recordsMineOnly
            End Get
        End Property

        ' No profiles yet: straight to typing. Otherwise list players (current first), then + NEW PLAYER and BACK.
        Private Sub OpenPlayerSelect(after As Action)
            _afterPlayerChosen = after
            _playerList = _scores.Players.Take(7).ToList()   ' ponytail: 7 most recent fit above the footer; upgrade to a scrolling list for more. Older players type their name and continue via the "already exists" prompt.
            If _playerList.Count = 0 Then OpenNameEntry() : Return
            _playerMenu.Items.Clear()
            For Each p As PlayerProfile In _playerList
                _playerMenu.Items.Add(p.Name)
            Next
            _playerMenu.Items.Add("+ NEW PLAYER")
            _playerMenu.Items.Add("BACK")
            GoTo_(ShellScreen.PlayerSelect)
            Dim key As String = ScoreManager.KeyOf(_scores.PlayerName)
            _playerMenu.Reset(Math.Max(0, _playerList.FindIndex(Function(p) p.Key = key)))
        End Sub

        Private Sub PlayerChoice(index As Integer)
            If index < _playerList.Count Then
                _scores.PlayerName = _playerList(index).Name
                ContinueAfterPlayer()
            ElseIf index = _playerList.Count Then
                OpenNameEntry()
            Else
                _afterPlayerChosen = Nothing
                GoTo_(ShellScreen.Title)
            End If
        End Sub

        ' First Enter on a name that already exists asks; the second Enter continues as that player.
        Private Function ConfirmNewName(name As String) As Boolean
            Dim existing As PlayerProfile = _scores.FindPlayer(name)
            If existing Is Nothing OrElse existing Is _pendingPlayer Then Return True
            _pendingPlayer = existing
            _nameNotice = $"{existing.Name} ALREADY EXISTS  ·  PRESS ENTER TO CONTINUE AS {existing.Name}"
            Return False
        End Function

        Private Sub ClearNameNotice()
            _nameNotice = ""
            _pendingPlayer = Nothing
        End Sub

        Private Sub OpenProfile()
            RefreshProfileLabels()
            GoTo_(ShellScreen.Profile)
        End Sub

        Private Sub RefreshProfileLabels()
            _profileMenu.Items(0) = "GHOST: " & If(_scores.GhostEnabled, "ON", "OFF")
        End Sub

        Private Sub ProfileChoice(index As Integer)
            If index = 0 Then
                _scores.GhostEnabled = Not _scores.GhostEnabled
                RefreshProfileLabels()
            Else
                GoTo_(ShellScreen.Title)
            End If
        End Sub

        Private Sub RecordsChoice(index As Integer)
            If index = 0 Then
                _recordsMineOnly = Not _recordsMineOnly
                _recordsMenu.Items(0) = "SHOW: " & If(_recordsMineOnly, "JUST ME", "ALL PLAYERS")
            Else
                Back()
            End If
        End Sub

        ' ── Calibration ─────────────────────────────────────────────────────
        ' Cycling values (confirm = next step) keeps left/right free for hold-to-confirm/back on the board.
        Private ReadOnly _calibrateMenu As New MenuList("RECENTER", "SENSITIVITY", "DEAD ZONE", "INVERT X", "INVERT Y", "SWAP AXES", "TEST TILT", "BACK")
        Private Shared ReadOnly SensitivitySteps As Single() = {10, 15, 20, 25, 30}
        Private Shared ReadOnly DeadzoneSteps As Single() = {0.04F, 0.08F, 0.12F, 0.16F}
        Private _testingTilt As Boolean
        Private _testBall As PointF
        Private _testVelocity As PointF
        Private _testExitMs As Single

        Private Sub OpenCalibration()
            _testingTilt = False
            RefreshCalibrationLabels()
            GoTo_(ShellScreen.Calibrate)
        End Sub

        Private Sub ApplyCalibration()
            Dim board As ArduinoController = _input.Board
            If board Is Nothing Then Return
            Dim c As CalibrationSettings = _scores.Calibration
            board.FullTiltDegrees = c.FullTiltDegrees
            board.Deadzone = c.Deadzone
            board.Smoothing = c.Smoothing
            board.SwapAxes = c.SwapAxes
            board.InvertX = c.InvertX
            board.InvertY = c.InvertY
        End Sub

        Private Shared Function NextStep(steps As Single(), current As Single) As Single
            For Each v As Single In steps
                If v > current + 0.001F Then Return v
            Next
            Return steps(0)
        End Function

        Private Sub RefreshCalibrationLabels()
            Dim c As CalibrationSettings = _scores.Calibration
            _calibrateMenu.Items(1) = $"SENSITIVITY: FULL TILT AT {c.FullTiltDegrees:0}°"
            _calibrateMenu.Items(2) = $"DEAD ZONE: {c.Deadzone:0.00}"
            _calibrateMenu.Items(3) = "INVERT X: " & If(c.InvertX, "ON", "OFF")
            _calibrateMenu.Items(4) = "INVERT Y: " & If(c.InvertY, "ON", "OFF")
            _calibrateMenu.Items(5) = "SWAP AXES: " & If(c.SwapAxes, "ON", "OFF")
        End Sub

        Private Sub CalibrationChoice(index As Integer)
            Dim c As CalibrationSettings = _scores.Calibration
            Select Case index
                Case 0 : _input.Board?.Calibrate() : Return
                Case 1 : c.FullTiltDegrees = NextStep(SensitivitySteps, c.FullTiltDegrees)
                Case 2 : c.Deadzone = NextStep(DeadzoneSteps, c.Deadzone)
                Case 3 : c.InvertX = Not c.InvertX
                Case 4 : c.InvertY = Not c.InvertY
                Case 5 : c.SwapAxes = Not c.SwapAxes
                Case 6
                    _testingTilt = True
                    _testBall = PointF.Empty
                    _testVelocity = PointF.Empty
                    _testExitMs = 0
                    Return
                Case Else : GoTo_(ShellScreen.Title) : Return
            End Select
            ApplyCalibration()
            _scores.SaveSettings()
            RefreshCalibrationLabels()
        End Sub

        ' TEST TILT: the menu freezes and a ball rolls in a circle with the board's tilt.
        ' Leave with Enter/Esc, or by holding the board fully left for 2 seconds.
        Private Sub TickCalibrate(tx As Single, ty As Single)
            If Not _testingTilt Then
                Select Case Nav(_calibrateMenu, tx, ty)
                    Case MenuAction.Confirm : CalibrationChoice(_calibrateMenu.Selected)
                    Case MenuAction.Back : GoTo_(ShellScreen.Title)
                End Select
                Return
            End If
            _testVelocity = New PointF((_testVelocity.X + tx * 0.01F) * 0.9F, (_testVelocity.Y + ty * 0.01F) * 0.9F)
            _testBall = New PointF(_testBall.X + _testVelocity.X, _testBall.Y + _testVelocity.Y)
            Dim d As Single = CSng(Math.Sqrt(_testBall.X * _testBall.X + _testBall.Y * _testBall.Y))
            If d > 1.0F Then
                _testBall = New PointF(_testBall.X / d, _testBall.Y / d)
                _testVelocity = PointF.Empty
            End If
            _testExitMs = If(tx <= -0.9F, _testExitMs + TickMs, 0.0F)
            If _testExitMs >= 2000.0F Then _testingTilt = False : _calibrateMenu.Reset(6)
        End Sub
    End Class
End Namespace
