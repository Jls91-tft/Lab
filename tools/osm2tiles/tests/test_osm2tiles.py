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
    assert doc["format"] == "cepacero.map" and doc["version"] == 2
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


# ---- Contract with the C# reader (packages/com.cepacero.core) ---------------
CONTRACT_FIXTURE = Path(__file__).resolve().parents[3] / "packages/com.cepacero.core/Tests/Fixtures/synthetic.map.json"


def test_csharp_contract_fixture_is_up_to_date(synthetic_osm, tmp_path):
    """The C# MapImporter tests load CONTRACT_FIXTURE. If the format changes,
    regenerate it with UPDATE_FIXTURES=1 python -m pytest and fix both sides."""
    import os

    bbox = ",".join(str(v) for v in BBOX.as_list())
    assert main(["--bbox", bbox, "--input", str(synthetic_osm), "--name", "synthetic", "--out", str(tmp_path)]) == 0
    fresh = (tmp_path / "synthetic.map.json").read_text(encoding="utf-8")
    if os.environ.get("UPDATE_FIXTURES") == "1":
        CONTRACT_FIXTURE.parent.mkdir(parents=True, exist_ok=True)
        CONTRACT_FIXTURE.write_text(fresh, encoding="utf-8")
    assert CONTRACT_FIXTURE.exists(), "run once with UPDATE_FIXTURES=1"
    assert json.loads(CONTRACT_FIXTURE.read_text(encoding="utf-8")) == json.loads(fresh)


# ---- Storeys, basements and roofs (cepacero.map v2) -------------------------
def test_levels_come_from_the_tag_then_from_the_kind():
    assert rules.building_levels({"building:levels": "5"}, "generic") == rules.MAX_LEVELS
    assert rules.building_levels({"building:levels": "3;4"}, "generic") == 3
    assert rules.building_levels({"building:levels": "2,5"}, "generic") == 2
    assert rules.building_levels({"building:levels": "0"}, "generic") == 1
    assert rules.building_levels({"building:levels": "many"}, "residential") == rules.DEFAULT_LEVELS["residential"]
    assert rules.building_levels({}, "office") == rules.DEFAULT_LEVELS["office"]
    assert all(1 <= v <= rules.MAX_LEVELS for v in rules.DEFAULT_LEVELS.values())
    assert set(rules.DEFAULT_LEVELS) == set(rules.BUILDING_KINDS)


def test_basements_only_when_osm_says_so_and_are_capped():
    assert rules.building_underground({}) == 0
    assert rules.building_underground({"building:levels:underground": "2"}) == 2
    assert rules.building_underground({"building:levels:underground": "9"}) == rules.MAX_UNDERGROUND
    assert rules.building_underground({"building:levels:underground": "-1"}) == 0


def test_roofs_follow_material_then_shape_then_kind():
    assert rules.building_roof({"roof:material": "roof_tiles"}, "industrial") == "tile"
    assert rules.building_roof({"roof:material": "metal"}, "residential") == "sheet"
    assert rules.building_roof({"roof:shape": "flat"}, "residential") == "flat"
    assert rules.building_roof({}, "residential") == "tile"
    assert rules.building_roof({}, "garage") == "sheet"
    assert rules.building_roof({}, "office") == "flat"
    assert all(rules.building_roof({}, k) in rules.ROOFS for k in rules.BUILDING_KINDS)


def test_buildings_carry_storeys_basement_and_roof(synthetic_osm):
    tm, _ = _map(synthetic_osm)
    by_kind = {b.kind: b for b in tm.buildings}
    tagged = next(b for b in tm.buildings if b.underground == 1)
    assert (tagged.levels, tagged.roof) == (2, "sheet")                          # building A has its tags
    assert by_kind["residential"].levels == rules.DEFAULT_LEVELS["residential"]   # apartments, no tag
    assert by_kind["medical"].levels == rules.DEFAULT_LEVELS["medical"]


def test_document_is_v2_and_lists_the_roofs(synthetic_osm, tmp_path):
    from osm2tiles.export import FORMAT_VERSION, to_document
    tm, _ = _map(synthetic_osm)
    doc = to_document(tm, "t", BBOX, MPT, "2026-10-15T00:00:00Z")
    assert doc["version"] == FORMAT_VERSION == 2
    assert doc["legend"]["roofs"] == rules.ROOFS
    for b in doc["buildings"]:
        assert {"levels", "underground", "roof"} <= set(b)
