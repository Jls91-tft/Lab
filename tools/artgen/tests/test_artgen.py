import io
import json

from PIL import Image

from artgen import characters, tiles
from artgen.__main__ import build_all, build_sprites, game_ids
from artgen.palette import CLEAR, PALETTE, SOFT_SHADOW

ALLOWED = {(r, g, b, 255) for r, g, b in PALETTE.values()} | {CLEAR, SOFT_SHADOW}


def test_palette_has_32_distinct_colours():
    assert len(PALETTE) == 32
    assert len(set(PALETTE.values())) == 32


def test_every_pixel_is_from_the_palette():
    for name, c in build_sprites().items():
        if name == "ui_white":  # tinted by the game, so it has to be pure white
            continue
        colours = {p for _, p in c.image.getcolors(maxcolors=4096)}
        stray = colours - ALLOWED
        assert not stray, f"{name} uses colours outside the palette: {sorted(stray)[:3]}"


def test_covers_every_item_zombie_ground_and_block():
    item_ids, zombie_ids = game_ids()
    names = build_sprites().keys()
    for iid in item_ids:
        assert f"item_{iid}" in names
    for zid in zombie_ids:
        for facing in characters.FACINGS:
            for frame in range(characters.FRAMES):
                assert f"zombie_{zid}_{facing}_{frame}" in names
    for ground in ("dirt", "asphalt", "sidewalk", "grass", "water", "floor", "rail"):
        for v in range(tiles.GROUND_VARIANTS):
            assert f"ground_{ground}_{v}" in names
    for block in ("building_wall", "tree", "door", "door_open", "fence", "wood_wall", "metal_wall",
                  "concrete_wall", "workbench", "chest", "spike_trap"):
        assert f"block_{block}" in names


def test_tiles_and_characters_are_16px_except_the_brute():
    for name, c in build_sprites().items():
        if name.startswith(("ground_", "block_", "player_", "item_")) or (
                name.startswith("zombie_") and not name.startswith("zombie_brute")):
            assert (c.width, c.height) == (16, 16), name
        if name.startswith("zombie_brute"):
            assert (c.width, c.height) == (32, 32), name


def test_walk_frames_differ_from_standing():
    sprites = build_sprites()
    for who in ("player", "zombie_walker"):
        stand = sprites[f"{who}_down_0"].image.tobytes()
        assert sprites[f"{who}_down_1"].image.tobytes() != stand
        assert sprites[f"{who}_down_3"].image.tobytes() != stand


def test_output_is_reproducible_and_rectangles_match_the_png():
    png_a, doc_a = build_all()
    png_b, doc_b = build_all()
    assert doc_a == doc_b
    assert Image.open(io.BytesIO(png_a)).tobytes() == Image.open(io.BytesIO(png_b)).tobytes()

    atlas = Image.open(io.BytesIO(png_a)).convert("RGBA")
    doc = json.loads(doc_a)
    sprites = build_sprites()
    assert doc["ppu"] == 16 and doc["width"] == atlas.width and doc["height"] == atlas.height
    for e in doc["sprites"]:
        top = atlas.height - e["y"] - e["h"]  # JSON uses Unity's bottom-left origin
        crop = atlas.crop((e["x"], top, e["x"] + e["w"], top + e["h"]))
        assert crop.tobytes() == sprites[e["name"]].image.tobytes(), e["name"]
