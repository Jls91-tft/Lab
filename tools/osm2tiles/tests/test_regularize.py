from osm2tiles import rasterize
from osm2tiles.rasterize import _regularize


def _grid(rows):
    w = len(rows[0])
    return [1 if ch == "#" else 0 for row in rows for ch in row], w, len(rows)


def test_a_staircase_with_a_jut_becomes_a_clean_rectangle():
    rows = [
        "..............",
        ".##########...",
        ".##########...",
        ".##########.#.",
        ".##########...",
        ".##########...",
        "..............",
    ]
    ids, w, h = _grid(rows)
    _regularize(ids, [3] * len(ids), w, h)
    kept = {(i % w, i // w) for i, b in enumerate(ids) if b}
    xs = [x for x, _ in kept]
    ys = [y for _, y in kept]
    assert len(kept) == (max(xs) - min(xs) + 1) * (max(ys) - min(ys) + 1), "a filled rectangle"
    assert (12, 3) not in kept, "the one-tile jut is gone"


def test_two_buildings_never_overlap():
    ids = [0] * 40
    w, h = 10, 4
    for x in range(0, 5):
        for y in range(0, 4):
            ids[y * w + x] = 1
    for x in range(5, 10):
        for y in range(0, 4):
            ids[y * w + x] = 2
    _regularize(ids, [3] * 40, w, h)
    assert set(ids) <= {0, 1, 2}
    assert all(ids[y * w + x] in (0, 1) for x in range(0, 5) for y in range(4))
    assert all(ids[y * w + x] in (0, 2) for x in range(5, 10) for y in range(4))
