Option Strict On
Option Explicit On

Imports System.IO

Namespace Levels
    Public NotInheritable Class MazeManager
        Private Sub New()
        End Sub

        Public Shared Function LoadFromFile(filePath As String) As MazeDefinition
            Dim dungeonPath = Path.ChangeExtension(filePath, ".dungeon.json")
            Dim dungeon As DungeonDefinition = Nothing
            If File.Exists(dungeonPath) Then dungeon = DungeonDefinition.Load(dungeonPath)
            Return New MazeDefinition(File.ReadAllLines(filePath), dungeon)
        End Function
    End Class
End Namespace
