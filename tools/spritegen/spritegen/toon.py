"""Stylized buildings (D-035): flat, bright plaster facades in a few strong colours, chunky windows with shutters, balconies upstairs
and clean roof tiles that take one colour per building in the game.

The realistic brick and plaster of structures.py read as noise once a whole street is made of them (D-024 looked busy). Here every
facade is one colour with cel shading: lit south face, cooler shaded west face, a dark plinth at the foot and a light cornice at the top.
Detail comes only from the pieces that carry meaning (windows, doors, balconies), so a building reads at a glance.

Names follow iso.py: `iso_wall_fac_<colour>` (+ `_low`, `_<door|dooropen|window>_<s|w>`) for the ground storey and
`iso_wall_fac_<colour>_up` (+ `_<window|balcony>_<s|w>`) for the storeys above, which the game lifts onto the one below.
"""

from __future__ import annotations

from PIL import Image

from .iso import D, DOOR_H, LOW_H, W, WALL_H, WINDOW_HIGH, WINDOW_LOW, _hash, _mix, _shade

# Bright, saturated but friendly plaster: the city as a storybook, not a photo.
FACADES: dict[str, tuple[tuple, tuple]] = {          # colour: (plaster, shutters)
    "ochre": ((226, 172, 82), (58, 112, 86)),
    "terracotta": ((206, 112, 78), (232, 214, 170)),
    "mint": ((132, 200, 168), (196, 92, 74)),
    "sky": ((124, 172, 216), (240, 226, 186)),
    "coral": ((230, 134, 122), (70, 98, 150)),
    "cream": ((238, 220, 178), (60, 118, 104)),
    "lavender": ((170, 150, 210), (242, 220, 150)),
    "sage": ((156, 182, 122), (130, 72, 52)),
    "white": ((236, 234, 226), (70, 120, 170)),
    "mustard": ((222, 190, 72), (110, 70, 110)),
    "slate": ((118, 132, 156), (226, 190, 96)),
    "sand": ((214, 196, 156), (150, 60, 56)),
}

PLINTH = 7                 # px of dark stone at the foot of the ground storey
CORNICE = 4                # px of light cornice at the top of a storey
UP_CORNICE = 7             # the top storey's cornice is deeper
COURSE = 3                 # the light band at the foot of an upper storey (the floor line)
GLASS_LO, GLASS_HI = (36, 50, 76), (86, 118, 156)
FRAME = (244, 238, 222)
IRON = (40, 40, 50)


def _plaster(c: tuple, tx: int, h: int, height: int, ground: bool) -> tuple:
    """The bare facade: one colour, a touch lighter upwards, a plinth or a floor line at the foot and a cornice on top."""
    if ground and h < PLINTH:
        return _shade(c, -.42 if h < PLINTH - 1 else -.55)
    if not ground and h < COURSE:
        return _shade(c, .32 if h < COURSE - 1 else -.30)
    top = height - (CORNICE if ground else UP_CORNICE)
    if h >= top:
        return _shade(c, .55 if h == height - 1 else .30)
    if h == top - 1:
        return _shade(c, -.38)                             # the shadow under the cornice
    return _shade(c, -.03 if h < height * .4 else 0.0 if h < height * .75 else .03)     # three flat bands, lighter upwards


def _window(c: tuple, shutter: tuple, tx: int, h: int, low: int, high: int) -> tuple | None:
    """A tall window with a white frame, a cross of glazing bars, a sill and two slatted shutters folded open."""
    if not low - 3 <= h < high:
        return None
    if h < low:                                            # the sill
        if 6 <= tx <= 25:
            return _shade(FRAME, -.35) if h == low - 3 else FRAME
        return None
    if 3 <= tx <= 7 or 24 <= tx <= 28:                     # shutters
        k = -.30 if (high - h) % 3 == 0 else (.08 if (high - h) % 3 == 1 else 0)
        if tx in (3, 28) or h in (low, high - 1):
            k = -.45
        return _shade(shutter, k)
    if not 8 <= tx <= 23:
        return None
    if tx in (8, 9, 22, 23) or h in (low, low + 1, high - 2, high - 1) or tx in (15, 16) or h in ((low + high) // 2, (low + high) // 2 + 1):
        return _shade(FRAME, -.12) if tx in (8, 22) or h == low else FRAME
    t = (h - low) / (high - low)
    glass = _mix(GLASS_LO, GLASS_HI, t) + (255,)
    if 0 <= (tx - 10) - (h - low) * .45 <= 2:
        glass = _mix(glass, (200, 226, 240), .55) + (255,)  # a streak of sky
    return glass


def _door(c: tuple, shutter: tuple, tx: int, h: int, open_: bool) -> tuple | None:
    """A big wooden door under a light lintel, two panels and a brass knob; open, a dark hall behind it."""
    if not 6 <= tx <= 25 or h >= DOOR_H + 3:
        return None
    if h >= DOOR_H:
        return _shade(FRAME, -.2) if h == DOOR_H else FRAME   # lintel
    if tx in (6, 7, 24, 25):
        return _shade(FRAME, -.18)
    if open_:
        return _mix((22, 18, 26), (46, 38, 44), h / DOOR_H) + (255,)
    wood = _mix(shutter, (110, 70, 44), .55)
    k = 0.0
    if tx in (8, 23) or h in (2, DOOR_H - 2) or tx in (15, 16):
        k = -.35
    elif h in (DOOR_H // 2, DOOR_H // 2 + 1) or 10 <= tx <= 13 and 30 <= h <= 50 or 18 <= tx <= 21 and 30 <= h <= 50:
        k = -.15
    if tx == 20 and 26 <= h <= 27:
        return (232, 196, 90, 255)                           # knob
    return _shade(wood + (255,), k)


def _balcony(c: tuple, shutter: tuple, tx: int, h: int) -> tuple | None:
    """A French window down to the floor behind a little iron balcony."""
    if 2 <= tx <= 29 and 2 <= h <= 4:
        return _shade(FRAME, -.15) if h == 2 else FRAME     # the slab
    if 2 <= tx <= 29 and 5 <= h <= 22:
        if h in (21, 22) or tx in (2, 29) or tx % 3 == 0:
            return IRON + (255,)
    return _window(c, shutter, tx, h, 8, WINDOW_HIGH)


def toon_block(colour: str, height: int, piece: str | None = None, side: str = "s", ground: bool = True) -> Image.Image:
    """A storey of facade: the cap on top, the west face (shaded, a little cooler) and the south face (lit), with an optional piece on
    one face. Same geometry as iso.block, so it lines up with everything else."""
    c, shutter = FACADES[colour]
    c, shutter = c + (255,), shutter + (255,)
    img = Image.new("RGBA", (W, D + height), (0, 0, 0, 0))
    px = img.load()
    base = D + height
    for X in range(W):
        u = (X + .5) / (W / 2)
        west = X < W // 2
        if west:
            gline = base - D / 2 + u * D / 2
        else:
            u -= 1
            gline = base - u * D / 2
        tx = int(u * 32) % 32
        here = piece if (side == "w") == west else None
        for Y in range(img.height):
            h = int(gline - (Y + .5))
            if not 0 <= h < height:
                continue
            p = None
            if here == "window":
                p = _window(c, shutter, tx, h, WINDOW_LOW, WINDOW_HIGH)
            elif here in ("door", "dooropen"):
                p = _door(c, shutter, tx, h, here == "dooropen")
            elif here == "balcony":
                p = _balcony(c, shutter, tx, h)
            col = p or _plaster(c, tx, h, height, ground)
            if west:
                col = _mix(_shade(col, -.30), (54, 62, 112), .10) + (255,)
            px[X, Y] = col[:3] + (255,)
    cap = _shade(c, .42)
    for X in range(W):
        for Y in range(D):
            fx = (X + .5 - W / 2) / (W / 2)
            fy = (D / 2 - (Y + .5)) / (D / 2)
            if abs(fx) + abs(fy) <= 1:
                px[X, Y] = cap if abs(fx) + abs(fy) < .9 or fy > 0 else _shade(c, .15)
    return img


def roof_piece(kind: str) -> Image.Image:
    """A clean, light, colourless roof texture: the game tints it once per building (PrototypeWorldView.RoofTints)."""
    img = Image.new("RGBA", (32, 32), (0, 0, 0, 255))
    px = img.load()
    for y in range(32):
        for x in range(32):
            if kind == "tile":                               # big curved tiles in staggered rows
                row, yy = y // 8, y % 8
                joint = (x + (row % 2) * 6) % 12 == 0
                v = 246 if yy == 0 else 150 if yy == 7 or joint else 204 if yy >= 5 else 228
            else:                                             # a flat roof: big pale slabs
                v = 170 if x % 16 == 0 or y % 16 == 0 else 226 + int((_hash(x // 16, y // 16, 5) - .5) * 14)
            px[x, y] = (v, int(v * .97), int(v * .93), 255)
    return img


TOON_ROOFS = ("tile", "flat")


def is_toon(name: str) -> bool:
    """Sprites drawn here, which keep their own colours instead of the closed palette of D-021."""
    return name.startswith("iso_wall_fac_") or any(name.startswith(f"iso_roof_{k}_") for k in TOON_ROOFS)


def build() -> dict[str, Image.Image]:
    out: dict[str, Image.Image] = {}
    for colour in FACADES:
        m = "fac_" + colour
        out[f"iso_wall_{m}"] = toon_block(colour, WALL_H)
        out[f"iso_wall_{m}_low"] = toon_block(colour, LOW_H)
        out[f"iso_wall_{m}_up"] = toon_block(colour, WALL_H, ground=False)
        for side in ("s", "w"):
            for piece in ("door", "dooropen", "window"):
                out[f"iso_wall_{m}_{piece}_{side}"] = toon_block(colour, WALL_H, piece, side)
            for piece in ("window", "balcony"):
                out[f"iso_wall_{m}_up_{piece}_{side}"] = toon_block(colour, WALL_H, piece, side, ground=False)
    return out
