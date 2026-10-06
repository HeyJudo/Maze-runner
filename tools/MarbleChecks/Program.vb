Imports System
Imports System.Drawing
Imports System.Numerics
Imports System.Diagnostics
Imports GravityMaze.Rendering
Imports GravityMaze.Engine
Imports GravityMaze.Levels

Module Program
    Private checks As Integer
    Private Sub Check(condition As Boolean, message As String)
        checks += 1
        If Not condition Then Throw New Exception(message)
    End Sub
    Private Function SameRotation(a As Quaternion, b As Quaternion) As Boolean
        Return Math.Abs(Quaternion.Dot(a, b)) > 0.99999F
    End Function

    Sub Main(args As String())
        Dim motion As New MarbleMotion()
        motion.Reset(0, 0)
        motion.Advance(MarbleMotion.Radius * CSng(Math.PI / 2), 0)
        Dim front = Vector3.Transform(Vector3.UnitZ, motion.Orientation)
        Check(front.X > 0.999F AndAlso Math.Abs(front.Y) < 0.00001, "Rightward travel must turn the surface to the right")
        motion.Advance(0, 0)
        Check(SameRotation(motion.Orientation, Quaternion.Identity), "Reversing must undo the roll")
        motion.Advance(0, MarbleMotion.Radius * CSng(Math.PI / 2))
        front = Vector3.Transform(Vector3.UnitZ, motion.Orientation)
        Check(front.Y > 0.999F, "Downward travel must turn the surface downward")
        motion.Advance(0, 0)
        Check(SameRotation(motion.Orientation, Quaternion.Identity), "Vertical reversal must undo the roll")
        motion.Advance(MarbleMotion.Radius * CSng(Math.PI * 2), 0)
        Check(SameRotation(motion.Orientation, Quaternion.Identity), "One circumference must restore surface orientation")

        Dim singleStep As New MarbleMotion(), smallSteps As New MarbleMotion()
        singleStep.Reset(0, 0) : smallSteps.Reset(0, 0)
        singleStep.Advance(0.6F, 0.8F)
        For i As Integer = 1 To 100
            smallSteps.Advance(i * 0.006F, i * 0.008F)
        Next
        Check(SameRotation(singleStep.Orientation, smallSteps.Orientation), "Straight roll must be independent of tick subdivision")
        Dim held = singleStep.Orientation
        For i As Integer = 1 To 120
            singleStep.Advance(0.6F, 0.8F)
            Check(SameRotation(held, singleStep.Orientation), "Stationary marble must not rotate")
        Next
        singleStep.Rebase(40, 20)
        singleStep.Advance(40, 20)
        Check(SameRotation(held, singleStep.Orientation), "Respawn must not spin across the maze")
        singleStep.Advance(Single.NaN, Single.PositiveInfinity)
        Check(SameRotation(held, singleStep.Orientation), "Invalid positions must not corrupt the surface")
        singleStep.Reset()
        singleStep.Advance(30, 40)
        Check(SameRotation(singleStep.Orientation, Quaternion.Identity), "Retry must establish a new position without rolling a teleport")
        singleStep.Reset(0, 0)
        Check(SameRotation(singleStep.Orientation, Quaternion.Identity), "Level load/retry must reset the surface")
        For i As Integer = 1 To 10000
            singleStep.Advance(CSng(Math.Sin(i * 0.03)), CSng(Math.Cos(i * 0.04)))
        Next
        Check(Math.Abs(singleStep.Orientation.Length() - 1) < 0.00001, "Long paths must keep the orientation normalized")

        ' Exercise actual collision resolution rather than infer rolling from held input.
        Dim maze As New MazeDefinition({"111111", "1S0011", "1000G1", "111111"})
        Dim engine As New GameEngine(maze, 0) With {.HeartsEnabled = False}
        motion.Reset(engine.BallX, engine.BallY)
        For i As Integer = 1 To 150
            engine.Update(1, 0)
            motion.Advance(engine.BallX, engine.BallY)
        Next
        Check(engine.VelocityX = 0 AndAlso engine.BallX > 3, "Fixture must reach a wall")
        held = motion.Orientation
        For i As Integer = 1 To 120
            engine.Update(1, 0)
            motion.Advance(engine.BallX, engine.BallY)
            Check(SameRotation(held, motion.Orientation), "Held tilt at a wall must stop surface rotation")
        Next
        engine.Update(-1, 0) : motion.Advance(engine.BallX, engine.BallY)
        Check(Not SameRotation(held, motion.Orientation), "Rolling must resume when leaving the wall")

        Dim changed As Integer
        Dim largestChange As Integer
        Dim turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.7F)
        For row As Integer = -8 To 8
            For col As Integer = -8 To 8
                Dim x = col / 10.0F, y = row / 10.0F
                If x * x + y * y >= 0.95 Then Continue For
                Dim a = MarbleMaterial.Shade(x, y, Quaternion.Identity, 0, 0, "Wooden Workshop")
                Dim b = MarbleMaterial.Shade(x, y, turn, 0, 0, "Wooden Workshop")
                Dim difference = Math.Max(Math.Abs(CInt(a.R) - b.R), Math.Max(Math.Abs(CInt(a.G) - b.G), Math.Abs(CInt(a.B) - b.B)))
                If difference > 2 Then changed += 1
                largestChange = Math.Max(largestChange, difference)
                Check(a.A = 255 AndAlso b.A = 255, "Surface inside silhouette must stay opaque")
            Next
        Next
        Check(changed > 100, "Rolling must visibly change the brushed surface")
        Check(largestChange <= 20, "Rolling should change subtle texture, not rotate the main lighting")
        Dim warm = MarbleMaterial.Shade(0.65F, 0.4F, Quaternion.Identity, 0, 0, "Wooden Workshop")
        Dim cold = MarbleMaterial.Shade(0.65F, 0.4F, Quaternion.Identity, 0, 0, "Frozen Labyrinth")
        Dim neon = MarbleMaterial.Shade(0.65F, 0.4F, Quaternion.Identity, 0, 0, "Neon Velocity")
        Check(warm.R > cold.R AndAlso cold.B > warm.B, "Wood and ice must reflect warm and cool light")
        Check(neon.B > neon.R, "Neon must give a colored reflection to the silver marble")
        Check(MarbleMaterial.Shade(0.4F, 0.2F, Quaternion.Identity, 1, -1, "Wooden Workshop") <>
              MarbleMaterial.Shade(0.4F, 0.2F, Quaternion.Identity, -1, 1, "Wooden Workshop"), "Tilt must shift reflections")
        Check(MarbleMaterial.Shade(1, 1, turn, 0, 0, "Wooden Workshop").A = 0, "Outside sphere must be transparent")
        Check(MarbleMaterial.Shade(0, 0, turn, 0, 0, "Wooden Workshop", 1).R < 30, "Pit ghost must darken")

        For Each size In {New Size(960, 600), New Size(1600, 900), New Size(600, 960)}
            For Each tx In {-1.0, 0.0, 1.0}
                For Each ty In {-1.0, 0.0, 1.0}
                    Dim projection As New BoardProjection(size.Width, size.Height, tx, ty)
                    For Each nx In {-0.6F, 0.0F, 0.6F}
                        For Each ny In {-0.6F, 0.0F, 0.6F}
                            Dim center As New PointF((nx + 1) * size.Width / 2, (ny + 1) * size.Height / 2)
                            Dim sphere = projection.ProjectSphere(center, 20, size.Width, size.Height)
                            Dim floor = projection.Project(nx, ny)
                            Check(Math.Abs(sphere.Center.X - (floor.X + 1) * size.Width / 2) < 0.001 AndAlso
                                  Math.Abs(sphere.Center.Y - (floor.Y + 1) * size.Height / 2) < 0.001, "Sphere must stay aligned with board position")
                            Check(Single.IsFinite(sphere.Radius) AndAlso sphere.Radius > 10 AndAlso sphere.Radius < 25, "Depth-scaled circle must stay bounded")
                        Next
                    Next
                Next
            Next
        Next
        Check(MarbleMaterial.ShadowSpread(3) > MarbleMaterial.ShadowSpread(0), "Airborne shadow must spread")
        Check(MarbleMaterial.ShadowAlpha(3) < MarbleMaterial.ShadowAlpha(0), "Airborne shadow must soften")
        Check(MarbleMaterial.ShadowAlpha(-1) = MarbleMaterial.ShadowAlpha(0), "Shadow must clamp ground contact")
        If args.Length = 2 AndAlso args(0) = "--preview" Then MaterialPreview.Save(args(1))
        Console.WriteLine($"PASS: {checks} marble motion, material, collision, projection and shadow checks")
    End Sub
End Module
