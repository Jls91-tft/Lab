using System.Collections.Generic;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Tests.TestSupport
{
    /// <summary>
    /// Builds small worlds from text so tests read like the scenario they check.
    /// First line = north (highest y). Legend:
    /// '.' asphalt, ',' grass, '~' water, '#' wood wall, 'M' metal wall,
    /// 'C' concrete wall, 'T' tree, 'D' closed door, 'P' target (player), 'Z' marker.
    /// </summary>
    public sealed class AsciiMap
    {
        public AsciiMap(params string[] rows)
        {
            Registry = BlockRegistry.CreateDefault();
            int h = rows.Length;
            int w = rows[0].Length;
            Ground = new GroundLayer(w, h);
            Blocks = new BlockWorld(w, h, Registry);
            for (int row = 0; row < h; row++)
            {
                int y = h - 1 - row;
                for (int x = 0; x < w; x++)
                {
                    char c = rows[row][x];
                    Ground.Set(x, y, c == '~' ? GroundType.Water : c == ',' ? GroundType.Grass : GroundType.Asphalt);
                    string key = c switch
                    {
                        '#' => "wood_wall",
                        'M' => "metal_wall",
                        'C' => "concrete_wall",
                        'T' => "tree",
                        'D' => "door",
                        _ => null,
                    };
                    if (key != null)
                    {
                        Blocks.SetBlock(x, y, Registry.Get(key).Id);
                    }
                    if (c == 'P')
                    {
                        Targets.Add(new Int2(x, y));
                    }
                    if (c == 'Z')
                    {
                        Markers.Add(new Int2(x, y));
                    }
                }
            }
            Blocks.DrainChanges(new List<BlockChange>());
        }

        public BlockRegistry Registry { get; }

        public GroundLayer Ground { get; }

        public BlockWorld Blocks { get; }

        public List<Int2> Targets { get; } = new List<Int2>();

        public List<Int2> Markers { get; } = new List<Int2>();

        public int Width => Ground.Width;

        public int Height => Ground.Height;
    }
}
