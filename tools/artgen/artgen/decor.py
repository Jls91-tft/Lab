"""Decoration sprites (D-019): street furniture, cars and furniture from Kenney's CC0
packs, burnt cars and wear decals drawn here, all graded like the world tiles.

decor_*: things that stand (cars, lamps, trees, furniture); the game sorts them by height
on screen like players and zombies. decal_*: flat marks (blood, stains, rubbish, glass,
rugs) drawn between the ground and the blocks. Visual only: they do not block movement.
"""

from __future__ import annotations

from PIL import Image

from .canvas import Canvas, rng
from .kenney import BLOOD, CITY, GRIME, STEP, TILE, VENDOR, grade

INDOOR = VENDOR / "roguelike-indoors" / "roguelikeIndoor_transparent.png"

# name: (sheet, column, row, width in tiles, height in tiles)
CITY_DECOR: dict[str, tuple[int, int, int, int]] = {
    "decor_car_green_h": (31, 17, 3, 1),
    "decor_car_green_v": (31, 18, 2, 2),
    "decor_car_grey_h": (31, 21, 3, 1),
    "decor_car_grey_v": (31, 22, 2, 2),
    "decor_car_orange_h": (31, 25, 3, 1),
    "decor_car_orange_v": (31, 26, 2, 2),
    "decor_lamp": (1, 16, 1, 3),
    "decor_mailbox": (4, 17, 1, 2),
    "decor_trash_bag": (13, 13, 1, 1),
    "decor_bin_0": (15, 13, 1, 1),
    "decor_bin_1": (16, 13, 1, 1),
    "decor_barrel": (12, 14, 1, 1),
    "decor_hydrant": (15, 14, 1, 1),
    "decor_cone": (14, 18, 1, 1),
    "decor_bench": (15, 15, 1, 1),
    "decor_boxes_0": (13, 16, 1, 1),
    "decor_boxes_1": (13, 17, 1, 1),
    "decor_tree_0": (32, 10, 1, 2),
    "decor_tree_1": (34, 10, 1, 2),
    "decor_tree_dead": (33, 10, 1, 2),
    "decor_bush_0": (32, 13, 1, 1),
    "decor_bush_1": (34, 13, 1, 1),
    "decor_bush_dead": (33, 13, 1, 1),
    "decor_shelf_0": (12, 18, 1, 1),
    "decor_shelf_1": (13, 18, 1, 1),
}

INDOOR_DECOR: dict[str, tuple[int, int, int, int]] = {
    "decor_table_long": (0, 0, 3, 1),
    "decor_table_oval": (3, 0, 2, 1),
    "decor_table_round": (7, 0, 1, 1),
    "decor_chair_0": (0, 2, 1, 1),
    "decor_chair_1": (0, 6, 1, 1),
    "decor_bed_0": (12, 0, 2, 2),
    "decor_bed_1": (14, 0, 2, 2),
    "decor_armchair": (14, 9, 1, 2),
    "decor_counter_0": (0, 14, 1, 1),
    "decor_counter_1": (4, 14, 1, 1),
    "decor_counter_2": (5, 14, 1, 1),
    "decor_counter_3": (7, 14, 1, 1),
    "decor_stove": (14, 14, 1, 1),
    "decor_plant_0": (16, 0, 1, 1),
    "decor_plant_1": (17, 0, 1, 1),
    "decal_rug_0": (0, 9, 3, 1),
    "decal_rug_1": (7, 9, 3, 1),
}


def region(sheet: Image.Image, col: int, row: int, w: int, h: int) -> Image.Image:
    """Stitches w x h tiles of a spaced Kenney sheet and trims to what is drawn."""
    out = Image.new("RGBA", (w * TILE, h * TILE), (0, 0, 0, 0))
    for dy in range(h):
        for dx in range(w):
            x, y = (col + dx) * STEP, (row + dy) * STEP
            out.paste(sheet.crop((x, y, x + TILE, y + TILE)), (dx * TILE, dy * TILE))
    box = out.getbbox()
    return out.crop(box) if box else out


def _canvas(image: Image.Image) -> Canvas:
    c = Canvas(image.width, image.height)
    c.image.paste(image, (0, 0))
    return c


def _burnt(image: Image.Image, name: str) -> Image.Image:
    """A burnt-out wreck: soot black body, rust patches, no glass."""
    out = image.copy()
    px = out.load()
    r = rng(name)
    for x in range(out.width):
        for y in range(out.height):
            red, green, blue, a = px[x, y]
            if a == 0:
                continue
            lum = (red + green + blue) / 3
            if lum > 180:  # windows and lights: empty, dark
                px[x, y] = (24, 22, 22, a)
            elif r.random() < 0.18:
                px[x, y] = (120, 62, 34, a)  # rust
            else:
                v = int(30 + lum * 0.25)
                px[x, y] = (v + 6, v, v - 2, a)
    return out


def _decal(name: str, colours: list[tuple[int, int, int]], blobs: int, size: int) -> Canvas:
    c = Canvas(TILE)
    r = rng(name)
    for _ in range(blobs):
        cx, cy = r.randint(4, 11), r.randint(4, 11)
        colour = r.choice(colours)
        for _ in range(size):
            c.px(cx + r.randint(-2, 2), cy + r.randint(-2, 2), (colour[0], colour[1], colour[2], 255))
    return c


def _rubbish(name: str) -> Canvas:
    c = Canvas(TILE)
    r = rng(name)
    for _ in range(4):
        x, y = r.randint(2, 12), r.randint(3, 12)
        colour = r.choice([(214, 210, 196), (157, 170, 171), (198, 101, 39), (70, 112, 178)])
        c.rect(x, y, x + r.randint(1, 2), y + r.randint(0, 1), (colour[0], colour[1], colour[2], 255))
    return c


def _glass(name: str) -> Canvas:
    c = Canvas(TILE)
    r = rng(name)
    for _ in range(9):
        x, y = r.randint(2, 13), r.randint(3, 13)
        c.px(x, y, (196, 222, 230, 255))
        if r.random() < 0.5:
            c.px(x + 1, y, (150, 186, 196, 255))
    return c


def build() -> dict[str, Canvas]:
    city = Image.open(CITY).convert("RGBA")
    indoor = Image.open(INDOOR).convert("RGBA")
    raw: dict[str, Image.Image] = {}
    for name, (col, row, w, h) in CITY_DECOR.items():
        raw[name] = region(city, col, row, w, h)
    for name, (col, row, w, h) in INDOOR_DECOR.items():
        raw[name] = region(indoor, col, row, w, h)
    raw["decor_car_burnt_h"] = _burnt(raw["decor_car_grey_h"], "burnt_h")
    raw["decor_car_burnt_v"] = _burnt(raw["decor_car_grey_v"], "burnt_v")

    out = {name: _canvas(grade(image)) for name, image in raw.items()}
    for i in range(3):
        out[f"decal_blood_{i}"] = _canvas(grade(_decal(f"blood_{i}", [BLOOD, (96, 22, 24)], 2 + i, 7).image))
        out[f"decal_stain_{i}"] = _canvas(grade(_decal(f"stain_{i}", [GRIME, (70, 66, 58)], 2, 8).image))
        out[f"decal_rubbish_{i}"] = _canvas(grade(_rubbish(f"rubbish_{i}").image))
    for i in range(2):
        out[f"decal_glass_{i}"] = _canvas(grade(_glass(f"glass_{i}").image))
    return out
