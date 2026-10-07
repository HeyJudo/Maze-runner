Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports GravityMaze.Levels
Imports GravityMaze.Engine

Namespace Rendering
    ' Top-down stonework and readable trap states; all gameplay comes from DungeonRun.
    Partial Public NotInheritable Class DungeonRenderer
        Private Sub New()
        End Sub

        Public Shared Sub Floor(g As Graphics, board As RectangleF, tile As Single, maze As MazeDefinition)
            Using base As New SolidBrush(Color.FromArgb(32, 29, 40))
                g.FillRectangle(base, board)
            End Using
            For r As Integer = 0 To maze.RowCount - 1
                For c As Integer = 0 To maze.ColumnCount - 1
                    If maze.GetTile(r, c) = "1"c Then Continue For
                    Dim rect As New RectangleF(board.Left + c * tile + 0.6F, board.Top + r * tile + 0.6F, tile - 1.2F, tile - 1.2F)
                    Dim tone = (r * 17 + c * 29) Mod 13
                    Using brush As New SolidBrush(Color.FromArgb(48 + tone, 44 + tone, 59 + tone)),
                          edge As New Pen(Color.FromArgb(85, 80, 100), Math.Max(0.6F, tile * 0.03F)),
                          crack As New Pen(Color.FromArgb(34, 30, 44), Math.Max(0.5F, tile * 0.025F))
                        g.FillRectangle(brush, rect)
                        g.DrawLine(edge, rect.Left, rect.Top, rect.Right, rect.Top)
                        If tone Mod 4 = 0 Then
                            Dim x = rect.Left + tile * 0.3F
                            g.DrawLines(crack, {New PointF(x, rect.Top), New PointF(x + tile * 0.1F, rect.Top + tile * 0.4F), New PointF(x - tile * 0.15F, rect.Top + tile * 0.7F)})
                        End If
                    End Using
                Next
            Next
        End Sub

        Public Shared Sub Wall(g As Graphics, rect As RectangleF, r As Integer, c As Integer)
            Using mortar As New SolidBrush(Color.FromArgb(38, 31, 49)),
                  edge As New Pen(Color.FromArgb(142, 125, 158), Math.Max(0.6F, rect.Width * 0.06F))
                g.FillRectangle(mortar, rect)
                For row As Integer = 0 To 2
                    For column As Integer = 0 To 1
                        Dim x = rect.Left + column * rect.Width / 2
                        Dim y = rect.Top + row * rect.Height / 3
                        Dim tone = (r * 7 + c * 3 + row * 11 + column * 5) Mod 15
                        Dim brick As New RectangleF(x + 0.8F, y + 0.8F, rect.Width / 2 - 1.6F, rect.Height / 3 - 1.6F)
                        Using brush As New SolidBrush(Color.FromArgb(91 + tone, 75 + tone, 111 + tone))
                            g.FillRectangle(brush, brick)
                        End Using
                    Next
                Next
                g.DrawLine(edge, rect.Left, rect.Top + 1, rect.Right, rect.Top + 1)
            End Using
        End Sub

        Public Shared Sub Portal(g As Graphics, rect As RectangleF)
            Dim ring As New RectangleF(rect.X + rect.Width * 0.08F, rect.Y + rect.Height * 0.08F, rect.Width * 0.84F, rect.Height * 0.84F)
            Using dark As New SolidBrush(Color.FromArgb(12, 8, 22)), rim As New Pen(Color.FromArgb(255, 183, 80), Math.Max(1.5F, rect.Width * 0.08F)),
                  inner As New Pen(Color.FromArgb(186, 135, 236), Math.Max(0.8F, rect.Width * 0.04F))
                g.FillEllipse(dark, ring) : g.DrawEllipse(rim, ring)
                g.DrawEllipse(inner, ring.X + rect.Width * 0.14F, ring.Y + rect.Height * 0.14F, ring.Width - rect.Width * 0.28F, ring.Height - rect.Height * 0.28F)
            End Using
        End Sub
    End Class
End Namespace
