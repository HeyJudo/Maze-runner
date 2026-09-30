Option Strict On
Option Explicit On

Namespace Engine
    ' Represents the current phase of a single level run.
    Public Enum GameState
        Playing        ' Ball is in motion; timer is counting.
        LevelComplete  ' Ball reached the goal; timer stopped.
        TimeUp         ' Countdown expired before reaching the goal (Level 3).
    End Enum
End Namespace
