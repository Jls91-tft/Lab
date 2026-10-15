import io
import json

from PIL import Image

from artgen import characters, decor, kenney, tiles
from artgen.__main__ import build_all, build_sprites, game_ids, render_map
from artgen.palette import CLEAR, PALETTE, SOFT_SHADOW

ALLOWED = {(r, g, b, 255) for r, g, b in PALETTE.values()} | {CLEAR, SOFT_SHADOW}


def test_palette_has_32_distinct_colours():
    assert len(PALETTE) == 32
    assert len(set(PALETTE.values())) == 32


def test_icons_and_interface_use_our_palette():
    # World, people and decoration use Kenney's colours through the grade (D-019);
    # what we draw from scratch for the interface stays in the 32-colour palette.
    for name, c in build_sprites().items():
        if not name.startswith(("item_", "icon_", "ui_", "pile")) or name == "ui_white":
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


def test_kenney_world_replaces_every_generated_tile_and_adds_the_wall_front():
    names = build_sprites().keys()
    generated = tiles.build().keys()
    assert generated <= names
    assert "block_building_wall_face" in names


def test_decoration_is_complete_and_trimmed():
    sprites = decor.build()
    for name in list(decor.CITY_DECOR) + list(decor.INDOOR_DECOR) + ["decor_car_burnt_h", "decor_car_burnt_v"]:
        c = sprites[name]
        assert c.image.getbbox() == (0, 0, c.width, c.height), f"{name} is not trimmed or is empty"
    for kind, count in (("blood", 3), ("stain", 3), ("rubbish", 3), ("glass", 2)):
        for i in range(count):
            assert sprites[f"decal_{kind}_{i}"].image.getbbox() is not None


def test_grade_mutes_colour_and_keeps_transparency():
    im = Image.new("RGBA", (2, 1), (0, 0, 0, 0))
    im.putpixel((0, 0), (200, 40, 40, 255))
    out = kenney.grade(im)
    r, g, b, a = out.getpixel((0, 0))
    assert a == 255 and r < 200 and (r - g) < 160  # less saturated, darker
    assert out.getpixel((1, 0)) == (0, 0, 0, 0)


def test_map_preview_draws_wall_fronts(tmp_path):
    doc = {
        "width": 3, "height": 3,
        "legend": {"ground": ["dirt", "floor"], "blocks": ["none", "building_wall"]},
        "layers": {"ground": [0] * 9, "blocks": [0, 0, 0, 1, 1, 1, 1, 1, 1]},  # rows 1-2 are walls
    }
    path = tmp_path / "tiny.map.json"
    path.write_text(json.dumps(doc), encoding="utf-8")
    image = render_map(path, graded=True)
    world = build_sprites()
    face = world["block_building_wall_face"].image.tobytes()
    top = world["block_building_wall"].image.tobytes()
    # image row 0 is map row 2 (wall above wall: top); image row 1 is map row 1 (dirt below: front)
    assert image.crop((0, 0, 16, 16)).tobytes() == top
    assert image.crop((0, 16, 16, 32)).tobytes() == face


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
