Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Collections.Generic
Imports System.Linq
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
        ' Ice: low grip (tilt barely steers or brakes), near-frictionless, higher top speed.
        ' Tune IceGrip up if the Arduino's partial tilt makes ice feel unplayable.
        Public Property IceGrip          As Single = 0.30F    ' fraction of BaseAcceleration applied on ice
        Public Property IceFriction      As Single = 0.985F   ' velocity multiplier each tick on ice
        Public Property IceMaxSpeed      As Single = 0.18F    ' speed ceiling on ice
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
        Public Event BallFell As EventHandler(Of BallFellEventArgs)

        ' Ball drops when its centre is this close to a hole's centre (edges can be grazed).
        Private Const HoleRadius As Single = 0.4F

        ' Health is stored in integer quarters: walls cost 1/4 heart, pits cost one,
        ' and L tiles restore one heart, capped at MaxHearts.
        Public Const MaxHearts As Integer = 3
        Public Const QuartersPerHeart As Integer = 4
        Private Const WallDamageQuarters As Integer = 1
        Public Const InvulnerableMs As Single = 1000.0F
        Private Const PickupRadius As Single = 0.45F
        Public Property HeartsEnabled As Boolean = True      ' False for the attract demo and test bots

        Public Event HeartLost As EventHandler(Of HeartEventArgs)
        Public Event HeartGained As EventHandler(Of HeartEventArgs)

        ' Overdrive barriers (B): a marble at break speed smashes the whole connected barrier;
        ' slower contact is an ordinary wall hit. Break speed sits above BaseMaxSpeed, so only a boost reaches it.
        Public Property BarrierBreakSpeed As Single = 0.16F
        Public Property BarrierSmashDamping As Single = 0.5F   ' velocity kept after smashing through
        Public Event BarrierBroken As EventHandler(Of BarrierEventArgs)
        Public Event BarrierBounced As EventHandler(Of BarrierEventArgs)
        Private ReadOnly _barrierGroup As Integer()                   ' per tile: barrier id, -1 = none
        Private ReadOnly _barrierCenters As New List(Of PointF)()
        Private ReadOnly _brokenBarriers As New HashSet(Of Integer)()

        ' Run counters for the journal; Reset clears them.
        Private _wallHits As Integer
        Private _falls As Integer
        Private _quartersLost As Integer

        Private _heartQuarters As Integer = MaxHearts * QuartersPerHeart
        Private _invulnerableMs As Single
        Private ReadOnly _takenPickups As New HashSet(Of Integer)   ' row * ColumnCount + col

        ' Ball state in tile-space.
        Private _ballX As Single
        Private _ballY As Single
        Private _velocityX As Single
        Private _velocityY As Single

        ' Wall contact state tracking to suppress repeated impact flashes while resting.
        Private _inContactLeft As Boolean = False
        Private _inContactRight As Boolean = False
        Private _inContactTop As Boolean = False
        Private _inContactBottom As Boolean = False
        Private _completionFired As Boolean = False

        ' Game state.
        Private _state As GameState = GameState.Playing
        Private _elapsedMs As Single = 0.0F
        Private _attempts As Integer = 1

        ' Goal position cached from the maze definition.
        Private ReadOnly _goalCenterX As Single
        Private ReadOnly _goalCenterY As Single

        Private ReadOnly _maze As MazeDefinition
        Private ReadOnly _timeLimitSecs As Integer

        ' ── Public surface ─────────────────────────────────────────────────
        Public ReadOnly Property Dungeon As DungeonRun
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

        Public ReadOnly Property Maze As MazeDefinition
            Get
                Return _maze
            End Get
        End Property

        Public ReadOnly Property VelocityX As Single
            Get
                Return _velocityX
            End Get
        End Property

        Public ReadOnly Property VelocityY As Single
            Get
                Return _velocityY
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
            _maze = maze
            If maze.Dungeon IsNot Nothing Then Dungeon = New DungeonRun(maze)
            _timeLimitSecs = timeLimitSecs
            _ballX = maze.StartColumn + 0.5F
            _ballY = maze.StartRow + 0.5F
            _goalCenterX = maze.GoalColumn + 0.5F
            _goalCenterY = maze.GoalRow + 0.5F
            _barrierGroup = Enumerable.Repeat(-1, maze.RowCount * maze.ColumnCount).ToArray()
            For r As Integer = 0 To maze.RowCount - 1
                For c As Integer = 0 To maze.ColumnCount - 1
                    If maze.GetTile(r, c) <> "B"c OrElse _barrierGroup(r * maze.ColumnCount + c) >= 0 Then Continue For
                    Dim id As Integer = _barrierCenters.Count
                    Dim sumX As Single = 0, sumY As Single = 0
                    Dim n As Integer = 0
                    Dim todo As New Stack(Of Point)()
                    _barrierGroup(r * maze.ColumnCount + c) = id
                    todo.Push(New Point(c, r))
                    While todo.Count > 0
                        Dim p As Point = todo.Pop()
                        sumX += p.X + 0.5F : sumY += p.Y + 0.5F : n += 1
                        For Each d As Point In {New Point(1, 0), New Point(-1, 0), New Point(0, 1), New Point(0, -1)}
                            Dim nx As Integer = p.X + d.X
                            Dim ny As Integer = p.Y + d.Y
                            If nx < 0 OrElse ny < 0 OrElse nx >= maze.ColumnCount OrElse ny >= maze.RowCount Then Continue For
                            If maze.GetTile(ny, nx) <> "B"c OrElse _barrierGroup(ny * maze.ColumnCount + nx) >= 0 Then Continue For
                            _barrierGroup(ny * maze.ColumnCount + nx) = id
                            todo.Push(New Point(nx, ny))
                        Next
                    End While
                    _barrierCenters.Add(New PointF(sumX / n, sumY / n))
                Next
            Next
        End Sub

        ' Resets the current level for another attempt.
        ' Preserves the attempt counter and increments it.
        Public Sub Reset()
            _attempts += 1
            _ballX = _maze.StartColumn + 0.5F
            _ballY = _maze.StartRow + 0.5F
            _velocityX = 0.0F
            _velocityY = 0.0F
            _elapsedMs = 0.0F
            _state = GameState.Playing
            _completionFired = False
            _inContactLeft = False
            _inContactRight = False
            _inContactTop = False
            _inContactBottom = False
            _heartQuarters = MaxHearts * QuartersPerHeart
            _invulnerableMs = 0.0F
            _takenPickups.Clear()
            _brokenBarriers.Clear()
            _wallHits = 0
            _falls = 0
            _quartersLost = 0
            Dungeon?.Reset()
        End Sub

        Public ReadOnly Property Hearts As Single
            Get
                Return CSng(_heartQuarters) / QuartersPerHeart
            End Get
        End Property

        Public ReadOnly Property IsInvulnerable As Boolean
            Get
                Return _invulnerableMs > 0.0F
            End Get
        End Property

        Public Function IsPickupTaken(row As Integer, col As Integer) As Boolean
            Return _takenPickups.Contains(row * _maze.ColumnCount + col)
        End Function

        Public ReadOnly Property BarriersTotal As Integer
            Get
                Return _barrierCenters.Count
            End Get
        End Property

        Public ReadOnly Property BarriersBroken As Integer
            Get
                Return _brokenBarriers.Count
            End Get
        End Property

        Public Function IsBarrierStanding(row As Integer, col As Integer) As Boolean
            Return BarrierAt(row, col) >= 0
        End Function

        Public ReadOnly Property WallHits As Integer
            Get
                Return _wallHits
            End Get
        End Property

        Public ReadOnly Property Falls As Integer
            Get
                Return _falls
            End Get
        End Property

        Public ReadOnly Property HeartsLost As Single
            Get
                Return CSng(_quartersLost) / QuartersPerHeart
            End Get
        End Property

        ' Standing barrier id at a tile, or -1.
        Private Function BarrierAt(row As Integer, col As Integer) As Integer
            If row < 0 OrElse row >= _maze.RowCount OrElse col < 0 OrElse col >= _maze.ColumnCount Then Return -1
            Dim id As Integer = _barrierGroup(row * _maze.ColumnCount + col)
            Return If(id < 0 OrElse _brokenBarriers.Contains(id), -1, id)
        End Function

        ' Barriers on the ball's leading edge: at break speed every touched barrier opens and the ball keeps
        ' (damped) momentum; slower contact leaves them standing and reports one bounce per contact.
        Private Function SmashBarriers(lead As Integer, fromIndex As Integer, toIndex As Integer, horizontal As Boolean,
                                       speed As Single, touching As Boolean) As Boolean
            Dim hit As New HashSet(Of Integer)()
            Dim hx As Single, hy As Single
            For i As Integer = fromIndex To toIndex
                Dim r As Integer = If(horizontal, i, lead)
                Dim c As Integer = If(horizontal, lead, i)
                Dim id As Integer = BarrierAt(r, c)
                If id >= 0 AndAlso hit.Add(id) Then hx = c + 0.5F : hy = r + 0.5F
            Next
            If hit.Count = 0 Then Return False
            If speed < BarrierBreakSpeed Then
                If Not touching Then RaiseEvent BarrierBounced(Me, New BarrierEventArgs(hx, hy))
                Return False
            End If
            For Each id As Integer In hit
                _brokenBarriers.Add(id)
                RaiseEvent BarrierBroken(Me, New BarrierEventArgs(_barrierCenters(id).X, _barrierCenters(id).Y))
            Next
            _velocityX *= BarrierSmashDamping
            _velocityY *= BarrierSmashDamping
            Return True
        End Function

        ' Wall contacts respect the grace period; pits always apply their full damage.
        Private Sub TakeHit(x As Single, y As Single, damageQuarters As Integer, ignoreInvulnerable As Boolean)
            If Not HeartsEnabled OrElse _state <> GameState.Playing Then Return
            If Not ignoreInvulnerable AndAlso _invulnerableMs > 0.0F Then Return
            Dim previousHearts As Single = Hearts
            Dim before As Integer = _heartQuarters
            _heartQuarters = Math.Max(0, _heartQuarters - damageQuarters)
            _quartersLost += before - _heartQuarters
            _invulnerableMs = InvulnerableMs
            RaiseEvent HeartLost(Me, New HeartEventArgs(x, y, Hearts, previousHearts))
            If _heartQuarters = 0 Then
                _state = GameState.OutOfHearts
                _velocityX = 0.0F
                _velocityY = 0.0F
            End If
        End Sub

        ' ── Game loop ──────────────────────────────────────────────────────
        ' Call once per tick. tiltX / tiltY: -1.0 to 1.0.
        ' Does nothing if the level is already complete.
        Public Sub Update(tiltX As Single, tiltY As Single)
            If _state <> GameState.Playing Then Return
            If _invulnerableMs > 0.0F Then _invulnerableMs -= MsPerTick

            Dim oldX = _ballX, oldY = _ballY
            ' Determine current tile modifiers
            Dim cRow As Integer = Math.Max(0, Math.Min(_maze.RowCount - 1, CInt(Math.Floor(_ballY))))
            Dim cCol As Integer = Math.Max(0, Math.Min(_maze.ColumnCount - 1, CInt(Math.Floor(_ballX))))
            Dim tile As Char = _maze.GetTile(cRow, cCol)

            Dim currentAccel As Single = BaseAcceleration
            Dim currentFriction As Single = BaseFriction
            Dim currentMaxSpeed As Single = BaseMaxSpeed

            If tile = "I"c Then
                currentAccel = BaseAcceleration * IceGrip
                currentFriction = IceFriction
                currentMaxSpeed = IceMaxSpeed
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
            If _velocityX > currentMaxSpeed Then _velocityX = currentMaxSpeed
            If _velocityY < -currentMaxSpeed Then _velocityY = -currentMaxSpeed
            If _velocityY > currentMaxSpeed Then _velocityY = currentMaxSpeed

            ' 4. X movement with leading-edge wall check.
            If _velocityX <> 0.0F Then
                Dim newX As Single = _ballX + _velocityX
                Dim blockedX As Boolean = WallBlocksX(newX)
                If blockedX Then
                    Dim leadCol As Integer = If(_velocityX > 0.0F, CInt(Math.Floor(newX + BallRadius)), CInt(Math.Floor(newX - BallRadius)))
                    If SmashBarriers(leadCol, Math.Max(0, CInt(Math.Floor(_ballY - BallRadius + Eps))),
                                     Math.Min(_maze.RowCount - 1, CInt(Math.Floor(_ballY + BallRadius - Eps))), True,
                                     Math.Abs(_velocityX), If(_velocityX > 0.0F, _inContactRight, _inContactLeft)) Then blockedX = WallBlocksX(newX)
                End If
                If blockedX Then
                    Dim impactSpeed As Single = Math.Abs(_velocityX)
                    Dim contactX As Single
                    If _velocityX > 0.0F Then
                        contactX = CSng(Math.Floor(newX + BallRadius))
                        _ballX = contactX - BallRadius
                        If Not _inContactRight AndAlso impactSpeed >= MinImpactSpeed Then
                            RaiseEvent WallImpacted(Me, New WallImpactEventArgs(contactX, _ballY, -1.0F, 0.0F, impactSpeed))
                        End If
                        If Not _inContactRight Then _wallHits += 1 : TakeHit(contactX, _ballY, WallDamageQuarters, False)
                        _inContactRight = True
                    Else
                        contactX = CSng(Math.Ceiling(newX - BallRadius))
                        _ballX = contactX + BallRadius
                        If Not _inContactLeft AndAlso impactSpeed >= MinImpactSpeed Then
                            RaiseEvent WallImpacted(Me, New WallImpactEventArgs(contactX, _ballY, 1.0F, 0.0F, impactSpeed))
                        End If
                        If Not _inContactLeft Then _wallHits += 1 : TakeHit(contactX, _ballY, WallDamageQuarters, False)
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
                Dim blockedY As Boolean = WallBlocksY(newY)
                If blockedY Then
                    Dim leadRow As Integer = If(_velocityY > 0.0F, CInt(Math.Floor(newY + BallRadius)), CInt(Math.Floor(newY - BallRadius)))
                    If SmashBarriers(leadRow, Math.Max(0, CInt(Math.Floor(_ballX - BallRadius + Eps))),
                                     Math.Min(_maze.ColumnCount - 1, CInt(Math.Floor(_ballX + BallRadius - Eps))), False,
                                     Math.Abs(_velocityY), If(_velocityY > 0.0F, _inContactBottom, _inContactTop)) Then blockedY = WallBlocksY(newY)
                End If
                If blockedY Then
                    Dim impactSpeed As Single = Math.Abs(_velocityY)
                    Dim contactY As Single
                    If _velocityY > 0.0F Then
                        contactY = CSng(Math.Floor(newY + BallRadius))
                        _ballY = contactY - BallRadius
                        If Not _inContactBottom AndAlso impactSpeed >= MinImpactSpeed Then
                            RaiseEvent WallImpacted(Me, New WallImpactEventArgs(_ballX, contactY, 0.0F, -1.0F, impactSpeed))
                        End If
                        If Not _inContactBottom Then _wallHits += 1 : TakeHit(_ballX, contactY, WallDamageQuarters, False)
                        _inContactBottom = True
                    Else
                        contactY = CSng(Math.Ceiling(newY - BallRadius))
                        _ballY = contactY + BallRadius
                        If Not _inContactTop AndAlso impactSpeed >= MinImpactSpeed Then
                            RaiseEvent WallImpacted(Me, New WallImpactEventArgs(_ballX, contactY, 0.0F, 1.0F, impactSpeed))
                        End If
                        If Not _inContactTop Then _wallHits += 1 : TakeHit(_ballX, contactY, WallDamageQuarters, False)
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

            ' A final hit must not be overwritten by the timer or goal checks below.
            If _state <> GameState.Playing Then Return

            ' 6. Advance timer.
            _elapsedMs += MsPerTick

            If _timeLimitSecs > 0 AndAlso _elapsedMs >= _timeLimitSecs * 1000.0F Then
                _state = GameState.TimeUp
                _velocityX = 0.0F
                _velocityY = 0.0F
                Return
            End If

            ' 7. Holes — the centre must be on an H tile and clear of any edge that borders solid floor,
            ' so multi-tile pits have no safe seams. Timer keeps running; attempts do not change.
            Dim hRow As Integer = CInt(Math.Floor(_ballY))
            Dim hCol As Integer = CInt(Math.Floor(_ballX))
            If OverHole(hRow, hCol) Then
                If Dungeon IsNot Nothing Then
                    Dungeon.Advance(oldX, oldY, _ballX, _ballY)
                    Dungeon.OnFell()
                End If
                _falls += 1
                RaiseEvent BallFell(Me, New BallFellEventArgs(hCol + 0.5F, hRow + 0.5F))
                Dim spawn = If(Dungeon Is Nothing, New Point(_maze.StartColumn, _maze.StartRow), Dungeon.RespawnPoint)
                _ballX = spawn.X + 0.5F
                _ballY = spawn.Y + 0.5F
                _velocityX = 0.0F
                _velocityY = 0.0F
                TakeHit(hCol + 0.5F, hRow + 0.5F, QuartersPerHeart, True)
                Return
            End If

            ' 7b. Heart pickups — only when a heart is missing; a taken pickup stays gone until Reset.
            If HeartsEnabled AndAlso Hearts < MaxHearts AndAlso hRow >= 0 AndAlso hRow < _maze.RowCount AndAlso
               hCol >= 0 AndAlso hCol < _maze.ColumnCount AndAlso _maze.GetTile(hRow, hCol) = "L"c AndAlso
               Not IsPickupTaken(hRow, hCol) Then
                Dim pdx As Single = _ballX - (hCol + 0.5F)
                Dim pdy As Single = _ballY - (hRow + 0.5F)
                If pdx * pdx + pdy * pdy <= PickupRadius * PickupRadius Then
                    _takenPickups.Add(hRow * _maze.ColumnCount + hCol)
                    Dim previousHearts As Single = Hearts
                    _heartQuarters = Math.Min(MaxHearts * QuartersPerHeart, _heartQuarters + QuartersPerHeart)
                    RaiseEvent HeartGained(Me, New HeartEventArgs(hCol + 0.5F, hRow + 0.5F, Hearts, previousHearts))
                End If
            End If

            If Dungeon IsNot Nothing Then
                Dungeon.Collect(_ballX, _ballY)
                Dim trapHit = Dungeon.Advance(oldX, oldY, _ballX, _ballY)
                If trapHit IsNot Nothing Then TakeHit(trapHit.X, trapHit.Y, QuartersPerHeart, False)
                If _state <> GameState.Playing Then Return
            End If

            ' 8. Goal detection — ball centre within GoalRadius of goal centre.
            Dim gdx As Single = _ballX - _goalCenterX
            Dim gdy As Single = _ballY - _goalCenterY
            If gdx * gdx + gdy * gdy <= GoalRadius * GoalRadius AndAlso (Dungeon Is Nothing OrElse Dungeon.Collapse IsNot Nothing) Then
                _state = GameState.LevelComplete
                _velocityX = 0.0F
                _velocityY = 0.0F
                If Not _completionFired Then
                    _completionFired = True
                    RaiseEvent LevelCompleted(Me, EventArgs.Empty)
                End If
            End If
        End Sub

        Private Function IsHole(r As Integer, c As Integer) As Boolean
            If r < 0 OrElse r >= _maze.RowCount OrElse c < 0 OrElse c >= _maze.ColumnCount Then Return False
            Return _maze.GetTile(r, c) = "H"c OrElse (Dungeon IsNot Nothing AndAlso Dungeon.Pit(r, c))
        End Function

        Private Function OverHole(r As Integer, c As Integer) As Boolean
            If Not IsHole(r, c) Then Return False
            Dim m As Single = 0.5F - HoleRadius
            Dim fx As Single = _ballX - c
            Dim fy As Single = _ballY - r
            If fx < m AndAlso Not IsHole(r, c - 1) Then Return False
            If fx > 1.0F - m AndAlso Not IsHole(r, c + 1) Then Return False
            If fy < m AndAlso Not IsHole(r - 1, c) Then Return False
            If fy > 1.0F - m AndAlso Not IsHole(r + 1, c) Then Return False
            Return True
        End Function

        Private Function Solid(row As Integer, column As Integer) As Boolean
            If BarrierAt(row, column) >= 0 Then Return True
            Return If(Dungeon Is Nothing, _maze.GetTile(row, column) = "1"c, Dungeon.Solid(row, column))
        End Function

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
                If Solid(r, leadCol) Then Return True
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
                If Solid(leadRow, c) Then Return True
            Next
            Return False
        End Function
    End Class

    ' Where health changed (tile-space), with exact quarter-heart totals before and after.
    Public NotInheritable Class HeartEventArgs
        Inherits EventArgs

        Public ReadOnly Property X As Single
        Public ReadOnly Property Y As Single
        Public ReadOnly Property Hearts As Single
        Public ReadOnly Property PreviousHearts As Single

        Public Sub New(x As Single, y As Single, hearts As Single, previousHearts As Single)
            Me.X = x
            Me.Y = y
            Me.Hearts = hearts
            Me.PreviousHearts = previousHearts
        End Sub
    End Class

    ' Hole centre (tile-space) where the ball fell, for effects and sound.
    Public NotInheritable Class BallFellEventArgs
        Inherits EventArgs

        Public ReadOnly Property HoleX As Single
        Public ReadOnly Property HoleY As Single

        Public Sub New(holeX As Single, holeY As Single)
            Me.HoleX = holeX
            Me.HoleY = holeY
        End Sub
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

    ' Barrier centre (tile-space) for smash and bounce feedback.
    Public NotInheritable Class BarrierEventArgs
        Inherits EventArgs

        Public ReadOnly Property X As Single
        Public ReadOnly Property Y As Single

        Public Sub New(x As Single, y As Single)
            Me.X = x
            Me.Y = y
        End Sub
    End Class
End Namespace
