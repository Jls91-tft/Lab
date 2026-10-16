from __future__ import annotations

import io
import json
from functools import lru_cache
from pathlib import Path

import pytest
from PIL import Image

from spritegen import characters, decor, world
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


def test_colour_budget():
    """The world art has no fixed palette yet (D-021): this only stops it drifting into noise.
    Quantising to 48-96 colours automatically was tried and ruins accents (yellow hard hat, green veins)."""
    colours = set()
    for im in images().values():
        colours.update(c for c in im.getdata() if c[3] == 255)
    print(f"spritegen uses {len(colours)} distinct colours")
    assert len(colours) < 4000
