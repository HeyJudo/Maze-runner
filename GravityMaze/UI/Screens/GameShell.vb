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

Namespace UI.Screens
    Public Enum ShellScreen
        Title
        LevelSelect
        Records
        HowToPlay
        NameEntry
        Intro          ' level card + 3-2-1-GO; engine frozen
        Playing
        Paused
        LevelComplete
        TimeUp
        OutOfHearts
        Victory
    End Enum

    ' The game loop and screen flow: menus, level intro, play, pause, results, victory.
    ' Owns which engine is shown on the canvas (attract demo behind menus, real game otherwise),
    ' routes engine events to effects and sound, and records results. Drawing lives in GameShell.Draw.vb.
    Partial Public NotInheritable Class GameShell
        Public Const TickMs As Single = 16.0F
        Private Const IntroCardMs As Single = 1600.0F
        Private Const CountStepMs As Single = 650.0F
        Private Const CompleteDelayMs As Single = 900.0F

        Public Event QuitRequested As EventHandler

        Private ReadOnly _canvas As GameCanvas
        Private ReadOnly _input As InputManager
        Private ReadOnly _sound As SoundManager
        Private ReadOnly _scores As ScoreManager
        Private ReadOnly _levels As List(Of LevelConfig)
        Private ReadOnly _mazes As New Dictionary(Of Integer, MazeDefinition)()

        Private _screen As ShellScreen = ShellScreen.Title
        Private _screenMs As Single
        Private _returnScreen As ShellScreen = ShellScreen.Title   ' where NameEntry goes after confirming

        ' Real game
        Private _engine As GameEngine
        Private _levelIndex As Integer
        Private _campaign As Boolean
        Private _campaignTime As Single
        Private _campaignStars As Integer
        Private _skipCard As Boolean
        Private _completedAtMs As Single = -1
        Private _lastTile As Char = "0"c
        Private _heartLostAgoMs As Single = 99999.0F   ' time since the last heart loss (HUD drain animation)
        Private _heartLostIndex As Integer              ' HUD slot of the heart that was just lost
        Private _lastWholeSecondLeft As Integer = -1
        Private _spawnSoundInMs As Single = -1

        ' Result of the level just finished
        Private _resultTime As Single
        Private _resultAttempts As Integer
        Private _resultStars As Integer
        Private _resultScore As Integer
        Private _resultNewBest As Boolean
        Private _resultPrevBest As ScoreRecord
        Private _victoryRank As Integer

        ' Attract demo behind the menus
        Private _attractEngine As GameEngine
        Private _attractPilot As AttractPilot
        Private _attractLevel As Integer = 2      ' index into _levels; alternates Frozen / Neon
        Private _attractDoneMs As Single = -1

        ' Menus
        Private ReadOnly _titleMenu As New MenuList("PLAY", "LEVEL SELECT", "RECORDS", "HOW TO PLAY", "CHANGE PLAYER", "QUIT")
        Private ReadOnly _levelMenu As New MenuList()
        Private ReadOnly _pauseMenu As New MenuList("RESUME", "RESTART LEVEL", "MAIN MENU", "TILT VIEW: ON")
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
            _canvas.OverlayPainter = AddressOf Paint
            StartAttract()
            GoTo_(ShellScreen.Title)
        End Sub

        ' Current level's engine (Nothing on menus). Read-only; used by the verification tour's bot.
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
                Case ShellScreen.Records, ShellScreen.HowToPlay : Return _backMenu
                Case ShellScreen.NameEntry : Return _nameMenu
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
            _heartLostAgoMs += TickMs
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
                            If _levelMenu.Selected >= _levels.Count Then Back() Else StartLevel(_levelMenu.Selected, campaign:=False)
                        Case MenuAction.Back : Back()
                    End Select
                Case ShellScreen.Records, ShellScreen.HowToPlay
                    TickAttract()
                    Dim a As MenuAction = Nav(_backMenu, tx, ty)
                    If a = MenuAction.Confirm OrElse a = MenuAction.Back Then Back()
                Case ShellScreen.NameEntry
                    TickAttract()
                    Select Case Nav(_nameMenu, tx, ty)
                        Case MenuAction.Confirm : ConfirmName()
                        Case MenuAction.Back : Back()
                    End Select
                Case ShellScreen.Intro
                    TickIntro()
                Case ShellScreen.Playing
                    TickPlaying(tx, ty)
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
                _canvas.PickupTaken = AddressOf _engine.IsPickupTaken
                _canvas.BallBlink = _engine.IsInvulnerable AndAlso _screen = ShellScreen.Playing
                _canvas.UpdateBallPosition(_engine.BallX, _engine.BallY)
                _canvas.UpdateBallMotion(If(_screen = ShellScreen.Playing, _engine.VelocityX, 0.0F),
                                         If(_screen = ShellScreen.Playing, _engine.VelocityY, 0.0F), TileUnder(_engine))
            Else
                _canvas.PickupTaken = Nothing
                _canvas.BallBlink = False
                _canvas.UpdateBallPosition(_attractEngine.BallX, _attractEngine.BallY)
                _canvas.UpdateBallMotion(_attractEngine.VelocityX, _attractEngine.VelocityY, TileUnder(_attractEngine))
            End If
            UpdateInsets()
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
            Return _engine IsNot Nothing AndAlso _screen >= ShellScreen.Intro
        End Function

        Private Function TileUnder(e As GameEngine) As Char
            Dim m As MazeDefinition = e.Maze
            Dim r As Integer = Math.Max(0, Math.Min(m.RowCount - 1, CInt(Math.Floor(e.BallY))))
            Dim c As Integer = Math.Max(0, Math.Min(m.ColumnCount - 1, CInt(Math.Floor(e.BallX))))
            Return m.GetTile(r, c)
        End Function

        ' Menus put the board on the right (menu on the left); game screens leave room for the HUD.
        Private Sub UpdateInsets()
            Dim w As Integer = _canvas.ClientSize.Width
            Dim h As Integer = _canvas.ClientSize.Height
            Dim s As Single = UiScale()
            Dim wanted As Padding
            If IsGameScreen() Then
                wanted = New Padding(CInt(24 * s), CInt(118 * s), CInt(24 * s), CInt(56 * s))
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
                Case ShellScreen.Title : _titleMenu.Reset(_titleMenu.Selected)
                Case ShellScreen.LevelSelect : _levelMenu.Reset(_levelMenu.Selected)
                Case ShellScreen.Records, ShellScreen.HowToPlay : _backMenu.Reset()
                Case ShellScreen.NameEntry : _nameMenu.Reset()
                Case ShellScreen.Paused : _pauseMenu.Reset()
                Case ShellScreen.TimeUp : _timeUpMenu.Reset()
                Case ShellScreen.OutOfHearts : _heartsMenu.Reset()
                Case ShellScreen.Victory : _victoryMenu.Reset()
            End Select
            If screen <> ShellScreen.Playing Then _sound.StopLoop("ice_slide")
            _canvas.BackColor = CurrentPalette().Backdrop
        End Sub

        ' ── Menus ───────────────────────────────────────────────────────────
        Private Sub TitleChoice(index As Integer)
            Select Case index
                Case 0
                    If String.IsNullOrWhiteSpace(_scores.PlayerName) Then
                        OpenNameEntry(ShellScreen.Intro)
                    Else
                        StartLevel(0, campaign:=True)
                    End If
                Case 1 : GoTo_(ShellScreen.LevelSelect)
                Case 2 : GoTo_(ShellScreen.Records)
                Case 3 : GoTo_(ShellScreen.HowToPlay)
                Case 4 : OpenNameEntry(ShellScreen.Title)
                Case 5 : RaiseEvent QuitRequested(Me, EventArgs.Empty)
            End Select
        End Sub

        Private Sub OpenNameEntry(returnTo As ShellScreen)
            _returnScreen = returnTo
            _nameBuffer = If(String.IsNullOrWhiteSpace(_scores.PlayerName), "PLAYER", _scores.PlayerName)
            GoTo_(ShellScreen.NameEntry)
        End Sub

        Private Sub ConfirmName()
            Dim name As String = _nameBuffer.Trim()
            If name.Length = 0 Then Return
            _scores.PlayerName = name
            If _returnScreen = ShellScreen.Intro Then StartLevel(0, campaign:=True) Else GoTo_(ShellScreen.Title)
        End Sub

        Private Sub Back()
            Select Case _screen
                Case ShellScreen.Records
                    GoTo_(If(_engine IsNot Nothing AndAlso _campaignStars > 0 AndAlso _victoryRank > 0, ShellScreen.Title, ShellScreen.Title))
                Case ShellScreen.LevelSelect, ShellScreen.HowToPlay, ShellScreen.NameEntry
                    GoTo_(ShellScreen.Title)
            End Select
        End Sub

        Private Sub ToMainMenu()
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

        Private Sub StartLevel(index As Integer, campaign As Boolean)
            If campaign AndAlso index = 0 Then
                _campaignTime = 0
                _campaignStars = 0
            End If
            _campaign = campaign
            _levelIndex = index
            Dim cfg As LevelConfig = _levels(index)
            If _engine IsNot Nothing Then DetachEngine(_engine)
            _engine = New GameEngine(MazeFor(index), cfg.TimeLimitSecs)
            AddHandler _engine.WallImpacted, AddressOf OnWallImpacted
            AddHandler _engine.BallFell, AddressOf OnBallFell
            AddHandler _engine.HeartLost, AddressOf OnHeartLost
            AddHandler _engine.HeartGained, AddressOf OnHeartGained
            _canvas.ShowMaze(_engine.Maze, cfg.ThemeName)
            _skipCard = False
            BeginRun()
        End Sub

        Private Sub DetachEngine(e As GameEngine)
            RemoveHandler e.WallImpacted, AddressOf OnWallImpacted
            RemoveHandler e.BallFell, AddressOf OnBallFell
            RemoveHandler e.HeartLost, AddressOf OnHeartLost
            RemoveHandler e.HeartGained, AddressOf OnHeartGained
        End Sub

        Private Sub BeginRun()
            _completedAtMs = -1
            _lastTile = "0"c
            _lastWholeSecondLeft = -1
            _spawnSoundInMs = -1
            _heartLostAgoMs = 99999.0F
            _canvas.ClearEffects()
            GoTo_(ShellScreen.Intro)
        End Sub

        Private Sub Retry()
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
            If _completedAtMs < 0 Then
                _engine.Update(tx, ty)
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

            ' Timer warnings in the last five seconds
            Dim cfg As LevelConfig = _levels(_levelIndex)
            If cfg.TimeLimitSecs > 0 AndAlso _engine.State = GameState.Playing Then
                Dim whole As Integer = CInt(Math.Ceiling(_engine.TimeRemainingSeconds))
                If whole <= 5 AndAlso whole <> _lastWholeSecondLeft AndAlso whole > 0 Then _sound.Play("tick")
                _lastWholeSecondLeft = whole
            End If

            If _engine.State = GameState.TimeUp Then
                _sound.Play("time_up")
                GoTo_(ShellScreen.TimeUp)
            ElseIf _engine.State = GameState.OutOfHearts Then
                _sound.Play("time_up")
                GoTo_(ShellScreen.OutOfHearts)
            ElseIf _engine.State = GameState.LevelComplete Then
                If _completedAtMs < 0 Then
                    _completedAtMs = _screenMs
                    _canvas.TriggerGoalCelebration(_engine.BallX, _engine.BallY)
                    _sound.Play("goal")
                    RecordResult()
                ElseIf _screenMs - _completedAtMs >= CompleteDelayMs Then
                    EnterLevelComplete()
                End If
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
                .Attempts = _resultAttempts, .Stars = _resultStars, .Score = _resultScore, .Date = DateTime.Now})
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
                    ' A retry after finishing replaces that level's contribution to the campaign total.
                    If _campaign Then
                        _campaignTime -= _resultTime
                        _campaignStars -= _resultStars
                    End If
                    Retry()
                Case "FINISH" : EnterVictory()
                Case Else : ToMainMenu()
            End Select
        End Sub

        Private Sub EnterVictory()
            _scores.AddRecord(New ScoreRecord With {
                .PlayerName = _scores.PlayerName, .LevelNumber = 0, .TimeSeconds = _campaignTime,
                .Attempts = 1, .Stars = _campaignStars, .Score = 0, .Date = DateTime.Now})
            _victoryRank = _scores.TopRecords(0, 1000).FindIndex(
                Function(r) r.PlayerName = _scores.PlayerName AndAlso Math.Abs(r.TimeSeconds - _campaignTime) < 0.001F) + 1
            _sound.Play("victory")
            GoTo_(ShellScreen.Victory)
        End Sub

        Private Sub PauseChoice(index As Integer)
            Select Case index
                Case 0 : ResumeGame()
                Case 1 : Retry()
                Case 2 : ToMainMenu()
                Case 3 : ToggleTiltView()
            End Select
        End Sub

        Private Sub Pause()
            GoTo_(ShellScreen.Paused)
        End Sub

        Private Sub ResumeGame()
            _screen = ShellScreen.Playing
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

        Private Sub OnHeartLost(sender As Object, e As HeartEventArgs)
            _heartLostAgoMs = 0
            _heartLostIndex = e.Hearts
            _canvas.FlashDamage()
            _sound.Play("wall_hit", 1.0F)
        End Sub

        Private Sub OnHeartGained(sender As Object, e As HeartEventArgs)
            _canvas.AddHeartBurst(e.X, e.Y)
            _sound.Play("menu_confirm", 0.8F)
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
            If key = Keys.F8 Then
                ToggleTiltView()
                Return True
            End If
            If key = Keys.M Then
                _sound.Muted = Not _sound.Muted
                Return True
            End If
            Select Case _screen
                Case ShellScreen.Intro, ShellScreen.Playing
                    If key = Keys.Escape OrElse key = Keys.P Then Pause() : Return True
                    If key = Keys.R AndAlso _completedAtMs < 0 Then Retry() : Return True
                Case ShellScreen.Paused
                    If key = Keys.Escape OrElse key = Keys.P Then ResumeGame() : Return True
                    If key = Keys.R Then Retry() : Return True
                    If IsEnter(key) Then _sound.Play("menu_confirm") : PauseChoice(_pauseMenu.Selected) : Return True
                Case ShellScreen.Title
                    If IsEnter(key) Then _sound.Play("menu_confirm") : TitleChoice(_titleMenu.Selected) : Return True
                Case ShellScreen.LevelSelect
                    If IsEnter(key) Then
                        _sound.Play("menu_confirm")
                        If _levelMenu.Selected >= _levels.Count Then Back() Else StartLevel(_levelMenu.Selected, campaign:=False)
                        Return True
                    End If
                    If key = Keys.Escape Then _sound.Play("menu_back") : Back() : Return True
                Case ShellScreen.Records, ShellScreen.HowToPlay
                    If IsEnter(key) OrElse key = Keys.Escape Then _sound.Play("menu_back") : Back() : Return True
                Case ShellScreen.NameEntry
                    If key = Keys.Enter Then _sound.Play("menu_confirm") : ConfirmName() : Return True
                    If key = Keys.Escape Then _sound.Play("menu_back") : Back() : Return True
                    If key = Keys.Back AndAlso _nameBuffer.Length > 0 Then
                        _nameBuffer = _nameBuffer.Substring(0, _nameBuffer.Length - 1)
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
                    _sound.Play("menu_move", 0.5F)
                End If
            End If
        End Sub

        Private Shared Function IsEnter(key As Keys) As Boolean
            Return key = Keys.Enter OrElse key = Keys.Space
        End Function

        ' Window lost focus mid-run: pause so the timer doesn't run on.
        Public Sub OnDeactivated()
            If _screen = ShellScreen.Playing OrElse _screen = ShellScreen.Intro Then Pause()
        End Sub

        Private Function CurrentPalette() As ThemePalette
            If IsGameScreen() Then Return ThemePalette.ForTheme(_levels(_levelIndex).ThemeName)
            Return ThemePalette.ForTheme(_levels(_attractLevel).ThemeName)
        End Function
    End Class
End Namespace
