"""python -m spritegen [--out DIR] [--check] [--preview]

Generates the 32 px world art (D-021) into CepaCero/Assets/Resources/Cepa/Art32/:
  atlas.png  (ground, blocks, decoration, marks, players and zombies) · atlas.json (names, rectangles, pivots)
and a 3x preview in tools/spritegen/out/preview.png (not committed).
--check fails (exit 1) if the committed atlas is out of date. When the PNG is only a Git LFS
pointer (CI checks out without LFS) it compares the JSON alone; when nothing is committed yet it passes.
"""

from __future__ import annotations

import argparse
import io
import json
import sys
from pathlib import Path

from PIL import Image

from . import brute, characters, decor, palette, structures, world
from .atlas import describe, pack

REPO = Path(__file__).resolve().parents[3]
OUT = REPO / "CepaCero" / "Assets" / "Resources" / "Cepa" / "Art32"
PREVIEW = REPO / "tools" / "spritegen" / "out" / "preview.png"
LFS_POINTER = b"version https://git-lfs"


def zombie_ids() -> list[str]:
    bestiary = json.loads((REPO / "data" / "generated" / "bestiary.json").read_text(encoding="utf-8"))
    return [z["id"] for z in bestiary["zombies"]]


def build_images() -> dict[str, Image.Image]:
    images: dict[str, Image.Image] = {}
    parts = [
        world.ground_tiles(),
        world.block_images(),
        structures.build(),
        {n: s.image() for n, s in decor.build().items()},
        {n: s.image() for n, s in characters.build(zombie_ids()).items()},
        {n: s.image() for n, s in brute.build().items()},
    ]
    for part in parts:
        clash = images.keys() & part.keys()
        if clash:
            raise ValueError(f"spritegen: duplicate sprite names {sorted(clash)}")
        images.update(part)
    return {n: palette.apply(im) for n, im in images.items()}


def build_all() -> tuple[bytes, str]:
    atlas, entries = pack(build_images())
    png = io.BytesIO()
    atlas.save(png, format="PNG", optimize=False)
    return png.getvalue(), describe(entries, atlas)


def preview(png: bytes, scale: int = 3) -> None:
    atlas = Image.open(io.BytesIO(png)).convert("RGBA")
    back = Image.new("RGBA", atlas.size, (40, 38, 46, 255))
    back.alpha_composite(atlas)
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    back.resize((atlas.width * scale, atlas.height * scale), Image.Resampling.NEAREST).save(PREVIEW)


def check(png: bytes, text: str, out: Path) -> int:
    json_path, png_path = out / "atlas.json", out / "atlas.png"
    if not json_path.exists():
        print("spritegen: no committed atlas yet (nothing to check).")
        return 0
    if json_path.read_text(encoding="utf-8") != text:
        print("spritegen: atlas.json is out of date: run `python -m spritegen`.", file=sys.stderr)
        return 1
    committed = png_path.read_bytes() if png_path.exists() else b""
    if committed.startswith(LFS_POINTER):
        return 0
    same = Image.open(io.BytesIO(committed)).convert("RGBA").tobytes() == Image.open(io.BytesIO(png)).convert("RGBA").tobytes()
    if not same:
        print("spritegen: atlas.png is out of date: run `python -m spritegen`.", file=sys.stderr)
        return 1
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="spritegen", description=__doc__.split("\n")[0])
    parser.add_argument("--out", type=Path, default=OUT, help="output folder (default: the Unity Resources folder)")
    parser.add_argument("--check", action="store_true", help="fail if the committed atlas is out of date")
    parser.add_argument("--preview", action="store_true", help="also write a 3x preview to tools/spritegen/out/")
    args = parser.parse_args(argv)
    png, text = build_all()
    if args.check:
        return check(png, text, args.out)
    args.out.mkdir(parents=True, exist_ok=True)
    (args.out / "atlas.png").write_bytes(png)
    (args.out / "atlas.json").write_text(text, encoding="utf-8", newline="\n")
    if args.preview:
        preview(png)
    print(f"spritegen: {len(json.loads(text)['sprites'])} sprites -> {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
