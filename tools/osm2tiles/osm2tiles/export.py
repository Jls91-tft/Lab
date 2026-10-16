"""Writers for `cepacero.map` v2 (JSON; v1 plus storeys, basements and roofs per building) and the PNG preview."""

from __future__ import annotations

import json
from dataclasses import asdict
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from . import __version__, rules
from .geo import BBox
from .rasterize import TileMap

FORMAT = "cepacero.map"
FORMAT_VERSION = 2
ATTRIBUTION = "© OpenStreetMap contributors, ODbL 1.0 — https://www.openstreetmap.org/copyright"
ATTRIBUTION_PLAIN = "(c) OpenStreetMap contributors, ODbL 1.0 - openstreetmap.org/copyright"


def to_document(tm: TileMap, name: str, bbox: BBox, meters_per_tile: float, osm_timestamp: str) -> dict:
    """Build the JSON document. Field layout is JsonUtility-friendly (no
    dictionaries, no nested arrays) so Unity can read it without extra packages."""
    return {
        "format": FORMAT,
        "version": FORMAT_VERSION,
        "generator": f"osm2tiles {__version__}",
        "name": name,
        "attribution": ATTRIBUTION,
        "license": "ODbL-1.0",
        "source": {"bbox": bbox.as_list(), "osm_timestamp": osm_timestamp, "projection": "local-equirectangular"},
        "meters_per_tile": meters_per_tile,
        "width": tm.width,
        "height": tm.height,
        "origin": "bottom-left",
        "legend": {"ground": rules.GROUND, "blocks": rules.BLOCKS, "building_kinds": rules.BUILDING_KINDS, "roofs": rules.ROOFS},
        "layers": {"ground": tm.ground, "blocks": tm.blocks},
        "buildings": [asdict(b) for b in tm.buildings],
    }


def write_json(doc: dict, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as f:
        json.dump(doc, f, ensure_ascii=False, separators=(",", ":"))


# ---- Preview ---------------------------------------------------------------
GROUND_COLORS = {
    "dirt": (122, 104, 84), "asphalt": (58, 58, 64), "sidewalk": (150, 146, 140),
    "grass": (76, 128, 62), "water": (52, 96, 160), "floor": (196, 186, 166), "rail": (110, 80, 70),
}
BLOCK_COLORS = {
    "building_wall": (40, 30, 30), "tree": (30, 80, 34), "door": (220, 160, 40), "fence": (170, 120, 70),
}
KIND_TINTS = {
    "generic": (196, 186, 166), "residential": (206, 180, 150), "commercial": (120, 170, 210),
    "food": (230, 200, 100), "hardware": (200, 130, 90), "medical": (235, 110, 110),
    "police": (90, 110, 200), "school": (180, 140, 210), "religious": (170, 170, 120),
    "office": (150, 190, 190), "industrial": (150, 150, 150), "garage": (130, 130, 120),
}


def write_preview(tm: TileMap, path: Path, scale: int = 3, title: str = "") -> None:
    w, h = tm.width, tm.height
    img = Image.new("RGB", (w, h))
    px = img.load()
    floor_kind = [None] * (w * h)
    for b in tm.buildings:
        x0, y0, x1, y1 = b.bbox
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                floor_kind[y * w + x] = b.kind  # bbox approximation is fine for a preview
    g_names, b_names = rules.GROUND, rules.BLOCKS
    for i in range(w * h):
        x, row = i % w, i // w
        y = h - 1 - row  # image row 0 is north
        blk = b_names[tm.blocks[i]]
        if blk != "none":
            color = BLOCK_COLORS[blk]
        else:
            g = g_names[tm.ground[i]]
            color = KIND_TINTS[floor_kind[i]] if g == "floor" and floor_kind[i] else GROUND_COLORS[g]
        px[x, y] = color
    img = img.resize((w * scale, h * scale), Image.NEAREST)

    font = ImageFont.load_default()
    entries = [(n, c) for n, c in GROUND_COLORS.items() if n != "floor"] + list(BLOCK_COLORS.items())
    entries += [(f"floor:{k}", c) for k, c in KIND_TINTS.items()]
    cols = max(1, (w * scale - 10) // 130)
    rows = -(-len(entries) // cols)
    bar_h = 44 + rows * 16
    canvas = Image.new("RGB", (w * scale, h * scale + bar_h), (20, 20, 22))
    canvas.paste(img, (0, 0))
    d = ImageDraw.Draw(canvas)
    top = h * scale + 6
    for k, (name, color) in enumerate(entries):
        cx, cy = 6 + (k % cols) * 130, top + (k // cols) * 16
        d.rectangle((cx, cy + 2, cx + 10, cy + 12), fill=color)
        d.text((cx + 14, cy), name, fill=(230, 230, 230), font=font)
    # The default bitmap font lacks some glyphs (em dash): keep the image text plain.
    d.text((6, top + rows * 16 + 2), f"{title}  |  {w}x{h} tiles", fill=(200, 200, 200), font=font)
    d.text((6, top + rows * 16 + 16), ATTRIBUTION_PLAIN, fill=(200, 200, 200), font=font)
    path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(path)
