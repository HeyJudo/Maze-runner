Option Strict On
Option Explicit On

Imports System
Imports GravityMaze.Levels

Namespace Engine
    ' Updates ball position using velocity, acceleration, friction, and wall collision.
    ' Also tracks game state, elapsed time, and attempt count.
    ' Does not access Arduino, UI controls, or rendering systems.
    Public NotInheritable Class GameEngine
        ' Physics constants — tune per level as needed.
        Public Property BaseAcceleration As Single = 0.010F   ' tiles/tick added per unit of tilt
        Public Property BaseFriction     As Single = 0.90F    ' velocity multiplier each tick
        Public Property BaseMaxSpeed     As Single = 0.12F    ' speed ceiling in tiles/tick
        Public Property IceFriction As Single = 0.955F   ' reduced friction on ice tiles (coasts ~2.36x normal floor)
        Public Property FastAcceleration As Single = 0.030F   ' acceleration on fast tiles
        Public Property FastMaxSpeed     As Single = 0.24F    ' speed ceiling on fast tiles

        ' Ball radius in tile-space. Must match the renderer visual radius.
        Public Const BallRadius As Single = 0.27F

        ' Distance from goal centre that triggers level completion.
        Private Const GoalRadius As Single = 0.35F

        ' Assumed milliseconds per game-loop tick (matches the 16 ms timer in Form1).
        Private Const MsPerTick As Single = 16.0F

        ' Epsilon: prevents a ball edge exactly on a tile boundary from spanning two tiles.
        Private Const Eps As Single = 0.001F

        ' Minimum impact speed into wall to trigger visual feedback effect.
        Public Const MinImpactSpeed As Single = 0.020F

        ' Events exposed by the engine (does not reference UI or rendering directly).
        Public Event WallImpacted As EventHandler(Of WallImpactEventArgs)
        Public Event LevelCompleted As EventHandler

        ' Ball state in tile-space.
        Private _ballX     As Single
        Private _ballY     As Single
        Private _velocityX As Single
        Private _velocityY As Single

        ' Wall contact state tracking to suppress repeated impact flashes while resting.
        Private _inContactLeft   As Boolean = False
        Private _inContactRight  As Boolean = False
        Private _inContactTop    As Boolean = False
        Private _inContactBottom As Boolean = False
        Private _completionFired As Boolean = False

        ' Game state.
        Private _state     As GameState = GameState.Playing
        Private _elapsedMs As Single    = 0.0F
        Private _attempts  As Integer   = 1

        ' Goal position cached from the maze definition.
        Private ReadOnly _goalCenterX As Single
        Private ReadOnly _goalCenterY As Single

        Private ReadOnly _maze As MazeDefinition
        Private ReadOnly _timeLimitSecs As Integer

        ' ── Public surface ─────────────────────────────────────────────────
        Public ReadOnly Property BallX As Single
            Get
                Return _ballX
            End Get
        End Property

        Public ReadOnly Property BallY As Single
            Get
                Return _ballY
            End Get
        End Property

        Public ReadOnly Property State As GameState
            Get
                Return _state
            End Get
        End Property

        ' Elapsed seconds since the current attempt started.
        Public ReadOnly Property ElapsedSeconds As Single
            Get
                Return _elapsedMs / 1000.0F
            End Get
        End Property
        
        Public ReadOnly Property TimeRemainingSeconds As Single
            Get
                If _timeLimitSecs <= 0 Then Return 0.0F
                Dim remSecs As Single = _timeLimitSecs - ElapsedSeconds
                If remSecs < 0.0F Then remSecs = 0.0F
                Return remSecs
            End Get
        End Property

        Public ReadOnly Property Attempts As Integer
            Get
                Return _attempts
            End Get
        End Property

        Public Sub New(maze As MazeDefinition, timeLimitSecs As Integer)
            If maze Is Nothing Then Throw New ArgumentNullException(NameOf(maze))
            _maze         = maze
            _timeLimitSecs = timeLimitSecs
            _ballX        = maze.StartColumn + 0.5F
            _ballY        = maze.StartRow    + 0.5F
            _goalCenterX  = maze.GoalColumn  + 0.5F
            _goalCenterY  = maze.GoalRow     + 0.5F
        End Sub

        ' Resets the current level for another attempt.
        ' Preserves the attempt counter and increments it.
        Public Sub Reset()
            _attempts  += 1
            _ballX      = _maze.StartColumn + 0.5F
            _ballY      = _maze.StartRow    + 0.5F
            _velocityX  = 0.0F
            _velocityY  = 0.0F
            _elapsedMs  = 0.0F
            _state      = GameState.Playing
            _completionFired = False
            _inContactLeft   = False
            _inContactRight  = False
            _inContactTop    = False
            _inContactBottom = False
        End Sub

        ' ── Game loop ──────────────────────────────────────────────────────
        ' Call once per tick. tiltX / tiltY: -1.0 to 1.0.
        ' Does nothing if the level is already complete.
        Public Sub Update(tiltX As Single, tiltY As Single)
            If _state <> GameState.Playing Then Return

            ' Determine current tile modifiers
            Dim cRow As Integer = Math.Max(0, Math.Min(_maze.RowCount - 1, CInt(Math.Floor(_ballY))))
            Dim cCol As Integer = Math.Max(0, Math.Min(_maze.ColumnCount - 1, CInt(Math.Floor(_ballX))))
            Dim tile As Char = _maze.GetTile(cRow, cCol)

            Dim currentAccel As Single = BaseAcceleration
            Dim currentFriction As Single = BaseFriction
            Dim currentMaxSpeed As Single = BaseMaxSpeed

            If tile = "I"c Then
                currentFriction = IceFriction
            ElseIf tile = "F"c Then
                currentAccel = FastAcceleration
                currentMaxSpeed = FastMaxSpeed
            End If

            ' 1. Acceleration
            _velocityX += tiltX * currentAccel
            _velocityY += tiltY * currentAccel

            ' 2. Friction
            _velocityX *= currentFriction
            _velocityY *= currentFriction

            ' 3. Speed cap
            If _velocityX < -currentMaxSpeed Then _velocityX = -currentMaxSpeed
            If _velocityX >  currentMaxSpeed Then _velocityX =  currentMaxSpeed
            If _velocityY < -currentMaxSpeed Then _velocityY = -currentMaxSpeed
            If _velocityY >  currentMaxSpeed Then _velocityY =  currentMaxSpeed

            ' 4. X movement with leading-edge wall check.
            If _velocityX <> 0.0F Then
                Dim newX As Single = _ballX + _velocityX
                If WallBlocksX(newX) Then
                    Dim impactSpeed As Single = Math.Abs(_velocityX)
                    Dim contactX As Single
                    If _velocityX > 0.0F Then
                        contactX = CSng(Math.Floor(newX + BallRadius))
                        _ballX = contactX - BallRadius
                        If Not _inContactRight AndAlso impactSpeed >= MinImpactSpeed Then
                            RaiseEvent WallImpacted(Me, New WallImpactEventArgs(contactX, _ballY, -1.0F, 0.0F, impactSpeed))
                        End If
                        _inContactRight = True
                    Else
                        contactX = CSng(Math.Ceiling(newX - BallRadius))
                        _ballX = contactX + BallRadius
                        If Not _inContactLeft AndAlso impactSpeed >= MinImpactSpeed Then
                            RaiseEvent WallImpacted(Me, New WallImpactEventArgs(contactX, _ballY, 1.0F, 0.0F, impactSpeed))
                        End If
                        _inContactLeft = True
                    End If
                    _velocityX = 0.0F
                Else
                    _ballX = newX
                    If _velocityX > Eps Then _inContactLeft = False
                    If _velocityX < -Eps Then _inContactRight = False
                End If
            Else
                If tiltX < -0.1F Then _inContactRight = False
                If tiltX > 0.1F Then _inContactLeft = False
            End If

            ' 5. Y movement with leading-edge wall check.
            If _velocityY <> 0.0F Then
                Dim newY As Single = _ballY + _velocityY
                If WallBlocksY(newY) Then
                    Dim impactSpeed As Single = Math.Abs(_velocityY)
                    Dim contactY As Single
                    If _velocityY > 0.0F Then
                        contactY = CSng(Math.Floor(newY + BallRadius))
                        _ballY = contactY - BallRadius
                        If Not _inContactBottom AndAlso impactSpeed >= MinImpactSpeed Then
                            RaiseEvent WallImpacted(Me, New WallImpactEventArgs(_ballX, contactY, 0.0F, -1.0F, impactSpeed))
                        End If
                        _inContactBottom = True
                    Else
                        contactY = CSng(Math.Ceiling(newY - BallRadius))
                        _ballY = contactY + BallRadius
                        If Not _inContactTop AndAlso impactSpeed >= MinImpactSpeed Then
                            RaiseEvent WallImpacted(Me, New WallImpactEventArgs(_ballX, contactY, 0.0F, 1.0F, impactSpeed))
                        End If
                        _inContactTop = True
                    End If
                    _velocityY = 0.0F
                Else
                    _ballY = newY
                    If _velocityY > Eps Then _inContactTop = False
                    If _velocityY < -Eps Then _inContactBottom = False
                End If
            Else
                If tiltY < -0.1F Then _inContactBottom = False
                If tiltY > 0.1F Then _inContactTop = False
            End If

            ' 6. Advance timer.
            _elapsedMs += MsPerTick
            
            If _timeLimitSecs > 0 AndAlso _elapsedMs >= _timeLimitSecs * 1000.0F Then
                _state = GameState.TimeUp
                _velocityX = 0.0F
                _velocityY = 0.0F
                Return
            End If

            ' 7. Goal detection — ball centre within GoalRadius of goal centre.
            Dim gdx As Single = _ballX - _goalCenterX
            Dim gdy As Single = _ballY - _goalCenterY
            If gdx * gdx + gdy * gdy <= GoalRadius * GoalRadius Then
                _state     = GameState.LevelComplete
                _velocityX = 0.0F
                _velocityY = 0.0F
                If Not _completionFired Then
                    _completionFired = True
                    RaiseEvent LevelCompleted(Me, EventArgs.Empty)
                End If
            End If
        End Sub

        ' ── Collision helpers ───────────────────────────────────────────────
        Private Function WallBlocksX(newX As Single) As Boolean
            Dim leadCol As Integer = If(_velocityX > 0.0F,
                                        CInt(Math.Floor(newX + BallRadius)),
                                        CInt(Math.Floor(newX - BallRadius)))
            If leadCol < 0 OrElse leadCol >= _maze.ColumnCount Then Return True

            Dim rowMin As Integer = Math.Max(0,
                CInt(Math.Floor(_ballY - BallRadius + Eps)))
            Dim rowMax As Integer = Math.Min(_maze.RowCount - 1,
                CInt(Math.Floor(_ballY + BallRadius - Eps)))

            For r As Integer = rowMin To rowMax
                If _maze.GetTile(r, leadCol) = "1"c Then Return True
            Next
            Return False
        End Function

        Private Function WallBlocksY(newY As Single) As Boolean
            Dim leadRow As Integer = If(_velocityY > 0.0F,
                                        CInt(Math.Floor(newY + BallRadius)),
                                        CInt(Math.Floor(newY - BallRadius)))
            If leadRow < 0 OrElse leadRow >= _maze.RowCount Then Return True

            Dim colMin As Integer = Math.Max(0,
                CInt(Math.Floor(_ballX - BallRadius + Eps)))
            Dim colMax As Integer = Math.Min(_maze.ColumnCount - 1,
                CInt(Math.Floor(_ballX + BallRadius - Eps)))

            For c As Integer = colMin To colMax
                If _maze.GetTile(leadRow, c) = "1"c Then Return True
            Next
            Return False
        End Function
    End Class

    ' Holds impact information exposed by GameEngine for renderer visual effects.
    Public NotInheritable Class WallImpactEventArgs
        Inherits EventArgs

        Public ReadOnly Property ImpactX As Single
        Public ReadOnly Property ImpactY As Single
        Public ReadOnly Property NormalX As Single
        Public ReadOnly Property NormalY As Single
        Public ReadOnly Property Speed As Single

        Public Sub New(impactX As Single, impactY As Single, normalX As Single, normalY As Single, speed As Single)
            Me.ImpactX = impactX
            Me.ImpactY = impactY
            Me.NormalX = normalX
            Me.NormalY = normalY
            Me.Speed   = speed
        End Sub
    End Class
End Namespace
