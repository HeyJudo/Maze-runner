Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports GravityMaze.Data
Imports GravityMaze.Levels

Namespace UI.Screens
    ' All screen drawing. Layout is authored for 1080p and scaled by s = height / 1080.
    Partial Public NotInheritable Class GameShell
        Private Shared ReadOnly Danger As Color = Color.FromArgb(255, 84, 96)

        Private Sub Paint(g As Graphics, b As Rectangle)
            If b.Width < 50 OrElse b.Height < 50 Then Return
            UiDraw.Prepare(g)
            Dim s As Single = UiScale()
            Dim pal As ThemePalette = CurrentPalette()

            Select Case _screen
                Case ShellScreen.Title : DrawTitle(g, b, s, pal)
                Case ShellScreen.LevelSelect : DrawLevelSelect(g, b, s, pal)
                Case ShellScreen.Records : DrawRecords(g, b, s, pal)
                Case ShellScreen.HowToPlay : DrawHowToPlay(g, b, s, pal)
                Case ShellScreen.NameEntry : DrawNameEntry(g, b, s, pal)
                Case ShellScreen.Intro : DrawHud(g, b, s, pal) : DrawIntro(g, b, s, pal)
                Case ShellScreen.Playing : DrawHud(g, b, s, pal)
                Case ShellScreen.Paused : DrawHud(g, b, s, pal) : DrawPaused(g, b, s, pal)
                Case ShellScreen.LevelComplete : DrawHud(g, b, s, pal) : DrawLevelComplete(g, b, s, pal)
                Case ShellScreen.TimeUp : DrawHud(g, b, s, pal) : DrawTimeUp(g, b, s, pal)
                Case ShellScreen.Victory : DrawVictory(g, b, s, pal)
            End Select

            ' Short fade-in from black on every screen change (not for pause / resume).
            If _screen <> ShellScreen.Paused AndAlso _screen <> ShellScreen.Playing AndAlso _screenMs < 240 Then
                Using fade As New SolidBrush(Color.FromArgb(CInt(255 * (1.0F - _screenMs / 240.0F)), 0, 0, 0))
                    g.FillRectangle(fade, b)
                End Using
            End If
        End Sub

        ' ── Shared pieces ───────────────────────────────────────────────────
        Private Shared Sub Scrim(g As Graphics, b As Rectangle, pal As ThemePalette, coverage As Single, Optional solid As Integer = 250)
            Dim w As Single = Math.Max(1.0F, b.Width * coverage)
            Using lg As New LinearGradientBrush(New RectangleF(b.X - 1, b.Y, w + 2, b.Height),
                                                Color.FromArgb(solid, pal.Backdrop), Color.FromArgb(0, pal.Backdrop), 0.0F)
                lg.SetBlendTriangularShape(0.0F, 1.0F)
                lg.Blend = New Blend() With {.Positions = {0.0F, 0.55F, 1.0F}, .Factors = {0.0F, 0.25F, 1.0F}}
                g.FillRectangle(lg, b.X, b.Y, w, b.Height)
            End Using
            Using top As New LinearGradientBrush(New Rectangle(b.X, b.Y, b.Width, b.Height \ 4 + 1),
                                                 Color.FromArgb(150, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90.0F),
                  bottom As New LinearGradientBrush(New Rectangle(b.X, b.Bottom - b.Height \ 4 - 1, b.Width, b.Height \ 4 + 1),
                                                    Color.FromArgb(0, 0, 0, 0), Color.FromArgb(170, 0, 0, 0), 90.0F)
                g.FillRectangle(top, b.X, b.Y, b.Width, b.Height \ 4)
                g.FillRectangle(bottom, b.X, b.Bottom - b.Height \ 4, b.Width, b.Height \ 4)
            End Using
        End Sub

        Private Shared Sub Dim_(g As Graphics, b As Rectangle, alpha As Integer)
            Using br As New SolidBrush(Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), 4, 4, 8))
                g.FillRectangle(br, b)
            End Using
        End Sub

        Private Sub DrawLogo(g As Graphics, x As Single, y As Single, s As Single, pal As ThemePalette)
            Dim size As Single = 150 * s
            Dim f As Font = GameFonts.Display(size)
            UiDraw.GlowText(g, "GRAVITY", f, ThemePalette.Brand.Text, pal.AccentSoft, x, y, 0, 4 * s, 5 * s)
            UiDraw.GlowText(g, "MAZE", f, pal.Accent, pal.Accent, x, y + size * 0.86F, 0, 4 * s, 7 * s)
            UiDraw.Text(g, "TILT  ·  ROLL  ·  ESCAPE", GameFonts.Body(22 * s, GameFonts.FontWeight.SemiBold),
                        ThemePalette.Brand.TextDim, x + 6 * s, y + size * 1.98F, 0, 5 * s)
        End Sub

        ' Vertical menu. align 0 = left at x, 0.5 = centred on x.
        Private Sub DrawMenu(g As Graphics, menu As MenuList, x As Single, y As Single, s As Single,
                             pal As ThemePalette, Optional align As Single = 0.0F, Optional sizePx As Single = 34,
                             Optional spacing As Single = 58)
            Dim f As Font = GameFonts.Body(sizePx * s, GameFonts.FontWeight.SemiBold)
            For i As Integer = 0 To menu.Items.Count - 1
                Dim label As String = menu.Items(i)
                Dim iy As Single = y + i * spacing * s
                Dim sel As Boolean = i = menu.Selected
                Dim enabled As Boolean = menu.Enabled(i)
                Dim color As Color = If(sel, pal.Accent, If(enabled, pal.Text, UiDraw.WithAlpha(pal.TextDim, 0.4F)))
                Dim size As SizeF = UiDraw.Measure(g, label, f, 3 * s)
                Dim ix As Single = x - size.Width * align + If(sel AndAlso align = 0, 22 * s, 0)
                If sel Then
                    ' Marker: accent diamond before the item + soft highlight band.
                    Dim my As Single = iy + size.Height * 0.55F
                    Dim mx As Single = ix - 22 * s
                    If align = 0 Then
                        Dim bandRect As New RectangleF(mx - 12 * s, iy - 5 * s, size.Width + 90 * s, size.Height + 10 * s)
                        Using band As New LinearGradientBrush(New RectangleF(bandRect.X - 1, bandRect.Y, bandRect.Width + 2, bandRect.Height),
                                                              Color.FromArgb(60, pal.Accent), Color.FromArgb(0, pal.Accent), 0.0F)
                            band.WrapMode = WrapMode.TileFlipX
                            g.FillRectangle(band, bandRect)
                        End Using
                    Else
                        Dim pill As New RectangleF(ix - 44 * s, iy - 6 * s, size.Width + 88 * s, size.Height + 12 * s)
                        Using path As GraphicsPath = UiDraw.RoundRect(pill, pill.Height / 2), fill As New SolidBrush(Color.FromArgb(45, pal.Accent)),
                              edge As New Pen(Color.FromArgb(110, pal.Accent), 1.2F * s)
                            g.FillPath(fill, path)
                            g.DrawPath(edge, path)
                        End Using
                        mx = pill.X + 22 * s
                    End If
                    Using br As New SolidBrush(pal.Accent)
                        Dim d As Single = 7 * s
                        g.FillPolygon(br, {New PointF(mx, my - d), New PointF(mx + d, my), New PointF(mx, my + d), New PointF(mx - d, my)})
                    End Using
                    UiDraw.GlowText(g, label, f, color, pal.Accent, ix, iy, 0, 3 * s, 3 * s)
                    ' Hold-to-confirm fill (tilt right).
                    If menu.ConfirmProgress > 0 Then
                        Using bg As New SolidBrush(Color.FromArgb(50, pal.Accent)), fg As New SolidBrush(pal.Accent)
                            g.FillRectangle(bg, ix, iy + size.Height + 4 * s, size.Width, 3 * s)
                            g.FillRectangle(fg, ix, iy + size.Height + 4 * s, size.Width * menu.ConfirmProgress, 3 * s)
                        End Using
                    End If
                Else
                    UiDraw.Text(g, label, f, color, ix, iy, 0, 3 * s)
                End If
            Next
        End Sub

        Private Sub DrawFooter(g As Graphics, b As Rectangle, s As Single, pal As ThemePalette,
                               hints As (String, String)(), Optional menu As MenuList = Nothing)
            Dim x As Single = 48 * s
            Dim y As Single = b.Bottom - 58 * s
            For Each h In hints
                x += UiDraw.KeyHint(g, h.Item1, h.Item2, x, y, pal.Accent, pal.TextDim)
            Next
            Dim small As Font = GameFonts.Body(17 * s, GameFonts.FontWeight.Medium)
            UiDraw.Text(g, "BOARD: TILT UP/DOWN TO MOVE  ·  HOLD RIGHT TO SELECT  ·  HOLD LEFT FOR BACK",
                        small, UiDraw.WithAlpha(pal.TextDim, 0.7F), 48 * s, y + 32 * s, 0, 1.5F * s)
            If menu IsNot Nothing AndAlso menu.BackProgress > 0 Then
                Using fg As New SolidBrush(pal.Accent)
                    g.FillRectangle(fg, 48 * s, y - 10 * s, 220 * s * menu.BackProgress, 3 * s)
                End Using
            End If
            ' Right side: player + sound state
            Dim label As Font = GameFonts.Body(16 * s, GameFonts.FontWeight.Bold)
            Dim value As Font = GameFonts.Body(22 * s, GameFonts.FontWeight.SemiBold)
            Dim rx As Single = b.Right - 48 * s
            Dim player As String = If(String.IsNullOrWhiteSpace(_scores.PlayerName), "—", _scores.PlayerName)
            Dim status As String = If(_sound.Muted, "SOUND OFF  [M]", "SOUND ON  [M]") & "   ·   " & BoardStatus()
            UiDraw.Text(g, status, label, If(BoardConnected(), pal.Accent, pal.TextDim), rx, y + 30 * s, 1, 2 * s)
            UiDraw.Text(g, "PLAYER", label, pal.TextDim, rx - UiDraw.Measure(g, player, value, 1 * s).Width - 12 * s, y + 2 * s, 1, 3 * s)
            UiDraw.Text(g, player, value, pal.Text, rx, y - 2 * s, 1, 1 * s)
        End Sub

        Private Shared ReadOnly MenuHints As (String, String)() = {("UP/DOWN", "Navigate"), ("ENTER", "Select"), ("ESC", "Back")}

        ' ── Title ───────────────────────────────────────────────────────────
        Private Sub DrawTitle(g As Graphics, b As Rectangle, s As Single, pal As ThemePalette)
            Scrim(g, b, pal, 0.62F)
            Dim x As Single = b.Width * 0.065F
            Dim intro As Single = UiDraw.EaseOut(_screenMs / 600.0F)
            DrawLogo(g, x - (1 - intro) * 40 * s, b.Height * 0.12F, s, pal)
            DrawMenu(g, _titleMenu, x + 8 * s, b.Height * 0.52F, s, pal)
            DrawFooter(g, b, s, pal, {("UP/DOWN", "Navigate"), ("ENTER", "Select")}, _titleMenu)
        End Sub

        ' ── Level select ────────────────────────────────────────────────────
        Private Sub DrawLevelSelect(g As Graphics, b As Rectangle, s As Single, pal As ThemePalette)
            Scrim(g, b, pal, 0.66F)
            Dim x As Single = b.Width * 0.065F
            UiDraw.Text(g, "CHOOSE YOUR BOARD", GameFonts.Body(22 * s, GameFonts.FontWeight.Bold), pal.Accent, x, b.Height * 0.1F, 0, 6 * s)
            UiDraw.GlowText(g, "LEVEL SELECT", GameFonts.Display(110 * s), pal.Text, pal.AccentSoft, x, b.Height * 0.1F + 34 * s, 0, 3 * s, 4 * s)

            Dim cardW As Single = Math.Min(b.Width * 0.36F, 640 * s)
            Dim cardH As Single = 128 * s
            Dim y As Single = b.Height * 0.33F
            For i As Integer = 0 To _levels.Count - 1
                Dim lv As LevelConfig = _levels(i)
                Dim lp As ThemePalette = ThemePalette.ForTheme(lv.ThemeName)
                Dim sel As Boolean = _levelMenu.Selected = i
                Dim r As New RectangleF(x + If(sel, 16 * s, 0), y + i * (cardH + 18 * s), cardW, cardH)
                UiDraw.Panel(g, r, Color.FromArgb(If(sel, 235, 190), lp.Backdrop), If(sel, lp.Accent, Color.FromArgb(70, lp.TextDim)), 12 * s)
                If sel Then
                    Using bar As New SolidBrush(lp.Accent)
                        g.FillRectangle(bar, r.X, r.Y + 14 * s, 4 * s, r.Height - 28 * s)
                    End Using
                End If
                UiDraw.Text(g, (i + 1).ToString("00"), GameFonts.Display(76 * s), If(sel, lp.Accent, UiDraw.WithAlpha(lp.Accent, 0.55F)), r.X + 26 * s, r.Y + 22 * s)
                UiDraw.Text(g, lv.ThemeName.ToUpperInvariant(), GameFonts.Display(46 * s), lp.Text, r.X + 116 * s, r.Y + 20 * s, 0, 2 * s)
                UiDraw.Text(g, lp.Tagline, GameFonts.Body(20 * s), lp.TextDim, r.X + 118 * s, r.Y + 72 * s)
                Dim best As ScoreRecord = _scores.BestRecordFor(_scores.PlayerName, lv.LevelNumber)
                Dim starCount As Integer = BestStars(lv.LevelNumber)
                For k As Integer = 1 To 3
                    UiDraw.Star(g, r.Right - 118 * s + (k - 1) * 34 * s, r.Y + 38 * s, 13 * s, k <= starCount, lp.Accent)
                Next
                UiDraw.Text(g, If(best Is Nothing, "--:--.--", UiDraw.FormatTime(best.TimeSeconds)), GameFonts.Body(24 * s, GameFonts.FontWeight.SemiBold),
                            If(best Is Nothing, lp.TextDim, lp.Text), r.Right - 24 * s, r.Y + 66 * s, 1, 1 * s)
                If lv.TimeLimitSecs > 0 Then
                    UiDraw.Text(g, $"{lv.TimeLimitSecs}S LIMIT", GameFonts.Body(15 * s, GameFonts.FontWeight.Bold), Danger, r.Right - 24 * s, r.Y + 96 * s, 1, 3 * s)
                End If
            Next
            Dim backSel As Boolean = _levelMenu.Selected = _levels.Count
            Dim by As Single = y + _levels.Count * (cardH + 18 * s) + 14 * s
            UiDraw.Text(g, "BACK", GameFonts.Body(30 * s, GameFonts.FontWeight.SemiBold), If(backSel, pal.Accent, pal.TextDim), x + If(backSel, 16 * s, 0), by, 0, 3 * s)
            DrawFooter(g, b, s, pal, MenuHints, _levelMenu)
        End Sub

        Private Function BestStars(levelNumber As Integer) As Integer
            Dim best As Integer = 0
            For Each r As ScoreRecord In _scores.TopRecords(levelNumber, 1000)
                If String.Equals(r.PlayerName, _scores.PlayerName, StringComparison.OrdinalIgnoreCase) Then best = Math.Max(best, r.Stars)
            Next
            Return best
        End Function

        ' ── Records ─────────────────────────────────────────────────────────
        Private Sub DrawRecords(g As Graphics, b As Rectangle, s As Single, pal As ThemePalette)
            Dim_(g, b, 238)
            Dim x As Single = b.Width * 0.065F
            UiDraw.Text(g, "HALL OF FAME", GameFonts.Body(22 * s, GameFonts.FontWeight.Bold), pal.Accent, x, b.Height * 0.08F, 0, 6 * s)
            UiDraw.GlowText(g, "RECORDS", GameFonts.Display(110 * s), pal.Text, pal.AccentSoft, x, b.Height * 0.08F + 34 * s, 0, 3 * s, 4 * s)

            Dim cols As New List(Of (String, Integer, ThemePalette))()
            For Each lv As LevelConfig In _levels
                cols.Add((lv.ThemeName.ToUpperInvariant(), lv.LevelNumber, ThemePalette.ForTheme(lv.ThemeName)))
            Next
            cols.Add(("FULL RUN", 0, ThemePalette.Brand))
            Dim gap As Single = 24 * s
            Dim colW As Single = (b.Width - x * 2 - gap * (cols.Count - 1)) / cols.Count
            Dim top As Single = b.Height * 0.3F
            For ci As Integer = 0 To cols.Count - 1
                Dim c = cols(ci)
                Dim rows As List(Of ScoreRecord) = _scores.TopRecords(c.Item2, 8)
                Dim r As New RectangleF(x + ci * (colW + gap), top, colW, (110 + Math.Max(5, rows.Count) * 44) * s)
                UiDraw.Panel(g, r, Color.FromArgb(230, c.Item3.Backdrop), Color.FromArgb(90, c.Item3.Accent), 12 * s)
                UiDraw.Text(g, c.Item1, GameFonts.Display(40 * s), c.Item3.Accent, r.X + 24 * s, r.Y + 20 * s, 0, 2 * s)
                If rows.Count = 0 Then
                    UiDraw.Text(g, "No runs yet", GameFonts.Body(22 * s), c.Item3.TextDim, r.X + 24 * s, r.Y + 90 * s)
                End If
                For ri As Integer = 0 To rows.Count - 1
                    Dim ry As Single = r.Y + 84 * s + ri * 44 * s
                    Dim rec As ScoreRecord = rows(ri)
                    Dim mine As Boolean = String.Equals(rec.PlayerName, _scores.PlayerName, StringComparison.OrdinalIgnoreCase)
                    Dim rowColor As Color = If(mine, c.Item3.Text, c.Item3.TextDim)
                    UiDraw.Text(g, (ri + 1).ToString(), GameFonts.Display(30 * s), If(ri = 0, c.Item3.Accent, rowColor), r.X + 24 * s, ry)
                    UiDraw.Text(g, rec.PlayerName, GameFonts.Body(22 * s, GameFonts.FontWeight.SemiBold), rowColor, r.X + 60 * s, ry + 3 * s)
                    UiDraw.Text(g, UiDraw.FormatTime(rec.TimeSeconds), GameFonts.Body(22 * s, GameFonts.FontWeight.Bold), rowColor, r.Right - 24 * s, ry + 3 * s, 1)
                Next
            Next
            DrawFooter(g, b, s, pal, {("ESC", "Back")}, _backMenu)
        End Sub

        ' ── How to play ─────────────────────────────────────────────────────
        Private Sub DrawHowToPlay(g As Graphics, b As Rectangle, s As Single, pal As ThemePalette)
            Dim_(g, b, 238)
            Dim x As Single = b.Width * 0.065F
            UiDraw.Text(g, "THE BASICS", GameFonts.Body(22 * s, GameFonts.FontWeight.Bold), pal.Accent, x, b.Height * 0.08F, 0, 6 * s)
            UiDraw.GlowText(g, "HOW TO PLAY", GameFonts.Display(110 * s), pal.Text, pal.AccentSoft, x, b.Height * 0.08F + 34 * s, 0, 3 * s, 4 * s)

            Dim colW As Single = (b.Width - x * 2 - 40 * s) / 2
            Dim top As Single = b.Height * 0.3F
            Dim left As New RectangleF(x, top, colW, 430 * s)
            Dim right As New RectangleF(x + colW + 40 * s, top, colW, 430 * s)
            UiDraw.Panel(g, left, Color.FromArgb(230, pal.Backdrop), Color.FromArgb(90, pal.Accent), 12 * s)
            UiDraw.Panel(g, right, Color.FromArgb(230, pal.Backdrop), Color.FromArgb(90, pal.Accent), 12 * s)

            Dim head As Font = GameFonts.Display(42 * s)
            Dim body As Font = GameFonts.Body(24 * s, GameFonts.FontWeight.Medium)
            Dim key As Font = GameFonts.Body(22 * s, GameFonts.FontWeight.Bold)
            UiDraw.Text(g, "CONTROLS", head, pal.Accent, left.X + 32 * s, left.Y + 24 * s, 0, 2 * s)
            Dim lines As (String, String)() = {
                ("ARROWS / WASD", "Tilt the board"),
                ("ARDUINO BOARD", "Tilt it for real (plug in any time)"),
                ("C", "Re-center: hold the board level, press C"),
                ("ESC", "Pause"),
                ("R", "Restart the level"),
                ("M", "Sound on / off"),
                ("F11", "Fullscreen / window")}
            For i As Integer = 0 To lines.Length - 1
                Dim ly As Single = left.Y + 96 * s + i * 50 * s
                UiDraw.Text(g, lines(i).Item1, key, pal.Accent, left.X + 32 * s, ly, 0, 2 * s)
                UiDraw.Text(g, lines(i).Item2, body, pal.Text, left.X + colW * 0.45F, ly)
            Next

            UiDraw.Text(g, "THE BOARD", head, pal.Accent, right.X + 32 * s, right.Y + 24 * s, 0, 2 * s)
            Dim items As (String, String)() = {
                ("goal", "Roll into the green portal to escape"),
                ("ice", "Ice: almost no grip. Brake early"),
                ("boost", "Boost: launches you. Pits wait past the turn"),
                ("hole", "Hole: fall in and you restart from the entrance"),
                ("star", "Stars: beat the par time for up to three")}
            For i As Integer = 0 To items.Length - 1
                Dim iy As Single = right.Y + 96 * s + i * 60 * s
                DrawLegendIcon(g, items(i).Item1, right.X + 50 * s, iy + 14 * s, 20 * s, pal)
                UiDraw.Text(g, items(i).Item2, body, pal.Text, right.X + 92 * s, iy)
            Next
            DrawFooter(g, b, s, pal, {("ESC", "Back")}, _backMenu)
        End Sub

        Private Shared Sub DrawLegendIcon(g As Graphics, kind As String, cx As Single, cy As Single, r As Single, pal As ThemePalette)
            Select Case kind
                Case "goal"
                    Using p As New Pen(Color.FromArgb(115, 252, 180), r * 0.22F), br As New SolidBrush(Color.FromArgb(8, 36, 22))
                        g.FillEllipse(br, cx - r, cy - r, r * 2, r * 2)
                        g.DrawEllipse(p, cx - r, cy - r, r * 2, r * 2)
                    End Using
                Case "ice"
                    Using br As New LinearGradientBrush(New RectangleF(cx - r, cy - r, r * 2, r * 2), Color.FromArgb(160, 235, 255), Color.FromArgb(60, 175, 235), 135.0F),
                          p As New Pen(Color.White, r * 0.12F)
                        g.FillRectangle(br, cx - r, cy - r, r * 2, r * 2)
                        g.DrawLine(p, cx - r * 0.5F, cy + r * 0.5F, cx + r * 0.5F, cy - r * 0.5F)
                    End Using
                Case "boost"
                    Using br As New SolidBrush(Color.FromArgb(70, 40, 8)), p As New Pen(Color.FromArgb(255, 220, 90), r * 0.22F)
                        g.FillRectangle(br, cx - r, cy - r, r * 2, r * 2)
                        p.LineJoin = LineJoin.Round
                        g.DrawLines(p, {New PointF(cx - r * 0.4F, cy - r * 0.5F), New PointF(cx + r * 0.2F, cy), New PointF(cx - r * 0.4F, cy + r * 0.5F)})
                    End Using
                Case "hole"
                    Using br As New SolidBrush(Color.Black), p As New Pen(Color.FromArgb(255, 90, 120), r * 0.18F)
                        g.FillEllipse(br, cx - r, cy - r, r * 2, r * 2)
                        g.DrawEllipse(p, cx - r, cy - r, r * 2, r * 2)
                    End Using
                Case Else
                    UiDraw.Star(g, cx, cy, r, True, pal.Accent)
            End Select
        End Sub

        ' ── Name entry ──────────────────────────────────────────────────────
        Private Sub DrawNameEntry(g As Graphics, b As Rectangle, s As Single, pal As ThemePalette)
            Dim_(g, b, 200)
            Dim w As Single = 760 * s
            Dim h As Single = 420 * s
            Dim r As New RectangleF(b.Width / 2.0F - w / 2, b.Height / 2.0F - h / 2, w, h)
            UiDraw.Panel(g, r, pal.Panel, pal.Accent, 16 * s)
            Dim cx As Single = r.X + w / 2
            UiDraw.Text(g, "WHO'S ROLLING?", GameFonts.Body(22 * s, GameFonts.FontWeight.Bold), pal.Accent, cx, r.Y + 40 * s, 0.5F, 6 * s)
            UiDraw.Text(g, "ENTER YOUR NAME", GameFonts.Display(64 * s), pal.Text, cx, r.Y + 74 * s, 0.5F, 2 * s)

            Dim box As New RectangleF(r.X + 60 * s, r.Y + 170 * s, w - 120 * s, 96 * s)
            Using path As GraphicsPath = UiDraw.RoundRect(box, 10 * s),
                  fill As New SolidBrush(Color.FromArgb(120, 0, 0, 0)), pen As New Pen(pal.Accent, 2 * s)
                g.FillPath(fill, path)
                g.DrawPath(pen, path)
            End Using
            Dim nameFont As Font = GameFonts.Display(76 * s)
            Dim nameSize As SizeF = UiDraw.Measure(g, _nameBuffer, nameFont, 6 * s)
            UiDraw.Text(g, _nameBuffer, nameFont, pal.Text, cx, box.Y + 10 * s, 0.5F, 6 * s)
            If (CInt(_screenMs) \ 450) Mod 2 = 0 Then
                Using caret As New SolidBrush(pal.Accent)
                    g.FillRectangle(caret, cx + nameSize.Width / 2 + 8 * s, box.Y + 18 * s, 4 * s, box.Height - 36 * s)
                End Using
            End If
            UiDraw.Text(g, "Type with the keyboard  ·  up to 12 letters", GameFonts.Body(20 * s), pal.TextDim, cx, box.Bottom + 16 * s, 0.5F)
            DrawMenu(g, _nameMenu, cx, box.Bottom + 56 * s, s, pal, 0.5F, 30)
            DrawFooter(g, b, s, pal, {("ENTER", "Confirm"), ("BACKSPACE", "Delete"), ("ESC", "Back")}, _nameMenu)
        End Sub

        ' ── In-game HUD ─────────────────────────────────────────────────────
        Private Sub DrawHud(g As Graphics, b As Rectangle, s As Single, pal As ThemePalette)
            If _engine Is Nothing Then Return
            Dim cfg As LevelConfig = _levels(_levelIndex)
            Using band As New LinearGradientBrush(New Rectangle(b.X, b.Y, b.Width, CInt(130 * s) + 1),
                                                  Color.FromArgb(230, pal.Backdrop), Color.FromArgb(0, pal.Backdrop), 90.0F)
                g.FillRectangle(band, b.X, b.Y, b.Width, 130 * s)
            End Using
            Dim label As Font = GameFonts.Body(17 * s, GameFonts.FontWeight.Bold)
            Dim big As Font = GameFonts.Display(54 * s)

            ' Left: level
            Dim lx As Single = 40 * s
            UiDraw.Text(g, $"LEVEL {cfg.LevelNumber:00}" & If(_campaign, $"  ·  RUN {UiDraw.FormatTime(_campaignTime)}", ""),
                        label, pal.Accent, lx, 24 * s, 0, 4 * s)
            UiDraw.Text(g, cfg.ThemeName.ToUpperInvariant(), big, pal.Text, lx, 44 * s, 0, 2 * s)

            ' Centre: timer
            Dim cx As Single = b.Width / 2.0F
            Dim timed As Boolean = cfg.TimeLimitSecs > 0
            Dim secs As Single = If(timed, _engine.TimeRemainingSeconds, _engine.ElapsedSeconds)
            Dim urgent As Boolean = timed AndAlso secs <= 5.0F AndAlso _screen = ShellScreen.Playing
            Dim pulse As Single = If(urgent, CSng(0.5 + 0.5 * Math.Cos((secs Mod 1.0F) * Math.PI * 2)), 0)
            Dim timeColor As Color = If(urgent, UiDraw.Lerp(pal.Text, Danger, 0.6F + 0.4F * pulse), pal.Text)
            UiDraw.Text(g, If(timed, "TIME LEFT", "TIME"), label, If(urgent, Danger, pal.TextDim), cx, 20 * s, 0.5F, 5 * s)
            Dim timeFont As Font = GameFonts.Display((72 + If(urgent, 8 * pulse, 0)) * s)
            UiDraw.GlowText(g, UiDraw.FormatTime(secs), timeFont, timeColor, If(urgent, Danger, pal.AccentSoft), cx, 40 * s, 0.5F, 2 * s, If(urgent, 6, 3) * s)
            If timed Then
                ' Thin time bar under the clock
                Dim barW As Single = 260 * s
                Dim frac As Single = Math.Max(0, Math.Min(1, secs / cfg.TimeLimitSecs))
                Using bg As New SolidBrush(Color.FromArgb(50, pal.Text)), fg As New SolidBrush(If(urgent, Danger, pal.Accent))
                    g.FillRectangle(bg, cx - barW / 2, 116 * s, barW, 4 * s)
                    g.FillRectangle(fg, cx - barW / 2, 116 * s, barW * frac, 4 * s)
                End Using
            End If

            ' Right: best + attempt
            Dim rx As Single = b.Width - 40 * s
            UiDraw.Text(g, "ATTEMPT", label, pal.TextDim, rx, 24 * s, 1, 4 * s)
            UiDraw.Text(g, _engine.Attempts.ToString(), big, pal.Text, rx, 44 * s, 1)
            Dim best As ScoreRecord = _scores.BestRecordFor(_scores.PlayerName, cfg.LevelNumber)
            Dim bx As Single = rx - 170 * s
            UiDraw.Text(g, "PERSONAL BEST", label, pal.TextDim, bx, 24 * s, 1, 4 * s)
            UiDraw.Text(g, If(best Is Nothing, "--:--.--", UiDraw.FormatTime(best.TimeSeconds)), big,
                        If(best Is Nothing, pal.TextDim, pal.Text), bx, 44 * s, 1, 1 * s)

            ' Bottom hints
            Dim hy As Single = b.Bottom - 44 * s
            Dim x As Single = 40 * s
            For Each h In {("ESC", "Pause"), ("R", "Restart"), ("M", If(_sound.Muted, "Sound off", "Sound on")), ("C", "Re-center board")}
                x += UiDraw.KeyHint(g, h.Item1, h.Item2, x, hy, pal.Accent, pal.TextDim)
            Next
            UiDraw.Text(g, _scores.PlayerName & "   ·   " & BoardStatus(), GameFonts.Body(18 * s, GameFonts.FontWeight.Bold),
                        If(BoardConnected(), pal.Accent, pal.TextDim), b.Width - 40 * s, hy + 2 * s, 1, 2 * s)
        End Sub

        Private Function BoardConnected() As Boolean
            Return _input.Board IsNot Nothing AndAlso _input.Board.IsConnected
        End Function

        Private Function BoardStatus() As String
            Dim board = _input.Board
            If board Is Nothing OrElse Not board.IsConnected Then Return "NO BOARD  ·  KEYBOARD"
            If board.IsCalibrating Then Return "BOARD " & board.ConnectedPort & "  ·  HOLD LEVEL..."
            Return "BOARD " & board.ConnectedPort
        End Function

        ' ── Intro card + countdown ──────────────────────────────────────────
        Private Sub DrawIntro(g As Graphics, b As Rectangle, s As Single, pal As ThemePalette)
            Dim cfg As LevelConfig = _levels(_levelIndex)
            Dim cardMs As Single = If(_skipCard, 0.0F, IntroCardMs)
            Dim cx As Single = b.Width / 2.0F
            If _screenMs < cardMs Then
                Dim t As Single = _screenMs
                Dim a As Single = Math.Min(1.0F, Math.Min(t / 300.0F, (cardMs - t) / 300.0F))
                Dim slide As Single = (1.0F - UiDraw.EaseOut(t / 500.0F)) * 60 * s
                Dim_(g, b, CInt(170 * a))
                Dim y As Single = b.Height * 0.3F
                UiDraw.Text(g, $"LEVEL {cfg.LevelNumber:00}", GameFonts.Body(28 * s, GameFonts.FontWeight.Bold),
                            UiDraw.WithAlpha(pal.Accent, a), cx, y, 0.5F, 12 * s)
                UiDraw.GlowText(g, cfg.ThemeName.ToUpperInvariant(), GameFonts.Display(170 * s), UiDraw.WithAlpha(pal.Text, a),
                                UiDraw.WithAlpha(pal.AccentSoft, a), cx + slide, y + 40 * s, 0.5F, 4 * s, 6 * s)
                Using p As New Pen(UiDraw.WithAlpha(pal.Accent, a), 3 * s)
                    Dim lw As Single = 180 * s * UiDraw.EaseOut(t / 700.0F)
                    g.DrawLine(p, cx - lw, y + 222 * s, cx + lw, y + 222 * s)
                End Using
                UiDraw.Text(g, pal.Tagline, GameFonts.Body(32 * s, GameFonts.FontWeight.Medium), UiDraw.WithAlpha(pal.TextDim, a), cx - slide, y + 246 * s, 0.5F, 1 * s)
                If cfg.TimeLimitSecs > 0 Then
                    UiDraw.Text(g, $"BEAT THE CLOCK  ·  {cfg.TimeLimitSecs} SECONDS", GameFonts.Body(22 * s, GameFonts.FontWeight.Bold),
                                UiDraw.WithAlpha(Danger, a), cx, y + 300 * s, 0.5F, 5 * s)
                End If
                Return
            End If

            ' 3-2-1-GO: each number pops in big and fades.
            Dim ct As Single = _screenMs - cardMs
            Dim stepIndex As Integer = Math.Min(3, CInt(Math.Floor(ct / CountStepMs)))
            Dim local As Single = (ct - stepIndex * CountStepMs) / CountStepMs
            Dim text As String = If(stepIndex = 3, "GO!", (3 - stepIndex).ToString())
            Dim scale As Single = 1.35F - 0.35F * UiDraw.EaseOut(local * 3)
            Dim alpha As Single = If(local > 0.7F, (1 - local) / 0.3F, 1.0F)
            Dim_(g, b, CInt(70 * (1 - ct / (3 * CountStepMs))))
            Dim color As Color = If(stepIndex = 3, pal.Accent, pal.Text)
            Dim f As Font = GameFonts.Display(260 * s * scale)
            Dim h As Single = UiDraw.Measure(g, text, f).Height
            UiDraw.GlowText(g, text, f, UiDraw.WithAlpha(color, alpha), UiDraw.WithAlpha(pal.Accent, alpha), cx, b.Height / 2.0F - h / 2, 0.5F, 4 * s, 10 * s)
        End Sub

        ' ── Pause ───────────────────────────────────────────────────────────
        Private Sub DrawPaused(g As Graphics, b As Rectangle, s As Single, pal As ThemePalette)
            Dim_(g, b, 185)
            Dim cx As Single = b.Width / 2.0F
            UiDraw.GlowText(g, "PAUSED", GameFonts.Display(150 * s), pal.Text, pal.AccentSoft, cx, b.Height * 0.24F, 0.5F, 8 * s, 5 * s)
            DrawMenu(g, _pauseMenu, cx, b.Height * 0.46F, s, pal, 0.5F, 38, 64)
        End Sub

        ' ── Level complete ──────────────────────────────────────────────────
        Private Sub DrawLevelComplete(g As Graphics, b As Rectangle, s As Single, pal As ThemePalette)
            Dim cfg As LevelConfig = _levels(_levelIndex)
            Dim appear As Single = UiDraw.EaseOutBack(_screenMs / 450.0F)
            Dim_(g, b, CInt(165 * Math.Min(1.0F, _screenMs / 300.0F)))
            Dim w As Single = 700 * s
            Dim h As Single = 720 * s
            Dim r As New RectangleF(b.Width / 2.0F - w / 2, b.Height / 2.0F - h / 2 + (1 - appear) * 90 * s, w, h)
            UiDraw.Panel(g, r, pal.Panel, pal.Accent, 18 * s)
            Dim cx As Single = r.X + w / 2

            UiDraw.Text(g, "LEVEL COMPLETE", GameFonts.Body(24 * s, GameFonts.FontWeight.Bold), pal.Accent, cx, r.Y + 36 * s, 0.5F, 8 * s)
            UiDraw.Text(g, cfg.ThemeName.ToUpperInvariant(), GameFonts.Display(62 * s), pal.Text, cx, r.Y + 68 * s, 0.5F, 2 * s)

            ' Stars pop in one by one.
            For i As Integer = 1 To 3
                Dim scale As Single = If(i <= _resultStars, UiDraw.EaseOutBack((_screenMs - StarRevealMs(i)) / 300.0F), 1.0F)
                Dim filled As Boolean = i <= _resultStars AndAlso _screenMs >= StarRevealMs(i)
                Dim radius As Single = If(i = 2, 48, 40) * s
                UiDraw.Star(g, cx + (i - 2) * 124 * s, r.Y + 196 * s - If(i = 2, 10 * s, 0), radius, filled, pal.Accent, If(filled, scale, 1.0F))
            Next

            UiDraw.Text(g, "YOUR TIME", GameFonts.Body(18 * s, GameFonts.FontWeight.Bold), pal.TextDim, cx, r.Y + 272 * s, 0.5F, 6 * s)
            UiDraw.GlowText(g, UiDraw.FormatTime(_resultTime), GameFonts.Display(120 * s), pal.Text, pal.AccentSoft, cx, r.Y + 294 * s, 0.5F, 3 * s, 4 * s)
            If _resultNewBest AndAlso _screenMs > 900 Then
                Dim pulse As Single = CSng(0.85 + 0.15 * Math.Sin(_screenMs / 140.0))
                Dim badge As New RectangleF(cx - 90 * s * pulse, r.Y + 420 * s, 180 * s * pulse, 38 * s * pulse)
                Using path As GraphicsPath = UiDraw.RoundRect(badge, 19 * s), br As New SolidBrush(pal.Accent)
                    g.FillPath(br, path)
                End Using
                UiDraw.Text(g, "NEW BEST", GameFonts.Body(22 * s * pulse, GameFonts.FontWeight.Bold), pal.Backdrop, cx, badge.Y + 5 * s * pulse, 0.5F, 4 * s)
            End If

            ' Stats row
            Dim prevBest As String = If(_resultPrevBest Is Nothing, "--:--.--", UiDraw.FormatTime(_resultPrevBest.TimeSeconds))
            Dim stats As (String, String)() = {("ATTEMPTS", _resultAttempts.ToString()), ("PREVIOUS BEST", prevBest), ("SCORE", _resultScore.ToString("N0"))}
            For i As Integer = 0 To 2
                Dim sx As Single = r.X + w * (i + 0.5F) / 3
                UiDraw.Text(g, stats(i).Item1, GameFonts.Body(15 * s, GameFonts.FontWeight.Bold), pal.TextDim, sx, r.Y + 480 * s, 0.5F, 4 * s)
                UiDraw.Text(g, stats(i).Item2, GameFonts.Display(40 * s), pal.Text, sx, r.Y + 502 * s, 0.5F, 1 * s)
            Next
            Using p As New Pen(Color.FromArgb(50, pal.Text), 1)
                g.DrawLine(p, r.X + 60 * s, r.Y + 562 * s, r.Right - 60 * s, r.Y + 562 * s)
            End Using
            If _screenMs > 1300 Then
                DrawMenu(g, _completeMenu, cx, r.Y + 584 * s, s, pal, 0.5F, 28, 44)
            End If
        End Sub

        ' ── Time up ─────────────────────────────────────────────────────────
        Private Sub DrawTimeUp(g As Graphics, b As Rectangle, s As Single, pal As ThemePalette)
            Dim appear As Single = UiDraw.EaseOutBack(_screenMs / 450.0F)
            Using red As New SolidBrush(Color.FromArgb(CInt(90 * Math.Max(0, 1 - _screenMs / 500.0F)), Danger))
                g.FillRectangle(red, b)
            End Using
            Dim_(g, b, CInt(175 * Math.Min(1.0F, _screenMs / 300.0F)))
            Dim cx As Single = b.Width / 2.0F
            Dim y As Single = b.Height * 0.26F + (1 - appear) * 60 * s
            UiDraw.Panel(g, New RectangleF(cx - 420 * s, y - 50 * s, 840 * s, 500 * s), pal.Panel, Danger, 18 * s)
            UiDraw.Text(g, "OUT OF TIME", GameFonts.Body(24 * s, GameFonts.FontWeight.Bold), Danger, cx, y, 0.5F, 8 * s)
            UiDraw.GlowText(g, "TIME'S UP", GameFonts.Display(170 * s), pal.Text, Danger, cx, y + 36 * s, 0.5F, 6 * s, 6 * s)
            UiDraw.Text(g, $"Attempt {_engine.Attempts}  ·  Tip: brake on the boost, before the corner.", GameFonts.Body(28 * s), pal.TextDim, cx, y + 230 * s, 0.5F)
            If _screenMs > 600 Then DrawMenu(g, _timeUpMenu, cx, y + 300 * s, s, pal, 0.5F, 36, 60)
        End Sub

        ' ── Victory ─────────────────────────────────────────────────────────
        Private Sub DrawVictory(g As Graphics, b As Rectangle, s As Single, pal As ThemePalette)
            Dim brand As ThemePalette = ThemePalette.Brand
            Dim_(g, b, 225)
            ' Drifting embers
            For i As Integer = 0 To 59
                Dim seed As Double = i * 12.9898
                Dim px As Single = CSng((Math.Sin(seed) * 43758.5453 - Math.Floor(Math.Sin(seed) * 43758.5453)) * b.Width)
                Dim speed As Single = 30 + (i Mod 7) * 12
                Dim py As Single = b.Height - ((_screenMs / 1000.0F * speed * s + i * 97) Mod (b.Height + 40))
                Dim a As Integer = 60 + (i Mod 5) * 30
                Using br As New SolidBrush(Color.FromArgb(a, If(i Mod 3 = 0, pal.Accent, brand.Accent)))
                    Dim d As Single = (2 + (i Mod 4)) * s
                    g.FillEllipse(br, px, py, d, d)
                End Using
            Next

            Dim cx As Single = b.Width / 2.0F
            Dim appear As Single = UiDraw.EaseOut(_screenMs / 800.0F)
            Dim y As Single = b.Height * 0.14F + (1 - appear) * 50 * s
            UiDraw.Text(g, "YOU ESCAPED THE MAZE", GameFonts.Body(28 * s, GameFonts.FontWeight.Bold), brand.Accent, cx, y, 0.5F, 12 * s)
            UiDraw.GlowText(g, RankTitle(_campaignStars), GameFonts.Display(190 * s), brand.Text, brand.Accent, cx, y + 44 * s, 0.5F, 6 * s, 8 * s)

            Dim maxStars As Integer = _levels.Count * 3
            For i As Integer = 0 To maxStars - 1
                Dim reveal As Single = 700 + i * 90
                Dim filled As Boolean = i < _campaignStars AndAlso _screenMs >= reveal
                UiDraw.Star(g, cx + (i - (maxStars - 1) / 2.0F) * 64 * s, y + 300 * s, 22 * s, filled, brand.Accent,
                            If(filled, UiDraw.EaseOutBack((_screenMs - reveal) / 250.0F), 1.0F))
            Next

            Dim stats As (String, String)() = {("TOTAL TIME", UiDraw.FormatTime(_campaignTime)),
                                                ("STARS", $"{_campaignStars} / {maxStars}"),
                                                ("ALL-TIME RANK", If(_victoryRank > 0, $"#{_victoryRank}", "--"))}
            For i As Integer = 0 To 2
                Dim sx As Single = cx + (i - 1) * 300 * s
                UiDraw.Text(g, stats(i).Item1, GameFonts.Body(18 * s, GameFonts.FontWeight.Bold), brand.TextDim, sx, y + 360 * s, 0.5F, 5 * s)
                UiDraw.Text(g, stats(i).Item2, GameFonts.Display(64 * s), brand.Text, sx, y + 386 * s, 0.5F, 1 * s)
            Next
            If _screenMs > 1500 Then DrawMenu(g, _victoryMenu, cx, y + 500 * s, s, brand, 0.5F, 34, 56)
        End Sub

        Private Function RankTitle(stars As Integer) As String
            Dim max As Integer = _levels.Count * 3
            If stars >= max Then Return "MAZE MASTER"
            If stars >= max - 3 Then Return "GRAVITY ACE"
            If stars >= _levels.Count + 1 Then Return "SMOOTH ROLLER"
            Return "ESCAPE ARTIST"
        End Function
    End Class
End Namespace
