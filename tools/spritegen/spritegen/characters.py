"""People and zombies: one anatomy, three views (front, back, side), 8 walk frames (and 8 running ones for the players).

`Look` holds the appearance (identical in every view); `draw_front/back/side` draw it.
Right-facing sprites are the left-facing ones mirrored by the game, as in D-018.
"""

from __future__ import annotations

import math
import random
from dataclasses import dataclass, field

from .gfx import (Color, Sprite, capsule, ellipse, hexc, hnoise, mix, poly, ramp5, rect)

NFRAMES = 8
FOOT_Y = 44                      # pivot of every standing sprite: 44 px from the top of 48
GLOW: Color = (190, 255, 96)
GLOW_DIM: Color = (96, 190, 56)
BLOOD = [(104, 24, 30), (138, 34, 38), (168, 52, 50)]
WOUND = [(92, 24, 30), (150, 52, 56), (196, 96, 92)]
BONE: Color = (222, 214, 190)
CLAW: Color = (50, 44, 56)
METAL = (126, 130, 138)

SKINS = [(126, 146, 110), (144, 146, 116), (116, 138, 144), (152, 132, 116), (106, 130, 108)]
SHIRTS = [(150, 70, 62), (74, 100, 132), (150, 128, 66), (112, 116, 120), (98, 128, 78), (190, 188, 172), (86, 64, 96)]
PANTS = [(62, 74, 102), (96, 78, 62), (74, 76, 84), (54, 58, 50), (112, 102, 86)]
HAIRS = [(48, 38, 30), (92, 72, 48), (124, 118, 110), (30, 26, 26), (150, 100, 50)]
PATCHES = [(92, 100, 132), (68, 66, 44), (140, 90, 96)]
SKIN_HUMAN = (214, 164, 134)


@dataclass
class Look:
    kind: str = "zombie"              # zombie | human
    seed: int = 1
    build: str = "normal"             # normal | heavy | thin | brute
    skin: Color = (126, 146, 110)
    shirt: Color = (74, 100, 132)
    pants: Color = (62, 74, 102)
    hair: Color = (48, 38, 30)
    hair_style: str = "scruff"        # none | scruff | long | short
    tilt: int = 0
    head_off: int = 0
    pose: str = "hang"                # hang | one | both | scream | run
    reach_side: int = 1
    torn_sleeve: bool = True
    pattern: str = "plain"
    buttons: bool = False
    limp: bool = False
    mut: str | None = None            # tumor | bone | claw | sac | jaw
    stride: float = 2.6
    arm_swing: float = 1.7
    lift: float = 1.6                 # how high the swinging foot rises
    bob: int = 1                      # how far the body drops when both feet are down
    armor: bool = False
    hat: str = "none"                 # none | helmet | hardhat
    pack: str = "none"                # none | dynamite | bag
    vest: bool = False
    gown: bool = False
    extra_veins: int = 0
    drool: bool = False
    lean: int = 0                     # forward hunch (runner)
    belly: bool = False               # swollen belly (spitter)
    prop: str = ""                    # iv (patient zero's drip stand)
    hat_color: Color | None = None    # cap colour (defaults to the shirt)
    tie: Color | None = None
    badge: bool = False
    big_jaw: bool = False
    glow_eyes: Color = GLOW
    parts: dict = field(default_factory=dict)


def random_look(seed: int, build: str = "normal", kind: str = "zombie", **over) -> Look:
    rng = random.Random(seed * 7919 + 13)
    z = kind == "zombie"
    look = Look(
        kind=kind, seed=seed, build=build,
        skin=rng.choice(SKINS) if z else SKIN_HUMAN,
        shirt=rng.choice(SHIRTS) if z else (58, 98, 138),
        pants=rng.choice(PANTS) if z else (58, 56, 64),
        hair=rng.choice(HAIRS),
        hair_style=rng.choice(("none", "scruff", "long", "scruff")) if z else "short",
        tilt=rng.choice((0, 1, 2)) if z else 0,
        head_off=rng.choice((-1, 0, 1, 1)) if z else 0,
        pose=rng.choice(("hang", "one", "both", "one")) if z else "hang",
        reach_side=rng.choice((-1, 1)),
        torn_sleeve=rng.random() < .6,
        pattern=rng.choice(("plain", "plaid", "stripes", "logo", "plain")) if z else "plain",
        buttons=rng.random() < .6,
        limp=z and rng.random() < .3,
        mut=rng.choice((None, None, "tumor", "bone", "claw", "sac", "jaw")) if z else None,
    )
    for k, v in over.items():
        setattr(look, k, v)
    return look


# ------------------------------------------------------------------ shared pose maths
class Pose:
    def __init__(self, look: Look, frame: int):
        self.ph = frame * 2 * math.pi / NFRAMES if frame >= 0 else 0.0
        self.k = 0.0 if frame < 0 else 1.0     # frame -1: standing still, feet side by side and arms down
        self.sn, self.cs = math.sin(self.ph) * self.k, math.cos(self.ph) * self.k
        self.bob = int(look.bob * abs(self.sn) + .25)        # lowest with the feet apart, highest as they pass
        z = look.kind == "zombie"
        self.sway = (int(round(self.sn * .9)) + (1 if look.limp else 0)) if z else 0
        self.swing = int(round(look.arm_swing * self.sn))
        self.wd = {"normal": 0, "heavy": 2, "thin": -1, "brute": 6}[look.build]
        self.thick = 1.0 + (0.45 if look.build == "brute" else 0.0)


def _ramps(look: Look):
    skin, shirt, pants = ramp5(look.skin), ramp5(look.shirt), ramp5(look.pants)
    shoe = ramp5((52, 44, 48) if look.kind == "zombie" else (74, 58, 44))
    hair = ramp5(look.hair)
    return skin, shirt, pants, shoe, hair


def _foot(look: Look, pose: Pose, side: int, back: bool = False):
    p = pose.ph if side < 0 else pose.ph + math.pi
    amp = .7 if (look.limp and side > 0) else look.stride
    sgn = -1 if back else 1                 # walking away: the forward foot rises on screen
    k = pose.k
    return (16 + side * (3.6 + .4 * math.cos(p) * k), 41.8 + sgn * amp * math.sin(p) * k - look.lift * max(0, math.cos(p)) * k)


def _legs(S: Sprite, look: Look, pose: Pose, back: bool, wy: int, pants, shoe, skin, rng: random.Random):
    wd, th = pose.wd, pose.thick
    legs = sorted([(_foot(look, pose, -1, back), -1), (_foot(look, pose, 1, back), 1)], key=lambda lf: lf[0][1])
    hole = rng.choice((-1, 1))
    for fpos, side in legs:
        hip = (16 + side * (3 + wd * .2), wy + 1)
        knee = (hip[0] * .45 + fpos[0] * .55 + side * .8, hip[1] * .45 + fpos[1] * .55 - .5)
        S.paint(capsule(hip, knee, (2.9 + wd * .25) * (1 if wd < 6 else 1.0) * th), pants, grain=.14)
        S.paint(capsule(knee, fpos, (2.4 + wd * .2) * th), pants, grain=.14)
        for t in (.2, .45, .7):
            S.set(hip[0] + (knee[0] - hip[0]) * t + side * 1.6, hip[1] + (knee[1] - hip[1]) * t, pants[3])
        S.paint(ellipse(fpos[0] + side * .3, fpos[1] + .6, 3.3 * th, 1.9), shoe, edge=True)
        fx, fy = int(fpos[0]), int(fpos[1])
        if not back:
            S.set(fx, fy, shoe[4]); S.set(fx + side, fy, shoe[3])
        for dx in range(-2, 3):
            S.set(fx + dx + (1 if side > 0 else 0), fy + 2, shoe[0])
        if look.kind == "zombie" and side == hole and not back:
            kx, ky = int(knee[0]), int(knee[1])
            for (x, y) in ((kx - 1, ky), (kx, ky), (kx, ky + 1)):
                S.set(x, y, skin[2])
            S.set(kx + 1, ky, BLOOD[1])
        elif look.kind == "human" and not back:
            S.set(int(knee[0]), int(knee[1]), pants[3])
        S.set(fx - 2 + (1 if side > 0 else 0), fy - 3, pants[0])


def _veins(S: Sprite, rng: random.Random, x: int, y: int, vs: int, n: int = 7) -> None:
    for i in range(n):
        if (x, y) in S.px:
            S.set(x, y, GLOW_DIM if i % 3 else mix(GLOW_DIM, GLOW, .4), True)
        y += 1
        if i in (2, 4):
            x -= vs


def _hat(S: Sprite, look: Look, hx: int, hy: int, side: str = "front") -> None:
    if look.hat == "hardhat":
        yel = ramp5((226, 178, 40))
        S.paint(poly([(hx - 4.8, hy - 2), (hx - 3.8, hy - 6.2), (hx + 3.8, hy - 6.2), (hx + 4.8, hy - 2)]), yel, edge=True)
        for x in range(hx - 5, hx + 6):
            S.set(x, hy - 2, yel[1])
        if side == "front":
            S.set(hx, hy - 5, (255, 244, 200), True)
            S.set(hx - 1, hy - 5, (255, 244, 200)); S.set(hx + 1, hy - 5, (255, 244, 200))
    elif look.hat == "cap":
        cc = ramp5(look.hat_color or look.shirt)
        if side == "front":
            S.paint(poly([(hx - 4.6, hy - 2.2), (hx - 3.8, hy - 6.2), (hx + 3.8, hy - 6.2), (hx + 4.6, hy - 2.2)]), cc, grain=.15, edge=True)
            S.paint(rect(hx - 5, hy - 3, hx + 5, hy - 2), [mix(c, (0, 0, 0), .25) for c in cc], edge=True)
        elif side == "back":
            S.paint(poly([(hx - 4.8, hy - 1.6), (hx - 3.8, hy - 6.2), (hx + 3.8, hy - 6.2), (hx + 4.8, hy - 1.6)]), cc, grain=.15, edge=True)
        else:
            S.paint(poly([(hx - 3.2, hy - 2.4), (hx - 2.4, hy - 6.4), (hx + 4.4, hy - 5.6), (hx + 4.6, hy - 1.4), (hx + 1, hy - 2.2)]), cc, grain=.15, edge=True)
            S.paint(rect(hx - 7, hy - 3, hx - 2, hy - 2), [mix(c, (0, 0, 0), .25) for c in cc], edge=True)
        if look.badge and side == "front":
            S.set(hx, hy - 5, (226, 190, 70)); S.set(hx, hy - 4, (180, 140, 40))
    elif look.hat == "hood":
        hd = ramp5(look.hat_color or look.shirt)
        outer = poly([(hx - 5.6, hy + 3), (hx - 5.2, hy - 4), (hx - 2.4, hy - 7.2), (hx + 2.4, hy - 7.2), (hx + 5.2, hy - 4), (hx + 5.6, hy + 3), (hx + 3.4, hy + 5), (hx - 3.4, hy + 5)])
        if side == "front":
            outer = outer - ellipse(hx, hy - .2, 3.7, 4.3)
        elif side == "side":
            outer = outer - ellipse(hx - 2.2, hy, 3.6, 4.3)
        S.paint(outer, hd, grain=.2, edge=True)
    elif look.hat == "helmet":
        m = ramp5(METAL)
        S.paint(poly([(hx - 4.9, hy - 1), (hx - 4, hy - 6.2), (hx + 4, hy - 6.2), (hx + 4.9, hy - 1), (hx + 3.4, hy + 0.4), (hx - 3.4, hy + 0.4)]), m, grain=.2, edge=True)
        if side == "front":
            for x in range(hx - 3, hx + 4):
                S.set(x, hy - 2, (14, 12, 18))
            S.set(hx - 2, hy - 2, look.glow_eyes, True); S.set(hx + 2, hy - 2, look.glow_eyes, True)
        for x in (hx - 3, hx + 3):
            S.set(x, hy - 5, m[4])


def _armor(S: Sprite, look: Look, pose: Pose, Lx: int, Rx: int, syl: int, syr: int, cx: int, sy: int, side: str = "front") -> None:
    m = ramp5(METAL)
    if side == "side":
        S.paint(ellipse(cx + 1, syl + 1.5, 3.4, 2.6), m, grain=.2, edge=True)
        S.set(cx + 1, syl + 1, m[4])
        return
    S.paint(ellipse(Lx + .5, syl + .6, 3.2, 2.3), m, grain=.2, edge=True)
    S.paint(ellipse(Rx - .5, syr + .6, 3.2, 2.3), m, grain=.2, edge=True)
    if side == "front":
        S.paint(poly([(cx - 3.8, sy + 1.2), (cx + 3.8, sy + 1.2), (cx + 3, sy + 9), (cx - 3, sy + 9)]), m, grain=.15, edge=True)
        for (x, y) in ((cx - 2, sy + 3), (cx + 2, sy + 3), (cx - 2, sy + 7), (cx + 2, sy + 7)):
            S.set(x, y, m[4])
    else:
        S.paint(poly([(cx - 3.8, sy + 1), (cx + 3.8, sy + 1), (cx + 3.2, sy + 10), (cx - 3.2, sy + 10)]), m, grain=.15, edge=True)
        for y in range(sy + 2, sy + 10, 2):
            S.set(cx, y, m[0])


def _pack(S: Sprite, look: Look, cx: int, sy: int, side: str) -> None:
    if look.pack == "none":
        return
    if look.pack == "dynamite":
        S.paint(poly([(cx - 4.8, sy + 1.5), (cx + 4.8, sy + 1.5), (cx + 4.8, sy + 11), (cx - 4.8, sy + 11)]), ramp5((120, 100, 60)), grain=.2, edge=True)
        for i, x in enumerate((cx - 3, cx, cx + 3)):
            S.paint(rect(x - 1, sy + 3, x, sy + 8), ramp5((190, 40, 40)), edge=True)
            S.set(x - 1, sy + 2, (230, 220, 190))
    else:
        S.paint(poly([(cx - 4.6, sy + 1), (cx + 4.6, sy + 1), (cx + 4.6, sy + 12), (cx - 4.6, sy + 12)]), ramp5((70, 82, 58)), grain=.2, edge=True)
        S.paint(rect(cx - 3, sy + 7, cx + 3, sy + 10), ramp5((60, 72, 50)), edge=True)


def _iv(S: Sprite, x: int, bag_x: int, side: str = "front") -> None:
    """Patient zero drags an IV stand: a thin pole, a bag with glowing fluid, a tube to the arm."""
    steel = ramp5((176, 182, 190))
    S.paint(rect(x, 4, x, 41), steel, reach=2)
    S.paint(rect(x - 3, 41, x + 3, 42), steel, edge=True, reach=2)
    for wx in (x - 3, x + 3):
        S.paint(ellipse(wx, 44, 1.6, 1.4), ramp5((60, 60, 66)), edge=True, reach=2)
    S.paint(rect(x - 3, 3, x + 3, 3), steel, reach=2)
    S.paint(ellipse(bag_x, 8, 2.8, 3.6), ramp5((190, 226, 220)), edge=True, reach=3)
    S.set(bag_x, 7, (240, 255, 220), True); S.set(bag_x, 9, GLOW_DIM, True); S.set(bag_x - 1, 8, GLOW_DIM, True)
    for i in range(10):
        S.set(bag_x + (1 if side == "front" else -1) * (i // 4), 12 + i * 2, (170, 220, 200))


def _belly(S: Sprite, cx: int, sy: int, shirt, skin, side: str = "front") -> None:
    if side == "front":
        S.paint(ellipse(cx, sy + 11, 8.6, 5.6), skin, edge=True, reach=6)
        S.paint(ellipse(cx, sy + 12.5, 8.2, 4.4), shirt, grain=.2, reach=6)
        for x in range(cx - 7, cx + 8, 3):
            S.set(x, sy + 9, shirt[1])
        S.set(cx, sy + 10, GLOW_DIM, True)
    else:
        S.paint(ellipse(cx - 4.5, sy + 11, 5.4, 6), skin, edge=True, reach=6)
        S.paint(ellipse(cx - 4.5, sy + 12, 5, 5), shirt, grain=.2, reach=6)


# ------------------------------------------------------------------ front view
def draw_front(look: Look, frame: int) -> Sprite:
    z = look.kind == "zombie"
    rng = random.Random(look.seed * 31 + 7)
    pose = Pose(look, frame)
    S = Sprite(salt=look.seed)
    skin, shirt, pants, shoe, hair = _ramps(look)
    wd, th = pose.wd, pose.thick
    cl = 16
    cx = 16 + pose.sway
    sy = 14 + pose.bob + (1 if z else 0) + look.lean
    Lx, Rx = cx - 6 - wd, cx + 6 + wd
    syl, syr = sy + look.tilt, sy - (1 if look.tilt == 2 else 0)
    wy = sy + 13
    wl, wr = cx - 4 - (1 if wd > 0 else 0) - (wd // 3), cx + 4 + (1 if wd > 0 else 0) + (wd // 3)
    S.shadow = ellipse(cl, 44, 9 + wd * .6, 2.6)
    if look.prop == "iv":
        _iv(S, cx + 11, cx + 11, "front")
    _legs(S, look, pose, False, wy, pants, shoe, skin, rng)
    S.paint(poly([(cl - 4.5 - wd * .3, wy - 1), (cl + 4.5 + wd * .3, wy - 1), (cl + 5.5 + wd * .3, wy + 4), (cl - 5.5 - wd * .3, wy + 4)]), pants, grain=.1)
    if look.pack == "bag" or (look.kind == "human" and look.pack == "none"):
        S.paint(poly([(Lx - 1.5, sy + 1), (Rx + 1.5, sy + 1), (Rx + 1.5, sy + 12), (Lx - 1.5, sy + 12)]), ramp5((70, 82, 58)), grain=.2, edge=True)
    torso = poly([(Lx, syl), (cx - 3, sy - 1), (cx + 3, sy - 1), (Rx, syr), (Rx - 1, sy + 7), (wr, wy), (wl, wy), (Lx + 1, sy + 7)])
    if z and not look.gown:
        for _ in range(3):
            x = rng.randint(wl, wr)
            for y in range(wy - 1 - rng.randint(0, 2), wy):
                torso.discard((x, y))
    S.paint(torso, shirt, grain=.16)
    if look.pattern == "plaid":
        S.tint([p for p in torso if p[0] % 5 == 0], (255, 255, 255), .14)
        S.tint([p for p in torso if p[1] % 5 == 0], (0, 0, 0), .18)
    elif look.pattern == "stripes":
        S.tint([p for p in torso if p[1] % 3 == 0], (255, 255, 255), .16)
    elif look.pattern == "logo":
        for (x, y) in rect(cx + 1, sy + 4, cx + 4, sy + 6):
            S.set(x, y, (212, 206, 190))
        S.set(cx + 2, sy + 5, (160, 40, 40)); S.set(cx + 3, sy + 5, (160, 40, 40))
    if look.vest:
        S.tint(torso, (255, 140, 30), .6)
        for y in (sy + 5, wy - 3):
            for x in range(wl, wr + 1):
                S.set(x, y, (230, 230, 196))
    for x in range(wl + 1, wr):
        S.set(x, wy, mix((44, 34, 28), (18, 14, 14), .3 if x % 2 else 0))
    S.set(cx, wy, (200, 180, 118))
    if z:
        for x in range(wl + 1, wr):
            if (x, wy - 1) not in torso and not look.gown:
                S.set(x, wy - 1, skin[1])
        S.paint(poly([(cx - 2, sy - 1), (cx + 2, sy - 1), (cx, sy + 3)]), skin)
        if look.buttons and not look.gown:
            for k in range(3):
                S.set(cx, sy + 4 + k * 3, shirt[4])
        for _ in range(rng.randint(2, 4)):
            S.tint(ellipse(rng.randint(Lx + 3, Rx - 3), rng.randint(sy + 3, wy - 2), rng.uniform(1, 2.8), rng.uniform(1, 2.5)), BLOOD[1], .7, .85)
        gx, gy = rng.randint(cx - 4, cx + 1), rng.randint(sy + 4, sy + 7)
        for i in range(4):
            S.set(gx + i, gy + (i // 2), WOUND[0 if i % 3 == 0 else 1])
        S.set(gx + 1, gy, WOUND[2])
    else:
        for y in range(sy + 1, wy):
            S.set(cx, y, shirt[3] if y % 2 else shirt[0])
        for (x, y) in ((cx - 4, sy + 8), (cx - 3, sy + 8), (cx - 2, sy + 8), (cx + 2, sy + 8), (cx + 3, sy + 8), (cx + 4, sy + 8)):
            S.set(x, y, shirt[1])
        for y in range(sy, sy + 8):
            S.set(cx - 3, y, (34, 36, 30)); S.set(cx + 3, y, (34, 36, 30))
        for x in range(cx - 2, cx + 3):
            S.set(x, sy, shirt[4])
    for fx_ in (cx - 3, cx + 2):
        for y in range(sy + 4, sy + 10):
            if hnoise(fx_, y, look.seed) < .6:
                S.set(fx_, y, shirt[1])
    if look.tie is not None:
        tr = ramp5(look.tie)
        S.paint(poly([(cx - 1.4, sy + 1), (cx + 1.4, sy + 1), (cx + 1.8, sy + 3), (cx - 1.8, sy + 3)]), tr, reach=2)
        S.paint(poly([(cx - 1.6, sy + 3), (cx + 1.6, sy + 3), (cx + 2.2, sy + 11), (cx, sy + 12.4), (cx - 2.2, sy + 11)]), tr, reach=3)
        for x in (cx - 3, cx + 3):
            S.set(x, sy, (232, 232, 224))
    if look.badge:
        S.set(cx + 3, sy + 4, (226, 190, 70)); S.set(cx + 3, sy + 5, (180, 140, 40)); S.set(cx + 2, sy + 4, (240, 214, 120))
    if look.mut == "bone":
        for (x, y) in rect(cx + 2, sy + 5, cx + 6, sy + 9):
            S.set(x, y, (84, 20, 28))
        for i, y in enumerate((sy + 5, sy + 7, sy + 9)):
            for x in range(cx + 2, cx + 6 - i % 2):
                S.set(x, y, (214, 204, 178) if x % 2 else (168, 158, 134))
    if look.belly:
        _belly(S, cx, sy, shirt, skin, "front")
    if look.armor:
        _armor(S, look, pose, Lx, Rx, syl, syr, cx, sy, "front")
    # neck, head
    hx, hy = cx + look.head_off, sy - 6 + (1 if z else 0)
    S.paint(rect(cx - 1, sy - 4, cx + 1, sy), skin)
    S.paint(poly([(hx - 4.2, hy - 3), (hx - 2.2, hy - 5.2), (hx + 2.2, hy - 5.2), (hx + 4.2, hy - 3), (hx + 4.2, hy + 1), (hx + 2.6, hy + 4), (hx, hy + 5.2),
                  (hx - 2.6, hy + 4), (hx - 4.2, hy + 1)]), skin, edge=True)
    S.set(hx - 5, hy, skin[1]); S.set(hx + 4, hy, skin[1]); S.set(hx - 5, hy + 1, skin[0]); S.set(hx + 4, hy + 1, skin[0])
    S.set(hx - 5, hy - 1, skin[3]); S.set(hx + 4, hy - 1, skin[2])
    S.set(hx, hy, skin[4]); S.set(hx, hy + 1, skin[1]); S.set(hx - 1, hy + 1, skin[0]); S.set(hx + 1, hy + 1, skin[0])
    side_b = rng.choice((-1, 1))
    if z:
        for ex, big in ((hx - 2, rng.random() < .5), (hx + 2, rng.random() < .5)):
            for (x, y) in rect(ex - 1, hy - 2, ex + (1 if big else 0), hy - 1):
                S.set(x, y, (20, 16, 26) if y == hy - 2 else (34, 28, 40))
            S.set(ex, hy - 1, look.glow_eyes, True)
            S.set(ex + (1 if ex > hx else -1), hy - 1, GLOW_DIM, True)
            if look.extra_veins:
                S.set(ex + (1 if ex > hx else -1) * 2, hy - 1, look.glow_eyes, True)
        for x in range(hx - 3, hx + 4):
            if (x, hy - 3) in S.px and x != hx:
                S.set(x, hy - 3, skin[1])
        S.set(hx - 3, hy + 1, skin[0]); S.set(hx + 3, hy + 1, skin[0]); S.set(hx - 3, hy + 2, skin[3]); S.set(hx + 2, hy + 2, skin[3])
        for x in range(hx - 2, hx + 3):
            S.set(x, hy + 3, (30, 12, 18))
        for x in (hx - 2, hx, hx + 2):
            S.set(x, hy + 3, BONE)
        for x in range(hx - 1, hx + 2):
            S.set(x, hy + 4, (86, 22, 28))
        S.set(hx, hy + 4, (158, 64, 74))
        if rng.random() < .7:
            S.set(hx - 1, hy + 4, BONE)
        S.set(hx - 1, hy + 2, skin[0]); S.set(hx + 1, hy + 2, skin[0])
        if rng.random() < .5:
            for i in range(4):
                S.set(hx + 2 + i // 2, hy - 3 + i, skin[4] if i % 2 else WOUND[1])
        for i in range(rng.randint(1, 3)):
            S.set(hx + side_b * 2, hy + 4 + i, BLOOD[i % 2])
        S.tint(ellipse(hx + rng.choice((-2, 2)), hy + rng.choice((-1, 1)), 2.2, 1.8), rng.choice(PATCHES), .5, .9)
        S.tint(ellipse(Lx + 2, sy + 10, 2, 3), rng.choice(PATCHES), .45, .8)
        _veins(S, rng, hx - side_b * 4, hy - 1, -side_b)
        for k in range(look.extra_veins):
            _veins(S, rng, hx + (k - 1) * 2, hy + 4, 1 if k % 2 else -1, 5)
            _veins(S, rng, Lx + 3 + k * 3, sy + 3, 1, 6)
        if look.drool:
            for i in range(4):
                S.set(hx - 1 + (i // 2), hy + 5 + i, GLOW_DIM, True)
        if look.hair_style != "none":
            style = poly([(hx - 4, hy - 2), (hx - 3, hy - 5.4), (hx, hy - 6.4), (hx + 3, hy - 5.4), (hx + 4, hy - 2), (hx + 2, hy - 3.2), (hx - 2, hy - 3.2)])
            for p in rng.sample(sorted(style), 3):
                style.discard(p)
            S.paint(style, hair, grain=.3, edge=True)
            if look.hair_style == "long":
                for x_ in (hx - 5, hx + 4):
                    for y_ in range(hy - 3, hy + 4):
                        S.set(x_, y_, hair[1] if hnoise(x_, y_, look.seed) < .7 else hair[0])
            for _ in range(4):
                S.set(hx + rng.randint(-4, 4), hy - 6 + rng.randint(0, 1), hair[0])
    else:
        for ex in (hx - 2, hx + 2):
            S.set(ex - 1, hy - 1, (236, 232, 222)); S.set(ex, hy - 1, (36, 30, 34))
            S.set(ex - 1, hy - 2, skin[1]); S.set(ex, hy - 2, hair[1])
        for x in range(hx - 1, hx + 2):
            S.set(x, hy + 3, skin[0])
        S.set(hx, hy + 2, skin[1]); S.set(hx - 1, hy + 2, skin[0]); S.set(hx + 1, hy + 2, skin[0])
        for x in range(hx - 3, hx + 4):
            if hnoise(x, hy + 4, look.seed) < .55:
                S.set(x, hy + 4, mix(skin[2], hair[1], .45))
        S.set(hx - 3, hy + 2, skin[3]); S.set(hx + 3, hy + 1, skin[0])
        S.paint(poly([(hx - 4.4, hy - 1.6), (hx - 3.4, hy - 5.4), (hx, hy - 6.4), (hx + 3.4, hy - 5.4), (hx + 4.4, hy - 1.6), (hx + 2.6, hy - 3), (hx - 2.6, hy - 3)]),
                hair, grain=.25, edge=True)
        for (x, y) in ((hx - 2, hy - 5), (hx - 1, hy - 5), (hx + 1, hy - 6)):
            S.set(x, y, hair[4])
    _hat(S, look, hx, hy, "front")
    # arms
    def arm(shoulder, elbow, hand, bare, claw, vein):
        S.paint(capsule(shoulder, elbow, 2.3 * th), shirt, grain=.14, edge=True)
        if bare and z:
            for k in range(3):
                S.set(elbow[0] - 2 + k * 2, elbow[1] - 1, shirt[0])
        S.paint(capsule(elbow, hand, 1.9 * th), skin if bare else shirt, grain=0 if bare else .1, edge=True)
        glove = not (bare or z)
        hc = ramp5((44, 44, 50)) if glove else skin
        S.paint(ellipse(hand[0], hand[1] + 1.1, 1.8 * th, 1.9 * th), hc, edge=True)
        fx, fy = int(hand[0]), int(hand[1]) + 3
        for dx in (-1, 0, 1):
            S.set(fx + dx, fy, hc[1] if dx else hc[0])
        S.set(fx - 1, fy + 1, hc[0]); S.set(fx + 1, fy + 1, hc[0])
        if claw:
            for dx in (-1, 0, 1):
                S.set(fx + dx, fy + 1, CLAW); S.set(fx + dx, fy + 2, CLAW)
            S.set(fx - 1, fy + 3, (214, 206, 190)); S.set(fx + 1, fy + 3, (214, 206, 190))
        if bare and z and rng.random() < .8:
            mx, my = int(elbow[0] * .4 + hand[0] * .6), int(elbow[1] * .4 + hand[1] * .6)
            for i, c in enumerate(WOUND):
                S.set(mx - 1 + i, my, c)
            S.set(mx, my + 1, WOUND[0])
            if look.mut == "bone":
                S.set(mx, my - 1, BONE); S.set(mx + 1, my - 1, (170, 160, 138))
        if vein:
            S.set(int(hand[0]), int(hand[1]) - 1, GLOW_DIM, True)

    arms = []
    for side in (-1, 1):
        shoulder = ((Lx + 1.2) if side < 0 else (Rx - 1.2), (syl if side < 0 else syr) + 1.6)
        swing = pose.swing * (1 if side < 0 else -1)
        reach = z and (look.pose in ("both", "scream") or (look.pose == "one" and side == look.reach_side))
        if look.pose == "scream":
            elbow = (cx + side * (Rx - cx + 2.5), sy + 4)
            hand = (cx + side * (Rx - cx + 1), sy - 2)
        elif reach:
            elbow = (cx + side * (Rx - cx + 2), sy + 8)
            hand = (cx + side * 4.5, sy + 10 + swing * .4)
        else:
            elbow = (cx + side * (Rx - cx + .5), sy + 8 + swing * .5)
            hand = (cx + side * (Rx - cx + (1 if z else .5)), sy + 15 + swing)
        arms.append((reach, side, shoulder, elbow, hand))
    for reach, side, shoulder, elbow, hand in sorted(arms, key=lambda a: a[0]):
        bare = z and (look.torn_sleeve or side == look.reach_side) and not look.gown
        arm(shoulder, elbow, hand, bare, claw=(look.mut == "claw" and side == look.reach_side) or look.pose == "run", vein=z and reach)
        if not z and side > 0:
            fx, fy = int(hand[0]), int(hand[1])
            S.paint(rect(fx + 1, fy, fx + 4, fy + 1), ramp5((72, 74, 80)), edge=True)
            S.set(fx + 5, fy, (255, 240, 190), True)
            S.meta["flash"] = (fx + 5, fy)
    if look.mut == "tumor":
        tx = Lx + 1 if look.reach_side > 0 else Rx - 1
        S.paint(ellipse(tx, syl - 1.2, 3.4, 2.8), ramp5((156, 154, 112)), edge=True)
        S.tint(ellipse(tx, syl - 1, 2, 1.6), (190, 90, 96), .45)
        S.set(tx, syl - 2, GLOW_DIM, True); S.set(tx + 1, syl - 1, GLOW_DIM, True); S.set(tx - 1, syl, (210, 190, 140))
    elif look.mut == "sac":
        gx, gy = cx + side_b * 2, sy + 1.6
        S.paint(ellipse(gx, gy, 3.1, 2.3), ramp5((110, 176, 78)), edge=True)
        S.set(gx, gy - 1, (214, 255, 130), True); S.set(gx + 1, gy, GLOW, True); S.set(gx - 1, gy, GLOW_DIM, True); S.set(gx - 2, gy - 2, GLOW_DIM, True)
    elif look.mut == "jaw" or look.big_jaw:
        r = 3.2 if look.big_jaw else 2.4
        S.paint(ellipse(hx, hy + 5.4, r, r + .4), [(30, 12, 18), (60, 16, 24), (86, 22, 28), (120, 36, 44), (150, 60, 66)], edge=True)
        for x in (hx - 1, hx + 1):
            S.set(x, hy + 4, BONE); S.set(x, hy + 7, BONE)
        S.set(hx, hy + 6, (170, 70, 80))
    S.outline()
    return S


# ------------------------------------------------------------------ back view
def draw_back(look: Look, frame: int) -> Sprite:
    z = look.kind == "zombie"
    rng = random.Random(look.seed * 31 + 7)
    pose = Pose(look, frame)
    S = Sprite(salt=look.seed)
    skin, shirt, pants, shoe, hair = _ramps(look)
    wd, th = pose.wd, pose.thick
    cl = 16
    cx = 16 + pose.sway
    sy = 14 + pose.bob + (1 if z else 0) + look.lean
    Lx, Rx = cx - 6 - wd, cx + 6 + wd
    syl, syr = sy + look.tilt, sy - (1 if look.tilt == 2 else 0)
    wy = sy + 13
    wl, wr = cx - 4 - (1 if wd > 0 else 0) - (wd // 3), cx + 4 + (1 if wd > 0 else 0) + (wd // 3)
    S.shadow = ellipse(cl, 44, 9 + wd * .6, 2.6)
    if look.prop == "iv":
        _iv(S, cx - 11, cx - 11, "back")
    _legs(S, look, pose, True, wy, pants, shoe, skin, rng)
    S.paint(poly([(cl - 4.5 - wd * .3, wy - 1), (cl + 4.5 + wd * .3, wy - 1), (cl + 5.5 + wd * .3, wy + 4), (cl - 5.5 - wd * .3, wy + 4)]), pants, grain=.1)
    for x0 in (cl - 4, cl + 1):                                           # back pockets
        S.paint(rect(x0, wy + 1, x0 + 2, wy + 3), [pants[0], pants[0], pants[1], pants[1], pants[2]])
    # arms hang or reach out sideways; drawn behind the torso from this side
    def back_arm(side):
        shoulder = ((Lx + 1.2) if side < 0 else (Rx - 1.2), (syl if side < 0 else syr) + 1.6)
        swing = pose.swing * (1 if side < 0 else -1)
        reach = z and (look.pose in ("both", "scream") or (look.pose == "one" and side == look.reach_side))
        if look.pose == "scream":
            elbow, hand = (cx + side * (Rx - cx + 2.5), sy + 4), (cx + side * (Rx - cx + 1), sy - 2)
        elif reach:
            elbow, hand = (cx + side * (Rx - cx + 2), sy + 7), (cx + side * (Rx - cx + 2.5), sy + 11)
        else:
            elbow, hand = (cx + side * (Rx - cx + .5), sy + 8 + swing * .5), (cx + side * (Rx - cx + (1 if z else .5)), sy + 15 + swing)
        bare = z and (look.torn_sleeve or side == look.reach_side) and not look.gown
        S.paint(capsule(shoulder, elbow, 2.3 * th), shirt, grain=.14, edge=True)
        S.paint(capsule(elbow, hand, 1.9 * th), skin if bare else shirt, edge=True)
        hc = skin if (bare or z) else ramp5((44, 44, 50))
        S.paint(ellipse(hand[0], hand[1] + 1.1, 1.8 * th, 1.9 * th), hc, edge=True)
        fx, fy = int(hand[0]), int(hand[1]) + 3
        for dx in (-1, 0, 1):
            S.set(fx + dx, fy, hc[1] if dx else hc[0])
        if look.mut == "claw" and side == look.reach_side:
            for dx in (-1, 0, 1):
                S.set(fx + dx, fy + 1, CLAW)
            S.set(fx - 1, fy + 2, (214, 206, 190)); S.set(fx + 1, fy + 2, (214, 206, 190))
    torso = poly([(Lx, syl), (cx - 3, sy - 1), (cx + 3, sy - 1), (Rx, syr), (Rx - 1, sy + 7), (wr, wy), (wl, wy), (Lx + 1, sy + 7)])
    if z and not look.gown:
        for _ in range(3):
            x = rng.randint(wl, wr)
            for y in range(wy - 1 - rng.randint(0, 2), wy):
                torso.discard((x, y))
    S.paint(torso, shirt, grain=.16)
    if look.pattern == "plaid":
        S.tint([p for p in torso if p[0] % 5 == 0], (255, 255, 255), .14)
        S.tint([p for p in torso if p[1] % 5 == 0], (0, 0, 0), .18)
    elif look.pattern == "stripes":
        S.tint([p for p in torso if p[1] % 3 == 0], (255, 255, 255), .16)
    if look.vest:
        S.tint(torso, (255, 140, 30), .6)
        for y in (sy + 5, wy - 3):
            for x in range(wl, wr + 1):
                S.set(x, y, (230, 230, 196))
    for x in range(wl + 1, wr):
        S.set(x, wy, mix((44, 34, 28), (18, 14, 14), .3 if x % 2 else 0))
    for fx_ in (cx - 3, cx + 2):
        for y in range(sy + 4, sy + 10):
            if hnoise(fx_, y, look.seed) < .6:
                S.set(fx_, y, shirt[1])
    for y in range(sy, wy):                                               # spine seam
        if y % 2:
            S.set(cx, y, shirt[1])
    if z:
        for _ in range(rng.randint(2, 4)):
            S.tint(ellipse(rng.randint(Lx + 3, Rx - 3), rng.randint(sy + 3, wy - 2), rng.uniform(1, 2.8), rng.uniform(1, 2.5)), BLOOD[1], .7, .85)
        if look.mut == "bone":
            for i, y in enumerate(range(sy + 1, wy - 1, 2)):               # exposed spine
                S.set(cx, y, BONE); S.set(cx - 1, y, (168, 158, 134)); S.set(cx, y + 1, WOUND[0])
        gx, gy = rng.randint(cx - 4, cx + 1), rng.randint(sy + 3, sy + 6)
        for i in range(3):
            S.set(gx + i, gy + (i // 2), WOUND[0 if i % 2 == 0 else 1])
    if look.pack != "none" or look.kind == "human":
        _pack(S, look, cx, sy, "back") if look.pack != "none" else S.paint(
            poly([(cx - 4.6, sy + 1), (cx + 4.6, sy + 1), (cx + 4.6, sy + 12), (cx - 4.6, sy + 12)]), ramp5((70, 82, 58)), grain=.2, edge=True)
        if look.pack == "none":
            S.paint(rect(cx - 3, sy + 7, cx + 3, sy + 10), ramp5((60, 72, 50)), edge=True)
            for x in (cx - 4, cx + 4):
                S.set(x, sy + 2, (34, 36, 30))
    if look.armor:
        _armor(S, look, pose, Lx, Rx, syl, syr, cx, sy, "back")
    for side in (-1, 1):
        back_arm(side)
    # head from behind
    hx, hy = cx + look.head_off, sy - 6 + (1 if z else 0)
    S.paint(rect(cx - 1, sy - 4, cx + 1, sy), skin)
    S.paint(poly([(hx - 4.2, hy - 3), (hx - 2.2, hy - 5.2), (hx + 2.2, hy - 5.2), (hx + 4.2, hy - 3), (hx + 4.2, hy + 1), (hx + 2.6, hy + 4), (hx, hy + 5.2),
                  (hx - 2.6, hy + 4), (hx - 4.2, hy + 1)]), skin, edge=True)
    S.set(hx - 5, hy, skin[1]); S.set(hx + 4, hy, skin[1]); S.set(hx - 5, hy + 1, skin[0]); S.set(hx + 4, hy + 1, skin[0])
    hs = look.hair_style
    if not z:
        hs = "short"
    if hs == "none":
        S.tint(ellipse(hx + rng.choice((-2, 2)), hy - 1, 2.6, 2.2), rng.choice(PATCHES), .5)
        for i in range(3):
            S.set(hx - 1 + i, hy - 2 + (i % 2), WOUND[i % 3])               # scalp wound
        S.set(hx, hy - 1, GLOW_DIM, True)
    else:
        extent = 5.0 if hs == "long" else 2.6
        S.paint(poly([(hx - 4.6, hy - 1.6), (hx - 3.6, hy - 5.6), (hx, hy - 6.6), (hx + 3.6, hy - 5.6), (hx + 4.6, hy - 1.6), (hx + 4, hy + extent),
                      (hx - 4, hy + extent)]), hair, grain=.28, edge=True)
        for (x, y) in ((hx - 2, hy - 5), (hx + 1, hy - 5), (hx, hy - 2)):
            S.set(x, y, hair[4])
        if hs == "long":
            for y in range(hy + 1, hy + 6):
                S.set(hx - 4, y, hair[0]); S.set(hx + 3, y, hair[1])
    if z:
        _veins(S, rng, hx + rng.choice((-1, 1)), hy + 3, 1, 5)
        for k in range(look.extra_veins):
            _veins(S, rng, hx + (k - 1) * 2, hy + 3, 1 if k % 2 else -1, 5)
    _hat(S, look, hx, hy, "back")
    if look.mut == "tumor":
        tx = Lx + 1 if look.reach_side > 0 else Rx - 1
        S.paint(ellipse(tx, syl - 1.2, 3.4, 2.8), ramp5((156, 154, 112)), edge=True)
        S.set(tx, syl - 2, GLOW_DIM, True); S.set(tx + 1, syl - 1, GLOW_DIM, True)
    elif look.mut == "sac":
        for sx_ in (hx - 4, hx + 4):
            S.paint(ellipse(sx_, sy - 1.5, 1.8, 2.2), ramp5((110, 176, 78)), edge=True)
            S.set(sx_, sy - 2, GLOW, True)
    S.outline()
    return S


# ------------------------------------------------------------------ side view (facing left)
def draw_side(look: Look, frame: int) -> Sprite:
    z = look.kind == "zombie"
    rng = random.Random(look.seed * 31 + 7)
    pose = Pose(look, frame)
    S = Sprite(salt=look.seed)
    skin, shirt, pants, shoe, hair = _ramps(look)
    dskin, dshirt, dpants = [mix(c, (20, 18, 40), .28) for c in skin], [mix(c, (20, 18, 40), .28) for c in shirt], [mix(c, (20, 18, 40), .28) for c in pants]
    wd, th = pose.wd, pose.thick
    cl = 16
    lean = (-1 - look.lean) if z else 0
    cx = 16 + (pose.sway // 2 if z else 0) + (lean + 1 if z else 0)
    sy = 14 + pose.bob + (1 if z else 0) + (look.lean // 2)
    wy = sy + 13
    half = 3.6 + wd * .55
    S.shadow = ellipse(cl, 44, 9 + wd * .5, 2.4)
    if look.prop == "iv":
        _iv(S, cl - 12, cl - 12, "side")

    def foot(side):
        p = pose.ph if side < 0 else pose.ph + math.pi
        amp = .8 if (look.limp and side > 0) else look.stride * 2.1
        return (cl - amp * math.sin(p) * pose.k, 43.2 - 1.5 * look.lift * max(0, math.cos(p)) * pose.k)
    legs = sorted([(foot(-1), -1), (foot(1), 1)], key=lambda lf: -lf[1])   # far leg (side 1) first
    for fpos, side in legs:
        far = side == 1
        pc, sc = (dpants if far else pants), (shoe if not far else [mix(c, (20, 18, 40), .3) for c in shoe])
        hip = (cl + .3, wy + 1)
        fwd = max(0, math.cos(pose.ph if side < 0 else pose.ph + math.pi)) * pose.k
        knee = ((hip[0] + fpos[0]) / 2 - 1.4 * fwd - .6, (hip[1] + fpos[1]) / 2 - .5)
        S.paint(capsule(hip, knee, (3.0 + wd * .25) * th), pc, grain=.14)
        S.paint(capsule(knee, fpos, (2.4 + wd * .2) * th), pc, grain=.14)
        S.paint(ellipse(fpos[0] - 1.5, fpos[1] + .6, 3.8 * th, 1.9), sc, edge=True)
        S.set(fpos[0] - 3, fpos[1] + 1, sc[3])
        for dx in range(-4, 2):
            S.set(fpos[0] + dx - 1, fpos[1] + 2, sc[0])
        if not far and z and fpos[1] < 43:
            S.set(int(knee[0]) - 1, int(knee[1]), skin[2]); S.set(int(knee[0]) - 1, int(knee[1]) + 1, BLOOD[1])
    S.paint(poly([(cl - 4.2, wy - 1), (cl + 4.2, wy - 1), (cl + 5, wy + 4), (cl - 5, wy + 4)]), pants, grain=.1)
    # far arm (behind the torso)
    def side_arm(near: bool):
        swing = pose.swing * (-1 if near else 1)
        sc_, kc_, pc_ = (shirt, skin, pants) if near else (dshirt, dskin, dpants)
        shoulder = (cx + 1 + lean * .5, sy + 2.4)
        reach = z and (look.pose in ("both", "scream") or (look.pose == "one" and near))
        if reach:
            elbow = (cx - 3.6 - (0 if near else 1), sy + 7 + (0 if near else -1))
            hand = (cx - 8 - (0 if near else 1), sy + 8.4 + (swing * .3))
        else:
            elbow = (cx + 1 + swing * .55, sy + 8.5)
            hand = (cx + 1 + swing * 1.5, sy + 15 - abs(swing) * .4)
        bare = z and look.torn_sleeve and not look.gown
        S.paint(capsule(shoulder, elbow, 2.4 * th), sc_, grain=.14, edge=near)
        S.paint(capsule(elbow, hand, 1.9 * th), kc_ if bare else sc_, edge=near)
        glove = not z
        hc = ramp5((44, 44, 50)) if glove else kc_
        S.paint(ellipse(hand[0], hand[1] + 1, 1.8 * th, 1.9 * th), hc, edge=near)
        fx, fy = int(hand[0]), int(hand[1]) + 3
        for dx in (-1, 0, 1):
            S.set(fx + dx - (1 if reach else 0), fy - (2 if reach else 0), hc[1])
        if (look.mut == "claw" and near) or look.pose == "run":
            for k in range(3):
                S.set(int(hand[0]) - 2 - k * 1, int(hand[1]) + 1 + (k // 2), CLAW)
            S.set(int(hand[0]) - 5, int(hand[1]) + 2, (214, 206, 190))
        if near and not z:
            S.paint(rect(hand[0] - 5, hand[1] - 1, hand[0] - 2, hand[1]), ramp5((72, 74, 80)), edge=True)
            S.set(hand[0] - 6, hand[1] - 1, (255, 240, 190), True)
            S.meta["flash"] = (int(hand[0]) - 6, int(hand[1]) - 1)
    side_arm(False)
    # backpack goes on the back (right side when facing left)
    if look.pack != "none" or look.kind == "human":
        if look.pack == "dynamite":
            S.paint(rect(cx + 2.5, sy + 2, cx + 7, sy + 11), ramp5((120, 100, 60)), grain=.2, edge=True)
            for y in (sy + 4, sy + 7):
                S.paint(rect(cx + 3, y, cx + 6, y + 1), ramp5((190, 40, 40)), edge=True)
        else:
            S.paint(rect(cx + 2.5, sy + 1.5, cx + 7.5, sy + 11), ramp5((70, 82, 58)), grain=.2, edge=True)
            S.paint(rect(cx + 4, sy + 7, cx + 7.5, sy + 10), ramp5((60, 72, 50)), edge=True)
    torso = poly([(cx - half + lean, sy + 1), (cx - 1.8 + lean, sy - 1), (cx + 2.2, sy - 1), (cx + half + .6, sy + 1.2), (cx + half + (1.2 if z else 0), sy + 4),
                  (cx + half, sy + 8), (cx + half - .4, wy), (cx - half + .6, wy), (cx - half + .2 + lean * .4, sy + 8)])
    if z and not look.gown:
        for _ in range(2):
            x = rng.randint(int(cx - half + 1), int(cx + half - 1))
            for y in range(wy - 1 - rng.randint(0, 2), wy):
                torso.discard((x, y))
    S.paint(torso, shirt, grain=.16)
    if look.pattern == "plaid":
        S.tint([p for p in torso if p[0] % 5 == 0], (255, 255, 255), .14)
        S.tint([p for p in torso if p[1] % 5 == 0], (0, 0, 0), .18)
    elif look.pattern == "stripes":
        S.tint([p for p in torso if p[1] % 3 == 0], (255, 255, 255), .16)
    if look.vest:
        S.tint(torso, (255, 140, 30), .6)
        for y in (sy + 5, wy - 3):
            for x in range(int(cx - half), int(cx + half) + 1):
                S.set(x, y, (230, 230, 196))
    for x in range(int(cx - half + 1), int(cx + half)):
        S.set(x, wy, mix((44, 34, 28), (18, 14, 14), .3 if x % 2 else 0))
    S.set(int(cx - half + 1), wy, (200, 180, 118))
    for y in range(sy + 4, sy + 10):
        if hnoise(cx, y, look.seed) < .6:
            S.set(cx - 1, y, shirt[1])
    if z:
        for _ in range(rng.randint(2, 3)):
            S.tint(ellipse(rng.randint(int(cx - 2), int(cx + 2)), rng.randint(sy + 3, wy - 2), rng.uniform(1, 2.4), rng.uniform(1, 2.2)), BLOOD[1], .7, .85)
        if look.mut == "bone":
            for i, y in enumerate((sy + 5, sy + 7, sy + 9)):
                for x in range(int(cx - 3), int(cx - 1 + i % 2)):
                    S.set(x, y, BONE if x % 2 else (168, 158, 134))
    if look.belly:
        _belly(S, cx, sy, shirt, skin, "side")
    if look.armor:
        _armor(S, look, pose, 0, 0, sy + 1, sy + 1, cx, sy, "side")
        S.paint(poly([(cx - 3.4, sy + 2), (cx + 3, sy + 2), (cx + 2.6, sy + 9), (cx - 3.2, sy + 9)]), ramp5(METAL), grain=.15, edge=True)
    # head in profile, facing left
    hx, hy = int(cx - 1 + lean + look.head_off * 0 - (1 if z else 0)), sy - 6 + (1 if z else 0)
    S.paint(rect(cx - 2, sy - 4, cx + 1, sy), skin)
    prof = [(hx - 3.6, hy - 1.5), (hx - 2.6, hy - 4.8), (hx + 1, hy - 5.4), (hx + 3.8, hy - 3.2), (hx + 4, hy + 1.5), (hx + 2.2, hy + 4.6),
            (hx - 1.2, hy + 5.2), (hx - 2.8, hy + 3.9), (hx - 3.6, hy + 2.4), (hx - 4.6, hy + .4), (hx - 3.9, hy - .8)]
    S.paint(poly(prof), skin, edge=True)
    S.set(hx + 1, hy, skin[1]); S.set(hx + 2, hy, skin[0]); S.set(hx + 1, hy - 1, skin[3])           # ear
    if z:
        for (x, y) in rect(hx - 3, hy - 2, hx - 1, hy - 1):
            S.set(x, y, (20, 16, 26) if y == hy - 2 else (34, 28, 40))
        S.set(hx - 3, hy - 1, look.glow_eyes, True); S.set(hx - 2, hy - 1, GLOW_DIM, True)
        S.set(hx - 3, hy + 1, skin[0]); S.set(hx - 2, hy + 2, skin[3])
        S.set(hx - 3, hy + 3, (30, 12, 18)); S.set(hx - 2, hy + 3, (30, 12, 18)); S.set(hx - 3, hy + 3, BONE)
        S.set(hx - 2, hy + 4, (86, 22, 28)); S.set(hx - 3, hy + 4, (86, 22, 28))
        S.set(hx - 4, hy, skin[4]); S.set(hx - 4, hy + 1, skin[1])
        for i in range(rng.randint(1, 3)):
            S.set(hx - 3, hy + 5 + i, BLOOD[i % 2])
        S.tint(ellipse(hx + 1, hy + 1, 2.2, 1.8), rng.choice(PATCHES), .5, .9)
        _veins(S, rng, hx + 2, hy - 1, -1, 6)
        for k in range(look.extra_veins):
            _veins(S, rng, hx + k, hy + 1, 1, 5)
            _veins(S, rng, int(cx - 1 + k * 2), sy + 2, 1, 6)
        if look.drool:
            for i in range(4):
                S.set(hx - 3, hy + 5 + i, GLOW_DIM, True)
        if look.hair_style != "none":
            style = poly([(hx - 2.6, hy - 4.2), (hx + 1, hy - 5.8), (hx + 4, hy - 3.4), (hx + 4.2, hy + 1.4), (hx + 1.6, hy + 1.4), (hx + 1, hy - 2.6), (hx - 1, hy - 2.8)])
            S.paint(style, hair, grain=.3, edge=True)
            if look.hair_style == "long":
                for y in range(hy + 1, hy + 6):
                    S.set(hx + 3, y, hair[1] if y % 2 else hair[0])
    else:
        S.set(hx - 2, hy - 1, (236, 232, 222)); S.set(hx - 3, hy - 1, (36, 30, 34))
        S.set(hx - 3, hy - 2, hair[1]); S.set(hx - 2, hy - 2, hair[1])
        S.set(hx - 4, hy, skin[4]); S.set(hx - 4, hy + 1, skin[1]); S.set(hx - 3, hy + 3, skin[0]); S.set(hx - 2, hy + 3, skin[0])
        S.paint(poly([(hx - 3, hy - 4.4), (hx + 1, hy - 6), (hx + 4, hy - 3.4), (hx + 4.4, hy + 1.8), (hx + 1.8, hy + 1.8), (hx + 1.4, hy - 2.4), (hx - 2.8, hy - 2.8)]),
                hair, grain=.25, edge=True)
    _hat(S, look, hx, hy, "side")
    side_arm(True)
    if look.mut == "tumor":
        S.paint(ellipse(cx + 3, sy + 1, 3.2, 2.8), ramp5((156, 154, 112)), edge=True)
        S.set(cx + 3, sy, GLOW_DIM, True); S.set(cx + 4, sy + 1, GLOW_DIM, True)
    elif look.mut == "sac":
        S.paint(ellipse(hx - 1, sy + .6, 2.6, 2.4), ramp5((110, 176, 78)), edge=True)
        S.set(hx - 1, sy, (214, 255, 130), True); S.set(hx - 2, sy + 1, GLOW, True)
    elif look.mut == "jaw" or look.big_jaw:
        S.paint(ellipse(hx - 2, hy + 6, 2.4 if not look.big_jaw else 3.2, 2.6), [(30, 12, 18), (60, 16, 24), (86, 22, 28), (120, 36, 44), (150, 60, 66)], edge=True)
        S.set(hx - 3, hy + 5, BONE); S.set(hx - 3, hy + 8, BONE)
    S.outline()
    return S


# ------------------------------------------------------------------ lying down (player_downed)
def draw_downed(look: Look) -> Sprite:
    S = Sprite(32, 32, salt=look.seed)
    skin, shirt, pants, shoe, hair = _ramps(look)
    S.shadow = ellipse(16, 24, 13, 3.2)
    blood = poly([(6, 21), (14, 17), (24, 18), (28, 23), (20, 26), (9, 26)])
    for (x, y) in blood:
        S.px[(x, y)] = BLOOD[(x + y) % 2]
    S.paint(capsule((20, 20), (28, 20.6), 2.4), pants, grain=.12)
    S.paint(ellipse(29, 21, 2.6, 1.9), shoe, edge=True)
    S.paint(capsule((11, 19), (21, 19.6), 4.2), shirt, grain=.16)
    S.paint(capsule((12, 15.8), (17, 19), 1.8), shirt, edge=True)
    S.paint(ellipse(8, 18.6, 4.4, 4.2), skin, edge=True)
    S.paint(poly([(4, 16.4), (6, 14), (11, 14.4), (11.6, 17.4), (8, 16.2)]), hair, grain=.25, edge=True)
    S.set(6, 19, (236, 232, 222)); S.set(5, 19, (36, 30, 34)); S.set(6, 21, skin[0]); S.set(5, 21, skin[0])
    S.set(15, 21, BLOOD[0]); S.set(16, 22, BLOOD[1])
    S.outline()
    return S


# ------------------------------------------------------------------ catalogue
DIRS = ("down", "up", "left")
DRAWERS = {"down": draw_front, "up": draw_back, "left": draw_side}


def zombie_looks() -> dict[str, Look]:
    """One look per bestiary id (docs/bestiario.md). Variants of the walker add crowd variety."""
    L: dict[str, Look] = {}
    L["walker"] = random_look(3, mut=None, pose="one", torn_sleeve=True)
    L["walker_v1"] = random_look(11, "heavy", mut="tumor")
    L["walker_v2"] = random_look(27, "thin", mut=None)
    L["walker_v3"] = random_look(41, "normal", mut="bone")
    # the crowd: recognisable jobs, so a horde reads as a city's people
    L["walker_v4"] = random_look(121, "normal", shirt=(36, 50, 88), pants=(28, 36, 62), hat="cap", badge=True, hair_style="scruff",
                                 pattern="plain", mut=None, pose="one", torn_sleeve=False, buttons=True)           # police
    L["walker_v5"] = random_look(131, "thin", shirt=(78, 160, 148), pants=(66, 140, 130), hair_style="long", pattern="plain", mut=None,
                                 pose="both", torn_sleeve=False, buttons=False)                                      # nurse
    L["walker_v6"] = random_look(141, "normal", shirt=(50, 52, 62), pants=(40, 42, 52), tie=(150, 40, 44), hair_style="scruff", hair=(30, 26, 26),
                                 pattern="plain", mut=None, pose="hang", torn_sleeve=False, buttons=True)           # office worker
    L["walker_v7"] = random_look(151, "normal", shirt=(96, 110, 98), pants=(60, 64, 76), hat="hood", pattern="plain", mut=None,
                                 pose="one", torn_sleeve=False)                                                      # hoodie
    L["walker_v8"] = random_look(161, "thin", shirt=(176, 156, 114), pants=(108, 98, 84), hair=(206, 206, 210), hair_style="scruff", pattern="plain",
                                 mut=None, pose="hang", torn_sleeve=False, buttons=True, limp=True, tilt=2)          # elderly
    L["walker_v9"] = random_look(171, "heavy", shirt=(52, 78, 124), pants=(52, 78, 124), hat="cap", hat_color=(170, 50, 46), pattern="plain",
                                 mut="claw", pose="both", torn_sleeve=True, hair_style="none")                       # mechanic
    L["runner"] = random_look(52, "thin", skin=(150, 162, 98), shirt=(116, 120, 124), pants=(74, 76, 84), hair_style="none", pose="run",
                              mut="claw", torn_sleeve=True, stride=3.2, arm_swing=3.0, pattern="plain", limp=False, tilt=1, lean=2)
    L["screamer"] = random_look(61, "thin", skin=(150, 128, 164), shirt=(64, 52, 76), pants=(52, 56, 48), hair=(30, 26, 26), hair_style="long",
                                pose="scream", mut=None, big_jaw=True, pattern="plain", limp=False, tilt=0, head_off=0)
    L["spitter"] = random_look(73, "heavy", skin=(122, 164, 78), shirt=(150, 128, 66), pants=(96, 78, 62), hair_style="scruff", mut="sac",
                               drool=True, pose="hang", pattern="plain", limp=False, belly=True)
    L["armored"] = random_look(84, "heavy", skin=(116, 132, 112), shirt=(74, 76, 84), pants=(54, 58, 50), hair_style="none", mut=None,
                               armor=True, hat="helmet", pose="hang", pattern="plain", limp=False, glow_eyes=GLOW)
    L["sapper"] = random_look(95, "normal", skin=(138, 142, 112), shirt=(226, 130, 40), pants=(74, 76, 84), hair_style="none", mut=None,
                              hat="hardhat", pack="dynamite", vest=True, pose="hang", pattern="plain", limp=False)
    L["brute"] = random_look(106, "brute", skin=(140, 104, 98), shirt=(80, 60, 56), pants=(60, 54, 50), hair_style="none", mut="bone",
                             pose="both", torn_sleeve=True, pattern="plain", limp=False, stride=1.6, arm_swing=1.0, tilt=2)
    L["patient_zero"] = random_look(117, "thin", skin=(196, 206, 200), shirt=(204, 216, 216), pants=(190, 200, 204), hair_style="none", mut=None,
                                    gown=True, extra_veins=2, pose="one", pattern="plain", limp=False, torn_sleeve=False,
                                    glow_eyes=(230, 255, 190), prop="iv")
    return L


def player_looks() -> dict[str, Look]:
    return {
        "player": random_look(5, kind="human", build="normal", shirt=(58, 98, 138), **WALK),
        "player_other": random_look(9, kind="human", build="normal", shirt=(196, 112, 52), pants=(70, 62, 58), hair=(92, 72, 48), **WALK),
    }


# A person walks with a clear step and swinging arms; running is longer, higher and bouncier (the game picks it by speed).
WALK = dict(stride=3.0, arm_swing=2.6, lift=2.2)
RUN = dict(stride=3.9, arm_swing=3.9, lift=3.8, bob=2)


class _Img:
    """A finished image standing in for a Sprite (the atlas only calls .image())."""

    def __init__(self, img):
        self._img = img

    def image(self):
        return self._img


def _column(src, x: float, y: int):
    """Pixel of `src` at a fractional column (nearest), transparent outside."""
    xi = int(x)
    if 0 <= xi < src.width and 0 <= y < src.height:
        return src.getpixel((xi, y))
    return (0, 0, 0, 0)


LEGS_Y = 31                        # first row below the hips


def three_quarter(front, side, back, view: str):
    """The diagonal views of D-025 (8 directions), made from the three drawn ones: facing down-left the leading half of the body is the
    left profile and the trailing half the front, narrowed; facing up-left it is the back, narrowed and turned a pixel toward the left. The right-hand views are these mirrored by the game."""
    from PIL import Image
    w, h = front.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = out.load()
    for y in range(h):
        for x in range(w):
            if y >= LEGS_Y:
                c = _column(side, x, y)                    # the legs stride in profile, so the step reads in the diagonals too
            elif view == "dl":
                c = _column(side, x, y) if x < 16 else _column(front, 16 + (x - 16) * 1.3, y)
                if x == 16 and c[3] == 0:
                    c = _column(front, 16, y)
            else:                                                                        # "ul"
                c = _column(back, 16 + (x + 1 - 16) * 1.18, y)
            px[x, y] = c
    return out


def build(zombie_ids: list[str]) -> dict[str, Sprite]:
    looks = zombie_looks()
    missing = [z for z in zombie_ids if z not in looks]
    if missing:
        raise ValueError(f"spritegen: zombies without a look: {missing}")
    out: dict[str, Sprite] = {}

    def add(prefix: str, look: Look) -> None:
        for d in DIRS:
            for f in range(NFRAMES):
                out[f"{prefix}_{d}_{f}"] = DRAWERS[d](look, f)
            out[f"{prefix}_{d}_idle"] = DRAWERS[d](look, -1)
        for f in list(range(NFRAMES)) + ["idle"]:
            imgs = {d: out[f"{prefix}_{d}_{f}"].image() for d in DIRS}
            out[f"{prefix}_dl_{f}"] = _Img(three_quarter(imgs["down"], imgs["left"], imgs["up"], "dl"))
            out[f"{prefix}_ul_{f}"] = _Img(three_quarter(imgs["down"], imgs["left"], imgs["up"], "ul"))

    for name, look in looks.items():
        if name == "brute":
            continue                       # La Mole is drawn natively at 64x96 in brute.py
        add(f"zombie_{name}", look)
    for name, look in player_looks().items():
        add(name, look)
        for k, v in RUN.items():
            setattr(look, k, v)
        add(name + "_run", look)
    out["player_downed"] = draw_downed(player_looks()["player"])
    return out
