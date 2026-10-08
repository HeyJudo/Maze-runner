"""One-shot: build the Level 4 dungeon sprite atlas from the Raou and Timberwolf sheets.

Writes GravityMaze/Sprites/Dungeon/{dungeon,freed,maze,extra}.png and atlas.json, plus a contact
sheet (every sprite x4 with its name) for eyeballing the cuts.

Usage:  python tools/build_dungeon_atlas.py <scratch-dir> [assets-dir]
        assets-dir defaults to <repo>/ASSETS (Dungeon.png, free D.png, maze_sprites(1).PNG).
The output is committed; the game never runs this script. Needs Pillow only.
"""
import json
import math
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "GravityMaze" / "Sprites" / "Dungeon"
TINT = (0.92, 0.86, 1.06)

# name -> (sheet, x, y, w, h, frames). Rects found by listing connected opaque regions per sheet.
D, F, M, X = "dungeon", "freed", "maze", "extra"
ATLAS = {
    # Raou Dungeon.png: floor slab cells, wall top/face
    "floor_0": (D, 20, 52, 16, 16, 1), "floor_1": (D, 36, 84, 16, 16, 1),
    "floor_2": (D, 84, 52, 16, 16, 1), "floor_3": (D, 100, 20, 16, 16, 1),
    "wall_top": (D, 12, 204, 16, 16, 1), "wall_face": (D, 48, 296, 16, 16, 1),
    # Raou arches and doors
    "gate_closed": (D, 13, 457, 37, 44, 1), "door_wood": (D, 78, 457, 37, 44, 1),
    "arch_open": (D, 13, 521, 37, 44, 1), "chains": (D, 75, 203, 9, 35, 1),
    "gold_pile": (D, 77, 640, 42, 33, 1), "gold_coins": (D, 38, 617, 23, 15, 1),
    "gold_stack": (D, 1, 673, 31, 29, 1), "iron_ball": (D, 103, 681, 19, 19, 1),
    "table": (D, 11, 769, 42, 31, 1), "chair": (D, 72, 740, 14, 26, 1),
    "rack_swords": (D, 65, 787, 29, 38, 1), "rack_spears": (D, 97, 786, 29, 39, 1),
    # Raou free D.png props
    "arch_pillars": (F, 294, 323, 52, 62, 1), "torch": (F, 329, 262, 14, 22, 1),
    "banner": (F, 322, 229, 29, 26, 1), "chest": (F, 292, 261, 26, 25, 1),
    "pot": (F, 264, 232, 16, 18, 1), "skull": (F, 295, 232, 18, 17, 1),
    "bones": (F, 296, 204, 16, 12, 1), "keg": (F, 327, 198, 18, 24, 1),
    "pedestal": (F, 229, 255, 23, 34, 1),
    # Timberwolf maze_sprites(1).PNG (keyed + tinted)
        "arrow": (M, 46, 228, 18, 8, 1), "crumble": (M, 0, 288, 16, 16, 8),
    "lava": (M, 288, 240, 16, 16, 2), "key": (M, 224, 304, 16, 16, 1),
    "blood": (M, 32, 128, 32, 32, 4),
}
# spikes (reversed strip) and saw (toothed disc) are composed into extra.png below the tiles.
# Composed 16-px tiles, packed on a grid in extra.png (in this order); gate_open (37x44) sits below them.
EXTRA = ["wall_edge_n", "wall_edge_e", "wall_edge_s", "wall_edge_w", "wall_inner_ne", "wall_inner_nw",
         "wall_inner_se", "wall_inner_sw", "pit", "rubble", "wall_top"]

REQUIRED = ("floor_0 floor_1 floor_2 floor_3 wall_top wall_face wall_edge_n wall_edge_e wall_edge_s wall_edge_w "
            "wall_inner_ne wall_inner_nw wall_inner_se wall_inner_sw gate_closed gate_open door_wood arch_open arch_pillars "
            "torch chains banner gold_pile gold_coins gold_stack chest pot skull bones keg pedestal iron_ball table chair "
            "rack_swords rack_spears spikes saw arrow crumble lava pit rubble key blood").split()


def key_checkerboard(im):
    """Make the two baked checker colours transparent, then tint toward Raou purple."""
    im = im.convert("RGBA")
    px = im.load()
    keys = {px[x, y][:3] for x, y in ((304, 160), (288, 160))}
    assert len(keys) == 2, "expected two checker colours, got %r" % keys
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, _ = px[x, y]
            if (r, g, b) in keys:
                px[x, y] = (0, 0, 0, 0)
            else:
                px[x, y] = tuple(min(255, int(c * t)) for c, t in zip((r, g, b), TINT)) + (255,)
    return im


def crop(sheets, name):
    s, x, y, w, h, _ = ATLAS[name]
    return sheets[s].crop((x, y, x + w, y + h))


def darken(im, k):
    r, g, b, a = im.split()
    return Image.merge("RGBA", [c.point(lambda v: int(v * k)) for c in (r, g, b)] + [a])


def build_extra(sheets):
    floor, dun = crop(sheets, "floor_0"), sheets[D]
    # Raou's wall top is the same lilac as the floor slab; darken it so walls read as solid mass (still lighter than wall_face).
    top = darken(crop(sheets, "wall_top"), 0.58)
    rim = darken(top, 0.45)
    t = {"wall_top": top}
    # Edges: wall_top with a 4-px dark rim on the side that meets the floor.
    for name, box in (("wall_edge_n", (0, 0, 16, 4)), ("wall_edge_e", (12, 0, 16, 16)),
                      ("wall_edge_s", (0, 12, 16, 16)), ("wall_edge_w", (0, 0, 4, 16))):
        e = top.copy()
        e.paste(rim.crop(box), box[:2])
        t[name] = e
    # Inner corners: the two adjoining rims meet (darker pixel wins).
    for name, (a, b) in (("wall_inner_ne", ("n", "e")), ("wall_inner_nw", ("n", "w")),
                         ("wall_inner_se", ("s", "e")), ("wall_inner_sw", ("s", "w"))):
        t[name] = ImageChops.darker(t["wall_edge_" + a], t["wall_edge_" + b])
    # Open gate: closed gate with its lower interior swapped for the open dark arch (bars retracted).
    g, a = crop(sheets, "gate_closed").copy(), crop(sheets, "arch_open")
    g.paste(a.crop((0, 18, 37, 44)), (0, 18))
    t["gate_open"] = g
    # Pit: the near-black block from Dungeon.png inside a dark floor rim.
    pit = darken(floor, 0.3)
    pit.paste(dun.crop((98, 162, 112, 176)), (1, 1))
    t["pit"] = pit
    # Rubble: dark floor with the slab's broken lower edge scattered over it.
    rub = darken(floor, 0.55)
    for dx, flip in ((16, False), (64, True)):
        frag = dun.crop((dx, 116, dx + 16, 132))
        if flip:
            frag = frag.transpose(Image.FLIP_TOP_BOTTOM)
        rub.alpha_composite(frag)
    t["rubble"] = rub
    sheet = Image.new("RGBA", (16 * 16, 16 + 44 + 20 + 20), (0, 0, 0, 0))
    for i, name in enumerate(EXTRA):
        sheet.alpha_composite(t[name], (i * 16, 0))
        ATLAS[name] = (X, i * 16, 0, 16, 16, 1)
    sheet.alpha_composite(t["gate_open"], (0, 16))
    ATLAS["gate_open"] = (X, 0, 16, 37, 44, 1)
    # Spikes: the Timberwolf strip runs fully up -> retracted; the contract wants retracted (0) -> fully up (3).
    for i in range(4):
        sheet.alpha_composite(sheets[M].crop((224 + (3 - i) * 16, 40, 240 + (3 - i) * 16, 60)), (i * 16, 80))
    ATLAS["spikes"] = (X, 0, 80, 16, 20, 4)
    # Saw: the steel disc frames (maze 31,111 and 127,111) ringed with 8 tooth tips cut from the steel
    # knife blade's tip (maze 182,113); the second frame turns the teeth half a tooth so it spins.
    m = sheets[M]
    tip = m.crop((182, 113, 188, 117))
    pyramid = Image.new("L", (6, 4), 0)
    for row, (a, b) in enumerate(((2, 4), (1, 5), (0, 6), (0, 6))):
        ImageDraw.Draw(pyramid).rectangle((a, row, b - 1, row), fill=255)
    tip.putalpha(ImageChops.multiply(tip.getchannel("A"), pyramid))
    for f, (disc_x, turn) in enumerate(((31, 0.0), (127, 22.5))):
        frame = Image.new("RGBA", (20, 20), (0, 0, 0, 0))
        frame.alpha_composite(m.crop((disc_x, 111, disc_x + 16, 128)), (2, 1))
        for k in range(8):
            ang = k * 45.0 + turn
            rot = tip.rotate(-ang, Image.NEAREST, expand=True)
            cx, cy = 10 + 8.6 * math.sin(math.radians(ang)), 10 - 8.6 * math.cos(math.radians(ang))
            frame.alpha_composite(rot, (int(round(cx - rot.width / 2)), int(round(cy - rot.height / 2))))
        sheet.alpha_composite(frame, (f * 20, 60))
    ATLAS["saw"] = (X, 0, 60, 20, 20, 2)
    return sheet


def contact(sheets, path):
    cols, s = 6, 4
    ims = {}
    cell_w = cell_h = 0
    for n in REQUIRED:
        sh, x, y, w, h, fr = ATLAS[n]
        k = min(fr, 4)
        ims[n] = sheets[sh].crop((x, y, x + w * k, y + h)).resize((w * k * s, h * s), Image.NEAREST)
        cell_w, cell_h = max(cell_w, ims[n].width), max(cell_h, ims[n].height)
    cell_w, cell_h = cell_w + 8, cell_h + 20
    rows = (len(REQUIRED) + cols - 1) // cols
    bg = Image.new("RGBA", (cols * cell_w, rows * cell_h), (70, 110, 70, 255))
    d = ImageDraw.Draw(bg)
    for i, n in enumerate(REQUIRED):
        cx, cy = (i % cols) * cell_w, (i // cols) * cell_h
        d.text((cx + 2, cy + 2), "%s x%d" % (n, ATLAS[n][5]), fill=(255, 255, 255))
        bg.alpha_composite(ims[n], (cx + 2, cy + 16))
    bg.save(path)


def main():
    scratch = Path(sys.argv[1])
    assets = Path(sys.argv[2]) if len(sys.argv) > 2 else ROOT / "ASSETS"
    OUT.mkdir(parents=True, exist_ok=True)
    scratch.mkdir(parents=True, exist_ok=True)
    sheets = {D: Image.open(assets / "Dungeon.png").convert("RGBA"),
              F: Image.open(assets / "free D.png").convert("RGBA"),
              M: key_checkerboard(Image.open(assets / "maze_sprites(1).PNG"))}
    for k in (D, F, M):
        sheets[k].save(OUT / (k + ".png"))
    sheets[X] = build_extra(sheets)
    sheets[X].save(OUT / "extra.png")
    missing = [n for n in REQUIRED if n not in ATLAS]
    assert not missing, missing
    atlas = {n: dict(zip(("sheet", "x", "y", "w", "h", "frames"), v)) for n, v in ATLAS.items()}
    (OUT / "atlas.json").write_text(json.dumps(atlas, indent=1), encoding="utf-8")
    contact(sheets, scratch / "atlas_contact.png")


if __name__ == "__main__":
    main()
