"""Minimal OpenStreetMap XML reader (no third-party dependencies)."""

from __future__ import annotations

import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path


@dataclass
class Node:
    lat: float
    lon: float
    tags: dict[str, str]


@dataclass
class Way:
    refs: list[int]
    tags: dict[str, str]

    @property
    def is_closed(self) -> bool:
        return len(self.refs) >= 4 and self.refs[0] == self.refs[-1]


@dataclass
class Member:
    type: str
    ref: int
    role: str


@dataclass
class Relation:
    members: list[Member]
    tags: dict[str, str]


@dataclass
class OsmData:
    nodes: dict[int, Node] = field(default_factory=dict)
    ways: dict[int, Way] = field(default_factory=dict)
    relations: dict[int, Relation] = field(default_factory=dict)
    timestamp: str = ""


def _tags(elem: ET.Element) -> dict[str, str]:
    return {t.get("k"): t.get("v") for t in elem.findall("tag")}


def parse_osm(path: Path) -> OsmData:
    """Parse an .osm XML file (Overpass output or openstreetmap.org export)."""
    data = OsmData()
    for _, elem in ET.iterparse(path, events=("end",)):
        tag = elem.tag
        if tag == "node":
            data.nodes[int(elem.get("id"))] = Node(
                float(elem.get("lat")), float(elem.get("lon")), _tags(elem)
            )
        elif tag == "way":
            refs = [int(nd.get("ref")) for nd in elem.findall("nd")]
            data.ways[int(elem.get("id"))] = Way(refs, _tags(elem))
        elif tag == "relation":
            members = [
                Member(m.get("type"), int(m.get("ref")), m.get("role") or "")
                for m in elem.findall("member")
            ]
            data.relations[int(elem.get("id"))] = Relation(members, _tags(elem))
        elif tag == "meta":
            # Overpass: <meta osm_base="2026-10-15T08:00:00Z"/>
            data.timestamp = elem.get("osm_base", data.timestamp)
        elif tag == "osm":
            # openstreetmap.org export: <osm timestamp="...">
            data.timestamp = data.timestamp or elem.get("timestamp", "")
        else:
            continue
        if tag in ("node", "way", "relation"):
            elem.clear()
    return data


def assemble_rings(data: OsmData, way_ids: list[int]) -> list[list[int]]:
    """Join open way segments end-to-end into closed rings of node ids.

    Multipolygon members are often split into several ways; incomplete rings
    (e.g. clipped by the download bbox) are closed by joining their ends.
    """
    segments = [list(data.ways[w].refs) for w in way_ids if w in data.ways]
    segments = [s for s in segments if len(s) >= 2]
    rings: list[list[int]] = []
    while segments:
        ring = segments.pop(0)
        changed = True
        while ring[0] != ring[-1] and changed:
            changed = False
            for i, seg in enumerate(segments):
                if seg[0] == ring[-1]:
                    ring += seg[1:]
                elif seg[-1] == ring[-1]:
                    ring += seg[-2::-1]
                elif seg[-1] == ring[0]:
                    ring = seg[:-1] + ring
                elif seg[0] == ring[0]:
                    ring = seg[:0:-1] + ring
                else:
                    continue
                segments.pop(i)
                changed = True
                break
        if len(ring) >= 3:
            rings.append(ring)
    return rings
