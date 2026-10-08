Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic

Namespace Rendering
    ' Picks atlas pieces for one wall tile from its neighbours. First piece is the base, the rest are overlays.
    Public NotInheritable Class WallAutotile
        Private Sub New()
        End Sub

        Public Shared Function Pieces(isWall As Func(Of Integer, Integer, Boolean), row As Integer, column As Integer) As List(Of String)
            Dim n = isWall(row - 1, column), e = isWall(row, column + 1)
            Dim s = isWall(row + 1, column), w = isWall(row, column - 1)
            Dim result As New List(Of String) From {If(s, "wall_top", "wall_face")}
            If Not n Then result.Add("wall_edge_n")
            If Not e Then result.Add("wall_edge_e")
            If Not w Then result.Add("wall_edge_w")
            If n AndAlso e AndAlso Not isWall(row - 1, column + 1) Then result.Add("wall_inner_ne")
            If n AndAlso w AndAlso Not isWall(row - 1, column - 1) Then result.Add("wall_inner_nw")
            If s AndAlso e AndAlso Not isWall(row + 1, column + 1) Then result.Add("wall_inner_se")
            If s AndAlso w AndAlso Not isWall(row + 1, column - 1) Then result.Add("wall_inner_sw")
            Return result
        End Function
    End Class
End Namespace
