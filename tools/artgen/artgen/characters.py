"""Players and zombies: 3 facings (down, up, left; right is left mirrored at runtime)
x 4 walk frames (0 and 2 stand, 1 and 3 step). Seen from above at a slight angle.

Names: <who>_<facing>_<frame>, e.g. player_down_1, zombie_walker_left_3,
plus player_other_*, player_downed and zombie_<id> sizes from the bestiary.
"""

from __future__ import annotations

from dataclasses import dataclass, replace

from .canvas import Canvas

FACINGS = ("down", "up", "left")
FRAMES = 4


@dataclass(frozen=True)
class Look:
    skin: str = "skin"
    skin_dark: str = "skin_d"
    shirt: str = "blue"
    shirt_dark: str = "dblue"
    pants: str = "dgray"
    hair: str | None = "dbrown"
    eyes: str = "ink"
    zombie: bool = False
    hat: str | None = None
    plates: str | None = None
    mouth: str | None = None
    wide: bool = False
    stains: str | None = None


PLAYER = Look()
PLAYER_OTHER = Look(shirt="orange", shirt_dark="brown", hair="tan")
PLAYER_DOWNED = Look(shirt="gray", shirt_dark="dgray", hair="dbrown")

_ZOMBIE = Look(skin="zskin", skin_dark="zskin_d", shirt="brown", shirt_dark="dbrown", pants="dblue",
               hair="dgreen", eyes="red", zombie=True, stains="dred")

ZOMBIES: dict[str, Look] = {
    "walker": _ZOMBIE,
    "runner": replace(_ZOMBIE, shirt="dred", shirt_dark="dbrown", hair="black", eyes="yellow"),
    "screamer": replace(_ZOMBIE, skin="pale", skin_dark="lgray", shirt="purple", shirt_dark="ink",
                        hair="ink", eyes="lpurple", mouth="ink"),
    "spitter": replace(_ZOMBIE, skin="lgreen", skin_dark="green", shirt="green", shirt_dark="dgreen",
                       hair=None, eyes="lime", mouth="lime", wide=True, stains="lime"),
    "armored": replace(_ZOMBIE, shirt="steel", shirt_dark="gray", hair=None, hat="steel", plates="lsteel"),
    "sapper": replace(_ZOMBIE, shirt="orange", shirt_dark="lbrown", hair=None, hat="yellow"),
    "brute": replace(_ZOMBIE, skin="zskin_d", skin_dark="dgreen", shirt="dred", shirt_dark="dbrown",
                     hair=None, eyes="yellow", wide=True),
    "patient_zero": replace(_ZOMBIE, skin="white", skin_dark="pale", shirt="pale", shirt_dark="lgray",
                            pants="pale", hair=None, eyes="cyan", stains="red"),
}

# Drawn bigger than one tile (bestiary size_tiles 2.0 for La Mole).
BIG = {"brute": 2}


def humanoid(look: Look, facing: str, frame: int) -> Canvas:
    c = Canvas(16)
    step = {1: 1, 3: -1}.get(frame, 0)  # +1: left leg forward, -1: right leg forward
    left, right = (4, 11) if not look.wide else (3, 12)

    c.shadow(4, 12, 11, 15)

    # legs
    if facing == "left":
        c.rect(7 - step, 11, 8 - step, 13, look.pants)
        c.rect(7 + step, 11, 8 + step, 13, look.pants)
        c.rect(7 + step, 13, 8 + step, 13, "ink")
    else:
        c.rect(6, 11 - max(step, 0), 7, 13 - max(step, 0), look.pants)
        c.rect(8, 11 - max(-step, 0), 9, 13 - max(-step, 0), look.pants)

    # torso
    c.rect(left + 1, 7, right - 1, 11, look.shirt)
    c.rect(right - 1, 7, right - 1, 11, look.shirt_dark)
    c.rect(left + 1, 11, right - 1, 11, look.shirt_dark)
    if look.plates:
        c.rect(left + 2, 8, right - 2, 8, look.plates)
        c.rect(left + 2, 10, right - 2, 10, look.plates)
    if look.stains:
        c.px(left + 2, 9, look.stains)
        c.px(right - 2, 10, look.stains)

    # arms: the living swing them, zombies reach forward
    if look.zombie:
        if facing == "down":
            c.rect(left, 8, left, 12, look.skin)
            c.rect(right, 8, right, 12, look.skin_dark)
        elif facing == "up":
            c.rect(left, 3, left, 8, look.skin)
            c.rect(right, 3, right, 8, look.skin_dark)
        else:
            c.rect(2, 8, 6, 8, look.skin)
            c.rect(2, 9, 5, 9, look.skin_dark)
    else:
        if facing == "left":
            c.rect(7 + step, 8, 8 + step, 10, look.shirt_dark)
            c.px(7 + step, 11, look.skin)
        else:
            c.rect(left, 7 - step, left, 10 - step, look.shirt)
            c.px(left, 11 - step, look.skin)
            c.rect(right, 7 + step, right, 10 + step, look.shirt_dark)
            c.px(right, 11 + step, look.skin)

    # head
    if facing == "left":
        c.rect(5, 2, 9, 7, look.skin)
        c.rect(9, 3, 9, 7, look.skin_dark)
        if look.hair:
            c.rect(6, 2, 9, 3, look.hair)
            c.rect(8, 4, 9, 5, look.hair)
        c.px(6, 5, look.eyes)
        if look.mouth:
            c.px(5, 6, look.mouth)
    else:
        c.rect(5, 2, 10, 7, look.skin)
        c.rect(10, 3, 10, 7, look.skin_dark)
        if facing == "up":
            if look.hair:
                c.rect(5, 2, 10, 6, look.hair)
        else:
            if look.hair:
                c.rect(5, 2, 10, 3, look.hair)
                c.px(5, 4, look.hair)
            c.px(6, 5, look.eyes)
            c.px(9, 5, look.eyes)
            if look.mouth:
                c.rect(7, 6, 8, 7, look.mouth)
    if look.hat:
        c.rect(5, 1, 10, 3, look.hat)
        c.rect(5, 3, 10, 3, "ink" if look.hat != "yellow" else "orange")

    c.outline("ink")
    return c


def build(bestiary_ids: list[str]) -> dict[str, Canvas]:
    out: dict[str, Canvas] = {}
    for facing in FACINGS:
        for frame in range(FRAMES):
            out[f"player_{facing}_{frame}"] = humanoid(PLAYER, facing, frame)
            out[f"player_other_{facing}_{frame}"] = humanoid(PLAYER_OTHER, facing, frame)
    out["player_downed"] = humanoid(PLAYER_DOWNED, "left", 0).rotated(1)

    for zid in bestiary_ids:
        look = ZOMBIES.get(zid)
        if look is None:
            raise KeyError(f"artgen: no look for zombie '{zid}' (add it to characters.ZOMBIES)")
        for facing in FACINGS:
            for frame in range(FRAMES):
                c = humanoid(look, facing, frame)
                if zid in BIG:
                    c = c.scaled(BIG[zid])
                out[f"zombie_{zid}_{facing}_{frame}"] = c
    return out
