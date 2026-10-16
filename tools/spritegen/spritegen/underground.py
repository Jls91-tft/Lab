"""The underground (D-022): earth and rock you dig out, cave floors, pits, stairs down, ladders, hatches, beams, torches.

Earth and rock reuse the wall machinery of `structures` (autotiling: exposed sides show the cut face), so a dug room
looks right whatever shape it is. Below ground everything is solid by default; the game only stores what is excavated.
"""

from __future__ import annotations

import math
import random

from PIL import Image

from .gfx import Color, Sprite, ellipse, hnoise, mix, poly, ramp5, rect
from . import structures as st

S = 32


def _shade(c: Color, k: float) -> Color:
    return st._shade(c, k)


def pit(variant: int) -> Image.Image:
    """An open hole in the ground: you can fall (or climb) down through it."""
    t = st.Tile((0, 0, 0))
    for y in range(S):
        for x in range(S):
            d = math.hypot((x - 15.5) / 14.5, (y - 15.5) / 13.5 + variant * .05)
            if d < 1:
                depth = d
                t.put(x, y, mix((10, 8, 12), (42, 30, 26), depth ** 2))
            elif d < 1.22:
                t.put(x, y, mix(st.face("earth", x, y), (255, 244, 220), .22 if (x + y) % 3 else 0) if y < 16 else mix(st.face("earth", x, y), (14, 12, 20), .3))
            else:
                t.put(x, y, (0, 0, 0))
    img = t.image()
    for y in range(S):
        for x in range(S):
            if math.hypot((x - 15.5) / 14.5, (y - 15.5) / 13.5) >= 1.22:
                img.putpixel((x, y), (0, 0, 0, 0))
    return img


def stairs_down(m: str, d: str) -> Image.Image:
    base = st.stairs(m, d)
    sp = Sprite(S, S, salt=650)
    px = base.load()
    for y in range(S):
        for x in range(S):
            r, g, b, a = px[x, y]
            if a:
                if d == "n":
                    k = (y / 31)
                elif d == "s":
                    k = 1 - y / 31
                elif d == "e":
                    k = 1 - x / 31
                else:
                    k = x / 31
                sp.px[(x, y)] = mix((r, g, b), (10, 8, 14), min(.8, .7 * (1 - k)))
    sp.outline()
    return sp.image()


def ladder(m: str) -> Image.Image:
    sp = Sprite(S, S, salt=660)
    rail = ramp5((126, 90, 58) if m == "wood" else (150, 156, 164))
    rung = ramp5((150, 108, 70) if m == "wood" else (176, 182, 190))
    for x0 in (9, 21):
        sp.paint(rect(x0, 1, x0 + 1, 30), rail, edge=True, reach=2)
    for y in range(4, 30, 5):
        sp.paint(rect(10, y, 20, y + 1), rung, edge=True, reach=2)
    sp.shadow = {(x, 30) for x in range(7, 25)}
    sp.outline()
    return sp.image()


def hatch(m: str, open_: bool) -> Image.Image:
    sp = Sprite(S, S, salt=670)
    wood = m == "wood"
    frame = ramp5((88, 62, 42) if wood else (86, 92, 102))
    sp.paint(rect(3, 3, 28, 28), frame, grain=.2, edge=True, reach=4)
    if not open_:
        leaf = ramp5((136, 96, 60) if wood else (150, 160, 172))
        for k, y0 in enumerate(range(5, 27, 6)):
            sp.paint(rect(5, y0, 26, y0 + 4), leaf, grain=.25, edge=True, reach=2)
        sp.paint(ellipse(16, 16, 2.6, 2.6), ramp5((214, 190, 110)), edge=True, reach=2)
        sp.set(16, 16, (30, 24, 20))
    else:
        for (x, y) in rect(6, 6, 25, 25):
            sp.px[(x, y)] = mix((14, 10, 16), (50, 38, 30), (y - 6) / 20)
        for x0 in (11, 19):
            sp.paint(rect(x0, 6, x0 + 1, 25), ramp5((126, 90, 58) if wood else (150, 156, 164)), edge=False, reach=2)
        for y in range(8, 25, 4):
            sp.paint(rect(12, y, 19, y + 1), ramp5((150, 108, 70) if wood else (176, 182, 190)), edge=False, reach=2)
        sp.paint(poly([(5, 5), (26, 3), (26, 8), (5, 9)]), ramp5((136, 96, 60) if wood else (150, 160, 172)), grain=.2, edge=True, reach=3)
    sp.outline()
    return sp.image()


def beam(kind: str) -> Image.Image:
    sp = Sprite(S, S, salt=680)
    wood = ramp5((120, 84, 54))
    if kind == "h":
        sp.shadow = {(x, 19) for x in range(0, 32)}
        sp.paint(rect(0, 12, 31, 17), wood, grain=.25, edge=True, reach=3)
        for x in (4, 27):
            sp.set(x, 14, (190, 196, 204)); sp.set(x, 15, (60, 60, 68))
    elif kind == "v":
        sp.shadow = {(y % 3 + 18, y) for y in range(0, 32)}
        sp.paint(rect(13, 0, 18, 31), wood, grain=.25, edge=True, reach=3)
        for y in (4, 27):
            sp.set(15, y, (190, 196, 204)); sp.set(16, y, (60, 60, 68))
    else:                                                                  # prop: a post with a cap plate
        sp.shadow = ellipse(16, 27, 7, 2)
        sp.paint(rect(13, 4, 18, 27), wood, grain=.25, edge=True, reach=3)
        sp.paint(rect(8, 2, 23, 5), ramp5((96, 66, 42)), grain=.2, edge=True, reach=2)
        sp.paint(rect(9, 27, 22, 29), ramp5((84, 60, 40)), grain=.2, edge=True, reach=2)
    sp.outline()
    return sp.image()


def dig_overlay(level: int) -> Image.Image:
    sp = Sprite(S, S, salt=690 + level)
    rng = random.Random(690 + level)
    for _ in range(2 + level * 2):
        x, y = rng.randrange(3, 29), rng.randrange(3, 12)
        for _ in range(8 + level * 5):
            sp.set(x, y, (34, 24, 18))
            if rng.random() < .5:
                sp.set(x + 1, y - 1, (170, 136, 98))
            x = max(0, min(S - 1, x + rng.choice((-1, 0, 1))))
            y = min(S - 1, y + rng.choice((0, 1, 1)))
    for _ in range(level * 12):                                           # crumbs and dust
        sp.px[(rng.randrange(S), rng.randrange(S))] = rng.choice(((150, 118, 82), (110, 80, 54), (200, 176, 140)))
    return sp.image()


def torch(frame: int) -> Sprite:
    sp = Sprite(S, S, salt=700 + frame)
    sp.paint(rect(15, 14, 16, 29), ramp5((110, 76, 48)), grain=.2, edge=True, reach=2)
    sp.paint(rect(13, 11, 18, 14), ramp5((70, 70, 78)), edge=True, reach=2)
    flames = [([(13, 11), (12, 6 - frame % 2), (15, 1 + frame % 3), (17, 7), (18, 11)], (255, 126, 30)),
              ([(14, 11), (14, 6), (16, 3 - frame % 2), (17, 8), (17, 11)], (255, 190, 70)),
              ([(15, 11), (15, 7), (16, 5), (16, 8), (16, 11)], (255, 236, 150))]
    for pts, col in flames:
        for (x, y) in poly(pts):
            sp.set(x, y, col, True)
    for i in range(3):
        sp.set(11 + (frame * 4 + i * 5) % 10, 1 + (i * 2 + frame) % 5, (255, 200, 90), True)
    sp.meta["light"] = (16, 6)
    sp.outline()
    return sp


def build() -> dict[str, Image.Image]:
    out: dict[str, Image.Image] = {}
    for m in st.UNDERGROUND:
        for mask in range(16):
            out[f"wall_{m}_top_{mask}"] = st.wall_top(m, mask)
        for mask in range(4):
            out[f"wall_{m}_face_{mask}"] = st.wall_face(m, mask).image()
        for v in range(2):
            out[f"floor_{m}_{v}"] = st.floor(m, v)
    for v in range(2):
        out[f"pit_{v}"] = pit(v)
        out[f"floor_cave_{v}"] = st.floor("earth", v)               # the bare floor of a dug-out cell (the "cave" block)
    for m in ("wood", "stone", "concrete", "steel"):
        for d in "nesw":
            out[f"stairs_down_{m}_{d}"] = stairs_down(m, d)
    for m in ("wood", "steel"):
        out[f"ladder_{m}"] = ladder(m)
        out[f"hatch_{m}_closed"], out[f"hatch_{m}_open"] = hatch(m, False), hatch(m, True)
    for kind in ("h", "v", "prop"):
        out[f"beam_{kind}"] = beam(kind)
    for level in (1, 2, 3):
        out[f"overlay_dig_{level}"] = dig_overlay(level)
    for f in range(4):
        out[f"obj_torch_{f}"] = torch(f).image()
    return out
