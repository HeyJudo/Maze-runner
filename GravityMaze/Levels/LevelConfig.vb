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

        Public Sub New(levelNumber As Integer, themeName As String,
                       mazePath As String, timeLimitSecs As Integer)
            Me.LevelNumber   = levelNumber
            Me.ThemeName     = themeName
            Me.MazePath      = mazePath
            Me.TimeLimitSecs = timeLimitSecs
        End Sub
    End Class
End Namespace
