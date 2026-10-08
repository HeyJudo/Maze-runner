Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Linq
Imports GravityMaze.Engine
Imports GravityMaze.Levels
Imports GravityMaze.UI

Namespace VerificationRunner
    ' Presentation fixtures driven through real collection/advance contracts. DungeonChecks proves physics clears.
    Public Module DungeonTour
        Friend ReadOnly StateNames As String() = {"explore", "vault", "rumble", "escape", "caved"}
        Friend ReadOnly Insets As New System.Windows.Forms.Padding(24, 120, 24, 60)

        Public Sub Run(outDir As String)
            Directory.CreateDirectory(outDir)
            Dim maze = LoadMaze()
            For Each size In {New Size(960, 600), New Size(1600, 900)}
                For Each tilted In {False, True}
                    For state As Integer = 0 To StateNames.Length - 1
                        Dim run = CreateState(maze, state)
                        Using canvas = CreateCanvas(maze, run, size, tilted)
                            canvas.OverlayPainter = Sub(g, bounds) g.FillRectangle(Brushes.Magenta, 0, 0, 24, 24)
                            For zoom As Integer = 0 To 2
                                SettleCamera(canvas)
                                Using image As New Bitmap(size.Width, size.Height)
                                    canvas.DrawToBitmap(image, New Rectangle(Point.Empty, size))
                                    If image.GetPixel(10, 10).ToArgb() <> Color.Magenta.ToArgb() Then Throw New Exception("Dungeon camera must leave HUD fixed")
                                    image.Save(Path.Combine(outDir, $"{size.Width}-{If(tilted, "tilt", "flat")}-{StateNames(state)}-zoom{zoom}.png"), ImageFormat.Png)
                                End Using
                                canvas.CycleCameraZoom()
                            Next
                        End Using
                    Next
                Next
            Next
            Console.WriteLine("PASS: five asserted dungeon states, three zooms, two sizes, and flat/tilt views; 60 snapshots saved")
        End Sub

        Friend Function LoadMaze() As MazeDefinition
            Return MazeManager.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Mazes", "Level4.txt"))
        End Function

        Friend Function CreateCanvas(maze As MazeDefinition, run As DungeonRun, size As Size, tilted As Boolean) As GameCanvas
            Dim canvas As New GameCanvas With {.Size = size, .CameraEnabled = True, .TiltViewEnabled = tilted, .BoardInsets = Insets}
            canvas.ShowMaze(maze, "Forgotten Keep")
            ' Start the capture matrix at full maze even if the shell's preferred dungeon zoom changes.
            For cycle As Integer = 1 To 3
                If canvas.CameraLabel = "FULL MAZE" Then Exit For
                canvas.CycleCameraZoom()
            Next
            If canvas.CameraLabel <> "FULL MAZE" Then Throw New Exception("Dungeon fixture must start at full maze zoom")
            canvas.DungeonState = run
            canvas.UpdateBoardTilt(0.7F, -0.8F, 600)
            canvas.UpdateBallPosition(run.MarbleX, run.MarbleY)
            Return canvas
        End Function

        Friend Sub SettleCamera(canvas As GameCanvas)
            For tick As Integer = 1 To 120 : canvas.UpdateCamera(16) : Next
        End Sub

        Friend Function CreateState(maze As MazeDefinition, state As Integer) As DungeonRun
            Dim run As New DungeonRun(maze)
            Dim ball As New PointF(maze.StartColumn + 0.5F, maze.StartRow + 0.5F)
            If state >= 1 Then
                CollectSeal(maze, run, "K"c)
                CollectSeal(maze, run, "Q"c)
                ball = TileCenter(maze, "c"c)
            End If
            If state >= 2 Then
                CollectSeal(maze, run, "V"c)
                ball = TileCenter(maze, "V"c)
            End If
            Select Case state
                Case 0, 1
                    AdvanceTo(run, 16, ball)
                Case 2
                    AdvanceTo(run, 2000, ball)
                Case 3
                    ' Occupancy keeps the first cave-in warning open using the engine's real deferred-close rule.
                    ' No state is injected: at ~12 s this remains a warning only while the marble is on its tile.
                    Dim first = maze.Dungeon.Collapse.CaveIns(0).Tiles(0)
                    ball = New PointF(first.Column + 0.5F, first.Row + 0.5F)
                    AdvanceTo(run, 12000, ball)
                Case 4
                    Dim cp = maze.Dungeon.Checkpoints.Where(Function(p) p.Escape).
                        OrderByDescending(Function(p) run.Collapse.Distance(p.Row, p.Column)).First()
                    ball = New PointF(cp.Column + 0.5F, cp.Row + 0.5F)
                    Dim lastReach = maze.Dungeon.Collapse.CaveIns.Max(Function(c) c.AtReach)
                    Dim closedAt = maze.Dungeon.Collapse.RumbleMs + CInt(Math.Ceiling(lastReach / maze.Dungeon.Collapse.Speed * 1000)) + Collapse.CaveWarningMs + 32
                    AdvanceTo(run, closedAt, ball)
                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(state))
            End Select
            AssertState(maze, run, state, ball)
            Return run
        End Function

        Private Sub AdvanceTo(run As DungeonRun, ms As Integer, ball As PointF)
            While run.ElapsedMs < ms
                run.Advance(run.MarbleX, run.MarbleY, ball.X, ball.Y)
            End While
        End Sub

        Private Sub AssertState(maze As MazeDefinition, run As DungeonRun, state As Integer, ball As PointF)
            Check(run.MarbleX = ball.X AndAlso run.MarbleY = ball.Y, "fixture marble must match runtime light position")
            Select Case state
                Case 0
                    Check(run.KeyMask = 0 AndAlso run.Collapse Is Nothing, "explore must have zero keys and no collapse")
                    Check(ball = New PointF(maze.StartColumn + 0.5F, maze.StartRow + 0.5F), "explore marble must be in Crypt")
                Case 1
                    Check(run.KeyMask = 3 AndAlso run.Collapse Is Nothing AndAlso run.GateOpen("c"c), "vault must have K/Q and an open inbound gate")
                    Check(maze.GetTile(CInt(Math.Floor(ball.Y)), CInt(Math.Floor(ball.X))) = "c"c, "vault marble must be at c gate")
                Case 2, 3, 4
                    Check(run.KeyMask = 7 AndAlso run.Collapse IsNot Nothing AndAlso Not run.GateOpen("c"c), "V must start collapse and close inbound gate")
                    For Each tile In maze.Dungeon.Collapse.Breakaway
                        Check(Not run.Solid(tile.Row, tile.Column), "collapse must open each breakaway tile")
                    Next
                    If state = 2 Then
                        Check(run.Collapse.Rumbling AndAlso run.Collapse.ElapsedMs = 2000, "rumble must be two seconds into V collapse")
                    ElseIf state = 3 Then
                        Check(Not run.Collapse.Rumbling AndAlso run.Collapse.ElapsedMs = 12000, "escape must be twelve seconds into V collapse")
                        Check(run.Collapse.CaveInState(0) = Collapse.Warning, "escape's first cave-in must actually be warning")
                        Check(maze.Dungeon.Collapse.CaveIns(0).Tiles.Any(Function(t) run.Collapse.IsCaveInWarning(t.Row, t.Column)), "escape warning tiles must be live")
                    Else
                        Check(Enumerable.Range(0, maze.Dungeon.Collapse.CaveIns.Count).All(Function(i) run.Collapse.CaveInState(i) = Collapse.Closed), "caved must have all cave-ins closed")
                        Check(Enumerable.Range(0, maze.Dungeon.Checkpoints.Count).Any(Function(i) maze.Dungeon.Checkpoints(i).Escape AndAlso run.CheckpointLit(i)), "caved marble must light an escape checkpoint")
                        Check(Not run.Solid(CInt(Math.Floor(ball.Y)), CInt(Math.Floor(ball.X))) AndAlso Not run.Pit(CInt(Math.Floor(ball.Y)), CInt(Math.Floor(ball.X))), "caved escape checkpoint must still be safe")
                    End If
            End Select
        End Sub

        Private Sub Check(condition As Boolean, message As String)
            If Not condition Then Throw New Exception(message)
        End Sub

        Private Sub CollectSeal(maze As MazeDefinition, run As DungeonRun, symbol As Char)
            Dim p = TileCenter(maze, symbol)
            run.Collect(p.X, p.Y)
        End Sub

        Private Function TileCenter(maze As MazeDefinition, symbol As Char) As PointF
            For row As Integer = 0 To maze.RowCount - 1
                For column As Integer = 0 To maze.ColumnCount - 1
                    If maze.GetTile(row, column) = symbol Then Return New PointF(column + 0.5F, row + 0.5F)
                Next
            Next
            Throw New Exception($"Missing dungeon fixture tile {symbol}")
        End Function
    End Module
End Namespace
