"""Building pieces for D-020: 4 materials x wall, door, window, low barricade, fence, floor.

Walls join their neighbours (autotiling): `wall_<m>_top_<mask>` uses N=1 E=2 S=4 W=8 (a bit is set
when that neighbour is the same wall); `wall_<m>_face_<mask>` is the south front shown when nothing
wall-like is below, with E=1 W=2. Damage is a transparent overlay drawn over any piece.
"""

from __future__ import annotations

import math

from PIL import Image

from .gfx import Color, Sprite, Tile, ellipse, hnoise, mix, poly, ramp5, rect

MATERIALS = ("pallet", "drywall", "wood", "log", "sandbag", "scrap", "adobe", "container", "brick", "stone",
             "concrete", "rebar", "steel", "glass")
# how the thin pieces (fence) are drawn: rails, mesh or a low wall
FENCE_STYLE = {"pallet": "rails", "wood": "rails", "log": "rails", "drywall": "wall", "sandbag": "wall", "scrap": "mesh", "adobe": "wall",
               "container": "mesh", "brick": "wall", "stone": "wall", "concrete": "wall", "rebar": "wall", "steel": "mesh", "glass": "glass"}
S = 32
CAP_ROWS = 8                    # the lit top strip of a facade


def _n(x: int, y: int, salt: int, amp: int) -> int:
    return int(hnoise(x, y, salt) * amp) - amp // 2


def face(m: str, x: int, y: int) -> Color:
    """Colour of the vertical face of material `m` at (x, y); tiles seamlessly in x."""
    if m == "wood":
        row = y // 6
        base = mix((138, 96, 58), (255, 220, 160) if hnoise(row, 1, 5) < .5 else (40, 20, 10), .02 + hnoise(row, 2, 5) * .14)
        n = _n(x // 3, y, 11 + row, 14)
        c = (base[0] + n, base[1] + n * 3 // 4, base[2] + n // 2)
        if y % 6 == 0: c = mix(c, (255, 226, 176), .22)
        if y % 6 == 4: c = mix(c, (30, 14, 8), .42)
        if y % 6 == 5: c = (58, 38, 24)
        if x % 32 in (3, 28) and y % 6 == 2: c = (176, 176, 184) if x % 32 == 3 else (36, 36, 42)
        if hnoise(x // 6, y, 17 + row) < .06 and y % 6 in (1, 2, 3): c = mix(c, (50, 30, 18), .5)
        return c
    if m == "scrap":
        cx, cy = x // 16, (y + (6 if (x // 16) % 2 else 0)) // 12
        pick = int(hnoise(cx, cy, 23) * 4)
        base = ((142, 86, 54), (92, 106, 122), (88, 112, 94), (124, 130, 136))[pick]
        c = mix(base, (255, 255, 255) if x % 4 in (0, 1) else (0, 0, 20), .10 if x % 4 in (0, 1) else .12)
        c = (c[0] + _n(x, y, 29, 8), c[1] + _n(x, y, 29, 8), c[2] + _n(x, y, 29, 8))
        if x % 16 == 0 or (y + (6 if (x // 16) % 2 else 0)) % 12 == 0: c = (46, 50, 58)
        if x % 16 == 1 or (y + (6 if (x // 16) % 2 else 0)) % 12 == 1: c = mix(c, (220, 224, 230), .25)
        if x % 16 in (2, 13) and (y + (6 if (x // 16) % 2 else 0)) % 12 in (2, 10): c = (196, 200, 208)
        if y % 24 in (10, 11) and cx % 2 == 0: c = (176, 180, 186) if y % 2 else (136, 140, 148)       # tape
        if hnoise(x, y // 3, 31) < .09 and y > 6: c = mix(c, (140, 70, 36), .6)                          # rust streaks
        return c
    if m == "brick":
        row = y // 4
        off = 8 if row % 2 else 0
        bx = (x + off) // 16
        col = mix(((138, 76, 62), (124, 66, 56), (152, 90, 70), (112, 58, 50), (132, 72, 64))[int(hnoise(bx, row, 3) * 5)],
                  (255, 255, 255) if hnoise(bx, row, 4) < .5 else (0, 0, 0), hnoise(bx, row, 6) * .12)
        n = _n(x, y, 7 + row, 14)
        c = (col[0] + n, col[1] + n // 2, col[2] + n // 2)
        if y % 4 == 0: c = mix(c, (255, 220, 190), .22)
        if y % 4 == 2: c = mix(c, (20, 10, 20), .28)
        if y % 4 == 3 or (x + off) % 16 == 15: c = (102, 104, 108) if (x + y) % 5 else (88, 90, 96)
        return c
    if m == "pallet":
        row = y // 8
        slat = y % 8
        n = _n(x // 2, y, 201 + row, 12)
        base = mix((176, 142, 96), (255, 235, 190) if hnoise(row, 3, 7) < .5 else (80, 56, 34), hnoise(row, 4, 7) * .16)
        c = (base[0] + n, base[1] + n * 3 // 4, base[2] + n // 2)
        if slat in (6, 7): c = (34, 24, 20)                                   # gap between slats
        if slat == 0: c = mix(c, (255, 240, 200), .2)
        if x % 16 in (1, 14) and slat in (2, 3): c = (96, 96, 104)            # nails
        if hnoise(x // 5, y, 203) < .05 and slat < 6: c = mix(c, (60, 36, 20), .5)
        return c
    if m == "drywall":
        n = _n(x, y, 211, 8)
        c = (206 + n, 200 + n, 188 + n)
        if x % 16 == 0 or y % 24 == 0: c = (150, 146, 136)
        elif x % 16 == 1 or y % 24 == 1: c = (232, 228, 216)
        if (x % 16, y % 24) in ((3, 4), (12, 4), (3, 18), (12, 18)): c = (150, 150, 156)       # screws
        if hnoise(x // 3, y // 3, 213) < .05: c = mix(c, (150, 130, 100), .5)                   # scuffs
        return c
    if m == "log":
        row = y // 8
        k = y % 8
        n = _n(x // 2, y, 221 + row, 12)
        base = mix((112, 78, 50), (255, 220, 170) if hnoise(row, 5, 9) < .5 else (30, 16, 10), hnoise(row, 6, 9) * .12)
        tone = (-34, -10, 14, 22, 14, 0, -18, -44)[k]                           # rounded: lit on top, dark below
        c = (base[0] + tone + n, base[1] + tone * 3 // 4 + n * 3 // 4, base[2] + tone // 2 + n // 2)
        if (x + row * 5) % 32 in (0, 31): c = (60, 38, 24)                      # notch where logs cross
        if hnoise(x // 4, y, 223) < .08 and 1 < k < 6: c = mix(c, (60, 36, 22), .5)
        return c
    if m == "sandbag":
        row = y // 6
        off = 8 if row % 2 else 0
        bx = (x + off) // 16
        k = y % 6
        n = _n(x, y, 231, 10)
        base = mix((180, 156, 108), (255, 240, 200) if hnoise(bx, row, 11) < .5 else (80, 60, 36), hnoise(bx, row, 12) * .12)
        c = (base[0] + n, base[1] + n * 3 // 4, base[2] + n // 2)
        if k == 0: c = mix(c, (255, 244, 210), .25)
        if k == 5 or (x + off) % 16 in (0, 15): c = (92, 76, 52)
        if k == 2 and (x + off) % 16 in (3, 7, 11): c = (120, 98, 66)           # stitching
        return c
    if m == "adobe":
        n = _n(x // 2, y // 2, 241, 16)
        c = (186 + n, 140 + n * 3 // 4, 96 + n // 2)
        if hnoise(x // 4, y // 4, 243) < .18: c = mix(c, (150, 100, 66), .35)
        if y % 16 == 0 or (x + (8 if (y // 16) % 2 else 0)) % 16 == 0: c = mix(c, (110, 76, 50), .35)
        if hnoise(x, y, 245) < .03: c = (226, 196, 120)                          # straw
        return c
    if m == "container":
        rib = x % 4
        base = (150, 62, 44)
        c = mix(base, (255, 210, 190) if rib in (1, 2) else (30, 8, 10), .16 if rib in (1, 2) else .22)
        c = (c[0] + _n(x // 4, y, 251, 10), c[1] + _n(x // 4, y, 251, 10), c[2] + _n(x // 4, y, 251, 10))
        if y % 24 in (0, 1): c = (74, 78, 86)                                    # frame rails
        if x % 32 in (0, 31): c = (60, 62, 70)
        if hnoise(x // 2, y // 3, 253) < .1 and y > 4: c = mix(c, (110, 60, 40), .55)
        if 9 <= y % 24 <= 14 and x % 8 in (2, 3, 5): c = mix(c, (236, 236, 226), .8)      # stencil
        return c
    if m == "stone":
        row = y // 10
        off = (row * 7) % 18
        w_ = 14 + (row % 3) * 3
        bx = (x + off) // w_
        base = mix((124, 120, 112), (220, 216, 200) if hnoise(bx, row, 13) < .5 else (30, 30, 36), hnoise(bx, row, 14) * .2)
        n = _n(x, y, 261, 14)
        c = (base[0] + n, base[1] + n, base[2] + n)
        if y % 10 == 0 or (x + off) % w_ == 0: c = (66, 64, 62)
        elif y % 10 == 1 or (x + off) % w_ == 1: c = mix(c, (255, 250, 230), .25)
        if hnoise(x // 2, y // 2, 263) < .05 and y % 10 > 5: c = (84, 104, 70)       # moss
        return c
    if m == "rebar":
        n = _n(x, y, 271, 12)
        c = (118 + n, 118 + n, 116 + n)
        if y % 16 == 0 or x % 16 == 0: c = (66, 66, 68)
        elif y % 16 == 1 or x % 16 == 1: c = (150, 150, 146)
        if (x % 16, y % 16) in ((2, 2), (13, 2), (2, 13), (13, 13)): c = (210, 120, 60)    # rusty bar ends
        if x % 8 == 4 and hnoise(x, y // 4, 273) < .5: c = mix(c, (176, 96, 52), .45)         # rust stains
        return c
    if m == "steel":
        n = _n(x // 4, y, 281, 8)
        c = (150 + n, 160 + n, 172 + n)
        if y % 16 == 0 or x % 16 == 0: c = (60, 68, 82)
        elif y % 16 == 1 or x % 16 == 1: c = (206, 216, 228)
        elif y % 16 == 15 or x % 16 == 15: c = (96, 106, 120)
        if (x % 16, y % 16) in ((3, 3), (12, 3), (3, 12), (12, 12)): c = (220, 226, 236) if (x + y) % 2 else (70, 78, 92)    # bolts
        if y % 32 in (7, 8) and x % 32 < 12: c = (226, 190, 60) if (x // 3) % 2 else (40, 40, 44)                            # hazard strip
        return c
    if m == "glass":
        return (150, 196, 214)
    # concrete
    row = y // 8
    off = 8 if row % 2 else 0
    c = (142 + _n(x, y, 41, 14), 140 + _n(x, y, 41, 14), 134 + _n(x, y, 41, 14))
    if y % 8 == 0 or (x + off) % 16 == 0: c = (96, 94, 90)
    elif y % 8 == 1 or (x + off) % 16 == 1: c = (166, 164, 158)
    if hnoise(x // 2, y // 2, 43) < .07: c = mix(c, (96, 108, 84), .35)
    return c


def cap(m: str, x: int, y: int) -> Color:
    """Colour of the top surface of material `m`."""
    if m == "wood":
        c = mix((150, 112, 72), (255, 226, 176) if (x // 8) % 2 else (60, 36, 20), .1 + hnoise(x // 8, 0, 51) * .1)
        n = _n(x, y // 2, 53, 12)
        c = (c[0] + n, c[1] + n * 3 // 4, c[2] + n // 2)
        if x % 8 == 7: c = (70, 46, 28)
        if hnoise(x, y, 55) < .03: c = (60, 36, 22)
        return c
    if m == "scrap":
        n = _n(x // 2, y, 57, 12)
        c = (112 + n, 118 + n, 126 + n)
        if y % 16 in (3, 12) and x % 8 == 4: c = (200, 204, 212)
        if (x + y) % 16 == 0: c = (92, 98, 106)
        return c
    if m == "brick":
        row = y // 4
        off = 8 if row % 2 else 0
        n = _n(x, y, 59, 12)
        col = mix((130, 72, 60), (255, 220, 190), hnoise((x + off) // 8, row, 3) * .15)
        c = (col[0] + n, col[1] + n // 2, col[2] + n // 2)
        if y % 4 == 3 or (x + off) % 8 == 7: c = (112, 112, 116)
        return c
    if m == "pallet":
        n = _n(x, y // 2, 291, 12)
        c = (170 + n, 136 + n * 3 // 4, 92 + n // 2)
        if x % 8 in (6, 7): c = (34, 24, 20)
        return c
    if m == "drywall":
        n = _n(x, y, 293, 8)
        c = (208 + n, 202 + n, 190 + n)
        if x % 16 == 0: c = (150, 146, 136)
        return c
    if m == "log":
        n = _n(x, y // 3, 295, 14)
        c = (120 + n, 84 + n * 3 // 4, 54 + n // 2)
        if y % 8 in (0, 7): c = (64, 42, 28)
        if hnoise(x // 3, y, 297) < .08: c = (90, 62, 40)
        return c
    if m == "sandbag":
        n = _n(x, y, 299, 10)
        c = (176 + n, 152 + n * 3 // 4, 104 + n // 2)
        if x % 12 in (0, 11) or y % 10 in (0, 9): c = (96, 80, 56)
        return c
    if m == "adobe":
        n = _n(x // 2, y // 2, 301, 14)
        c = (190 + n, 144 + n * 3 // 4, 100 + n // 2)
        if hnoise(x, y, 303) < .03: c = (226, 196, 120)
        return c
    if m == "container":
        n = _n(x // 4, y, 305, 8)
        c = (156 + n, 68 + n // 2, 48 + n // 2) if x % 4 in (1, 2) else (118 + n, 46 + n // 2, 34 + n // 2)
        return c
    if m == "stone":
        n = _n(x, y, 307, 14)
        c = (138 + n, 134 + n, 126 + n)
        if x % 16 == 0 or y % 20 == 0: c = (84, 82, 78)
        return c
    if m == "rebar":
        n = _n(x, y, 309, 10)
        c = (112 + n, 112 + n, 110 + n)
        if x % 16 in (7, 8) or y % 16 in (7, 8): c = (136, 136, 132)
        if x % 16 == 0 or y % 16 == 0: c = (70, 70, 72)
        return c
    if m == "steel":
        n = _n(x, y, 311, 8)
        c = (156 + n, 166 + n, 178 + n)
        if (x + y * 2) % 12 in (0, 1): c = (206, 216, 228)
        if x % 16 == 0 or y % 16 == 0: c = (66, 74, 88)
        return c
    if m == "glass":
        return (190, 224, 238)
    n = _n(x, y, 61, 14)
    c = (146 + n, 144 + n, 138 + n)
    if x % 16 == 0 or y % 16 == 0: c = (110, 108, 104)
    if hnoise(x, y, 63) < .02: c = (170, 168, 160)
    return c


def _edges_top(t: Tile, sides: str) -> None:
    """Bevel and outline on the sides (subset of 'NESW') that are exposed."""
    for side in sides:
        for d in range(S):
            for k in range(3):
                x, y = {"N": (d, k), "S": (d, S - 1 - k), "W": (k, d), "E": (S - 1 - k, d)}[side]
                if k == 0:
                    t.put(x, y, mix(t.get(x, y), (12, 10, 20), .8))
                elif k == 1:
                    t.put(x, y, mix(t.get(x, y), (255, 240, 210) if side in "NW" else (20, 18, 40), .38 if side in "NW" else .35))
                else:
                    t.put(x, y, mix(t.get(x, y), (255, 240, 210) if side in "NW" else (20, 18, 40), .14))


def wall_top(m: str, mask: int) -> Image.Image:
    t = Tile((0, 0, 0))
    for y in range(S):
        for x in range(S):
            t.put(x, y, cap(m, x, y))
    missing = "".join(s for b, s in ((1, "N"), (2, "E"), (4, "S"), (8, "W")) if not mask & b)
    _edges_top(t, missing)
    img = t.image()
    for (cx, cy, a, b) in ((0, 0, "N", "W"), (S - 1, 0, "N", "E"), (0, S - 1, "S", "W"), (S - 1, S - 1, "S", "E")):
        if a in missing and b in missing:                                    # rounded outer corner
            for dx, dy in ((0, 0), (1, 0), (0, 1)):
                x = cx + (dx if cx == 0 else -dx)
                y = cy + (dy if cy == 0 else -dy)
                img.putpixel((x, y), (0, 0, 0, 0))
    return img


def wall_face(m: str, mask: int, overlay=None) -> Tile:
    """South front: a lit cap strip on top and the vertical face below. mask: E=1, W=2."""
    t = Tile((0, 0, 0))
    for y in range(S):
        for x in range(S):
            if y < CAP_ROWS:
                c = cap(m, x, y * 2)
                c = mix(c, (255, 240, 210), .18) if y < 2 else mix(c, (20, 18, 40), .0 if y < 6 else .2 + (y - 6) * .12)
            else:
                c = face(m, x, y - CAP_ROWS)
                c = mix(c, (20, 18, 40), max(0, .22 - (y - CAP_ROWS) * .02))        # soft shade under the cap
            t.put(x, y, c)
    for y in range(S):
        if not mask & 2:                                                          # exposed west end: lit edge
            t.put(0, y, mix(t.get(0, y), (12, 10, 20), .8)); t.put(1, y, mix(t.get(1, y), (255, 240, 210), .3))
        if not mask & 1:
            t.put(S - 1, y, mix(t.get(S - 1, y), (12, 10, 20), .8)); t.put(S - 2, y, mix(t.get(S - 2, y), (20, 18, 40), .3))
    for x in range(S):
        t.put(x, S - 1, mix(t.get(x, S - 1), (12, 10, 20), .6))
    return t


# ------------------------------------------------------------------ doors and windows
def door_face(m: str, open_: bool) -> Image.Image:
    t = wall_face(m, 3)
    frame = (58, 40, 28) if m in ("wood", "brick", "pallet", "log", "adobe", "drywall", "sandbag") else (60, 64, 72)
    for y in range(7, S):
        for x in range(5, 27):
            edge = x in (5, 26) or y == 7
            if edge:
                t.put(x, y, frame)
            elif open_:
                t.put(x, y, (26, 22, 28) if y < 16 else mix((46, 40, 44), (20, 18, 24), (y - 16) / 24))
            else:
                if m == "wood":
                    c = face("wood", x + 7, y)
                elif m == "scrap":
                    c = mix(face("scrap", x, y), (60, 66, 74), .45)
                elif m == "brick":
                    c = mix((122, 88, 60), (40, 24, 12), ((x // 5) % 2) * .12)
                else:
                    c = mix(face(m, x, y), (60, 64, 74), .38)
                t.put(x, y, c)
    if not open_:
        for y in (11, 25):
            for x in range(6, 26):
                t.put(x, y, mix(t.get(x, y), (30, 30, 36), .7))
        for (x, y) in ((22, 18), (22, 19), (21, 18)):
            t.put(x, y, (214, 190, 110) if (x + y) % 2 else (150, 130, 70))
        t.put(22, 20, (40, 30, 20))
    else:
        for y in range(8, S):                                                      # the leaf swung inwards: a sliver on the left
            for x in range(6, 10):
                t.put(x, y, mix(face(m, x, y), (20, 18, 40), .15 * (x - 6)))
        for y in range(S - 7, S):
            for x in range(10, 26):
                t.put(x, y, mix(t.get(x, y), (90, 80, 70), .25))                     # threshold
    return t.image()


def window_face(m: str) -> Image.Image:
    t = wall_face(m, 3)
    for y in range(11, 25):
        for x in range(8, 24):
            edge = x in (8, 23) or y in (11, 24)
            if edge:
                t.put(x, y, (50, 36, 28) if m in ("wood", "brick") else (58, 62, 70))
            elif m == "scrap":
                t.put(x, y, (14, 16, 22) if (x + y) % 4 else (88, 94, 102))        # barred slot
            else:
                g = mix((38, 52, 78), (14, 18, 30), (y - 12) / 12)
                if (x - 9) + (y - 12) in (3, 4, 9) and (y - 12) < 9:
                    g = mix(g, (190, 214, 230), .55)
                t.put(x, y, g)
    for x in range(7, 25):
        t.put(x, 25, (210, 204, 190)); t.put(x, 26, (60, 54, 52))                  # sill
    if m != "scrap":
        for y in range(12, 24):
            t.put(15, y, (60, 44, 34)); t.put(16, y, (110, 84, 60))
        for x in range(9, 23):
            t.put(x, 17, (60, 44, 34))
    return t.image()


def barricade_overlay() -> Image.Image:
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sp = Sprite(S, S, salt=77)
    for (y, tilt) in ((10, 1), (17, -1), (24, 1)):
        pts = poly([(2, y - tilt), (30, y + tilt), (30, y + tilt + 5), (2, y - tilt + 5)])
        sp.paint(pts, ramp5((148, 106, 66)), grain=.3, edge=True, reach=3)
        for x in (5, 26):
            sp.set(x, y + 2 + (tilt if x > 16 else -tilt), (34, 34, 40))
    return sp.image()


def crack_overlay(level: int) -> Image.Image:
    sp = Sprite(S, S, salt=90 + level)
    import random
    rng = random.Random(90 + level)
    for _ in range(1 + level * 2):
        x, y = rng.randrange(4, 28), rng.randrange(0, 8)
        for _ in range(12 + level * 6):
            sp.px[(x, y)] = (22, 18, 22)
            if (x + 1, y - 1) not in sp.px and rng.random() < .5:
                sp.px[(x + 1, y - 1)] = (150, 140, 130)
            x = max(0, min(S - 1, x + rng.choice((-1, 0, 0, 1))))
            y = min(S - 1, y + rng.choice((0, 1, 1)))
    if level >= 2:
        for (cx, cy, r) in ((20, 20, 4), (9, 24, 3)):
            for (x, y) in ellipse(cx, cy, r, r * .8):
                if rng.random() < .8:
                    sp.px[(x, y)] = (20, 16, 20) if (x + y) % 3 else (70, 58, 50)
    return sp.image()


# ------------------------------------------------------------------ low barricade and fence
def low_barricade(m: str, mask: int) -> Image.Image:
    sp = Sprite(S, S, salt=110)
    x0 = 0 if not mask & 2 else -2
    x1 = S - 1 if not mask & 1 else S + 1
    top = [(x, y) for x in range(max(0, x0), min(S, x1 + 1)) for y in range(14, 20)]
    body = [(x, y) for x in range(max(0, x0), min(S, x1 + 1)) for y in range(20, 31)]
    for (x, y) in top:
        sp.px[(x, y)] = mix(cap(m, x, y), (255, 240, 210), .15 if y < 16 else 0)
    for (x, y) in body:
        sp.px[(x, y)] = mix(face(m, x, y - 20), (20, 18, 40), .15 if y > 27 else 0)
    sp.shadow = {(x, 31) for x in range(2, 30)}
    sp.outline()
    return sp.image()


def fence(m: str, mask: int) -> Image.Image:
    sp = Sprite(S, S, salt=120)
    style = FENCE_STYLE[m]
    post = ramp5(face(m, 3, 3))
    xs = []
    if not mask & 2: xs.append(0)
    if not mask & 1: xs.append(S - 3)
    if not xs or mask == 3:
        xs = [14]
    x_from, x_to = (0 if mask & 2 else 3), (S if mask & 1 else S - 3)
    if style != "wall":
        for x0 in xs:
            sp.paint(rect(x0, 5, x0 + 2, 30), post, grain=.2, edge=True, reach=3)
    for x in range(x_from, x_to):
        if style == "rails":
            for y in (10, 21):
                sp.px[(x, y)] = mix(face(m, x, y), (255, 240, 200), .12); sp.px[(x, y + 1)] = mix(face(m, x, y), (20, 18, 40), .4)
        elif style == "mesh":
            for y in range(8, 28):
                if (x + y) % 5 == 0 or (x - y) % 5 == 0:
                    sp.px[(x, y)] = (150, 156, 164) if (x + y) % 2 else (108, 114, 122)
        elif style == "wall":
            for y in range(16, 29):
                sp.px[(x, y)] = face(m, x, y - 16)
    if style in ("rails", "mesh"):
        for x in range(x_from, x_to):
            sp.px[(x, 6)] = (176, 182, 190) if style == "mesh" else mix(face(m, x, 6), (255, 240, 200), .15)
    sp.shadow = {(x, 30) for x in range(2, 30)}
    sp.outline()
    return sp.image()


# ------------------------------------------------------------------ floors
def floor(m: str, variant: int) -> Image.Image:
    t = Tile((0, 0, 0))
    o = variant * 5
    for y in range(S):
        for x in range(S):
            if m == "wood":
                row = y // 8
                base = mix((152, 120, 84), (255, 230, 180) if hnoise(row, o, 71) < .5 else (60, 36, 20), .02 + hnoise(row, o, 72) * .12)
                n = _n(x // 4, y, 73 + o, 12)
                c = (base[0] + n, base[1] + n * 3 // 4, base[2] + n // 2)
                if y % 8 == 0: c = mix(c, (255, 226, 176), .18)
                if y % 8 == 7: c = (86, 62, 42)
                if x == (row * 11 + o * 3) % 32: c = (86, 62, 42)
            elif m == "pallet":
                n = _n(x // 3, y, 331 + o, 12)
                c = (172 + n, 138 + n * 3 // 4, 94 + n // 2)
                if y % 8 in (6, 7): c = (40, 28, 22)
                if x % 16 in (2, 13) and y % 8 == 3: c = (96, 96, 104)
            elif m == "drywall":
                cell = ((x // 8) + (y // 8)) % 2
                n = _n(x, y, 333 + o, 6)
                c = ((226 if cell else 196) + n, (222 if cell else 192) + n, (210 if cell else 182) + n)
                if x % 8 == 0 or y % 8 == 0: c = (150, 146, 136)
            elif m == "log":
                row = y // 8
                n = _n(x // 3, y, 335 + o, 12)
                c = (96 + n, 66 + n * 3 // 4, 42 + n // 2)
                if y % 8 == 0: c = mix(c, (255, 226, 176), .15)
                if y % 8 == 7: c = (50, 32, 22)
                if x == (row * 13 + o * 5) % 32: c = (50, 32, 22)
            elif m == "sandbag":
                n = _n(x, y, 337 + o, 14)
                c = (194 + n, 170 + n * 3 // 4, 122 + n // 2)
                if hnoise(x // 2, y // 2, 339 + o) < .12: c = (170, 146, 100)
                if hnoise(x, y, 341 + o) < .02: c = (120, 110, 100)
            elif m == "scrap":
                n = _n(x, y, 75 + o, 10)
                c = (112 + n, 118 + n, 124 + n)
                if (x // 8 + y // 8) % 2 == 0:
                    c = mix(c, (255, 255, 255), .08)
                d = (x % 8, y % 8)
                if d in ((1, 1), (6, 6)): c = (196, 200, 208)
                if (x + y * 2) % 16 in (0, 1) and (x // 8 + y // 8) % 2: c = (150, 156, 164)
                if x % 16 == 0 or y % 16 == 0: c = (60, 64, 72)
            elif m == "adobe":
                n = _n(x // 2, y // 2, 343 + o, 18)
                c = (176 + n, 128 + n * 3 // 4, 86 + n // 2)
                if hnoise(x // 5, y // 5, 345 + o) < .2: c = mix(c, (140, 96, 62), .4)
                if hnoise(x, y, 347 + o) < .02: c = (226, 196, 120)
            elif m == "container":
                n = _n(x // 4, y, 349 + o, 8)
                c = (120 + n, 124 + n, 130 + n) if x % 4 in (1, 2) else (88 + n, 92 + n, 100 + n)
                if y % 16 == 0: c = (56, 60, 68)
            elif m == "brick":
                bx, by = x // 8, y // 8
                horizontal = (bx + by) % 2 == 0
                col = mix((128, 96, 84), (255, 230, 210) if hnoise(bx * 2 + (y // 4 if horizontal else x // 4), by, 77 + o) < .5 else (30, 20, 20),
                          hnoise(bx * 3 + (y // 4 if horizontal else x // 4), by + 1, 78 + o) * .16)
                n = _n(x, y, 79 + o, 10)
                c = (col[0] + n, col[1] + n // 2, col[2] + n // 2)
                if (horizontal and (y % 4 == 3 or x % 8 == 7)) or (not horizontal and (x % 4 == 3 or y % 8 == 7)):
                    c = (74, 62, 58)
            elif m == "stone":
                bx, by = (x + (by_ := y // 11) * 6) // 12, y // 11
                base = mix((128, 124, 116), (220, 216, 200) if hnoise(bx, by, 351 + o) < .5 else (30, 30, 36), hnoise(bx, by, 353 + o) * .2)
                n = _n(x, y, 355 + o, 12)
                c = (base[0] + n, base[1] + n, base[2] + n)
                if y % 11 == 0 or (x + by * 6) % 12 == 0: c = (72, 70, 66)
                elif y % 11 == 1 or (x + by * 6) % 12 == 1: c = mix(c, (255, 250, 230), .2)
            elif m == "rebar":
                n = _n(x, y, 357 + o, 10)
                c = (124 + n, 124 + n, 122 + n)
                if x % 16 == 0 or y % 16 == 0: c = (74, 74, 76)
                elif x % 16 == 1 or y % 16 == 1: c = (156, 156, 152)
                if (x % 16, y % 16) == (8, 8): c = (176, 100, 56)
            elif m == "steel":
                n = _n(x, y, 359 + o, 8)
                c = (140 + n, 150 + n, 162 + n)
                if (x + y * 2) % 12 in (0, 1) and (x // 8 + y // 8) % 2: c = (206, 216, 228)
                if (x - y * 2) % 12 in (0, 1) and (x // 8 + y // 8) % 2 == 0: c = (206, 216, 228)
                if x % 16 == 0 or y % 16 == 0: c = (60, 68, 82)
            else:                                                          # concrete (glass has its own)
                n = _n(x, y, 81 + o, 14)
                c = (150 + n, 148 + n, 142 + n)
                if x % 16 == 0 or y % 16 == 0: c = (104, 102, 98)
                elif x % 16 == 1 or y % 16 == 1: c = (170, 168, 162)
                if hnoise(x, y, 83 + o) < .015: c = (176, 174, 166)
            t.put(x, y, c)
    return t.image()


# ------------------------------------------------------------------ glass: see-through pieces
PANE = (176, 212, 230, 78)          # alpha 78: lets the ground and the light show through
GLASS_FRAME = ramp5((150, 160, 172))


def _glass_streaks(sp: Sprite, x0: int, y0: int, x1: int, y1: int, salt: int) -> None:
    for k in range(0, (x1 - x0) + (y1 - y0), 11):
        for d in range(5):
            x, y = x0 + k - d, y0 + d
            if x0 <= x <= x1 and y0 <= y <= y1:
                sp.px[(x, y)] = (232, 246, 252)


def _finish_glass(sp: Sprite, panes: set) -> Image.Image:
    img = sp.image()
    for (x, y) in panes:
        if (x, y) not in sp.px:
            img.putpixel((x, y), PANE)
    return img


def glass_top(mask: int) -> Image.Image:
    sp = Sprite(S, S, salt=401)
    panes = {(x, y) for x in range(S) for y in range(S)}
    for k in range(S):
        for pos in (0, 15, 16, 31):
            for (x, y) in ((k, pos), (pos, k)):
                sp.px[(x, y)] = GLASS_FRAME[3 if pos in (0, 15) else 1]
    for side, bit in (("N", 1), ("E", 2), ("S", 4), ("W", 8)):
        if not mask & bit:
            for k in range(S):
                x, y = {"N": (k, 0), "S": (k, S - 1), "W": (0, k), "E": (S - 1, k)}[side]
                sp.px[(x, y)] = (12, 10, 20)
    _glass_streaks(sp, 2, 2, 29, 29, 1)
    return _finish_glass(sp, panes)


def glass_face(mask: int) -> Image.Image:
    sp = Sprite(S, S, salt=402)
    panes = {(x, y) for x in range(CAP_ROWS, S) for y in range(S)}
    for x in range(S):
        for y in range(CAP_ROWS):
            sp.px[(x, y)] = GLASS_FRAME[3] if y < 2 else GLASS_FRAME[2] if y < 6 else GLASS_FRAME[0]
    panes = {(x, y) for x in range(S) for y in range(CAP_ROWS, S)}
    for x in (0, 15, 16, 31):
        for y in range(CAP_ROWS, S):
            sp.px[(x, y)] = GLASS_FRAME[3] if x in (0, 15) else GLASS_FRAME[1]
    for x in range(S):
        sp.px[(x, S - 1)] = GLASS_FRAME[0]; sp.px[(x, CAP_ROWS)] = GLASS_FRAME[3]
    _glass_streaks(sp, 2, CAP_ROWS + 2, 29, S - 3, 2)
    return _finish_glass(sp, panes)


def glass_door(open_: bool) -> Image.Image:
    sp = Sprite(S, S, salt=403)
    for x in range(S):
        for y in range(CAP_ROWS):
            sp.px[(x, y)] = GLASS_FRAME[3] if y < 2 else GLASS_FRAME[2] if y < 6 else GLASS_FRAME[0]
    panes = {(x, y) for x in range(S) for y in range(CAP_ROWS, S)}
    if not open_:
        for y in range(7, S):
            for x in (5, 26):
                sp.px[(x, y)] = GLASS_FRAME[2]
        for x in range(5, 27):
            sp.px[(x, 7)] = GLASS_FRAME[2]
        for y in range(19, 22):
            sp.px[(22, y)] = (214, 190, 110)
        _glass_streaks(sp, 7, 10, 24, 30, 3)
    else:
        for y in range(7, S):
            for x in (5, 6, 7):
                sp.px[(x, y)] = GLASS_FRAME[2]
        for x in range(5, 27):
            sp.px[(x, 7)] = GLASS_FRAME[2]
    return _finish_glass(sp, panes)


def glass_window() -> Image.Image:
    sp = Sprite(S, S, salt=404)
    for x in range(S):
        for y in range(CAP_ROWS):
            sp.px[(x, y)] = GLASS_FRAME[3] if y < 2 else GLASS_FRAME[2] if y < 6 else GLASS_FRAME[0]
    for y in range(CAP_ROWS, S):
        for x in range(S):
            if x < 3 or x > 28 or y > S - 4:
                sp.px[(x, y)] = GLASS_FRAME[2]
    panes = {(x, y) for x in range(3, 29) for y in range(CAP_ROWS, S - 3)}
    for y in range(CAP_ROWS, S - 3):
        sp.px[(15, y)] = GLASS_FRAME[1]; sp.px[(16, y)] = GLASS_FRAME[3]
    _glass_streaks(sp, 4, CAP_ROWS + 1, 27, S - 5, 4)
    return _finish_glass(sp, panes)


def glass_low(mask: int, fence_: bool) -> Image.Image:
    sp = Sprite(S, S, salt=405)
    top, bottom = (12, 28) if not fence_ else (8, 28)
    for x in range(S):
        sp.px[(x, top)] = GLASS_FRAME[3]; sp.px[(x, top + 1)] = GLASS_FRAME[2]
        sp.px[(x, bottom)] = GLASS_FRAME[1]; sp.px[(x, bottom + 1)] = GLASS_FRAME[0]
    xs = ([0] if not mask & 2 else []) + ([S - 3] if not mask & 1 else [])
    for x0 in xs or [14]:
        for y in range(top, bottom + 2):
            for dx in range(3):
                sp.px[(x0 + dx, y)] = GLASS_FRAME[2 if dx == 1 else 1]
    panes = {(x, y) for x in range(S) for y in range(top + 2, bottom)}
    _glass_streaks(sp, 3, top + 3, 28, bottom - 2, 5)
    sp.shadow = {(x, 30) for x in range(2, 30)}
    return _finish_glass(sp, panes)


def glass_floor(variant: int) -> Image.Image:
    sp = Sprite(S, S, salt=406 + variant)
    panes = {(x, y) for x in range(S) for y in range(S)}
    for k in range(S):
        for pos in (0, 15, 16, 31):
            sp.px[(k, pos)] = GLASS_FRAME[1]; sp.px[(pos, k)] = GLASS_FRAME[1]
    for (x, y) in ((3, 3), (12, 3), (3, 12), (12, 12), (19, 3), (28, 3), (19, 12), (28, 12)):
        sp.px[(x, y + variant * 16)] = GLASS_FRAME[4] if y + variant * 16 < S else GLASS_FRAME[4]
    _glass_streaks(sp, 1, 1, 30, 30, 6 + variant)
    return _finish_glass(sp, panes)


# ------------------------------------------------------------------ objects with a function
def campfire(frame: int) -> Sprite:
    sp = Sprite(32, 40, salt=130 + frame)
    sp.shadow = ellipse(16, 35, 13, 3.2)
    for (cx, cy) in ((6, 31), (10, 34), (16, 35), (22, 34), (26, 31), (8, 28), (24, 28)):
        sp.paint(ellipse(cx, cy, 3.6, 2.8), ramp5((120, 118, 114)), grain=.3, edge=True, reach=3)
    sp.paint(poly([(8, 31), (26, 25), (28, 28), (10, 34)]), ramp5((96, 66, 42)), grain=.3, edge=True, reach=3)
    sp.paint(poly([(6, 26), (24, 32), (22, 35), (4, 29)]), ramp5((112, 78, 50)), grain=.3, edge=True, reach=3)
    sp.paint(ellipse(16, 30, 7, 2.8), [(40, 20, 12), (90, 30, 14), (160, 60, 20), (220, 110, 30), (250, 170, 60)], reach=3)
    flames = [([(10, 30), (9, 20 - frame), (13, 12 + frame % 2), (15, 21), (16, 30)], (255, 126, 30)),
              ([(14, 30), (14, 16), (17, 5 + frame * 2 % 3), (20, 15), (22, 30)], (255, 150, 40)),
              ([(13, 30), (15, 22), (17, 14 - frame % 2), (19, 23), (20, 30)], (255, 214, 96))]
    for pts, col in flames:
        for (x, y) in poly(pts):
            sp.set(x, y, col, True)
    for i in range(4):
        sp.set(10 + (frame * 5 + i * 6) % 14, 4 + (i * 3 + frame * 2) % 9, (255, 200, 90), True)
    sp.meta["fire"] = (16, 18)
    sp.outline()
    return sp


def lantern() -> Sprite:
    sp = Sprite(18, 30, salt=140)
    sp.shadow = ellipse(9, 27, 7, 2)
    sp.paint(rect(4, 24, 13, 26), ramp5((70, 66, 66)), edge=True, reach=2)
    sp.paint(rect(5, 10, 12, 24), ramp5((84, 88, 96)), edge=True, reach=3)
    for (x, y) in rect(6, 11, 11, 22):
        sp.set(x, y, (255, 214, 120) if y > 14 else (230, 160, 70), True)
    for y in range(11, 23):
        sp.set(8, y, (80, 60, 40)); sp.set(9, y, (80, 60, 40)) if y % 2 else None
    sp.paint(poly([(4, 10), (9, 5), (14, 10)]), ramp5((70, 66, 66)), edge=True, reach=3)
    sp.paint(rect(8, 2, 9, 5), ramp5((90, 90, 96)))
    sp.meta["light"] = (9, 16)
    sp.outline()
    return sp


def rain_barrel() -> Sprite:
    sp = Sprite(24, 32, salt=150)
    sp.shadow = ellipse(12, 29, 10, 2.6)
    sp.paint(poly([(3, 9), (21, 9), (22, 27), (2, 27)]), ramp5((60, 96, 128)), grain=.2, edge=True, reach=6)
    for y in (13, 22):
        for x in range(3, 22):
            sp.set(x, y, (36, 56, 78))
    sp.paint(ellipse(12, 9, 9, 3), ramp5((76, 112, 146)), edge=True, reach=3)
    sp.paint(ellipse(12, 9, 7, 2), [(26, 54, 86), (40, 82, 120), (60, 120, 156), (120, 180, 206), (190, 226, 238)], reach=3)
    sp.paint(poly([(18, 2), (23, 2), (21, 9), (19, 9)]), ramp5((140, 144, 152)), edge=True, reach=2)          # gutter
    sp.set(20, 8, (190, 226, 238))
    sp.paint(rect(11, 26, 13, 28), ramp5((190, 150, 70)), edge=True, reach=2)
    sp.outline()
    return sp


def barbed_wire() -> Sprite:
    sp = Sprite(32, 32, salt=160)
    sp.shadow = {(x, 29) for x in range(3, 29)}
    for k, y in enumerate((15, 19, 23)):
        for x in range(2, 30):
            yy = y + int(round(math.sin(x * .9 + k) * 2))
            sp.px[(x, yy)] = (150, 156, 164) if x % 2 else (96, 102, 110)
        for x in range(4 + k * 2, 29, 6):
            sp.px[(x, y - 3)] = (200, 206, 214); sp.px[(x, y + 3)] = (200, 206, 214); sp.px[(x + 1, y)] = (60, 62, 70)
    for x in (2, 29):
        sp.paint(rect(x - 1, 8, x + 1, 28), ramp5((96, 70, 48)), edge=True, reach=2)
    return sp


def sofa() -> Sprite:
    sp = Sprite(96, 44, salt=170)
    sp.shadow = ellipse(48, 40, 44, 3)
    fab = ramp5((92, 112, 98))
    sp.paint(rect(3, 4, 92, 20), fab, grain=.2, edge=True, reach=8)
    sp.paint(rect(1, 14, 8, 36), ramp5((76, 94, 82)), grain=.2, edge=True, reach=6)
    sp.paint(rect(87, 14, 94, 36), ramp5((76, 94, 82)), grain=.2, edge=True, reach=6)
    for x0 in (9, 33, 57):
        sp.paint(rect(x0, 20, x0 + 22, 35), ramp5((110, 132, 116)), grain=.2, edge=True, reach=6)
    for x in range(14, 88, 24):
        sp.set(x, 10, fab[0]); sp.set(x + 1, 10, fab[0])
    for x in (4, 88):
        sp.paint(rect(x, 36, x + 3, 40), ramp5((70, 50, 36)), edge=True, reach=2)
    sp.outline()
    return sp


def build() -> dict[str, Image.Image]:
    out: dict[str, Image.Image] = {}
    for m in MATERIALS:
        if m == "glass":
            for mask in range(16):
                out[f"wall_glass_top_{mask}"] = glass_top(mask)
            for mask in range(4):
                out[f"wall_glass_face_{mask}"] = glass_face(mask)
                out[f"lowbarricade_glass_{mask}"] = glass_low(mask, False)
                out[f"fence_glass_{mask}"] = glass_low(mask, True)
            out["door_glass_closed"], out["door_glass_open"] = glass_door(False), glass_door(True)
            out["window_glass"] = glass_window()
            for v in range(2):
                out[f"floor_glass_{v}"] = glass_floor(v)
            continue
        for mask in range(16):
            out[f"wall_{m}_top_{mask}"] = wall_top(m, mask)
        for mask in range(4):
            out[f"wall_{m}_face_{mask}"] = wall_face(m, mask).image()
            out[f"lowbarricade_{m}_{mask}"] = low_barricade(m, mask)
            out[f"fence_{m}_{mask}"] = fence(m, mask)
        out[f"door_{m}_closed"] = door_face(m, False)
        out[f"door_{m}_open"] = door_face(m, True)
        out[f"window_{m}"] = window_face(m)
        for v in range(2):
            out[f"floor_{m}_{v}"] = floor(m, v)
    out["overlay_barricade"] = barricade_overlay()
    for level in (1, 2):
        out[f"overlay_crack_{level}"] = crack_overlay(level)
    for f in range(4):
        out[f"obj_campfire_{f}"] = campfire(f).image()
    out["obj_lantern"] = lantern().image()
    out["obj_rain_barrel"] = rain_barrel().image()
    out["obj_barbed_wire"] = barbed_wire().image()
    out["obj_sofa"] = sofa().image()
    return out
