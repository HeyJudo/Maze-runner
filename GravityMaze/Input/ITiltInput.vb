Option Strict On
Option Explicit On

Namespace Input
    ' Common contract for all tilt-input sources (keyboard, Arduino, etc.).
    ' The Game Engine will only ever see this interface - it never knows the source.
    ' To swap keyboard for Arduino, provide a new implementation here.
    ' No engine code changes are needed.
    Public Interface ITiltInput
        ' Horizontal tilt: -1.0 = full left, 0.0 = neutral, 1.0 = full right
        ReadOnly Property TiltX As Single
        ' Vertical tilt:   -1.0 = full up,   0.0 = neutral, 1.0 = full down
        ReadOnly Property TiltY As Single
    End Interface
End Namespace
