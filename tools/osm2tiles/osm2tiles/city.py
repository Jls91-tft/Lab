"""City mode (D-029): a recognisable city built on a square grid, the Project Zomboid way.

OpenStreetMap only gives the skeleton: where the streets, squares, the river and the landmarks are. Every line is drawn as a staircase
of horizontal and vertical runs (no diagonals), and the ordinary buildings are not copied: each block of houses is cut into clean
rectangular lots, one house per lot, with straight party walls. Landmarks (churches, the town hall, very large buildings) keep their
place and size as one clean rectangle.
"""

from __future__ import annotations

import random

LOT_MIN, LOT_MAX = 8, 14          # lot frontage in tiles (metres at 1 m/tile)
DEPTH_MAX = 16                    # deeper rectangles are split into a front and a back row of lots
MIN_SIDE = 6                      # a rectangle narrower than this is not a house (it stays yard)
LANDMARK_TILES = 1500
LANDMARK_TAGS = {"cathedral", "church", "basilica", "chapel", "government", "public", "civic", "museum", "townhall"}


def simplify(points: list[tuple[float, float]], tolerance: float = 6.0) -> list[tuple[float, float]]:
    """Douglas-Peucker: drops the small wiggles of an OSM line, so a diagonal street becomes a few big steps, not a fine staircase."""
    if len(points) < 3:
        return points
    (x0, y0), (x1, y1) = points[0], points[-1]
    dx, dy = x1 - x0, y1 - y0
    length = (dx * dx + dy * dy) ** 0.5 or 1.0
    best, index = -1.0, 0
    for i in range(1, len(points) - 1):
        px, py = points[i]
        d = abs(dy * px - dx * py + x1 * y0 - y1 * x0) / length
        if d > best:
            best, index = d, i
    if best <= tolerance:
        return [points[0], points[-1]]
    return simplify(points[:index + 1], tolerance)[:-1] + simplify(points[index:], tolerance)


def manhattan(points: list[tuple[float, float]]) -> list[tuple[float, float]]:
    """A polyline or ring as horizontal and vertical runs: each segment becomes an L that keeps both ends (longer leg first)."""
    if len(points) < 2:
        return points
    closed = len(points) > 3 and abs(points[0][0] - points[-1][0]) < 1e-6 and abs(points[0][1] - points[-1][1]) < 1e-6
    if not closed:
        points = simplify(points)                              # streets: a few big steps; outlines keep their corners
    out = [points[0]]
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        if abs(x1 - x0) < 0.5 or abs(y1 - y0) < 0.5:
            out.append((x1, y1))
            continue
        corner = (x1, y0) if abs(x1 - x0) >= abs(y1 - y0) else (x0, y1)
        out.append(corner)
        out.append((x1, y1))
    return out


def is_landmark(tags: dict[str, str], tiles: int) -> bool:
    if tiles >= LANDMARK_TILES:
        return True
    if tags.get("building") in LANDMARK_TAGS or tags.get("amenity") in ("place_of_worship", "townhall") or tags.get("historic"):
        return True
    return False


def _best_rectangle(mask, w: int, h: int, min_side: int):
    heights = [0] * w
    best = (0, None)
    for y in range(h):
        for x in range(w):
            heights[x] = heights[x] + 1 if mask[y][x] else 0
        stack: list[tuple[int, int]] = []
        for x in range(w + 1):
            cur = heights[x] if x < w else 0
            start = x
            while stack and stack[-1][1] >= cur:
                sx, sh = stack.pop()
                area = sh * (x - sx)
                if area > best[0] and sh >= min_side and (x - sx) >= min_side:
                    best = (area, (sx, y - sh + 1, x - 1, y))
                start = sx
            stack.append((start, cur))
    return best[1]


def tidy_landmarks(bid_at: list[int], w: int, h: int, keep: set[int]) -> None:
    """A landmark becomes at most three big rectangles (a church and its wings), never a staircase."""
    for b in keep:
        idx = [i for i, v in enumerate(bid_at) if v == b]
        if not idx:
            continue
        xs = [i % w for i in idx]
        ys = [i // w for i in idx]
        x0, y0 = min(xs), min(ys)
        bw, bh = max(xs) - x0 + 1, max(ys) - y0 + 1
        mask = [[False] * bw for _ in range(bh)]
        for i in idx:
            mask[i // w - y0][i % w - x0] = True
        keepcells = set()
        for _ in range(3):
            rect = _best_rectangle(mask, bw, bh, MIN_SIDE)
            if rect is None:
                break
            rx0, ry0, rx1, ry1 = rect
            area = (rx1 - rx0 + 1) * (ry1 - ry0 + 1)
            if keepcells and area < 0.12 * len(idx):
                break
            for yy in range(ry0, ry1 + 1):
                for xx in range(rx0, rx1 + 1):
                    mask[yy][xx] = False
                    keepcells.add((yy + y0) * w + xx + x0)
        if keepcells:
            for i in idx:
                if i not in keepcells:
                    bid_at[i] = 0


def lots(bid_at: list[int], w: int, h: int, keep: set[int], tags: dict[int, dict[str, str]], seed: int = 29) -> dict[int, dict[str, str]]:
    """Replaces every ordinary building by rectangular lots, one house each (new ids). Returns the tags of the new ids.

    Blocks are the connected groups of building cells; each block is covered by its biggest rectangles, and each rectangle is cut into lots
    along its long side (two rows back to back when it is deep). A lot takes the tags of the OSM building that covered most of it."""
    rng = random.Random(seed)
    original = bid_at[:]
    for i, b in enumerate(original):
        if b and b not in keep:
            bid_at[i] = 0
    seen = [False] * (w * h)
    new_tags: dict[int, dict[str, str]] = {b: tags[b] for b in keep}
    next_id = max(tags) + 1 if tags else 1
    for start in range(w * h):
        b = original[start]
        if not b or b in keep or seen[start]:
            continue
        block = []
        stack = [start]
        seen[start] = True
        while stack:
            i = stack.pop()
            block.append(i)
            x, y = i % w, i // w
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if 0 <= nx < w and 0 <= ny < h:
                    j = ny * w + nx
                    if not seen[j] and original[j] and original[j] not in keep:
                        seen[j] = True
                        stack.append(j)
        xs = [i % w for i in block]
        ys = [i // w for i in block]
        x0, y0 = min(xs), min(ys)
        bw, bh = max(xs) - x0 + 1, max(ys) - y0 + 1
        mask = [[False] * bw for _ in range(bh)]
        for i in block:
            mask[i // w - y0][i % w - x0] = True
        for _ in range(64):
            rect = _best_rectangle(mask, bw, bh, MIN_SIDE)
            if rect is None:
                break
            rx0, ry0, rx1, ry1 = rect
            for yy in range(ry0, ry1 + 1):
                for xx in range(rx0, rx1 + 1):
                    mask[yy][xx] = False
            for (lx0, ly0, lx1, ly1) in _cut(rx0 + x0, ry0 + y0, rx1 + x0, ry1 + y0, rng):
                votes: dict[int, int] = {}
                for yy in range(ly0, ly1 + 1):
                    for xx in range(lx0, lx1 + 1):
                        o = original[yy * w + xx]
                        votes[o] = votes.get(o, 0) + 1
                        bid_at[yy * w + xx] = next_id
                source = max(votes, key=votes.get)
                new_tags[next_id] = dict(tags.get(source, {}))
                next_id += 1
    return new_tags


def _cut(x0: int, y0: int, x1: int, y1: int, rng: random.Random) -> list[tuple[int, int, int, int]]:
    """A rectangle as lots: rows along its long side, lots LOT_MIN..LOT_MAX wide."""
    w, h = x1 - x0 + 1, y1 - y0 + 1
    along_x = w >= h
    length, depth = (w, h) if along_x else (h, w)
    rows = [(0, depth - 1)]
    if depth > DEPTH_MAX and depth >= 2 * MIN_SIDE:
        half = depth // 2
        rows = [(0, half - 1), (half, depth - 1)]
    out = []
    for d0, d1 in rows:
        pos = 0
        while pos < length:
            size = rng.randint(LOT_MIN, LOT_MAX)
            if length - (pos + size) < MIN_SIDE:
                size = length - pos                                   # the last lot takes what is left
            a0, a1 = pos, pos + size - 1
            if along_x:
                out.append((x0 + a0, y0 + d0, x0 + a1, y0 + d1))
            else:
                out.append((x0 + d0, y0 + a0, x0 + d1, y0 + a1))
            pos += size
    return out
