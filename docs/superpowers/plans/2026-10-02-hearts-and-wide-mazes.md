# Hearts, Pickups and Wider Mazes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the player 3 hearts per level. Walls and pits cost hearts, heart pickups refill them, running out shows an OUT OF HEARTS screen, and corridors become 2 tiles wide so the rule is fair.

**Architecture:** A one-shot Python script widens the committed maze files and places `L` pickup tiles. `GameEngine` owns heart state, damage, invulnerability and pickups, and raises events. `GameShell` and `MazeRenderer` react to those events for the HUD, effects and the new fail screen, the same way they already react to `WallImpacted` / `BallFell`.

**Tech Stack:** VB.NET WinForms on .NET 10 (`net10.0-windows`), GDI+ drawing, Python 3 for tools. Tests are the console app `tools/VerificationRunner`, which uses `Check(cond, msg)` and throws on failure.

**Spec:** `docs/superpowers/specs/2026-10-02-hearts-and-wide-mazes-design.md` (read it first)

## Global Constraints

- Every VB file starts with `Option Strict On` / `Option Explicit On`; match the surrounding style (4-space indent, `' ──` section comments, short comments).
- `Engine/` must never reference UI, rendering or input (see `Gravity_Maze_AGENTS.md`).
- Hearts: `MaxHearts = 3`, `InvulnerableMs = 1000`, pickup radius `0.45` tiles, pickup counts L1=1, L2=2, L3=3.
- Widened sizes: L1 14x14, L2 31x31, L3 37x37.
- Maze files stay LF line endings, one row per line.
- Sprites are copied to the build output; a missing sprite must not crash the game.
- Run all checks with: `dotnet run --project tools/VerificationRunner` from the repo root. It must end with `ALL VERIFICATIONS PASSED SUCCESSFULLY!`. Also `dotnet build GravityMaze/GravityMaze.vbproj` must have 0 errors.
- Work on branch `feature/hearts` (create it from `main`). Commit after each task. End every commit message with:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
- Do not push.

## Review Focus

1. **Holding the ball against a wall:** a player who keeps tilting into a wall must lose only one heart, not one per second. Contact is edge-triggered on the `_inContact*` flags (Task 2 test "pinned for 1.5 s").
2. **Sliding along the seam of a 2x2 pit:** the old rule (centre within 0.4 of one hole centre) lets a ball pass between two hole tiles. The new rule must catch it (Task 1 test "seam crossing").
3. **The hit that takes the last heart on the same tick as reaching the goal:** the state must stay `OutOfHearts` and must not flip to `LevelComplete` (Task 2 early return after movement).
4. **Attract demo and keyboard bot:** they must never run out of hearts or show hit effects (`HeartsEnabled = False`, Task 2 test).
5. **Retry after OUT OF HEARTS:** full hearts, pickups visible again, attempt +1, and the HUD drain animation reset (Task 2 `Reset` test, Task 4 UI tour).

---

### Task 1: Widen the mazes, add the `L` tile, fix 2-wide pits and boost arrows

**Files:**
- Create: `tools/widen_maze.py`
- Create: `tools/maze_src/Level1.txt`, `Level2.txt`, `Level3.txt` (`git mv` the current `GravityMaze/Mazes/LevelN.txt` here, unchanged)
- Modify: `GravityMaze/Mazes/Level{1,2,3}.txt` (generated)
- Modify: `GravityMaze/Levels/MazeDefinition.vb` (valid symbols `"10SGIMFH"` → `"10SGIMFHL"`)
- Modify: `GravityMaze/Engine/GameEngine.vb` (hole check)
- Modify: `GravityMaze/Rendering/MazeRenderer.vb` (`L` tile drawn as floor; `BoostAngle` direction for 2-wide strips)
- Modify: `GravityMaze/UI/Screens/AttractPilot.vb` and `tools/VerificationRunner/Program.vb` `FindShortestPath` if they treat unknown tiles as walls (`L` is floor)
- Test: `tools/VerificationRunner/Program.vb`

**Interfaces:**
- Produces: maze files containing `L`; `MazeDefinition` accepts `L`; `GameEngine` hole rule works for multi-tile pits.

- [ ] **Step 1: Create the branch and move the source mazes**

```bash
git checkout -b feature/hearts
mkdir -p tools/maze_src
git mv GravityMaze/Mazes/Level1.txt tools/maze_src/Level1.txt
git mv GravityMaze/Mazes/Level2.txt tools/maze_src/Level2.txt
git mv GravityMaze/Mazes/Level3.txt tools/maze_src/Level3.txt
```

- [ ] **Step 2: Write `tools/widen_maze.py`**

```python
"""One-shot: widen the 1-wide source mazes to 2-wide corridors and place heart pickups (L).

Rule: every odd row/column index strictly inside the border is duplicated, so corridor
lines become 2 wide and wall lines stay 1 thick. S, G and L stay single tiles; their
duplicate becomes floor (0), or wall (1) on the outer edge.

Usage:  python tools/widen_maze.py      (reads tools/maze_src, writes GravityMaze/Mazes)
The output is committed; the game never runs this script.
"""
from collections import deque
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "tools" / "maze_src"
OUT = ROOT / "GravityMaze" / "Mazes"
PICKUPS = {1: 1, 2: 2, 3: 3}
DIRS = [(0, 1), (1, 0), (0, -1), (-1, 0)]


def passable(t):
    return t not in "1H"


def find(g, ch):
    return next((r, c) for r, row in enumerate(g) for c, t in enumerate(row) if t == ch)


def bfs(g, starts):
    dist = {s: 0 for s in starts}
    prev = {}
    q = deque(starts)
    while q:
        r, c = q.popleft()
        for dr, dc in DIRS:
            n = (r + dr, c + dc)
            if 0 <= n[0] < len(g) and 0 <= n[1] < len(g[0]) and n not in dist and passable(g[n[0]][n[1]]):
                dist[n] = dist[(r, c)] + 1
                prev[n] = (r, c)
                q.append(n)
    return dist, prev


def place_pickups(g, count):
    s, goal = find(g, "S"), find(g, "G")
    _, prev = bfs(g, [s])
    path = [goal]
    while path[-1] != s:
        path.append(prev[path[-1]])
    on_path = set(path)
    detour, _ = bfs(g, path)  # distance from the nearest solution-path tile

    def open_neighbours(r, c):
        return sum(1 for dr, dc in DIRS
                   if 0 <= r + dr < len(g) and 0 <= c + dc < len(g[0]) and g[r + dr][c + dc] != "1")

    floor = [p for p in detour if p not in on_path and g[p[0]][p[1]] == "0"]
    dead_ends = [p for p in floor if open_neighbours(*p) == 1]
    pool = sorted(dead_ends if len(dead_ends) >= count else floor, key=lambda p: (detour[p], p))
    for i in range(count):  # spread evenly across the short-to-long detour ranking
        r, c = pool[min(len(pool) - 1, int((i + 0.5) * len(pool) / count))]
        g[r][c] = "L"


def widen(g):
    rows_n, cols_n = len(g), len(g[0])
    rows = [i for i in range(rows_n) for _ in range(2 if i % 2 == 1 and 0 < i < rows_n - 1 else 1)]
    cols = [j for j in range(cols_n) for _ in range(2 if j % 2 == 1 and 0 < j < cols_n - 1 else 1)]
    seen = set()
    out = []
    for oi, i in enumerate(rows):
        line = []
        for oj, j in enumerate(cols):
            t = g[i][j]
            if t in "SGL":
                if (i, j) in seen:
                    edge = oi in (0, len(rows) - 1) or oj in (0, len(cols) - 1)
                    t = "1" if edge else "0"
                else:
                    seen.add((i, j))
            line.append(t)
        out.append("".join(line))
    return out


def main():
    for n, count in PICKUPS.items():
        g = [list(line) for line in (SRC / f"Level{n}.txt").read_text().split()]
        place_pickups(g, count)
        wide = widen(g)
        (OUT / f"Level{n}.txt").write_text("\n".join(wide) + "\n", newline="\n")
        print(f"Level{n}: {len(wide)}x{len(wide[0])}, {sum(r.count('L') for r in wide)} pickups")


if __name__ == "__main__":
    main()
```

- [ ] **Step 3: Run it and inspect**

Run: `python tools/widen_maze.py`
Expected:
```
Level1: 14x14, 1 pickups
Level2: 31x31, 2 pickups
Level3: 37x37, 3 pickups
```
Open the three files and check by eye that corridors are 2 wide, the walls are 1 thick, there is a single S and a single G, and the border is all `1` apart from S/G.

- [ ] **Step 4: Update the existing structure tests and add the widened-maze checks (they fail until Step 5)**

In `tools/VerificationRunner/Program.vb`:
- `TestPhysicalClearanceAndCorners`: change the expected size from 21x21 to 31x31. 2-wide dead ends have 2 open neighbours, so run the dead-end count on the source grid: `MazeManager.LoadFromFile(Path.Combine(ProjectRoot, "tools\maze_src\Level2.txt"))`.
- Add `TestWidenedMazes(maze1, maze2, maze3)`, called in `Main` right after loading:

```vb
        Private Sub TestWidenedMazes(m1 As MazeDefinition, m2 As MazeDefinition, m3 As MazeDefinition)
            Console.WriteLine("[TEST] Widened mazes + heart pickups...")
            Dim expected = {(m1, 14, 1), (m2, 31, 2), (m3, 37, 3)}
            For Each e In expected
                Dim m As MazeDefinition = e.Item1
                Check(m.RowCount = e.Item2 AndAlso m.ColumnCount = e.Item2, $"size {m.RowCount}x{m.ColumnCount}, want {e.Item2}")
                Dim pickups As Integer = 0
                For r As Integer = 0 To m.RowCount - 1
                    For c As Integer = 0 To m.ColumnCount - 1
                        If m.GetTile(r, c) = "L"c Then pickups += 1
                    Next
                Next
                Check(pickups = e.Item3, $"{e.Item2}x{e.Item2}: {pickups} pickups, want {e.Item3}")
                Check(Flood(m, Function(t) t <> "1"c AndAlso t <> "H"c).Contains(New Point(m.GoalColumn, m.GoalRow)),
                      "goal unreachable without crossing a pit")
            Next
            Console.WriteLine("  -> 14/31/37 grids, 1/2/3 pickups, goal reachable.")
        End Sub
```

- Add `TestWidePitSeam`, called in `Main`. A 2-wide corridor with a 2x2 pit; the ball is pressed into the bottom-left so its centre runs near the seam rows:

```vb
        Private Sub TestWidePitSeam()
            Console.WriteLine("[TEST] 2x2 pit catches a ball crossing its seam...")
            ' Rows 1-2 form a 2-wide corridor; the pit is columns 4-5 of both rows.
            Dim maze As New MazeDefinition({"111111111", "1S00HH0G1", "10000H001", "111111111"})
            For Each ty As Single In {0.0F, 1.0F, -1.0F}
                Dim engine As New GameEngine(maze, 0) With {.HeartsEnabled = False}
                Dim fell As Boolean = False
                AddHandler engine.BallFell, Sub(s As Object, e As BallFellEventArgs) fell = True
                For i As Integer = 1 To 200
                    engine.Update(1.0F, ty)
                    If fell Then Exit For
                Next
                Check(fell, $"ball crossed the pit without falling (tiltY={ty})")
            Next
            Console.WriteLine("  -> pit catches the ball on the top row, the bottom row and the seam.")
        End Sub
```
Note: this maze is irregular (row 2 has an H only in column 5) on purpose. With tilt-down the ball runs along row 2 at y≈2.73 and has to cross column 5; with tilt 0 it runs at y=1.5. Keep the test as written, because it exercises the neighbour logic.

`HeartsEnabled` doesn't exist until Task 2. For now, write the test without `With {.HeartsEnabled = False}`, and add it back in Task 2.

- [ ] **Step 5: Implement**

`MazeDefinition.vb`: change `"10SGIMFH"` to `"10SGIMFHL"`.

`GameEngine.vb`, step 7 (holes): replace the circle test with a rule that works for pits spanning several tiles. The ball falls when its centre's tile is `H` and, on each side whose neighbour is **not** `H`, the centre is at least `0.5 - HoleRadius` (0.1) inside the tile:

```vb
            ' 7. Holes — the centre must be on an H tile and clear of any edge that borders solid floor,
            ' so multi-tile pits have no safe seams. Timer keeps running; attempts do not change.
            Dim hRow As Integer = CInt(Math.Floor(_ballY))
            Dim hCol As Integer = CInt(Math.Floor(_ballX))
            If OverHole(hRow, hCol) Then
                ' ... existing BallFell / respawn body, unchanged ...
            End If
```

```vb
        Private Function IsHole(r As Integer, c As Integer) As Boolean
            Return r >= 0 AndAlso r < _maze.RowCount AndAlso c >= 0 AndAlso c < _maze.ColumnCount AndAlso _maze.GetTile(r, c) = "H"c
        End Function

        Private Function OverHole(r As Integer, c As Integer) As Boolean
            If Not IsHole(r, c) Then Return False
            Dim m As Single = 0.5F - HoleRadius
            Dim fx As Single = _ballX - c
            Dim fy As Single = _ballY - r
            If fx < m AndAlso Not IsHole(r, c - 1) Then Return False
            If fx > 1.0F - m AndAlso Not IsHole(r, c + 1) Then Return False
            If fy < m AndAlso Not IsHole(r - 1, c) Then Return False
            If fy > 1.0F - m AndAlso Not IsHole(r + 1, c) Then Return False
            Return True
        End Function
```
The `BallFell` event still passes the tile centre `(hCol + 0.5F, hRow + 0.5F)`.

`MazeRenderer.vb`:
- In the tile `Select Case` (around line 107), `L` falls through to normal floor drawing. Make sure no `Case Else` paints it as something else. The heart itself is drawn in Task 3.
- `BoostAngle`: a 2-wide strip has `F` neighbours on both axes. Decide the direction by run length instead:

```vb
            Dim runH As Integer = RunLength(maze, r, c, 0, 1) + RunLength(maze, r, c, 0, -1)
            Dim runV As Integer = RunLength(maze, r, c, 1, 0) + RunLength(maze, r, c, -1, 0)
            Dim horizontal As Boolean = runH >= runV
```
```vb
        Private Shared Function RunLength(maze As MazeDefinition, r As Integer, c As Integer, dr As Integer, dc As Integer) As Integer
            Dim n As Integer = 0
            r += dr : c += dc
            While r >= 0 AndAlso c >= 0 AndAlso r < maze.RowCount AndAlso c < maze.ColumnCount AndAlso maze.GetTile(r, c) = "F"c
                n += 1
                r += dr : c += dc
            End While
            Return n
        End Function
```

`AttractPilot.ShortestPath` and the runner's `FindShortestPath`: if either one treats anything other than an explicit list of floor tiles as blocked, make sure `L` counts as passable.

- [ ] **Step 6: Run everything**

Run: `dotnet run --project tools/VerificationRunner`
Expected: ends with `ALL VERIFICATIONS PASSED SUCCESSFULLY!`. Hard-coded coordinates or sizes in `TestLevel3`, `TestIceFrictionReductionAndRestoration`, `TestResizingPreservesAlignment`, `TestGoalCompletion` and the screenshot code may still assume the old grids. Fix each one by updating its numbers to the widened layout (`new = old index` mapped through the widening rule), not by deleting the check. The Level 2 bot time window (20–60 s) may need widening to 20–80 s; print the measured value either way. Look at `screenshots/` to confirm the boost chevrons point along the strips.

- [ ] **Step 7: Commit**

```bash
git add tools/widen_maze.py tools/maze_src GravityMaze/Mazes GravityMaze/Levels/MazeDefinition.vb GravityMaze/Engine/GameEngine.vb GravityMaze/Rendering/MazeRenderer.vb GravityMaze/UI/Screens/AttractPilot.vb tools/VerificationRunner
git commit -m "Widen mazes to 2-wide corridors and add heart pickup tiles"
```

---

### Task 2: Hearts in the engine

**Files:**
- Modify: `GravityMaze/Engine/GameState.vb`
- Modify: `GravityMaze/Engine/GameEngine.vb`
- Modify: `GravityMaze/UI/Screens/GameShell.vb` (attract engine: `HeartsEnabled = False`)
- Modify: `GravityMaze/UI/Screens/GameShell.vb` `TickPlaying` (minimal: treat `OutOfHearts` like `TimeUp` for now, going to `ShellScreen.TimeUp`; Task 4 swaps it for the real screen)
- Test: `tools/VerificationRunner/Program.vb` (`KeyboardBotRun` engine gets `HeartsEnabled = False`; new `TestHearts`)

**Interfaces:**
- Produces (used by Tasks 3–4):
  - `GameState.OutOfHearts`
  - `GameEngine.MaxHearts As Integer = 3` (Const), `GameEngine.InvulnerableMs As Single = 1000.0F` (Const)
  - `GameEngine.HeartsEnabled As Boolean` (property, default True)
  - `GameEngine.Hearts As Integer` (ReadOnly), `GameEngine.IsInvulnerable As Boolean` (ReadOnly)
  - `GameEngine.IsPickupTaken(row As Integer, col As Integer) As Boolean`
  - `Event HeartLost As EventHandler(Of HeartEventArgs)`, `Event HeartGained As EventHandler(Of HeartEventArgs)`
  - `HeartEventArgs` with ReadOnly `X As Single`, `Y As Single` (tile-space), `Hearts As Integer` (count after the change)

- [ ] **Step 1: Write the failing tests**

Add to `Program.vb` and call `TestHearts()` from `Main` after `TestCollisionAndImpact`:

```vb
        Private Sub TestHearts()
            Console.WriteLine("[TEST] Hearts: walls, pits, pickups, out of hearts...")
            Dim room As New MazeDefinition({"11111", "1S001", "10001", "100G1", "11111"})

            ' One contact = one heart; staying pinned costs nothing more, even after invulnerability ends.
            Dim e As New GameEngine(room, 0)
            Dim lost As Integer = 0
            AddHandler e.HeartLost, Sub(s As Object, a As HeartEventArgs) lost += 1
            Check(e.Hearts = GameEngine.MaxHearts, "starts with full hearts")
            For i As Integer = 1 To 94 : e.Update(-1.0F, 0.0F) : Next   ' ~1.5 s pinned to the left wall
            Check(e.Hearts = 2 AndAlso lost = 1, $"pinned for 1.5 s: hearts={e.Hearts}, events={lost}")
            Check(Not e.IsInvulnerable, "invulnerability ends after 1 s")

            ' Leave the wall, come back after invulnerability: one more heart.
            For i As Integer = 1 To 20 : e.Update(1.0F, 0.0F) : Next
            For i As Integer = 1 To 40 : e.Update(-1.0F, 0.0F) : Next
            Check(e.Hearts = 1, $"second contact: hearts={e.Hearts}")

            ' Contact during invulnerability is free.
            Dim f As New GameEngine(room, 0)
            For i As Integer = 1 To 30 : f.Update(-1.0F, 0.0F) : Next
            For i As Integer = 1 To 8 : f.Update(1.0F, 0.0F) : Next
            For i As Integer = 1 To 10 : f.Update(-1.0F, 0.0F) : Next
            Check(f.Hearts = 2, $"re-hit inside 1 s must be free: hearts={f.Hearts}")

            ' Pits always cost a heart and respawn; 3 pits = out of hearts; Update is then a no-op.
            Dim pit As New MazeDefinition({"1111111", "1S0H0G1", "1111111"})
            Dim p As New GameEngine(pit, 0)
            Dim falls As Integer = 0
            AddHandler p.BallFell, Sub(s As Object, a As BallFellEventArgs) falls += 1
            For i As Integer = 1 To 600
                p.Update(1.0F, 0.0F)
                If p.State <> GameState.Playing Then Exit For
            Next
            Check(falls = 3 AndAlso p.Hearts = 0 AndAlso p.State = GameState.OutOfHearts, $"pits: falls={falls} hearts={p.Hearts} state={p.State}")
            Dim bx As Single = p.BallX
            p.Update(1.0F, 0.0F)
            Check(p.BallX = bx, "no movement after OutOfHearts")

            ' Reset restores hearts and state.
            p.Reset()
            Check(p.Hearts = GameEngine.MaxHearts AndAlso p.State = GameState.Playing AndAlso Not p.IsInvulnerable, "Reset restores hearts")

            ' Pickups: not taken at full hearts; taken at 2 hearts; restored by Reset.
            Dim pk As New MazeDefinition({"1111111", "1S0L001", "10000G1", "1111111"})
            Dim k As New GameEngine(pk, 0)
            For i As Integer = 1 To 36 : k.Update(1.0F, 0.0F) : Next      ' x≈3.9: just rolled over L at full hearts
            Check(Not k.IsPickupTaken(1, 3) AndAlso k.Hearts = 3, "pickup must stay at full hearts")
            Dim k2 As New GameEngine(pk, 0)
            Dim gained As Integer = 0
            AddHandler k2.HeartGained, Sub(s As Object, a As HeartEventArgs) gained += 1
            For i As Integer = 1 To 10 : k2.Update(-1.0F, 0.0F) : Next     ' wall hit -> 2 hearts
            For i As Integer = 1 To 40 : k2.Update(1.0F, 0.0F) : Next      ' x≈4.1: past L, short of the right wall
            Check(k2.Hearts = 3 AndAlso gained = 1 AndAlso k2.IsPickupTaken(1, 3), $"pickup: hearts={k2.Hearts} gained={gained}")
            k2.Reset()
            Check(Not k2.IsPickupTaken(1, 3), "Reset restores pickups")

            ' Hearts disabled: nothing ever changes.
            Dim d As New GameEngine(pit, 0) With {.HeartsEnabled = False}
            For i As Integer = 1 To 300 : d.Update(1.0F, 0.0F) : Next
            Check(d.Hearts = GameEngine.MaxHearts AndAlso d.State = GameState.Playing, "HeartsEnabled=False must not cost hearts")
            Console.WriteLine("  -> contact edge-trigger, 1 s invulnerability, pits, pickups, reset, disabled mode.")
        End Sub
```
Also set `With {.HeartsEnabled = False}` on the engine in `KeyboardBotRun`, and add it back to `TestWidePitSeam`.

- [ ] **Step 2: Run to confirm it fails**

Run: `dotnet run --project tools/VerificationRunner`
Expected: build error, because `HeartEventArgs` / `Hearts` / `HeartsEnabled` / `GameState.OutOfHearts` are not defined yet.

- [ ] **Step 3: Implement**

`GameState.vb`: add `OutOfHearts    ' Lost every heart before reaching the goal.`

`GameEngine.vb`:

```vb
        ' Hearts: any new wall contact or pit costs one; L tiles refill one.
        Public Const MaxHearts As Integer = 3
        Public Const InvulnerableMs As Single = 1000.0F
        Private Const PickupRadius As Single = 0.45F
        Public Property HeartsEnabled As Boolean = True      ' False for the attract demo and test bots

        Public Event HeartLost As EventHandler(Of HeartEventArgs)
        Public Event HeartGained As EventHandler(Of HeartEventArgs)

        Private _hearts As Integer = MaxHearts
        Private _invulnerableMs As Single
        Private ReadOnly _takenPickups As New HashSet(Of Integer)   ' row * ColumnCount + col
```
(add `Imports System.Collections.Generic`)

```vb
        Public ReadOnly Property Hearts As Integer
            Get
                Return _hearts
            End Get
        End Property

        Public ReadOnly Property IsInvulnerable As Boolean
            Get
                Return _invulnerableMs > 0.0F
            End Get
        End Property

        Public Function IsPickupTaken(row As Integer, col As Integer) As Boolean
            Return _takenPickups.Contains(row * _maze.ColumnCount + col)
        End Function

        ' Costs one heart unless invulnerable (pits pass ignoreInvulnerable).
        Private Sub TakeHit(x As Single, y As Single, ignoreInvulnerable As Boolean)
            If Not HeartsEnabled OrElse _state <> GameState.Playing Then Return
            If Not ignoreInvulnerable AndAlso _invulnerableMs > 0.0F Then Return
            _hearts -= 1
            _invulnerableMs = InvulnerableMs
            RaiseEvent HeartLost(Me, New HeartEventArgs(x, y, _hearts))
            If _hearts <= 0 Then
                _state = GameState.OutOfHearts
                _velocityX = 0.0F
                _velocityY = 0.0F
            End If
        End Sub
```

In `Update`:
- Right after `If _state <> GameState.Playing Then Return`: `If _invulnerableMs > 0.0F Then _invulnerableMs -= MsPerTick`
- In each of the four blocked branches, *before* the line that sets the contact flag to True, add a rising-edge hit. Example for the right side:
  ```vb
                        If Not _inContactRight Then TakeHit(contactX, _ballY, False)
                        _inContactRight = True
  ```
  Do the same for left `(contactX, _ballY)`, bottom `(_ballX, contactY)` and top `(_ballX, contactY)`.
- After step 5 (Y movement), before step 6: `If _state <> GameState.Playing Then Return`. This stops a final hit from being overwritten by the goal check.
- In step 7, after the respawn lines and before `Return`: `TakeHit(hCol + 0.5F, hRow + 0.5F, True)`
- New step between 7 and 8:
  ```vb
            ' 7b. Heart pickups — only when a heart is missing; a taken pickup stays gone until Reset.
            If HeartsEnabled AndAlso _hearts < MaxHearts AndAlso hRow >= 0 AndAlso hRow < _maze.RowCount AndAlso
               hCol >= 0 AndAlso hCol < _maze.ColumnCount AndAlso _maze.GetTile(hRow, hCol) = "L"c AndAlso
               Not IsPickupTaken(hRow, hCol) Then
                Dim pdx As Single = _ballX - (hCol + 0.5F)
                Dim pdy As Single = _ballY - (hRow + 0.5F)
                If pdx * pdx + pdy * pdy <= PickupRadius * PickupRadius Then
                    _takenPickups.Add(hRow * _maze.ColumnCount + hCol)
                    _hearts += 1
                    RaiseEvent HeartGained(Me, New HeartEventArgs(hCol + 0.5F, hRow + 0.5F, _hearts))
                End If
            End If
  ```
- `Reset()`: add `_hearts = MaxHearts`, `_invulnerableMs = 0.0F`, `_takenPickups.Clear()`.

At the bottom of the file, next to `BallFellEventArgs`:
```vb
    ' Where a heart was lost or gained (tile-space) and how many remain.
    Public NotInheritable Class HeartEventArgs
        Inherits EventArgs

        Public ReadOnly Property X As Single
        Public ReadOnly Property Y As Single
        Public ReadOnly Property Hearts As Integer

        Public Sub New(x As Single, y As Single, hearts As Integer)
            Me.X = x
            Me.Y = y
            Me.Hearts = hearts
        End Sub
    End Class
```

`GameShell.vb` `StartAttract`: `_attractEngine = New GameEngine(maze, 0) With {.HeartsEnabled = False}`.
`GameShell.vb` `TickPlaying`: change `If _engine.State = GameState.TimeUp Then` to `If _engine.State = GameState.TimeUp OrElse _engine.State = GameState.OutOfHearts Then`. This is temporary until Task 4.

- [ ] **Step 4: Run tests**

Run: `dotnet run --project tools/VerificationRunner`
Expected: `ALL VERIFICATIONS PASSED SUCCESSFULLY!`. If the hard-coded tick counts in `TestHearts` don't line up with the physics, for example if the ball needs more ticks to reach a wall, adjust the counts but not what the test asserts. If you change one, write down why in a short comment.

- [ ] **Step 5: Commit**

```bash
git add GravityMaze/Engine GravityMaze/UI/Screens/GameShell.vb tools/VerificationRunner/Program.vb
git commit -m "Engine: hearts, wall/pit damage, invulnerability and heart pickups"
```

---

### Task 3: Heart sprites, HUD hearts, pickups on the board, hit feedback

**Files:**
- Move: `hearts/*.png` → `GravityMaze/Sprites/` (the folder is currently untracked; move it, then `git add`)
- Modify: `GravityMaze/GravityMaze.vbproj` (copy `Sprites\**` to output)
- Create: `GravityMaze/UI/Sprites.vb` (cached loader)
- Modify: `GravityMaze/UI/Screens/GameShell.Draw.vb` (`DrawHud`)
- Modify: `GravityMaze/UI/Screens/GameShell.vb` (subscribe `HeartLost` / `HeartGained` in `StartLevel` / `DetachEngine`; feedback)
- Modify: `GravityMaze/UI/GameCanvas.vb`, `GravityMaze/Rendering/MazeRenderer.vb` (draw untaken pickups, blink the ball)
- Optional: `GravityMaze/Rendering/Particles.vb` (sparkle burst)

**Interfaces:**
- Consumes: everything Task 2 produces.
- Produces: `UI.Sprites.Heart As Image` (Nothing if the file is missing), `UI.Sprites.HeartFrame(index As Integer) As Image`. `GameCanvas.PickupTaken As Func(Of Integer, Integer, Boolean)`, `GameCanvas.BallBlink As Boolean`, `GameCanvas.AddHeartBurst(x As Single, y As Single)`.

- [ ] **Step 1: Assets and project file**

```bash
mkdir -p GravityMaze/Sprites
mv hearts/* GravityMaze/Sprites/ && rmdir hearts
```
In `GravityMaze.vbproj`, next to the `Fonts` items:
```xml
    <None Remove="Sprites\**" />
```
```xml
    <Content Include="Sprites\**">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
```

- [ ] **Step 2: `GravityMaze/UI/Sprites.vb`**

Open `heart_spritesheet_32x32.png` and look at it before choosing frame indices. It is a grid of 32x32 frames: row 0 goes from a full heart to progressively darker or emptier ones, and row 1 holds more full or half frames and a bright one. Set `DrainFrames` to the frame indices that read as "full → empty", in that order.

```vb
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO

Namespace UI
    ' Loads the heart sprites once from <output>\Sprites. Missing files return Nothing; callers draw a fallback.
    Public NotInheritable Class Sprites
        Private Shared _heart As Image
        Private Shared _frames As List(Of Image)
        Private Shared _loaded As Boolean

        ' Spritesheet frame indices (row-major, 32x32) from full to empty, for the lose-a-heart animation.
        Public Shared ReadOnly DrainFrames As Integer() = {0, 1, 2, 3, 4}

        Private Sub New()
        End Sub

        Public Shared ReadOnly Property Heart As Image
            Get
                EnsureLoaded()
                Return _heart
            End Get
        End Property

        Public Shared Function HeartFrame(index As Integer) As Image
            EnsureLoaded()
            If _frames Is Nothing OrElse index < 0 OrElse index >= _frames.Count Then Return Nothing
            Return _frames(index)
        End Function

        Private Shared Sub EnsureLoaded()
            If _loaded Then Return
            _loaded = True
            Dim dir As String = Path.Combine(AppContext.BaseDirectory, "Sprites")
            Try
                _heart = Image.FromFile(Path.Combine(dir, "heart_32x32.png"))
                Using sheet As Image = Image.FromFile(Path.Combine(dir, "heart_spritesheet_32x32.png"))
                    _frames = New List(Of Image)()
                    For y As Integer = 0 To sheet.Height - 32 Step 32
                        For x As Integer = 0 To sheet.Width - 32 Step 32
                            Dim f As New Bitmap(32, 32)
                            Using g As Graphics = Graphics.FromImage(f)
                                g.DrawImage(sheet, New Rectangle(0, 0, 32, 32), New Rectangle(x, y, 32, 32), GraphicsUnit.Pixel)
                            End Using
                            _frames.Add(f)
                        Next
                    Next
                End Using
            Catch
                ' ponytail: missing/corrupt sprite -> callers draw a plain red heart instead of crashing.
            End Try
        End Sub
    End Class
End Namespace
```
Pixel art must stay crisp: whoever draws it sets `g.InterpolationMode = InterpolationMode.NearestNeighbor` and `g.PixelOffsetMode = PixelOffsetMode.Half`, then restores the previous values.

- [ ] **Step 3: HUD hearts (`DrawHud`)**

Below the theme name on the left (around `y = 104 * s`), draw `GameEngine.MaxHearts` hearts, each `30 * s` px with `6 * s` gaps:
- index < `_engine.Hearts`: `Sprites.Heart`
- lost heart: while `_heartLostAgoMs < 400` and the index equals the heart just lost, step through `Sprites.DrainFrames`; otherwise draw `Sprites.Heart` with a 0.3 alpha `ImageAttributes` color matrix (dim)
- if `Sprites.Heart Is Nothing`: fill a red ellipse pair + triangle (any simple heart shape) instead

Track `_heartLostAgoMs` (Single, set to 0 in `OnHeartLost`, += `TickMs` in `Tick`, start at a large value such as 99999) and `_heartLostIndex` (= the hearts count after the loss) in `GameShell.vb`. Reset `_heartLostAgoMs` to 99999 in `BeginRun`.

- [ ] **Step 4: Engine events → feedback (`GameShell.vb`)**

In `StartLevel`, add `AddHandler _engine.HeartLost, AddressOf OnHeartLost` and `AddHandler _engine.HeartGained, AddressOf OnHeartGained`. Add the matching `RemoveHandler` lines in `DetachEngine`.

```vb
        Private Sub OnHeartLost(sender As Object, e As HeartEventArgs)
            _heartLostAgoMs = 0
            _heartLostIndex = e.Hearts
            _canvas.FlashDamage()
            _sound.Play("wall_hit", 1.0F)
        End Sub

        Private Sub OnHeartGained(sender As Object, e As HeartEventArgs)
            _canvas.AddHeartBurst(e.X, e.Y)
            _sound.Play("menu_confirm", 0.8F)
        End Sub
```
`GameCanvas.FlashDamage()` starts a 250 ms red overlay (alpha 90 → 0) and a 200 ms shake of at most 6 px on the board offset. Add both inside `GameCanvas` / `MazeRenderer`, following how `AddImpact` / `TriggerGoalCelebration` keep their timers.

Each tick in `GameShell.Tick`, where the ball position is pushed to the canvas, also set: `_canvas.BallBlink = _engine.IsInvulnerable AndAlso _screen = ShellScreen.Playing`. Set `_canvas.PickupTaken = AddressOf _engine.IsPickupTaken` whenever `_engine` changes, and set it to `Nothing` for the attract screens. With `Nothing`, every pickup is drawn.

- [ ] **Step 5: Pickups and blink in `MazeRenderer`**

Pass two new optional parameters to `Draw`: `Optional pickupTaken As Func(Of Integer, Integer, Boolean) = Nothing` and `Optional ballBlink As Boolean = False`. In `GameCanvas`'s paint call, pass `PickupTaken` / `BallBlink`.
- `L` tile: draw normal floor, then, if `pickupTaken Is Nothing OrElse Not pickupTaken(r, c)`, draw `Sprites.Heart`. Size: `tileSize * 0.6 * (1 + 0.08 * sin(t * 2π / 900ms))`. Offset it vertically by `tileSize * 0.06 * sin(t * 2π / 1400ms)`. Add a soft glow behind it with alpha 60. Take `t` from `Environment.TickCount64`, or from the clock the renderer already uses for its animations.
- `ballBlink`: skip drawing the ball on alternate 100 ms slices (`(t \ 100) Mod 2 = 0`).
- `AddHeartBurst`: about 14 pink or red particles fanning out over 500 ms, using `Particles` the same way `AddHoleFall` does.

- [ ] **Step 6: Verify**

Run: `dotnet build GravityMaze/GravityMaze.vbproj` (0 errors), then `dotnet run --project tools/VerificationRunner` (all passed).
In `GenerateScreenshots`, add a screenshot of Level 1 with the HUD showing 2 of 3 hearts and the pickup visible. Use whatever helper the existing HUD screenshots use. Open the PNG and check that the hearts are crisp and the dimmed heart is readable.

- [ ] **Step 7: Commit**

```bash
git add GravityMaze tools/VerificationRunner
git commit -m "Hearts HUD, heart pickups on the board, damage flash and blink"
```

---

### Task 4: OUT OF HEARTS screen, How To Play legend, UI tour

**Files:**
- Modify: `GravityMaze/UI/Screens/GameShell.vb` (enum value, menu, tick, keys, `GoTo_` reset, `TickPlaying`)
- Modify: `GravityMaze/UI/Screens/GameShell.Draw.vb` (`DrawOutOfHearts`, draw dispatch, How To Play legend)
- Modify: `tools/VerificationRunner/UiTour.vb` (screenshot of the new screen)

**Interfaces:**
- Consumes: `GameState.OutOfHearts`.
- Produces: `ShellScreen.OutOfHearts`.

- [ ] **Step 1: Screen flow (`GameShell.vb`)**
- Add `OutOfHearts` to `ShellScreen`, right after `TimeUp`.
- Add `Private ReadOnly _heartsMenu As New MenuList("RETRY", "MAIN MENU")`.
- Everywhere `ShellScreen.TimeUp` / `_timeUpMenu` is handled (menu lookup around line 129, `GoTo_` reset around line 258, `Tick` around line 188, `HandleKey` around line 585), add the matching `OutOfHearts` / `_heartsMenu` case with the same behaviour: RETRY → `Retry()`, MAIN MENU → `ToMainMenu()`, R → Retry, Esc → main menu, Enter/Space → the selected item, and a 600 ms delay before the menu takes input.
- `IsGameScreen()` (and any other list of screens that shows the maze or HUD) must include `OutOfHearts` wherever it includes `TimeUp`.
- `TickPlaying`: undo Task 2's temporary change and add:
  ```vb
            ElseIf _engine.State = GameState.OutOfHearts Then
                _sound.Play("time_up")
                GoTo_(ShellScreen.OutOfHearts)
  ```
  as its own branch next to the `TimeUp` one.

- [ ] **Step 2: Drawing (`GameShell.Draw.vb`)**
- In the draw dispatch: `Case ShellScreen.OutOfHearts : DrawHud(g, b, s, pal) : DrawOutOfHearts(g, b, s, pal)`
- `DrawOutOfHearts`: copy `DrawTimeUp`. Change the small label to `"NO HEARTS LEFT"`, the big title to `"OUT OF HEARTS"` (shrink the font from 170 to about 130 if it overflows the 840-wide panel), the line to `$"Attempt {_engine.Attempts}  ·  Tip: gentle tilts — walls cost hearts."`, and the menu to `_heartsMenu`. Draw three dimmed hearts (Task 3's dim style) above the title.
- How To Play: add a legend line to the controls list or the tiles legend:
  `("HEARTS", "Walls and pits cost a heart · grab floating hearts to refill")`. If the panel doesn't have room for one more row, shorten the text instead of moving the panels.

- [ ] **Step 3: UI tour**

In `UiTour.vb`, add a step that reaches `OutOfHearts` and saves `screenshots/ui/out_of_hearts.png`. Follow the existing TIME UP step: if it forces the screen or the engine state through a test hook, do the same for `OutOfHearts`. If there is no hook, drive a real engine into three pit falls using a small pit maze (see the pit test in `TestHearts`) and put the shell in that state the way the tour sets up its other screens.

- [ ] **Step 4: Verify**

Run: `dotnet run --project tools/VerificationRunner`
Expected: all passed, and `screenshots/ui/out_of_hearts.png` exists. Open it and check it matches the TIME UP screen's style, with nothing overlapping or cut off. Also check the How To Play screenshot.

- [ ] **Step 5: Commit**

```bash
git add GravityMaze/UI tools/VerificationRunner
git commit -m "OUT OF HEARTS screen, hearts legend and UI tour screenshot"
```

---

### Task 5: Re-tune par times and the Level 3 limit, update docs

**Files:**
- Modify: `GravityMaze/Form1.vb` (the `levels` list)
- Modify: `tools/VerificationRunner/Program.vb` (print bot times for all 3 levels; adjust the L2 time window only if needed)
- Modify: `SETUP.md` (section 4: one line about hearts), `tickets.md` (add a done entry in the existing style)

- [ ] **Step 1: Measure**

Make the runner print the keyboard-bot clear time for each level, e.g. in `Main` after loading:
```vb
            For Each m In {maze1, maze2, maze3}
                Console.WriteLine($"  -> bot clear time {m.RowCount}x{m.ColumnCount}: {KeyboardBotRun(m) * 0.016F:F1}s")
            Next
```
Run: `dotnet run --project tools/VerificationRunner` and note the three times.

- [ ] **Step 2: Apply**

In `Form1.vb`, set each `LevelConfig` par to the measured time, rounded to 0.1 s. Set the Level 3 time limit to `Ceiling(1.4 * L3par)` whole seconds. Keep the comment above the list (`Par = keyboard-bot clear time ...; Level 3 limit = 1.4x its par.`).

- [ ] **Step 3: Docs**

`SETUP.md` section 4, step 4, add: "You have 3 hearts: any wall touch or pit costs one (then 1 s of safety). Floating hearts refill one." Add the done entry to `tickets.md` following its existing format.

- [ ] **Step 4: Full verification**

Run: `dotnet build GravityMaze/GravityMaze.vbproj` and `dotnet run --project tools/VerificationRunner`
Expected: 0 errors; `ALL VERIFICATIONS PASSED SUCCESSFULLY!`. Look through `screenshots/` and `screenshots/ui/` for the three levels, the HUD and the OUT OF HEARTS screen.

- [ ] **Step 5: Commit**

```bash
git add GravityMaze/Form1.vb tools/VerificationRunner SETUP.md tickets.md
git commit -m "Re-tune par times and Level 3 limit for the wider mazes"
```
