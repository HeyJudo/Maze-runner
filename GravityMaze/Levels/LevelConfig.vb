Option Strict On
Option Explicit On

Imports System
Imports GravityMaze.Levels

Namespace Levels
    ' Immutable descriptor for one level.
    ' Keeps theme name, maze file path, and optional countdown timer separate
    ' from game-engine physics so each concern stays in its own layer.
    Public NotInheritable Class LevelConfig
        Public ReadOnly Property LevelNumber   As Integer   ' 1-based display number
        Public ReadOnly Property ThemeName     As String    ' e.g. "Wooden Workshop"
        Public ReadOnly Property MazePath      As String    ' relative to AppContext.BaseDirectory
        Public ReadOnly Property TimeLimitSecs As Integer   ' 0 = unlimited
        Public ReadOnly Property ParSecs       As Single    ' target clear time; stars are earned against it

        Public Sub New(levelNumber As Integer, themeName As String,
                       mazePath As String, timeLimitSecs As Integer, parSecs As Single)
            Me.LevelNumber   = levelNumber
            Me.ThemeName     = themeName
            Me.MazePath      = mazePath
            Me.TimeLimitSecs = timeLimitSecs
            Me.ParSecs       = parSecs
        End Sub

        ' 3 stars within 1.3x par, 2 within 1.8x par, 1 for any finish.
        Public Function StarsFor(timeSecs As Single) As Integer
            If timeSecs <= ParSecs * 1.3F Then Return 3
            If timeSecs <= ParSecs * 1.8F Then Return 2
            Return 1
        End Function
    End Class
End Namespace
