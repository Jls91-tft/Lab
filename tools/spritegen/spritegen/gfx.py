"""Pixel-art primitives: colours, shapes, and a Sprite with 5-tone shading and outlines.

Everything is deterministic: no global random state, noise comes from `hnoise`.
Sprites are drawn in a dict of pixels and converted to a Pillow image at the end.
"""

from __future__ import annotations

import math
from typing import Iterable

from PIL import Image

Color = tuple[int, int, int]
Pixels = set[tuple[int, int]]

SHADOW_ALPHA = 78
OUTLINE_DARK: Color = (12, 10, 20)
NB4 = ((0, -1), (-1, 0), (1, 0), (0, 1))


def hexc(h: str) -> Color:
    return int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16)


def c8(v: float) -> int:
    return max(0, min(255, int(round(v))))


def mix(a: Color, b: Color, t: float) -> Color:
    return c8(a[0] * (1 - t) + b[0] * t), c8(a[1] * (1 - t) + b[1] * t), c8(a[2] * (1 - t) + b[2] * t)


def ramp5(base: Color) -> list[Color]:
    """Dark to light: shadows drift towards violet, highlights towards warm."""
    return [mix(base, (22, 20, 46), .62), mix(base, (22, 20, 46), .32), base,
            mix(base, (255, 236, 196), .2), mix(base, (255, 240, 210), .4)]


def hnoise(x: int, y: int, salt: int) -> float:
    """Stateless noise in [0, 1): the same pixel always gets the same value."""
    return ((x * 73856093) ^ (y * 19349663) ^ (salt * 83492791)) % 1000 / 1000.0


# ----------------------------------------------------------------- shapes
def poly(points: list[tuple[float, float]]) -> Pixels:
    xs = [p[0] for p in points]
    ys = [p[1] for p in points]
    out: Pixels = set()
    n = len(points)
    for y in range(int(min(ys)) - 1, int(max(ys)) + 2):
        for x in range(int(min(xs)) - 1, int(max(xs)) + 2):
            px, py = x + .5, y + .5
            inside = False
            j = n - 1
            for i in range(n):
                xi, yi = points[i]
                xj, yj = points[j]
                if (yi > py) != (yj > py) and px < (xj - xi) * (py - yi) / (yj - yi) + xi:
                    inside = not inside
                j = i
            if inside:
                out.add((x, y))
    return out


def capsule(a: tuple[float, float], b: tuple[float, float], r: float) -> Pixels:
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    l2 = dx * dx + dy * dy or 1
    out: Pixels = set()
    for y in range(int(min(ay, by) - r) - 1, int(max(ay, by) + r) + 2):
        for x in range(int(min(ax, bx) - r) - 1, int(max(ax, bx) + r) + 2):
            px, py = x + .5, y + .5
            t = max(0, min(1, ((px - ax) * dx + (py - ay) * dy) / l2))
            if (px - ax - t * dx) ** 2 + (py - ay - t * dy) ** 2 <= r * r:
                out.add((x, y))
    return out


def ellipse(cx: float, cy: float, rx: float, ry: float) -> Pixels:
    return {(x, y) for y in range(int(cy - ry) - 1, int(cy + ry) + 2) for x in range(int(cx - rx) - 1, int(cx + rx) + 2)
            if ((x + .5 - cx) / rx) ** 2 + ((y + .5 - cy) / ry) ** 2 <= 1}


def rect(x0: float, y0: float, x1: float, y1: float) -> Pixels:
    return {(x, y) for y in range(int(y0), int(y1) + 1) for x in range(int(x0), int(x1) + 1)}


# ----------------------------------------------------------------- sprite
class Sprite:
    """A pixel canvas. `paint` shades a shape with a 5-tone ramp lit from the top left."""

    def __init__(self, w: int = 32, h: int = 48, salt: int = 0):
        self.w, self.h, self.salt = w, h, salt
        self.px: dict[tuple[int, int], Color] = {}
        self.owner: dict[tuple[int, int], int] = {}
        self.glow: dict[tuple[int, int], Color] = {}
        self.shadow: Pixels = set()
        self.meta: dict[str, tuple[int, int]] = {}
        self.sid = 0

    def inb(self, x: int, y: int) -> bool:
        return 0 <= x < self.w and 0 <= y < self.h

    def set(self, x: float, y: float, c: Color, glow: bool = False) -> None:
        x, y = int(x), int(y)
        if self.inb(x, y):
            self.px[(x, y)] = c
            if glow:
                self.glow[(x, y)] = c

    def paint(self, pts: Iterable[tuple[int, int]], base, grain: float = 0.0, edge: bool = False) -> None:
        ramp = base if isinstance(base, list) else ramp5(base)
        shape = {p for p in pts if self.inb(*p)}
        self.sid += 1
        for (x, y) in shape:
            dl = dr = du = dd = 1
            while (x - dl, y) in shape and dl < 8: dl += 1
            while (x + dr, y) in shape and dr < 8: dr += 1
            while (x, y - du) in shape and du < 8: du += 1
            while (x, y + dd) in shape and dd < 8: dd += 1
            sc = ((dr - dl) / (dr + dl)) * .62 + ((dd - du) / (dd + du)) * .38
            t = int(round(2 + sc * 2.7))
            if dl == 1 and dr > 1: t = max(t, 3)
            if du == 1 and dd > 1: t = max(t, 3)
            if dr == 1 and dl > 1: t = min(t, 1)
            if dd == 1 and du > 1: t = min(t, 1)
            if grain and hnoise(x, y, self.salt) < grain:
                t += 1 if hnoise(y, x, self.salt + 5) < .5 else -1
            self.px[(x, y)] = ramp[max(0, min(4, t))]
        if edge:
            dark = mix(ramp[0], OUTLINE_DARK, .4)
            for p in shape:
                for d in NB4:
                    n = (p[0] + d[0], p[1] + d[1])
                    if n not in shape and n in self.owner and self.owner[n] != self.sid:
                        self.px[p] = dark
        for p in shape:
            self.owner[p] = self.sid
            self.glow.pop(p, None)

    def tint(self, pts: Iterable[tuple[int, int]], color: Color, alpha: float, grain: float = 1.0) -> None:
        for p in pts:
            if p in self.px and hnoise(p[0], p[1], self.salt + 9) < grain:
                self.px[p] = mix(self.px[p], color, alpha)

    def outline(self) -> None:
        res: dict[tuple[int, int], Color] = {}
        for (x, y) in sorted(self.px):
            for dx, dy in ((0, 1), (-1, 0), (1, 0), (0, -1)):
                n = (x + dx, y + dy)
                if n not in self.px and self.inb(*n) and n not in res:
                    res[n] = mix(self.px[(x, y)], OUTLINE_DARK, .8)
        self.px.update(res)

    def flipped(self) -> "Sprite":
        """Horizontal mirror (right-facing from left-facing)."""
        o = Sprite(self.w, self.h, self.salt)
        o.px = {(self.w - 1 - x, y): c for (x, y), c in self.px.items()}
        o.glow = {(self.w - 1 - x, y): c for (x, y), c in self.glow.items()}
        o.shadow = {(self.w - 1 - x, y) for (x, y) in self.shadow}
        o.meta = {k: (self.w - 1 - v[0], v[1]) for k, v in self.meta.items()}
        return o

    def doubled(self) -> "Sprite":
        """Nearest-neighbour 2x (used for the one boss that must be two tiles wide)."""
        o = Sprite(self.w * 2, self.h * 2, self.salt)
        for (x, y), c in self.px.items():
            for dx in (0, 1):
                for dy in (0, 1):
                    o.px[(x * 2 + dx, y * 2 + dy)] = c
        for (x, y), c in self.glow.items():
            for dx in (0, 1):
                for dy in (0, 1):
                    o.glow[(x * 2 + dx, y * 2 + dy)] = c
        for (x, y) in self.shadow:
            for dx in (0, 1):
                for dy in (0, 1):
                    o.shadow.add((x * 2 + dx, y * 2 + dy))
        o.meta = {k: (v[0] * 2, v[1] * 2) for k, v in self.meta.items()}
        return o

    def image(self) -> Image.Image:
        img = Image.new("RGBA", (self.w, self.h), (0, 0, 0, 0))
        put = img.putpixel
        for (x, y) in self.shadow:
            if self.inb(x, y):
                put((x, y), (0, 0, 0, SHADOW_ALPHA))
        for (x, y), c in self.px.items():
            put((x, y), c + (255,))
        return img


class Tile:
    """A 32x32 opaque square, painted pixel by pixel."""

    def __init__(self, base: Color, size: int = 32):
        self.size = size
        self.g = [[base] * size for _ in range(size)]

    def put(self, x: int, y: int, c: Color) -> None:
        if 0 <= x < self.size and 0 <= y < self.size:
            self.g[y][x] = c

    def blend(self, x: int, y: int, c: Color, a: float) -> None:
        if 0 <= x < self.size and 0 <= y < self.size:
            self.g[y][x] = mix(self.g[y][x], c, a)

    def get(self, x: int, y: int) -> Color:
        return self.g[y][x]

    def image(self) -> Image.Image:
        img = Image.new("RGBA", (self.size, self.size))
        for y in range(self.size):
            for x in range(self.size):
                img.putpixel((x, y), self.g[y][x] + (255,))
        return img
