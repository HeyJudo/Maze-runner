Option Strict On
Option Explicit On

Imports System
Imports System.IO
Imports System.Linq
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.Xml.Linq

Namespace Data
    ' How an attempt ended. Only cleared runs compete for records.
    Public NotInheritable Class RunOutcome
        Public Const Cleared As String = "cleared"
        Public Const OutOfHearts As String = "out-of-hearts"
        Public Const Restart As String = "restart"
        Public Const Quit As String = "quit"
        Public Const TimeUp As String = "time-up"

        Private Sub New()
        End Sub
    End Class

    ' One attempt. LevelNumber 0 = full campaign run.
    Public NotInheritable Class ScoreRecord
        Public Property PlayerName As String = ""
        Public Property LevelNumber As Integer
        Public Property CampaignLevels As Integer = 3 ' Legacy full runs contained three levels.
        Public Property Outcome As String = RunOutcome.Cleared
        Public Property TimeSeconds As Single
        Public Property Attempts As Integer
        Public Property Stars As Integer
        Public Property Score As Integer
        Public Property WallHits As Integer
        Public Property Falls As Integer
        Public Property HeartsLost As Single
        Public Property BarriersBroken As Integer
        Public Property [Date] As DateTime

        Public ReadOnly Property Cleared As Boolean
            Get
                Return Outcome = RunOutcome.Cleared
            End Get
        End Property
    End Class

    ' The best clear's marble path, sampled every StepMs, raced as a translucent ghost.
    Public NotInheritable Class GhostRun
        Public Const DefaultStepMs As Single = 96.0F   ' every 6th 16 ms tick, about 10 Hz

        Public ReadOnly Property TimeSeconds As Single
        Public ReadOnly Property StepMs As Single
        Public ReadOnly Property Points As IReadOnlyList(Of PointF)

        Public Sub New(timeSeconds As Single, stepMs As Single, points As IEnumerable(Of PointF))
            Me.TimeSeconds = timeSeconds
            Me.StepMs = stepMs
            Me.Points = points.ToList()
        End Sub

        Public Function PositionAt(seconds As Single) As PointF
            If Points.Count = 0 Then Return PointF.Empty
            Dim f As Single = Math.Max(0.0F, seconds * 1000.0F / StepMs)
            Dim i As Integer = CInt(Math.Floor(f))
            If i >= Points.Count - 1 Then Return Points(Points.Count - 1)
            Dim t As Single = f - i
            Dim a As PointF = Points(i)
            Dim b As PointF = Points(i + 1)
            Return New PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t)
        End Function

        Public Function Encode() As String
            Return String.Join(";", Points.Select(Function(p) p.X.ToString("0.00", CultureInfo.InvariantCulture) & "," &
                                                              p.Y.ToString("0.00", CultureInfo.InvariantCulture)))
        End Function

        ' Malformed data returns Nothing, so a bad ghost is skipped instead of failing the load.
        Public Shared Function Decode(timeSeconds As Single, stepMs As Single, text As String) As GhostRun
            Dim pts As New List(Of PointF)()
            For Each pair As String In If(text, "").Split(";"c, StringSplitOptions.RemoveEmptyEntries)
                Dim xy As String() = pair.Split(","c)
                Dim x, y As Single
                If xy.Length <> 2 OrElse
                   Not Single.TryParse(xy(0), NumberStyles.Float, CultureInfo.InvariantCulture, x) OrElse
                   Not Single.TryParse(xy(1), NumberStyles.Float, CultureInfo.InvariantCulture, y) Then Return Nothing
                pts.Add(New PointF(x, y))
            Next
            If pts.Count < 2 OrElse stepMs <= 0 Then Return Nothing
            Return New GhostRun(timeSeconds, stepMs, pts)
        End Function
    End Class

    Public NotInheritable Class PlayerProfile
        Public Property Key As String = ""
        Public Property Name As String = ""
        Public Property Created As DateTime
        Public Property LastPlayed As DateTime
        Public Property UnlockedLevel As Integer = 1
        Public Property GhostEnabled As Boolean = True
        Public ReadOnly Property Ghosts As New Dictionary(Of Integer, GhostRun)()
    End Class

    ' Controller tuning belongs to the machine (it describes the hardware), not to a player.
    Public NotInheritable Class CalibrationSettings
        Public Property FullTiltDegrees As Single = 20.0F
        Public Property Deadzone As Single = 0.08F
        Public Property Smoothing As Single = 0.35F
        Public Property SwapAxes As Boolean
        Public Property InvertX As Boolean
        Public Property InvertY As Boolean
    End Class

    ' Worked out from the journal for the Profile screen; never stored.
    Public NotInheritable Class LevelStats
        Public Property Attempts As Integer
        Public Property Clears As Integer
        Public Property BestTime As Single = -1.0F   ' -1 = never cleared
        Public Property BestStars As Integer
        Public ReadOnly Property Outcomes As New Dictionary(Of String, Integer)()
    End Class

    ' XML v2: player profiles (progress, ghosts), a journal of every attempt, and controller settings.
    ' v1 files (cleared <Record>s only) are upgraded on load and rewritten as v2 on the next save.
    Public NotInheritable Class ScoreManager
        Private ReadOnly filePath As String
        Private ReadOnly runs As New List(Of ScoreRecord)()
        Private ReadOnly profiles As New List(Of PlayerProfile)()
        Private current As PlayerProfile
        Private _calibration As New CalibrationSettings()
        Private Shared ReadOnly Inv As CultureInfo = CultureInfo.InvariantCulture

        Public Sub New(filePath As String)
            Me.filePath = filePath
            Load()
        End Sub

        Public Shared Function DefaultPath() As String
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GravityMaze", "records.xml")
        End Function

        ' One player per name regardless of capitals or extra spaces: "  jude " = "Jude" = "JUDE".
        Public Shared Function KeyOf(name As String) As String
            Return CleanName(name).ToLowerInvariant()
        End Function

        Private Shared Function CleanName(name As String) As String
            Return String.Join(" ", If(name, "").Split(CType(Nothing, Char()), StringSplitOptions.RemoveEmptyEntries))
        End Function

        ' ── Players & progress ─────────────────────────────────────────────
        Public ReadOnly Property Players As List(Of PlayerProfile)
            Get
                Return profiles.OrderByDescending(Function(p) p.LastPlayed).ToList()
            End Get
        End Property

        Public Function FindPlayer(name As String) As PlayerProfile
            Dim key As String = KeyOf(name)
            Return profiles.FirstOrDefault(Function(p) p.Key = key)
        End Function

        ' Current player's display name ("" = nobody chosen). Setting it selects, or creates, that profile.
        Public Property PlayerName As String
            Get
                Return If(current Is Nothing, "", current.Name)
            End Get
            Set(value As String)
                current = If(KeyOf(value).Length = 0, Nothing, EnsurePlayer(value))
                If current IsNot Nothing Then current.LastPlayed = DateTime.Now
                Save()
            End Set
        End Property

        Public ReadOnly Property UnlockedLevel As Integer
            Get
                Return If(current Is Nothing, 1, current.UnlockedLevel)
            End Get
        End Property

        Public Sub Unlock(levelNumber As Integer)
            If current Is Nothing OrElse levelNumber <= current.UnlockedLevel Then Return
            current.UnlockedLevel = levelNumber
            Save()
        End Sub

        Public Property GhostEnabled As Boolean
            Get
                Return current IsNot Nothing AndAlso current.GhostEnabled
            End Get
            Set(value As Boolean)
                If current Is Nothing Then Return
                current.GhostEnabled = value
                Save()
            End Set
        End Property

        Public Function GhostFor(levelNumber As Integer) As GhostRun
            Dim ghost As GhostRun = Nothing
            If current IsNot Nothing Then current.Ghosts.TryGetValue(levelNumber, ghost)
            Return ghost
        End Function

        Public Sub SaveGhost(levelNumber As Integer, ghost As GhostRun)
            If current Is Nothing OrElse ghost Is Nothing OrElse ghost.Points.Count < 2 OrElse ghost.StepMs <= 0 Then Return
            current.Ghosts(levelNumber) = ghost
            Save()
        End Sub

        Public ReadOnly Property Calibration As CalibrationSettings
            Get
                Return _calibration
            End Get
        End Property

        Public Sub SaveSettings()
            Save()
        End Sub

        Private Function EnsurePlayer(name As String) As PlayerProfile
            Dim p As PlayerProfile = FindPlayer(name)
            If p IsNot Nothing Then Return p
            p = New PlayerProfile With {.Key = KeyOf(name), .Name = CleanName(name), .Created = DateTime.Now, .LastPlayed = DateTime.Now}
            profiles.Add(p)
            Return p
        End Function

        ' ── Run journal ────────────────────────────────────────────────────
        ' Journals one attempt and saves. Returns True if it is a new personal-best clear
        ' (a player's first clear counts). Nameless runs and unfinished runs with no time are ignored.
        Public Function AddRecord(record As ScoreRecord) As Boolean
            If KeyOf(record.PlayerName).Length = 0 Then Return False
            If Not record.Cleared AndAlso record.TimeSeconds <= 0 Then Return False
            Dim p As PlayerProfile = EnsurePlayer(record.PlayerName)
            record.PlayerName = p.Name
            If record.[Date] > p.LastPlayed Then p.LastPlayed = record.[Date]
            Dim isBest As Boolean = False
            If record.Cleared Then
                Dim prev As ScoreRecord = BestRecordFor(p.Name, record.LevelNumber, If(record.LevelNumber = 0, record.CampaignLevels, 0))
                isBest = prev Is Nothing OrElse record.TimeSeconds < prev.TimeSeconds
                If record.LevelNumber > 0 Then p.UnlockedLevel = Math.Max(p.UnlockedLevel, record.LevelNumber + 1)
            End If
            runs.Add(record)
            Save()
            Return isBest
        End Function

        Private Function Clears(levelNumber As Integer, campaignLevels As Integer) As IEnumerable(Of ScoreRecord)
            Return runs.Where(Function(r) r.Cleared AndAlso r.LevelNumber = levelNumber AndAlso
                                  (levelNumber <> 0 OrElse campaignLevels <= 0 OrElse r.CampaignLevels = campaignLevels))
        End Function

        Public Function BestRecord(levelNumber As Integer, Optional campaignLevels As Integer = 0) As ScoreRecord
            Return Clears(levelNumber, campaignLevels).OrderBy(Function(r) r.TimeSeconds).FirstOrDefault()
        End Function

        Public Function BestRecordFor(playerName As String, levelNumber As Integer, Optional campaignLevels As Integer = 0) As ScoreRecord
            Dim key As String = KeyOf(playerName)
            Return Clears(levelNumber, campaignLevels).Where(Function(r) KeyOf(r.PlayerName) = key).
                OrderBy(Function(r) r.TimeSeconds).FirstOrDefault()
        End Function

        Public Function TopRecords(levelNumber As Integer, count As Integer, Optional campaignLevels As Integer = 0) As List(Of ScoreRecord)
            Return Clears(levelNumber, campaignLevels).OrderBy(Function(r) r.TimeSeconds).Take(Math.Max(0, count)).ToList()
        End Function

        ' Every attempt (any outcome) by one player on one level, oldest first.
        Public Function RunsFor(playerName As String, levelNumber As Integer) As List(Of ScoreRecord)
            Dim key As String = KeyOf(playerName)
            Return runs.Where(Function(r) r.LevelNumber = levelNumber AndAlso KeyOf(r.PlayerName) = key).ToList()
        End Function

        Public Function StatsFor(playerName As String, levelNumber As Integer) As LevelStats
            Dim stats As New LevelStats()
            For Each r As ScoreRecord In RunsFor(playerName, levelNumber)
                stats.Attempts += 1
                Dim n As Integer = 0
                stats.Outcomes.TryGetValue(r.Outcome, n)
                stats.Outcomes(r.Outcome) = n + 1
                If Not r.Cleared Then Continue For
                stats.Clears += 1
                stats.BestStars = Math.Max(stats.BestStars, r.Stars)
                If stats.BestTime < 0 OrElse r.TimeSeconds < stats.BestTime Then stats.BestTime = r.TimeSeconds
            Next
            Return stats
        End Function

        ' score = stars*1000 + max(0, 3000 - time*25) - (attempts-1)*150, floored at 0
        Public Shared Function ComputeScore(timeSeconds As Single, attempts As Integer, stars As Integer) As Integer
            Dim s As Double = stars * 1000.0 + Math.Max(0.0, 3000.0 - timeSeconds * 25.0) - (Math.Max(1, attempts) - 1) * 150.0
            Return CInt(Math.Max(0.0, Math.Round(s)))
        End Function

        ' ── XML ─────────────────────────────────────────────────────────────
        Private Shared Function Attr(e As XElement, name As String, fallback As String) As String
            Return If(CStr(e.Attribute(name)), fallback)
        End Function

        Private Shared Function DateAttr(e As XElement, name As String) As DateTime
            Dim text As String = CStr(e.Attribute(name))
            Return If(text Is Nothing, DateTime.Now, DateTime.Parse(text, Inv, DateTimeStyles.RoundtripKind))
        End Function

        Private Sub Load()
            If Not File.Exists(filePath) Then Return
            Try
                Dim root As XElement = XDocument.Load(filePath).Root
                If root Is Nothing OrElse root.Name.LocalName <> "GravityMazeRecords" Then Throw New FormatException("bad root")

                Dim loadedProfiles As New List(Of PlayerProfile)()
                For Each e As XElement In root.Elements("Player")
                    Dim name As String = Attr(e, "name", "")
                    Dim p As New PlayerProfile With {
                        .Key = KeyOf(name), .Name = CleanName(name),
                        .Created = DateAttr(e, "created"), .LastPlayed = DateAttr(e, "lastPlayed"),
                        .UnlockedLevel = Integer.Parse(Attr(e, "unlockedLevel", "1"), Inv),
                        .GhostEnabled = Attr(e, "ghost", "true") = "true"}
                    For Each ge As XElement In e.Elements("Ghost")
                        Dim ghost As GhostRun = GhostRun.Decode(Single.Parse(Attr(ge, "time", "0"), Inv),
                                                                Single.Parse(Attr(ge, "stepMs", "0"), Inv), ge.Value)
                        If ghost IsNot Nothing Then p.Ghosts(Integer.Parse(Attr(ge, "level", "0"), Inv)) = ghost
                    Next
                    If p.Key.Length > 0 AndAlso Not loadedProfiles.Any(Function(x) x.Key = p.Key) Then loadedProfiles.Add(p)
                Next

                Dim loadedRuns As New List(Of ScoreRecord)()
                For Each e As XElement In root.Elements().Where(Function(x) x.Name.LocalName = "Run" OrElse x.Name.LocalName = "Record")
                    loadedRuns.Add(New ScoreRecord With {
                        .PlayerName = Attr(e, "player", ""),
                        .LevelNumber = Integer.Parse(Attr(e, "level", "0"), Inv),
                        .CampaignLevels = Integer.Parse(Attr(e, "campaignLevels", "3"), Inv),
                        .Outcome = Attr(e, "outcome", RunOutcome.Cleared),
                        .TimeSeconds = Single.Parse(Attr(e, "time", "0"), Inv),
                        .Attempts = Integer.Parse(Attr(e, "attempts", "1"), Inv),
                        .Stars = Integer.Parse(Attr(e, "stars", "0"), Inv),
                        .Score = Integer.Parse(Attr(e, "score", "0"), Inv),
                        .WallHits = Integer.Parse(Attr(e, "wallHits", "0"), Inv),
                        .Falls = Integer.Parse(Attr(e, "falls", "0"), Inv),
                        .HeartsLost = Single.Parse(Attr(e, "heartsLost", "0"), Inv),
                        .BarriersBroken = Integer.Parse(Attr(e, "barriersBroken", "0"), Inv),
                        .[Date] = DateAttr(e, "date")})
                Next

                Dim loadedCalibration As New CalibrationSettings()
                Dim last As String = ""
                Dim st As XElement = root.Element("Settings")
                If st IsNot Nothing Then
                    last = If(CStr(st.Attribute("LastPlayer")), Attr(st, "PlayerName", ""))   ' v1 used PlayerName
                    loadedCalibration.FullTiltDegrees = Single.Parse(Attr(st, "FullTiltDegrees", "20"), Inv)
                    loadedCalibration.Deadzone = Single.Parse(Attr(st, "Deadzone", "0.08"), Inv)
                    loadedCalibration.Smoothing = Single.Parse(Attr(st, "Smoothing", "0.35"), Inv)
                    loadedCalibration.SwapAxes = Attr(st, "SwapAxes", "false") = "true"
                    loadedCalibration.InvertX = Attr(st, "InvertX", "false") = "true"
                    loadedCalibration.InvertY = Attr(st, "InvertY", "false") = "true"
                End If

                profiles.AddRange(loadedProfiles)
                runs.AddRange(loadedRuns)
                _calibration = loadedCalibration
                ' v1 upgrade (and repair): every named run gets a profile under its first-seen spelling,
                ' and progress covers every level already cleared.
                For Each r As ScoreRecord In runs
                    If KeyOf(r.PlayerName).Length = 0 Then Continue For
                    Dim p As PlayerProfile = EnsurePlayer(r.PlayerName)
                    r.PlayerName = p.Name
                    If r.Cleared AndAlso r.LevelNumber > 0 Then p.UnlockedLevel = Math.Max(p.UnlockedLevel, r.LevelNumber + 1)
                Next
                current = If(KeyOf(last).Length = 0, Nothing, EnsurePlayer(last))
            Catch
                ' corrupt file: keep a backup, start empty
                runs.Clear()
                profiles.Clear()
                current = Nothing
                _calibration = New CalibrationSettings()
                Try
                    File.Copy(filePath, filePath & ".bak", True)
                Catch
                End Try
            End Try
        End Sub

        Private Shared Function Bool(value As Boolean) As String
            Return If(value, "true", "false")
        End Function

        Private Sub Save()
            Try
                Dim c As CalibrationSettings = _calibration
                Dim root As New XElement("GravityMazeRecords", New XAttribute("version", "2"),
                    New XElement("Settings",
                        New XAttribute("LastPlayer", PlayerName),
                        New XAttribute("FullTiltDegrees", c.FullTiltDegrees.ToString(Inv)),
                        New XAttribute("Deadzone", c.Deadzone.ToString(Inv)),
                        New XAttribute("Smoothing", c.Smoothing.ToString(Inv)),
                        New XAttribute("SwapAxes", Bool(c.SwapAxes)),
                        New XAttribute("InvertX", Bool(c.InvertX)),
                        New XAttribute("InvertY", Bool(c.InvertY))))
                For Each p As PlayerProfile In profiles
                    root.Add(New XElement("Player",
                        New XAttribute("name", p.Name),
                        New XAttribute("created", p.Created.ToString("s", Inv)),
                        New XAttribute("lastPlayed", p.LastPlayed.ToString("s", Inv)),
                        New XAttribute("unlockedLevel", p.UnlockedLevel.ToString(Inv)),
                        New XAttribute("ghost", Bool(p.GhostEnabled)),
                        p.Ghosts.OrderBy(Function(kv) kv.Key).Select(Function(kv) New XElement("Ghost",
                            New XAttribute("level", kv.Key.ToString(Inv)),
                            New XAttribute("time", kv.Value.TimeSeconds.ToString("0.00", Inv)),
                            New XAttribute("stepMs", kv.Value.StepMs.ToString(Inv)),
                            kv.Value.Encode()))))
                Next
                For Each r As ScoreRecord In runs
                    root.Add(New XElement("Run",
                        New XAttribute("player", r.PlayerName),
                        New XAttribute("level", r.LevelNumber.ToString(Inv)),
                        If(r.LevelNumber = 0, New XAttribute("campaignLevels", r.CampaignLevels.ToString(Inv)), Nothing),
                        New XAttribute("outcome", r.Outcome),
                        New XAttribute("time", r.TimeSeconds.ToString("0.00", Inv)),
                        New XAttribute("attempts", r.Attempts.ToString(Inv)),
                        New XAttribute("stars", r.Stars.ToString(Inv)),
                        New XAttribute("score", r.Score.ToString(Inv)),
                        New XAttribute("wallHits", r.WallHits.ToString(Inv)),
                        New XAttribute("falls", r.Falls.ToString(Inv)),
                        New XAttribute("heartsLost", r.HeartsLost.ToString(Inv)),
                        New XAttribute("barriersBroken", r.BarriersBroken.ToString(Inv)),
                        New XAttribute("date", r.[Date].ToString("s", Inv))))
                Next
                Dim dir As String = Path.GetDirectoryName(filePath)
                If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
                Dim tmp As String = filePath & ".tmp"
                Dim doc As New XDocument(root)
                doc.Save(tmp)
                If File.Exists(filePath) Then
                    File.Replace(tmp, filePath, Nothing)
                Else
                    File.Move(tmp, filePath)
                End If
            Catch
                ' disk problems must never crash the game
            End Try
        End Sub
    End Class
End Namespace
