"""Projection from lat/lon to tile coordinates."""

from __future__ import annotations

import math
from dataclasses import dataclass

EARTH_RADIUS_M = 6_371_008.8


@dataclass(frozen=True)
class BBox:
    """Bounding box in Overpass order: south, west, north, east."""

    south: float
    west: float
    north: float
    east: float

    @classmethod
    def parse(cls, text: str) -> "BBox":
        parts = [float(p) for p in text.split(",")]
        if len(parts) != 4:
            raise ValueError("bbox must be 'south,west,north,east'")
        bbox = cls(*parts)
        if bbox.south >= bbox.north or bbox.west >= bbox.east:
            raise ValueError("bbox must satisfy south < north and west < east")
        return bbox

    def as_list(self) -> list[float]:
        return [self.south, self.west, self.north, self.east]


class Projector:
    """Local equirectangular projection anchored at the bbox south-west corner.

    Error is negligible (< 0.1 %) for areas of a few kilometres, which is all
    this tool targets. Output y grows northwards, matching Unity's Y axis.
    """

    def __init__(self, bbox: BBox, meters_per_tile: float, rotation_deg: float = 0.0):
        self.bbox = bbox
        self.rotation_deg = rotation_deg
        self._cos = math.cos(math.radians(rotation_deg))
        self._sin = math.sin(math.radians(rotation_deg))
        self.meters_per_tile = meters_per_tile
        lat0 = math.radians((bbox.south + bbox.north) / 2)
        self._m_per_deg_lat = math.pi * EARTH_RADIUS_M / 180
        self._m_per_deg_lon = self._m_per_deg_lat * math.cos(lat0)
        self.width_m = (bbox.east - bbox.west) * self._m_per_deg_lon
        self.height_m = (bbox.north - bbox.south) * self._m_per_deg_lat
        # The epsilon keeps exact multiples (120 m / 2 m) from rounding up a tile.
        self.width = max(1, math.ceil(self.width_m / meters_per_tile - 1e-6))
        self.height = max(1, math.ceil(self.height_m / meters_per_tile - 1e-6))

    def to_tiles(self, lat: float, lon: float) -> tuple[float, float]:
        mx = (lon - self.bbox.west) * self._m_per_deg_lon
        my = (lat - self.bbox.south) * self._m_per_deg_lat
        if self.rotation_deg:
            # Turned about the middle of the area (D-027): the dominant street grid ends up along the axes, so most buildings are rectangles.
            dx, dy = mx - self.width_m / 2, my - self.height_m / 2
            mx = self.width_m / 2 + dx * self._cos - dy * self._sin
            my = self.height_m / 2 + dx * self._sin + dy * self._cos
        return mx / self.meters_per_tile, my / self.meters_per_tile

    def meters(self, m: float) -> float:
        return m / self.meters_per_tile

    @property
    def area_km2(self) -> float:
        return self.width_m * self.height_m / 1e6


def dominant_rotation(data, bbox: BBox) -> float:
    """The turn (degrees, -45..45) that puts most building walls along the axes: the commonest wall direction, weighted by length."""
    lat0 = math.radians((bbox.south + bbox.north) / 2)
    m_lat = math.pi * EARTH_RADIUS_M / 180
    m_lon = m_lat * math.cos(lat0)
    bins = [0.0] * 90
    for way in data.ways.values():
        if "building" not in way.tags:
            continue
        pts = [(data.nodes[r].lon * m_lon, data.nodes[r].lat * m_lat) for r in way.refs if r in data.nodes]
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            length = math.hypot(x1 - x0, y1 - y0)
            if length < 3:
                continue
            angle = math.degrees(math.atan2(y1 - y0, x1 - x0)) % 90.0
            bins[int(angle) % 90] += length
    if not any(bins):
        return 0.0
    smooth = [bins[(i - 2) % 90] + 2 * bins[(i - 1) % 90] + 3 * bins[i] + 2 * bins[(i + 1) % 90] + bins[(i + 2) % 90] for i in range(90)]
    best = max(range(90), key=lambda i: smooth[i]) + 0.5
    return -(best if best <= 45 else best - 90)                      # turn the wall direction onto the nearest axis
