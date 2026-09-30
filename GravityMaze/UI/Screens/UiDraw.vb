Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Text

Namespace UI.Screens
    ' Small drawing toolkit shared by all screens. Pure drawing, no state.
    Public NotInheritable Class UiDraw
        Private Sub New()
        End Sub

        Private Shared ReadOnly Typographic As StringFormat = CreateFormat()

        Private Shared Function CreateFormat() As StringFormat
            Dim f As StringFormat = CType(StringFormat.GenericTypographic.Clone(), StringFormat)
            f.FormatFlags = f.FormatFlags Or StringFormatFlags.MeasureTrailingSpaces Or StringFormatFlags.NoWrap
            Return f
        End Function

        Public Shared Sub Prepare(g As Graphics)
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.TextRenderingHint = TextRenderingHint.AntiAlias
            g.InterpolationMode = InterpolationMode.HighQualityBilinear
        End Sub

        Public Shared Function Measure(g As Graphics, text As String, font As Font, Optional tracking As Single = 0.0F) As SizeF
            If String.IsNullOrEmpty(text) Then Return SizeF.Empty
            Dim s As SizeF = g.MeasureString(text, font, PointF.Empty, Typographic)
            Return New SizeF(s.Width + tracking * (text.Length - 1), s.Height)
        End Function

        ' Draws text with optional letter tracking. align: 0 = left, 0.5 = centre, 1 = right (relative to x).
        Public Shared Sub Text(g As Graphics, value As String, font As Font, color As Color,
                               x As Single, y As Single, Optional align As Single = 0.0F,
                               Optional tracking As Single = 0.0F)
            If String.IsNullOrEmpty(value) OrElse color.A = 0 Then Return
            Dim width As Single = Measure(g, value, font, tracking).Width
            Dim cx As Single = x - width * align
            Using brush As New SolidBrush(color)
                If tracking = 0.0F Then
                    g.DrawString(value, font, brush, cx, y, Typographic)
                Else
                    For Each ch As Char In value
                        Dim s As String = ch.ToString()
                        g.DrawString(s, font, brush, cx, y, Typographic)
                        cx += g.MeasureString(s, font, PointF.Empty, Typographic).Width + tracking
                    Next
                End If
            End Using
        End Sub

        ' Text with a soft coloured glow behind it (cheap: a few offset passes at low alpha).
        Public Shared Sub GlowText(g As Graphics, value As String, font As Font, color As Color, glow As Color,
                                   x As Single, y As Single, Optional align As Single = 0.0F,
                                   Optional tracking As Single = 0.0F, Optional radius As Single = 4.0F)
            Dim glowColor As Color = Color.FromArgb(CInt(Math.Min(255, glow.A * 0.18F)), glow)
            For Each o As PointF In {New PointF(-radius, 0), New PointF(radius, 0), New PointF(0, -radius), New PointF(0, radius),
                                     New PointF(-radius * 0.7F, -radius * 0.7F), New PointF(radius * 0.7F, radius * 0.7F),
                                     New PointF(radius * 0.7F, -radius * 0.7F), New PointF(-radius * 0.7F, radius * 0.7F)}
                Text(g, value, font, glowColor, x + o.X, y + o.Y, align, tracking)
            Next
            Text(g, value, font, Color.FromArgb(120, 0, 0, 0), x + 2, y + 3, align, tracking)
            Text(g, value, font, color, x, y, align, tracking)
        End Sub

        Public Shared Function RoundRect(r As RectangleF, radius As Single) As GraphicsPath
            Dim p As New GraphicsPath()
            Dim d As Single = Math.Min(radius * 2.0F, Math.Min(r.Width, r.Height))
            If d <= 0.5F Then
                p.AddRectangle(r)
                Return p
            End If
            p.AddArc(r.X, r.Y, d, d, 180, 90)
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90)
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
            p.CloseFigure()
            Return p
        End Function

        ' Frosted-glass style panel: drop shadow, fill, thin accent border, top highlight.
        Public Shared Sub Panel(g As Graphics, r As RectangleF, fill As Color, border As Color, Optional radius As Single = 14.0F)
            For i As Integer = 3 To 1 Step -1
                Dim sr As RectangleF = r
                sr.Inflate(i * 5.0F, i * 5.0F)
                sr.Offset(0, 8.0F)
                Using sp As GraphicsPath = RoundRect(sr, radius + i * 5.0F),
                      sb As New SolidBrush(Color.FromArgb(28, 0, 0, 0))
                    g.FillPath(sb, sp)
                End Using
            Next
            Using path As GraphicsPath = RoundRect(r, radius),
                  fb As New SolidBrush(fill),
                  bp As New Pen(Color.FromArgb(Math.Min(255, CInt(border.A * 0.6F)), border), 1.5F)
                g.FillPath(fb, path)
                g.DrawPath(bp, path)
            End Using
            Using hp As New Pen(Color.FromArgb(40, 255, 255, 255), 1.0F)
                g.DrawLine(hp, r.X + radius, r.Y + 1.5F, r.Right - radius, r.Y + 1.5F)
            End Using
        End Sub

        ' Keyboard hint chip like "[Enter] Select". Returns the width used.
        Public Shared Function KeyHint(g As Graphics, key As String, label As String, x As Single, y As Single,
                                       keyColor As Color, labelColor As Color) As Single
            Dim keyFont As Font = GameFonts.Body(15, GameFonts.FontWeight.Bold)
            Dim labelFont As Font = GameFonts.Body(17, GameFonts.FontWeight.Medium)
            Dim ks As SizeF = Measure(g, key, keyFont)
            Dim chip As New RectangleF(x, y, ks.Width + 14, 24)
            Using path As GraphicsPath = RoundRect(chip, 5),
                  pen As New Pen(Color.FromArgb(150, keyColor), 1.2F),
                  fill As New SolidBrush(Color.FromArgb(40, keyColor))
                g.FillPath(fill, path)
                g.DrawPath(pen, path)
            End Using
            Text(g, key, keyFont, keyColor, chip.X + 7, chip.Y + (24 - ks.Height) / 2.0F)
            Dim ls As SizeF = Measure(g, label, labelFont)
            Text(g, label, labelFont, labelColor, chip.Right + 8, chip.Y + (24 - ls.Height) / 2.0F)
            Return chip.Width + 8 + ls.Width + 26
        End Function

        ' Five-point star; fill 0..1 scales it in with a pop.
        Public Shared Sub Star(g As Graphics, cx As Single, cy As Single, radius As Single, filled As Boolean,
                               accent As Color, Optional scale As Single = 1.0F)
            Dim r As Single = radius * scale
            If r <= 0.5F Then Return
            Dim pts(9) As PointF
            For i As Integer = 0 To 9
                Dim a As Double = -Math.PI / 2 + i * Math.PI / 5
                Dim rr As Single = If(i Mod 2 = 0, r, r * 0.45F)
                pts(i) = New PointF(cx + CSng(Math.Cos(a)) * rr, cy + CSng(Math.Sin(a)) * rr)
            Next
            If filled Then
                Using glow As New SolidBrush(Color.FromArgb(34, accent))
                    g.FillEllipse(glow, cx - r * 1.15F, cy - r * 1.15F, r * 2.3F, r * 2.3F)
                End Using
                Using b As New LinearGradientBrush(New RectangleF(cx - r, cy - r, r * 2, r * 2),
                                                   Color.FromArgb(255, 250, 220), accent, 90.0F)
                    g.FillPolygon(b, pts)
                End Using
            Else
                Using b As New SolidBrush(Color.FromArgb(40, 255, 255, 255)),
                      p As New Pen(Color.FromArgb(90, 255, 255, 255), 1.5F)
                    g.FillPolygon(b, pts)
                    g.DrawPolygon(p, pts)
                End Using
            End If
        End Sub

        Public Shared Function Lerp(a As Color, b As Color, t As Single) As Color
            t = Math.Max(0.0F, Math.Min(1.0F, t))
            Return Color.FromArgb(CInt(a.A + (CInt(b.A) - a.A) * t), CInt(a.R + (CInt(b.R) - a.R) * t),
                                  CInt(a.G + (CInt(b.G) - a.G) * t), CInt(a.B + (CInt(b.B) - a.B) * t))
        End Function

        Public Shared Function WithAlpha(c As Color, alpha As Single) As Color
            Return Color.FromArgb(CInt(Math.Max(0.0F, Math.Min(1.0F, alpha)) * c.A), c)
        End Function

        ' Ease-out cubic / back for UI motion.
        Public Shared Function EaseOut(t As Single) As Single
            t = Math.Max(0.0F, Math.Min(1.0F, t))
            Return 1.0F - (1.0F - t) * (1.0F - t) * (1.0F - t)
        End Function

        Public Shared Function EaseOutBack(t As Single) As Single
            t = Math.Max(0.0F, Math.Min(1.0F, t))
            Const c1 As Single = 1.70158F
            Const c3 As Single = c1 + 1.0F
            Return 1.0F + c3 * CSng(Math.Pow(t - 1, 3)) + c1 * CSng(Math.Pow(t - 1, 2))
        End Function

        Public Shared Function FormatTime(seconds As Single) As String
            If seconds < 0 Then seconds = 0
            Dim totalCs As Integer = CInt(Math.Floor(seconds * 100.0F))
            Return $"{totalCs \ 6000:D2}:{(totalCs \ 100) Mod 60:D2}.{totalCs Mod 100:D2}"
        End Function
    End Class
End Namespace
