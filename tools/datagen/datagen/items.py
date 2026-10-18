"""docs/objetos.md, docs/recetas.md and docs/botin.md → items, recipes and loot JSON.

Same rules as the bestiary: YAML blocks are the source of truth, the output
is JsonUtility-friendly (lists of fixed-shape records) and every cross
reference is checked (an item used anywhere must exist).
"""

from __future__ import annotations

import re

from .bestiary import DAMAGE_TYPES, BestiaryError, parse_markdown

CATEGORIES = ("material", "weapon", "tool", "food", "medical", "ammo", "sample", "misc")
STATIONS = ("hand", "workbench")
# Blocks a recipe may build. Must exist in BlockRegistry.CreateDefault() (checked by a C# test).
BUILDABLE_BLOCKS = ("wood_wall", "metal_wall", "concrete_wall", "door", "fence", "workbench", "chest", "spike_trap")
# Building kinds produced by tools/osm2tiles (rules.BUILDING_KINDS).
BUILDING_KINDS = ("generic", "residential", "commercial", "food", "hardware", "medical",
                  "police", "school", "religious", "office", "industrial", "garage")
# Tables of the furniture of a home (D-026, core FurnitureKinds).
FURNITURE_LOOT = ("kitchen", "bedroom", "bathroom", "living", "storage")
WEAPON_KEYS = {"damage", "damage_type", "range", "cooldown", "noise", "arc", "ammo"}
TOOL_KINDS = ("axe", "pickaxe", "shovel", "hammer")
ITEM_KEYS = {"id", "name", "category", "stack", "size", "weapon", "tool_power", "durability", "heal", "food", "water", "tool_kind"}
RECIPE_KEYS = {"id", "result", "result_block", "count", "ingredients", "station", "unlocked_by"}

_ID = re.compile(r"[a-z][a-z0-9_]*")


class DataError(BestiaryError):
    pass


def _check_id(value, what: str) -> str:
    if not isinstance(value, str) or not _ID.fullmatch(value):
        raise DataError(f"{what}: id '{value}' must be snake_case")
    return value


def _number(entry: dict, key: str, default, what: str, minimum=0):
    value = entry.get(key, default)
    if isinstance(value, bool) or not isinstance(value, (int, float)) or value < minimum:
        raise DataError(f"{what}: '{key}' must be a number >= {minimum}")
    return value


# ---------------------------------------------------------------- items

def item_to_runtime(entry: dict) -> dict:
    zid = _check_id(entry.get("id"), "item")
    unknown = set(entry) - ITEM_KEYS
    if unknown:
        raise DataError(f"{zid}: unknown keys {sorted(unknown)}")
    if not isinstance(entry.get("name"), str):
        raise DataError(f"{zid}: missing 'name'")
    if entry.get("category") not in CATEGORIES:
        raise DataError(f"{zid}: category must be one of {CATEGORIES}")
    stack = _number(entry, "stack", None, zid, 1) if "stack" in entry else None
    if stack is None or int(stack) != stack:
        raise DataError(f"{zid}: 'stack' must be an integer >= 1")
    size = _number(entry, "size", 1, zid, 1)
    weapon = entry.get("weapon") or {}
    if not isinstance(weapon, dict) or set(weapon) - WEAPON_KEYS:
        raise DataError(f"{zid}: weapon accepts {sorted(WEAPON_KEYS)}")
    if weapon:
        if weapon.get("damage_type", "blunt") not in DAMAGE_TYPES:
            raise DataError(f"{zid}: unknown weapon damage_type")
        if "ammo" in weapon and "arc" in weapon:
            raise DataError(f"{zid}: a weapon is either melee (arc) or ranged (ammo)")
    for bar in ("food", "water"):
        if bar in entry and (_number(entry, bar, 0, zid) > 100 or entry.get("category") != "food"):
            raise DataError(f"{zid}: '{bar}' is for food items and goes up to 100 (a full bar)")
    kind = entry.get("tool_kind", "")
    if kind and (kind not in TOOL_KINDS or _number(entry, "tool_power", 0, zid) <= 0):
        raise DataError(f"{zid}: tool_kind must be one of {TOOL_KINDS} and the item needs tool_power")
    return {
        "id": zid,
        "name": entry["name"],
        "category": entry["category"],
        "stack": int(stack),
        "size": int(size),
        "durability": int(_number(entry, "durability", 0, zid)),
        "tool_power": int(_number(entry, "tool_power", 0, zid)),
        "tool_kind": kind,
        "heal": int(_number(entry, "heal", 0, zid)),
        "food": int(_number(entry, "food", 0, zid)),
        "water": int(_number(entry, "water", 0, zid)),
        "is_weapon": bool(weapon),
        "damage": int(_number(weapon, "damage", 0, zid)),
        "damage_type": weapon.get("damage_type", "blunt"),
        "range": float(_number(weapon, "range", 0, zid)),
        "arc": float(_number(weapon, "arc", 0, zid)),
        "cooldown": float(_number(weapon, "cooldown", 0, zid)),
        "noise": float(_number(weapon, "noise", 0, zid)),
        "ammo": weapon.get("ammo", ""),
    }


# -------------------------------------------------------------- recipes

def recipe_to_runtime(entry: dict) -> dict:
    rid = _check_id(entry.get("id"), "recipe")
    unknown = set(entry) - RECIPE_KEYS
    if unknown:
        raise DataError(f"{rid}: unknown keys {sorted(unknown)}")
    has_item, has_block = "result" in entry, "result_block" in entry
    if has_item == has_block:
        raise DataError(f"{rid}: needs exactly one of 'result' or 'result_block'")
    if has_block and entry["result_block"] not in BUILDABLE_BLOCKS:
        raise DataError(f"{rid}: result_block must be one of {BUILDABLE_BLOCKS}")
    if entry.get("station", "hand") not in STATIONS:
        raise DataError(f"{rid}: station must be one of {STATIONS}")
    ingredients = entry.get("ingredients")
    if not isinstance(ingredients, dict) or not ingredients:
        raise DataError(f"{rid}: 'ingredients' must be a non-empty mapping item → count")
    out = []
    for item, count in ingredients.items():
        if isinstance(count, bool) or not isinstance(count, int) or count < 1:
            raise DataError(f"{rid}: ingredient '{item}' needs a positive integer count")
        out.append({"item": item, "count": count})
    count = entry.get("count", 1)
    if isinstance(count, bool) or not isinstance(count, int) or count < 1:
        raise DataError(f"{rid}: 'count' must be a positive integer")
    return {
        "id": rid,
        "result": entry.get("result", ""),
        "result_block": entry.get("result_block", ""),
        "count": count,
        "ingredients": out,
        "station": entry.get("station", "hand"),
        "unlocked_by": entry.get("unlocked_by", ""),
    }


# ----------------------------------------------------------------- loot

def loot_to_runtime(entries: list[dict]) -> dict:
    tables = []
    placement = {"tiles_per_container": 80, "max_containers": 6}
    seen = set()
    for entry in entries:
        kind = entry.get("kind")
        if kind == "_placement":
            placement = {
                "tiles_per_container": int(_number(entry, "tiles_per_container", 80, "_placement", 1)),
                "max_containers": int(_number(entry, "max_containers", 6, "_placement", 1)),
            }
            continue
        if kind not in BUILDING_KINDS and kind not in FURNITURE_LOOT:
            raise DataError(f"loot: kind '{kind}' is not a building kind of osm2tiles {BUILDING_KINDS} nor a furniture table {FURNITURE_LOOT}")
        if kind in seen:
            raise DataError(f"loot: duplicate kind '{kind}'")
        seen.add(kind)
        rolls = entry.get("rolls")
        if isinstance(rolls, bool) or not isinstance(rolls, int) or rolls < 1:
            raise DataError(f"loot {kind}: 'rolls' must be a positive integer")
        rows = []
        for item, spec in (entry.get("loot") or {}).items():
            if not (isinstance(spec, list) and len(spec) == 3 and 0 <= spec[0] <= spec[1] and 0 <= spec[2] <= 1):
                raise DataError(f"loot {kind}: '{item}' must be [min, max, chance] with 0 <= min <= max and 0 <= chance <= 1")
            rows.append({"item": item, "min": spec[0], "max": spec[1], "chance": float(spec[2])})
        if not rows:
            raise DataError(f"loot {kind}: empty table")
        tables.append({"kind": kind, "rolls": rolls, "loot": rows})
    missing = set(BUILDING_KINDS) - seen
    if missing:
        raise DataError(f"loot: no table for building kinds {sorted(missing)}")
    return {**placement, "tables": tables}


# ------------------------------------------------------------ cross-check

def cross_check(items: list[dict], recipes: list[dict], loot: dict, bestiary: dict) -> None:
    ids = [i["id"] for i in items]
    dupes = sorted({i for i in ids if ids.count(i) > 1})
    if dupes:
        raise DataError(f"duplicate item ids: {dupes}")
    known = set(ids)
    by_id = {i["id"]: i for i in items}

    def need(item: str, where: str) -> None:
        if item not in known:
            raise DataError(f"{where}: unknown item '{item}' (add it to docs/objetos.md)")

    for item in items:
        if item["ammo"]:
            need(item["ammo"], f"item {item['id']} ammo")
    rids = [r["id"] for r in recipes]
    if len(rids) != len(set(rids)):
        raise DataError("duplicate recipe ids")
    for r in recipes:
        if r["result"]:
            need(r["result"], f"recipe {r['id']}")
        for ing in r["ingredients"]:
            need(ing["item"], f"recipe {r['id']}")
        if r["unlocked_by"]:
            need(r["unlocked_by"], f"recipe {r['id']} unlocked_by")
            if by_id[r["unlocked_by"]]["category"] != "sample":
                raise DataError(f"recipe {r['id']}: unlocked_by must be a sample")
    for t in loot["tables"]:
        for row in t["loot"]:
            need(row["item"], f"loot {t['kind']}")
    for z in bestiary["zombies"]:
        for row in z["loot"]:
            need(row["item"], f"bestiary {z['id']} loot")


def build_items(markdown_text: str) -> list[dict]:
    return [item_to_runtime(e) for e in parse_markdown(markdown_text)]


def build_recipes(markdown_text: str) -> list[dict]:
    return [recipe_to_runtime(e) for e in parse_markdown(markdown_text)]


def build_loot(markdown_text: str) -> dict:
    return loot_to_runtime(parse_markdown(markdown_text))
