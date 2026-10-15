"""Interface pieces: a 9-slice panel, inventory slots, small 9x9 status icons and a
white pixel that the game tints for bars. Names start with ui_ or icon_."""

from __future__ import annotations

from .canvas import Canvas

PANEL_BORDER = 4  # 9-slice border in pixels (the runtime uses the same number)


def _panel() -> Canvas:
    c = Canvas(12)
    c.fill("ink")
    c.rect(0, 0, 11, 0, "dgray")
    c.rect(0, 0, 0, 11, "dgray")
    c.rect(0, 11, 11, 11, "black")
    c.rect(11, 0, 11, 11, "black")
    c.rect(1, 1, 10, 1, "shadow")
    return c


def _slot(border: str, inner: str) -> Canvas:
    c = Canvas(20)
    c.fill(border)
    c.rect(1, 1, 18, 18, inner)
    c.rect(1, 1, 18, 1, "black")
    c.rect(1, 1, 1, 18, "black")
    return c


def _cursor() -> Canvas:
    """Corner brackets around the targeted tile; the game tints it (in reach or not)."""
    c = Canvas(16)
    for x0, y0, dx, dy in ((0, 0, 1, 1), (15, 0, -1, 1), (0, 15, 1, -1), (15, 15, -1, -1)):
        for i in range(4):
            c.px(x0 + dx * i, y0, "white")
            c.px(x0, y0 + dy * i, "white")
    return c


def _icon_health() -> Canvas:
    c = Canvas(9)
    c.rect(1, 1, 3, 2, "red")
    c.rect(5, 1, 7, 2, "red")
    c.rect(0, 2, 8, 4, "red")
    c.rect(1, 5, 7, 5, "red")
    c.rect(2, 6, 6, 6, "red")
    c.rect(3, 7, 5, 7, "red")
    c.px(4, 8, "red")
    c.px(2, 2, "white")
    return c


def _icon_stamina() -> Canvas:
    c = Canvas(9)
    c.polygon([(5, 0), (1, 5), (4, 5), (3, 8), (7, 3), (4, 3)], "yellow")
    return c


def _icon_food() -> Canvas:
    c = Canvas(9)
    c.ellipse(1, 0, 6, 5, "lbrown")
    c.ellipse(2, 1, 4, 3, "tan")
    c.line(5, 5, 7, 7, "white", width=2)
    return c


def _icon_water() -> Canvas:
    c = Canvas(9)
    c.polygon([(4, 0), (1, 5), (7, 5)], "lblue")
    c.ellipse(1, 3, 7, 8, "lblue")
    c.px(3, 5, "white")
    return c


def _icon_day() -> Canvas:
    c = Canvas(9)
    c.ellipse(2, 2, 6, 6, "yellow")
    for x, y in ((4, 0), (4, 8), (0, 4), (8, 4), (1, 1), (7, 1), (1, 7), (7, 7)):
        c.px(x, y, "yellow")
    return c


def _icon_dusk() -> Canvas:
    c = Canvas(9)
    c.ellipse(1, 2, 7, 8, "orange")
    c.rect(0, 6, 8, 8, (0, 0, 0, 0))
    c.rect(0, 6, 8, 6, "red")
    return c


def _icon_night() -> Canvas:
    c = Canvas(9)
    c.ellipse(1, 1, 7, 7, "pale")
    c.ellipse(3, 0, 8, 5, (0, 0, 0, 0))
    return c


def _icon_zombie() -> Canvas:
    c = Canvas(9)
    c.rect(1, 1, 7, 6, "zskin")
    c.rect(1, 1, 7, 2, "dgreen")
    c.px(2, 4, "red")
    c.px(6, 4, "red")
    c.rect(3, 6, 5, 7, "zskin_d")
    return c


def _icon_skull() -> Canvas:
    c = Canvas(9)
    c.ellipse(1, 0, 7, 6, "white")
    c.rect(2, 6, 6, 7, "white")
    c.px(3, 3, "ink")
    c.px(5, 3, "ink")
    c.px(4, 5, "ink")
    return c


def build() -> dict[str, Canvas]:
    white = Canvas(2)
    white.fill((255, 255, 255, 255))
    out = {
        "ui_panel": _panel(),
        "ui_slot": _slot("dgray", "shadow"),
        "ui_slot_selected": _slot("yellow", "dgray"),
        "ui_white": white,
        "ui_cursor": _cursor(),
        "icon_health": _icon_health(),
        "icon_stamina": _icon_stamina(),
        "icon_food": _icon_food(),
        "icon_water": _icon_water(),
        "icon_day": _icon_day(),
        "icon_dusk": _icon_dusk(),
        "icon_night": _icon_night(),
        "icon_zombie": _icon_zombie(),
        "icon_skull": _icon_skull(),
    }
    for name in [n for n in out if n.startswith("icon_")]:
        padded = Canvas(11)
        padded.image.paste(out[name].image, (1, 1))
        padded.outline("black")
        out[name] = padded
    return out
