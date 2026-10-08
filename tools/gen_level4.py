"""One-shot generator for the 46x46 Forgotten Keep (Level 4 finale).

Builds a 31x31 source maze on a 15x15 cell grid (Crypt west, Armory east, Grand Hall centre,
Treasury + vault north, lower galleries south), widens it with the Level 1-3 rule (every odd
interior row/column is duplicated, so corridors are 2 tiles wide and walls 1 thick), then adds
the hazards, cave-ins, checkpoints and decor the spec asks for. It asserts every placement rule
and searches seeds until all of them pass.

Source tiles: S G L K Q V c E and walls. Lava W, spikes T, crumble C and the escape-midpoint
heart are added after widening. S, G, L, K, Q and V stay single tiles when widening.

Usage:  python tools/gen_level4.py [first_seed]
Writes tools/maze_src/Level4.txt, GravityMaze/Mazes/Level4.txt and Level4.dungeon.json.
The output is committed; the game never runs this script.
"""
import json
import random
import sys
from collections import Counter, deque
from pathlib import Path

from gen_maze import DIRS

ROOT = Path(__file__).resolve().parent.parent
SRC_OUT = ROOT / "tools" / "maze_src" / "Level4.txt"
MAP_OUT = ROOT / "GravityMaze" / "Mazes" / "Level4.txt"
JSON_OUT = ROOT / "GravityMaze" / "Mazes" / "Level4.dungeon.json"

CELLS, SRC, SIZE = 15, 31, 46
SINGLE = "SGLKQV"
S_CELL, G_CELL = (7, 14), (8, 14)
BREAKAWAY = {(1, 15), (2, 15)}  # wall between vault cell (5,0) and crypt cell (4,0), both widened rows
BLADE_X = 1.0   # track offset from the corridor's left column: 1.0 = centreline between the two lanes
SEARCH = 500
SPIKE_OFFSETS = [0, 1500, 3000, 4500]
CHECK_NAMES = {1: "explore routes", 2: "escape length", 3: "cave-ins", 4: "checkpoints", 5: "traps",
               6: "hearts", 7: "decor", 8: "floor share"}


class Fail(Exception):
    def __init__(self, rule, msg):
        super().__init__(msg)
        self.rule = rule


def need(rule, ok, msg):
    if not ok:
        raise Fail(rule, msg)


def region(cx, cy):
    if cy <= 1:
        return "crypt" if cx <= 4 else "vault" if cx <= 9 else "armory"
    if cy <= 3:
        return "crypt" if cx <= 3 else "treasury" if cx <= 10 else "armory"
    if cy <= 10:
        return "crypt" if cx <= 5 else "hall" if cx <= 8 else "armory"
    return "gallery"


def tile_region(r, c):
    return region(min(c // 3, 14), min(r // 3, 14))


def cells_of(name):
    return {(cx, cy) for cx in range(CELLS) for cy in range(CELLS) if region(cx, cy) == name}


# ---------------------------------------------------------------- source maze (31x31)

def carve_cells(cells, rng, straight=0.0, weave=0.0):
    """Recursive backtracker over a set of cells (gen_maze.carve, but on any cell shape).
    Returns the passage list, or None when the set is not connected."""
    start = rng.choice(sorted(cells))
    stack, seen, edges, last = [start], {start}, [], None
    while stack:
        cx, cy = stack[-1]
        opts = [(dx, dy) for dy, dx in DIRS if (cx + dx, cy + dy) in cells and (cx + dx, cy + dy) not in seen]
        if not opts:
            stack.pop()
            last = None
            continue
        if weave and last in opts and len(opts) > 1 and rng.random() < weave:
            opts.remove(last)  # weave = chance to turn instead of running on (twisty small mazes)
        dx, dy = last if straight and last in opts and rng.random() < straight else rng.choice(opts)
        last = (dx, dy)
        n = (cx + dx, cy + dy)
        edges.append(((cx, cy), n))
        seen.add(n)
        stack.append(n)
    return edges if len(seen) == len(cells) else None


class Source:
    def __init__(self):
        self.g = [["1"] * SRC for _ in range(SRC)]
        for cx in range(CELLS):
            for cy in range(CELLS):
                self.g[2 * cy + 1][2 * cx + 1] = "0"

    def link(self, a, b):
        self.g[a[1] + b[1] + 1][a[0] + b[0] + 1] = "0"

    def rect(self, x0, y0, x1, y1):
        for r in range(2 * y0 + 1, 2 * y1 + 2):
            for c in range(2 * x0 + 1, 2 * x1 + 2):
                self.g[r][c] = "0"

    def put(self, cell, ch):
        self.g[2 * cell[1] + 1][2 * cell[0] + 1] = ch

    def open_dirs(self, cell):
        r, c = 2 * cell[1] + 1, 2 * cell[0] + 1
        return [(dr, dc) for dr, dc in DIRS if self.g[r + dr][c + dc] != "1"]


def neighbours(cell, cells):
    return [(cell[0] + dx, cell[1] + dy) for dy, dx in DIRS if (cell[0] + dx, cell[1] + dy) in cells]


def wing(src, rng, name, exclude, no_room, straight, loops):
    """Carve one wing around a 2x2 side room, add loops, return (maze cells, room cells, heart cell)."""
    cells = cells_of(name) - exclude
    blocks = [(x, y) for x, y in sorted(cells) if {(x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)} <= cells - no_room]
    rng.shuffle(blocks)
    for bx, by in blocks:
        room = {(bx, by), (bx + 1, by), (bx, by + 1), (bx + 1, by + 1)}
        rest = cells - room
        edges = carve_cells(rest, rng, straight)
        doors = [(b, n) for b in sorted(room) for n in neighbours(b, rest)]
        if edges is None or not doors:
            continue
        for a, b in edges:
            src.link(a, b)
        src.rect(bx, by, bx + 1, by + 1)
        b, n = rng.choice(doors)
        src.link(b, n)
        heart = (2 * bx + 1 - b[0], 2 * by + 1 - b[1])  # diagonal opposite of the door cell
        walls = [(a, c) for a in sorted(rest) for c in neighbours(a, rest)
                 if a < c and src.g[a[1] + c[1] + 1][a[0] + c[0] + 1] == "1"]
        rng.shuffle(walls)
        for a, c in walls[:loops]:
            src.link(a, c)
        return rest, room, heart
    raise Fail(5, f"no room for a side room in the {name}")


def build_source(rng):
    src = Source()
    # Vault room and Grand Hall are open rooms; the hall gets pillars and two nooks by the vault gate.
    src.rect(5, 0, 9, 1)
    src.rect(6, 4, 8, 10)
    for r in (12, 16, 20):
        for c in (14, 16):
            src.g[r][c] = "1"
    src.g[9][14] = src.g[9][16] = "1"
    # Doors between regions.
    cg = rng.sample(range(0, 6), 2)       # crypt <-> galleries
    ag = rng.sample(range(9, 15), 2)      # armory <-> galleries
    tv = rng.choice([6, 8])               # treasury -> vault room
    for cx in cg + ag:
        src.link((cx, 10), (cx, 11))
    src.link((5, 7), (6, 7))
    src.link((8, 7), (9, 7))
    src.link((7, 10), (7, 11))
    src.link((tv, 1), (tv, 2))
    src.g[8][15] = "c"
    no_room = {"crypt": {(5, 7), (4, 0), (4, 1)} | {(cx, 10) for cx in cg},
               "armory": {(9, 7), (10, 0), (10, 1)} | {(cx, 10) for cx in ag},
               "gallery": {(7, 11), (7, 13)} | {(cx, 11) for cx in cg + ag}}
    rooms = {}
    for name, straight, loops in (("crypt", 0.0, 2), ("armory", 0.5, 2), ("gallery", 0.0, 5)):
        rooms[name] = wing(src, rng, name, {S_CELL, G_CELL}, no_room[name], straight, loops)
    src.link(S_CELL, (7, 13))
    src.g[29][16] = "E"
    src.put(S_CELL, "S")
    src.put(G_CELL, "G")
    # Treasury
    t_edges = None
    while t_edges is None:
        t_edges = carve_cells(cells_of("treasury"), rng, weave=0.85)
    for a, b in t_edges:
        src.link(a, b)
    treasury = sorted(cells_of("treasury"))
    walls = [(a, c) for a in treasury for c in neighbours(a, set(treasury))
             if a < c and src.g[a[1] + c[1] + 1][a[0] + c[0] + 1] == "1"]
    rng.shuffle(walls)
    for a, c in walls[:1]:  # one loop
        src.link(a, c)
    # Hearts, keys, vault key.
    for name in ("crypt", "armory", "gallery"):
        src.put(rooms[name][2], "L")
    src.put((7, 0), "V")
    return src, rooms, cg, ag


def cell_graph_depth(src, start):
    """Cell-step depth from the start cell over open walls (the vault gate c counts as shut)."""
    depth, q = {start: 0}, deque([start])
    while q:
        cx, cy = q.popleft()
        for dr, dc in DIRS:
            n = (cx + dc, cy + dr)
            wall = src.g[2 * cy + 1 + dr][2 * cx + 1 + dc]
            if 0 <= n[0] < CELLS and 0 <= n[1] < CELLS and n not in depth and wall not in "1c":
                depth[n] = depth[(cx, cy)] + 1
                q.append(n)
    return depth


def place_keys(src, rooms):
    depth = cell_graph_depth(src, S_CELL)
    for name, ch, banned in (("crypt", "K", {(4, 0), (4, 1)}), ("armory", "Q", {(10, 0), (10, 1)})):
        dead = [c for c in cells_of(name) if c not in rooms[name][1] and c not in banned
                and len(src.open_dirs(c)) == 1 and src.g[2 * c[1] + 1][2 * c[0] + 1] == "0"]
        need(5, dead, f"no dead end for {ch}")
        src.put(max(sorted(dead), key=lambda c: depth[c]), ch)


# ---------------------------------------------------------------- widening and map helpers

def widen(g):
    """widen_maze.widen with the extra single-tile symbols (K, Q, V)."""
    rows_n, cols_n = len(g), len(g[0])
    rows = [i for i in range(rows_n) for _ in range(2 if i % 2 == 1 and 0 < i < rows_n - 1 else 1)]
    cols = [j for j in range(cols_n) for _ in range(2 if j % 2 == 1 and 0 < j < cols_n - 1 else 1)]
    seen, out = set(), []
    for oi, i in enumerate(rows):
        line = []
        for oj, j in enumerate(cols):
            t = g[i][j]
            if t in SINGLE:
                if (i, j) in seen:
                    t = "0"
                else:
                    seen.add((i, j))
            line.append(t)
        out.append(line)
    return out


def find(m, ch):
    return next((r, c) for r in range(SIZE) for c in range(SIZE) if m[r][c] == ch)


def esc_open(m, r, c):
    if not (0 <= r < SIZE and 0 <= c < SIZE):
        return False
    t = m[r][c]
    return (r, c) in BREAKAWAY if t == "1" else t not in "HWCc"


def bfs(m, start, ok, blocked=frozenset()):
    dist, prev, q = {start: 0}, {}, deque([start])
    while q:
        cur = q.popleft()
        for dr, dc in DIRS:
            n = (cur[0] + dr, cur[1] + dc)
            if n not in dist and n not in blocked and ok(n[0], n[1]):
                dist[n] = dist[cur] + 1
                prev[n] = cur
                q.append(n)
    return dist, prev


def route(prev, goal):
    path = [goal]
    while path[-1] in prev:
        path.append(prev[path[-1]])
    return path[::-1]


def explore_dist(m, start, gate_open):
    """Walking distances while exploring: C, W, H and walls block; c opens once both keys are held."""
    return bfs(m, start, lambda r, c: 0 <= r < SIZE and 0 <= c < SIZE and (m[r][c] not in "1CWHEc" or (gate_open and m[r][c] == "c")))


def explore_routes(m):
    s, k, q, v = (find(m, ch) for ch in "SKQV")
    ds, ps = explore_dist(m, s, False)
    dk, _ = explore_dist(m, k, False)
    dq, pq = explore_dist(m, q, False)
    dv, _ = explore_dist(m, v, True)
    for t in (k, q):
        need(1, t in ds, "key unreachable while exploring")
    need(1, v in explore_dist(m, q, True)[0], "vault key unreachable")
    kqv = ds[k] + dk[q] + explore_dist(m, q, True)[0][v]
    qkv = ds[q] + dq[k] + explore_dist(m, k, True)[0][v]
    return kqv, qkv, route(ps, k), route(ps, q)


def guarantee(m, v, g, caved):
    """DungeonDefinition.Validate's cave-in guarantee."""
    reach = bfs(m, v, lambda r, c: esc_open(m, r, c))[0]
    home = bfs(m, g, lambda r, c: esc_open(m, r, c), caved)[0]
    return all(t in caved or t in home for t in reach)


# ---------------------------------------------------------------- cave-ins

def cave_candidates(m, path, caved, dist):
    seen, out = set(), []
    for i in range(1, len(path) - 1):
        p, a, b = path[i], path[i - 1], path[i + 1]
        if abs(b[0] - a[0]) == 2 and a[1] == b[1]:
            perp, along = (0, 1), (1, 0)
        elif abs(b[1] - a[1]) == 2 and a[0] == b[0]:
            perp, along = (1, 0), (0, 1)
        else:
            continue
        for s in (1, -1):
            q = (p[0] + perp[0] * s, p[1] + perp[1] * s)
            far_q = (q[0] + perp[0] * s, q[1] + perp[1] * s)
            far_p = (p[0] - perp[0] * s, p[1] - perp[1] * s)
            if any(m[x][y] != "0" for x, y in (p, q)) or any(m[x][y] != "1" or (x, y) in BREAKAWAY for x, y in (far_q, far_p)):
                continue
            if any(m[x + o * along[0]][y + o * along[1]] != "0" for x, y in (p, q) for o in (-1, 1)):
                continue
            key = frozenset((p, q))
            if key in seen or dist.get(p, 0) < 40 or any(abs(x - u) + abs(y - w) < 7 for x, y in key for u, w in caved):
                continue
            seen.add(key)
            out.append(key)
    return out


def place_caves(m, rng, v, g, open_len):
    caved, picks = set(), []
    while len(picks) < 6:
        dist, prev = bfs(m, v, lambda r, c: esc_open(m, r, c), caved)
        path = route(prev, g)
        cands = cave_candidates(m, path, caved, dist)
        rng.shuffle(cands)
        options = []
        for key in cands:
            trial = caved | key
            if guarantee(m, v, g, trial) and bfs(m, v, lambda r, c: esc_open(m, r, c), trial)[0].get(g, 999) <= 140:
                options.append(key)
        if not options:
            break
        key = rng.choice(options)  # every valid cut keeps the closed route inside the length cap
        caved |= key
        picks.append(key)
    need(3, len(picks) >= 4, f"only {len(picks)} cave-ins fit")
    dist, prev = bfs(m, v, lambda r, c: esc_open(m, r, c), caved)
    out = []
    for key in picks:
        d = min(dist[(t[0] + dr, t[1] + dc)] + 1 for t in key for dr, dc in DIRS if (t[0] + dr, t[1] + dc) in dist)
        at = d // 2 - 20
        need(3, at >= 0, "cave-in would trigger before the front moves")
        out.append((at, sorted(key)))
    out.sort()
    return caved, out, route(prev, g)


# ---------------------------------------------------------------- hazards

def runs(m, name):
    """Straight 2-wide corridor runs inside one region: ('v', left col, [rows]) or ('h', top row, [cols])."""
    out = []
    for axis in "vh":
        for lane in range(CELLS):
            a = 3 * lane + 1
            cur = []
            for i in range(1, SIZE - 1):
                r, c = (i, a) if axis == "v" else (a, i)
                dr, dc = (0, 1) if axis == "v" else (1, 0)
                ok = (m[r][c] == "0" and m[r + dr][c + dc] == "0" and m[r - dr][c - dc] == "1"
                      and m[r + 2 * dr][c + 2 * dc] == "1" and tile_region(r, c) == name)
                if ok:
                    cur.append(i)
                if (not ok or i == SIZE - 2) and cur:
                    if len(cur) >= 3:
                        out.append((axis, a, cur))
                    cur = []
    return out


def run_tiles(run, idx=None):
    axis, a, line = run
    return [(i, a + k) if axis == "v" else (a + k, i) for i in (line if idx is None else idx) for k in (0, 1)]


def cross(run, i):
    return run_tiles(run, [i])


def spaced(tiles, taken, gap):
    return all(abs(r - u) + abs(c - w) >= gap for r, c in tiles for u, w in taken)


def place_traps(m, rng, escape, route_tiles):
    """Spikes and crumble in the crypt, blades and darts in the armory; none on the escape path or a cave-in."""
    taken, spikes, crumbles, blades, launchers = set(), [], [], [], []
    keep = set(escape)  # the caller passes escape path + cave-in tiles

    pool = [(run, i) for run in runs(m, "crypt") for i in run[2][1:-1]]
    rng.shuffle(pool)

    def free_pairs(gap):
        for run, i in pool:
            tiles = cross(run, i)
            if not (keep & set(run_tiles(run, [i - 1, i, i + 1]))) and spaced(tiles, taken, gap):
                yield tiles

    for tiles in free_pairs(4):
        if len(spikes) == 8:
            break
        k = len(spikes) // 2
        for r, c in tiles:
            m[r][c] = "T"
            spikes.append({"row": r, "column": c, "offsetMs": SPIKE_OFFSETS[k % 4]})
        taken.update(tiles)
    need(5, len(spikes) >= 8, "fewer than 4 spike pairs fit in the crypt")
    s, k_, q, v = (find(m, ch) for ch in "SKQV")
    must = [k_, q, v] + [(r, c) for r in range(SIZE) for c in range(SIZE) if m[r][c] == "L"]
    for tiles in free_pairs(4):
        if len(crumbles) == 3:
            break
        for r, c in tiles:
            m[r][c] = "C"
        if all(t in explore_dist(m, s, True)[0] for t in must):
            crumbles.append(tiles)
            taken.update(tiles)
        else:
            for r, c in tiles:
                m[r][c] = "0"
    need(5, len(crumbles) >= 3, f"only {len(crumbles)} crumble pairs fit")
    # Blades: prefer corridors off the key routes.
    armory = runs(m, "armory")
    rng.shuffle(armory)
    for run in armory:
        if len(blades) == 3:
            break
        axis, a, line = run
        if len(line) < 4:
            continue
        seg = line[:8]
        zone = set(run_tiles(run, [seg[0] - 1] + seg + [seg[-1] + 1]))
        if zone & (keep | route_tiles) or not spaced(zone, taken, 6):  # a centreline blade shuts the corridor, so keep it off every required route
            continue
        n = len(blades)
        if axis == "v":
            blade = {"x1": a + BLADE_X, "y1": seg[0] + 0.5, "x2": a + BLADE_X, "y2": seg[-1] + 0.5}
        else:
            blade = {"x1": seg[0] + 0.5, "y1": a + BLADE_X, "x2": seg[-1] + 0.5, "y2": a + BLADE_X}
        blade.update(offsetMs=[0, 2000, 4000][n], periodMs=6000)
        blades.append(blade)
        taken.update(zone)
    need(5, len(blades) >= 3, f"only {len(blades)} blade corridors fit")
    # Launchers: start at a closed corridor end, fire along it; the other lane is the shelter.
    for run in armory:
        if len(launchers) == 3:
            break
        axis, a, line = run
        if len(line) < 4:
            continue
        for end, step in ((line[0], 1), (line[-1], -1)):
            if len(launchers) == 3:
                break
            lane = rng.choice((0, 1))
            for back in (0, 1, 2):  # start up to a corner cell behind the run, with a wall at the launcher's back
                start = end - back * step
                if axis == "v":
                    shot = [(start + step * k, a + lane) for k in range(40)]
                    dx, dy, x, y = 0, step, a + lane + 0.5, start + 0.5
                    behind = (start - step, a + lane)
                else:
                    shot = [(a + lane, start + step * k) for k in range(40)]
                    dx, dy, x, y = step, 0, start + 0.5, a + lane + 0.5
                    behind = (a + lane, start - step)
                lane_tiles = []
                for r, c in shot:
                    if m[r][c] == "1":
                        break
                    lane_tiles.append((r, c))
                if (m[behind[0]][behind[1]] != "1" or m[shot[0][0]][shot[0][1]] != "0" or len(lane_tiles) < 4
                        or set(lane_tiles) & keep or not spaced(lane_tiles, taken, 3)
                        or any(tile_region(r, c) != "armory" for r, c in lane_tiles)):  # the hall stays trap-free
                    continue
                n = len(launchers)
                launchers.append({"x": x, "y": y, "dx": dx, "dy": dy, "offsetMs": [0, 2000, 4000][n]})
                taken.update(lane_tiles)
                break
    need(5, len(launchers) >= 3, f"only {len(launchers)} launcher lanes fit")
    return spikes, blades, launchers, taken, crumbles


# ---------------------------------------------------------------- decor

def walls4(m, r, c):
    return sum(m[r + dr][c + dc] == "1" for dr, dc in DIRS)


def build_decor(m, rng, cg, ag, reserved):
    decor, used = [], set()

    def put(sprite, r, c, doorway=False):
        if doorway and (r, c) in reserved:
            return  # a cave-in lands here
        ok = m[r][c] == "0" and (r, c) not in used and (doorway or walls4(m, r, c) >= 2)
        need(7, ok, f"{sprite} cannot sit at {r},{c}")
        decor.append({"sprite": sprite, "row": r, "column": c})
        used.add((r, c))

    # Hall.
    for r, c in ((13, 19), (13, 20), (13, 25)):
        put("pedestal", r, c)
    put("banner", 13, 26)
    put("banner", 32, 26)
    put("chains", 32, 19)
    decor.append({"sprite": "pedestal", "row": 1, "column": 22})  # under the vault key V; the one tile exempt from the wall-neighbour rule
    # Doorways between regions: arch_open in the crypt, door_wood in the armory.
    for r, c in ((22, 18), (23, 18)):
        put("arch_open", r, c, True)
    for r, c in ((22, 27), (23, 27)):
        put("door_wood", r, c, True)
    for r, c in ((33, 22), (33, 23)):
        put("arch_pillars", r, c, True)
    for cx in cg:
        for c in (3 * cx + 1, 3 * cx + 2):
            put("arch_open", 33, c, True)
    for cx in ag:
        for c in (3 * cx + 1, 3 * cx + 2):
            put("door_wood", 33, c, True)
    # Region props on tiles with two or more wall neighbours, dead ends first.
    pools = {n: [] for n in ("crypt", "armory", "gold", "gallery")}
    for r in range(1, SIZE - 1):
        for c in range(1, SIZE - 1):
            if m[r][c] != "0" or (r, c) in used or (r, c) in reserved or walls4(m, r, c) < 2:
                continue
            reg = tile_region(r, c)
            reg = "gold" if reg in ("vault", "treasury") else reg
            if reg in pools:
                pools[reg].append((r, c))
    for pool in pools.values():
        rng.shuffle(pool)
        pool.sort(key=lambda p: -walls4(m, *p))
    plan = {"crypt": (["skull", "bones", "pot", "blood"], 16), "armory": (["rack_swords", "rack_spears", "iron_ball", "keg", "table", "chair"], 18),
            "gold": (["gold_pile", "gold_coins", "gold_stack", "chest", "banner"], 15), "gallery": (["bones", "pot", "keg"], 6)}
    for name, (sprites, count) in plan.items():
        placed = []
        for r, c in pools[name]:
            if len(placed) == count:
                break
            if all(abs(r - u) + abs(c - w) >= (2 if name == "gold" else 3) for u, w in placed):
                put(sprites[len(placed) % len(sprites)], r, c)
                placed.append((r, c))
    # Torches on south-facing wall tiles, roughly every 6 tiles, each lighting the floor below.
    torches, lights = [], []
    spots = [(r, c) for r in range(0, SIZE - 1) for c in range(1, SIZE - 1)
             if m[r][c] == "1" and (r, c) not in BREAKAWAY and m[r + 1][c] == "0" and (r + 1, c) not in used and (r + 1, c) not in reserved]
    rng.shuffle(spots)
    for r, c in spots:
        if len(torches) < 40 and all(abs(r - u) + abs(c - w) >= 6 for u, w in torches):
            torches.append((r, c))
            decor.append({"sprite": "torch", "row": r, "column": c})
            lights.append({"row": r + 1, "column": c, "radius": 3.5})
    return decor, lights


# ---------------------------------------------------------------- one full attempt

def attempt(seed):
    rng = random.Random(seed)
    src, rooms, cg, ag = build_source(rng)
    place_keys(src, rooms)
    sg = src.g
    dead = sum(1 for r in range(1, SRC - 1) for c in range(1, SRC - 1)
               if sg[r][c] != "1" and sum(sg[r + dr][c + dc] != "1" for dr, dc in DIRS) == 1)
    junc = sum(1 for r in range(1, SRC - 1) for c in range(1, SRC - 1)
               if sg[r][c] != "1" and sum(sg[r + dr][c + dc] != "1" for dr, dc in DIRS) >= 3)
    need(8, dead >= 20, f"{dead} dead ends (need 20)")
    need(8, junc >= 25, f"{junc} junctions (need 25)")
    # Treasury antechamber: a maze, not a band (cell rows 2-3, cx 4-10).
    t_links = sum(sg[2 * cy + 1][2 * cx + 2] != "1" for cx in range(4, 10) for cy in (2, 3)) +         sum(sg[6][2 * cx + 1] != "1" for cx in range(4, 11))
    need(7, t_links >= 14, "treasury has no loop")
    need(7, all(sum(sg[2 * cy + 1][2 * cx + 2] != "1" for cx in range(4, 10)) <= 5 for cy in (2, 3)), "treasury row is an open band")
    t_dead = sum(sum(sg[2 * cy + 1 + dr][2 * cx + 1 + dc] != "1" for dr, dc in DIRS) == 1 for cx in range(4, 11) for cy in (2, 3))
    need(7, t_dead >= 2, "treasury has under 2 dead ends")
    m = widen(sg)
    v, g, s = find(m, "V"), find(m, "G"), find(m, "S")
    # Lava along the vault room's inner west and east edges, clear of the breakaway and the walking line.
    for r in (3, 4, 5):
        m[r][16] = "W"
    for r in range(1, 6):
        m[r][29] = "W"
    kqv, qkv, route_k, route_q = explore_routes(m)
    route_v = route(explore_dist(m, s, True)[1], v)
    route_tiles = set(route_k) | set(route_q) | set(route_v)
    best = min(kqv, qkv)
    need(1, 220 <= best <= 420, f"explore route {best} (need 220-420)")
    open_len = bfs(m, v, lambda r, c: esc_open(m, r, c))[0][g]
    need(2, open_len <= 112, f"open escape {open_len} too long for the 1.25x rule")
    caved, caves, path = place_caves(m, rng, v, g, open_len)
    closed_len = len(path) - 1
    need(2, 90 <= closed_len <= 140, f"closed escape {closed_len} (need 90-140)")
    need(2, closed_len >= 1.25 * open_len, f"closed {closed_len} vs open {open_len} (need 1.25x)")
    escape = set(path)
    # Escape heart on a 1-tile detour near the midpoint.
    mid = len(path) // 2
    heart = None
    for off in sorted(range(-12, 13), key=abs):
        r, c = path[mid + off]
        for dr, dc in DIRS:
            n = (r + dr, c + dc)
            if m[n[0]][n[1]] == "0" and n not in escape and n not in caved and walls4(m, *n) >= 1:
                heart = n
                break
        if heart:
            break
    need(6, heart, "no detour tile for the escape heart")
    m[heart[0]][heart[1]] = "L"
    spikes, blades, launchers, taken, crumbles = place_traps(m, rng, escape | caved, route_tiles)
    need(5, not any(m[r][c] in "TC" for r, c in escape), "hazard on the escape path")
    # Checkpoints.
    cps = [{"row": 22, "column": 22, "escape": False}, {"row": 13, "column": 22, "escape": False}]
    marks = []
    for frac in (0.25, 0.5, 0.75):
        i = int(len(path) * frac)
        for off in sorted(range(-6, 7), key=abs):
            r, c = path[i + off]
            if m[r][c] == "0" and (r, c) not in marks and (r, c) not in caved and (r, c) != heart:
                marks.append((r, c))
                break
    need(4, len(marks) == 3, "escape checkpoints did not fit")
    cps += [{"row": r, "column": c, "escape": True} for r, c in marks]
    reserved = set(marks) | {(22, 22), (13, 22)} | taken | caved | {heart}
    reserved |= {(int(l["y"]), int(l["x"])) for l in launchers}
    decor, lights = build_decor(m, rng, cg, ag, reserved)
    need(7, len(decor) >= 60, f"{len(decor)} decor items (need 60)")
    kinds = len({d["sprite"] for d in decor})
    need(7, kinds >= 20, f"{kinds} decor kinds (need 20)")
    need(7, len(lights) >= 12, f"{len(lights)} lights (need 12)")
    # Final checks on the finished map.
    flat = "".join("".join(row) for row in m)
    need(1, all(flat.count(ch) == 1 for ch in "KQV"), "need exactly one K, Q, V")
    need(6, flat.count("L") >= 4, "need 4 hearts")
    need(5, flat.count("W") >= 4, "treasury lava")
    interior = sum(m[r][c] != "1" for r in range(1, 45) for c in range(1, 45)) / 44 ** 2
    need(8, interior >= 0.45, f"floor share {interior:.2f}")
    need(3, guarantee(m, v, g, caved), "cave-ins can trap the marble")
    kqv, qkv, _, _ = explore_routes(m)
    need(1, 220 <= min(kqv, qkv) <= 420, "explore route changed after traps")
    final_open = bfs(m, v, lambda r, c: esc_open(m, r, c))[0][g]
    final_closed = bfs(m, v, lambda r, c: esc_open(m, r, c), caved)[0][g]
    need(2, 90 <= final_closed <= 140 and final_closed >= 1.25 * final_open, "escape lengths changed after traps")
    side = {"spikes": spikes, "blades": blades, "launchers": launchers, "decor": decor, "lights": lights,
            "checkpoints": cps,
            "collapse": {"rumbleMs": 4000, "speed": 3.0, "pushback": 8,
                         "breakaway": [{"row": r, "column": c} for r, c in sorted(BREAKAWAY)],
                         "caveIns": [{"atReach": at, "tiles": [{"row": r, "column": c} for r, c in tiles]} for at, tiles in caves]}}
    summary = (f"explore K-Q-V={kqv} Q-K-V={qkv} escape closed={final_closed} open={final_open} "
               f"caves={len(caves)} spikes={len(spikes)} crumble={sum(row.count('C') for row in m)} blades={len(blades)} "
               f"darts={len(launchers)} lava={flat.count('W')} hearts={flat.count('L')} decor={len(decor)}/{kinds} "
               f"lights={len(lights)} floor={interior:.0%} dead={dead} junctions={junc}")
    return src, m, side, summary


def main():
    first = int(sys.argv[1]) if len(sys.argv) > 1 else 0
    fails = Counter()
    notes = {}
    for seed in range(first, first + SEARCH):
        try:
            src, m, side, summary = attempt(seed)
        except Fail as e:
            fails[e.rule] += 1
            print(f"seed {seed}: rule {e.rule}: {e}", file=sys.stderr)
            notes.setdefault(e.rule, []).append(str(e))
            continue
        SRC_OUT.write_text("\n".join("".join(row) for row in src.g) + "\n", newline="\n")
        MAP_OUT.write_text("\n".join("".join(row) for row in m) + "\n", newline="\n")
        JSON_OUT.write_text(json.dumps(side, indent=2) + "\n", newline="\n")
        print("\n".join("".join(row) for row in m))
        print(f"seed={seed} {summary}")
        return
    for rule, n in fails.most_common():
        print(f"rule {rule} ({CHECK_NAMES[rule]}) failed {n}x, e.g. {notes[rule][:3]}", file=sys.stderr)
    sys.exit("BLOCKED: no seed satisfies every rule")


if __name__ == "__main__":
    main()
