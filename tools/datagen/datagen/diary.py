"""docs/lore.md (YAML blocks) → diary.json, and every player-facing text → loc/strings.csv."""

from __future__ import annotations

import csv
import io
import re
from pathlib import Path

from .bestiary import parse_markdown
from .items import BUILDING_KINDS, DataError

DIARY_KEYS = {"id", "source", "trigger", "text"}
_TRIGGER = re.compile(r"^(match_start|first_container|night_started:[1-9]\d*|night_survived:[1-9]\d*|building_kind:[a-z_]+|sample:[a-z][a-z0-9_]*)$")


def build_diary(markdown_text: str, items: list[dict]) -> list[dict]:
    samples = {i["id"] for i in items if i["category"] == "sample"}
    entries = []
    for e in parse_markdown(markdown_text):
        eid = e.get("id")
        if not isinstance(eid, str) or not re.fullmatch(r"[a-z][a-z0-9_]*", eid):
            raise DataError(f"diary: id '{eid}' must be snake_case")
        unknown = set(e) - DIARY_KEYS
        if unknown or DIARY_KEYS - set(e):
            raise DataError(f"diary {eid}: needs exactly {sorted(DIARY_KEYS)}")
        trigger = e["trigger"]
        if not isinstance(trigger, str) or not _TRIGGER.match(trigger):
            raise DataError(f"diary {eid}: unknown trigger '{trigger}'")
        kind, _, arg = trigger.partition(":")
        if kind == "building_kind" and arg not in BUILDING_KINDS:
            raise DataError(f"diary {eid}: building kind '{arg}' does not exist")
        if kind == "sample" and arg not in samples:
            raise DataError(f"diary {eid}: '{arg}' is not a sample item")
        entries.append({"id": eid, "source": str(e["source"]).strip(), "trigger": trigger, "text": str(e["text"]).strip()})
    ids = [e["id"] for e in entries]
    if len(ids) != len(set(ids)):
        raise DataError("diary: duplicate ids")
    return entries


def localization_rows(bestiary: dict, items: list[dict], diary: list[dict], construction: dict | None = None) -> list[tuple[str, str]]:
    """(key, Spanish text) for every string the player can see. Keys are stable ids."""
    rows = []
    for z in bestiary["zombies"]:
        rows.append((f"zombie.{z['id']}.name", z["name"]))
        rows.append((f"zombie.{z['id']}.weakness", z["weakness"]))
    for i in items:
        rows.append((f"item.{i['id']}.name", i["name"]))
    for d in diary:
        rows.append((f"diary.{d['id']}.source", d["source"]))
        rows.append((f"diary.{d['id']}.text", d["text"]))
    for group in ("materials", "shapes", "roofs", "naturals"):
        for e in (construction or {}).get(group, []):
            rows.append((f"build.{group[:-1]}.{e['id']}.name", e["name"]))
    return rows


def render_strings_csv(rows: list[tuple[str, str]], translations_dir: Path) -> tuple[str, dict[str, int]]:
    """CSV with one column per language. Translations come from docs/traducciones/<lang>.csv
    (key,text) so regenerating never loses them. Returns the CSV and missing counts per language."""
    languages = sorted(p.stem for p in translations_dir.glob("*.csv")) if translations_dir.exists() else []
    translated: dict[str, dict[str, str]] = {}
    for lang in languages:
        with (translations_dir / f"{lang}.csv").open(encoding="utf-8", newline="") as f:
            translated[lang] = {r["key"]: r["text"] for r in csv.DictReader(f) if r.get("key")}
    out = io.StringIO()
    writer = csv.writer(out, lineterminator="\n", quoting=csv.QUOTE_ALL)
    writer.writerow(["key", "es", *languages])
    missing = {lang: 0 for lang in languages}
    for key, es in rows:
        values = []
        for lang in languages:
            text = translated[lang].get(key, "")
            missing[lang] += 0 if text else 1
            values.append(text)
        writer.writerow([key, es, *values])
    return out.getvalue(), missing
