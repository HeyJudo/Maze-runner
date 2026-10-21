Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports GravityMaze.Audio
Imports GravityMaze.Data
Imports GravityMaze.Engine
Imports GravityMaze.Input
Imports GravityMaze.Levels
Imports GravityMaze.Rendering

Namespace UI.Screens
    Public Enum ShellScreen
        Title
        LevelSelect
        Records
        HowToPlay
        NameEntry
        PlayerSelect
        Profile
        Calibrate
        Intro          ' level card + 3-2-1-GO; engine frozen
        Playing
        GoalTransition
        Paused
        LevelComplete
        TimeUp
        OutOfHearts
        Victory
        DungeonBriefing
        DungeonRules
        DungeonReveal
    End Enum

    ' The game loop and screen flow: menus, level intro, play, pause, results, victory.
    ' Owns which engine is shown on the canvas (attract demo behind menus, real game otherwise),
    ' routes engine events to effects and sound, and records results. Drawing lives in GameShell.Draw.vb.
    Partial Public NotInheritable Class GameShell
        Public Const TickMs As Single = 16.0F
        Private Const IntroCardMs As Single = 1600.0F
        Private Const CountStepMs As Single = 650.0F

        Public Event QuitRequested As EventHandler

        Private ReadOnly _canvas As GameCanvas
        Private ReadOnly _input As InputManager
        Private ReadOnly _sound As SoundManager
        Private ReadOnly _scores As ScoreManager
        Private ReadOnly _levels As List(Of LevelConfig)
        Private ReadOnly _mazes As New Dictionary(Of Integer, MazeDefinition)()

        Private _screen As ShellScreen = ShellScreen.Title
        Private _screenMs As Single

        ' Real game
        Private _engine As GameEngine
        Private _levelIndex As Integer
        Private _campaign As Boolean
        Private _campaignTime As Single
        Private _campaignStars As Integer
        Private _campaignFromStart As Boolean   ' only an unbroken Level 1 → end run posts a campaign record
        Private _runLogged As Boolean           ' this attempt is already in the journal
        Private _afterPlayerChosen As Action    ' what to do once a player is picked (Nothing = back to title)
        Private _skipCard As Boolean
        Private _completedAtMs As Single = -1
        Private _goalTransition As GoalDropTransition
        Private _pausedScreen As ShellScreen = ShellScreen.Playing
        Private _pausedScreenMs As Single
        Private _lastTile As Char = "0"c
        Private _heartLostAgoMs As Single = 99999.0F   ' time since the last heart loss (HUD drain animation)
        Private _heartsBeforeDamage As Single           ' health before the most recent damage animation
        Private _lastWholeSecondLeft As Integer = -1
        Private _spawnSoundInMs As Single = -1
        Private _tooSlowMs As Single = -1   ' time since a too-slow barrier hit (-1 = hidden)

        ' Result of the level just finished
        Private _resultTime As Single
        Private _resultAttempts As Integer
        Private _resultStars As Integer
        Private _resultScore As Integer
        Private _resultNewBest As Boolean
        Private ReadOnly _ghostTrail As New List(Of PointF)()
        Private _ghost As GhostRun   ' best run being raced (Nothing = none or hidden)
        Private _resultPrevBest As ScoreRecord
        Private _victoryRank As Integer

        ' Attract demo behind the menus
        Private _attractEngine As GameEngine
        Private _attractPilot As AttractPilot
        Private _attractLevel As Integer = 2      ' index into _levels; alternates Frozen / Neon
        Private _attractDoneMs As Single = -1

        ' Menus
        Private ReadOnly _titleMenu As New MenuList("PLAY", "CONTINUE", "LEVEL SELECT", "RECORDS", "HOW TO PLAY",
                                                     "PROFILE", "CALIBRATE", "SWITCH PLAYER", "QUIT")
        Private ReadOnly _levelMenu As New MenuList()
        Private _cameraKeyHeld As Boolean
        Private ReadOnly _pauseMenu As New MenuList("RESUME", "RESTART LEVEL", "MAIN MENU", "TILT VIEW: ON", "CAMERA: FULL MAZE")
        Private ReadOnly _completeMenu As New MenuList()
        Private ReadOnly _timeUpMenu As New MenuList("RETRY", "MAIN MENU")
        Private ReadOnly _heartsMenu As New MenuList("RETRY", "MAIN MENU")
        Private ReadOnly _victoryMenu As New MenuList("VIEW RECORDS", "MAIN MENU")
        Private ReadOnly _backMenu As New MenuList("BACK")
        Private ReadOnly _nameMenu As New MenuList("CONFIRM")
        Private _nameBuffer As String = ""

        Public Sub New(canvas As GameCanvas, input As InputManager, sound As SoundManager,
                       scores As ScoreManager, levels As List(Of LevelConfig))
            _canvas = canvas
            _input = input
            _sound = sound
            _scores = scores
            _levels = levels
            For Each lv As LevelConfig In levels
                _levelMenu.Items.Add(lv.ThemeName.ToUpperInvariant())
            Next
            _levelMenu.Items.Add("BACK")
            _titleMenu.Enabled = AddressOf TitleItemEnabled
            _levelMenu.Enabled = Function(i) i >= _levels.Count OrElse i < _scores.UnlockedLevel
            _canvas.OverlayPainter = AddressOf Paint
            StartAttract()
            ApplyCalibration()
            GoTo_(ShellScreen.Title)
        End Sub

        ' Current level's engine (Nothing on menus). Read-only; used by the verification tour's bot.
        Public ReadOnly Property TooSlowVisible As Boolean
            Get
                Return _tooSlowMs >= 0
            End Get
        End Property

        Public ReadOnly Property Engine As GameEngine
            Get
                Return _engine
            End Get
        End Property

        ' Highlighted item of the menu on the current screen (-1 if none).
        Public ReadOnly Property SelectedIndex As Integer
            Get
                Dim m As MenuList = CurrentMenu()
                Return If(m Is Nothing, -1, m.Selected)
            End Get
        End Property

        Private Function CurrentMenu() As MenuList
            Select Case _screen
                Case ShellScreen.Title : Return _titleMenu
                Case ShellScreen.LevelSelect : Return _levelMenu
                Case ShellScreen.Records : Return _recordsMenu
                Case ShellScreen.HowToPlay : Return _backMenu
                Case ShellScreen.PlayerSelect : Return _playerMenu
                Case ShellScreen.Profile : Return _profileMenu
                Case ShellScreen.Calibrate : Return _calibrateMenu
                Case ShellScreen.NameEntry : Return _nameMenu
                Case ShellScreen.DungeonBriefing : Return _dungeonMenu
                Case ShellScreen.DungeonRules : Return _dungeonRulesMenu
                Case ShellScreen.Paused : Return _pauseMenu
                Case ShellScreen.LevelComplete : Return _completeMenu
                Case ShellScreen.TimeUp : Return _timeUpMenu
                Case ShellScreen.OutOfHearts : Return _heartsMenu
                Case ShellScreen.Victory : Return _victoryMenu
            End Select
            Return Nothing
        End Function

        Public ReadOnly Property Screen As ShellScreen
            Get
                Return _screen
            End Get
        End Property

        ' ── Loop ────────────────────────────────────────────────────────────
        Public Sub Tick()
            _screenMs += TickMs
            If _screen <> ShellScreen.Paused AndAlso _screen <> ShellScreen.DungeonReveal Then _heartLostAgoMs += TickMs
            Dim tx As Single = _input.TiltX
            Dim ty As Single = _input.TiltY
            If _screen = ShellScreen.Playing AndAlso _completedAtMs < 0 Then _canvas.UpdateBoardTilt(tx, ty, TickMs)

            Select Case _screen
                Case ShellScreen.Title
                    TickAttract()
                    Select Case Nav(_titleMenu, tx, ty)
                        Case MenuAction.Confirm : TitleChoice(_titleMenu.Selected)
                    End Select
                Case ShellScreen.LevelSelect
                    ' The demo behind the list previews the highlighted level.
                    If _levelMenu.Selected < _levels.Count AndAlso _levelMenu.Selected <> _attractLevel Then
                        _attractLevel = _levelMenu.Selected
                        StartAttract()
                        _canvas.BackColor = CurrentPalette().Backdrop
                    End If
                    TickAttract()
                    Select Case Nav(_levelMenu, tx, ty)
                        Case MenuAction.Confirm
                            If _levelMenu.Selected >= _levels.Count Then
                                Back()
                            ElseIf _levelMenu.Enabled(_levelMenu.Selected) Then
                                StartLevel(_levelMenu.Selected, campaign:=False)
                            End If
                        Case MenuAction.Back : Back()
                    End Select
                Case ShellScreen.Records
                    TickAttract()
                    Select Case Nav(_recordsMenu, tx, ty)
                        Case MenuAction.Confirm : RecordsChoice(_recordsMenu.Selected)
                        Case MenuAction.Back : Back()
                    End Select
                Case ShellScreen.HowToPlay
                    TickAttract()
                    Dim a As MenuAction = Nav(_backMenu, tx, ty)
                    If a = MenuAction.Confirm OrElse a = MenuAction.Back Then Back()
                Case ShellScreen.PlayerSelect
                    TickAttract()
                    Select Case Nav(_playerMenu, tx, ty)
                        Case MenuAction.Confirm : PlayerChoice(_playerMenu.Selected)
                        Case MenuAction.Back : _afterPlayerChosen = Nothing : GoTo_(ShellScreen.Title)
                    End Select
                Case ShellScreen.Profile
                    TickAttract()
                    Select Case Nav(_profileMenu, tx, ty)
                        Case MenuAction.Confirm : ProfileChoice(_profileMenu.Selected)
                        Case MenuAction.Back : GoTo_(ShellScreen.Title)
                    End Select
                Case ShellScreen.Calibrate
                    TickAttract()
                    TickCalibrate(tx, ty)
                Case ShellScreen.NameEntry
                    TickAttract()
                    Select Case Nav(_nameMenu, tx, ty)
                        Case MenuAction.Confirm : ConfirmName()
                        Case MenuAction.Back : Back()
                    End Select
                Case ShellScreen.DungeonBriefing
                    If Nav(_dungeonMenu, tx, ty) = MenuAction.Confirm Then DungeonBriefingChoice(_dungeonMenu.Selected)
                Case ShellScreen.DungeonRules
                    Dim action = Nav(_dungeonRulesMenu, tx, ty)
                    If action = MenuAction.Confirm OrElse action = MenuAction.Back Then ReturnFromDungeonRules()
                Case ShellScreen.DungeonReveal
                    TickDungeonReveal()
                Case ShellScreen.Intro
                    TickIntro()
                Case ShellScreen.Playing
                    TickPlaying(tx, ty)
                Case ShellScreen.GoalTransition
                    TickGoalTransition()
                Case ShellScreen.Paused
                    Select Case Nav(_pauseMenu, tx, ty)
                        Case MenuAction.Confirm : PauseChoice(_pauseMenu.Selected)
                        Case MenuAction.Back : ResumeGame()
                    End Select
                Case ShellScreen.LevelComplete
                    TickResultStars()
                    If _screenMs > 1300 AndAlso Nav(_completeMenu, tx, ty) = MenuAction.Confirm Then CompleteChoice(_completeMenu.Selected)
                Case ShellScreen.TimeUp
                    If _screenMs > 600 AndAlso Nav(_timeUpMenu, tx, ty) = MenuAction.Confirm Then
                        If _timeUpMenu.Selected = 0 Then Retry() Else ToMainMenu()
                    End If
                Case ShellScreen.OutOfHearts
                    If _screenMs > 600 AndAlso Nav(_heartsMenu, tx, ty) = MenuAction.Confirm Then
                        If _heartsMenu.Selected = 0 Then Retry() Else ToMainMenu()
                    End If
                Case ShellScreen.Victory
                    If _screenMs > 1500 AndAlso Nav(_victoryMenu, tx, ty) = MenuAction.Confirm Then
                        If _victoryMenu.Selected = 0 Then GoTo_(ShellScreen.Records) Else ToMainMenu()
                    End If
            End Select

            If IsGameScreen() Then
                _canvas.DungeonState = _engine.Dungeon
                _canvas.PickupTaken = AddressOf _engine.IsPickupTaken
                _canvas.BarrierStanding = AddressOf _engine.IsBarrierStanding
                Dim speed As Single = CSng(Math.Sqrt(_engine.VelocityX * _engine.VelocityX + _engine.VelocityY * _engine.VelocityY))
                _canvas.BarrierCharge = If(_screen = ShellScreen.Playing, Math.Min(1.0F, speed / _engine.BarrierBreakSpeed), 0.0F)
                _canvas.BallBlink = _engine.IsInvulnerable AndAlso _screen = ShellScreen.Playing
                _canvas.GhostPosition = If(_ghost IsNot Nothing AndAlso (_screen = ShellScreen.Playing OrElse _screen = ShellScreen.Paused),
                                           _ghost.PositionAt(_engine.ElapsedSeconds), CType(Nothing, PointF?))
                If _screen <> ShellScreen.Paused AndAlso _screen <> ShellScreen.DungeonReveal Then
                    _canvas.UpdateBallPosition(_engine.BallX, _engine.BallY)
                    _canvas.UpdateBallMotion(If(_screen = ShellScreen.Playing, _engine.VelocityX, 0.0F),
                                             If(_screen = ShellScreen.Playing, _engine.VelocityY, 0.0F), TileUnder(_engine))
                Else
                    _canvas.Invalidate() ' keep pause-menu navigation repainting while effects stay frozen
                End If
            Else
                _canvas.DungeonState = _attractEngine.Dungeon
                _canvas.PickupTaken = Nothing
                _canvas.BarrierStanding = AddressOf _attractEngine.IsBarrierStanding
                _canvas.BarrierCharge = 0.0F
                _canvas.GhostPosition = Nothing
                _canvas.BallBlink = False
                _canvas.UpdateBallPosition(_attractEngine.BallX, _attractEngine.BallY)
                _canvas.UpdateBallMotion(_attractEngine.VelocityX, _attractEngine.VelocityY, TileUnder(_attractEngine))
            End If
            UpdateInsets()
            _canvas.UpdateCamera(TickMs)
        End Sub

        Private Function Nav(menu As MenuList, tx As Single, ty As Single) As MenuAction
            Dim a As MenuAction = menu.Update(tx, ty, TickMs)
            Select Case a
                Case MenuAction.Moved : _sound.Play("menu_move", 0.6F)
                Case MenuAction.Confirm : _sound.Play("menu_confirm")
                Case MenuAction.Back : _sound.Play("menu_back")
            End Select
            Return a
        End Function

        Private Function IsGameScreen() As Boolean
            If _engine Is Nothing Then Return False
            Select Case _screen
                Case ShellScreen.Intro, ShellScreen.Playing, ShellScreen.GoalTransition, ShellScreen.Paused,
                     ShellScreen.LevelComplete, ShellScreen.TimeUp, ShellScreen.OutOfHearts,
                     ShellScreen.DungeonBriefing, ShellScreen.DungeonRules, ShellScreen.DungeonReveal
                    Return True
            End Select
            Return False
        End Function

        Private Function TileUnder(e As GameEngine) As Char
            Dim m As MazeDefinition = e.Maze
            Dim r As Integer = Math.Max(0, Math.Min(m.RowCount - 1, CInt(Math.Floor(e.BallY))))
            Dim c As Integer = Math.Max(0, Math.Min(m.ColumnCount - 1, CInt(Math.Floor(e.BallX))))
            Return m.GetTile(r, c)
        End Function

        ' Menus put the board on the right (menu on the left); game screens leave room for the HUD.
        Private Sub UpdateInsets()
            _canvas.CameraEnabled = IsGameScreen()
            _canvas.PresentationOverview = DungeonOverviewActive()
            Dim w As Integer = _canvas.ClientSize.Width
            Dim h As Integer = _canvas.ClientSize.Height
            Dim s As Single = UiScale()
            Dim wanted As Padding
            If DungeonOverviewActive() Then
                wanted = New Padding(CInt(w * 0.36F), CInt(100 * s), CInt(60 * s), CInt(100 * s))
            ElseIf IsGameScreen() Then
                wanted = New Padding(CInt(24 * s), CInt(If(_engine.Dungeon Is Nothing, 118, 280) * s), CInt(24 * s), CInt(56 * s))
            Else
                wanted = New Padding(CInt(w * 0.42F), CInt(40 * s), CInt(40 * s), CInt(40 * s))
            End If
            If _canvas.BoardInsets <> wanted Then _canvas.BoardInsets = wanted
            If h <= 0 Then Return
        End Sub

        Private Function UiScale() As Single
            Return Math.Max(0.6F, _canvas.ClientSize.Height / 1080.0F)
        End Function

        Private Sub GoTo_(screen As ShellScreen)
            _screen = screen
            _screenMs = 0
            Select Case screen
                Case ShellScreen.Title
                    Dim ci As Integer = ContinueIndex()
                    _titleMenu.Items(1) = If(ci >= 0, $"CONTINUE · LEVEL {ci + 1}", "CONTINUE")
                    _titleMenu.Reset(If(TitleItemEnabled(_titleMenu.Selected), _titleMenu.Selected, 0))
                Case ShellScreen.LevelSelect
                    _levelMenu.Reset(If(_levelMenu.Enabled(_levelMenu.Selected), _levelMenu.Selected, 0))
                Case ShellScreen.Records : _recordsMenu.Reset()
                Case ShellScreen.HowToPlay : _backMenu.Reset()
                Case ShellScreen.Profile : _profileMenu.Reset()
                Case ShellScreen.NameEntry : _nameMenu.Reset() : ClearNameNotice()
                Case ShellScreen.DungeonBriefing : _dungeonMenu.Reset(_dungeonMenu.Selected)
                Case ShellScreen.DungeonRules : _dungeonRulesMenu.Reset()
                Case ShellScreen.Paused : _pauseMenu.Reset()
                Case ShellScreen.TimeUp : _timeUpMenu.Reset()
                Case ShellScreen.OutOfHearts : _heartsMenu.Reset()
                Case ShellScreen.Victory : _victoryMenu.Reset()
            End Select
            If screen <> ShellScreen.Playing Then _sound.StopLoop("ice_slide")
            _canvas.BackColor = CurrentPalette().Backdrop
        End Sub

        ' ── Menus ───────────────────────────────────────────────────────────
        ' Level index Continue resumes at, or -1 when there is nothing to continue.
        Private Function ContinueIndex() As Integer
            Dim u As Integer = _scores.UnlockedLevel
            Return If(u > 1 AndAlso u <= _levels.Count, u - 1, -1)
        End Function

        Public Function TitleItemEnabled(index As Integer) As Boolean
            Return index <> 1 OrElse ContinueIndex() >= 0
        End Function

        Private Sub TitleChoice(index As Integer)
            Select Case index
                Case 0 : WithPlayer(Sub() StartCampaign(0))
                Case 1 : WithPlayer(Sub() StartCampaign(Math.Max(0, ContinueIndex())))
                Case 2 : WithPlayer(Sub() GoTo_(ShellScreen.LevelSelect))
                Case 3 : GoTo_(ShellScreen.Records)
                Case 4 : GoTo_(ShellScreen.HowToPlay)
                Case 5 : WithPlayer(AddressOf OpenProfile)
                Case 6 : OpenCalibration()
                Case 7 : OpenPlayerSelect(Nothing)
                Case 8 : RaiseEvent QuitRequested(Me, EventArgs.Empty)
            End Select
        End Sub

        Private Sub StartCampaign(fromIndex As Integer)
            _campaignTime = 0
            _campaignStars = 0
            _campaignFromStart = fromIndex = 0
            StartLevel(fromIndex, campaign:=True)
        End Sub

        Private Sub OpenNameEntry()
            _nameBuffer = ""
            GoTo_(ShellScreen.NameEntry)
        End Sub

        Private Sub ConfirmName()
            Dim name As String = _nameBuffer.Trim()
            If name.Length = 0 Then Return
            If Not ConfirmNewName(name) Then Return
            _scores.PlayerName = name
            ContinueAfterPlayer()
        End Sub

        Private Sub ContinueAfterPlayer()
            Dim nextStep As Action = _afterPlayerChosen
            _afterPlayerChosen = Nothing
            If nextStep Is Nothing Then GoTo_(ShellScreen.Title) Else nextStep()
        End Sub

        ' Runs action now if a player is chosen, otherwise after the player picks or types one.
        Private Sub WithPlayer(action As Action)
            If _scores.PlayerName.Length > 0 Then action() : Return
            OpenPlayerSelect(action)
        End Sub

        Private Sub Back()
            Select Case _screen
                Case ShellScreen.Records
                    GoTo_(If(_engine IsNot Nothing AndAlso _campaignStars > 0 AndAlso _victoryRank > 0, ShellScreen.Title, ShellScreen.Title))
                Case ShellScreen.NameEntry
                    _afterPlayerChosen = Nothing
                    GoTo_(ShellScreen.Title)
                Case ShellScreen.LevelSelect, ShellScreen.HowToPlay, ShellScreen.Profile, ShellScreen.PlayerSelect, ShellScreen.Calibrate
                    GoTo_(ShellScreen.Title)
            End Select
        End Sub

        ' Every attempt that actually moved is journaled once; cleared runs go through RecordResult.
        Private Sub LogUnfinishedRun(outcome As String)
            If _engine Is Nothing OrElse _completedAtMs >= 0 OrElse _runLogged Then Return
            _runLogged = True
            _scores.AddRecord(New ScoreRecord With {
                .PlayerName = _scores.PlayerName, .LevelNumber = _levels(_levelIndex).LevelNumber, .Outcome = outcome,
                .TimeSeconds = _engine.ElapsedSeconds, .Attempts = _engine.Attempts, .WallHits = _engine.WallHits,
                .Falls = _engine.Falls, .HeartsLost = _engine.HeartsLost, .BarriersBroken = _engine.BarriersBroken,
                .Date = DateTime.Now})
        End Sub

        ' Window closing mid-run counts as a quit.
        Public Sub OnClosing()
            LogUnfinishedRun(RunOutcome.Quit)
        End Sub

        Private Sub ToMainMenu()
            LogUnfinishedRun(RunOutcome.Quit)
            RestoreDungeonPreferences()
            _engine = Nothing
            _campaign = False
            ShowAttractMaze()
            GoTo_(ShellScreen.Title)
        End Sub

        ' ── Level flow ──────────────────────────────────────────────────────
        Private Function MazeFor(index As Integer) As MazeDefinition
            Dim m As MazeDefinition = Nothing
            If Not _mazes.TryGetValue(index, m) Then
                m = MazeManager.LoadFromFile(Path.Combine(AppContext.BaseDirectory, _levels(index).MazePath))
                _mazes(index) = m
            End If
            Return m
        End Function

        Private Sub StartLevel(index As Integer, campaign As Boolean, Optional readyToPlay As Boolean = False)
            RestoreDungeonPreferences()
            _campaign = campaign
            _levelIndex = index
            Dim cfg As LevelConfig = _levels(index)
            If _engine IsNot Nothing Then DetachEngine(_engine)
            _engine = New GameEngine(MazeFor(index), cfg.TimeLimitSecs)
            If _engine.Dungeon IsNot Nothing Then AddHandler _engine.Dungeon.Cue, AddressOf OnDungeonCue
            AddHandler _engine.WallImpacted, AddressOf OnWallImpacted
            AddHandler _engine.BallFell, AddressOf OnBallFell
            AddHandler _engine.HeartLost, AddressOf OnHeartLost
            AddHandler _engine.HeartGained, AddressOf OnHeartGained
            AddHandler _engine.BarrierBroken, AddressOf OnBarrierBroken
            AddHandler _engine.BarrierBounced, AddressOf OnBarrierBounced
            _canvas.ShowMaze(_engine.Maze, cfg.ThemeName)
            _canvas.DungeonState = _engine.Dungeon
            _skipCard = False
            BeginRun()
            If _engine.Dungeon IsNot Nothing Then
                CaptureDungeonPreferences()
                _dungeonMenu.Reset()
                RefreshCameraLabels()
                GoTo_(ShellScreen.DungeonBriefing)
            ElseIf readyToPlay Then
                GoTo_(ShellScreen.Playing)
            End If
        End Sub

        Private Sub DetachEngine(e As GameEngine)
            If e.Dungeon IsNot Nothing Then RemoveHandler e.Dungeon.Cue, AddressOf OnDungeonCue
            RemoveHandler e.WallImpacted, AddressOf OnWallImpacted
            RemoveHandler e.BallFell, AddressOf OnBallFell
            RemoveHandler e.HeartLost, AddressOf OnHeartLost
            RemoveHandler e.HeartGained, AddressOf OnHeartGained
            RemoveHandler e.BarrierBroken, AddressOf OnBarrierBroken
            RemoveHandler e.BarrierBounced, AddressOf OnBarrierBounced
        End Sub

        Private Sub BeginRun()
            _runLogged = False
            _goalTransition = Nothing
            _completedAtMs = -1
            _lastTile = "0"c
            _lastWholeSecondLeft = -1
            _spawnSoundInMs = -1
            _tooSlowMs = -1
            _heartLostAgoMs = 99999.0F
            _canvas.ClearEffects()
            _ghostTrail.Clear()
            _ghost = If(_scores.GhostEnabled, _scores.GhostFor(_levels(_levelIndex).LevelNumber), Nothing)
            ResetDungeonPresentation()
            GoTo_(ShellScreen.Intro)
        End Sub

        Private Sub Retry()
            LogUnfinishedRun(RunOutcome.Restart)
            If _campaign AndAlso _completedAtMs >= 0 Then
                _campaignTime -= _resultTime
                _campaignStars -= _resultStars
            End If
            _engine.Reset()
            _skipCard = True
            BeginRun()
        End Sub

        Private Sub TickIntro()
            Dim cardMs As Single = If(_skipCard, 0.0F, IntroCardMs)
            Dim t As Single = _screenMs - cardMs
            ' Beep on each count: 3 at 0, 2 at 1 step, 1 at 2 steps, GO at 3 steps.
            For i As Integer = 0 To 3
                Dim at As Single = i * CountStepMs
                If t >= at AndAlso t - TickMs < at Then _sound.Play(If(i = 3, "go", "countdown"))
            Next
            If t >= 3 * CountStepMs Then GoTo_(ShellScreen.Playing)
        End Sub

        Private Sub TickPlaying(tx As Single, ty As Single)
            If TryBeginDungeonReveal() Then Return
            TickDungeonMessages()
            If _completedAtMs < 0 Then
                _engine.Update(tx, ty)
                While _ghostTrail.Count * GhostRun.DefaultStepMs <= _engine.ElapsedSeconds * 1000.0F
                    _ghostTrail.Add(New PointF(_engine.BallX, _engine.BallY))
                End While
                If TryBeginDungeonReveal() Then Return
            End If

            ' Terrain sounds
            Dim tile As Char = TileUnder(_engine)
            Dim speed As Single = CSng(Math.Sqrt(_engine.VelocityX * _engine.VelocityX + _engine.VelocityY * _engine.VelocityY))
            If tile = "I"c AndAlso speed > 0.02F AndAlso _engine.State = GameState.Playing Then
                _sound.StartLoop("ice_slide", 0.0F)
                _sound.SetLoopVolume("ice_slide", Math.Min(1.0F, speed / 0.14F))
            Else
                _sound.StopLoop("ice_slide")
            End If
            If tile = "F"c AndAlso _lastTile <> "F"c Then _sound.Play("boost", 0.8F)
            _lastTile = tile
            If _spawnSoundInMs >= 0 Then
                _spawnSoundInMs -= TickMs
                If _spawnSoundInMs < 0 Then _sound.Play("spawn", 0.7F)
            End If
            If _tooSlowMs >= 0 Then
                _tooSlowMs += TickMs
                If _tooSlowMs > 900.0F Then _tooSlowMs = -1
            End If

            ' Timer warnings in the last five seconds
            Dim cfg As LevelConfig = _levels(_levelIndex)
            If cfg.TimeLimitSecs > 0 AndAlso _engine.State = GameState.Playing Then
                Dim whole As Integer = CInt(Math.Ceiling(_engine.TimeRemainingSeconds))
                If whole <= 5 AndAlso whole <> _lastWholeSecondLeft AndAlso whole > 0 Then _sound.Play("tick")
                _lastWholeSecondLeft = whole
            End If

            If _engine.State = GameState.TimeUp Then
                _sound.Play("time_up")
                LogUnfinishedRun(RunOutcome.TimeUp)
                GoTo_(ShellScreen.TimeUp)
            ElseIf _engine.State = GameState.OutOfHearts Then
                _sound.Play("time_up")
                LogUnfinishedRun(RunOutcome.OutOfHearts)
                GoTo_(ShellScreen.OutOfHearts)
            ElseIf _engine.State = GameState.LevelComplete Then
                If _completedAtMs < 0 Then
                    _completedAtMs = _screenMs
                    _canvas.TriggerGoalCelebration(_engine.Maze.GoalColumn + 0.5F, _engine.Maze.GoalRow + 0.5F)
                    _sound.Play("goal")
                    RecordResult()
                    Dim nextIndex As Integer = _levelIndex + 1
                    Dim advance As Boolean = _campaign AndAlso nextIndex < _levels.Count
                    _goalTransition = New GoalDropTransition(advance)
                    _canvas.BeginGoalDrop(_goalTransition, If(advance, MazeFor(nextIndex), Nothing),
                                          If(advance, _levels(nextIndex).ThemeName, Nothing))
                    _starsPlayed = 0
                    GoTo_(ShellScreen.GoalTransition)
                End If
            End If
        End Sub

        Private Sub TickGoalTransition()
            Dim previousElapsed As Single = _goalTransition.ElapsedMs
            _goalTransition.Advance(TickMs)
            TickResultStars()
            If _goalTransition.HasNextLevel AndAlso previousElapsed < GoalDropTransition.LandingContactMs AndAlso
               _goalTransition.ElapsedMs >= GoalDropTransition.LandingContactMs Then _sound.Play("spawn")
            If _goalTransition.Phase <> GoalDropPhase.Finished Then Return
            _canvas.EndGoalDrop()
            If Not _campaign Then
                EnterLevelComplete()
            ElseIf _goalTransition.HasNextLevel Then
                StartLevel(_levelIndex + 1, campaign:=True, readyToPlay:=True)
                _sound.Play("go")
            Else
                EnterVictory()
            End If
        End Sub

        Private Sub RecordResult()
            Dim cfg As LevelConfig = _levels(_levelIndex)
            _resultTime = _engine.ElapsedSeconds
            _resultAttempts = _engine.Attempts
            _resultStars = cfg.StarsFor(_resultTime)
            _resultScore = ScoreManager.ComputeScore(_resultTime, _resultAttempts, _resultStars)
            _resultPrevBest = _scores.BestRecordFor(_scores.PlayerName, cfg.LevelNumber)
            _resultNewBest = _scores.AddRecord(New ScoreRecord With {
                .PlayerName = _scores.PlayerName, .LevelNumber = cfg.LevelNumber, .TimeSeconds = _resultTime,
                .Attempts = _resultAttempts, .Stars = _resultStars, .Score = _resultScore, .Date = DateTime.Now,
                .WallHits = _engine.WallHits, .Falls = _engine.Falls, .HeartsLost = _engine.HeartsLost, .BarriersBroken = _engine.BarriersBroken})
            _ghostTrail.Add(New PointF(_engine.BallX, _engine.BallY))
            If _resultNewBest Then _scores.SaveGhost(cfg.LevelNumber, New GhostRun(_resultTime, GhostRun.DefaultStepMs, _ghostTrail))
            _runLogged = True
            If _campaign Then
                _campaignTime += _resultTime
                _campaignStars += _resultStars
            End If
        End Sub

        Private Sub EnterLevelComplete()
            _completeMenu.Items.Clear()
            Dim last As Boolean = _levelIndex = _levels.Count - 1
            If last Then
                _completeMenu.Items.Add(If(_campaign, "FINISH", "MAIN MENU"))
            Else
                _completeMenu.Items.Add("NEXT LEVEL")
            End If
            _completeMenu.Items.Add("RETRY")
            If Not (last AndAlso Not _campaign) Then _completeMenu.Items.Add("MAIN MENU")
            _completeMenu.Reset()
            _starsPlayed = 0
            GoTo_(ShellScreen.LevelComplete)
        End Sub

        Private _starsPlayed As Integer

        Private Sub TickResultStars()
            For i As Integer = 1 To _resultStars
                If _starsPlayed < i AndAlso _screenMs >= StarRevealMs(i) Then
                    _starsPlayed = i
                    _sound.Play("star", 0.8F + i * 0.1F)
                End If
            Next
        End Sub

        Private Shared Function StarRevealMs(i As Integer) As Single
            Return 450.0F + i * 250.0F
        End Function

        Private Sub CompleteChoice(index As Integer)
            Dim label As String = _completeMenu.Items(index)
            Select Case label
                Case "NEXT LEVEL" : StartLevel(_levelIndex + 1, _campaign)
                Case "RETRY"
                    Retry()
                Case "FINISH" : EnterVictory()
                Case Else : ToMainMenu()
            End Select
        End Sub

        Private Sub EnterVictory()
            RestoreDungeonPreferences()
            ShowAttractMaze()
            _victoryRank = 0
            If _campaignFromStart Then
                _scores.AddRecord(New ScoreRecord With {
                    .PlayerName = _scores.PlayerName, .LevelNumber = 0, .CampaignLevels = _levels.Count, .TimeSeconds = _campaignTime,
                    .Attempts = 1, .Stars = _campaignStars, .Score = 0, .Date = DateTime.Now})
                Dim mine As String = ScoreManager.KeyOf(_scores.PlayerName)
                _victoryRank = _scores.TopRecords(0, 1000, _levels.Count).FindIndex(
                    Function(r) ScoreManager.KeyOf(r.PlayerName) = mine AndAlso Math.Abs(r.TimeSeconds - _campaignTime) < 0.001F) + 1
            End If
            _sound.Play("victory")
            GoTo_(ShellScreen.Victory)
        End Sub

        Private Sub PauseChoice(index As Integer)
            Select Case index
                Case 0 : ResumeGame()
                Case 1 : Retry()
                Case 2 : ToMainMenu()
                Case 3 : ToggleTiltView()
                Case 4 : CycleCameraZoom()
                Case 5 : OpenDungeonRules(ShellScreen.Paused)
            End Select
        End Sub

        Private Sub Pause()
            _pausedScreen = _screen
            _pausedScreenMs = _screenMs
            GoTo_(ShellScreen.Paused)
        End Sub

        Private Sub ResumeGame()
            _screen = _pausedScreen
            _screenMs = _pausedScreenMs
        End Sub

        ' ── Engine events → effects + sound ─────────────────────────────────
        Private Sub OnWallImpacted(sender As Object, e As WallImpactEventArgs)
            _canvas.AddImpact(e.ImpactX, e.ImpactY, e.NormalX, e.NormalY, e.Speed)
            _sound.Play("wall_hit", Math.Max(0.25F, Math.Min(1.0F, e.Speed / 0.14F)))
        End Sub

        Private Sub OnBallFell(sender As Object, e As BallFellEventArgs)
            _canvas.AddHoleFall(e.HoleX, e.HoleY)
            _sound.Play("hole_fall")
            _spawnSoundInMs = 450.0F
        End Sub

        Private Sub OnDungeonCue(sender As Object, e As DungeonCueEventArgs)
            Select Case e.Kind
                Case "key"
                    _sound.Play("dungeon_seal", 0.8F)
                    _canvas.AddHeartBurst(e.X, e.Y)
                Case "collapse"
                    _sound.Play("dungeon_warning", 1.0F)
                    _sound.Play("hole_fall", 0.6F)
                    _canvas.Shake(4000.0F, 5.0F)
                Case "cavein_warning"
                    _sound.Play("dungeon_warning", 0.5F)
                Case "cavein"
                    _sound.Play("wall_hit", 0.9F)
                    _canvas.Shake(300.0F, 7.0F)
                Case "crumble"
                    _sound.Play("dungeon_warning", 0.3F)
                Case "checkpoint"
                    ShowDungeonCheckpoint()
                    _sound.Play("dungeon_seal", 0.4F)
                Case "warning"
                    _sound.Play("dungeon_warning", 0.45F)
            End Select
        End Sub

        Private Sub OnHeartLost(sender As Object, e As HeartEventArgs)
            _heartLostAgoMs = 0
            _heartsBeforeDamage = e.PreviousHearts
            _canvas.FlashDamage()
            _sound.Play("wall_hit", 1.0F)
        End Sub

        Private Sub OnHeartGained(sender As Object, e As HeartEventArgs)
            _canvas.AddHeartBurst(e.X, e.Y)
            _sound.Play("menu_confirm", 0.8F)
        End Sub

        Private Sub OnBarrierBroken(sender As Object, e As BarrierEventArgs)
            _canvas.AddImpact(e.X, e.Y, -Math.Sign(_engine.VelocityX), -Math.Sign(_engine.VelocityY), 0.24F)
            _canvas.Shake(220.0F, 4.0F)
            _sound.Play("goal", 0.55F)
        End Sub

        Private Sub OnBarrierBounced(sender As Object, e As BarrierEventArgs)
            _tooSlowMs = 0
            _sound.Play("barrier_slow") ' ponytail: reuses menu_back.wav until a bespoke cue exists
        End Sub

        Private Sub OnAttractImpact(sender As Object, e As WallImpactEventArgs)
            If Not IsGameScreen() Then _canvas.AddImpact(e.ImpactX, e.ImpactY, e.NormalX, e.NormalY, e.Speed)
        End Sub

        ' ── Attract mode ────────────────────────────────────────────────────
        Private Sub StartAttract()
            If _attractEngine IsNot Nothing Then RemoveHandler _attractEngine.WallImpacted, AddressOf OnAttractImpact
            Dim maze As MazeDefinition = MazeFor(_attractLevel)
            _attractEngine = New GameEngine(maze, 0) With {.HeartsEnabled = False}
            AddHandler _attractEngine.WallImpacted, AddressOf OnAttractImpact
            _attractPilot = New AttractPilot(maze)
            _attractDoneMs = -1
            ShowAttractMaze()
        End Sub

        Private Sub ShowAttractMaze()
            _canvas.ShowMaze(_attractEngine.Maze, _levels(_attractLevel).ThemeName)
            _canvas.DungeonState = _attractEngine.Dungeon
        End Sub

        Private Sub TickAttract()
            If _attractEngine.State = GameState.Playing Then
                Dim tilt As PointF = _attractPilot.NextTilt(_attractEngine)
                _attractEngine.Update(tilt.X, tilt.Y)
            ElseIf _attractDoneMs < 0 Then
                _attractDoneMs = 0
                _canvas.TriggerGoalCelebration(_attractEngine.BallX, _attractEngine.BallY)
            Else
                _attractDoneMs += TickMs
                If _attractDoneMs > 1800 Then
                    _attractLevel = If(_attractLevel = 2, 1, 2)
                    StartAttract()
                    _canvas.BackColor = CurrentPalette().Backdrop
                End If
            End If
        End Sub

        ' ── Keyboard shortcuts (tilt handles navigation) ────────────────────
        Public Function HandleKey(key As Keys) As Boolean
            If key = Keys.F9 Then
                If Not _cameraKeyHeld Then CycleCameraZoom()
                _cameraKeyHeld = True
                Return True
            End If
            If key = Keys.Tab AndAlso IsGameScreen() Then
                _canvas.CameraOverviewHeld = True
                _canvas.Invalidate()
                Return True
            End If
            If key = Keys.F8 Then
                ToggleTiltView()
                Return True
            End If
            If key = Keys.M Then
                _sound.Muted = Not _sound.Muted
                Return True
            End If
            If key = Keys.G AndAlso (_screen = ShellScreen.Playing OrElse _screen = ShellScreen.Paused OrElse _screen = ShellScreen.Intro) Then
                _scores.GhostEnabled = Not _scores.GhostEnabled
                _ghost = If(_scores.GhostEnabled, _scores.GhostFor(_levels(_levelIndex).LevelNumber), Nothing)
                RefreshProfileLabels()
                _sound.Play("menu_move", 0.6F)
                Return True
            End If
            Select Case _screen
                Case ShellScreen.DungeonBriefing
                    If IsEnter(key) Then DungeonBriefingChoice(_dungeonMenu.Selected) : Return True
                    If key = Keys.Escape OrElse key = Keys.P Then Pause() : Return True
                Case ShellScreen.DungeonRules
                    If IsEnter(key) OrElse key = Keys.Escape Then ReturnFromDungeonRules() : Return True
                    If key = Keys.P Then Pause() : Return True
                Case ShellScreen.Intro, ShellScreen.Playing, ShellScreen.GoalTransition, ShellScreen.DungeonReveal
                    If key = Keys.Escape OrElse key = Keys.P Then Pause() : Return True
                    If key = Keys.R AndAlso _completedAtMs < 0 Then Retry() : Return True
                Case ShellScreen.Paused
                    If key = Keys.Escape OrElse key = Keys.P Then ResumeGame() : Return True
                    If key = Keys.R Then Retry() : Return True
                    If IsEnter(key) Then _sound.Play("menu_confirm") : PauseChoice(_pauseMenu.Selected) : Return True
                Case ShellScreen.Title
                    If IsEnter(key) AndAlso TitleItemEnabled(_titleMenu.Selected) Then _sound.Play("menu_confirm") : TitleChoice(_titleMenu.Selected) : Return True
                Case ShellScreen.LevelSelect
                    If IsEnter(key) Then
                        _sound.Play("menu_confirm")
                        If _levelMenu.Selected >= _levels.Count Then
                            Back()
                        ElseIf _levelMenu.Enabled(_levelMenu.Selected) Then
                            StartLevel(_levelMenu.Selected, campaign:=False)
                        End If
                        Return True
                    End If
                    If key = Keys.Escape Then _sound.Play("menu_back") : Back() : Return True
                Case ShellScreen.Records
                    If IsEnter(key) Then _sound.Play("menu_confirm") : RecordsChoice(_recordsMenu.Selected) : Return True
                    If key = Keys.Escape Then _sound.Play("menu_back") : Back() : Return True
                Case ShellScreen.HowToPlay
                    If IsEnter(key) OrElse key = Keys.Escape Then _sound.Play("menu_back") : Back() : Return True
                Case ShellScreen.PlayerSelect
                    If IsEnter(key) Then _sound.Play("menu_confirm") : PlayerChoice(_playerMenu.Selected) : Return True
                    If key = Keys.Escape Then _sound.Play("menu_back") : _afterPlayerChosen = Nothing : GoTo_(ShellScreen.Title) : Return True
                Case ShellScreen.Profile
                    If IsEnter(key) Then _sound.Play("menu_confirm") : ProfileChoice(_profileMenu.Selected) : Return True
                    If key = Keys.Escape Then _sound.Play("menu_back") : GoTo_(ShellScreen.Title) : Return True
                Case ShellScreen.Calibrate
                    If _testingTilt AndAlso (IsEnter(key) OrElse key = Keys.Escape) Then _testingTilt = False : _calibrateMenu.Reset(6) : Return True
                    If IsEnter(key) Then _sound.Play("menu_confirm") : CalibrationChoice(_calibrateMenu.Selected) : Return True
                    If key = Keys.Escape Then _sound.Play("menu_back") : GoTo_(ShellScreen.Title) : Return True
                Case ShellScreen.NameEntry
                    If key = Keys.Enter Then _sound.Play("menu_confirm") : ConfirmName() : Return True
                    If key = Keys.Escape Then _sound.Play("menu_back") : Back() : Return True
                    If key = Keys.Back AndAlso _nameBuffer.Length > 0 Then
                        _nameBuffer = _nameBuffer.Substring(0, _nameBuffer.Length - 1)
                        ClearNameNotice()
                        _sound.Play("menu_move", 0.5F)
                        Return True
                    End If
                Case ShellScreen.LevelComplete
                    If _screenMs > 1300 AndAlso IsEnter(key) Then _sound.Play("menu_confirm") : CompleteChoice(_completeMenu.Selected) : Return True
                    If key = Keys.R Then CompleteChoice(_completeMenu.Items.IndexOf("RETRY")) : Return True
                Case ShellScreen.TimeUp
                    If key = Keys.R OrElse (IsEnter(key) AndAlso _timeUpMenu.Selected = 0) Then Retry() : Return True
                    If IsEnter(key) OrElse key = Keys.Escape Then ToMainMenu() : Return True
                Case ShellScreen.OutOfHearts
                    If key = Keys.R OrElse (IsEnter(key) AndAlso _heartsMenu.Selected = 0) Then Retry() : Return True
                    If IsEnter(key) OrElse key = Keys.Escape Then ToMainMenu() : Return True
                Case ShellScreen.Victory
                    If _screenMs > 1500 AndAlso IsEnter(key) Then
                        _sound.Play("menu_confirm")
                        If _victoryMenu.Selected = 0 Then GoTo_(ShellScreen.Records) Else ToMainMenu()
                        Return True
                    End If
            End Select
            Return False
        End Function

        Public Sub HandleKeyUp(key As Keys)
            If key = Keys.F9 Then _cameraKeyHeld = False
            If key = Keys.Tab Then
                _canvas.CameraOverviewHeld = False
                If _screen = ShellScreen.Paused AndAlso _pausedScreen = ShellScreen.DungeonReveal Then ApplyDungeonRevealFrame()
                _canvas.Invalidate()
            End If
        End Sub

        Private Sub CycleCameraZoom()
            _canvas.CycleCameraZoom()
            RefreshCameraLabels()
            _sound.Play("menu_move", 0.6F)
        End Sub

        Private Sub ToggleTiltView()
            _canvas.TiltViewEnabled = Not _canvas.TiltViewEnabled
            _pauseMenu.Items(3) = "TILT VIEW: " & If(_canvas.TiltViewEnabled, "ON", "OFF")
        End Sub

        Public Sub HandleChar(ch As Char)
            If _screen <> ShellScreen.NameEntry Then Return
            Dim up As Char = Char.ToUpperInvariant(ch)
            If (Char.IsLetterOrDigit(up) AndAlso up < ChrW(128)) OrElse (up = " "c AndAlso _nameBuffer.Length > 0) Then
                If _nameBuffer.Length < 12 Then
                    _nameBuffer &= up
                    ClearNameNotice()
                    _sound.Play("menu_move", 0.5F)
                End If
            End If
        End Sub

        Private Shared Function IsEnter(key As Keys) As Boolean
            Return key = Keys.Enter OrElse key = Keys.Space
        End Function

        ' Window lost focus mid-run: pause so the timer doesn't run on.
        Public Sub OnDeactivated()
            _cameraKeyHeld = False
            _canvas.CameraOverviewHeld = False
            If _screen = ShellScreen.DungeonReveal OrElse
               (_screen = ShellScreen.Paused AndAlso _pausedScreen = ShellScreen.DungeonReveal) Then ApplyDungeonRevealFrame()
            If _screen = ShellScreen.Playing OrElse _screen = ShellScreen.Intro OrElse _screen = ShellScreen.GoalTransition OrElse
               _screen = ShellScreen.DungeonBriefing OrElse _screen = ShellScreen.DungeonRules OrElse _screen = ShellScreen.DungeonReveal Then Pause()
        End Sub

        Private Function CurrentPalette() As ThemePalette
            If IsGameScreen() Then Return ThemePalette.ForTheme(_levels(_levelIndex).ThemeName)
            Return ThemePalette.ForTheme(_levels(_attractLevel).ThemeName)
        End Function
    End Class
End Namespace
