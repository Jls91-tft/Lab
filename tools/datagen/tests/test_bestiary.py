from __future__ import annotations

import json
import textwrap

import pytest

from datagen.__main__ import SOURCE, TARGET, main
from datagen.bestiary import BestiaryError, build, parse_markdown, to_runtime


def test_real_bestiary_builds_and_has_the_mvp_cast():
    doc = build(SOURCE)
    ids = [z["id"] for z in doc["zombies"]]
    assert {"walker", "runner", "screamer", "spitter", "brute"} <= set(ids)
    mvp = {z["id"] for z in doc["zombies"] if z["mvp"]}
    assert mvp == {"walker", "runner", "screamer", "spitter", "brute"}  # 2 commons + 2 specials + 1 miniboss


def test_runtime_shape_is_jsonutility_friendly():
    doc = build(SOURCE)

    def assert_no_nested_arrays(value, path="root"):
        if isinstance(value, dict):
            for k, v in value.items():
                assert_no_nested_arrays(v, f"{path}.{k}")
        elif isinstance(value, list):
            for i, v in enumerate(value):
                assert not isinstance(v, list), f"nested array at {path}[{i}]"
                assert_no_nested_arrays(v, f"{path}[{i}]")

    assert_no_nested_arrays(doc)
    spitter = next(z for z in doc["zombies"] if z["id"] == "spitter")
    assert spitter["structure_multipliers"] == {"wood": 3.0, "metal": 0.5, "concrete": 0.25}
    assert set(spitter["damage_taken"]) == {"blunt", "sharp", "ballistic", "fire", "acid"}
    assert spitter["loot"] == [{"item": "acid_sac", "min": 1, "max": 1, "chance": 1.0}]
    brute = next(z for z in doc["zombies"] if z["id"] == "brute")
    assert json.loads(brute["ability_json"])["charge"]["stunned_by"] == ["metal"]


def test_generated_file_is_up_to_date():
    assert main(["--check"]) == 0, f"run `python -m datagen` to refresh {TARGET}"


def _entry(**overrides):
    base = yaml_entry()
    base.update(overrides)
    return base


def yaml_entry():
    return parse_markdown(textwrap.dedent("""
        ```yaml
        id: test_zombie
        name: Test
        tier: common
        mvp: false
        size_tiles: 1
        health: 10
        speed: 1
        damage: 1
        attack_interval: 1
        attack_range: 1
        structure_damage: 1
        detect_sight: 1
        detect_hearing: 1
        behavior: [chase]
        damage_taken: { fire: 2 }
        weakness: "x"
        loot: { rag: [0, 1, 0.5] }
        visual: "x"
        audio: "x"
        ```
    """))[0]


def test_defaults_are_filled_in():
    z = to_runtime(yaml_entry())
    assert z["damage_taken"]["fire"] == 2.0 and z["damage_taken"]["acid"] == 1.0
    assert z["structure_multipliers"] == {"wood": 1.0, "metal": 1.0, "concrete": 1.0}
    assert z["damage_type"] == "blunt" and z["ability_json"] == "{}"


@pytest.mark.parametrize("overrides, message", [
    ({"tier": "boss"}, "tier"),
    ({"health": 0}, "health"),
    ({"id": "BadId"}, "snake_case"),
    ({"speed": "fast"}, "speed"),
    ({"mvp": "yes"}, "mvp"),
    ({"damage_taken": {"laser": 1}}, "damage types"),
    ({"loot": {"rag": [2, 1, 0.5]}}, "loot"),
])
def test_invalid_entries_are_rejected(overrides, message):
    with pytest.raises(BestiaryError, match=message):
        to_runtime(_entry(**overrides))


def test_missing_key_is_reported():
    entry = yaml_entry()
    del entry["audio"]
    with pytest.raises(BestiaryError, match="missing 'audio'"):
        to_runtime(entry)
