"""Hand-made pieces for the survivor's notebook HUD (D-036): round badges for the status icons and a little sky dial for the
clock, with the skyline of the Pilar on its horizon. Everything else of the HUD (kraft paper, tape, ink outlines, bars, tally
marks) is drawn by the game from flat rectangles, so only what needs curves lives here. Names start with ui_."""

from __future__ import annotations

import math

from .canvas import Canvas


def _badge() -> Canvas:
    """A white disc with an ink rim, where a status icon sits on the bars."""
    c = Canvas(15)
    for y in range(15):
        for x in range(15):
            d = math.hypot(x - 7, y - 7)
            if d <= 7.2:
                c.px(x, y, "ink" if d > 6.0 else ("pale" if y >= 10 and d > 4.6 else "white"))
    return c


# The Basilica del Pilar seen from the river, very small: a long nave, a slender tower at each end and domes in between.
def _pilar(c: Canvas, colour: str) -> None:
    sky = {(x, y) for y in range(c.height) for x in range(c.width) if c.get(x, y)[3] and c.get(x, y)[:3] != (28, 26, 33)}

    def put(x: int, y: int) -> None:
        if (x, y) in sky:
            c.px(x, y, colour)

    for x in range(10, 34):                                           # the nave
        for y in range(17, 20):
            put(x, y)
    for tx in (9, 33):                                                # the towers, with a spire
        for y in range(10, 20):
            put(tx, y)
            put(tx + 1, y)
        put(tx, 9)
        put(tx, 8)
    for dx, r, base in ((21.5, 3.0, 15), (15.5, 2.0, 16), (27.5, 2.0, 16)):
        for y in range(int(base - r) - 1, 18):
            for x in range(int(dx - r) - 1, int(dx + r) + 2):
                if y >= base or math.hypot(x + .5 - dx, (y + .5 - base) * 1.1) <= r:
                    put(x, y)
        put(int(dx), int(base - r) - 1)                               # lantern


def _dial(night: bool) -> Canvas:
    """A half-moon window on the sky: the sun or the moon travels across it (the game places it), the Pilar on the horizon."""
    w, h = 44, 24
    c = Canvas(w, h)
    cx, cy, r = 21.5, 22.5, 21.0
    for y in range(h):
        for x in range(w):
            d = math.hypot(x - cx, y - cy)
            if d > r or y > 22:
                continue
            if d > r - 1.2:
                c.px(x, y, "ink")
                continue
            t = y / 22
            if night:
                c.px(x, y, "ink" if t < .35 else "dblue" if t < .8 else "blue")
            else:
                c.px(x, y, "lblue" if t < .55 else "cyan" if t < .85 else "white")
    if night:
        for x, y in ((12, 6), (28, 4), (33, 11), (8, 13), (20, 9), (36, 15)):
            c.px(x, y, "white")
    _pilar(c, "ink" if night else "shadow")
    c.rect(1, 20, w - 2, 21, "dgreen" if night else "green")
    c.rect(0, 22, w - 1, 23, "ink")
    return c


def build() -> dict[str, Canvas]:
    return {
        "ui_badge": _badge(),
        "ui_dial_day": _dial(False),
        "ui_dial_night": _dial(True),
    }
