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
    assert len(outputs) == 4


def test_weapons_are_flattened():
    pistol = item_to_runtime(_item(id="pistol", category="weapon", stack=1,
                                   weapon={"damage": 35, "damage_type": "ballistic", "range": 12, "ammo": "pistol_ammo"}))
    assert pistol["is_weapon"] and pistol["ammo"] == "pistol_ammo" and pistol["arc"] == 0.0
    assert item_to_runtime(_item())["is_weapon"] is False


@pytest.mark.parametrize("overrides, message", [
    ({"category": "gadget"}, "category"),
    ({"stack": 0}, "stack"),
    ({"stack": 2.5}, "stack"),
    ({"weight": 3}, "unknown keys"),
    ({"weapon": {"damage": 1, "laser": 2}}, "weapon accepts"),
    ({"weapon": {"damage": 1, "arc": 90, "ammo": "x"}}, "melee"),
    ({"id": "Bad"}, "snake_case"),
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
