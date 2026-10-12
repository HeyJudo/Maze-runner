Option Strict On
Option Explicit On

Imports System
Imports System.IO
Imports System.Linq
Imports GravityMaze.Levels

Public Module AtlasChecks
    Public ReadOnly Property SpriteDir As String = Path.Combine(AppContext.BaseDirectory, "Sprites", "Dungeon")

    Public Sub Run()
        Dim atlas = SpriteAtlas.Load(Path.Combine(SpriteDir, "atlas.json"))
        For Each name In SpriteAtlas.RequiredNames
            Check(atlas.Has(name), "Atlas is missing required sprite " & name)
        Next
        Dim maze = MazeManager.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Mazes", "Level4.txt"))
        For Each item In maze.Dungeon.Decor
            Check(atlas.Has(item.Sprite), "Atlas is missing shipped Keep decor sprite " & item.Sprite)
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
        Using sheet As New System.Drawing.Bitmap(Path.Combine(SpriteDir, "extra.png")), dun As New System.Drawing.Bitmap(Path.Combine(SpriteDir, "dungeon.png"))
            Dim top = atlas("wall_top"), floor = atlas("floor_0")
            Check(AvgLuma(If(top.Sheet = "extra", sheet, dun), top) < AvgLuma(dun, floor) - 20, "wall_top is clearly darker than the floor")
        End Using
        Dim atlasForWalls = SpriteAtlas.Load(Path.Combine(SpriteDir, "atlas.json"))
        For mask As Integer = 0 To 255
            Dim m = mask
            ' bits: 0 N, 1 E, 2 S, 3 W, 4 NE, 5 NW, 6 SE, 7 SW; centre (1,1) is the wall being tiled.
            Dim isWall As Func(Of Integer, Integer, Boolean) =
                Function(r, c)
                    If r = 1 AndAlso c = 1 Then Return True
                    Dim bit = If(r = 0 AndAlso c = 1, 0, If(r = 1 AndAlso c = 2, 1, If(r = 2 AndAlso c = 1, 2, If(r = 1 AndAlso c = 0, 3,
                              If(r = 0 AndAlso c = 2, 4, If(r = 0 AndAlso c = 0, 5, If(r = 2 AndAlso c = 2, 6, 7)))))))
                    Return (m >> bit And 1) = 1
                End Function
            Dim pieces = Rendering.WallAutotile.Pieces(isWall, 1, 1)
            Check(pieces.Count >= 1, "Every wall gets at least one piece")
            For Each p In pieces
                Check(atlasForWalls.Has(p), "Auto-tile piece missing from atlas: " & p)
            Next
        Next
        Dim alone = Rendering.WallAutotile.Pieces(Function(r, c) r = 1 AndAlso c = 1, 1, 1)
        Check(alone(0) = "wall_face" AndAlso alone.Contains("wall_edge_n") AndAlso alone.Contains("wall_edge_e") AndAlso alone.Contains("wall_edge_w"), "A lone pillar shows its face and three rims")
        Dim buried = Rendering.WallAutotile.Pieces(Function(r, c) True, 1, 1)
        Check(buried.Count = 1 AndAlso buried(0) = "wall_top", "A wall surrounded by walls is plain top")
        Dim innerCorner = Rendering.WallAutotile.Pieces(Function(r, c) Not (r = 0 AndAlso c = 2), 1, 1)
        Check(innerCorner.SequenceEqual({"wall_top", "wall_inner_ne"}), "Diagonal-only floor yields one inner corner")
    End Sub

    Private Function AvgLuma(sheet As System.Drawing.Bitmap, s As AtlasSprite) As Double
        Dim sum As Double, n As Integer
        For y As Integer = s.Y To s.Y + s.H - 1
            For x As Integer = s.X To s.X + s.W - 1
                Dim p = sheet.GetPixel(x, y)
                If p.A > 0 Then sum += p.R * 0.3 + p.G * 0.59 + p.B * 0.11 : n += 1
            Next
        Next
        Return sum / Math.Max(1, n)
    End Function
End Module
