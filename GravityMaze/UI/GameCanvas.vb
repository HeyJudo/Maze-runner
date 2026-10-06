Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports System.Collections.Generic
Imports System.ComponentModel
Imports GravityMaze.Levels
Imports GravityMaze.Rendering

Namespace UI
    ' A drawing surface. It does not advance game state or handle input.
    Public Class GameCanvas
        Inherits Control

        Private currentMaze As MazeDefinition
        Private currentTheme As String = "Wooden Workshop"
        Private ReadOnly renderer As New MazeRenderer()
        Private ReadOnly perspective As New PerspectiveSurface()
        Private ReadOnly tilt As New BoardTilt()
        Private _tiltViewEnabled As Boolean = True
        Private goalDrop As GoalDropTransition
        Private incomingMaze As MazeDefinition
        Private incomingTheme As String
        Private goalStart As PointF
        Private completedMarbleHidden As Boolean
        Private ReadOnly incomingRenderer As New MazeRenderer()
        Private ReadOnly incomingSurface As New PerspectiveSurface()
        Private ReadOnly incomingTilt As New BoardTilt()

        Public Sub BeginGoalDrop(transition As GoalDropTransition, nextMaze As MazeDefinition, nextTheme As String)
            goalDrop = transition
            incomingMaze = nextMaze
            incomingTheme = nextTheme
            goalStart = New PointF(_ballX, _ballY)
            completedMarbleHidden = True
            fx.Reset()
            activeImpacts.Clear()
            _flashMs = 0 : _shakeMs = 0
            Invalidate()
        End Sub

        Public Sub EndGoalDrop()
            goalDrop = Nothing
            incomingMaze = Nothing
            Invalidate()
        End Sub

        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property TiltViewEnabled As Boolean
            Get
                Return _tiltViewEnabled
            End Get
            Set(value As Boolean)
                _tiltViewEnabled = value
                tilt.Reset()
                Invalidate()
            End Set
        End Property

        ' The shell sends the same resolved input used by physics, only during active play.
        Public Sub UpdateBoardTilt(x As Single, y As Single, elapsedMs As Single)
            If _tiltViewEnabled Then tilt.Advance(x, y, elapsedMs)
        End Sub

        ' Ball position in tile-space, updated each tick by the game loop.
        Private _ballX As Single
        Private _ballY As Single

        ' Space kept free around the board (e.g. for the HUD bar). The board is laid out inside the rest.
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property BoardInsets As Padding = Padding.Empty

        ' Screens (menus, HUD, panels) paint on top of the board through this hook. Nothing = board only.
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property OverlayPainter As Action(Of Graphics, Rectangle)

        ' Transient visual feedback effects
        Private ReadOnly activeImpacts As New List(Of ImpactEffect)()
        Private ReadOnly goalCelebration As New GoalCelebrationEffect()
        Private ReadOnly fx As New BallFxState()
        Private _flashMs As Single      ' red damage overlay, counts down
        Private _shakeMs As Single      ' board shake, counts down
        Private ReadOnly _shakeRng As New Random()

        ' Hearts: the engine's pickup lookup (Nothing = draw every pickup) and the invulnerable blink.
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property PickupTaken As Func(Of Integer, Integer, Boolean)
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property BallBlink As Boolean

        ' True while a fall or goal transition replaces the normal engine marble.
        Public ReadOnly Property IsBallHidden As Boolean
            Get
                Return fx.FallActive OrElse completedMarbleHidden
            End Get
        End Property

        ' Called every tick right after UpdateBallPosition; tile = maze tile under the ball centre.
        Public Sub UpdateBallMotion(vx As Single, vy As Single, tile As Char)
            fx.UpdateMotion(_ballX, _ballY, vx, vy, tile)
        End Sub

        ' Pink sparkle burst at a collected heart (tile-space).
        Public Sub AddHeartBurst(x As Single, y As Single)
            fx.AddBurst(x, y)
            Invalidate()
        End Sub

        ' Red flash plus a short shake when a heart is lost.
        Public Sub FlashDamage()
            _flashMs = 250.0F
            _shakeMs = 200.0F
            Invalidate()
        End Sub

        ' Tile-space hole centre. Starts the fall ghost, then a spawn pulse at the (already reset) ball.
        Public Sub AddHoleFall(holeX As Single, holeY As Single)
            fx.AddHoleFall(holeX, holeY)
            Invalidate()
        End Sub

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
            EndGoalDrop()
            completedMarbleHidden = False
            tilt.Reset()
            SyncLock activeImpacts
                activeImpacts.Clear()
            End SyncLock
            goalCelebration.Reset()
            fx.Reset()
            _flashMs = 0.0F
            _shakeMs = 0.0F
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
            fx.Advance(16.0F, x, y)
            If _flashMs > 0.0F Then _flashMs -= 16.0F
            If _shakeMs > 0.0F Then _shakeMs -= 16.0F

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

                Dim boardArea As New Rectangle(BoardInsets.Left, BoardInsets.Top,
                                               Math.Max(0, ClientSize.Width - BoardInsets.Horizontal),
                                               Math.Max(0, ClientSize.Height - BoardInsets.Vertical))
                Dim state As Drawing2D.GraphicsState = e.Graphics.Save()
                If _shakeMs > 0.0F Then
                    Dim amp As Single = 6.0F * _shakeMs / 200.0F
                    e.Graphics.TranslateTransform(CSng(_shakeRng.NextDouble() * 2 - 1) * amp, CSng(_shakeRng.NextDouble() * 2 - 1) * amp)
                End If
                If goalDrop IsNot Nothing Then
                    DrawGoalDrop(e.Graphics, boardArea)
                ElseIf TiltViewEnabled Then
                    perspective.Draw(e.Graphics, boardArea, tilt,
                        Sub(g As Graphics, area As Rectangle)
                            renderer.Draw(g, area, currentMaze, _ballX, _ballY,
                                          currentTheme, impactSnapshot, goalCelebration, fx, PickupTaken, BallBlink,
                                          If(completedMarbleHidden, 0.0F, 1.0F))
                        End Sub)
                Else
                    renderer.Draw(e.Graphics, boardArea, currentMaze, _ballX, _ballY,
                                  currentTheme, impactSnapshot, goalCelebration, fx, PickupTaken, BallBlink,
                                  If(completedMarbleHidden, 0.0F, 1.0F))
                End If
                e.Graphics.Restore(state)
                If _flashMs > 0.0F Then
                    Using br As New SolidBrush(Color.FromArgb(CInt(90 * _flashMs / 250.0F), 230, 30, 40))
                        e.Graphics.FillRectangle(br, boardArea)
                    End Using
                End If
            End If
            OverlayPainter?.Invoke(e.Graphics, ClientRectangle)
        End Sub

        Private Sub DrawGoalDrop(g As Graphics, area As Rectangle)
            Dim phase As GoalDropPhase = goalDrop.Phase
            Dim p As Single = goalDrop.Progress
            If incomingMaze IsNot Nothing AndAlso phase <> GoalDropPhase.Drop Then
                Dim lift As Single = If(phase = GoalDropPhase.Lift, GoalDropTransition.Ease(p), 1.0F)
                Dim visibleBall As Boolean = phase = GoalDropPhase.Landing OrElse phase = GoalDropPhase.Ready
                Dim pulse As Single = If(phase = GoalDropPhase.Landing AndAlso p >= 0.65F, (p - 0.65F) / 0.35F, -1.0F)
                DrawLayer(g, area, incomingSurface, incomingTilt,
                    Sub(cg, bounds)
                        incomingRenderer.Draw(cg, bounds, incomingMaze, incomingMaze.StartColumn + 0.5F,
                                              incomingMaze.StartRow + 0.5F, incomingTheme,
                                              ballScale:=If(visibleBall, 1.0F, 0.0F),
                                              ballLift:=goalDrop.LandingHeight, landingPulse:=pulse)
                    End Sub, lift, area.Height * 0.16F * (1 - lift), 0.94F + 0.06F * lift)
            End If
            If phase = GoalDropPhase.Drop OrElse phase = GoalDropPhase.Lift Then
                Dim lift As Single = If(phase = GoalDropPhase.Lift, GoalDropTransition.Ease(p), 0.0F)
                Dim pull As Single = If(phase = GoalDropPhase.Drop, GoalDropTransition.Ease(Math.Min(1, p / 0.65F)), 1.0F)
                Dim bx As Single = goalStart.X + (currentMaze.GoalColumn + 0.5F - goalStart.X) * pull
                Dim by As Single = goalStart.Y + (currentMaze.GoalRow + 0.5F - goalStart.Y) * pull
                Dim shrink As Single = If(phase = GoalDropPhase.Drop, 1 - p * p, 0.0F)
                DrawLayer(g, area, perspective, tilt,
                    Sub(cg, bounds)
                        renderer.Draw(cg, bounds, currentMaze, bx, by, currentTheme,
                                      goalEffect:=goalCelebration, pickupTaken:=PickupTaken, ballScale:=shrink)
                    End Sub, 1 - lift, -area.Height * 0.28F * lift, 1.0F + 0.06F * lift)
            End If
        End Sub

        Private Sub DrawLayer(g As Graphics, area As Rectangle, surface As PerspectiveSurface, pose As BoardTilt,
                              paint As Action(Of Graphics, Rectangle), opacity As Single, offsetY As Single, scale As Single)
            Dim state = g.Save()
            Try
                g.SetClip(area, Drawing2D.CombineMode.Intersect)
                Dim cx As Single = area.Left + area.Width / 2.0F
                Dim cy As Single = area.Top + area.Height / 2.0F
                g.TranslateTransform(cx, cy + offsetY)
                g.ScaleTransform(scale, scale)
                g.TranslateTransform(-cx, -cy)
                surface.Draw(g, area, pose, paint, TiltViewEnabled, opacity)
            Finally
                g.Restore(state)
            End Try
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                perspective.Dispose()
                renderer.Dispose()
                incomingSurface.Dispose()
                incomingRenderer.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
