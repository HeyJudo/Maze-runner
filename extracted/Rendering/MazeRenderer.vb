Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports GravityMaze.Levels

Namespace Rendering
    Public NotInheritable Class MazeRenderer
        Public Sub Draw(graphics As Graphics, bounds As Rectangle, maze As MazeDefinition)
            If bounds.Width < 32 OrElse bounds.Height < 32 Then Return

            Dim graphicsState As GraphicsState = graphics.Save()
            Try
                graphics.SmoothingMode = SmoothingMode.AntiAlias
                ' Grid coordinates stay independent of screen pixels.
                ' Derive tile size from both the window and the loaded maze dimensions.
                Dim margin As Single = Math.Min(bounds.Width, bounds.Height) * 0.04F
                Dim tileSize As Single = Math.Min(
                    (bounds.Width - margin * 2.0F) / maze.ColumnCount,
                    (bounds.Height - margin * 2.0F) / maze.RowCount)
                Dim boardWidth As Single = tileSize * maze.ColumnCount
                Dim boardHeight As Single = tileSize * maze.RowCount
                Dim left As Single = bounds.Left + (bounds.Width - boardWidth) / 2.0F
                Dim top As Single = bounds.Top + (bounds.Height - boardHeight) / 2.0F
                Dim board As New RectangleF(left, top, boardWidth, boardHeight)

                Using shadowBrush As New SolidBrush(Color.FromArgb(110, 0, 0, 0))
                    graphics.FillRectangle(shadowBrush, left + 5.0F, top + 7.0F, boardWidth, boardHeight)
                End Using
                Using floorBrush As New LinearGradientBrush(board, Color.FromArgb(216, 177, 123),
                                                            Color.FromArgb(172, 128, 80), 90.0F)
                    graphics.FillRectangle(floorBrush, board)
                End Using
                ' Subtle wood grain drawn in code; no image assets are required.
                Using grainPen As New Pen(Color.FromArgb(26, 90, 51, 22), Math.Max(0.5F, tileSize * 0.012F))
                    For lineIndex As Integer = 1 To maze.RowCount * 5 - 1
                        Dim y As Single = top + lineIndex * tileSize / 5.0F
                        graphics.DrawLine(grainPen, left, y, left + boardWidth, y)
                    Next
                End Using

                For rowIndex As Integer = 0 To maze.RowCount - 1
                    For columnIndex As Integer = 0 To maze.ColumnCount - 1
                        Dim tileBounds As New RectangleF(left + columnIndex * tileSize,
                                                        top + rowIndex * tileSize, tileSize, tileSize)
                        Select Case maze.GetTile(rowIndex, columnIndex)
                            Case "1"c
                                DrawWall(graphics, tileBounds)
                            Case "G"c
                                DrawGoal(graphics, tileBounds)
                            Case "I"c, "M"c, "F"c
                                DrawZone(graphics, tileBounds, maze.GetTile(rowIndex, columnIndex))
                        End Select
                    Next
                Next

                ' Milestone 1 draws the marble at S. Later, a game-state position
                ' will be passed to the renderer by the UI without changing the level.
                Dim ballCenter As New PointF(left + (maze.StartColumn + 0.5F) * tileSize,
                                             top + (maze.StartRow + 0.5F) * tileSize)
                DrawBall(graphics, ballCenter, tileSize * 0.27F)

                Using borderPen As New Pen(Color.FromArgb(197, 157, 105), Math.Max(1.0F, tileSize * 0.035F))
                    graphics.DrawRectangle(borderPen, left, top, boardWidth, boardHeight)
                End Using
            Finally
                graphics.Restore(graphicsState)
            End Try
        End Sub

        Private Shared Sub DrawWall(graphics As Graphics, tile As RectangleF)
            Using wallBrush As New LinearGradientBrush(tile, Color.FromArgb(116, 75, 43),
                                                       Color.FromArgb(75, 45, 28), 90.0F)
                graphics.FillRectangle(wallBrush, tile)
            End Using
            Using highlightPen As New Pen(Color.FromArgb(155, 113, 70), Math.Max(1.0F, tile.Width * 0.035F)),
                  shadePen As New Pen(Color.FromArgb(52, 33, 23), Math.Max(1.0F, tile.Width * 0.05F))
                graphics.DrawLine(highlightPen, tile.Left + 1.0F, tile.Top + 1.0F, tile.Right - 1.0F, tile.Top + 1.0F)
                graphics.DrawLine(shadePen, tile.Left, tile.Bottom - 1.0F, tile.Right, tile.Bottom - 1.0F)
            End Using
        End Sub

        Private Shared Sub DrawGoal(graphics As Graphics, tile As RectangleF)
            Dim inset As Single = tile.Width * 0.18F
            Dim hole As New RectangleF(tile.X + inset, tile.Y + inset,
                                      tile.Width - inset * 2.0F, tile.Height - inset * 2.0F)
            Using holeBrush As New SolidBrush(Color.FromArgb(36, 51, 36)),
                  ringPen As New Pen(Color.FromArgb(148, 219, 133), Math.Max(2.0F, tile.Width * 0.05F))
                graphics.FillEllipse(holeBrush, hole)
                graphics.DrawEllipse(ringPen, hole)
            End Using
        End Sub

        Private Shared Sub DrawBall(graphics As Graphics, center As PointF, radius As Single)
            Dim ball As New RectangleF(center.X - radius, center.Y - radius, radius * 2.0F, radius * 2.0F)
            Using shadowBrush As New SolidBrush(Color.FromArgb(95, 30, 20, 12))
                graphics.FillEllipse(shadowBrush, ball.X + radius * 0.18F, ball.Y + radius * 0.25F,
                                     ball.Width, ball.Height)
            End Using
            Using ballPath As New GraphicsPath()
                ballPath.AddEllipse(ball)
                Using metalBrush As New PathGradientBrush(ballPath)
                    metalBrush.CenterPoint = New PointF(center.X - radius * 0.35F, center.Y - radius * 0.4F)
                    metalBrush.CenterColor = Color.FromArgb(255, 255, 255)
                    metalBrush.SurroundColors = New Color() {Color.FromArgb(64, 75, 87)}
                    graphics.FillEllipse(metalBrush, ball)
                End Using
            End Using
            Using rimPen As New Pen(Color.FromArgb(65, 71, 80), Math.Max(1.0F, radius * 0.07F)),
                  shineBrush As New SolidBrush(Color.FromArgb(210, 255, 255, 255))
                graphics.DrawEllipse(rimPen, ball)
                graphics.FillEllipse(shineBrush, center.X - radius * 0.5F, center.Y - radius * 0.55F,
                                     radius * 0.4F, radius * 0.25F)
            End Using
        End Sub

        Private Shared Sub DrawZone(graphics As Graphics, tile As RectangleF, symbol As Char)
            Dim zoneColor As Color
            Select Case symbol
                Case "I"c
                    zoneColor = Color.FromArgb(150, 155, 223, 247)
                Case "F"c
                    zoneColor = Color.FromArgb(150, 232, 185, 65)
                Case Else
                    zoneColor = Color.FromArgb(150, 105, 79, 45)
            End Select
            Using zoneBrush As New SolidBrush(zoneColor)
                graphics.FillRectangle(zoneBrush, tile)
            End Using
        End Sub
    End Class
End Namespace
