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

    def __init__(self, bbox: BBox, meters_per_tile: float):
        self.bbox = bbox
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
        x = (lon - self.bbox.west) * self._m_per_deg_lon / self.meters_per_tile
        y = (lat - self.bbox.south) * self._m_per_deg_lat / self.meters_per_tile
        return x, y

    def meters(self, m: float) -> float:
        return m / self.meters_per_tile

    @property
    def area_km2(self) -> float:
        return self.width_m * self.height_m / 1e6
