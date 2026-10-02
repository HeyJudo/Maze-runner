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
