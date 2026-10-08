Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Text.Json

Namespace Levels
    Public NotInheritable Class AtlasSprite
        Public Property Sheet As String = ""
        Public Property X As Integer
        Public Property Y As Integer
        Public Property W As Integer
        Public Property H As Integer
        Public Property Frames As Integer = 1
    End Class

    ' Name -> rectangle map for the dungeon sheets. Frames sit left to right, each W wide.
    Public NotInheritable Class SpriteAtlas
        Public Shared ReadOnly Sheets As String() = {"dungeon", "freed", "maze", "extra"}
        Public Shared ReadOnly RequiredNames As String() = (
            "floor_0 floor_1 floor_2 floor_3 wall_top wall_face wall_edge_n wall_edge_e wall_edge_s wall_edge_w " &
            "wall_inner_ne wall_inner_nw wall_inner_se wall_inner_sw gate_closed gate_open door_wood arch_open arch_pillars " &
            "torch chains banner gold_pile gold_coins gold_stack chest pot skull bones keg pedestal iron_ball table chair " &
            "rack_swords rack_spears spikes saw arrow crumble lava pit rubble key blood").Split(" "c)
        Private ReadOnly sprites As Dictionary(Of String, AtlasSprite)

        Private Sub New(sprites As Dictionary(Of String, AtlasSprite))
            Me.sprites = sprites
        End Sub

        Public Shared Function Load(path As String) As SpriteAtlas
            Dim options As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}
            Dim map = JsonSerializer.Deserialize(Of Dictionary(Of String, AtlasSprite))(File.ReadAllText(path), options)
            If map Is Nothing Then Throw New InvalidDataException("Atlas is empty: " & path)
            Return New SpriteAtlas(map)
        End Function

        Public Function Has(name As String) As Boolean
            Return sprites.ContainsKey(name)
        End Function

        Default Public ReadOnly Property Item(name As String) As AtlasSprite
            Get
                Return sprites(name)
            End Get
        End Property

        Public Sub Validate(sheetSize As Func(Of String, Size))
            For Each pair In sprites
                Dim s = pair.Value
                If s Is Nothing OrElse Array.IndexOf(Sheets, s.Sheet) < 0 OrElse s.W <= 0 OrElse s.H <= 0 OrElse s.Frames < 1 OrElse s.X < 0 OrElse s.Y < 0 Then
                    Throw New InvalidDataException("Bad atlas entry " & pair.Key)
                End If
                Dim size = sheetSize(s.Sheet)
                If s.X + s.W * s.Frames > size.Width OrElse s.Y + s.H > size.Height Then Throw New InvalidDataException("Atlas entry outside its sheet: " & pair.Key)
            Next
        End Sub

        ' PNG width/height live big-endian at bytes 16-23 of the IHDR chunk.
        Public Shared Function PngSize(path As String) As Size
            Dim b = New Byte(23) {}
            Using f = File.OpenRead(path)
                If f.Read(b, 0, 24) <> 24 Then Throw New InvalidDataException("Not a PNG: " & path)
            End Using
            ' Widen to Integer first: Byte << 8 would wrap the shift count and drop the high bytes.
            Dim w = CInt(b(16)) << 24 Or CInt(b(17)) << 16 Or CInt(b(18)) << 8 Or CInt(b(19))
            Dim h = CInt(b(20)) << 24 Or CInt(b(21)) << 16 Or CInt(b(22)) << 8 Or CInt(b(23))
            Return New Size(w, h)
        End Function
    End Class
End Namespace
