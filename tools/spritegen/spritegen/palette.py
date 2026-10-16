"""The closed palette of the 32 px world (D-021): every opaque pixel of the atlas is one of these colours.

It is designed by hand in families of ramps, not extracted: automatic quantisation ruined the accents
(yellow hard hat, green strain). Violet shadows, warm highlights; the only saturated greens are the mutants'.
Shadow pixels (black, 30 % alpha) keep their colour: they are not part of the palette.
"""

from __future__ import annotations

from PIL import Image

from .gfx import Color, hexc

FAMILIES: dict[str, list[str]] = {
    # cool neutrals: asphalt, metal, concrete, steel, glass frames
    "grey": ["#0e0d16", "#1c1d26", "#2c2e38", "#3e404c", "#565966", "#646876", "#747886", "#848896", "#9498a4", "#a8acb8", "#bcc0c8", "#e2e4e8"],
    # warm stone: concrete, pavers, plaster
    "stone": ["#5a564e", "#7c786e", "#9a968a", "#b6b2a4", "#d0ccbc"],
    # water and wet things
    "teal": ["#1e4a5e", "#2e6a7e", "#4a8e9e", "#7cb8c0", "#b8dce0"],
    # wood, dirt, leather, rope
    "brown": ["#281a16", "#42281e", "#603c28", "#865a38", "#aa7c4c", "#cc9e66", "#e8c690"],
    # brick, rust, terracotta
    "brick": ["#46222a", "#682e34", "#8a4a40", "#ae6a52", "#cc8a68"],
    # human skin and hands
    "skin": ["#6e4642", "#a06a58", "#cc9a7c", "#e8bea0", "#f4d6bc"],
    # sickly zombie skin
    "zskin": ["#3c483c", "#54664e", "#6e8662", "#92a078", "#b4bc98"],
    # foliage and grass
    "green": ["#1c3024", "#28462e", "#3a6236", "#54803e", "#76a454", "#a0c872"],
    # water, denim, jackets, glass
    "blue": ["#18263e", "#28406a", "#3e648e", "#5e8cb0", "#98c2d8", "#cde6f0"],
    # the screamer
    "purple": ["#30263e", "#4c3c62", "#6e5884", "#9680a4"],
    # hard hats, vests, fire, lamps, tape
    "orange": ["#7a421e", "#b0622a", "#e2822c", "#f4b040", "#fad66e", "#fff0b4"],
    # blood and wounds
    "red": ["#4a1018", "#6c1a20", "#922a2c", "#b8423a", "#d86c5c"],
    # bone, teeth, paper
    "bone": ["#a89c84", "#cfc4a8", "#ece4cc"],
    # the strain: the only vivid greens
    "glow": ["#326e32", "#60be38", "#96eb50", "#d6ff8c"],
    # olive cloth, packs
    "olive": ["#2c3426", "#465238", "#687850"],
}

PALETTE: list[Color] = [hexc(c) for fam in FAMILIES.values() for c in fam]


def _dist(a: Color, b: Color) -> float:
    """Redmean colour distance: close to what the eye sees, cheap to compute."""
    rm = (a[0] + b[0]) / 2
    dr, dg, db = a[0] - b[0], a[1] - b[1], a[2] - b[2]
    return (2 + rm / 256) * dr * dr + 4 * dg * dg + (2 + (255 - rm) / 256) * db * db


_cache: dict[Color, Color] = {}


def nearest(c: Color) -> Color:
    hit = _cache.get(c)
    if hit is None:
        hit = min(PALETTE, key=lambda p: _dist(c, p))
        _cache[c] = hit
    return hit


def apply(img: Image.Image) -> Image.Image:
    """Maps every fully opaque pixel to its nearest palette colour; translucent shadow pixels stay as they are."""
    out = img.convert("RGBA")
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a == 255:
                px[x, y] = nearest((r, g, b)) + (255,)
    return out
