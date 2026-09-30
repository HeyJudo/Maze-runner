Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic

Namespace UI.Screens
    Public Enum MenuAction
        None
        Moved
        Confirm
        Back
    End Enum

    ' A vertical menu driven only by tilt values, so the keyboard (arrows produce tilt) and the
    ' Arduino board navigate it the same way. Tilt up/down moves; holding tilt right confirms,
    ' holding tilt left goes back (the hold shows as a fill bar). Enter/Esc are instant shortcuts.
    Public NotInheritable Class MenuList
        Public ReadOnly Items As New List(Of String)()
        Public Property Selected As Integer
        Public Property Enabled As Func(Of Integer, Boolean) = Function(i) True

        ' ponytail: fixed thresholds; expose as settings if the Arduino's resting noise trips them.
        Private Const MoveThreshold As Single = 0.5F
        Private Const HoldThreshold As Single = 0.6F
        Private Const HoldMs As Single = 900.0F
        Private Const FirstRepeatMs As Single = 380.0F
        Private Const RepeatMs As Single = 170.0F

        Private _moveHeldMs As Single = -1.0F
        Private _nextRepeatMs As Single
        Private _confirmMs As Single
        Private _backMs As Single
        Private _armed As Boolean   ' ignore a tilt that was already held when the menu appeared

        Public Sub New(ParamArray labels As String())
            Items.AddRange(labels)
        End Sub

        ' 0..1 progress of a hold-to-confirm / hold-to-back, for drawing the fill bar.
        Public ReadOnly Property ConfirmProgress As Single
            Get
                Return Math.Min(1.0F, _confirmMs / HoldMs)
            End Get
        End Property

        Public ReadOnly Property BackProgress As Single
            Get
                Return Math.Min(1.0F, _backMs / HoldMs)
            End Get
        End Property

        Public Sub Reset(Optional selectedIndex As Integer = 0)
            Selected = selectedIndex
            _moveHeldMs = -1.0F
            _confirmMs = 0.0F
            _backMs = 0.0F
            _armed = False
        End Sub

        Public Function Update(tiltX As Single, tiltY As Single, dtMs As Single) As MenuAction
            If Not _armed Then
                If Math.Abs(tiltX) < 0.3F AndAlso Math.Abs(tiltY) < 0.3F Then _armed = True
                Return MenuAction.None
            End If

            ' Vertical movement with key-repeat style timing.
            Dim result As MenuAction = MenuAction.None
            If Math.Abs(tiltY) >= MoveThreshold AndAlso Math.Abs(tiltY) >= Math.Abs(tiltX) Then
                If _moveHeldMs < 0 Then
                    _moveHeldMs = 0
                    _nextRepeatMs = FirstRepeatMs
                    If Move(Math.Sign(tiltY)) Then result = MenuAction.Moved
                Else
                    _moveHeldMs += dtMs
                    If _moveHeldMs >= _nextRepeatMs Then
                        _nextRepeatMs += RepeatMs
                        If Move(Math.Sign(tiltY)) Then result = MenuAction.Moved
                    End If
                End If
            Else
                _moveHeldMs = -1.0F
            End If

            ' Hold right = confirm, hold left = back.
            _confirmMs = If(tiltX >= HoldThreshold, _confirmMs + dtMs, 0.0F)
            _backMs = If(tiltX <= -HoldThreshold, _backMs + dtMs, 0.0F)
            If _confirmMs >= HoldMs AndAlso Enabled(Selected) Then
                _confirmMs = 0
                _armed = False
                Return MenuAction.Confirm
            End If
            If _backMs >= HoldMs Then
                _backMs = 0
                _armed = False
                Return MenuAction.Back
            End If
            Return result
        End Function

        Private Function Move(direction As Integer) As Boolean
            If Items.Count = 0 Then Return False
            Dim i As Integer = Selected
            For n As Integer = 1 To Items.Count
                i = (i + direction + Items.Count) Mod Items.Count
                If Enabled(i) Then
                    Selected = i
                    Return True
                End If
            Next
            Return False
        End Function
    End Class
End Namespace
