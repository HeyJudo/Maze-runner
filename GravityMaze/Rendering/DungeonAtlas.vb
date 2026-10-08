Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports GravityMaze.Levels

Namespace Rendering
    ' Loads the dungeon sprite atlas once from <output>\Sprites\Dungeon. Any failure leaves it Nothing
    ' and Draw returns False so callers fall back to flat drawing.
    Public NotInheritable Class DungeonAtlas
        Private Shared _atlas As SpriteAtlas
        Private Shared _sheets As Dictionary(Of String, Image)
        Private Shared _loaded As Boolean

        Private Sub New()
        End Sub

        Public Shared Function FrameCount(name As String) As Integer
            EnsureLoaded()
            If _atlas Is Nothing OrElse Not _atlas.Has(name) Then Return 1
            Return _atlas(name).Frames
        End Function

        Public Shared Function Draw(g As Graphics, name As String, dest As RectangleF, Optional frame As Integer = 0, Optional alpha As Single = 1.0F) As Boolean
            EnsureLoaded()
            If _atlas Is Nothing OrElse Not _atlas.Has(name) Then Return False
            Dim s = _atlas(name)
            Dim sheet As Image = Nothing
            If Not _sheets.TryGetValue(s.Sheet, sheet) Then Return False
            Dim f As Integer = ((frame Mod s.Frames) + s.Frames) Mod s.Frames
            Dim src As New Rectangle(s.X + f * s.W, s.Y, s.W, s.H)
            Dim interp As InterpolationMode = g.InterpolationMode
            Dim offset As PixelOffsetMode = g.PixelOffsetMode
            g.InterpolationMode = InterpolationMode.NearestNeighbor
            g.PixelOffsetMode = PixelOffsetMode.Half
            Try
                If alpha >= 0.999F Then
                    g.DrawImage(sheet, dest, src, GraphicsUnit.Pixel)
                Else
                    Using ia As New ImageAttributes()
                        ia.SetColorMatrix(New ColorMatrix() With {.Matrix33 = Math.Max(0.0F, alpha)})
                        g.DrawImage(sheet, Rectangle.Round(dest), src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, ia)
                    End Using
                End If
            Finally
                g.InterpolationMode = interp
                g.PixelOffsetMode = offset
            End Try
            Return True
        End Function

        Private Shared Sub EnsureLoaded()
            If _loaded Then Return
            _loaded = True
            Try
                Dim dir As String = Path.Combine(AppContext.BaseDirectory, "Sprites", "Dungeon")
                Dim atlas = SpriteAtlas.Load(Path.Combine(dir, "atlas.json"))
                Dim sheets As New Dictionary(Of String, Image)()
                For Each sheet In SpriteAtlas.Sheets
                    sheets(sheet) = Image.FromFile(Path.Combine(dir, sheet & ".png"))
                Next
                atlas.Validate(Function(sheet) sheets(sheet).Size)
                _sheets = sheets
                _atlas = atlas
            Catch
                ' ponytail: missing/corrupt atlas -> Draw returns False and callers use flat drawing.
                _atlas = Nothing
            End Try
        End Sub
    End Class
End Namespace
