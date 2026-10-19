from __future__ import annotations

import io
import json
from functools import lru_cache
from pathlib import Path

import pytest
from PIL import Image

from spritegen import characters, decor, structures, underground, world
from spritegen.__main__ import REPO, build_all, build_images, zombie_ids
from spritegen.atlas import PPU, pack

ALLOWED_ALPHA = {0, 78, 255}
DIRS = ("down", "up", "left")


@lru_cache(maxsize=1)
def images() -> dict[str, Image.Image]:
    return build_images()


def test_deterministic():
    a, ja = build_all()
    b, jb = build_all()
    assert ja == jb
    assert Image.open(io.BytesIO(a)).tobytes() == Image.open(io.BytesIO(b)).tobytes()


def test_every_bestiary_zombie_has_all_directions_and_frames():
    names = images().keys()
    for zid in zombie_ids():
        for d in DIRS:
            for f in range(characters.NFRAMES):
                assert f"zombie_{zid}_{d}_{f}" in names, (zid, d, f)


def test_players_and_downed_exist():
    names = images().keys()
    for prefix in ("player", "player_other"):
        for d in DIRS:
            for f in range(characters.NFRAMES):
                assert f"{prefix}_{d}_{f}" in names
    assert "player_downed" in names


def test_covers_every_world_name_of_the_current_16px_atlas():
    """The game asks for these names; spritegen must be able to replace the world half of artgen's atlas."""
    path = REPO / "CepaCero" / "Assets" / "Resources" / "Cepa" / "Art" / "atlas.json"
    if not path.exists():
        pytest.skip("no 16 px atlas in this checkout")
    wanted = {s["name"] for s in json.loads(path.read_text(encoding="utf-8"))["sprites"]
              if s["name"].startswith(("ground_", "block_", "decor_", "decal_", "player_", "zombie_"))}
    assert not wanted - images().keys()


def test_ground_and_blocks_cover_the_game_ids():
    g = REPO / "packages" / "com.cepacero.core" / "Runtime" / "World" / "GroundType.cs"
    text = g.read_text(encoding="utf-8")
    for kind in ("Dirt", "Asphalt", "Sidewalk", "Grass", "Water", "Floor", "Rail"):
        assert kind in text
        for v in range(3):
            assert f"ground_{kind.lower()}_{v}" in images()


def test_sizes():
    for name, im in images().items():
        if name.startswith(("ground_", "block_")) or name.startswith("decal_") and "rug" not in name:
            assert im.size == (32, 32), name
        elif name.startswith("decal_rug"):
            assert im.size == (96, 32), name
        elif name.startswith("zombie_brute"):
            assert im.size == (64, 96), name          # La Mole, two tiles wide
        elif name.startswith(("zombie_", "player_")) and "downed" not in name:
            assert im.size == (32, 48), name


def test_only_known_alpha_values_and_no_empty_sprite():
    for name, im in images().items():
        alpha = set(im.getchannel("A").getdata())
        assert alpha <= ALLOWED_ALPHA, (name, alpha)
        assert 255 in alpha, name


def test_ground_and_wall_tiles_are_opaque():
    for name, im in images().items():
        if name.startswith("ground_") or name in ("block_building_wall", "block_building_wall_face", "block_wood_wall",
                                                  "block_metal_wall", "block_concrete_wall"):
            assert set(im.getchannel("A").getdata()) == {255}, name


def test_walk_frames_differ():
    for zid in zombie_ids():
        for d in DIRS:
            frames = [images()[f"zombie_{zid}_{d}_{f}"].tobytes() for f in range(characters.NFRAMES)]
            assert len(set(frames)) >= 3, (zid, d)           # frames 0 and 2 may match: both feet down


def test_directions_differ():
    for zid in zombie_ids():
        views = {d: images()[f"zombie_{zid}_{d}_0"].tobytes() for d in DIRS}
        assert len(set(views.values())) == 3, zid


def test_zombie_types_have_distinct_silhouettes():
    sil = {}
    for zid in zombie_ids():
        im = images()[f"zombie_{zid}_down_0"]
        sil[zid] = tuple(a > 0 for a in im.getchannel("A").getdata())
    assert len(set(sil.values())) == len(sil)


def test_mirror_twice_is_identity():
    s = characters.draw_side(characters.zombie_looks()["walker"], 1)
    assert s.flipped().flipped().image().tobytes() == s.image().tobytes()


def test_a_zombie_without_a_look_fails_loudly():
    with pytest.raises(ValueError, match="without a look"):
        characters.build(["walker", "made_up_zombie"])


def test_atlas_rects_fit_and_do_not_overlap():
    atlas, entries = pack(images())
    boxes = []
    for e in entries:
        top = atlas.height - e["y"] - e["h"]
        assert 0 <= e["x"] and e["x"] + e["w"] <= atlas.width and 0 <= top and top + e["h"] <= atlas.height
        boxes.append((e["x"], top, e["x"] + e["w"], top + e["h"]))
    for i, a in enumerate(boxes):
        for b in boxes[i + 1:]:
            assert a[2] <= b[0] or b[2] <= a[0] or a[3] <= b[1] or b[3] <= a[1]


def test_atlas_json_matches_and_characters_have_a_foot_pivot():
    _, text = build_all()
    doc = json.loads(text)
    assert doc["ppu"] == PPU == 32
    by = {s["name"]: s for s in doc["sprites"]}
    assert by["zombie_walker_down_0"]["pivot_y"] == pytest.approx(4 / 48, abs=1e-3)
    assert by["zombie_brute_down_0"]["pivot_y"] == pytest.approx(8 / 96, abs=1e-3)
    assert "pivot_y" not in by["ground_grass_0"]
    assert "pivot_y" not in by["player_downed"]


def test_every_opaque_pixel_is_in_the_palette():
    """The world has a closed, hand-designed palette (D-021); the stylized buildings of D-035 keep their own flat colours."""
    from spritegen.palette import PALETTE
    from spritegen.toon import is_toon
    allowed = set(PALETTE)
    used = set()
    for name, im in images().items():
        if is_toon(name):
            continue
        for r, g, b, a in im.getdata():
            if a == 255:
                used.add((r, g, b))
    assert used <= allowed, sorted(used - allowed)[:5]
    print(f"spritegen uses {len(used)} of {len(allowed)} palette colours")
    assert len(used) >= len(allowed) * 0.8, "palette colours nobody uses: trim the palette"


def test_palette_has_no_duplicates_and_keeps_the_strain_vivid():
    from spritegen.palette import FAMILIES, PALETTE
    assert len(set(PALETTE)) == len(PALETTE)
    vivid = [c for c in PALETTE if c[1] > 180 and c[0] < 220 and c[2] < 160 and c[1] > c[0] + 20]
    assert len(vivid) >= 2, "the mutants' green must stay the loudest colour"


# ------------------------------------------------------------------ construction pieces (D-020)
def test_every_material_has_every_piece_and_autotile_mask():
    names = images().keys()
    for m in structures.MATERIALS:
        for mask in range(16):
            assert f"wall_{m}_top_{mask}" in names
        for mask in range(4):
            assert f"wall_{m}_face_{mask}" in names and f"lowbarricade_{m}_{mask}" in names and f"fence_{m}_{mask}" in names
        for n in (f"door_{m}_closed", f"door_{m}_open", f"window_{m}", f"floor_{m}_0", f"floor_{m}_1"):
            assert n in names, n
    for n in ("overlay_barricade", "overlay_crack_1", "overlay_crack_2"):
        assert n in names
    for f in range(4):
        assert f"obj_campfire_{f}" in names


def test_wall_top_masks_are_all_different_and_connected_edges_are_open():
    for m in structures.MATERIALS:
        tops = [images()[f"wall_{m}_top_{k}"].tobytes() for k in range(16)]
        assert len(set(tops)) == 16, m
        free, joined = images()[f"wall_{m}_top_0"], images()[f"wall_{m}_top_15"]
        assert free.getpixel((16, 0)) != joined.getpixel((16, 0)), m          # an exposed edge looks different
        if m != "glass":
            assert set(joined.getchannel("A").getdata()) == {255}             # nothing cut when fully surrounded


def test_glass_is_see_through_and_everything_else_is_not():
    for name, im in images().items():
        alpha = set(im.getchannel("A").getdata())
        solid_piece = name.startswith(("window_", "floor_")) or ("_face_" in name and name.startswith("wall_"))
        if "glass" in name and name.split("_")[0] in ("wall", "door", "window", "floor", "lowbarricade", "fence"):
            assert 78 in alpha, name
        elif solid_piece:
            assert alpha == {255}, name


def test_fourteen_materials_look_different():
    faces = {m: images()[f"wall_{m}_face_3"].convert("RGB") for m in structures.MATERIALS}
    assert len(faces) >= 14
    names = list(faces)
    for i, a in enumerate(names):
        for b in names[i + 1:]:
            pa, pb = list(faces[a].getdata()), list(faces[b].getdata())
            diff = sum(abs(x[k] - y[k]) for x, y in zip(pa, pb) for k in range(3)) / (len(pa) * 3)
            assert diff > 12, (a, b, diff)


# ------------------------------------------------------------------ polish
def test_walker_crowd_variants_are_all_different():
    variants = ["walker"] + [f"walker_v{i}" for i in range(1, 10)]
    views = {v: images()[f"zombie_{v}_down_1"].tobytes() for v in variants}
    assert len(set(views.values())) == len(variants)
    for v in variants:
        for d in DIRS:
            assert f"zombie_{v}_{d}_0" in images()


def test_animated_extras_exist_and_move():
    for prefix in ("anim_water", "decor_barrel_fire", "obj_campfire"):
        frames = [images()[f"{prefix}_{f}"].tobytes() for f in range(4)]
        assert len(set(frames)) == 4, prefix


def test_pillars_arches_gates_roofs_and_stairs_exist():
    names = images().keys()
    for m in structures.MATERIALS:
        assert f"pillar_{m}_top" in names and f"pillar_{m}_face" in names, m
        for part in ("left", "right"):
            assert f"bigwindow_{m}_{part}" in names, m
        if m != "glass":
            assert f"arch_{m}" in names, m
            for state in ("closed", "open"):
                for part in ("left", "right"):
                    assert f"gate_{m}_{state}_{part}" in names, (m, state, part)
    for roof in structures.ROOFS:
        for shape in structures.ROOF_SHAPES:
            assert f"roof_{roof}_{shape}" in names
    for m in structures.STAIR_MATERIALS:
        for d in "nesw":
            assert f"stairs_{m}_{d}" in names
        assert len({images()[f"stairs_{m}_{d}"].tobytes() for d in "nesw"}) == 4


def test_roof_shapes_are_lit_differently():
    for roof in structures.ROOFS:
        if roof == "glasshouse":
            continue
        shapes = {images()[f"roof_{roof}_{s}"].tobytes() for s in structures.ROOF_SHAPES}
        assert len(shapes) == len(structures.ROOF_SHAPES), roof


# ------------------------------------------------------------------ underground (D-022)
def test_underground_pieces_exist():
    names = images().keys()
    for m in underground.st.UNDERGROUND:
        for mask in range(16):
            assert f"wall_{m}_top_{mask}" in names
        for mask in range(4):
            assert f"wall_{m}_face_{mask}" in names
        for v in range(2):
            assert f"floor_{m}_{v}" in names
    for m in ("wood", "stone", "concrete", "steel"):
        for d in "nesw":
            assert f"stairs_down_{m}_{d}" in names
    for n in ("pit_0", "pit_1", "ladder_wood", "ladder_steel", "hatch_wood_closed", "hatch_wood_open", "hatch_steel_closed",
              "hatch_steel_open", "beam_h", "beam_v", "beam_prop", "overlay_dig_1", "overlay_dig_2", "overlay_dig_3"):
        assert n in names, n
    assert len({images()[f"obj_torch_{f}"].tobytes() for f in range(4)}) == 4


def test_earth_and_rock_look_different_from_each_other_and_from_the_surface():
    mean = {}
    for m in ("earth", "rock", "brick", "adobe"):
        px = list(images()[f"wall_{m}_face_3"].convert("RGB").getdata())
        mean[m] = tuple(sum(c[i] for c in px) / len(px) for i in range(3))
    assert sum(abs(a - b) for a, b in zip(mean["earth"], mean["rock"])) > 30
    assert sum(abs(a - b) for a, b in zip(mean["earth"], mean["adobe"])) > 15


def test_a_cave_floor_is_darker_than_the_earth_wall_next_to_it():
    def lum(name):
        px = list(images()[name].convert("RGB").getdata())
        return sum(sum(c) for c in px) / (len(px) * 3)
    assert lum("floor_earth_0") < lum("wall_earth_top_15")


# ------------------------------------------------------------------ data ↔ art (D-022)
SPRITES_FOR_SHAPE = {
    "wall": ("wall_{m}_top_15", "wall_{m}_face_3"), "halfwall": ("lowbarricade_{m}_0",), "pillar": ("pillar_{m}_top", "pillar_{m}_face"),
    "arch": ("arch_{m}",), "door": ("door_{m}_closed", "door_{m}_open"), "gate": ("gate_{m}_closed_left", "gate_{m}_closed_right"),
    "window": ("window_{m}",), "bigwindow": ("bigwindow_{m}_left", "bigwindow_{m}_right"), "fence": ("fence_{m}_0",),
    "floor": ("floor_{m}_0",), "stairs": ("stairs_{m}_n", "stairs_{m}_e", "stairs_{m}_s", "stairs_{m}_w"), "hatch": ("hatch_{m}_closed", "hatch_{m}_open"),
}


def _construction():
    path = REPO / "data" / "generated" / "construction.json"
    if not path.exists():
        pytest.skip("construction.json not generated")
    return json.loads(path.read_text(encoding="utf-8"))


def test_every_construction_block_has_its_art():
    c = _construction()
    names = images().keys()
    missing = []
    for b in c["blocks"]:
        if b["material"] in {m["id"] for m in c["materials"]}:
            for pattern in SPRITES_FOR_SHAPE[b["shape"]]:
                if pattern.format(m=b["material"]) not in names:
                    missing.append((b["key"], pattern.format(m=b["material"])))
    assert not missing, missing[:8]


def test_roofs_and_diggable_ground_have_art():
    c = _construction()
    names = images().keys()
    for r in c["roofs"]:
        for shape in structures.ROOF_SHAPES:
            assert f"roof_{r['id']}_{shape}" in names, r["id"]
    for n in c["naturals"]:
        if n["solid"]:
            assert f"wall_{n['id']}_top_15" in names and f"floor_{n['id']}_0" in names, n["id"]
        else:                                                         # cave floor and pit
            assert f"floor_{n['id']}_0" in names or f"{n['id']}_0" in names, n["id"]
