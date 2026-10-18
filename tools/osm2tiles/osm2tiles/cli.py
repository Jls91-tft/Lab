"""Command line: python -m osm2tiles --zone delicias-test"""

from __future__ import annotations

import argparse
import json
import sys
import time
from collections import Counter
from pathlib import Path

from . import rules
from .export import to_document, write_json, write_preview
from .fetch import DEFAULT_ENDPOINT, fetch
from .geo import BBox, Projector, dominant_rotation
from .osm import parse_osm
from .rasterize import build_map

TOOL_DIR = Path(__file__).resolve().parent.parent
ZONES_FILE = TOOL_DIR / "zones.json"
MAX_AREA_KM2 = 4.0  # be gentle with the public Overpass servers


def load_zones() -> dict:
    return json.loads(ZONES_FILE.read_text(encoding="utf-8"))


def parse_args(argv: list[str] | None) -> argparse.Namespace:
    p = argparse.ArgumentParser(prog="osm2tiles", description="OpenStreetMap → Cepa Cero tile map (cepacero.map v1)")
    src = p.add_mutually_exclusive_group()
    src.add_argument("--zone", help="preset from zones.json (e.g. delicias-test)")
    src.add_argument("--bbox", help="south,west,north,east in degrees")
    p.add_argument("--name", help="output name (default: zone name or 'map')")
    p.add_argument("--input", type=Path, help="read a local .osm file instead of downloading")
    p.add_argument("--meters-per-tile", type=float, default=2.0, help="tile size in metres (default 2)")
    p.add_argument("--rotate", default=None, help="turn the map this many degrees, or 'auto' to put the main building grid along the axes (D-027)")
    p.add_argument("--out", type=Path, default=TOOL_DIR / "out", help="output folder")
    p.add_argument("--cache", type=Path, default=TOOL_DIR / "cache", help="download cache folder")
    p.add_argument("--refresh", action="store_true", help="ignore the cache and download again")
    p.add_argument("--endpoint", default=DEFAULT_ENDPOINT, help="Overpass API endpoint")
    p.add_argument("--preview-scale", type=int, default=3, help="pixels per tile in the PNG")
    p.add_argument("--force", action="store_true", help=f"allow areas larger than {MAX_AREA_KM2} km²")
    p.add_argument("--list-zones", action="store_true", help="list presets in zones.json and exit")
    return p.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    if args.list_zones:
        for name, z in load_zones().items():
            print(f"{name:20} {z['bbox']}  {z.get('description', '')}")
        return 0
    if not (args.zone or args.bbox):
        print("error: one of --zone or --bbox is required", file=sys.stderr)
        return 2
    if args.meters_per_tile <= 0:
        print("error: --meters-per-tile must be > 0", file=sys.stderr)
        return 2

    if args.zone:
        zones = load_zones()
        if args.zone not in zones:
            print(f"error: unknown zone '{args.zone}'. Available: {', '.join(zones)}", file=sys.stderr)
            return 2
        bbox = BBox(*zones[args.zone]["bbox"])
        name = args.name or args.zone
    else:
        bbox = BBox.parse(args.bbox)
        name = args.name or "map"

    rotate_arg = args.rotate if args.rotate is not None else (zones[args.zone].get("rotate") if args.zone else None)
    rotation = 0.0
    if rotate_arg == "auto":
        rotation = None                                        # worked out below, once the data is parsed
    elif rotate_arg is not None:
        rotation = float(rotate_arg)
    proj = Projector(bbox, args.meters_per_tile, rotation or 0.0)
    if proj.area_km2 > MAX_AREA_KM2 and not args.force:
        print(f"error: area is {proj.area_km2:.2f} km² (> {MAX_AREA_KM2}). Use --force if you mean it.", file=sys.stderr)
        return 2

    if args.input:
        osm_path = args.input
    else:
        osm_path = args.cache / f"{name}.osm"
        if args.refresh or not osm_path.exists():
            print(f"Downloading {proj.area_km2:.2f} km² from {args.endpoint} …")
            try:
                fetch(bbox, osm_path, args.endpoint)
            except OSError as e:  # URLError, timeouts, HTTP 429/504 from a busy server
                print(f"error: download failed ({e}).\n"
                      "  Retry later, try another --endpoint, or export the area from openstreetmap.org "
                      "and pass it with --input file.osm", file=sys.stderr)
                return 1
        else:
            print(f"Using cached {osm_path}")

    t0 = time.perf_counter()
    data = parse_osm(osm_path)
    if rotation is None:
        rotation = dominant_rotation(data, bbox)
        proj = Projector(bbox, args.meters_per_tile, rotation)
        print(f"Turning the map {rotation:.0f} degrees so the main street grid runs along the axes.")
    tm = build_map(data, proj)
    doc = to_document(tm, name, bbox, args.meters_per_tile, data.timestamp)
    json_path = args.out / f"{name}.map.json"
    png_path = args.out / f"{name}.preview.png"
    write_json(doc, json_path)
    write_preview(tm, png_path, args.preview_scale, title=name)

    g = Counter(tm.ground)
    k = Counter(b.kind for b in tm.buildings)
    print(f"{name}: {tm.width}x{tm.height} tiles at {args.meters_per_tile} m/tile "
          f"({proj.width_m:.0f}x{proj.height_m:.0f} m) in {time.perf_counter() - t0:.1f}s")
    print("  ground:", ", ".join(f"{rules.GROUND[i]} {100 * n / len(tm.ground):.0f}%" for i, n in g.most_common()))
    print(f"  buildings: {len(tm.buildings)} ({', '.join(f'{kk} {n}' for kk, n in k.most_common())})")
    print(f"  doors: {sum(len(b.doors) // 2 for b in tm.buildings)}  trees: {tm.blocks.count(rules.B['tree'])}")
    print(f"  -> {json_path}\n  -> {png_path}")
    return 0
