Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Collections.Generic
Imports GravityMaze.Levels

Namespace Rendering
    Public NotInheritable Class MazeRenderer
        ' Static board (floor, walls, zones, goal, border) is drawn once per maze/theme/size
        ' and blitted each frame; only the ball and effects are redrawn per tick.
        Private _cache As Bitmap
        Private _cacheMaze As MazeDefinition
        Private _cacheTheme As String
        Private _cacheSize As Size

        ' ballX / ballY are in tile-space (e.g. 1.5 = centre of column 1).
        ' They come from the Game Engine via GameCanvas; no game state lives here.
        Public Sub Draw(graphics As Graphics, bounds As Rectangle,
                        maze As MazeDefinition,
                        ballX As Single, ballY As Single,
                        Optional themeName As String = "Wooden Workshop",
                        Optional impacts As IEnumerable(Of ImpactEffect) = Nothing,
                        Optional goalEffect As GoalCelebrationEffect = Nothing,
                        Optional fx As BallFxState = Nothing)
            If bounds.Width < 32 OrElse bounds.Height < 32 Then Return

            Dim graphicsState As GraphicsState = graphics.Save()
            Try
                graphics.SmoothingMode = SmoothingMode.AntiAlias

                ' Grid coordinates stay independent of screen pixels.
                ' Derive tile size from both the window and the loaded maze dimensions.
                Dim margin As Single = Math.Min(bounds.Width, bounds.Height) * 0.04F
                Dim tileSize As Single = Math.Min(
                    (bounds.Width  - margin * 2.0F) / maze.ColumnCount,
                    (bounds.Height - margin * 2.0F) / maze.RowCount)
                Dim boardWidth  As Single = tileSize * maze.ColumnCount
                Dim boardHeight As Single = tileSize * maze.RowCount
                Dim left As Single = bounds.Left + (bounds.Width  - boardWidth)  / 2.0F
                Dim top  As Single = bounds.Top  + (bounds.Height - boardHeight) / 2.0F
                Dim board As New RectangleF(left, top, boardWidth, boardHeight)

                ' 1-3. Static board layer (cached)
                If _cache Is Nothing OrElse _cacheMaze IsNot maze OrElse
                   _cacheTheme <> themeName OrElse _cacheSize <> bounds.Size Then
                    _cache?.Dispose()
                    _cache = New Bitmap(bounds.Width, bounds.Height)
                    Using cg As Graphics = Graphics.FromImage(_cache)
                        cg.SmoothingMode = SmoothingMode.AntiAlias
                        cg.TranslateTransform(-bounds.Left, -bounds.Top)
                        DrawStaticBoard(cg, bounds, board, tileSize, maze, themeName)
                    End Using
                    _cacheMaze = maze
                    _cacheTheme = themeName
                    _cacheSize = bounds.Size
                End If
                graphics.DrawImageUnscaled(_cache, bounds.Location)

                ' 4. Expanding Goal Celebration Ring Effect (if active)
                If goalEffect IsNot Nothing AndAlso goalEffect.IsActive Then
                    DrawGoalCelebration(graphics, left, top, tileSize, goalEffect, themeName)
                End If

                ' 5. Metallic Silver Marble (same ball across all levels)
                Dim ballCenter As New PointF(left + ballX * tileSize, top + ballY * tileSize)
                Dim ballRadius As Single = tileSize * 0.27F
                If fx IsNot Nothing Then DrawBallFxUnder(graphics, left, top, tileSize, ballRadius, fx, themeName)
                If fx Is Nothing OrElse Not fx.FallActive Then
                    If fx IsNot Nothing AndAlso fx.SpawnActive Then
                        ballRadius *= Math.Max(0.02F, EaseOutBack(fx.SpawnElapsed / BallFxState.SpawnMs))
                    End If
                    DrawBall(graphics, ballCenter, ballRadius, themeName)
                End If
                If fx IsNot Nothing Then DrawFallAndSpawn(graphics, left, top, tileSize, fx, themeName)

                ' 6. Local Wall Impact Feedback Effects
                If impacts IsNot Nothing Then
                    For Each impact As ImpactEffect In impacts
                        DrawImpact(graphics, left, top, tileSize, impact, themeName)
                    Next
                End If
            Finally
                graphics.Restore(graphicsState)
            End Try
        End Sub

        Private Shared Sub DrawStaticBoard(graphics As Graphics, bounds As Rectangle, board As RectangleF,
                                           tileSize As Single, maze As MazeDefinition, themeName As String)

            ' 1. Board Drop Shadow
            DrawBoardShadow(graphics, board, themeName)

            ' 2. Board Flooring & Texture
            DrawFloor(graphics, board, tileSize, maze, themeName)

            ' 3. Maze Tiles (Walls, Zones, Goal)
            For rowIndex As Integer = 0 To maze.RowCount - 1
                For columnIndex As Integer = 0 To maze.ColumnCount - 1
                    Dim tileBounds As New RectangleF(board.Left + columnIndex * tileSize,
                                                     board.Top  + rowIndex    * tileSize,
                                                     tileSize, tileSize)
                    Dim tileChar As Char = maze.GetTile(rowIndex, columnIndex)
                    Select Case tileChar
                        Case "1"c
                            DrawWall(graphics, tileBounds, themeName, rowIndex, columnIndex)
                        Case "G"c
                            DrawGoal(graphics, tileBounds, themeName)
                        Case "H"c
                            DrawHole(graphics, tileBounds)
                        Case "F"c
                            If themeName = "Neon Velocity" Then
                                DrawBoost(graphics, tileBounds, BoostAngle(maze, rowIndex, columnIndex))
                            Else
                                DrawZone(graphics, tileBounds, tileChar, themeName)
                            End If
                        Case "I"c, "M"c
                            DrawZone(graphics, tileBounds, tileChar, themeName)
                    End Select
                Next
            Next

            ' 4. Board Border and entrance/exit arrows
            If themeName = "Neon Velocity" Then
                DrawNeonEdges(graphics, board, tileSize, maze)
            End If
            DrawBoardBorder(graphics, board.Left, board.Top, board.Width, board.Height, tileSize, themeName, maze)
            DrawEdgeArrow(graphics, board, tileSize, maze.StartRow, maze.StartColumn, maze, themeName)
            DrawEdgeArrow(graphics, board, tileSize, maze.GoalRow, maze.GoalColumn, maze, themeName)
        End Sub

        ' Arrow in the margin next to an S/G tile that sits on the left or right edge.
        Private Shared Sub DrawEdgeArrow(graphics As Graphics, board As RectangleF, tileSize As Single,
                                         row As Integer, col As Integer, maze As MazeDefinition, theme As String)
            Dim x As Single
            If col = 0 Then
                x = board.Left - tileSize * 0.95F
            ElseIf col = maze.ColumnCount - 1 Then
                x = board.Right + tileSize * 0.15F
            Else
                Return
            End If
            Dim cy As Single = board.Top + (row + 0.5F) * tileSize
            Dim len As Single = tileSize * 0.8F
            Dim half As Single = tileSize * 0.28F
            Dim arrowColor As Color = If(theme = "Frozen Labyrinth", Color.FromArgb(220, 236, 250),
                                      If(theme = "Neon Velocity", Color.FromArgb(0, 240, 255), Color.FromArgb(240, 222, 192)))
            Using pen As New Pen(arrowColor, Math.Max(2.0F, tileSize * 0.09F)),
                  brush As New SolidBrush(arrowColor)
                pen.StartCap = LineCap.Round
                graphics.DrawLine(pen, x, cy, x + len - half, cy)
                graphics.FillPolygon(brush, New PointF() {
                    New PointF(x + len, cy),
                    New PointF(x + len - half * 1.3F, cy - half),
                    New PointF(x + len - half * 1.3F, cy + half)})
            End Using
        End Sub

        ' ── Drop Shadow ─────────────────────────────────────────────────────
        Private Shared Sub DrawBoardShadow(graphics As Graphics, board As RectangleF, theme As String)
            Dim shadowColor As Color
            If theme = "Frozen Labyrinth" Then
                shadowColor = Color.FromArgb(125, 4, 10, 24)
            ElseIf theme = "Neon Velocity" Then
                shadowColor = Color.FromArgb(140, 2, 2, 8)
            Else
                shadowColor = Color.FromArgb(110, 0, 0, 0)
            End If

            Using shadowBrush As New SolidBrush(shadowColor)
                graphics.FillRectangle(shadowBrush, board.Left + 5.0F, board.Top + 7.0F, board.Width, board.Height)
            End Using
        End Sub

        ' ── Flooring & Texture ──────────────────────────────────────────────
        Private Shared Sub DrawFloor(graphics As Graphics, board As RectangleF, tileSize As Single,
                                     maze As MazeDefinition, theme As String)
            If theme = "Frozen Labyrinth" Then
                ' Dark navy channel floor (matches the cobblestone concept art)
                Using floorBrush As New LinearGradientBrush(board,
                                                            Color.FromArgb(22, 34, 64),
                                                            Color.FromArgb(12, 20, 42), 90.0F)
                    graphics.FillRectangle(floorBrush, board)
                End Using
                ' Faint frost specks so the floor isn't a flat void
                Using fleckBrush As New SolidBrush(Color.FromArgb(22, 190, 220, 255))
                    For r As Integer = 0 To maze.RowCount - 1
                        For c As Integer = 0 To maze.ColumnCount - 1
                            Dim h As Integer = TileHash(r, c)
                            Dim fx As Single = board.Left + (c + 0.15F + (h And 7) * 0.09F) * tileSize
                            Dim fy As Single = board.Top + (r + 0.15F + ((h >> 3) And 7) * 0.09F) * tileSize
                            Dim fs As Single = tileSize * 0.05F
                            graphics.FillEllipse(fleckBrush, fx, fy, fs, fs)
                        Next
                    Next
                End Using
            ElseIf theme = "Neon Velocity" Then
                ' Deep dark cyber grid flooring
                Using floorBrush As New LinearGradientBrush(board,
                                                            Color.FromArgb(18, 14, 30),
                                                            Color.FromArgb(10, 8, 20), 90.0F)
                    graphics.FillRectangle(floorBrush, board)
                End Using
                Using gridPen As New Pen(Color.FromArgb(40, 0, 240, 255), Math.Max(0.5F, tileSize * 0.012F))
                    For r As Integer = 1 To maze.RowCount - 1
                        Dim y As Single = board.Top + r * tileSize
                        graphics.DrawLine(gridPen, board.Left, y, board.Right, y)
                    Next
                    For c As Integer = 1 To maze.ColumnCount - 1
                        Dim x As Single = board.Left + c * tileSize
                        graphics.DrawLine(gridPen, x, board.Top, x, board.Bottom)
                    Next
                End Using
            Else
                ' Classic warm wood flooring (Wooden Workshop)
                Using floorBrush As New LinearGradientBrush(board,
                                                            Color.FromArgb(216, 177, 123),
                                                            Color.FromArgb(172, 128, 80), 90.0F)
                    graphics.FillRectangle(floorBrush, board)
                End Using
                Using grainPen As New Pen(Color.FromArgb(26, 90, 51, 22), Math.Max(0.5F, tileSize * 0.012F))
                    For lineIndex As Integer = 1 To maze.RowCount * 5 - 1
                        Dim y As Single = board.Top + lineIndex * tileSize / 5.0F
                        graphics.DrawLine(grainPen, board.Left, y, board.Right, y)
                    Next
                End Using
            End If
        End Sub

        ' ── Walls ───────────────────────────────────────────────────────────
        Private Shared Sub DrawWall(graphics As Graphics, tile As RectangleF, theme As String,
                                    r As Integer, c As Integer)
            If theme = "Frozen Labyrinth" Then
                ' Frosted cobblestones: dark mortar bed, then a 3x3 grid of jittered rounded stones.
                Using mortarBrush As New SolidBrush(Color.FromArgb(58, 68, 88))
                    graphics.FillRectangle(mortarBrush, tile)
                End Using
                Dim cell As Single = tile.Width / 3.0F
                Dim rng As New Random(TileHash(r, c))
                Using outlinePen As New Pen(Color.FromArgb(40, 48, 64), Math.Max(0.8F, cell * 0.08F))
                    For sr As Integer = 0 To 2
                        For sc As Integer = 0 To 2
                            Dim cx As Single = tile.Left + (sc + 0.5F) * cell
                            Dim cy As Single = tile.Top + (sr + 0.5F) * cell
                            Dim pts(5) As PointF
                            For k As Integer = 0 To 5
                                Dim ang As Double = k * Math.PI / 3.0 + rng.NextDouble() * 0.4
                                Dim rad As Single = cell * CSng(0.46 + rng.NextDouble() * 0.12)
                                pts(k) = New PointF(cx + CSng(Math.Cos(ang)) * rad, cy + CSng(Math.Sin(ang)) * rad)
                            Next
                            Dim stoneRect As New RectangleF(cx - cell * 0.6F, cy - cell * 0.6F, cell * 1.2F, cell * 1.2F)
                            Dim shade As Integer = rng.Next(-18, 18)
                            Using stonePath As New GraphicsPath(),
                                  stoneBrush As New LinearGradientBrush(stoneRect,
                                        Color.FromArgb(Clamp(212 + shade), Clamp(224 + shade), Clamp(238 + shade)),
                                        Color.FromArgb(Clamp(118 + shade), Clamp(134 + shade), Clamp(156 + shade)), 60.0F)
                                stonePath.AddClosedCurve(pts, 0.55F)
                                graphics.FillPath(stoneBrush, stonePath)
                                graphics.DrawPath(outlinePen, stonePath)
                            End Using
                        Next
                    Next
                End Using
            ElseIf theme = "Neon Velocity" Then
                ' Solid dark slab; the glowing outline comes from DrawNeonEdges (only faces that touch floor).
                Using wallBrush As New SolidBrush(Color.FromArgb(30, 22, 54))
                    graphics.FillRectangle(wallBrush, tile)
                End Using
            Else
                ' Classic Wooden Workshop wall
                Using wallBrush As New LinearGradientBrush(tile,
                                                            Color.FromArgb(116, 75, 43),
                                                            Color.FromArgb(75, 45, 28), 90.0F)
                    graphics.FillRectangle(wallBrush, tile)
                End Using
                Using highlightPen As New Pen(Color.FromArgb(155, 113, 70), Math.Max(1.0F, tile.Width * 0.035F)),
                      shadePen     As New Pen(Color.FromArgb(52, 33, 23),   Math.Max(1.0F, tile.Width * 0.05F))
                    graphics.DrawLine(highlightPen, tile.Left + 1.0F, tile.Top + 1.0F, tile.Right - 1.0F, tile.Top + 1.0F)
                    graphics.DrawLine(shadePen,     tile.Left,        tile.Bottom - 1.0F, tile.Right, tile.Bottom - 1.0F)
                End Using
            End If
        End Sub

        ' ── Neon Wall Edges ─────────────────────────────────────────────────
        ' Glowing tube along every wall face that touches floor, so corridors read as lit channels.
        Private Shared Sub DrawNeonEdges(graphics As Graphics, board As RectangleF, tileSize As Single,
                                         maze As MazeDefinition)
            Dim segments As New List(Of RectangleF)() ' X1,Y1 in Location; X2,Y2 packed in Size
            For r As Integer = 0 To maze.RowCount - 1
                For c As Integer = 0 To maze.ColumnCount - 1
                    If maze.GetTile(r, c) <> "1"c Then Continue For
                    Dim x0 As Single = board.Left + c * tileSize
                    Dim y0 As Single = board.Top + r * tileSize
                    Dim x1 As Single = x0 + tileSize
                    Dim y1 As Single = y0 + tileSize
                    If IsOpen(maze, r - 1, c) Then segments.Add(New RectangleF(x0, y0, x1, y0))
                    If IsOpen(maze, r + 1, c) Then segments.Add(New RectangleF(x0, y1, x1, y1))
                    If IsOpen(maze, r, c - 1) Then segments.Add(New RectangleF(x0, y0, x0, y1))
                    If IsOpen(maze, r, c + 1) Then segments.Add(New RectangleF(x1, y0, x1, y1))
                Next
            Next

            ' Three passes (wide haze, mid glow, hot core) so overlapping segments blend cleanly.
            Dim passes As (Color, Single)() = {
                (Color.FromArgb(38, 255, 40, 200), tileSize * 0.34F),
                (Color.FromArgb(110, 255, 60, 210), tileSize * 0.14F),
                (Color.FromArgb(255, 255, 170, 240), Math.Max(1.2F, tileSize * 0.045F))}
            For Each pass In passes
                Using pen As New Pen(pass.Item1, pass.Item2)
                    pen.StartCap = LineCap.Round
                    pen.EndCap = LineCap.Round
                    For Each s As RectangleF In segments
                        graphics.DrawLine(pen, s.X, s.Y, s.Width, s.Height)
                    Next
                End Using
            Next
        End Sub

        Private Shared Function IsOpen(maze As MazeDefinition, r As Integer, c As Integer) As Boolean
            If r < 0 OrElse c < 0 OrElse r >= maze.RowCount OrElse c >= maze.ColumnCount Then Return False
            Return maze.GetTile(r, c) <> "1"c
        End Function

        ' ── Holes ───────────────────────────────────────────────────────────
        ' Visual radius matches the engine's fall radius (0.40 tile) so what you see is what drops you.
        Private Shared Sub DrawHole(graphics As Graphics, tile As RectangleF)
            Dim cx As Single = tile.Left + tile.Width / 2.0F
            Dim cy As Single = tile.Top + tile.Height / 2.0F
            Dim rad As Single = tile.Width * 0.42F
            Dim pit As New RectangleF(cx - rad, cy - rad, rad * 2.0F, rad * 2.0F)

            Using glowPen As New Pen(Color.FromArgb(70, 255, 60, 90), tile.Width * 0.14F)
                graphics.DrawEllipse(glowPen, pit)
            End Using
            Using pitPath As New GraphicsPath()
                pitPath.AddEllipse(pit)
                Using pitBrush As New PathGradientBrush(pitPath)
                    pitBrush.CenterPoint = New PointF(cx, cy + rad * 0.15F)
                    pitBrush.CenterColor = Color.Black
                    pitBrush.SurroundColors = New Color() {Color.FromArgb(70, 12, 30)}
                    graphics.FillEllipse(pitBrush, pit)
                End Using
            End Using
            ' Depth rings sinking toward the bottom of the pit
            Using ringPen As New Pen(Color.FromArgb(60, 255, 80, 120), Math.Max(0.8F, tile.Width * 0.02F))
                For i As Integer = 1 To 2
                    Dim rr As Single = rad * (1.0F - i * 0.3F)
                    graphics.DrawEllipse(ringPen, cx - rr, cy - rr + rad * 0.08F * i, rr * 2.0F, rr * 2.0F)
                Next
            End Using
            Using rimPen As New Pen(Color.FromArgb(255, 90, 120), Math.Max(1.5F, tile.Width * 0.05F))
                graphics.DrawEllipse(rimPen, pit)
            End Using
        End Sub

        ' ── Boost Strips (Neon) ─────────────────────────────────────────────
        ' Chevrons point toward the strip end that has a pit waiting past the corner (the danger end).
        Private Shared Function BoostAngle(maze As MazeDefinition, r As Integer, c As Integer) As Single
            ' 2-wide strips have F neighbours on both axes, so the longer run decides the direction.
            Dim horizontal As Boolean = RunLength(maze, r, c, 0, 1) + RunLength(maze, r, c, 0, -1) >=
                                        RunLength(maze, r, c, 1, 0) + RunLength(maze, r, c, -1, 0)
            Dim dr As Integer = If(horizontal, 0, 1)
            Dim dc As Integer = If(horizontal, 1, 0)
            If Not PitAhead(maze, r, c, dr, dc) AndAlso PitAhead(maze, r, c, -dr, -dc) Then
                dr = -dr
                dc = -dc
            End If
            Return CSng(Math.Atan2(dr, dc) * 180.0 / Math.PI)
        End Function

        Private Shared Function RunLength(maze As MazeDefinition, r As Integer, c As Integer, dr As Integer, dc As Integer) As Integer
            Dim n As Integer = 0
            r += dr : c += dc
            While r >= 0 AndAlso c >= 0 AndAlso r < maze.RowCount AndAlso c < maze.ColumnCount AndAlso maze.GetTile(r, c) = "F"c
                n += 1
                r += dr : c += dc
            End While
            Return n
        End Function

        Private Shared Function PitAhead(maze As MazeDefinition, r As Integer, c As Integer, dr As Integer, dc As Integer) As Boolean
            While r >= 0 AndAlso c >= 0 AndAlso r < maze.RowCount AndAlso c < maze.ColumnCount AndAlso maze.GetTile(r, c) = "F"c
                r += dr
                c += dc
            End While
            For k As Integer = 0 To 3
                Dim rr As Integer = r + dr * k
                Dim cc As Integer = c + dc * k
                If rr < 0 OrElse cc < 0 OrElse rr >= maze.RowCount OrElse cc >= maze.ColumnCount Then Return False
                If maze.GetTile(rr, cc) = "H"c Then Return True
            Next
            Return False
        End Function

        Private Shared Sub DrawBoost(graphics As Graphics, tile As RectangleF, angle As Single)
            Using baseBrush As New LinearGradientBrush(tile, Color.FromArgb(70, 40, 8), Color.FromArgb(40, 20, 6), angle)
                graphics.FillRectangle(baseBrush, tile)
            End Using
            Dim state As GraphicsState = graphics.Save()
            graphics.TranslateTransform(tile.Left + tile.Width / 2.0F, tile.Top + tile.Height / 2.0F)
            graphics.RotateTransform(angle)
            Dim s As Single = tile.Width
            Using glowPen As New Pen(Color.FromArgb(90, 255, 170, 30), s * 0.16F),
                  corePen As New Pen(Color.FromArgb(255, 220, 90), Math.Max(1.5F, s * 0.06F))
                For Each p As Pen In {glowPen, corePen}
                    p.LineJoin = LineJoin.Round
                    p.StartCap = LineCap.Round
                    p.EndCap = LineCap.Round
                    For Each ox As Single In {-0.2F, 0.12F}
                        graphics.DrawLines(p, New PointF() {
                            New PointF(s * (ox - 0.1F), -s * 0.24F),
                            New PointF(s * (ox + 0.12F), 0.0F),
                            New PointF(s * (ox - 0.1F), s * 0.24F)})
                    Next
                Next
            End Using
            graphics.Restore(state)
        End Sub

        ' ── Zones (Ice, Fast, Mud) ──────────────────────────────────────────
        Private Shared Sub DrawZone(graphics As Graphics, tile As RectangleF, symbol As Char, theme As String)
            If symbol = "I"c Then
                ' Glossy cyan ice zones with directional glints and fractures
                Using iceBrush As New LinearGradientBrush(tile,
                                                          Color.FromArgb(145, 230, 255),
                                                          Color.FromArgb(68, 182, 238), 135.0F)
                    graphics.FillRectangle(iceBrush, tile)
                End Using

                ' Glossy top sheen
                Using sheenBrush As New SolidBrush(Color.FromArgb(85, 255, 255, 255))
                    graphics.FillRectangle(sheenBrush, tile.Left, tile.Top, tile.Width, tile.Height * 0.35F)
                End Using

                ' Directional glints (sharp diagonal // streaks matching concept art)
                Using glintPen As New Pen(Color.FromArgb(215, 255, 255, 255), Math.Max(1.2F, tile.Width * 0.045F))
                    glintPen.StartCap = LineCap.Round
                    glintPen.EndCap   = LineCap.Round
                    graphics.DrawLine(glintPen, tile.Left + tile.Width * 0.18F, tile.Top + tile.Height * 0.78F,
                                                tile.Left + tile.Width * 0.78F, tile.Top + tile.Height * 0.18F)
                    graphics.DrawLine(glintPen, tile.Left + tile.Width * 0.42F, tile.Top + tile.Height * 0.90F,
                                                tile.Left + tile.Width * 0.90F, tile.Top + tile.Height * 0.42F)
                End Using

                ' Hairline crystalline fractures
                Using crackPen As New Pen(Color.FromArgb(135, 255, 255, 255), 0.75F)
                    graphics.DrawLine(crackPen, tile.Left + tile.Width * 0.62F, tile.Top + tile.Height * 0.30F,
                                                tile.Left + tile.Width * 0.82F, tile.Top + tile.Height * 0.38F)
                End Using

            ElseIf symbol = "F"c Then
                ' High-speed zone
                Using fastBrush As New LinearGradientBrush(tile,
                                                           Color.FromArgb(245, 195, 65),
                                                           Color.FromArgb(220, 120, 30), 45.0F)
                    graphics.FillRectangle(fastBrush, tile)
                End Using
                Using speedPen As New Pen(Color.FromArgb(220, 255, 255, 255), Math.Max(1.0F, tile.Width * 0.035F))
                    For i As Integer = 1 To 2
                        Dim oy As Single = tile.Top + tile.Height * (i / 3.0F)
                        graphics.DrawLine(speedPen, tile.Left + tile.Width * 0.2F, oy, tile.Right - tile.Width * 0.2F, oy)
                    Next
                End Using
            Else
                Dim zoneColor As Color = Color.FromArgb(150, 105, 79, 45)
                Using zoneBrush As New SolidBrush(zoneColor)
                    graphics.FillRectangle(zoneBrush, tile)
                End Using
            End If
        End Sub

        ' ── Goal ────────────────────────────────────────────────────────────
        Private Shared Sub DrawGoal(graphics As Graphics, tile As RectangleF, theme As String)
            Dim inset As Single = tile.Width * 0.16F
            Dim hole  As New RectangleF(tile.X + inset, tile.Y + inset,
                                        tile.Width  - inset * 2.0F,
                                        tile.Height - inset * 2.0F)

            If theme = "Frozen Labyrinth" Then
                ' Distinguishable green/mint glowing portal
                Dim haloRect As RectangleF = hole
                haloRect.Inflate(tile.Width * 0.08F, tile.Width * 0.08F)

                Using glowPen   As New Pen(Color.FromArgb(70, 45, 215, 140), Math.Max(3.0F, tile.Width * 0.08F)),
                      ringPen   As New Pen(Color.FromArgb(115, 252, 180),   Math.Max(2.0F, tile.Width * 0.05F)),
                      holeBrush As New SolidBrush(Color.FromArgb(8, 36, 22)),
                      coreBrush As New SolidBrush(Color.FromArgb(210, 255, 235))

                    graphics.DrawEllipse(glowPen,   haloRect)
                    graphics.FillEllipse(holeBrush, hole)
                    graphics.DrawEllipse(ringPen,   hole)

                    ' Inner glowing beacon
                    Dim coreSize As Single = tile.Width * 0.18F
                    Dim coreRect As New RectangleF(tile.X + (tile.Width - coreSize) / 2.0F,
                                                   tile.Y + (tile.Height - coreSize) / 2.0F,
                                                   coreSize, coreSize)
                    graphics.FillEllipse(coreBrush, coreRect)
                End Using
            ElseIf theme = "Neon Velocity" Then
                Using glowPen   As New Pen(Color.FromArgb(120, 0, 255, 200), Math.Max(3.0F, tile.Width * 0.07F)),
                      ringPen   As New Pen(Color.FromArgb(0, 255, 220),       Math.Max(2.0F, tile.Width * 0.05F)),
                      holeBrush As New SolidBrush(Color.FromArgb(12, 10, 28))
                    graphics.FillEllipse(holeBrush, hole)
                    graphics.DrawEllipse(glowPen,   hole)
                    graphics.DrawEllipse(ringPen,   hole)
                End Using
            Else
                ' Classic Wooden Workshop goal
                Using holeBrush As New SolidBrush(Color.FromArgb(36, 51, 36)),
                      ringPen   As New Pen(Color.FromArgb(148, 219, 133), Math.Max(2.0F, tile.Width * 0.05F))
                    graphics.FillEllipse(holeBrush, hole)
                    graphics.DrawEllipse(ringPen,   hole)
                End Using
            End If
        End Sub

        ' ── Ball ────────────────────────────────────────────────────────────
        Private Shared Sub DrawBall(graphics As Graphics, center As PointF, radius As Single, theme As String)
            Dim ball As New RectangleF(center.X - radius, center.Y - radius, radius * 2.0F, radius * 2.0F)

            ' Contact shadow under ball
            Dim shadowColor As Color
            If theme = "Frozen Labyrinth" Then
                shadowColor = Color.FromArgb(110, 8, 18, 30)
            Else
                shadowColor = Color.FromArgb(95, 30, 20, 12)
            End If

            Using shadowBrush As New SolidBrush(shadowColor)
                graphics.FillEllipse(shadowBrush, ball.X + radius * 0.18F, ball.Y + radius * 0.25F,
                                     ball.Width, ball.Height)
            End Using

            ' Metallic silver marble
            Using ballPath As New GraphicsPath()
                ballPath.AddEllipse(ball)
                Using metalBrush As New PathGradientBrush(ballPath)
                    metalBrush.CenterPoint    = New PointF(center.X - radius * 0.35F, center.Y - radius * 0.4F)
                    metalBrush.CenterColor    = Color.FromArgb(255, 255, 255)
                    metalBrush.SurroundColors = New Color() {Color.FromArgb(64, 75, 87)}
                    graphics.FillEllipse(metalBrush, ball)
                End Using
            End Using

            Using rimPen     As New Pen(Color.FromArgb(65, 71, 80), Math.Max(1.0F, radius * 0.07F)),
                  shineBrush As New SolidBrush(Color.FromArgb(210, 255, 255, 255))
                graphics.DrawEllipse(rimPen, ball)
                graphics.FillEllipse(shineBrush, center.X - radius * 0.5F, center.Y - radius * 0.55F,
                                     radius * 0.4F, radius * 0.25F)
            End Using
        End Sub

        ' ── Ball FX (trail, streak, particles, hole fall, spawn pulse) ───────
        Private Shared Function EaseOutBack(t As Single) As Single
            t = Math.Max(0.0F, Math.Min(1.0F, t)) - 1.0F
            Return 1.0F + t * t * (2.70158F * t + 1.70158F)
        End Function

        Private Shared Function Lerp(a As Integer, b As Integer, t As Single) As Integer
            Return CInt(a + (b - a) * t)
        End Function

        Private Shared Sub DrawBallFxUnder(graphics As Graphics, left As Single, top As Single,
                                           tileSize As Single, radius As Single, fx As BallFxState,
                                           themeName As String)
            Dim trail As List(Of PointF) = fx.Trail
            Dim n As Integer = trail.Count
            Dim boost As Boolean = fx.BoostMs > 0.0F AndAlso fx.Speed > BallFxState.BoostSpeed * 0.6F
            Dim streak As Boolean = Not boost AndAlso fx.Speed > BallFxState.StreakSpeed
            If n > 1 AndAlso (boost OrElse streak) Then
                Dim fade As Single = If(boost, Math.Min(1.0F, fx.BoostMs / 120.0F), Math.Min(1.0F, 0.35F + (fx.Speed - BallFxState.StreakSpeed) * 5.0F))
                Dim first As Integer = If(boost, 1, Math.Max(1, n - 4))
                Dim stretch As Single = If(boost, 2.6F, 1.8F)
                Dim head As PointF = trail(n - 1)
                Dim streakColor As Color = If(themeName = "Neon Velocity", Color.FromArgb(120, 220, 255),
                                           If(themeName = "Frozen Labyrinth", Color.FromArgb(205, 238, 255), Color.FromArgb(255, 240, 215)))
                Using glowPen As New Pen(Color.White), corePen As New Pen(Color.White)
                    glowPen.StartCap = LineCap.Round
                    glowPen.EndCap = LineCap.Round
                    corePen.StartCap = LineCap.Round
                    corePen.EndCap = LineCap.Round
                    For i As Integer = first To n - 1
                        Dim t As Single = i / CSng(n - 1)
                        Dim a As New PointF(left + (head.X + (trail(i - 1).X - head.X) * stretch) * tileSize, top + (head.Y + (trail(i - 1).Y - head.Y) * stretch) * tileSize)
                        Dim b As New PointF(left + (head.X + (trail(i).X - head.X) * stretch) * tileSize, top + (head.Y + (trail(i).Y - head.Y) * stretch) * tileSize)
                        If boost Then
                            glowPen.Color = Color.FromArgb(ClampAlpha(70.0F * t * fade), 255, Lerp(60, 170, t), Lerp(200, 40, t))
                            corePen.Color = Color.FromArgb(ClampAlpha(210.0F * t * fade), 255, Lerp(90, 200, t), Lerp(210, 60, t))
                            glowPen.Width = radius * (0.8F + 1.8F * t)
                            corePen.Width = radius * (0.25F + 0.9F * t)
                            graphics.DrawLine(glowPen, a, b)
                            graphics.DrawLine(corePen, a, b)
                        Else
                            glowPen.Color = Color.FromArgb(ClampAlpha(55.0F * t * fade), streakColor)
                            glowPen.Width = radius * (0.7F + 0.9F * t)
                            graphics.DrawLine(glowPen, a, b)
                        End If
                    Next
                End Using
            End If

            If fx.Particles.Count > 0 Then
                Using brush As New SolidBrush(Color.White), glow As New SolidBrush(Color.White), edge As New Pen(Color.White, 1.0F)
                    For Each p As FxParticle In fx.Particles
                        Dim life As Single = 1.0F - p.Age / p.Life
                        Dim cx As Single = left + p.X * tileSize
                        Dim cy As Single = top + p.Y * tileSize
                        Dim sz As Single = tileSize * p.Size * (0.35F + 0.65F * life)
                        If p.IsSpark Then
                            glow.Color = Color.FromArgb(ClampAlpha(90.0F * life), 255, 120, 200)
                            brush.Color = Color.FromArgb(ClampAlpha(255.0F * life), 255, 215, 90)
                            graphics.FillEllipse(glow, cx - sz * 1.6F, cy - sz * 1.6F, sz * 3.2F, sz * 3.2F)
                            graphics.FillEllipse(brush, cx - sz * 0.6F, cy - sz * 0.6F, sz * 1.2F, sz * 1.2F)
                        Else
                            brush.Color = Color.FromArgb(ClampAlpha(235.0F * life), If(p.Spin > 1.6F, 235, 170), 240, 255)
                            Dim ca As Single = CSng(Math.Cos(p.Spin + p.Age * 0.004F))
                            Dim sa As Single = CSng(Math.Sin(p.Spin + p.Age * 0.004F))
                            Dim pts() As PointF = {
                                New PointF(cx + ca * sz * 1.3F, cy + sa * sz * 1.3F),
                                New PointF(cx - sa * sz * 0.6F, cy + ca * sz * 0.6F),
                                New PointF(cx - ca * sz * 1.3F, cy - sa * sz * 1.3F),
                                New PointF(cx + sa * sz * 0.6F, cy - ca * sz * 0.6F)}
                            graphics.FillPolygon(brush, pts)
                            edge.Color = Color.FromArgb(ClampAlpha(200.0F * life), 70, 140, 215)
                            graphics.DrawPolygon(edge, pts)
                        End If
                    Next
                End Using
            End If
        End Sub

        Private Shared Sub DrawFallAndSpawn(graphics As Graphics, left As Single, top As Single,
                                            tileSize As Single, fx As BallFxState, themeName As String)
            If fx.FallActive Then
                Dim p As Single = Math.Min(1.0F, fx.FallElapsed / BallFxState.FallMs)
                Dim hc As New PointF(left + fx.HoleX * tileSize, top + fx.HoleY * tileSize)

                ' Collapsing dark ripple at the hole
                Dim ringR As Single = tileSize * (0.62F * (1.0F - p) + 0.12F)
                Dim ringA As Single = CSng(Math.Sin(Math.PI * Math.Min(1.0F, p * 0.9F + 0.1F)))
                Using pen As New Pen(Color.FromArgb(ClampAlpha(190.0F * ringA), 6, 4, 10), Math.Max(1.5F, tileSize * 0.07F))
                    graphics.DrawEllipse(pen, hc.X - ringR, hc.Y - ringR, ringR * 2.0F, ringR * 2.0F)
                End Using
                Using pen As New Pen(Color.FromArgb(ClampAlpha(80.0F * ringA), 6, 4, 10), Math.Max(3.0F, tileSize * 0.16F))
                    graphics.DrawEllipse(pen, hc.X - ringR, hc.Y - ringR, ringR * 2.0F, ringR * 2.0F)
                End Using

                ' Ghost ball spiralling in, shrinking and darkening
                Dim ox As Single = fx.GhostStartX - fx.HoleX
                Dim oy As Single = fx.GhostStartY - fx.HoleY
                Dim ang As Single = p * 7.85F
                Dim k As Single = CSng(Math.Pow(1.0F - p, 1.4))
                Dim gx As Single = hc.X + (ox * CSng(Math.Cos(ang)) - oy * CSng(Math.Sin(ang))) * tileSize * k
                Dim gy As Single = hc.Y + (ox * CSng(Math.Sin(ang)) + oy * CSng(Math.Cos(ang))) * tileSize * k
                Dim gr As Single = tileSize * 0.27F * CSng(Math.Pow(1.0F - p, 0.85))
                If gr >= 1.0F Then
                    DrawBall(graphics, New PointF(gx, gy), gr, themeName)
                    Using dark As New SolidBrush(Color.FromArgb(ClampAlpha(235.0F * p), 4, 3, 8))
                        graphics.FillEllipse(dark, gx - gr, gy - gr, gr * 2.0F, gr * 2.0F)
                    End Using
                End If
            End If

            If fx.SpawnActive Then
                Dim sp As Single = Math.Min(1.0F, fx.SpawnElapsed / BallFxState.SpawnMs)
                Dim c As New PointF(left + fx.SpawnX * tileSize, top + fx.SpawnY * tileSize)
                Dim ring As Color = If(themeName = "Neon Velocity", Color.FromArgb(255, 110, 220),
                                    If(themeName = "Frozen Labyrinth", Color.FromArgb(150, 225, 255), Color.FromArgb(255, 232, 185)))
                Dim e As Single = 1.0F - (1.0F - sp) * (1.0F - sp)
                Dim rr As Single = tileSize * (0.15F + 0.8F * e)
                Using pen As New Pen(Color.FromArgb(ClampAlpha(70.0F * (1.0F - sp)), ring), Math.Max(3.0F, tileSize * 0.2F))
                    graphics.DrawEllipse(pen, c.X - rr, c.Y - rr, rr * 2.0F, rr * 2.0F)
                End Using
                Using pen As New Pen(Color.FromArgb(ClampAlpha(230.0F * (1.0F - sp)), ring), Math.Max(1.5F, tileSize * 0.06F))
                    graphics.DrawEllipse(pen, c.X - rr, c.Y - rr, rr * 2.0F, rr * 2.0F)
                End Using
            End If
        End Sub

        ' ── Wall Impact Visual Effect ────────────────────────────────────────
        Private Shared Sub DrawImpact(graphics As Graphics, left As Single, top As Single,
                                      tileSize As Single, impact As ImpactEffect, themeName As String)
            Dim progress As Single = impact.ElapsedMs / impact.LifetimeMs
            If progress < 0.0F OrElse progress >= 1.0F Then Return

            Dim alpha As Single = 1.0F - progress
            Dim ease As Single = 1.0F - alpha * alpha * alpha ' fast start, slow finish
            ' 1.0 (gentle) .. 1.8 (fast): a soft hit is still clearly visible
            Dim speedScale As Single = Math.Min(1.8F, Math.Max(1.0F, impact.Speed / 0.08F))

            Dim px As Single = left + impact.TileX * tileSize
            Dim py As Single = top  + impact.TileY * tileSize
            Dim nx As Single = impact.NormalX
            Dim ny As Single = impact.NormalY

            ' Theme palette: core flash, glow, spark A, spark B
            Dim glow As Color, sparkA As Color, sparkB As Color
            Select Case themeName
                Case "Frozen Labyrinth"
                    glow = Color.FromArgb(70, 200, 255) : sparkA = Color.FromArgb(255, 255, 255) : sparkB = Color.FromArgb(120, 210, 255)
                Case "Neon Velocity"
                    glow = Color.FromArgb(255, 60, 220) : sparkA = Color.FromArgb(255, 90, 235) : sparkB = Color.FromArgb(70, 240, 255)
                Case Else
                    glow = Color.FromArgb(255, 170, 60) : sparkA = Color.FromArgb(255, 210, 110) : sparkB = Color.FromArgb(200, 140, 80)
            End Select

            Dim state As GraphicsState = graphics.Save()
            Try
                graphics.SmoothingMode = SmoothingMode.AntiAlias

                ' 0. Whole struck wall block pulses with the theme glow
                Dim wallCol As Single = CSng(Math.Floor(impact.TileX - nx * 0.5F))
                Dim wallRow As Single = CSng(Math.Floor(impact.TileY - ny * 0.5F))
                Using blockBrush As New SolidBrush(Color.FromArgb(ClampAlpha(150.0F * alpha * alpha), glow))
                    graphics.FillRectangle(blockBrush, left + wallCol * tileSize, top + wallRow * tileSize, tileSize, tileSize)
                End Using

                ' 1. Struck wall face glow: a line longer than a tile, perpendicular to the normal
                Dim halfLen As Single = tileSize * 0.75F * (0.8F + 0.2F * speedScale)
                Dim tx As Single = -ny
                Dim ty As Single = nx
                Dim wallA As Integer = ClampAlpha(255.0F * alpha)
                Using wide As New Pen(Color.FromArgb(ClampAlpha(90.0F * alpha), glow), tileSize * 0.24F),
                      mid As New Pen(Color.FromArgb(ClampAlpha(190.0F * alpha), glow), tileSize * 0.12F),
                      core As New Pen(Color.FromArgb(wallA, 255, 255, 255), Math.Max(1.5F, tileSize * 0.03F))
                    wide.StartCap = LineCap.Round : wide.EndCap = LineCap.Round
                    mid.StartCap = LineCap.Round : mid.EndCap = LineCap.Round
                    For Each pen As Pen In {wide, mid, core}
                        graphics.DrawLine(pen, px - tx * halfLen, py - ty * halfLen, px + tx * halfLen, py + ty * halfLen)
                    Next
                End Using

                ' 2. Shockwave half-ring on the open side (two staggered rings)
                For ring As Integer = 0 To 1
                    Dim rp As Single = Math.Min(1.0F, progress * (1.0F + 0.4F * (1 - ring)) - 0.08F * ring)
                    If rp > 0.0F Then
                        Dim r As Single = tileSize * (0.3F + 1.1F * rp) * speedScale
                        Dim center As Single = CSng(Math.Atan2(ny, nx) * 180.0 / Math.PI)
                        Using ringPen As New Pen(Color.FromArgb(ClampAlpha(230.0F * (1.0F - rp)), If(ring = 0, Color.White, glow)),
                                                 Math.Max(2.0F, tileSize * 0.09F * (1.0F - rp) * speedScale))
                            graphics.DrawArc(ringPen, px - r, py - r, r * 2.0F, r * 2.0F, center - 85.0F, 170.0F)
                        End Using
                    End If
                Next

                ' 3. Sparks spraying along the normal, decelerating and fading
                Dim baseAngle As Double = Math.Atan2(ny, nx)
                Using sparkBrush As New SolidBrush(sparkA),
                      trailPen As New Pen(sparkA, 1.5F)
                    For i As Integer = 0 To impact.SparkAngles.Length - 1
                        Dim ang As Double = baseAngle + impact.SparkAngles(i)
                        Dim dist As Single = tileSize * (0.3F + 1.3F * speedScale * impact.SparkDistances(i) * ease)
                        Dim prevA As Single = Math.Min(1.0F, alpha + 0.15F)
                        Dim prev As Single = tileSize * (0.3F + 1.3F * speedScale * impact.SparkDistances(i) * (1.0F - prevA * prevA * prevA))
                        Dim dx As Single = CSng(Math.Cos(ang))
                        Dim dy As Single = CSng(Math.Sin(ang))
                        Dim sx As Single = px + dx * dist
                        Dim sy As Single = py + dy * dist
                        Dim c As Color = If(i Mod 2 = 0, sparkA, sparkB)
                        Dim a As Integer = ClampAlpha(255.0F * alpha)
                        trailPen.Color = Color.FromArgb(a \ 2, c)
                        trailPen.Width = Math.Max(1.2F, tileSize * 0.03F * impact.SparkSizes(i))
                        graphics.DrawLine(trailPen, px + dx * Math.Max(0.0F, prev), py + dy * Math.Max(0.0F, prev), sx, sy)
                        sparkBrush.Color = Color.FromArgb(a, c)
                        Dim ss As Single = Math.Max(3.0F, tileSize * 0.10F * impact.SparkSizes(i) * (0.4F + alpha))
                        graphics.FillEllipse(sparkBrush, sx - ss / 2.0F, sy - ss / 2.0F, ss, ss)
                    Next
                End Using

                ' 4. Bright contact flash (strongest in the first ~40% of life)
                Dim flash As Single = Math.Max(0.0F, 1.0F - progress * 2.5F)
                Dim fr As Single = tileSize * (0.45F + 0.35F * speedScale) * (0.5F + 0.5F * flash)
                Using path As New GraphicsPath()
                    path.AddEllipse(px - fr, py - fr, fr * 2.0F, fr * 2.0F)
                    Using pgb As New PathGradientBrush(path)
                        pgb.CenterColor = Color.FromArgb(ClampAlpha(255.0F * (0.25F + 0.75F * flash)), 255, 255, 255)
                        pgb.SurroundColors = New Color() {Color.FromArgb(0, glow)}
                        graphics.FillPath(pgb, path)
                    End Using
                End Using
            Finally
                graphics.Restore(state)
            End Try
        End Sub

        Private Shared Function ClampAlpha(value As Single) As Integer
            Return CInt(Math.Max(0.0F, Math.Min(255.0F, value)))
        End Function

        ' ── Goal Celebration Rings ──────────────────────────────────────────
        Private Shared Sub DrawGoalCelebration(graphics As Graphics, left As Single, top As Single,
                                               tileSize As Single, goalEffect As GoalCelebrationEffect,
                                               theme As String)
            Dim progress As Single = Math.Min(1.0F, goalEffect.ElapsedMs / goalEffect.LifetimeMs)
            Dim cx As Single = left + goalEffect.CenterX * tileSize
            Dim cy As Single = top  + goalEffect.CenterY * tileSize

            ' Wave 1 (primary expanding celebration ring)
            Dim r1 As Single = tileSize * (0.35F + 1.35F * progress)
            Dim a1 As Integer = CInt(Math.Max(0.0F, 220.0F * (1.0F - progress)))

            Using ring1Pen As New Pen(Color.FromArgb(a1, 110, 255, 185), Math.Max(2.0F, tileSize * 0.05F))
                graphics.DrawEllipse(ring1Pen, cx - r1, cy - r1, r1 * 2.0F, r1 * 2.0F)
            End Using

            ' Wave 2 (secondary trailing pulse)
            If progress > 0.2F Then
                Dim p2 As Single = (progress - 0.2F) / 0.8F
                Dim r2 As Single = tileSize * (0.35F + 1.05F * p2)
                Dim a2 As Integer = CInt(Math.Max(0.0F, 180.0F * (1.0F - p2)))
                Using ring2Pen As New Pen(Color.FromArgb(a2, 160, 255, 215), Math.Max(1.5F, tileSize * 0.035F))
                    graphics.DrawEllipse(ring2Pen, cx - r2, cy - r2, r2 * 2.0F, r2 * 2.0F)
                End Using
            End If

            ' Celebration spark particles radiating outward
            Using sparkBrush As New SolidBrush(Color.FromArgb(a1, 210, 255, 235))
                For i As Integer = 0 To 7
                    Dim angle As Double = (i * Math.PI / 4.0) + (progress * 0.5)
                    Dim dist As Single  = tileSize * (0.4F + 0.8F * progress)
                    Dim px As Single    = CSng(cx + Math.Cos(angle) * dist)
                    Dim py As Single    = CSng(cy + Math.Sin(angle) * dist)
                    Dim sz As Single    = Math.Max(1.5F, tileSize * 0.04F * (1.0F - progress))
                    graphics.FillEllipse(sparkBrush, px - sz / 2.0F, py - sz / 2.0F, sz, sz)
                Next
            End Using
        End Sub

        ' ── Board Border ────────────────────────────────────────────────────
        Private Shared Sub DrawBoardBorder(graphics As Graphics, left As Single, top As Single,
                                           boardWidth As Single, boardHeight As Single,
                                           tileSize As Single, theme As String, maze As MazeDefinition)
            If theme = "Frozen Labyrinth" Then
                Return
            ElseIf theme = "Neon Velocity" Then
                ' Cyan frame with gaps at edge openings (entrance/exit).
                Dim right As Single = left + boardWidth
                Dim bottom As Single = top + boardHeight
                Using neonPen As New Pen(Color.FromArgb(0, 240, 255), Math.Max(2.0F, tileSize * 0.045F))
                    graphics.DrawLine(neonPen, left, top, right, top)
                    graphics.DrawLine(neonPen, left, bottom, right, bottom)
                    For Each x As Single In {left, right}
                        Dim col As Integer = If(x = left, 0, maze.ColumnCount - 1)
                        Dim y As Single = top
                        For r As Integer = 0 To maze.RowCount - 1
                            If maze.GetTile(r, col) <> "1"c Then
                                graphics.DrawLine(neonPen, x, y, x, top + r * tileSize)
                                y = top + (r + 1) * tileSize
                            End If
                        Next
                        graphics.DrawLine(neonPen, x, y, x, bottom)
                    Next
                End Using
            Else
                Using borderPen As New Pen(Color.FromArgb(197, 157, 105), Math.Max(1.0F, tileSize * 0.035F))
                    graphics.DrawRectangle(borderPen, left, top, boardWidth, boardHeight)
                End Using
            End If
        End Sub

        ' Stable per-tile hash so procedural textures don't shimmer between redraws.
        Private Shared Function TileHash(r As Integer, c As Integer) As Integer
            ' Long math: the products overflow Integer on grids past ~29 rows.
            Return CInt(((CLng(r) * 73856093L) Xor (CLng(c) * 19349663L)) And &H7FFFFFFFL)
        End Function

        Private Shared Function Clamp(v As Integer) As Integer
            Return Math.Max(0, Math.Min(255, v))
        End Function

        ' ── Corner Decorative Frost (Strictly Non-Playable Margins) ──────────
        Private Shared Sub DrawCornerFrost(graphics As Graphics, bounds As Rectangle, board As RectangleF)
            ' Draw subtle frosted crystal accents in the margins outside the playable board
            Using frostPen As New Pen(Color.FromArgb(30, 150, 210, 255), 1.2F)
                ' Top-left margin
                If board.Left > bounds.Left + 16.0F AndAlso board.Top > bounds.Top + 16.0F Then
                    Dim cx As Single = bounds.Left + 12.0F
                    Dim cy As Single = bounds.Top + 12.0F
                    graphics.DrawLine(frostPen, cx, cy, cx + 24.0F, cy + 24.0F)
                    graphics.DrawLine(frostPen, cx + 10.0F, cy + 10.0F, cx + 6.0F, cy + 16.0F)
                    graphics.DrawLine(frostPen, cx + 14.0F, cy + 14.0F, cx + 20.0F, cy + 8.0F)
                End If

                ' Top-right margin
                If bounds.Right - board.Right > 16.0F AndAlso board.Top > bounds.Top + 16.0F Then
                    Dim cx As Single = bounds.Right - 12.0F
                    Dim cy As Single = bounds.Top + 12.0F
                    graphics.DrawLine(frostPen, cx, cy, cx - 24.0F, cy + 24.0F)
                    graphics.DrawLine(frostPen, cx - 10.0F, cy + 10.0F, cx - 6.0F, cy + 16.0F)
                    graphics.DrawLine(frostPen, cx - 14.0F, cy + 14.0F, cx - 20.0F, cy + 8.0F)
                End If
            End Using
        End Sub
    End Class
End Namespace
