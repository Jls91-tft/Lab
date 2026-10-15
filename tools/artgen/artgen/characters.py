"""Players and zombies in the style of Kenney's Roguelike Characters (D-019): square head,
broad short body, dot eyes, no black line but darker edges. Kenney's sheet only faces
front; the other facings and the walk are drawn here.

3 facings (down, up, left; right is left mirrored at runtime) x 4 walk frames
(0 and 2 stand, 1 and 3 step). Names: <who>_<facing>_<frame>, e.g. player_down_1,
zombie_walker_left_3, plus player_other_*, player_downed and zombie_<id>.
"""

from __future__ import annotations

from dataclasses import dataclass, replace

from .canvas import Canvas
from .palette import RGB

FACINGS = ("down", "up", "left")
FRAMES = 4

# Kenney's skins (roguelikeChar rows 0-3) and clothes.
SKIN_LIGHT = ((229, 196, 157), (170, 143, 111))
SKIN_MID = ((200, 164, 128), (156, 118, 80))
ORC_GREEN = ((64, 156, 98), (48, 126, 77))
ORANGE = ((198, 101, 39), (163, 84, 34))
BROWN = ((161, 125, 82), (114, 88, 57))
LIME = ((149, 206, 62), (100, 140, 50))
GREY = ((79, 79, 79), (58, 58, 60))
BLUE = ((70, 112, 178), (48, 82, 138))
TEAL = ((52, 140, 140), (36, 106, 106))
PURPLE = ((128, 88, 160), (96, 62, 124))
STEEL = ((157, 170, 171), (120, 122, 126))
PALE = ((214, 210, 196), (170, 166, 152))
DEAD_GREEN = ((112, 150, 108), (82, 112, 80))
DARK_RED = ((140, 40, 38), (104, 28, 28))
SHOES = (54, 44, 38)
EYES = (48, 36, 30)
RED_EYES = (212, 48, 40)
BLOOD = (128, 28, 30)
Pair = tuple[RGB, RGB]


@dataclass(frozen=True)
class Look:
    skin: Pair = SKIN_LIGHT
    shirt: Pair = BLUE
    pants: Pair = GREY
    hair: Pair | None = BROWN
    eyes: RGB = EYES
    zombie: bool = False
    helmet: Pair | None = None
    vest: Pair | None = None
    mouth: RGB | None = None
    wide: bool = False
    blood: bool = False


PLAYER = Look()
PLAYER_OTHER = Look(shirt=ORANGE, hair=((226, 212, 168), (191, 177, 131)))
PLAYER_DOWNED = Look(shirt=GREY, pants=GREY)

_ZOMBIE = Look(skin=ORC_GREEN, shirt=BROWN, pants=((60, 66, 96), (44, 48, 72)), hair=((40, 70, 46), (30, 52, 34)),
               eyes=RED_EYES, zombie=True, blood=True)

ZOMBIES: dict[str, Look] = {
    "walker": _ZOMBIE,
    "runner": replace(_ZOMBIE, skin=DEAD_GREEN, shirt=DARK_RED, hair=((34, 30, 30), (24, 22, 22)), eyes=(236, 200, 72)),
    "screamer": replace(_ZOMBIE, skin=PALE, shirt=PURPLE, hair=((40, 34, 44), (28, 24, 32)),
                        eyes=(186, 130, 220), mouth=(40, 20, 24)),
    "spitter": replace(_ZOMBIE, skin=LIME, shirt=((60, 110, 50), (44, 84, 38)), hair=None, eyes=(220, 240, 120),
                       mouth=(160, 220, 80), wide=True),
    "armored": replace(_ZOMBIE, shirt=GREY, vest=STEEL, hair=None, helmet=STEEL),
    "sapper": replace(_ZOMBIE, shirt=ORANGE, vest=((236, 200, 72), (196, 160, 52)), hair=None,
                      helmet=((236, 200, 72), (196, 160, 52))),
    "brute": replace(_ZOMBIE, skin=((48, 126, 77), (34, 96, 58)), shirt=DARK_RED, hair=None, eyes=(236, 200, 72), wide=True),
    "patient_zero": replace(_ZOMBIE, skin=((236, 236, 228), (196, 196, 186)), shirt=((206, 222, 226), (160, 178, 184)),
                            pants=((206, 222, 226), (160, 178, 184)), hair=None, eyes=(120, 210, 220)),
}

# Drawn bigger than one tile (bestiary size_tiles 2.0 for La Mole).
BIG = {"brute": 2}


def _c(rgb: RGB) -> tuple[int, int, int, int]:
    return (rgb[0], rgb[1], rgb[2], 255)


def _part(c: Canvas, x0: int, y0: int, x1: int, y1: int, pair: Pair) -> None:
    """A block in its colour with Kenney's shading: darker right column and bottom row."""
    c.rect(x0, y0, x1, y1, _c(pair[0]))
    c.rect(x1, y0, x1, y1, _c(pair[1]))
    c.rect(x0, y1, x1, y1, _c(pair[1]))


def humanoid(look: Look, facing: str, frame: int) -> Canvas:
    c = Canvas(16)
    step = {1: 1, 3: -1}.get(frame, 0)  # +1: left leg forward, -1: right leg forward
    bob = 1 if step else 0  # the body rises a pixel on each step
    hunch = 1 if look.zombie else 0  # zombies carry the head low
    l, r = (1, 14) if look.wide else (2, 13)

    c.shadow(3, 13, 12, 15)

    # legs and shoes
    if facing == "left":
        for dx in (-step, step):
            c.rect(6 + dx, 13 - bob, 8 + dx, 14 - bob, _c(look.pants[0]))
            c.rect(6 + dx, 15 - bob, 8 + dx, 15 - bob, _c(SHOES))
    else:
        for x, lift in ((5, max(step, 0)), (8, max(-step, 0))):
            c.rect(x, 13 - lift, x + 2, 14 - lift, _c(look.pants[0]))
            c.rect(x + 2, 13 - lift, x + 2, 14 - lift, _c(look.pants[1]))
            c.rect(x, 15 - lift, x + 2, 15 - lift, _c(SHOES))

    # torso
    tl, tr = (l + 2, r - 2) if facing == "left" else (l + 1, r - 1)
    _part(c, tl, 8 - bob, tr, 13 - bob, look.shirt)
    if look.vest:
        c.rect(tl + 1, 9 - bob, tr - 1, 11 - bob, _c(look.vest[0]))
        c.rect(tl + 1, 11 - bob, tr - 1, 11 - bob, _c(look.vest[1]))
    if look.blood:
        c.px(tl + 2, 10 - bob, _c(BLOOD))
        c.px(tr - 1, 12 - bob, _c(BLOOD))
        c.px(tr - 2, 9 - bob, _c(BLOOD))

    # arms: the living swing them, zombies reach forward
    skin, skin_d = _c(look.skin[0]), _c(look.skin[1])
    if look.zombie:
        if facing == "down":
            c.rect(l, 9 - bob, l, 13 - bob, _c(look.shirt[1]))
            c.rect(r, 9 - bob, r, 13 - bob, _c(look.shirt[1]))
            c.px(l, 14 - bob, skin)
            c.px(r, 14 - bob, skin_d)
        elif facing == "up":
            c.rect(l, 4, l, 9 - bob, _c(look.shirt[1]))
            c.rect(r, 4, r, 9 - bob, _c(look.shirt[1]))
            c.px(l, 3, skin)
            c.px(r, 3, skin_d)
        else:
            c.rect(1, 9 - bob, 6, 10 - bob, _c(look.shirt[1]))
            c.rect(0, 9 - bob, 0, 10 - bob, skin)
    else:
        if facing == "left":
            c.rect(7 + step, 9 - bob, 8 + step, 11 - bob, _c(look.shirt[1]))
            c.rect(7 + step, 12 - bob, 8 + step, 12 - bob, skin)
        else:
            c.rect(l, 9 - bob - step, l, 11 - bob - step, _c(look.shirt[0]))
            c.px(l, 12 - bob - step, skin)
            c.rect(r, 9 - bob + step, r, 11 - bob + step, _c(look.shirt[1]))
            c.px(r, 12 - bob + step, skin_d)

    # head (8 wide, square, Kenney proportions)
    top = 1 + hunch - bob
    if facing == "left":
        hx0, hx1 = 4, 10
    else:
        hx0, hx1 = 4, 11
    _part(c, hx0, top, hx1, top + 7, look.skin)
    if look.hair:
        hair, hair_d = _c(look.hair[0]), _c(look.hair[1])
        if facing == "up":
            c.rect(hx0, top, hx1, top + 6, hair)
            c.rect(hx1, top, hx1, top + 6, hair_d)
        elif facing == "left":
            c.rect(hx0, top, hx1, top + 1, hair)
            c.rect(hx1 - 2, top, hx1, top + 4, hair)
            c.rect(hx1, top, hx1, top + 4, hair_d)
        else:
            c.rect(hx0, top, hx1, top + 1, hair)
            c.px(hx0, top + 2, hair)
            c.px(hx1, top + 2, hair_d)
    if look.helmet:
        c.rect(hx0, top, hx1, top + 2, _c(look.helmet[0]))
        c.rect(hx0, top + 2, hx1, top + 2, _c(look.helmet[1]))
    if facing == "down":
        c.px(hx0 + 2, top + 4, _c(look.eyes))
        c.px(hx1 - 2, top + 4, _c(look.eyes))
        if look.mouth:
            c.rect(hx0 + 3, top + 5, hx1 - 3, top + 6, _c(look.mouth))
        elif look.zombie:
            c.rect(hx0 + 3, top + 6, hx1 - 3, top + 6, skin_d)
    elif facing == "left":
        c.px(hx0 + 1, top + 4, _c(look.eyes))
        if look.mouth:
            c.px(hx0, top + 5, _c(look.mouth))

    _darken_edges(c)
    return c


def _darken_edges(c: Canvas, factor: float = 0.72) -> None:
    """Kenney's line: no outline, the silhouette's own pixels a shade darker."""
    pixels = c.image.load()
    solid = {(x, y) for x in range(c.width) for y in range(c.height) if pixels[x, y][3] == 255}
    for x, y in solid:
        if any((x + dx, y + dy) not in solid for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            r, g, b, a = pixels[x, y]
            pixels[x, y] = (int(r * factor), int(g * factor), int(b * factor), a)


def build(bestiary_ids: list[str]) -> dict[str, Canvas]:
    out: dict[str, Canvas] = {}
    for facing in FACINGS:
        for frame in range(FRAMES):
            out[f"player_{facing}_{frame}"] = humanoid(PLAYER, facing, frame)
            out[f"player_other_{facing}_{frame}"] = humanoid(PLAYER_OTHER, facing, frame)
    out["player_downed"] = humanoid(PLAYER_DOWNED, "left", 0).rotated(1)

    for zid in bestiary_ids:
        look = ZOMBIES.get(zid)
        if look is None:
            raise KeyError(f"artgen: no look for zombie '{zid}' (add it to characters.ZOMBIES)")
        for facing in FACINGS:
            for frame in range(FRAMES):
                c = humanoid(look, facing, frame)
                if zid in BIG:
                    c = c.scaled(BIG[zid])
                out[f"zombie_{zid}_{facing}_{frame}"] = c
    return out
