Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports System.Collections.Generic
Imports GravityMaze.Levels
Imports GravityMaze.Rendering

Namespace UI
    ' A drawing surface. It does not advance game state or handle input.
    Public Class GameCanvas
        Inherits Control

        Private currentMaze As MazeDefinition
        Private currentTheme As String = "Wooden Workshop"
        Private ReadOnly renderer As New MazeRenderer()

        ' Ball position in tile-space, updated each tick by the game loop.
        Private _ballX As Single
        Private _ballY As Single

        ' Transient visual feedback effects
        Private ReadOnly activeImpacts As New List(Of ImpactEffect)()
        Private ReadOnly goalCelebration As New GoalCelebrationEffect()

        Public Sub New()
            DoubleBuffered = True
            ResizeRedraw = True
            BackColor = Color.FromArgb(27, 24, 22)
            TabStop = False
            AccessibleName = "Gravity Maze board"
        End Sub

        ' Called once when a level is loaded. Resets the ball and effects.
        Public Sub ShowMaze(maze As MazeDefinition, Optional themeName As String = "Wooden Workshop")
            currentMaze = maze
            currentTheme = themeName
            _ballX = maze.StartColumn + 0.5F
            _ballY = maze.StartRow + 0.5F
            ClearEffects()
            Invalidate()
        End Sub

        ' Clears all active impact animations and goal celebrations.
        Public Sub ClearEffects()
            SyncLock activeImpacts
                activeImpacts.Clear()
            End SyncLock
            goalCelebration.Reset()
            Invalidate()
        End Sub

        ' Adds a local wall collision impact effect (white/cyan expanding arc).
        Public Sub AddImpact(x As Single, y As Single, nx As Single, ny As Single, speed As Single)
            SyncLock activeImpacts
                activeImpacts.Add(New ImpactEffect(x, y, nx, ny, speed))
            End SyncLock
            Invalidate()
        End Sub

        ' Triggers the expanding goal celebration ring animation.
        Public Sub TriggerGoalCelebration(cx As Single, cy As Single)
            goalCelebration.Trigger(cx, cy)
            Invalidate()
        End Sub

        ' Called by the game loop each tick with the engine's current ball position.
        Public Sub UpdateBallPosition(x As Single, y As Single)
            _ballX = x
            _ballY = y

            ' Advance transient visual effects by one frame tick (16 ms)
            SyncLock activeImpacts
                For i As Integer = activeImpacts.Count - 1 To 0 Step -1
                    activeImpacts(i).Advance(16.0F)
                    If activeImpacts(i).IsExpired Then
                        activeImpacts.RemoveAt(i)
                    End If
                Next
            End SyncLock

            If goalCelebration.IsActive Then
                goalCelebration.Advance(16.0F)
            End If

            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            If currentMaze IsNot Nothing Then
                Dim impactSnapshot As List(Of ImpactEffect) = Nothing
                SyncLock activeImpacts
                    If activeImpacts.Count > 0 Then
                        impactSnapshot = New List(Of ImpactEffect)(activeImpacts)
                    End If
                End SyncLock

                renderer.Draw(e.Graphics, ClientRectangle, currentMaze, _ballX, _ballY,
                              currentTheme, impactSnapshot, goalCelebration)
            End If
        End Sub
    End Class
End Namespace
