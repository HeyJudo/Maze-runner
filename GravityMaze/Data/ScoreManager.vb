Option Strict On
Option Explicit On

Imports System
Imports System.IO
Imports System.Linq
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Xml.Linq

Namespace Data
    ' One finished run. LevelNumber 0 = full campaign run.
    Public NotInheritable Class ScoreRecord
        Public Property PlayerName As String = ""
        Public Property LevelNumber As Integer
        Public Property TimeSeconds As Single
        Public Property Attempts As Integer
        Public Property Stars As Integer
        Public Property Score As Integer
        Public Property [Date] As DateTime
    End Class

    ' Performance history stored as XML (no game progress).
    Public NotInheritable Class ScoreManager
        Private ReadOnly filePath As String
        Private ReadOnly records As New List(Of ScoreRecord)()
        Private lastName As String = ""
        Private Shared ReadOnly Inv As CultureInfo = CultureInfo.InvariantCulture

        Public Sub New(filePath As String)
            Me.filePath = filePath
            Load()
        End Sub

        Public Shared Function DefaultPath() As String
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GravityMaze", "records.xml")
        End Function

        ' Last used player name, persisted in <Settings PlayerName="..."/>
        Public Property PlayerName As String
            Get
                Return lastName
            End Get
            Set(value As String)
                lastName = If(value, "")
                Save()
            End Set
        End Property

        ' Saves immediately. Returns True if this is a new personal best time for
        ' that player + level (a player's first run on a level counts as a best).
        Public Function AddRecord(record As ScoreRecord) As Boolean
            Dim prev As ScoreRecord = BestRecordFor(record.PlayerName, record.LevelNumber)
            Dim isBest As Boolean = prev Is Nothing OrElse record.TimeSeconds < prev.TimeSeconds
            records.Add(record)
            Save()
            Return isBest
        End Function

        Public Function BestRecord(levelNumber As Integer) As ScoreRecord
            Return records.Where(Function(r) r.LevelNumber = levelNumber).OrderBy(Function(r) r.TimeSeconds).FirstOrDefault()
        End Function

        Public Function BestRecordFor(playerName As String, levelNumber As Integer) As ScoreRecord
            Return records.Where(Function(r) r.LevelNumber = levelNumber AndAlso
                                     String.Equals(r.PlayerName, playerName, StringComparison.OrdinalIgnoreCase)).
                                     OrderBy(Function(r) r.TimeSeconds).FirstOrDefault()
        End Function

        Public Function TopRecords(levelNumber As Integer, count As Integer) As List(Of ScoreRecord)
            Return records.Where(Function(r) r.LevelNumber = levelNumber).OrderBy(Function(r) r.TimeSeconds).Take(Math.Max(0, count)).ToList()
        End Function

        ' score = stars*1000 + max(0, 3000 - time*25) - (attempts-1)*150, floored at 0
        Public Shared Function ComputeScore(timeSeconds As Single, attempts As Integer, stars As Integer) As Integer
            Dim s As Double = stars * 1000.0 + Math.Max(0.0, 3000.0 - timeSeconds * 25.0) - (Math.Max(1, attempts) - 1) * 150.0
            Return CInt(Math.Max(0.0, Math.Round(s)))
        End Function

        Private Sub Load()
            If Not File.Exists(filePath) Then Return
            Try
                Dim root As XElement = XDocument.Load(filePath).Root
                If root Is Nothing OrElse root.Name.LocalName <> "GravityMazeRecords" Then Throw New FormatException("bad root")
                Dim loaded As New List(Of ScoreRecord)()
                For Each e As XElement In root.Elements("Record")
                    loaded.Add(New ScoreRecord With {
                        .PlayerName = If(CStr(e.Attribute("player")), ""),
                        .LevelNumber = Integer.Parse(CStr(e.Attribute("level")), Inv),
                        .TimeSeconds = Single.Parse(CStr(e.Attribute("time")), Inv),
                        .Attempts = Integer.Parse(CStr(e.Attribute("attempts")), Inv),
                        .Stars = Integer.Parse(CStr(e.Attribute("stars")), Inv),
                        .Score = Integer.Parse(CStr(e.Attribute("score")), Inv),
                        .[Date] = DateTime.Parse(CStr(e.Attribute("date")), Inv, DateTimeStyles.RoundtripKind)
                    })
                Next
                Dim st As XElement = root.Element("Settings")
                If st IsNot Nothing Then lastName = If(CStr(st.Attribute("PlayerName")), "")
                records.AddRange(loaded)
            Catch
                ' corrupt file: keep a backup, start empty
                records.Clear()
                lastName = ""
                Try
                    File.Copy(filePath, filePath & ".bak", True)
                Catch
                End Try
            End Try
        End Sub

        Private Sub Save()
            Try
                Dim root As New XElement("GravityMazeRecords", New XAttribute("version", "1"),
                                         New XElement("Settings", New XAttribute("PlayerName", lastName)))
                For Each r As ScoreRecord In records
                    root.Add(New XElement("Record",
                        New XAttribute("player", r.PlayerName),
                        New XAttribute("level", r.LevelNumber.ToString(Inv)),
                        New XAttribute("time", r.TimeSeconds.ToString("0.00", Inv)),
                        New XAttribute("attempts", r.Attempts.ToString(Inv)),
                        New XAttribute("stars", r.Stars.ToString(Inv)),
                        New XAttribute("score", r.Score.ToString(Inv)),
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
