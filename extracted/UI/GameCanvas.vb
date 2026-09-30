Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Windows.Forms
Imports GravityMaze.Levels
Imports GravityMaze.Rendering

Namespace UI
    ' A drawing surface. It does not advance game state or handle input.
    Public Class GameCanvas
        Inherits Control

        Private currentMaze As MazeDefinition
        Private ReadOnly renderer As New MazeRenderer()

        Public Sub New()
            DoubleBuffered = True
            ResizeRedraw = True
            BackColor = Color.FromArgb(27, 24, 22)
            TabStop = False
            AccessibleName = "Gravity Maze board"
        End Sub

        Public Sub ShowMaze(maze As MazeDefinition)
            currentMaze = maze
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            If currentMaze IsNot Nothing Then
                renderer.Draw(e.Graphics, ClientRectangle, currentMaze)
            End If
        End Sub
    End Class
End Namespace
