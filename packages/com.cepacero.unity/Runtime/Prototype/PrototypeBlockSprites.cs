using System;
using CepaCero.Building;
using CepaCero.World;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Picks the sprite of tools/spritegen (D-021) for a cell of the world: walls join the walls around them, a wall shows its
    /// front when nothing wall-like stands south of it and its top when it continues (seen from above at an angle, D-019),
    /// stairs turn with their rotation, and earth and rock show their cut face where the ground was dug.
    /// A piece the atlas does not have falls back to a flat stand-in (<see cref="PrototypePlaceholders"/>).
    /// </summary>
    public sealed class PrototypeBlockSprites
    {
        // Neighbour masks (spritegen README): top = N 1, E 2, S 4, W 8; front and fence-like = E 1, W 2.
        private const int N = 1, E = 2, S = 4, W = 8;
        private static readonly string[] Facing = { "n", "e", "s", "w" };

        private readonly BlockWorld _blocks;
        private readonly PrototypeArt _art;
        private readonly PrototypePlaceholders _placeholders;

        public PrototypeBlockSprites(BlockWorld blocks, PrototypeArt art)
        {
            _blocks = blocks;
            _art = art;
            _placeholders = new PrototypePlaceholders(art.PixelsPerUnit);
        }

        /// <summary>Sprite of the structure of a cell at a level, or null when there is nothing (open air or a cave).</summary>
        public Sprite Structure(int x, int y, int level)
        {
            BlockType type = _blocks.GetBlock(x, y, level, BuildLayer.Structure);
            if (type.IsAir)
            {
                if (level < 0 && _blocks.IsUndug(x, y, level))
                {
                    return Soil(level <= -3 ? "rock" : "earth", x, y, level);
                }
                return null;
            }
            return StructureOf(type, x, y, level);
        }

        /// <summary>Sprite of a block seen on its own (ghosts, the held piece): no neighbours, a representative shape.</summary>
        public Sprite Representative(BlockType type)
        {
            if (type.IsAir)
            {
                return null;
            }
            BlockBuildInfo b = type.Build;
            if (b == null)
            {
                return Named("block_" + type.Key, type);
            }
            switch (b.Shape)
            {
                case "wall": return Named($"wall_{b.MaterialId}_top_0", type);
                case "halfwall": return Named($"lowbarricade_{b.MaterialId}_3", type);
                case "fence": return Named($"fence_{b.MaterialId}_3", type);
                case "pillar": return Named($"pillar_{b.MaterialId}_face", type);
                case "gate": return Named($"gate_{b.MaterialId}_{(b.IsOpenVariant ? "open" : "closed")}_left", type);
                case "bigwindow": return Named($"bigwindow_{b.MaterialId}_left", type);
                case "stairs": return Named(StairsName(type, 0), type);
                case "door": return Named($"door_{b.MaterialId}_{(b.IsOpenVariant ? "open" : "closed")}", type);
                case "window": return Named($"window_{b.MaterialId}", type);
                case "arch": return Named($"arch_{b.MaterialId}", type);
                case "floor": return Named($"floor_{b.MaterialId}_0", type);
                case "hatch": return Named(HatchName(type), type);
                case "roof": return Named($"roof_{b.MaterialId}_flat", type);
                case "natural": return Named(type.Key == "pit" ? "pit_0" : type.Key == "cave" ? "floor_cave_0" : $"wall_{type.Key}_top_0", type);
                default: return _placeholders.Get(type);
            }
        }

        /// <summary>Sprite of the floor of a cell at a level, or null.</summary>
        public Sprite Floor(int x, int y, int level)
        {
            BlockType type = _blocks.GetBlock(x, y, level, BuildLayer.Floor);
            if (type.IsAir)
            {
                return null;
            }
            BlockBuildInfo b = type.Build;
            if (b == null)
            {
                return Named("block_" + type.Key, type);
            }
            if (b.Shape == "hatch")
            {
                return Named(HatchName(type), type);
            }
            return Named($"floor_{b.MaterialId}_{(x + y) & 1}", type);
        }

        /// <summary>Sprite of the object of a cell at a level, or null.</summary>
        public Sprite Object(int x, int y, int level)
        {
            BlockType type = _blocks.GetBlock(x, y, level, BuildLayer.Object);
            return type.IsAir ? null : Representative(type);
        }

        /// <summary>Roof sprite for its place in the roof (slope, corner, ridge, flat), from the neighbours' roof.</summary>
        public Sprite Roof(BlockType type, RoofShape shape)
        {
            return Named($"roof_{type.Build.MaterialId}_{RoofShapes.SpriteSuffix(shape)}", type);
        }

        /// <summary>
        /// The standing block of a wall-like cell in the isometric view (D-025): its material, a door or a window on the face that runs along
        /// the wall (south when the wall goes east-west, west when it goes north-south), or the low cut-away block.
        /// </summary>
        public Sprite IsoWall(int x, int y, int level, bool low, string facade = null)
        {
            BlockType t = _blocks.GetBlock(x, y, level, BuildLayer.Structure);
            string m = t.Build != null ? t.Build.MaterialId : facade ?? "brick";       // the map's own walls take their building's facade
            if (facade != null && t.Build != null && t.Build.Shape != "wall" && _art.TryGet("iso_wall_" + facade, out _))
            {
                m = facade;                     // its windows and doors too (D-035): a brick window in a plaster wall made stripes
            }
            if (t.IsAir)
            {
                m = level <= -3 ? "rock" : "earth";
            }
            string name = "iso_wall_" + m;
            if (low)
            {
                name += "_low";
            }
            else
            {
                string shape = t.Build != null ? t.Build.Shape : t.Key == "door" || t.Key == "door_open" ? "door" : "wall";
                bool open = t.Build != null ? t.Build.IsOpenVariant : t.Key == "door_open";
                string piece = shape == "door" ? (open ? "dooropen" : "door") : shape == "window" || shape == "bigwindow" ? "window" : null;
                if (piece != null)
                {
                    bool eastWest = IsWallLike(x + 1, y, level) || IsWallLike(x - 1, y, level);
                    name += "_" + piece + "_" + (eastWest ? "s" : "w");
                }
            }
            if (_art.TryGet(name, out Sprite s))
            {
                return s;
            }
            return _art.TryGet("iso_wall_brick" + (low ? "_low" : ""), out s) ? s : null;
        }

        /// <summary>The second storey of a wall (D-026): its material, with a window on every other cell of a plain wall; doors and windows below stay a plain wall above.</summary>
        public Sprite IsoUpperWall(int x, int y, int level, string facade, Sprite below)
        {
            BlockType t = _blocks.GetBlock(x, y, level, BuildLayer.Structure);
            string m = t.Build != null ? t.Build.MaterialId : facade ?? "brick";
            if (facade != null && t.Build != null && t.Build.Shape != "wall")
            {
                m = facade;
            }
            bool eastWest = IsWallLike(x + 1, y, level) || IsWallLike(x - 1, y, level);
            bool window = ((x + y) & 1) == 0 && (t.Build == null || t.Build.Shape == "wall" || t.Build.Shape == "window");
            string face = eastWest ? "s" : "w";
            // The stylized facades have their own upper storey (D-035): a floor line at the foot, a deeper cornice, and balconies here and there.
            if (_art.TryGet($"iso_wall_{m}_up", out Sprite up))
            {
                if (!window)
                {
                    return up;
                }
                bool balcony = (((x + y) >> 1) & 1) == 0;
                return _art.TryGet($"iso_wall_{m}_up_{(balcony ? "balcony" : "window")}_{face}", out Sprite piece) ? piece : up;
            }
            string name = window ? $"iso_wall_{m}_window_{face}" : $"iso_wall_{m}";
            return _art.TryGet(name, out Sprite s) ? s : below;
        }

        /// <summary>The roof piece as a diamond lifted onto the walls (D-025, spritegen iso.roof); the flat one if the atlas is older.</summary>
        public Sprite IsoRoof(BlockType type, int level, string rising)
        {
            string name = rising == null ? $"iso_roof_{type.Build.MaterialId}_f{level}" : $"iso_roof_{type.Build.MaterialId}_s{level}_{rising}";
            return _art.TryGet(name, out Sprite s) ? s : Roof(type, RoofShape.Flat);
        }

        /// <summary>True for blocks that join walls: walls, doors, windows, gates, arches, pillars and the map's own walls.</summary>
        public bool IsWallLike(int x, int y, int level)
        {
            if (!_blocks.InBounds(x, y))
            {
                return false;
            }
            BlockType t = _blocks.GetBlock(x, y, level, BuildLayer.Structure);
            if (t.IsAir)
            {
                return level < 0 && _blocks.IsUndug(x, y, level);
            }
            return IsWallLike(t);
        }

        public static bool IsWallLike(BlockType t)
        {
            if (t.Build == null)
            {
                return t.Key == "building_wall" || t.Key == "door" || t.Key == "door_open" || t.Key == "concrete_wall"
                    || t.Key == "metal_wall" || t.Key == "wood_wall";
            }
            switch (t.Build.Shape)
            {
                case "wall":
                case "door":
                case "window":
                case "bigwindow":
                case "gate":
                case "arch":
                case "pillar":
                    return true;
                case "natural":
                    return t.Key == "earth" || t.Key == "rock";
                default:
                    return false;
            }
        }

        private Sprite StructureOf(BlockType type, int x, int y, int level)
        {
            BlockBuildInfo b = type.Build;
            if (b == null)
            {
                // The map's own blocks (building_wall, tree, door…): a wall shows its brick front when nothing stands south of it.
                if (type.Key == "building_wall" && !IsWallLike(x, y - 1, level))
                {
                    return Named("block_building_wall_face", type);
                }
                return Named("block_" + type.Key, type);
            }

            string m = b.MaterialId;
            bool southWall = IsWallLike(x, y - 1, level);
            switch (b.Shape)
            {
                case "wall":
                    return southWall ? Named($"wall_{m}_top_{TopMask(x, y, level)}", type) : Named($"wall_{m}_face_{FrontMask(x, y, level)}", type);
                case "window":
                case "arch":
                case "door":
                    if (southWall && b.Shape != "door")
                    {
                        return Named($"wall_{m}_top_{TopMask(x, y, level)}", type);   // seen along the wall: just its top
                    }
                    return b.Shape == "door"
                        ? Named($"door_{m}_{(b.IsOpenVariant ? "open" : "closed")}", type)
                        : Named($"{b.Shape}_{m}", type);
                case "pillar":
                    return Named($"pillar_{m}_{(southWall ? "top" : "face")}", type);
                case "halfwall":
                    return Named($"lowbarricade_{m}_{SameShapeMask(x, y, level, type)}", type);
                case "fence":
                    return Named($"fence_{m}_{SameShapeMask(x, y, level, type)}", type);
                case "gate":
                    return Named($"gate_{m}_{(b.IsOpenVariant ? "open" : "closed")}_{Half(x, y, level, type)}", type);
                case "bigwindow":
                    return southWall ? Named($"wall_{m}_top_{TopMask(x, y, level)}", type) : Named($"bigwindow_{m}_{Half(x, y, level, type)}", type);
                case "stairs":
                    return Named(StairsName(type, BlockVariant.Rotation(_blocks.GetVariant(x, y, level, BuildLayer.Structure))), type);
                case "hatch":
                    return Named(HatchName(type), type);
                case "natural":
                    if (type.Key == "pit")
                    {
                        return Named($"pit_{(x * 7 + y * 13) & 1}", type);
                    }
                    return type.Key == "cave" ? Named($"floor_cave_{(x + y) & 1}", type) : Soil(type.Key, x, y, level);
                default:
                    return _placeholders.Get(type);
            }
        }

        /// <summary>Untouched ground or earth/rock walls: the cut face shows toward whatever has been dug out.</summary>
        private Sprite Soil(string kind, int x, int y, int level)
        {
            bool southSolid = IsWallLike(x, y - 1, level);
            string name = southSolid ? $"wall_{kind}_top_{TopMask(x, y, level)}" : $"wall_{kind}_face_{FrontMask(x, y, level)}";
            return _art.TryGet(name, out Sprite s) ? s : _placeholders.Get(_blocks.Registry.Get(kind));
        }

        private int TopMask(int x, int y, int level) =>
            (IsWallLike(x, y + 1, level) ? N : 0) | (IsWallLike(x + 1, y, level) ? E : 0) | (IsWallLike(x, y - 1, level) ? S : 0) | (IsWallLike(x - 1, y, level) ? W : 0);

        private int FrontMask(int x, int y, int level) => (IsWallLike(x + 1, y, level) ? 1 : 0) | (IsWallLike(x - 1, y, level) ? 2 : 0);

        /// <summary>Barricades and fences join their own kind to the east (1) and west (2).</summary>
        private int SameShapeMask(int x, int y, int level, BlockType type)
        {
            string shape = type.Build.Shape;
            return (SameShape(x + 1, y, level, shape) ? 1 : 0) | (SameShape(x - 1, y, level, shape) ? 2 : 0);
        }

        private bool SameShape(int x, int y, int level, string shape)
        {
            if (!_blocks.InBounds(x, y))
            {
                return false;
            }
            BlockBuildInfo b = _blocks.GetBlock(x, y, level, BuildLayer.Structure).Build;
            return b != null && b.Shape == shape;
        }

        /// <summary>A two-cell piece (double gate, big window): the left half when the same piece continues to the east.</summary>
        private string Half(int x, int y, int level, BlockType type)
        {
            if (_blocks.InBounds(x + 1, y) && _blocks.GetBlock(x + 1, y, level, BuildLayer.Structure).Key == type.Key)
            {
                return "left";
            }
            return "right";
        }

        private static string StairsName(BlockType type, int rotation)
        {
            string material = type.Build.MaterialId;
            string dir = Facing[rotation & 3];
            return type.Build.Connects == "down" ? $"stairs_down_{material}_{dir}" : $"stairs_{material}_{dir}";
        }

        private static string HatchName(BlockType type)
        {
            string material = type.Build.MaterialId == "steel" ? "steel" : "wood";
            return $"hatch_{material}_{(type.Build.IsOpenVariant ? "open" : "closed")}";
        }

        private Sprite Named(string name, BlockType fallback) => _art.TryGet(name, out Sprite s) ? s : _placeholders.Get(fallback);
    }
}
