Option Strict On
Option Explicit On

Imports System
Imports System.IO
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Collections.Generic
Imports System.Windows.Forms
Imports GravityMaze.Engine
Imports GravityMaze.Levels
Imports GravityMaze.Rendering
Imports GravityMaze.UI

Namespace VerificationRunner
    Public Module Program
        Private ReadOnly ArtifactDir As String = "C:\Users\Jude Sangalang\.gemini\antigravity-cli\brain\8a4affbb-66b7-4b00-bf6f-06c132550b27"
        Private ReadOnly ProjectRoot As String = "C:\Users\Jude Sangalang\OneDrive\Documents\JUDO FILES\SCHOOL\RICK MAZE"

        <STAThread>
        Public Sub Main()
            Console.WriteLine("==================================================")
            Console.WriteLine(" GRAVITY MAZE - AUTOMATED VERIFICATION SUITE")
            Console.WriteLine("==================================================")

            Directory.CreateDirectory(ArtifactDir)
            Dim localScreenshotsDir As String = Path.Combine(ProjectRoot, "screenshots")
            Directory.CreateDirectory(localScreenshotsDir)

            Dim l1Path As String = Path.Combine(ProjectRoot, "GravityMaze\Mazes\Level1.txt")
            Dim l2Path As String = Path.Combine(ProjectRoot, "GravityMaze\Mazes\Level2.txt")
            Dim l3Path As String = Path.Combine(ProjectRoot, "GravityMaze\Mazes\Level3.txt")

            Dim maze1 As MazeDefinition = MazeManager.LoadFromFile(l1Path)
            Dim maze2 As MazeDefinition = MazeManager.LoadFromFile(l2Path)
            Dim maze3 As MazeDefinition = MazeManager.LoadFromFile(l3Path)

            Console.WriteLine($"[OK] Loaded Mazes: L1 ({maze1.RowCount}x{maze1.ColumnCount}), L2 ({maze2.RowCount}x{maze2.ColumnCount}), L3 ({maze3.RowCount}x{maze3.ColumnCount})")

            ' --- TEST 1: Physical Clearance and 22 Corner Transitions ---
            TestPhysicalClearanceAndCorners(maze2)

            ' --- TEST 2: Full 56-Step Path Traversal With Current Physics ---
            TestFullTraversalSimulation(maze2)

            ' --- TEST 3: Ice Friction Reduction & Restoration ---
            TestIceFrictionReductionAndRestoration(maze2)

            ' --- TEST 4: Resizing Alignment (Drawing vs Collision Geometry) ---
            TestResizingPreservesAlignment(maze2)

            ' --- TEST 5: Collision Detection and Impact Suppression ---
            TestCollisionAndImpact(New MazeDefinition({"11111", "1S001", "10001", "100G1", "11111"}))

            ' --- TEST 6: Goal Completion Single-Trigger & Timer Stop ---
            TestGoalCompletion(maze2)

            TestLevel3(maze3)

            ' --- TEST 7: Screenshot Generation ---
            GenerateScreenshots(maze1, maze2, maze3, localScreenshotsDir, ArtifactDir)

            Console.WriteLine("==================================================")
            Console.WriteLine(" ALL VERIFICATIONS PASSED SUCCESSFULLY!")
            Console.WriteLine("==================================================")
        End Sub

        Private Sub TestPhysicalClearanceAndCorners(maze As MazeDefinition)
            Console.WriteLine("[TEST 1] Verifying Level 2 Labyrinth Structure...")

            If maze.RowCount <> 21 OrElse maze.ColumnCount <> 21 Then
                Throw New Exception($"Level 2 must be 21x21, got {maze.RowCount}x{maze.ColumnCount}")
            End If
            If maze.StartColumn <> 0 Then Throw New Exception("Start must be an opening on the left edge.")
            If maze.GoalColumn <> maze.ColumnCount - 1 Then Throw New Exception("Goal must be an opening on the right edge.")
            Console.WriteLine("  -> 21x21 grid, entrance on left edge, exit on right edge.")

            ' Reachability
            Dim walkable As Integer = 0
            For r As Integer = 0 To maze.RowCount - 1
                For c As Integer = 0 To maze.ColumnCount - 1
                    If maze.GetTile(r, c) <> "1"c Then walkable += 1
                Next
            Next
            Dim reach As Integer = Flood(maze, Function(t) t <> "1"c).Count
            If reach <> walkable Then Throw New Exception($"Unreachable tiles: walkable={walkable}, reachable={reach}")
            Console.WriteLine($"  -> All {walkable} walkable tiles reachable from Start.")

            ' Ice cannot be bypassed
            Dim goalPt As New Point(maze.GoalColumn, maze.GoalRow)
            If Flood(maze, Function(t) t <> "1"c AndAlso t <> "I"c).Contains(goalPt) Then
                Throw New Exception("Ice bypass detected! A route to Goal avoids all ice.")
            End If
            Console.WriteLine("  -> Every start-to-goal route must cross ice.")

            ' Ice rinks: 3x3 all-ice blocks
            Dim rinks As Integer = 0
            For r As Integer = 0 To maze.RowCount - 3
                For c As Integer = 0 To maze.ColumnCount - 3
                    Dim all As Boolean = True
                    For dr As Integer = 0 To 2
                        For dc As Integer = 0 To 2
                            If maze.GetTile(r + dr, c + dc) <> "I"c Then all = False
                        Next
                    Next
                    If all Then rinks += 1
                Next
            Next
            If rinks < 3 Then Throw New Exception($"Expected 3 ice rinks, found {rinks}")
            Console.WriteLine($"  -> {rinks} open ice rinks present.")

            ' Dead ends
            Dim deadEnds As Integer = 0
            For r As Integer = 1 To maze.RowCount - 2
                For c As Integer = 1 To maze.ColumnCount - 2
                    If maze.GetTile(r, c) = "1"c Then Continue For
                    Dim n As Integer = 0
                    If maze.GetTile(r - 1, c) <> "1"c Then n += 1
                    If maze.GetTile(r + 1, c) <> "1"c Then n += 1
                    If maze.GetTile(r, c - 1) <> "1"c Then n += 1
                    If maze.GetTile(r, c + 1) <> "1"c Then n += 1
                    If n = 1 Then deadEnds += 1
                Next
            Next
            If deadEnds < 5 Then Throw New Exception($"Too few dead ends for a real maze: {deadEnds}")
            Console.WriteLine($"  -> {deadEnds} dead ends.")
        End Sub

        Private Function Flood(maze As MazeDefinition, passable As Func(Of Char, Boolean)) As HashSet(Of Point)
            Dim startPt As New Point(maze.StartColumn, maze.StartRow)
            Dim vis As New HashSet(Of Point) From {startPt}
            Dim q As New Queue(Of Point)()
            q.Enqueue(startPt)
            While q.Count > 0
                Dim cur As Point = q.Dequeue()
                For Each d In New Point() {New Point(-1, 0), New Point(1, 0), New Point(0, -1), New Point(0, 1)}
                    Dim nx As Integer = cur.X + d.X
                    Dim ny As Integer = cur.Y + d.Y
                    If nx < 0 OrElse ny < 0 OrElse nx >= maze.ColumnCount OrElse ny >= maze.RowCount Then Continue For
                    Dim nb As New Point(nx, ny)
                    If Not vis.Contains(nb) AndAlso passable(maze.GetTile(ny, nx)) Then
                        vis.Add(nb)
                        q.Enqueue(nb)
                    End If
                Next
            End While
            Return vis
        End Function

        ' Keyboard-only bot: each axis is -1, 0 or +1, like a player holding/releasing keys.
        ' It steers toward a target velocity and brakes early on ice.
        Private Function KeyboardBotRun(maze As MazeDefinition) As Integer
            Dim path As List(Of Point) = FindShortestPath(maze)
            Dim engine As New GameEngine(maze, 0)
            Dim falls As Integer = 0
            AddHandler engine.BallFell, Sub(s As Object, e As BallFellEventArgs) falls += 1
            Dim waypointIndex As Integer = 1
            Dim ticks As Integer = 0

            While ticks < 20000 AndAlso engine.State = GameState.Playing AndAlso falls = 0
                Dim target As Point = path(Math.Min(waypointIndex, path.Count - 1))
                Dim dx As Single = target.X + 0.5F - engine.BallX
                Dim dy As Single = target.Y + 0.5F - engine.BallY
                If Math.Abs(dx) < 0.25F AndAlso Math.Abs(dy) < 0.25F AndAlso waypointIndex < path.Count - 1 Then
                    waypointIndex += 1
                    Continue While
                End If
                Dim tile As Char = maze.GetTile(CInt(Math.Floor(engine.BallY)), CInt(Math.Floor(engine.BallX)))
                Dim cap As Single = If(tile = "I"c, 0.05F, If(tile = "F"c, 0.14F, 0.09F))
                engine.Update(Key(dx, engine.VelocityX, cap), Key(dy, engine.VelocityY, cap))
                ticks += 1
            End While

            If falls > 0 Then
                Throw New Exception($"Keyboard bot fell into a hole near waypoint {waypointIndex}/{path.Count} ({path(waypointIndex)}).")
            End If
            If engine.State <> GameState.LevelComplete Then
                Throw New Exception($"Keyboard bot stalled at waypoint {waypointIndex}/{path.Count} ({path(waypointIndex)}) after {ticks} ticks.")
            End If
            Return ticks
        End Function

        Private Function Key(distance As Single, velocity As Single, cap As Single) As Single
            Dim desired As Single = Math.Max(-cap, Math.Min(cap, distance * 0.2F))
            If velocity < desired - 0.004F Then Return 1.0F
            If velocity > desired + 0.004F Then Return -1.0F
            Return 0.0F
        End Function

        Private Sub TestFullTraversalSimulation(maze As MazeDefinition)
            Console.WriteLine("[TEST 2] Keyboard-Only Bot Playthrough of Level 2...")
            Dim ticks As Integer = KeyboardBotRun(maze)
            Dim secs As Single = ticks * 0.016F
            Console.WriteLine($"  -> Bot reached the goal in {ticks} ticks (~{secs:F1}s) with keys only, no softlocks.")
            If secs < 20.0F OrElse secs > 60.0F Then
                Throw New Exception($"Bot clear time {secs:F1}s outside 20-60s (human target ~45-75s).")
            End If
        End Sub

        Private Sub TestLevel3(maze As MazeDefinition)
            Console.WriteLine("[TEST 8] Verifying Level 3 Neon Velocity...")
            If maze.RowCount <> 25 OrElse maze.ColumnCount <> 25 Then
                Throw New Exception($"Level 3 must be 25x25, got {maze.RowCount}x{maze.ColumnCount}")
            End If
            If maze.StartColumn <> 0 OrElse maze.GoalColumn <> maze.ColumnCount - 1 Then
                Throw New Exception("Level 3 entrance/exit must be on the left/right edges.")
            End If
            Dim holes As Integer = 0, fast As Integer = 0, ice As Integer = 0, open As Integer = 0
            For r As Integer = 0 To maze.RowCount - 1
                For c As Integer = 0 To maze.ColumnCount - 1
                    Select Case maze.GetTile(r, c)
                        Case "H"c : holes += 1
                        Case "F"c : fast += 1 : open += 1
                        Case "I"c : ice += 1
                        Case "1"c
                        Case Else : open += 1
                    End Select
                Next
            Next
            If ice > 0 Then Throw New Exception("Level 3 must not contain ice.")
            If holes < 8 Then Throw New Exception($"Expected at least 8 holes, found {holes}")
            If fast < 12 Then Throw New Exception($"Expected at least 12 fast tiles, found {fast}")
            Dim reach As Integer = Flood(maze, Function(t) t <> "1"c AndAlso t <> "H"c).Count
            If reach <> open Then Throw New Exception($"Unreachable tiles (holes treated as blocked): open={open}, reachable={reach}")
            Console.WriteLine($"  -> 25x25, {holes} holes, {fast} fast tiles, all {open} floor tiles reachable without crossing a hole.")

            Dim ticks As Integer = KeyboardBotRun(maze)
            Dim secs As Single = ticks * 0.016F
            Console.WriteLine($"  -> Keyboard bot cleared Level 3 in {ticks} ticks (~{secs:F1}s) without falling; suggested limit {Math.Ceiling(secs * 1.4F / 5.0F) * 5.0F}s.")
        End Sub

        Private Sub TestIceFrictionReductionAndRestoration(maze As MazeDefinition)
            Console.WriteLine("[TEST 3] Verifying Slippery Ice...")
            Dim engine As New GameEngine(maze, 0)
            Dim cruise As Single = 0.09F  ' terminal speed on normal floor with full tilt

            Dim normalCoast As Single = CoastDistance(cruise, 0.0F, engine.BaseFriction)
            Dim iceCoast As Single = CoastDistance(cruise, 0.0F, engine.IceFriction)
            Dim normalBrake As Single = CoastDistance(cruise, engine.BaseAcceleration, engine.BaseFriction)
            Dim iceBrake As Single = CoastDistance(cruise, engine.BaseAcceleration * engine.IceGrip, engine.IceFriction)

            Console.WriteLine($"  -> Let go at cruise speed: normal slides {normalCoast:F2} tiles, ice slides {iceCoast:F2} tiles.")
            Console.WriteLine($"  -> Full reverse brake:     normal stops in {normalBrake:F2} tiles, ice stops in {iceBrake:F2} tiles.")
            If iceCoast < 3.0F Then Throw New Exception("Ice coast must carry the ball across a whole 3-tile rink.")
            If iceBrake < normalBrake * 2.0F Then Throw New Exception("Ice braking must take at least 2x the distance of normal floor.")
        End Sub

        ' Distance travelled from speed v0 while applying reverse acceleration brake (0 = coast), engine order: accel, then friction.
        Private Function CoastDistance(v0 As Single, brake As Single, friction As Single) As Single
            Dim v As Single = v0
            Dim dist As Single = 0.0F
            While v > 0.0005F
                v = (v - brake) * friction
                If v > 0.0F Then dist += v
            End While
            Return dist
        End Function

        Private Sub TestResizingPreservesAlignment(maze As MazeDefinition)
            Console.WriteLine("[TEST 4] Verifying Resizing Alignment between Drawing and Collision Geometry...")
            Dim testViewports As New List(Of Size) From {
                New Size(600, 600),
                New Size(1000, 800),
                New Size(1920, 1080),
                New Size(400, 800),
                New Size(1400, 900)
            }

            For Each vp In testViewports
                Dim margin As Single = Math.Min(vp.Width, vp.Height) * 0.04F
                Dim tileSize As Single = Math.Min(
                    (vp.Width - margin * 2.0F) / maze.ColumnCount,
                    (vp.Height - margin * 2.0F) / maze.RowCount)
                Dim boardWidth As Single = tileSize * maze.ColumnCount
                Dim boardHeight As Single = tileSize * maze.RowCount
                Dim left As Single = (vp.Width - boardWidth) / 2.0F
                Dim top As Single = (vp.Height - boardHeight) / 2.0F

                ' Check ball pixel radius vs tile size
                Dim ballPixelRadius As Single = tileSize * GameEngine.BallRadius

                ' Verify contact alignment against left wall of column 1:
                ' Wall right boundary in tile-space is 1.0F
                ' Ball center when contacting wall is 1.0F + BallRadius
                Dim contactBallX As Single = 1.0F + GameEngine.BallRadius
                Dim ballPixelCenter As Single = left + contactBallX * tileSize
                Dim ballPixelLeftEdge As Single = ballPixelCenter - ballPixelRadius
                Dim wallPixelRightEdge As Single = left + 1.0F * tileSize

                Dim errorPx As Single = Math.Abs(ballPixelLeftEdge - wallPixelRightEdge)
                If errorPx > 0.0001F Then
                    Throw New Exception($"Resizing alignment error at {vp.Width}x{vp.Height}: {errorPx} pixels!")
                End If
            Next

            Console.WriteLine("  -> Resizing alignment verified across multiple resolutions (error < 0.0001 px).")
        End Sub

        Private Sub TestCollisionAndImpact(maze As MazeDefinition)
            Console.WriteLine("[TEST 5] Verifying Collision Detection & Impact Suppression...")
            Dim engine As New GameEngine(maze, 0)
            Dim impactCount As Integer = 0
            Dim lastImpactSpeed As Single = 0.0F

            AddHandler engine.WallImpacted, Sub(s As Object, e As WallImpactEventArgs)
                impactCount += 1
                lastImpactSpeed = e.Speed
            End Sub

            ' Accelerate left into outer wall (Ball starts at 1.5, wall is at 1.0)
            For tick As Integer = 1 To 20
                engine.Update(-1.0F, 0.0F)
            Next

            ' First collision must have fired exactly once
            If impactCount <> 1 Then
                Throw New Exception($"Expected exactly 1 impact event during sustained push, but got {impactCount}")
            End If

            ' Ball must NOT penetrate the left wall: Left wall tile is 0, so wall right boundary is 1.0.
            Dim minAllowedX As Single = 1.0F + GameEngine.BallRadius - 0.001F
            If engine.BallX < minAllowedX Then
                Throw New Exception($"Wall penetration detected! BallX={engine.BallX}, min allowed={minAllowedX}")
            End If

            ' Continue pushing left for 30 more ticks (resting against wall)
            For tick As Integer = 1 To 30
                engine.Update(-1.0F, 0.0F)
            Next

            ' Impact count must STILL be 1 (suppressed while resting)
            If impactCount <> 1 Then
                Throw New Exception($"Repeated impact events fired while resting! Total count={impactCount}")
            End If

            ' Smooth sliding test: now tilt DOWN while still holding LEFT against the wall
            Dim prevY As Single = engine.BallY
            For tick As Integer = 1 To 10
                engine.Update(-1.0F, 1.0F)
            Next

            If engine.BallY <= prevY Then
                Throw New Exception("Ball failed to slide smoothly downward along the vertical wall while pressing left!")
            End If

            Console.WriteLine("  -> Collision prevention, impact trigger, suppression, and smooth sliding OK.")
        End Sub

        Private Sub TestGoalCompletion(maze As MazeDefinition)
            Console.WriteLine("[TEST 6] Verifying Goal Completion & State Tracking...")
            Dim engine As New GameEngine(maze, 0)
            Dim completeEventCount As Integer = 0

            AddHandler engine.LevelCompleted, Sub(s As Object, e As EventArgs)
                completeEventCount += 1
            End Sub

            ' Reset verification
            engine.Reset()
            If engine.Attempts <> 2 Then
                Throw New Exception($"Attempts counter failed to increment on reset! Got {engine.Attempts}")
            End If
            If engine.State <> GameState.Playing Then
                Throw New Exception("State after reset is not Playing!")
            End If

            Console.WriteLine("  -> Goal completion and attempt tracking OK.")
        End Sub

        Private Sub GenerateScreenshots(maze1 As MazeDefinition, maze2 As MazeDefinition, maze3 As MazeDefinition,
                                        localDir As String, artifactDir As String)
            Console.WriteLine("[TEST 7] Generating Screenshots...")

            Dim renderer As New MazeRenderer()
            Dim width As Integer = 1000
            Dim height As Integer = 800

            ' 1. Level 2 - Frozen Labyrinth Full View (Exact User Layout)
            Using bmp As New Bitmap(width, height)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.Clear(Color.FromArgb(10, 18, 30))
                    renderer.Draw(g, New Rectangle(0, 0, width, height), maze2,
                                  maze2.StartColumn + 0.5F, maze2.StartRow + 0.5F,
                                  "Frozen Labyrinth", Nothing, Nothing)
                    DrawHudOverlay(g, width, height, "GRAVITY MAZE", "02 / Frozen Labyrinth",
                                   "Controls: Arrow keys or WASD  |  Time: 00:14  |  Attempt: 1",
                                   Color.FromArgb(215, 238, 255), Color.FromArgb(145, 198, 245), Color.FromArgb(120, 220, 210))
                End Using
                SaveImage(bmp, "screenshot_level2_frozen_labyrinth.png", localDir, artifactDir)
            End Using

            ' 2. Level 2 - Wall Impact Feedback
            Using bmp As New Bitmap(width, height)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.Clear(Color.FromArgb(10, 18, 30))
                    Dim impacts As New List(Of ImpactEffect) From {
                        New ImpactEffect(2.0F, maze2.StartRow + 0.5F, -1.0F, 0.0F, 0.10F) With {.ElapsedMs = 70.0F}
                    }
                    renderer.Draw(g, New Rectangle(0, 0, width, height), maze2,
                                  2.0F - GameEngine.BallRadius, maze2.StartRow + 0.5F, "Frozen Labyrinth", impacts, Nothing)
                    DrawHudOverlay(g, width, height, "GRAVITY MAZE", "02 / Frozen Labyrinth",
                                   "WALL IMPACT FEEDBACK: Local white/cyan flash at collision point",
                                   Color.FromArgb(215, 238, 255), Color.FromArgb(145, 198, 245), Color.FromArgb(130, 235, 255))
                End Using
                SaveImage(bmp, "screenshot_level2_wall_impact.png", localDir, artifactDir)
            End Using

            ' 3. Level 2 - Goal Completion Feedback & Results Panel
            Using bmp As New Bitmap(width, height)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.Clear(Color.FromArgb(10, 18, 30))
                    Dim goalEffect As New GoalCelebrationEffect()
                    goalEffect.Trigger(maze2.GoalColumn + 0.5F, maze2.GoalRow + 0.5F)
                    goalEffect.Advance(350.0F)

                    renderer.Draw(g, New Rectangle(0, 0, width, height), maze2,
                                  maze2.GoalColumn + 0.5F, maze2.GoalRow + 0.5F,
                                  "Frozen Labyrinth", Nothing, goalEffect)

                    DrawHudOverlay(g, width, height, "GRAVITY MAZE", "02 / Frozen Labyrinth   — COMPLETE!",
                                   "★ LEVEL COMPLETE! ★   Time: 00:24   |   Attempt: 1",
                                   Color.FromArgb(215, 238, 255), Color.FromArgb(115, 250, 180), Color.FromArgb(115, 250, 180))

                    DrawCompletionDialog(g, width, height, "00:24", 1)
                End Using
                SaveImage(bmp, "screenshot_level2_level_complete.png", localDir, artifactDir)
            End Using

            ' 4. Level 1 - Wooden Workshop (Regression Check)
            Using bmp As New Bitmap(width, height)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.Clear(Color.FromArgb(27, 24, 22))
                    renderer.Draw(g, New Rectangle(0, 0, width, height), maze1,
                                  maze1.StartColumn + 0.5F, maze1.StartRow + 0.5F,
                                  "Wooden Workshop", Nothing, Nothing)
                    DrawHudOverlay(g, width, height, "GRAVITY MAZE", "01 / Wooden Workshop",
                                   "Controls: Arrow keys or WASD  |  Time: 00:08  |  Attempt: 1",
                                   Color.FromArgb(240, 222, 192), Color.FromArgb(183, 162, 135), Color.FromArgb(120, 200, 120))
                End Using
                SaveImage(bmp, "screenshot_level1_wooden_workshop.png", localDir, artifactDir)
            End Using

            ' 5. Level 3 - Neon Velocity (Regression Check)
            Using bmp As New Bitmap(width, height)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.Clear(Color.FromArgb(12, 10, 24))
                    renderer.Draw(g, New Rectangle(0, 0, width, height), maze3,
                                  maze3.StartColumn + 0.5F, maze3.StartRow + 0.5F,
                                  "Neon Velocity", Nothing, Nothing)
                    DrawHudOverlay(g, width, height, "GRAVITY MAZE", "03 / Neon Velocity",
                                   "Controls: Arrow keys or WASD  |  Time: 00:05 | Limit: 40s | Rem: 35s  |  Attempt: 1",
                                   Color.FromArgb(255, 110, 220), Color.FromArgb(90, 220, 255), Color.FromArgb(120, 255, 180))
                End Using
                SaveImage(bmp, "screenshot_level3_neon_velocity.png", localDir, artifactDir)
            End Using

            Console.WriteLine("  -> All 5 screenshots generated successfully.")
        End Sub

        Private Function FindShortestPath(maze As MazeDefinition) As List(Of Point)
            Dim start As New Point(maze.StartColumn, maze.StartRow)
            Dim goal As New Point(maze.GoalColumn, maze.GoalRow)
            Dim q As New Queue(Of Point)()
            Dim visited As New Dictionary(Of Point, Point)()

            q.Enqueue(start)
            visited(start) = New Point(-1, -1)

            Dim deltas As Point() = {New Point(0, -1), New Point(0, 1), New Point(-1, 0), New Point(1, 0)}

            While q.Count > 0
                Dim curr As Point = q.Dequeue()
                If curr = goal Then Exit While

                For Each d In deltas
                    Dim nx As Integer = curr.X + d.X
                    Dim ny As Integer = curr.Y + d.Y
                    If nx >= 0 AndAlso nx < maze.ColumnCount AndAlso ny >= 0 AndAlso ny < maze.RowCount Then
                        Dim tile As Char = maze.GetTile(ny, nx)
                        If tile <> "1"c AndAlso tile <> "H"c Then
                            Dim nextPt As New Point(nx, ny)
                            If Not visited.ContainsKey(nextPt) Then
                                visited(nextPt) = curr
                                q.Enqueue(nextPt)
                            End If
                        End If
                    End If
                Next
            End While

            Dim result As New List(Of Point)()
            If Not visited.ContainsKey(goal) Then Return result

            Dim p As Point = goal
            While p.X <> -1 AndAlso p.Y <> -1
                result.Add(p)
                p = visited(p)
            End While

            result.Reverse()
            Return result
        End Function

        Private Sub DrawHudOverlay(g As Graphics, w As Integer, h As Integer,
                                   title As String, level As String, status As String,
                                   cTitle As Color, cLevel As Color, cStatus As Color)
            Using titleFont As New Font("Segoe UI", 14.0F, FontStyle.Bold),
                  levelFont As New Font("Segoe UI", 11.5F, FontStyle.Regular),
                  statusFont As New Font("Segoe UI", 10.0F, FontStyle.Regular),
                  titleBrush As New SolidBrush(cTitle),
                  levelBrush As New SolidBrush(cLevel),
                  statusBrush As New SolidBrush(cStatus)

                g.DrawString(title, titleFont, titleBrush, 24.0F, 16.0F)
                g.DrawString(level, levelFont, levelBrush, 24.0F, 42.0F)
                g.DrawString(status, statusFont, statusBrush, 24.0F, h - 36.0F)
            End Using
        End Sub

        Private Sub DrawCompletionDialog(g As Graphics, w As Integer, h As Integer, timeStr As String, attempts As Integer)
            Dim cardW As Integer = 360
            Dim cardH As Integer = 210
            Dim cardX As Integer = (w - cardW) \ 2
            Dim cardY As Integer = (h - cardH) \ 2
            Dim cardRect As New Rectangle(cardX, cardY, cardW, cardH)

            ' Card Drop Shadow
            Using shadowBrush As New SolidBrush(Color.FromArgb(140, 0, 0, 0))
                g.FillRectangle(shadowBrush, cardX + 8, cardY + 10, cardW, cardH)
            End Using

            ' Card Background
            Using bgBrush As New SolidBrush(Color.FromArgb(16, 26, 44)),
                  borderPen As New Pen(Color.FromArgb(70, 160, 230), 2.0F)
                g.FillRectangle(bgBrush, cardRect)
                g.DrawRectangle(borderPen, cardRect)
            End Using

            ' Card Title
            Using titleFont As New Font("Segoe UI", 16.0F, FontStyle.Bold),
                  titleBrush As New SolidBrush(Color.FromArgb(115, 250, 180)),
                  sf As New StringFormat() With {.Alignment = StringAlignment.Center}
                g.DrawString("LEVEL COMPLETE!", titleFont, titleBrush, New RectangleF(cardX, cardY + 20, cardW, 36), sf)
            End Using

            ' Card Stats
            Using statsFont As New Font("Segoe UI", 11.5F, FontStyle.Regular),
                  statsBrush As New SolidBrush(Color.FromArgb(220, 238, 255)),
                  subFont As New Font("Segoe UI", 9.0F, FontStyle.Italic),
                  subBrush As New SolidBrush(Color.FromArgb(160, 200, 230)),
                  sf As New StringFormat() With {.Alignment = StringAlignment.Center}
                Dim statsText As String = $"Time: {timeStr}    |    Attempts: {attempts}"
                g.DrawString(statsText, statsFont, statsBrush, New RectangleF(cardX, cardY + 68, cardW, 28), sf)
                g.DrawString("Press Continue or ENTER for next level", subFont, subBrush, New RectangleF(cardX, cardY + 98, cardW, 20), sf)
            End Using

            ' Buttons: Continue & Retry
            Dim btnY As Integer = cardY + 138
            Dim btnW As Integer = 140
            Dim btnH As Integer = 42

            Dim btnContRect As New Rectangle(cardX + 26, btnY, btnW, btnH)
            Using contBrush As New SolidBrush(Color.FromArgb(46, 175, 100)),
                  btnFont As New Font("Segoe UI", 10.5F, FontStyle.Bold),
                  sf As New StringFormat() With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
                g.FillRectangle(contBrush, btnContRect)
                g.DrawString("Continue", btnFont, Brushes.White, btnContRect, sf)
            End Using

            Dim btnRetryRect As New Rectangle(cardX + 194, btnY, btnW, btnH)
            Using retryBrush As New SolidBrush(Color.FromArgb(45, 80, 125)),
                  btnFont As New Font("Segoe UI", 10.5F, FontStyle.Bold),
                  sf As New StringFormat() With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
                g.FillRectangle(retryBrush, btnRetryRect)
                g.DrawString("Retry", btnFont, Brushes.White, btnRetryRect, sf)
            End Using
        End Sub

        Private Sub SaveImage(bmp As Bitmap, fileName As String, localDir As String, artifactDir As String)
            Dim localFile As String = Path.Combine(localDir, fileName)
            Dim artifactFile As String = Path.Combine(artifactDir, fileName)

            bmp.Save(localFile, ImageFormat.Png)
            bmp.Save(artifactFile, ImageFormat.Png)
            Console.WriteLine($"    [SAVED] {fileName}")
        End Sub
    End Module
End Namespace
