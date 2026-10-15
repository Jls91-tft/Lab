"""A tiny pixel canvas on top of Pillow: integer coordinates, inclusive rectangles,
y grows downwards (north of a tile is its top row). No anti-aliasing anywhere."""

from __future__ import annotations

import random
import zlib

from PIL import Image, ImageDraw

from .palette import CLEAR, SOFT_SHADOW, RGBA, rgba


def rng(name: str) -> random.Random:
    """Deterministic randomness per sprite, so regenerating gives the same pixels."""
    return random.Random(zlib.crc32(name.encode("utf-8")))


class Canvas:
    def __init__(self, width: int, height: int | None = None) -> None:
        self.image = Image.new("RGBA", (width, height or width), CLEAR)
        self._draw = ImageDraw.Draw(self.image)

    @property
    def width(self) -> int:
        return self.image.width

    @property
    def height(self) -> int:
        return self.image.height

    def inside(self, x: int, y: int) -> bool:
        return 0 <= x < self.width and 0 <= y < self.height

    def px(self, x: int, y: int, colour: str | RGBA) -> None:
        if self.inside(x, y):
            self.image.putpixel((x, y), _c(colour))

    def get(self, x: int, y: int) -> RGBA:
        return self.image.getpixel((x, y)) if self.inside(x, y) else CLEAR

    def rect(self, x0: int, y0: int, x1: int, y1: int, colour: str | RGBA) -> None:
        if x1 < x0 or y1 < y0:
            return
        self._draw.rectangle((x0, y0, x1, y1), fill=_c(colour))

    def fill(self, colour: str | RGBA) -> None:
        self.rect(0, 0, self.width - 1, self.height - 1, colour)

    def ellipse(self, x0: int, y0: int, x1: int, y1: int, colour: str | RGBA) -> None:
        self._draw.ellipse((x0, y0, x1, y1), fill=_c(colour))

    def line(self, x0: int, y0: int, x1: int, y1: int, colour: str | RGBA, width: int = 1) -> None:
        self._draw.line((x0, y0, x1, y1), fill=_c(colour), width=width)

    def polygon(self, points: list[tuple[int, int]], colour: str | RGBA) -> None:
        self._draw.polygon(points, fill=_c(colour))

    def shadow(self, x0: int, y0: int, x1: int, y1: int) -> None:
        """Soft ground shadow (the only semi-transparent colour)."""
        self._draw.ellipse((x0, y0, x1, y1), fill=SOFT_SHADOW)

    def speckle(self, r: random.Random, colours: list[str], count: int, x0: int = 0, y0: int = 0,
                x1: int | None = None, y1: int | None = None) -> None:
        x1 = self.width - 1 if x1 is None else x1
        y1 = self.height - 1 if y1 is None else y1
        for _ in range(count):
            self.px(r.randint(x0, x1), r.randint(y0, y1), r.choice(colours))

    def outline(self, colour: str = "ink") -> None:
        """Draws a 1 px outline around every opaque pixel (shadows excluded)."""
        solid = {(x, y) for x in range(self.width) for y in range(self.height) if self.get(x, y)[3] == 255}
        ring = set()
        for x, y in solid:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                n = (x + dx, y + dy)
                if n not in solid and self.inside(*n):
                    ring.add(n)
        for x, y in ring:
            self.px(x, y, colour)

    def bevel(self, light: str, dark: str) -> None:
        """Light top and left edge, dark bottom and right edge: makes a block look raised."""
        self.rect(0, 0, self.width - 1, 0, light)
        self.rect(0, 0, 0, self.height - 1, light)
        self.rect(0, self.height - 1, self.width - 1, self.height - 1, dark)
        self.rect(self.width - 1, 0, self.width - 1, self.height - 1, dark)

    def recolour(self, mapping: dict[str, str]) -> None:
        swap = {rgba(a): rgba(b) for a, b in mapping.items()}
        pixels = self.image.load()
        for x in range(self.width):
            for y in range(self.height):
                p = pixels[x, y]
                if p in swap:
                    pixels[x, y] = swap[p]

    def flipped(self) -> "Canvas":
        out = Canvas(self.width, self.height)
        out.image = self.image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        out._draw = ImageDraw.Draw(out.image)
        return out

    def rotated(self, quarter_turns: int) -> "Canvas":
        out = Canvas(self.width, self.height)
        out.image = self.image.rotate(90 * quarter_turns, expand=False)
        out._draw = ImageDraw.Draw(out.image)
        return out

    def scaled(self, factor: int) -> "Canvas":
        out = Canvas(self.width * factor, self.height * factor)
        out.image = self.image.resize((self.width * factor, self.height * factor), Image.Resampling.NEAREST)
        out._draw = ImageDraw.Draw(out.image)
        return out


def _c(colour: str | RGBA) -> RGBA:
    return rgba(colour) if isinstance(colour, str) else colour
