"""One-shot maze generator for Gravity Maze level files.

Builds a perfect maze (recursive backtracker) on an odd-sized tile grid,
opens ice rinks on the solution path (each rink gets one extra opening = one loop),
turns straight solution runs that end in a turn into ice corridors,
and searches seeds until every start->goal route must cross ice.

Level 3 (build_neon) uses straight-biased corridors, hole plazas and boost strips
that end at a corner with an overshoot pit.

Usage:  python tools/gen_maze.py level2 > GravityMaze/Mazes/Level2.txt
        python tools/gen_maze.py level3 [first_seed] > GravityMaze/Mazes/Level3.txt
The output is committed; the game never runs this script.
"""
import random
import sys
from collections import deque

DIRS = [(0, 1), (1, 0), (0, -1), (-1, 0)]


def carve(size, rng, straight=0.0):
    """straight = chance to keep going the same way (long 'highway' corridors)."""
    g = [["1"] * size for _ in range(size)]
    cells = (size - 1) // 2
    stack = [(rng.randrange(cells), rng.randrange(cells))]
    seen = {stack[0]}
    g[2 * stack[0][0] + 1][2 * stack[0][1] + 1] = "0"
    last = None
    while stack:
        r, c = stack[-1]
        opts = [(dr, dc) for dr, dc in DIRS
                if 0 <= r + dr < cells and 0 <= c + dc < cells and (r + dr, c + dc) not in seen]
        if not opts:
            stack.pop()
            last = None
            continue
        if straight and last in opts and rng.random() < straight:
            dr, dc = last
        else:
            dr, dc = rng.choice(opts)
        last = (dr, dc)
        nr, nc = r + dr, c + dc
        g[2 * r + 1 + dr][2 * c + 1 + dc] = "0"
        g[2 * nr + 1][2 * nc + 1] = "0"
        seen.add((nr, nc))
        stack.append((nr, nc))
    return g


def bfs(g, start, goal, passable=lambda t: t not in "1H"):
    n = len(g)
    prev = {start: None}
    q = deque([start])
    while q:
        cur = q.popleft()
        if cur == goal:
            break
        for dr, dc in DIRS:
            nr, nc = cur[0] + dr, cur[1] + dc
            if 0 <= nr < n and 0 <= nc < n and (nr, nc) not in prev and passable(g[nr][nc]):
                prev[(nr, nc)] = cur
                q.append((nr, nc))
    if goal not in prev:
        return None
    path, p = [], goal
    while p:
        path.append(p)
        p = prev[p]
    return path[::-1]


def straight_runs(path):
    """Yield (i, j) index ranges of maximal same-direction runs on the path."""
    i = 0
    while i < len(path) - 1:
        d = (path[i + 1][0] - path[i][0], path[i + 1][1] - path[i][1])
        j = i + 1
        while j < len(path) - 1 and (path[j + 1][0] - path[j][0], path[j + 1][1] - path[j][1]) == d:
            j += 1
        yield i, j
        i = j


def build(size, seed, rinks, ice_runs, run_len):
    rng = random.Random(seed)
    g = carve(size, rng)
    mid = size // 2 | 1  # odd row near the middle
    start, goal = (mid - 2 * rng.randrange(2), 0), (mid + 2 * rng.randrange(2), size - 1)
    g[start[0]][0], g[goal[0]][size - 1] = "S", "G"

    path = bfs(g, start, goal)
    cells = (size - 1) // 2
    # Rinks: 3x3-tile ice rooms (2x2 cells) centred on solution cells, away from edges and each other.
    candidates = [(r, c) for r, c in path if r % 2 == 1 and c % 2 == 1
                  and 3 <= r <= size - 6 and 3 <= c <= size - 6]
    rng.shuffle(candidates)
    placed = []
    for r, c in candidates:
        if len(placed) == rinks:
            break
        if any(abs(r - pr) < 6 and abs(c - pc) < 6 for pr, pc in placed):
            continue
        placed.append((r, c))
        for rr in range(r, r + 3):
            for cc in range(c, c + 3):
                g[rr][cc] = "I"
        # One extra opening on the rink boundary -> one loop through the rink.
        walls = [(rr, cc) for rr, cc in
                 [(r - 1, c + 1), (r + 3, c + 1), (r + 1, c - 1), (r + 1, c + 3)]
                 if g[rr][cc] == "1" and 0 < rr < size - 1 and 0 < cc < size - 1]
        if walls:
            wr, wc = rng.choice(walls)
            g[wr][wc] = "0"
    if len(placed) < rinks:
        return None

    # Ice corridors: straight solution runs that end in a turn.
    path = bfs(g, start, goal)
    runs = [(i, j) for i, j in straight_runs(path) if j - i >= run_len and j < len(path) - 1
            and all(g[r][c] == "0" for r, c in path[i + 1:j])]
    rng.shuffle(runs)
    for i, j in runs[:ice_runs]:
        for r, c in path[i + 1:j]:  # the corner tile path[j] stays grippy
            g[r][c] = "I"
    if len(runs) < ice_runs:
        return None

    if bfs(g, start, goal, lambda t: t not in "1I") is not None:
        return None  # ice can be bypassed
    path = set(bfs(g, start, goal))
    for r, c in placed:  # the route must actually cross every rink
        if sum((rr, cc) in path for rr in range(r, r + 3) for cc in range(c, c + 3)) < 3:
            return None
    return g, len(path)


# Hole patterns inside a 5x5 plaza (offsets 1..3 only, so the outer ring always stays open).
HOLE_PATTERNS = [
    [(1, 1), (1, 3), (3, 1), (3, 3)],          # four pits: weave through the cross
    [(2, 2), (1, 3), (3, 1)],                  # diagonal slalom
    [(2, 2), (1, 1), (3, 3)],                  # the other diagonal
    [(1, 2), (2, 1), (2, 3), (3, 2)],          # diamond: hug the ring or thread the centre
]


def build_neon(size, seed, plazas, boosts, run_len):
    """Level 3: straight 'highway' maze, boost strips that end at a corner with an
    overshoot pit straight ahead, and open plazas studded with holes."""
    rng = random.Random(seed)
    g = carve(size, rng, straight=0.45)
    third = (size // 3) | 1
    start, goal = (third, 0), (size - 1 - third, size - 1)
    g[start[0]][0], g[goal[0]][size - 1] = "S", "G"

    path = bfs(g, start, goal)
    candidates = [(r, c) for r, c in path if r % 2 == 1 and c % 2 == 1
                  and 3 <= r <= size - 8 and 3 <= c <= size - 8]
    rng.shuffle(candidates)
    placed = []
    plaza_tiles = set()
    for r, c in candidates:
        if len(placed) == plazas:
            break
        if any(abs(r - pr) < 8 and abs(c - pc) < 8 for pr, pc in placed):
            continue
        placed.append((r, c))
        for rr in range(r - 1, r + 6):
            for cc in range(c - 1, c + 6):
                plaza_tiles.add((rr, cc))
                if rr in range(r, r + 5) and cc in range(c, c + 5):
                    g[rr][cc] = "0"
        for dr, dc in rng.choice(HOLE_PATTERNS):
            g[r + dr][c + dc] = "H"
        walls = [(rr, cc) for rr, cc in
                 [(r - 1, c + 2), (r + 5, c + 2), (r + 2, c - 1), (r + 2, c + 5)]
                 if g[rr][cc] == "1" and 0 < rr < size - 1 and 0 < cc < size - 1]
        if walls:
            wr, wc = rng.choice(walls)
            g[wr][wc] = "0"
    if len(placed) < plazas:
        return None

    # Boost strips: straight route runs ending at a corner; a pit waits straight past the corner.
    path = bfs(g, start, goal)
    runs = []
    for i, j in straight_runs(path):
        if j - i < run_len or j >= len(path) - 1:
            continue
        if not all(g[r][c] == "0" and (r, c) not in plaza_tiles for r, c in path[i + 1:j + 1]):
            continue
        dr, dc = path[j][0] - path[j - 1][0], path[j][1] - path[j - 1][1]
        pr, pc = path[j][0] + dr, path[j][1] + dc
        if 0 < pr < size - 1 and 0 < pc < size - 1 and g[pr][pc] == "1":
            runs.append((i, j, pr, pc))
    rng.shuffle(runs)
    if len(runs) < boosts:
        return None
    for i, j, pr, pc in runs[:boosts]:
        for r, c in path[max(i + 1, j - 7):j - 1]:  # max 6 boost tiles; one grippy tile before the corner
            g[r][c] = "F"
        g[pr][pc] = "H"

    path = bfs(g, start, goal)
    if path is None:
        return None
    on_route = set(path)
    for r, c in placed:  # route must cross every plaza
        if sum((rr, cc) in on_route for rr in range(r, r + 5) for cc in range(c, c + 5)) < 4:
            return None
    return g, len(path)


PRESETS = {
    # name: (size, rinks, ice_runs, run_len, min_path, max_path)
    "level2": (21, 3, 2, 5, 90, 140),
    # name: (size, plazas, boosts, run_len, min_path, max_path)
    "level3": (25, 2, 4, 6, 120, 190),
}

if __name__ == "__main__":
    size, a, b, run_len, lo, hi = PRESETS[sys.argv[1]]
    make = build_neon if sys.argv[1] == "level3" else build
    for seed in range(int(sys.argv[2]) if len(sys.argv) > 2 else 0, 10000):
        res = make(size, seed, a, b, run_len)
        if res and lo <= res[1] <= hi:
            g, plen = res
            print("\n".join("".join(row) for row in g))
            print(f"seed={seed} path={plen}", file=sys.stderr)
            break
