Option Strict On
Option Explicit On

Imports System
Imports System.IO
Imports System.Linq
Imports GravityMaze.Levels

Public Module LevelChecks
    Public Sub Run()
        Dim maze = MazeManager.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Mazes", "Level4.txt"))   ' runs Validate
        Check(maze.ColumnCount = 46 AndAlso maze.RowCount = 46, "Finale is 46x46")
        Dim tiles = Enumerable.Range(0, maze.RowCount).SelectMany(Function(r) Enumerable.Range(0, maze.ColumnCount).Select(Function(c) maze.GetTile(r, c))).ToList()
        Dim count = Function(ch As Char) tiles.LongCount(Function(t) t = ch)
        Check(count("T"c) >= 4 AndAlso count("C"c) >= 3 AndAlso count("W"c) >= 4 AndAlso count("L"c) >= 4 AndAlso count("c"c) >= 1, "Every wing carries its hazard and the vault gate exists")
        Dim d = maze.Dungeon
        Check(d.Blades.Count >= 3 AndAlso d.Launchers.Count >= 3 AndAlso d.Spikes.Count = count("T"c), "Armory blades and darts are authored")
        Check(d.Decor.Count >= 60 AndAlso d.Decor.Select(Function(x) x.Sprite).Distinct().Count() >= 20 AndAlso d.Lights.Count >= 12, "The keep is dressed with the asset packs")
        Check(d.Checkpoints.Where(Function(x) x.Escape).Count() = 3 AndAlso d.Checkpoints.Where(Function(x) Not x.Escape).Count() >= 2, "Checkpoints for both phases")
        Check(d.Collapse.CaveIns.Count >= 4 AndAlso d.Collapse.CaveIns.Count <= 6 AndAlso d.Collapse.Breakaway.Count >= 1, "Escape has cave-ins and a breakaway")
        Dim interior = (maze.RowCount - 2) * (maze.ColumnCount - 2)
        Dim open = Enumerable.Range(1, maze.RowCount - 2).Sum(Function(r) Enumerable.Range(1, maze.ColumnCount - 2).Count(Function(c) maze.GetTile(r, c) <> "1"c))
        Check(open / CDbl(interior) >= 0.45, "No solid filler: at least 45% of the interior is walkable")
    End Sub
End Module
