Option Strict On
Option Explicit On

Namespace Engine
    ' Represents the current phase of a single level run.
    Public Enum GameState
        Playing        ' Ball is in motion; timer is counting.
        LevelComplete  ' Ball reached the goal; timer stopped.
        TimeUp         ' Optional countdown expired (no level uses one).
        OutOfHearts    ' Lost every heart before reaching the goal.
    End Enum
End Namespace
