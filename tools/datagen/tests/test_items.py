from __future__ import annotations

import pytest

from datagen.__main__ import build_all
from datagen.items import (BUILDING_KINDS, DataError, cross_check, item_to_runtime, loot_to_runtime,
                           recipe_to_runtime)


def _item(**kw):
    base = {"id": "thing", "name": "Cosa", "category": "material", "stack": 10}
    base.update(kw)
    return base


def test_real_docs_build_and_cross_check():
    outputs = build_all()
    assert len(outputs) == 7  # bestiary, items, recipes, loot, construction, diary, strings


def test_weapons_are_flattened():
    pistol = item_to_runtime(_item(id="pistol", category="weapon", stack=1,
                                   weapon={"damage": 35, "damage_type": "ballistic", "range": 12, "ammo": "pistol_ammo"}))
    assert pistol["is_weapon"] and pistol["ammo"] == "pistol_ammo" and pistol["arc"] == 0.0
    assert item_to_runtime(_item())["is_weapon"] is False


def test_food_fills_bars():
    can = item_to_runtime(_item(id="can", category="food", food=40, heal=5))
    assert can["food"] == 40 and can["water"] == 0 and can["heal"] == 5


@pytest.mark.parametrize("overrides, message", [
    ({"category": "gadget"}, "category"),
    ({"stack": 0}, "stack"),
    ({"stack": 2.5}, "stack"),
    ({"weight": 3}, "unknown keys"),
    ({"weapon": {"damage": 1, "laser": 2}}, "weapon accepts"),
    ({"weapon": {"damage": 1, "arc": 90, "ammo": "x"}}, "melee"),
    ({"id": "Bad"}, "snake_case"),
    ({"category": "food", "food": 150}, "full bar"),
    ({"water": 20}, "food items"),
])
def test_invalid_items(overrides, message):
    with pytest.raises(DataError, match=message):
        item_to_runtime(_item(**overrides))


@pytest.mark.parametrize("entry, message", [
    ({"id": "r", "ingredients": {"wood": 1}}, "exactly one"),
    ({"id": "r", "result": "a", "result_block": "door", "ingredients": {"wood": 1}}, "exactly one"),
    ({"id": "r", "result_block": "castle", "ingredients": {"wood": 1}}, "result_block"),
    ({"id": "r", "result": "a", "ingredients": {}}, "non-empty"),
    ({"id": "r", "result": "a", "ingredients": {"wood": 0}}, "positive"),
    ({"id": "r", "result": "a", "ingredients": {"wood": 1}, "station": "forge"}, "station"),
])
def test_invalid_recipes(entry, message):
    with pytest.raises(DataError, match=message):
        recipe_to_runtime(entry)


def test_loot_needs_every_building_kind():
    tables = [{"kind": k, "rolls": 1, "loot": {"wood": [1, 1, 1.0]}} for k in BUILDING_KINDS if k != "police"]
    with pytest.raises(DataError, match="police"):
        loot_to_runtime(tables)


def test_cross_references_are_checked():
    items = [item_to_runtime(_item(id="wood"))]
    recipes = [recipe_to_runtime({"id": "r", "result": "wood", "ingredients": {"stone": 1}})]
    loot = {"tables": []}
    with pytest.raises(DataError, match="unknown item 'stone'"):
        cross_check(items, recipes, loot, {"zombies": []})
    with pytest.raises(DataError, match="bestiary walker"):
        cross_check(items, [], loot, {"zombies": [{"id": "walker", "loot": [{"item": "rag"}]}]})
    samples = [item_to_runtime(_item(id="wood")), item_to_runtime(_item(id="key"))]
    locked = [recipe_to_runtime({"id": "r", "result": "wood", "ingredients": {"wood": 1}, "unlocked_by": "key"})]
    with pytest.raises(DataError, match="must be a sample"):
        cross_check(samples, locked, loot, {"zombies": []})


# ------------------------------------------------------------ construction (D-022)
def _construction():
    return build_all()


def _construction_doc():
    from datagen.__main__ import DOCS
    from datagen.construction import build_construction
    return build_construction((DOCS / "construccion.md").read_text(encoding="utf-8"))


def test_construction_blocks_are_derived_from_shape_times_material():
    c = _construction_doc()
    keys = {b["key"] for b in c["blocks"]}
    assert len(keys) == len(c["blocks"])
    assert {"wall_wood", "wall_brick", "door_steel", "door_steel_open", "gate_wood", "stairs_stone", "roof_tile", "earth", "rock"} <= keys
    assert len(c["materials"]) >= 14
    # every material block exists for exactly the shapes it declares
    for m in c["materials"]:
        for s in m["shapes"]:
            assert f"{s}_{m['id']}" in keys


def test_costs_health_and_open_variants():
    c = _construction_doc()
    b = {x["key"]: x for x in c["blocks"]}
    assert b["wall_wood"]["cost"] == [{"item": "wood", "count": 2}, {"item": "nails", "count": 1}]
    assert b["halfwall_wood"]["cost"] == [{"item": "wood", "count": 1}, {"item": "nails", "count": 1}]       # ceil(0.5 * n), at least 1
    assert b["gate_wood"]["cost"][0]["count"] == 8 and b["gate_wood"]["width"] == 2
    assert b["wall_concrete"]["hp"] == 600 and b["door_wood"]["hp"] == 50
    assert b["door_wood"]["toggle_to"] == "door_wood_open" and b["door_wood_open"]["solid"] is False and b["door_wood_open"]["cost"] == []
    assert b["door_wood_open"]["is_open_variant"] and b["door_wood_open"]["toggle_to"] == "door_wood"
    assert b["stairs_stone"]["connects"] == "up" and b["hatch_wood"]["connects"] == "down"
    assert b["window_glass"]["see_through"] and not b["wall_brick"]["see_through"]


def test_materials_get_harder_as_designed():
    c = _construction_doc()
    hp = {m["id"]: m["hp"] for m in c["materials"]}
    order = ["pallet", "glass", "drywall", "wood", "sandbag", "log", "adobe", "scrap", "container", "brick", "stone", "concrete", "rebar", "steel"]
    assert hp["pallet"] < hp["wood"] < hp["log"] < hp["scrap"] < hp["brick"] < hp["stone"] < hp["concrete"] < hp["rebar"] < hp["steel"]
    assert set(order) == set(hp)


def test_construction_items_and_tools_exist():
    out = _construction()
    from datagen.__main__ import OUT
    assert OUT / "construction.json" in out
    import json
    items = {i["id"]: i for i in json.loads(out[OUT / "items.json"])["items"]}
    for tool in ("shovel", "pickaxe", "hammer", "axe"):
        assert items[tool]["tool_kind"] == tool
    assert items["earth"]["category"] == "material"


@pytest.mark.parametrize("edit, message", [
    (lambda c: c.replace("kind: rules", "kind: rulez", 1), "kind"),
    (lambda c: c.replace("tool: axe", "tool: spoon", 1), "tool"),
    (lambda c: c.replace("shapes: [wall, halfwall, fence, door, floor]", "shapes: [wall, castle]", 1), "unknown shapes"),
    (lambda c: c.replace("layer: structure", "layer: attic", 1), "layer"),
    (lambda c: c.replace("hp: 40\ntool: axe", "hp: 0\ntool: axe", 1), "hp"),
])
def test_invalid_construction(edit, message):
    from datagen.__main__ import DOCS
    from datagen.construction import build_construction
    text = edit((DOCS / "construccion.md").read_text(encoding="utf-8"))
    with pytest.raises(DataError, match=message):
        build_construction(text)


def test_unknown_item_in_a_material_is_caught():
    from datagen.construction import build_construction, cross_check_construction
    from datagen.__main__ import DOCS
    c = build_construction((DOCS / "construccion.md").read_text(encoding="utf-8").replace("{ pallet: 1 }", "{ unobtainium: 1 }", 1))
    items = [{"id": "wood", "tool_kind": ""}]
    with pytest.raises(DataError, match="unknown item"):
        cross_check_construction(c, items)


def test_tool_kind_needs_tool_power():
    with pytest.raises(DataError, match="tool_kind"):
        item_to_runtime(_item(id="spoon", category="tool", tool_kind="shovel"))
    assert item_to_runtime(_item(id="spade", category="tool", tool_kind="shovel", tool_power=5))["tool_kind"] == "shovel"
