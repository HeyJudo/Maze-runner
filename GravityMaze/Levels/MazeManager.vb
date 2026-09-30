Option Strict On
Option Explicit On

Imports System.IO

Namespace Levels
    Public NotInheritable Class MazeManager
        Private Sub New()
        End Sub

        Public Shared Function LoadFromFile(filePath As String) As MazeDefinition
            Return New MazeDefinition(File.ReadAllLines(filePath))
        End Function
    End Class
End Namespace
