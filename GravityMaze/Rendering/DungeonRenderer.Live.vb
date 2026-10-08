Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports GravityMaze.Levels
Imports GravityMaze.Engine

Namespace Rendering
    Partial Public NotInheritable Class DungeonRenderer
        Public Shared Sub Draw(g As Graphics, left As Single, top As Single, tile As Single,
                               maze As MazeDefinition, run As DungeonRun)
            Dim definition = maze.Dungeon
            If definition Is Nothing Then Return
            Dim at As Long = If(run Is Nothing, 0, run.ElapsedMs)
            For r As Integer = 0 To maze.RowCount - 1
                For c As Integer = 0 To maze.ColumnCount - 1
                    Dim symbol = maze.GetTile(r, c)
                    Dim center As New PointF(left + (c + 0.5F) * tile, top + (r + 0.5F) * tile)
                    If symbol = "K"c OrElse symbol = "Q"c OrElse symbol = "V"c Then
                        Dim taken = run IsNot Nothing AndAlso (run.KeyMask And If(symbol = "K"c, 1, If(symbol = "Q"c, 2, 4))) <> 0
                        DrawSeal(g, center, tile * 0.33F, symbol = "K"c, taken, at)
                    ElseIf symbol = "c"c OrElse symbol = "E"c Then
                        DrawGate(g, center, tile, symbol, run IsNot Nothing AndAlso run.GateOpen(symbol))
                    End If
                Next
            Next
            For Each spike In definition.Spikes
                Dim phase = DungeonRun.Phase(at, spike.OffsetMs)
                Dim warning = phase >= DungeonRun.SpikeWarningStart AndAlso phase < DungeonRun.SpikeActiveStart
                Dim active = phase >= DungeonRun.SpikeActiveStart AndAlso phase < DungeonRun.SpikeActiveEnd
                DrawSpikes(g, New RectangleF(left + spike.Column * tile, top + spike.Row * tile, tile, tile), warning, active, at)
            Next
            For Each blade In definition.Blades
                Dim a As New PointF(left + blade.X1 * tile, top + blade.Y1 * tile)
                Dim b As New PointF(left + blade.X2 * tile, top + blade.Y2 * tile)
                Using rail As New Pen(Color.FromArgb(26, 23, 32), Math.Max(2, tile * 0.16F)),
                      line As New Pen(Color.FromArgb(126, 114, 139), Math.Max(1, tile * 0.035F))
                    g.DrawLine(rail, a, b) : g.DrawLine(line, a, b)
                End Using
                Dim pos = DungeonRun.BladePosition(blade, at)
                DrawBlade(g, New PointF(left + pos.X * tile, top + pos.Y * tile), tile * DungeonRun.BladeRadius, at)
            Next
            For Each launcher In definition.Launchers
                DrawLauncher(g, left, top, tile, maze, run, launcher, at)
            Next
            If run IsNot Nothing Then
                Using glow As New Pen(Color.FromArgb(120, 255, 125, 60), Math.Max(2, tile * 0.15F)),
                      core As New Pen(Color.FromArgb(255, 236, 159), Math.Max(1, tile * 0.045F))
                    For Each dart In run.Darts
                        Dim x = left + dart.X * tile, y = top + dart.Y * tile
                        Dim a As New PointF(x - dart.DX * tile * 0.34F, y - dart.DY * tile * 0.34F)
                        Dim b As New PointF(x + dart.DX * tile * 0.1F, y + dart.DY * tile * 0.1F)
                        g.DrawLine(glow, a, b) : g.DrawLine(core, a, b)
                    Next
                End Using
            End If
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
