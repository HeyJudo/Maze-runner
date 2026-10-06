Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Threading.Tasks

Namespace Rendering
    ' A perspective camera viewing a textured plane, in normalized coordinates (-1..1).
    ' Explicit homography lets the artwork and every effect share exactly the same mapping.
    Public NotInheritable Class BoardProjection
        Private ReadOnly _a, _b, _c, _d, _p, _q As Double
        Private ReadOnly _ia, _ib, _ic, _id As Double
        Private ReadOnly _scale As Double

        Public Sub New(width As Integer, height As Integer, tiltX As Double, tiltY As Double)
            If width <= 0 OrElse height <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(width))
            Dim pitch As Double = (BoardTilt.RestPitchDegrees - BoardTilt.MaxTiltDegrees * Math.Clamp(tiltY, -1.0, 1.0)) * Math.PI / 180
            Dim roll As Double = BoardTilt.MaxTiltDegrees * Math.Clamp(tiltX, -1.0, 1.0) * Math.PI / 180
            Dim aspect As Double = CDbl(width) / height
            Dim cameraDistance As Double = 4.5 * Math.Max(1.0, aspect)
            Dim cp = Math.Cos(pitch), sp = Math.Sin(pitch)
            Dim cr = Math.Cos(roll), sr = Math.Sin(roll)
            ' Pitch around X, then roll around Y. Positive input lowers its corresponding edge.
            _a = cr
            _b = sr * sp / aspect
            _c = 0
            _d = cp
            _p = sr * aspect / cameraDistance
            _q = -cr * sp / cameraDistance
            ' Fixed headroom for every supported angle; never auto-zoom as the player tilts.
            Dim maxRoll As Double = Math.Sin(BoardTilt.MaxTiltDegrees * Math.PI / 180)
            Dim maxPitch As Double = Math.Sin((BoardTilt.RestPitchDegrees + BoardTilt.MaxTiltDegrees) * Math.PI / 180)
            Dim nearestDepth As Double = 1 - (maxRoll * aspect + maxPitch) / cameraDistance
            Dim scale As Double = 0.97 * nearestDepth / (1 + maxRoll * maxPitch / aspect)
            _scale = scale
            _a *= scale : _b *= scale : _c *= scale : _d *= scale
            Dim det As Double = _a * _d - _b * _c
            _ia = _d / det : _ib = -_b / det
            _ic = -_c / det : _id = _a / det
        End Sub

        Public Function Project(x As Double, y As Double) As PointF
            Dim denominator As Double = 1 + _p * x + _q * y
            Return New PointF(CSng((_a * x + _b * y) / denominator), CSng((_c * x + _d * y) / denominator))
        End Function

        ' A sphere keeps its circular silhouette; only its location and depth scale follow the plane.
        Public Function ProjectSphere(center As PointF, radius As Single, width As Integer, height As Integer) As (Center As PointF, Radius As Single)
            Dim x As Double = center.X * 2.0 / width - 1
            Dim y As Double = center.Y * 2.0 / height - 1
            Dim projected = Project(x, y)
            Return (New PointF((projected.X + 1) * width / 2, (projected.Y + 1) * height / 2),
                    CSng(radius * _scale / (1 + _p * x + _q * y)))
        End Function

        Public Function Unproject(x As Double, y As Double) As PointF
            Dim u As Double = _ia * x + _ib * y
            Dim v As Double = _ic * x + _id * y
            Dim denominator As Double = 1 - _p * u - _q * v
            Return New PointF(CSng(u / denominator), CSng(v / denominator))
        End Function

        ' Managed pixel mapping is also runnable in the platform-independent verification target.
        ' Buffers use premultiplied BGRA, matching Format32bppPArgb. Bilinear interpolation keeps
        ' walls and text smooth; transparent pixels outside the board preserve the backdrop.
        Public Sub Warp(source As Byte(), destination As Byte(), width As Integer, height As Integer)
            If source.Length <> width * height * 4 OrElse destination.Length <> source.Length Then
                Throw New ArgumentException("Pixel buffers must match the requested size.")
            End If
            Array.Clear(destination)
            Dim dx As Double = 2.0 / width
            Dim du As Double = _ia * dx, dv As Double = _ic * dx
            Dim dd As Double = -_p * du - _q * dv
            Dim workers As Integer = If(width * height >= 256 * 256, Math.Min(4, Environment.ProcessorCount), 1)
            Parallel.For(0, workers, Sub(worker As Integer)
                For row As Integer = worker * height \ workers To (worker + 1) * height \ workers - 1
                    Dim y As Double = (row + 0.5) * 2 / height - 1
                    Dim x As Double = 1.0 / width - 1
                    Dim u As Double = _ia * x + _ib * y
                    Dim v As Double = _ic * x + _id * y
                    Dim denominator As Double = 1 - _p * u - _q * v
                    Dim output As Integer = row * width * 4
                    For col As Integer = 0 To width - 1
                        Dim sx As Double = (u / denominator + 1) * width * 0.5 - 0.5
                        Dim sy As Double = (v / denominator + 1) * height * 0.5 - 0.5
                        If sx >= 0 AndAlso sx < width - 1 AndAlso sy >= 0 AndAlso sy < height - 1 Then
                            Dim ix As Integer = CInt(Math.Floor(sx)), iy As Integer = CInt(Math.Floor(sy))
                            ' Fixed-point weights avoid eight floating-point multiplies per channel.
                            Dim fx As Integer = CInt((sx - ix) * 256), fy As Integer = CInt((sy - iy) * 256)
                            Dim w00 As Integer = (256 - fx) * (256 - fy)
                            Dim w10 As Integer = fx * (256 - fy)
                            Dim w01 As Integer = (256 - fx) * fy
                            Dim w11 As Integer = fx * fy
                            Dim start As Integer = (iy * width + ix) * 4
                            Dim below As Integer = start + width * 4
                            For channel As Integer = 0 To 3
                                destination(output + channel) = CByte((source(start + channel) * w00 +
                                    source(start + 4 + channel) * w10 + source(below + channel) * w01 +
                                    source(below + 4 + channel) * w11 + 32768) >> 16)
                            Next
                        End If
                        u += du : v += dv : denominator += dd : output += 4
                    Next
                Next
            End Sub)
        End Sub
    End Class
End Namespace
