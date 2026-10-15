"""16x16 item icons (item_<id>, one per entry in data/generated/items.json) and the
sack shown for item piles on the floor (pile)."""

from __future__ import annotations

from typing import Callable

from .canvas import Canvas


def _wood(c: Canvas) -> None:
    for y in (4, 9):
        c.rect(2, y, 13, y + 3, "lbrown")
        c.rect(2, y + 3, 13, y + 3, "brown")
        c.ellipse(11, y, 14, y + 3, "tan")
        c.px(12, y + 1, "lbrown")


def _metal_scrap(c: Canvas) -> None:
    c.polygon([(2, 11), (6, 4), (9, 8), (13, 3), (13, 12), (3, 13)], "steel")
    c.line(5, 10, 11, 6, "lsteel")
    c.px(7, 11, "orange")


def _scrap_cloth(c: Canvas) -> None:
    c.polygon([(2, 5), (13, 3), (12, 12), (3, 13)], "pale")
    c.line(3, 7, 12, 6, "lgray")
    c.line(3, 10, 12, 9, "lgray")
    c.px(10, 11, "lbrown")


def _tape(c: Canvas) -> None:
    c.ellipse(2, 2, 13, 13, "lgray")
    c.ellipse(5, 5, 10, 10, "dgray")
    c.ellipse(6, 6, 9, 9, (0, 0, 0, 0))
    c.rect(11, 9, 14, 11, "lgray")


def _nails(c: Canvas) -> None:
    for x in (4, 7, 10):
        c.rect(x, 4, x, 13, "lsteel")
        c.rect(x - 1, 3, x + 1, 3, "steel")


def _concrete_mix(c: Canvas) -> None:
    c.rect(3, 3, 12, 14, "lgray")
    c.rect(3, 3, 12, 4, "pale")
    c.rect(5, 7, 10, 10, "white")
    c.rect(6, 8, 9, 9, "gray")


def _metal_plate(c: Canvas) -> None:
    c.rect(2, 3, 13, 12, "steel")
    c.rect(2, 3, 13, 3, "lsteel")
    for x, y in ((4, 5), (11, 5), (4, 10), (11, 10)):
        c.px(x, y, "lsteel")


def _bone_claw(c: Canvas) -> None:
    c.polygon([(3, 13), (6, 6), (11, 2), (9, 8), (6, 13)], "white")
    c.line(6, 11, 9, 5, "pale")


def _vocal_gland(c: Canvas) -> None:
    c.ellipse(3, 4, 12, 12, "lpurple")
    c.line(5, 8, 10, 6, "purple")
    c.line(6, 10, 10, 10, "purple")


def _acid_sac(c: Canvas) -> None:
    c.ellipse(3, 3, 12, 11, "lime")
    c.ellipse(5, 4, 8, 6, "lgreen")
    c.rect(7, 12, 8, 13, "lime")


def _vial(liquid: str) -> Callable[[Canvas], None]:
    def paint(c: Canvas) -> None:
        c.rect(5, 2, 10, 3, "gray")
        c.rect(6, 4, 9, 13, "lsteel")
        c.rect(6, 8, 9, 13, liquid)
        c.px(7, 5, "white")
    return paint


def _bat(c: Canvas) -> None:
    c.line(3, 13, 12, 3, "lbrown", width=3)
    c.line(3, 13, 5, 11, "dbrown", width=2)
    c.px(12, 3, "tan")


def _knife(c: Canvas) -> None:
    c.line(6, 9, 12, 3, "lsteel", width=2)
    c.line(3, 12, 6, 9, "dbrown", width=2)


def _axe(c: Canvas) -> None:
    c.line(3, 13, 11, 4, "lbrown", width=2)
    c.polygon([(8, 2), (13, 3), (14, 8), (11, 6)], "lsteel")
    c.line(9, 3, 13, 7, "steel")


def _pistol(c: Canvas) -> None:
    c.rect(2, 5, 13, 8, "dgray")
    c.rect(2, 5, 13, 5, "gray")
    c.rect(4, 8, 7, 13, "dgray")
    c.rect(8, 9, 9, 10, "dgray")


def _pistol_ammo(c: Canvas) -> None:
    for x in (3, 7, 11):
        c.rect(x, 6, x + 2, 13, "yellow")
        c.rect(x, 4, x + 2, 6, "orange")
        c.px(x + 1, 3, "orange")


def _bandage(dirty: bool) -> Callable[[Canvas], None]:
    def paint(c: Canvas) -> None:
        c.ellipse(2, 4, 11, 13, "white")
        c.ellipse(5, 7, 8, 10, "pale")
        c.rect(9, 9, 14, 12, "white")
        if dirty:
            c.px(4, 6, "lbrown")
            c.px(10, 11, "brown")
            c.px(6, 11, "dred")
    return paint


def _medkit(c: Canvas) -> None:
    c.rect(2, 4, 13, 13, "white")
    c.rect(6, 2, 9, 4, "pale")
    c.rect(7, 6, 8, 11, "red")
    c.rect(5, 8, 10, 9, "red")


def _food_can(c: Canvas) -> None:
    c.rect(4, 3, 11, 13, "lsteel")
    c.rect(4, 6, 11, 10, "red")
    c.rect(4, 3, 11, 3, "white")
    c.px(7, 8, "yellow")


def _water_bottle(c: Canvas) -> None:
    c.rect(6, 1, 9, 2, "blue")
    c.rect(5, 3, 10, 14, "lblue")
    c.rect(5, 7, 10, 9, "cyan")
    c.rect(6, 4, 6, 12, "white")


ICONS: dict[str, Callable[[Canvas], None]] = {
    "wood": _wood,
    "metal_scrap": _metal_scrap,
    "scrap_cloth": _scrap_cloth,
    "tape": _tape,
    "nails": _nails,
    "concrete_mix": _concrete_mix,
    "metal_plate": _metal_plate,
    "bone_claw": _bone_claw,
    "vocal_gland": _vocal_gland,
    "acid_sac": _acid_sac,
    "sample_brute": _vial("red"),
    "sample_zero": _vial("cyan"),
    "bat": _bat,
    "knife": _knife,
    "axe": _axe,
    "pistol": _pistol,
    "pistol_ammo": _pistol_ammo,
    "bandage_dirty": _bandage(True),
    "bandage": _bandage(False),
    "medkit": _medkit,
    "food_can": _food_can,
    "water_bottle": _water_bottle,
}


def _pile(c: Canvas) -> None:
    c.shadow(3, 11, 13, 15)
    c.ellipse(3, 5, 12, 13, "tan")
    c.rect(6, 3, 9, 5, "lbrown")
    c.rect(6, 5, 9, 5, "dbrown")
    c.px(5, 8, "lbrown")


def build(item_ids: list[str]) -> dict[str, Canvas]:
    out: dict[str, Canvas] = {}
    for iid in item_ids:
        painter = ICONS.get(iid)
        if painter is None:
            raise KeyError(f"artgen: no icon for item '{iid}' (add it to items.ICONS)")
        c = Canvas(16)
        painter(c)
        c.outline("ink")
        out[f"item_{iid}"] = c
    pile = Canvas(16)
    _pile(pile)
    pile.outline("ink")
    out["pile"] = pile
    return out
