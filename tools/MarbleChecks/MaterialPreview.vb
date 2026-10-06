Imports System
Imports System.Drawing
Imports System.Numerics
Imports System.IO
Imports System.Text
Imports GravityMaze.Rendering

' Material samples only, independent of Windows/GDI compositing.
Module MaterialPreview
    Public Sub Save(path As String)
        Const width As Integer = 720, height As Integer = 360
        Using stream = File.Create(path)
            Dim header = Encoding.ASCII.GetBytes($"P6{vbLf}{width} {height}{vbLf}255{vbLf}")
            stream.Write(header)
            For row As Integer = 0 To height - 1
                For col As Integer = 0 To width - 1
                    Dim theme = {"Wooden Workshop", "Frozen Labyrinth", "Neon Velocity"}(col \ 240)
                    Dim x = ((col Mod 240) - 119.5F) / 72
                    Dim y = ((row Mod 180) - 89.5F) / 72
                    Dim turn = If(row < 180, Quaternion.Identity, Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.8F))
                    Dim color = MarbleMaterial.Shade(x, y, turn, 0.4, -0.3, theme)
                    If color.A = 0 Then color = Color.FromArgb(30, 32, 38)
                    stream.WriteByte(color.R) : stream.WriteByte(color.G) : stream.WriteByte(color.B)
                Next
            Next
        End Using
    End Sub
End Module
