Option Strict On
Option Explicit On

Imports System.Windows.Forms

Namespace Input
    ' Converts keyboard key states into tilt values matching the ITiltInput contract.
    ' Arrow keys and WASD are both supported. Diagonal input is allowed.
    ' The form calls NotifyKeyDown / NotifyKeyUp; this class does no UI work of its own.
    Public NotInheritable Class KeyboardController
        Implements ITiltInput

        Private _leftHeld  As Boolean
        Private _rightHeld As Boolean
        Private _upHeld    As Boolean
        Private _downHeld  As Boolean

        ' -1.0 = left, 0.0 = neutral, 1.0 = right
        Public ReadOnly Property TiltX As Single Implements ITiltInput.TiltX
            Get
                If _leftHeld  AndAlso Not _rightHeld Then Return -1.0F
                If _rightHeld AndAlso Not _leftHeld  Then Return  1.0F
                Return 0.0F
            End Get
        End Property

        ' -1.0 = up, 0.0 = neutral, 1.0 = down
        Public ReadOnly Property TiltY As Single Implements ITiltInput.TiltY
            Get
                If _upHeld   AndAlso Not _downHeld Then Return -1.0F
                If _downHeld AndAlso Not _upHeld   Then Return  1.0F
                Return 0.0F
            End Get
        End Property

        ' Call from the form's KeyDown event.
        Public Sub NotifyKeyDown(keyCode As Keys)
            Select Case keyCode
                Case Keys.Left,  Keys.A : _leftHeld  = True
                Case Keys.Right, Keys.D : _rightHeld = True
                Case Keys.Up,    Keys.W : _upHeld    = True
                Case Keys.Down,  Keys.S : _downHeld  = True
            End Select
        End Sub

        ' Call from the form's KeyUp event.
        Public Sub NotifyKeyUp(keyCode As Keys)
            Select Case keyCode
                Case Keys.Left,  Keys.A : _leftHeld  = False
                Case Keys.Right, Keys.D : _rightHeld = False
                Case Keys.Up,    Keys.W : _upHeld    = False
                Case Keys.Down,  Keys.S : _downHeld  = False
            End Select
        End Sub

        ' Call when the window loses focus so no key stays stuck held.
        Public Sub ReleaseAll()
            _leftHeld  = False
            _rightHeld = False
            _upHeld    = False
            _downHeld  = False
        End Sub
    End Class
End Namespace
