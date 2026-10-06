Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization

Namespace Levels
    ' Authored level data; the engine owns clocks/collisions, rendering owns artwork.
    Public NotInheritable Class SpikeTrap
        Public Property Row As Integer
        Public Property Column As Integer
        Public Property OffsetMs As Integer
    End Class

    Public NotInheritable Class BladeTrap
        Public Property X1 As Single
        Public Property Y1 As Single
        Public Property X2 As Single
        Public Property Y2 As Single
        Public Property OffsetMs As Integer
        Public Property PeriodMs As Integer = 6000
    End Class

    Public NotInheritable Class DartLauncher
        Public Property X As Single
        Public Property Y As Single
        Public Property DX As Integer
        Public Property DY As Integer
        Public Property OffsetMs As Integer
    End Class

    Public NotInheritable Class DungeonLandmark
        Public Property Name As String = ""
        Public Property Column As Integer
        Public Property Row As Integer
        Public Property Kind As String = "torch"
    End Class

    Public NotInheritable Class DungeonDefinition
        Public Property Spikes As New List(Of SpikeTrap)()
        Public Property Blades As New List(Of BladeTrap)()
        Public Property Launchers As New List(Of DartLauncher)()
        Public Property Landmarks As New List(Of DungeonLandmark)()

        Public Shared Function Load(path As String) As DungeonDefinition
            Dim options As New JsonSerializerOptions With {
                .PropertyNameCaseInsensitive = True,
                .UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow}
            Dim result = JsonSerializer.Deserialize(Of DungeonDefinition)(File.ReadAllText(path), options)
            If result Is Nothing Then Throw New InvalidDataException("Dungeon definition is empty: " & path)
            Return result
        End Function

        Public Sub Validate(maze As MazeDefinition)
            If Spikes Is Nothing OrElse Blades Is Nothing OrElse Launchers Is Nothing OrElse Landmarks Is Nothing Then
                Throw New ArgumentException("Dungeon trap and landmark lists cannot be null.")
            End If
            Dim sealsA As Integer, sealsB As Integer, exits As Integer
            For r As Integer = 0 To maze.RowCount - 1
                For c As Integer = 0 To maze.ColumnCount - 1
                    Select Case maze.GetTile(r, c)
                        Case "K"c : sealsA += 1
                        Case "Q"c : sealsB += 1
                        Case "E"c : exits += 1
                    End Select
                Next
            Next
            If sealsA <> 1 OrElse sealsB <> 1 OrElse exits = 0 Then Throw New ArgumentException("A dungeon needs two distinct seals (K/Q) and an exit gate (E).")
            Dim cells As New HashSet(Of Integer)()
            For Each spike In Spikes
                If spike Is Nothing OrElse Not Floor(maze, spike.Column + 0.5F, spike.Row + 0.5F) Then Throw New ArgumentException("Spike must lie on dungeon floor.")
                If maze.GetTile(spike.Row, spike.Column) <> "T"c OrElse Not cells.Add(spike.Row * maze.ColumnCount + spike.Column) Then
                    Throw New ArgumentException("Each spike needs a unique T tile.")
                End If
            Next
            For r As Integer = 0 To maze.RowCount - 1
                For c As Integer = 0 To maze.ColumnCount - 1
                    If maze.GetTile(r, c) = "T"c AndAlso Not cells.Contains(r * maze.ColumnCount + c) Then Throw New ArgumentException("T tile has no spike definition.")
                Next
            Next
            For Each blade In Blades
                If blade Is Nothing OrElse blade.PeriodMs < 4000 OrElse Not Single.IsFinite(blade.X1) OrElse Not Single.IsFinite(blade.Y1) OrElse
                   Not Single.IsFinite(blade.X2) OrElse Not Single.IsFinite(blade.Y2) OrElse
                   (blade.X1 <> blade.X2 AndAlso blade.Y1 <> blade.Y2) OrElse (blade.X1 = blade.X2 AndAlso blade.Y1 = blade.Y2) Then
                    Throw New ArgumentException("Blade tracks need distinct cardinal endpoints and a period of at least four seconds.")
                End If
                Dim samples = CInt(Math.Ceiling((Math.Abs(blade.X2 - blade.X1) + Math.Abs(blade.Y2 - blade.Y1)) * 8))
                For i As Integer = 0 To samples
                    Dim t = i / CSng(samples)
                    If Not Floor(maze, blade.X1 + (blade.X2 - blade.X1) * t, blade.Y1 + (blade.Y2 - blade.Y1) * t) Then
                        Throw New ArgumentException("Blade track crosses a wall or pit.")
                    End If
                Next
            Next
            For Each launcher In Launchers
                If launcher Is Nothing OrElse Math.Abs(launcher.DX) + Math.Abs(launcher.DY) <> 1 OrElse Not Floor(maze, launcher.X, launcher.Y) Then
                    Throw New ArgumentException("Dart launchers need a floor opening and a cardinal direction.")
                End If
            Next
            For Each landmark In Landmarks
                If landmark Is Nothing OrElse landmark.Column < 0 OrElse landmark.Row < 0 OrElse
                   landmark.Column >= maze.ColumnCount OrElse landmark.Row >= maze.RowCount OrElse
                   (landmark.Kind <> "torch" AndAlso landmark.Kind <> "cell" AndAlso landmark.Kind <> "rubble" AndAlso landmark.Kind <> "altar") Then
                    Throw New ArgumentException("Invalid dungeon landmark.")
                End If
            Next
        End Sub

        Private Shared Function Floor(maze As MazeDefinition, x As Single, y As Single) As Boolean
            If Not Single.IsFinite(x) OrElse Not Single.IsFinite(y) OrElse x < 0 OrElse y < 0 OrElse x >= maze.ColumnCount OrElse y >= maze.RowCount Then Return False
            Return "0TM".Contains(maze.GetTile(CInt(Math.Floor(y)), CInt(Math.Floor(x))))
        End Function
    End Class
End Namespace
