from osm2tiles.city import lots, manhattan


def test_lines_have_no_diagonals():
    pts = manhattan([(0.0, 0.0), (10.0, 4.0), (12.0, 20.0)])
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        assert x0 == x1 or y0 == y1


def test_a_block_becomes_rectangular_lots():
    w, h = 40, 20
    ids = [0] * (w * h)
    for y in range(2, 18):
        for x in range(2, 38):
            ids[y * w + x] = 1 if x < 20 else 2
    tags = lots(ids, w, h, set(), {1: {"building": "house"}, 2: {"building": "shop"}})
    assert len(tags) >= 3
    for b in set(ids) - {0}:
        cells = [(i % w, i // w) for i, v in enumerate(ids) if v == b]
        xs = [c[0] for c in cells]
        ys = [c[1] for c in cells]
        assert len(cells) == (max(xs) - min(xs) + 1) * (max(ys) - min(ys) + 1), "every lot is a filled rectangle"
