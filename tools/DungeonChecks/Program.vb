Imports System
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports GravityMaze.Engine
Imports GravityMaze.Levels
Imports GravityMaze.Data

Module Program
    Private checks As Integer
    Public Sub Check(condition As Boolean, message As String)
        checks += 1
        If Not condition Then Throw New Exception(message)
    End Sub

    Public Function Room(Optional launcher As Boolean = False, Optional blade As Boolean = False) As MazeDefinition
        Dim spec As New DungeonDefinition()
        spec.Spikes.Add(New SpikeTrap With {.Row = 1, .Column = 3})
        If launcher Then spec.Launchers.Add(New DartLauncher With {.X = 1.5F, .Y = 2.5F, .DX = 1})
        If blade Then spec.Blades.Add(New BladeTrap With {.X1 = 2.5F, .Y1 = 2.5F, .X2 = 5.5F, .Y2 = 2.5F})
        Return New MazeDefinition({"111111111", "1S0T0K001", "100000V01", "1c00E0001", "1000Q0001", "100000G01", "111111111"}, spec)
    End Function

    Sub Main()
        SchemaChecks.Run()
        TrapChecks.Run()
        KeyChecks.Run()
        TestRecords()
        AtlasChecks.Run()
        CollapseChecks.Run()
        PilotChecks.Run()
        LevelChecks.Run()
        ProofChecks.Run()
        Console.WriteLine($"PASS: {checks} dungeon checks")
    End Sub


    Private Sub TestRecords()
        Dim scoreDir = Path.Combine(Path.GetTempPath(), "dungeon_records_" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(scoreDir)
        Try
            Dim file = Path.Combine(scoreDir, "records.xml")
            Dim scores As New ScoreManager(file)
            scores.AddRecord(New ScoreRecord With {.PlayerName = "TEST", .LevelNumber = 0, .CampaignLevels = 3, .TimeSeconds = 80, .Date = DateTime.UtcNow})
            scores.AddRecord(New ScoreRecord With {.PlayerName = "TEST", .LevelNumber = 0, .CampaignLevels = 4, .TimeSeconds = 140, .Date = DateTime.UtcNow})
            scores = New ScoreManager(file)
            Check(scores.TopRecords(0, 10, 4).Count = 1 AndAlso scores.BestRecordFor("TEST", 0, 4).TimeSeconds = 140, "Four-level campaigns must not compete against old three-level times")
            Check(scores.TopRecords(0, 10).Count = 2, "Old campaign records must remain preserved")
            IO.File.WriteAllText(file, IO.File.ReadAllText(file).Replace(" campaignLevels=""3""", ""))
            scores = New ScoreManager(file)
            Check(scores.TopRecords(0, 10, 3).Count = 1 AndAlso scores.BestRecord(0, 3).TimeSeconds = 80, "Legacy XML without campaign size must reload as a three-level run")
        Finally
            Directory.Delete(scoreDir, True)
        End Try
    End Sub

    Public Sub Drive(engine As GameEngine, x As Single, y As Single)
        For i As Integer = 1 To 1000
            Dim dx = x - engine.BallX, dy = y - engine.BallY
            If Math.Abs(dx) < 0.06F AndAlso Math.Abs(dy) < 0.06F AndAlso Math.Abs(engine.VelocityX) < 0.012F AndAlso Math.Abs(engine.VelocityY) < 0.012F Then Return
            Dim vx = Math.Clamp(dx * 0.2F, -0.07F, 0.07F), vy = Math.Clamp(dy * 0.2F, -0.07F, 0.07F)
            Dim tx As Single = If(engine.VelocityX < vx - 0.003F, 1, If(engine.VelocityX > vx + 0.003F, -1, 0))
            Dim ty As Single = If(engine.VelocityY < vy - 0.003F, 1, If(engine.VelocityY > vy + 0.003F, -1, 0))
            engine.Update(tx, ty)
            If engine.State <> GameState.Playing Then Return
        Next
    End Sub
End Module
