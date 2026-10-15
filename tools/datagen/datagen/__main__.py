"""python -m datagen [--check]

Regenerates data/generated/bestiary.json from docs/bestiario.md.
--check fails (exit 1) if the committed file is out of date: use it in CI.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from .bestiary import BestiaryError, build, render

REPO = Path(__file__).resolve().parents[3]
SOURCE = REPO / "docs" / "bestiario.md"
TARGET = REPO / "data" / "generated" / "bestiary.json"


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(prog="datagen", description=__doc__.splitlines()[0])
    p.add_argument("--check", action="store_true", help="only verify the generated file is up to date")
    args = p.parse_args(argv)
    try:
        text = render(build(SOURCE))
    except BestiaryError as e:
        print(f"error: {SOURCE.relative_to(REPO)}: {e}", file=sys.stderr)
        return 2
    current = TARGET.read_text(encoding="utf-8") if TARGET.exists() else ""
    if args.check:
        if current != text:
            print(f"error: {TARGET.relative_to(REPO)} is out of date. Run: python -m datagen", file=sys.stderr)
            return 1
        print(f"{TARGET.relative_to(REPO)} is up to date")
        return 0
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    TARGET.write_text(text, encoding="utf-8")
    print(f"wrote {TARGET.relative_to(REPO)} ({text.count(chr(10))} lines)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
