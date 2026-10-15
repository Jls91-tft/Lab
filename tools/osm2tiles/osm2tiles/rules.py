"""OSM tag → tile classification rules.

Ids are part of the `cepacero.map` v1 format: append new entries at the end,
never reorder (Unity maps ids to tiles by index).
"""

from __future__ import annotations

# ---- Legends (index = id) --------------------------------------------------
GROUND = ["dirt", "asphalt", "sidewalk", "grass", "water", "floor", "rail"]
BLOCKS = ["none", "building_wall", "tree", "door", "fence"]
BUILDING_KINDS = [
    "generic", "residential", "commercial", "food", "hardware", "medical",
    "police", "school", "religious", "office", "industrial", "garage",
]

G = {name: i for i, name in enumerate(GROUND)}
B = {name: i for i, name in enumerate(BLOCKS)}

# Ground types a door may open onto (from outside).
DOOR_OUTSIDE = {G["sidewalk"], G["asphalt"], G["grass"], G["dirt"]}

# ---- Roads -----------------------------------------------------------------
# highway value → (carriageway width m, ground id). Drivable roads also get a
# sidewalk border of SIDEWALK_M on each side.
ROADS: dict[str, tuple[float, str]] = {
    "motorway": (16, "asphalt"), "trunk": (14, "asphalt"),
    "primary": (12, "asphalt"), "secondary": (10, "asphalt"),
    "tertiary": (8, "asphalt"), "unclassified": (6, "asphalt"),
    "residential": (6, "asphalt"), "living_street": (5, "asphalt"),
    "service": (4, "asphalt"),
    "motorway_link": (6, "asphalt"), "trunk_link": (6, "asphalt"),
    "primary_link": (6, "asphalt"), "secondary_link": (6, "asphalt"),
    "tertiary_link": (6, "asphalt"),
    "pedestrian": (6, "sidewalk"), "footway": (2, "sidewalk"),
    "path": (2, "sidewalk"), "cycleway": (2, "sidewalk"),
    "steps": (2, "sidewalk"), "track": (3, "dirt"),
}
SIDEWALK_M = 2.0

# ---- Water lines -----------------------------------------------------------
WATERWAYS = {"river": 30.0, "canal": 10.0, "stream": 3.0, "drain": 1.5, "ditch": 1.5}

RAILWAYS = {"rail": 4.0, "light_rail": 3.0, "narrow_gauge": 3.0}  # tram runs on streets: skipped


def area_class(tags: dict[str, str]) -> str | None:
    """Classify a closed way / multipolygon. Returns a ground name, 'building',
    'wood' (grass + scattered trees) or None."""
    if tags.get("building", "no") != "no":
        return "building"
    natural, landuse, leisure = tags.get("natural"), tags.get("landuse"), tags.get("leisure")
    if natural == "water" or tags.get("waterway") == "riverbank" or landuse in ("reservoir", "basin"):
        return "water"
    if natural == "wood" or landuse == "forest":
        return "wood"
    if (leisure in ("park", "garden", "playground", "pitch", "dog_park", "common", "golf_course")
            or landuse in ("grass", "meadow", "village_green", "recreation_ground", "orchard", "vineyard",
                           "allotments", "cemetery")
            or natural in ("grassland", "scrub", "heath")):
        return "grass"
    if tags.get("place") == "square" or (tags.get("highway") in ("pedestrian", "footway") and tags.get("area") == "yes"):
        return "sidewalk"
    if tags.get("amenity") == "parking" and tags.get("parking", "surface") == "surface":
        return "asphalt"
    return None


# Paint order for areas (later wins).
AREA_ORDER = {"grass": 0, "wood": 0, "water": 1, "asphalt": 2, "sidewalk": 2}

# ---- Building kinds (for loot tables). Names and brands are never kept. ----
_BUILDING_TAG = {
    "hospital": "medical", "school": "school", "university": "school", "college": "school",
    "kindergarten": "school", "church": "religious", "chapel": "religious",
    "cathedral": "religious", "mosque": "religious", "temple": "religious",
    "synagogue": "religious", "commercial": "commercial", "retail": "commercial",
    "supermarket": "food", "kiosk": "commercial", "industrial": "industrial",
    "warehouse": "industrial", "factory": "industrial", "office": "office",
    "residential": "residential", "apartments": "residential", "house": "residential",
    "detached": "residential", "terrace": "residential", "dormitory": "residential",
    "garage": "garage", "garages": "garage", "parking": "garage",
}
_AMENITY = {
    "hospital": "medical", "clinic": "medical", "doctors": "medical", "pharmacy": "medical",
    "dentist": "medical", "police": "police", "school": "school", "kindergarten": "school",
    "college": "school", "university": "school", "restaurant": "food", "cafe": "food",
    "bar": "food", "pub": "food", "fast_food": "food", "place_of_worship": "religious",
    "fuel": "commercial", "bank": "office", "townhall": "office",
}
_FOOD_SHOPS = {"supermarket", "convenience", "greengrocer", "butcher", "bakery", "deli", "frozen_food"}
_HARDWARE_SHOPS = {"hardware", "doityourself", "trade"}

# Higher wins when several hints apply to one building.
KIND_PRIORITY = {
    "generic": 0, "residential": 1, "garage": 2, "office": 3, "industrial": 3,
    "religious": 4, "commercial": 5, "food": 6, "hardware": 6, "school": 7,
    "police": 8, "medical": 9,
}


def building_kind(tags: dict[str, str]) -> str | None:
    """Kind hinted by a set of tags (a building outline or a POI node)."""
    if "healthcare" in tags:
        return "medical"
    if tags.get("amenity") in _AMENITY:
        return _AMENITY[tags["amenity"]]
    shop = tags.get("shop")
    if shop:
        if shop in _FOOD_SHOPS:
            return "food"
        if shop in _HARDWARE_SHOPS:
            return "hardware"
        return "commercial"
    if tags.get("office"):
        return "office"
    return _BUILDING_TAG.get(tags.get("building", ""))


def better_kind(current: str, candidate: str | None) -> str:
    if candidate and KIND_PRIORITY[candidate] > KIND_PRIORITY[current]:
        return candidate
    return current
