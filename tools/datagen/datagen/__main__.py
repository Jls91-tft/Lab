"""python -m datagen [--check]

Regenerates data/generated/*.json from the design docs:
  docs/bestiario.md → bestiary.json · docs/objetos.md → items.json
  docs/recetas.md → recipes.json   · docs/botin.md → loot.json
--check fails (exit 1) if a committed file is out of date: use it in CI.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from .bestiary import BestiaryError, build, render
from .items import build_items, build_loot, build_recipes, cross_check

REPO = Path(__file__).resolve().parents[3]
DOCS = REPO / "docs"
OUT = REPO / "data" / "generated"
SOURCE = DOCS / "bestiario.md"
TARGET = OUT / "bestiary.json"


def build_all() -> dict[Path, str]:
    bestiary = build(SOURCE)
    items = build_items((DOCS / "objetos.md").read_text(encoding="utf-8"))
    recipes = build_recipes((DOCS / "recetas.md").read_text(encoding="utf-8"))
    loot = build_loot((DOCS / "botin.md").read_text(encoding="utf-8"))
    cross_check(items, recipes, loot, bestiary)
    return {
        TARGET: render(bestiary),
        OUT / "items.json": render({"format": "cepacero.items", "version": 1, "source": "docs/objetos.md", "items": items}),
        OUT / "recipes.json": render({"format": "cepacero.recipes", "version": 1, "source": "docs/recetas.md", "recipes": recipes}),
        OUT / "loot.json": render({"format": "cepacero.loot", "version": 1, "source": "docs/botin.md", **loot}),
    }


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(prog="datagen", description=__doc__.splitlines()[0])
    p.add_argument("--check", action="store_true", help="only verify the generated files are up to date")
    args = p.parse_args(argv)
    try:
        outputs = build_all()
    except BestiaryError as e:
        print(f"error: {e}", file=sys.stderr)
        return 2
    stale = []
    for path, text in outputs.items():
        current = path.read_text(encoding="utf-8") if path.exists() else ""
        if current == text:
            continue
        if args.check:
            stale.append(path)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text, encoding="utf-8")
            print(f"wrote {path.relative_to(REPO)}")
    if stale:
        names = ", ".join(str(s.relative_to(REPO)) for s in stale)
        print(f"error: out of date: {names}. Run: python -m datagen", file=sys.stderr)
        return 1
    if args.check:
        print(f"{len(outputs)} generated files are up to date")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
