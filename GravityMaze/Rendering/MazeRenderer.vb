Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Collections.Generic
Imports GravityMaze.Levels

Namespace Rendering
    Public NotInheritable Class MazeRenderer
        ' ballX / ballY are in tile-space (e.g. 1.5 = centre of column 1).
        ' They come from the Game Engine via GameCanvas; no game state lives here.
        Public Sub Draw(graphics As Graphics, bounds As Rectangle,
                        maze As MazeDefinition,
                        ballX As Single, ballY As Single,
                        Optional themeName As String = "Wooden Workshop",
                        Optional impacts As IEnumerable(Of ImpactEffect) = Nothing,
                        Optional goalEffect As GoalCelebrationEffect = Nothing)
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

                ' Optional decorative margin frost (stays completely clear of board)
                If themeName = "Frozen Labyrinth" Then
                    DrawCornerFrost(graphics, bounds, board)
                End If

                ' 1. Board Drop Shadow
                DrawBoardShadow(graphics, board, themeName)

                ' 2. Board Flooring & Texture
                DrawFloor(graphics, board, tileSize, maze, themeName)

                ' 3. Maze Tiles (Walls, Zones, Goal)
                For rowIndex As Integer = 0 To maze.RowCount - 1
                    For columnIndex As Integer = 0 To maze.ColumnCount - 1
                        Dim tileBounds As New RectangleF(left + columnIndex * tileSize,
                                                         top  + rowIndex    * tileSize,
                                                         tileSize, tileSize)
                        Dim tileChar As Char = maze.GetTile(rowIndex, columnIndex)
                        Select Case tileChar
                            Case "1"c
                                DrawWall(graphics, tileBounds, themeName, rowIndex, columnIndex)
                            Case "G"c
                                DrawGoal(graphics, tileBounds, themeName)
                            Case "I"c, "M"c, "F"c
                                DrawZone(graphics, tileBounds, tileChar, themeName)
                        End Select
                    Next
                Next

                ' 4. Expanding Goal Celebration Ring Effect (if active)
                If goalEffect IsNot Nothing AndAlso goalEffect.IsActive Then
                    DrawGoalCelebration(graphics, left, top, tileSize, goalEffect, themeName)
                End If

                ' 5. Metallic Silver Marble (same ball across all levels)
                Dim ballCenter As New PointF(left + ballX * tileSize, top + ballY * tileSize)
                DrawBall(graphics, ballCenter, tileSize * 0.27F, themeName)

                ' 6. Local Wall Impact Feedback Effects
                If impacts IsNot Nothing Then
                    For Each impact As ImpactEffect In impacts
                        DrawImpact(graphics, left, top, tileSize, impact)
                    Next
                End If

                ' 7. Board Border
                DrawBoardBorder(graphics, left, top, boardWidth, boardHeight, tileSize, themeName)
            Finally
                graphics.Restore(graphicsState)
            End Try
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
                ' Matte pale blue-gray slate stone flooring
                Using floorBrush As New LinearGradientBrush(board,
                                                            Color.FromArgb(204, 216, 228),
                                                            Color.FromArgb(174, 190, 206), 90.0F)
                    graphics.FillRectangle(floorBrush, board)
                End Using

                ' Subtle slate tile joint lines to convey grippy, non-slippery stone surface
                Using stonePen As New Pen(Color.FromArgb(32, 110, 140, 170), Math.Max(0.5F, tileSize * 0.015F))
                    For r As Integer = 1 To maze.RowCount - 1
                        Dim y As Single = board.Top + r * tileSize
                        graphics.DrawLine(stonePen, board.Left, y, board.Right, y)
                    Next
                    For c As Integer = 1 To maze.ColumnCount - 1
                        Dim x As Single = board.Left + c * tileSize
                        graphics.DrawLine(stonePen, x, board.Top, x, board.Bottom)
                    Next
                End Using

                ' Subtle stone stipple/mottling flecks across the floor for grippy texture
                Using fleckBrush As New SolidBrush(Color.FromArgb(18, 255, 255, 255))
                    For r As Integer = 0 To maze.RowCount - 1
                        For c As Integer = 0 To maze.ColumnCount - 1
                            Dim fx As Single = board.Left + (c + 0.3F) * tileSize
                            Dim fy As Single = board.Top  + (r + 0.35F) * tileSize
                            graphics.FillRectangle(fleckBrush, fx, fy, tileSize * 0.4F, tileSize * 0.25F)
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
                ' Frosted blue ice/stone blocks with restrained bevel highlights and shadows
                Using wallBrush As New LinearGradientBrush(tile,
                                                            Color.FromArgb(68, 132, 194),
                                                            Color.FromArgb(28, 72, 128), 90.0F)
                    graphics.FillRectangle(wallBrush, tile)
                End Using

                Dim bevelWidth As Single = Math.Max(1.0F, tile.Width * 0.04F)
                Using highlightPen As New Pen(Color.FromArgb(200, 230, 255), bevelWidth),
                      sideHiPen    As New Pen(Color.FromArgb(140, 190, 240), Math.Max(0.8F, bevelWidth * 0.75F)),
                      shadePen     As New Pen(Color.FromArgb(14, 38, 70),   Math.Max(1.2F, bevelWidth * 1.1F)),
                      sideShadePen As New Pen(Color.FromArgb(20, 48, 86),   Math.Max(0.8F, bevelWidth * 0.85F)),
                      facetPen     As New Pen(Color.FromArgb(35, 255, 255, 255), 0.75F)

                    ' Top bevel highlight
                    graphics.DrawLine(highlightPen, tile.Left + 1.0F, tile.Top + 1.0F, tile.Right - 1.0F, tile.Top + 1.0F)
                    ' Left bevel highlight
                    graphics.DrawLine(sideHiPen, tile.Left + 1.0F, tile.Top + 1.0F, tile.Left + 1.0F, tile.Bottom - 1.0F)
                    ' Bottom bevel shadow
                    graphics.DrawLine(shadePen, tile.Left, tile.Bottom - 1.0F, tile.Right, tile.Bottom - 1.0F)
                    ' Right bevel shadow
                    graphics.DrawLine(sideShadePen, tile.Right - 1.0F, tile.Top + 1.0F, tile.Right - 1.0F, tile.Bottom - 1.0F)

                    ' Subtle crystalline block facet
                    graphics.DrawLine(facetPen, tile.Left + tile.Width * 0.2F, tile.Top + tile.Height * 0.25F,
                                                tile.Right - tile.Width * 0.2F, tile.Bottom - tile.Height * 0.25F)
                End Using
            ElseIf theme = "Neon Velocity" Then
                Using wallBrush As New SolidBrush(Color.FromArgb(24, 20, 45))
                    graphics.FillRectangle(wallBrush, tile)
                End Using
                Using neonPen As New Pen(Color.FromArgb(255, 0, 180), Math.Max(1.0F, tile.Width * 0.04F)),
                      glowPen As New Pen(Color.FromArgb(60, 255, 0, 180), Math.Max(2.5F, tile.Width * 0.09F))
                    graphics.DrawRectangle(glowPen, tile.Left, tile.Top, tile.Width, tile.Height)
                    graphics.DrawRectangle(neonPen, tile.Left, tile.Top, tile.Width, tile.Height)
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

                ' Fine ice perimeter highlight
                Using iceBorderPen As New Pen(Color.FromArgb(90, 255, 255, 255), 1.0F)
                    graphics.DrawRectangle(iceBorderPen, tile.Left, tile.Top, tile.Width, tile.Height)
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

        ' ── Wall Impact Visual Effect ────────────────────────────────────────
        Private Shared Sub DrawImpact(graphics As Graphics, left As Single, top As Single,
                                      tileSize As Single, impact As ImpactEffect)
            Dim progress As Single = impact.ElapsedMs / impact.LifetimeMs
            If progress < 0.0F OrElse progress >= 1.0F Then Return

            Dim alpha As Single = 1.0F - progress
            Dim speedScale As Single = Math.Min(1.4F, Math.Max(0.6F, impact.Speed / 0.12F))

            ' Contact point in screen coordinates
            Dim px As Single = left + impact.TileX * tileSize
            Dim py As Single = top  + impact.TileY * tileSize

            ' Expanding arc radius
            Dim radius As Single = (tileSize * 0.10F + tileSize * 0.22F * progress) * speedScale
            Dim arcRect As New RectangleF(px - radius, py - radius, radius * 2.0F, radius * 2.0F)

            ' Determine start angle for arc based on normal vector pointing outward from wall
            ' Normal (-1, 0): wall is to right, arc points left (into open space)
            Dim startAngle As Single
            Dim sweepAngle As Single = 120.0F

            If impact.NormalX < -0.5F Then
                startAngle = 120.0F
            ElseIf impact.NormalX > 0.5F Then
                startAngle = 300.0F
            ElseIf impact.NormalY < -0.5F Then
                startAngle = 210.0F
            Else
                startAngle = 30.0F
            End If

            Dim glowAlpha As Integer = CInt(Math.Max(0.0F, Math.Min(255.0F, 190.0F * alpha)))
            Dim coreAlpha As Integer = CInt(Math.Max(0.0F, Math.Min(255.0F, 240.0F * alpha)))

            ' Outer cyan glow arc
            Using glowPen As New Pen(Color.FromArgb(glowAlpha, 130, 230, 255), Math.Max(2.0F, 3.5F * speedScale)),
                  corePen As New Pen(Color.FromArgb(coreAlpha, 255, 255, 255), Math.Max(1.0F, 1.8F * speedScale)),
                  flashBrush As New SolidBrush(Color.FromArgb(coreAlpha, 255, 255, 255))

                graphics.DrawArc(glowPen, arcRect, startAngle, sweepAngle)
                graphics.DrawArc(corePen, arcRect, startAngle, sweepAngle)

                ' Small contact flash point
                Dim dotSize As Single = Math.Max(2.0F, 4.0F * speedScale * alpha)
                graphics.FillEllipse(flashBrush, px - dotSize / 2.0F, py - dotSize / 2.0F, dotSize, dotSize)

                ' Subtle sparks along the normal
                Dim sparkDist As Single = (tileSize * 0.15F * progress) * speedScale
                Dim sx As Single = px + impact.NormalX * sparkDist
                Dim sy As Single = py + impact.NormalY * sparkDist
                Dim sparkSize As Single = Math.Max(1.5F, 2.5F * alpha)
                graphics.FillEllipse(flashBrush, sx - sparkSize / 2.0F, sy - sparkSize / 2.0F, sparkSize, sparkSize)
            End Using
        End Sub

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
                                           tileSize As Single, theme As String)
            If theme = "Frozen Labyrinth" Then
                Using borderPen As New Pen(Color.FromArgb(120, 185, 238), Math.Max(2.0F, tileSize * 0.045F)),
                      innerPen  As New Pen(Color.FromArgb(42, 90, 150),   Math.Max(1.0F, tileSize * 0.02F))
                    graphics.DrawRectangle(borderPen, left, top, boardWidth, boardHeight)
                    graphics.DrawRectangle(innerPen, left + 1.5F, top + 1.5F, boardWidth - 3.0F, boardHeight - 3.0F)
                End Using
            ElseIf theme = "Neon Velocity" Then
                Using neonPen As New Pen(Color.FromArgb(0, 240, 255), Math.Max(2.0F, tileSize * 0.045F))
                    graphics.DrawRectangle(neonPen, left, top, boardWidth, boardHeight)
                End Using
            Else
                Using borderPen As New Pen(Color.FromArgb(197, 157, 105), Math.Max(1.0F, tileSize * 0.035F))
                    graphics.DrawRectangle(borderPen, left, top, boardWidth, boardHeight)
                End Using
            End If
        End Sub

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
