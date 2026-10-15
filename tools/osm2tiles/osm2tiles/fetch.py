"""Download OSM data for a bbox from the Overpass API (with a local cache)."""

from __future__ import annotations

import urllib.parse
import urllib.request
from pathlib import Path

from . import __version__
from .geo import BBox

DEFAULT_ENDPOINT = "https://overpass-api.de/api/interpreter"

QUERY_TEMPLATE = """
[out:xml][timeout:90];
(
  way["highway"]({b});
  way["building"]({b});
  relation["building"]({b});
  way["landuse"]({b});
  way["leisure"]({b});
  way["natural"]({b});
  relation["type"="multipolygon"]["natural"]({b});
  relation["type"="multipolygon"]["landuse"]({b});
  relation["type"="multipolygon"]["leisure"]({b});
  relation["type"="multipolygon"]["waterway"]({b});
  way["waterway"]({b});
  way["railway"]({b});
  way["barrier"]({b});
  way["amenity"]({b});
  way["place"="square"]({b});
  node["natural"="tree"]({b});
  node["shop"]({b});
  node["amenity"]({b});
  node["healthcare"]({b});
  node["office"]({b});
);
(._;>;);
out body;
"""


def build_query(bbox: BBox) -> str:
    b = f"{bbox.south},{bbox.west},{bbox.north},{bbox.east}"
    return QUERY_TEMPLATE.format(b=b)


def fetch(bbox: BBox, dest: Path, endpoint: str = DEFAULT_ENDPOINT, timeout: int = 120) -> Path:
    """POST the query to Overpass and save the raw XML to `dest`."""
    body = urllib.parse.urlencode({"data": build_query(bbox)}).encode()
    req = urllib.request.Request(
        endpoint, data=body,
        headers={"User-Agent": f"cepacero-osm2tiles/{__version__} (game prototype)"},
    )
    dest.parent.mkdir(parents=True, exist_ok=True)
    tmp = dest.with_suffix(".part")
    with urllib.request.urlopen(req, timeout=timeout) as resp, tmp.open("wb") as f:
        while chunk := resp.read(1 << 16):
            f.write(chunk)
    tmp.replace(dest)
    return dest
