"""Isometric pieces for D-025 (Project Zomboid look): walls as tall blocks seen from the south-west.

The world keeps its square grid; the game projects it 2:1 (a cell is a 64x32 diamond). A wall cell is drawn as a block
`WALL_H` px tall with its top cap, its west face (left, in shade) and its south face (right, lit). Doors and windows sit on
the face that runs along the wall, so a wall going east-west shows them on the south face and one going north-south on the
west face. Every texture comes from the 32 px pieces already made for the top-down view (structures.py): the front of the
wall (`wall_<m>_face_3`), its cap (`wall_<m>_top_15`), the door and the window, so the materials look the same.

Names: `iso_roof_<kind>_<shape>` (64x32, drawn WALL_H up) · `iso_wall_<m>` · `iso_wall_<m>_low` (cut away, PZ) · `iso_wall_<m>_<door|dooropen|window>_<s|w>`.
Each is 64 px wide and `32 + height` tall; its pivot is the centre of the footprint (atlas.py).
"""

from __future__ import annotations

from PIL import Image

from .structures import MATERIALS

W = 64                  # diamond width
D = 32                  # diamond height
WALL_H = 80             # wall height in px (2.5 cells of screen height)
LOW_H = 10              # a wall cut away near the player
FACE_TOP = 8            # rows of cap at the top of the 32 px front sprites; the face below tiles every 24 rows
DOOR_H = 56
WINDOW_LOW, WINDOW_HIGH = 22, 62


def _shade(c: tuple, k: float) -> tuple:
    r, g, b = c[:3]
    if k >= 0:
        return (int(r + (255 - r) * k), int(g + (255 - g) * k), int(b + (255 - b) * k), 255)
    return (int(r * (1 + k)), int(g * (1 + k)), int(b * (1 + k)), 255)


def _face_px(front: Image.Image, tx: int, h: int, height: int) -> tuple:
    """Colour of the wall face `tx` px along the wall and `h` px above the ground, from the 32 px front sprite."""
    rows = front.height - FACE_TOP
    from_top = height - 1 - h
    y = FACE_TOP + from_top % rows
    c = front.getpixel((tx % front.width, y))
    if from_top < 2:
        c = _shade(c, .22)                                   # the lit lip under the cap
    if h < 3:
        c = _shade(c, -.35)                                  # dirt and shadow at the foot
    return c


def _sample_diamond(src: Image.Image, X: int, Y: int, top: int) -> tuple | None:
    """Maps a pixel of a diamond whose top vertex is at row `top` to the square texture (north up, east right)."""
    fx = (X + .5 - W / 2) / (W / 2)
    fy = (top + D - (Y + .5)) / (D / 2) - 1                  # 1 at the top vertex, -1 at the bottom one
    a = (fx + fy + 1) / 2                                     # east, 0..1
    b = (fy - fx + 1) / 2                                     # north, 0..1
    if not (0 <= a < 1 and 0 <= b < 1):
        return None
    return src.getpixel((int(a * src.width), int((1 - b) * src.height) if b > 0 else src.height - 1))


def _overlay(piece: Image.Image | None, tx: int, h: int, low: int, high: int) -> tuple | None:
    """Pixel of a door or window sprite stretched over the face between heights `low` and `high`."""
    if piece is None or not (low <= h < high):
        return None
    span = piece.height - FACE_TOP
    y = FACE_TOP + int((high - 1 - h) / (high - low) * span)
    c = piece.getpixel((tx, min(piece.height - 1, y)))
    return c if c[3] == 255 else None


def block(front: Image.Image, cap: Image.Image, height: int, south: Image.Image | None = None, west: Image.Image | None = None,
          low: int = 0, high: int = 0) -> Image.Image:
    """A wall block: cap on top, west face on the left (darker), south face on the right; optional piece on either face."""
    img = Image.new("RGBA", (W, D + height), (0, 0, 0, 0))
    px = img.load()
    base = D + height                                          # bottom edge of the footprint
    for X in range(W):
        u = (X + .5) / (W / 2)
        if X < W // 2:                                         # west face: from the left vertex down to the bottom one
            ground = base - D / 2 + u * D / 2
            tx = int(u * 32) % 32
            piece = west
            k = -.24
        else:                                                  # south face: from the bottom vertex up to the right one
            u -= 1
            ground = base - u * D / 2
            tx = int(u * 32) % 32
            piece = south
            k = 0.0
        for Y in range(img.height):
            h = int(ground - (Y + .5))
            if 0 <= h < height:
                c = _overlay(piece, tx, h, low, high) or _face_px(front, tx, h, height)
                if X in (W // 2 - 1, W // 2):
                    c = _shade(c, .1)                          # the corner catches the light
                px[X, Y] = _shade(c, k) if k else c
    for X in range(W):
        for Y in range(D):
            c = _sample_diamond(cap, X, Y, 0)
            if c is not None:
                px[X, Y] = _shade(c, .08)
    return img


ROOF_STEP = 10                                        # px of roof height per step of the pitch
ROOF_LEVELS = 4                                       # flat cells at 0..3 steps above the eaves
SLOPE_SHADE = {"n": 0.10, "e": -0.18, "s": -0.02, "w": -0.08}      # the slope rising north faces you and is lit; the one rising east is in shade
SAMPLES = 96


def roof_cell(piece: Image.Image, level: int, rising: str | None) -> Image.Image:
    """A roof cell at `level` steps over the eaves; with `rising` ("n", "e", "s", "w") its top climbs one more step toward that side, so
    neighbouring cells join into a smooth pitch. West and south faces run down to the eaves, so nothing leaves a gap."""
    top = level + (1 if rising else 0)
    img = Image.new("RGBA", (W, D + top * ROOF_STEP), (0, 0, 0, 0))
    px = img.load()
    height = img.height

    def lift(a: float, b: float) -> float:
        t = {"n": b, "e": a, "s": 1 - b, "w": 1 - a}[rising] if rising else 0.0
        return (level + t) * ROOF_STEP

    for ia in range(SAMPLES):
        for ib in range(SAMPLES):
            a, b = (ia + .5) / SAMPLES, (ib + .5) / SAMPLES
            X = int(32 + 32 * (a - b))
            base = D - (a + b) * D / 2                                  # row from the top of the eave diamond
            Y = int(height - D + base - lift(a, b))
            if 0 <= X < W and 0 <= Y < height:
                c = piece.getpixel((int(a * 32) % 32, int((1 - b) * 32) % 32))
                px[X, Y] = _shade(c, SLOPE_SHADE[rising] if rising else 0.0)
    for edge in ("west", "south"):
        for i in range(SAMPLES):
            t = (i + .5) / SAMPLES
            a, b = (0.0, 1 - t) if edge == "west" else (t, 0.0)
            X = int(32 + 32 * (a - b))
            base = D - (a + b) * D / 2
            y_top = height - D + base - lift(a, b)
            y_floor = height - D + base
            k = -.30 if edge == "west" else -.14
            for Yf in range(int(y_top) + 1, int(y_floor) + 1):
                if 0 <= X < W and 0 <= Yf < height and px[X, Yf][3] == 0:
                    c = piece.getpixel((int(t * 32) % 32, (Yf * 2) % 32))
                    px[X, Yf] = _shade(c, k)
                    if X + 1 < W and px[X + 1, Yf][3] == 0 and edge == "west":
                        px[X + 1, Yf] = _shade(c, k)
                    if X - 1 >= 0 and px[X - 1, Yf][3] == 0 and edge == "south":
                        px[X - 1, Yf] = _shade(c, k)
    return img


def ground_diamond(src: Image.Image) -> Image.Image:
    """A flat 32 px tile as a 64x32 diamond, drawn a hair larger than its cell so neighbouring diamonds leave no seam."""
    img = Image.new("RGBA", (W, D), (0, 0, 0, 0))
    for X in range(W):
        for Y in range(D):
            fx = (X + .5 - W / 2) / (W / 2)
            fy = (D / 2 - (Y + .5)) / (D / 2)
            if abs(fx) + abs(fy) > 1.06:
                continue
            a = min(.999, max(0.0, (fx + fy + 1) / 2))
            b = min(.999, max(0.0, (fy - fx + 1) / 2))
            img.putpixel((X, Y), src.getpixel((min(src.width - 1, int(a * src.width)), min(src.height - 1, int((1 - b) * src.height)))))
    return img


def roof(piece: Image.Image) -> Image.Image:
    """A roof piece laid on top of the walls: the 32 px piece as a 64x32 diamond (the game lifts it WALL_H, atlas.py pivot)."""
    img = Image.new("RGBA", (W, D), (0, 0, 0, 0))
    for X in range(W):
        for Y in range(D):
            c = _sample_diamond(piece, X, Y, 0)
            if c is not None:
                img.putpixel((X, Y), c)
    return img


# ------------------------------------------------------------------ things hung on walls (D-027)
DECO_LOW, DECO_HIGH = 18, 70


def _front(draw) -> Image.Image:
    """A 32 px wide front picture of a wall piece; rows FACE_TOP.. map to the wall between DECO_LOW and DECO_HIGH."""
    img = Image.new("RGBA", (32, 40), (0, 0, 0, 0))
    draw(img.load())
    return img


def _rect(px, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            px[x, y] = c


def _picture(px):
    _rect(px, 9, 13, 22, 29, (70, 46, 30, 255))                         # frame
    _rect(px, 10, 14, 21, 21, (126, 168, 196, 255))                      # sky
    _rect(px, 10, 22, 21, 28, (92, 128, 76, 255))                        # field
    _rect(px, 13, 19, 16, 23, (210, 196, 150, 255))                      # a house far away
    _rect(px, 18, 16, 19, 17, (240, 226, 160, 255))                      # sun


def _picture2(px):
    _rect(px, 11, 11, 20, 32, (150, 116, 54, 255))
    _rect(px, 12, 12, 19, 31, (60, 50, 62, 255))
    _rect(px, 14, 15, 17, 19, (206, 168, 140, 255))                      # a face
    _rect(px, 13, 20, 18, 30, (96, 48, 52, 255))                         # in red


def _clock(px):
    for y in range(14, 30):
        for x in range(8, 24):
            d = ((x - 15.5) ** 2 + (y - 21.5) ** 2) ** .5
            if d <= 7.5:
                px[x, y] = (40, 36, 40, 255) if d > 6.4 else (232, 228, 214, 255)
    _rect(px, 15, 17, 16, 21, (30, 30, 34, 255))
    _rect(px, 16, 21, 19, 22, (30, 30, 34, 255))


def _shelf(px):
    _rect(px, 4, 30, 27, 31, (112, 76, 46, 255))
    for i, (x, h, c) in enumerate(((6, 7, (150, 52, 48)), (9, 8, (52, 92, 140)), (12, 6, (204, 174, 70)), (15, 8, (70, 120, 80)),
                                    (21, 5, (230, 230, 230)), (24, 4, (160, 120, 80)))):
        _rect(px, x, 30 - h, x + 2, 29, c + (255,))


def _curtain(px):
    _rect(px, 1, 8, 30, 8, (90, 70, 50, 255))                            # rod
    for x0 in (2, 22):
        for x in range(x0, x0 + 8):
            fold = (x - x0) % 3
            c = (156, 54, 50) if fold == 0 else (186, 70, 62) if fold == 1 else (132, 44, 42)
            for y in range(9, 40):
                px[x, y] = c + (255,)


DECO = {"picture": _picture, "picture2": _picture2, "clock": _clock, "shelf": _shelf, "curtain": _curtain}


def face_only(piece: Image.Image, side: str, low: int, high: int) -> Image.Image:
    """Only the piece, laid on the south or west face of a wall block (same geometry as `block`)."""
    img = Image.new("RGBA", (W, D + WALL_H), (0, 0, 0, 0))
    px = img.load()
    base = D + WALL_H
    for X in range(W):
        u = (X + .5) / (W / 2)
        west = X < W // 2
        if west != (side == "w"):
            continue
        if west:
            ground = base - D / 2 + u * D / 2
            tx, k = int(u * 32) % 32, -.24
        else:
            u -= 1
            ground = base - u * D / 2
            tx, k = int(u * 32) % 32, 0.0
        for Y in range(img.height):
            h = int(ground - (Y + .5))
            c = _overlay(piece, tx, h, low, high)
            if c is not None:
                px[X, Y] = _shade(c, k) if k else c
    return img


def build(images: dict[str, Image.Image]) -> dict[str, Image.Image]:
    out: dict[str, Image.Image] = {}
    for name, draw in DECO.items():
        front = _front(draw)
        for side in ("s", "w"):
            out[f"iso_deco_{name}_{side}"] = face_only(front, side, DECO_LOW, DECO_HIGH)
    for name, piece in list(images.items()):
        if (name.startswith("ground_") or name.startswith("floor_")) and piece.size == (32, 32):
            out["iso_" + name] = ground_diamond(piece)
    for name, piece in images.items():
        if name.startswith("roof_") and name.endswith("_flat"):
            kind = name[len("roof_"):-len("_flat")]
            for level in range(ROOF_LEVELS):
                out[f"iso_roof_{kind}_f{level}"] = roof_cell(piece, level, None)
                if level < ROOF_LEVELS - 1:
                    for d in ("n", "e", "s", "w"):
                        out[f"iso_roof_{kind}_s{level}_{d}"] = roof_cell(piece, level, d)
    for m in MATERIALS:
        front, cap = images.get(f"wall_{m}_face_3"), images.get(f"wall_{m}_top_15")
        if front is None or cap is None:
            continue
        door, door_open, window = images.get(f"door_{m}_closed"), images.get(f"door_{m}_open"), images.get(f"window_{m}")
        out[f"iso_wall_{m}"] = block(front, cap, WALL_H)
        out[f"iso_wall_{m}_low"] = block(front, cap, LOW_H)
        for side in ("s", "w"):
            for name, piece, lo, hi in (("door", door, 0, DOOR_H), ("dooropen", door_open, 0, DOOR_H), ("window", window, WINDOW_LOW, WINDOW_HIGH)):
                kw = {"south": piece} if side == "s" else {"west": piece}
                out[f"iso_wall_{m}_{name}_{side}"] = block(front, cap, WALL_H, low=lo, high=hi, **kw)
    return out
