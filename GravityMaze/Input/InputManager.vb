Option Strict On
Option Explicit On

Imports System

Namespace Input
    ' Routes tilt data to the Game Engine; the engine never learns the source.
    ' Keyboard and Arduino board work side by side: while any arrow/WASD key is held the keyboard
    ' wins, otherwise the board (if connected) drives. Unplugging the board just leaves the keyboard.
    Public NotInheritable Class InputManager
        Private ReadOnly _keyboard As ITiltInput
        Public ReadOnly Property Board As ArduinoController   ' Nothing = keyboard only

        Public Sub New(keyboard As ITiltInput, Optional board As ArduinoController = Nothing)
            If keyboard Is Nothing Then Throw New ArgumentNullException(NameOf(keyboard))
            _keyboard = keyboard
            Me.Board = board
        End Sub

        Private ReadOnly Property KeyboardActive As Boolean
            Get
                Return _keyboard.TiltX <> 0.0F OrElse _keyboard.TiltY <> 0.0F
            End Get
        End Property

        ' Horizontal tilt for the engine: -1.0 (left) to 1.0 (right)
        Public ReadOnly Property TiltX As Single
            Get
                Return If(KeyboardActive OrElse Board Is Nothing, _keyboard.TiltX, Board.TiltX)
            End Get
        End Property

        ' Vertical tilt for the engine: -1.0 (up) to 1.0 (down)
        Public ReadOnly Property TiltY As Single
            Get
                Return If(KeyboardActive OrElse Board Is Nothing, _keyboard.TiltY, Board.TiltY)
            End Get
        End Property
    End Class
End Namespace
