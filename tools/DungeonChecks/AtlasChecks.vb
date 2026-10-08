Imports System
Imports System.IO
Imports GravityMaze.Levels

Public Module AtlasChecks
    Public ReadOnly Property SpriteDir As String = Path.Combine(AppContext.BaseDirectory, "Sprites", "Dungeon")

    Public Sub Run()
        Dim atlas = SpriteAtlas.Load(Path.Combine(SpriteDir, "atlas.json"))
        For Each name In SpriteAtlas.RequiredNames
            Check(atlas.Has(name), "Atlas is missing required sprite " & name)
        Next
        atlas.Validate(Function(sheet) SpriteAtlas.PngSize(Path.Combine(SpriteDir, sheet & ".png")))
        Check(atlas("spikes").Frames = 4 AndAlso atlas("crumble").Frames >= 3 AndAlso atlas("saw").Frames >= 2 AndAlso atlas("lava").Frames >= 2, "Animated sprites carry their frames")
        Check(SpriteAtlas.PngSize(Path.Combine(SpriteDir, "maze.png")).Width = 320, "Keyed Timberwolf sheet keeps its size")
        Dim bad = SpriteAtlas.Load(Path.Combine(SpriteDir, "atlas.json"))
        bad("key").X = 10000
        Dim threw = False
        Try
            bad.Validate(Function(sheet) SpriteAtlas.PngSize(Path.Combine(SpriteDir, sheet & ".png")))
        Catch ex As InvalidDataException
            threw = True
        End Try
        Check(threw, "A sprite rectangle outside its sheet is rejected")
    End Sub
End Module
