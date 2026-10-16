"""Standing decoration (`decor_*`) and flat marks on the floor (`decal_*`), 32 px per tile.

Sizes follow the footprints DecorPlanner uses: one tile unless noted (tables 3x1 and 2x1,
beds 2x2, armchair 1x2, rugs 3x1). Standing pieces rest on their bottom edge.
"""

from __future__ import annotations

import math
import random

from PIL import Image

from .gfx import Color, Sprite, capsule, ellipse, hnoise, mix, poly, ramp5, rect

GLASS = ramp5((38, 50, 70))
BLOOD = [(104, 24, 30), (138, 34, 38), (168, 52, 50)]


def _shadow(S: Sprite, cx: float, cy: float, rx: float, ry: float) -> None:
    S.shadow = ellipse(cx, cy, rx, ry)


# ------------------------------------------------------------------ cars
CAR_COLORS = {
    "green": (84, 112, 82), "grey": (150, 154, 160), "orange": (200, 110, 44), "burnt": (46, 40, 40),
}


def car_h(color: str) -> Sprite:
    burnt = color == "burnt"
    seed = sum(map(ord, color))
    rng = random.Random(seed)
    S = Sprite(72, 40, salt=seed)
    body = ramp5(CAR_COLORS[color])
    glass = ramp5((18, 20, 26)) if burnt else GLASS
    _shadow(S, 36, 36, 33, 3.6)
    S.paint(poly([(21, 18), (28, 8), (49, 8), (56, 18)]), body, grain=.1, edge=True)
    S.paint(poly([(25, 17), (30, 10.5), (37, 10.5), (37, 17)]), glass)
    S.paint(poly([(39, 17), (39, 10.5), (47, 10.5), (52, 17)]), glass)
    if not burnt:
        for i in range(4):
            S.set(28 + i, 15 - i * 1.2, glass[4])
    S.paint(poly([(2, 30), (2, 22), (11, 19), (22, 18), (54, 18), (65, 20), (70, 25), (70, 30)]), body, grain=.12, edge=True)
    for y in range(19, 30):
        S.set(38, y, body[0])
    S.set(39, 23, body[4]); S.set(40, 23, body[4])
    for x in range(4, 68):
        S.set(x, 27, body[1])
    chrome = ramp5((60, 56, 56) if burnt else (170, 172, 180))
    S.paint(rect(1, 27, 4, 30), chrome, edge=True)
    S.paint(rect(68, 27, 71, 30), chrome, edge=True)
    if not burnt:
        S.set(3, 23, (150, 30, 30)); S.set(3, 22, (110, 20, 24)); S.set(67, 23, (230, 226, 170)); S.set(68, 23, (180, 176, 120))
    for _ in range(14 if burnt else 4):
        S.tint(ellipse(rng.randint(6, 64), rng.randint(20, 28), rng.uniform(1.5, 4), rng.uniform(1, 2.2)),
               (150, 82, 40) if not burnt else (120, 60, 30), .65, .85)
    if burnt:
        for _ in range(30):
            S.set(rng.randint(5, 66), rng.randint(9, 29), rng.choice(((24, 22, 24), (70, 52, 44))))
    else:
        for (x, y) in ((33, 12), (34, 13), (35, 14), (32, 14), (36, 11)):
            S.set(x, y, (10, 12, 18))
    for wx in (15, 57):
        flat = (wx == 57 and not burnt)
        ry = 3.6 if flat else 5
        S.paint(ellipse(wx, 32 + (1.4 if flat else 0), 5.8, ry), ramp5((40, 40, 46)), edge=True)
        S.paint(ellipse(wx, 32 + (1.4 if flat else 0), 2.6, min(2.6, ry - 1)), ramp5((150, 150, 158) if not burnt else (84, 70, 62)))
    S.meta["foot"] = (36, 36)
    S.outline()
    return S


def car_v(color: str) -> Sprite:
    burnt = color == "burnt"
    seed = sum(map(ord, color)) + 5
    rng = random.Random(seed)
    S = Sprite(44, 60, salt=seed)
    body = ramp5(CAR_COLORS[color])
    glass = ramp5((18, 20, 26)) if burnt else GLASS
    _shadow(S, 22, 55, 19, 3.4)
    for wx in (5, 38):
        S.paint(rect(wx - 2, 38, wx + 2, 52), ramp5((34, 34, 40)), edge=True)
    S.paint(poly([(6, 38), (6, 20), (11, 8), (33, 8), (38, 20), (38, 38)]), body, grain=.12, edge=True)       # cabin
    S.paint(poly([(10, 19), (13, 11), (31, 11), (34, 19)]), glass)
    S.paint(poly([(3, 52), (3, 31), (8, 25), (36, 25), (41, 31), (41, 52)]), body, grain=.12, edge=True)     # hood
    for x in range(8, 36):
        S.set(x, 33, body[1])
    for (x, y) in ((8, 40), (9, 40), (34, 40), (35, 40)):
        S.set(x, y, (230, 226, 170) if not burnt else (70, 60, 50))
    S.paint(rect(8, 46, 35, 51), ramp5((170, 172, 180) if not burnt else (60, 56, 56)), edge=True)
    for x in range(14, 30, 2):
        S.set(x, 42, (24, 24, 28))
    S.paint(rect(18, 47, 25, 49), ramp5((226, 226, 214)))
    for _ in range(14 if burnt else 4):
        S.tint(ellipse(rng.randint(8, 36), rng.randint(28, 48), rng.uniform(1.5, 4), rng.uniform(1.5, 3)),
               (150, 82, 40) if not burnt else (120, 60, 30), .65, .85)
    if not burnt:
        for (x, y) in ((20, 14), (21, 15), (22, 16), (19, 16)):
            S.set(x, y, (10, 12, 18))
    S.meta["foot"] = (22, 55)
    S.outline()
    return S


# ------------------------------------------------------------------ street furniture
def bin_metal() -> Sprite:
    S = Sprite(18, 28, salt=3)
    _shadow(S, 9, 24, 8, 2.4)
    S.paint(poly([(3, 8), (15, 8), (14, 24), (4, 24)]), ramp5((112, 118, 126)), grain=.2, edge=True)
    for y in (12, 18):
        for x in range(4, 15):
            S.set(x, y, (66, 70, 78))
    S.paint(ellipse(9, 8, 6.6, 2.4), ramp5((150, 156, 164)), edge=True)
    S.paint(ellipse(9, 6, 2, 1.2), ramp5((60, 64, 72)))
    S.tint(ellipse(7, 20, 2.5, 2), (140, 80, 44), .6)
    S.outline()
    return S


def bin_green() -> Sprite:
    S = Sprite(18, 28, salt=4)
    _shadow(S, 9, 24, 8, 2.4)
    S.paint(poly([(3, 8), (15, 8), (14, 24), (4, 24)]), ramp5((58, 106, 70)), grain=.15, edge=True)
    S.paint(poly([(2, 5), (16, 5), (15, 9), (3, 9)]), ramp5((46, 86, 58)), edge=True)
    S.paint(rect(13, 5, 15, 7), ramp5((70, 74, 80)))
    for x in (6, 11):
        S.set(x, 14, (40, 76, 52)); S.set(x, 18, (40, 76, 52))
    S.paint(ellipse(6, 22, 2, 1.4), ramp5((30, 30, 34)), edge=True)
    S.paint(ellipse(13, 22, 2, 1.4), ramp5((30, 30, 34)), edge=True)
    S.outline()
    return S


def trash_bag() -> Sprite:
    S = Sprite(22, 20, salt=6)
    _shadow(S, 11, 17, 9, 2)
    S.paint(ellipse(11, 11, 8.6, 6.6), ramp5((36, 40, 44)), grain=.3, edge=True)
    S.paint(poly([(8, 6), (11, 1.4), (14, 6)]), ramp5((36, 40, 44)))
    for (x, y) in ((8, 9), (13, 8), (9, 11)):
        S.set(x, y, (120, 126, 140))
    S.set(14, 13, (170, 150, 90)); S.set(6, 12, (200, 196, 180))
    S.outline()
    return S


def hydrant() -> Sprite:
    S = Sprite(16, 26, salt=7)
    _shadow(S, 8, 22, 7, 2)
    red = ramp5((190, 48, 44))
    S.paint(poly([(4, 8), (12, 8), (13, 22), (3, 22)]), red, grain=.1, edge=True)
    S.paint(ellipse(8, 7, 5.2, 3), red, edge=True)
    S.paint(rect(1, 12, 3, 15), red, edge=True); S.paint(rect(13, 12, 15, 15), red, edge=True)
    S.paint(rect(5, 3, 10, 5), ramp5((226, 200, 80)), edge=True)
    S.set(8, 12, (230, 230, 220)); S.set(8, 13, (230, 230, 220))
    S.outline()
    return S


def mailbox() -> Sprite:
    S = Sprite(20, 44, salt=8)
    _shadow(S, 10, 41, 6, 1.8)
    S.paint(rect(9, 20, 11, 40), ramp5((84, 62, 44)), grain=.2, edge=True)
    blue = ramp5((60, 98, 160))
    S.paint(poly([(2, 12), (4, 6), (16, 6), (18, 12), (18, 21), (2, 21)]), blue, edge=True)
    S.paint(rect(5, 13, 15, 14), ramp5((20, 24, 34)))
    S.paint(rect(14, 4, 16, 8), ramp5((200, 60, 56)), edge=True)
    S.outline()
    return S


def lamp() -> Sprite:
    S = Sprite(24, 96, salt=9)
    _shadow(S, 12, 92, 7, 2)
    steel = ramp5((84, 90, 98))
    S.paint(rect(11, 14, 13, 90), steel, edge=True)
    S.paint(ellipse(12, 89, 4.4, 1.8), ramp5((60, 66, 74)), edge=True)
    S.paint(poly([(11, 14), (11, 9), (8, 4), (3, 3), (3, 5), (8, 8), (13, 14)]), steel, edge=True)
    S.paint(rect(1, 3, 7, 6), ramp5((70, 76, 84)), edge=True)
    S.set(2, 6, (255, 236, 170), True); S.set(3, 6, (255, 236, 170), True); S.set(4, 6, (255, 244, 200), True)
    S.meta["light"] = (3, 7)
    S.outline()
    return S


def bench() -> Sprite:
    S = Sprite(32, 28, salt=10)
    _shadow(S, 16, 25, 14, 2)
    wood = ramp5((136, 96, 60))
    for y in (5, 10):
        S.paint(rect(2, y, 29, y + 3), wood, grain=.2, edge=True)
    for y in (15, 19):
        S.paint(rect(3, y, 28, y + 2), wood, grain=.2, edge=True)
    for x in (4, 25):
        S.paint(rect(x, 14, x + 2, 25), ramp5((60, 64, 72)), edge=True)
        S.paint(rect(x, 3, x + 2, 14), ramp5((60, 64, 72)))
    S.outline()
    return S


def cone() -> Sprite:
    S = Sprite(16, 22, salt=11)
    _shadow(S, 8, 19, 7, 2)
    S.paint(poly([(8, 1), (12.5, 15), (3.5, 15)]), ramp5((226, 104, 40)), edge=True)
    S.paint(poly([(6.5, 6), (9.5, 6), (10.4, 9), (5.6, 9)]), ramp5((236, 236, 230)))
    S.paint(rect(2, 15, 13, 17), ramp5((60, 60, 66)), edge=True)
    S.outline()
    return S


def barrel() -> Sprite:
    S = Sprite(20, 26, salt=12)
    _shadow(S, 10, 22, 9, 2.4)
    S.paint(poly([(3, 7), (17, 7), (18, 22), (2, 22)]), ramp5((112, 82, 62)), grain=.25, edge=True)
    for y in (10, 18):
        for x in range(3, 18):
            S.set(x, y, (52, 38, 34))
    for (x, y) in ((6, 13), (11, 15), (8, 20), (14, 9)):
        S.set(x, y, (150, 70, 40))
    S.paint(ellipse(10, 7, 7.6, 2.8), ramp5((74, 64, 62)), edge=True)
    S.paint(ellipse(10, 7, 5.4, 1.8), ramp5((36, 28, 26)))
    S.outline()
    return S


def boxes(kind: int) -> Sprite:
    rng = random.Random(30 + kind)
    S = Sprite(24 if kind == 0 else 24, 24 if kind == 0 else 30, salt=30 + kind)
    _shadow(S, 12, S.h - 4, 10, 2.2)
    card = ramp5((166, 126, 82))
    if kind == 0:
        S.paint(rect(2, 8, 21, 20), card, grain=.2, edge=True)
        S.paint(rect(2, 8, 21, 11), ramp5((190, 150, 100)), edge=True)
        S.paint(rect(9, 8, 14, 20), ramp5((210, 190, 150)))
    else:
        S.paint(rect(1, 14, 14, 25), card, grain=.2, edge=True)
        S.paint(rect(10, 10, 22, 25), ramp5((140, 104, 66)), grain=.2, edge=True)
        S.paint(rect(4, 5, 13, 13), ramp5((190, 150, 100)), grain=.2, edge=True)
        S.paint(rect(5, 19, 9, 21), ramp5((236, 230, 214)))
    S.outline()
    return S


def bush(kind: int) -> Sprite:
    rng = random.Random(40 + kind)
    S = Sprite(22, 26 if kind != 1 else 28, salt=40 + kind)
    _shadow(S, 11, S.h - 4, 9, 2.4)
    base = {0: (62, 100, 56), 1: (52, 90, 70), 2: (110, 98, 62)}[kind]
    for (cx, cy, r) in ((11, S.h - 11, 8), (6, S.h - 9, 5.4), (16, S.h - 9, 5.4), (11, S.h - 15, 6)):
        S.paint(ellipse(cx, cy, r, r * .9), ramp5(mix(base, (255, 255, 200), rng.uniform(-.05, .1))), grain=.35)
    if kind == 2:
        for _ in range(8):
            x, y = rng.randint(5, 17), rng.randint(6, S.h - 6)
            S.set(x, y, (70, 54, 38)); S.set(x + 1, y - 2, (70, 54, 38))
    S.outline()
    return S


def tree(kind: int) -> Sprite:
    rng = random.Random(50 + kind)
    S = Sprite(40, 60, salt=50 + kind)
    _shadow(S, 20, 56, 12, 3)
    if kind == 2:                                   # dead tree
        bark = ramp5((92, 78, 66))
        S.paint(poly([(17, 36), (23, 36), (24, 54), (27, 57), (13, 57), (16, 54)]), bark, grain=.3, edge=True)
        for a, b, c in (((20, 36), (11, 22), 1.3), ((20, 38), (30, 20), 1.3), ((20, 30), (20, 10), 1.6), ((12, 24), (6, 17), 1), ((29, 22), (34, 15), 1), ((20, 20), (14, 12), 1)):
            S.paint(capsule(a, b, c), bark, edge=True)
        S.outline()
        return S
    if kind == 1:                                   # conifer
        S.paint(poly([(18, 44), (22, 44), (22, 56), (18, 56)]), ramp5((96, 70, 50)), grain=.3, edge=True)
        for i, (w, y) in enumerate(((16, 38), (13, 28), (10, 19), (6, 11))):
            S.paint(poly([(20 - w, y + 8), (20, y - 8), (20 + w, y + 8)]), ramp5(mix((44, 84, 56), (255, 255, 200), .05 * i)), grain=.35, edge=True)
    else:
        S.paint(poly([(17, 34), (23, 34), (24, 52), (27, 55), (13, 55), (16, 52)]), ramp5((96, 70, 50)), grain=.3, edge=True)
        for (cx, cy, r, tone) in ((20, 24, 12, -.15), (11, 27, 9, -.1), (29, 27, 9, -.1), (20, 14, 10, .05), (13, 17, 8, 0), (28, 18, 8, .05), (20, 22, 8, .12)):
            S.paint(ellipse(cx, cy, r, r * .95), ramp5(mix((60, 98, 54), (255, 255, 200) if tone > 0 else (10, 30, 20), abs(tone))), grain=.35)
    for _ in range(60):
        x, y = rng.randint(6, 34), rng.randint(4, 36)
        if (x, y) in S.px:
            S.set(x, y, rng.choice(((120, 164, 80), (44, 74, 44), (138, 176, 92))))
    S.outline()
    return S


# ------------------------------------------------------------------ indoor furniture
def table(w: int, round_: bool = False, seed: int = 60) -> Sprite:
    S = Sprite(w * 32, 32, salt=seed)
    _shadow(S, w * 16, 28, w * 14, 2.4)
    wood = ramp5((150, 108, 70))
    if round_:
        S.paint(rect(14, 14, 17, 27), ramp5((96, 68, 46)), edge=True)
        S.paint(ellipse(16, 13, 13, 8), wood, grain=.2, edge=True)
    else:
        for x in (3, w * 32 - 6):
            S.paint(rect(x, 16, x + 2, 27), ramp5((96, 68, 46)), edge=True)
        S.paint(rect(1, 6, w * 32 - 2, 19), wood, grain=.2, edge=True)
        S.paint(rect(1, 17, w * 32 - 2, 20), ramp5((108, 76, 50)), edge=True)
        if w == 2:
            S.tint(ellipse(5, 12, 4, 6), (150, 108, 70), .0)
    S.outline()
    return S


def table_oval() -> Sprite:
    S = Sprite(64, 32, salt=61)
    _shadow(S, 32, 28, 28, 2.4)
    for x in (12, 49):
        S.paint(rect(x, 17, x + 2, 27), ramp5((96, 68, 46)), edge=True)
    S.paint(ellipse(32, 13, 30, 9), ramp5((150, 108, 70)), grain=.2, edge=True)
    S.paint(ellipse(32, 15, 30, 8), ramp5((120, 84, 54)))
    S.paint(ellipse(32, 12, 28, 7), ramp5((156, 114, 74)), grain=.2)
    S.outline()
    return S


def chair(kind: int) -> Sprite:
    S = Sprite(20, 26 if kind == 0 else 30, salt=70 + kind)
    _shadow(S, 10, S.h - 3, 8, 2)
    wood = ramp5((126, 88, 56) if kind == 0 else (88, 90, 98))
    top = S.h - 14
    for x in (3, 14):
        S.paint(rect(x, top + 4, x + 2, S.h - 3), ramp5((84, 60, 40)), edge=True)
    S.paint(rect(2, top, 17, top + 5), wood, grain=.2, edge=True)
    S.paint(rect(3, 2, 16, top - 1), wood, grain=.2, edge=True)
    if kind:
        S.paint(rect(5, 4, 14, top - 3), ramp5((140, 60, 56)))
    S.outline()
    return S


def counter(kind: int) -> Sprite:
    S = Sprite(32, 32, salt=80 + kind)
    S.paint(rect(1, 3, 30, 12), ramp5((200, 196, 188)), grain=.1, edge=True)
    S.paint(rect(1, 13, 30, 30), ramp5((120, 92, 64)), grain=.2, edge=True)
    for x0 in (3, 17):
        S.paint(rect(x0, 15, x0 + 11, 28), ramp5((142, 108, 74)), grain=.2, edge=True)
        S.paint(rect(x0 + 4, 20, x0 + 7, 21), ramp5((214, 190, 110)))
    if kind == 1:
        S.paint(rect(8, 5, 22, 10), ramp5((130, 136, 144)), edge=True)
        S.paint(rect(14, 3, 16, 6), ramp5((190, 194, 202)), edge=True)
    elif kind == 2:
        for cx in (9, 22):
            S.paint(ellipse(cx, 8, 4, 2.6), ramp5((40, 40, 46)), edge=True)
    elif kind == 3:
        S.paint(rect(5, 4, 11, 9), ramp5((190, 70, 60)), edge=True)
        S.paint(ellipse(20, 7, 3.4, 2.6), ramp5((226, 226, 214)), edge=True)
    S.outline()
    return S


def stove() -> Sprite:
    S = Sprite(32, 32, salt=90)
    S.paint(rect(1, 3, 30, 11), ramp5((180, 184, 190)), grain=.1, edge=True)
    for (cx, cy) in ((9, 7), (22, 7)):
        S.paint(ellipse(cx, cy, 4.4, 2.8), ramp5((40, 40, 46)), edge=True)
        S.paint(ellipse(cx, cy, 2, 1.2), ramp5((120, 50, 40)))
    S.paint(rect(1, 12, 30, 30), ramp5((150, 154, 160)), grain=.1, edge=True)
    S.paint(rect(5, 17, 26, 27), ramp5((52, 56, 62)), edge=True)
    S.paint(rect(6, 14, 25, 15), ramp5((200, 204, 210)))
    for x in (8, 14, 20):
        S.set(x, 13, (230, 70, 60))
    S.outline()
    return S


def plant(kind: int) -> Sprite:
    rng = random.Random(100 + kind)
    S = Sprite(18, 30 if kind == 0 else 24, salt=100 + kind)
    _shadow(S, 9, S.h - 3, 7, 1.8)
    S.paint(poly([(4, S.h - 10), (14, S.h - 10), (12, S.h - 3), (6, S.h - 3)]), ramp5((176, 96, 64)), edge=True)
    for i in range(7):
        a = -math.pi / 2 + (i - 3) * .42
        tip = (9 + math.cos(a) * (9 if kind == 0 else 7), S.h - 11 + math.sin(a) * (15 if kind == 0 else 10))
        S.paint(capsule((9, S.h - 10), tip, 1.7), ramp5(mix((60, 112, 60), (255, 255, 180), rng.uniform(0, .12))), edge=True)
    S.outline()
    return S


def shelf(kind: int) -> Sprite:
    rng = random.Random(110 + kind)
    S = Sprite(28, 40, salt=110 + kind)
    _shadow(S, 14, 37, 12, 2)
    S.paint(rect(1, 2, 26, 36), ramp5((112, 80, 52)), grain=.2, edge=True)
    for y0 in (4, 14, 24):
        S.paint(rect(3, y0, 24, y0 + 7), ramp5((54, 40, 30)))
        x = 4
        while x < 23:
            w = rng.randint(2, 4)
            col = rng.choice(((170, 60, 56), (60, 100, 160), (210, 190, 90), (90, 140, 80), (200, 200, 190))) if kind == 0 else \
                rng.choice(((220, 190, 70), (190, 60, 60), (240, 240, 230), (90, 150, 90), (120, 90, 60)))
            h = rng.randint(4, 7)
            S.paint(rect(x, y0 + 7 - h, x + w - 1, y0 + 6), ramp5(col))
            x += w + 1
        S.paint(rect(3, y0 + 7, 24, y0 + 8), ramp5((150, 108, 70)))
    S.outline()
    return S


def armchair() -> Sprite:
    S = Sprite(32, 54, salt=120)
    _shadow(S, 16, 50, 14, 2.6)
    fab = ramp5((120, 74, 72))
    S.paint(rect(3, 6, 28, 28), fab, grain=.2, edge=True)
    S.paint(rect(1, 24, 6, 46), ramp5((100, 62, 60)), grain=.2, edge=True)
    S.paint(rect(25, 24, 30, 46), ramp5((100, 62, 60)), grain=.2, edge=True)
    S.paint(rect(6, 28, 25, 46), ramp5((140, 90, 86)), grain=.2, edge=True)
    for (x, y) in ((11, 14), (16, 14), (21, 14), (11, 20), (16, 20), (21, 20)):
        S.set(x, y, fab[0])
    for x in (4, 26):
        S.paint(rect(x, 46, x + 2, 50), ramp5((70, 50, 36)), edge=True)
    S.outline()
    return S


def bed(kind: int) -> Sprite:
    S = Sprite(64, 58, salt=130 + kind)
    _shadow(S, 32, 54, 29, 2.6)
    S.paint(rect(2, 4, 61, 12), ramp5((110, 78, 50)), grain=.2, edge=True)                       # headboard
    S.paint(rect(3, 12, 60, 50), ramp5((226, 222, 210)), grain=.1, edge=True)                       # sheets
    blanket = (70, 96, 140) if kind == 0 else (150, 70, 66)
    S.paint(rect(3, 26, 60, 50), ramp5(blanket), grain=.2, edge=True)
    S.paint(rect(3, 24, 60, 27), ramp5(mix(blanket, (255, 255, 255), .3)))
    for x0 in (8, 36):
        S.paint(ellipse(x0 + 9, 18, 10, 4.6), ramp5((240, 238, 228)), grain=.1, edge=True)
    for x in (4, 56):
        S.paint(rect(x, 50, x + 3, 55), ramp5((88, 62, 42)), edge=True)
    S.outline()
    return S


# ------------------------------------------------------------------ decals (flat, drawn over the ground)
def _decal(w: int, h: int, seed: int) -> tuple[Sprite, random.Random]:
    return Sprite(w, h, salt=seed), random.Random(seed)


def decal_blood(i: int) -> Sprite:
    S, rng = _decal(32, 32, 200 + i)
    for _ in range(rng.randint(3, 5)):
        cx, cy, r = rng.uniform(8, 24), rng.uniform(8, 24), rng.uniform(3, 7)
        for y in range(32):
            for x in range(32):
                d = math.hypot((x - cx) * .85, y - cy)
                if d < r and rng.random() < 1 - d / r + .3:
                    S.px[(x, y)] = rng.choice(BLOOD[:2])
    for _ in range(rng.randint(8, 16)):
        S.px[(rng.randrange(32), rng.randrange(32))] = BLOOD[0]
    for (x, y) in list(S.px)[:: 7]:
        S.px[(x, y)] = BLOOD[2]
    return S


def decal_stain(i: int) -> Sprite:
    S, rng = _decal(32, 32, 210 + i)
    col = [(36, 34, 40), (70, 62, 48), (50, 46, 56)][i % 3]
    for _ in range(3):
        cx, cy, r = rng.uniform(8, 24), rng.uniform(8, 24), rng.uniform(4, 8)
        for y in range(32):
            for x in range(32):
                if math.hypot(x - cx, y - cy) < r and rng.random() < .65:
                    S.px[(x, y)] = mix(col, (0, 0, 0), rng.uniform(0, .3))
    return S


def decal_rubbish(i: int) -> Sprite:
    S, rng = _decal(32, 32, 220 + i)
    for _ in range(rng.randint(6, 10)):
        x, y = rng.randint(3, 27), rng.randint(3, 27)
        col = rng.choice(((226, 224, 214), (180, 150, 100), (90, 130, 100), (170, 60, 56), (60, 80, 140), (200, 190, 80)))
        w, h = rng.randint(2, 4), rng.randint(1, 3)
        for dy in range(h):
            for dx in range(w):
                S.px[(x + dx, y + dy)] = mix(col, (0, 0, 0), .25 if dy == h - 1 else 0)
    return S


def decal_glass(i: int) -> Sprite:
    S, rng = _decal(32, 32, 230 + i)
    for _ in range(rng.randint(14, 24)):
        x, y = rng.randint(2, 29), rng.randint(2, 29)
        S.px[(x, y)] = rng.choice(((196, 220, 230), (150, 180, 196), (240, 250, 255)))
        if rng.random() < .5:
            S.px[(x + 1, y)] = (110, 140, 160)
    return S


def decal_rug(i: int) -> Sprite:
    S = Sprite(96, 32, salt=240 + i)
    base = [(150, 60, 56), (60, 96, 140)][i % 2]
    S.paint(rect(1, 1, 94, 30), ramp5(base), grain=.18)
    S.paint(rect(5, 5, 90, 26), ramp5(mix(base, (0, 0, 0), .25)), grain=.12)
    S.paint(rect(10, 10, 85, 21), ramp5(mix(base, (255, 230, 180), .25)), grain=.12)
    for x in range(14, 82, 8):
        S.set(x, 15, (230, 210, 150)); S.set(x + 1, 16, (230, 210, 150))
    for y in range(1, 31, 2):
        S.set(0, y, (226, 220, 200)); S.set(95, y, (226, 220, 200))
    return S


# ------------------------------------------------------------------ catalogue
def build() -> dict[str, Sprite]:
    out: dict[str, Sprite] = {}
    for color in CAR_COLORS:
        out[f"decor_car_{color}_h"] = car_h(color)
        out[f"decor_car_{color}_v"] = car_v(color)
    out.update({
        "decor_bin_0": bin_metal(), "decor_bin_1": bin_green(), "decor_trash_bag": trash_bag(), "decor_hydrant": hydrant(),
        "decor_mailbox": mailbox(), "decor_lamp": lamp(), "decor_bench": bench(), "decor_cone": cone(), "decor_barrel": barrel(),
        "decor_boxes_0": boxes(0), "decor_boxes_1": boxes(1), "decor_bush_0": bush(0), "decor_bush_1": bush(1), "decor_bush_dead": bush(2),
        "decor_tree_0": tree(0), "decor_tree_1": tree(1), "decor_tree_dead": tree(2),
        "decor_table_long": table(3), "decor_table_oval": table_oval(), "decor_table_round": table(1, True),
        "decor_chair_0": chair(0), "decor_chair_1": chair(1), "decor_stove": stove(),
        "decor_plant_0": plant(0), "decor_plant_1": plant(1), "decor_shelf_0": shelf(0), "decor_shelf_1": shelf(1),
        "decor_armchair": armchair(), "decor_bed_0": bed(0), "decor_bed_1": bed(1),
    })
    for i in range(4):
        out[f"decor_counter_{i}"] = counter(i)
    for i in range(3):
        out[f"decal_blood_{i}"] = decal_blood(i)
        out[f"decal_stain_{i}"] = decal_stain(i)
        out[f"decal_rubbish_{i}"] = decal_rubbish(i)
    for i in range(2):
        out[f"decal_glass_{i}"] = decal_glass(i)
        out[f"decal_rug_{i}"] = decal_rug(i)
    return out
