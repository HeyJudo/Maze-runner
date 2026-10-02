Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO

Namespace UI
    ' Loads the heart sprites once from <output>\Sprites. Missing files return Nothing; DrawHeart then
    ' paints a plain red heart instead of crashing.
    Public NotInheritable Class Sprites
        Private Shared _heart As Image
        Private Shared _frames As List(Of Image)
        Private Shared _loaded As Boolean

        ' Spritesheet frame indices (row-major, 32x32): full, 3/4, 1/2, 1/4, empty — the lose-a-heart animation.
        Public Shared ReadOnly DrainFrames As Integer() = {0, 3, 1, 2, 4}
        Public Const EmptyFrame As Integer = 4

        Private Sub New()
        End Sub

        Public Shared ReadOnly Property Heart As Image
            Get
                EnsureLoaded()
                Return _heart
            End Get
        End Property

        Public Shared Function HeartFrame(index As Integer) As Image
            EnsureLoaded()
            If _frames Is Nothing OrElse index < 0 OrElse index >= _frames.Count Then Return Nothing
            Return _frames(index)
        End Function

        ' Draws a heart image crisp (nearest-neighbour) into r; img Nothing = fallback red heart shape.
        Public Shared Sub DrawHeart(g As Graphics, img As Image, r As RectangleF, Optional alpha As Single = 1.0F)
            If img Is Nothing Then
                Using path As New GraphicsPath(),
                      br As New SolidBrush(Color.FromArgb(CInt(255 * alpha), 214, 40, 52))
                    Dim w As Single = r.Width / 2.0F
                    path.AddEllipse(r.X, r.Y, w + 1, r.Height * 0.6F)
                    path.AddEllipse(r.X + w - 1, r.Y, w + 1, r.Height * 0.6F)
                    path.AddPolygon({New PointF(r.X + 0.5F, r.Y + r.Height * 0.38F), New PointF(r.Right - 0.5F, r.Y + r.Height * 0.38F),
                                     New PointF(r.X + w, r.Bottom)})
                    g.FillPath(br, path)
                End Using
                Return
            End If
            Dim interp As InterpolationMode = g.InterpolationMode
            Dim offset As PixelOffsetMode = g.PixelOffsetMode
            g.InterpolationMode = InterpolationMode.NearestNeighbor
            g.PixelOffsetMode = PixelOffsetMode.Half
            If alpha >= 0.999F Then
                g.DrawImage(img, r)
            Else
                Using ia As New ImageAttributes()
                    ia.SetColorMatrix(New ColorMatrix() With {.Matrix33 = alpha})
                    g.DrawImage(img, Rectangle.Round(r), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia)
                End Using
            End If
            g.InterpolationMode = interp
            g.PixelOffsetMode = offset
        End Sub

        Private Shared Sub EnsureLoaded()
            If _loaded Then Return
            _loaded = True
            Dim dir As String = Path.Combine(AppContext.BaseDirectory, "Sprites")
            Try
                _heart = Image.FromFile(Path.Combine(dir, "heart_32x32.png"))
                Using sheet As Image = Image.FromFile(Path.Combine(dir, "heart_spritesheet_32x32.png"))
                    _frames = New List(Of Image)()
                    For y As Integer = 0 To sheet.Height - 32 Step 32
                        For x As Integer = 0 To sheet.Width - 32 Step 32
                            Dim f As New Bitmap(32, 32)
                            Using g As Graphics = Graphics.FromImage(f)
                                g.DrawImage(sheet, New Rectangle(0, 0, 32, 32), New Rectangle(x, y, 32, 32), GraphicsUnit.Pixel)
                            End Using
                            _frames.Add(f)
                        Next
                    Next
                End Using
            Catch
                ' ponytail: missing/corrupt sprite -> callers draw a plain red heart instead of crashing.
            End Try
        End Sub
    End Class
End Namespace
