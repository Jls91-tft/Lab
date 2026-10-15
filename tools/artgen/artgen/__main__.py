"""python -m artgen [--check]

Generates the placeholder art (D-018) into CepaCero/Assets/Resources/Cepa/Art/:
  atlas.png  (every sprite, 16 px per tile) · atlas.json (names and rectangles)
and a 4x preview in tools/artgen/out/preview.png (not committed).
--check fails (exit 1) if the committed atlas is out of date. When the PNG is only a
Git LFS pointer (CI checks out without LFS), it compares the JSON alone.
"""

from __future__ import annotations

import argparse
import io
import json
import sys
from pathlib import Path

from PIL import Image

from . import characters, items, tiles, ui
from .atlas import describe, pack
from .canvas import Canvas

REPO = Path(__file__).resolve().parents[3]
OUT = REPO / "CepaCero" / "Assets" / "Resources" / "Cepa" / "Art"
PREVIEW = REPO / "tools" / "artgen" / "out" / "preview.png"
LFS_POINTER = b"version https://git-lfs"


def game_ids() -> tuple[list[str], list[str]]:
    data = REPO / "data" / "generated"
    item_ids = [i["id"] for i in json.loads((data / "items.json").read_text(encoding="utf-8"))["items"]]
    bestiary = json.loads((data / "bestiary.json").read_text(encoding="utf-8"))
    zombie_ids = [z["id"] for z in bestiary["zombies"]]
    return item_ids, zombie_ids


def build_sprites() -> dict[str, Canvas]:
    item_ids, zombie_ids = game_ids()
    sprites: dict[str, Canvas] = {}
    for part in (tiles.build(), characters.build(zombie_ids), items.build(item_ids), ui.build()):
        clash = sprites.keys() & part.keys()
        if clash:
            raise ValueError(f"artgen: duplicate sprite names {sorted(clash)}")
        sprites.update(part)
    return sprites


def build_all() -> tuple[bytes, str]:
    atlas, entries = pack(build_sprites())
    png = io.BytesIO()
    atlas.save(png, format="PNG", optimize=False)
    return png.getvalue(), describe(entries, atlas)


def preview(png: bytes, scale: int = 4) -> None:
    atlas = Image.open(io.BytesIO(png))
    back = Image.new("RGBA", atlas.size, (40, 38, 46, 255))
    back.alpha_composite(atlas)
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    back.resize((atlas.width * scale, atlas.height * scale), Image.Resampling.NEAREST).save(PREVIEW)


def same_pixels(a: bytes, b: bytes) -> bool:
    ia, ib = Image.open(io.BytesIO(a)).convert("RGBA"), Image.open(io.BytesIO(b)).convert("RGBA")
    return ia.size == ib.size and ia.tobytes() == ib.tobytes()


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="artgen", description=__doc__.splitlines()[0])
    parser.add_argument("--check", action="store_true", help="fail if the committed atlas is out of date")
    args = parser.parse_args(argv)

    png, doc = build_all()
    png_path, json_path = OUT / "atlas.png", OUT / "atlas.json"
    if args.check:
        stale = []
        if not json_path.exists() or json_path.read_text(encoding="utf-8") != doc:
            stale.append(json_path)
        if not png_path.exists():
            stale.append(png_path)
        else:
            committed = png_path.read_bytes()
            if committed.startswith(LFS_POINTER):
                print("note: atlas.png is a Git LFS pointer here; compared atlas.json only")
            elif not same_pixels(committed, png):
                stale.append(png_path)
        for p in stale:
            print(f"out of date: {p.relative_to(REPO)} (run: python -m artgen)", file=sys.stderr)
        if not stale:
            print(f"atlas is up to date ({len(json.loads(doc)['sprites'])} sprites)")
        return 1 if stale else 0

    OUT.mkdir(parents=True, exist_ok=True)
    png_path.write_bytes(png)
    json_path.write_text(doc, encoding="utf-8", newline="\n")
    preview(png)
    print(f"wrote {png_path.relative_to(REPO)} and atlas.json ({len(json.loads(doc)['sprites'])} sprites); "
          f"preview in {PREVIEW.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
