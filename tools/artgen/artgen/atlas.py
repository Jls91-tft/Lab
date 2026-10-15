"""Packs named sprites into one atlas PNG and describes them in JSON.

Rectangles in the JSON use Unity's texture convention (origin at the bottom-left),
so the game can pass them straight to Sprite.Create.
"""

from __future__ import annotations

import json

from PIL import Image

from .canvas import Canvas

PADDING = 2
ATLAS_WIDTH = 256
PPU = 16


def pack(sprites: dict[str, Canvas]) -> tuple[Image.Image, list[dict]]:
    # Tallest first, then by name: a stable order makes the output reproducible.
    order = sorted(sprites, key=lambda n: (-sprites[n].height, -sprites[n].width, n))
    x = y = PADDING
    shelf = 0
    placed: list[tuple[str, int, int]] = []
    for name in order:
        c = sprites[name]
        if x + c.width + PADDING > ATLAS_WIDTH:
            x = PADDING
            y += shelf + PADDING
            shelf = 0
        placed.append((name, x, y))
        x += c.width + PADDING
        shelf = max(shelf, c.height)
    height = _pow2(y + shelf + PADDING)

    atlas = Image.new("RGBA", (ATLAS_WIDTH, height), (0, 0, 0, 0))
    entries = []
    for name, px, py in placed:
        c = sprites[name]
        atlas.paste(c.image, (px, py))
        entries.append({"name": name, "x": px, "y": height - py - c.height, "w": c.width, "h": c.height})
    entries.sort(key=lambda e: e["name"])
    return atlas, entries


def describe(entries: list[dict], atlas: Image.Image) -> str:
    doc = {
        "format": "cepacero.atlas",
        "version": 1,
        "source": "tools/artgen",
        "ppu": PPU,
        "width": atlas.width,
        "height": atlas.height,
        "sprites": entries,
    }
    return json.dumps(doc, ensure_ascii=False, indent=1) + "\n"


def _pow2(n: int) -> int:
    p = 1
    while p < n:
        p *= 2
    return p
