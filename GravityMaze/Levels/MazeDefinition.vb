Option Strict On
Option Explicit On

Imports System

Namespace Levels
    ' Level data only. No UI, rendering, input, or physics belongs here.
    Public NotInheritable Class MazeDefinition
        Private ReadOnly rows As String()

        Public ReadOnly Property RowCount As Integer
        Public ReadOnly Property ColumnCount As Integer
        Public ReadOnly Property StartRow As Integer
        Public ReadOnly Property StartColumn As Integer
        Public ReadOnly Property GoalRow As Integer
        Public ReadOnly Property GoalColumn As Integer

        Public Sub New(levelRows As String())
            If levelRows Is Nothing OrElse levelRows.Length < 3 Then
                Throw New ArgumentException("A maze must have at least three rows.")
            End If
            If String.IsNullOrEmpty(levelRows(0)) OrElse levelRows(0).Length < 3 Then
                Throw New ArgumentException("A maze must have at least three columns.")
            End If

            rows = DirectCast(levelRows.Clone(), String())
            RowCount = rows.Length
            ColumnCount = rows(0).Length
            Dim startCount As Integer = 0
            Dim goalCount As Integer = 0

            For rowIndex As Integer = 0 To RowCount - 1
                If rows(rowIndex) Is Nothing OrElse rows(rowIndex).Length <> ColumnCount Then
                    Throw New ArgumentException("Every maze row must have the same number of characters.")
                End If
                For columnIndex As Integer = 0 To ColumnCount - 1
                    Dim tile As Char = rows(rowIndex)(columnIndex)
                    If "10SGIMF".IndexOf(tile) < 0 Then
                        Throw New ArgumentException($"Unknown maze symbol '{tile}' at row {rowIndex + 1}, column {columnIndex + 1}.")
                    End If
                    Dim isEdge As Boolean = rowIndex = 0 OrElse rowIndex = RowCount - 1 OrElse
                                            columnIndex = 0 OrElse columnIndex = ColumnCount - 1
                    ' S and G may sit on the edge as entrance/exit openings; the engine treats off-grid as wall.
                    If isEdge AndAlso tile <> "1"c AndAlso tile <> "S"c AndAlso tile <> "G"c Then
                        Throw New ArgumentException("The outside edge of the maze must contain only walls (1), S, or G.")
                    End If
                    Select Case tile
                        Case "S"c
                            startCount += 1
                            StartRow = rowIndex
                            StartColumn = columnIndex
                        Case "G"c
                            goalCount += 1
                            GoalRow = rowIndex
                            GoalColumn = columnIndex
                    End Select
                Next
            Next

            If startCount <> 1 OrElse goalCount <> 1 Then
                Throw New ArgumentException("A maze must contain exactly one start (S) and one goal (G).")
            End If
        End Sub

        Public Function GetTile(rowIndex As Integer, columnIndex As Integer) As Char
            If rowIndex < 0 OrElse rowIndex >= RowCount OrElse
               columnIndex < 0 OrElse columnIndex >= ColumnCount Then
                Throw New ArgumentOutOfRangeException(NameOf(rowIndex), "Tile coordinates are outside the maze.")
            End If
            Return rows(rowIndex)(columnIndex)
        End Function
    End Class
End Namespace
