"""Rasterise OSM features into the two tile layers of `cepacero.map` v1.

Drawing happens in image space (row 0 = north, Pillow's convention); the
result is flipped at the end so that row 0 = south (Unity's Y axis).
"""

from __future__ import annotations

import math
from collections import deque
from dataclasses import dataclass, field

from PIL import Image, ImageDraw

from . import city as city_mode
from . import rules
from .geo import Projector
from .osm import OsmData, assemble_rings

G, B = rules.G, rules.B

TREE_ROW_SPACING_M = 6.0
WOOD_TREE_EVERY_N_TILES = 7  # rough density for natural=wood / landuse=forest


def _pixels(img: Image.Image) -> list[int]:
    getter = getattr(img, "get_flattened_data", None) or img.getdata  # Pillow >= 12 renamed it
    return list(getter())


@dataclass
class Building:
    id: int
    kind: str
    bbox: list[int]          # [min_x, min_y, max_x, max_y], inclusive, row 0 = south
    tiles: int
    doors: list[int] = field(default_factory=list)  # flat [x0, y0, x1, y1, ...]
    levels: int = 1          # storeys above ground, ground floor included (v2)
    underground: int = 0     # basement levels (v2)
    roof: str = "flat"       # one of rules.ROOFS (v2)


@dataclass
class TileMap:
    width: int
    height: int
    ground: list[int]        # row-major, row 0 = south
    blocks: list[int]
    buildings: list[Building]


@dataclass
class _Area:
    cls: str
    tags: dict[str, str]
    outers: list[list[int]]
    inners: list[list[int]]


MIN_SIDE = 4              # a rectangle of a regular building is at least this many tiles on a side
KEEP_FRACTION = 0.93      # stop adding rectangles once this part of the footprint is covered
MAX_RECTS = 6


def _best_rectangle(mask: list[list[bool]], w: int, h: int) -> tuple[int, int, int, int] | None:
    """Largest axis-aligned rectangle of True cells (x0, y0, x1, y1 inclusive), by the histogram method."""
    heights = [0] * w
    best = (0, None)
    for y in range(h):
        for x in range(w):
            heights[x] = heights[x] + 1 if mask[y][x] else 0
        stack: list[tuple[int, int]] = []                      # (start x, height)
        for x in range(w + 1):
            cur = heights[x] if x < w else 0
            start = x
            while stack and stack[-1][1] >= cur:
                sx, sh = stack.pop()
                area = sh * (x - sx)
                if area > best[0] and sh >= MIN_SIDE and (x - sx) >= MIN_SIDE:
                    best = (area, (sx, y - sh + 1, x - 1, y))
                start = sx
            stack.append((start, cur))
    return best[1]


def _oriented_rectangle(points: list[tuple[int, int]]) -> tuple[float, float, float, float, float]:
    """Minimum-area bounding rectangle of the cells, trying turns of 0..90 degrees: (angle in degrees folded to -45..45, side a, side b, centre x, centre y)."""
    best = None
    for deg in range(0, 90, 3):
        a = math.radians(deg)
        ca, sa = math.cos(a), math.sin(a)
        us = [x * ca + y * sa for x, y in points]
        vs = [-x * sa + y * ca for x, y in points]
        du, dv = max(us) - min(us) + 1, max(vs) - min(vs) + 1
        if best is None or du * dv < best[0]:
            cu, cv = (max(us) + min(us)) / 2, (max(vs) + min(vs)) / 2
            best = (du * dv, deg, du, dv, cu * ca - cv * sa, cu * sa + cv * ca)
    _, deg, du, dv, cx, cy = best
    if deg > 45:
        deg -= 90
        du, dv = dv, du                                       # a quarter turn swaps the sides
    return deg, du, dv, cx, cy


def _regularize(bid_at: list[int], gnd: list[int], w: int, h: int) -> None:
    """Tidies every footprint (D-027) so houses are the clean volumes of Project Zomboid, not the staircases an OSM outline makes on the grid.
    A building that is nearly a rectangle (its area fills 75% of its oriented bounding box) becomes an axis-aligned rectangle of the same
    size and centre, turned to the nearest axis; one that is axis-aligned already, or an L, T or U, becomes the union of its biggest
    rectangles. A building never grows over another one, a road, water or a railway."""
    original = bid_at[:]
    outside = _outside(original, w, h)
    cells: dict[int, list[int]] = {}
    for i, b in enumerate(original):
        if b:
            cells.setdefault(b, []).append(i)
    blocked = (G["asphalt"], G["water"], G["rail"])
    result: dict[int, set[int]] = {}
    for b, idx in cells.items():
        xs = [i % w for i in idx]
        ys = [i // w for i in idx]
        pts = list(zip(xs, ys))
        total = len(idx)
        deg, du, dv, cx, cy = _oriented_rectangle(pts)
        fill = total / max(1.0, du * dv)
        shape: set[int] = set()
        if abs(deg) >= 12 and fill >= 0.75 and min(du, dv) >= MIN_SIDE:
            half_w, half_h = round(du) / 2, round(dv) / 2
            x0, x1 = math.floor(cx - half_w + 0.5), math.floor(cx + half_w - 0.5)
            y0, y1 = math.floor(cy - half_h + 0.5), math.floor(cy + half_h - 0.5)
            for y in range(max(0, y0), min(h - 1, y1) + 1):
                for x in range(max(0, x0), min(w - 1, x1) + 1):
                    i = y * w + x
                    if original[i] == b or (original[i] == 0 and outside[i] and gnd[i] not in blocked and _touches(original, i, b, w, h)):
                        shape.add(i)
        if shape:
            shape = _largest_rectangle(shape, w)           # where a neighbour or a street cut a corner off, shrink to a clean rectangle
        if len(shape) < 0.55 * total:
            shape = _rectangle_union(idx, w, total)
        result[b] = shape
    for i, b in enumerate(original):
        if b:
            bid_at[i] = 0
    for b in sorted(result):
        for i in result[b]:
            if bid_at[i] == 0:
                bid_at[i] = b


def _touches(ids: list[int], i: int, b: int, w: int, h: int) -> bool:
    x, y = i % w, i // w
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        nx, ny = x + dx, y + dy
        if 0 <= nx < w and 0 <= ny < h and ids[ny * w + nx] == b:
            return True
    return False


def _outside(ids: list[int], w: int, h: int) -> list[bool]:
    """Empty cells joined to the edge of the map: the street side. Courtyards closed in by buildings are not (they must stay open)."""
    reach = [False] * (w * h)
    stack = [i for i in range(w * h) if ids[i] == 0 and (i % w in (0, w - 1) or i // w in (0, h - 1))]
    for i in stack:
        reach[i] = True
    while stack:
        i = stack.pop()
        x, y = i % w, i // w
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h:
                j = ny * w + nx
                if not reach[j] and ids[j] == 0:
                    reach[j] = True
                    stack.append(j)
    return reach


def _largest_rectangle(cells: set[int], w: int) -> set[int]:
    xs = [i % w for i in cells]
    ys = [i // w for i in cells]
    x0, y0 = min(xs), min(ys)
    bw, bh = max(xs) - x0 + 1, max(ys) - y0 + 1
    mask = [[False] * bw for _ in range(bh)]
    for i in cells:
        mask[i // w - y0][i % w - x0] = True
    rect = _best_rectangle(mask, bw, bh)
    if rect is None:
        return set()
    rx0, ry0, rx1, ry1 = rect
    return {(yy + y0) * w + xx + x0 for yy in range(ry0, ry1 + 1) for xx in range(rx0, rx1 + 1)}


def _rectangle_union(idx: list[int], w: int, total: int) -> set[int]:
    xs = [i % w for i in idx]
    ys = [i // w for i in idx]
    x0, y0, x1, y1 = min(xs), min(ys), max(xs), max(ys)
    bw, bh = x1 - x0 + 1, y1 - y0 + 1
    remaining = [[False] * bw for _ in range(bh)]
    for i in idx:
        remaining[i // w - y0][i % w - x0] = True
    keep: set[int] = set()
    for _ in range(MAX_RECTS):
        rect = _best_rectangle(remaining, bw, bh)
        if rect is None:
            break
        rx0, ry0, rx1, ry1 = rect
        for yy in range(ry0, ry1 + 1):
            for xx in range(rx0, rx1 + 1):
                keep.add((yy + y0) * w + xx + x0)
                remaining[yy][xx] = False
        if len(keep) >= KEEP_FRACTION * total:
            break
    return keep if len(keep) >= 0.5 * total else set(idx)           # too irregular to tidy: as OSM drew it


def _yards(gnd: list[int], bid_at: list[int], blk: list[int], w: int, h: int) -> None:
    """The bare ground left between buildings becomes yards (D-027): paving a metre wide around the houses and grass beyond, instead of a
    brown waste. Trees placed later find the grass."""
    dirt, grass, paving = G["dirt"], G["grass"], G["sidewalk"]
    for i in range(w * h):
        if gnd[i] != dirt or bid_at[i]:
            continue
        x, y = i % w, i // w
        near = False
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and bid_at[ny * w + nx]:
                    near = True
        gnd[i] = paving if near else grass


def _collect_areas(data: OsmData) -> list[_Area]:
    areas: list[_Area] = []
    used_as_outer: set[int] = set()
    for rel in data.relations.values():
        if rel.tags.get("type") not in ("multipolygon", "boundary"):
            continue
        outer_ids = [m.ref for m in rel.members if m.type == "way" and m.role in ("outer", "")]
        inner_ids = [m.ref for m in rel.members if m.type == "way" and m.role == "inner"]
        tags = dict(rel.tags)
        cls = rules.area_class(tags)
        if cls is None and len(outer_ids) == 1 and outer_ids[0] in data.ways:
            # Old-style multipolygon: tags live on the outer way.
            tags = data.ways[outer_ids[0]].tags
            cls = rules.area_class(tags)
        if cls is None and rules.building_kind(tags) is None:
            continue
        used_as_outer.update(outer_ids)
        areas.append(_Area(cls or "poi", tags, assemble_rings(data, outer_ids), assemble_rings(data, inner_ids)))
    for wid, way in data.ways.items():
        if not way.is_closed or wid in used_as_outer:
            continue
        if way.tags.get("area") == "no":
            continue
        cls = rules.area_class(way.tags)
        if cls is None and rules.building_kind(way.tags) is None:
            continue
        areas.append(_Area(cls or "poi", way.tags, [way.refs], []))
    return areas


class _Rasterizer:
    def __init__(self, data: OsmData, proj: Projector):
        self.data = data
        self.proj = proj
        self.w, self.h = proj.width, proj.height
        self.manhattan = False                                   # city mode (D-029): no diagonal lines

    def pts(self, refs: list[int]) -> list[tuple[float, float]]:
        # Pillow puts pixel centres on integer coordinates; tile (i, j) spans
        # [i, i+1), hence the half-tile shift.
        out = []
        for r in refs:
            n = self.data.nodes.get(r)
            if n is not None:
                x, y = self.proj.to_tiles(n.lat, n.lon)
                out.append((x - 0.5, self.h - y - 0.5))
        return city_mode.manhattan(out) if self.manhattan else out

    def cell(self, lat: float, lon: float) -> tuple[int, int]:
        """Image-space cell (x, row) containing a point; may be out of range."""
        x, y = self.proj.to_tiles(lat, lon)
        return math.floor(x), self.h - 1 - math.floor(y)

    def width_px(self, meters: float) -> int:
        return max(1, round(self.proj.meters(meters)))

    def line(self, draw: ImageDraw.ImageDraw, refs: list[int], fill: int, width: int) -> None:
        p = self.pts(refs)
        if len(p) < 2:
            return
        draw.line(p, fill=fill, width=width, joint=None if self.manhattan else "curve")
        if width > 2:  # caps so intersections have no notches (square ones on a city grid)
            r = width / 2
            for x, y in p if self.manhattan else (p[0], p[-1]):
                if self.manhattan:
                    draw.rectangle((x - r + 0.5, y - r + 0.5, x + r - 0.5, y + r - 0.5), fill=fill)
                else:
                    draw.ellipse((x - r, y - r, x + r, y + r), fill=fill)

    def fill_area(self, img: Image.Image, area: _Area, value: int) -> None:
        if not area.inners:
            draw = ImageDraw.Draw(img)
            for ring in area.outers:
                p = self.pts(ring)
                if len(p) >= 3:
                    draw.polygon(p, fill=value)
            return
        mask = Image.new("1", img.size, 0)
        md = ImageDraw.Draw(mask)
        for ring in area.outers:
            p = self.pts(ring)
            if len(p) >= 3:
                md.polygon(p, fill=1)
        for ring in area.inners:
            p = self.pts(ring)
            if len(p) >= 3:
                md.polygon(p, fill=0)
        img.paste(value, mask=mask)


def _distance_to_roads(gnd: list[int], bid_at: list[int], w: int, h: int) -> list[int]:
    """BFS distance (tiles, 4-connected) from every outdoor cell to the nearest
    street or sidewalk. Unreachable cells get a large value."""
    unreachable = w * h
    dist = [unreachable] * (w * h)
    queue = deque()
    for i, g in enumerate(gnd):
        if bid_at[i] == 0 and g in (G["asphalt"], G["sidewalk"]):
            dist[i] = 0
            queue.append(i)
    while queue:
        i = queue.popleft()
        x, y = i % w, i // w
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h:
                n = ny * w + nx
                if dist[n] == unreachable and bid_at[n] == 0 and gnd[n] != G["water"]:
                    dist[n] = dist[i] + 1
                    queue.append(n)
    return dist


def build_map(data: OsmData, proj: Projector, yards: bool = False, city: bool = False) -> TileMap:
    r = _Rasterizer(data, proj)
    r.manhattan = city
    w, h = r.w, r.h
    ground = Image.new("L", (w, h), G["dirt"])
    wood = Image.new("L", (w, h), 0)
    fences = Image.new("L", (w, h), 0)
    ids = Image.new("I", (w, h), 0)
    kinds_img = Image.new("L", (w, h), 0)  # POI areas (e.g. a hospital campus)
    gd, fd = ImageDraw.Draw(ground), ImageDraw.Draw(fences)

    areas = _collect_areas(data)

    # 1. Ground areas, in paint order.
    for area in sorted((a for a in areas if a.cls in rules.AREA_ORDER), key=lambda a: rules.AREA_ORDER[a.cls]):
        if area.cls == "wood":
            r.fill_area(ground, area, G["grass"])
            r.fill_area(wood, area, 1)
        else:
            r.fill_area(ground, area, G[area.cls])

    # 2. Lines: water, rail, sidewalks, paths, carriageways, fences.
    ways = list(data.ways.values())
    for way in ways:
        ww = way.tags.get("waterway")
        if ww in rules.WATERWAYS and way.tags.get("tunnel") in (None, "no"):
            r.line(gd, way.refs, G["water"], r.width_px(rules.WATERWAYS[ww]))
    for way in ways:
        rw = way.tags.get("railway")
        if rw in rules.RAILWAYS and way.tags.get("tunnel") in (None, "no"):
            r.line(gd, way.refs, G["rail"], r.width_px(rules.RAILWAYS[rw]))

    roads = []
    for way in ways:
        hw = way.tags.get("highway")
        if hw not in rules.ROADS or way.tags.get("area") == "yes":
            continue
        if way.tags.get("tunnel") not in (None, "no"):
            continue
        width_m, surface = rules.ROADS[hw]
        roads.append((width_m, surface, way))
    for width_m, surface, way in roads:
        if surface == "asphalt":
            r.line(gd, way.refs, G["sidewalk"], r.width_px(width_m + 2 * rules.SIDEWALK_M))
    for width_m, surface, way in roads:
        if surface != "asphalt":
            r.line(gd, way.refs, G[surface], r.width_px(width_m))
    for width_m, surface, way in sorted(roads, key=lambda t: t[0]):
        if surface == "asphalt":
            r.line(gd, way.refs, G["asphalt"], r.width_px(width_m))

    for way in ways:
        if way.tags.get("barrier") in ("fence", "wall", "hedge", "guard_rail"):
            r.line(fd, way.refs, 1, 1)

    # 3. POI areas (kind hints for the buildings inside them), low priority first.
    poi_areas = [(rules.building_kind(a.tags), a) for a in areas if a.cls != "building"]
    poi_areas = [(k, a) for k, a in poi_areas if k]
    for kind, area in sorted(poi_areas, key=lambda t: rules.KIND_PRIORITY[t[0]]):
        r.fill_area(kinds_img, area, rules.BUILDING_KINDS.index(kind))

    # 4. Building footprints into the id raster.
    building_tags: dict[int, dict[str, str]] = {}
    for area in (a for a in areas if a.cls == "building"):
        bid = len(building_tags) + 1
        building_tags[bid] = area.tags
        r.fill_area(ids, area, bid)

    # ---- Per-cell post-processing (image space: row 0 = north) ----
    gnd = _pixels(ground)
    bid_at = _pixels(ids)
    _regularize(bid_at, gnd, w, h)
    if city:
        # D-029: landmarks stay (one clean rectangle each), every other building becomes rectangular lots, one house per lot.
        sizes: dict[int, int] = {}
        for b in bid_at:
            if b:
                sizes[b] = sizes.get(b, 0) + 1
        keep = {b for b, n in sizes.items() if city_mode.is_landmark(building_tags.get(b, {}), n)}
        city_mode.tidy_landmarks(bid_at, w, h, keep)
        building_tags = city_mode.lots(bid_at, w, h, keep, building_tags)
    wood_at = _pixels(wood)
    fence_at = _pixels(fences)
    kind_hint = _pixels(kinds_img)
    blk = [B["none"]] * (w * h)

    def bid(x: int, y: int) -> int:
        return bid_at[y * w + x] if 0 <= x < w and 0 <= y < h else 0

    cells: dict[int, list[int]] = {}
    for i, b in enumerate(bid_at):
        if not b:
            continue
        cells.setdefault(b, []).append(i)
        gnd[i] = G["floor"]
        x, y = i % w, i // w
        # A cell is wall if it touches the outside or a building with a lower id:
        # party walls between neighbours stay one tile thick.
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            nb = bid(nx, ny)
            if nb == 0 or nb < b:
                blk[i] = B["building_wall"]
                break

    # Kinds: own tags, POI nodes inside, POI areas around.
    kinds = {b: rules.building_kind(t) or "generic" for b, t in building_tags.items()}
    for node in data.nodes.values():
        if not node.tags:
            continue
        hint = rules.building_kind(node.tags)
        if not hint:
            continue
        b = bid(*r.cell(node.lat, node.lon))
        if b:
            kinds[b] = rules.better_kind(kinds[b], hint)
    for b, idx in cells.items():
        hint = kind_hint[idx[len(idx) // 2]]
        if hint:
            kinds[b] = rules.better_kind(kinds[b], rules.BUILDING_KINDS[hint])

    # Doors: a wall cell with walkable outside on one side and interior opposite,
    # on the facade closest (walking distance) to a street.
    road_dist = _distance_to_roads(gnd, bid_at, w, h)
    doors: dict[int, list[int]] = {}
    for b, idx in cells.items():
        candidates = []
        for i in idx:
            if blk[i] != B["building_wall"]:
                continue
            x, y = i % w, i // w
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ox, oy, ix, iy = x + dx, y + dy, x - dx, y - dy
                if not (0 <= ox < w and 0 <= oy < h and 0 <= ix < w and 0 <= iy < h):
                    continue
                o, inner = oy * w + ox, iy * w + ix
                if bid_at[o] == 0 and gnd[o] in rules.DOOR_OUTSIDE and bid_at[inner] == b and blk[inner] == B["none"]:
                    candidates.append((road_dist[o], i))
                    break
        if not candidates:
            # Shut in by its neighbours (D-031): a door through the party wall into the house next door, so no building is sealed.
            for i in idx:
                if blk[i] != B["building_wall"]:
                    continue
                x, y = i % w, i // w
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ox, oy, ix, iy = x + dx, y + dy, x - dx, y - dy
                    if not (0 <= ox < w and 0 <= oy < h and 0 <= ix < w and 0 <= iy < h):
                        continue
                    o, inner = oy * w + ox, iy * w + ix
                    if bid_at[o] not in (0, b) and blk[o] == B["none"] and bid_at[inner] == b and blk[inner] == B["none"]:
                        candidates.append((0, i))
                        break
                if candidates:
                    break
        if not candidates:
            continue
        candidates.sort()
        nearest = candidates[0][0]
        best = sorted(i for d, i in candidates if d <= nearest + 1)  # the whole street-facing facade
        count = min(len(best), 1 + len(idx) // 400)
        chosen = [best[(k * len(best)) // count + len(best) // (2 * count)] for k in range(count)]
        doors[b] = chosen
        for i in chosen:
            blk[i] = B["door"]

    # Trees and fences on free outdoor cells.
    def free_outdoor(i: int) -> bool:
        return bid_at[i] == 0 and blk[i] == B["none"] and gnd[i] in (G["grass"], G["dirt"], G["sidewalk"])

    for i in range(w * h):
        if wood_at[i] and free_outdoor(i):
            x, y = i % w, i // w
            if ((x * 73856093) ^ (y * 19349663)) % WOOD_TREE_EVERY_N_TILES == 0:
                blk[i] = B["tree"]
        if fence_at[i] and bid_at[i] == 0 and blk[i] == B["none"] and gnd[i] != G["water"]:
            blk[i] = B["fence"]

    tree_points: list[tuple[float, float]] = []  # tile space, y north
    for node in data.nodes.values():
        if node.tags.get("natural") == "tree":
            tree_points.append(r.proj.to_tiles(node.lat, node.lon))
    step = r.proj.meters(TREE_ROW_SPACING_M)
    for way in ways:
        if way.tags.get("natural") != "tree_row":
            continue
        p = [r.proj.to_tiles(data.nodes[n].lat, data.nodes[n].lon) for n in way.refs if n in data.nodes]
        for (x0, y0), (x1, y1) in zip(p, p[1:]):
            seg = math.hypot(x1 - x0, y1 - y0)
            for k in range(int(seg / step) + 1):
                t = (k * step) / seg if seg else 0
                tree_points.append((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t))
    for x, y in tree_points:
        ix, iy = math.floor(x), h - 1 - math.floor(y)
        if 0 <= ix < w and 0 <= iy < h and free_outdoor(iy * w + ix):
            blk[iy * w + ix] = B["tree"]

    if yards:
        _yards(gnd, bid_at, blk, w, h)

    # ---- Flip to row 0 = south and renumber buildings ----
    def flip(layer: list[int]) -> list[int]:
        out: list[int] = []
        for row in range(h - 1, -1, -1):
            out.extend(layer[row * w:(row + 1) * w])
        return out

    buildings: list[Building] = []
    for new_id, (b, idx) in enumerate(sorted(cells.items()), start=1):
        xs = [i % w for i in idx]
        ys = [h - 1 - i // w for i in idx]
        flat_doors: list[int] = []
        for i in doors.get(b, []):
            flat_doors += [i % w, h - 1 - i // w]
        tags = building_tags[b]
        buildings.append(Building(new_id, kinds[b], [min(xs), min(ys), max(xs), max(ys)], len(idx), flat_doors,
                                  rules.building_levels(tags, kinds[b]), rules.building_underground(tags),
                                  rules.building_roof(tags, kinds[b])))

    return TileMap(w, h, flip(gnd), flip(blk), buildings)
