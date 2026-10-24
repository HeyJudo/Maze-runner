Option Strict On
Option Explicit On

Imports System
Imports System.IO
Imports System.Linq
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Xml.Linq
Imports MySqlConnector

Namespace Data
    ' Copies records.xml into MySQL after each save. The XML stays the source of truth: a dead
    ' server only changes Status. Push never blocks (the caller is the UI thread); the latest snapshot wins.
    Public NotInheritable Class MySqlMirror
        Private Shared ReadOnly Inv As CultureInfo = CultureInfo.InvariantCulture
        Private Const Dot As Char = ChrW(183)    ' middle dot
        Private Shared _status As String = ""

        ' "" = mirror never started, draw nothing. Written from worker threads, read by the UI.
        Public Shared Property Status As String
            Get
                Return _status
            End Get
            Set(value As String)
                _status = value
            End Set
        End Property

        Private ReadOnly write As Action(Of XElement)
        Private ReadOnly gate As New Object()
        Private pending As XElement
        Private busy As Boolean

        Public Sub New(write As Action(Of XElement))
            Me.write = write
        End Sub

        Public Sub Push(root As XElement)
            SyncLock gate
                pending = root
                If busy Then Return
                busy = True
            End SyncLock
            Task.Run(AddressOf Drain)
        End Sub

        Private Sub Drain()
            Do
                Dim [next] As XElement
                SyncLock gate
                    [next] = pending
                    pending = Nothing
                    If [next] Is Nothing Then
                        busy = False
                        Return
                    End If
                End SyncLock
                Status = "MYSQL SYNCING" & ChrW(8230)
                Try
                    write.Invoke([next])
                    Status = "MYSQL SYNCED " & DateTime.Now.ToString("HH:mm", Inv)
                Catch ex As Exception
                    Status = "MYSQL OFFLINE " & Dot & " XML ONLY " & Dot & " " & Clip(ex.Message)
                End Try
            Loop
        End Sub

        ' Checks only: waits until nothing is queued or running.
        Public Function WaitIdle(timeoutMs As Integer) As Boolean
            Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
            Do
                SyncLock gate
                    If Not busy Then Return True
                End SyncLock
                If sw.ElapsedMilliseconds > timeoutMs Then Return False
                Thread.Sleep(10)
            Loop
        End Function

        Private Shared Function Clip(message As String) As String
            Dim m As String = If(message, "")
            Return If(m.Length > 60, m.Substring(0, 60), m)
        End Function

        ' ── Config ──────────────────────────────────────────────────────────
        Public Shared Function DefaultConfigPath() As String
            Return Path.Combine(Path.GetDirectoryName(ScoreManager.DefaultPath()), "mysql.ini")
        End Function

        Private Const Template As String =
            "# Gravity Maze MySQL mirror. records.xml stays the main save; this copies it into MySQL." & vbLf &
            "# Run GravityMaze\Data\gravitymaze.sql once in MySQL Workbench, then fill in your password." & vbLf &
            "host=localhost" & vbLf & "port=3306" & vbLf & "database=gravitymaze" & vbLf & "user=root" & vbLf & "password=" & vbLf

        ' Missing file: writes the template and returns Nothing. Bad port: FormatException.
        Public Shared Function ConnectionStringFrom(iniPath As String) As String
            If Not File.Exists(iniPath) Then
                Dim dir As String = Path.GetDirectoryName(iniPath)
                If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
                File.WriteAllText(iniPath, Template)
                Return Nothing
            End If
            Dim kv As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            For Each line As String In File.ReadAllLines(iniPath)
                Dim t As String = line.Trim()
                Dim i As Integer = t.IndexOf("="c)
                If t.StartsWith("#") OrElse i <= 0 Then Continue For
                kv(t.Substring(0, i).Trim()) = t.Substring(i + 1).Trim()
            Next
            Dim v = Function(key As String, fallback As String) As String
                        Dim s As String = Nothing
                        Return If(kv.TryGetValue(key, s), s, fallback)
                    End Function
            Dim b As New MySqlConnectionStringBuilder() With {
                .Server = v("host", "localhost"),
                .Port = UInteger.Parse(v("port", "3306"), Inv),
                .Database = v("database", "gravitymaze"),
                .UserID = v("user", "root"),
                .Password = v("password", ""),
                .ConnectionTimeout = 3UI,
                .DefaultCommandTimeout = 10UI}
            Return b.ConnectionString
        End Function

        Public Shared Function FromConfig(iniPath As String) As MySqlMirror
            Try
                Dim cs As String = ConnectionStringFrom(iniPath)
                If cs Is Nothing Then
                    Status = "MYSQL OFF " & Dot & " EDIT mysql.ini"
                    Return Nothing
                End If
                Return New MySqlMirror(Sub(root) WriteAll(cs, root))
            Catch ex As Exception
                Status = "MYSQL CONFIG ERROR " & Dot & " " & Clip(ex.Message)
                Return Nothing
            End Try
        End Function

        ' ── XML → rows ──────────────────────────────────────────────────────
        Private Shared Function A(e As XElement, name As String, fallback As String) As String
            Return If(CStr(e.Attribute(name)), fallback)
        End Function

        Private Shared Function D(e As XElement, name As String) As DateTime
            Dim text As String = CStr(e.Attribute(name))
            Return If(text Is Nothing, DateTime.Now, DateTime.Parse(text, Inv, DateTimeStyles.RoundtripKind))
        End Function

        Private Shared Function I(e As XElement, name As String, fallback As String) As Integer
            Return Integer.Parse(A(e, name, fallback), Inv)
        End Function

        Private Shared Function F(e As XElement, name As String, fallback As String) As Single
            Return Single.Parse(A(e, name, fallback), Inv)
        End Function

        Private Shared Function Dec(e As XElement, name As String) As Decimal
            Return Decimal.Parse(A(e, name, "0"), Inv)
        End Function

        Public Shared Function Tables(root As XElement) As Dictionary(Of String, List(Of Dictionary(Of String, Object)))
            Dim players As New List(Of Dictionary(Of String, Object))()
            Dim runs As New List(Of Dictionary(Of String, Object))()
            Dim ghosts As New List(Of Dictionary(Of String, Object))()
            Dim settings As New List(Of Dictionary(Of String, Object))()

            For Each e As XElement In root.Elements("Player")
                Dim key As String = ScoreManager.KeyOf(A(e, "name", ""))
                players.Add(New Dictionary(Of String, Object) From {
                    {"playerKey", key}, {"name", A(e, "name", "")}, {"created", D(e, "created")}, {"lastPlayed", D(e, "lastPlayed")},
                    {"unlockedLevel", I(e, "unlockedLevel", "1")}, {"ghost", A(e, "ghost", "true") = "true"}})
                For Each g As XElement In e.Elements("Ghost")
                    ghosts.Add(New Dictionary(Of String, Object) From {
                        {"playerKey", key}, {"level", I(g, "level", "0")}, {"time", Dec(g, "time")},
                        {"stepMs", F(g, "stepMs", "0")}, {"points", g.Value}})
                Next
            Next

            For Each e As XElement In root.Elements().Where(Function(x) x.Name.LocalName = "Run" OrElse x.Name.LocalName = "Record")
                runs.Add(New Dictionary(Of String, Object) From {
                    {"id", runs.Count + 1}, {"player", A(e, "player", "")}, {"playerKey", ScoreManager.KeyOf(A(e, "player", ""))},
                    {"level", I(e, "level", "0")},
                    {"campaignLevels", If(e.Attribute("campaignLevels") Is Nothing, DBNull.Value, CObj(I(e, "campaignLevels", "3")))},
                    {"outcome", A(e, "outcome", RunOutcome.Cleared)}, {"time", Dec(e, "time")},
                    {"attempts", I(e, "attempts", "1")}, {"stars", I(e, "stars", "0")}, {"score", I(e, "score", "0")},
                    {"wallHits", I(e, "wallHits", "0")}, {"falls", I(e, "falls", "0")}, {"heartsLost", F(e, "heartsLost", "0")},
                    {"barriersBroken", I(e, "barriersBroken", "0")}, {"date", D(e, "date")}})
            Next

            Dim s As XElement = root.Element("Settings")
            If s IsNot Nothing Then
                settings.Add(New Dictionary(Of String, Object) From {
                    {"id", 1}, {"LastPlayer", If(CStr(s.Attribute("LastPlayer")), A(s, "PlayerName", ""))},
                    {"FullTiltDegrees", F(s, "FullTiltDegrees", "20")}, {"Deadzone", F(s, "Deadzone", "0.08")},
                    {"Smoothing", F(s, "Smoothing", "0.35")}, {"SwapAxes", A(s, "SwapAxes", "false") = "true"},
                    {"InvertX", A(s, "InvertX", "false") = "true"}, {"InvertY", A(s, "InvertY", "false") = "true"}})
            End If

            Return New Dictionary(Of String, List(Of Dictionary(Of String, Object))) From {
                {"players", players}, {"runs", runs}, {"ghosts", ghosts}, {"settings", settings}}
        End Function

        ' ── Rows → MySQL ────────────────────────────────────────────────────
        ' ponytail: a full replace per save is fine for a school-sized journal;
        ' switch to per-row upserts keyed on runs.id if it gets slow.
        Public Shared Sub WriteAll(connectionString As String, root As XElement)
            Dim data = Tables(root)
            Using con As New MySqlConnection(connectionString)
                con.Open()
                Using tx As MySqlTransaction = con.BeginTransaction()
                    For Each table As String In {"runs", "ghosts", "players", "settings"}
                        Using cmd As New MySqlCommand("DELETE FROM `" & table & "`", con, tx)
                            cmd.ExecuteNonQuery()
                        End Using
                    Next
                    For Each table As String In {"players", "runs", "ghosts", "settings"}
                        For Each row As Dictionary(Of String, Object) In data(table)
                            Dim cols As String() = row.Keys.ToArray()
                            Dim sql As String = "INSERT INTO `" & table & "` (" & String.Join(", ", cols.Select(Function(c) "`" & c & "`")) &
                                ") VALUES (" & String.Join(", ", cols.Select(Function(c, n) "@p" & n)) & ")"
                            Using cmd As New MySqlCommand(sql, con, tx)
                                For n As Integer = 0 To cols.Length - 1
                                    cmd.Parameters.AddWithValue("@p" & n, row(cols(n)))
                                Next
                                cmd.ExecuteNonQuery()
                            End Using
                        Next
                    Next
                    tx.Commit()
                End Using
            End Using
        End Sub
    End Class
End Namespace
