Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Reflection
Imports GravityMaze.Audio
Imports GravityMaze.Data
Imports GravityMaze.Engine
Imports GravityMaze.Input
Imports GravityMaze.Levels
Imports GravityMaze.UI
Imports GravityMaze.UI.Screens

Namespace VerificationRunner
    Friend NotInheritable Class DungeonFrameFixture
        Implements IDisposable

        Friend ReadOnly Property Canvas As GameCanvas
        Private ReadOnly sound As SoundManager
        Private ReadOnly shell As GameShell

        Private NotInheritable Class NeutralTilt
            Implements ITiltInput

            Public ReadOnly Property TiltX As Single Implements ITiltInput.TiltX
                Get
                    Return 0
                End Get
            End Property
            Public ReadOnly Property TiltY As Single Implements ITiltInput.TiltY
                Get
                    Return 0
                End Get
            End Property
        End Class

        Friend Sub New(state As Integer, size As Size, tilted As Boolean)
            Canvas = New GameCanvas With {.Size = size}
            sound = New SoundManager(Nothing)
            Dim levels As New List(Of LevelConfig) From {
                New LevelConfig(1, "Wooden Workshop", "Mazes/Level1.txt", 0, 7.3F),
                New LevelConfig(2, "Frozen Labyrinth", "Mazes/Level2.txt", 0, 36.9F),
                New LevelConfig(3, "Neon Velocity", "Mazes/Level3.txt", 57, 40.2F),
                New LevelConfig(4, "Forgotten Keep", "Mazes/Level4.txt", 0, 180.0F)}
            ' Never read or write the player's records. The shell constructor installs its real Paint
            ' delegate, so hearts, timer, keys, objective, warnings and cue messages use production code.
            Dim records = Path.Combine(Path.GetTempPath(), $"gm_frame_{Guid.NewGuid():N}.xml")
            shell = New GameShell(Canvas, New InputManager(New NeutralTilt()), sound, New ScoreManager(records), levels)
            InvokeShell("StartLevel", 3, False, True)
            InvokeShell("GoTo_", ShellScreen.Playing)
            Dim run = DungeonTour.CreateState(shell.Engine.Maze, state, shell.Engine.Dungeon)
            ' Presentation fixture: synchronize displayed engine clock/ball with the asserted runtime
            ' state without ticking physics, countdown or shell message lifetimes during measurements.
            SetEngineField("_elapsedMs", CSng(run.ElapsedMs))
            SetEngineField("_ballX", run.MarbleX)
            SetEngineField("_ballY", run.MarbleY)
            InvokeShell("UpdateInsets")
            Canvas.TiltViewEnabled = tilted
            For cycle As Integer = 1 To 3
                If Canvas.CameraLabel = "FULL MAZE" Then Exit For
                Canvas.CycleCameraZoom()
            Next
            If Canvas.CameraLabel <> "FULL MAZE" Then Throw New Exception("Full-frame fixture must start at full maze zoom")
            Canvas.UpdateBoardTilt(0.7F, -0.8F, 600)
            Canvas.UpdateBallPosition(run.MarbleX, run.MarbleY)
        End Sub

        Private Sub InvokeShell(name As String, ParamArray args As Object())
            Dim method = GetType(GameShell).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic)
            If method Is Nothing Then Throw New Exception($"Missing shell fixture entry point: {name}")
            method.Invoke(shell, args)
        End Sub

        Private Sub SetEngineField(name As String, value As Single)
            Dim field = GetType(GameEngine).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic)
            If field Is Nothing Then Throw New Exception($"Missing engine presentation field: {name}")
            field.SetValue(shell.Engine, value)
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Canvas.Dispose()
            sound.Dispose()
        End Sub
    End Class
End Namespace
