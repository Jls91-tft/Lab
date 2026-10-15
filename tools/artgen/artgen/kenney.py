"""World tiles from Kenney's CC0 roguelike packs (art/vendor/kenney, D-019), turned into
our own look: what the packs lack is drawn here in Kenney's colours, the city gets wear
(cracks, stains, blood) and everything goes through a muted, slightly sickly grade.

Names match tiles.py, so the game does not care where a tile comes from, plus
block_building_wall_face: the brick front shown when nothing solid stands below a wall.
"""

from __future__ import annotations

import random
from pathlib import Path

from PIL import Image

from .canvas import Canvas, rng

VENDOR = Path(__file__).resolve().parents[3] / "art" / "vendor" / "kenney"
CITY = VENDOR / "roguelike-modern-city" / "tilemap.png"
TILE = 16
STEP = 17  # Kenney sheets leave 1 px between tiles

# (column, row) in the city sheet; variants share a list.
CITY_TILES: dict[str, list[tuple[int, int]]] = {
    "ground_grass": [(0, 24), (1, 24), (1, 26)],
    "ground_dirt": [(4, 24), (5, 24), (11, 26)],
    "ground_sidewalk": [(2, 24), (3, 24), (2, 24)],
    "ground_floor": [(6, 24), (7, 24), (6, 24)],
    "block_building_wall": [(14, 0)],
    "block_building_wall_face": [(2, 6)],
    "block_tree": [(34, 13)],
    "block_fence": [(21, 14)],
    "block_wood_wall": [(15, 16)],
    "block_door": [(20, 26)],
    "block_door_open": [(22, 26)],
    "block_workbench": [(12, 17)],
    "block_chest": [(14, 16)],
}

# Kenney's own colours, for the tiles drawn here.
ASPHALT = (64, 64, 64)
ASPHALT_DARK = (50, 50, 52)
WATER = (99, 197, 207)
WATER_LIGHT = (150, 222, 228)
WATER_DARK = (78, 168, 180)
STEEL = (157, 170, 171)
STEEL_LIGHT = (184, 197, 202)
ROOF = (166, 169, 174)
ROOF_DARK = (120, 122, 126)
WOOD = (180, 131, 85)
WOOD_DARK = (139, 94, 58)
BLOOD = (120, 28, 30)
GRIME = (92, 84, 70)

# The grade: 30 % less saturation, a bit darker, a touch of green. Keeps every pixel's
# relation to its neighbours, so Kenney's shading survives.
DESATURATE = 0.30
TINT = (0.86, 0.89, 0.83)


def grade(image: Image.Image) -> Image.Image:
    out = image.copy()
    px = out.load()
    for x in range(out.width):
        for y in range(out.height):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            lum = 0.299 * r + 0.587 * g + 0.114 * b
            r, g, b = (c + (lum - c) * DESATURATE for c in (r, g, b))
            px[x, y] = (_byte(r * TINT[0]), _byte(g * TINT[1]), _byte(b * TINT[2]), a)
    return out


def _byte(v: float) -> int:
    return max(0, min(255, int(round(v))))


def _canvas(image: Image.Image) -> Canvas:
    c = Canvas(image.width, image.height)
    c.image.paste(image, (0, 0))
    return c


def _rgba(rgb: tuple[int, int, int]) -> tuple[int, int, int, int]:
    return (rgb[0], rgb[1], rgb[2], 255)


# ---------------------------------------------------------------- wear

def _crack(c: Canvas, r: random.Random, colour: tuple[int, int, int]) -> None:
    x, y = r.randint(3, 12), r.randint(2, 5)
    for _ in range(r.randint(5, 8)):
        c.px(x, y, _rgba(colour))
        x = max(1, min(14, x + r.choice((-1, 0, 1))))
        y = min(14, y + 1)


def _stain(c: Canvas, r: random.Random, colour: tuple[int, int, int], size: int) -> None:
    cx, cy = r.randint(4, 11), r.randint(4, 11)
    for _ in range(size):
        c.px(cx + r.randint(-2, 2), cy + r.randint(-1, 1), _rgba(colour))


def _wear(name: str, variant: int, c: Canvas) -> None:
    # Kept light on purpose: a mark on one variant repeats on a third of the cells and
    # reads as noise. Stains, blood and rubbish belong in sparse decals instead.
    r = rng(f"{name}_{variant}_wear")
    if name == "ground_grass" and variant == 2:
        _stain(c, r, (120, 140, 70), 8)  # dead, yellowed grass


# ------------------------------------------------- tiles Kenney does not have

def _asphalt(variant: int) -> Canvas:
    c = Canvas(TILE)
    c.fill(_rgba(ASPHALT))
    r = rng(f"asphalt_{variant}")
    if variant == 1:
        _crack(c, r, (40, 40, 42))
    elif variant == 2:
        c.ellipse(4, 6, 10, 10, _rgba(ASPHALT_DARK))  # oil
        c.px(12, 3, _rgba(STEEL))  # a bit of rubbish
    return c


def _water(variant: int) -> Canvas:
    c = Canvas(TILE)
    c.fill(_rgba(WATER))
    r = rng(f"water_{variant}")
    for _ in range(3):
        x, y = r.randint(0, 11), r.randint(1, 14)
        c.rect(x, y, x + 3, y, _rgba(WATER_LIGHT))
    for _ in range(6):
        c.px(r.randint(0, 15), r.randint(0, 15), _rgba(WATER_DARK))
    return c


def _rail(variant: int) -> Canvas:
    c = Canvas(TILE)
    c.fill(_rgba(ROOF_DARK))
    r = rng(f"rail_{variant}")
    for _ in range(30):
        c.px(r.randint(0, 15), r.randint(0, 15), _rgba(r.choice((ROOF, ASPHALT, STEEL))))
    for y in (1, 6, 11):
        c.rect(1, y, 14, y + 2, _rgba(WOOD_DARK))
        c.rect(1, y, 14, y, _rgba(WOOD))
    for x in (3, 11):
        c.rect(x, 0, x + 1, 15, _rgba(STEEL))
        c.rect(x, 0, x, 15, _rgba(STEEL_LIGHT))
    return c


def _plate(base: tuple[int, int, int], light: tuple[int, int, int], dark: tuple[int, int, int], rivets: bool) -> Canvas:
    c = Canvas(TILE)
    c.fill(_rgba(base))
    c.rect(0, 0, 15, 0, _rgba(light))
    c.rect(0, 0, 0, 15, _rgba(light))
    c.rect(0, 15, 15, 15, _rgba(dark))
    c.rect(15, 0, 15, 15, _rgba(dark))
    if rivets:
        c.rect(1, 7, 14, 8, _rgba(dark))
        for x, y in ((3, 3), (12, 3), (3, 12), (12, 12)):
            c.px(x, y, _rgba(light))
    else:
        _crack(c, rng("concrete_wall"), dark)
    return c


def build(generated: dict[str, Canvas], graded: bool = True) -> dict[str, Canvas]:
    """Kenney-based world tiles, graded. Takes the generated set for the few tiles drawn
    in our own palette (spike trap) so they get the same grade. graded=False is only for
    the before/after preview."""
    sheet = Image.open(CITY).convert("RGBA")
    raw: dict[str, Canvas] = {}
    for name, cells in CITY_TILES.items():
        variants = [(name, None)] if not name.startswith("ground_") else [(f"{name}_{v}", v) for v in range(len(cells))]
        for (out_name, v), (col, row) in zip(variants, cells):
            tile = sheet.crop((col * STEP, row * STEP, col * STEP + TILE, row * STEP + TILE))
            c = _canvas(tile)
            if v is not None:
                _wear(name, v, c)
            raw[out_name] = c
    for v in range(3):
        raw[f"ground_asphalt_{v}"] = _asphalt(v)
        raw[f"ground_water_{v}"] = _water(v)
        raw[f"ground_rail_{v}"] = _rail(v)
    raw["block_metal_wall"] = _plate(STEEL, STEEL_LIGHT, ROOF_DARK, rivets=True)
    raw["block_concrete_wall"] = _plate(ROOF, STEEL_LIGHT, ROOF_DARK, rivets=False)
    raw["block_spike_trap"] = generated["block_spike_trap"]
    return {name: _canvas(grade(c.image)) if graded else c for name, c in raw.items()}
