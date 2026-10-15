from __future__ import annotations

import json
from pathlib import Path

from PIL import Image

from osm2tiles import rules
from osm2tiles.cli import main
from osm2tiles.geo import Projector
from osm2tiles.osm import parse_osm
from osm2tiles.rasterize import build_map

from .conftest import BBOX

MPT = 2.0
G, B = rules.G, rules.B


def _map(path: Path):
    data = parse_osm(path)
    proj = Projector(BBOX, MPT)
    return build_map(data, proj), proj


def _at(tm, layer: str, x_m: float, y_m: float) -> int:
    """Value at a point given in metres from the SW corner (row 0 = south)."""
    x, y = int(x_m / MPT), int(y_m / MPT)
    return getattr(tm, layer)[y * tm.width + x]


def test_dimensions_follow_meters_per_tile(synthetic_osm):
    tm, proj = _map(synthetic_osm)
    assert (tm.width, tm.height) == (65, 60)
    assert len(tm.ground) == len(tm.blocks) == tm.width * tm.height


def test_row_zero_is_south(synthetic_osm):
    tm, _ = _map(synthetic_osm)
    assert _at(tm, "ground", 30, 20) == G["grass"]      # park is in the south
    assert _at(tm, "ground", 25, 80) == G["floor"]      # building A is in the north


def test_street_has_asphalt_and_sidewalks(synthetic_osm):
    tm, _ = _map(synthetic_osm)
    x = int(60 / MPT)
    column = [tm.ground[y * tm.width + x] for y in range(tm.height)]
    asphalt = [y for y, g in enumerate(column) if g == G["asphalt"]]
    assert len(asphalt) == 3                            # 6 m carriageway at 2 m/tile
    assert column[asphalt[0] - 1] == column[asphalt[-1] + 1] == G["sidewalk"]
    assert abs((asphalt[0] + asphalt[-1]) / 2 - 50 / MPT) <= 1


def test_party_wall_is_one_tile_thick(synthetic_osm):
    tm, _ = _map(synthetic_osm)
    row = int(80 / MPT)
    walls = [x for x in range(int(36 / MPT), int(44 / MPT))
             if tm.blocks[row * tm.width + x] == B["building_wall"]]
    assert len(walls) == 1, walls
    assert _at(tm, "blocks", 25, 80) == B["none"]       # interior is walkable


def test_every_street_facing_building_has_a_door_to_the_outside(synthetic_osm):
    tm, _ = _map(synthetic_osm)
    assert len(tm.buildings) == 3
    for b in tm.buildings:
        assert b.doors, b
        x, y = b.doors[0], b.doors[1]
        assert tm.blocks[y * tm.width + x] == B["door"]
        neighbours = [tm.ground[(y + dy) * tm.width + (x + dx)] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))]
        assert any(n in rules.DOOR_OUTSIDE for n in neighbours)


def test_building_kinds_from_tags_and_pois(synthetic_osm):
    tm, _ = _map(synthetic_osm)
    kinds = sorted(b.kind for b in tm.buildings)
    assert kinds == ["food", "medical", "residential"]


def test_multipolygon_inner_ring_is_a_hole(synthetic_osm):
    tm, _ = _map(synthetic_osm)
    assert _at(tm, "ground", 90, 100) == G["water"]
    assert _at(tm, "ground", 104, 86) == G["dirt"]      # island


def test_trees_and_fences(synthetic_osm):
    tm, _ = _map(synthetic_osm)
    assert _at(tm, "blocks", 31, 21) == B["tree"]
    assert _at(tm, "blocks", 79, 70) == B["fence"]


def test_cli_writes_json_and_preview(synthetic_osm, tmp_path):
    out = tmp_path / "out"
    bbox = ",".join(str(v) for v in BBOX.as_list())
    rc = main(["--bbox", bbox, "--input", str(synthetic_osm), "--name", "t", "--out", str(out)])
    assert rc == 0
    doc = json.loads((out / "t.map.json").read_text(encoding="utf-8"))
    assert doc["format"] == "cepacero.map" and doc["version"] == 1
    assert "OpenStreetMap contributors" in doc["attribution"] and doc["license"] == "ODbL-1.0"
    assert doc["source"]["osm_timestamp"] == "2026-10-15T00:00:00Z"
    assert doc["legend"]["ground"] == rules.GROUND
    assert len(doc["layers"]["ground"]) == doc["width"] * doc["height"]
    assert "Brand X" not in json.dumps(doc)              # names and brands never leak
    with Image.open(out / "t.preview.png") as img:
        assert img.width == doc["width"] * 3


def test_rejects_huge_areas(tmp_path):
    assert main(["--bbox", "41.60,-0.95,41.70,-0.85", "--input", "x.osm", "--out", str(tmp_path)]) == 2


def test_doors_face_the_street(synthetic_osm):
    tm, _ = _map(synthetic_osm)
    street_row = 50 / MPT
    for b in tm.buildings:
        door_y = b.doors[1]
        facade = b.bbox[1] if b.bbox[1] > street_row else b.bbox[3]   # side closest to the street
        assert door_y == facade, b
