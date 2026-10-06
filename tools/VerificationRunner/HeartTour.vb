Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports GravityMaze.UI

Namespace VerificationRunner
    Public Module HeartTour
        Public Sub Run(outDir As String)
            Directory.CreateDirectory(outDir)
            Dim distinct As New HashSet(Of String)()
            Using grid As New Bitmap(600, 200), g As Graphics = Graphics.FromImage(grid),
                  font As New Font("Segoe UI", 14.0F)
                g.Clear(Color.FromArgb(20, 24, 40))
                For quarters As Integer = 0 To 4
                    Using sample As New Bitmap(64, 64)
                        Using sg = Graphics.FromImage(sample)
                            sg.Clear(Color.Black)
                            Sprites.DrawHeartFill(sg, New RectangleF(8, 8, 48, 48), quarters / 4.0F)
                        End Using
                        Dim pixels As New List(Of String)()
                        For y As Integer = 0 To 63
                            For x As Integer = 0 To 63
                                pixels.Add(sample.GetPixel(x, y).ToArgb().ToString("X8"))
                            Next
                        Next
                        If Not distinct.Add(String.Join("", pixels)) Then Throw New Exception("Quarter-heart states must be visually distinct")
                        g.DrawImageUnscaled(sample, 30 + quarters * 110, 40)
                        g.DrawString($"{quarters}/4", font, Brushes.White, 36 + quarters * 110, 120)
                    End Using
                Next
                grid.Save(Path.Combine(outDir, "quarter-heart-states.png"), ImageFormat.Png)
            End Using
            Console.WriteLine("PASS: five distinct heart-fill states; comparison screenshot saved")
        End Sub
    End Module
End Namespace
