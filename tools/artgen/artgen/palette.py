"""The Cepa Cero palette: 32 opaque colours plus transparency and one soft shadow.

Every opaque pixel the generator draws must be one of these colours (tests check it).
Dark and earthy on purpose: the night has to feel dark, and the mutants' greens,
purples and the blood reds have to stand out against it.
"""

from __future__ import annotations

RGB = tuple[int, int, int]
RGBA = tuple[int, int, int, int]

PALETTE: dict[str, RGB] = {
    # neutrals
    "black": (13, 12, 16),
    "ink": (28, 26, 33),
    "shadow": (43, 40, 50),
    "dgray": (66, 63, 74),
    "gray": (99, 96, 105),
    "lgray": (140, 137, 140),
    "pale": (186, 181, 170),
    "white": (232, 228, 214),
    # earth and wood
    "dbrown": (59, 40, 33),
    "brown": (97, 66, 46),
    "lbrown": (143, 99, 64),
    "tan": (194, 154, 105),
    # skin
    "skin": (225, 170, 130),
    "skin_d": (170, 112, 84),
    # greens
    "dgreen": (30, 58, 40),
    "green": (52, 98, 52),
    "lgreen": (96, 148, 66),
    "lime": (160, 200, 80),
    # mutant skin
    "zskin": (120, 150, 110),
    "zskin_d": (78, 104, 78),
    # blues
    "dblue": (30, 42, 74),
    "blue": (48, 90, 150),
    "lblue": (96, 150, 210),
    "cyan": (120, 210, 220),
    # reds, oranges, yellows
    "dred": (96, 24, 32),
    "red": (170, 40, 44),
    "orange": (220, 120, 40),
    "yellow": (236, 200, 72),
    # purples
    "purple": (110, 60, 130),
    "lpurple": (170, 110, 190),
    # metal
    "steel": (120, 136, 152),
    "lsteel": (176, 190, 200),
}

CLEAR: RGBA = (0, 0, 0, 0)
SOFT_SHADOW: RGBA = (13, 12, 16, 96)


def rgba(name: str) -> RGBA:
    r, g, b = PALETTE[name]
    return (r, g, b, 255)
