"""Isometric furniture (D-027, the Project Zomboid way): every piece is a small solid drawn in the same 2:1 projection as the walls, with
its front painted (cupboard doors, the oven, books on a shelf) and its back plain.

Names: `iso_furn_<kind>_<o>` where `<o>` says where the piece stands in its 1 m cell:
  s  against the north wall, front to the south (seen)       w  against the east wall, front to the west (seen)
  n  against the south wall, its back toward you             e  against the west wall, its back toward you
  c  in the middle of the cell (tables, chairs)
A sprite is 64 px wide (128 for the 2x2 bed) and its pivot is the middle of its footprint (atlas.py), like the walls.
"""

from __future__ import annotations

import math

from PIL import Image

W, D = 64, 32
ORIENTS = ("s", "w", "n", "e")


def _mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def _shade(c, k):
    return _mix(c, (255, 255, 255), k) if k >= 0 else _mix(c, (0, 0, 0), -k)


class Canvas:
    """A sprite with a depth buffer: cells is the footprint side in cells (1 or 2), height the tallest point in px."""

    def __init__(self, cells: int, height: int):
        self.cells = cells
        self.w = W * cells
        self.h = D * cells + height + 2
        self.img = Image.new("RGBA", (self.w, self.h), (0, 0, 0, 0))
        self.px = self.img.load()
        self.depth = [[-1e9] * self.h for _ in range(self.w)]

    def screen(self, gx: float, gy: float, z: float) -> tuple[float, float]:
        return self.w / 2 + (gx - gy) * W / 2, self.h - (gx + gy) * D / 2 - z

    def plot(self, gx: float, gy: float, z: float, color) -> None:
        x, y = self.screen(gx, gy, z)
        xi, yi = int(x), int(y)
        if 0 <= xi < self.w and 0 <= yi < self.h:
            d = -(gx + gy) + z * 0.001                                     # nearer the camera: smaller gx + gy
            if d >= self.depth[xi][yi]:
                self.depth[xi][yi] = d
                self.px[xi, yi] = tuple(color[:3]) + (255,)

    def box(self, x0, x1, y0, y1, z0, z1, top, side, front=None, front_face=None):
        """A solid: top, south face (y = y0, lit) and west face (x = x0, in shade). `front(u, v)` paints the face named `front_face` ('s' or 'w'),
        u along the face 0..1 (left to right as seen), v height in px from its base."""
        step = 0.5 / W                                                     # half a pixel in cell units
        nx = max(2, int((x1 - x0) / step))
        ny = max(2, int((y1 - y0) / step))
        nz = max(2, int((z1 - z0) * 2))
        for i in range(nx + 1):
            gx = x0 + (x1 - x0) * i / nx
            for j in range(ny + 1):
                gy = y0 + (y1 - y0) * j / ny
                self.plot(gx, gy, z1, top(i / nx, j / ny) if callable(top) else top)
        for i in range(nx + 1):                                           # south face
            u = i / nx
            for k in range(nz + 1):
                v = (z1 - z0) * k / nz
                c = front(u, v) if (front and front_face == "s") else side
                self.plot(x0 + (x1 - x0) * u, y0, z0 + v, _shade(c, -0.02))
        for j in range(ny + 1):                                           # west face, seen from the left: u runs north to south
            u = j / ny
            for k in range(nz + 1):
                v = (z1 - z0) * k / nz
                c = front(1 - u, v) if (front and front_face == "w") else side
                self.plot(x0, y1 - (y1 - y0) * (1 - u), z0 + v, _shade(c, -0.22))

    def finish(self) -> Image.Image:
        """A dark outline around the piece, as everything else in the atlas."""
        out = self.img.copy()
        src = self.img.load()
        dst = out.load()
        for x in range(self.w):
            for y in range(self.h):
                if src[x, y][3]:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < self.w and 0 <= ny < self.h and src[nx, ny][3]:
                        dst[x, y] = (24, 20, 26, 255)
                        break
        return out


def _footprint(orient: str, width: float, depth: float, cells: int = 1):
    """x0, x1, y0, y1 of a piece `width` along its wall and `depth` deep, pushed against the wall named by the orientation."""
    m = (cells - width) / 2
    if orient == "s":
        return m, m + width, cells - depth, cells
    if orient == "w":
        return cells - depth, cells, m, m + width
    if orient == "n":
        return m, m + width, 0.0, depth
    if orient == "e":
        return 0.0, depth, m, m + width
    c = (cells - depth) / 2
    return m, m + width, c, c + depth


def _front_face(orient: str):
    return {"s": "s", "w": "w"}.get(orient)


# ------------------------------------------------------------------ painters of the fronts

def doors(body, line, handle, n=2, top_gap=2):
    def f(u, v):
        if (u * n) % 1 < 0.04 or u < 0.03 or u > 0.97:
            return line
        if abs((u * n) % 1 - (0.85 if int(u * n) % 2 == 0 else 0.15)) < 0.05 and 0.55 < v / 30 < 0.75:
            return handle
        return body
    return f


def oven(u, v):
    if v > 24:
        return (60, 62, 68)
    if 0.15 < u < 0.85 and 6 < v < 20:
        return (40, 36, 40) if not (0.2 < u < 0.8 and 8 < v < 18) else (90, 70, 50)
    if v > 21 and abs(u * 4 % 1 - 0.5) < 0.15:
        return (40, 40, 44)
    return (220, 222, 226)


def fridge(u, v):
    if abs(v - 40) < 0.8:
        return (150, 154, 162)
    if 0.78 < u < 0.86 and (44 < v < 54 or 22 < v < 34):
        return (120, 124, 132)
    return (226, 230, 236)


def books(frame, back):
    colours = [(150, 52, 48), (52, 92, 140), (204, 174, 70), (70, 120, 80), (120, 70, 120), (200, 200, 190)]

    def f(u, v):
        if u < 0.06 or u > 0.94 or v % 15 < 2 or v > 57:
            return frame
        if v % 15 > 12:
            return back
        k = int(u * 14 + int(v / 15) * 3)
        return colours[k % len(colours)]
    return f


def goods(frame):
    colours = [(200, 60, 50), (60, 120, 180), (230, 200, 80), (80, 160, 90), (230, 230, 230), (190, 120, 60)]

    def f(u, v):
        if u < 0.05 or u > 0.95 or v % 16 < 2:
            return frame
        if v % 16 > 11:
            return (70, 72, 78)
        return colours[int(u * 9 + int(v / 16) * 2) % len(colours)]
    return f


def drawers(body, line, knob):
    def f(u, v):
        if v % 9 < 1 or u < 0.03 or u > 0.97:
            return line
        if abs(u - 0.5) < 0.06 and v % 9 in (4, 5):
            return knob
        return body
    return f


def mirror_cabinet(u, v):
    if 0.2 < u < 0.8 and 8 < v < 22:
        return (170, 200, 214) if (u * 10 + v / 4) % 3 > 0.6 else (220, 236, 240)
    return doors((236, 236, 232), (180, 180, 176), (140, 140, 150))(u, v)


# ------------------------------------------------------------------ pieces

def piece(kind: str, orient: str) -> Image.Image:
    wood = (150, 104, 66)
    wood_dark = (112, 76, 46)
    white = (232, 232, 228)
    grey = (150, 154, 160)
    f = _front_face(orient)
    if kind == "counter" or kind == "cupboard":
        c = Canvas(1, 40)
        x0, x1, y0, y1 = _footprint(orient, 1.0, 0.6)
        c.box(x0, x1, y0, y1, 0, 30, (214, 210, 200), white, doors(white, (176, 172, 164), (120, 120, 128)), f)
        c.box(x0, x1, y0, y1, 30, 33, (196, 190, 176), (176, 170, 156))       # worktop
        return c.finish()
    if kind == "stove":
        c = Canvas(1, 40)
        x0, x1, y0, y1 = _footprint(orient, 0.9, 0.6)
        def top(u, w):
            for cx, cy in ((0.3, 0.3), (0.7, 0.3), (0.3, 0.7), (0.7, 0.7)):
                if (u - cx) ** 2 + (w - cy) ** 2 < 0.02:
                    return (40, 40, 44)
            return (210, 212, 216)
        c.box(x0, x1, y0, y1, 0, 32, top, white, oven, f)
        return c.finish()
    if kind == "fridge":
        c = Canvas(1, 70)
        x0, x1, y0, y1 = _footprint(orient, 0.8, 0.6)
        c.box(x0, x1, y0, y1, 0, 64, (236, 238, 242), (226, 230, 236), fridge, f)
        return c.finish()
    if kind == "wardrobe":
        c = Canvas(1, 78)
        x0, x1, y0, y1 = _footprint(orient, 0.95, 0.55)
        c.box(x0, x1, y0, y1, 0, 70, (170, 122, 80), wood, doors((166, 118, 76), wood_dark, (220, 190, 110)), f)
        return c.finish()
    if kind == "bookshelf":
        c = Canvas(1, 70)
        x0, x1, y0, y1 = _footprint(orient, 0.9, 0.4)
        c.box(x0, x1, y0, y1, 0, 60, (160, 112, 72), wood, books(wood_dark, (70, 50, 36)), f)
        return c.finish()
    if kind == "shelf":
        c = Canvas(1, 70)
        x0, x1, y0, y1 = _footprint(orient, 1.0, 0.5)
        c.box(x0, x1, y0, y1, 0, 62, (170, 174, 180), grey, goods((110, 114, 120)), f)
        return c.finish()
    if kind == "nightstand":
        c = Canvas(1, 30)
        x0, x1, y0, y1 = _footprint(orient, 0.5, 0.45)
        c.box(x0, x1, y0, y1, 0, 22, (170, 122, 80), wood, drawers((166, 118, 76), wood_dark, (220, 190, 110)), f)
        c.box(x0 + 0.1, x0 + 0.2, y0 + 0.1, y0 + 0.2, 22, 32, (240, 220, 160), (200, 180, 120))   # a lamp
        return c.finish()
    if kind == "bathroom_cabinet":
        c = Canvas(1, 40)
        x0, x1, y0, y1 = _footprint(orient, 0.8, 0.5)
        c.box(x0, x1, y0, y1, 0, 30, (236, 236, 232), white, mirror_cabinet, f)
        return c.finish()
    if kind == "toilet":
        c = Canvas(1, 40)
        x0, x1, y0, y1 = _footprint(orient, 0.45, 0.25)
        c.box(x0, x1, y0, y1, 0, 34, (240, 242, 246), (232, 234, 238))           # cistern against the wall
        bx0, bx1, by0, by1 = _footprint(orient, 0.4, 0.7)
        c.box(bx0, bx1, by0, by1, 0, 16, (236, 238, 242), (226, 228, 232))
        return c.finish()
    if kind == "desk":
        c = Canvas(1, 34)
        x0, x1, y0, y1 = _footprint(orient, 1.0, 0.6)
        c.box(x0, x1, y0, y1, 24, 27, (176, 128, 84), wood)
        for lx, ly in ((x0, y0), (x1 - 0.06, y0), (x0, y1 - 0.06), (x1 - 0.06, y1 - 0.06)):
            c.box(lx, lx + 0.06, ly, ly + 0.06, 0, 24, wood_dark, wood_dark)
        c.box(x0 + 0.3, x0 + 0.55, y0 + 0.15, y0 + 0.4, 27, 40, (60, 62, 70), (40, 42, 48))   # a computer screen
        return c.finish()
    if kind in ("dining_table", "coffee_table"):
        low = kind == "coffee_table"
        c = Canvas(1, 34)
        x0, x1, y0, y1 = _footprint("c", 0.8, 0.8)
        h = 12 if low else 24
        c.box(x0, x1, y0, y1, h, h + 3, (176, 128, 84) if not low else (130, 88, 56), wood)
        for lx, ly in ((x0 + 0.05, y0 + 0.05), (x1 - 0.11, y0 + 0.05), (x0 + 0.05, y1 - 0.11), (x1 - 0.11, y1 - 0.11)):
            c.box(lx, lx + 0.06, ly, ly + 0.06, 0, h, wood_dark, wood_dark)
        return c.finish()
    if kind == "chair":
        c = Canvas(1, 40)
        x0, x1, y0, y1 = _footprint("c", 0.4, 0.4)
        c.box(x0, x1, y0, y1, 14, 17, (176, 128, 84), wood)
        for lx, ly in ((x0, y0), (x1 - 0.05, y0), (x0, y1 - 0.05), (x1 - 0.05, y1 - 0.05)):
            c.box(lx, lx + 0.05, ly, ly + 0.05, 0, 14, wood_dark, wood_dark)
        c.box(x0, x1, y1 - 0.06, y1, 17, 34, (176, 128, 84), wood)            # back
        return c.finish()
    if kind == "sofa":
        c = Canvas(1, 36)
        x0, x1, y0, y1 = _footprint(orient if orient != "c" else "s", 1.0, 0.7)
        fabric, dark = (110, 132, 116), (82, 100, 88)
        c.box(x0, x1, y0, y1, 0, 14, (124, 148, 130), fabric)
        bx0, bx1, by0, by1 = _footprint(orient if orient != "c" else "s", 1.0, 0.2)
        c.box(bx0, bx1, by0, by1, 14, 30, (124, 148, 130), dark)                # back rest against the wall
        return c.finish()
    if kind in ("boxes", "crate"):
        c = Canvas(1, 40)
        cardboard, tape = (176, 138, 88), (210, 190, 140)
        x0, x1, y0, y1 = _footprint(orient, 0.6, 0.5)
        c.box(x0, x1, y0, y1, 0, 16, cardboard, (156, 118, 72), lambda u, v: tape if abs(u - 0.5) < 0.05 else (156, 118, 72), "s")
        c.box(x0 + 0.08, x1 - 0.12, y0 + 0.06, y1 - 0.06, 16, 30, (186, 148, 96), (160, 122, 76))
        return c.finish()
    raise ValueError(kind)


def bed(orient: str) -> Image.Image:
    """A double bed on a 2x2 footprint, the headboard against its wall."""
    c = Canvas(2, 36)
    frame, sheet, quilt = (120, 84, 54), (236, 234, 226), (70, 100, 150)
    head = {"s": (0.2, 1.8, 1.85, 2.0), "w": (1.85, 2.0, 0.2, 1.8), "n": (0.2, 1.8, 0.0, 0.15), "e": (0.0, 0.15, 0.2, 1.8)}[orient]
    c.box(0.2, 1.8, 0.2, 1.8, 0, 10, sheet, frame)
    c.box(0.2, 1.8, 0.2, 1.8, 10, 14, lambda u, w: quilt if ((orient in ("s", "n") and (w < 0.65 if orient == "s" else w > 0.35))
                                                               or (orient in ("w", "e") and (u < 0.65 if orient == "w" else u > 0.35))) else sheet,
          (60, 86, 130))
    c.box(head[0], head[1], head[2], head[3], 0, 30, (140, 98, 64), frame)
    return c.finish()


KINDS = ("counter", "stove", "fridge", "wardrobe", "bookshelf", "shelf", "nightstand", "bathroom_cabinet", "toilet", "desk",
         "sofa", "boxes")


def build() -> dict[str, Image.Image]:
    out: dict[str, Image.Image] = {}
    for kind in KINDS:
        for o in ORIENTS:
            out[f"iso_furn_{kind}_{o}"] = piece(kind, o)
    for kind in ("dining_table", "coffee_table", "chair"):
        out[f"iso_furn_{kind}_c"] = piece(kind, "c")
    for o in ORIENTS:
        out[f"iso_furn_bed_{o}_2x2"] = bed(o)
    return out
