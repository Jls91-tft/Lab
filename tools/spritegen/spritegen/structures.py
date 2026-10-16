"""Building pieces for D-020: 4 materials x wall, door, window, low barricade, fence, floor.

Walls join their neighbours (autotiling): `wall_<m>_top_<mask>` uses N=1 E=2 S=4 W=8 (a bit is set
when that neighbour is the same wall); `wall_<m>_face_<mask>` is the south front shown when nothing
wall-like is below, with E=1 W=2. Damage is a transparent overlay drawn over any piece.
"""

from __future__ import annotations

import math

from PIL import Image

from .gfx import Color, Sprite, Tile, ellipse, hnoise, mix, poly, ramp5, rect

MATERIALS = ("wood", "scrap", "brick", "concrete")
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
    frame = (58, 40, 28) if m in ("wood", "brick") else (60, 64, 72)
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
                    c = mix(face("concrete", x, y), (76, 82, 92), .6)
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
                t.put(x, y, mix(face("wood" if m != "scrap" else "scrap", x, y), (20, 18, 40), .15 * (x - 6)))
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
    post = ramp5({"wood": (136, 96, 60), "scrap": (124, 130, 136), "brick": (130, 72, 60), "concrete": (150, 148, 142)}[m])
    xs = []
    if not mask & 2: xs.append(0)
    if not mask & 1: xs.append(S - 3)
    if not xs or mask == 3:
        xs = [14]
    for x0 in xs:
        sp.paint(rect(x0, 5, x0 + 2, 30), post, grain=.2, edge=True, reach=3)
    for x in range(0 if mask & 2 else 3, S if mask & 1 else S - 3):
        if m == "wood":
            for y in (10, 21):
                sp.px[(x, y)] = (150, 106, 66) if x % 8 else (110, 78, 48); sp.px[(x, y + 1)] = (96, 66, 40)
        elif m == "scrap":
            for y in range(8, 28):
                if (x + y) % 5 == 0 or (x - y) % 5 == 0:
                    sp.px[(x, y)] = (150, 156, 164) if (x + y) % 2 else (108, 114, 122)
        elif m == "brick":
            for y in range(18, 29):
                sp.px[(x, y)] = face("brick", x, y - 18)
        else:
            for y in range(16, 29):
                sp.px[(x, y)] = face("concrete", x, y - 16)
    if m in ("wood", "scrap"):
        for x in range(0 if mask & 2 else 3, S if mask & 1 else S - 3):
            sp.px[(x, 6)] = (176, 182, 190) if m == "scrap" else (150, 106, 66)
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
            elif m == "scrap":
                n = _n(x, y, 75 + o, 10)
                c = (112 + n, 118 + n, 124 + n)
                if (x // 8 + y // 8) % 2 == 0:
                    c = mix(c, (255, 255, 255), .08)
                d = (x % 8, y % 8)
                if d in ((1, 1), (6, 6)): c = (196, 200, 208)
                if (x + y * 2) % 16 in (0, 1) and (x // 8 + y // 8) % 2: c = (150, 156, 164)      # diamond-plate ribs
                if x % 16 == 0 or y % 16 == 0: c = (60, 64, 72)
            elif m == "brick":                                               # basket-weave pavers, greyer than the walls
                bx, by = x // 8, y // 8
                horizontal = (bx + by) % 2 == 0
                px_, py_ = (x % 8, y % 4) if horizontal else (x % 4, y % 8)
                cell = ((x % 8) // 1, (y % 4) // 1) if horizontal else ((x % 4), (y % 8))
                col = mix((128, 96, 84), (255, 230, 210) if hnoise(bx * 2 + (y // 4 if horizontal else x // 4), by, 77 + o) < .5 else (30, 20, 20),
                          hnoise(bx * 3 + (y // 4 if horizontal else x // 4), by + 1, 78 + o) * .16)
                n = _n(x, y, 79 + o, 10)
                c = (col[0] + n, col[1] + n // 2, col[2] + n // 2)
                if (horizontal and (y % 4 == 3 or x % 8 == 7)) or (not horizontal and (x % 4 == 3 or y % 8 == 7)):
                    c = (74, 62, 58)
            else:
                n = _n(x, y, 81 + o, 14)
                c = (150 + n, 148 + n, 142 + n)
                if x % 16 == 0 or y % 16 == 0: c = (104, 102, 98)
                elif x % 16 == 1 or y % 16 == 1: c = (170, 168, 162)
                if hnoise(x, y, 83 + o) < .015: c = (176, 174, 166)
            t.put(x, y, c)
    return t.image()


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
