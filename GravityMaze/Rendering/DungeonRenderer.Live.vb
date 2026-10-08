Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports GravityMaze.Levels
Imports GravityMaze.Engine

Namespace Rendering
    Partial Public NotInheritable Class DungeonRenderer
        Private Const LightScale As Integer = 2        ' light-map pixels per tile (was 4; halved for speed)
        Private Const Smooth As Integer = 4            ' CPU upsample factor before the stretch -> 8 px per tile
        Private Const Darkness As Single = 0.78F
        Private Shared _light As Bitmap
        Private Shared _smooth As Bitmap
        Private Shared _inBytes, _outBytes As Byte()
        Private Shared _glowMaze As MazeDefinition
        Private Shared _lavaTiles As New List(Of Point)()
        Private Shared _keyTiles As New List(Of Point)()
        Private Shared _hallSlots As New List(Of DecorItem)()

        ' Static per-maze lists so the per-frame passes do not rescan the whole grid for glows and hall slots.
        Private Shared Sub EnsureLists(maze As MazeDefinition)
            If ReferenceEquals(_glowMaze, maze) Then Return
            _glowMaze = maze
            _lavaTiles.Clear() : _keyTiles.Clear() : _hallSlots.Clear()
            Dim vault As Point? = Nothing
            For r As Integer = 0 To maze.RowCount - 1
                For c As Integer = 0 To maze.ColumnCount - 1
                    Select Case maze.GetTile(r, c)
                        Case "W"c : _lavaTiles.Add(New Point(c, r))
                        Case "K"c, "Q"c, "V"c : _keyTiles.Add(New Point(c, r))
                        Case "E"c : vault = New Point(c, r)
                    End Select
                Next
            Next
            If vault Is Nothing Then Return
            Dim v = vault.Value
            Dim pedestals = maze.Dungeon.Decor.Where(Function(d) d.Sprite = "pedestal").
                OrderBy(Function(d) (d.Column - v.X) ^ 2 + (d.Row - v.Y) ^ 2).Take(3).OrderBy(Function(d) d.Column)
            _hallSlots.AddRange(pedestals)
        End Sub

        Private Shared Function KeyTint(symbol As Char) As Color
            Return If(symbol = "K"c, Color.FromArgb(255, 177, 73), If(symbol = "Q"c, Color.FromArgb(183, 137, 255), Color.FromArgb(255, 215, 90)))
        End Function

        Public Shared Sub Draw(g As Graphics, left As Single, top As Single, tile As Single,
                               maze As MazeDefinition, run As DungeonRun)
            Dim definition = maze.Dungeon
            If definition Is Nothing Then Return
            EnsureLists(maze)
            Dim at As Long = If(run Is Nothing, 0, run.ElapsedMs)
            Dim col = run?.Collapse
            Dim clip = g.VisibleClipBounds
            Dim r0 = Math.Max(0, CInt(Math.Floor((clip.Top - top) / tile)) - 1)
            Dim r1 = Math.Min(maze.RowCount - 1, CInt(Math.Ceiling((clip.Bottom - top) / tile)))
            Dim c0 = Math.Max(0, CInt(Math.Floor((clip.Left - left) / tile)) - 1)
            Dim c1 = Math.Min(maze.ColumnCount - 1, CInt(Math.Ceiling((clip.Right - left) / tile)))
            Dim lavaFrames = DungeonAtlas.FrameCount("lava")

            ' 1. Breakaway tiles show floor with rubble once the collapse has begun.
            If col IsNot Nothing Then
                For Each t In definition.Collapse.Breakaway
                    Dim rect As New RectangleF(left + t.Column * tile, top + t.Row * tile, tile, tile)
                    DrawComposite(g, "breakaway", "floor_0", "rubble", 0.8F, rect)
                Next
            End If

            For r As Integer = r0 To r1
                For c As Integer = c0 To c1
                    Dim symbol = maze.GetTile(r, c)
                    Dim rect As New RectangleF(left + c * tile, top + r * tile, tile, tile)
                    Dim center As New PointF(rect.Left + tile / 2, rect.Top + tile / 2)
                    ' 2. Lava and crumbling floor.
                    If symbol = "W"c Then
                        DungeonAtlas.Draw(g, "lava", rect, CInt(at \ 250) Mod lavaFrames)
                    ElseIf symbol = "C"c AndAlso run IsNot Nothing Then
                        Select Case run.CrumbleStage(r, c)
                            Case 0
                                DrawComposite(g, "crumble0", "floor_1", "crumble", 0.6F, rect)
                            Case 1
                                ' ponytail: engine exposes no crack age, so frames cycle on the clock instead of tracking the 800 ms window.
                                DungeonAtlas.Draw(g, "crumble", rect, 1 + CInt(at \ 100) Mod Math.Max(1, DungeonAtlas.FrameCount("crumble") - 1))
                            Case Else
                                DungeonAtlas.Draw(g, "pit", rect)
                        End Select
                    End If
                    ' 3. Collapse front, cave-ins and rubble.
                    If col IsNot Nothing Then
                        If col.IsCollapsed(r, c) Then
                            DungeonAtlas.Draw(g, "pit", rect)
                        ElseIf col.IsCracking(r, c) Then
                            Dim f = (col.Reach + Collapse.CrackBand - col.Distance(r, c)) / Collapse.CrackBand
                            DungeonAtlas.Draw(g, "crumble", rect, CInt(Math.Floor(Math.Min(0.999F, Math.Max(0.0F, f)) * DungeonAtlas.FrameCount("crumble"))))
                        ElseIf col.IsCaveInWarning(r, c) Then
                            DungeonAtlas.Draw(g, "crumble", rect, 0)
                            DrawDust(g, rect, r, c, at)
                        ElseIf col.IsRubble(r, c) Then
                            DungeonAtlas.Draw(g, "rubble", rect)
                        End If
                    End If
                    ' 7. Gates.
                    If symbol = "c"c OrElse symbol = "E"c Then
                        Dim opened = run IsNot Nothing AndAlso run.GateOpen(symbol)
                        Dim h = tile * 44.0F / 37.0F
                        Dim gateRect As New RectangleF(rect.Left, rect.Bottom - h, tile, h)
                        If Not opened Then
                            Using shadow As New SolidBrush(Color.FromArgb(90, 5, 4, 10))
                                g.FillRectangle(shadow, rect)
                            End Using
                        End If
                        If Not DungeonAtlas.Draw(g, If(opened, "gate_open", "gate_closed"), gateRect) Then DrawGate(g, center, tile, symbol, opened)
                    End If
                Next
            Next
            ' 4. Spikes.
            For Each spike In definition.Spikes
                If spike.Row < r0 OrElse spike.Row > r1 OrElse spike.Column < c0 OrElse spike.Column > c1 Then Continue For
                Dim phase = DungeonRun.Phase(at, spike.OffsetMs)
                Dim warning = phase >= DungeonRun.SpikeWarningStart AndAlso phase < DungeonRun.SpikeActiveStart
                Dim active = phase >= DungeonRun.SpikeActiveStart AndAlso phase < DungeonRun.SpikeActiveEnd
                Dim rect As New RectangleF(left + spike.Column * tile, top + spike.Row * tile, tile, tile)
                If warning Then
                    Using danger As New SolidBrush(Color.FromArgb(35 + CInt(30 * (0.5 + 0.5 * Math.Sin(at / 70.0))), 255, 139, 70))
                        g.FillRectangle(danger, rect)
                    End Using
                End If
                Dim frame = If(active, 3, If(warning, 1 + CInt(at \ 120) Mod 2, 0))
                If Not DungeonAtlas.Draw(g, "spikes", New RectangleF(rect.Left, rect.Bottom - tile * 1.25F, tile, tile * 1.25F), frame) Then
                    DrawSpikes(g, rect, warning, active, at)
                End If
            Next
            ' 5. Blades.
            For Each blade In definition.Blades
                Dim a As New PointF(left + blade.X1 * tile, top + blade.Y1 * tile)
                Dim b As New PointF(left + blade.X2 * tile, top + blade.Y2 * tile)
                Using rail As New Pen(Color.FromArgb(26, 23, 32), Math.Max(2, tile * 0.16F)),
                      line As New Pen(Color.FromArgb(126, 114, 139), Math.Max(1, tile * 0.035F))
                    g.DrawLine(rail, a, b) : g.DrawLine(line, a, b)
                End Using
                Dim pos = DungeonRun.BladePosition(blade, at)
                Dim bc As New PointF(left + pos.X * tile, top + pos.Y * tile)
                Dim radius = tile * DungeonRun.BladeRadius
                Using shadow As New SolidBrush(Color.FromArgb(110, 0, 0, 0))
                    g.FillEllipse(shadow, bc.X - radius, bc.Y - radius + radius * 0.22F, radius * 2, radius * 2)
                End Using
                If Not DungeonAtlas.Draw(g, "saw", New RectangleF(bc.X - radius, bc.Y - radius, radius * 2, radius * 2), CInt(at \ 80)) Then DrawBlade(g, bc, radius, at)
            Next
            ' 6. Launchers and darts.
            For Each launcher In definition.Launchers
                DrawLauncher(g, left, top, tile, maze, run, launcher, at)
            Next
            If run IsNot Nothing Then DrawDarts(g, left, top, tile, run)
            ' 8. Keys.
            For Each p In _keyTiles
                If p.Y < r0 OrElse p.Y > r1 OrElse p.X < c0 OrElse p.X > c1 Then Continue For
                Dim symbol = maze.GetTile(p.Y, p.X)
                Dim bit = If(symbol = "K"c, 1, If(symbol = "Q"c, 2, 4))
                Dim taken = run IsNot Nothing AndAlso (run.KeyMask And bit) <> 0
                Dim center As New PointF(left + (p.X + 0.5F) * tile, top + (p.Y + 0.5F) * tile)
                If DungeonAtlas.SizeOf("pedestal").Width <= 0 OrElse DungeonAtlas.SizeOf("key").Width <= 0 Then
                    DrawSeal(g, center, tile * 0.33F, symbol = "K"c, taken, at)
                    Continue For
                End If
                DrawPedestal(g, center.X, center.Y + tile * 0.5F, tile * 1.2F)
                If Not taken Then DrawKey(g, center.X, center.Y - tile * 0.15F, tile, KeyTint(symbol), at)
            Next
            If run IsNot Nothing Then
                For i As Integer = 0 To _hallSlots.Count - 1
                    If (run.KeyMask And (1 << i)) = 0 Then Continue For
                    Dim d = _hallSlots(i)
                    Dim bottom = top + (d.Row + 1) * tile
                    DrawKey(g, left + (d.Column + 0.5F) * tile, bottom - tile * 1.55F, tile, KeyTint(If(i = 0, "K"c, If(i = 1, "Q"c, "V"c))), at)
                Next
            End If
            ' 9. Checkpoints.
            For i As Integer = 0 To definition.Checkpoints.Count - 1
                Dim cp = definition.Checkpoints(i)
                Dim lit = run IsNot Nothing AndAlso run.CheckpointLit(i)
                Dim cx = left + (cp.Column + 0.5F) * tile, bottom = top + (cp.Row + 0.5F) * tile + tile * 0.4F
                Dim potH = tile * 0.8F * 18.0F / 16.0F
                If Not DungeonAtlas.Draw(g, "pot", New RectangleF(cx - tile * 0.4F, bottom - potH, tile * 0.8F, potH), 0, If(lit, 1.0F, 0.7F)) Then
                    Using b As New SolidBrush(Color.FromArgb(70, 60, 78))
                        g.FillEllipse(b, cx - tile * 0.3F, bottom - tile * 0.5F, tile * 0.6F, tile * 0.5F)
                    End Using
                End If
                DrawFlame(g, cx, bottom - potH, tile, at + i * 137, lit)
            Next
            ' 10. Torches.
            For Each d In definition.Decor
                If d.Sprite <> "torch" OrElse d.Row < r0 OrElse d.Row > r1 OrElse d.Column < c0 OrElse d.Column > c1 Then Continue For
                Dim flicker = 0.925F + 0.075F * CSng(Math.Sin(at / 90.0 + d.Row * 3 + d.Column))
                Dim size = DungeonAtlas.SizeOf("torch")
                Dim w = size.Width * tile / 16.0F, h = size.Height * tile / 16.0F
                Dim cx = left + (d.Column + 0.5F) * tile, bottom = top + (d.Row + 1) * tile
                DrawGlow(g, cx, bottom - h * 0.6F, tile * 1.7F * (0.85F + (flicker - 0.85F)), Color.FromArgb(70, 255, 160, 70))
                DungeonAtlas.Draw(g, "torch", New RectangleF(cx - w / 2, bottom - h + CSng(Math.Sin(at / 53.0 + d.Column)) * tile * 0.02F, w, h), 0, flicker)
            Next
            ' 11. Darkness, with 12. the collapse tint (alpha 18 + 22 * pulse at 2 Hz) folded into the same blit:
            ' a separate full-board alpha fill alone cost ~8 ms at 2x zoom.
            If run IsNot Nothing Then
                Dim tint = If(col Is Nothing, 0.0F, 18.0F + 22.0F * (0.5F + 0.5F * CSng(Math.Sin(at / 1000.0 * Math.PI * 4))))
                DrawDarkness(g, left, top, tile, maze, run, r0, r1, c0, c1, tint)
            End If
        End Sub

        ' A translucent overlay on a floor tile is composed once into a 16x16 bitmap; per-frame alpha draws
        ' (a fresh ImageAttributes each) were the main cost of the collapse frames.
        Private Shared ReadOnly _composites As New Dictionary(Of String, Bitmap)()

        Private Shared Sub DrawComposite(g As Graphics, key As String, floorName As String, overlay As String, alpha As Single, rect As RectangleF)
            Dim bmp As Bitmap = Nothing
            If Not _composites.TryGetValue(key, bmp) Then
                bmp = New Bitmap(16, 16, Imaging.PixelFormat.Format32bppPArgb)
                Using cg = Graphics.FromImage(bmp)
                    If Not DungeonAtlas.Draw(cg, floorName, New RectangleF(0, 0, 16, 16)) Then bmp.Dispose() : bmp = Nothing
                    If bmp IsNot Nothing Then DungeonAtlas.Draw(cg, overlay, New RectangleF(0, 0, 16, 16), 0, alpha)
                End Using
                _composites(key) = bmp
            End If
            If bmp Is Nothing Then Return
            Dim mode = g.InterpolationMode
            Dim offset = g.PixelOffsetMode
            g.InterpolationMode = InterpolationMode.NearestNeighbor
            g.PixelOffsetMode = PixelOffsetMode.Half
            g.DrawImage(bmp, rect, New RectangleF(0, 0, 16, 16), GraphicsUnit.Pixel)
            g.InterpolationMode = mode
            g.PixelOffsetMode = offset
        End Sub

        Private Shared Sub DrawDust(g As Graphics, rect As RectangleF, r As Integer, c As Integer, at As Long)
            For i As Integer = 0 To 5
                Dim h = CInt(((r * 73856093L) Xor (c * 19349663L) Xor (i * 83492791L)) And &H7FFFFFFFL)
                Dim phase = ((at / 700.0 + (h Mod 997) / 997.0) Mod 1.0)
                Dim x = rect.Left + (h \ 997 Mod 100) / 100.0F * rect.Width
                Dim s = Math.Max(1.5F, rect.Width * 0.1F)
                Using b As New SolidBrush(Color.FromArgb(CInt(235 * (1 - phase)), 215, 190, 150))
                    g.FillRectangle(b, x, rect.Top + CSng(phase) * rect.Height, s, s)
                End Using
            Next
        End Sub

        Private Shared Sub DrawPedestal(g As Graphics, cx As Single, bottom As Single, height As Single)
            Dim size = DungeonAtlas.SizeOf("pedestal")
            Dim w = height * size.Width / size.Height
            DungeonAtlas.Draw(g, "pedestal", New RectangleF(cx - w / 2, bottom - height, w, height))
        End Sub

        Private Shared Sub DrawKey(g As Graphics, cx As Single, cy As Single, tile As Single, tint As Color, at As Long)
            cy += CSng(Math.Sin(at / 320.0)) * tile * 0.06F
            DrawGlow(g, cx, cy, tile * 0.75F, Color.FromArgb(110, tint))
            Dim s = tile * 0.6F
            DungeonAtlas.Draw(g, "key", New RectangleF(cx - s / 2, cy - s / 2, s, s))
        End Sub

        Private Shared Sub DrawFlame(g As Graphics, cx As Single, y As Single, tile As Single, at As Long, lit As Boolean)
            If Not lit Then
                Using b As New SolidBrush(Color.FromArgb(150, 120, 120, 128))
                    g.FillEllipse(b, cx - tile * 0.08F, y - tile * 0.06F, tile * 0.16F, tile * 0.1F)
                End Using
                Return
            End If
            Dim f = 0.8F + 0.2F * CSng(Math.Sin(at / 70.0))
            DrawGlow(g, cx, y - tile * 0.15F, tile * 1.1F * f, Color.FromArgb(90, 255, 150, 60))
            Using outer As New SolidBrush(Color.FromArgb(235, 255, 140, 40)), inner As New SolidBrush(Color.FromArgb(255, 255, 225, 120))
                g.FillEllipse(outer, cx - tile * 0.14F, y - tile * 0.5F * f, tile * 0.28F, tile * 0.5F * f)
                g.FillEllipse(inner, cx - tile * 0.07F, y - tile * 0.3F * f, tile * 0.14F, tile * 0.3F * f)
            End Using
        End Sub

        ' Radial glows are pre-rendered per (colour, pixel diameter) and blitted 1:1. A PathGradientBrush per torch per
        ' frame, or a scaled bitmap blit, each cost more than the whole rest of the layer.
        Private Shared ReadOnly _glows As New Dictionary(Of Long, Bitmap)()

        Private Shared Sub DrawGlow(g As Graphics, cx As Single, cy As Single, radius As Single, color As Color)
            Dim d = Math.Max(4, CInt(radius / 2.0F) * 4)
            Dim key = (CLng(color.ToArgb()) << 16) Or CLng(d)
            Dim glow As Bitmap = Nothing
            If Not _glows.TryGetValue(key, glow) Then
                If _glows.Count > 96 Then
                    For Each old In _glows.Values : old.Dispose() : Next
                    _glows.Clear()
                End If
                glow = New Bitmap(d, d, Imaging.PixelFormat.Format32bppPArgb)
                Using gg = Graphics.FromImage(glow), path As New GraphicsPath()
                    path.AddEllipse(0, 0, d, d)
                    Using brush As New PathGradientBrush(path)
                        brush.CenterColor = color
                        brush.SurroundColors = {Color.FromArgb(0, color)}
                        gg.FillPath(brush, path)
                    End Using
                End Using
                _glows(key) = glow
            End If
            g.DrawImageUnscaled(glow, CInt(cx - d / 2.0F), CInt(cy - d / 2.0F))
        End Sub

        Private Shared Sub DrawDarts(g As Graphics, left As Single, top As Single, tile As Single, run As DungeonRun)
            Dim sheet = DungeonAtlas.SizeOf("arrow")
            Using glow As New Pen(Color.FromArgb(120, 255, 125, 60), Math.Max(2, tile * 0.15F)),
                  core As New Pen(Color.FromArgb(255, 236, 159), Math.Max(1, tile * 0.045F))
                For Each dart In run.Darts
                    Dim x = left + dart.X * tile, y = top + dart.Y * tile
                    If sheet.Width > 0 Then
                        Dim state = g.Save()
                        g.TranslateTransform(x, y)
                        g.RotateTransform(CSng(Math.Atan2(dart.DY, dart.DX) * 180.0 / Math.PI))
                        DungeonAtlas.Draw(g, "arrow", New RectangleF(-tile * 0.4F, -tile * 0.18F, tile * 0.8F, tile * 0.36F))
                        g.Restore(state)
                    Else
                        Dim a As New PointF(x - dart.DX * tile * 0.34F, y - dart.DY * tile * 0.34F)
                        Dim b As New PointF(x + dart.DX * tile * 0.1F, y + dart.DY * tile * 0.1F)
                        g.DrawLine(glow, a, b) : g.DrawLine(core, a, b)
                    End If
                Next
            End Using
        End Sub

        ' Low-res light map: black = dark, white = lit. The colour matrix turns brightness into purple darkness alpha.
        Private Shared Sub DrawDarkness(g As Graphics, left As Single, top As Single, tile As Single, maze As MazeDefinition, run As DungeonRun,
                                         r0 As Integer, r1 As Integer, c0 As Integer, c1 As Integer, tintAlpha As Single)
            Dim w = maze.ColumnCount * LightScale, h = maze.RowCount * LightScale
            If _light Is Nothing OrElse _light.Width <> w OrElse _light.Height <> h Then
                _light?.Dispose()
                _light = New Bitmap(w, h, Imaging.PixelFormat.Format32bppPArgb)
            End If
            Using lg = Graphics.FromImage(_light)
                lg.Clear(Color.Black)
                Dim at = run.ElapsedMs
                For Each l In maze.Dungeon.Lights
                    AddLight(lg, l.Column + 0.5F, l.Row + 0.5F, l.Radius * (1 + 0.04F * CSng(Math.Sin(at / 170.0 + l.Row))), 1.0F)
                Next
                AddLight(lg, run.MarbleX, run.MarbleY, 4.5F, 1.0F)
                For Each p In _keyTiles
                    Dim bit = If(maze.GetTile(p.Y, p.X) = "K"c, 1, If(maze.GetTile(p.Y, p.X) = "Q"c, 2, 4))
                    If (run.KeyMask And bit) = 0 Then AddLight(lg, p.X + 0.5F, p.Y + 0.5F, 1.5F, 0.8F)
                Next
                For Each p In _lavaTiles
                    AddLight(lg, p.X + 0.5F, p.Y + 0.5F, 1.2F, 0.8F)
                Next
                For i As Integer = 0 To maze.Dungeon.Checkpoints.Count - 1
                    If run.CheckpointLit(i) Then AddLight(lg, maze.Dungeon.Checkpoints(i).Column + 0.5F, maze.Dungeon.Checkpoints(i).Row + 0.5F, 2.5F, 0.8F)
                Next
            End Using
            ' Bake brightness -> purple darkness alpha while bilinear-upsampling the small light map by Smooth on the CPU,
            ' then stretch with nearest-neighbour. GDI+ bilinear over a 1600x900 destination costs 20-35 ms, nearest ~5.
            Dim sw = w * Smooth, sh = h * Smooth
            If _smooth Is Nothing OrElse _smooth.Width <> sw OrElse _smooth.Height <> sh Then
                _smooth?.Dispose()
                _smooth = New Bitmap(sw, sh, Imaging.PixelFormat.Format32bppPArgb)
            End If
            Dim src = _light.LockBits(New Rectangle(0, 0, w, h), Imaging.ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppPArgb)
            If _inBytes Is Nothing OrElse _inBytes.Length <> src.Stride * h Then _inBytes = New Byte(src.Stride * h - 1) {}
            Dim inBytes = _inBytes
            Runtime.InteropServices.Marshal.Copy(src.Scan0, inBytes, 0, inBytes.Length)
            Dim srcStride = src.Stride
            _light.UnlockBits(src)
            Dim k = LightScale * Smooth
            Dim xs = Math.Max(0, c0 * k), xe = Math.Min(sw - 1, (c1 + 1) * k - 1)
            Dim ys = Math.Max(0, r0 * k), ye = Math.Min(sh - 1, (r1 + 1) * k - 1)
            Dim dst = _smooth.LockBits(New Rectangle(0, 0, sw, sh), Imaging.ImageLockMode.WriteOnly, Imaging.PixelFormat.Format32bppPArgb)
            Try
                If _outBytes Is Nothing OrElse _outBytes.Length <> dst.Stride * sh Then _outBytes = New Byte(dst.Stride * sh - 1) {}
                Dim outBytes = _outBytes
                Dim t = tintAlpha / 255.0F, keep = 1.0F - t
                Dim x0(sw - 1) As Integer, x1(sw - 1) As Integer, fx(sw - 1) As Single
                For x As Integer = xs To xe
                    Dim p = (x + 0.5F) / Smooth - 0.5F
                    Dim i0 = CInt(Math.Floor(p))
                    fx(x) = p - i0
                    x0(x) = Math.Min(w - 1, Math.Max(0, i0)) : x1(x) = Math.Min(w - 1, Math.Max(0, i0 + 1))
                Next
                For y As Integer = ys To ye
                    Dim p = (y + 0.5F) / Smooth - 0.5F
                    Dim j0 = CInt(Math.Floor(p))
                    Dim fy = p - j0
                    Dim ra = Math.Min(h - 1, Math.Max(0, j0)) * srcStride, rb = Math.Min(h - 1, Math.Max(0, j0 + 1)) * srcStride
                    Dim row = y * dst.Stride
                    For x As Integer = xs To xe
                        Dim ta = inBytes(ra + x0(x) * 4) * (1 - fx(x)) + inBytes(ra + x1(x) * 4) * fx(x)
                        Dim tb = inBytes(rb + x0(x) * 4) * (1 - fx(x)) + inBytes(rb + x1(x) * 4) * fx(x)
                        Dim a = Darkness * (1.0F - (ta + (tb - ta) * fy) / 255.0F)
                        Dim i = row + x * 4
                        ' premultiplied: the collapse tint (alpha t) is composited over the purple darkness right here
                        outBytes(i) = CByte(15 * a * keep + 10 * t) : outBytes(i + 1) = CByte(5 * a * keep + 10 * t) : outBytes(i + 2) = CByte(8 * a * keep + 120 * t)
                        outBytes(i + 3) = CByte(255 * (a * keep + t))
                    Next
                Next
                Runtime.InteropServices.Marshal.Copy(outBytes, 0, dst.Scan0, outBytes.Length)
            Finally
                _smooth.UnlockBits(dst)
            End Try
            Dim mode = g.InterpolationMode
            Dim offset = g.PixelOffsetMode
            g.InterpolationMode = InterpolationMode.NearestNeighbor
            g.PixelOffsetMode = PixelOffsetMode.Half
            Dim dest As New Rectangle(CInt(left + c0 * tile), CInt(top + r0 * tile), CInt((c1 - c0 + 1) * tile) + 1, CInt((r1 - r0 + 1) * tile) + 1)
            g.DrawImage(_smooth, dest, c0 * k, r0 * k, (c1 - c0 + 1) * k, (r1 - r0 + 1) * k, GraphicsUnit.Pixel)
            g.InterpolationMode = mode
            g.PixelOffsetMode = offset
        End Sub

        Private Shared Sub AddLight(lg As Graphics, x As Single, y As Single, radius As Single, intensity As Single)
            Dim r = radius * LightScale
            Using path As New GraphicsPath()
                path.AddEllipse(x * LightScale - r, y * LightScale - r, r * 2, r * 2)
                Using brush As New PathGradientBrush(path)
                    brush.CenterColor = Color.FromArgb(CInt(255 * intensity), 255, 255, 255)
                    brush.SurroundColors = {Color.FromArgb(0, 255, 255, 255)}
                    lg.FillPath(brush, path)
                End Using
            End Using
        End Sub

        Private Shared Sub DrawSeal(g As Graphics, center As PointF, radius As Single, ember As Boolean, taken As Boolean, at As Long)
            Dim tint = If(ember, Color.FromArgb(255, 177, 73), Color.FromArgb(183, 137, 255))
            Using base As New SolidBrush(Color.FromArgb(37, 30, 49)), pen As New Pen(Color.FromArgb(95, tint), Math.Max(1, radius * 0.12F))
                g.FillEllipse(base, center.X - radius, center.Y - radius * 0.55F, radius * 2, radius * 1.1F)
                g.DrawEllipse(pen, center.X - radius, center.Y - radius * 0.55F, radius * 2, radius * 1.1F)
            End Using
            If taken Then Return
            Dim bob = CSng(Math.Sin(at / 320.0)) * radius * 0.1F
            center.Y += bob
            Dim points As PointF() = {New PointF(center.X, center.Y - radius), New PointF(center.X + radius * 0.7F, center.Y), New PointF(center.X, center.Y + radius), New PointF(center.X - radius * 0.7F, center.Y)}
            Using glow As New SolidBrush(Color.FromArgb(35, tint)), brush As New SolidBrush(tint), edge As New Pen(Color.FromArgb(240, 230, 210), Math.Max(1, radius * 0.08F))
                g.FillEllipse(glow, center.X - radius * 1.5F, center.Y - radius * 1.5F, radius * 3, radius * 3)
                g.FillPolygon(brush, points) : g.DrawPolygon(edge, points)
            End Using
        End Sub

        Private Shared Sub DrawGate(g As Graphics, center As PointF, tile As Single, symbol As Char, opened As Boolean)
            Dim tint = If(symbol = "a"c, Color.FromArgb(255, 177, 73), If(symbol = "b"c, Color.FromArgb(183, 137, 255), Color.FromArgb(220, 195, 136)))
            Using rim As New Pen(Color.FromArgb(125, tint), Math.Max(1, tile * 0.08F))
                g.DrawRectangle(rim, center.X - tile * 0.45F, center.Y - tile * 0.45F, tile * 0.9F, tile * 0.9F)
            End Using
            If opened Then
                Using pen As New Pen(Color.FromArgb(150, tint), Math.Max(1, tile * 0.07F))
                    g.DrawLine(pen, center.X - tile * 0.38F, center.Y - tile * 0.4F, center.X - tile * 0.38F, center.Y + tile * 0.4F)
                    g.DrawLine(pen, center.X + tile * 0.38F, center.Y - tile * 0.4F, center.X + tile * 0.38F, center.Y + tile * 0.4F)
                End Using
            Else
                Using shadow As New SolidBrush(Color.FromArgb(120, 5, 4, 10)), bar As New Pen(Color.FromArgb(174, 158, 190), Math.Max(1.5F, tile * 0.10F)), light As New Pen(tint, Math.Max(0.6F, tile * 0.025F))
                    g.FillRectangle(shadow, center.X - tile / 2, center.Y - tile / 2, tile, tile)
                    For i As Integer = -1 To 1
                        Dim x = center.X + i * tile * 0.27F
                        g.DrawLine(bar, x, center.Y - tile * 0.45F, x, center.Y + tile * 0.45F)
                        g.DrawLine(light, x - tile * 0.025F, center.Y - tile * 0.45F, x - tile * 0.025F, center.Y + tile * 0.45F)
                    Next
                    g.DrawLine(bar, center.X - tile * 0.45F, center.Y, center.X + tile * 0.45F, center.Y)
                End Using
            End If
        End Sub

        Private Shared Sub DrawSpikes(g As Graphics, rect As RectangleF, warning As Boolean, active As Boolean, at As Long)
            Using base As New SolidBrush(Color.FromArgb(33, 28, 40)), edge As New Pen(Color.FromArgb(104, 91, 113), Math.Max(0.7F, rect.Width * 0.03F))
                g.FillRectangle(base, rect) : g.DrawRectangle(edge, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2)
            End Using
            If warning OrElse active Then
                Dim alpha = If(active, 80, 35 + CInt(30 * (0.5 + 0.5 * Math.Sin(at / 70.0))))
                Using danger As New SolidBrush(Color.FromArgb(alpha, 255, 139, 70))
                    g.FillRectangle(danger, rect)
                End Using
            End If
            For row As Integer = 0 To 2
                For col As Integer = 0 To 2
                    Dim x = rect.Left + (0.2F + col * 0.3F) * rect.Width
                    Dim y = rect.Top + (0.24F + row * 0.28F) * rect.Height
                    If active Then
                        Dim size = rect.Width * 0.12F
                        Using metal As New SolidBrush(Color.FromArgb(218, 213, 225)), dark As New Pen(Color.FromArgb(75, 62, 89), Math.Max(0.7F, rect.Width * 0.025F))
                            Dim points = {New PointF(x, y - size), New PointF(x + size * 0.65F, y + size), New PointF(x - size * 0.65F, y + size)}
                            g.FillPolygon(metal, points) : g.DrawPolygon(dark, points)
                        End Using
                    Else
                        Using hole As New SolidBrush(If(warning, Color.FromArgb(255, 174, 77), Color.FromArgb(10, 8, 17)))
                            g.FillEllipse(hole, x - rect.Width * 0.07F, y - rect.Width * 0.04F, rect.Width * 0.14F, rect.Width * 0.08F)
                        End Using
                    End If
                Next
            Next
        End Sub

        Private Shared Sub DrawBlade(g As Graphics, center As PointF, radius As Single, at As Long)
            Dim points(23) As PointF
            For i As Integer = 0 To points.Length - 1
                Dim angle = i * Math.PI * 2 / points.Length + at / 130.0
                Dim r = radius * If(i Mod 2 = 0, 1.0F, 0.72F)
                points(i) = New PointF(center.X + CSng(Math.Cos(angle)) * r, center.Y + CSng(Math.Sin(angle)) * r)
            Next
            Using shadow As New SolidBrush(Color.FromArgb(140, 0, 0, 0)), metal As New SolidBrush(Color.FromArgb(188, 181, 195)), edge As New Pen(Color.FromArgb(255, 132, 77), Math.Max(1, radius * 0.10F)), hub As New SolidBrush(Color.FromArgb(55, 44, 68))
                g.FillEllipse(shadow, center.X - radius, center.Y - radius + radius * 0.22F, radius * 2, radius * 2)
                g.FillPolygon(metal, points) : g.DrawPolygon(edge, points)
                g.FillEllipse(hub, center.X - radius * 0.25F, center.Y - radius * 0.25F, radius * 0.5F, radius * 0.5F)
            End Using
        End Sub

        Private Shared Sub DrawLauncher(g As Graphics, left As Single, top As Single, tile As Single, maze As MazeDefinition,
                                        run As DungeonRun, launcher As DartLauncher, at As Long)
            Dim x = left + launcher.X * tile, y = top + launcher.Y * tile
            Dim warning = DungeonRun.Phase(at, launcher.OffsetMs) < DungeonRun.WarningMs
            Dim dx = launcher.DX, dy = launcher.DY
            Using lane As New Pen(Color.FromArgb(If(warning, 150, 45), 255, 145, 79), Math.Max(0.7F, tile * 0.035F))
                lane.DashStyle = DashStyle.Dot
                For i As Integer = 1 To 20
                    Dim tx = launcher.X + dx * i, ty = launcher.Y + dy * i
                    Dim r = CInt(Math.Floor(ty)), c = CInt(Math.Floor(tx))
                    If r < 0 OrElse c < 0 OrElse r >= maze.RowCount OrElse c >= maze.ColumnCount Then Exit For
                    If maze.GetTile(r, c) = "1"c OrElse (run IsNot Nothing AndAlso run.Solid(r, c)) Then Exit For
                    g.DrawLine(lane, x + dx * (i - 1) * tile, y + dy * (i - 1) * tile, left + tx * tile, top + ty * tile)
                Next
            End Using
            Using base As New SolidBrush(Color.FromArgb(20, 16, 28)), rim As New Pen(Color.FromArgb(125, 108, 144), Math.Max(1, tile * 0.08F)), eye As New SolidBrush(If(warning, Color.FromArgb(255, 173, 66), Color.FromArgb(84, 49, 41)))
                g.FillEllipse(base, x - tile * 0.3F, y - tile * 0.3F, tile * 0.6F, tile * 0.6F)
                g.DrawEllipse(rim, x - tile * 0.3F, y - tile * 0.3F, tile * 0.6F, tile * 0.6F)
                g.FillEllipse(eye, x - tile * 0.1F, y - tile * 0.1F, tile * 0.2F, tile * 0.2F)
            End Using
        End Sub
    End Class
End Namespace
