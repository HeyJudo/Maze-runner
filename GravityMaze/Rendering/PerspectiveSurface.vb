Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Drawing.Drawing2D
Imports System.Runtime.InteropServices

Namespace Rendering
    ' Reuses bounded offscreen surfaces; avoids allocating a full-screen bitmap every frame.
    Public NotInheritable Class PerspectiveSurface
        Implements IDisposable
        Private _source As Bitmap
        Private _output As Bitmap
        Private _sourcePixels As Byte()
        Private _outputPixels As Byte()

        Public Sub Draw(target As Graphics, bounds As Rectangle, tilt As BoardTilt,
                        paint As Action(Of Graphics, Rectangle),
                        Optional project As Boolean = True, Optional opacity As Single = 1.0F,
                        Optional paintSpheres As Action(Of Graphics, Rectangle, BoardProjection) = Nothing)
            If bounds.Width < 32 OrElse bounds.Height < 32 Then Return
            Dim factor As Double = Math.Min(1.0, 1024.0 / Math.Max(bounds.Width, bounds.Height))
            Dim width As Integer = Math.Max(32, CInt(bounds.Width * factor))
            Dim height As Integer = Math.Max(32, CInt(bounds.Height * factor))
            If _source Is Nothing OrElse _source.Width <> width OrElse _source.Height <> height Then
                Dispose()
                _source = New Bitmap(width, height, PixelFormat.Format32bppPArgb)
                _output = New Bitmap(width, height, PixelFormat.Format32bppPArgb)
                _sourcePixels = New Byte(width * height * 4 - 1) {}
                _outputPixels = New Byte(_sourcePixels.Length - 1) {}
            End If
            Dim area As New Rectangle(0, 0, width, height)
            Using g As Graphics = Graphics.FromImage(_source)
                g.Clear(Color.Transparent)
                paint(g, area)
            End Using
            Dim frame As Bitmap = _source
            Dim projection As BoardProjection = Nothing
            If project Then
                Dim data As BitmapData = _source.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb)
                Try
                    Marshal.Copy(data.Scan0, _sourcePixels, 0, _sourcePixels.Length)
                Finally
                    _source.UnlockBits(data)
                End Try
                projection = New BoardProjection(width, height, tilt.X, tilt.Y)
                projection.Warp(_sourcePixels, _outputPixels, width, height)
                data = _output.LockBits(area, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb)
                Try
                    Marshal.Copy(_outputPixels, 0, data.Scan0, _outputPixels.Length)
                Finally
                    _output.UnlockBits(data)
                End Try
                frame = _output
            End If
            If paintSpheres IsNot Nothing Then
                Using g As Graphics = Graphics.FromImage(frame)
                    g.SmoothingMode = SmoothingMode.AntiAlias
                    paintSpheres(g, area, projection)
                End Using
            End If
            Dim state = target.Save()
            Try
                target.SetClip(bounds, CombineMode.Intersect)
                target.InterpolationMode = InterpolationMode.HighQualityBilinear
                If opacity >= 1 Then
                    target.DrawImage(frame, bounds)
                Else
                    Using attributes As New ImageAttributes()
                        attributes.SetColorMatrix(New ColorMatrix() With {.Matrix33 = Math.Clamp(opacity, 0.0F, 1.0F)})
                        target.DrawImage(frame, bounds, 0, 0, width, height, GraphicsUnit.Pixel, attributes)
                    End Using
                End If
            Finally
                target.Restore(state)
            End Try
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            _source?.Dispose() : _output?.Dispose()
            _source = Nothing : _output = Nothing
            _sourcePixels = Nothing : _outputPixels = Nothing
        End Sub
    End Class
End Namespace
