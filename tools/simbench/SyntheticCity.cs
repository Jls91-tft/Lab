using CepaCero.World;

namespace CepaCero.SimBench
{
    /// <summary>A Delicias-like grid: 80 m blocks of buildings with party walls, streets, sidewalks and a door each.</summary>
    internal static class SyntheticCity
    {
        public static (BlockWorld, GroundLayer) Build(int width, int height, BlockRegistry registry)
        {
            var ground = new GroundLayer(width, height);
            var blocks = new BlockWorld(width, height, registry);
            ushort wall = registry.Get("building_wall").Id;
            ushort door = registry.Get("door").Id;
            const int block = 40;  // tiles between street axes (80 m at 2 m/tile)
            const int street = 5;  // asphalt + sidewalks
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int lx = x % block;
                    int ly = y % block;
                    bool isStreet = lx < street || ly < street;
                    ground.Set(x, y, isStreet ? (lx < 3 || ly < 3 ? GroundType.Asphalt : GroundType.Sidewalk) : GroundType.Floor);
                }
            }
            // Buildings: 4 per city block, each 8 tiles wide, walls on the outline.
            for (int by = 0; by + block <= height; by += block)
            {
                for (int bx = 0; bx + block <= width; bx += block)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        int x0 = bx + street + k * 8;
                        int y0 = by + street;
                        int x1 = x0 + 8;
                        int y1 = by + block - 1;
                        for (int x = x0; x <= x1; x++)
                        {
                            blocks.SetBlock(x, y0, wall);
                            blocks.SetBlock(x, y1, wall);
                        }
                        for (int y = y0; y <= y1; y++)
                        {
                            blocks.SetBlock(x0, y, wall);
                            blocks.SetBlock(x1, y, wall);
                        }
                        blocks.SetBlock(x0 + 4, y0, door);
                    }
                }
            }
            // Leave a square in the middle so the players have room to build.
            int cx = width / 2;
            int cy = height / 2;
            for (int y = cy - 12; y <= cy + 12; y++)
            {
                for (int x = cx - 12; x <= cx + 12; x++)
                {
                    if (blocks.InBounds(x, y))
                    {
                        blocks.SetBlock(x, y, BlockRegistry.AirId);
                        ground.Set(x, y, GroundType.Sidewalk);
                    }
                }
            }
            return (blocks, ground);
        }
    }
}
