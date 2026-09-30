Option Strict On
Option Explicit On

Imports System

Namespace Input
    ' Routes tilt data from any ITiltInput source to the Game Engine.
    ' The engine only calls TiltX / TiltY here - it is never told the source type.
    ' To switch from keyboard to Arduino: pass an ArduinoController to the constructor.
    ' No Game Engine code changes are needed.
    Public NotInheritable Class InputManager
        Private ReadOnly _source As ITiltInput

        Public Sub New(inputSource As ITiltInput)
            If inputSource Is Nothing Then Throw New ArgumentNullException(NameOf(inputSource))
            _source = inputSource
        End Sub

        ' Horizontal tilt for the engine: -1.0 (left) to 1.0 (right)
        Public ReadOnly Property TiltX As Single
            Get
                Return _source.TiltX
            End Get
        End Property

        ' Vertical tilt for the engine: -1.0 (up) to 1.0 (down)
        Public ReadOnly Property TiltY As Single
            Get
                Return _source.TiltY
            End Get
        End Property
    End Class
End Namespace
