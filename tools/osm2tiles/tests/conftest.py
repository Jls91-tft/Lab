"""Synthetic OSM fixture (not real OSM data) laid out in metres from the SW corner."""

from __future__ import annotations

import math
from pathlib import Path

import pytest

from osm2tiles.geo import EARTH_RADIUS_M, BBox

SOUTH, WEST = 41.6500, -0.9100
M_PER_DEG_LAT = math.pi * EARTH_RADIUS_M / 180
M_PER_DEG_LON = M_PER_DEG_LAT * math.cos(math.radians(41.6505))
BBOX = BBox(SOUTH, WEST, SOUTH + 120 / M_PER_DEG_LAT, WEST + 130 / M_PER_DEG_LON)


class _Builder:
    def __init__(self):
        self.nodes: list[str] = []
        self.ways: list[str] = []
        self.relations: list[str] = []
        self._next = 1

    def _id(self) -> int:
        self._next += 1
        return self._next

    @staticmethod
    def _tags(tags: dict[str, str]) -> str:
        return "".join(f'<tag k="{k}" v="{v}"/>' for k, v in tags.items())

    def node(self, x: float, y: float, tags: dict[str, str] | None = None) -> int:
        nid = self._id()
        lat, lon = SOUTH + y / M_PER_DEG_LAT, WEST + x / M_PER_DEG_LON
        self.nodes.append(f'<node id="{nid}" lat="{lat:.8f}" lon="{lon:.8f}">{self._tags(tags or {})}</node>')
        return nid

    def way(self, points: list[tuple[float, float]], tags: dict[str, str], closed: bool = False) -> int:
        refs = [self.node(x, y) for x, y in points]
        if closed:
            refs.append(refs[0])
        wid = self._id()
        nds = "".join(f'<nd ref="{r}"/>' for r in refs)
        self.ways.append(f'<way id="{wid}">{nds}{self._tags(tags)}</way>')
        return wid

    def rect(self, x0, y0, x1, y1, tags) -> int:
        return self.way([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], tags, closed=True)

    def multipolygon(self, outer: int, inner: int, tags: dict[str, str]) -> None:
        rid = self._id()
        members = f'<member type="way" ref="{outer}" role="outer"/><member type="way" ref="{inner}" role="inner"/>'
        tags = {"type": "multipolygon", **tags}
        self.relations.append(f'<relation id="{rid}">{members}{self._tags(tags)}</relation>')

    def xml(self) -> str:
        body = "\n".join(self.nodes + self.ways + self.relations)
        return f'<?xml version="1.0"?>\n<osm version="0.6"><meta osm_base="2026-10-15T00:00:00Z"/>\n{body}\n</osm>\n'


@pytest.fixture
def synthetic_osm(tmp_path: Path) -> Path:
    b = _Builder()
    b.way([(-5, 50), (135, 50)], {"highway": "residential"})                # E-W street
    b.rect(10, 65, 40, 95, {"building": "yes", "building:levels": "2", "building:levels:underground": "1", "roof:material": "metal"})                            # A (north of street)
    b.rect(40, 65, 70, 95, {"building": "apartments"})                     # B shares a wall with A
    b.node(25, 80, {"shop": "supermarket", "name": "Brand X"})             # inside A
    b.rect(80, 10, 110, 40, {"building": "hospital"})                      # C (south of street)
    b.rect(10, 5, 60, 40, {"leisure": "park"})
    b.node(31, 21, {"natural": "tree"})                                     # tile centre
    outer = b.rect(85, 65, 125, 110, {})
    inner = b.rect(98, 80, 110, 92, {})
    b.multipolygon(outer, inner, {"natural": "water"})                     # pond with island
    b.way([(79, 60), (79, 105)], {"barrier": "fence"})                 # tile centre
    path = tmp_path / "synthetic.osm"
    path.write_text(b.xml(), encoding="utf-8")
    return path
