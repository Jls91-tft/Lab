"""Ground tiles (7 types x 3 variants) and the 12 blocks of BlockRegistry, 32 px each."""

from __future__ import annotations

import math
import random

from PIL import Image

from .gfx import Color, Sprite, Tile, capsule, ellipse, hnoise, mix, poly, ramp5, rect

SIZE = 32


# ----------------------------------------------------------------- helpers
def speckle(t: Tile, rng: random.Random, colors: list[Color], n: int) -> None:
    for _ in range(n):
        t.put(rng.randrange(SIZE), rng.randrange(SIZE), rng.choice(colors))


def blob(t: Tile, rng: random.Random, cx: float, cy: float, r: float, c: Color, a: float) -> None:
    for y in range(int(cy - r) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            d = math.hypot(x - cx, y - cy)
            if d <= r and rng.random() < (1 - d / r) * 1.6:
                t.blend(x, y, c, a)


def crack(t: Tile, rng: random.Random, x: int, y: int, n: int, c: Color, hi: Color | None = None, branch: float = .22) -> None:
    for _ in range(n):
        t.put(x, y, c)
        if hi:
            t.blend(x + 1, y - 1, hi, .5)
        x += rng.choice((-1, 0, 0, 1))
        y += rng.choice((0, 1, 1))
        if rng.random() < branch:
            crack(t, rng, x, y, n // 3, c, hi, 0)


# ----------------------------------------------------------------- ground
def asphalt(seed: int) -> Tile:
    rng = random.Random(seed)
    t = Tile((58, 60, 68))
    for _ in range(5):
        blob(t, rng, rng.randrange(32), rng.randrange(32), rng.randint(5, 9), rng.choice(((44, 46, 54), (74, 76, 84))), .22)
    speckle(t, rng, [(48, 50, 57), (50, 52, 60), (68, 70, 78)], 150)
    speckle(t, rng, [(86, 88, 96), (96, 98, 104), (40, 41, 48)], 22)
    if rng.random() < .55:
        crack(t, rng, rng.randrange(4, 28), rng.randrange(0, 8), rng.randint(14, 26), (28, 29, 35), (84, 86, 94))
    if rng.random() < .25:
        x0, y0 = rng.randrange(2, 14), rng.randrange(2, 14)
        for y in range(y0, y0 + 12):
            for x in range(x0, x0 + 15):
                edge = x in (x0, x0 + 14) or y in (y0, y0 + 11)
                t.blend(x, y, (36, 37, 44) if edge else (50, 52, 58), .8 if edge else .5)
    return t


def sidewalk(seed: int, curb: str | None = None) -> Tile:
    rng = random.Random(seed)
    t = Tile((126, 128, 134))
    for sx in (0, 16):
        for sy in (0, 16):
            off = rng.randint(-7, 6)
            for y in range(sy, sy + 16):
                for x in range(sx, sx + 16):
                    t.g[y][x] = mix(t.g[y][x], (255, 255, 255) if off > 0 else (0, 0, 20), abs(off) / 70)
    speckle(t, rng, [(118, 120, 126), (136, 138, 144), (108, 110, 116)], 120)
    for i in range(32):
        for p in (0, 16):
            t.put(i, p, (74, 76, 82)); t.put(p, i, (74, 76, 82))
            t.put(i, p + 1, (150, 152, 158)); t.put(p + 1, i, (148, 150, 156))
    for _ in range(rng.randint(2, 5)):
        x = rng.choice((0, 16)) if rng.random() < .5 else rng.randrange(32)
        y = rng.randrange(32) if x in (0, 16) else rng.choice((0, 16))
        t.put(x, y, (72, 100, 60)); t.put(x, y - 1, (96, 128, 72))
    if rng.random() < .4:
        crack(t, rng, rng.randrange(3, 13), rng.randrange(3, 10), 12, (82, 84, 90), (156, 158, 164))
    if rng.random() < .5:
        blob(t, rng, rng.randrange(32), rng.randrange(32), 3, (80, 76, 70), .4)
    return t


def grass(seed: int) -> Tile:
    rng = random.Random(seed)
    t = Tile((64, 92, 54))
    for _ in range(7):
        blob(t, rng, rng.randrange(32), rng.randrange(32), rng.randint(4, 8), rng.choice(((48, 74, 42), (82, 112, 64))), .35)
    speckle(t, rng, [(54, 80, 46), (72, 102, 58), (50, 76, 44)], 160)
    if rng.random() < .6:
        blob(t, rng, rng.randrange(6, 26), rng.randrange(6, 26), rng.randint(3, 5), (104, 84, 58), .85)
    for _ in range(44):
        x, y = rng.randrange(1, 31), rng.randrange(3, 31)
        t.put(x, y, (44, 70, 38)); t.put(x, y - 1, (96, 132, 70))
        if rng.random() < .35:
            t.put(x + rng.choice((-1, 1)), y - 2, (126, 164, 86))
    for _ in range(rng.randint(0, 3)):
        x, y = rng.randrange(1, 31), rng.randrange(3, 31)
        t.put(x, y, (146, 132, 74)); t.put(x, y - 1, (170, 154, 86))
    if rng.random() < .35:
        x, y = rng.randrange(3, 29), rng.randrange(3, 29)
        t.put(x, y, (122, 120, 114)); t.put(x + 1, y, (146, 144, 138)); t.put(x, y + 1, (70, 68, 66)); t.put(x + 1, y + 1, (70, 68, 66))
    if rng.random() < .3:
        x, y = rng.randrange(3, 29), rng.randrange(3, 29)
        t.put(x, y, (222, 214, 120)); t.put(x + 1, y, (240, 232, 150))
    return t


def dirt(seed: int) -> Tile:
    rng = random.Random(seed)
    t = Tile((108, 86, 62))
    for _ in range(8):
        blob(t, rng, rng.randrange(32), rng.randrange(32), rng.randint(4, 9), rng.choice(((90, 70, 50), (126, 102, 74))), .4)
    speckle(t, rng, [(96, 76, 54), (120, 96, 70), (84, 66, 48)], 170)
    for _ in range(rng.randint(2, 5)):
        x, y = rng.randrange(2, 30), rng.randrange(2, 30)
        t.put(x, y, (150, 142, 128)); t.put(x + 1, y, (176, 168, 152)); t.put(x, y + 1, (70, 58, 46)); t.put(x + 1, y + 1, (70, 58, 46))
    if rng.random() < .5:
        y0 = rng.randrange(6, 24)
        for x in range(32):
            t.blend(x, y0 + int(math.sin(x * .5) * 1.2), (74, 58, 42), .6)
    for _ in range(rng.randint(0, 4)):
        x, y = rng.randrange(1, 31), rng.randrange(3, 31)
        t.put(x, y, (86, 112, 62)); t.put(x, y - 1, (110, 142, 76))
    return t


def water(seed: int, phase: float = 0.0) -> Tile:
    rng = random.Random(seed)
    t = Tile((52, 100, 116))
    for y in range(32):
        for x in range(32):
            w = math.sin(x * .45 + y * .22 + seed * 1.7 + phase) + math.sin(y * .6 - x * .15 + seed - phase)
            t.g[y][x] = mix((52, 100, 116), (30, 70, 94) if w < 0 else (84, 142, 156), min(1, abs(w) * .3))
    for _ in range(7):
        x, y = rng.randrange(1, 28), rng.randrange(1, 31)
        x = (x + int(phase * 3)) % 28
        for i in range(rng.randint(3, 6)):
            t.put(x + i, y, (176, 214, 222))
        for i in range(2):
            t.put(x + i + 1, y + 1, (60, 112, 130))
    speckle(t, rng, [(40, 84, 104), (70, 124, 140)], 60)
    return t


def floor(seed: int) -> Tile:
    rng = random.Random(seed)
    t = Tile((150, 118, 82))
    for k in range(4):
        y0 = k * 8
        base = mix((152, 120, 84), (255, 230, 180) if rng.random() < .5 else (60, 36, 20), rng.uniform(.02, .14))
        off = rng.randrange(0, 32)
        for y in range(y0, y0 + 8):
            for x in range(32):
                n = int(hnoise(x // 4, y, seed + k) * 12) - 6
                t.g[y][x] = (base[0] + n, base[1] + n * 3 // 4, base[2] + n // 2)
        for x in range(32):
            t.g[y0][x] = mix(t.g[y0][x], (255, 226, 176), .18)
            t.g[y0 + 7][x] = (86, 62, 42)
        for y in range(y0, y0 + 8):
            t.put((off) % 32, y, (86, 62, 42))
        for _ in range(3):
            gx, gy = rng.randrange(0, 24), y0 + rng.randint(1, 6)
            for i in range(rng.randint(4, 9)):
                t.blend(gx + i, gy, (90, 60, 36), .3)
        t.put((off + 2) % 32, y0 + 1, (60, 60, 66)); t.put((off + 29) % 32, y0 + 6, (60, 60, 66))
    if rng.random() < .35:
        blob(t, rng, rng.randrange(32), rng.randrange(32), 4, (84, 60, 40), .5)
    return t


def rail(seed: int) -> Tile:
    rng = random.Random(seed)
    t = Tile((112, 108, 100))
    speckle(t, rng, [(96, 92, 86), (132, 128, 120), (80, 76, 72), (148, 144, 134)], 260)
    for x0 in range(2, 32, 8):                                                 # sleepers
        for y in range(0, 32):
            for x in range(x0, x0 + 4):
                n = int(hnoise(x, y, seed) * 10) - 5
                t.g[y][x] = (96 + n, 70 + n, 48 + n)
            t.put(x0, y, (126, 94, 62)); t.put(x0 + 3, y, (62, 44, 30))
    for y0 in (7, 21):                                                         # rails
        for x in range(32):
            t.put(x, y0 - 1, (96, 98, 104)); t.put(x, y0, (176, 180, 188)); t.put(x, y0 + 1, (122, 124, 132)); t.put(x, y0 + 2, (46, 44, 50))
            if rng.random() < .1:
                t.put(x, y0, (150, 106, 70))
        for x0 in range(3, 32, 8):
            t.put(x0, y0 + 1, (60, 60, 66)); t.put(x0 + 1, y0 + 1, (60, 60, 66))
    return t


GROUND = {
    "dirt": dirt, "asphalt": asphalt, "sidewalk": sidewalk, "grass": grass, "water": water, "floor": floor, "rail": rail,
}


def ground_tiles() -> dict[str, Image.Image]:
    out: dict[str, Image.Image] = {}
    for name, fn in GROUND.items():
        for v in range(3):
            out[f"ground_{name}_{v}"] = fn(hash_seed(name, v)).image()
    return out


def water_frames() -> dict[str, Image.Image]:
    """Four looping frames of moving water, for the game to cycle when it wants living water."""
    return {f"anim_water_{f}": water(hash_seed("water", 0), f * math.pi / 2).image() for f in range(4)}


def hash_seed(name: str, v: int) -> int:
    return sum(ord(c) * (i + 3) for i, c in enumerate(name)) * 7 + v * 131


# ----------------------------------------------------------------- blocks
def brick_face(seed: int, window: bool = False) -> Tile:
    rng = random.Random(seed)
    t = Tile((98, 100, 106))
    for y in range(0, 7):
        for x in range(32):
            v = (172, 168, 160) if y < 2 else (146, 142, 136) if y < 5 else (96, 92, 90) if y == 5 else (54, 48, 50)
            n = int(hnoise(x, y, seed) * 14) - 7
            t.g[y][x] = (v[0] + n, v[1] + n, v[2] + n)
    bricks = [(138, 76, 62), (124, 66, 56), (152, 90, 70), (112, 58, 50), (132, 72, 64)]
    for row in range(6):
        y0 = 7 + row * 4
        for bx in range(-1, 3):
            x0 = bx * 16 + (8 if row % 2 else 0)
            col = mix(rng.choice(bricks), (255, 255, 255) if rng.random() < .5 else (0, 0, 0), rng.uniform(0, .12))
            for y in range(y0, min(32, y0 + 3)):
                for x in range(x0, x0 + 15):
                    if 0 <= x < 32:
                        n = int(hnoise(x, y, seed + row) * 14) - 7
                        c = (col[0] + n, col[1] + n // 2, col[2] + n // 2)
                        if y == y0:
                            c = mix(c, (255, 220, 190), .22)
                        if y == y0 + 2:
                            c = mix(c, (20, 10, 20), .28)
                        t.g[y][x] = c
    for y in range(7, 32):
        for x in range(32):
            if (y - 7) % 4 == 3 or ((x + (8 if ((y - 7) // 4) % 2 else 0)) % 16 == 15):
                t.g[y][x] = (102, 104, 108) if (y + x) % 5 else (88, 90, 96)
    for _ in range(3):
        sx, ln = rng.randrange(32), rng.randint(6, 16)
        for y in range(7, 7 + ln):
            t.blend(sx, y, (30, 24, 28), .18 * (1 - (y - 7) / ln))
    for _ in range(rng.randint(2, 4)):
        x, y = rng.randrange(1, 30), rng.randrange(9, 30)
        t.put(x, y, (44, 30, 36)); t.put(x + 1, y, (44, 30, 36)); t.put(x, y + 1, (74, 50, 50))
    return t


def roof_top(seed: int) -> Tile:
    rng = random.Random(seed)
    t = Tile((104, 100, 98))
    speckle(t, rng, [(96, 92, 90), (116, 112, 108), (86, 82, 82), (130, 126, 120)], 260)
    for y in range(32):
        for x in range(32):
            if x % 16 == 0 or y % 16 == 0:
                t.g[y][x] = (70, 66, 66)
            elif x % 16 == 1 or y % 16 == 1:
                t.g[y][x] = (124, 120, 116)
    for _ in range(3):
        blob(t, rng, rng.randrange(32), rng.randrange(32), 4, (60, 70, 52), .35)
    return t


def wood_wall(seed: int, damage: int = 0) -> Tile:
    rng = random.Random(seed)
    t = Tile((140, 98, 60))
    for k in range(5):
        y0 = k * 6 + 1
        col = mix((138, 96, 58), (255, 220, 160) if rng.random() < .5 else (40, 20, 10), rng.uniform(.02, .16))
        for y in range(y0, y0 + 5):
            for x in range(32):
                n = int(hnoise(x // 3, y, seed + k) * 14) - 7
                t.g[y][x] = (col[0] + n, col[1] + n * 3 // 4, col[2] + n // 2)
        for x in range(32):
            t.g[y0][x] = mix(t.g[y0][x], (255, 226, 176), .22)
            t.g[y0 + 4][x] = mix(t.g[y0 + 4][x], (30, 14, 8), .42)
            t.g[max(0, y0 - 1)][x] = (58, 38, 24)
        for _ in range(3):
            gx, gy = rng.randrange(0, 22), y0 + rng.randint(1, 3)
            for i in range(rng.randint(4, 10)):
                t.blend(gx + i, gy, (60, 36, 20), .35)
        if rng.random() < .4:
            kx, ky = rng.randrange(4, 28), y0 + 2
            for (dx, dy, a) in ((0, 0, .85), (-1, 0, .5), (1, 0, .5), (0, -1, .35), (0, 1, .35)):
                t.blend(kx + dx, ky + dy, (46, 26, 14), a)
        for x in (3, 28):
            t.put(x, y0 + 2, (36, 36, 42)); t.put(x - 1, y0 + 1, (176, 176, 184)); t.put(x, y0 + 3, (70, 50, 40))
    for i in range(32):
        t.put(i, 0, (58, 38, 24)); t.put(0, i, (58, 38, 24)); t.put(31, i, (40, 24, 16)); t.put(i, 31, (40, 24, 16))
    if damage:
        crack(t, rng, rng.randrange(8, 24), 1, 30, (36, 20, 12), (190, 140, 96), .3)
        for _ in range(damage * 22):
            t.blend(rng.randrange(32), rng.randrange(32), (30, 18, 12), .55)
        if damage > 1:
            for y in range(8, 15):
                for x in range(12, 22):
                    if rng.random() < .75 and abs(x - 17) + abs(y - 11) < 9:
                        t.put(x, y, (24, 16, 14) if (x + y) % 3 else (60, 40, 28))
    return t


def metal_wall(seed: int) -> Tile:
    rng = random.Random(seed)
    t = Tile((118, 124, 130))
    for x in range(32):
        phase = x % 8
        v = (150, 156, 164) if phase in (1, 2) else (92, 98, 106) if phase in (5, 6) else (118, 124, 130)
        for y in range(32):
            n = int(hnoise(x, y, seed) * 10) - 5
            t.g[y][x] = (v[0] + n, v[1] + n, v[2] + n)
    for y in (0, 15, 16, 31):
        for x in range(32):
            t.put(x, y, (60, 64, 72) if y in (0, 31) else (170, 176, 184) if y == 15 else (70, 74, 82))
    for (x, y) in ((3, 3), (28, 3), (3, 28), (28, 28), (3, 19), (28, 19), (3, 11), (28, 11)):
        t.put(x, y, (190, 196, 204)); t.put(x + 1, y + 1, (50, 54, 60))
    for _ in range(5):
        blob(t, rng, rng.randrange(32), rng.randrange(32), rng.randint(2, 5), (140, 80, 44), .6)
    for x in (0, 31):
        for y in range(32):
            t.put(x, y, (58, 62, 70))
    return t


def concrete_wall(seed: int) -> Tile:
    rng = random.Random(seed)
    t = Tile((142, 140, 134))
    speckle(t, rng, [(132, 130, 124), (152, 150, 144), (120, 118, 112), (160, 158, 152)], 300)
    for y in range(32):
        for x in range(32):
            if y % 16 == 0 or (x + (8 if (y // 16) % 2 else 0)) % 16 == 0:
                t.g[y][x] = (92, 90, 86)
            elif y % 16 == 1 or (x + (8 if (y // 16) % 2 else 0)) % 16 == 1:
                t.g[y][x] = (168, 166, 160)
    crack(t, rng, rng.randrange(3, 28), 2, 18, (70, 68, 64), (176, 174, 168))
    for _ in range(3):
        blob(t, rng, rng.randrange(32), rng.randrange(32), rng.randint(2, 5), (96, 108, 84), .3)
    for i in range(32):
        t.put(0, i, (80, 78, 74)); t.put(31, i, (80, 78, 74)); t.put(i, 31, (70, 68, 64))
    return t


def door(open_: bool) -> Sprite:
    S = Sprite(32, 32, salt=21)
    frame = ramp5((88, 62, 42))
    S.paint(rect(1, 1, 30, 31), frame, grain=.2)
    if not open_:
        wood = ramp5((126, 88, 56))
        for k, x0 in enumerate(range(4, 28, 6)):
            S.paint(rect(x0, 4, x0 + 5, 31), mix_ramp(wood, k), grain=.2)
        for y in (9, 23):
            S.paint(rect(4, y, 27, y + 2), ramp5((70, 70, 78)), grain=.1)
        S.paint(ellipse(24, 19, 1.6, 1.6), ramp5((214, 190, 110)), edge=True)
        S.set(24, 20, (60, 50, 30))
    else:
        for (x, y) in rect(4, 4, 27, 31):
            S.px[(x, y)] = (28, 24, 30) if y < 10 else (46, 40, 44)
        S.paint(poly([(4, 4), (11, 6), (11, 31), (4, 31)]), ramp5((126, 88, 56)), grain=.2, edge=True)
        S.paint(rect(5, 9, 10, 11), ramp5((70, 70, 78)))
        S.paint(rect(5, 23, 10, 25), ramp5((70, 70, 78)))
    S.outline()
    return S


def mix_ramp(r, k):
    return [mix(c, (255, 230, 190) if k % 2 else (30, 16, 8), .06) for c in r]


def fence() -> Sprite:
    S = Sprite(32, 32, salt=31)
    post = ramp5((120, 124, 132))
    for x0 in (0, 29):
        S.paint(rect(x0, 4, x0 + 2, 30), post, edge=True)
        S.paint(ellipse(x0 + 1.5, 4, 2, 1.4), ramp5((160, 164, 172)))
    for y in range(7, 27):
        for x in range(3, 29):
            if (x + y) % 5 == 0 or (x - y) % 5 == 0:
                S.px[(x, y)] = (150, 156, 164) if (x + y) % 2 else (108, 114, 122)
    for x in range(3, 29):
        S.px[(x, 6)] = (176, 182, 190); S.px[(x, 27)] = (90, 96, 104)
    S.shadow = {(x, 30) for x in range(2, 30)}
    return S


def spike_trap() -> Sprite:
    S = Sprite(32, 32, salt=41)
    S.paint(rect(2, 6, 29, 29), ramp5((86, 62, 42)), grain=.25, edge=True)
    for gy in range(3):
        for gx in range(4):
            cx, cy = 6 + gx * 7, 11 + gy * 7
            S.paint(poly([(cx - 2.6, cy + 3), (cx, cy - 4), (cx + 2.6, cy + 3)]), ramp5((196, 200, 208)), edge=True)
            S.set(cx, cy - 3, (240, 244, 250))
    for (x, y) in ((8, 17), (9, 18), (21, 12), (22, 12), (14, 24)):
        S.set(x, y, (138, 34, 38))
    S.outline()
    return S


def workbench() -> Sprite:
    S = Sprite(32, 32, salt=51)
    S.paint(rect(1, 4, 30, 15), ramp5((170, 124, 80)), grain=.2, edge=True)
    S.paint(rect(1, 16, 30, 24), ramp5((112, 78, 50)), grain=.2, edge=True)
    for x in (3, 26):
        S.paint(rect(x, 24, x + 2, 30), ramp5((92, 64, 42)), edge=True)
    S.paint(rect(5, 7, 12, 8), ramp5((120, 124, 132)), edge=True)                    # saw
    S.paint(rect(14, 6, 19, 9), ramp5((110, 110, 118)), edge=True)                   # vise
    S.paint(rect(22, 8, 27, 9), ramp5((100, 68, 44)), edge=True)                      # hammer
    S.paint(rect(25, 6, 28, 9), ramp5((140, 144, 152)), edge=True)
    for (x, y) in ((6, 12), (9, 12), (12, 11), (20, 12), (21, 13)):
        S.set(x, y, (196, 200, 208))                                                   # nails
    S.outline()
    return S


def chest() -> Sprite:
    S = Sprite(32, 32, salt=61)
    S.paint(rect(3, 10, 28, 27), ramp5((126, 88, 56)), grain=.25, edge=True)
    S.paint(poly([(3, 10), (5, 4), (26, 4), (28, 10)]), ramp5((146, 104, 66)), grain=.2, edge=True)
    for x in (7, 23):
        S.paint(rect(x, 4, x + 2, 27), ramp5((74, 74, 82)))
    S.paint(rect(3, 14, 28, 15), ramp5((74, 74, 82)))
    S.paint(rect(14, 12, 17, 18), ramp5((214, 190, 110)), edge=True)
    S.set(15, 15, (40, 30, 20))
    S.shadow = {(x, 29) for x in range(4, 28)} | {(x, 30) for x in range(5, 27)}
    S.outline()
    return S


def tree_block(seed: int) -> Sprite:
    rng = random.Random(seed)
    S = Sprite(32, 32, salt=seed)
    S.shadow = ellipse(16, 27, 12, 3)
    for (cx, cy, r, tone) in ((16, 15, 13, -.1), (9, 18, 8, -.15), (23, 18, 8, -.12), (16, 9, 8, .08), (11, 11, 6, .05), (22, 12, 6, .1)):
        c = mix((60, 98, 54), (255, 255, 200) if tone > 0 else (10, 30, 20), abs(tone))
        S.paint(ellipse(cx, cy, r, r), ramp5(c), grain=.35)
    for _ in range(50):
        x, y = rng.randint(3, 28), rng.randint(2, 28)
        if (x, y) in S.px:
            S.set(x, y, rng.choice(((120, 164, 80), (44, 74, 44), (138, 176, 92))))
    S.outline()
    return S


def block_images() -> dict[str, Image.Image]:
    out = {
        "block_building_wall": roof_top(7).image(),
        "block_building_wall_face": brick_face(11).image(),
        "block_wood_wall": wood_wall(13).image(),
        "block_metal_wall": metal_wall(17).image(),
        "block_concrete_wall": concrete_wall(19).image(),
        "block_door": door(False).image(),
        "block_door_open": door(True).image(),
        "block_fence": fence().image(),
        "block_spike_trap": spike_trap().image(),
        "block_workbench": workbench().image(),
        "block_chest": chest().image(),
        "block_tree": tree_block(23).image(),
    }
    return out
