Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports System.Collections.Generic
Imports GravityMaze.Engine
Imports GravityMaze.Input
Imports GravityMaze.Levels
Imports GravityMaze.UI

Public Class Form1
    ' Input layer -----------------------------------------------------------
    Private keyboardController As KeyboardController
    Private inputManager As InputManager

    ' Physics / game state layer --------------------------------------------
    Private engine As GameEngine

    ' Level progression -----------------------------------------------------
    Private levels As List(Of LevelConfig)
    Private currentLevelIndex As Integer = 0

    ' UI references ---------------------------------------------------------
    Private layoutPanel       As TableLayoutPanel
    Private canvas            As GameCanvas
    Private titleLabel        As Label
    Private levelLabel        As Label
    Private inputStatusLabel  As Label
    Private legendLabel       As Label
    Private gameLoopTimer     As Timer

    ' Level Complete Modal Panel -------------------------------------------
    Private completionPanel      As Panel
    Private completionTitleLabel As Label
    Private completionStatsLabel As Label
    Private btnContinue          As Button
    Private btnRetry             As Button

    ' Setup -----------------------------------------------------------------
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Text = "Gravity Maze"
        BackColor = Color.FromArgb(27, 24, 22)
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(700, 560)
        KeyPreview = True

        keyboardController = New KeyboardController()
        inputManager = New InputManager(keyboardController)

        ' Define the levels matching the PRD specifications
        levels = New List(Of LevelConfig) From {
            New LevelConfig(1, "Wooden Workshop", "Mazes\Level1.txt", 0),
            New LevelConfig(2, "Frozen Labyrinth", "Mazes\Level2.txt", 0),
            New LevelConfig(3, "Neon Velocity",   "Mazes\Level3.txt", 30)
        }

        ' Main Layout -------------------------------------------------------
        layoutPanel = New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 5,
            .Padding = New Padding(16),
            .BackColor = BackColor
        }
        layoutPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layoutPanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        layoutPanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        layoutPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layoutPanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        layoutPanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        titleLabel = New Label With {
            .Text = "GRAVITY MAZE",
            .AutoSize = True,
            .Font = New Font("Segoe UI", 13.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(240, 222, 192),
            .Margin = New Padding(0, 0, 0, 4)
        }
        levelLabel = New Label With {
            .Text = "01 / Wooden Workshop",
            .AutoSize = True,
            .Font = New Font("Segoe UI", 11.0F, FontStyle.Regular),
            .ForeColor = Color.FromArgb(183, 162, 135),
            .Margin = New Padding(0, 0, 0, 8)
        }
        canvas = New GameCanvas With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0)
        }
        legendLabel = New Label With {
            .Text = "START: silver marble     GOAL: green portal     R = restart",
            .AutoSize = True,
            .Font = New Font("Segoe UI", 9.5F, FontStyle.Regular),
            .ForeColor = Color.FromArgb(218, 207, 190),
            .Margin = New Padding(0, 8, 0, 4)
        }
        inputStatusLabel = New Label With {
            .Text = "Controls: Arrow keys or WASD  |  Time: 0:00  |  Attempt: 1",
            .AutoSize = True,
            .Font = New Font("Segoe UI", 9.5F, FontStyle.Regular),
            .ForeColor = Color.FromArgb(120, 200, 120),
            .Margin = New Padding(0, 0, 0, 0)
        }

        layoutPanel.Controls.Add(titleLabel,       0, 0)
        layoutPanel.Controls.Add(levelLabel,       0, 1)
        layoutPanel.Controls.Add(canvas,           0, 2)
        layoutPanel.Controls.Add(legendLabel,      0, 3)
        layoutPanel.Controls.Add(inputStatusLabel, 0, 4)
        Controls.Add(layoutPanel)

        ' Build Level Complete Dialog Panel ---------------------------------
        BuildCompletionPanel()

        ' Load first level --------------------------------------------------
        LoadLevel(0)

        ' Game loop timer (16 ms ~ 60 FPS) ----------------------------------
        gameLoopTimer = New Timer With {.Interval = 16}
        AddHandler gameLoopTimer.Tick, AddressOf GameLoop_Tick
        gameLoopTimer.Start()
    End Sub

    Private Sub BuildCompletionPanel()
        completionPanel = New Panel With {
            .Size = New Size(360, 220),
            .BackColor = Color.FromArgb(16, 26, 42),
            .Visible = False,
            .Padding = New Padding(20)
        }
        AddHandler completionPanel.Paint, Sub(s As Object, pe As PaintEventArgs)
            Using borderPen As New Pen(Color.FromArgb(70, 160, 230), 2.0F)
                pe.Graphics.DrawRectangle(borderPen, 1, 1, completionPanel.Width - 2, completionPanel.Height - 2)
            End Using
        End Sub

        completionTitleLabel = New Label With {
            .Text = "LEVEL COMPLETE!",
            .Font = New Font("Segoe UI", 16.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(115, 250, 180),
            .Dock = DockStyle.Top,
            .Height = 44,
            .TextAlign = ContentAlignment.MiddleCenter
        }

        completionStatsLabel = New Label With {
            .Text = "Time: 00:24" & vbCrLf & "Attempts: 1",
            .Font = New Font("Segoe UI", 11.5F, FontStyle.Regular),
            .ForeColor = Color.FromArgb(220, 238, 255),
            .Dock = DockStyle.Top,
            .Height = 65,
            .TextAlign = ContentAlignment.MiddleCenter
        }

        Dim buttonContainer As New Panel With {
            .Dock = DockStyle.Bottom,
            .Height = 52
        }

        btnContinue = New Button With {
            .Text = "Continue",
            .Size = New Size(140, 42),
            .Location = New Point(15, 5),
            .BackColor = Color.FromArgb(46, 175, 100),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 10.5F, FontStyle.Bold),
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        btnContinue.FlatAppearance.BorderSize = 0
        AddHandler btnContinue.Click, AddressOf OnContinueClicked

        btnRetry = New Button With {
            .Text = "Retry",
            .Size = New Size(140, 42),
            .Location = New Point(165, 5),
            .BackColor = Color.FromArgb(45, 80, 125),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 10.5F, FontStyle.Bold),
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        btnRetry.FlatAppearance.BorderSize = 0
        AddHandler btnRetry.Click, AddressOf OnRetryClicked

        buttonContainer.Controls.Add(btnContinue)
        buttonContainer.Controls.Add(btnRetry)

        completionPanel.Controls.Add(buttonContainer)
        completionPanel.Controls.Add(completionStatsLabel)
        completionPanel.Controls.Add(completionTitleLabel)

        Controls.Add(completionPanel)
        completionPanel.BringToFront()
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        PositionCompletionPanel()
    End Sub

    Private Sub PositionCompletionPanel()
        If completionPanel Is Nothing OrElse canvas Is Nothing Then Return
        Dim canvasScreen As Rectangle = canvas.RectangleToScreen(canvas.ClientRectangle)
        Dim clientTopLeft As Point = PointToClient(canvasScreen.Location)
        Dim cx As Integer = clientTopLeft.X + (canvasScreen.Width - completionPanel.Width) \ 2
        Dim cy As Integer = clientTopLeft.Y + (canvasScreen.Height - completionPanel.Height) \ 2
        completionPanel.Location = New Point(Math.Max(10, cx), Math.Max(10, cy))
    End Sub

    Private Sub LoadLevel(index As Integer)
        If index < 0 OrElse index >= levels.Count Then Return
        currentLevelIndex = index
        Dim config As LevelConfig = levels(index)

        Text = $"Gravity Maze | {config.ThemeName}"

        If engine IsNot Nothing Then
            RemoveHandler engine.WallImpacted, AddressOf Engine_WallImpacted
            RemoveHandler engine.LevelCompleted, AddressOf Engine_LevelCompleted
        End If

        Try
            Dim levelPath As String = Path.Combine(AppContext.BaseDirectory, config.MazePath)
            Dim maze As MazeDefinition = MazeManager.LoadFromFile(levelPath)
            
            canvas.ShowMaze(maze, config.ThemeName)
            engine = New GameEngine(maze, config.TimeLimitSecs)
            
            AddHandler engine.WallImpacted, AddressOf Engine_WallImpacted
            AddHandler engine.LevelCompleted, AddressOf Engine_LevelCompleted

            ApplyTheme(config.ThemeName)
            If completionPanel IsNot Nothing Then completionPanel.Visible = False
        Catch ex As Exception
            ShowLevelError(ex.Message)
        End Try
    End Sub

    Private Sub ApplyTheme(themeName As String)
        If themeName = "Frozen Labyrinth" Then
            ' Coordinated dark navy background and cool ice-blue HUD
            Dim navyBg As Color = Color.FromArgb(10, 18, 30)
            BackColor = navyBg
            layoutPanel.BackColor = navyBg
            canvas.BackColor = navyBg
            titleLabel.ForeColor = Color.FromArgb(215, 238, 255)
            levelLabel.ForeColor = Color.FromArgb(145, 198, 245)
            legendLabel.ForeColor = Color.FromArgb(170, 210, 235)
            inputStatusLabel.ForeColor = Color.FromArgb(120, 220, 210)
        ElseIf themeName = "Neon Velocity" Then
            Dim cyberBg As Color = Color.FromArgb(12, 10, 24)
            BackColor = cyberBg
            layoutPanel.BackColor = cyberBg
            canvas.BackColor = cyberBg
            titleLabel.ForeColor = Color.FromArgb(255, 110, 220)
            levelLabel.ForeColor = Color.FromArgb(90, 220, 255)
            legendLabel.ForeColor = Color.FromArgb(220, 180, 255)
            inputStatusLabel.ForeColor = Color.FromArgb(120, 255, 180)
        Else
            ' Wooden Workshop
            Dim woodBg As Color = Color.FromArgb(27, 24, 22)
            BackColor = woodBg
            layoutPanel.BackColor = woodBg
            canvas.BackColor = woodBg
            titleLabel.ForeColor = Color.FromArgb(240, 222, 192)
            levelLabel.ForeColor = Color.FromArgb(183, 162, 135)
            legendLabel.ForeColor = Color.FromArgb(218, 207, 190)
            inputStatusLabel.ForeColor = Color.FromArgb(120, 200, 120)
        End If
    End Sub

    ' ── Event Handlers from GameEngine ─────────────────────────────────────
    Private Sub Engine_WallImpacted(sender As Object, e As WallImpactEventArgs)
        canvas.AddImpact(e.ImpactX, e.ImpactY, e.NormalX, e.NormalY, e.Speed)
    End Sub

    Private Sub Engine_LevelCompleted(sender As Object, e As EventArgs)
        canvas.TriggerGoalCelebration(engine.BallX, engine.BallY)
        ShowCompletionPanel()
    End Sub

    Private Sub ShowCompletionPanel()
        Dim totalSecs As Integer = CInt(Math.Floor(engine.ElapsedSeconds))
        Dim mins As Integer = totalSecs \ 60
        Dim secs As Integer = totalSecs Mod 60
        Dim timeStr As String = $"{mins:D2}:{secs:D2}"

        completionStatsLabel.Text = $"Time: {timeStr}    |    Attempts: {engine.Attempts}" & vbCrLf &
                                    "Press Continue or ENTER for next level"
        PositionCompletionPanel()
        completionPanel.Visible = True
        completionPanel.BringToFront()
        btnContinue.Focus()
    End Sub

    Private Sub OnContinueClicked(Optional sender As Object = Nothing, Optional e As EventArgs = Nothing)
        If currentLevelIndex < levels.Count - 1 Then
            LoadLevel(currentLevelIndex + 1)
        Else
            MessageBox.Show(Me, "Congratulations! You have completed all levels of Gravity Maze!",
                            "Gravity Maze", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Private Sub OnRetryClicked(Optional sender As Object = Nothing, Optional e As EventArgs = Nothing)
        If engine IsNot Nothing Then
            engine.Reset()
            canvas.ClearEffects()
            canvas.UpdateBallPosition(engine.BallX, engine.BallY)
            If completionPanel IsNot Nothing Then completionPanel.Visible = False
        End If
    End Sub

    ' ── Keyboard events ────────────────────────────────────────────────────
    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        keyboardController.NotifyKeyDown(e.KeyCode)

        If engine Is Nothing Then Return

        ' If Level Complete panel is visible, handle shortcuts
        If engine.State = GameState.LevelComplete Then
            If e.KeyCode = Keys.Enter Then
                OnContinueClicked()
                e.Handled = True
                Return
            ElseIf e.KeyCode = Keys.R Then
                OnRetryClicked()
                e.Handled = True
                Return
            End If
        End If

        ' R — restart the current level attempt
        If e.KeyCode = Keys.R Then
            OnRetryClicked()
            e.Handled = True
            Return
        End If

        ' Prevent arrow keys from cycling focus between controls
        Select Case e.KeyCode
            Case Keys.Up, Keys.Down, Keys.Left, Keys.Right
                e.Handled = True
                e.SuppressKeyPress = True
        End Select
    End Sub

    Protected Overrides Sub OnKeyUp(e As KeyEventArgs)
        MyBase.OnKeyUp(e)
        keyboardController.NotifyKeyUp(e.KeyCode)
    End Sub

    Protected Overrides Sub OnDeactivate(e As EventArgs)
        MyBase.OnDeactivate(e)
        keyboardController.ReleaseAll()
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        If gameLoopTimer IsNot Nothing Then
            gameLoopTimer.Stop()
            gameLoopTimer.Dispose()
        End If
        MyBase.OnFormClosed(e)
    End Sub

    ' ── Game loop ──────────────────────────────────────────────────────────
    Private Sub GameLoop_Tick(sender As Object, e As EventArgs)
        If engine Is Nothing Then Return

        Dim tx As Single = inputManager.TiltX
        Dim ty As Single = inputManager.TiltY

        engine.Update(tx, ty)
        canvas.UpdateBallPosition(engine.BallX, engine.BallY)
        UpdateHud(tx, ty)
    End Sub

    ' ── HUD ────────────────────────────────────────────────────────────────
    Private Sub UpdateHud(tx As Single, ty As Single)
        Dim config As LevelConfig = levels(currentLevelIndex)
        Dim levelTitle As String = $"{config.LevelNumber:D2} / {config.ThemeName}"

        Dim totalSecs As Integer = CInt(Math.Floor(engine.ElapsedSeconds))
        Dim mins      As Integer = totalSecs \ 60
        Dim secs      As Integer = totalSecs Mod 60
        Dim timeStr   As String  = $"{mins:D2}:{secs:D2}"

        Dim timeStatus As String = ""
        If config.TimeLimitSecs > 0 Then
            Dim remSecs As Integer = CInt(Math.Ceiling(engine.TimeRemainingSeconds))
            timeStatus = $" | Limit: {config.TimeLimitSecs}s | Rem: {remSecs}s"
        End If

        If engine.State = GameState.LevelComplete Then
            inputStatusLabel.ForeColor = Color.FromArgb(115, 250, 180)
            inputStatusLabel.Text =
                $"★ LEVEL COMPLETE! ★   Time: {timeStr}   |   Attempt: {engine.Attempts}"
            levelLabel.ForeColor = Color.FromArgb(115, 250, 180)
            levelLabel.Text      = levelTitle & "   — COMPLETE!"
        ElseIf engine.State = GameState.TimeUp Then
            inputStatusLabel.ForeColor = Color.FromArgb(255, 100, 100)
            inputStatusLabel.Text =
                $"OUT OF TIME!   |   Attempt: {engine.Attempts}   |   Press R to restart"
            levelLabel.ForeColor = Color.FromArgb(255, 100, 100)
            levelLabel.Text      = levelTitle & "   — TIME UP!"
        Else
            If config.ThemeName = "Frozen Labyrinth" Then
                inputStatusLabel.ForeColor = Color.FromArgb(120, 220, 210)
                levelLabel.ForeColor = Color.FromArgb(145, 198, 245)
            ElseIf config.ThemeName = "Neon Velocity" Then
                inputStatusLabel.ForeColor = Color.FromArgb(120, 255, 180)
                levelLabel.ForeColor = Color.FromArgb(90, 220, 255)
            Else
                inputStatusLabel.ForeColor = Color.FromArgb(120, 200, 120)
                levelLabel.ForeColor = Color.FromArgb(183, 162, 135)
            End If
            inputStatusLabel.Text =
                $"Controls: Arrow keys or WASD  |  Time: {timeStr}{timeStatus}  |  Attempt: {engine.Attempts}"
            levelLabel.Text = levelTitle
        End If
    End Sub

    ' Helpers ---------------------------------------------------------------
    Private Sub ShowLevelError(details As String)
        MessageBox.Show(Me,
                        "The maze could not be loaded." & Environment.NewLine & details,
                        "Gravity Maze", MessageBoxButtons.OK, MessageBoxIcon.Warning)
    End Sub
End Class
