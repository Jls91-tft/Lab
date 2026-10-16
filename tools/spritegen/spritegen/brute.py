"""La Mole (brute): the two-tile miniboss, drawn natively at 64x96 in 3 views and 4 frames."""

from __future__ import annotations

import math
import random

from .gfx import Color, Sprite, capsule, ellipse, hnoise, mix, poly, ramp5, rect
from .characters import BLOOD, BONE, GLOW, GLOW_DIM, NFRAMES, WOUND

W, H, FOOT = 64, 96, 88
SKIN: Color = (140, 104, 98)
PANTS: Color = (62, 54, 50)
IRON: Color = (104, 108, 116)


def _veins(S: Sprite, rng: random.Random, x: int, y: int, n: int, drift: int = 1) -> None:
    for i in range(n):
        if (x, y) in S.px:
            S.set(x, y, GLOW_DIM if i % 3 else mix(GLOW_DIM, GLOW, .45), True)
        y += 1
        x += rng.choice((0, 0, drift))


def _chain(S: Sprite, a: tuple[float, float], b: tuple[float, float], links: int, flat: bool = False) -> None:
    iron = ramp5(IRON)
    for i in range(links):
        t = i / max(1, links - 1)
        x, y = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t + math.sin(t * math.pi) * 2.5
        S.paint(ellipse(x, y, 2.6 if i % 2 else 1.6, 1.6 if i % 2 else 2.6), iron, edge=True, reach=3)
        S.set(x, y, (20, 18, 24))


def _scar(S: Sprite, x: int, y: int, length: int, dx: int, dy: int) -> None:
    for i in range(length):
        S.set(x + dx * i, y + dy * i, (210, 176, 156))
        if i % 3 == 1:
            S.set(x + dx * i - dy, y + dy * i + dx, (60, 30, 34))          # stitch
            S.set(x + dx * i + dy, y + dy * i - dx, (60, 30, 34))


def _spike(S: Sprite, x: float, y: float, dx: float, dy: float, ln: float) -> None:
    px, py = -dy, dx
    S.paint(poly([(x + px * 2.2, y + py * 2.2), (x - px * 2.2, y - py * 2.2), (x + dx * ln, y + dy * ln)]), ramp5(BONE), edge=True, reach=3)
    S.set(x + dx * ln * .35 + px * .4, y + dy * ln * .35 + py * .4, (250, 246, 230))


def _legs(S: Sprite, ph: float, side_view: bool, back: bool, skin) -> tuple[float, float]:
    sn, cs = math.sin(ph), math.cos(ph)
    pants, shoe = ramp5(PANTS), ramp5((46, 38, 40))
    feet = []
    for side in (-1, 1):
        p = ph if side < 0 else ph + math.pi
        if side_view:
            fx = 32 - 9 * math.sin(p)
            fy = FOOT - 2 - 5 * max(0, math.cos(p))
        else:
            fx = 32 + side * 10
            fy = FOOT - 1 + (-1 if back else 1) * 4 * math.sin(p) - 2.5 * max(0, math.cos(p))
        feet.append((fy, side, fx))
    for fy, side, fx in sorted(feet):
        hip = (32 + (0 if side_view else side * 9), 60)
        far = side_view and side == 1
        pc = [mix(c, (20, 18, 40), .28) for c in pants] if far else pants
        knee = ((hip[0] + fx) / 2 + (side * 1.5 if not side_view else -2), (hip[1] + fy) / 2 - 1)
        S.paint(capsule(hip, knee, 7.2), pc, grain=.2, reach=12)
        S.paint(capsule(knee, (fx, fy), 6.0), pc, grain=.2, reach=12)
        S.paint(ellipse(fx - (3 if side_view else 0), fy + 1.4, 9.2 if side_view else 8.2, 4.0), shoe, edge=True, reach=6)
        for dx in range(-8, 9):
            S.set(fx + dx - (3 if side_view else 0), fy + 4, shoe[0])
        if not back and not far:
            kx, ky = int(knee[0]), int(knee[1])
            for (x, y) in ((kx - 2, ky), (kx - 1, ky), (kx, ky), (kx - 1, ky + 1), (kx, ky + 1), (kx + 1, ky + 1)):
                S.set(x, y, skin[2])
            S.set(kx + 2, ky, BLOOD[1]); S.set(kx + 2, ky + 1, BLOOD[0])
    return sn, cs


def _torn(S: Sprite, rng: random.Random, y: int) -> None:
    for x in range(18, 47):
        if hnoise(x, y, 7) < .5:
            S.px.pop((x, y), None)
            S.px.pop((x, y + 1), None) if hnoise(x, y, 9) < .4 else None


def draw_front(frame: int, seed: int = 106) -> Sprite:
    rng = random.Random(seed)
    S = Sprite(W, H, salt=seed)
    ph = frame * 2 * math.pi / NFRAMES
    sn = math.sin(ph)
    bob = 2 if abs(sn) > .8 else 0
    skin = ramp5(SKIN)
    S.shadow = ellipse(32, FOOT + 3, 24, 5)
    _legs(S, ph, False, False, skin)
    S.paint(poly([(18, 56), (46, 56), (50, 70), (14, 70)]), ramp5(PANTS), grain=.2, reach=12)
    for x in range(15, 50):                                                    # rope belt
        S.set(x, 58, (150, 120, 76) if x % 3 else (96, 72, 46)); S.set(x, 59, (96, 72, 46))
    S.paint(ellipse(32, 59, 3, 2.2), ramp5((150, 120, 76)), edge=True, reach=3)
    sy = 28 + bob
    torso = poly([(10, sy + 3), (19, sy - 3), (45, sy - 3), (54, sy + 3), (53, sy + 17), (49, sy + 30), (46, 60), (18, 60), (15, sy + 30), (11, sy + 17)])
    S.paint(torso, skin, grain=.18, reach=14)
    S.tint([p for p in torso if p[1] > 56], PANTS, .0)
    for y in range(sy + 2, sy + 22):                                           # pecs and breastbone
        S.set(32, y, skin[1]); S.set(31, y, skin[0]) if y % 2 else None
    for x in range(14, 31):
        S.set(x, sy + 21 + (x - 14) // 8, skin[1])
    for x in range(34, 51):
        S.set(x, sy + 21 + (50 - x) // 8, skin[1])
    for (cx0, yy) in ((24, sy + 28), (40, sy + 28), (24, sy + 34), (40, sy + 34)):
        S.paint(rect(cx0 - 3, yy, cx0 + 3, yy + 3), ramp5(SKIN), edge=False, reach=3)
    S.tint(ellipse(18, sy + 14, 6, 7), (96, 100, 132), .35, .8)                  # bruising
    S.tint(ellipse(46, sy + 22, 5, 5), (68, 66, 44), .4, .8)
    _scar(S, 20, sy + 4, 14, 1, 1); _scar(S, 44, sy + 8, 11, -1, 1)
    for _ in range(4):
        S.tint(ellipse(rng.randint(14, 50), rng.randint(sy + 6, 56), rng.uniform(1.5, 4), rng.uniform(1.5, 3.4)), BLOOD[1], .65, .85)
    for (x, y, dx, dy, ln) in ((15, sy - 1, -.6, -1, 8), (22, sy - 5, -.2, -1, 9), (49, sy - 1, .6, -1, 8), (42, sy - 5, .2, -1, 9)):
        _spike(S, x, y, dx, dy, ln)                                              # shoulder spurs
    S.paint(ellipse(17, sy + 13, 5.5, 5), ramp5((158, 148, 112)), edge=True, reach=4)  # tumour
    S.set(16, sy + 12, GLOW_DIM, True); S.set(18, sy + 14, GLOW_DIM, True); S.set(17, sy + 11, GLOW, True)
    _veins(S, rng, 28, sy + 2, 18); _veins(S, rng, 38, sy + 4, 16, -1)
    _chain(S, (14, sy + 2), (49, sy + 28), 11)
    # head: small, sunk between the shoulders
    hy = sy - 8
    S.paint(rect(27, hy + 6, 37, sy), skin, reach=6)
    S.paint(poly([(24, hy - 2), (27, hy - 8), (37, hy - 8), (40, hy - 2), (41, hy + 5), (37, hy + 11), (32, hy + 13), (27, hy + 11), (23, hy + 5)]), skin, edge=True, reach=8)
    S.set(22, hy + 2, skin[1]); S.set(41, hy + 2, skin[1]); S.set(22, hy + 3, skin[0]); S.set(41, hy + 3, skin[0])
    for x in range(26, 39):
        S.set(x, hy - 1, skin[0]); S.set(x, hy, skin[1])                         # brow ridge
    for ex in (28, 35):
        for (x, y) in rect(ex, hy + 1, ex + 2, hy + 3):
            S.set(x, y, (20, 16, 26))
        S.set(ex + 1, hy + 2, GLOW, True); S.set(ex + 2, hy + 2, GLOW, True); S.set(ex, hy + 2, GLOW_DIM, True)
    S.set(31, hy + 5, skin[0]); S.set(32, hy + 6, skin[0]); S.set(33, hy + 5, skin[0])
    for x in range(27, 38):
        S.set(x, hy + 9, (30, 12, 18)); S.set(x, hy + 10, (86, 22, 28))
    for x in (28, 31, 34, 37):
        S.set(x, hy + 9, BONE)
    _spike(S, 27, hy + 9, -.3, 1, 5); _spike(S, 37, hy + 9, .3, 1, 5)            # tusks
    _scar(S, 36, hy - 4, 8, 0, 1)
    _veins(S, rng, 25, hy + 1, 9)
    # arms (huge) with shackles
    for side in (-1, 1):
        swing = int(round(3 * sn)) * (1 if side < 0 else -1)
        sh = (32 + side * 21, sy + 5)
        el = (32 + side * 25, sy + 23)
        hd = (32 + side * 22, sy + 40 + swing)
        S.paint(ellipse(sh[0], sh[1], 8, 7.4), skin, edge=True, reach=8)
        S.paint(capsule(sh, el, 6.6), skin, grain=.15, edge=True, reach=10)
        S.paint(capsule(el, hd, 6.2), skin, grain=.15, edge=True, reach=10)
        S.paint(ellipse(hd[0], hd[1] + 4, 7.6, 7.2), skin, edge=True, reach=8)
        for k in range(4):
            S.set(hd[0] - 4 + k * 3, hd[1] + 9, skin[0]); S.set(hd[0] - 4 + k * 3, hd[1] + 8, skin[1])
        S.paint(ellipse(hd[0], hd[1] - 2, 7, 2.4), ramp5(IRON), edge=True, reach=3)   # shackle
        _chain(S, (hd[0] + side * 3, hd[1] - 1), (hd[0] + side * 6, hd[1] + 10), 4)
        _scar(S, int(el[0]) - 2, int(el[1]) - 6, 8, 0, 1)
        _veins(S, rng, int(el[0]), int(el[1]) - 6, 14, 0)
        if side == 1:
            for k in range(3):
                S.set(el[0] + 7, el[1] - 4 + k * 3, BONE); S.set(el[0] + 8, el[1] - 4 + k * 3, (170, 160, 138))
    S.outline()
    return S


def draw_back(frame: int, seed: int = 106) -> Sprite:
    rng = random.Random(seed)
    S = Sprite(W, H, salt=seed)
    ph = frame * 2 * math.pi / NFRAMES
    sn = math.sin(ph)
    bob = 2 if abs(sn) > .8 else 0
    skin = ramp5(SKIN)
    S.shadow = ellipse(32, FOOT + 3, 24, 5)
    _legs(S, ph, False, True, skin)
    S.paint(poly([(18, 56), (46, 56), (50, 70), (14, 70)]), ramp5(PANTS), grain=.2, reach=12)
    for x in range(15, 50):
        S.set(x, 58, (150, 120, 76) if x % 3 else (96, 72, 46))
    sy = 28 + bob
    torso = poly([(10, sy + 3), (19, sy - 3), (45, sy - 3), (54, sy + 3), (53, sy + 17), (49, sy + 30), (46, 60), (18, 60), (15, sy + 30), (11, sy + 17)])
    S.paint(torso, skin, grain=.18, reach=14)
    for y in range(sy, 58):                                                    # spine
        S.set(32, y, skin[0]); S.set(31, y, skin[1]) if y % 2 else None
    for i, y in enumerate(range(sy + 1, 56, 5)):
        _spike(S, 32, y, 0, -.35, 5 + (i % 2))
    S.paint(poly([(17, sy + 5), (31, sy + 3), (30, sy + 24), (19, sy + 20)]), ramp5(SKIN), edge=False, reach=8)   # shoulder blades
    S.paint(poly([(33, sy + 3), (47, sy + 5), (45, sy + 20), (34, sy + 24)]), ramp5(SKIN), edge=False, reach=8)
    for y in range(sy + 26, 58, 4):
        for x in range(20, 45):
            if abs(x - 32) > 2:
                S.set(x, y, skin[1]) if x % 2 else None
    _scar(S, 22, sy + 8, 16, 1, 1); _scar(S, 42, sy + 12, 12, -1, 1)
    for _ in range(4):
        S.tint(ellipse(rng.randint(14, 50), rng.randint(sy + 6, 56), rng.uniform(1.5, 4), rng.uniform(1.5, 3.4)), BLOOD[1], .65, .85)
    for (x, y, dx, dy, ln) in ((15, sy - 1, -.6, -1, 8), (22, sy - 5, -.2, -1, 9), (49, sy - 1, .6, -1, 8), (42, sy - 5, .2, -1, 9)):
        _spike(S, x, y, dx, dy, ln)
    _chain(S, (49, sy + 2), (15, sy + 28), 11)
    _veins(S, rng, 26, sy + 3, 20); _veins(S, rng, 40, sy + 3, 20, -1)
    hy = sy - 8
    S.paint(rect(27, hy + 6, 37, sy), skin, reach=6)
    S.paint(poly([(24, hy - 2), (27, hy - 8), (37, hy - 8), (40, hy - 2), (41, hy + 5), (37, hy + 11), (32, hy + 13), (27, hy + 11), (23, hy + 5)]), skin, edge=True, reach=8)
    S.set(22, hy + 2, skin[1]); S.set(41, hy + 2, skin[1])
    S.tint(ellipse(30, hy, 4, 3.5), (96, 100, 132), .4)
    _scar(S, 28, hy - 6, 10, 1, 1)
    for i in range(4):
        S.set(34 + i, hy - 3 + i // 2, WOUND[i % 3])
    _veins(S, rng, 36, hy + 4, 8)
    for side in (-1, 1):
        swing = int(round(3 * sn)) * (1 if side < 0 else -1)
        sh, el, hd = (32 + side * 21, sy + 5), (32 + side * 25, sy + 23), (32 + side * 22, sy + 40 + swing)
        S.paint(ellipse(sh[0], sh[1], 8, 7.4), skin, edge=True, reach=8)
        S.paint(capsule(sh, el, 6.6), skin, grain=.15, edge=True, reach=10)
        S.paint(capsule(el, hd, 6.2), skin, grain=.15, edge=True, reach=10)
        S.paint(ellipse(hd[0], hd[1] + 4, 7.6, 7.2), skin, edge=True, reach=8)
        S.paint(ellipse(hd[0], hd[1] - 2, 7, 2.4), ramp5(IRON), edge=True, reach=3)
        _scar(S, int(el[0]) - 2, int(el[1]) - 6, 8, 0, 1)
    S.outline()
    return S


def draw_side(frame: int, seed: int = 106) -> Sprite:
    rng = random.Random(seed)
    S = Sprite(W, H, salt=seed)
    ph = frame * 2 * math.pi / NFRAMES
    sn = math.sin(ph)
    bob = 2 if abs(sn) > .8 else 0
    skin = ramp5(SKIN)
    dskin = [mix(c, (20, 18, 40), .28) for c in skin]
    S.shadow = ellipse(32, FOOT + 3, 24, 5)
    _legs(S, ph, True, False, skin)
    S.paint(poly([(20, 56), (44, 56), (46, 70), (18, 70)]), ramp5(PANTS), grain=.2, reach=12)
    for x in range(19, 46):
        S.set(x, 58, (150, 120, 76) if x % 3 else (96, 72, 46))
    sy = 30 + bob
    swing = int(round(4 * sn))

    def arm(near: bool) -> None:
        c = skin if near else dskin
        sh = (30, sy + 6)
        el = (20 - swing * .6, sy + 22)
        hd = (14 - swing * 1.4, sy + 38 - abs(swing))
        S.paint(capsule(sh, el, 7.2), c, grain=.15, edge=near, reach=10)
        S.paint(capsule(el, hd, 6.8), c, grain=.15, edge=near, reach=10)
        S.paint(ellipse(hd[0], hd[1] + 3, 8.4, 7.4), c, edge=near, reach=8)
        S.paint(ellipse(hd[0] + 1, hd[1] - 2, 7, 2.4), ramp5(IRON), edge=near, reach=3)
        if near:
            _chain(S, (hd[0], hd[1] - 1), (hd[0] - 4, hd[1] + 10), 4)
            _scar(S, int(el[0]) - 2, int(el[1]) - 5, 8, 0, 1)
    arm(False)
    torso = poly([(16, sy + 2), (26, sy - 4), (42, sy - 4), (52, sy + 4), (53, sy + 16), (48, sy + 28), (44, 60), (22, 60), (18, sy + 28), (14, sy + 14)])
    S.paint(torso, skin, grain=.18, reach=14)
    for (x, y, dx, dy, ln) in ((46, sy - 1, .8, -.7, 9), (50, sy + 8, 1, -.2, 8), (49, sy + 18, 1, .3, 7)):
        _spike(S, x, y, dx, dy, ln)                                              # spine spurs on the back
    for i in range(4):
        S.set(24 + i * 3, sy + 12 + i // 2, skin[0]); S.set(24 + i * 3, sy + 13 + i // 2, skin[1])
    _scar(S, 24, sy + 4, 14, 1, 1)
    for _ in range(3):
        S.tint(ellipse(rng.randint(20, 46), rng.randint(sy + 6, 56), rng.uniform(1.5, 4), rng.uniform(1.5, 3.4)), BLOOD[1], .65, .85)
    _chain(S, (22, sy + 2), (46, sy + 26), 9)
    _veins(S, rng, 34, sy + 2, 18)
    hx, hy = 22, sy - 8
    S.paint(rect(24, hy + 6, 32, sy + 1), skin, reach=6)
    S.paint(poly([(hx - 8, hy + 2), (hx - 5, hy - 6), (hx + 6, hy - 8), (hx + 12, hy - 2), (hx + 12, hy + 6), (hx + 6, hy + 12), (hx - 2, hy + 14),
                  (hx - 7, hy + 11), (hx - 9, hy + 6)]), skin, edge=True, reach=8)
    S.set(hx + 7, hy + 2, skin[1]); S.set(hx + 8, hy + 2, skin[0])
    for x in range(hx - 6, hx + 3):
        S.set(x, hy - 1, skin[0])
    for (x, y) in rect(hx - 6, hy + 1, hx - 3, hy + 3):
        S.set(x, y, (20, 16, 26))
    S.set(hx - 6, hy + 2, GLOW, True); S.set(hx - 5, hy + 2, GLOW, True)
    S.set(hx - 8, hy + 5, skin[0]); S.set(hx - 8, hy + 6, skin[1])
    for x in range(hx - 7, hx):
        S.set(x, hy + 9, (30, 12, 18)); S.set(x, hy + 10, (86, 22, 28))
    for x in (hx - 6, hx - 3):
        S.set(x, hy + 9, BONE)
    _spike(S, hx - 6, hy + 10, -.4, 1, 5)
    _scar(S, hx + 2, hy - 4, 8, 0, 1)
    _veins(S, rng, hx + 5, hy, 10)
    arm(True)
    S.outline()
    return S


VIEWS = {"down": draw_front, "up": draw_back, "left": draw_side}


def build() -> dict[str, Sprite]:
    return {f"zombie_brute_{d}_{f}": fn(f) for d, fn in VIEWS.items() for f in range(NFRAMES)}
