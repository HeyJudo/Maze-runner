Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports GravityMaze.Levels
Imports GravityMaze.UI

Public Class Form1
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Text = "Gravity Maze | Wooden Workshop"
        BackColor = Color.FromArgb(27, 24, 22)
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(640, 540)

        Dim layout As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(16),
            .BackColor = BackColor
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        Dim titleLabel As New Label With {
            .Text = "GRAVITY MAZE",
            .AutoSize = True,
            .ForeColor = Color.FromArgb(240, 222, 192),
            .Margin = New Padding(0, 0, 0, 8)
        }
        Dim levelLabel As New Label With {
            .Text = "01 / Wooden Workshop",
            .AutoSize = True,
            .ForeColor = Color.FromArgb(183, 162, 135),
            .Margin = New Padding(0, 0, 0, 8)
        }
        Dim legendLabel As New Label With {
            .Text = "START: silver marble     GOAL: green ring",
            .AutoSize = True,
            .ForeColor = Color.FromArgb(218, 207, 190),
            .Margin = New Padding(0, 8, 0, 0)
        }
        Dim canvas As New GameCanvas With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0)
        }

        layout.Controls.Add(titleLabel, 0, 0)
        layout.Controls.Add(levelLabel, 0, 1)
        layout.Controls.Add(canvas, 0, 2)
        layout.Controls.Add(legendLabel, 0, 3)
        Controls.Add(layout)

        Try
            Dim levelPath As String = Path.Combine(AppContext.BaseDirectory, "Mazes", "Level1.txt")
            canvas.ShowMaze(MazeManager.LoadFromFile(levelPath))
        Catch ex As IOException
            ShowLevelError(ex.Message)
        Catch ex As UnauthorizedAccessException
            ShowLevelError(ex.Message)
        Catch ex As ArgumentException
            ShowLevelError(ex.Message)
        End Try
    End Sub

    Private Sub ShowLevelError(details As String)
        MessageBox.Show(Me,
                        "The maze could not be loaded." & Environment.NewLine & details,
                        "Gravity Maze", MessageBoxButtons.OK, MessageBoxIcon.Warning)
    End Sub
End Class
