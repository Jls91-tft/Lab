using CepaCero.World;

namespace CepaCero.AI
{
    /// <summary>
    /// How a kind of walker values the world. Costs are in "tenths of a tile":
    /// walking one normal tile costs 10. A breakable block adds the time it
    /// would take to break it, so zombies go around a wall when the detour is
    /// shorter than breaking through, and smash it otherwise.
    /// </summary>
    public sealed class PathPolicy
    {
        /// <summary>Fixed cost added for any breakable obstacle.</summary>
        public int BreakBaseCost { get; set; } = 40;

        /// <summary>Extra cost per point of block health (wood 100 hp ≈ 50 → +90 total).</summary>
        public float BreakCostPerHealth { get; set; } = 0.5f;

        /// <summary>Whether this walker can break blocks of this material at all.</summary>
        public bool CanBreak(BlockMaterial material) => material != BlockMaterial.Natural && material != BlockMaterial.None;

        public static PathPolicy Zombie => new PathPolicy();

        /// <summary>Cost of entering a cell, or 0 if impassable.</summary>
        public int CellCost(GroundLayer ground, BlockWorld blocks, int x, int y) => CellCost(ground, blocks, x, y, 0);

        /// <summary>
        /// As above on any level (D-022). Untouched ground underground is impassable (zombies do not dig) and so is an upper cell with
        /// nothing under it (they would fall; falling is a portal, not a path).
        /// </summary>
        public int CellCost(GroundLayer ground, BlockWorld blocks, int x, int y, int level)
        {
            if (level != 0)
            {
                if (!blocks.InBounds(x, y) || blocks.IsUndug(x, y, level) || (level > 0 && !LevelRules.Supports(blocks, x, y, level)))
                {
                    return 0;
                }
                BlockType here = blocks.GetBlock(x, y, level, BuildLayer.Structure);
                if (!here.IsSolid)
                {
                    return 10;
                }
                if (here.IsIndestructible || !CanBreak(here.Material))
                {
                    return 0;
                }
                return 10 + BreakBaseCost + (int)(blocks.GetHealth(x, y, level, BuildLayer.Structure) * BreakCostPerHealth);
            }
            int cost = GroundLayer.WalkCost(ground.Get(x, y));
            if (cost == 0)
            {
                return 0;
            }
            BlockType block = blocks.GetBlock(x, y);
            if (!block.IsSolid)
            {
                return cost;
            }
            if (block.IsIndestructible || !CanBreak(block.Material))
            {
                return 0;
            }
            return cost + BreakBaseCost + (int)(blocks.GetHealth(x, y) * BreakCostPerHealth);
        }
    }
}
