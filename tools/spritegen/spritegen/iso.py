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


def roof(piece: Image.Image) -> Image.Image:
    """A roof piece laid on top of the walls: the 32 px piece as a 64x32 diamond (the game lifts it WALL_H, atlas.py pivot)."""
    img = Image.new("RGBA", (W, D), (0, 0, 0, 0))
    for X in range(W):
        for Y in range(D):
            c = _sample_diamond(piece, X, Y, 0)
            if c is not None:
                img.putpixel((X, Y), c)
    return img


def build(images: dict[str, Image.Image]) -> dict[str, Image.Image]:
    out: dict[str, Image.Image] = {}
    for name, piece in images.items():
        if name.startswith("roof_"):
            out["iso_" + name] = roof(piece)
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
