Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports GravityMaze.Data

Namespace UI.Screens
    ' Who's Playing?, name de-duplication, the Profile screen and the Records filter.
    ' Every list is a MenuList, so the board and the keyboard drive them the same way.
    Partial Public NotInheritable Class GameShell
        Private ReadOnly _playerMenu As New MenuList()
        Private _playerList As New List(Of PlayerProfile)()
        Private ReadOnly _profileMenu As New MenuList("GHOST: ON", "BACK")
        Private ReadOnly _recordsMenu As New MenuList("SHOW: ALL PLAYERS", "BACK")
        Private _recordsMineOnly As Boolean
        Private _nameNotice As String = ""
        Private _pendingPlayer As PlayerProfile   ' typed name matched this profile; Enter again confirms

        Public ReadOnly Property NameNotice As String
            Get
                Return _nameNotice
            End Get
        End Property

        Public ReadOnly Property RecordsMineOnly As Boolean
            Get
                Return _recordsMineOnly
            End Get
        End Property

        ' No profiles yet: straight to typing. Otherwise list players (current first), then + NEW PLAYER and BACK.
        Private Sub OpenPlayerSelect(after As Action)
            _afterPlayerChosen = after
            _playerList = _scores.Players.Take(7).ToList()   ' ponytail: 7 most recent fit above the footer; upgrade to a scrolling list for more. Older players type their name and continue via the "already exists" prompt.
            If _playerList.Count = 0 Then OpenNameEntry() : Return
            _playerMenu.Items.Clear()
            For Each p As PlayerProfile In _playerList
                _playerMenu.Items.Add(p.Name)
            Next
            _playerMenu.Items.Add("+ NEW PLAYER")
            _playerMenu.Items.Add("BACK")
            GoTo_(ShellScreen.PlayerSelect)
            Dim key As String = ScoreManager.KeyOf(_scores.PlayerName)
            _playerMenu.Reset(Math.Max(0, _playerList.FindIndex(Function(p) p.Key = key)))
        End Sub

        Private Sub PlayerChoice(index As Integer)
            If index < _playerList.Count Then
                _scores.PlayerName = _playerList(index).Name
                ContinueAfterPlayer()
            ElseIf index = _playerList.Count Then
                OpenNameEntry()
            Else
                _afterPlayerChosen = Nothing
                GoTo_(ShellScreen.Title)
            End If
        End Sub

        ' First Enter on a name that already exists asks; the second Enter continues as that player.
        Private Function ConfirmNewName(name As String) As Boolean
            Dim existing As PlayerProfile = _scores.FindPlayer(name)
            If existing Is Nothing OrElse existing Is _pendingPlayer Then Return True
            _pendingPlayer = existing
            _nameNotice = $"{existing.Name} ALREADY EXISTS  ·  PRESS ENTER TO CONTINUE AS {existing.Name}"
            Return False
        End Function

        Private Sub ClearNameNotice()
            _nameNotice = ""
            _pendingPlayer = Nothing
        End Sub

        Private Sub OpenProfile()
            RefreshProfileLabels()
            GoTo_(ShellScreen.Profile)
        End Sub

        Private Sub RefreshProfileLabels()
            _profileMenu.Items(0) = "GHOST: " & If(_scores.GhostEnabled, "ON", "OFF")
        End Sub

        Private Sub ProfileChoice(index As Integer)
            If index = 0 Then
                _scores.GhostEnabled = Not _scores.GhostEnabled
                RefreshProfileLabels()
            Else
                GoTo_(ShellScreen.Title)
            End If
        End Sub

        Private Sub RecordsChoice(index As Integer)
            If index = 0 Then
                _recordsMineOnly = Not _recordsMineOnly
                _recordsMenu.Items(0) = "SHOW: " & If(_recordsMineOnly, "JUST ME", "ALL PLAYERS")
            Else
                Back()
            End If
        End Sub
    End Class
End Namespace
