"""docs/bestiario.md (YAML blocks) → data/generated/bestiary.json.

The JSON layout is JsonUtility-friendly (no dictionaries): loot becomes a
list of objects and free-form ability blocks are kept as a JSON string.
"""

from __future__ import annotations

import json
import re
from pathlib import Path

import yaml

FORMAT = "cepacero.bestiary"
VERSION = 1
TIERS = ("common", "special", "miniboss")
DAMAGE_TYPES = ("blunt", "sharp", "ballistic", "fire", "acid")
MATERIALS = ("wood", "metal", "concrete")

_BLOCK_RE = re.compile(r"```yaml\n(.*?)```", re.S)

REQUIRED = {
    "id": str, "name": str, "tier": str, "mvp": bool, "size_tiles": (int, float),
    "health": int, "speed": (int, float), "damage": int, "attack_interval": (int, float),
    "attack_range": (int, float), "structure_damage": int, "detect_sight": (int, float),
    "detect_hearing": (int, float), "behavior": list, "damage_taken": dict,
    "weakness": str, "loot": dict, "visual": str, "audio": str,
}


class BestiaryError(ValueError):
    pass


def parse_markdown(text: str) -> list[dict]:
    blocks = _BLOCK_RE.findall(text)
    if not blocks:
        raise BestiaryError("no ```yaml blocks found")
    entries = []
    for n, block in enumerate(blocks, start=1):
        try:
            data = yaml.safe_load(block)
        except yaml.YAMLError as e:
            raise BestiaryError(f"YAML block {n}: {e}") from e
        if not isinstance(data, dict):
            raise BestiaryError(f"YAML block {n} is not a mapping")
        entries.append(data)
    return entries


def _check(entry: dict) -> None:
    zid = entry.get("id", "?")
    for key, types in REQUIRED.items():
        if key not in entry:
            raise BestiaryError(f"{zid}: missing '{key}'")
        if isinstance(entry[key], bool) and types is not bool:
            raise BestiaryError(f"{zid}: '{key}' must be {types}, not bool")
        if not isinstance(entry[key], types):
            raise BestiaryError(f"{zid}: '{key}' must be {types}, got {type(entry[key]).__name__}")
    if not re.fullmatch(r"[a-z][a-z0-9_]*", zid):
        raise BestiaryError(f"{zid}: id must be snake_case")
    if entry["tier"] not in TIERS:
        raise BestiaryError(f"{zid}: tier must be one of {TIERS}")
    if entry["health"] <= 0 or entry["speed"] < 0:
        raise BestiaryError(f"{zid}: health must be > 0 and speed >= 0")
    unknown = set(entry["damage_taken"]) - set(DAMAGE_TYPES)
    if unknown:
        raise BestiaryError(f"{zid}: unknown damage types {sorted(unknown)}")
    if entry.get("damage_type", "blunt") not in DAMAGE_TYPES:
        raise BestiaryError(f"{zid}: unknown damage_type")
    for item, spec in entry["loot"].items():
        if not (isinstance(spec, list) and len(spec) == 3):
            raise BestiaryError(f"{zid}: loot '{item}' must be [min, max, chance]")
        lo, hi, chance = spec
        if not (0 <= lo <= hi and 0 <= chance <= 1):
            raise BestiaryError(f"{zid}: loot '{item}' needs 0 <= min <= max and 0 <= chance <= 1")


def to_runtime(entry: dict) -> dict:
    _check(entry)
    taken = {t: float(entry["damage_taken"].get(t, 1.0)) for t in DAMAGE_TYPES}
    struct = {m: float(entry.get("structure_multipliers", {}).get(m, 1.0)) for m in MATERIALS}
    armor = entry.get("armor", {})
    return {
        "id": entry["id"],
        "name": entry["name"],
        "tier": entry["tier"],
        "mvp": entry["mvp"],
        "size_tiles": float(entry["size_tiles"]),
        "health": entry["health"],
        "health_per_extra_player": float(entry.get("health_per_extra_player", 0.0)),
        "speed": float(entry["speed"]),
        "damage": entry["damage"],
        "damage_type": entry.get("damage_type", "blunt"),
        "attack_interval": float(entry["attack_interval"]),
        "attack_range": float(entry["attack_range"]),
        "structure_damage": entry["structure_damage"],
        "structure_multipliers": struct,
        "detect_sight": float(entry["detect_sight"]),
        "detect_hearing": float(entry["detect_hearing"]),
        "behavior": list(entry["behavior"]),
        "damage_taken": taken,
        "armor_front_arc_degrees": float(armor.get("front_arc_degrees", 0.0)),
        "armor_front_multiplier": float(armor.get("front_multiplier", 1.0)),
        "spawn_condition": entry.get("spawn_condition", ""),
        "ability_json": json.dumps(entry.get("ability", {}), ensure_ascii=False, sort_keys=True),
        "weakness": entry["weakness"],
        "loot": [{"item": k, "min": v[0], "max": v[1], "chance": float(v[2])} for k, v in entry["loot"].items()],
        "visual": entry["visual"],
        "audio": entry["audio"],
    }


def build(markdown: Path) -> dict:
    entries = [to_runtime(e) for e in parse_markdown(markdown.read_text(encoding="utf-8"))]
    ids = [e["id"] for e in entries]
    dupes = sorted({i for i in ids if ids.count(i) > 1})
    if dupes:
        raise BestiaryError(f"duplicate ids: {dupes}")
    return {"format": FORMAT, "version": VERSION, "source": "docs/bestiario.md", "zombies": entries}


def render(doc: dict) -> str:
    return json.dumps(doc, ensure_ascii=False, indent=2) + "\n"
