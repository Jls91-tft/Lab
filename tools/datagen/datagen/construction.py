"""docs/construccion.md → construction.json (D-022).

Materials, shapes, roofs and diggable ground are declared as data; the blocks of the game are the
combinations (`wall_brick`, `door_wood`…), so the catalogue grows without writing blocks by hand.
Costs, health and open variants are computed here; the C# core only reads the result.
"""

from __future__ import annotations

import math

from .bestiary import parse_markdown
from .items import TOOL_KINDS, DataError, _check_id, _number

LAYERS = ("floor", "structure", "roof", "object")
CONNECTS = ("", "up", "down")
KINDS = ("rules", "shape", "material", "roof", "natural")
SHAPE_KEYS = {"kind", "id", "name", "layer", "units", "hp_factor", "solid", "see_through", "opens", "width", "height", "connects"}
MATERIAL_KEYS = {"kind", "id", "name", "ingredients", "hp", "tool", "noise", "build_seconds", "paintable", "see_through", "shapes"}
ROOF_KEYS = {"kind", "id", "name", "ingredients", "hp"}
NATURAL_KEYS = {"kind", "id", "name", "hp", "tool", "noise", "drops"}
RULES_KEYS = {"kind", "wrong_tool_factor", "dismantle_refund", "dig_noise"}
MAX_HP = 60000


def _round(x: float) -> int:
    return int(math.floor(x + 0.5))        # half away from zero, like the game (CLAUDE.md)


def _check_keys(entry: dict, allowed: set, what: str) -> None:
    unknown = set(entry) - allowed
    if unknown:
        raise DataError(f"{what}: unknown keys {sorted(unknown)}")


def _bool(entry: dict, key: str, default: bool, what: str) -> bool:
    value = entry.get(key, default)
    if not isinstance(value, bool):
        raise DataError(f"{what}: '{key}' must be true or false")
    return value


def _name(entry: dict, what: str) -> str:
    if not isinstance(entry.get("name"), str) or not entry["name"].strip():
        raise DataError(f"{what}: missing 'name'")
    return entry["name"]


def _mapping(entry: dict, key: str, what: str, required: bool = True) -> list[dict]:
    value = entry.get(key)
    if value is None and not required:
        return []
    if not isinstance(value, dict) or not value:
        raise DataError(f"{what}: '{key}' must be a non-empty mapping item → count")
    out = []
    for item, count in value.items():
        if isinstance(count, bool) or not isinstance(count, int) or count < 1:
            raise DataError(f"{what}: '{key}' item '{item}' needs a positive integer count")
        out.append({"item": item, "count": count})
    return out


def _tool(entry: dict, what: str) -> str:
    if entry.get("tool") not in TOOL_KINDS:
        raise DataError(f"{what}: 'tool' must be one of {TOOL_KINDS}")
    return entry["tool"]


def _hp(entry: dict, what: str) -> int:
    hp = _number(entry, "hp", None, what, 1) if "hp" in entry else None
    if hp is None or int(hp) != hp or hp > MAX_HP:
        raise DataError(f"{what}: 'hp' must be an integer between 1 and {MAX_HP}")
    return int(hp)


def rules_to_runtime(entry: dict) -> dict:
    _check_keys(entry, RULES_KEYS, "rules")
    out = {
        "wrong_tool_factor": float(_number(entry, "wrong_tool_factor", 0.25, "rules", 0)),
        "dismantle_refund": float(_number(entry, "dismantle_refund", 0.5, "rules", 0)),
        "dig_noise": float(_number(entry, "dig_noise", 9, "rules", 0)),
    }
    if out["wrong_tool_factor"] > 1 or out["dismantle_refund"] > 1:
        raise DataError("rules: wrong_tool_factor and dismantle_refund are shares between 0 and 1")
    return out


def shape_to_runtime(entry: dict) -> dict:
    sid = _check_id(entry.get("id"), "shape")
    _check_keys(entry, SHAPE_KEYS, f"shape {sid}")
    if entry.get("layer") not in LAYERS:
        raise DataError(f"shape {sid}: layer must be one of {LAYERS}")
    if entry.get("connects", "") not in CONNECTS:
        raise DataError(f"shape {sid}: connects must be one of {CONNECTS}")
    width = _number(entry, "width", 1, f"shape {sid}", 1)
    height = _number(entry, "height", 1, f"shape {sid}", 1)
    if int(width) != width or int(height) != height:
        raise DataError(f"shape {sid}: width and height are whole tiles")
    return {
        "id": sid,
        "name": _name(entry, f"shape {sid}"),
        "layer": entry["layer"],
        "units": float(_number(entry, "units", None, f"shape {sid}", 0.01)) if "units" in entry else _fail(f"shape {sid}: missing 'units'"),
        "hp_factor": float(_number(entry, "hp_factor", 1, f"shape {sid}", 0.01)),
        "solid": _bool(entry, "solid", True, f"shape {sid}"),
        "see_through": _bool(entry, "see_through", False, f"shape {sid}"),
        "opens": _bool(entry, "opens", False, f"shape {sid}"),
        "width": int(width),
        "height": int(height),
        "connects": entry.get("connects", ""),
    }


def _fail(message: str):
    raise DataError(message)


def material_to_runtime(entry: dict) -> dict:
    mid = _check_id(entry.get("id"), "material")
    _check_keys(entry, MATERIAL_KEYS, f"material {mid}")
    shapes = entry.get("shapes")
    if not isinstance(shapes, list) or not shapes or len(set(shapes)) != len(shapes):
        raise DataError(f"material {mid}: 'shapes' must be a list of distinct shape ids")
    return {
        "id": mid,
        "name": _name(entry, f"material {mid}"),
        "ingredients": _mapping(entry, "ingredients", f"material {mid}"),
        "hp": _hp(entry, f"material {mid}"),
        "tool": _tool(entry, f"material {mid}"),
        "noise": float(_number(entry, "noise", 0, f"material {mid}")),
        "build_seconds": float(_number(entry, "build_seconds", 1, f"material {mid}", 0.05)),
        "paintable": _bool(entry, "paintable", False, f"material {mid}"),
        "see_through": _bool(entry, "see_through", False, f"material {mid}"),
        "shapes": shapes,
    }


def roof_to_runtime(entry: dict) -> dict:
    rid = _check_id(entry.get("id"), "roof")
    _check_keys(entry, ROOF_KEYS, f"roof {rid}")
    return {"id": rid, "name": _name(entry, f"roof {rid}"), "ingredients": _mapping(entry, "ingredients", f"roof {rid}"), "hp": _hp(entry, f"roof {rid}")}


def natural_to_runtime(entry: dict) -> dict:
    nid = _check_id(entry.get("id"), "natural")
    _check_keys(entry, NATURAL_KEYS, f"natural {nid}")
    return {
        "id": nid,
        "name": _name(entry, f"natural {nid}"),
        "hp": _hp(entry, f"natural {nid}"),
        "tool": _tool(entry, f"natural {nid}"),
        "noise": float(_number(entry, "noise", 0, f"natural {nid}")),
        "drops": _mapping(entry, "drops", f"natural {nid}"),
    }


def _block(**kw) -> dict:
    base = {"key": "", "shape": "", "material": "", "layer": "structure", "hp": 1, "solid": True, "see_through": False, "width": 1, "height": 1,
            "tool": "", "noise": 0.0, "paintable": False, "build_seconds": 1.0, "connects": "", "toggle_to": "", "is_open_variant": False,
            "cost": [], "drops": []}
    base.update(kw)
    return base


def derive_blocks(shapes: list[dict], materials: list[dict], roofs: list[dict], naturals: list[dict]) -> list[dict]:
    """Every block the construction data defines, in a stable order: materials, then shapes, then roofs, then ground."""
    by_shape = {s["id"]: s for s in shapes}
    blocks: list[dict] = []
    for m in materials:
        for shape in shapes:                                         # shape order is the declaration order in the doc
            if shape["id"] not in m["shapes"]:
                continue
            key = f"{shape['id']}_{m['id']}"
            cost = [{"item": i["item"], "count": max(1, math.ceil(shape["units"] * i["count"] - 1e-9))} for i in m["ingredients"]]
            common = dict(shape=shape["id"], material=m["id"], layer=shape["layer"], hp=max(1, _round(m["hp"] * shape["hp_factor"])),
                          see_through=shape["see_through"] or m["see_through"], width=shape["width"], height=shape["height"],
                          tool=m["tool"], noise=m["noise"], paintable=m["paintable"], build_seconds=round(m["build_seconds"] * shape["units"], 3),
                          connects=shape["connects"])
            blocks.append(_block(key=key, solid=shape["solid"], toggle_to=key + "_open" if shape["opens"] else "", cost=cost, **common))
            if shape["opens"]:
                blocks.append(_block(key=key + "_open", solid=False, toggle_to=key, is_open_variant=True, cost=[], **common))
    for r in roofs:
        blocks.append(_block(key=f"roof_{r['id']}", shape="roof", material=r["id"], layer="roof", hp=r["hp"], solid=False, see_through=False,
                             cost=[{"item": i["item"], "count": i["count"]} for i in r["ingredients"]], build_seconds=0.5))
    for n in naturals:
        blocks.append(_block(key=n["id"], shape="natural", material=n["id"], layer="structure", hp=n["hp"], solid=True, tool=n["tool"],
                             noise=n["noise"], drops=n["drops"]))
    keys = [b["key"] for b in blocks]
    dupes = sorted({k for k in keys if keys.count(k) > 1})
    if dupes:
        raise DataError(f"construction: duplicate block keys {dupes}")
    del by_shape
    return blocks


def build_construction(markdown_text: str) -> dict:
    entries = parse_markdown(markdown_text)
    rules = None
    groups = {"shape": [], "material": [], "roof": [], "natural": []}
    makers = {"shape": shape_to_runtime, "material": material_to_runtime, "roof": roof_to_runtime, "natural": natural_to_runtime}
    for e in entries:
        kind = e.get("kind")
        if kind not in KINDS:
            raise DataError(f"construction: kind '{kind}' must be one of {KINDS}")
        if kind == "rules":
            if rules is not None:
                raise DataError("construction: more than one 'rules' block")
            rules = rules_to_runtime(e)
        else:
            groups[kind].append(makers[kind](e))
    if rules is None:
        raise DataError("construction: missing the 'rules' block")
    for kind, group in groups.items():
        ids = [g["id"] for g in group]
        if len(ids) != len(set(ids)):
            raise DataError(f"construction: duplicate {kind} ids")
        if not group:
            raise DataError(f"construction: no '{kind}' blocks")
    shape_ids = {s["id"] for s in groups["shape"]}
    for m in groups["material"]:
        unknown = [s for s in m["shapes"] if s not in shape_ids]
        if unknown:
            raise DataError(f"material {m['id']}: unknown shapes {unknown}")
    blocks = derive_blocks(groups["shape"], groups["material"], groups["roof"], groups["natural"])
    return {"rules": rules, "shapes": groups["shape"], "materials": groups["material"], "roofs": groups["roof"],
            "naturals": groups["natural"], "blocks": blocks}


def cross_check_construction(construction: dict, items: list[dict]) -> None:
    known = {i["id"] for i in items}

    def need(item: str, where: str) -> None:
        if item not in known:
            raise DataError(f"{where}: unknown item '{item}' (add it to docs/objetos.md)")

    for group in ("materials", "roofs"):
        for e in construction[group]:
            for ing in e["ingredients"]:
                need(ing["item"], f"{group[:-1]} {e['id']}")
    for n in construction["naturals"]:
        for d in n["drops"]:
            need(d["item"], f"natural {n['id']} drops")
    kinds = {i["tool_kind"] for i in items if i["tool_kind"]}
    used = {m["tool"] for m in construction["materials"]} | {n["tool"] for n in construction["naturals"]}
    missing = used - kinds
    if missing:
        raise DataError(f"construction: no item of tool_kind {sorted(missing)} exists, so those materials could never be broken properly")
