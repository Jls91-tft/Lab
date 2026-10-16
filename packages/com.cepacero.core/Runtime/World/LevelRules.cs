using CepaCero.Util;

namespace CepaCero.World
{
    /// <summary>
    /// How levels work for walking players (D-022 phase F): what holds you up, where stairs and trapdoors lead and what falls.
    /// Level 0 is the street; levels above are floors of a building; levels below are dug out of the ground, which is solid
    /// everywhere until somebody digs it (see <see cref="BlockWorld.IsSolidAt"/>).
    /// </summary>
    public static class LevelRules
    {
        public const string Up = "up";
        public const string Down = "down";

        /// <summary>Nothing under your feet means you fall. The street and dug-out cells hold you; upper cells need a floor or a stairwell.</summary>
        public static bool Supports(BlockWorld world, int x, int y, int z)
        {
            if (z <= 0)
            {
                return true;
            }
            return world.GetTypeId(x, y, z, BuildLayer.Floor) != BlockRegistry.AirId || Connects(world, x, y, z - 1, Up);
        }

        /// <summary>True when the player would drop one level: unsupported, a level below exists and its cell is open.</summary>
        public static bool ShouldFall(BlockWorld world, int x, int y, int z) =>
            !Supports(world, x, y, z) && z - 1 >= world.MinLevel && !world.IsSolidAt(x, y, z - 1);

        /// <summary>Stairs at this level lead up; a trapdoor in the floor above does too. The landing must be open.</summary>
        public static bool CanAscend(BlockWorld world, int x, int y, int z) =>
            z + 1 <= world.MaxLevel && !world.IsSolidAt(x, y, z + 1) && (Connects(world, x, y, z, Up) || Connects(world, x, y, z + 1, Down));

        /// <summary>A trapdoor or pit here leads down; stairs just below do too. The landing must be open.</summary>
        public static bool CanDescend(BlockWorld world, int x, int y, int z) =>
            z - 1 >= world.MinLevel && !world.IsSolidAt(x, y, z - 1) && (Connects(world, x, y, z, Down) || Connects(world, x, y, z - 1, Up));

        /// <summary>The level a player at this position reaches by going up or down, or the same level if they cannot.</summary>
        public static int Change(BlockWorld world, Float2 position, int z, bool up)
        {
            Int2 t = position.Tile;
            if (!world.InBounds(t.X, t.Y))
            {
                return z;
            }
            if (up)
            {
                return CanAscend(world, t.X, t.Y, z) ? z + 1 : z;
            }
            return CanDescend(world, t.X, t.Y, z) ? z - 1 : z;
        }

        /// <summary>Upper floors are built outwards from something that already holds: below a solid wall, on a floor, or next to one.</summary>
        public static bool CanBuildAt(BlockWorld world, int x, int y, int z)
        {
            if (!world.InBounds(x, y) || !world.InLevels(z))
            {
                return false;
            }
            if (z <= 0)
            {
                return !world.IsSolidAt(x, y, z) || z == 0;
            }
            if (Supports(world, x, y, z) || world.GetBlock(x, y, z - 1, BuildLayer.Structure).IsSolid)
            {
                return true;
            }
            return Holds(world, x + 1, y, z) || Holds(world, x - 1, y, z) || Holds(world, x, y + 1, z) || Holds(world, x, y - 1, z);
        }

        public const int NotDiggable = 0;

        /// <summary>Dig sideways into untouched ground underground.</summary>
        public const int DigSideways = 1;

        /// <summary>Dig down through a bare street or cave floor, leaving a pit.</summary>
        public const int DigDown = 2;

        /// <summary>What the shovel or pickaxe can do in a cell: nothing, dig sideways into untouched ground, or dig down through a bare floor.</summary>
        public static int DigMode(BlockWorld world, GroundLayer ground, int x, int y, int z)
        {
            if (z > 0 || !world.InBounds(x, y) || !world.InLevels(z))
            {
                return NotDiggable;
            }
            if (world.IsUndug(x, y, z))
            {
                return DigSideways;
            }
            if (z - 1 < world.MinLevel || !world.Registry.TryGet("pit", out _))
            {
                return NotDiggable;
            }
            bool bare = world.GetTypeId(x, y, z, BuildLayer.Structure) == BlockRegistry.AirId && world.GetTypeId(x, y, z, BuildLayer.Object) == BlockRegistry.AirId;
            if (!bare)
            {
                return NotDiggable;
            }
            if (z == 0)
            {
                return world.GetTypeId(x, y, 0, BuildLayer.Floor) == BlockRegistry.AirId && GroundLayer.IsBuildable(ground.Get(x, y)) ? DigDown : NotDiggable;
            }
            return world.GetBlock(x, y, z, BuildLayer.Floor).Build?.Shape == "natural" ? DigDown : NotDiggable;
        }

        private static bool Holds(BlockWorld world, int x, int y, int z) =>
            world.InBounds(x, y) && (world.GetTypeId(x, y, z, BuildLayer.Floor) != BlockRegistry.AirId
                || world.GetTypeId(x, y, z, BuildLayer.Structure) != BlockRegistry.AirId || Connects(world, x, y, z - 1, Up));

        private static bool Connects(BlockWorld world, int x, int y, int z, string direction)
        {
            if (!world.InBounds(x, y) || !world.InLevels(z))
            {
                return false;
            }
            // stairs and pits are structures; trapdoors are part of the floor
            BlockType structure = world.GetBlock(x, y, z, BuildLayer.Structure);
            BlockType floor = world.GetBlock(x, y, z, BuildLayer.Floor);
            return (structure.Build != null && structure.Build.Connects == direction) || (floor.Build != null && floor.Build.Connects == direction);
        }
    }
}
