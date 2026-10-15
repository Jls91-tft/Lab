"""16x16 ground tiles (3 variants each) and blocks.

Names match the game data: ground_<groundtype>_<variant> (lowercase GroundType enum)
and block_<block key> (keys of BlockRegistry.CreateDefault()).
"""

from __future__ import annotations

from typing import Callable

from .canvas import Canvas, rng

T = 16
GROUND_VARIANTS = 3


# ------------------------------------------------------------------ ground

def _dirt(c: Canvas, name: str) -> None:
    r = rng(name)
    c.fill("brown")
    c.speckle(r, ["dbrown", "lbrown"], 26)
    c.speckle(r, ["tan"], 3)


def _asphalt(c: Canvas, name: str) -> None:
    r = rng(name)
    c.fill("shadow")
    c.speckle(r, ["dgray", "ink"], 30)
    c.speckle(r, ["gray"], 4)
    if name.endswith("_2"):
        c.line(3, 11, 9, 9, "ink")  # a crack


def _sidewalk(c: Canvas, name: str) -> None:
    r = rng(name)
    c.fill("lgray")
    c.speckle(r, ["gray", "pale"], 14)
    c.rect(0, 7, 15, 7, "gray")
    c.rect(7, 0, 7, 15, "gray")
    c.rect(0, 15, 15, 15, "gray")
    c.rect(15, 0, 15, 15, "gray")


def _grass(c: Canvas, name: str) -> None:
    r = rng(name)
    c.fill("green")
    for _ in range(22):
        x, y = r.randint(0, 15), r.randint(1, 15)
        c.px(x, y, "lgreen")
        c.px(x, y - 1, "lgreen" if r.random() < 0.5 else "green")
    c.speckle(r, ["dgreen"], 14)
    if name.endswith("_1"):
        c.px(5, 6, "yellow")  # a flower
        c.px(11, 12, "white")


def _water(c: Canvas, name: str) -> None:
    r = rng(name)
    c.fill("blue")
    c.speckle(r, ["dblue"], 10)
    for _ in range(4):
        x, y = r.randint(0, 11), r.randint(0, 15)
        c.rect(x, y, x + 3, y, "lblue")
    if name.endswith("_0"):
        c.px(9, 4, "cyan")


def _floor(c: Canvas, name: str) -> None:
    r = rng(name)
    c.fill("tan")
    for row in range(4):
        y = row * 4
        c.rect(0, y + 3, 15, y + 3, "lbrown")
        joint = (row * 5 + r.randint(0, 3)) % 16
        c.rect(joint, y, joint, y + 2, "lbrown")
    c.speckle(r, ["lbrown"], 6)


def _rail(c: Canvas, name: str) -> None:
    r = rng(name)
    c.fill("dgray")
    c.speckle(r, ["gray", "shadow", "lgray"], 30)
    for y in (1, 6, 11):
        c.rect(1, y, 14, y + 2, "dbrown")
    c.rect(3, 0, 4, 15, "steel")
    c.rect(11, 0, 12, 15, "steel")
    c.rect(3, 0, 3, 15, "lsteel")
    c.rect(11, 0, 11, 15, "lsteel")


GROUNDS: dict[str, Callable[[Canvas, str], None]] = {
    "dirt": _dirt,
    "asphalt": _asphalt,
    "sidewalk": _sidewalk,
    "grass": _grass,
    "water": _water,
    "floor": _floor,
    "rail": _rail,
}


# ------------------------------------------------------------------ blocks

def _building_wall(c: Canvas) -> None:
    c.fill("dgray")
    for row in range(4):
        y = row * 4
        c.rect(0, y + 3, 15, y + 3, "shadow")
        offset = 0 if row % 2 == 0 else 4
        for x in range(offset, 16, 8):
            c.rect(x, y, x, y + 2, "shadow")
    c.bevel("gray", "ink")


def _concrete_wall(c: Canvas) -> None:
    r = rng("concrete_wall")
    c.fill("lgray")
    c.speckle(r, ["gray", "pale"], 18)
    c.line(4, 3, 7, 7, "gray")
    c.line(7, 7, 6, 10, "gray")
    c.bevel("pale", "dgray")


def _wood_wall(c: Canvas) -> None:
    c.fill("lbrown")
    for x in (0, 4, 8, 12):
        c.rect(x + 3, 0, x + 3, 15, "brown")
        c.px(x + 1, 2, "pale")
        c.px(x + 1, 13, "pale")
    c.px(6, 8, "tan")
    c.px(10, 5, "tan")
    c.bevel("tan", "dbrown")


def _metal_wall(c: Canvas) -> None:
    c.fill("steel")
    c.rect(0, 7, 15, 8, "gray")
    for x, y in ((2, 2), (13, 2), (2, 13), (13, 13), (2, 5), (13, 10)):
        c.px(x, y, "lsteel")
    c.line(5, 3, 10, 3, "lsteel")
    c.bevel("lsteel", "dgray")


def _fence(c: Canvas) -> None:
    c.shadow(1, 10, 14, 14)
    c.rect(0, 5, 15, 6, "lbrown")
    c.rect(0, 10, 15, 11, "lbrown")
    for x in (2, 7, 12):
        c.rect(x, 2, x + 1, 13, "tan")
        c.rect(x + 1, 2, x + 1, 13, "lbrown")
    c.outline("dbrown")


def _tree(c: Canvas) -> None:
    c.shadow(2, 9, 15, 15)
    c.ellipse(1, 0, 14, 13, "dgreen")
    c.ellipse(2, 1, 12, 10, "green")
    c.ellipse(4, 2, 9, 6, "lgreen")
    r = rng("tree")
    c.speckle(r, ["dgreen"], 6, 3, 3, 12, 11)
    c.px(5, 3, "lime")
    c.outline("ink")


def _door(c: Canvas) -> None:
    c.fill("dbrown")
    c.rect(2, 1, 13, 14, "lbrown")
    for x in (5, 9):
        c.rect(x, 1, x, 14, "brown")
    c.rect(11, 7, 12, 8, "yellow")
    c.bevel("brown", "ink")


def _door_open(c: Canvas) -> None:
    c.rect(0, 0, 15, 1, "dbrown")
    c.rect(0, 14, 15, 15, "dbrown")
    c.rect(0, 2, 2, 13, "lbrown")
    c.rect(2, 2, 2, 13, "brown")
    c.outline("ink")


def _workbench(c: Canvas) -> None:
    c.shadow(0, 11, 15, 15)
    c.rect(1, 2, 14, 12, "lbrown")
    c.rect(1, 2, 14, 2, "tan")
    c.rect(1, 12, 14, 12, "brown")
    c.rect(3, 5, 8, 6, "gray")  # a saw
    c.rect(3, 5, 4, 6, "dbrown")
    c.rect(10, 4, 11, 9, "brown")  # a hammer
    c.rect(9, 4, 12, 5, "steel")
    c.outline("ink")


def _chest(c: Canvas) -> None:
    c.shadow(1, 11, 15, 15)
    c.rect(1, 3, 14, 13, "brown")
    c.rect(1, 3, 14, 6, "lbrown")
    c.rect(1, 7, 14, 7, "dbrown")
    c.rect(4, 3, 4, 13, "steel")
    c.rect(11, 3, 11, 13, "steel")
    c.rect(7, 6, 8, 9, "yellow")
    c.outline("ink")


def _spike_trap(c: Canvas) -> None:
    c.rect(1, 1, 14, 14, "brown")
    c.rect(1, 1, 14, 1, "lbrown")
    for y in (3, 8):
        for x in (3, 7, 11):
            c.polygon([(x, y + 4), (x + 1, y), (x + 2, y + 4)], "lsteel")
            c.px(x + 1, y + 1, "white")
    c.px(4, 12, "red")
    c.outline("ink")


BLOCKS: dict[str, Callable[[Canvas], None]] = {
    "building_wall": _building_wall,
    "tree": _tree,
    "door": _door,
    "door_open": _door_open,
    "fence": _fence,
    "wood_wall": _wood_wall,
    "metal_wall": _metal_wall,
    "concrete_wall": _concrete_wall,
    "workbench": _workbench,
    "chest": _chest,
    "spike_trap": _spike_trap,
}


def build() -> dict[str, Canvas]:
    out: dict[str, Canvas] = {}
    for ground, painter in GROUNDS.items():
        for v in range(GROUND_VARIANTS):
            name = f"ground_{ground}_{v}"
            c = Canvas(T)
            painter(c, name)
            out[name] = c
    for key, painter in BLOCKS.items():
        c = Canvas(T)
        painter(c)
        out[f"block_{key}"] = c
    return out
