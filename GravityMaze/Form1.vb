Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports GravityMaze.Audio
Imports GravityMaze.Data
Imports GravityMaze.Input
Imports GravityMaze.Levels
Imports GravityMaze.UI
Imports GravityMaze.UI.Screens

' Hosts the game: one full-window canvas, the 60 FPS loop timer, and keyboard wiring.
' Everything the player sees is drawn by GameCanvas + GameShell.
Public Class Form1
    Private keyboardController As KeyboardController
    Private inputManager As InputManager
    Private sound As SoundManager
    Private canvas As GameCanvas
    Private shell As GameShell
    Private gameLoopTimer As Timer

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Text = "Gravity Maze"
        BackColor = Color.Black
        KeyPreview = True
        MinimumSize = New Size(960, 600)
        SetFullscreen(True)

        keyboardController = New KeyboardController()
        ' ponytail: swap KeyboardController for the Arduino controller here; nothing else changes.
        inputManager = New InputManager(keyboardController)
        sound = New SoundManager(Path.Combine(AppContext.BaseDirectory, "Sounds"))

        ' Par = keyboard-bot clear time (VerificationRunner); Level 3 limit = 1.4x its par.
        Dim levels As New List(Of LevelConfig) From {
            New LevelConfig(1, "Wooden Workshop", "Mazes\Level1.txt", 0, 5.4F),
            New LevelConfig(2, "Frozen Labyrinth", "Mazes\Level2.txt", 0, 25.8F),
            New LevelConfig(3, "Neon Velocity", "Mazes\Level3.txt", 40, 28.5F)
        }

        canvas = New GameCanvas With {.Dock = DockStyle.Fill}
        Controls.Add(canvas)

        Try
            shell = New GameShell(canvas, inputManager, sound, New ScoreManager(ScoreManager.DefaultPath()), levels)
        Catch ex As Exception
            MessageBox.Show(Me, "Gravity Maze could not start." & Environment.NewLine & ex.Message,
                            "Gravity Maze", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Close()
            Return
        End Try
        AddHandler shell.QuitRequested, Sub() Close()

        gameLoopTimer = New Timer With {.Interval = 16}
        AddHandler gameLoopTimer.Tick, Sub() shell.Tick()
        gameLoopTimer.Start()
    End Sub

    Private Sub SetFullscreen(fullscreen As Boolean)
        WindowState = FormWindowState.Normal
        FormBorderStyle = If(fullscreen, FormBorderStyle.None, FormBorderStyle.Sizable)
        WindowState = FormWindowState.Maximized
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        keyboardController.NotifyKeyDown(e.KeyCode)
        If e.KeyCode = Keys.F11 Then
            SetFullscreen(FormBorderStyle <> FormBorderStyle.None)
            e.Handled = True
        ElseIf shell IsNot Nothing AndAlso shell.HandleKey(e.KeyCode) Then
            e.Handled = True
        End If
        ' Arrow keys must never move focus between controls.
        Select Case e.KeyCode
            Case Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Space
                e.Handled = True
                e.SuppressKeyPress = e.KeyCode <> Keys.Space OrElse shell Is Nothing OrElse shell.Screen <> ShellScreen.NameEntry
        End Select
    End Sub

    Protected Overrides Sub OnKeyPress(e As KeyPressEventArgs)
        MyBase.OnKeyPress(e)
        shell?.HandleChar(e.KeyChar)
    End Sub

    Protected Overrides Sub OnKeyUp(e As KeyEventArgs)
        MyBase.OnKeyUp(e)
        keyboardController.NotifyKeyUp(e.KeyCode)
    End Sub

    Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
        Return True
    End Function

    Protected Overrides Sub OnDeactivate(e As EventArgs)
        MyBase.OnDeactivate(e)
        keyboardController?.ReleaseAll()
        shell?.OnDeactivated()
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        gameLoopTimer?.Stop()
        gameLoopTimer?.Dispose()
        sound?.Dispose()
        MyBase.OnFormClosed(e)
    End Sub
End Class
