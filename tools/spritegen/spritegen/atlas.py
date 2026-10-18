"""Packs the generated sprites into one atlas PNG and describes them in JSON.

Same layout as tools/artgen so the game can read it with the same code: rectangles use
Unity's convention (origin at the bottom-left). Differences: `ppu` is 32 and sprites that
stand on the ground carry a `pivot_y` so their feet sit on the entity position.
"""

from __future__ import annotations

import json

from PIL import Image

from .gfx import Sprite

PADDING = 2
ATLAS_WIDTH = 1024
PPU = 32
FOOT = 44 / 48            # a 32x48 character stands with its feet 44 px from the top


def pivot_y(name: str, height: int) -> float | None:
    """Vertical pivot (0 = bottom edge) for sprites that must stand on the ground, else None."""
    if name.startswith(("zombie_", "player_")) and not name.endswith("downed"):
        return round((height - height * FOOT) / height, 4)
    if name.startswith("iso_roof_"):
        return round((16 - 80) / height, 4)                   # D-025: the eave diamond (its centre is 16 px up from the bottom) lifted onto an 80 px wall
    if name.startswith("iso_"):
        return round(16 / height, 4)                          # D-025: the centre of the 64x32 footprint at the bottom
    return None


def pack(sprites: dict[str, Image.Image]) -> tuple[Image.Image, list[dict]]:
    order = sorted(sprites, key=lambda n: (-sprites[n].height, -sprites[n].width, n))
    x = y = PADDING
    shelf = 0
    placed: list[tuple[str, int, int]] = []
    for name in order:
        im = sprites[name]
        if x + im.width + PADDING > ATLAS_WIDTH:
            x = PADDING
            y += shelf + PADDING
            shelf = 0
        placed.append((name, x, y))
        x += im.width + PADDING
        shelf = max(shelf, im.height)
    height = _pow2(y + shelf + PADDING)
    atlas = Image.new("RGBA", (ATLAS_WIDTH, height), (0, 0, 0, 0))
    entries = []
    for name, px, py in placed:
        im = sprites[name]
        atlas.paste(im, (px, py))
        entry = {"name": name, "x": px, "y": height - py - im.height, "w": im.width, "h": im.height}
        pv = pivot_y(name, im.height)
        if pv is not None:
            entry["pivot_x"] = 0.5
            entry["pivot_y"] = pv
        entries.append(entry)
    entries.sort(key=lambda e: e["name"])
    return atlas, entries


def describe(entries: list[dict], atlas: Image.Image) -> str:
    doc = {
        "format": "cepacero.atlas",
        "version": 1,
        "source": "tools/spritegen",
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
